using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.UI.Map;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// A tela central fora de campanha (GDD 17.3, 17.14 e 17.15): mandato de vereador, prefeitura e entressafra usam a
/// mesma moldura da campanha (barra do topo com o tempo corrente, trilho de áreas, painel de 400 px, mapa das zonas,
/// notícias e objetivos). Aqui ficam o modo, o tempo, o trilho, a barra, as pausas, a faixa de decisão e as peças que
/// todas as áreas do mandato usam: previsão antes do clique, "o que mudou" depois e bloqueio com motivo.
/// </summary>
public partial class HubScreen
{
    private enum GovKind
    {
        Vereador,
        Prefeito,
        Entressafra,
    }

    private static readonly (string Id, string Icon, string Name, string Tip)[] CouncilAreas =
    [
        ("mandato", "megafone", "Mandato", "O resumo do mandato, a pauta da Câmara com a contagem de votos e a sua instrução de voto."),
        ("gabinete", "pessoas", "Gabinete", "Seus assessores: quem está nomeado e quem pode ser."),
        ("instrumentos", "documento", "Instrumentos", "Requerimento, indicação, fiscalização, emenda, projeto de lei e negociação de voto."),
        ("mesa", "predio", "Mesa e CPI", "A presidência da Câmara, as comissões de inquérito e a composição das cadeiras."),
        ("base", "mapa", "Base", "As zonas que cobram a sua presença e as promessas e dívidas do Livro de compromissos."),
        ("indicadores", "indicador", "Indicadores", "Os números da cidade em unidade real, com fonte, variação e por que mudaram."),
        ("partido", "bandeira", "Partido", "Seu partido, a janela partidária e a fundação de um partido."),
    ];

    private static readonly (string Id, string Icon, string Name, string Tip)[] MayorAreas =
    [
        ("governo", "megafone", "Governo", "O resumo da prefeitura, a aprovação e as crises, cada uma com a causa."),
        ("secretariado", "pessoas", "Secretariado", "Os secretários de cada pasta e os nomes que os partidos oferecem."),
        ("obras", "obra", "Obras", "As obras da cidade por zona: fase, prazo, atraso e licitação."),
        ("fiscal", "cedula", "Pessoal e fiscal", "A folha sobre a receita, os limites da lei, a negociação com os servidores e os mínimos."),
        ("convenios", "acordo", "Convênios", "Dinheiro do estado e da União para obras, com contrapartida e prazo."),
        ("camara", "predio", "Câmara", "A contagem de votos das políticas que precisam de lei e a composição das cadeiras."),
        ("orcamento", "barras", "Orçamento e políticas", "A divisão do orçamento por área e os programas do governo."),
        ("indicadores", "indicador", "Indicadores", "Os números da cidade em unidade real, com fonte, variação e por que mudaram."),
        ("partido", "bandeira", "Partido", "Seu partido e a fundação de um partido."),
    ];

    private static readonly (string Id, string Icon, string Name, string Tip)[] OffSeasonAreas =
    [
        ("entressafra", "calendario", "Agenda", "O que dá para fazer sem cargo e sem eleição aberta: caminhar nas zonas, dar entrevista e ir à pré-campanha."),
        ("liderancas", "pessoas", "Lideranças", "As lideranças que você conhece e as visitas."),
        ("posicoes", "megafone", "Posições", "O que você pensa sobre cada tema, registrado no Livro de compromissos."),
        ("indicadores", "indicador", "Cidade", "Os números da cidade em unidade real e a composição da Câmara."),
        ("partido", "bandeira", "Partido", "Seu partido, a filiação e a fundação de um partido."),
    ];

    /// <summary>Dicas de primeira vez das áreas do mandato, da prefeitura e da entressafra (até 40 palavras; GDD 17.1).</summary>
    private static readonly Dictionary<string, string> GovHints = new(StringComparer.Ordinal)
    {
        ["mandato"] = "O mandato anda em turnos: três por semana para os instrumentos. A pauta mostra quem vota como antes da sessão. Defina uma instrução de voto e o jogo só para nas votações em conflito.",
        ["gabinete"] = "Cada assessor ocupa uma função; o de base cuida de uma zona. Nomear gente da campanha ou indicados de lideranças paga promessas; quem fica de fora lembra. Clique num nome para ver a previsão.",
        ["instrumentos"] = "Cada instrumento gasta turnos e mostra a previsão antes de confirmar. Indicação e emenda pedem uma zona: clique nela no mapa. Fiscalizar pode achar uma irregularidade, e aí há três saídas.",
        ["mesa"] = "A Mesa é eleita a cada dois anos. A CPI precisa de um terço das assinaturas e de um fato determinado, como uma obra com irregularidade conhecida.",
        ["base"] = "As zonas da sua base cobram presença, e cada promessa do Livro tem prazo e alguém que cobra. A lista diz como cumprir cada uma e leva ao instrumento certo.",
        ["governo"] = "A aprovação resume como a cidade vê o governo. Toda crise tem uma causa que aparece aqui: uma negociação que falhou, uma zona sem drenagem, um secretário, um indicador ruim.",
        ["secretariado"] = "Cada pasta tem um titular com competência, lealdade e agenda própria. Nomear o nome de um partido o traz para a base; demitir o tira dela. A competência aparece em faixa nas primeiras semanas.",
        ["obras"] = "Toda obra tem zona, custo e quatro etapas: projeto, licitação, execução e entrega. Escolha a modalidade de licitação: a rápida só vale em emergência. Só se inaugura obra pronta.",
        ["fiscal"] = "A folha é uma fração da receita corrente líquida. A barra mostra os limites de alerta, prudencial e máximo; acima do prudencial, reajuste e contratação ficam vedados.",
        ["convenios"] = "O estado e a União oferecem dinheiro para uma obra, com contrapartida do município e prazo. Convênio que vence sem a obra devolve o dinheiro e bloqueia novos por um tempo.",
        ["camara"] = "Política que precisa de lei vai a voto. A contagem mostra quem vota como e o pedido de cada indeciso: uma obra no reduto, um cargo. Atendido, ele lembra; enganado, também.",
        ["orcamento"] = "O orçamento se divide por área e os indicadores seguem o gasto com atraso. Saúde e educação têm mínimo legal. Programas custam uma fatia da reserva livre.",
        ["indicadores"] = "Cada indicador aparece na unidade real, com fonte e ano. O valor de partida é o dado oficial; daí em diante é simulado. Clique num indicador para ver por que mudou.",
        ["entressafra"] = "Sem cargo e sem eleição aberta, você tem três turnos por semana para manter a presença nas zonas, visitar lideranças, dar entrevista e assumir posições. Tudo isso vai com você para a próxima campanha.",
        ["liderancas"] = "Visitar uma liderança fora de época de eleição melhora a relação e fica na memória dela. Cada visita gasta um turno, e a mesma pessoa só rende de novo depois de algumas semanas.",
        ["posicoes"] = "Assumir uma posição a registra no Livro de compromissos, com a data. Mudar depois deixa as duas no Livro, e rivais e imprensa podem usar a mudança na campanha.",
    };

    private readonly Stack<(string Sheet, string? Id)> _govBack = new();
    private readonly HashSet<string> _govSeen = new(StringComparer.Ordinal);
    private bool _gov;
    private GovKind _govKind;
    private string _govSheet = "";
    private string? _govId;
    private string? _govZone;
    private string? _govPick;
    private string? _govResultTitle;
    private GovResult? _govResult;
    private string? _govConfirm;

    /// <summary>Área do mandato aberta no painel ("mandato", "gabinete", "obras"…); testes de fumaça.</summary>
    public string AreaName => _area;

    /// <summary>Ficha do mandato aberta no painel ("" = nenhuma); testes de fumaça.</summary>
    public string GovSheetName => _govSheet;

    /// <summary>Zona escolhida para o comando aberto (emenda, indicação, obra, caminhada); testes de fumaça.</summary>
    public string? GovZone => _govZone;

    /// <summary>Texto do último resultado mostrado em "O que mudou"; testes de fumaça.</summary>
    public string? GovResultText => _govResult is null ? null : _govResult.Text + "\n" + string.Join("\n", _govResult.Changes);

    /// <summary>Tela central fora de campanha (mandato, prefeitura ou entressafra).</summary>
    public bool GovMode => _gov;

    private GovKind Kind => State.Executive is not null ? GovKind.Prefeito : State.Mandate is not null ? GovKind.Vereador : GovKind.Entressafra;

    private GovernanceEngine? Gov => _game.Governo is { Active: true } engine ? engine : null;

    private OffSeasonEngine? Off => _game.Entressafra;

    private Core.Municipios.Municipio City => _game.Municipios.All.First(m => m.CodigoIbge == State.FocusMunicipio);

    private (string Id, string Icon, string Name, string Tip)[] GovAreas => Kind switch
    {
        GovKind.Prefeito => MayorAreas,
        GovKind.Vereador => CouncilAreas,
        _ => OffSeasonAreas,
    };

    private string GZone(string? id) => id is not null && _map.Zonas.TryGetValue(id, out Zona? z) ? z.Nome : "";

    private string GovName(string? personId) =>
        personId is null ? "" : personId == RaceSetup.PlayerId ? Tr("você") : State.Npcs.FirstOrDefault(n => n.Id == personId)?.BallotName ?? Gov?.NameOf(personId) ?? "";

    private string GovPartyOf(string? partyId) => partyId is null ? Tr("sem partido") : State.Party(partyId)?.Sigla ?? partyId;

    // ------------------------------------------------------------------ modo

    /// <summary>
    /// Acerta o modo da tela com o estado: sem campanha aberta, a tela mostra o mandato, a prefeitura ou a entressafra;
    /// com campanha, a campanha. Troca de modo sem trocar de cena (posse, fim de mandato, abertura da pré-campanha).
    /// </summary>
    private void SyncMode()
    {
        bool gov = State.Campaign is null;
        GovKind kind = Kind;
        bool sameEngine = gov || ReferenceEquals(_engine, _game.Engine);
        if (gov == _gov && sameEngine && (!gov || (kind == _govKind && GovAreas.Any(a => a.Id == _area))))
        {
            return;
        }

        _gov = gov;
        _govKind = kind;
        _engine = _game.Engine!;
        _sheet = Sheet.Nenhuma;
        _sheetId = null;
        _back.Clear();
        _draft = null;
        ResetGovSheet();
        _area = gov ? GovAreas[0].Id : "campanha";
        _mapMode = !gov ? "forca" : kind == GovKind.Prefeito ? "obras" : "base";
        _running = false;
        _notice = null;
        RememberDeadlines();
        LoadZones();
    }

    private void ResetGovSheet()
    {
        _govSheet = "";
        _govId = null;
        _govZone = null;
        _govPick = null;
        _govConfirm = null;
        _govBack.Clear();
    }

    /// <summary>Abre uma área do mandato pelo id (testes de fumaça e atalhos entre áreas).</summary>
    public void OpenGovArea(string area)
    {
        if (!GovAreas.Any(a => a.Id == area) && !(area == CampaignGovArea && !_gov))
        {
            return;
        }

        OpenArea(area);
    }

    private void OpenGov(string sheet, string? id, string? zone = null)
    {
        if (_govSheet.Length > 0 && (_govSheet != sheet || _govId != id))
        {
            _govBack.Push((_govSheet, _govId));
        }

        _govSheet = sheet;
        _govId = id;
        _govZone = zone;
        _govPick = null;
        _govConfirm = null;
        Refresh();
    }

    private void GovBack()
    {
        (_govSheet, _govId) = _govBack.Count > 0 ? _govBack.Pop() : ("", null);
        _govZone = _govSheet == "zona" ? _govId : null;
        _govPick = null;
        _govConfirm = null;
        _map.Select(_govZone);
        Refresh();
    }

    private void GovBackButton()
    {
        var b = Ui.Button(_left, "Voltar", GovBack, Palette.Tip(Tr("Fecha esta ficha."), Tr("volta ao que estava aberto antes no painel."), Tr("nada.")));
        b.Name = "Voltar";
        b.Icon = Palette.Icon("voltar");
        b.ExpandIcon = true;
        b.Alignment = HorizontalAlignment.Left;
        b.CustomMinimumSize = new Vector2(132, 44);
        b.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    }

    /// <summary>Clique no mapa fora de campanha: escolhe a zona do comando aberto ou abre a ficha da zona.</summary>
    private bool GovZoneClicked(string? id)
    {
        if (!_gov && !_area.StartsWith("gov_", StringComparison.Ordinal))
        {
            return false;
        }

        if (_govPick is not null)
        {
            if (id is not null)
            {
                _govZone = id;
            }

            _map.Select(_govZone);
            Refresh();
            return true;
        }

        if (!_gov)
        {
            return false;
        }

        if (id is null)
        {
            if (_govSheet == "zona")
            {
                GovBack();
            }
            else
            {
                Refresh();
            }

            return true;
        }

        _govBack.Clear();
        _govSheet = "zona";
        _govId = id;
        _govZone = id;
        Refresh();
        return true;
    }

    // ------------------------------------------------------------------ tempo

    private void GovProcess(double delta)
    {
        if (GovBlockReason() is not null)
        {
            _running = false;
            Refresh();
            return;
        }

        _accumulated += delta;
        if (_accumulated >= Clock.SecondsPerDay(_game.Time, Clock.ModoMandato, _speed))
        {
            _accumulated = 0;
            StepGovDay();
        }
    }

    /// <summary>O que impede o tempo de andar fora de campanha: só o que não tem padrão (crise na mesa do prefeito).</summary>
    private string? GovBlockReason() =>
        State.Executive?.Crisis is not null ? Tr("há uma crise esperando a sua decisão")
        : GovCardWanted && !_govCardHidden ? Tr("há uma decisão aberta sobre o mapa")
        : null;

    /// <summary>Um dia do mandato ou da entressafra; na segunda-feira roda a semana da carreira e as pausas são conferidas.</summary>
    private void StepGovDay()
    {
        if (GovBlockReason() is not null)
        {
            _running = false;
            Refresh();
            return;
        }

        GovKind before = Kind;
        Game.DayReport report = _game.AdvanceDay();
        if (report.WeekTurned)
        {
            SaveManager.Instance.Autosave();
            string? line = report.Note switch
            {
                Game.WeekNote.DiplomaCassado => Tr("A Justiça Eleitoral cassou o seu diploma por causa das contas da campanha. O mandato acabou."),
                Game.WeekNote.MandatoEncerrado => Tr("Seu mandato terminou."),
                Game.WeekNote.SemPartidoNoPrazo => Tr("Passou o prazo de filiação sem partido válido: você não disputa a eleição deste ano."),
                Game.WeekNote.SemIdade => Tr("Você ainda não tem idade para disputar esta eleição."),
                Game.WeekNote.CampanhaAberta => Tr("Abriu a pré-campanha da próxima eleição."),
                _ => null,
            };
            if (line is not null)
            {
                _log.Insert(0, line);
                _running = false;
                _notice = new Notice(line);
            }

            // Contas da eleição anterior: prestação, julgamento ou representação com novidade nesta semana.
            if (line is null && _game.AccountsNews.Count > 0 && Clock.ShouldPause(_game.Time, Clock.DecisaoImediata, Settings.PauseOverrides))
            {
                _running = false;
                _notice = AccountsNotice();
            }

            if (State.Campaign is not null || Kind != before)
            {
                // Mudou o momento da carreira: a tela se refaz no modo novo (em jogo, a cena é recarregada).
                _running = false;
                _game.GoToCareer();
                Refresh();
                return;
            }

            CheckGovPauses();
        }

        Refresh();
    }

    /// <summary>Faixa com as novidades das contas da eleição anterior, com o atalho para o resumo onde está o andamento.</summary>
    private Notice AccountsNotice() =>
        new(string.Join(". ", _game.AccountsNews.Select(h => h.Text)) + ".", "Ver as contas", () => { _notice = null; OpenGovArea(GovAreas[0].Id); },
            Palette.Tip(Tr("Abre o resumo, com o andamento das contas."), Tr("mostra a prestação de contas final, o julgamento das contas da eleição anterior e a representação, se houver."), Tr("nada.")));

    private void GovStop(string id, Notice notice)
    {
        if (!_govSeen.Add(id))
        {
            return;
        }

        _running = false;
        _notice ??= notice;
    }

    /// <summary>Pausas automáticas do mandato (GDD 17.4): decisão que chegou, votação em conflito, achado, mesa aberta, cobrança e luto.</summary>
    private void CheckGovPauses()
    {
        var overrides = Settings.PauseOverrides;
        bool Pause(string id) => Clock.ShouldPause(_game.Time, id, overrides);
        if (Gov is not { } gov)
        {
            return;
        }

        GovernanceState g = gov.G;
        if (g.Decision is { } decision && Pause(Clock.DecisaoImediata) && _govSeen.Add("decisao:" + decision.Id))
        {
            _running = false;
        }

        if (State.Executive?.Crisis is not null)
        {
            _running = false;
        }

        if (Kind == GovKind.Vereador && _game.Mandate is { } mandate && !mandate.InRecess && !mandate.InMourning && Pause(Clock.VotacaoNaPauta))
        {
            var conflicts = gov.VoteConflicts().Where(c => c.Bill.PlayerVote is null).ToList();
            var open = mandate.Pauta.Where(b => b.PlayerVote is null).ToList();
            if (conflicts.Count > 0)
            {
                GovStop("conflito:" + conflicts[0].Bill.Id, new Notice(string.Format(Tr("Votação em conflito na pauta: {0}."), conflicts[0].Bill.Title), "Abrir a pauta", () => OpenGovArea("mandato"),
                    Palette.Tip(Tr("Abre a pauta da Câmara."), Tr("mostra a contagem de votos, o motivo do conflito e os três votos possíveis."), Tr("nada."))));
            }
            else if (open.Count > 0 && g.Instruction == VoteInstruction.Nenhuma)
            {
                GovStop("pauta:" + open[0].Id, new Notice(string.Format(Tr(open.Count == 1 ? "Há {0} votação na pauta desta semana e você não marcou o voto." : "Há {0} votações na pauta desta semana e você não marcou o voto."), open.Count), "Abrir a pauta", () => OpenGovArea("mandato"),
                    Palette.Tip(Tr("Abre a pauta da Câmara."), Tr("sem voto marcado e sem instrução de voto, a sessão conta como falta."), Tr("nada."))));
            }
        }

        if (g.Inspections.FirstOrDefault(i => i.Pending) is { } finding)
        {
            GovStop("achado:" + finding.Id, new Notice(Tr("A fiscalização trouxe um achado e espera a sua decisão."), "Ver o achado", () => OpenGov("achado", finding.Id),
                Palette.Tip(Tr("Abre o achado da fiscalização."), Tr("mostra o que foi encontrado e as três saídas, cada uma com a previsão."), Tr("nada."))));
        }

        if (Kind == GovKind.Prefeito && gov.PendingNegotiation is { } negotiation)
        {
            GovStop("mesa:" + negotiation.Id, new Notice(Tr("Abriu a data-base dos servidores: há um pedido de reajuste na mesa."), "Abrir a mesa", () => { OpenGovArea("fiscal"); OpenGov("negociacao", negotiation.Id); },
                Palette.Tip(Tr("Abre a mesa de negociação."), Tr("mostra o pedido, a folha de hoje contra os limites da lei e as respostas possíveis."), Tr("nada."))));
        }

        if (g.Demands.LastOrDefault(d => d.Met is null && d.Kind != Demand.Presenca && d.Date >= State.Calendar.WeekStart.AddDays(-7)) is { } demand && _govSeen.Add("cobranca:" + demand.Id))
        {
            _running = false;
            _govDemand = demand.Id;
        }

        if (State.Renewal is { } renewal && Core.Renewal.RenewalQueries.InMourning(renewal, State.Calendar.WeekStart) && renewal.LutoAte is { } until)
        {
            GovStop("luto:" + until.DayNumber, new Notice(string.Format(Tr("Luto oficial até {0}: a Câmara não faz sessão nesta semana."), until.ToString("d 'de' MMMM", PtBr))));
        }

        if (g.Works.FirstOrDefault(w => w.Stage == WorkStage.Pronta && GovernanceQueries.Works(gov).Any(v => v.Work.Id == w.Id && v.ByPlayer)) is { } ready)
        {
            GovStop("pronta:" + ready.Id, new Notice(string.Format(Tr("Obra pronta para a entrega: {0} em {1}."), gov.WorkName(ready), GZone(ready.ZoneId)), "Ver a obra", () => OpenGov("obra", ready.Id),
                Palette.Tip(Tr("Abre a ficha da obra."), Tr("mostra a previsão de inaugurar e de entregar sem cerimônia."), Tr("nada."))));
        }
    }

    // ------------------------------------------------------------------ montagem

    private void RefreshGov()
    {
        BuildGovTop();
        BuildGovTrail();
        Ui.Clear(_left);
        BuildGovLeft();
        BuildGovMap();
        BuildGovRight();
        BuildGovBanner();
        ShowGovCard();
    }

    private void BuildGovTop()
    {
        Ui.Clear(_top);
        DateOnly today = Clock.Today(State);
        Label date = Ui.Value(_top, today.ToString("ddd, d MMM yyyy", PtBr), 24);
        date.AddThemeFontOverride("font", Palette.Weight(Palette.Oswald, 500));
        date.AddThemeColorOverride("font_color", Palette.Texto);
        date.CustomMinimumSize = new Vector2(190, 0);
        date.MouseFilter = MouseFilterEnum.Stop;
        date.TooltipText = Palette.Tip(Tr("Data do jogo. O dia é a unidade do tempo."), Tr("fora de campanha o tempo corre mais depressa; a cada segunda-feira a cidade se mexe: Câmara, obras, folha, cobranças e a renovação de quem está na política."), Tr("nada."));

        string? block = GovBlockReason();
        var pause = IconButton(_top, "pausa", !_running, () => { _running = false; Refresh(); },
            Palette.Tip(Tr("Pausa o tempo."), Tr("o dia para; você pode olhar o mapa, usar os instrumentos e decidir com calma."), Tr("nada.")));
        pause.Modulate = !_running && (block is not null || _notice is not null || Gov?.G.Decision is not null) ? Palette.Atencao : Colors.White;
        pause.Name = "Pausa";
        foreach (int speed in _game.Time.Velocidades)
        {
            double seconds = Clock.SecondsPerDay(_game.Time, Clock.ModoMandato, speed);
            string tip = Palette.Tip(
                string.Format(Tr("Tempo em velocidade {0}x."), speed),
                string.Format(PtBr, Tr("cada dia de jogo passa em {0:0.##} s; o jogo para sozinho quando algo pede decisão."), seconds),
                Tr("nada."));
            var b = Ui.Button(_top, $"{speed}x", () => SetSpeed(speed), tip);
            b.Name = "Velocidade_" + speed;
            b.ToggleMode = true;
            b.ButtonPressed = _running && _speed == speed;
            b.CustomMinimumSize = new Vector2(56, 44);
            if (block is not null)
            {
                b.Disabled = true;
                b.TooltipText = Palette.Blocked(string.Format(Tr("{0}. Resolva antes de soltar o tempo."), block), tip);
            }
        }

        Core.Municipios.Municipio city = City;
        string phase;
        switch (Kind)
        {
            case GovKind.Prefeito:
                phase = string.Format(Tr("Prefeitura · {0} ({1}) · faltam {2} sem."), city.Nome, city.Uf, WeeksUntil(State.Executive!.End));
                break;
            case GovKind.Vereador:
                phase = string.Format(Tr("Vereador · {0} ({1}) · faltam {2} sem."), city.Nome, city.Uf, WeeksUntil(State.Mandate!.End));
                break;
            default:
                int year = CareerFlow.NextEligibleElectionYear(State);
                phase = string.Format(Tr("Sem mandato · {0} ({1}) · pré-campanha em {2} sem."), city.Nome, city.Uf, WeeksUntil(CareerFlow.CampaignOpens(year)));
                break;
        }

        var phaseLabel = Glossary.Rich(_top, phase, 18);
        phaseLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        phaseLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        phaseLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        phaseLabel.ClipContents = true;

        // O rótulo cede quando a barra aperta (nome de cidade comprido): corta e mostra o texto inteiro no tooltip.
        phaseLabel.FitContent = false;
        phaseLabel.CustomMinimumSize = new Vector2(220, 28);
        phaseLabel.MouseFilter = MouseFilterEnum.Stop;
        phaseLabel.TooltipText = Palette.Tip(phase + ".", Tr("mostra o cargo, a cidade e quanto falta para o próximo marco da carreira."), Tr("nada."));

        // No máximo cinco números na barra (ART_GUIDE, densidade); o resto fica no painel da área.
        if (Gov is { } gov)
        {
            GovernanceSummary s = GovernanceQueries.Summary(gov);
            if (Kind == GovKind.Vereador)
            {
                Stat(Tr("turnos"), string.Format(PtBr, "{0}/{1}", s.TurnsLeft, _game.GovernanceContent.Instruments.Regras.TurnosPorSemana), Tr("Turnos da semana para os instrumentos do mandato."),
                    Tr("cada instrumento gasta turnos; eles voltam toda segunda-feira."));
                Stat(Tr("capital"), s.Capital.ToString(PtBr), Tr("Capital político: o crédito que você tem para articular."), Tr("paga a emenda, o projeto de lei e a candidatura à Mesa; cresce com votações ganhas e acordos cumpridos."));
                Stat(Tr("sua força"), s.CityPresence.ToString("0", PtBr), Tr("Sua presença na cidade, de 0 a 100: a média das zonas pela população."), Tr("cai um pouco toda semana nas zonas em que você não aparece; é com ela que a campanha de reeleição começa."));
                ImageStat(State.Mandate!.Image, Tr("muda com os seus votos, as faltas e os instrumentos do mandato, e é com ela que a campanha de reeleição começa."));
                Stat(Tr("com o partido"), State.Mandate!.Fidelity.ToString(PtBr), Tr("Fidelidade ao partido, de 0 a 100."), Tr("cai quando você vota contra a orientação; baixa demais, o partido adverte, pune e pode expulsar."));
                Stat(Tr("governo"), string.Format(PtBr, "{0:0}%", s.Approval), Tr("Aprovação do governo da cidade, que hoje é de outro: o prefeito."), Tr("com aprovação baixa e escândalo conhecido, a Câmara abre CPI e pode cassar o prefeito."));
            }
            else
            {
                FiscalView fiscal = GovernanceQueries.Fiscal(gov);
                Stat(Tr("aprovação"), string.Format(PtBr, "{0:0}%", s.Approval), Tr("Aprovação do seu governo na cidade."), Tr("sobe com indicadores melhores e obras entregues; cai com crises, atrasos e promessas vencidas."));
                ImageStat(State.Executive!.Image, Tr("pesa na aprovação do governo; muda com as decisões, as crises e as votações da Câmara."));
                Stat(Tr("energia"), string.Format(PtBr, "{0}/{1}", State.Executive!.EnergyLeft, _game.ExecutiveContent.Config.WeeklyEnergy), Tr("Energia da semana para os atos do prefeito."), Tr("gasta ao decretar, mandar projeto à Câmara e agir pelo gabinete; volta toda segunda-feira."));
                Stat(Tr("folha"), string.Format(PtBr, "{0:0.0}%", fiscal.PayrollShare * 100), Tr("Folha de pagamento do Executivo sobre a receita corrente líquida."),
                    string.Format(PtBr, Tr("alerta em {0:0.0}%, limite prudencial em {1:0.0}% e limite máximo em {2:0.0}%; acima do prudencial, reajuste e contratação ficam vedados."), fiscal.AlertAt * 100, fiscal.PrudentialAt * 100, fiscal.LimitAt * 100),
                    fiscal.Band >= PayrollBand.Prudencial ? Palette.Negativo : null);
                Stat(Tr("obras"), GovMoney(fiscal.InvestmentLeft), Tr("Quanto ainda cabe de obra nova neste ano."), Tr("é a parte da fatia de obras do orçamento que pode virar investimento; cada obra iniciada desconta daqui."));
                Stat(Tr("capital"), s.Capital.ToString(PtBr), Tr("Capital político: o crédito que você tem para articular."), Tr("paga acordos e respostas a crises; cresce com entregas e acordos cumpridos."));
            }
        }
        else if (Off is { } off)
        {
            Stat(Tr("turnos"), string.Format(PtBr, "{0}/{1}", off.S.TurnsLeft, _game.OffSeasonConfig.TurnosPorSemana), Tr("Turnos da semana para a agenda fora de campanha."), Tr("caminhada, visita, entrevista e posição gastam um turno cada; eles voltam toda segunda-feira."));
            Stat(Tr("sua força"), off.S.CityPresence().ToString("0", PtBr), Tr("Sua presença na cidade, de 0 a 100: a média das zonas pela população."), Tr("cai um pouco toda semana sem agenda; é com ela que a próxima campanha começa."));
            Stat(Tr("caixa"), Money(State.Player.Resources.Money), Tr("Seu dinheiro."), Tr("fora de campanha nada da agenda custa dinheiro; ele fica guardado para a campanha."));
            Stat(Tr("capital"), State.Player.Resources.PoliticalCapital.ToString(PtBr), Tr("Capital político: o crédito que você tem para articular."), Tr("é o que paga a fundação de um partido."));
        }

        Ui.Button(_top, "Mapa", () => _game.GoTo(Game.Scenes.Map), Palette.Tip(Tr("Mapa do seu estado em 2.5D, com a força dos partidos por município."), Tr("abre o mapa do estado; Voltar retorna a esta tela."), Tr("nada; o jogo fica pausado.")));
        Ui.Button(_top, "Salvar", ShowSaveMenu, Palette.Tip(Tr("Grava a carreira."), Tr("salva num dos slots manuais (no Ironman, no save único)."), Tr("nada.")));
        Ui.Button(_top, "Menu", () => { SaveManager.Instance.Autosave(); _game.GoTo(Game.Scenes.MainMenu); }, Palette.Tip(Tr("Volta ao menu principal."), Tr("salva automaticamente e sai da carreira."), Tr("nada.")));
        IconButton(_top, "ajuda", false, () => EncyclopediaDialog.Open(GetTree().Root), Palette.Tip(Tr("Enciclopédia (F1)."), Tr("explica termos, as regras reais do mandato e da prefeitura, quem é quem neste mundo e o seu Livro de compromissos."), Tr("nada; o jogo fica pausado.")));
    }

    private int WeeksUntil(DateOnly date) => Math.Max(0, (date.DayNumber - Clock.Today(State).DayNumber + 6) / 7);

    /// <summary>Valor em reais em escala legível: "R$ 12.300", "R$ 4,2 mi".</summary>
    private string GovMoney(decimal value)
    {
        decimal abs = Math.Abs(value);
        string sign = value < 0 ? "−" : "";
        return abs >= 1_000_000_000 ? string.Create(PtBr, $"{sign}R$ {abs / 1_000_000_000:0.00} bi")
            : abs >= 1_000_000 ? string.Create(PtBr, $"{sign}R$ {abs / 1_000_000:0.0} mi")
            : string.Create(PtBr, $"{sign}R$ {abs:N0}");
    }

    /// <summary>Nome abreviado termina em ponto ("Larissa R."): a frase não ganha um segundo.</summary>
    private static string OneDot(string text)
    {
        while (text.Contains("..", StringComparison.Ordinal))
        {
            text = text.Replace("..", ".", StringComparison.Ordinal);
        }

        return text;
    }

    private void BuildGovTrail()
    {
        Ui.Clear(_trail);
        foreach (var (id, icon, name, tip) in GovAreas)
        {
            var b = IconButton(_trail, icon, _area == id, () => OpenArea(id), Palette.Tip($"{Tr(name)}: {Tr(tip)}", Tr("mostra esta área no painel da esquerda; o mapa continua visível."), Tr("nada.")));
            b.Name = "Area_" + id;
        }
    }

    /// <summary>Área que o trilho da campanha ganha quando há mandato em curso (campanha de reeleição).</summary>
    private const string CampaignGovArea = "gov_obras";

    /// <summary>Na campanha de reeleição, o trilho ganha a área das obras do mandato (inaugurar e o período vedado).</summary>
    private void GovTrailExtra()
    {
        if (Gov is null)
        {
            return;
        }

        var b = IconButton(_trail, "obra", _area == CampaignGovArea, () => OpenArea(CampaignGovArea),
            Palette.Tip(Tr("Obras do mandato: as obras da cidade e as que são suas para entregar."), Tr("mostra esta área no painel da esquerda; na campanha, o mandato segue sozinho, mas a entrega de obra pronta é com você."), Tr("nada.")));
        b.Name = "Area_" + CampaignGovArea;
    }

    private void GovHint(string key)
    {
        if (!Settings.HintsEnabled || Settings.HintSeen(key) || !GovHints.TryGetValue(key, out string? text))
        {
            return;
        }

        PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Acao);
        card.Name = "Dica_" + key;
        var col = Ui.Column(card, 8);
        Glossary.Rich(col, Tr(text), 18, Palette.Texto);
        var row = Ui.Row(col, 8);
        Ui.Button(row, "Entendi", () => { Settings.MarkHintSeen(key); Refresh(); }, Palette.Tip(Tr("Fecha esta dica."), Tr("ela não volta; tudo o que ela diz está na Enciclopédia (F1)."), Tr("nada.")));
        Ui.Button(row, "Sem dicas", () => { Settings.DisableHints(); Refresh(); }, Palette.Tip(Tr("Desliga todas as dicas de primeira vez."), Tr("nenhuma dica volta."), Tr("nada.")));
    }

    /// <summary>
    /// Painel da esquerda nas áreas do mandato. Devolve false quando a área aberta é da campanha (a tela segue o
    /// caminho de sempre).
    /// </summary>
    private bool BuildGovLeft()
    {
        if (!_gov && _area != CampaignGovArea)
        {
            return false;
        }

        if (!_gov && _sheet != Sheet.Nenhuma)
        {
            return false;
        }

        GovResultCard();
        if (_govSheet.Length > 0 && BuildGovSheet())
        {
            return true;
        }

        if (_govSheet.Length > 0)
        {
            // A ficha perdeu o alvo (obra entregue, pessoa que saiu): volta à área.
            ResetGovSheet();
        }

        if (!_gov)
        {
            Heading(_left, Tr("Obras do mandato"));
            GovHint("obras");
            BuildWorksArea();
            return true;
        }

        var area = GovAreas.FirstOrDefault(a => a.Id == _area);
        Heading(_left, Tr(area.Name ?? ""));
        GovHint(_area);

        // Contas da última eleição (GDD 17.12): o andamento fica no resumo do mandato, da prefeitura e da entressafra;
        // no alto quando há novidade na semana, e no fim do resumo no resto do tempo, para não empurrar a agenda.
        bool accountsHere = _area == GovAreas[0].Id;
        if (accountsHere && _game.AccountsNews.Count > 0)
        {
            AccountsCasePanel.BuildFromGame(_left);
            accountsHere = false;
        }

        switch (_area)
        {
            case "mandato": BuildCouncilArea(); break;
            case "gabinete": BuildCabinetArea(); break;
            case "instrumentos": BuildInstrumentsArea(); break;
            case "mesa": BuildMesaArea(); break;
            case "base": BuildBaseArea(); break;
            case "governo": BuildCityHallArea(); break;
            case "secretariado": BuildSecretariesArea(); break;
            case "obras": BuildWorksArea(); break;
            case "fiscal": BuildFiscalArea(); break;
            case "convenios": BuildAgreementsArea(); break;
            case "camara": BuildChamberArea(); break;
            case "orcamento": BuildBudgetPoliciesArea(); break;
            case "indicadores": BuildIndicatorsArea(); break;
            case "partido": BuildGovParty(); break;
            case "liderancas": BuildOffLeaders(); break;
            case "posicoes": BuildOffPositions(); break;
            default: BuildOffAgenda(); break;
        }

        if (accountsHere)
        {
            AccountsCasePanel.BuildFromGame(_left);
        }

        GovLog();
        return true;
    }

    private bool BuildGovSheet()
    {
        switch (_govSheet)
        {
            case "zona" when _govId is not null && _map.Zonas.ContainsKey(_govId): BuildGovZoneSheet(_map.Zonas[_govId]); return true;
            case "obra" when Gov is { } gov && _govId is not null && gov.G.Work(_govId) is { } work: BuildWorkSheet(gov, work); return true;
            case "nova_obra" when Gov is not null: BuildNewWorkSheet(); return true;
            case "instrumento" when Gov is not null && _govId is not null: BuildInstrumentSheet(_govId); return true;
            case "achado" when Gov is { } gov && gov.G.Inspections.FirstOrDefault(i => i.Id == _govId) is { } inspection: BuildFindingSheet(gov, inspection); return true;
            case "assessor" when Gov is { } gov && gov.G.Aides.FirstOrDefault(a => a.PersonId == _govId) is { } aide: BuildAideSheet(gov, aide); return true;
            case "pasta" when Gov is not null && _govId is not null: BuildSecretariaSheet(_govId); return true;
            case "contagem" when Gov is not null && _govId is not null: return BuildCountSheet(_govId);
            case "negociacao" when Gov is { } gov && gov.G.Negotiations.FirstOrDefault(n => n.Id == _govId) is { } negotiation: BuildNegotiationSheet(gov, negotiation); return true;
            case "convenio" when Gov is { } gov && gov.G.Agreements.FirstOrDefault(a => a.Id == _govId) is { } agreement: BuildAgreementSheet(gov, agreement); return true;
            case "politica" when Gov is not null && _govId is not null && _game.ExecutiveContent.Policies.Find(_govId) is { } policy: BuildPolicySheet(policy); return true;
            case "indicador" when _govId is not null && State.Indicators?.Find(_govId) is not null: BuildIndicatorSheet(_govId); return true;
            case "cadeiras" when State.Renewal is not null: BuildSeatsSheet(); return true;
            case "pessoa" when _govId is not null: BuildGovPersonSheet(_govId); return true;
            case "livro" when Gov is { } gov:
                _govPick = null;
                GovBackButton();
                NameTitle(_left, Tr("Livro de compromissos"), 22);
                BuildLedgerBlock(gov);
                return true;
            case "caminhada" when Off is not null: BuildOffWalkSheet(); return true;
            case "entrevista" when Off is not null: BuildOffInterviewSheet(); return true;
            case "posicao" when Off is not null && _govId is not null: BuildOffPositionSheet(_govId); return true;
            case "visita" when Off is not null && _govId is not null: BuildOffVisitSheet(_govId); return true;
            default: return false;
        }
    }

    private void GovLog()
    {
        if (_log.Count == 0)
        {
            return;
        }

        Heading(_left, Tr("O que você fez"), 20);
        foreach (string line in _log.Take(5))
        {
            Glossary.Rich(_left, "· " + line);
        }
    }

    // ------------------------------------------------------------------ previsão, resultado e bloqueio

    private string GovFailureText(GovFailure failure) => failure switch
    {
        GovFailure.SemTurno => Tr("você já usou os turnos desta semana; eles voltam na segunda-feira."),
        GovFailure.SemCapital => Tr("falta capital político."),
        GovFailure.SemVaga => Tr("não há vaga livre para esta função."),
        GovFailure.SemEmenda => Tr("as emendas deste ano acabaram; em janeiro há novas."),
        GovFailure.SemInvestimento => Tr("o investimento do ano não cobre esta obra."),
        GovFailure.AlvoInvalido => Tr("escolha um alvo válido (uma zona, uma obra ou uma pessoa)."),
        GovFailure.LimiteLegal => Tr("passa de um limite da lei."),
        GovFailure.PeriodoVedado => Tr("candidato não comparece a inauguração nos três meses antes da eleição (Lei 9.504/1997, art. 77)."),
        GovFailure.SemObraPronta => Tr("a obra ainda não está pronta."),
        GovFailure.VedadoArt22 => Tr("a folha está acima do limite prudencial (LC 101/2000, art. 22)."),
        GovFailure.JaFeito => Tr("isso já foi feito."),
        GovFailure.PapelErrado => Tr("este comando não é do seu cargo."),
        GovFailure.NaoEncontrado => Tr("o alvo não existe mais."),
        _ => Tr("não é possível agora."),
    };

    private string GovReason(GovForecast forecast) =>
        !string.IsNullOrWhiteSpace(forecast.BlockedReason) ? forecast.BlockedReason!.TrimEnd('.') + "." : forecast.Blocked is { } failure ? GovFailureText(failure) : "";

    private string GovRange(ForecastLine l, string format, double scale = 1) => Range(l.Min * scale, l.Max * scale, format);

    /// <summary>Uma linha da previsão por extenso, com a faixa: "presença em Centro +0,8 a +1,4".</summary>
    private string GovLine(ForecastLine l) => l.Kind switch
    {
        GovForecast.Presenca => l.Target is null ? string.Format(Tr("presença em todas as zonas, em média: {0}"), GovRange(l, "0.0")) : string.Format(Tr("presença em {0}: {1}"), GZone(l.Target), GovRange(l, "0.0")),
        GovForecast.PresencaDepois => string.Format(Tr("presença em {0} quando a obra for entregue: {1}"), GZone(l.Target), GovRange(l, "0.0")),
        GovForecast.Imagem => string.Format(Tr("imagem: {0}"), GovRange(l, "0.00")),
        GovForecast.Imagem + "_later" => string.Format(Tr("imagem, se a resposta revelar irregularidade: {0}"), GovRange(l, "0.00")),
        GovForecast.Capital => string.Format(Tr("capital político: {0}"), GovRange(l, "0")),
        GovForecast.RelacaoPrefeito => string.Format(Tr("relação com o prefeito: {0}"), GovRange(l, "0")),
        GovForecast.Relacao => string.Format(Tr("relação com {0}: {1}"), GovName(l.Target), GovRange(l, "0")),
        GovForecast.Aprovacao => string.Format(Tr("aprovação do governo: {0} pontos"), GovRange(l, "0.0")),
        GovForecast.AprovacaoZona => string.Format(Tr("aprovação em {0}: {1} pontos"), GZone(l.Target), GovRange(l, "0.0")),
        GovForecast.Folha => string.Format(Tr("folha: {0} ponto(s) da receita corrente líquida"), GovRange(l, "0.00", 100)),
        GovForecast.Dinheiro => string.Format(Tr("caixa: {0}"), Math.Abs(l.Max - l.Min) < 0.5 ? SignedMoney((decimal)l.Expected) : $"{SignedMoney((decimal)l.Min)} {Tr("a")} {SignedMoney((decimal)l.Max)}"),
        GovForecast.Investimento => string.Format(Tr("investimento do ano: {0}"), (l.Expected < 0 ? "−" : "+") + GovMoney(Math.Abs((decimal)l.Expected))),
        GovForecast.PromessaCumprida => Tr("cumpre promessa do Livro de compromissos"),
        GovForecast.Turnos => string.Format(Tr(Math.Abs(l.Expected) == 1 ? "{0} turno da semana" : "{0} turnos da semana"), Math.Abs(l.Expected).ToString("0", PtBr)),
        GovForecast.Esqueleto => string.Format(Tr("esqueleto no Livro de compromissos, gravidade {0}"), l.Expected.ToString("0", PtBr)),
        GovForecast.Indicador => string.Format(Tr("{0}: {1} ponto(s) na escala de 0 a 100, na hora"), IndicatorName(l.Target), GovRange(l, "0.0")),
        GovForecast.IndicadorAlvo => string.Format(Tr("alvo de {0}: {1} ponto(s) na escala de 0 a 100"), IndicatorName(l.Target), GovRange(l, "0.0")),
        GovForecast.Apoio => string.Format(Tr("apoio de {0}: {1} ponto(s)"), EffectSummary.GroupName(l.Target ?? ""), GovRange(l, "0.0", 100)),
        GovForecast.Reserva => string.Format(Tr("reserva livre: {0} ponto(s) do orçamento ao ano ({1})"), GovRange(l, "0.0", 100), SignedMoney((Mayor?.AnnualRevenue ?? 0) * (decimal)l.Expected)),
        GovForecast.Energia => string.Format(Tr("{0} de energia da semana"), Math.Abs(l.Expected).ToString("0", PtBr)),
        GovForecast.VotosSim => Math.Abs(l.Max - l.Min) < 0.5 ? string.Format(Tr("{0} voto(s) a favor"), l.Min.ToString("0", PtBr)) : string.Format(Tr("de {0} a {1} votos a favor"), l.Min.ToString("0", PtBr), l.Max.ToString("0", PtBr)),
        GovForecast.Reconhecimento => string.Format(Tr("reconhecimento: {0}"), GovRange(l, "0.0")),
        GovForecast.RelacaoCamara => string.Format(Tr("relação com cada vereador: {0}"), GovRange(l, "0")),
        GovForecast.Fidelidade => string.Format(Tr("fidelidade ao partido: {0}"), GovRange(l, "0")),
        _ => l.Kind,
    };

    private string IndicatorName(string? id) => LowerFirst(_game.ExecutiveContent.Config.Indicators.FirstOrDefault(i => i.Id == id)?.Name ?? id ?? "");

    /// <summary>Linha que muda alguma coisa na precisão que a tela mostra (efeito que arredonda para zero não aparece).</summary>
    private static bool Matters(ForecastLine l) =>
        l.Kind is GovForecast.Turnos or GovForecast.Esqueleto or GovForecast.PromessaCumprida or GovForecast.Energia or GovForecast.VotosSim
        || Math.Max(Math.Abs(l.Min), Math.Abs(l.Max)) >= (l.Kind is GovForecast.Imagem or GovForecast.Folha or GovForecast.Apoio or GovForecast.Reserva || l.Kind == GovForecast.Imagem + "_later" ? 0.0005 : 0.05);

    private static bool IsCostLine(ForecastLine l) =>
        l.Kind is GovForecast.Turnos or GovForecast.Esqueleto or GovForecast.Energia
        || (l.Kind is GovForecast.Capital or GovForecast.Dinheiro or GovForecast.Investimento or GovForecast.RelacaoPrefeito or GovForecast.Imagem or GovForecast.Aprovacao
            or GovForecast.Indicador or GovForecast.IndicadorAlvo or GovForecast.Apoio or GovForecast.Reserva or GovForecast.RelacaoCamara or GovForecast.Reconhecimento
            or GovForecast.Fidelidade && l.Max < 0)
        || (l.Kind == GovForecast.Folha && l.Min > 0);

    /// <summary>"O que faz" do tooltip, tirado da previsão do Core (nunca texto fixo).</summary>
    private string GovDoes(GovForecast f)
    {
        if (f.Blocked is not null)
        {
            return Tr("nada enquanto estiver bloqueado.");
        }

        var parts = f.Lines.Where(l => !IsCostLine(l) && Matters(l)).Select(GovLine).ToList();
        parts.AddRange(f.Outcomes.Select(o => $"{o.Probability.ToString("P0", PtBr)}: {o.Text}"));
        if (parts.Count == 0)
        {
            parts.AddRange(f.Explanation.Take(2));
        }

        return parts.Count == 0 ? Tr("sem efeito nos números.") : OneDot(string.Join("; ", parts).TrimEnd('.') + ".");
    }

    private string GovCost(GovForecast f)
    {
        var parts = f.Lines.Where(l => IsCostLine(l) && Matters(l)).Select(GovLine).ToList();
        return parts.Count == 0 ? Tr("nada.") : OneDot(string.Join("; ", parts) + ".");
    }

    private string GovTip(string what, GovForecast f) => Palette.Tip(what, GovDoes(f), GovCost(f));

    /// <summary>Cartão da previsão antes do clique (princípio f): efeitos com faixa, chances, explicação, fatores e quem vai lembrar.</summary>
    private void GovForecastCard(Node parent, GovForecast f, string name = "Previsao", string? title = null)
    {
        PanelContainer card = Card(parent, Palette.Cartao, Palette.Linha);
        card.Name = name;
        var col = Ui.Column(card, 6);
        Heading(col, title ?? Tr("Previsão"), 20);
        if (f.Blocked is not null)
        {
            PanelContainer stop = Card(col, Palette.CartaoAlto, Palette.Linha, 10);
            stop.Name = "Bloqueio";
            Glossary.Rich(stop, Tr("Não dá para fazer agora:") + " " + GovReason(f), 18, Palette.Texto);
            return;
        }

        foreach (string line in f.Explanation)
        {
            Glossary.Rich(col, "· " + line.TrimEnd('.') + ".", 18, Palette.Texto);
        }

        foreach (ForecastLine l in f.Lines.Where(Matters))
        {
            string text = GovLine(l);
            Glossary.Rich(col, OneDot("· " + char.ToUpper(text[0], PtBr) + text[1..] + "."));
        }

        foreach (ForecastOutcome o in f.Outcomes)
        {
            Glossary.Rich(col, string.Format(Tr("· Chance de {0}: {1}"), o.Probability.ToString("P0", PtBr), o.Text.TrimEnd('.') + "."));
        }

        if (f.Factors.Count > 0)
        {
            Glossary.Rich(col, Tr("Fatores:") + " " + string.Join(", ", f.Factors.Select(x => $"{x.Text} ({Percent(x.Multiplier)})")) + ".");
        }

        if (f.Remembered.Count > 0)
        {
            Glossary.Rich(col, OneDot(string.Format(Tr("Quem vai lembrar: {0}."), string.Join(", ", f.Remembered))));
        }
    }

    private void GovShow(string title, GovResult result)
    {
        _govResultTitle = title;
        _govResult = result;
        if (result.Ok)
        {
            _log.Insert(0, result.Text.TrimEnd('.') + ".");
        }
    }

    /// <summary>"O que mudou" depois do comando (princípio f), no alto do painel, até o jogador fechar.</summary>
    private void GovResultCard()
    {
        if (_govResult is not { } result)
        {
            return;
        }

        PanelContainer card = Card(_left, Palette.CartaoAlto, result.Ok ? Palette.Acao : Palette.Linha);
        card.Name = "OQueMudou";
        var col = Ui.Column(card, 6);
        Heading(col, result.Ok ? Tr("O que mudou") : Tr("Não foi feito"), 20);
        Glossary.Rich(col, (result.Ok ? result.Text : result.Text.Length > 0 ? result.Text : result.Failure is { } failure ? GovFailureText(failure) : "").TrimEnd('.') + ".", 18, Palette.Texto);
        foreach (string change in result.Changes)
        {
            Glossary.Rich(col, "· " + change.TrimEnd('.') + ".");
        }

        if (result.Factors.Count > 0)
        {
            Glossary.Rich(col, Tr("Fatores:") + " " + string.Join(", ", result.Factors.Select(x => $"{x.Text} ({Percent(x.Multiplier)})")) + ".");
        }

        var close = Ui.Button(col, "Continuar", () => { _govResult = null; _govResultTitle = null; Refresh(); },
            Palette.Tip(Tr("Fecha este resumo."), Tr("o que mudou continua registrado nas notícias, à direita."), Tr("nada.")));
        close.Name = "FecharResultado";
        close.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
    }

    /// <summary>
    /// Botão de comando do mandato: o tooltip em três partes vem da previsão; bloqueado, diz o motivo; ao clicar,
    /// executa e mostra "O que mudou".
    /// </summary>
    private Button GovCommand(Node parent, string label, string what, GovForecast forecast, Func<GovResult> run, string? name = null, bool primary = false, bool danger = false, Action<GovResult>? after = null)
    {
        string tip = GovTip(what, forecast);
        Button? button = null;
        button = Ui.Button(parent, label, () =>
        {
            if (danger && _govConfirm != name)
            {
                _govConfirm = name;
                Refresh();
                return;
            }

            _govConfirm = null;
            GovResult result = run();
            GovShow(label, result);
            after?.Invoke(result);
            Refresh();
        }, tip);
        if (name is not null)
        {
            button.Name = name;
        }

        button.ClipText = true;
        button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (forecast.Blocked is not null)
        {
            button.Disabled = true;
            button.TooltipText = Palette.Blocked(GovReason(forecast), tip);
        }
        else if (danger)
        {
            Palette.Danger(button);
            if (_govConfirm == name && name is not null)
            {
                button.Text = Tr("Confirmar:") + " " + label;
            }
        }
        else if (primary)
        {
            Palette.Primary(button);
        }

        return button;
    }

    /// <summary>Linha de lista do mandato: texto em até duas linhas, abre uma ficha.</summary>
    private Button GovRow(Node parent, string text, Action open, string tip, string? name = null, bool selected = false)
    {
        var b = Ui.Button(parent, text, open, tip);
        if (name is not null)
        {
            b.Name = name;
        }

        b.Alignment = HorizontalAlignment.Left;
        b.ClipText = true;
        b.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        Palette.ListRow(b, selected);
        return b;
    }

    /// <summary>Linha "rótulo — zona escolhida" com a instrução de clicar no mapa (comandos que pedem zona).</summary>
    private void GovZonePicker(string purpose, Func<string, bool>? fits = null)
    {
        _govPick = purpose;
        PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
        card.Name = "EscolherZona";
        var col = Ui.Column(card, 4);
        Glossary.Rich(col, _govZone is null ? Tr("Zona: clique numa zona do mapa.") : string.Format(Tr("Zona: {0}. Para trocar, clique em outra zona do mapa."), GZone(_govZone)), 18, Palette.Texto);
        if (_govZone is not null && fits is not null && !fits(_govZone))
        {
            Glossary.Rich(col, Tr("Este tipo não cabe nesta zona (urbana ou rural); escolha outra zona ou outro tipo."));
        }
    }

    // ------------------------------------------------------------------ faixa de decisão

    private void BuildGovBanner()
    {
        if (_banner is not null && IsInstanceValid(_banner))
        {
            _banner.QueueFree();
        }

        _banner = null;
        string? text = null;
        var buttons = new List<(string Label, Action Action, string Tip)>();
        bool cardOpen = GovCardWanted && !_govCardHidden;
        string hideTip = Palette.Tip(Tr("Esconde o cartão para você consultar o mapa e os painéis."), Tr("a decisão continua esperando; Decidir traz o cartão de volta."), Tr("nada."));
        string showTip = Palette.Tip(Tr("Abre o cartão da decisão."), Tr("mostra as opções, cada uma com a previsão e quem vai lembrar."), Tr("nada."));
        if (State.Executive?.Crisis is { } crisis)
        {
            text = string.Format(Tr("Crise: {0}. Jogo pausado até você decidir."), _game.ExecutiveContent.Crises.Find(crisis.CrisisId)?.Name ?? crisis.CrisisId);
        }
        else if (Gov?.G.Decision is { } decision)
        {
            text = string.Format(Tr("{0}. Responda até {1}; sem resposta, vale a opção padrão."), decision.Title.TrimEnd('.'), decision.Deadline.ToString("d 'de' MMMM", PtBr));
        }
        else if (_govDemand is not null)
        {
            text = Tr("Cobrança do Livro de compromissos.");
        }
        else if (_cardResult is not null)
        {
            text = Tr("Decisão tomada: veja o que mudou.");
        }

        if (text is not null)
        {
            buttons.Add(_govCardHidden ? ("Decidir", () => { _govCardHidden = false; Refresh(); }, showTip) : ("Ver o mapa", () => { _govCardHidden = true; Refresh(); }, hideTip));
        }
        else if (_notice is not null && !_running)
        {
            text = _notice.Text + " " + Tr("Jogo pausado.");
            if (_notice.Button is not null && _notice.Action is { } action)
            {
                Notice notice = _notice;
                buttons.Add((notice.Button!, () => { _notice = null; action(); }, notice.Tip ?? ""));
            }

            buttons.Add(("Fechar aviso", () => { _notice = null; Refresh(); },
                Palette.Tip(Tr("Fecha este aviso."), Tr("o jogo continua pausado até você escolher uma velocidade na barra de cima."), Tr("nada."))));
        }

        if (text is null)
        {
            return;
        }

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
            b.Name = label switch { "Fechar aviso" => "FecharAviso", "Decidir" => "Decidir", "Ver o mapa" => "VerOMapa", _ => "AvisoAcao" };
        }
    }

    // ------------------------------------------------------------------ partido

    private string? _govSwitchTo;

    private void BuildGovParty()
    {
        GameParty? party = State.Party(State.Player.PartyId);
        if (party is null)
        {
            Glossary.Rich(_left, Tr("Você está sem partido. Só disputa eleição quem está filiado a um partido registrado até seis meses antes dela."), 18, Palette.Texto);
        }
        else
        {
            string family = _game.World.Parties.Families.Families.FirstOrDefault(f => f.Id == party.FamilyId)?.Name ?? party.FamilyId;
            NameTitle(_left, $"{party.Sigla} · {party.Number}", 22);
            Glossary.Rich(_left, string.Format(Tr("{0}. Família ideológica: {1}. Partido fictício."), party.Name, family));
        }

        if (State.Mandate is { } mandate)
        {
            ValueRow(_left, Tr("Fidelidade ao partido (0 a 100)"), mandate.Fidelity.ToString(PtBr), highlight: true);
            if (mandate.Expelled)
            {
                Glossary.Rich(_left, Tr("Você foi expulso do partido: o mandato continua, mas só disputa a reeleição quem se filiar até o prazo."), 18, Palette.Texto);
            }
            else if (mandate.Sanctioned)
            {
                Glossary.Rich(_left, Tr("Você está punido pelo partido: fora da comissão e da liderança da bancada."), 18, Palette.Texto);
            }
        }

        bool window = PartyAffiliation.InWindow(State);
        bool loses = PartyAffiliation.LosesMandate(State);
        Heading(_left, Tr("Trocar de partido"), 20);
        Glossary.Rich(_left, loses
            ? Tr("Fora da janela partidária, o vereador que troca de partido perde o mandato.")
            : window ? Tr("A janela partidária está aberta: dá para trocar de partido sem perder o mandato.") : Tr("Sem mandato de vereador em jogo, a troca de partido é livre."));
        var options = State.Parties.Where(p => p.Id != State.Player.PartyId).Select(p => (p.Id, $"{p.Number} · {p.Sigla} — {p.Name}")).ToList();
        _govSwitchTo ??= options.FirstOrDefault().Id;
        OptionButton choice = Ui.Options(_left, options, _govSwitchTo, id => { _govSwitchTo = id; _govConfirm = null; Refresh(); }, translate: false);
        choice.Name = "PartidoNovo";
        choice.ClipText = true;
        choice.TooltipText = Palette.Tip(Tr("Partido a que você quer se filiar."), Tr("escolhe o partido; a filiação só acontece no botão abaixo."), Tr("nada."));
        string joinTip = Palette.Tip(Tr("Filia você ao partido escolhido."), Tr(loses ? "você muda de partido e perde o mandato de vereador, que vai para o suplente." : "você muda de partido; o número de urna e a orientação de voto passam a ser os dele."), Tr(loses ? "o mandato." : "nada."));
        var join = Ui.Button(_left, loses && _govConfirm == "filiar" ? "Confirmar e perder o mandato" : "Filiar-se", () =>
        {
            if (_govSwitchTo is null)
            {
                return;
            }

            if (loses && _govConfirm != "filiar")
            {
                _govConfirm = "filiar";
                Refresh();
                return;
            }

            _govConfirm = null;
            string sigla = State.Party(_govSwitchTo)?.Sigla ?? "";
            _game.JoinParty(_govSwitchTo);
            SaveManager.Instance.Autosave();
            _log.Insert(0, string.Format(Tr("Você se filiou ao {0}."), sigla));
            _govSwitchTo = null;
            if (State.Mandate is null && State.Executive is null)
            {
                _game.GoToCareer();
            }

            Refresh();
        }, joinTip);
        join.Name = "Filiar";
        if (loses)
        {
            Palette.Danger(join);
        }

        if (_govSwitchTo is null)
        {
            join.Disabled = true;
            join.TooltipText = Palette.Blocked(Tr("não há outro partido registrado para escolher."), joinTip);
        }

        Heading(_left, Tr("Fundar um partido"), 20);
        Glossary.Rich(_left, Tr("Fundar um partido exige fundadores em vários estados, assinaturas de apoio e registro, e leva meses."));
        var found = Ui.Button(_left, "Fundar um partido", ShowGovFounding, Palette.Tip(Tr("Abre a fundação de partido."), Tr("mostra a etapa em que a fundação está e o que falta para o registro."), Tr("nada para abrir; cada etapa mostra o custo em capital político e dinheiro.")));
        found.Name = "Fundar";
    }

    private void ShowGovFounding()
    {
        _running = false;
        var shade = this.Add(new ColorRect { Color = new Color(Palette.Fundo, 0.6f), MouseFilter = MouseFilterEnum.Stop });
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = shade.Add(new CenterContainer());
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.Add(new FoundingDialog(() =>
        {
            shade.QueueFree();
            Refresh();
        }));
    }
}
