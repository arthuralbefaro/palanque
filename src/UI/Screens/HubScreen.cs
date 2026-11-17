using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Electorate;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.UI.Map;
using Planalto.UI.Portrait;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Tela central (GDD 17.3, ART_GUIDE seção Interface): barra superior com data e tempo, trilho de áreas, painel da
/// área (ou a ficha de zona, de pessoa ou de compromisso), mapa das zonas sempre visível, feed e intenção de voto,
/// decisões em cartão sobre o mapa. O tempo corre por dia (GDD 17.4): a cada dia acontecem os compromissos marcados
/// na agenda, e o jogo pausa sozinho quando algo pede decisão. Só lê o estado e chama o Core.
/// </summary>
public partial class HubScreen : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly (string Id, string Icon, string Name, string Tip)[] Areas =
    [
        ("campanha", "megafone", "Campanha", "Sua candidatura e os próximos compromissos."),
        ("pessoas", "pessoas", "Pessoas", "A chapa do seu partido, os rivais e as lideranças que você conhece."),
        ("agenda", "calendario", "Agenda", "A semana em dias e turnos: marcar, desmarcar e responder a convites."),
        ("territorio", "barras", "Território", "As zonas da cidade, a sua presença e a demanda de cada uma."),
        ("financas", "cedula", "Finanças", "Caixa, contas da campanha, doadores e pesquisas."),
        ("midia", "jornal", "Mídia", "Os veículos da cidade, o tema em alta, os ataques com prazo e o noticiário."),
        ("partido", "predio", "Partido", "Seu partido nesta eleição."),
    ];

    /// <summary>Dicas de primeira vez (até 40 palavras; GDD 17.1). Tudo o que dizem está também na Enciclopédia.</summary>
    private static readonly Dictionary<string, string> Hints = new(StringComparer.Ordinal)
    {
        ["campanha"] = "A campanha anda por compromissos com dia, turno do dia e zona. Marque na Agenda (terceiro ícone); o resultado de cada um aparece na coluna da direita.",
        ["equipe"] = "Cada função tem de dois a quatro nomes. A qualidade aparece em faixa nas primeiras semanas. Clique num nome para ver o vínculo, a previsão e contratar. Toda pessoa lembra do que você fez com ela.",
        ["verba"] = "A verba é semanal e sai do caixa toda segunda-feira. Escolha o canal, o alvo e o valor: a previsão mostra duas contas, o retorno da sua verba e a disputa com a verba dos rivais no mesmo lugar.",
        ["plataforma"] = "Em cada tema você tem uma posição de −2 a +2. Quem não conhece a sua posição presume a do partido. Mudar o que já é conhecido custa incoerência; a previsão mostra quem gosta e quem não gosta.",
        ["pessoas"] = "Rivais têm plano e memória; lideranças movem votos de um público numa zona. Clique num nome para abrir a ficha. Visitar uma liderança traz o pedido dela: aceitar vira promessa no Livro de compromissos.",
        ["rivais"] = "A ficha mostra o que a sua coordenação consegue ler do plano do rival. Atacar tira imagem dos dois e fica na memória; pacto fecha zonas; investigar pode achar a prova. Veja a previsão antes de confirmar.",
        ["contas"] = "Toda campanha tem limite de gastos e presta contas. Dinheiro \"por fora\" não tem limite, mas vira esqueleto: quem sabe pode contar. O risco das contas decide o julgamento depois da eleição.",
        ["agenda"] = "Cada dia tem três turnos. Clique num turno livre, escolha o compromisso e a zona e veja a previsão antes de confirmar. Repetir o mesmo compromisso na mesma zona rende cada vez menos: é o cansaço.",
        ["territorio"] = "Sua presença muda de zona para zona e cai nas zonas em que você não aparece. Clique numa zona do mapa ou da lista para abrir a ficha.",
        ["ficha_zona"] = "A ficha mostra quem mora aqui, a demanda da zona e quem é mais conhecido. Parte do que você ganha numa zona transborda para as vizinhas. Fazer agora age no próximo turno livre; Agendar para outro dia marca com antecedência.",
        ["financas"] = "O bloco Contas mostra o limite de gastos, o risco e as ofertas de doação à espera de resposta. Pesquisa interna mostra onde você está; institutos publicam sozinhos durante a campanha.",
        ["midia"] = "Cada veículo tem linha editorial: ela decide o tom da notícia, nunca o fato. Clique num veículo para marcar entrevista, sugerir pauta ou anunciar. Tema em alta rende mais naquela semana.",
        ["partido"] = "Seu partido define o número de urna e pesa no voto de quem já gosta dele.",
    };

    /// <summary>O que ocupa o painel da esquerda no lugar da área (ART_GUIDE: ficha abre no painel, com voltar).</summary>
    private enum Sheet
    {
        Nenhuma,
        Zona,
        Pessoa,
        Marcar,
        Compromisso,
        Equipe,
        Membro,
        Verba,
        Canal,
        Plataforma,
        Tema,
        Prometer,
        Rival,
        Atacar,
        Chapa,
        Gente,
        Ataque,
        Veiculo,
        Contas,
        Oferta,
    }

    /// <summary>Fichas que pertencem à área Campanha (o trilho continua marcando a área).</summary>
    private static bool IsCampaignSheet(Sheet sheet) => sheet is Sheet.Equipe or Sheet.Membro or Sheet.Verba or Sheet.Canal or Sheet.Plataforma or Sheet.Tema or Sheet.Prometer;

    private sealed record Notice(string Text, string? Button = null, Action? Action = null, string? Tip = null);

    private readonly List<string> _log = [];
    private readonly Dictionary<string, Dictionary<string, double>> _zoneParties = [];
    private readonly Stack<(Sheet Sheet, string? Id)> _back = new();
    private Game _game = null!;
    private CampaignEngine _engine = null!;
    private HBoxContainer _top = null!;
    private VBoxContainer _trail = null!;
    private VBoxContainer _left = null!;
    private PanelContainer _leftPanel = null!;
    private ScrollContainer _leftScroll = null!;
    private VBoxContainer _you = null!;
    private HBoxContainer _dayRow = null!;
    private VBoxContainer _feed = null!;
    private VBoxContainer _poll = null!;
    private VBoxContainer _objectives = null!;
    private Control _mapArea = null!;
    private ZoneMapView _map = null!;
    private HBoxContainer _modes = null!;
    private VBoxContainer _legend = null!;
    private PanelContainer? _banner;
    private ColorRect? _eventShade;
    private string _area = "campanha";
    private string _mapMode = "forca";
    private Sheet _sheet = Sheet.Nenhuma;
    private string? _sheetId;
    private Notice? _notice;
    private bool _running;
    private int _speed = 1;
    private double _accumulated;
    private ZonasMunicipio? _zonas;
    private HashSet<string> _knownAttacks = [];
    private HashSet<string> _knownOffers = [];

    private GameState State => _game.State!;

    private CampaignState Campaign => State.Campaign!;

    private AgendaState Agenda => Campaign.Agenda;

    private TerritoryState Territory => Campaign.Territory;

    /// <summary>Registro do que o jogador fez na tela, do mais recente ao mais antigo.</summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>Cartão de decisão aberto (testes de fumaça conferem que ele sai).</summary>
    public bool EventOpen => _eventShade is not null && IsInstanceValid(_eventShade);

    public bool Running => _running;

    public ZoneMapView ZoneMap => _map;

    /// <summary>Nome da ficha aberta no painel da esquerda ("Nenhuma", "Zona", "Pessoa", "Marcar", "Compromisso").</summary>
    public string SheetName => _sheet.ToString();

    /// <summary>Largura do painel da área (ART_GUIDE: 400 px lógicos, fixa); o teste de layout confere que nenhum conteúdo o alarga.</summary>
    public float LeftPanelWidth => _leftPanel.Size.X;

    /// <summary>Largura da coluna direita (ART_GUIDE: 400 px lógicos, fixa).</summary>
    public float RightPanelWidth => _objectives.GetParent<Control>().GetParent<Control>().Size.X;

    /// <summary>Texto da faixa de pausa automática, se houver (testes de fumaça).</summary>
    public string? NoticeText => _notice?.Text;

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.State is null)
        {
            _game.GoTo(Game.Scenes.MainMenu);
            return;
        }

        // Sem campanha aberta, a mesma tela mostra o mandato, a prefeitura ou a entressafra (HubScreen.Gov.cs).
        _gov = _game.State.Campaign is null;
        if (_gov)
        {
            _govKind = Kind;
            _area = GovAreas[0].Id;
            _mapMode = Kind == GovKind.Prefeito ? "obras" : "base";
        }
        else
        {
            // Na campanha, a tela abre no painel de zonas (área Território), como na referência da RL.
            _area = "territorio";
        }

        _engine = _game.Engine!;
        if (_engine is not null)
        {
            // Painéis novos por evento do Core (RL): o motor avisa, a tela refaz só eles no quadro seguinte.
            _engine.Changed += OnCampaignChanged;
            CampaignEngine engine = _engine;
            TreeExiting += () => engine.Changed -= OnCampaignChanged;
        }

        GetViewport().GuiFocusChanged += OnFocusChanged;
        TreeExiting += () => GetViewport().GuiFocusChanged -= OnFocusChanged;
        Theme = Palette.CentralTheme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = this.Add(new ColorRect { Color = Palette.Fundo, MouseFilter = MouseFilterEnum.Ignore });
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var root = this.Add(new VBoxContainer());
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 1);
        // Barra de cima em duas linhas: data, tempo, fase, números e menus; embaixo, o relógio e a linha do dia (RL).
        var topRows = Panel(root, new Vector2(0, 56), 8).Add(new VBoxContainer());
        topRows.AddThemeConstantOverride("separation", 6);
        _top = topRows.Add(new HBoxContainer());
        _top.AddThemeConstantOverride("separation", 12);
        _dayRow = topRows.Add(new HBoxContainer { Name = "LinhaDoDia" });
        _dayRow.AddThemeConstantOverride("separation", 12);

        var body = root.Add(new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill });
        body.AddThemeConstantOverride("separation", 1);
        _trail = Panel(body, new Vector2(64, 0), 8).Add(new VBoxContainer());
        _trail.AddThemeConstantOverride("separation", 8);
        // Painel da área: o painel Você fica sempre no alto (RL); embaixo, o conteúdo da área, que rola.
        _leftPanel = Panel(body, new Vector2(400, 0), 16);
        var leftColumn = _leftPanel.Add(new VBoxContainer());
        leftColumn.AddThemeConstantOverride("separation", 12);
        _you = leftColumn.Add(new VBoxContainer { Name = "PainelVoce" });
        _leftScroll = leftColumn.Add(new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill });
        _left = _leftScroll.Add(new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _left.AddThemeConstantOverride("separation", 8);

        _mapArea = body.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(640, 0), ClipContents = true });
        _map = _mapArea.Add(new ZoneMapView());
        _map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _map.ZoneClicked += OnZoneClicked;
        _map.MarkerClicked += id => { if (!_gov) { OpenAreaSheet(Sheet.Rival, id); } };
        _map.HoverText = ZoneTooltip;
        _modes = _mapArea.Add(new HBoxContainer { Position = new Vector2(16, 16) });
        _modes.AddThemeConstantOverride("separation", 8);

        // Legenda: acima da altura da faixa de decisão, para as duas nunca se cobrirem.
        var legendPanel = _mapArea.Add(new PanelContainer { MouseFilter = MouseFilterEnum.Ignore });
        legendPanel.AddThemeStyleboxOverride("panel", Palette.Box(new Color(Palette.Painel, 0.88f), 8, 10, Palette.Linha));
        legendPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        legendPanel.GrowVertical = GrowDirection.Begin;
        legendPanel.OffsetLeft = 16;
        legendPanel.OffsetBottom = -104;
        _legend = legendPanel.Add(new VBoxContainer());
        _legend.AddThemeConstantOverride("separation", 4);
        WireMapLabels(legendPanel);

        // Coluna direita: o feed rola; a intenção de voto fica fixa embaixo (ART_GUIDE, grade de painéis).
        var right = Panel(body, new Vector2(400, 0), 16).Add(new VBoxContainer());
        right.AddThemeConstantOverride("separation", 12);
        var feedScroll = right.Add(new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill });
        _feed = feedScroll.Add(new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _feed.AddThemeConstantOverride("separation", 8);
        right.Add(new ColorRect { Color = Palette.Linha, CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore });
        _objectives = right.Add(new VBoxContainer { Name = "Objetivos" });
        _objectives.AddThemeConstantOverride("separation", 6);
        right.Add(new ColorRect { Color = Palette.Linha, CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore });
        _poll = right.Add(new VBoxContainer());
        _poll.AddThemeConstantOverride("separation", 6);

        // O que já estava de pé ao abrir a tela não pausa de novo; só o que chegar depois.
        RememberDeadlines();
        LoadZones();
        Refresh();
    }

    /// <summary>Guarda os ataques e as ofertas com prazo já avisados, para a pausa valer só para os novos.</summary>
    private void RememberDeadlines()
    {
        // Fora de campanha (mandato, prefeitura, entressafra) não há motor de campanha nem ataque ou oferta com prazo.
        if (_gov || _engine is null)
        {
            _knownAttacks = [];
            _knownOffers = [];
            return;
        }

        _knownAttacks = _engine.PendingAttacks().Select(a => a.Attack.Id).ToHashSet(StringComparer.Ordinal);
        _knownOffers = _engine.Finance.Offers.Select(o => o.Id).ToHashSet(StringComparer.Ordinal);
    }

    private static PanelContainer Panel(Node parent, Vector2 minimum, int margin)
    {
        var panel = parent.Add(new PanelContainer { CustomMinimumSize = minimum });
        panel.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Painel, 0, margin));
        return panel;
    }

    private void LoadZones()
    {
        var municipio = City;
        _zonas = _game.ZonasDe(municipio);
        string prefix = municipio.CodigoIbge.ToString(CultureInfo.InvariantCulture) + "-";
        GeoZonasUf geo = _game.GeoZonasDe(municipio.Uf);
        _map.Load(_zonas, geo.Zonas.Where(z => z.Id.StartsWith(prefix, StringComparison.Ordinal)), geo.Scale);
    }

    // ------------------------------------------------------------------ tempo

    public override void _Process(double delta)
    {
        if (_panelsDirty && !_gov)
        {
            RefreshFromCore();
        }

        if (!_running)
        {
            return;
        }

        if (_gov)
        {
            GovProcess(delta);
            return;
        }

        if (BlockReason() is not null)
        {
            _running = false;
            Refresh();
            return;
        }

        // Relógio do dia (RL): a hora anda no Core; quando um turno fecha, o compromisso dele acontece.
        Game.TickReport tick = _game.TickCampaignClock(delta, _speed);
        UpdateClock();
        if (tick.Results.Count > 0)
        {
            ShowResultFloats(tick.Results);
            Refresh();
        }

        if (tick.DayEnded)
        {
            StepDay();
        }
    }

    /// <summary>Motivo para o tempo não andar (decisão imediata ou fase que se resolve fora do mapa); null se pode.</summary>
    public string? BlockReason() =>
        _gov ? GovBlockReason()
        : Campaign.Events.Pending.Count > 0 ? Tr("há uma decisão esperando você")
        : Agenda.PendingRequest is not null ? Tr("uma liderança fez um pedido e espera a sua resposta")
        : _engine.Team.Pending.Count > 0 ? Tr("alguém da sua equipe espera uma decisão sua")
        : Campaign.Debate is not null ? Tr("há um debate na TV hoje")
        : Campaign.Phase == CampaignPhase.Votacao ? Tr("é dia de eleição: vá à urna")
        : Campaign.Phase == CampaignPhase.Encerrada ? Tr("a eleição terminou: veja a apuração")
        : null;

    /// <summary>
    /// Um dia de jogo: acontecem os compromissos de hoje (cada um vira um cartão no feed) e o dia vira; no domingo
    /// roda a simulação da semana. Depois as pausas automáticas são conferidas (GDD 17.4).
    /// </summary>
    public void StepDay()
    {
        if (_gov)
        {
            StepGovDay();
            return;
        }

        if (BlockReason() is not null)
        {
            _running = false;
            Refresh();
            return;
        }

        int polls = Campaign.Polls.Count;
        CampaignPhase phase = Campaign.Phase;
        FeedItem? lastFeed = Agenda.Feed.LastOrDefault();
        Game.DayReport report = _game.AdvanceDay();
        if (report.WeekTurned)
        {
            SaveManager.Instance.Autosave();
            if (report.Note == Game.WeekNote.MandatoEncerrado)
            {
                _log.Insert(0, Tr("Seu mandato terminou."));
            }

            if (_game.Engine is null || State.Campaign is null)
            {
                _game.GoToCareer();
                return;
            }
        }

        CheckPauses(polls, phase, lastFeed, report);
        ShowResultFloats(report.Results);
        Refresh();
    }

    private void CheckPauses(int polls, CampaignPhase phase, FeedItem? lastFeed, Game.DayReport report)
    {
        var overrides = Settings.PauseOverrides;
        bool Pause(string id) => Clock.ShouldPause(_game.Time, id, overrides);
        void Stop(Notice? notice)
        {
            _running = false;
            _notice ??= notice;
        }

        if ((report.Stopped || BlockReason() is not null) && Pause(Clock.DecisaoImediata))
        {
            Stop(null);
        }

        if (Campaign.Phase != phase && Pause(Clock.FaseNova))
        {
            Stop(new Notice(Campaign.Phase == CampaignPhase.Campanha ? Tr("Começou a campanha oficial: o comício está liberado.") : Tr("A fase da eleição mudou.")));
        }

        if (Campaign.Polls.Count > polls && Pause(Clock.PesquisaPublicada))
        {
            Stop(null);
        }

        // Ataque novo contra o jogador: o prazo de resposta começou a correr (pausa que dá para desligar).
        if (_engine.PendingAttacks().FirstOrDefault(a => !_knownAttacks.Contains(a.Attack.Id)) is { } fresh && Pause(Clock.AtaqueContraVoce))
        {
            string attackId = fresh.Attack.Id;
            Stop(new Notice(AttackBannerText(fresh), "Responder", () => { _notice = null; OpenAreaSheet(Sheet.Ataque, attackId); },
                Palette.Tip(Tr("Abre as respostas ao ataque."), Tr("mostra o ataque e cada resposta possível com a previsão: quanto do dano sobra, em quantas semanas se apaga e o custo."), Tr("nada para abrir; o prazo corre se você soltar o tempo."))));
        }

        // Oferta de doação nova: decisão com prazo, pausa ao chegar.
        if (_engine.Finance.Offers.FirstOrDefault(o => !_knownOffers.Contains(o.Id)) is { } offered && Pause(Clock.DecisaoComPrazo))
        {
            string offerId = offered.Id;
            Stop(new Notice(string.Format(Tr("{0} fez uma oferta de doação: responda até {1}."), _engine.NameOf(offered.DonorId), offered.Deadline.ToString("dddd, d/MM", PtBr)), "Decidir", () => { _notice = null; OpenAreaSheet(Sheet.Oferta, offerId); },
                Palette.Tip(Tr("Abre a oferta de doação."), Tr("mostra cada resposta com a previsão: o dinheiro que entra, o risco das contas, a dívida e quem fica sabendo."), Tr("nada para abrir; o prazo corre se você soltar o tempo."))));
        }

        RememberDeadlines();

        // Contas da eleição anterior: julgamento ou representação com novidade nesta semana.
        if (report.WeekTurned && _game.AccountsNews.Count > 0 && Pause(Clock.DecisaoImediata))
        {
            Stop(new Notice(string.Join(". ", _game.AccountsNews.Select(h => h.Text)) + ".", "Ver as contas", () => { _notice = null; OpenAreaSheet(Sheet.Contas, null); },
                Palette.Tip(Tr("Abre o painel Contas."), Tr("mostra o andamento do julgamento das contas da eleição anterior e da representação, se houver."), Tr("nada."))));
        }

        // O que entrou no feed desde ontem: liderança perdida e rival em zona onde o jogador lidera a presença.
        int from = lastFeed is null ? 0 : Agenda.Feed.FindLastIndex(f => ReferenceEquals(f, lastFeed)) + 1;
        foreach (FeedItem item in Agenda.Feed.Skip(from))
        {
            if (item.Kind == "lideranca_perdida" && Pause(Clock.LiderancaPerdida))
            {
                string? person = item.PersonId;
                Stop(new Notice(item.Text + ".", person is null ? null : "Abrir a ficha", () => OpenSheet(Sheet.Pessoa, person),
                    Palette.Tip(Tr("Abre a ficha dessa pessoa."), Tr("mostra a relação, as lembranças e o pedido dela."), Tr("nada."))));
            }
            else if (item.Kind == Clock.CaixaBaixo && Pause(Clock.CaixaBaixo))
            {
                Stop(new Notice(item.Text + ".", "Ver a equipe e a folha", () => OpenCampaignSheet(Sheet.Equipe, null),
                    Palette.Tip(Tr("Abre a Equipe, com a folha da semana."), Tr("mostra quem recebe quanto e quantas semanas o caixa paga; salário atrasado derruba a lealdade de todos."), Tr("nada."))));
            }
            else if (item.Kind == "rival_zona" && item.ZoneId is { } zone && _engine.PlayerLeadsPresence(Territory.IndexOf(zone)) && Pause(Clock.RivalEmZonaLider))
            {
                Stop(new Notice(string.Format(Tr("{0}, zona em que você lidera a presença."), item.Text), "Ver a zona", () => { _map.CenterOn(zone); _map.Select(zone); OpenSheet(Sheet.Zona, zone); },
                    Palette.Tip(Tr("Mostra a zona no mapa e abre a ficha dela."), Tr("você vê a presença de cada um ali e pode marcar um compromisso."), Tr("nada."))));
            }
        }

        if (report.WeekTurned)
        {
            decimal cheapest = _engine.Context.Campaign.Commitments.Tipos.Where(t => t.AllowedIn(Campaign.Phase)).Select(_engine.CostOf).Where(c => c > 0).DefaultIfEmpty(0).Min();
            if (State.Player.Resources.Money < cheapest && Pause(Clock.CaixaBaixo))
            {
                Stop(new Notice(string.Format(Tr("O caixa ({0}) não paga nem o compromisso mais barato ({1})."), Money(State.Player.Resources.Money), Money(cheapest))));
            }
        }

        // Domingo: a semana que vem ainda está vazia.
        DateOnly nextWeek = State.Calendar.WeekStart.AddDays(7);
        if (State.Time.Day == 6 && Campaign.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha && nextWeek < Campaign.ElectionDate
            && _engine.ScheduledIn(nextWeek, nextWeek.AddDays(6)) == 0 && Pause(Clock.SemanaSemCompromisso))
        {
            Stop(new Notice(Tr("A semana que vem não tem nenhum compromisso marcado."), "Abrir a agenda", () => { _weekOffset = 1; OpenArea("agenda"); },
                Palette.Tip(Tr("Abre a Agenda na semana que vem."), Tr("você marca os compromissos da semana; sem nenhum, a sua presença só cai."), Tr("nada."))));
        }
    }

    private void SetSpeed(int speed)
    {
        _speed = speed;
        _running = BlockReason() is null;
        if (_running)
        {
            _notice = null;
        }

        _accumulated = 0;
        Refresh();
    }

    // ------------------------------------------------------------------ navegação do painel

    /// <summary>Mostra uma área do trilho (fecha a ficha aberta).</summary>
    public void OpenArea(string area)
    {
        _area = area;
        _sheet = Sheet.Nenhuma;
        _sheetId = null;
        _draft = null;
        _confirmFire = null;
        _changes = null;
        _back.Clear();
        ResetGovSheet();
        Refresh();
    }

    /// <summary>Abre uma tela inteira da área Campanha (Equipe, Verba, Plataforma) a partir de qualquer lugar.</summary>
    private void OpenCampaignSheet(Sheet sheet, string? id)
    {
        _area = "campanha";
        _back.Clear();
        _sheet = Sheet.Nenhuma;
        _draft = null;
        OpenSheet(sheet, id);
    }

    private void OpenSheet(Sheet sheet, string? id)
    {
        if (_sheet != Sheet.Nenhuma && _sheet != Sheet.Marcar && (_sheet != sheet || _sheetId != id))
        {
            _back.Push((_sheet, _sheetId));
        }

        _sheet = sheet;
        _sheetId = id;
        _confirmFire = null;
        _positionDraft = null;
        _positionResult = null;
        _changes = null;
        if (sheet == Sheet.Marcar)
        {
            // Marcar pede escolhas em listas: o tempo para enquanto a marcação está aberta.
            _running = false;
        }

        Refresh();
    }

    private void Back()
    {
        _draft = null;
        _confirmFire = null;
        _positionDraft = null;
        _positionResult = null;
        _changes = null;
        (_sheet, _sheetId) = _back.Count > 0 ? _back.Pop() : (Sheet.Nenhuma, null);
        _map.Select(_sheet == Sheet.Zona ? _sheetId : null);
        Refresh();
    }

    private void OnZoneClicked(string? id)
    {
        if (GovZoneClicked(id))
        {
            return;
        }

        if (_sheet == Sheet.Marcar && _draft is not null)
        {
            // Com a marcação aberta, o clique no mapa escolhe a zona do compromisso.
            if (id is not null && TypeOf(_draft).ExigeZona && !TypeOf(_draft).SoEvento)
            {
                SetDraftZone(id);
            }

            _map.Select(_draft.ZoneId);
            Refresh();
            return;
        }

        if (_sheet == Sheet.Canal && id is not null && _sheetId is not null && _engine.ChannelTargets(_sheetId).Any(t => t.Id == id))
        {
            // Com um canal de zona aberto, o clique no mapa escolhe o alvo da verba.
            SetChannelTarget(_sheetId, id);
            _map.Select(id);
            Refresh();
            return;
        }

        if (id is null)
        {
            if (_sheet == Sheet.Zona)
            {
                Back();
            }
            else
            {
                if (_area == "territorio" && _sheet == Sheet.Nenhuma)
                {
                    _selectedZone = null;
                }

                Refresh();
            }

            return;
        }

        // Painel de zonas (RL): o clique no mapa destaca a linha da zona e a lista rola até ela; a ficha flutua no mapa.
        _selectedZone = id;
        _scrollToZone = _area == "territorio";
        _back.Clear();
        _sheet = Sheet.Zona;
        _sheetId = id;
        Refresh();
    }

    // ------------------------------------------------------------------ montagem

    public void Refresh()
    {
        if (_top is null)
        {
            return;
        }

        SyncMode();
        if (_gov)
        {
            // A linha do dia e o painel Você são da campanha; o mandato tem a barra própria.
            _dayRow.Visible = false;
            _you.Visible = false;
            RefreshGov();
            return;
        }

        BuildTop();
        BuildDayLine();
        BuildYou();
        BuildTrail();
        BuildLeft();
        BuildMap();
        BuildWindows();
        BuildRight();
        BuildObjectives();
        BuildBanner();
        ShowPendingDecision();
        _panelsDirty = false;
    }

    private string Money(decimal value) => string.Create(PtBr, $"R$ {value:N0}");

    private static string Signed(double value, string format) => (value >= 0 ? "+" : "−") + Math.Abs(value).ToString(format, PtBr);

    private string Range(double min, double max, string format) =>
        Signed(min, format) == Signed(max, format) ? Signed(min, format) : $"{Signed(min, format)} {Tr("a")} {Signed(max, format)}";

    private string SignedMoney(decimal value) => (value >= 0 ? "+" : "−") + Money(Math.Abs(value));

    /// <summary>Multiplicador como variação: 1,30 vira "+30%"; 0,45 vira "−55%".</summary>
    private static string Percent(double multiplier) => Signed((multiplier - 1) * 100, "0") + "%";

    private static string DayShort(DateOnly date) => date.ToString("ddd d", PtBr).Replace(".", "", StringComparison.Ordinal);

    private static string DayLong(DateOnly date) => date.ToString("dddd, d 'de' MMMM", PtBr);

    private string TurnoName(Turno turno) => turno switch
    {
        Turno.Manha => Tr("manhã"),
        Turno.Tarde => Tr("tarde"),
        _ => Tr("noite"),
    };

    private string ZoneName(string? id) => id is null ? "" : _engine.ZoneName(id);

    private string PartySigla(string partyId) => State.Party(partyId)?.Sigla ?? partyId;

    /// <summary>Nome fictício sempre com o que é (princípio j): "Fulana (candidata a vereadora, SIGLA)".</summary>
    private string CandidateLabel(Candidate c)
    {
        string office = _game.People.Offices.Name(c.Office, c.Gender).ToLowerInvariant();
        string role = c.Gender switch
        {
            Gender.Feminino => string.Format(Tr("candidata a {0}"), office),
            Gender.Masculino => string.Format(Tr("candidato a {0}"), office),
            _ => string.Format(Tr("candidatura a {0}"), office),
        };
        return $"{c.Name} ({role}, {PartySigla(c.PartyId)})";
    }

    private static RichTextLabel Heading(Node parent, string text, int size = 22) =>
        Glossary.Rich(parent, text, size, Palette.Texto, Palette.Weight(Palette.Inter, 600));

    /// <summary>Título com nome próprio (zona, pessoa): sem links de glossário.</summary>
    private static RichTextLabel NameTitle(Node parent, string text, int size = 24) =>
        Glossary.Plain(parent, text, size, Palette.Texto, Palette.Weight(Palette.Inter, 600));

    private static PanelContainer Card(Node parent, Color? background = null, Color? border = null, int margin = 12)
    {
        var card = parent.Add(new PanelContainer());
        card.AddThemeStyleboxOverride("panel", Palette.Box(background ?? Palette.Cartao, 8, margin, border));
        return card;
    }

    /// <summary>Cartão clicável (linha de lista ou item do feed): fundo mais claro sob o mouse, tooltip em três partes.</summary>
    private PanelContainer ClickCard(Node parent, Action onClick, string tip, string? name = null)
    {
        var card = parent.Add(new PanelContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = tip, MouseDefaultCursorShape = CursorShape.PointingHand });
        if (name is not null)
        {
            card.Name = name;
        }

        card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Cartao, 8, 12));
        card.MouseEntered += () => card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 8, 12));
        card.MouseExited += () => card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Cartao, 8, 12));
        card.GuiInput += e =>
        {
            // Clique num termo sublinhado abre o verbete, não o cartão.
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } && !EncyclopediaDialog.IsOpen(GetTree().Root))
            {
                onClick();
            }
        };
        return card;
    }

    private Button IconButton(Node parent, string icon, bool pressed, Action onPressed, string tip)
    {
        var b = parent.Add(new Button
        {
            Icon = Palette.Icon(icon),
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(48, 44),
            ToggleMode = true,
            ButtonPressed = pressed,
            TooltipText = tip,
        });
        b.Pressed += onPressed;
        return b;
    }

    private void BackButton()
    {
        var b = Ui.Button(_left, "Voltar", Back, Palette.Tip(Tr("Fecha esta ficha."), Tr("volta ao que estava aberto antes no painel."), Tr("nada.")));
        b.Name = "Voltar";
        b.Icon = Palette.Icon("voltar");
        b.ExpandIcon = true;
        b.Alignment = HorizontalAlignment.Left;
        b.CustomMinimumSize = new Vector2(132, 44);
        b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        ChangesCard();
    }

    private void BuildTop()
    {
        Ui.Clear(_top);
        DateOnly today = Clock.Today(State);
        Label date = Ui.Value(_top, today.ToString("ddd, d MMM yyyy", PtBr), 24);
        date.AddThemeFontOverride("font", Palette.Weight(Palette.Oswald, 500));
        date.AddThemeColorOverride("font_color", Palette.Texto);
        date.CustomMinimumSize = new Vector2(190, 0);
        date.MouseFilter = MouseFilterEnum.Stop;
        date.TooltipText = Palette.Tip(Tr("Data do jogo. O dia é a unidade do tempo."), Tr("a cada dia acontecem os compromissos marcados; a cada segunda-feira a cidade se mexe: rivais, lideranças, pesquisas e eventos."), Tr("nada."));

        string? block = BlockReason();
        var pause = IconButton(_top, "pausa", !_running, () => { _running = false; Refresh(); },
            Palette.Tip(Tr("Pausa o tempo."), Tr("o dia para; você pode olhar o mapa, marcar compromissos e decidir com calma."), Tr("nada.")));
        pause.Modulate = !_running && (block is not null || _notice is not null) ? Palette.Atencao : Colors.White;
        pause.Name = "Pausa";
        foreach (int speed in _game.Time.Velocidades)
        {
            double seconds = Clock.SecondsPerDay(_game.Time, Clock.ModoCampanha, speed);
            string tip = Palette.Tip(
                string.Format(Tr("Tempo em velocidade {0}x."), speed),
                string.Format(PtBr, Tr("cada dia de jogo passa em {0:0.#} s; o jogo para sozinho quando algo pede decisão."), seconds),
                Tr("nada."));
            var b = Ui.Button(_top, $"{speed}x", () => SetSpeed(speed), tip);
            b.ToggleMode = true;
            b.ButtonPressed = _running && _speed == speed;
            b.CustomMinimumSize = new Vector2(56, 44);
            if (block is not null)
            {
                b.Disabled = true;
                b.TooltipText = Palette.Blocked(string.Format(Tr("{0}. Resolva antes de soltar o tempo."), block), tip);
            }
        }

        string phase = Campaign.Phase switch
        {
            CampaignPhase.PreCampanha => Tr("Pré-campanha"),
            CampaignPhase.Campanha => Tr("Campanha"),
            CampaignPhase.Votacao => Tr("Dia da eleição"),
            _ => Tr("Eleição encerrada"),
        };
        var phaseLabel = Glossary.Rich(_top, $"{phase} · {_engine.Context.Municipio.Nome} ({_engine.Context.Municipio.Uf}) · " + string.Format(Tr("{0} sem. até a eleição"), _engine.WeeksToElection), 18);
        phaseLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        phaseLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        phaseLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        phaseLabel.ClipContents = true;

        // Caixa, estresse, reconhecimento e imagem ficam no painel Você, sempre visível (RL): a barra de cima fica com
        // data, relógio, tempo, fase e a contagem até a eleição.
        Ui.Button(_top, "Mapa", () => _game.GoTo(Game.Scenes.Map), Palette.Tip(Tr("Mapa do seu estado em 2.5D, com a força dos partidos por município."), Tr("abre o mapa do estado; Voltar retorna a esta tela."), Tr("nada; o jogo fica pausado.")));
        Ui.Button(_top, "Salvar", ShowSaveMenu, Palette.Tip(Tr("Grava a carreira."), Tr("salva num dos slots manuais (no Ironman, no save único)."), Tr("nada.")));
        Ui.Button(_top, "Menu", () => { SaveManager.Instance.Autosave(); _game.GoTo(Game.Scenes.MainMenu); }, Palette.Tip(Tr("Volta ao menu principal."), Tr("salva automaticamente e sai da carreira."), Tr("nada.")));
        OptionsButton();
        IconButton(_top, "ajuda", false, () => EncyclopediaDialog.Open(GetTree().Root), Palette.Tip(Tr("Enciclopédia (F1)."), Tr("explica termos, regras eleitorais reais, quem é quem neste mundo e o seu Livro de compromissos."), Tr("nada; o jogo fica pausado.")));
    }

    /// <summary>Opções (aba Tempo) na barra de cima: pausa o tempo, abre por cima da tela e redesenha ao fechar.</summary>
    private void OptionsButton()
    {
        var options = Ui.Button(_top, "Opções", () =>
        {
            _running = false;
            Refresh();
            OptionsDialog.Open(GetTree().Root, Refresh);
        }, Palette.Tip(Tr("Abre as Opções."), Tr("na aba Tempo, liga ou desliga cada pausa automática e religa as dicas de primeira vez; o tempo para enquanto as Opções estão abertas."), Tr("nada.")));
        options.Name = "AbrirOpcoes";
    }

    private void Stat(string caption, string value, string what, string does, Color? color = null)
    {
        var box = _top.Add(new VBoxContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = Palette.Tip(what, does, Tr("é um resultado, não um custo.")) });
        box.AddThemeConstantOverride("separation", -4);
        box.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Label top = Ui.Label(box, caption, 18);
        top.AddThemeColorOverride("font_color", Palette.Texto3);
        top.MouseFilter = MouseFilterEnum.Pass;
        Label label = Ui.Value(box, value, 20);
        label.AddThemeColorOverride("font_color", color ?? Palette.Texto);
        label.MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>
    /// Imagem na barra de cima: a faixa nomeada (data/campaign/config.json) e uma régua com uma casa por faixa; o
    /// número cru fica só no tooltip, com os limites da faixa. Serve à campanha, ao mandato e à prefeitura;
    /// <paramref name="effect"/> diz o que a imagem faz em cada momento.
    /// </summary>
    private void ImageStat(double image, string effect)
    {
        CampaignConfig config = _game.Campaign.Config;
        int index = config.ImageBandIndex(image);
        int side = config.ImageBandSide(index);
        var (from, to) = config.ImageBandRange(index);
        Color color = side > 0 ? Palette.Positivo : side < 0 ? Palette.Negativo : Palette.Texto;
        string tip = Palette.Tip(
            Tr("Imagem: como a cidade vê você, numa régua de −1 a +1."),
            string.Format(Tr("hoje {0}, na faixa \"{1}\" (de {2} a {3}); {4}"), Signed(image, "0.00"), config.ImageScale[index].Name, Signed(from, "0.00"), Signed(to, "0.00"), effect),
            Tr("é um resultado, não um custo."));
        var box = _top.Add(new VBoxContainer { Name = "Imagem", MouseFilter = MouseFilterEnum.Stop, TooltipText = tip });
        box.AddThemeConstantOverride("separation", -4);
        box.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Label caption = Ui.Label(box, "como te veem", 18);
        caption.AddThemeColorOverride("font_color", Palette.Texto3);
        caption.MouseFilter = MouseFilterEnum.Pass;
        var row = box.Add(new HBoxContainer { MouseFilter = MouseFilterEnum.Pass });
        row.AddThemeConstantOverride("separation", 8);
        var ruler = row.Add(new HBoxContainer { Name = "Regua", MouseFilter = MouseFilterEnum.Pass, SizeFlagsVertical = SizeFlags.ShrinkCenter });
        ruler.AddThemeConstantOverride("separation", 3);
        for (int i = 0; i < config.ImageScale.Count; i++)
        {
            ruler.Add(new ColorRect { Color = i == index ? color : new Color(Palette.Texto3, 0.5f), CustomMinimumSize = new Vector2(8, i == index ? 18 : 10), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        }

        // Cor de significado vem sempre com sinal (ART_GUIDE): seta para cima ou para baixo antes do nome da faixa.
        string sign = side > 0 ? "▲ " : side < 0 ? "▼ " : "";
        Label value = Ui.Value(row, sign + config.ImageScale[index].Name, 20);
        value.Name = "Faixa";
        value.AddThemeColorOverride("font_color", color);
        value.MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>Aviso visível quando o estresse passa do limite: o que ele causa e como baixar.</summary>
    private void StressWarning()
    {
        AgendaRules rules = _engine.Context.Campaign.Commitments.Regras;
        int stress = State.Player.Resources.Stress;
        if (stress <= rules.EstresseLimite)
        {
            return;
        }

        PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Negativo, 10);
        card.Name = "AvisoEstresse";
        string tested = string.Join(", ", _engine.Context.Campaign.Commitments.Tipos.Where(t => t.Teste is not null).Select(t => t.Nome));
        Glossary.Rich(card, string.Format(PtBr, Tr("Estresse em {0} (limite: {1}). Acima do limite, a chance de você se sair bem cai {2:0} pontos nos compromissos que dependem disso ({3}). Marque Descanso e família na Agenda para baixar."), stress, rules.EstresseLimite, rules.EstressePenalidade * 100, tested), 18, Palette.Texto);
    }

    private void BuildTrail()
    {
        Ui.Clear(_trail);
        foreach (var (id, icon, name, tip) in Areas)
        {
            var b = IconButton(_trail, icon, _area == id && (_sheet == Sheet.Nenhuma || SheetArea(_sheet) == id), () => OpenArea(id), Palette.Tip($"{Tr(name)}: {Tr(tip)}", Tr("mostra esta área no painel da esquerda; o mapa continua visível."), Tr("nada.")));
            b.Name = "Area_" + id;
        }

        GovTrailExtra();
    }

    // ------------------------------------------------------------------ painel da área

    private void BuildLeft()
    {
        Ui.Clear(_left);
        if (BuildGovLeft())
        {
            return;
        }

        switch (_sheet)
        {
            case Sheet.Zona when _sheetId is not null && _map.Zonas.ContainsKey(_sheetId):
                // A ficha da zona flutua sobre o mapa (HubScreen.Windows.cs); o painel mostra a área por baixo.
                break;
            case Sheet.Marcar when ScheduleFloats:
                break;
            case Sheet.Pessoa when _sheetId is not null && _engine.City.Leader(_sheetId) is { } leader:
                BuildPersonSheet(leader);
                return;
            case Sheet.Marcar when _draft is not null:
                BuildScheduleSheet(_draft);
                return;
            case Sheet.Compromisso when _sheetId is not null && BuildCommitmentSheet(_sheetId):
                return;
            case Sheet.Equipe:
                BuildTeamSheet();
                return;
            case Sheet.Membro when _sheetId is not null && _engine.Staff(_sheetId) is { } member:
                BuildMemberSheet(member);
                return;
            case Sheet.Verba:
                BuildBudgetSheet();
                return;
            case Sheet.Canal when _sheetId is not null && _engine.Context.Campaign.Channels.Find(_sheetId) is { } channel:
                BuildChannelSheet(channel);
                return;
            case Sheet.Plataforma:
                BuildPlatformSheet();
                return;
            case Sheet.Tema when _sheetId is not null && _engine.Context.Themes.Themes.FirstOrDefault(t => t.Id == _sheetId) is { } theme:
                BuildThemeSheet(theme);
                return;
            case Sheet.Prometer:
                BuildPromiseSheet();
                return;
            case Sheet.Rival when _sheetId is not null && _engine.RivalSheetOf(_sheetId) is { } rivalSheet:
                BuildRivalSheet(rivalSheet);
                return;
            case Sheet.Atacar when _sheetId is not null && _engine.RivalSheetOf(_sheetId) is not null:
                BuildAttackSheet(_sheetId);
                return;
            case Sheet.Chapa:
                BuildSlateSheet();
                return;
            case Sheet.Gente when _sheetId is not null && _engine.City.Person(_sheetId) is { } anyone:
                BuildGenericPersonSheet(anyone);
                return;
            case Sheet.Ataque when _sheetId is not null:
                BuildAttackResponseSheet(_sheetId);
                return;
            case Sheet.Veiculo when _sheetId is not null && _engine.Outlets().FirstOrDefault(o => o.Outlet.Id == _sheetId) is { } outletView:
                BuildOutletSheet(outletView);
                return;
            case Sheet.Contas:
                BuildAccountsSheet();
                return;
            case Sheet.Oferta when _sheetId is not null:
                BuildOfferSheet(_sheetId);
                return;
            case Sheet.Nenhuma:
                break;
            default:
                // A ficha perdeu o alvo (compromisso concluído e fora do registro, por exemplo): volta à área.
                _sheet = Sheet.Nenhuma;
                _sheetId = null;
                _back.Clear();
                break;
        }

        if (BuildGovLeft())
        {
            return;
        }

        Heading(_left, Tr(Areas.First(a => a.Id == _area).Name));
        FirstTimeHint(_area);
        ChangesCard();
        StressWarning();
        switch (_area)
        {
            case "pessoas": BuildPeople(); break;
            case "agenda": BuildAgendaArea(); break;
            case "territorio": BuildTerritory(); break;
            case "financas": BuildFinances(); break;
            case "midia": BuildMedia(); break;
            case "partido": BuildParty(); break;
            default: BuildCampaign(); break;
        }
    }

    private void FirstTimeHint(string key)
    {
        if (!Settings.HintsEnabled || Settings.HintSeen(key) || !Hints.TryGetValue(key, out string? text))
        {
            return;
        }

        PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Acao);
        card.Name = "Dica_" + key;
        var col = Ui.Column(card, 8);
        Glossary.Rich(col, Tr(text), 18, Palette.Texto);
        var row = Ui.Row(col, 8);
        Ui.Button(row, "Entendi", () => { Settings.MarkHintSeen(key); Refresh(); }, Palette.Tip(Tr("Fecha esta dica."), Tr("ela não volta sozinha (dá para religar as dicas em Opções); tudo o que ela diz está na Enciclopédia (F1)."), Tr("nada.")));
        Ui.Button(row, "Sem dicas", () => { Settings.DisableHints(); Refresh(); }, Palette.Tip(Tr("Desliga todas as dicas de primeira vez."), Tr("nenhuma dica volta, a não ser que você as religue em Opções."), Tr("nada.")));
    }

    private void BuildCampaign()
    {
        Office office = Campaign.Player.Office;
        string current = _game.People.Offices.Name(office, State.Player.Gender);
        Glossary.Rich(_left, string.Format(Tr("Candidatura: {0} · {1} · número {2}"), current, PartySigla(Campaign.Player.PartyId), Campaign.Player.Number), 18, Palette.Texto);
        if (State.Mandate is not null)
        {
            Glossary.Rich(_left, Tr("Campanha de reeleição: durante a campanha, seu mandato vota seguindo a orientação do partido."));
        }

        if (Campaign.Phase == CampaignPhase.PreCampanha)
        {
            Office other = office == Office.Prefeito ? Office.Vereador : Office.Prefeito;
            string otherName = _game.People.Offices.Name(other, State.Player.Gender).ToLowerInvariant();
            bool renounces = State.Executive is not null && other != Office.Prefeito;
            string tip = Palette.Tip(string.Format(Tr("Troca a candidatura para {0}."), otherName), Tr("muda a disputa, os rivais e as pesquisas; só até 16 de agosto."), Tr(renounces ? "a prefeitura: quem é prefeito precisa renunciar para disputar outro cargo." : "nada."));
            var b = Ui.Button(_left, string.Format(Tr(renounces ? "Disputar para {0} (renuncia)" : "Disputar para {0}"), otherName), () =>
            {
                var (issue, renounced) = _game.SwitchCandidacy(other);
                _log.Insert(0, issue switch
                {
                    CandidacyIssue.IdadeMinima => Tr("Você não terá a idade mínima para o cargo na data da posse."),
                    CandidacyIssue.ForaDoPrazo => Tr("A troca de candidatura só vale na pré-campanha."),
                    _ => renounced ? Tr("Você renunciou à prefeitura para disputar outro cargo.") : string.Format(Tr("Agora você disputa para {0}."), otherName),
                });
                Refresh();
            }, tip);
            if (RaceSetup.CanRunFor(_engine.Context, other) == CandidacyIssue.IdadeMinima)
            {
                b.Disabled = true;
                b.TooltipText = Palette.Blocked(Tr("você não terá a idade mínima para o cargo na data da posse."), tip);
            }
        }

        BuildTeamBlock();
        BuildBudgetBlock();
        BuildPlatformBlock();

        Heading(_left, Tr("Próximos compromissos"), 20);
        var next = Agenda.Scheduled.OrderBy(c => c.Date).ThenBy(c => c.Turno).Take(3).ToList();
        if (next.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhum compromisso marcado. Sem compromisso, a sua presença cai um pouco toda semana em todas as zonas."));
        }

        foreach (Commitment c in next)
        {
            CommitmentRow(c);
        }

        var open = Ui.Button(_left, "Abrir a agenda", () => OpenArea("agenda"), Palette.Tip(Tr("Abre a Agenda da semana."), Tr("mostra os sete dias e os três turnos de cada um, para marcar e desmarcar."), Tr("nada.")));
        Palette.Primary(open);

        if (_log.Count > 0)
        {
            Heading(_left, Tr("O que você fez"), 20);
            foreach (string line in _log.Take(6))
            {
                Glossary.Rich(_left, "· " + line);
            }
        }
    }

    /// <summary>Linha de um compromisso marcado: o quê e onde à esquerda, quando à direita; abre o detalhe.</summary>
    private void CommitmentRow(Commitment c)
    {
        CommitmentType type = _engine.Context.Campaign.Commitments.Get(c.Type);
        string where = c.ZoneId is null ? "" : " · " + ZoneName(c.ZoneId);
        string when = $"{DayShort(c.Date)}, {TurnoName(c.Turno)}";
        var b = Ui.Button(_left, $"{type.Nome}{where}\n{when}", () => OpenSheet(Sheet.Compromisso, c.Id),
            Palette.Tip(string.Format(Tr("{0}, {1}."), CommitmentTitle(c), when), Tr("abre o compromisso: previsão de hoje e a opção de desmarcar."), CostText(type)));
        b.Alignment = HorizontalAlignment.Left;
        b.ClipText = true;
        Palette.ListRow(b);
    }

    /// <summary>Pesquisas e custo dos compromissos (o resto da área Finanças; o bloco Contas vem antes).</summary>
    private void BuildPollsBlock()
    {
        Heading(_left, Tr("Pesquisas"), 20);
        Glossary.Rich(_left, Tr("Encomende uma pesquisa (sai na hora):"));
        foreach (PollsterDefinition p in _game.Electorate.Pollsters.Pollsters.Where(p => p.Internal || p.HouseBias == 0))
        {
            decimal cost = Math.Round(p.Cost * (decimal)_engine.Context.Config.PorteCostMultiplier.GetValueOrDefault(_engine.Context.Municipio.Porte, 1) / 100) * 100;
            string tip = Palette.Tip(
                string.Format(Tr("{0} ({1})."), p.Name, Tr(p.Internal ? "pesquisa interna da sua campanha" : "instituto de pesquisa")),
                string.Format(PtBr, Tr("mostra a intenção de voto na cidade, com amostra de {0} a {1} entrevistas; sai na hora."), p.SampleMin, p.SampleMax),
                Money(cost) + ".");
            var b = Ui.Button(_left, $"{p.Name} · {Money(cost)}", () =>
            {
                if (_engine.CommissionPoll(p.Id) is null)
                {
                    _log.Insert(0, Tr("Sem dinheiro para a pesquisa."));
                }

                Refresh();
            }, tip);
            b.ClipText = true;
            b.Disabled = State.Player.Resources.Money < cost || Campaign.Phase is CampaignPhase.Votacao or CampaignPhase.Encerrada;
            if (b.Disabled)
            {
                b.TooltipText = Palette.Blocked(string.Format(Tr("faltam {0}."), Money(Math.Max(0, cost - State.Player.Resources.Money))), tip);
            }
        }

        foreach (PollRecord poll in Campaign.Polls.TakeLast(3).Reverse())
        {
            PollsterDefinition pollster = _game.Electorate.Pollsters.Get(poll.PollsterId);
            Glossary.Rich(_left, string.Format(PtBr, Tr("{0} · {1:dd/MM} · {2} entrevistas · margem de erro de {3:0.0} pontos · não sabem: {4:0.0}%"), pollster.Name, poll.Week, poll.SampleSize, poll.MarginOfError, poll.Undecided));
        }

        Heading(_left, Tr("Custo dos compromissos"), 20);
        foreach (CommitmentType type in _engine.Context.Campaign.Commitments.Tipos)
        {
            Glossary.Rich(_left, $"· {type.Nome}: {CostText(type)}");
        }
    }

    private void BuildParty()
    {
        GameParty? party = State.Party(State.Player.PartyId);
        if (party is null)
        {
            Glossary.Rich(_left, Tr("Você está sem partido."));
            return;
        }

        string family = _game.World.Parties.Families.Families.FirstOrDefault(f => f.Id == party.FamilyId)?.Name ?? party.FamilyId;
        NameTitle(_left, $"{party.Sigla} · {party.Number}", 22);
        Glossary.Rich(_left, string.Format(Tr("{0}. Família ideológica: {1}. Partido fictício."), party.Name, family));
        int named = Campaign.ForOffice(Office.Vereador).Count(c => c.PartyId == party.Id && !c.IsBulk && !c.IsPlayer);
        Glossary.Rich(_left, string.Format(Tr("Colegas de chapa nomeados para vereador nesta cidade: {0}, além dos demais da lista."), named));
        BuildSlateBlock();
        if (_engine.City.Bosses.TryGetValue(party.Id, out string? boss))
        {
            Heading(_left, Tr("Direção"), 20);
            PersonLink(_left, boss, string.Format(Tr("Dirigente na cidade: {0}"), _engine.NameOf(boss)));
            Glossary.Rich(_left, Tr("É quem decide a sua cota do fundo eleitoral: pesam a relação, a sua viabilidade na pesquisa e a distância da linha do partido. O valor de hoje está em Finanças, Contas."));
        }
    }

    // ------------------------------------------------------------------ decisões sobre o mapa

    private void BuildBanner()
    {
        if (_banner is not null && IsInstanceValid(_banner))
        {
            // Sai da árvore já: a faixa nova precisa do mesmo nome, mesmo com dois redesenhos no mesmo quadro.
            _banner.GetParent()?.RemoveChild(_banner);
            _banner.QueueFree();
        }

        _banner = null;
        string? text = null;
        var buttons = new List<(string Label, Action Action, string Tip)>();
        switch (Campaign.Phase)
        {
            case CampaignPhase.Votacao:
                text = Tr("Dia da eleição. O jogo está pausado até você votar.");
                buttons.Add(("Ir para a urna", GoToUrna, Palette.Tip(Tr("Vai à seção eleitoral votar."), Tr("abre a urna; depois vem a apuração."), Tr("nada; antes, a carreira é salva para você poder rejogar a eleição."))));
                break;
            case CampaignPhase.Encerrada:
                text = Tr("A eleição terminou.");
                buttons.Add(("Ver a apuração", () => _game.GoTo(Game.Scenes.ElectionNight), Palette.Tip(Tr("Mostra a noite da apuração."), Tr("as urnas são apuradas em parciais até o resultado."), Tr("nada."))));
                break;
            default:
                if (Campaign.Debate is not null)
                {
                    text = Tr("Há um debate na TV hoje. Jogo pausado.");
                    buttons.Add(("Ir ao debate", () => _game.GoTo(Game.Scenes.Debate), Palette.Tip(Tr("Participa do debate de prefeito na TV."), Tr("três rodadas de cartas; o placar mexe na imagem de todos."), Tr("nada."))));
                    buttons.Add(("Recusar o debate", () =>
                    {
                        Core.Debate.DebateEngine.Decline(_engine.Context, _game.Debate);
                        _log.Insert(0, Tr("Você faltou ao debate: a cadeira vazia virou assunto."));
                        Refresh();
                    }, Palette.Tip(Tr("Não vai ao debate."), Tr("a cadeira vazia vira assunto na cidade."), string.Format(PtBr, Tr("imagem {0:+0.00;−0.00}."), _game.Debate.DeclineImage))));
                }
                else if (Agenda.PendingRequest is { } personId && _engine.City.Leader(personId) is { } leader && Campaign.Events.Pending.Count == 0)
                {
                    text = string.Format(Tr("{0} fez um pedido. Jogo pausado."), LeaderLabel(leader));
                    if (_requestHidden)
                    {
                        buttons.Add(("Decidir", () => { _requestHidden = false; Refresh(); },
                            Palette.Tip(Tr("Abre o cartão do pedido."), Tr("mostra o pedido e as quatro respostas, cada uma com a previsão e quem vai lembrar."), Tr("nada; o jogo segue pausado até você responder."))));
                    }
                    else
                    {
                        buttons.Add(("Ver o mapa", () => { _requestHidden = true; Refresh(); },
                            Palette.Tip(Tr("Esconde o cartão do pedido para você consultar o mapa."), Tr("o pedido continua esperando; Decidir traz o cartão de volta."), Tr("nada; o jogo segue pausado até você responder."))));
                    }
                }
                else if (_engine.Team.Pending.FirstOrDefault() is { } staffDecision && Campaign.Events.Pending.Count == 0 && _engine.Staff(staffDecision.PersonId) is { } staffMember)
                {
                    text = string.Format(Tr("{0} espera uma decisão sua. Jogo pausado."), StaffLabel(staffMember));
                    if (_staffHidden)
                    {
                        buttons.Add(("Decidir", () => { _staffHidden = false; Refresh(); },
                            Palette.Tip(Tr("Abre o cartão da decisão."), Tr("mostra as opções, cada uma com a previsão e quem vai lembrar."), Tr("nada; o jogo segue pausado até você responder."))));
                    }
                    else
                    {
                        buttons.Add(("Ver o mapa", () => { _staffHidden = true; Refresh(); },
                            Palette.Tip(Tr("Esconde o cartão da decisão para você consultar o mapa e a equipe."), Tr("a decisão continua esperando; Decidir traz o cartão de volta."), Tr("nada; o jogo segue pausado até você responder."))));
                    }
                }
                else if (_notice is not null && !_running)
                {
                    text = _notice.Text + " " + Tr("Jogo pausado.");
                    if (_notice.Button is not null && _notice.Action is { } action)
                    {
                        buttons.Add((_notice.Button, action, _notice.Tip ?? ""));
                    }

                    buttons.Add(("Fechar aviso", () => { _notice = null; Refresh(); },
                        Palette.Tip(Tr("Fecha este aviso."), Tr("o jogo continua pausado até você escolher uma velocidade na barra de cima."), Tr("nada."))));
                }
                else if (_engine.PendingAttacks().OrderBy(a => a.Deadline).FirstOrDefault() is { } pendingAttack && !(_sheet == Sheet.Ataque && _sheetId == pendingAttack.Attack.Id))
                {
                    // Decisão com prazo (GDD 17.4): não trava o tempo; a faixa mostra a contagem e a opção padrão.
                    text = AttackBannerText(pendingAttack);
                    string attackId = pendingAttack.Attack.Id;
                    buttons.Add(("Responder", () => { _notice = null; OpenAreaSheet(Sheet.Ataque, attackId); },
                        Palette.Tip(Tr("Abre as respostas ao ataque."), Tr("mostra o ataque e cada resposta possível com a previsão: quanto do dano sobra, em quantas semanas se apaga e o custo."), Tr("nada para abrir; o prazo continua correndo."))));
                }
                else if (_engine.Finance.Offers.OrderBy(o => o.Deadline).FirstOrDefault() is { } openOffer && !(_sheet == Sheet.Oferta && _sheetId == openOffer.Id))
                {
                    text = string.Format(Tr("{0} fez uma oferta de doação: responda até {1}."), _engine.NameOf(openOffer.DonorId), openOffer.Deadline.ToString("dddd, d/MM", PtBr));
                    string offerId = openOffer.Id;
                    buttons.Add(("Decidir", () => { _notice = null; OpenAreaSheet(Sheet.Oferta, offerId); },
                        Palette.Tip(Tr("Abre a oferta de doação."), Tr("mostra cada resposta com a previsão: o dinheiro que entra, o risco das contas, a dívida e quem fica sabendo."), Tr("nada para abrir; o prazo continua correndo."))));
                }
                else if (!_running && Campaign.Polls.Count > 0 && Campaign.Polls[^1].Week == State.Calendar.WeekStart && State.Time.Day == 0 && !Campaign.Polls[^1].Internal)
                {
                    text = string.Format(Tr("Saiu pesquisa do {0}. Jogo pausado."), _game.Electorate.Pollsters.Get(Campaign.Polls[^1].PollsterId).Name);
                }

                break;
        }

        if (text is null)
        {
            return;
        }

        // Um destaque por vez (ART_GUIDE): com um cartão de decisão aberto, a faixa fica neutra.
        bool cardOpen = Campaign.Events.Pending.Count > 0 || (Agenda.PendingRequest is not null && !_requestHidden) || (_engine.Team.Pending.Count > 0 && !_staffHidden);
        _banner = _mapArea.Add(new PanelContainer { Name = "FaixaDecisao" });
        _banner.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 8, 12, cardOpen ? Palette.Linha : Palette.Atencao));
        _banner.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
        _banner.GrowVertical = GrowDirection.Begin;
        _banner.OffsetLeft = -360;
        _banner.OffsetRight = 360;
        _banner.OffsetTop = -88;
        _banner.OffsetBottom = -16;
        HBoxContainer row = Ui.Row(_banner, 12);
        Glossary.Rich(row, text, 18, Palette.Texto).SizeFlagsVertical = SizeFlags.ShrinkCenter;
        foreach (var (label, action, tip) in buttons)
        {
            Button b = Ui.Button(row, label, action, tip);
            b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            if (label == "Fechar aviso")
            {
                b.Name = "FecharAviso";
            }
        }
    }

    /// <summary>Zona citada num cartão de decisão: centraliza o mapa e abre a ficha no painel.</summary>
    private void OpenZoneFromCard(string zone)
    {
        if (!_map.Zonas.ContainsKey(zone))
        {
            return;
        }

        _map.CenterOn(zone);
        _map.Select(zone);
        _area = "territorio";
        OpenSheet(Sheet.Zona, zone);
    }

    private void GoToUrna()
    {
        // Save da véspera para rejogar a eleição; no Ironman, só o save único.
        if (State.World?.Options.Ironman == true)
        {
            SaveManager.Instance.Autosave();
        }
        else
        {
            SaveManager.Instance.Save(SaveManager.ElectionSlot);
        }

        _game.GoTo(Game.Scenes.Urna);
    }

    /// <summary>
    /// Cartão da decisão pendente sobre o mapa escurecido; os painéis laterais continuam visíveis. Eventos vêm antes
    /// do pedido de liderança. Ao fechar, o fundo sai junto (nunca há mais de um fundo).
    /// </summary>
    private void ShowPendingDecision()
    {
        if (_eventShade is not null && IsInstanceValid(_eventShade))
        {
            _eventShade.QueueFree();
        }

        _eventShade = null;
        _requestCard = null;
        if (Agenda.PendingRequest is null && _requestResult is null)
        {
            _requestHidden = false;
        }

        _staffCard = null;
        if (_engine.Team.Pending.Count == 0 && _staffResult is null)
        {
            _staffHidden = false;
        }

        PendingEvent? pending = Campaign.Events.Pending.FirstOrDefault();
        bool request = pending is null && (Agenda.PendingRequest is not null || _requestResult is not null) && !_requestHidden;
        bool staff = pending is null && !request && Agenda.PendingRequest is null && (_engine.Team.Pending.Count > 0 || _staffResult is not null) && !_staffHidden;
        if (pending is null && !request && !staff)
        {
            return;
        }

        _running = false;
        var shade = _mapArea.Add(new ColorRect { Color = new Color(Palette.Fundo, 0.6f), MouseFilter = MouseFilterEnum.Stop });
        _eventShade = shade;
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = shade.Add(new CenterContainer());
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (pending is not null)
        {
            center.Add(new EventDialog(_engine, pending, () =>
            {
                shade.QueueFree();
                if (_eventShade == shade)
                {
                    _eventShade = null;
                }

                Refresh();
            })
            {
                // Nomes do cartão são links: a ficha abre no painel da esquerda, que continua legível.
                PersonLabel = PersonLabel,
                CanOpenPerson = HasPersonSheet,
                OpenPerson = OpenPerson,
                OpenZone = OpenZoneFromCard,
                OpenOutlet = outlet => OpenAreaSheet(Sheet.Veiculo, outlet),
            });
        }
        else if (request)
        {
            BuildRequestCard(center);
        }
        else
        {
            BuildStaffCard(center);
        }

        // A faixa de decisão fica por cima do escurecido.
        if (_banner is not null)
        {
            _mapArea.MoveChild(_banner, -1);
        }
    }

    private void ShowSaveMenu()
    {
        if (State.World?.Options.Ironman == true)
        {
            SaveManager.Instance.Autosave();
            _log.Insert(0, Tr("Modo Ironman: a carreira foi salva no save único."));
            Refresh();
            return;
        }

        var menu = new PopupMenu();
        foreach (string slot in SaveManager.Instance.ManualSlots())
        {
            menu.AddItem(string.Format(Tr("Salvar no {0}"), slot));
        }

        menu.IndexPressed += i =>
        {
            string slot = SaveManager.Instance.ManualSlots().ElementAt((int)i);
            SaveManager.Instance.Save(slot);
            _log.Insert(0, string.Format(Tr("Carreira salva no {0}."), slot));
            Refresh();
        };
        AddChild(menu);
        menu.PopupCentered();
    }
}
