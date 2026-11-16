using Godot;
using Planalto.Core.Geo;
using Planalto.Core.Territory;
using Planalto.UI.Theme;

namespace Planalto.UI.Map;

/// <summary>
/// Mapa 2D das zonas do município em foco (GDD 17.3, ART_GUIDE seção Interface): polígonos triangulados no
/// importador, cor por zona, contorno, rótulos das maiores zonas, destaque de mouse e de seleção.
/// </summary>
public partial class ZoneMapView : Control
{
    private sealed record Shape(string Id, List<Vector2[]> Triangles, List<Vector2[]> Rings, Rect2 Bounds);

    private readonly List<Shape> _shapes = [];
    private Dictionary<string, Zona> _zonas = [];
    private Dictionary<string, Color> _colors = [];
    private Rect2 _world;
    private int _scale = 1;
    private string? _hover;
    private float _zoom = 1;
    private Vector2 _pan = Vector2.Zero;
    private Rect2 _view;
    private bool _dragging;

    public string? Selected { get; private set; }

    /// <summary>Zona clicada (ou null ao clicar fora).</summary>
    public event Action<string?>? ZoneClicked;

    /// <summary>Zona sob o mouse mudou (para o tooltip de origem dos números).</summary>
    public Func<string, string>? HoverText { get; set; }

    public IReadOnlyDictionary<string, Zona> Zonas => _zonas;

    public void Load(ZonasMunicipio municipio, IEnumerable<GeoZona> contornos, int scale)
    {
        _scale = scale;
        _zonas = municipio.Zonas.ToDictionary(z => z.Id);
        _shapes.Clear();
        double latRad = municipio.Zonas.Average(z => z.Lat) * Math.PI / 180;
        float xScale = (float)Math.Cos(latRad);
        foreach (GeoZona g in contornos.Where(g => _zonas.ContainsKey(g.Id)))
        {
            var triangles = new List<Vector2[]>();
            var rings = new List<Vector2[]>();
            foreach (GeoPolygon p in g.Polygons)
            {
                Vector2[] points = AllPoints(p, xScale);
                for (int t = 0; t + 2 < p.Triangles.Count; t += 3)
                {
                    triangles.Add([points[p.Triangles[t]], points[p.Triangles[t + 1]], points[p.Triangles[t + 2]]]);
                }

                rings.Add(Ring(p.Outer, xScale));
                rings.AddRange(p.Holes.Select(h => Ring(h, xScale)));
            }

            var all = rings.SelectMany(r => r).ToArray();
            var bounds = new Rect2(all[0], Vector2.Zero);
            foreach (Vector2 v in all)
            {
                bounds = bounds.Expand(v);
            }

            _shapes.Add(new Shape(g.Id, triangles, rings, bounds));
        }

        _world = _shapes.Count == 0 ? new Rect2(0, 0, 1, 1) : _shapes.Select(s => s.Bounds).Aggregate((a, b) => a.Merge(b));

        // Em cidade com zona rural enorme, as zonas urbanas somem: o enquadramento inicial é a área urbana com folga.
        // Núcleo urbano: a zona urbana mais populosa e as urbanas vizinhas (vilas de distrito afastadas ficam de fora).
        Zona? core = _zonas.Values.Where(z => !z.IsRural).MaxBy(z => z.Populacao);
        var nucleus = core is null ? [] : core.Vizinhas.Where(v => _zonas.TryGetValue(v, out Zona? n) && !n.IsRural).Append(core.Id).ToHashSet();
        var urban = _shapes.Where(s => nucleus.Contains(s.Id)).Select(s => s.Bounds).ToList();
        Rect2 urbanBox = urban.Count == 0 ? _world : urban.Aggregate((a, b) => a.Merge(b));
        _view = urban.Count > 0 && urbanBox.Area < 0.25f * _world.Area ? urbanBox.Grow(Math.Max(urbanBox.Size.X, urbanBox.Size.Y) * 0.4f) : _world;
        _zoom = 1;
        _pan = Vector2.Zero;
        QueueRedraw();
    }

    public void SetColors(Dictionary<string, Color> colors)
    {
        _colors = colors;
        QueueRedraw();
    }

    public void Select(string? id)
    {
        Selected = id;
        QueueRedraw();
    }

    private Vector2[] AllPoints(GeoPolygon p, float xScale) =>
        Ring(p.Outer, xScale).Concat(p.Holes.SelectMany(h => Ring(h, xScale))).ToArray();

    private Vector2[] Ring(List<int> flat, float xScale)
    {
        var result = new Vector2[flat.Count / 2];
        for (int i = 0; i < result.Length; i++)
        {
            // Norte para cima: a latitude cresce para o norte, a tela cresce para baixo.
            result[i] = new Vector2(flat[2 * i] / (float)_scale * xScale, -flat[(2 * i) + 1] / (float)_scale);
        }

        return result;
    }

    /// <summary>Transformação do mundo (graus) para a tela, mantendo a proporção e com margem.</summary>
    private Transform2D ToScreen()
    {
        const float margin = 32;
        Vector2 available = Size - new Vector2(2 * margin, 2 * margin);
        float scale = Math.Min(available.X / Math.Max(_view.Size.X, 1e-6f), available.Y / Math.Max(_view.Size.Y, 1e-6f)) * _zoom;
        Vector2 offset = (Size / 2) - ((_view.Position + (_view.Size / 2)) * scale) + _pan;
        return new Transform2D(new Vector2(scale, 0), new Vector2(0, scale), offset);
    }

    public override void _Draw()
    {
        if (_shapes.Count == 0)
        {
            return;
        }

        Transform2D t = ToScreen();
        foreach (Shape s in _shapes)
        {
            Color fill = _colors.GetValueOrDefault(s.Id, Palette.NeutroDado);
            foreach (Vector2[] tri in s.Triangles)
            {
                DrawColoredPolygon([t * tri[0], t * tri[1], t * tri[2]], fill);
            }
        }

        foreach (Shape s in _shapes)
        {
            bool selected = s.Id == Selected;
            bool hover = s.Id == _hover;
            Color line = selected ? Palette.Texto : hover ? Palette.Texto2 : new Color(Palette.Texto, 0.35f);
            float width = selected ? 3 : hover ? 2 : 1.5f;
            foreach (Vector2[] ring in s.Rings)
            {
                var points = ring.Select(p => t * p).Append(t * ring[0]).ToArray();
                DrawPolyline(points, line, width, antialiased: true);
            }
        }

        // Rótulos: no máximo 12, das zonas mais populosas (ART_GUIDE, densidade).
        Font font = Palette.Weight(Palette.Inter, 500);
        foreach (Zona z in _zonas.Values.OrderByDescending(z => z.Populacao).Take(12))
        {
            Shape? shape = _shapes.FirstOrDefault(s => s.Id == z.Id);
            if (shape is null)
            {
                continue;
            }

            Vector2 at = t * new Vector2((float)(z.Lon * Math.Cos(_zonas.Values.Average(v => v.Lat) * Math.PI / 180)), -(float)z.Lat);
            Vector2 size = font.GetStringSize(z.Nome, HorizontalAlignment.Left, -1, 18);
            if ((shape.Bounds.Size * t.X.X).X < size.X * 0.6f)
            {
                continue;
            }

            DrawString(font, at - new Vector2((size.X / 2) + 1, -7), z.Nome, HorizontalAlignment.Left, -1, 18, new Color(0, 0, 0, 0.7f));
            DrawString(font, at - new Vector2(size.X / 2, -6), z.Nome, HorizontalAlignment.Left, -1, 18, Palette.Texto);
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion motion)
        {
            string? hit = Hit(motion.Position);
            if (hit != _hover)
            {
                _hover = hit;
                TooltipText = hit is not null && HoverText is not null ? HoverText(hit) : "";
                QueueRedraw();
            }
        }

        if (e is InputEventMouseMotion drag && _dragging)
        {
            _pan += drag.Relative;
            QueueRedraw();
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true } wheel)
        {
            // Zoom com a roda, mantendo o ponto sob o mouse no lugar.
            Vector2 before = ToScreen().AffineInverse() * wheel.Position;
            _zoom = Math.Clamp(_zoom * (wheel.ButtonIndex == MouseButton.WheelUp ? 1.2f : 1 / 1.2f), 0.5f, 40);
            _pan += wheel.Position - (ToScreen() * before);
            QueueRedraw();
        }
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right or MouseButton.Middle } pan)
        {
            _dragging = pan.Pressed;
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left, DoubleClick: true })
        {
            ResetView();
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            Selected = Hit(click.Position);
            ZoneClicked?.Invoke(Selected);
            QueueRedraw();
        }
    }

    /// <summary>Volta ao enquadramento inicial.</summary>
    public void ResetView()
    {
        _zoom = 1;
        _pan = Vector2.Zero;
        QueueRedraw();
    }

    /// <summary>Mostra a cidade inteira (o enquadramento inicial pode ser só a área urbana).</summary>
    public void ShowWhole()
    {
        _view = _world;
        ResetView();
    }

    /// <summary>Zona sob um ponto da tela (teste de triângulos).</summary>
    public string? Hit(Vector2 screen)
    {
        Vector2 world = ToScreen().AffineInverse() * screen;
        foreach (Shape s in _shapes.Where(s => s.Bounds.Grow(1e-4f).HasPoint(world)))
        {
            if (s.Triangles.Any(tri => Geometry2D.PointIsInsideTriangle(world, tri[0], tri[1], tri[2])))
            {
                return s.Id;
            }
        }

        return null;
    }

    /// <summary>Centro da zona na tela (testes de fumaça clicam por aqui).</summary>
    public Vector2 ScreenCenterOf(string id)
    {
        Zona z = _zonas[id];
        float xScale = (float)Math.Cos(_zonas.Values.Average(v => v.Lat) * Math.PI / 180);
        return ToScreen() * new Vector2((float)z.Lon * xScale, -(float)z.Lat);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized)
        {
            QueueRedraw();
        }
    }
}
