using System.Collections.Concurrent;
using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Elections;
using Planalto.Core.Executive;
using Planalto.Core.Governance;
using Planalto.Core.Indicators;
using Planalto.Core.Legislature;
using Planalto.Core.Municipios;
using Planalto.Core.People;
using Planalto.Core.Renewal;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Indicators;
using Planalto.Core.Tests.Renewal;
using Planalto.Core.World;
using Xunit;

namespace Planalto.Core.Tests.Governance;

/// <summary>
/// Um estado com tudo ligado, como o jogo monta: o mundo que anda sozinho (indicadores reais e renovação) e os motores
/// de mandato com os contextos apontando para ele.
/// </summary>
internal sealed class Linked
{
    private static readonly ConcurrentDictionary<(ulong, bool), Lazy<string>> Careers = new();

    private Linked(GameState state, WorldEngine world)
    {
        State = state;
        World = world;
        Gov = state.Governance is not null && (state.Mandate is not null || state.Executive is not null) ? new GovernanceEngine(GovernanceContext(state, world)) : null;
        Legislature = state.Mandate is null ? null : new LegislatureEngine(CampaignKit.LegislatureContext(state));
        Executive = state.Executive is null ? null : new ExecutiveEngine(ExecutiveContext(state, world));
    }

    public GameState State { get; }

    public WorldEngine World { get; }

    public GovernanceEngine? Gov { get; }

    public LegislatureEngine? Legislature { get; }

    public ExecutiveEngine? Executive { get; }

    public RenewalEngine Renewal => World.Renewal!;

    public IndicatorEvolution Evolution => World.Indicators!;

    public RenewalState R => State.Renewal!;

    public GovernanceState G => State.Governance!;

    public MunicipalIndicatorsState Real => State.Indicators!;

    /// <summary>O mundo do estado, com os indicadores e a renovação garantidos no save (mundo novo ou save antigo).</summary>
    public static WorldEngine NewWorld(GameState state)
    {
        Municipio m = CampaignKit.Municipio(state.FocusMunicipio);
        var world = new WorldEngine(state, new IndicatorEvolution(IndicatorKit.Content, m, CampaignKit.C.Projection), new RenewalEngine(RenewalKit.Context(state)));
        world.EnsureIndicators(IndicatorKit.Uf(m.Uf), CampaignKit.C.Municipios);
        world.Renewal!.Ensure();
        return world;
    }

    public static Linked Of(GameState state) => new(state, NewWorld(state));

    public static Linked Load(string json) => Of(GameStateSerializer.Deserialize(json));

    private static IndicadoresMunicipio? Official(GameState state) =>
        IndicatorKit.Uf(CampaignKit.Municipio(state.FocusMunicipio).Uf).Municipios.FirstOrDefault(i => i.CodigoIbge == state.FocusMunicipio);

    public static GovernanceContext GovernanceContext(GameState state, WorldEngine world)
    {
        GovernanceContext plain = CampaignKit.GovernanceContext(state, Official(state));
        return new GovernanceContext
        {
            State = plain.State, Municipio = plain.Municipio, Content = plain.Content, Legislature = plain.Legislature, Executive = plain.Executive, People = plain.People, Campaign = plain.Campaign,
            Zonas = plain.Zonas, Indicadores = plain.Indicadores, IndicatorEvolution = world.Indicators, Renewal = world.Renewal,
        };
    }

    public static ExecutiveContext ExecutiveContext(GameState state, WorldEngine world)
    {
        ExecutiveContext plain = CampaignKit.ExecutiveContext(state, Official(state));
        return new ExecutiveContext
        {
            State = plain.State, Municipio = plain.Municipio, Content = plain.Content, Legislature = plain.Legislature, Electorate = plain.Electorate, RecognitionScale = plain.RecognitionScale,
            Indicadores = plain.Indicadores, IndicatorEvolution = world.Indicators,
        };
    }

    /// <summary>Uma semana da carreira com tudo ligado; a crise pendente do prefeito é respondida pela primeira opção.</summary>
    public void Week()
    {
        if (State.Executive is { Crisis: not null } && Executive is not null)
        {
            Executive.ResolveCrisis(0);
        }

        CareerFlow.AdvanceWeek(State, null, State.Mandate is null ? null : Legislature, executive: State.Executive is null ? null : Executive, governance: Gov, world: World);
    }

    public void Weeks(int n)
    {
        for (int i = 0; i < n; i++)
        {
            Week();
        }
    }

    public bool InOffice => State.Mandate is not null || State.Executive is not null;

    /// <summary>O gabinete do vereador visto pelos testes do mandato (para reusar o mandatário automático).</summary>
    public GovKit.Council Council => new(State, Gov!, Legislature!);

    /// <summary>
    /// Carreira jogada com tudo ligado desde o primeiro dia: o mundo anda toda semana da campanha, a apuração entrega os
    /// eleitos à renovação e a posse monta o governo ligado. Guardada como save; cada teste parte de uma cópia.
    /// </summary>
    public static Linked Career(ulong seed, bool mayor) => Load(Careers.GetOrAdd((seed, mayor), key => new Lazy<string>(() => PlayCareer(key.Item1, key.Item2))).Value);

    private static string PlayCareer(ulong firstSeed, bool mayor)
    {
        for (ulong seed = firstSeed; seed < firstSeed + 40; seed++)
        {
            CampaignEngine campaign = CampaignKit.NewCareer(GovKit.SmallTown(), seed, strongestParty: true);
            GameState state = campaign.Context.State;
            if (mayor)
            {
                state.Player.Attributes.Set(Planalto.Core.Characters.AttributeId.Carisma, 70);
                if (CareerFlow.SwitchCandidacy(campaign.Context, Office.Prefeito).Issue is not null)
                {
                    continue;
                }
            }

            WorldEngine world = NewWorld(state);
            var robot = new CampaignRobot(campaign, seed, new RobotOptions { R2b = !mayor, Answer = LeaderAnswer.Aceitar });
            robot.PlayToElection(advanceWeek: () => CareerFlow.AdvanceWeek(state, campaign, null, world: world));
            robot.Vote();
            if (!state.Campaign!.Outcome!.PlayerElected)
            {
                continue;
            }

            CareerFlow.ConcludeElection(campaign.Context, CampaignKit.LegislatureContext(state), mayor ? ExecutiveContext(state, world) : null, GovernanceContext(state, world), world.Renewal);
            return GameStateSerializer.Serialize(state);
        }

        throw new InvalidOperationException("Nenhuma semente elegeu o jogador.");
    }
}

/// <summary>
/// Integração das duas metades da R4: o governo da cidade (GDD 17.14 e 17.15) ligado aos indicadores em unidade real
/// (17.17) e à renovação do mundo político (17.16).
/// </summary>
public class WorldLinkTests
{
    private static readonly SuccessionRulesConfig Rules = SuccessionRulesConfig.Default;

    private static GovernanceContent Content => CampaignKit.C.Governance;

    // ---------------------------------------------------------------- invariantes

    /// <summary>O que tem de valer toda semana com tudo ligado.</summary>
    private static void AssertLinked(Linked l)
    {
        GameState state = l.State;
        RenewalState r = l.R;
        // O mundo anda no começo da semana, antes de o calendário virar: fica uma semana atrás dele.
        Assert.Equal(state.Calendar.WeekStart.AddDays(-7), l.Real.UltimoPasso);
        Assert.Equal(state.Calendar.WeekStart.AddDays(-7), r.UltimoPasso);
        foreach (IndicatorValue v in l.Real.Indicadores)
        {
            RealIndicatorDefinition d = IndicatorKit.Content.Definitions.Get(v.Id);
            var (min, max) = IndicatorScale.Bounds(d, v.Partida);
            Assert.InRange(v.Valor, min, max);
        }

        if (state.Governance is not { PlayerRemoved: false } g || l.Gov is null || !l.Gov.Active)
        {
            return;
        }

        // A Câmara do mandato é a da renovação (fora do intervalo entre a eleição e a posse).
        if (r.Eleitos is null)
        {
            Assert.Equal(r.Titulares.Order(StringComparer.Ordinal), l.Gov.Members.Select(m => m.Id).Order(StringComparer.Ordinal));
        }

        if (state.Mandate is { } mandate && r.Eleitos is null && r.PrefeitoId is not null)
        {
            Assert.Equal(r.PrefeitoId, mandate.MayorId);
        }

        // Nada no governo aponta para quem saiu da Câmara, morreu ou foi embora.
        Assert.All(g.MemberZones.Keys, id => Assert.NotNull(l.Gov.Member(id)));
        Assert.All(l.Gov.Members.Where(m => !m.IsPlayer), m => Assert.True(g.MemberZones.ContainsKey(m.Id), m.Id));
        Assert.All(g.Deals.Where(d => !d.Extinct && !d.Voted && state.Ledger.Promises.Any(p => p.Id == d.PromiseId && p.State == PromiseState.Aberta)), d => Assert.NotNull(l.Gov.Member(d.MemberId)));
        Assert.All(g.Secretaries.Where(s => s.SecretariaId is not null && !s.Left), s => Assert.False(Gone(r, s.PersonId), s.PersonId));
        Assert.All(g.Cabinet, a => Assert.False(Gone(r, a.PersonId), a.PersonId));
        Assert.All(state.Ledger.Open.Where(p => p.ToKind == Promise.ParaPessoa), p => Assert.False(Gone(r, p.ToId), p.Text));
        Assert.All(state.Ledger.Debts.Where(d => !d.Settled), d => Assert.False(Gone(r, d.ToPersonId), d.Text));
        if (g.Mesa.PresidentId is { } president)
        {
            Assert.NotNull(l.Gov.Member(president));
        }

        if (state.Executive is { } e)
        {
            Assert.All(e.Indicators.Values, v => Assert.InRange(v, 0, 100));
            Assert.InRange(e.Approval, 0, 100);
        }

        Assert.InRange(g.MayorApproval, 0, 100);
        Assert.InRange(g.Payroll.Share, 0.15, 0.95);
        Assert.Equal(g.RenewalSeen, r.NextEventId - 1);
    }

    private static bool Gone(RenewalState r, string id) => r.Status(id)?.Saida is ExitKind.Morte or ExitKind.Mudanca;

    /// <summary>Prefeito automático: responde ao que aparece, inaugura, abre obra e assina convênio de vez em quando.</summary>
    private static void PlayMayorWeek(Linked l, Rng policy)
    {
        GovernanceEngine gov = l.Gov!;
        GovernanceState g = l.G;
        if (g.Decision is not null)
        {
            TermEventDefinition def = gov.DecisionDefinition!;
            if (!gov.Decide(policy.Pick(def.Opcoes).Id).Ok)
            {
                Assert.True(gov.Decide(def.Opcoes.First(o => o.Padrao).Id).Ok);
            }
        }

        foreach (WorkView ready in GovernanceQueries.Works(gov).Where(w => w.CanInaugurate).ToList())
        {
            Assert.True(gov.Inaugurate(ready.Work.Id).Ok);
        }

        if (policy.Chance(0.12))
        {
            WorkType type = policy.Pick(Content.Works.Tipos);
            string zone = policy.Pick(g.Zones);
            if (gov.ForecastStartWork(type.Id, zone).Blocked is null)
            {
                Assert.True(gov.StartWork(type.Id, zone).Ok);
            }
        }

        foreach (Agreement offer in g.Agreements.Where(a => a.State == AgreementState.Oferta).ToList())
        {
            string zone = policy.Pick(g.Zones);
            if (policy.Chance(0.5) && gov.ForecastSignAgreement(offer.Id, zone).Blocked is null)
            {
                Assert.True(gov.SignAgreement(offer.Id, zone).Ok);
            }
        }
    }

    private static void Play(Linked l, Rng policy)
    {
        if (l.Gov is null || !l.Gov.Active)
        {
            return;
        }

        if (l.State.Mandate is not null)
        {
            MandateCareerTests.PlayWeek(l.Council, policy);
        }
        else if (l.State.Executive is not null)
        {
            PlayMayorWeek(l, policy);
        }
    }

    /// <summary>Abre uma CPI do jogador contra o prefeito, com a Câmara a favor, e leva a aprovação ao chão até a votação.</summary>
    private static Cpi CassateMayor(Linked l)
    {
        GovernanceState g = l.G;
        Work work = g.Works.First(w => w.Open);
        work.CompanyId = g.Companies[0].Id;
        work.Stage = WorkStage.Execucao;
        work.StageEnd = l.State.Calendar.WeekStart.AddDays(7 * 60);
        work.Irregular = true;
        work.Revealed = true;
        g.Requests.Add(new InfoRequest { Id = "req_teste", TargetKind = InfoRequest.SobreObra, TargetId = work.Id, Filed = g.Start, Due = g.Start, Answered = true });
        foreach (ChamberMember m in l.State.Mandate!.Members.Where(m => !m.IsPlayer))
        {
            m.Relation = 100;
        }

        l.State.Mandate.BaseParties.Clear();
        Assert.True(l.Gov!.RequestCpi(work.Id).Ok);
        Cpi cpi = g.Cpi!;
        for (int week = 0; week < 20 && g.Cpi is not null; week++)
        {
            g.MayorApproval = 10;
            l.Week();
        }

        Assert.Null(g.Cpi);
        Assert.True(g.Cassations.Single().Passed);
        return cpi;
    }

    // ---------------------------------------------------------------- save antigo

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Save_antigo_ganha_indicadores_e_renovacao_ao_ser_ligado_e_segue_carregando(bool mayor)
    {
        GameState old = mayor ? GovKit.Mayor().State : GovKit.Councillor().State;
        Assert.Null(old.Indicators);
        Assert.Null(old.Renewal);
        string before = GameStateSerializer.Serialize(old);
        Assert.DoesNotContain("\"renewal\":{", before);

        Linked l = Linked.Of(old);
        Assert.Equal(old.FocusMunicipio, l.Real.Municipio);
        Assert.Equal(old.FocusMunicipio, l.R.Municipio);
        Assert.Equal(old.Governance!.Start, l.R.MandatoInicio);

        // A Câmara e a Prefeitura da renovação são as do mandato salvo.
        Assert.Equal(l.Gov!.Members.Select(m => m.Id).Order(), l.R.Titulares.Order());
        Assert.Equal(mayor ? RaceSetup.PlayerId : old.Mandate!.MayorId, l.R.PrefeitoId);
        Assert.NotNull(l.R.ViceId);
        Assert.True(l.Gov.UsesRealIndicators);
        Assert.True(l.Gov.UsesRenewal);

        // Garantir de novo não troca nada; e o estado ligado salva e carrega igual.
        string linked = GameStateSerializer.Serialize(old);
        Linked again = Linked.Of(old);
        Assert.Equal(linked, GameStateSerializer.Serialize(again.State));
        Assert.Equal(linked, GameStateSerializer.Serialize(GameStateSerializer.Deserialize(linked)));
        l.Weeks(30);
        AssertLinked(l);
        if (mayor)
        {
            Assert.True(l.Executive!.UsesRealIndicators);
        }
    }

    // ---------------------------------------------------------------- cassação e sucessão

    [Fact]
    public void Cassacao_do_prefeito_pela_Camara_segue_a_regra_e_o_vice_eleito_assume()
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        GovernanceState g = l.G;
        RenewalState r = l.R;
        MandateState mandate = l.State.Mandate!;
        string mayor = r.PrefeitoId!;
        string vice = r.ViceId!;
        string provisional = g.ViceId;
        Assert.Equal(mayor, mandate.MayorId);
        Assert.NotEqual(provisional, vice);

        Cpi cpi = CassateMayor(l);

        // O vice eleito assumiu pela regra; o vice provisório do governo não foi usado.
        OfficeVacancy vacancy = Assert.Single(g.Vacancies);
        Assert.True(vacancy.Handled);
        Assert.Equal(mayor, vacancy.HolderId);
        Assert.Equal(cpi.Id, vacancy.CpiId);
        Assert.Equal(SuccessionOutcome.ViceAssumiu, vacancy.Outcome);
        Assert.Equal(vice, vacancy.SuccessorId);
        Assert.Equal(Rules.BaseDuplaVacancia.Artigo, vacancy.Rule);
        Assert.Null(vacancy.ElectionOn);
        Assert.Equal(vice, r.PrefeitoId);
        Assert.Null(r.ViceId);
        Assert.Empty(r.EleicoesPendentes);
        Assert.Equal(ExitKind.Cassacao, r.Status(mayor)!.Saida);
        Assert.Equal(vice, mandate.MayorId);
        Assert.NotEqual(provisional, mandate.MayorId);
        Assert.Equal(RenewalKit.Npc(l.State, vice).BallotName, mandate.MayorName);
        Assert.True(g.MayorRemoved);

        // O fato da renovação é o registro e a notícia: cassação com o desfecho, posse do vice.
        RenewalEvent fact = r.Historico.Single(e => e.Tipo == RenewalEventKind.Cassacao && e.PessoaId == mayor);
        Assert.Equal(SuccessionOutcome.ViceAssumiu, fact.Desfecho);
        RenewalEvent posse = r.Historico.Single(e => e.Tipo == RenewalEventKind.PosseVice);
        Assert.Contains(g.Feed, f => f.Kind == "mundo" && f.Text == posse.Texto);
        Assert.DoesNotContain(g.Feed, f => f.Text.Contains("assume a prefeitura"));

        // O vice deve o cargo à CPI do jogador e lembra disso; a relação recomeça dali.
        Assert.Equal(Content.Chamber.Lembrancas["vice_grato"].Pontos, mandate.MayorRelation);
        Assert.Contains(l.State.City!.Person(vice)!.Memories, m => m.Fact == "vice_grato");
        Assert.Equal(vice, g.MayorSeenId);
        l.Weeks(10);
        AssertLinked(l);
    }

    /// <summary>
    /// Dupla vacância por cassação: sem vice, a regra do art. 81 (padrão do jogo): eleição direta na primeira metade do
    /// mandato e indireta, pela Câmara, na segunda. Quem preside a Câmara responde pela Prefeitura até lá.
    /// </summary>
    [Theory]
    [InlineData(20, false)]
    [InlineData(112, true)]
    public void Dupla_vacancia_leva_a_eleicao_direta_ou_indireta_conforme_a_metade_do_mandato(int weeksIn, bool indirect)
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        GovernanceState g = l.G;
        RenewalState r = l.R;
        MandateState mandate = l.State.Mandate!;
        l.Weeks(weeksIn);
        DateOnly week = l.State.Calendar.WeekStart;
        Assert.Equal(indirect, week >= Rules.EndOfDirectPeriod(r.MandatoInicio, r.MandatoFim));

        // O vice sai antes (renúncia): o cargo fica vago e não tem substituto.
        if (r.ViceId is not null)
        {
            Assert.Equal(SuccessionOutcome.ViceFicaVago, l.Renewal.VacateVice(VacancyCause.Renuncia, week)!.Desfecho);
        }

        Assert.NotNull(r.PrefeitoId);
        string mayor = mandate.MayorId;
        Assert.Equal(r.PrefeitoId, mayor);
        mandate.MayorRelation = -40;
        var vacancy = new OfficeVacancy { Id = g.NewId("vaga"), Date = week, Office = Office.Prefeito, HolderId = mayor, Cause = OfficeVacancy.Cassacao };
        g.Vacancies.Add(vacancy);
        vacancy.Handled = l.Gov!.ResolveVacancy(vacancy);

        Assert.True(vacancy.Handled);
        Assert.Equal(indirect ? SuccessionOutcome.EleicaoIndireta : SuccessionOutcome.EleicaoDireta, vacancy.Outcome);
        Assert.Null(vacancy.SuccessorId);
        Assert.Equal(week.AddDays(indirect ? Rules.PrazoEleicaoIndiretaDias : Rules.PrazoEleicaoDiretaDias), vacancy.ElectionOn);
        Assert.Equal(Rules.BaseDuplaVacancia.Artigo, vacancy.Rule);
        PendingElection election = Assert.Single(r.EleicoesPendentes, e => e.Cargo == CivicRole.Prefeito);
        Assert.Equal(indirect, election.Indireta);
        Assert.Null(r.PrefeitoId);
        Assert.Equal(week, r.ExecutivoVagoDesde);

        // Até a eleição, quem preside a Câmara responde pela Prefeitura; o governo nota a troca e a relação recomeça.
        Assert.NotNull(r.PresidenteCamaraId);
        Assert.Equal(r.PresidenteCamaraId, mandate.MayorId);
        l.Week();
        Assert.Equal(mandate.MayorId, g.MayorSeenId);
        Assert.Equal(0, mandate.MayorRelation);
        Assert.Contains(g.Feed, f => f.Kind == "mundo" && f.Text.Contains("responde por ela até a nova eleição"));
        Assert.Contains(g.Feed, f => f.Kind == "prefeito");

        // Feita a eleição, há prefeito e vice de novo; se o eleito era vereador, o suplente dele assumiu a cadeira.
        while (l.State.Calendar.WeekStart <= vacancy.ElectionOn!.Value.AddDays(7))
        {
            l.Week();
            AssertLinked(l);
        }

        Assert.DoesNotContain(r.EleicoesPendentes, e => e.Cargo == CivicRole.Prefeito);
        Assert.NotNull(r.PrefeitoId);
        Assert.NotNull(r.ViceId);
        Assert.Null(r.ExecutivoVagoDesde);
        Assert.Equal(r.PrefeitoId, mandate.MayorId);
        RenewalEvent result = r.Historico.Single(e => e.Tipo == RenewalEventKind.EleicaoResolvida && e.Papel == CivicRole.Prefeito);
        Assert.Equal(indirect ? SuccessionOutcome.EleicaoIndireta : SuccessionOutcome.EleicaoDireta, result.Desfecho);
        Assert.Contains(g.Feed, f => f.Kind == "mundo" && f.Text == result.Texto);
        Assert.Null(mandate.Members.FirstOrDefault(m => m.Id == r.PrefeitoId));
    }

    [Fact]
    public void Cassacao_de_vereador_pelo_gancho_da_a_cadeira_ao_suplente_do_partido()
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        GovernanceState g = l.G;
        RenewalState r = l.R;
        ChamberMember member = l.State.Mandate!.Members.First(m => !m.IsPlayer && r.Suplentes.GetValueOrDefault(m.PartyId ?? "")?.Count > 0);
        string substitute = r.Suplentes[member.PartyId!][0];
        string zone = g.MemberZones[member.Id];
        var vacancy = new OfficeVacancy { Id = g.NewId("vaga"), Date = l.State.Calendar.WeekStart, Office = Office.Vereador, HolderId = member.Id, Cause = OfficeVacancy.Cassacao };
        Assert.True(l.Gov!.ResolveVacancy(vacancy));
        Assert.Equal(SuccessionOutcome.SuplenteAssumiu, vacancy.Outcome);
        Assert.Equal(substitute, vacancy.SuccessorId);
        Assert.Equal(Rules.BaseVereador.Artigo, vacancy.Rule);
        l.Week();
        Assert.Null(l.Gov.Member(member.Id));
        Assert.NotNull(l.Gov.Member(substitute));
        Assert.Equal(zone, g.MemberZones[substitute]);
        AssertLinked(l);
    }

    [Fact]
    public void Prefeito_jogador_cassado_da_lugar_ao_vice_pela_regra()
    {
        Linked l = Linked.Of(GovKit.Mayor().State);
        RenewalState r = l.R;
        Assert.Equal(RaceSetup.PlayerId, r.PrefeitoId);
        string vice = r.ViceId!;
        var vacancy = new OfficeVacancy { Id = "vaga_teste", Date = l.State.Calendar.WeekStart, Office = Office.Prefeito, HolderId = RaceSetup.PlayerId, Cause = OfficeVacancy.Cassacao };
        Assert.True(l.Gov!.ResolveVacancy(vacancy));
        Assert.Null(l.State.Executive);
        Assert.Equal(SuccessionOutcome.ViceAssumiu, vacancy.Outcome);
        Assert.Equal(vice, r.PrefeitoId);
        Assert.Equal(vice, l.State.Local!.MayorId);

        // Sem cargo, o mundo segue andando.
        for (int i = 0; i < 12; i++)
        {
            CareerFlow.AdvanceWeek(l.State, null, null, world: l.World);
        }

        Assert.Equal(l.State.Calendar.WeekStart.AddDays(-7), r.UltimoPasso);
        Assert.Equal(l.State.Calendar.WeekStart.AddDays(-7), l.Real.UltimoPasso);
    }

    // ---------------------------------------------------------------- obras e indicadores

    [Fact]
    public void Obra_de_saude_entregue_melhora_o_indicador_real_e_a_variacao_diz_por_que()
    {
        Linked with = Linked.Of(GovKit.Councillor().State);
        Linked without = Linked.Load(GameStateSerializer.Serialize(with.State));
        GovernanceState g = with.G;
        WorkType type = Content.Works.Type("posto_saude");
        Assert.Equal("saude", type.Indicador);
        string zone = g.Zones.First(z => with.Gov!.Fits(type, z));
        with.State.Mandate!.EmendasLeft = 1;
        Work work = g.Work(with.Gov!.Emenda(type.Id, zone).CreatedId!)!;
        work.Stage = WorkStage.Pronta;
        DateOnly delivered = with.State.Calendar.WeekStart;
        Assert.True(with.Gov.Inaugurate(work.Id).Ok);

        // A entrega vira pressão nos indicadores reais da saúde, com o prazo declarado em data/works/types.json.
        string origin = GovernanceEngine.WorkOrigin + work.Id;
        List<IndicatorPressure> pressures = with.Real.Pressoes.Where(p => p.Origem == origin).ToList();
        Assert.Equal(new[] { "leitos", "mortalidade_infantil" }, pressures.Select(p => p.IndicatorId).Order());
        Assert.All(pressures, p =>
        {
            Assert.Equal(type.IndicadorPontos, p.Pontos);
            Assert.Equal(delivered, p.Desde);
            Assert.Equal(delivered.AddDays((7 * Content.Works.Regras.IndicadorSemanasObra) - 1), p.Ate);
            Assert.Contains(type.Nome, p.Texto);
            Assert.Contains(with.Gov.ZoneName(zone), p.Texto);
        });
        Assert.DoesNotContain(without.Real.Pressoes, p => p.Origem.StartsWith(GovernanceEngine.WorkOrigin, StringComparison.Ordinal));

        // Um ano depois, com o mesmo mundo e os mesmos sorteios, a cidade que recebeu o posto está melhor.
        DateOnly later = delivered.AddDays(7 * 52);
        with.Evolution.AdvanceTo(with.Real, later);
        without.Evolution.AdvanceTo(without.Real, later);
        Assert.True(with.Real.Get("mortalidade_infantil").Valor < without.Real.Get("mortalidade_infantil").Valor);
        Assert.True(with.Real.Get("leitos").Valor > without.Real.Get("leitos").Valor);
        Assert.Equal(with.Real.Get("ideb").Valor, without.Real.Get("ideb").Valor);

        // A explicação da variação traz a obra, com o nome e a zona, empurrando para o lado bom.
        foreach (string id in new[] { "mortalidade_infantil", "leitos" })
        {
            IndicatorFactor factor = IndicatorQueries.LastChange(with.Real, id)!.Fatores.Single(f => f.Tipo == IndicatorFactor.Pressao && f.Origem == origin);
            Assert.True(factor.Pontos > 0);
            Assert.Contains(type.Nome, factor.Texto);
            bool better = IndicatorKit.Content.Definitions.Get(id).MaiorMelhor ? factor.Delta > 0 : factor.Delta < 0;
            Assert.True(better, $"{id}: {factor.Delta}");
            Assert.Contains(IndicatorQueries.YearChange(with.Real, id)!.Fatores, f => f.Origem == origin);
        }

        // E a nota da saúde que o governo lê (a mesma régua com prefeito NPC) é maior onde a obra foi entregue.
        Assert.True(with.Gov.IndicatorOf("saude") > without.Gov!.IndicatorOf("saude"));
    }

    [Fact]
    public void Servico_em_vigor_e_obra_do_prefeito_jogador_tambem_empurram_o_indicador_real()
    {
        Linked l = Linked.Of(GovKit.Mayor().State);
        GovernanceState g = l.G;
        l.Week();
        Assert.True(l.Executive!.UsesRealIndicators);
        WorkType service = Content.Works.Tipos.First(t => t.Servico && t.Indicador == "saude");
        string zone = g.Zones.First(z => l.Gov!.Fits(service, z));
        GovResult started = l.Gov!.StartWork(service.Id, zone);
        Assert.True(started.Ok, started.Text);
        string origin = GovernanceEngine.WorkOrigin + started.CreatedId;
        for (int week = 0; week < 60 && g.Work(started.CreatedId!)!.Stage != WorkStage.Entregue; week++)
        {
            l.Week();
        }

        Work work = g.Work(started.CreatedId!)!;
        Assert.Equal(WorkStage.Entregue, work.Stage);
        List<IndicatorPressure> pressures = l.Real.Pressoes.Where(p => p.Origem == origin).ToList();
        Assert.Equal(2, pressures.Count);
        Assert.All(pressures, p => Assert.Equal(work.Delivered!.Value.AddDays((7 * Content.Works.Regras.IndicadorSemanasServico) - 1), p.Ate));
        l.Weeks(4);
        Assert.Contains(IndicatorQueries.LastChange(l.Real, "leitos")!.Fatores, f => f.Tipo == IndicatorFactor.Pressao && f.Origem == origin);
        AssertLinked(l);
    }

    [Fact]
    public void Crise_de_indicador_baixo_tem_causa_no_indicador_real_tambem_com_prefeito_NPC()
    {
        // Sem a ligação, o governo de um prefeito NPC não tem indicador para ler.
        Assert.Null(GovKit.Councillor().Gov.IndicatorOf("saude"));

        Linked l = Linked.Of(GovKit.Councillor().State);
        GovernanceState g = l.G;
        CrisisDefinition crisis = CampaignKit.C.Executive.Crises.Crises.First(c => c.Cause is { Kind: CrisisRecord.CausaIndicador, Indicator: "saude" });
        double healthy = l.Gov!.IndicatorOf("saude")!.Value;
        Assert.Equal(
            Math.Clamp(IndicatorQueries.GameScore(l.Real, IndicatorKit.Content.Definitions, IndicatorKit.Content.Model, "saude")!.Value, 0, 100),
            healthy, 9);

        // A saúde real desaba (um choque de fora): a nota cai abaixo do limite da causa e o surto vem dela.
        CrisisRecord? record = null;
        for (int week = 0; week < 200 && record is null && l.InOffice; week++)
        {
            if (l.Gov.IndicatorOf("saude") >= crisis.Cause!.Below - 5)
            {
                l.Evolution.ApplyGameShock(l.Real, "saude", -30, l.State.Calendar.WeekStart, "teste", "Choque de teste");
            }

            l.Week();
            record = g.Crises.FirstOrDefault(c => c.CrisisId == crisis.Id);
        }

        Assert.NotNull(record);
        Assert.Equal(CrisisRecord.CausaIndicador, record!.CauseKind);
        Assert.Equal("saude", record.CauseRef);
        Assert.True(record.Presented);

        // A crise também pesa no indicador real: o choque fica registrado com a origem dela.
        string origin = GovernanceEngine.CrisisOrigin + record.Id;
        foreach (string indicator in crisis.Shock.Keys.Where(k => l.Evolution.Components(k).Any()))
        {
            Assert.All(l.Evolution.Components(indicator), id =>
                Assert.Contains(l.Real.Get(id).VariacaoNoAno?.Fatores ?? l.Real.Get(id).VariacaoAnoAnterior!.Fatores, f => f.Tipo == IndicatorFactor.Choque && f.Origem == origin));
        }
    }

    [Fact]
    public void Receita_do_ano_vem_da_receita_real_simulada_dentro_da_faixa_dos_dados()
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        GovernanceState g = l.G;
        Planalto.Core.Governance.Range range = Content.Personnel.Regras.RclCrescimentoAnual;
        int year = l.State.Calendar.Year;
        while (g.Fiscal.Year == year)
        {
            l.Week();
        }

        IndicatorVariation last = l.Real.Get(IndicatorDefinitions.ReceitaPerCapita).VariacaoAnoAnterior!;
        Municipio m = CampaignKit.Municipio(l.State.FocusMunicipio);
        double population = CampaignKit.C.Projection.Population(m, year + 1) / (double)CampaignKit.C.Projection.Population(m, year);
        double expected = Math.Clamp(((range.Min + range.Max) / 2) + ((last.Depois / last.Antes * population) - 1), range.Min, range.Max);
        Assert.Equal(expected, g.Fiscal.Growth, 12);
        Assert.InRange(g.Fiscal.Growth, range.Min, range.Max);
    }

    [Fact]
    public void Competencia_do_secretario_muda_a_velocidade_do_indicador_real_da_area()
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        l.Week();
        GovernanceState g = l.G;
        Assert.NotEmpty(g.AreaSpeed);
        foreach (var (indicator, factor) in g.AreaSpeed)
        {
            foreach (string id in l.Evolution.Components(indicator))
            {
                Assert.Equal(factor, l.Real.VelocidadeFator.GetValueOrDefault(id, 1), 12);
            }
        }

        Assert.Contains(l.Real.VelocidadeFator.Values, v => Math.Abs(v - 1) > 1e-9);

        // Fora do governo acompanhado de perto, a velocidade volta à do modelo.
        l.State.Governance = null;
        l.State.Mandate = null;
        CareerFlow.AdvanceWeek(l.State, null, null, world: l.World);
        Assert.Empty(l.Real.VelocidadeFator);
    }

    // ---------------------------------------------------------------- morte e saída

    /// <summary>Joga até uma votação ter um indeciso com pedido e fecha o acordo de voto com ele.</summary>
    private static (Bill Bill, ChamberMember Member, VoteDeal Deal, Promise Promise) CloseDeal(Linked l)
    {
        l.State.Player.Resources.PoliticalCapital = 50;
        for (int week = 0; week < 150; week++)
        {
            l.Week();
            foreach (Bill bill in l.Legislature!.Pauta)
            {
                CountLine? undecided = l.Gov!.CountBill(bill.Id)!.Lines
                    .FirstOrDefault(x => x.Lean == NominalCount.Indeciso && x.RequestText is not null && l.R.Suplentes.GetValueOrDefault(l.Gov.Member(x.MemberId)!.PartyId ?? "")?.Count > 0);
                if (undecided is not null && l.Gov.ForecastNegociarVoto(bill.Id, undecided.MemberId, 1).Blocked is null)
                {
                    GovResult result = l.Gov.NegociarVoto(bill.Id, undecided.MemberId, 1);
                    Assert.True(result.Ok);
                    VoteDeal deal = l.G.Deals.Single(d => d.Id == result.CreatedId);
                    return (bill, l.Gov.Member(undecided.MemberId)!, deal, l.State.Ledger.Promises.Single(p => p.Id == deal.PromiseId));
                }
            }
        }

        throw new InvalidOperationException("Em 150 semanas nenhuma votação teve indeciso com pedido e suplente.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Morte_de_vereador_com_acordo_de_voto_pendente_nao_quebra_a_contagem(bool comSuplente)
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        GovernanceState g = l.G;
        RenewalState r = l.R;
        var (bill, member, deal, promise) = CloseDeal(l);
        Assert.Equal(NominalCount.Sim, l.Gov!.CountBill(bill.Id)!.Lines.Single(x => x.MemberId == member.Id).Lean);
        string zone = g.MemberZones[member.Id];
        string party = member.PartyId!;
        string? substitute = comSuplente ? r.Suplentes[party][0] : null;
        if (!comSuplente)
        {
            r.Suplentes[party].Clear();
        }

        int seats = l.State.Mandate!.Members.Count;
        DateOnly week = l.State.Calendar.WeekStart;
        l.Renewal.Die(member.Id, week);
        l.Gov.SyncWithWorld();

        // A contagem segue de pé, sem quem morreu.
        NominalCount count = l.Gov.CountBill(bill.Id)!;
        Assert.DoesNotContain(count.Lines, x => x.MemberId == member.Id);
        Assert.Equal(l.State.Mandate.Members.Count - 1, count.Lines.Count);
        Assert.Equal(count.Lines.Count, count.Yes + count.No + count.Undecided);
        Assert.False(string.IsNullOrWhiteSpace(GovernanceQueries.CountText(l.Gov, count)));
        Assert.False(g.MemberZones.ContainsKey(member.Id));
        Assert.Contains(g.Feed, f => f.Kind == "mundo" && f.Text.Contains(member.Name) && f.Text.Contains("morreu"));
        if (comSuplente)
        {
            // O suplente herda a cadeira, o reduto e o acordo: vota como combinado e é ele quem cobra a promessa.
            Assert.Equal(seats, l.State.Mandate.Members.Count);
            Assert.Equal(substitute, deal.MemberId);
            Assert.Equal(member.Id, deal.OriginalMemberId);
            Assert.False(deal.Extinct);
            Assert.Equal(PromiseState.Aberta, promise.State);
            Assert.Equal(substitute, promise.ToId);
            Assert.Equal(member.Id, promise.OriginalToId);
            Assert.Equal(zone, g.MemberZones[substitute!]);
            Assert.Equal(NominalCount.Sim, count.Lines.Single(x => x.MemberId == substitute).Lean);
            Assert.True(g.DealLogit(bill.Id, substitute!) > 0);
            Assert.Contains(g.Feed, f => f.Kind == "cobranca" && f.Text.Contains("herda o acordo de voto") && f.PersonId == substitute);
        }
        else
        {
            // Sem suplente, a cadeira fica vaga e o acordo se extingue com registro: nem cumprido nem quebrado.
            Assert.Equal(seats - 1, l.State.Mandate.Members.Count);
            Assert.True(deal.Extinct);
            Assert.Equal(member.Id, deal.MemberId);
            Assert.Equal(PromiseState.Extinta, promise.State);
            Assert.Null(l.Gov.Member(member.Id));
            Assert.Contains(g.Feed, f => f.Kind == "cobranca" && f.Text.Contains("se extingue") && f.Text.Contains(promise.Text));
        }

        // Sincronizar de novo não muda nada, e o mandato segue: a votação acontece e o acordo se acerta com quem ficou.
        string synced = GameStateSerializer.Serialize(l.State);
        l.Gov.SyncWithWorld();
        Assert.Equal(synced, GameStateSerializer.Serialize(l.State));
        int memories = l.State.City!.Person(member.Id)?.Memories.Count ?? 0;
        for (int i = 0; i < 40 && l.InOffice; i++)
        {
            l.Week();
            AssertLinked(l);
        }

        Assert.Equal(memories, l.State.City.Person(member.Id)?.Memories.Count ?? 0);
        Assert.NotEqual(PromiseState.Vencida, promise.State);
        if (!comSuplente)
        {
            Assert.Equal(PromiseState.Extinta, promise.State);
            Assert.False(g.NoDealUntil.ContainsKey(member.Id));
        }
    }

    [Fact]
    public void Promessa_e_divida_com_lideranca_que_morre_se_extinguem_com_registro()
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        GovernanceState g = l.G;
        Leader leader = l.State.City!.Leaders.First(x => l.R.SeatOf(x.PersonId) is null);
        Leader other = l.State.City.Leaders.First(x => x.PersonId != leader.PersonId);
        DateOnly week = l.State.Calendar.WeekStart;
        Promise promise = l.State.Ledger.AddPromise(Promise.ParaPessoa, leader.PersonId, "vaga_gabinete", "Vaga no gabinete para um indicado", null, week, week.AddDays(7 * 60));
        Promise kept = l.State.Ledger.AddPromise(Promise.ParaPessoa, other.PersonId, "vaga_gabinete", "Vaga no gabinete para outro indicado", null, week, week.AddDays(7 * 60));
        Debt debt = l.State.Ledger.AddDebt(leader.PersonId, "Apoio na campanha", week);

        l.Renewal.Die(leader.PersonId, week);
        l.Week();

        Assert.Null(l.State.City.Leader(leader.PersonId));
        Assert.Equal(PromiseState.Extinta, promise.State);
        Assert.Equal(PromiseState.Aberta, kept.State);
        Assert.True(debt.Settled);
        Assert.True(debt.Extinct);
        Assert.Contains(g.Feed, f => f.Kind == "cobranca" && f.Text.Contains("se extingue") && f.Text.Contains(promise.Text) && f.PersonId == leader.PersonId);
        Assert.Contains(g.Feed, f => f.Kind == "cobranca" && f.Text.Contains("A dívida com") && f.Text.Contains(debt.Text));

        // Extinta não é vencida: não cobra, não pesa na imagem nem vira cobrança depois.
        l.Weeks(70);
        Assert.Equal(PromiseState.Extinta, promise.State);
        Assert.Equal(0, promise.Cobrancas);
        Assert.DoesNotContain(g.Demands, d => d.RefId == promise.Id || d.RefId == debt.Id);

        // A promessa feita a quem segue na cidade continuou valendo: foi cobrada e, sem resposta, venceu.
        Assert.True(kept.Cobrancas >= 1);
        Assert.Equal(PromiseState.Vencida, kept.State);
        AssertLinked(l);
    }

    [Fact]
    public void Morte_de_secretario_deixa_a_pasta_vaga_sem_tirar_o_partido_da_base()
    {
        Linked l = Linked.Of(GovKit.Mayor().State);
        GovernanceState g = l.G;
        ExecutiveState e = l.State.Executive!;
        l.Week();
        Secretary secretary = g.Secretaries.First(s => s.SecretariaId is not null && !s.Left);
        string pasta = secretary.SecretariaId!;
        string party = secretary.PartyId ?? e.PartyId;
        int held = e.Secretarias.GetValueOrDefault(party);
        int memories = l.State.City!.Person(secretary.PersonId)!.Memories.Count;
        DateOnly week = l.State.Calendar.WeekStart;

        l.Renewal.Die(secretary.PersonId, week);
        Assert.Contains(l.R.Historico, x => x.Tipo == RenewalEventKind.Morte && x.PessoaId == secretary.PersonId && x.Papel == CivicRole.Governo);
        l.Gov!.SyncWithWorld();

        Assert.Null(g.SecretaryOf(pasta));
        Assert.True(secretary.Left);
        Assert.Equal(week, g.VacantSince[pasta]);
        Assert.Equal(held, e.Secretarias.GetValueOrDefault(party));
        Assert.Equal(memories, l.State.City.Person(secretary.PersonId)!.Memories.Count);
        Assert.Contains(g.Feed, f => f.Kind == "secretario" && f.Text.Contains("sem titular") && f.PersonId == secretary.PersonId);
        Assert.Contains(g.Feed, f => f.Kind == "mundo" && f.Text.Contains("trabalhava no governo do município"));

        // O prefeito pode nomear outro nome; se não nomear, a pasta não fica vaga para sempre.
        Assert.Null(l.Gov.ForecastAppointSecretary(pasta, l.Gov.SecretaryOffers().First().PersonId).Blocked);
        l.Weeks(Content.Secretaries.Regras.SemanasPastaVaga + 2);
        Assert.NotNull(g.SecretaryOf(pasta));
        AssertLinked(l);
    }

    [Fact]
    public void Morte_de_assessor_abre_a_vaga_no_gabinete()
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        GovernanceState g = l.G;
        Aide aide = g.Aides.First(a => !a.Appointed && !a.Left);
        Assert.True(l.Gov!.AppointAide(aide.PersonId, AideRole.Comunicacao).Ok);
        Assert.NotNull(g.AideIn(AideRole.Comunicacao));
        l.Renewal.Die(aide.PersonId, l.State.Calendar.WeekStart);
        l.Week();
        Assert.True(aide.Left);
        Assert.Null(g.AideIn(AideRole.Comunicacao));
        Assert.Contains(g.Feed, f => f.Kind == "gabinete" && f.Text.Contains("não faz mais parte do gabinete") && f.PersonId == aide.PersonId);
        Aide next = g.Aides.First(a => !a.Appointed && !a.Left);
        Assert.Null(l.Gov.ForecastAppointAide(next.PersonId, AideRole.Comunicacao).Blocked);
        AssertLinked(l);
    }

    [Fact]
    public void Morte_de_quem_preside_a_Camara_passa_a_presidencia_sem_mexer_no_calendario_da_Mesa()
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        GovernanceState g = l.G;
        l.Weeks(2);
        string president = g.Mesa.PresidentId!;
        Assert.NotEqual(RaceSetup.PlayerId, president);
        DateOnly next = g.Mesa.NextElection;
        l.Renewal.Die(president, l.State.Calendar.WeekStart);
        l.Week();
        Assert.NotNull(g.Mesa.PresidentId);
        Assert.NotEqual(president, g.Mesa.PresidentId);
        Assert.NotNull(l.Gov!.Member(g.Mesa.PresidentId!));
        Assert.Equal(g.Mesa.PresidentId, l.State.Mandate!.PresidentId);
        Assert.Equal(next, g.Mesa.NextElection);
        Assert.Contains(g.Feed, f => f.Kind == "mesa" && f.Text.Contains("passa a presidir a Câmara"));
        AssertLinked(l);
    }

    // ---------------------------------------------------------------- Mesa: sem recondução

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Mesa_nao_reconduz_o_presidente_ao_mesmo_cargo_na_eleicao_seguinte()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        c.Week();
        string first = g.Mesa.PresidentId!;
        DateOnly second = g.Mesa.NextElection;
        while (g.Mesa.NextElection == second && c.State.Mandate is not null)
        {
            c.Week();
        }

        Assert.True(g.Mesa.Elected >= second);
        Assert.NotNull(g.Mesa.PresidentId);
        Assert.NotEqual(first, g.Mesa.PresidentId);

        // O jogador que preside não pode se lançar de novo; a previsão diz por quê, com o artigo.
        c.State.Player.Resources.PoliticalCapital = 100;
        g.Mesa.PresidentId = RaceSetup.PlayerId;
        GovForecast forecast = c.Gov.ForecastRunForMesa();
        Assert.Equal(GovFailure.LimiteLegal, forecast.Blocked);
        Assert.Contains("art. 57, § 4º", forecast.BlockedReason);
        Assert.False(c.Gov.RunForMesa().Ok);
    }

    // ---------------------------------------------------------------- carreira completa

    /// <summary>
    /// Carreira completa com tudo ligado, em 3 sementes, de vereador e de prefeito: a campanha com o mundo andando toda
    /// semana, a posse e os 4 anos de mandato, sem erro; com save e load no meio, as duas linhas terminam iguais.
    /// </summary>
    [Theory]
    [Trait("Categoria", "Lento")]
    [InlineData(3UL, false, 70)]
    [InlineData(4UL, false, 120)]
    [InlineData(5UL, false, 95)]
    [InlineData(3UL, true, 60)]
    [InlineData(4UL, true, 130)]
    [InlineData(5UL, true, 100)]
    public void Carreira_completa_com_tudo_ligado_termina_igual_com_save_e_load_no_meio(ulong seed, bool mayor, int saveAt)
    {
        Linked original = Linked.Career(seed, mayor);
        Assert.Equal(mayor, original.State.Executive is not null);
        Assert.True(original.Gov!.UsesRealIndicators);
        Assert.True(original.Gov.UsesRenewal);

        // A posse veio da eleição jogada: os eleitos esperam o 1º de janeiro e o vice do governo é o eleito.
        Assert.True(original.R.Eleitos is { Jogada: true });
        Assert.Equal(original.R.Eleitos!.ViceId, original.G.ViceId);
        DateOnly start = original.G.Start;
        var policy = new Rng(seed * 31);
        for (int week = 0; week < saveAt; week++)
        {
            Play(original, policy);
            original.Week();
            AssertLinked(original);
        }

        Assert.Equal(start, original.R.MandatoInicio);
        Assert.Equal(start, original.Real.MandatoDesde);
        Assert.Null(original.R.Eleitos);
        Assert.Equal(mayor ? RaceSetup.PlayerId : original.State.Mandate!.MayorId, original.R.PrefeitoId);

        string json = GameStateSerializer.Serialize(original.State);
        Linked loaded = Linked.Load(json);
        Assert.Equal(json, GameStateSerializer.Serialize(loaded.State));

        var policyA = new Rng(seed * 53);
        var policyB = new Rng(seed * 53);
        int weeks = saveAt;
        while (original.InOffice)
        {
            Assert.True(++weeks < 230, "O mandato não terminou em 4 anos.");
            Play(original, policyA);
            original.Week();
            AssertLinked(original);
            Play(loaded, policyB);
            loaded.Week();
        }

        Assert.False(loaded.InOffice);
        Assert.True(weeks >= 205, $"O mandato durou só {weeks} semanas.");

        // Mais algumas semanas fora do cargo, para a posse seguinte entrar nas duas linhas.
        foreach (Linked line in new[] { original, loaded })
        {
            for (int i = 0; i < 4; i++)
            {
                CareerFlow.AdvanceWeek(line.State, null, null, world: line.World);
            }
        }

        Assert.Equal(CampaignKit.Canonical(original.State), CampaignKit.Canonical(loaded.State));

        // O mundo andou junto: a Câmara seguinte tomou posse, os indicadores chegaram ao fim do mandato e houve notícia.
        Assert.Equal(start.AddYears(4), original.R.MandatoInicio);
        Assert.Equal(start.AddYears(4), original.Real.MandatoDesde);
        Assert.Contains(original.G.Feed, f => f.Kind == "mundo");
        Assert.Contains(original.G.Works, w => w.Stage == WorkStage.Entregue);
        Assert.Contains(original.Real.Indicadores, v => v.VariacaoAnoAnterior is { } y && y.Fatores.Any(f => f.Origem.StartsWith(GovernanceEngine.WorkOrigin, StringComparison.Ordinal)));
    }

    /// <summary>Quatro anos sem nenhum comando do jogador, com tudo ligado: o mundo anda, o governo anda, nada quebra.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Quatro_anos_sem_comando_com_tudo_ligado(bool mayor)
    {
        Linked l = Linked.Career(3, mayor);
        GovernanceState g = l.G;
        RenewalState r = l.R;
        DateOnly start = g.Start;
        int weeks = 0;
        var news = new HashSet<DateOnly>();
        bool budgetPressure = false;
        while (l.InOffice)
        {
            Assert.True(++weeks < 230, "O mandato não terminou em 4 anos.");
            DateOnly week = l.State.Calendar.WeekStart;
            l.Week();
            AssertLinked(l);
            budgetPressure |= l.Real.Pressoes.Any(p => p.Origem.StartsWith(ExecutiveEngine.BudgetOrigin, StringComparison.Ordinal));
            if (g.Feed.Any(f => f.Date == week))
            {
                news.Add(week);
            }
        }

        Assert.True(weeks >= 205, $"O mandato durou só {weeks} semanas.");
        Assert.True(news.Count >= weeks - 2, $"Só {news.Count} de {weeks} semanas tiveram notícia.");
        Assert.Equal(CareerMode.ForaDeMandato, CareerFlow.Mode(l.State));
        for (int i = 0; i < 4; i++)
        {
            CareerFlow.AdvanceWeek(l.State, null, null, world: l.World);
        }

        // O mundo andou sozinho: eleição automática, posse da legislatura seguinte e fatos da renovação no feed.
        Assert.Equal(start.AddYears(4), r.MandatoInicio);
        Assert.Equal(2, r.Legislaturas.Count(x => x.Inicio >= start));
        Assert.Contains(r.Historico, e => e.Tipo == RenewalEventKind.EleicaoMunicipal);
        Assert.Contains(g.Feed, f => f.Kind == "mundo");
        Assert.DoesNotContain(g.Works, w => w.SponsorId == RaceSetup.PlayerId);
        if (mayor)
        {
            // Sem comando, o prefeito não abre obra; o orçamento dele empurrou os indicadores reais enquanto governou e
            // deixa de empurrar quando o mandato acaba.
            Assert.True(budgetPressure);
            Assert.DoesNotContain(l.Real.Pressoes, p => p.Origem.StartsWith(ExecutiveEngine.BudgetOrigin, StringComparison.Ordinal));
        }
        else
        {
            // As obras que o governo NPC entregou sem o jogador estão nos indicadores reais, com a origem.
            Assert.Contains(g.Works, w => w.Stage == WorkStage.Entregue);
            Assert.Contains(l.Real.Indicadores, v => new[] { v.VariacaoNoAno, v.VariacaoAnoAnterior }.Any(y => y is not null && y.Fatores.Any(f => f.Origem.StartsWith(GovernanceEngine.WorkOrigin, StringComparison.Ordinal))));
            Assert.All(g.Works.Where(w => w.Stage == WorkStage.Entregue && w.Delivered >= start && Content.Works.Type(w.TypeId).Indicador is { } i && l.Evolution.Components(i).Any()
                && w.Delivered.Value.AddDays(7 * Content.Works.Regras.IndicadorSemanasServico) > l.State.Calendar.WeekStart),
                w => Assert.Contains(l.Real.Pressoes, p => p.Origem == GovernanceEngine.WorkOrigin + w.Id));
        }

        // Depois do mandato o mundo segue, sem governo acompanhado.
        for (int i = 0; i < 20; i++)
        {
            CareerFlow.AdvanceWeek(l.State, null, null, world: l.World);
        }

        Assert.Equal(l.State.Calendar.WeekStart.AddDays(-7), l.Real.UltimoPasso);
        Assert.Equal(l.State.Calendar.WeekStart.AddDays(-7), r.UltimoPasso);
    }
}
