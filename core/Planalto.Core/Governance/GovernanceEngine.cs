using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Executive;
using Planalto.Core.Legislature;
using Planalto.Core.Municipios;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Governance;

/// <summary>Tudo o que o governo da cidade precisa, derivado do estado e do conteúdo.</summary>
public sealed class GovernanceContext
{
    public required GameState State { get; init; }

    public required Municipio Municipio { get; init; }

    public required GovernanceContent Content { get; init; }

    public required LegislatureContent Legislature { get; init; }

    public required ExecutiveContent Executive { get; init; }

    public required PeopleContent People { get; init; }

    /// <summary>Conteúdo da campanha: promessas da plataforma, tipos de liderança e regras da memória das pessoas.</summary>
    public required CampaignContent Campaign { get; init; }

    /// <summary>Regras da memória das pessoas (data/people/config.json).</summary>
    public PeopleConfig Memory => Campaign.People;

    /// <summary>Promessas da plataforma: a chave de obra de cada uma decide o que a cumpre.</summary>
    public PromiseCatalog Promises => Campaign.Promises;

    public required ZonasMunicipio Zonas { get; init; }

    /// <summary>Ponto de partida oficial do município (receita, RGF), quando importado.</summary>
    public Indicators.IndicadoresMunicipio? Indicadores { get; init; }

    public FiscalLawConfig FiscalLaw { get; init; } = FiscalLawConfig.Default;

    public ProcurementLawConfig ProcurementLaw { get; init; } = ProcurementLawConfig.Default;

    public InaugurationLawConfig InaugurationLaw { get; init; } = InaugurationLawConfig.Default;

    public ChamberRulesConfig ChamberRules { get; init; } = ChamberRulesConfig.Default;

    /// <summary>Condutas vedadas em ano de eleição (Lei 9.504/1997, art. 73) e a regra da reeleição.</summary>
    public ElectionConductLawConfig ElectionConduct { get; init; } = ElectionConductLawConfig.Default;

    /// <summary>
    /// Evolução dos indicadores reais (GDD 17.17). Com ela e com <see cref="GameState.Indicators"/> no estado, obra
    /// entregue, serviço e crise empurram o indicador real, e as crises com causa e a receita leem dele também quando o
    /// prefeito é NPC. Sem ela, vale o comportamento sem indicadores reais.
    /// </summary>
    public Indicators.IndicatorEvolution? IndicatorEvolution { get; init; }

    /// <summary>
    /// Renovação do mundo político (GDD 17.16). Com ela e com <see cref="GameState.Renewal"/> no estado, a cassação
    /// segue a regra de sucessão (vice, suplente, nova eleição) e a morte ou a saída de vereador, secretário, assessor
    /// ou liderança chega ao governo (<see cref="GovernanceEngine.SyncWithWorld"/>). Sem ela, vale o vice provisório.
    /// </summary>
    public Renewal.RenewalEngine? Renewal { get; init; }

    /// <summary>
    /// Gancho da vacância: chamado quando a Câmara cassa o prefeito (o NPC ou o jogador). Tem precedência sobre
    /// <see cref="Renewal"/>. Quem cuida de sucessão e vacância liga aqui e devolve true; sem ninguém ligado, ou
    /// devolvendo false, o vice assume, como padrão provisório. O registro fica em <see cref="GovernanceState.Vacancies"/>
    /// de todo jeito.
    /// </summary>
    public Func<OfficeVacancy, bool>? OnVacancy { get; init; }

    /// <summary>
    /// Ponto de extensão: crescimento nominal da receita no ano; tem precedência sobre <see cref="IndicatorEvolution"/>.
    /// Sem um nem outro, o crescimento é sorteado na faixa de data/executive/personnel.json.
    /// </summary>
    public Func<int, double>? RevenueGrowth { get; init; }

    /// <summary>
    /// Ponto de extensão: indicador do município na escala de jogo (0 a 100) quando o jogador não é o prefeito; tem
    /// precedência sobre <see cref="IndicatorEvolution"/>. Sem um nem outro, as crises que dependem de indicador só
    /// acontecem com o jogador na prefeitura.
    /// </summary>
    public Func<string, double?>? Indicator { get; init; }
}

/// <summary>Por que um comando do mandato não pode ser dado agora.</summary>
public enum GovFailure
{
    NaoEncontrado,
    PapelErrado,
    SemTurno,
    SemCapital,
    SemVaga,
    SemEmenda,
    SemInvestimento,
    AlvoInvalido,
    LimiteLegal,
    PeriodoVedado,
    SemObraPronta,
    VedadoArt22,
    Bloqueado,
    JaFeito,
}

/// <summary>
/// Previsão de um comando do mandato (GDD 17.1, princípio f): linhas com faixa, desfechos com a chance, fatores que
/// multiplicam o efeito e a explicação por extenso. Bloqueada, diz por quê.
/// </summary>
public sealed record GovForecast(
    GovFailure? Blocked,
    string? BlockedReason,
    IReadOnlyList<ForecastLine> Lines,
    IReadOnlyList<ForecastOutcome> Outcomes,
    IReadOnlyList<string> Explanation)
{
    public const string Presenca = "presence";
    public const string PresencaDepois = "presence_later";
    public const string Imagem = "image";
    public const string Capital = "capital";
    public const string RelacaoPrefeito = "mayor_relation";
    public const string Relacao = "relation";
    public const string Aprovacao = "approval";
    public const string AprovacaoZona = "zone_approval";
    public const string Folha = "payroll";
    public const string Dinheiro = "money";
    public const string PromessaCumprida = "promise";
    public const string Turnos = "turns";
    public const string Investimento = "investment";

    /// <summary>Esqueleto que a escolha põe no Livro (o valor é a gravidade).</summary>
    public const string Esqueleto = "skeleton";

    public IReadOnlyList<YieldFactor> Factors { get; init; } = [];

    /// <summary>Quem vai lembrar da decisão (nomes), para a tela mostrar antes do clique.</summary>
    public IReadOnlyList<string> Remembered { get; init; } = [];

    public ForecastLine? Line(string kind, string? target = null) => Lines.FirstOrDefault(l => l.Kind == kind && l.Target == target);

    /// <summary>O valor cabe na faixa da linha (com folga de arredondamento); sem a linha, só o zero cabe.</summary>
    public bool Contains(string kind, string? target, double value)
    {
        ForecastLine? line = Line(kind, target);
        return line is null ? Math.Abs(value) < 1e-6 : value >= line.Min - 1e-6 && value <= line.Max + 1e-6;
    }

    public static GovForecast Block(GovFailure failure, string reason) => new(failure, reason, [], [], [reason]);
}

/// <summary>Resultado explicado de um comando: o que mudou, onde e para quem, e os fatores que pesaram.</summary>
public sealed record GovResult(GovFailure? Failure, string Text, IReadOnlyList<string> Changes, IReadOnlyList<YieldFactor> Factors)
{
    public bool Ok => Failure is null;

    /// <summary>Id do que o comando criou (obra, requerimento, acordo), quando há.</summary>
    public string? CreatedId { get; init; }

    public static GovResult Fail(GovFailure failure, string reason) => new(failure, reason, [], []);
}

/// <summary>
/// Motor do governo da cidade num mandato do jogador (GDD 17.14 e 17.15): obras com licitação e prazo, secretários,
/// folha com os limites fiscais, convênios, gabinete e instrumentos do vereador, acordos de voto, Mesa, CPI e
/// cassação, crises com causa, eventos de mandato e a cobrança do Livro de compromissos. Semanal, como os motores de
/// mandato; o que não é do jogador anda sozinho. Toda aleatoriedade vem do Rng do estado.
/// </summary>
public sealed partial class GovernanceEngine(GovernanceContext ctx)
{
    public GovernanceContext Context { get; } = ctx;

    public GovernanceState G => State.Governance ?? throw new InvalidOperationException("Sem governo em curso.");

    private GameState State => Context.State;

    private GovernanceContent Content => Context.Content;

    private Rng Rng => State.Rng;

    private DateOnly Week => State.Calendar.WeekStart;

    public GovRole Role => G.Role;

    private string Porte => Context.Municipio.Porte;

    private MandateState? Mandate => State.Mandate;

    private ExecutiveState? Exec => State.Executive;

    /// <summary>O mandato do jogador ainda está de pé (não terminou nem foi cassado).</summary>
    public bool Active => State.Governance is { PlayerRemoved: false } && (State.Mandate is not null || State.Executive is not null);

    /// <summary>Vereadores da Câmara, com o jogador (se for um deles).</summary>
    public IReadOnlyList<ChamberMember> Members => Role == GovRole.Vereador ? Mandate?.Members ?? [] : Exec?.Chamber ?? [];

    public ChamberMember? Member(string id) => Members.FirstOrDefault(m => m.Id == id);

    /// <summary>Partidos da base do governo.</summary>
    public bool InBase(string? partyId) => Role == GovRole.Vereador ? Mandate?.InBase(partyId) == true : Exec?.InBase(partyId) == true;

    public string MayorId => Role == GovRole.Prefeito ? RaceSetup.PlayerId : Mandate?.MayorId ?? Work.Prefeito;

    public string MayorName => Role == GovRole.Prefeito ? State.Player.BallotName : Mandate?.MayorName ?? "";

    /// <summary>Aprovação do governo na cidade (0 a 100).</summary>
    public double Approval => Role == GovRole.Prefeito ? Exec?.Approval ?? 50 : G.MayorApproval;

    // ================================================================ posse

    /// <summary>
    /// Monta o governo na posse: zonas com a presença herdada da campanha, secretários, empresas, folha e receita de
    /// partida, redutos dos vereadores e, para o vereador, os candidatos ao gabinete. As obras da cidade passam do
    /// mandato anterior para o novo.
    /// </summary>
    public static GovernanceState Start(GovernanceContext ctx, TerritoryState? territory = null, GovernanceState? previous = null)
    {
        GameState state = ctx.State;
        Rng rng = state.Rng;
        bool mayor = state.Executive is not null;
        if (!mayor && state.Mandate is null)
        {
            throw new InvalidOperationException("Sem mandato para governar.");
        }

        if (previous?.Municipio != ctx.Municipio.CodigoIbge)
        {
            previous = null;
        }

        DateOnly start = mayor ? state.Executive!.Start : state.Mandate!.Start;
        DateOnly end = mayor ? state.Executive!.End : state.Mandate!.End;
        List<Zona> zonas = ctx.Zonas.Zonas;
        double recognition = mayor ? state.Executive!.Recognition : state.Mandate!.Recognition;
        List<double> presence = territory is not null && territory.Presence.TryGetValue(RaceSetup.PlayerId, out double[]? p) && p.Length == zonas.Count
            ? [.. p]
            : previous is not null && previous.Presence.Count == zonas.Count ? [.. previous.Presence] : zonas.Select(_ => recognition).ToList();

        InstrumentRules rules = ctx.Content.Instruments.Regras;
        PersonnelRules personnel = ctx.Content.Personnel.Regras;
        SecretaryRules secretaryRules = ctx.Content.Secretaries.Regras;
        Indicators.FinancasEnte? financas = ctx.Indicadores?.Financas;
        decimal rcl = financas?.ReceitaCorrenteLiquida ?? financas?.ReceitaCorrente
            ?? ctx.Municipio.Populacao * ctx.Executive.Config.RevenuePerCapita.GetValueOrDefault(ctx.Municipio.Porte, 4000m);
        Range payrollRange = personnel.FolhaInicialPorPorte[ctx.Municipio.Porte];
        bool fromRgf = financas?.PessoalPctRcl is { } pct && pct is > 20 and < 80;
        double share = fromRgf ? (double)financas!.PessoalPctRcl!.Value / 100 : rng.NextDouble(payrollRange.Min, payrollRange.Max);
        double taxBase = ctx.Executive.Config.TaxBaseShare.GetValueOrDefault(ctx.Municipio.Porte, 1);

        // Com a renovação do mundo ligada, o vice é o eleito de lá (o que toma posse, se a posse ainda não aconteceu);
        // sem ela, o governo gera um nome provisório.
        Renewal.RenewalState? world = ctx.Renewal is not null && state.Renewal is { } renewal && renewal.Municipio == ctx.Municipio.CodigoIbge ? renewal : null;
        string viceId = world is not null
            ? (world.Eleitos is { } slate ? slate.ViceId : world.ViceId) ?? ""
            : NewCharacter(ctx, rng, 35, 70, mayor ? state.Player.PartyId : state.Mandate!.MayorPartyId).Id;
        var g = new GovernanceState
        {
            Municipio = ctx.Municipio.CodigoIbge,
            Role = mayor ? GovRole.Prefeito : GovRole.Vereador,
            Start = start,
            End = end,
            Zones = zonas.Select(z => z.Id).ToList(),
            ZonePopulation = zonas.Select(z => z.Populacao).ToList(),
            Presence = presence,
            PresenceAtStart = [.. presence],
            BaseZones = presence.Select((v, i) => (v, i)).OrderByDescending(x => x.v).ThenBy(x => x.i).Take(Math.Min(rules.ZonasDaBase, zonas.Count)).Select(x => x.i).ToList(),
            ZoneApproval = previous is not null && previous.ZoneApproval.Count == zonas.Count ? [.. previous.ZoneApproval] : zonas.Select(_ => 0.0).ToList(),
            MayorApproval = rng.NextDouble(secretaryRules.AprovacaoInicialNpc.Min, secretaryRules.AprovacaoInicialNpc.Max),
            ViceId = viceId,
            RenewalSeen = world is null ? 0 : world.NextEventId - 1,
            MayorSeenId = mayor ? null : state.Mandate!.MayorId,
            GovernorRelation = rng.NextDouble(ctx.Content.Agreements.Regras.RelacaoGovernadorInicial.Min, ctx.Content.Agreements.Regras.RelacaoGovernadorInicial.Max),
            Mesa = new MesaState { NextElection = start },
            Payroll = new PayrollState { Share = share, FromRgf = fromRgf },
            Fiscal = new FiscalState
            {
                Rcl = rcl,
                Declared = financas is not null && financas.Fonte != Indicators.FinancasEnte.Estimativa,
                TaxBaseShare = taxBase,
                Year = start.Year,
                ChamberShare = ctx.FiscalLaw.TetoCamara(ctx.Municipio.Populacao) * personnel.RepasseCamaraInicial,
                GeneralRevision = rng.NextDouble(personnel.RevisaoGeral.Min, personnel.RevisaoGeral.Max),
            },
            TurnsLeft = rules.TurnosPorSemana,
        };
        g.Payroll.Band = ctx.FiscalLaw.Faixa(share);
        g.PromisesAtStart.AddRange(state.Ledger.Open.Select(x => x.Id));
        if (previous is not null)
        {
            g.Works.AddRange(previous.Works);
            g.Companies.AddRange(previous.Companies);
            g.DonorCompanies.AddRange(previous.DonorCompanies);
            g.NextId = previous.NextId;
        }

        state.Governance = g;
        var engine = new GovernanceEngine(ctx);
        engine.EnsureCompanies();
        engine.AssignRedutos();
        engine.SeatSecretaries();
        if (mayor)
        {
            engine.EnsureOffers();
        }
        else
        {
            engine.BuildAidePool();
            engine.SeedNpcWorks();
        }

        engine.RefreshDerived();
        return g;
    }

    /// <summary>
    /// Leva a presença por zona do mandato para a campanha que começa (ponto de extensão: a campanha semeia o
    /// território pelo reconhecimento da cidade; aqui a repartição por zona do mandato é recuperada).
    /// </summary>
    public static void CarryToCampaign(GameState state)
    {
        if (state.Governance is not { Role: GovRole.Vereador } g || state.Campaign is not { } campaign
            || !campaign.Territory.Presence.TryGetValue(RaceSetup.PlayerId, out double[]? presence) || presence.Length != g.Presence.Count)
        {
            return;
        }

        for (int z = 0; z < presence.Length; z++)
        {
            presence[z] = Math.Clamp(g.Presence[z], 0, 100);
        }

        campaign.Player.Recognition = campaign.Territory.Recognition(RaceSetup.PlayerId);
    }

    // ================================================================ semana

    /// <summary>
    /// Simulação semanal do governo. Roda depois da semana do mandato (as votações já aconteceram) e antes de o
    /// calendário andar. Com <paramref name="automatic"/> (ano de campanha), o que espera o jogador segue pelo padrão.
    /// </summary>
    public void ResolveWeek(bool automatic = false)
    {
        GovernanceState g = G;
        if (!Active)
        {
            return;
        }

        int feedBefore = g.Feed.Count;
        SyncWithWorld();
        FiscalYear();
        MesaWeek();
        WorksWeek(automatic);
        CouncilWeek(automatic);
        SecretariesWeek();
        PayrollWeek(automatic);
        AgreementsWeek(automatic);
        CrisesWeek(automatic);
        CpiWeek();
        CollectionWeek();
        EventsWeek(automatic);

        foreach (string key in g.Fatigue.Keys.ToList())
        {
            double left = g.Fatigue[key] - Content.Instruments.Regras.CansacoRecuperacaoSemanal;
            if (left <= 1e-9)
            {
                g.Fatigue.Remove(key);
            }
            else
            {
                g.Fatigue[key] = left;
            }
        }

        g.TurnsLeft = Content.Instruments.Regras.TurnosPorSemana;
        RefreshDerived();
        if (g.Feed.Count == feedBefore)
        {
            WeeklyDigest();
        }
    }

    /// <summary>Valores derivados que os outros motores leem do estado: bônus de comissão, velocidade dos indicadores, reconhecimento.</summary>
    private void RefreshDerived()
    {
        GovernanceState g = G;
        g.CommitteeBonus = 0;
        if (Role == GovRole.Vereador && Mandate is { } mandate)
        {
            if (g.AideIn(AideRole.Legislativo) is { } aide)
            {
                g.CommitteeBonus += Content.Instruments.Role(AideRole.Legislativo).At(aide.Quality);
            }

            if (g.Mesa.PresidentId == RaceSetup.PlayerId)
            {
                g.CommitteeBonus += Content.Chamber.Regras.MesaBonusComissao;
            }

            mandate.Recognition = Math.Clamp(g.CityPresence(), 0, 100);
        }

        g.AreaSpeed.Clear();
        SecretaryRules rules = Content.Secretaries.Regras;
        foreach (SecretariaDefinition pasta in Content.Secretaries.Secretarias.Where(s => s.Area is not null))
        {
            BudgetArea? area = Context.Executive.Config.Areas.FirstOrDefault(a => a.Id == pasta.Area);
            if (area is null)
            {
                continue;
            }

            double factor = rules.VelocidadePorCompetencia[(g.SecretaryOf(pasta.Id)?.Competence ?? 1) - 1];
            foreach (string indicator in area.Effects.Keys)
            {
                g.AreaSpeed[indicator] = g.AreaSpeed.TryGetValue(indicator, out double current) ? (current + factor) / 2 : factor;
            }
        }

        PushSpeedFactors();
    }

    /// <summary>Virada do ano fiscal: a receita cresce, a folha tem o crescimento vegetativo e o investimento do ano zera.</summary>
    private void FiscalYear()
    {
        GovernanceState g = G;
        if (Week.Year <= g.Fiscal.Year)
        {
            return;
        }

        PersonnelRules rules = Content.Personnel.Regras;
        double growth = RevenueGrowthOf(Week.Year);
        g.Fiscal.Year = Week.Year;
        g.Fiscal.Growth = growth;
        g.Fiscal.Rcl *= (decimal)(1 + growth);
        g.Fiscal.InvestmentUsed = 0;
        g.Fiscal.GeneralRevision = Rng.NextDouble(rules.RevisaoGeral.Min, rules.RevisaoGeral.Max);
        g.Payroll.Share = g.Payroll.Share * (1 + rules.CrescimentoVegetativoAnual) / (1 + growth);
        News("folha", Fill(Content.Personnel.Texto("revisao_anual"), ("receita", Texts.Percent(growth)), ("folha", Pct(g.Payroll.Share))));
    }

    /// <summary>
    /// Semana sem fato novo: a notícia fala do que está em andamento (o mundo anda sem comando). O assunto roda entre
    /// as obras abertas, a folha, a aprovação do governo, a Câmara e a presença do mandato, para não repetir.
    /// </summary>
    private void WeeklyDigest()
    {
        GovernanceState g = G;
        int turn = Week.DayNumber / 7;
        List<Work> works = g.Works.Where(w => w.Open && w.StageEnd is not null).OrderBy(w => w.Id, StringComparer.Ordinal).ToList();
        int topics = 4 + (Role == GovRole.Vereador ? 1 : 0);
        int topic = turn % (topics + works.Count);
        if (topic < works.Count)
        {
            Work work = works[(turn / (topics + works.Count)) % works.Count];
            work = works[(works.IndexOf(work) + topic) % works.Count];
            News("semana", T("semana_obra", ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("etapa", StageName(work.Stage)), ("data", Texts.Date(work.StageEnd!.Value))), work.ZoneId);
            return;
        }

        switch (topic - works.Count)
        {
            case 0:
                News("semana", T("semana_folha", ("folha", Pct(g.Payroll.Share)), ("faixa", BandName(g.Payroll.Band))));
                break;
            case 1:
                News("semana", T("semana_aprovacao", ("prefeito", MayorName), ("aprovacao", Texts.Number(Approval, "0"))));
                break;
            case 2:
            case 3:
                int bills = Mandate?.Bills.Count(b => b.Stage is BillStage.Comissao or BillStage.Plenario) ?? 0;
                News("semana", T("semana_camara", ("projetos", bills.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("presidente", NameOf(g.Mesa.PresidentId))), null, g.Mesa.PresidentId);
                break;
            default:
                int zone = turn % g.Zones.Count;
                News("semana", T("semana_presenca", ("zona", ZoneName(g.Zones[zone])), ("presenca", Texts.Number(g.Presence[zone], "0"))), g.Zones[zone]);
                break;
        }
    }

    // ================================================================ apoio

    internal static string Fill(string template, params (string Key, string Value)[] values) => Texts.Fill(template, values);

    internal static string Pct(double fraction) => (fraction * 100).ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")) + "%";

    internal static string Num(double value) => Texts.Number(value, "0.0");

    /// <summary>Texto curto do governo (data/mandate/texts.json), com as chaves preenchidas.</summary>
    internal string T(string key, params (string Key, string Value)[] values) => Fill(Content.Texts.Texto(key), values);

    /// <summary>Número com sinal, como aparece nas linhas de "o que mudou".</summary>
    internal static string Signed(double value, string format = "0.0") => (value < 0 ? "−" : "+") + Texts.Number(Math.Abs(value), format);

    private string ChPresence(string? zoneId, double value) => T("mud_presenca", ("valor", Signed(value)), ("zona", ZoneName(zoneId)));

    /// <summary>Imagem vai de −1 a 1; a linha mostra em pontos (×100).</summary>
    private string ChImage(double value) => T("mud_imagem", ("valor", Signed(value * 100)));

    private string ChRelation(string name, double value) => T("mud_relacao", ("valor", Signed(value, "0")), ("nome", name));

    private string ChCapital(double value) => T("mud_capital", ("valor", Signed(value, "0")));

    private string ChApproval(double value) => T("mud_aprovacao", ("valor", Signed(value)));

    private string ChZoneApproval(string? zoneId, double value) => T("mud_aprovacao_zona", ("valor", Signed(value)), ("zona", ZoneName(zoneId)));

    private string ChInvestment(decimal value) => T("mud_investimento", ("valor", (value < 0 ? "−" : "+") + Texts.Money(Math.Abs(value))));

    private string ChPayroll(double before, double after) => T("mud_folha", ("antes", Pct(before)), ("depois", Pct(after)));

    private string Vote(int side) => T(side > 0 ? "voto_sim" : "voto_nao");

    private string Join(IEnumerable<string> names) => string.Join(T("e"), names.Where(n => n.Length > 0));

    private void News(string kind, string text, string? zoneId = null, string? personId = null)
    {
        GovernanceState g = G;
        g.Feed.Add(new FeedItem(Week, kind, text, zoneId, personId));
        if (g.Feed.Count > GovernanceState.MaxFeed)
        {
            g.Feed.RemoveRange(0, g.Feed.Count - GovernanceState.MaxFeed);
        }
    }

    public string ZoneName(string? zoneId) => Context.Zonas.Zonas.FirstOrDefault(z => z.Id == zoneId)?.Nome ?? "";

    private Zona? Zone(string zoneId) => Context.Zonas.Zonas.FirstOrDefault(z => z.Id == zoneId);

    public string NameOf(string? personId) =>
        personId is null ? "" : personId == RaceSetup.PlayerId ? State.Player.BallotName
        : State.Npcs.FirstOrDefault(n => n.Id == personId)?.BallotName ?? (personId == Mandate?.MayorId ? Mandate.MayorName : "");

    public string WorkName(Work work) => Content.Works.Find(work.TypeId)?.Nome ?? work.TypeId;

    public string CompanyName(Work work) => G.Company(work.CompanyId)?.Name ?? "";

    public string StageName(WorkStage stage) => T(stage switch
    {
        WorkStage.Liberacao => "etapa_liberacao",
        WorkStage.Projeto => "etapa_projeto",
        WorkStage.Modalidade => "etapa_modalidade",
        WorkStage.Licitacao => "etapa_licitacao",
        WorkStage.Execucao => "etapa_execucao",
        WorkStage.Pronta => "etapa_pronta",
        WorkStage.Entregue => "etapa_entregue",
        _ => "etapa_cancelada",
    });

    public string BandName(PayrollBand band) => T(band switch
    {
        PayrollBand.Alerta => "faixa_alerta",
        PayrollBand.Prudencial => "faixa_prudencial",
        PayrollBand.Excedido => "faixa_excedido",
        _ => "faixa_normal",
    });

    private CityState City => State.City ??= new CityState { Municipio = Context.Municipio.CodigoIbge };

    /// <summary>A pessoa com memória de um id; cria a ficha se ainda não existe.</summary>
    private Person PersonOf(string id, PersonRole role)
    {
        Person? person = City.Person(id);
        if (person is null)
        {
            person = new Person { Id = id, Roles = [role], Interests = [], Temperament = (Temperament)(StableHash(id) % 5) };
            City.People.Add(person);
        }
        else if (!person.Has(role))
        {
            person.Roles.Add(role);
        }

        return person;
    }

    /// <summary>
    /// Grava a lembrança em quem estava envolvido. A relação de vereador com o jogador também anda (é ela que entra
    /// no voto).
    /// </summary>
    private void Remember(string? personId, PersonRole role, string fact, MemoryDefinition memory, params (string Key, string Value)[] fill)
    {
        if (personId is null || personId == RaceSetup.PlayerId || personId == Work.Prefeito)
        {
            return;
        }

        PersonOf(personId, role).Remember(Week, fact, memory.Peso, memory.Pontos, Fill(memory.Texto, fill), Context.Memory);
        if (Member(personId) is { IsPlayer: false } member)
        {
            member.Relation = Math.Clamp(member.Relation + memory.Pontos, -100, 100);
        }
    }

    /// <summary>Relação de uma pessoa com o jogador hoje.</summary>
    public double RelationOf(string personId) => Member(personId) is { } m ? m.Relation : City.Person(personId)?.Relation(Week, Context.Memory) ?? 0;

    private static Character NewCharacter(GovernanceContext ctx, Rng rng, int minAge, int maxAge, string? partyId)
    {
        GameState state = ctx.State;
        Municipio m = ctx.Municipio;
        int serial = state.Npcs.Count;
        string id;
        do
        {
            id = $"npc_{m.CodigoIbge}_{serial++}";
        }
        while (state.Npcs.Any(n => n.Id == id));

        Character npc = PersonGenerator.Generate(rng, ctx.People, new PersonRequest(id, m.Uf, m.CodigoIbge, state.Calendar.WeekStart, Age: rng.NextInt(minAge, maxAge + 1), PartyId: partyId));
        state.Npcs.Add(npc);
        return npc;
    }

    private Person NewPerson(PersonRole role, int minAge, int maxAge, string? partyId, string? zoneId = null)
    {
        Character npc = NewCharacter(Context, Rng, minAge, maxAge, partyId);
        var person = new Person
        {
            Id = npc.Id,
            Roles = [role],
            Interests = zoneId is null ? [] : [new Interest { Kind = Interest.ZonaKind, Target = zoneId }],
            Temperament = (Temperament)Rng.NextInt(5),
            Affinity = Rng.NextInt(-5, 6),
        };
        City.People.Add(person);
        return person;
    }

    /// <summary>Hash estável de um texto (FNV-1a): o mesmo em qualquer execução, para escolhas que não gastam o Rng.</summary>
    internal static ulong StableHash(string text)
    {
        ulong hash = 14695981039346656037UL;
        foreach (char c in text)
        {
            hash ^= c;
            hash *= 1099511628211UL;
        }

        return hash;
    }

    /// <summary>Cada vereador tem um reduto: a zona em que pede obra e emenda.</summary>
    private void AssignRedutos()
    {
        GovernanceState g = G;
        foreach (ChamberMember m in Members.Where(m => !m.IsPlayer && !g.MemberZones.ContainsKey(m.Id)))
        {
            g.MemberZones[m.Id] = g.Zones[Rng.PickWeighted(Enumerable.Range(0, g.Zones.Count).ToList(), i => Math.Max(1.0, g.ZonePopulation[i]))];
            PersonOf(m.Id, PersonRole.Vereador);
        }
    }

    // ---------------------------------------------------------------- presença e aprovação

    /// <summary>Ganho de presença do vereador numa zona, com retorno decrescente; devolve o que de fato mudou.</summary>
    private double GainPresence(int zone, double amount, bool spill = false)
    {
        GovernanceState g = G;
        if (zone < 0 || zone >= g.Presence.Count)
        {
            return 0;
        }

        double before = g.Presence[zone];
        g.Presence[zone] = EffectApplier.Gain(before, amount);
        if (spill && amount > 0 && Zone(g.Zones[zone]) is { } z)
        {
            foreach (int n in z.Vizinhas.Select(g.IndexOf).Where(i => i >= 0))
            {
                g.Presence[n] = EffectApplier.Gain(g.Presence[n], amount * Content.Works.Regras.Transbordo / Math.Max(1, z.Vizinhas.Count));
            }
        }

        return g.Presence[zone] - before;
    }

    /// <summary>O que uma sequência de ganhos de presença renderia numa zona, um depois do outro, sem aplicar.</summary>
    private double ChainPresence(int zone, IEnumerable<double> amounts)
    {
        if (zone < 0 || zone >= G.Presence.Count)
        {
            return 0;
        }

        double value = G.Presence[zone];
        foreach (double amount in amounts)
        {
            value = EffectApplier.Gain(value, amount);
        }

        return value - G.Presence[zone];
    }

    /// <summary>O que um ganho de presença renderia numa zona, sem aplicar.</summary>
    private double PresenceGain(int zone, double amount) => zone < 0 || zone >= G.Presence.Count ? 0 : EffectApplier.Gain(G.Presence[zone], amount) - G.Presence[zone];

    private void ShiftImage(double delta)
    {
        if (Role == GovRole.Vereador && Mandate is { } mandate)
        {
            mandate.Image = Math.Clamp(mandate.Image + delta, -1, 1);
        }
        else if (Exec is { } e)
        {
            e.Image = Math.Clamp(e.Image + delta, -1, 1);
        }
    }

    /// <summary>
    /// Muda a aprovação do governo na cidade, em pontos. Com o jogador prefeito, a aprovação é recalculada toda
    /// semana pelos indicadores; a mudança entra pela imagem, no peso que ela tem na aprovação.
    /// </summary>
    private void ShiftApproval(double points)
    {
        if (Role == GovRole.Prefeito && Exec is { } e)
        {
            e.Image = Math.Clamp(e.Image + (points / Math.Max(1e-9, Context.Executive.Config.Approval.ImageWeight)), -1, 1);
            e.Approval = Math.Clamp(e.Approval + points, 0, 100);
        }
        else
        {
            G.MayorApproval = Math.Clamp(G.MayorApproval + points, 0, 100);
        }
    }

    private void ShiftZoneApproval(int zone, double points)
    {
        if (zone >= 0 && zone < G.ZoneApproval.Count)
        {
            G.ZoneApproval[zone] = Math.Clamp(G.ZoneApproval[zone] + points, -40, 40);
        }
    }

    private void ShiftMayorRelation(double delta)
    {
        if (Mandate is { } mandate)
        {
            mandate.MayorRelation = Math.Clamp(mandate.MayorRelation + delta, -100, 100);
        }
    }

    private int Capital
    {
        get => State.Player.Resources.PoliticalCapital;
        set => State.Player.Resources.PoliticalCapital = value;
    }

    /// <summary>Probabilidade de uma normal padrão ficar abaixo de x.</summary>
    internal static double Phi(double x)
    {
        // Abramowitz e Stegun, 7.1.26.
        double t = 1 / (1 + (0.3275911 * Math.Abs(x) / Math.Sqrt(2)));
        double erf = 1 - ((((((1.061405429 * t) - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x / 2);
        return x >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
    }
}
