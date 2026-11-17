using Godot;
using Planalto.Core.Territory;
using Planalto.UI.Theme;

namespace Planalto.UI.Map;

/// <summary>
/// Crédito dos nomes de bairro do OpenStreetMap (D-058, licença ODbL): discreto, no canto inferior direito do mapa, só
/// quando o município tem zona com nome de lá (o Core decide; aqui só se desenha). Os nomes das zonas desviam dele.
/// </summary>
public partial class ZoneMapView
{
    private const int CreditFontSize = 18;
    private const float CreditMargin = 8;
    private const float CreditPad = 4;

    /// <summary>Texto do crédito do mapa da cidade, ou null se nenhuma zona do município tem nome do OpenStreetMap.</summary>
    public string? Credit { get; private set; }

    private void LoadCredit(ZonasMunicipio municipio) => Credit = ZoneNameOrigin.Credito(municipio) is { } text ? Tr(text) : null;

    /// <summary>Caixa do crédito na tela, acima de qualquer controle que cubra o canto; null sem crédito.</summary>
    public Rect2? CreditRect()
    {
        if (Credit is null || Size.X <= 0 || Size.Y <= 0)
        {
            return null;
        }

        Font font = Palette.Inter;
        Vector2 size = font.GetStringSize(Credit, HorizontalAlignment.Left, -1, CreditFontSize) + new Vector2(2 * CreditPad, 2 * CreditPad);
        var rect = new Rect2(Size - size - new Vector2(CreditMargin, CreditMargin), size);
        List<Rect2> controls = Obstacles?.Invoke().ToList() ?? [];
        for (int i = 0; i < controls.Count && controls.FirstOrDefault(c => c.Intersects(rect)) is { Size.X: > 0 } over; i++)
        {
            rect.Position = new Vector2(rect.Position.X, over.Position.Y - size.Y - CreditMargin);
        }

        return rect;
    }

    private void DrawCredit()
    {
        if (CreditRect() is not { } rect)
        {
            return;
        }

        Font font = Palette.Inter;
        DrawRect(rect, new Color(Palette.Fundo, 0.72f));
        DrawString(font, rect.Position + new Vector2(CreditPad, CreditPad + font.GetAscent(CreditFontSize)), Credit!, HorizontalAlignment.Left, -1, CreditFontSize, Palette.Texto2);
    }
}
