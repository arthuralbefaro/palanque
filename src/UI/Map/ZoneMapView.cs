using Godot;
using Planalto.Core.Geo;
using Planalto.Core.Territory;
using Planalto.UI.Theme;

namespace Planalto.UI.Map;

/// <summary>
/// Mapa 2D das zonas do município em foco (GDD 17.3, ART_GUIDE seção Interface): polígonos triangulados no
/// importador, cor por zona, contorno, rótulos das maiores zonas (ZoneMapView.Labels.cs), destaque de mouse e de seleção.
/// </summary>
public partial class ZoneMapView : Control
{
    /// <summary>
    /// Marcador numa zona (GDD 17.3): compromisso da semana, rival visto, liderança conhecida. Com ícone, é um disco
    /// com o ícone na cor; com texto (sigla de partido), uma etiqueta na cor.
    /// </summary>
    public sealed record Marker(string ZoneId, Color Color, Texture2D? Icon, string? Text);

    /// <summary>Acima deste número, os marcadores da zona viram um só, com a contagem (ART_GUIDE, densidade).</summary>
    public const int MaxMarkersPerZone = 3;

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
    private List<Marker> _markers = [];
    private HashSet<string> _pulsing = [];
    private double _time;

    public IReadOnlyList<Marker> Markers => _markers;

    public IReadOnlyCollection<string> Pulsing => _pulsing;

    /// <summary>Marcadores por zona e as zonas com acontecimento hoje (contorno que pulsa em atencao).</summary>
    public void SetMarkers(IEnumerable<Marker> markers, IEnumerable<string> pulsing)
    {
        _markers = markers.Where(m => _zonas.ContainsKey(m.ZoneId)).ToList();
        _pulsing = pulsing.Where(_zonas.ContainsKey).ToHashSet(StringComparer.Ordinal);
        SetProcess(_pulsing.Count > 0);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _time += delta;
        if (_pulsing.Count > 0)
        {
            QueueRedraw();
        }
    }

    /// <summary>Leva a zona para o centro do mapa (clique num item do feed).</summary>
    public void CenterOn(string id)
    {
        if (!_zonas.ContainsKey(id))
        {
            return;
        }

        _pan += (Size / 2) - ScreenCenterOf(id);
        QueueRedraw();
    }

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

        FrameNucleus();
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

        // Zona com acontecimento hoje: contorno que pulsa em atencao.
        float pulse = 0.5f + (0.5f * (float)Math.Sin(_time * 4));
        foreach (Shape s in _shapes.Where(s => _pulsing.Contains(s.Id)))
        {
            foreach (Vector2[] ring in s.Rings)
            {
                var points = ring.Select(p => t * p).Append(t * ring[0]).ToArray();
                DrawPolyline(points, new Color(Palette.Atencao, 0.45f + (0.55f * pulse)), 2 + (2 * pulse), antialiased: true);
            }
        }

        Font font = Palette.Weight(Palette.Inter, 500);
        DrawLabels(font);
        DrawMarkers(t, font);
    }

    /// <summary>Marcadores em fila sob o rótulo da zona; mais de três viram um disco com a contagem.</summary>
    private void DrawMarkers(Transform2D t, Font font)
    {
        const float Height = 28;
        const float Gap = 4;
        float xScale = (float)Math.Cos(_zonas.Values.Average(v => v.Lat) * Math.PI / 180);
        foreach (var group in _markers.GroupBy(m => m.ZoneId))
        {
            Zona z = _zonas[group.Key];
            Vector2 anchor = (t * new Vector2((float)z.Lon * xScale, -(float)z.Lat)) + new Vector2(0, 30);
            List<Marker> list = group.ToList();
            if (list.Count > MaxMarkersPerZone)
            {
                string count = list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                Vector2 size = font.GetStringSize(count, HorizontalAlignment.Left, -1, 18);
                DrawCircle(anchor, 16, new Color(Palette.Fundo, 0.9f));
                DrawArc(anchor, 16, 0, Mathf.Tau, 32, Palette.Texto, 2, antialiased: true);
                DrawString(font, anchor + new Vector2(-size.X / 2, 6), count, HorizontalAlignment.Left, -1, 18, Palette.Texto);
                continue;
            }

            float[] widths = list.Select(m => m.Text is null ? Height : font.GetStringSize(m.Text, HorizontalAlignment.Left, -1, 18).X + 14).ToArray();
            float x = anchor.X - ((widths.Sum() + (Gap * (list.Count - 1))) / 2);
            for (int i = 0; i < list.Count; i++)
            {
                Marker m = list[i];
                var rect = new Rect2(x, anchor.Y - (Height / 2), widths[i], Height);
                if (m.Text is not null)
                {
                    DrawRect(rect, m.Color);
                    DrawRect(rect, Palette.Fundo, filled: false, width: 2);
                    DrawString(font, new Vector2(rect.Position.X + 8, rect.End.Y - 7), m.Text, HorizontalAlignment.Left, -1, 18, new Color(0, 0, 0, 0.8f));
                    DrawString(font, new Vector2(rect.Position.X + 7, rect.End.Y - 8), m.Text, HorizontalAlignment.Left, -1, 18, Palette.Texto);
                }
                else
                {
                    Vector2 center = rect.GetCenter();
                    DrawCircle(center, Height / 2, new Color(Palette.Fundo, 0.9f));
                    DrawArc(center, Height / 2, 0, Mathf.Tau, 32, m.Color, 2, antialiased: true);
                    if (m.Icon is not null)
                    {
                        DrawTextureRect(m.Icon, new Rect2(center - new Vector2(9, 9), new Vector2(18, 18)), false, m.Color);
                    }
                }

                x += widths[i] + Gap;
            }
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

    /// <summary>Zona sob um ponto da tela (teste de triângulos).</summary>
    public string? Hit(Vector2 screen)
    {
        // O nome deslocado pode estar sobre outra zona: vale a zona do nome.
        if (LabelAt(screen) is string labeled)
        {
            return labeled;
        }

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
