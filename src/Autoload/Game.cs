using Godot;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Electorate;
using Planalto.Core.Municipios;
using Planalto.Core.Scenarios;
using Planalto.Core.State;
using Planalto.Core.World;

namespace Planalto.Autoload;

/// <summary>
/// Ponto de entrada da UI no Core: carrega o conteúdo de data/, guarda o estado e troca de tela.
/// A UI só lê daqui e chama os métodos de comando; toda regra fica no Core.
/// </summary>
public partial class Game : Node
{
    public static Game Instance { get; private set; } = null!;

    public string DataDirectory { get; private set; } = "";

    public WorldContent World { get; private set; } = null!;

    public PeopleContent People { get; private set; } = null!;

    public ElectorateContent Electorate { get; private set; } = null!;

    public MunicipiosDataSet Municipios { get; private set; } = null!;

    public PorteCatalog Portes { get; private set; } = null!;

    private Brasil2026Scenario? _brasil2026;

    /// <summary>Mundo em configuração na tela "Novo Mundo"; null fora dela.</summary>
    public WorldSetup? PendingWorld { get; set; }

    /// <summary>Semente da carreira em criação.</summary>
    public ulong PendingSeed { get; set; }

    public GameState? State { get; private set; }

    public Core.Campaign.CampaignContent Campaign { get; private set; } = null!;

    public Core.Events.EventContent Events { get; private set; } = null!;

    /// <summary>Motor da campanha do município em foco; recriado ao começar ou carregar uma carreira.</summary>
    public Core.Campaign.CampaignEngine? Engine { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        DataDirectory = ProjectSettings.GlobalizePath("res://data");

        // Fundo de todas as telas na cor do painel de fundo do tema (sem cor fixa no código).
        if (ThemeDB.GetProjectTheme()?.GetStylebox("panel", "BackgroundPanel") is StyleBoxFlat background)
        {
            RenderingServer.SetDefaultClearColor(background.BgColor);
        }
        Portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(DataDirectory, "portes.json")).GetValueOrThrow();
        var (municipios, errors) = MunicipiosDataSet.Load(DataDirectory, Portes);
        if (municipios is null)
        {
            GD.PushError("data/municipios inválido: " + string.Join("; ", errors.Take(5)));
            return;
        }

        Municipios = municipios;
        World = WorldContent.LoadOrThrow(DataDirectory);
        People = PeopleContent.LoadOrThrow(DataDirectory);
        ElectorateContent.Load(DataDirectory, World.Parties.Themes, Municipios, out ElectorateContent? electorate);
        Electorate = electorate ?? throw new DataValidationException(["data/electorate inválido."]);
        Campaign = Core.Campaign.CampaignContent.LoadOrThrow(DataDirectory);
        Events = Core.Events.EventContent.LoadOrThrow(DataDirectory);
    }

    /// <summary>Contexto da simulação detalhada do município em foco (GDD 12.5).</summary>
    public Core.Campaign.CampaignContext ContextFor(GameState state)
    {
        Municipio m = Municipios.All.First(x => x.CodigoIbge == state.FocusMunicipio);
        MunicipalScenario? local = state.World?.ScenarioId == "brasil_2026" ? Brasil2026?.Municipio(m.CodigoIbge) : null;
        return new Core.Campaign.CampaignContext
        {
            State = state,
            Municipio = m,
            Local = local,
            Electorate = Core.Campaign.CampaignContext.BuildElectorate(state, m, Electorate, World.Parties.Themes, World.Parties.Generator.SizeShareWeights, local),
            People = People,
            ElectorateContent = Electorate,
            Campaign = Campaign,
            Themes = World.Parties.Themes,
            Events = Events,
            Portes = Portes,
        };
    }

    /// <summary>Cenário Brasil 2026 completo (estados e municípios), carregado na primeira vez que for pedido.</summary>
    public Brasil2026Scenario? Brasil2026
    {
        get
        {
            if (_brasil2026 is null && Brasil2026Scenario.Exists(DataDirectory))
            {
                _brasil2026 = Brasil2026Scenario.Load(DataDirectory, Municipios, World.Parties).Scenario;
            }

            return _brasil2026;
        }
    }

    /// <summary>Começa (ou retoma, ao carregar) a carreira e a campanha do município em foco.</summary>
    public void StartCareer(GameState state)
    {
        State = state;
        Engine = Core.Campaign.CampaignEngine.Start(ContextFor(state));
    }

    /// <summary>Nos testes de fumaça da UI, registra a navegação em vez de trocar de cena.</summary>
    public bool SuppressNavigation { get; set; }

    public string? LastNavigation { get; private set; }

    public void GoTo(string scene)
    {
        LastNavigation = scene;
        if (!SuppressNavigation)
        {
            GetTree().ChangeSceneToFile(scene);
        }
    }

    public static class Scenes
    {
        public const string MainMenu = "res://src/UI/Screens/MainMenu.tscn";
        public const string NewWorld = "res://src/UI/Screens/NewWorldScreen.tscn";
        public const string CharacterCreator = "res://src/UI/Screens/CharacterCreatorScreen.tscn";
        public const string Hub = "res://src/UI/Screens/HubScreen.tscn";
        public const string Urna = "res://src/UI/Screens/UrnaScreen.tscn";
        public const string ElectionNight = "res://src/UI/Screens/ElectionNightScreen.tscn";
        public const string Map = "res://src/UI/Map/MapScreen.tscn";
    }
}
