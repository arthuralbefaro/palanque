using Godot;
using Planalto.Core.Territory;

namespace Planalto.UI.Map;

/// <summary>
/// Enquadramento inicial no núcleo urbano: em cidade com zona rural grande, o mapa abre na sede, e o botão
/// "Cidade inteira" mostra o município todo e volta.
/// </summary>
public partial class ZoneMapView
{
    /// <summary>Folga em volta do núcleo urbano no enquadramento inicial, em fração do maior lado.</summary>
    private const float NucleusMargin = 0.2f;

    /// <summary>Distância máxima de um pedaço urbano à mancha do núcleo para entrar nela, em fração do maior lado da zona central.</summary>
    private const float NucleusGap = 0.5f;

    private Rect2 _home;
    private Rect2 _nucleusBox;
    private HashSet<string> _nucleus = [];

    /// <summary>O enquadramento inicial é só o núcleo urbano (há o que ver ao afastar).</summary>
    public bool HasNucleusView => _home != _world;

    /// <summary>O mapa está enquadrando o município inteiro.</summary>
    public bool ShowingWhole => _view == _world;

    /// <summary>Zonas do núcleo urbano: a zona urbana mais populosa e as urbanas perto dela, sem as vilas afastadas.</summary>
    public IReadOnlyCollection<string> Nucleus => _nucleus;

    /// <summary>
    /// Em cidade com zona rural enorme, as zonas urbanas somem: o enquadramento inicial é o núcleo urbano com folga.
    /// O núcleo é a mancha urbana contínua em volta da zona urbana mais populosa, pedaço por pedaço de contorno: uma
    /// zona urbana que junta bairros da sede e uma vila de distrito afastada entra só com o pedaço da sede.
    /// </summary>
    private void FrameNucleus()
    {
        _nucleus = [];
        _nucleusBox = _world;
        _home = _world;
        _view = _world;
        Zona? core = _zonas.Values.Where(z => !z.IsRural).MaxBy(z => z.Populacao);
        Shape? coreShape = core is null ? null : _shapes.FirstOrDefault(s => s.Id == core.Id);
        if (coreShape is null)
        {
            return;
        }

        var pieces = _shapes.Where(s => _zonas.TryGetValue(s.Id, out Zona? z) && !z.IsRural)
            .SelectMany(s => s.Rings.Select(r => (s.Id, Bounds: RingBounds(r)))).ToList();

        // A zona urbana mais populosa pode juntar bairros da sede e uma vila de distrito afastada (Zona Sudoeste, em
        // Mesquita/MG): cada pedaço dela é uma semente, e vale a mancha que reúne mais zonas urbanas (a sede), com o
        // maior pedaço como desempate.
        Rect2 box = default;
        HashSet<int> inside = [];
        int most = -1;
        foreach (Rect2 seed in coreShape.Rings.Select(RingBounds).OrderByDescending(b => b.Area))
        {
            var grown = Grow(seed, pieces.Select(p => p.Bounds).ToList());
            int zones = grown.Inside.Select(i => pieces[i].Id).Distinct().Count();
            if (zones > most)
            {
                (box, inside, most) = (grown.Box, grown.Inside, zones);
            }
        }

        _nucleus = inside.Select(i => pieces[i].Id).ToHashSet(StringComparer.Ordinal);
        _nucleusBox = box;
        if (box.Area < 0.25f * _world.Area)
        {
            _home = box.Grow(Math.Max(box.Size.X, box.Size.Y) * NucleusMargin);
            _view = _home;
        }
    }

    /// <summary>
    /// Mancha urbana a partir de um pedaço: bairros encostados ou separados por pouco (até meio pedaço inicial) entram,
    /// e a mancha cresce com eles; a vila de distrito a quilômetros fica de fora.
    /// </summary>
    private static (Rect2 Box, HashSet<int> Inside) Grow(Rect2 seed, IReadOnlyList<Rect2> pieces)
    {
        Rect2 box = seed;
        float gap = NucleusGap * Math.Max(seed.Size.X, seed.Size.Y);
        var inside = new HashSet<int>();
        bool grew = true;
        while (grew)
        {
            grew = false;
            for (int i = 0; i < pieces.Count; i++)
            {
                if (!inside.Contains(i) && pieces[i].Intersects(box.Grow(gap), includeBorders: true))
                {
                    inside.Add(i);
                    box = box.Merge(pieces[i]);
                    grew = true;
                }
            }
        }

        return (box, inside);
    }

    private static Rect2 RingBounds(Vector2[] ring)
    {
        var bounds = new Rect2(ring[0], Vector2.Zero);
        foreach (Vector2 v in ring)
        {
            bounds = bounds.Expand(v);
        }

        return bounds;
    }

    /// <summary>Mostra a cidade inteira (o enquadramento inicial pode ser só o núcleo urbano).</summary>
    public void ShowWhole()
    {
        _view = _world;
        ResetView();
    }

    /// <summary>Volta ao enquadramento inicial, no núcleo urbano.</summary>
    public void ShowNucleus()
    {
        _view = _home;
        ResetView();
    }

    /// <summary>Caixa, na tela, da mancha urbana do núcleo (ou do município, se não há zona urbana).</summary>
    public Rect2 NucleusScreenRect()
    {
        Transform2D t = ToScreen();
        return new Rect2(t * _nucleusBox.Position, _nucleusBox.Size * t.X.X);
    }
}
