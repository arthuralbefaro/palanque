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

    /// <summary>Motor da campanha do município em foco; null fora de campanha.</summary>
    public Core.Campaign.CampaignEngine? Engine { get; private set; }

    public Core.Legislature.LegislatureContent Legislature { get; private set; } = null!;

    public Core.Career.FoundingConfig Founding { get; private set; } = null!;

    /// <summary>Motor do mandato; null fora de mandato.</summary>
    public Core.Legislature.LegislatureEngine? Mandate { get; private set; }

    /// <summary>Motor da fundação de partido (vale em qualquer momento da carreira).</summary>
    public Core.Career.PartyFoundingEngine? FoundingEngine { get; private set; }

    public Core.Executive.ExecutiveContent ExecutiveContent { get; private set; } = null!;

    public Core.Debate.DebateContent Debate { get; private set; } = null!;

    /// <summary>Motor da prefeitura; null fora do mandato de prefeito.</summary>
    public Core.Executive.ExecutiveEngine? Prefeitura { get; private set; }

    /// <summary>Conteúdo dos mandatos da R4 (obras, secretários, folha, instrumentos, cobrança, eventos de mandato).</summary>
    public Core.Governance.GovernanceContent GovernanceContent { get; private set; } = null!;

    /// <summary>Governo da cidade no mandato em curso (GDD 17.14 e 17.15); null fora de mandato ou em save antigo.</summary>
    public Core.Governance.GovernanceEngine? Governo { get; private set; }

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
        Legislature = Core.Legislature.LegislatureContent.LoadOrThrow(
            DataDirectory,
            Electorate.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal),
            World.Parties.Themes.Themes.Select(t => t.Id).ToHashSet(StringComparer.Ordinal));
        Founding = JsonData.LoadFile<Core.Career.FoundingConfig>(Path.Combine(DataDirectory, Core.Career.FoundingConfig.File)).GetValueOrThrow();
        ExecutiveContent = Core.Executive.ExecutiveContent.LoadOrThrow(DataDirectory, Electorate.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal));
        GovernanceContent = Core.Governance.GovernanceContent.LoadOrThrow(DataDirectory, ExecutiveContent, Campaign.Promises);
        Debate = JsonData.LoadFile<Core.Debate.DebateContent>(Path.Combine(DataDirectory, Core.Debate.DebateContent.File)).GetValueOrThrow();
        Encyclopedia = JsonData.LoadFile<Core.Encyclopedia.EncyclopediaContent>(Path.Combine(DataDirectory, Core.Encyclopedia.EncyclopediaContent.File)).GetValueOrThrow();
        Time = JsonData.LoadFile<Core.State.TimeConfig>(Path.Combine(DataDirectory, Core.State.TimeConfig.File)).GetValueOrThrow();
        Projection = PopulationProjection.Load(DataDirectory, Municipios);
        IndicatorModel = Core.Indicators.IndicatorContent.LoadOrThrow(DataDirectory);
        RenewalContent = Core.Renewal.RenewalContent.LoadOrThrow(DataDirectory);
    }

    /// <summary>Definições e modelo de evolução dos indicadores reais (GDD 17.17).</summary>
    public Core.Indicators.IndicatorContent IndicatorModel { get; private set; } = null!;

    /// <summary>Conteúdo da renovação do mundo político (GDD 17.16).</summary>
    public Core.Renewal.RenewalContent RenewalContent { get; private set; } = null!;

    /// <summary>
    /// O mundo que anda sozinho no município em foco: indicadores em unidade real e renovação do mundo político; null
    /// antes de haver município em foco.
    /// </summary>
    public WorldEngine? Mundo { get; private set; }

    /// <summary>
    /// Monta o mundo do estado e garante que o save tem os indicadores e a renovação do município em foco: cria os dois
    /// num mundo novo e num save anterior a eles, sem mexer no resto.
    /// </summary>
    private WorldEngine? NewMundo(GameState state)
    {
        Municipio? m = Municipios.All.FirstOrDefault(x => x.CodigoIbge == state.FocusMunicipio);
        if (m is null)
        {
            return null;
        }

        MunicipalScenario? scenario = LocalScenario(state, m.CodigoIbge);
        var renewal = new Core.Renewal.RenewalEngine(new Core.Renewal.RenewalContext
        {
            State = state,
            Municipio = m,
            People = People,
            Content = RenewalContent,
            Projection = Projection,
            Themes = World.Parties.Themes.Themes.Select(t => t.Id).ToList(),
            Staff = Campaign.Staff,
            InitialSeats = scenario?.Camara,
            InitialMayorParty = scenario?.Prefeito,
        });
        var world = new WorldEngine(state, new Core.Indicators.IndicatorEvolution(IndicatorModel, m, Projection), renewal);
        if (IndicadoresUf(m.Uf) is { } uf)
        {
            world.EnsureIndicators(uf, Municipios);
        }

        renewal.Ensure();
        return world;
    }

    /// <summary>O mundo do estado, se é o que está montado.</summary>
    private WorldEngine? MundoDe(GameState state) => Mundo is { } world && ReferenceEquals(world.State, state) ? world : null;

    /// <summary>Glossário e artigos da Enciclopédia (F1 em qualquer tela).</summary>
    public Core.Encyclopedia.EncyclopediaContent Encyclopedia { get; private set; } = null!;

    /// <summary>Ritmo do tempo e pausas automáticas (GDD 17.4).</summary>
    public Core.State.TimeConfig Time { get; private set; } = null!;

    /// <summary>F1 abre a Enciclopédia em qualquer tela (princípio j).</summary>
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 })
        {
            UI.Widgets.EncyclopediaDialog.Open(GetTree().Root);
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Projeção da população e das vagas de vereador a cada eleição.</summary>
    public PopulationProjection Projection { get; private set; } = null!;

    /// <summary>Contexto da simulação detalhada do município em foco (GDD 12.5).</summary>
    public Core.Campaign.CampaignContext ContextFor(GameState state)
    {
        Municipio m = Municipios.All.First(x => x.CodigoIbge == state.FocusMunicipio);
        MunicipalScenario? local = LocalScenario(state, m.CodigoIbge);
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
            Debate = Debate,
            Projection = Projection,
            Zonas = ZonasDe(m),
            MaiorEleitoradoUf = MaiorEleitoradoDaUf(m.Uf),
        };
    }

    private readonly Dictionary<string, long> _maiorEleitorado = [];

    /// <summary>Eleitorado do município com mais eleitores da UF (o teto de cabos do vereador depende dele, art. 100-A).</summary>
    private long MaiorEleitoradoDaUf(string uf)
    {
        if (!_maiorEleitorado.TryGetValue(uf, out long maior))
        {
            _maiorEleitorado[uf] = maior = Municipios.All.Where(x => x.Uf == uf).Max(x => x.Eleitorado);
        }

        return maior;
    }

    private readonly Dictionary<string, Core.Territory.ZonasUf> _zonas = [];
    private readonly Dictionary<string, Core.Territory.GeoZonasUf> _geoZonas = [];

    /// <summary>Zonas do município (data/zonas/, GDD 17.5); o arquivo da UF é lido uma vez.</summary>
    public Core.Territory.ZonasMunicipio ZonasDe(Municipio m)
    {
        if (!_zonas.TryGetValue(m.Uf, out Core.Territory.ZonasUf? file))
        {
            file = Core.Territory.ZonasPaths.Load(DataDirectory, m.Uf).GetValueOrThrow();
            _zonas[m.Uf] = file;
        }

        return file.Municipios.First(z => z.CodigoIbge == m.CodigoIbge);
    }

    /// <summary>Contornos das zonas de uma UF (data/geo/zonas/), lidos uma vez.</summary>
    public Core.Territory.GeoZonasUf GeoZonasDe(string uf)
    {
        if (!_geoZonas.TryGetValue(uf, out Core.Territory.GeoZonasUf? file))
        {
            file = Core.Territory.ZonasPaths.LoadGeo(DataDirectory, uf).GetValueOrThrow();
            _geoZonas[uf] = file;
        }

        return file;
    }

    /// <summary>Contexto do governo da cidade (R4) no município em foco.</summary>
    public Core.Governance.GovernanceContext GovernanceContextFor(GameState state)
    {
        Core.Campaign.CampaignContext campaign = ContextFor(state);
        return new Core.Governance.GovernanceContext
        {
            State = state,
            Municipio = campaign.Municipio,
            Content = GovernanceContent,
            Legislature = Legislature,
            Executive = ExecutiveContent,
            People = People,
            Campaign = Campaign,
            Zonas = campaign.Zonas,
            Indicadores = IndicadoresDe(campaign.Municipio),
            IndicatorEvolution = MundoDe(state)?.Indicators,
            Renewal = MundoDe(state)?.Renewal,
        };
    }

    private Core.Governance.GovernanceEngine? NewGoverno(GameState state) =>
        state.Governance is not null && (state.Mandate is not null || state.Executive is not null) ? new Core.Governance.GovernanceEngine(GovernanceContextFor(state)) : null;

    /// <summary>Contexto da prefeitura no município em foco.</summary>
    public Core.Executive.ExecutiveContext ExecutiveContextFor(GameState state)
    {
        Core.Campaign.CampaignContext campaign = ContextFor(state);
        return new Core.Executive.ExecutiveContext
        {
            State = state,
            Municipio = campaign.Municipio,
            Content = ExecutiveContent,
            Legislature = Legislature.Config,
            Electorate = campaign.Electorate,
            RecognitionScale = Campaign.Config.PorteRecognitionMultiplier.GetValueOrDefault(campaign.Municipio.Porte, 1),
            Indicadores = IndicadoresDe(campaign.Municipio),
            IndicatorEvolution = MundoDe(state)?.Indicators,
        };
    }

    private Core.Indicators.IndicatorDefinitions? _realIndicators;

    /// <summary>Unidade, fonte e faixa de cada indicador real (data/indicators/definitions.json); null se o arquivo faltar.</summary>
    public Core.Indicators.IndicatorDefinitions? RealIndicators =>
        _realIndicators ??= JsonData.LoadFile<Core.Indicators.IndicatorDefinitions>(Path.Combine(DataDirectory, Core.Indicators.IndicatorDefinitions.File)).Value;

    private readonly Dictionary<string, Core.Indicators.IndicadoresUfFile?> _indicadores = [];

    /// <summary>Ponto de partida oficial do município (data/indicadores_reais/, GDD 17.17); null se o arquivo da UF faltar.</summary>
    public Core.Indicators.IndicadoresMunicipio? IndicadoresDe(Municipio m) =>
        IndicadoresUf(m.Uf)?.Municipios.FirstOrDefault(i => i.CodigoIbge == m.CodigoIbge);

    /// <summary>Arquivo de indicadores da UF, lido uma vez; null se faltar.</summary>
    private Core.Indicators.IndicadoresUfFile? IndicadoresUf(string uf)
    {
        if (!_indicadores.TryGetValue(uf, out Core.Indicators.IndicadoresUfFile? file))
        {
            file = Core.Indicators.IndicadoresPaths.Load(DataDirectory, uf).Value;
            _indicadores[uf] = file;
        }

        return file;
    }

    /// <summary>Dados reais do município no cenário Brasil 2026; null nos mundos gerados.</summary>
    public MunicipalScenario? LocalScenario(GameState state, int codigoIbge) =>
        state.World?.ScenarioId == "brasil_2026" ? Brasil2026?.Municipio(codigoIbge) : null;

    private readonly Dictionary<string, Core.Geo.GeoUf> _geo = [];

    /// <summary>Contornos dos municípios de uma UF (data/geo/), carregados na primeira vez que forem pedidos.</summary>
    public Core.Geo.GeoUf Geo(string uf)
    {
        if (!_geo.TryGetValue(uf, out Core.Geo.GeoUf? geo))
        {
            geo = Core.Geo.GeoContent.Load(DataDirectory, uf).GetValueOrThrow();
            _geo[uf] = geo;
        }

        return geo;
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

    /// <summary>Contexto do mandato no município em foco, com o mesmo eleitorado da campanha.</summary>
    public Core.Legislature.LegislatureContext LegislatureContextFor(GameState state)
    {
        Core.Campaign.CampaignContext campaign = ContextFor(state);
        return new Core.Legislature.LegislatureContext
        {
            State = state,
            Municipio = campaign.Municipio,
            Content = Legislature,
            People = People,
            Electorate = campaign.Electorate,
            RecognitionScale = Campaign.Config.PorteRecognitionMultiplier.GetValueOrDefault(campaign.Municipio.Porte, 1),
        };
    }

    /// <summary>Começa uma carreira nova: a primeira campanha abre na hora (DECISOES D-019).</summary>
    public void StartCareer(GameState state)
    {
        State = state;

        // Quem não tem 18 anos no prazo do registro da primeira eleição começa fora da disputa (DECISOES D-036).
        int firstElection = Core.Campaign.RaceSetup.NextMunicipalYear(state.Calendar.Year);
        Engine = Core.Career.CareerFlow.MeetsAge(state, Core.Elections.Office.Vereador, firstElection)
            ? Core.Campaign.CampaignEngine.Start(ContextFor(state))
            : null;
        Mandate = null;
        Prefeitura = null;
        Governo = null;
        FoundingEngine = NewFoundingEngine(state);

        // Depois da campanha montada, para a cidade já ter gente (lideranças e equipe) quando o mundo começa a andar.
        Mundo = NewMundo(state);
    }

    /// <summary>Retoma uma carreira salva no ponto em que estava (campanha, mandato ou entressafra).</summary>
    public void Resume(GameState state)
    {
        State = state;

        // Antes dos motores de mandato, que leem o mundo pelo contexto; save antigo ganha aqui indicadores e renovação.
        Mundo = NewMundo(state);
        Engine = state.Campaign is null ? null : Core.Campaign.CampaignEngine.Start(ContextFor(state));
        Mandate = state.Mandate is null ? null : new Core.Legislature.LegislatureEngine(LegislatureContextFor(state));
        Prefeitura = state.Executive is null ? null : new Core.Executive.ExecutiveEngine(ExecutiveContextFor(state));
        Governo = NewGoverno(state);
        FoundingEngine = NewFoundingEngine(state);
    }

    private Core.Career.PartyFoundingEngine NewFoundingEngine(GameState state) =>
        new(state, Founding, World.Parties, Municipios, World.NumberRules,
            state.World?.ScenarioId == "brasil_2026" ? Brasil2026?.National.CamaraValidVotes : null);

    /// <summary>O que aconteceu de especial ao avançar a semana (a tela decide como avisar).</summary>
    public enum WeekNote
    {
        Nenhuma,
        CampanhaAberta,
        SemPartidoNoPrazo,
        SemIdade,
        MandatoEncerrado,
    }

    /// <summary>Avança uma semana da carreira e abre a campanha quando chega o prazo de filiação do ano da eleição.</summary>
    public WeekNote AdvanceWeek()
    {
        GameState state = State!;
        bool hadMandate = state.Mandate is not null || state.Executive is not null;
        Core.Career.CareerFlow.AdvanceWeek(state, Engine, Mandate, FoundingEngine, Prefeitura, Governo, MundoDe(state));
        if (state.Mandate is null)
        {
            Mandate = null;
        }

        if (state.Mandate is null && state.Executive is null)
        {
            Governo = null;
        }

        if (state.Executive is null)
        {
            Prefeitura = null;
        }

        WeekNote note = hadMandate && state.Mandate is null && state.Executive is null ? WeekNote.MandatoEncerrado : WeekNote.Nenhuma;
        if (Core.Career.CareerFlow.ShouldStartCampaign(state))
        {
            if (Core.Career.CareerFlow.CanRun(state, state.Calendar.Year))
            {
                Engine = Core.Campaign.CampaignEngine.Start(ContextFor(state));
                // A presença por zona do mandato vai para a campanha de reeleição.
                Core.Governance.GovernanceEngine.CarryToCampaign(state);
                return WeekNote.CampanhaAberta;
            }

            return Core.Career.CareerFlow.MeetsAge(state, Core.Elections.Office.Vereador, state.Calendar.Year) ? WeekNote.SemPartidoNoPrazo : WeekNote.SemIdade;
        }

        return note;
    }

    /// <summary>O que um dia de jogo trouxe: os compromissos concluídos, se a semana virou e o aviso da carreira.</summary>
    public sealed record DayReport(IReadOnlyList<Core.Campaign.CommitmentResult> Results, bool WeekTurned, WeekNote Note, bool Stopped);

    /// <summary>
    /// Avança um dia (GDD 17.4): primeiro acontecem os compromissos de hoje; se um deles deixou uma decisão esperando
    /// (pedido de liderança), o dia não vira. No domingo roda a semana da carreira inteira (campanha, mandato em
    /// segundo plano, fundação de partido).
    /// </summary>
    public DayReport AdvanceDay()
    {
        GameState state = State!;
        IReadOnlyList<Core.Campaign.CommitmentResult> results = [];
        if (Engine is not null && state.Campaign is { Phase: Core.Campaign.CampaignPhase.PreCampanha or Core.Campaign.CampaignPhase.Campanha })
        {
            results = Engine.RunToday();
            if (Engine.DecisionPending)
            {
                return new DayReport(results, false, WeekNote.Nenhuma, true);
            }
        }

        WeekNote note = WeekNote.Nenhuma;
        bool turned = Clock.AdvanceDay(state, () => note = AdvanceWeek());
        return new DayReport(results, turned, note, false);
    }

    /// <summary>Na pré-campanha, avança até surgir evento, mudar a fase ou acontecer algo na carreira.</summary>
    public WeekNote AdvanceUntilSomethingHappens()
    {
        WeekNote note;
        do
        {
            note = AdvanceWeek();
        }
        while (note == WeekNote.Nenhuma && State!.Campaign is { Phase: Core.Campaign.CampaignPhase.PreCampanha, Debate: null } campaign && campaign.Events.Pending.Count == 0);
        return note;
    }

    /// <summary>
    /// Fora de mandato: avança semana a semana (a fundação de partido segue andando) até a abertura da
    /// pré-campanha da próxima eleição municipal.
    /// </summary>
    public WeekNote SkipToNextElection()
    {
        WeekNote note;
        int guard = 0;
        do
        {
            note = AdvanceWeek();
        }
        while ((note == WeekNote.Nenhuma || (note == WeekNote.SemIdade && SkipBlockedWindow())) && ++guard < 6 * 53);
        return note;
    }

    /// <summary>Sem idade para esta eleição: pula a janela dela, para o salto seguir até a próxima.</summary>
    private bool SkipBlockedWindow()
    {
        GameState state = State!;
        state.Calendar.JumpTo(Core.State.Calendar.ElectionFor(state.Calendar.Year).CampaignStart);
        return true;
    }

    /// <summary>Depois da apuração: posse (se eleito) ou fim do mandato, e o calendário vai a 1º de janeiro.</summary>
    public void ConcludeElection()
    {
        GameState state = State!;
        Core.Career.CareerFlow.ConcludeElection(Engine!.Context, LegislatureContextFor(state), ExecutiveContextFor(state), GovernanceContextFor(state), MundoDe(state)?.Renewal);
        Engine = null;
        Governo = NewGoverno(state);
        Mandate = state.Mandate is null ? null : new Core.Legislature.LegislatureEngine(LegislatureContextFor(state));
        Prefeitura = state.Executive is null ? null : new Core.Executive.ExecutiveEngine(ExecutiveContextFor(state));
    }

    /// <summary>Troca o cargo disputado na pré-campanha; prefeito que disputa outro cargo renuncia.</summary>
    public (Core.Campaign.CandidacyIssue? Issue, bool Renounced) SwitchCandidacy(Core.Elections.Office office)
    {
        var result = Core.Career.CareerFlow.SwitchCandidacy(Engine!.Context, office);
        if (State!.Executive is null)
        {
            Prefeitura = null;
        }

        return result;
    }

    /// <summary>Muda o partido do jogador (troca, filiação depois de expulso ou partido fundado).</summary>
    public void JoinParty(string partyId)
    {
        Core.Career.PartyAffiliation.Join(State!, partyId);
        if (State!.Mandate is null)
        {
            Mandate = null;
        }
    }

    /// <summary>Tela certa para o momento da carreira.</summary>
    public void GoToCareer()
    {
        GameState state = State!;
        GoTo(Core.Career.CareerFlow.Mode(state) switch
        {
            Core.Career.CareerMode.Mandato => Scenes.Mandate,
            Core.Career.CareerMode.Prefeitura => Scenes.Executive,
            Core.Career.CareerMode.ForaDeMandato => Scenes.OutOfOffice,
            _ => state.Campaign?.Outcome is not null ? Scenes.ElectionNight : state.Campaign?.Debate is not null ? Scenes.Debate : Scenes.Hub,
        });
    }

    /// <summary>Nos testes de fumaça da UI, registra a navegação em vez de trocar de cena.</summary>
    public bool SuppressNavigation { get; set; }

    public string? LastNavigation { get; private set; }

    /// <summary>Teste de fumaça: zera a última navegação registrada.</summary>
    public void ResetNavigationForTest() => LastNavigation = null;

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
        public const string Mandate = "res://src/UI/Screens/MandateScreen.tscn";
        public const string OutOfOffice = "res://src/UI/Screens/OutOfOfficeScreen.tscn";
        public const string Executive = "res://src/UI/Screens/ExecutiveScreen.tscn";
        public const string Debate = "res://src/UI/Screens/DebateScreen.tscn";
    }
}
