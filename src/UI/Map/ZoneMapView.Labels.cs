using Godot;
using Planalto.Core.Territory;
using Planalto.UI.Theme;

namespace Planalto.UI.Map;

/// <summary>
/// Nomes das zonas com desvio de colisão: nenhum nome cobre outro nome, um marcador ou os controles sobre o mapa; o
/// nome que não cabe no lugar é deslocado e ganha uma linha-guia, e o nome longo demais é abreviado com reticências
/// (o inteiro fica no tooltip da zona).
/// </summary>
public partial class ZoneMapView
{
    /// <summary>Nome de uma zona no mapa: o texto desenhado, o nome inteiro, a caixa, o ponto da zona e se há linha-guia.</summary>
    public sealed record LabelBox(string ZoneId, string Text, string FullName, Rect2 Rect, Vector2 Anchor, bool Leader)
    {
        public bool Abbreviated => Text != FullName;
    }

    private const int LabelFontSize = 18;
    private const float LabelPad = 3;

    /// <summary>No máximo 12 nomes de zona visíveis (ART_GUIDE, densidade); os demais aparecem ao aproximar.</summary>
    public const int MaxLabels = 12;

    /// <summary>Largura máxima de um nome: o de 22 caracteres do ART_GUIDE cabe; o mais longo ganha reticências.</summary>
    private const float MaxLabelWidth = 230;

    /// <summary>
    /// O nome fica logo acima do ponto da zona (como a linha de base de antes, 7 px abaixo do ponto), deixando livre a
    /// fila de marcadores, que começa 16 px abaixo do ponto.
    /// </summary>
    private static readonly Vector2 LabelLift = new(0, -2);

    private List<LabelBox> _labels = [];

    /// <summary>Retângulos sobre o mapa (seletor de modo, legenda), nas coordenadas do mapa: os nomes desviam deles.</summary>
    public Func<IEnumerable<Rect2>>? Obstacles { get; set; }

    /// <summary>Nomes desenhados no último quadro.</summary>
    public IReadOnlyList<LabelBox> Labels => _labels;

    private float XScale() => (float)Math.Cos(_zonas.Values.Average(v => v.Lat) * Math.PI / 180);

    private Vector2 AnchorOf(Zona z, Transform2D t, float xScale) => t * new Vector2((float)z.Lon * xScale, -(float)z.Lat);

    /// <summary>
    /// Caixas dos marcadores de cada zona, na tela. Mesma conta de <see cref="DrawMarkers"/>: fila centrada 30 px
    /// abaixo do ponto da zona; mais de três viram um disco.
    /// </summary>
    public IReadOnlyList<Rect2> MarkerBoxes()
    {
        const float Height = 28;
        const float Gap = 4;
        if (_zonas.Count == 0)
        {
            return [];
        }

        Transform2D t = ToScreen();
        Font font = Palette.Weight(Palette.Inter, 500);
        float xScale = XScale();
        var result = new List<Rect2>();
        foreach (var group in _markers.GroupBy(m => m.ZoneId))
        {
            Vector2 anchor = AnchorOf(_zonas[group.Key], t, xScale) + new Vector2(0, 30);
            List<Marker> list = group.ToList();
            if (list.Count > MaxMarkersPerZone)
            {
                result.Add(new Rect2(anchor - new Vector2(17, 17), new Vector2(34, 34)));
                continue;
            }

            float width = list.Sum(m => m.Text is null ? Height : font.GetStringSize(m.Text, HorizontalAlignment.Left, -1, LabelFontSize).X + 14) + (Gap * (list.Count - 1));
            result.Add(new Rect2(anchor.X - (width / 2) - 1, anchor.Y - (Height / 2) - 1, width + 2, Height + 2));
        }

        return result;
    }

    /// <summary>
    /// Nomes das zonas visíveis, da mais populosa para a menos, até <see cref="MaxLabels"/>: cada nome fica no ponto da
    /// zona ou, se ali cobriria outro nome, um marcador ou um controle, no lugar livre mais próximo, com linha-guia.
    /// Nome sem lugar livre não é desenhado (continua no tooltip da zona).
    /// </summary>
    public IReadOnlyList<LabelBox> LabelBoxes()
    {
        var placed = new List<LabelBox>();
        if (_shapes.Count == 0 || Size.X <= 0 || Size.Y <= 0)
        {
            return placed;
        }

        Transform2D t = ToScreen();
        Font font = Palette.Weight(Palette.Inter, 500);
        float xScale = XScale();
        var area = new Rect2(Vector2.Zero, Size).Grow(-4);
        List<Rect2> controls = Obstacles?.Invoke().ToList() ?? [];
        List<Rect2> blocked = MarkerBoxes().Concat(controls).ToList();
        float line = font.GetHeight(LabelFontSize) + (2 * LabelPad);
        foreach (Zona z in _zonas.Values.OrderByDescending(z => z.Populacao).ThenBy(z => z.Id, StringComparer.Ordinal))
        {
            if (placed.Count >= MaxLabels)
            {
                break;
            }

            // Zona cujo ponto está fora do mapa ou sob a legenda: o nome ficaria preso a uma linha que sai de baixo dela.
            Vector2 anchor = AnchorOf(z, t, xScale);
            if (!area.HasPoint(anchor) || controls.Any(c => c.HasPoint(anchor)))
            {
                continue;
            }

            string text = Fit(font, z.Nome);
            Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, -1, LabelFontSize) + new Vector2(2 * LabelPad, 2 * LabelPad);
            foreach (Vector2 offset in Offsets(size, line))
            {
                var rect = new Rect2(anchor + LabelLift + offset - (size / 2), size);
                if (!area.Encloses(rect) || blocked.Any(b => b.Intersects(rect)) || placed.Any(p => p.Rect.Grow(2).Intersects(rect)))
                {
                    continue;
                }

                // Deslocado: a linha-guia não pode passar por cima de outro nome.
                bool leader = offset != Vector2.Zero;
                if (leader && placed.Any(p => SegmentCrosses(anchor, rect.GetCenter(), p.Rect)))
                {
                    continue;
                }

                placed.Add(new LabelBox(z.Id, text, z.Nome, rect, anchor, leader));
                break;
            }
        }

        return placed;
    }

    /// <summary>Zona do nome sob o ponto da tela (o nome deslocado pode estar sobre outra zona).</summary>
    private string? LabelAt(Vector2 screen) => _labels.LastOrDefault(l => l.Rect.HasPoint(screen))?.ZoneId;

    /// <summary>O nome inteiro, ou abreviado com reticências até caber em <see cref="MaxLabelWidth"/>.</summary>
    private static string Fit(Font font, string name)
    {
        float Width(string s) => font.GetStringSize(s, HorizontalAlignment.Left, -1, LabelFontSize).X;
        if (Width(name) <= MaxLabelWidth)
        {
            return name;
        }

        for (int n = name.Length - 1; n > 1; n--)
        {
            string cut = name[..n].TrimEnd(' ', ',', '(', '-') + "…";
            if (Width(cut) <= MaxLabelWidth)
            {
                return cut;
            }
        }

        return name[..1] + "…";
    }

    /// <summary>Lugares candidatos para um nome, do mais perto para o mais longe do ponto da zona.</summary>
    private static IEnumerable<Vector2> Offsets(Vector2 size, float line)
    {
        yield return Vector2.Zero;
        float side = (size.X / 2) + 14;
        for (int ring = 1; ring <= 6; ring++)
        {
            float dy = ring * (line + 2);
            yield return new Vector2(0, -dy);
            yield return new Vector2(side, -dy + (line / 2));
            yield return new Vector2(-side, -dy + (line / 2));
            yield return new Vector2(side * ring, 0);
            yield return new Vector2(-side * ring, 0);
            yield return new Vector2(side, dy + line);
            yield return new Vector2(-side, dy + line);
            yield return new Vector2(0, dy + (2 * line));
        }
    }

    private static bool SegmentCrosses(Vector2 from, Vector2 to, Rect2 rect)
    {
        const int Steps = 12;
        for (int i = 1; i < Steps; i++)
        {
            if (rect.HasPoint(from.Lerp(to, i / (float)Steps)))
            {
                return true;
            }
        }

        return false;
    }

    private void DrawLabels(Font font)
    {
        _labels = LabelBoxes().ToList();
        float ascent = font.GetAscent(LabelFontSize);
        foreach (LabelBox label in _labels)
        {
            if (label.Leader)
            {
                // Linha-guia do ponto da zona até a borda mais próxima da caixa do nome.
                var edge = new Vector2(Math.Clamp(label.Anchor.X, label.Rect.Position.X, label.Rect.End.X), Math.Clamp(label.Anchor.Y, label.Rect.Position.Y, label.Rect.End.Y));
                DrawLine(label.Anchor, edge, new Color(0, 0, 0, 0.6f), 3, antialiased: true);
                DrawLine(label.Anchor, edge, Palette.Texto, 1.5f, antialiased: true);
                DrawCircle(label.Anchor, 3.5f, new Color(0, 0, 0, 0.6f));
                DrawCircle(label.Anchor, 2.5f, Palette.Texto);
                DrawRect(label.Rect, new Color(Palette.Fundo, 0.72f));
            }

            Vector2 at = label.Rect.Position + new Vector2(LabelPad, LabelPad + ascent);
            DrawString(font, at + new Vector2(-1, 1), label.Text, HorizontalAlignment.Left, -1, LabelFontSize, new Color(0, 0, 0, 0.7f));
            DrawString(font, at, label.Text, HorizontalAlignment.Left, -1, LabelFontSize, Palette.Texto);
        }
    }
}
