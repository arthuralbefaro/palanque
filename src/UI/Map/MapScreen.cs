using System.Diagnostics;
using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Geo;
using Planalto.Core.Municipios;
using Planalto.Core.State;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Map;

/// <summary>
/// Protótipo do mapa 2.5D (GDD 13.1, Fase 3): os municípios de uma UF extrudados num SubViewport 3D.
/// Altura = força de um partido (o do jogador, por padrão); cor = partido mais forte, mais viva quanto maior a margem.
/// Câmera inclinada, zoom pela roda do mouse, arraste com o botão esquerdo; passar o mouse mostra os dados.
/// </summary>
public partial class MapScreen : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // Escala visual do protótipo: 1 unidade 3D = 10 km; relevo de 0,4 a 7 unidades.
    private const float KmPerUnit = 10f;
    private const float BaseHeight = 0.4f;
    private const float ReliefHeight = 6.6f;
    private const float TiltDegrees = 35f;

    // A força costuma se concentrar em poucos municípios; o topo da escala é o percentil 95, não o máximo.
    private const double ReliefPercentile = 0.95;

    private static readonly Color Neutral = new("#4A5568");
    private static readonly Color Focus = new("#FFE08A");

    private Game _game = null!;
    private GeoUf _geo = null!;
    private GeoProjection _projection = null!;
    private Dictionary<int, MapCell> _cells = [];
    private readonly List<Region> _regions = [];
    private readonly Dictionary<int, Municipio> _municipios = [];
    private int _focus;
    private string? _heightParty;
    private Node3D _meshes = null!;
    private Camera3D? _camera;
    private Node3D _pivot = null!;
    private SubViewportContainer _container = null!;
    private Label _tooltip = null!;
    private Label _fps = null!;
    private Label _subtitle = null!;
    private OptionButton? _partyOptions;
    private readonly List<string> _partyIds = [];
    private float _distance;
    private bool _dragging;

    /// <summary>Tempo para calcular as camadas e montar as malhas, em ms (lido pelo teste de fumaça).</summary>
    public long BuildMilliseconds { get; private set; }

    public int MeshCount => _regions.Count;

    /// <summary>UF a mostrar; sem valor, a do município em foco.</summary>
    public string? Uf { get; set; }

    private sealed record Region(int CodigoIbge, MapCell Cell, List<Vector2[]> Rings, Rect2 Bounds, float Height);

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.State is not { } state)
        {
            _game.GoTo(Game.Scenes.MainMenu);
            return;
        }

        Municipio focus = _game.Municipios.All.First(m => m.CodigoIbge == state.FocusMunicipio);
        _focus = focus.CodigoIbge;
        string uf = Uf ?? focus.Uf;
        foreach (Municipio m in _game.Municipios.All.Where(m => m.Uf == uf))
        {
            _municipios[m.CodigoIbge] = m;
        }

        _heightParty = state.Player.PartyId;

        VBoxContainer screen = Ui.Screen(this, 16);
        HBoxContainer top = Ui.Row(screen, 20);
        Ui.Value(top, string.Format(Tr("Mapa de {0}"), uf), 30);
        _subtitle = Ui.Value(top, "", 18);
        top.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _fps = Ui.Value(top, "", 16);
        Ui.Button(top, "Voltar", _game.GoToCareer, Palette.Tip(Tr("Volta à tela central."), Tr("fecha o mapa do estado; a carreira continua de onde estava."), Tr("nada.")));

        HBoxContainer body = Ui.Row(screen, 12);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        _container = body.Add(new SubViewportContainer { Stretch = true, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill });
        var viewport = _container.Add(new SubViewport { Msaa3D = Viewport.Msaa.Msaa4X, HandleInputLocally = false });
        VBoxContainer side = Ui.Column(body, 8);
        side.CustomMinimumSize = new Vector2(360, 0);

        var watch = Stopwatch.StartNew();
        _geo = _game.Geo(uf);
        _projection = GeoProjection.For(_geo);
        _cells = ElectoralMap.Cells(state, _municipios.Values, _game.Electorate, _game.World.Parties.Generator.SizeShareWeights, ibge => _game.LocalScenario(state, ibge))
            .ToDictionary(c => c.CodigoIbge);
        BuildScene(viewport);
        BuildMeshes();
        watch.Stop();
        BuildMilliseconds = watch.ElapsedMilliseconds;

        BuildSide(side, state);
    }

    private void BuildScene(SubViewport viewport)
    {
        var root = viewport.Add(new Node3D());
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = RenderingServer.GetDefaultClearColor(),
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.75f, 0.78f, 0.85f),
            AmbientLightEnergy = 0.8f,
        };
        root.Add(new WorldEnvironment { Environment = environment });

        // Sol vindo do sul e do alto, para que as paredes voltadas à câmera fiquem iluminadas.
        var sun = root.Add(new DirectionalLight3D { LightEnergy = 0.9f });
        sun.RotationDegrees = new Vector3(-50, 160, 0);
        _meshes = root.Add(new Node3D());

        // Câmera: pivô no centro da UF, inclinada, à distância que enquadra o estado inteiro.
        _pivot = root.Add(new Node3D());
        _pivot.RotationDegrees = new Vector3(-(90 - TiltDegrees), 0, 0);
        _distance = (float)Math.Max(_projection.WidthKm, _projection.HeightKm) / KmPerUnit * 1.15f;
        _camera = _pivot.Add(new Camera3D { Fov = 45, Far = 5000 });
        _camera.Position = new Vector3(0, 0, _distance);
    }

    /// <summary>(Re)monta as malhas com a altura pela força do partido escolhido.</summary>
    private void BuildMeshes()
    {
        GameState state = _game.State!;
        foreach (Node child in _meshes.GetChildren())
        {
            _meshes.RemoveChild(child);
            child.QueueFree();
        }

        _regions.Clear();
        string sigla = _heightParty is not null ? state.Party(_heightParty)?.Sigla ?? _heightParty : Tr("nenhum partido");
        _subtitle.Text = string.Format(Tr("Altura: força do {0} · Cor: partido mais forte"), sigla);

        var relief = ElectoralMap.Relief(_cells.Values, _heightParty, ReliefPercentile);
        var material = new StandardMaterial3D { VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Roughness = 0.9f };
        foreach (GeoMunicipio m in _geo.Municipios)
        {
            if (!_cells.TryGetValue(m.CodigoIbge, out MapCell? cell))
            {
                continue;
            }

            float height = BaseHeight + ((float)relief[m.CodigoIbge] * ReliefHeight);
            Color color = ColorFor(state, cell);
            var rings = new List<Vector2[]>();
            var mesh = new ArrayMesh();
            foreach (GeoPolygon polygon in m.Polygons)
            {
                AddPolygon(mesh, polygon, height, color, rings);
            }

            for (int s = 0; s < mesh.GetSurfaceCount(); s++)
            {
                mesh.SurfaceSetMaterial(s, material);
            }

            _meshes.Add(new MeshInstance3D { Mesh = mesh });
            _regions.Add(new Region(m.CodigoIbge, cell, rings, BoundsOf(rings), height));
            if (m.CodigoIbge == _focus)
            {
                AddOutline(_meshes, rings, height + 0.05f);
            }
        }

        // Do mais alto para o mais baixo: no teste do mouse, o primeiro acerto é o que está na frente.
        _regions.Sort((a, b) => b.Height.CompareTo(a.Height));
    }

    private static Color ColorFor(GameState state, MapCell cell)
    {
        Color party = state.Party(cell.LeaderPartyId) is { } p ? new Color(p.Colors.Primary) : Neutral;
        float intensity = 0.35f + (0.65f * (float)Math.Min(1, cell.Margin / 0.15));
        return Neutral.Lerp(party, intensity);
    }

    private Vector2 Project(double lon, double lat)
    {
        var (x, y) = _projection.Project(lon, lat);
        return new Vector2((float)(x / KmPerUnit), (float)(-y / KmPerUnit));
    }

    /// <summary>Face de cima (triângulos do importador) e paredes de cada anel, numa superfície.</summary>
    private void AddPolygon(ArrayMesh mesh, GeoPolygon polygon, float height, Color color, List<Vector2[]> rings)
    {
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Color>();

        var all = new Vector2[polygon.VertexCount];
        for (int i = 0; i < all.Length; i++)
        {
            var (lon, lat) = polygon.Vertex(i, _geo.Scale);
            all[i] = Project(lon, lat);
        }

        for (int t = 0; t + 2 < polygon.Triangles.Count; t += 3)
        {
            // Godot desenha a face da frente no sentido horário; o importador grava anti-horário (norte para cima).
            foreach (int index in new[] { polygon.Triangles[t], polygon.Triangles[t + 2], polygon.Triangles[t + 1] })
            {
                vertices.Add(new Vector3(all[index].X, height, all[index].Y));
                normals.Add(Vector3.Up);
                colors.Add(color);
            }
        }

        Color wall = color.Darkened(0.15f);
        int offset = 0;
        foreach (List<int> ring in polygon.Holes.Prepend(polygon.Outer))
        {
            int n = ring.Count / 2;
            Vector2[] points = all[offset..(offset + n)];
            offset += n;
            rings.Add(points);
            for (int i = 0; i < n; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % n];
                Vector2 d = b - a;
                var normal = new Vector3(d.Y, 0, -d.X).Normalized();
                Vector3 a0 = new(a.X, 0, a.Y), b0 = new(b.X, 0, b.Y), a1 = new(a.X, height, a.Y), b1 = new(b.X, height, b.Y);
                foreach (Vector3 v in new[] { a0, a1, b1, a0, b1, b0 })
                {
                    vertices.Add(v);
                    normals.Add(normal);
                    colors.Add(wall);
                }
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colors.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }

    /// <summary>Contorno brilhante do município em foco (GDD 13.1).</summary>
    private static void AddOutline(Node3D parent, List<Vector2[]> rings, float height)
    {
        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Focus };
        foreach (Vector2[] ring in rings)
        {
            mesh.SurfaceBegin(Mesh.PrimitiveType.LineStrip, material);
            foreach (Vector2 p in ring.Append(ring[0]))
            {
                mesh.SurfaceAddVertex(new Vector3(p.X, height, p.Y));
            }

            mesh.SurfaceEnd();
        }

        parent.Add(new MeshInstance3D { Mesh = mesh });
    }

    private static Rect2 BoundsOf(List<Vector2[]> rings)
    {
        Vector2 min = rings[0][0], max = rings[0][0];
        foreach (Vector2 p in rings.SelectMany(r => r))
        {
            min = min.Min(p);
            max = max.Max(p);
        }

        return new Rect2(min, max - min);
    }

    private void BuildSide(VBoxContainer side, GameState state)
    {
        Ui.Label(side, "Altura pela força de", 18);
        var present = _cells.Values.SelectMany(c => c.Strength.Where(s => s.Value > 0).Select(s => s.Key))
            .GroupBy(id => id)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();
        if (_heightParty is not null && !present.Contains(_heightParty))
        {
            present.Insert(0, _heightParty);
        }

        _partyIds.AddRange(present);
        _partyOptions = Ui.Options(
            side,
            present.Select(id => (id, $"{state.Party(id)?.Sigla ?? id}{(id == state.Player.PartyId ? " (" + Tr("seu partido") + ")" : "")}")),
            _heightParty,
            id =>
            {
                _heightParty = id;
                BuildMeshes();
            },
            translate: false);
        _partyOptions.TooltipText = Palette.Tip(Tr("O partido que dá a altura dos municípios no mapa."), Tr("cada município sobe conforme a força desse partido nele; a cor continua sendo a do partido mais forte. Arraste para mover e use a roda do mouse para aproximar."), Tr("nada; o tempo do jogo não anda no mapa."));

        _tooltip = Ui.Paragraph(side, Tr("Passe o mouse sobre um município."), 340, 18);
        Ui.Label(side, "Partido mais forte", 18);
        foreach (var group in _cells.Values.GroupBy(c => c.LeaderPartyId).OrderByDescending(g => g.Count()).Take(10))
        {
            HBoxContainer row = Ui.Row(side, 8);
            GameParty? party = state.Party(group.Key);
            row.Add(new ColorRect { Color = party is null ? Neutral : new Color(party.Colors.Primary), CustomMinimumSize = new Vector2(22, 22) });
            Ui.Value(row, string.Format(PtBr, "{0}: {1} {2}", party?.Sigla ?? group.Key, group.Count(), Tr("municípios")), 16);
        }

        Glossary.LinkTerms(side);
    }

    /// <summary>Troca o partido que define a altura (usado pelo teste de fumaça).</summary>
    public void ShowStrengthOf(string partyId)
    {
        _heightParty = partyId;
        int index = _partyIds.IndexOf(partyId);
        if (_partyOptions is not null && index >= 0)
        {
            _partyOptions.Select(index);
        }

        BuildMeshes();
    }

    /// <summary>Partido com presença no maior número de municípios da UF.</summary>
    public string MostPresentParty() =>
        _cells.Values.SelectMany(c => c.Strength.Where(s => s.Value > 0).Select(s => s.Key)).GroupBy(id => id).MaxBy(g => g.Count())!.Key;

    public override void _Process(double delta)
    {
        if (_camera is not null)
        {
            _fps.Text = string.Format(PtBr, "{0:0} FPS", Engine.GetFramesPerSecond());
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (_camera is null || !_container.GetGlobalRect().HasPoint(GetGlobalMousePosition()))
        {
            _dragging = false;
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }:
                Zoom(0.88f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }:
                Zoom(1 / 0.88f);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _dragging = button.Pressed;
                break;
            case InputEventMouseMotion motion when _dragging:
                // Arrasta no plano do chão, proporcional à distância da câmera.
                float k = _distance / 900f;
                _pivot.Position += new Vector3(-motion.Relative.X * k, 0, -motion.Relative.Y * k);
                break;
            case InputEventMouseMotion:
                Hover();
                break;
        }
    }

    private void Zoom(float factor)
    {
        _distance = Mathf.Clamp(_distance * factor, 8, 400);
        _camera!.Position = new Vector3(0, 0, _distance);
    }

    /// <summary>Raio da câmera até o plano do topo de cada município e teste de ponto no polígono.</summary>
    private void Hover()
    {
        Vector2 local = _container.GetLocalMousePosition();
        Vector3 origin = _camera!.ProjectRayOrigin(local);
        Vector3 direction = _camera.ProjectRayNormal(local);
        if (Mathf.IsZeroApprox(direction.Y))
        {
            return;
        }

        Region? hit = null;
        foreach (Region region in _regions)
        {
            float t = (region.Height - origin.Y) / direction.Y;
            Vector3 p = origin + (direction * t);
            var point = new Vector2(p.X, p.Z);
            if (region.Bounds.Grow(0.01f).HasPoint(point) && Contains(region.Rings, point))
            {
                hit = region;
                break;
            }
        }

        if (hit is null)
        {
            return;
        }

        GameState state = _game.State!;
        Municipio m = _municipios[hit.CodigoIbge];
        string Sigla(string? id) => id is null ? "—" : state.Party(id)?.Sigla ?? id;
        _tooltip.Text = string.Format(
            PtBr,
            "{0} ({1})\n{2}: {3:N0}\n{4}: {5} ({6:0.0}%)\n{7} {8}: {9:0.0}%",
            m.Nome,
            m.Uf,
            Tr("Eleitores"),
            m.Eleitorado,
            Tr("Partido mais forte"),
            Sigla(hit.Cell.LeaderPartyId),
            hit.Cell.LeaderShare * 100,
            Tr("Força do"),
            Sigla(_heightParty),
            hit.Cell.ShareOf(_heightParty) * 100);
    }

    /// <summary>Par-ímpar sobre todos os anéis (contornos e furos).</summary>
    private static bool Contains(List<Vector2[]> rings, Vector2 p)
    {
        bool inside = false;
        foreach (Vector2[] ring in rings)
        {
            for (int i = 0, j = ring.Length - 1; i < ring.Length; j = i++)
            {
                if ((ring[i].Y > p.Y) != (ring[j].Y > p.Y)
                    && p.X < ((ring[j].X - ring[i].X) * (p.Y - ring[i].Y) / (ring[j].Y - ring[i].Y)) + ring[i].X)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }
}
