using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Electorate;
using Planalto.Core.Elections;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.UI.Map;
using Planalto.UI.Portrait;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Tela central (GDD 17.3, ART_GUIDE seção Interface): barra superior com data e tempo, trilho de áreas, painel da
/// área, mapa das zonas sempre visível, notícias e intenção de voto, decisões em cartão sobre o mapa. O tempo corre
/// por dia (GDD 17.4) e pausa sozinho quando algo pede decisão. Só lê o estado e chama o Core.
/// </summary>
public partial class HubScreen : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly Dictionary<string, (ZonasUf Zonas, GeoZonasUf Geo)> ZoneCache = new(StringComparer.Ordinal);

    private static readonly (string Id, string Icon, string Name, string Tip)[] Areas =
    [
        ("campanha", "megafone", "Campanha", "Candidatura e as ações da semana."),
        ("pessoas", "pessoas", "Pessoas", "Rivais e candidatos a prefeito."),
        ("agenda", "calendario", "Agenda", "O que você fez nesta semana e as datas que vêm."),
        ("territorio", "barras", "Território", "As zonas da cidade e o perfil de cada uma."),
        ("financas", "cedula", "Finanças", "Caixa, custos e pesquisas."),
        ("midia", "jornal", "Mídia", "Tudo o que saiu nas notícias."),
        ("partido", "predio", "Partido", "Seu partido nesta eleição."),
    ];

    private static readonly Dictionary<string, string> Hints = new(StringComparer.Ordinal)
    {
        ["campanha"] = "Cada ação gasta energia da semana e dinheiro. Passe o mouse para ver a previsão antes de clicar.",
        ["pessoas"] = "Seus adversários diretos. Quem tem mais reconhecimento costuma ter mais voto.",
        ["agenda"] = "A semana vai de segunda a domingo; no domingo a cidade reage ao que todos fizeram.",
        ["territorio"] = "Clique numa zona do mapa para ver quem mora ali e como ela tende a votar.",
        ["financas"] = "Pesquisa interna mostra onde você está; institutos publicam sozinhos durante a campanha.",
        ["midia"] = "O que aconteceu na cidade e com você, do mais recente ao mais antigo.",
        ["partido"] = "Seu partido define o número de urna e pesa no voto de quem já gosta dele.",
    };

    private readonly List<string> _log = [];
    private readonly Dictionary<string, Dictionary<string, double>> _zoneParties = [];
    private Game _game = null!;
    private CampaignEngine _engine = null!;
    private HBoxContainer _top = null!;
    private VBoxContainer _trail = null!;
    private VBoxContainer _left = null!;
    private VBoxContainer _right = null!;
    private Control _mapArea = null!;
    private ZoneMapView _map = null!;
    private HBoxContainer _modes = null!;
    private VBoxContainer _legend = null!;
    private PanelContainer? _banner;
    private ColorRect? _eventShade;
    private string _area = "campanha";
    private string _mapMode = "lider";
    private string? _visitTarget;
    private bool _running;
    private int _speed = 1;
    private double _accumulated;
    private ZonasMunicipio? _zonas;

    private GameState State => _game.State!;

    private CampaignState Campaign => State.Campaign!;

    /// <summary>Registro de resultados da semana, do mais recente ao mais antigo.</summary>
    public IReadOnlyList<string> Log => _log;

    /// <summary>Cartão de decisão aberto (testes de fumaça conferem que ele sai).</summary>
    public bool EventOpen => _eventShade is not null && IsInstanceValid(_eventShade);

    public bool Running => _running;

    public ZoneMapView ZoneMap => _map;

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.State is null)
        {
            _game.GoTo(Game.Scenes.MainMenu);
            return;
        }

        if (_game.Engine is null)
        {
            _game.GoToCareer();
            return;
        }

        _engine = _game.Engine;
        Theme = Palette.CentralTheme();
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = this.Add(new ColorRect { Color = Palette.Fundo, MouseFilter = MouseFilterEnum.Ignore });
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var root = this.Add(new VBoxContainer());
        root.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 1);
        _top = Panel(root, new Vector2(0, 56), 8).Add(new HBoxContainer());
        _top.AddThemeConstantOverride("separation", 12);

        var body = root.Add(new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill });
        body.AddThemeConstantOverride("separation", 1);
        _trail = Panel(body, new Vector2(64, 0), 8).Add(new VBoxContainer());
        _trail.AddThemeConstantOverride("separation", 8);
        _left = Scroll(Panel(body, new Vector2(400, 0), 16));

        _mapArea = body.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(640, 0), ClipContents = true });
        _map = _mapArea.Add(new ZoneMapView());
        _map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _map.ZoneClicked += id =>
        {
            if (id is not null)
            {
                _area = "territorio";
            }

            Refresh();
        };
        _map.HoverText = ZoneTooltip;
        _modes = _mapArea.Add(new HBoxContainer { Position = new Vector2(16, 16) });
        _modes.AddThemeConstantOverride("separation", 8);
        _legend = _mapArea.Add(new VBoxContainer());
        _legend.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomLeft);
        _legend.GrowVertical = GrowDirection.Begin;
        _legend.OffsetLeft = 16;
        _legend.OffsetBottom = -16;

        _right = Scroll(Panel(body, new Vector2(400, 0), 16));
        LoadZones();
        Refresh();
    }

    private static PanelContainer Panel(Node parent, Vector2 minimum, int margin)
    {
        var panel = parent.Add(new PanelContainer { CustomMinimumSize = minimum });
        panel.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Painel, 0, margin));
        return panel;
    }

    private static VBoxContainer Scroll(PanelContainer panel)
    {
        var scroll = panel.Add(new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill });
        var column = scroll.Add(new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        column.AddThemeConstantOverride("separation", 8);
        return column;
    }

    private void LoadZones()
    {
        var municipio = _engine.Context.Municipio;
        if (!ZoneCache.TryGetValue(municipio.Uf, out var files))
        {
            var zonas = ZonasPaths.Load(_game.DataDirectory, municipio.Uf);
            var geo = ZonasPaths.LoadGeo(_game.DataDirectory, municipio.Uf);
            if (zonas.Value is null || geo.Value is null)
            {
                GD.PushError($"Zonas de {municipio.Uf} indisponíveis: rode o importador no modo zonas.");
                return;
            }

            files = (zonas.Value, geo.Value);
            ZoneCache[municipio.Uf] = files;
        }

        _zonas = files.Zonas.Municipios.FirstOrDefault(m => m.CodigoIbge == municipio.CodigoIbge);
        if (_zonas is not null)
        {
            string prefix = municipio.CodigoIbge.ToString(CultureInfo.InvariantCulture) + "-";
            _map.Load(_zonas, files.Geo.Zonas.Where(z => z.Id.StartsWith(prefix, StringComparison.Ordinal)), files.Geo.Scale);
        }
    }

    // ------------------------------------------------------------------ tempo

    public override void _Process(double delta)
    {
        if (!_running)
        {
            return;
        }

        if (BlockReason() is not null)
        {
            _running = false;
            Refresh();
            return;
        }

        _accumulated += delta;
        if (_accumulated >= Clock.SecondsPerDay(_game.Time, Clock.ModoCampanha, _speed))
        {
            _accumulated = 0;
            StepDay();
        }
    }

    /// <summary>Motivo para o tempo não andar (decisão imediata ou fase que se resolve fora do mapa); null se pode.</summary>
    public string? BlockReason() =>
        Campaign.Events.Pending.Count > 0 ? Tr("há uma decisão esperando você")
        : Campaign.Debate is not null ? Tr("há um debate na TV hoje")
        : Campaign.Phase == CampaignPhase.Votacao ? Tr("é dia de eleição: vá à urna")
        : Campaign.Phase == CampaignPhase.Encerrada ? Tr("a eleição terminou: veja a apuração")
        : null;

    /// <summary>Um dia de jogo. No domingo, a simulação da semana roda e as pausas automáticas são conferidas.</summary>
    public void StepDay()
    {
        int polls = Campaign.Polls.Count;
        CampaignPhase phase = Campaign.Phase;
        int actions = Campaign.ActionsThisWeek.Count;
        Game.WeekNote note = Game.WeekNote.Nenhuma;
        bool turned = Clock.AdvanceDay(State, () => note = _game.AdvanceWeek());
        if (turned)
        {
            SaveManager.Instance.Autosave();
            if (note == Game.WeekNote.MandatoEncerrado)
            {
                _log.Insert(0, Tr("Seu mandato terminou."));
            }

            if (_game.Engine is null || State.Campaign is null)
            {
                _game.GoToCareer();
                return;
            }

            CheckPauses(polls, phase, actions);
        }

        Refresh();
    }

    private void CheckPauses(int polls, CampaignPhase phase, int actionsLastWeek)
    {
        var overrides = Settings.PauseOverrides;
        bool Pause(string id) => Clock.ShouldPause(_game.Time, id, overrides);
        if (BlockReason() is not null && Pause(Clock.DecisaoImediata))
        {
            _running = false;
        }

        if (Campaign.Phase != phase && Pause(Clock.FaseNova))
        {
            _running = false;
            _log.Insert(0, Campaign.Phase == CampaignPhase.Campanha ? Tr("Começou a campanha oficial: comício e propaganda liberados.") : Tr("A fase da eleição mudou."));
        }

        if (Campaign.Polls.Count > polls && Pause(Clock.PesquisaPublicada))
        {
            _running = false;
        }

        decimal cheapest = _game.Campaign.Actions.Actions.Where(a => a.AllowedIn(Campaign.Phase)).Select(_engine.CostOf).Where(c => c > 0).DefaultIfEmpty(0).Min();
        if (State.Player.Resources.Money < cheapest && Pause(Clock.CaixaBaixo))
        {
            _running = false;
        }

        if (actionsLastWeek == 0 && Pause(Clock.SemanaSemCompromisso))
        {
            _running = false;
        }
    }

    private void SetSpeed(int speed)
    {
        _speed = speed;
        _running = BlockReason() is null;
        _accumulated = 0;
        Refresh();
    }

    // ------------------------------------------------------------------ montagem

    public void Refresh()
    {
        if (_top is null)
        {
            return;
        }

        BuildTop();
        BuildTrail();
        BuildLeft();
        BuildMap();
        BuildRight();
        BuildBanner();
        ShowPendingEvent();
    }

    private string Money(decimal value) => string.Create(PtBr, $"R$ {value:N0}");

    private string PartySigla(string partyId) => State.Party(partyId)?.Sigla ?? partyId;

    private static Label Heading(Node parent, string text, int size = 22)
    {
        Label l = Ui.Label(parent, text, size);
        l.AddThemeFontOverride("font", Palette.Weight(Palette.Inter, 600));
        l.AddThemeColorOverride("font_color", Palette.Texto);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    private Button IconButton(Node parent, string icon, bool pressed, Action onPressed, string tip)
    {
        var b = parent.Add(new Button
        {
            Icon = GD.Load<Texture2D>($"res://assets/icons/ui/{icon}.svg"),
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

    private void BuildTop()
    {
        Ui.Clear(_top);
        DateOnly today = Clock.Today(State);
        Label date = Ui.Value(_top, today.ToString("ddd, d MMM yyyy", PtBr), 24);
        date.AddThemeFontOverride("font", Palette.Weight(Palette.Oswald, 500));
        date.AddThemeColorOverride("font_color", Palette.Texto);
        date.CustomMinimumSize = new Vector2(190, 0);
        date.MouseFilter = MouseFilterEnum.Stop;
        date.TooltipText = Palette.Tip(Tr("Data do jogo. O dia é a unidade do tempo."), Tr("a cada segunda-feira a cidade se mexe: rivais, pesquisas e eventos."), Tr("nada."));

        string? block = BlockReason();
        var pause = IconButton(_top, "pausa", !_running, () => { _running = false; Refresh(); },
            Palette.Tip(Tr("Pausa o tempo."), Tr("o dia para; você pode olhar o mapa, fazer ações e decidir com calma."), Tr("nada.")));
        pause.Modulate = !_running && block is not null ? Palette.Atencao : Colors.White;
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

        Stat(Tr("caixa"), Money(State.Player.Resources.Money), Tr("Dinheiro em caixa da campanha."), Tr("paga ações e pesquisas; acabou, só a arrecadação traz mais."));
        Stat(Tr("conhecem você"), string.Format(PtBr, "{0:0}%", Campaign.Player.Recognition), Tr("Reconhecimento: quanto da cidade sabe quem você é."), Tr("o eleitor só vota em quem conhece; cai um pouco toda semana sem campanha."));
        Stat(Tr("como te veem"), string.Format(PtBr, "{0:+0.00;−0.00;0.00}", Campaign.Player.Image), Tr("Imagem: como a cidade vê você, de −1 a +1."), Tr("pesa no voto; volta aos poucos para zero sem novidades."));
        Stat(Tr("estresse"), string.Format(PtBr, "{0}", State.Player.Resources.Stress), Tr("Estresse do personagem."), Tr("sobe com a campanha; alto por muito tempo, piora a saúde."));
        Ui.Button(_top, "Mapa", () => _game.GoTo(Game.Scenes.Map), Palette.Tip(Tr("Mapa do seu estado em 2.5D, com a força dos partidos por município."), Tr("abre o mapa do estado; Voltar retorna a esta tela."), Tr("nada; o jogo fica pausado.")));
        Ui.Button(_top, "Salvar", ShowSaveMenu, Palette.Tip(Tr("Grava a carreira."), Tr("salva num dos slots manuais (no Ironman, no save único)."), Tr("nada.")));
        Ui.Button(_top, "Menu", () => { SaveManager.Instance.Autosave(); _game.GoTo(Game.Scenes.MainMenu); }, Palette.Tip(Tr("Volta ao menu principal."), Tr("salva automaticamente e sai da carreira."), Tr("nada.")));
        IconButton(_top, "ajuda", false, () => EncyclopediaDialog.Open(GetTree().Root), Palette.Tip(Tr("Enciclopédia (F1)."), Tr("explica termos, regras eleitorais reais e quem é quem neste mundo."), Tr("nada; o jogo fica pausado.")));
    }

    private void Stat(string caption, string value, string what, string does)
    {
        var box = _top.Add(new VBoxContainer { MouseFilter = MouseFilterEnum.Stop, TooltipText = Palette.Tip(what, does, Tr("é um resultado, não um custo.")) });
        box.AddThemeConstantOverride("separation", -4);
        box.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Label top = Ui.Label(box, caption, 18);
        top.AddThemeColorOverride("font_color", Palette.Texto3);
        top.MouseFilter = MouseFilterEnum.Pass;
        Label label = Ui.Value(box, value, 20);
        label.AddThemeColorOverride("font_color", Palette.Texto);
        label.MouseFilter = MouseFilterEnum.Pass;
    }

    private void BuildTrail()
    {
        Ui.Clear(_trail);
        foreach (var (id, icon, name, tip) in Areas)
        {
            IconButton(_trail, icon, _area == id, () => { _area = id; Refresh(); }, Palette.Tip($"{Tr(name)}: {Tr(tip)}", Tr("mostra esta área no painel da esquerda; o mapa continua visível."), Tr("nada.")));
        }
    }

    // ------------------------------------------------------------------ painel da área

    private void BuildLeft()
    {
        Ui.Clear(_left);
        Heading(_left, Tr(Areas.First(a => a.Id == _area).Name));
        FirstTimeHint(_area);
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

    private void FirstTimeHint(string area)
    {
        if (!Settings.HintsEnabled || Settings.HintSeen(area) || !Hints.TryGetValue(area, out string? text))
        {
            return;
        }

        var card = _left.Add(new PanelContainer());
        card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 8, 12, Palette.Acao));
        var col = Ui.Column(card, 8);
        Glossary.Rich(col, Tr(text), 18, Palette.Texto);
        var row = Ui.Row(col, 8);
        Ui.Button(row, "Entendi", () => { Settings.MarkHintSeen(area); Refresh(); }, Palette.Tip(Tr("Fecha esta dica."), Tr("ela não volta nesta área; tudo o que ela diz está na Enciclopédia (F1)."), Tr("nada.")));
        Ui.Button(row, "Sem dicas", () => { Settings.DisableHints(); Refresh(); }, Palette.Tip(Tr("Desliga todas as dicas de primeira vez."), Tr("nenhuma dica volta."), Tr("nada.")));
    }

    private void BuildCampaign()
    {
        Office office = Campaign.Player.Office;
        string current = _game.People.Offices.Name(office, State.Player.Gender);
        Glossary.Rich(_left, string.Format(Tr("Candidatura: {0} · número {1}"), current, Campaign.Player.Number), 18, Palette.Texto);
        if (State.Mandate is not null)
        {
            Glossary.Rich(_left, Tr("Campanha de reeleição: durante a campanha, seu mandato vota seguindo a orientação do partido."), 16);
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

        Heading(_left, string.Format(Tr("Ações da semana · energia {0}/{1}"), Campaign.EnergyLeft, _engine.Context.Config.WeeklyEnergy), 20);
        foreach (CampaignActionDefinition action in _game.Campaign.Actions.Actions)
        {
            if (action.Targets.Count > 0)
            {
                var options = Ui.Options(_left, action.Targets.Select(t => (t.Id, t.Name)), _visitTarget ?? action.Targets[0].Id, id => { _visitTarget = id; Refresh(); }, translate: false);
                options.TooltipText = Palette.Tip(Tr("Onde fazer a visita."), Tr("escolhe o grupo de eleitores que a visita alcança."), Tr("nada; o custo é o da ação."));
            }

            AddAction(action);
        }

        if (_log.Count > 0)
        {
            Heading(_left, Tr("O que mudou"), 18);
            foreach (string line in _log.Take(6))
            {
                Glossary.Rich(_left, "· " + line, 16);
            }
        }
    }

    private void AddAction(CampaignActionDefinition action)
    {
        string? target = action.Targets.Count > 0 ? _visitTarget ?? action.Targets[0].Id : null;
        Forecast forecast = _engine.Forecast(action.Id, target);
        decimal cost = _engine.CostOf(action);
        string money = cost >= 0 ? Money(cost) : "+" + Money(-cost);
        ActionFailure? failure = _engine.CanExecute(action, target);
        string tip = Palette.Tip(Tr(action.Description), ForecastText.Full(forecast, EffectSummary.GroupName) + ". " + Tr("Efeito na cidade inteira."), ForecastText.Cost(forecast, action.Energy) + ".");
        var button = Ui.Button(_left, $"{action.Name}\n{action.Energy} {Tr("energia")} · {money}", () =>
        {
            EffectSnapshot before = Snapshot();
            ActionResult result = _engine.Execute(action.Id, target);
            _log.Insert(0, EffectSummary.Line(action.Name, result.Text, EffectSummary.Describe(before, Snapshot(), EffectSummary.GroupName)));
            Refresh();
        }, failure is null ? tip : Palette.Blocked(FailureText(failure.Value), tip));
        button.Alignment = HorizontalAlignment.Left;
        button.Disabled = failure is not null;
        button.ClipText = true;
    }

    private EffectSnapshot Snapshot() => new(
        Campaign.Player.Recognition,
        Campaign.Player.Image,
        State.Player.Resources.Money,
        State.Player.Resources.PoliticalCapital,
        new Dictionary<string, double>(Campaign.Player.Support),
        new Dictionary<string, double>());

    private string FailureText(ActionFailure failure)
    {
        int weeks = Math.Max(0, (Campaign.CampaignStart.DayNumber - Clock.Today(State).DayNumber + 6) / 7);
        return failure switch
        {
            ActionFailure.FaseNaoPermite => string.Format(Tr("só na campanha oficial, a partir de 16 de agosto (faltam {0} semanas)."), weeks),
            ActionFailure.SemEnergia => string.Format(Tr("falta energia nesta semana (restam {0}); ela volta na segunda-feira."), Campaign.EnergyLeft),
            ActionFailure.SemDinheiro => Tr("falta dinheiro em caixa; a arrecadação traz mais."),
            ActionFailure.EventoPendente => Tr("resolva a decisão aberta antes."),
            _ => Tr("escolha onde fazer a visita."),
        };
    }

    private void BuildPeople()
    {
        Office office = Campaign.Player.Office;
        Heading(_left, Tr(office == Office.Prefeito ? "Na disputa pela prefeitura" : "Principais rivais"), 18);
        foreach (Candidate rival in Campaign.ForOffice(office).Where(c => !c.IsPlayer && !c.IsBulk).OrderByDescending(c => c.Recognition).Take(8))
        {
            PersonRow(rival, office == Office.Prefeito ? Tr("candidato(a) a prefeito(a)") : Tr("candidato(a) a vereador(a)"));
        }

        if (office != Office.Prefeito)
        {
            Heading(_left, Tr("Prefeitura"), 18);
            foreach (Candidate mayor in Campaign.ForOffice(Office.Prefeito))
            {
                PersonRow(mayor, Tr("candidato(a) a prefeito(a)"));
            }
        }
    }

    private void PersonRow(Candidate c, string role)
    {
        var card = _left.Add(new PanelContainer());
        card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Cartao, 8, 8));
        HBoxContainer row = Ui.Row(card, 8);
        Appearance? appearance = State.Npcs.FirstOrDefault(n => n.Id == c.CharacterId)?.Appearance;
        if (appearance is not null)
        {
            var view = row.Add(new PortraitView { CustomMinimumSize = new Vector2(56, 56) });
            GameParty? party = State.Party(c.PartyId);
            view.Show(appearance, new PortraitOptions(Planalto.Core.Characters.Expression.Neutra, WardrobeContext.Campanha, party?.Colors.Primary, "background_solido", party?.Colors.Secondary), _game.People.Appearance);
        }

        var col = Ui.Column(row, 2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Label name = Ui.Value(col, $"{c.Name} ({PartySigla(c.PartyId)})", 18);
        name.AddThemeColorOverride("font_color", Palette.Texto);
        name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        Glossary.Rich(col, string.Format(PtBr, "{0}{1} · {2} {3:0}%", role, c.Incumbent ? " · " + Tr("tem mandato") : "", Tr("reconhecimento"), c.Recognition), 16);
    }

    private void BuildAgendaArea()
    {
        Glossary.Rich(_left, string.Format(Tr("Hoje é {0}. Energia da semana: {1} de {2}."), Clock.Today(State).ToString("dddd, d 'de' MMMM", PtBr), Campaign.EnergyLeft, _engine.Context.Config.WeeklyEnergy), 18, Palette.Texto);
        Heading(_left, Tr("Feito nesta semana"), 18);
        if (Campaign.ActionsThisWeek.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nada ainda. As ações ficam na área Campanha."), 16);
        }

        foreach (var group in Campaign.ActionsThisWeek.GroupBy(a => a))
        {
            Glossary.Rich(_left, $"· {_game.Campaign.Actions.Get(group.Key).Name} × {group.Count()}", 16);
        }

        Heading(_left, Tr("Datas que vêm"), 18);
        var dates = new List<(DateOnly Date, string Text)>
        {
            (Campaign.CampaignStart, Tr("início da campanha oficial (comício e propaganda)")),
            (Campaign.ElectionDate, Tr("primeiro turno da eleição")),
        };
        if (Campaign.Player.Office == Office.Prefeito)
        {
            dates.AddRange(_game.Debate.WeeksBeforeElection.Select(w => (Campaign.ElectionDate.AddDays(-7 * w), Tr("semana de debate na TV"))));
        }

        foreach (var (date, text) in dates.Where(d => d.Date >= Clock.Today(State)).OrderBy(d => d.Date))
        {
            Glossary.Rich(_left, $"{date.ToString("dd/MM/yyyy", PtBr)} · {text}", 16);
        }

        Glossary.Rich(_left, Tr("Na próxima parte do jogo, cada compromisso terá dia, turno, zona e público."), 14, Palette.Texto3);
    }

    private void BuildTerritory()
    {
        if (_zonas is null)
        {
            Glossary.Rich(_left, Tr("As zonas desta cidade não foram carregadas."), 16);
            return;
        }

        if (_map.Selected is { } id && _map.Zonas.TryGetValue(id, out Zona? zona))
        {
            ZoneCard(zona);
            Ui.Button(_left, "Ver todas as zonas", () => { _map.Select(null); Refresh(); }, Palette.Tip(Tr("Volta à lista de zonas."), Tr("tira a seleção do mapa."), Tr("nada.")));
            return;
        }

        Glossary.Rich(_left, string.Format(Tr("{0} zonas da cidade, montadas com setores do Censo 2022 do IBGE. Clique numa zona do mapa ou da lista."), _zonas.Zonas.Count), 16);
        foreach (Zona z in _zonas.Zonas.OrderByDescending(z => z.Populacao))
        {
            var b = Ui.Button(_left, string.Format(PtBr, "{0} · {1:N0} hab.", z.Nome, z.Populacao), () => { _map.Select(z.Id); Refresh(); },
                Palette.Tip(string.Format(Tr("Zona {0}."), z.Nome), Tr("abre a ficha da zona e a destaca no mapa."), Tr("nada.")));
            b.Alignment = HorizontalAlignment.Left;
            b.ClipText = true;
        }
    }

    private void ZoneCard(Zona z)
    {
        Heading(_left, z.Nome, 24);
        string origem = z.NomeFonte switch
        {
            Zona.NomeBairro => Tr("nome do bairro mais populoso da zona, pela malha de bairros do IBGE"),
            Zona.NomeDistrito => Tr("nome do distrito do IBGE"),
            _ => Tr("nome descritivo do jogo; não há bairro oficial aqui"),
        };
        Glossary.Rich(_left, string.Format(PtBr, Tr("{0}. {1:N0} moradores, {2:N0} domicílios, {3:N1} km², {4} setores do Censo 2022. Origem do nome: {5}."),
            Tr(z.IsRural ? "Zona rural" : "Zona urbana"), z.Populacao, z.Domicilios, z.AreaKm2, z.Setores, origem), 16);
        if (z.Perfil.RendaResponsavel is { } renda)
        {
            Glossary.Rich(_left, string.Format(PtBr, Tr("Rendimento médio do responsável pelo domicílio: R$ {0:N0} ({1})."), renda, z.Perfil.RendaFonte == PerfilZona.FonteZona ? Tr("dado da zona") : Tr("dado do município: setores pequenos têm sigilo")), 16);
        }

        var ages = ZoneProjection.AgeShares(z);
        if (ages.Values.Sum() > 0)
        {
            Glossary.Rich(_left, string.Format(PtBr, Tr("Eleitores por idade: 16 a 34 anos {0:P0}, 35 a 59 {1:P0}, 60 ou mais {2:P0}."),
                ages["idade_16_17"] + ages["idade_18_34"], ages["idade_35_59"], ages["idade_60_69"] + ages["idade_70_mais"]), 16);
        }

        if (_zoneParties.TryGetValue(z.Id, out var parties) && parties.Count > 0)
        {
            Heading(_left, Tr("Intenção por partido"), 18);
            foreach (var (party, share) in parties.OrderByDescending(p => p.Value).Take(5))
            {
                Glossary.Rich(_left, string.Format(PtBr, "{0} {1:P1}", PartySigla(party), share), 16);
            }
        }

        Glossary.Rich(_left, Tr("Fonte: IBGE, Censo 2022 (setores censitários). A intenção é a da cidade aplicada à idade e à situação da zona: uma estimativa por zona, não uma pesquisa."), 14, Palette.Texto3);
    }

    private void BuildFinances()
    {
        Glossary.Rich(_left, string.Format(Tr("Caixa: {0}."), Money(State.Player.Resources.Money)), 18, Palette.Texto);
        Heading(_left, Tr("Pesquisas"), 18);
        foreach (PollsterDefinition p in _game.Electorate.Pollsters.Pollsters.Where(p => p.Internal || p.HouseBias == 0))
        {
            decimal cost = Math.Round(p.Cost * (decimal)_engine.Context.Config.PorteCostMultiplier.GetValueOrDefault(_engine.Context.Municipio.Porte, 1) / 100) * 100;
            string tip = Palette.Tip(
                string.Format(Tr("{0} ({1})."), p.Name, Tr(p.Internal ? "pesquisa interna da sua campanha" : "instituto de pesquisa")),
                string.Format(PtBr, Tr("mostra a intenção de voto na cidade, com amostra de {0} a {1} entrevistas; sai na hora."), p.SampleMin, p.SampleMax),
                Money(cost) + ".");
            var b = Ui.Button(_left, $"{Tr("Encomendar")}: {p.Name} · {Money(cost)}", () =>
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
            Glossary.Rich(_left, string.Format(PtBr, Tr("{0} · {1:dd/MM} · {2} entrevistas · margem de erro de {3:0.0} pontos · não sabem: {4:0.0}%"), pollster.Name, poll.Week, poll.SampleSize, poll.MarginOfError, poll.Undecided), 16);
        }
    }

    private void BuildMedia()
    {
        var items = NewsItems().ToList();
        if (items.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nada saiu ainda. Semana tranquila na cidade."), 16);
        }

        foreach (var (date, source, text) in items)
        {
            NewsCard(_left, date, source, text);
        }
    }

    private void BuildParty()
    {
        GameParty? party = State.Party(State.Player.PartyId);
        if (party is null)
        {
            Glossary.Rich(_left, Tr("Você está sem partido."), 16);
            return;
        }

        string family = _game.World.Parties.Families.Families.FirstOrDefault(f => f.Id == party.FamilyId)?.Name ?? party.FamilyId;
        Heading(_left, $"{party.Sigla} · {party.Number}", 22);
        Glossary.Rich(_left, string.Format(Tr("{0}. Família ideológica: {1}. Partido fictício."), party.Name, family), 16);
        int named = Campaign.ForOffice(Office.Vereador).Count(c => c.PartyId == party.Id && !c.IsBulk && !c.IsPlayer);
        Glossary.Rich(_left, string.Format(Tr("Colegas de chapa nomeados para vereador nesta cidade: {0}, além dos demais da lista."), named), 16);
    }

    // ------------------------------------------------------------------ mapa

    private void BuildMap()
    {
        Ui.Clear(_modes);
        foreach (var (id, name, tip) in new[]
        {
            ("lider", "Partido líder", "Cor do partido com mais intenção de voto na zona; mais forte quanto maior a vantagem. Estimativa por zona."),
            ("renda", "Renda", "Rendimento médio do responsável pelo domicílio (Censo 2022), em 5 faixas da própria cidade."),
            ("idade", "Idosos", "Parte dos eleitores com 60 anos ou mais (Censo 2022), em 5 faixas da própria cidade."),
            ("situacao", "Urbana/rural", "Zonas urbanas e rurais pelo Censo 2022."),
        })
        {
            var b = Ui.Button(_modes, name, () => { _mapMode = id; Refresh(); }, Palette.Tip(Tr(tip), Tr("muda as cores do mapa."), Tr("nada.")));
            b.ToggleMode = true;
            b.ButtonPressed = _mapMode == id;
        }

        Ui.Button(_modes, "Cidade inteira", _map.ShowWhole, Palette.Tip(Tr("Enquadra o município inteiro."), Tr("mostra todas as zonas; a roda do mouse aproxima, o botão direito arrasta e o duplo clique volta ao enquadramento."), Tr("nada.")));

        Ui.Clear(_legend);
        if (_zonas is null)
        {
            return;
        }

        ComputeZoneParties();
        var colors = new Dictionary<string, Color>();
        switch (_mapMode)
        {
            case "lider":
                foreach (Zona z in _zonas.Zonas)
                {
                    if (!_zoneParties.TryGetValue(z.Id, out var shares) || shares.Count == 0)
                    {
                        continue;
                    }

                    var ordered = shares.OrderByDescending(s => s.Value).ToList();
                    double margin = ordered[0].Value - (ordered.Count > 1 ? ordered[1].Value : 0);
                    Color party = new(State.Party(ordered[0].Key)?.Colors.Primary ?? "#8A94A3");
                    colors[z.Id] = new Color(party, (float)Math.Clamp(0.45 + (margin * 3), 0.45, 1));
                }

                foreach (string leader in colors.Keys.Select(id => _zoneParties[id].MaxBy(s => s.Value).Key).Distinct())
                {
                    LegendRow(new Color(State.Party(leader)?.Colors.Primary ?? "#8A94A3"), PartySigla(leader));
                }

                Glossary.Rich(_legend, Tr("estimativa por zona"), 14, Palette.Texto3).CustomMinimumSize = new Vector2(220, 0);
                break;
            case "situacao":
                foreach (Zona z in _zonas.Zonas)
                {
                    colors[z.Id] = z.IsRural ? Palette.Perfil(0.2) : Palette.Perfil(0.9);
                }

                LegendRow(Palette.Perfil(0.9), Tr("urbana"));
                LegendRow(Palette.Perfil(0.2), Tr("rural"));
                break;
            default:
                Func<Zona, double?> value = _mapMode == "renda"
                    ? z => z.Perfil.RendaResponsavel
                    : z =>
                    {
                        var a = ZoneProjection.AgeShares(z);
                        return a.Values.Sum() > 0 ? a["idade_60_69"] + a["idade_70_mais"] : null;
                    };
                var values = _zonas.Zonas.Select(z => (z.Id, V: value(z))).Where(v => v.V is not null).OrderBy(v => v.V).ToList();
                for (int i = 0; i < values.Count; i++)
                {
                    colors[values[i].Id] = Palette.Perfil(Math.Floor(5.0 * i / Math.Max(1, values.Count)) / 4.0);
                }

                if (values.Count > 0)
                {
                    string Format(double v) => _mapMode == "renda" ? string.Create(PtBr, $"R$ {v:N0}") : v.ToString("P0", PtBr);
                    LegendRow(Palette.Perfil(0), Format(values[0].V!.Value));
                    LegendRow(Palette.Perfil(1), Format(values[^1].V!.Value));
                }

                break;
        }

        _map.SetColors(colors);
    }

    private void LegendRow(Color color, string text)
    {
        HBoxContainer row = Ui.Row(_legend, 8);
        row.Add(new ColorRect { Color = color, CustomMinimumSize = new Vector2(18, 18) });
        Label l = Ui.Label(row, text, 18);
        l.AddThemeColorOverride("font_color", Palette.Texto);
    }

    /// <summary>Intenção por partido em cada zona (a disputa do jogador), pela projeção sem ruído.</summary>
    private void ComputeZoneParties()
    {
        _zoneParties.Clear();
        if (_zonas is null)
        {
            return;
        }

        Office office = Campaign.Player.Office;
        VoteProjection projection = _engine.Project(office, noise: null);
        var partyOf = Campaign.ForOffice(office).ToDictionary(c => c.Id, c => c.PartyId);
        foreach (Zona z in _zonas.Zonas)
        {
            _zoneParties[z.Id] = ZoneProjection.Shares(projection, z)
                .GroupBy(s => partyOf.GetValueOrDefault(s.Key, ""))
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Value));
        }
    }

    private string ZoneTooltip(string id)
    {
        if (!_map.Zonas.TryGetValue(id, out Zona? z))
        {
            return "";
        }

        string leader = _zoneParties.TryGetValue(id, out var shares) && shares.Count > 0
            ? string.Format(PtBr, Tr("Partido líder (estimativa por zona): {0} com {1:P0}."), PartySigla(shares.MaxBy(s => s.Value).Key), shares.Values.Max())
            : "";
        return Palette.Tip(
            string.Format(PtBr, Tr("{0}: {1:N0} moradores ({2})."), z.Nome, z.Populacao, Tr(z.IsRural ? "zona rural" : "zona urbana")),
            Tr("clique para ver a ficha da zona.") + " " + leader,
            Tr("nada. Fonte: IBGE, Censo 2022."));
    }

    // ------------------------------------------------------------------ coluna direita

    private IEnumerable<(DateOnly Date, string Source, string Text)> NewsItems()
    {
        var items = Campaign.Events.History.Select(h => (h.Week, Tr("Cidade"), $"{_game.Events.Find(h.EventId)?.Title ?? h.EventId}: {h.Text}")).ToList();
        foreach (PollRecord poll in Campaign.Polls.Where(p => !p.Internal))
        {
            var lead = poll.Candidates.MaxBy(c => c.Value);
            items.Add((poll.Week, _game.Electorate.Pollsters.Get(poll.PollsterId).Name, string.Format(PtBr, Tr("{0} lidera com {1:0.0}%."), Campaign.Find(lead.Key)?.Name, lead.Value)));
        }

        return items.OrderByDescending(i => i.Item1);
    }

    private static void NewsCard(Node parent, DateOnly date, string source, string text)
    {
        var card = parent.Add(new PanelContainer());
        card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Cartao, 8, 12));
        var col = Ui.Column(card, 4);
        Label meta = Ui.Label(col, $"{source} · {date.ToString("dd/MM", PtBr)}", 18);
        meta.AddThemeColorOverride("font_color", Palette.Texto3);
        Glossary.Rich(col, text, 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
    }

    private void BuildRight()
    {
        Ui.Clear(_right);
        Heading(_right, Tr("Notícias"));
        var items = NewsItems().Take(5).ToList();
        if (items.Count == 0)
        {
            Glossary.Rich(_right, Tr("Semana tranquila na cidade."), 16);
        }

        foreach (var (date, source, text) in items)
        {
            NewsCard(_right, date, source, text);
        }

        PollRecord? poll = Campaign.Polls.LastOrDefault();
        Heading(_right, poll is null ? Tr("Intenção de voto") : string.Format(PtBr, Tr("Intenção ({0}, {1:dd/MM})"), _game.Electorate.Pollsters.Get(poll.PollsterId).Name, poll.Week));
        if (poll is null)
        {
            Glossary.Rich(_right, Tr("Nenhuma pesquisa ainda. Encomende uma em Finanças; os institutos publicam sozinhos durante a campanha."), 16);
            return;
        }

        var ordered = poll.Candidates.OrderByDescending(c => c.Value).ToList();
        int me = ordered.FindIndex(c => c.Key == RaceSetup.PlayerId);
        int from = Math.Max(0, me < 0 ? 0 : me - 2);
        foreach (var (id, value) in ordered.Skip(from).Take(5))
        {
            Candidate? c = Campaign.Find(id);
            HBoxContainer row = Ui.Row(_right, 8);
            Label name = Ui.Label(row, $"{(c?.IsPlayer == true ? Tr("Você") : c?.Name)} ({PartySigla(c?.PartyId ?? "")})", 18);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.TooltipText = c?.Name ?? "";
            name.MouseFilter = MouseFilterEnum.Stop;
            name.AddThemeColorOverride("font_color", c?.IsPlayer == true ? Palette.Atencao : Palette.Texto);
            Ui.Value(row, string.Format(PtBr, "{0:0.0}%", value), 18).AddThemeColorOverride("font_color", Palette.Texto);
        }

        Glossary.Rich(_right, string.Format(PtBr, Tr("margem de erro de {0:0.0} pontos"), poll.MarginOfError), 14, Palette.Texto3);
    }

    // ------------------------------------------------------------------ decisões sobre o mapa

    private void BuildBanner()
    {
        if (_banner is not null && IsInstanceValid(_banner))
        {
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

        _banner = _mapArea.Add(new PanelContainer());
        _banner.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 8, 12, Palette.Atencao));
        _banner.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom);
        _banner.OffsetLeft = -360;
        _banner.OffsetRight = 360;
        _banner.OffsetTop = -88;
        _banner.OffsetBottom = -16;
        HBoxContainer row = Ui.Row(_banner, 12);
        Glossary.Rich(row, text, 18, Palette.Texto);
        foreach (var (label, action, tip) in buttons)
        {
            Ui.Button(row, label, action, tip);
        }
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
    /// Cartão da decisão pendente sobre o mapa escurecido; os painéis laterais continuam visíveis. Ao fechar, o fundo
    /// sai junto (nunca há mais de um fundo).
    /// </summary>
    private void ShowPendingEvent()
    {
        if (_eventShade is not null && IsInstanceValid(_eventShade))
        {
            _eventShade.QueueFree();
        }

        _eventShade = null;
        if (Campaign.Events.Pending.FirstOrDefault() is not { } pending)
        {
            return;
        }

        _running = false;
        var shade = _mapArea.Add(new ColorRect { Color = new Color(Palette.Fundo, 0.6f), MouseFilter = MouseFilterEnum.Stop });
        _eventShade = shade;
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = shade.Add(new CenterContainer());
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.Add(new EventDialog(_engine, pending, () =>
        {
            shade.QueueFree();
            if (_eventShade == shade)
            {
                _eventShade = null;
            }

            Refresh();
        }));
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
