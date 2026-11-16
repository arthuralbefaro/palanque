using Planalto.Core.Campaign;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Xunit;

namespace Planalto.Core.Tests.Governance;

/// <summary>Mandato de vereador (GDD 17.14): gabinete, instrumentos, acordos de voto, contagem nominal, instrução e a base que cobra.</summary>
public class CouncilTests
{
    private static InstrumentCatalog Instruments => CampaignKit.C.Governance.Instruments;

    private static double MayorRelation(GovKit.Council c) => c.Mandate.MayorRelation;

    [Fact]
    public void Posse_monta_o_governo_da_cidade_com_gente_obras_e_gabinete_a_escolher()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        Assert.Equal(GovRole.Vereador, g.Role);
        Assert.Equal(c.Mandate.Start, g.Start);
        Assert.Equal(CampaignKit.Zonas(CampaignKit.Municipio(g.Municipio)).Zonas.Count, g.Zones.Count);
        Assert.Equal(g.Zones.Count, g.Presence.Count);
        Assert.All(g.Presence, p => Assert.InRange(p, 0, 100));
        Assert.True(g.Presence.Max() > 0, "A presença da campanha não chegou ao mandato.");
        Assert.NotEmpty(g.BaseZones);

        // Secretários: uma pessoa com rosto e memória em cada pasta.
        Assert.Equal(CampaignKit.C.Governance.Secretaries.Secretarias.Count, g.Secretaries.Count(s => s.SecretariaId is not null));
        Assert.All(g.Secretaries, s =>
        {
            Assert.Contains(c.State.Npcs, n => n.Id == s.PersonId);
            Assert.True(c.State.City!.Person(s.PersonId)!.Has(PersonRole.Secretario));
            Assert.InRange(s.Competence, 1, 5);
            Assert.InRange(s.Integrity, 1, 5);
        });

        // Vereadores com reduto e ficha; empresas fictícias; obras do prefeito já no mapa.
        Assert.All(c.Mandate.Members.Where(m => !m.IsPlayer), m => Assert.Contains(g.MemberZones[m.Id], g.Zones));
        Assert.True(g.Companies.Count >= 2);
        Assert.Contains(g.Works, w => w.Open && w.SponsorId == Work.Prefeito);

        // Gabinete: quem trabalhou na campanha é candidato, e há um parente e nomes de mercado.
        Assert.Empty(g.Cabinet);
        Assert.Contains(g.Aides, a => a.Origin == Aide.Parente);
        Assert.Contains(g.Aides, a => a.Origin == Aide.DeMercado);
        foreach (StaffMember staff in c.State.City!.Staff.Where(s => s.Hired && !s.Left))
        {
            Assert.Contains(g.Aides, a => a.PersonId == staff.PersonId && a.Origin == Aide.DaCampanha);
        }

        // A Mesa é eleita na primeira semana.
        c.Week();
        Assert.NotNull(g.Mesa.PresidentId);
        Assert.Equal(g.Mesa.PresidentId, c.Mandate.PresidentId);
        Assert.Equal(new DateOnly(g.Start.Year + 2, 1, 1), g.Mesa.NextElection);
    }

    [Fact]
    public void Requerimento_revela_o_que_estava_escondido_e_esfria_o_prefeito()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        Work work = g.Works.First(w => w.Open);
        // Obra já contratada, com irregularidade escondida.
        work.CompanyId = g.Companies[0].Id;
        work.Stage = WorkStage.Execucao;
        work.StageEnd = c.State.Calendar.WeekStart.AddDays(7 * 30);
        work.Irregular = true;
        work.Overprice = 0.2;
        int zone = g.IndexOf(work.ZoneId);
        double presence = g.Presence[zone], image = c.Mandate.Image, relation = MayorRelation(c);

        GovForecast forecast = c.Gov.ForecastRequerimento(InfoRequest.SobreObra, work.Id);
        Assert.Null(forecast.Blocked);
        Assert.NotEmpty(forecast.Explanation);
        Assert.Contains(forecast.Outcomes, o => o.Probability is > 0 and < 1);
        Assert.Contains(c.Mandate.MayorName, forecast.Remembered);

        GovResult result = c.Gov.Requerimento(InfoRequest.SobreObra, work.Id);
        Assert.True(result.Ok);
        Assert.NotEmpty(result.Changes);
        GovKit.AssertWithin(forecast, GovForecast.Presenca, work.ZoneId, g.Presence[zone] - presence);
        GovKit.AssertWithin(forecast, GovForecast.Imagem, null, c.Mandate.Image - image);
        GovKit.AssertWithin(forecast, GovForecast.RelacaoPrefeito, null, MayorRelation(c) - relation);
        Assert.True(MayorRelation(c) < relation, "O requerimento tem de esfriar a relação com o prefeito.");
        Assert.Equal(Instruments.Regras.TurnosPorSemana - 1, g.TurnsLeft);
        Assert.False(work.Revealed);

        // Quem lembra: o prefeito guarda o requerimento.
        Assert.Contains(c.State.City!.Person(c.Mandate.MayorId)!.Memories, m => m.Fact == "requerimento");

        // O mesmo alvo não aceita outro requerimento enquanto a resposta não chega.
        Assert.Equal(GovFailure.AlvoInvalido, c.Gov.ForecastRequerimento(InfoRequest.SobreObra, work.Id).Blocked);

        c.Weeks(5);
        InfoRequest request = g.Requests.Single();
        Assert.True(request.Answered);
        Assert.Equal(Inspection.Irregular, request.Finding);
        Assert.True(work.Revealed);
        Assert.Contains(g.Feed, f => f.Kind == "gabinete" && f.ZoneId == work.ZoneId && f.Text.Contains("irregularidade"));
    }

    [Fact]
    public void Repetir_o_mesmo_instrumento_rende_cada_vez_menos_e_a_previsao_avisa()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        c.State.Player.Resources.PoliticalCapital = 50;
        string zone = g.Zones[0];
        WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => (t.Pequena || t.Servico) && c.Gov.Fits(t, zone));

        GovForecast first = c.Gov.ForecastIndicacao(type.Id, zone);
        Assert.Null(first.Blocked);
        Assert.DoesNotContain(first.Factors, f => f.Id == "cansaco");
        Assert.True(c.Gov.Indicacao(type.Id, zone).Ok);

        GovForecast second = c.Gov.ForecastIndicacao(type.Id, zone);
        YieldFactor tired = Assert.Single(second.Factors, f => f.Id == "cansaco");
        Assert.Equal(Instruments.Regras.CansacoFator, tired.Multiplier, 6);
        Assert.Contains(second.Factors, f => f.Id == "cansaco_zona");
        Assert.True(second.Line(GovForecast.Presenca, zone)!.Expected < first.Line(GovForecast.Presenca, zone)!.Expected, "A segunda indicação seguida na mesma zona tem de render menos.");
        Assert.True(c.Gov.Indicacao(type.Id, zone).Ok);
        GovForecast third = c.Gov.ForecastIndicacao(type.Id, zone);
        Assert.True(third.Line(GovForecast.Presenca, zone)!.Expected < second.Line(GovForecast.Presenca, zone)!.Expected);
        Assert.True(c.Gov.Indicacao(type.Id, zone).Ok);

        // Acabaram os turnos da semana: o comando bloqueado diz por quê.
        GovForecast blocked = c.Gov.ForecastIndicacao(type.Id, zone);
        Assert.Equal(GovFailure.SemTurno, blocked.Blocked);
        Assert.False(string.IsNullOrWhiteSpace(blocked.BlockedReason));

        // O cansaço some com o tempo.
        c.Weeks(8);
        Assert.DoesNotContain(c.Gov.ForecastIndicacao(type.Id, zone).Factors, f => f.Id == "cansaco");
    }

    [Fact]
    public void Indicacao_atendida_vira_obra_a_pedido_do_vereador_e_negada_fica_registrada()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        c.State.Player.Resources.PoliticalCapital = 100;
        int attended = 0, denied = 0;
        for (int week = 0; week < 12; week++)
        {
            string zone = g.Zones[week % g.Zones.Count];
            WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => (t.Pequena || t.Servico) && c.Gov.Fits(t, zone));
            GovForecast forecast = c.Gov.ForecastIndicacao(type.Id, zone);
            Assert.Null(forecast.Blocked);
            Assert.Equal(1, forecast.Outcomes.Sum(o => o.Probability), 6);
            Assert.True(c.Gov.Indicacao(type.Id, zone).Ok);
            c.Week();
        }

        c.Weeks(4);
        foreach (Indication indication in g.Indications)
        {
            Assert.NotEqual(IndicationState.Pendente, indication.State);
            if (indication.State == IndicationState.Atendida)
            {
                attended++;
                Work work = g.Work(indication.WorkId!)!;
                Assert.Equal(Work.DeIndicacao, work.Origin);
                Assert.Equal(RaceSetup.PlayerId, work.SponsorId);
                Assert.Equal(indication.ZoneId, work.ZoneId);
            }
            else
            {
                denied++;
                Assert.Null(indication.WorkId);
            }
        }

        Assert.True(attended > 0 && denied > 0, $"Em 12 indicações esperava atendidas e negadas (atendidas {attended}, negadas {denied}).");
    }

    [Fact]
    public void Fiscalizacao_acha_irregularidade_e_o_que_fazer_com_ela_e_uma_troca()
    {
        foreach (string option in FindingOption.Ids)
        {
            GovKit.Council c = GovKit.Councillor();
            GovernanceState g = c.G;
            Work work = g.Works.First(w => w.Open);
            work.CompanyId = g.Companies[0].Id;
            work.Stage = WorkStage.Execucao;
            work.StageEnd = c.State.Calendar.WeekStart.AddDays(7 * 40);
            work.Irregular = true;
            work.Overprice = 0.25;
            work.Revealed = true; // já se sabe por requerimento: a fiscalização confirma com certeza
            int zone = g.IndexOf(work.ZoneId);

            GovForecast forecast = c.Gov.ForecastFiscalizacao(work.Id);
            Assert.Null(forecast.Blocked);
            Assert.Contains(forecast.Outcomes, o => o.Probability > 0.99);
            double before = g.Presence[zone];
            GovResult result = c.Gov.Fiscalizacao(work.Id);
            Assert.True(result.Ok);
            GovKit.AssertWithin(forecast, GovForecast.Presenca, work.ZoneId, g.Presence[zone] - before);
            Inspection inspection = g.Inspections.Single();
            Assert.Equal(Inspection.Irregular, inspection.Finding);
            Assert.True(inspection.Pending);

            int skeletons = c.State.Ledger.Skeletons.Count;
            double relation = MayorRelation(c), image = c.Mandate.Image;
            before = g.Presence[zone];
            GovForecast decision = c.Gov.ForecastFinding(inspection.Id, option);
            Assert.Null(decision.Blocked);
            Assert.NotEmpty(decision.Remembered);
            Assert.True(c.Gov.DecideFinding(inspection.Id, option).Ok);
            string owner = g.Company(work.CompanyId)!.OwnerId;
            switch (option)
            {
                case FindingOption.Denunciar:
                    Assert.True(MayorRelation(c) < relation);
                    Assert.True(c.Mandate.Image > image);
                    Assert.True(g.Presence[zone] > before);
                    GovKit.AssertWithin(decision, GovForecast.Presenca, work.ZoneId, g.Presence[zone] - before);
                    Assert.Contains(c.State.City!.Person(owner)!.Memories, m => m.Fact == "achado_denunciar" && m.Pontos < 0);
                    Assert.Contains(c.State.City.Person(c.Mandate.MayorId)!.Memories, m => m.Fact == "achado_denunciar");
                    // A denúncia é a causa de um escândalo no governo.
                    CrisisRecord scandal = Assert.Single(g.Crises, r => r.CauseKind == CrisisRecord.CausaLicitacao);
                    Assert.Equal(work.Id, scandal.CauseRef);
                    Assert.True(work.Denounced);
                    break;
                case FindingOption.Negociar:
                    Assert.True(MayorRelation(c) > relation);
                    Assert.Equal(skeletons + 1, c.State.Ledger.Skeletons.Count);
                    LedgerSkeleton skeleton = c.State.Ledger.Skeletons[^1];
                    Assert.Contains(owner, skeleton.KnownBy);
                    Assert.Contains(c.Mandate.MayorId, skeleton.KnownBy);
                    Assert.Contains(g.Works, w => w.Origin == Work.DeAcordo && w.SponsorId == RaceSetup.PlayerId && w.ZoneId == work.ZoneId);
                    Assert.Empty(g.Crises);
                    break;
                default:
                    Assert.Equal(relation, MayorRelation(c));
                    Assert.Equal(skeletons, c.State.Ledger.Skeletons.Count);
                    Assert.NotNull(inspection.TribunalDue);
                    c.Weeks(22);
                    Assert.True(inspection.Closed);
                    Assert.Contains(g.Feed, f => f.Text.Contains("Tribunal de Contas deu razão"));
                    break;
            }
        }
    }

    [Fact]
    public void Achado_sem_decisao_no_prazo_segue_para_o_Tribunal_de_Contas()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        Work work = g.Works.First(w => w.Open);
        work.CompanyId = g.Companies[0].Id;
        work.Stage = WorkStage.Execucao;
        work.StageEnd = c.State.Calendar.WeekStart.AddDays(7 * 40);
        work.DelayWeeks = 8;
        Assert.True(c.Gov.Fiscalizacao(work.Id).Ok);
        Inspection inspection = g.Inspections.Single();
        Assert.Equal(Inspection.Atraso, inspection.Finding);
        c.Weeks(Instruments.Regras.PrazoDoAchadoSemanas + 1);
        Assert.Equal(FindingOption.Tribunal, inspection.Decision);
    }

    [Fact]
    public void Emenda_entra_no_mapa_espera_a_liberacao_e_so_se_inaugura_obra_pronta()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        string zone = g.Zones[g.BaseZones[0]];
        WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => t.Emenda && t.Pequena && c.Gov.Fits(t, zone));
        int emendas = c.Mandate.EmendasLeft;
        int index = g.IndexOf(zone);
        double before = g.Presence[index];

        GovForecast forecast = c.Gov.ForecastEmenda(type.Id, zone);
        Assert.Null(forecast.Blocked);
        Assert.NotNull(forecast.Line(GovForecast.PresencaDepois, zone));
        GovResult result = c.Gov.Emenda(type.Id, zone);
        Assert.True(result.Ok);
        Assert.Equal(emendas - 1, c.Mandate.EmendasLeft);
        GovKit.AssertWithin(forecast, GovForecast.Presenca, zone, g.Presence[index] - before);
        Work work = g.Work(result.CreatedId!)!;
        Assert.Equal(WorkStage.Liberacao, work.Stage);
        Assert.Equal(Work.DeEmenda, work.Origin);
        Assert.Equal(zone, work.ZoneId);

        // Antes de ficar pronta, não há inauguração.
        Assert.Equal(GovFailure.SemObraPronta, c.Gov.ForecastInaugurate(work.Id).Blocked);
        Assert.Equal(GovFailure.SemObraPronta, c.Gov.Inaugurate(work.Id).Failure);
        Assert.Equal(GovFailure.SemObraPronta, c.Gov.Inaugurate("obra_que_nao_existe").Failure);

        var stages = new List<WorkStage> { work.Stage };
        for (int week = 0; week < 150 && work.Stage != WorkStage.Pronta; week++)
        {
            c.Week();
            if (stages[^1] != work.Stage)
            {
                stages.Add(work.Stage);
            }
        }

        Assert.Equal([WorkStage.Liberacao, WorkStage.Projeto, WorkStage.Licitacao, WorkStage.Execucao, WorkStage.Pronta], stages);
        Assert.NotNull(work.CompanyId);
        Assert.NotNull(work.Mode);

        before = g.Presence[index];
        GovForecast inaugurate = c.Gov.ForecastInaugurate(work.Id);
        Assert.Null(inaugurate.Blocked);
        GovResult opened = c.Gov.Inaugurate(work.Id);
        Assert.True(opened.Ok);
        Assert.Equal(WorkStage.Entregue, work.Stage);
        Assert.True(work.Ceremony && work.PlayerAttended);
        GovKit.AssertWithin(inaugurate, GovForecast.Presenca, zone, g.Presence[index] - before);
        Assert.True(g.Presence[index] > before);
        Assert.Contains(opened.Changes, x => x.Contains("presença"));

        // Não se inaugura duas vezes.
        Assert.Equal(GovFailure.SemObraPronta, c.Gov.Inaugurate(work.Id).Failure);
    }

    [Fact]
    public void Obra_pronta_sem_inauguracao_e_entregue_sem_cerimonia_e_rende_menos()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        string zone = g.Zones[0];
        WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => t.Emenda && c.Gov.Fits(t, zone));
        Work work = g.Work(c.Gov.Emenda(type.Id, zone).CreatedId!)!;
        work.Stage = WorkStage.Pronta;
        work.StageStart = c.State.Calendar.WeekStart;
        c.Weeks(CampaignKit.C.Governance.Works.Regras.SemanasParaEntregaSemCerimonia + 1);
        Assert.Equal(WorkStage.Entregue, work.Stage);
        Assert.False(work.Ceremony);
        Assert.False(work.PlayerAttended);
        Assert.Contains(g.Feed, f => f.Kind == "obra" && f.Text.Contains("sem cerimônia"));
    }

    [Fact]
    public void No_periodo_vedado_o_candidato_nao_inaugura_mas_a_obra_e_entregue()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        string zone = g.Zones[0];
        WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => t.Emenda && c.Gov.Fits(t, zone));
        Work work = g.Work(c.Gov.Emenda(type.Id, zone).CreatedId!)!;
        work.Stage = WorkStage.Pronta;

        // Julho do ano da eleição, já em campanha: faltam menos de 3 meses para o pleito.
        int electionYear = g.End.Year;
        c.State.Calendar.JumpTo(new DateOnly(electionYear, 7, 10));
        Clock.ResetDay(c.State);
        Assert.False(c.Gov.PlayerBarredFromInauguration, "Sem candidatura, não há vedação.");
        CampaignEngine.Start(CampaignKit.Context(c.State));
        Assert.True(c.Gov.PlayerBarredFromInauguration);

        GovForecast forecast = c.Gov.ForecastInaugurate(work.Id);
        Assert.Equal(GovFailure.PeriodoVedado, forecast.Blocked);
        Assert.Contains("art. 77", forecast.BlockedReason);
        Assert.Equal(GovFailure.PeriodoVedado, c.Gov.Inaugurate(work.Id).Failure);
        Assert.Equal(WorkStage.Pronta, work.Stage);

        Assert.True(c.Gov.DeliverQuietly(work.Id).Ok);
        Assert.Equal(WorkStage.Entregue, work.Stage);
        Assert.False(work.PlayerAttended);

        // Em junho, mais de 3 meses antes, ainda podia.
        GovKit.Council early = GovKit.Councillor();
        Work other = early.G.Work(early.Gov.Emenda(type.Id, zone).CreatedId!)!;
        other.Stage = WorkStage.Pronta;
        early.State.Calendar.JumpTo(new DateOnly(electionYear, 6, 15));
        Clock.ResetDay(early.State);
        CampaignEngine.Start(CampaignKit.Context(early.State));
        Assert.False(early.Gov.PlayerBarredFromInauguration);
        Assert.True(early.Gov.Inaugurate(other.Id).Ok);
    }

    [Fact]
    public void Contagem_nominal_mostra_quem_vota_como_e_o_acordo_garante_o_voto()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        c.State.Player.Resources.PoliticalCapital = 50;
        NominalCount? count = null;
        CountLine? undecided = null;
        Bill? bill = null;
        for (int week = 0; week < 120 && undecided is null; week++)
        {
            c.Week();
            foreach (Bill b in c.Legislature.Pauta)
            {
                count = c.Gov.CountBill(b.Id)!;
                Assert.Equal(c.Mandate.Members.Count - 1, count.Lines.Count);
                Assert.Equal(count.Lines.Count, count.Lines.Count(l => l.Lean is NominalCount.Sim or NominalCount.Nao or NominalCount.Indeciso));
                Assert.All(count.Lines, l => Assert.False(string.IsNullOrWhiteSpace(l.Reason)));
                undecided = count.Lines.FirstOrDefault(l => l.Lean == NominalCount.Indeciso && l.RequestText is not null);
                if (undecided is not null)
                {
                    bill = b;
                    break;
                }
            }
        }

        Assert.True(undecided is not null && bill is not null && count is not null, "Em 120 semanas nenhuma votação teve indeciso com pedido.");
        Assert.Contains("quer", undecided!.Reason);
        Assert.False(string.IsNullOrWhiteSpace(GovernanceQueries.CountText(c.Gov, count!)));

        // Quem já está decidido não negocia.
        CountLine? decided = count!.Lines.FirstOrDefault(l => l.Lean != NominalCount.Indeciso);
        if (decided is not null)
        {
            Assert.Equal(GovFailure.AlvoInvalido, c.Gov.ForecastNegociarVoto(bill!.Id, decided.MemberId, 1).Blocked);
        }

        int promises = c.State.Ledger.Promises.Count;
        ChamberMember member = c.Mandate.Member(undecided.MemberId)!;
        double relation = member.Relation;
        GovForecast forecast = c.Gov.ForecastNegociarVoto(bill!.Id, undecided.MemberId, 1);
        Assert.Null(forecast.Blocked);
        Assert.Contains(member.Name, forecast.Remembered);
        GovResult result = c.Gov.NegociarVoto(bill.Id, undecided.MemberId, 1);
        Assert.True(result.Ok);
        Assert.Equal(promises + 1, c.State.Ledger.Promises.Count);
        Promise promise = c.State.Ledger.Promises[^1];
        Assert.Equal(Promise.ParaPessoa, promise.ToKind);
        Assert.Equal(member.Id, promise.ToId);
        Assert.True(member.Relation > relation);
        GovKit.AssertWithin(forecast, GovForecast.Relacao, member.Id, member.Relation - relation);
        Assert.Contains(c.State.City!.Person(member.Id)!.Memories, m => m.Fact == "acordo");

        // Depois do acordo, a contagem dá o voto dele como certo.
        CountLine after = c.Gov.CountBill(bill.Id)!.Lines.First(l => l.MemberId == member.Id);
        Assert.Equal(NominalCount.Sim, after.Lean);
        Assert.True(g.DealLogit(bill.Id, member.Id) > 0);
    }

    [Fact]
    public void Acordo_nao_cumprido_no_prazo_fica_na_memoria_e_o_vereador_para_de_negociar()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        ChamberMember member = c.Mandate.Members.First(m => !m.IsPlayer);
        DateOnly week = c.State.Calendar.WeekStart;
        Promise promise = c.State.Ledger.AddPromise(Promise.ParaPessoa, member.Id, "voto_" + VoteRequest.EmendaReduto, "Emenda no reduto", g.MemberZones[member.Id], week, week.AddDays(14));
        g.Deals.Add(new VoteDeal { Id = "acordo_teste", SubjectId = "pl_x", MemberId = member.Id, Side = 1, Logit = 4, RequestId = VoteRequest.EmendaReduto, ZoneId = g.MemberZones[member.Id], PromiseId = promise.Id, Date = week });
        double relation = member.Relation;
        c.Weeks(4);
        Assert.Equal(PromiseState.Quebrada, promise.State);
        Assert.True(member.Relation < relation - 10);
        Assert.Contains(c.State.City!.Person(member.Id)!.Memories, m => m.Fact == "acordo_quebrado" && m.Peso == -3);
        Assert.True(g.NoDealUntil[member.Id] > c.State.Calendar.WeekStart);
    }

    [Fact]
    public void Emenda_no_reduto_cumpre_o_acordo_com_o_vereador()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        ChamberMember member = c.Mandate.Members.First(m => !m.IsPlayer);
        string zone = g.MemberZones[member.Id];
        DateOnly week = c.State.Calendar.WeekStart;
        Promise promise = c.State.Ledger.AddPromise(Promise.ParaPessoa, member.Id, "voto_" + VoteRequest.EmendaReduto, "Emenda no reduto", zone, week, week.AddDays(7 * 52));
        g.Deals.Add(new VoteDeal { Id = "acordo_teste", SubjectId = "pl_x", MemberId = member.Id, Side = 1, Logit = 4, RequestId = VoteRequest.EmendaReduto, ZoneId = zone, PromiseId = promise.Id, Date = week });
        WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => t.Emenda && c.Gov.Fits(t, zone));
        double relation = member.Relation;
        GovResult result = c.Gov.Emenda(type.Id, zone);
        Assert.True(result.Ok);
        Assert.Equal(PromiseState.Cumprida, promise.State);
        Assert.True(member.Relation > relation);
        Assert.Contains(result.Changes, x => x.Contains(member.Name));
    }

    [Fact]
    public void Instrucao_permanente_vota_no_lugar_do_jogador_e_os_conflitos_sao_listados()
    {
        GovKit.Council absent = GovKit.Councillor();
        GovKit.Council instructed = GovKit.Councillor();
        Assert.True(instructed.Gov.SetInstruction(VoteInstruction.SeguirOrientacao).Ok);
        for (int week = 0; week < 40; week++)
        {
            absent.Week();
            instructed.Week();
        }

        List<Bill> voted = instructed.Mandate.Bills.Where(b => b.Stage is BillStage.Aprovado or BillStage.Rejeitado).ToList();
        Assert.NotEmpty(voted);
        Assert.All(voted, b => Assert.True(b.PlayerVote is VoteChoice.Sim or VoteChoice.Nao, "Com instrução, o jogador não falta."));
        Assert.DoesNotContain(instructed.Mandate.News, n => n.Kind == MandateNewsKind.Ausencia);

        // Sem instrução e sem marcar voto, o jogador falta (o comportamento de antes).
        Assert.Contains(absent.Mandate.News, n => n.Kind == MandateNewsKind.Ausencia);

        // A lista de conflitos é só das votações da pauta sem voto marcado; nenhuma linha vem sem motivo.
        Assert.All(instructed.Gov.VoteConflicts(), x => Assert.NotEmpty(x.Reasons));
        Assert.All(GovernanceQueries.Agenda(instructed.Gov), a => Assert.NotEqual(VoteChoice.Ausente, a.Planned));
    }

    [Fact]
    public void Gabinete_paga_promessa_de_vaga_parente_vira_esqueleto_e_quem_fica_de_fora_lembra()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        int slots = c.Gov.CabinetSlots;
        Assert.Equal(Instruments.Regras.GabinetePorPorte["pequeno"], slots);

        // Parente: esqueleto no Livro.
        Aide relative = g.Aides.First(a => a.Origin == Aide.Parente);
        int skeletons = c.State.Ledger.Skeletons.Count;
        GovForecast forecast = c.Gov.ForecastAppointAide(relative.PersonId, AideRole.Base, g.Zones[g.BaseZones[0]]);
        Assert.Null(forecast.Blocked);
        Assert.True(c.Gov.AppointAide(relative.PersonId, AideRole.Base, g.Zones[g.BaseZones[0]]).Ok);
        Assert.Equal(skeletons + 1, c.State.Ledger.Skeletons.Count);
        Assert.Equal("nepotismo", c.State.Ledger.Skeletons[^1].Type);
        Assert.Contains(c.State.City!.Person(relative.PersonId)!.Memories, m => m.Fact == "nomeado");

        // Assessor de base precisa de zona, e uma zona só tem um.
        Aide market = g.Aides.First(a => a.Origin == Aide.DeMercado);
        Assert.Equal(GovFailure.AlvoInvalido, c.Gov.ForecastAppointAide(market.PersonId, AideRole.Base, null).Blocked);
        Assert.Equal(GovFailure.AlvoInvalido, c.Gov.ForecastAppointAide(market.PersonId, AideRole.Base, g.Zones[g.BaseZones[0]]).Blocked);

        // Quem trabalhou na campanha e tinha cargo prometido: nomear cumpre a promessa.
        Aide? promised = g.Aides.FirstOrDefault(a => a.Origin == Aide.DaCampanha && c.State.Ledger.Open.Any(p => p.ToId == a.PersonId && p.Object == "cargo_equipe"));
        Aide second = promised ?? g.Aides.First(a => !a.Appointed && a.Origin == Aide.DaCampanha || a.Origin == Aide.DeMercado && !a.Appointed);
        GovResult appointed = c.Gov.AppointAide(second.PersonId, AideRole.Comunicacao);
        Assert.True(appointed.Ok);
        if (promised is not null)
        {
            Assert.Contains(c.State.Ledger.Promises, p => p.ToId == promised.PersonId && p.Object == "cargo_equipe" && p.State == PromiseState.Cumprida);
            Assert.Contains(appointed.Changes, x => x.Contains("Promessa cumprida"));
        }

        // O assessor de comunicação entra como fator de rendimento.
        Assert.Contains(c.Gov.YieldFactors(InstrumentDefinition.Requerimento, null), f => f.Id == "comunicacao");

        // Gabinete cheio (2 vagas em cidade pequena): não cabe mais ninguém, e os da campanha que ficaram de fora lembram.
        Assert.Equal(slots, g.Cabinet.Count());
        Assert.Equal(GovFailure.SemVaga, c.Gov.ForecastAppointAide(market.PersonId, AideRole.Chefe).Blocked);
        foreach (Aide left in g.Aides.Where(a => !a.Appointed && a.Origin == Aide.DaCampanha))
        {
            Assert.True(left.PassedOver);
            Assert.Contains(c.State.City.Person(left.PersonId)!.Memories, m => m.Fact == "preterido" && m.Pontos < 0);
        }

        // Exonerar abre a vaga e deixa lembrança.
        Assert.True(c.Gov.DismissAide(second.PersonId).Ok);
        Assert.Equal(slots - 1, g.Cabinet.Count());
        Assert.Contains(c.State.City.Person(second.PersonId)!.Memories, m => m.Fact == "exonerado");
    }

    [Fact]
    public void Assessor_de_base_segura_a_presenca_da_zona()
    {
        GovKit.Council with = GovKit.Councillor();
        GovKit.Council without = GovKit.Councillor();
        int zone = with.G.BaseZones[0];
        string zoneId = with.G.Zones[zone];
        Aide aide = with.G.Aides.Where(a => a.Origin == Aide.DeMercado).OrderByDescending(a => a.Quality).First();
        Assert.True(with.Gov.AppointAide(aide.PersonId, AideRole.Base, zoneId).Ok);
        with.Weeks(30);
        without.Weeks(30);
        Assert.True(with.G.Presence[zone] > without.G.Presence[zone], "Sem assessor de base a presença tem de cair mais.");
        Assert.True(without.G.Presence[zone] >= without.G.PresenceAtStart[zone] * Instruments.Regras.PresencaPiso - 1e-9);
        // O reconhecimento do mandato acompanha a presença nas zonas.
        Assert.Equal(without.G.CityPresence(), without.Mandate.Recognition, 6);
    }

    [Fact]
    public void A_base_cobra_presenca_e_some_com_quem_nao_aparece()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        int zone = g.BaseZones[0];
        string zoneId = g.Zones[zone];
        g.Presence[zone] = g.PresenceAtStart[zone] * 0.5;
        c.Week();
        Demand demand = Assert.Single(g.Demands, d => d.Kind == Demand.Presenca);
        Assert.Equal(zoneId, demand.ZoneId);
        Assert.Null(demand.Met);
        Assert.Contains(g.Feed, f => f.Kind == "cobranca" && f.ZoneId == zoneId);

        // Atender: qualquer instrumento do gabinete na zona.
        GovKit.Council answered = GovKit.Load(Planalto.Core.Save.GameStateSerializer.Serialize(c.State));
        answered.State.Player.Resources.PoliticalCapital = 20;
        WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => (t.Pequena || t.Servico) && answered.Gov.Fits(t, zoneId));
        Assert.True(answered.Gov.Indicacao(type.Id, zoneId).Ok);
        Assert.True(answered.G.Demands.Single(d => d.Kind == Demand.Presenca).Met);

        // Ignorar: vence, e quem cobrou lembra.
        c.Weeks(Instruments.Regras.BaseCobraPrazoSemanas + 2);
        Assert.False(demand.Met);
        if (demand.PersonId is not null)
        {
            Assert.Contains(c.State.City!.Person(demand.PersonId)!.Memories, m => m.Fact == "base_cobra_vencida" && m.Pontos < 0);
            Assert.NotEqual(RaceSetup.PlayerId, c.State.City.Leader(demand.PersonId)?.AlignedWith);
        }
    }

    [Fact]
    public void Mesa_e_eleita_a_cada_dois_anos_e_o_vereador_pode_disputar()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        c.Week();
        string? first = g.Mesa.PresidentId;
        Assert.NotNull(first);
        c.State.Player.Resources.PoliticalCapital = 0;
        Assert.Equal(GovFailure.SemCapital, c.Gov.ForecastRunForMesa().Blocked);

        c.State.Player.Resources.PoliticalCapital = 30;
        // Todos gostam do jogador: ele ganha a Mesa.
        foreach (ChamberMember m in c.Mandate.Members.Where(m => !m.IsPlayer))
        {
            m.Relation = 100;
        }

        GovForecast forecast = c.Gov.ForecastRunForMesa();
        Assert.Null(forecast.Blocked);
        Assert.True(c.Gov.RunForMesa().Ok);
        Assert.Equal(GovFailure.JaFeito, c.Gov.ForecastRunForMesa().Blocked);
        while (c.State.Calendar.WeekStart < g.Mesa.NextElection.AddDays(7) && c.State.Mandate is not null && g.Mesa.Elected < new DateOnly(g.Start.Year + 2, 1, 1))
        {
            foreach (ChamberMember m in c.Mandate.Members.Where(m => !m.IsPlayer))
            {
                m.Relation = 100;
            }

            c.Week();
        }

        Assert.Equal(RaceSetup.PlayerId, g.Mesa.PresidentId);
        Assert.Equal(RaceSetup.PlayerId, c.Mandate.PresidentId);
        Assert.False(g.Mesa.PlayerRunning);
        Assert.Contains(g.Feed, f => f.Kind == "mesa" && f.Text.Contains("Você vence a eleição da Mesa"));
        Assert.True(g.CommitteeBonus >= CampaignKit.C.Governance.Chamber.Regras.MesaBonusComissao - 1e-9);
    }

    [Fact]
    public void CPI_precisa_de_um_terco_das_assinaturas_e_pode_levar_a_cassacao_do_prefeito()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        Work work = g.Works.First(w => w.Open);
        work.CompanyId = g.Companies[0].Id;
        work.Stage = WorkStage.Execucao;
        work.StageEnd = c.State.Calendar.WeekStart.AddDays(7 * 60);

        // Sem irregularidade conhecida, não há CPI.
        Assert.Equal(GovFailure.AlvoInvalido, c.Gov.ForecastCpi(work.Id).Blocked);
        work.Irregular = true;
        work.Revealed = true;
        g.Requests.Add(new InfoRequest { Id = "req_teste", TargetKind = InfoRequest.SobreObra, TargetId = work.Id, Filed = g.Start, Due = g.Start, Answered = true });

        // Ninguém assina: o pedido cai.
        foreach (ChamberMember m in c.Mandate.Members.Where(m => !m.IsPlayer))
        {
            m.Relation = -100;
        }

        GovKit.Council alone = GovKit.Load(Planalto.Core.Save.GameStateSerializer.Serialize(c.State));
        foreach (ChamberMember m in alone.Mandate.Members.Where(m => !m.IsPlayer))
        {
            m.PartyId = alone.Mandate.MayorPartyId;
        }

        Assert.True(alone.Gov.RequestCpi(work.Id).Ok);
        Assert.Null(alone.G.Cpi);
        Assert.Contains(alone.G.Feed, f => f.Kind == "cpi" && f.Text.Contains("não reuniu assinaturas"));

        // Com a Câmara a favor, a CPI abre, indicia (há requerimento respondido) e a cassação passa.
        foreach (ChamberMember m in c.Mandate.Members.Where(m => !m.IsPlayer))
        {
            m.Relation = 100;
            m.PartyId = null;
        }

        c.Mandate.BaseParties.Clear();
        GovForecast forecast = c.Gov.ForecastCpi(work.Id);
        Assert.Null(forecast.Blocked);
        Assert.Contains(forecast.Explanation, x => x.Contains("Assinaturas"));
        string mayor = c.Mandate.MayorName;
        GovResult result = c.Gov.RequestCpi(work.Id);
        Assert.True(result.Ok);
        Cpi cpi = g.Cpi!;
        Assert.True(cpi.Signatures >= CampaignKit.GovernanceContext(c.State).ChamberRules.AssinaturasNecessarias(c.Mandate.Members.Count));
        Assert.True(cpi.Evidence >= CampaignKit.C.Governance.Chamber.Regras.CpiIndiciaAcima);
        Assert.Contains(c.State.City!.Person(c.Mandate.MayorId)!.Memories, m => m.Fact == "cpi_autor" && m.Peso == -3);
        g.MayorApproval = 10;
        for (int week = 0; week < 20 && g.Cpi is not null; week++)
        {
            g.MayorApproval = 10;
            c.Week();
        }

        Assert.Null(g.Cpi);
        Assert.Equal(CpiOutcome.Indiciou, g.CpiHistory.Single().Outcome);
        CassationVote vote = g.Cassations.Single();
        Assert.True(vote.Passed, $"Placar {vote.Yes} a {vote.No}, precisava de {vote.Needed}.");
        Assert.True(g.MayorRemoved);
        Assert.Equal(g.ViceId, c.Mandate.MayorId);
        Assert.NotEqual(mayor, c.Mandate.MayorName);
        Assert.Contains(g.Feed, f => f.Text.Contains("assume a prefeitura"));

        // A vaga fica registrada para o sistema de sucessão; sem ele ligado, valeu o padrão provisório (o vice).
        OfficeVacancy vacancy = Assert.Single(g.Vacancies);
        Assert.Equal(Planalto.Core.Elections.Office.Prefeito, vacancy.Office);
        Assert.Equal(OfficeVacancy.Cassacao, vacancy.Cause);
        Assert.Equal(cpi.Id, vacancy.CpiId);
        Assert.False(vacancy.Handled);

        // Outra CPI não abre logo em seguida.
        Assert.Equal(GovFailure.Bloqueado, c.Gov.ForecastCpi(work.Id).Blocked);
    }

    [Fact]
    public void Cassacao_entrega_a_vaga_ao_gancho_de_sucessao_quando_ele_esta_ligado()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceContext plain = CampaignKit.GovernanceContext(c.State);
        OfficeVacancy? received = null;
        var hooked = new GovernanceEngine(new GovernanceContext
        {
            State = plain.State, Municipio = plain.Municipio, Content = plain.Content, Legislature = plain.Legislature, Executive = plain.Executive, People = plain.People, Campaign = plain.Campaign,
            Zonas = plain.Zonas,
            OnVacancy = v =>
            {
                received = v;
                return true;
            },
        });
        GovernanceState g = c.G;
        Work work = g.Works.First(w => w.Open);
        work.CompanyId = g.Companies[0].Id;
        work.Stage = WorkStage.Execucao;
        work.StageEnd = c.State.Calendar.WeekStart.AddDays(7 * 60);
        work.Irregular = true;
        work.Revealed = true;
        g.Requests.Add(new InfoRequest { Id = "req_teste", TargetKind = InfoRequest.SobreObra, TargetId = work.Id, Filed = g.Start, Due = g.Start, Answered = true });
        foreach (ChamberMember m in c.Mandate.Members.Where(m => !m.IsPlayer))
        {
            m.Relation = 100;
            m.PartyId = null;
        }

        c.Mandate.BaseParties.Clear();
        string mayorId = c.Mandate.MayorId;
        Assert.True(hooked.RequestCpi(work.Id).CreatedId is not null);
        for (int week = 0; week < 20 && g.Cpi is not null; week++)
        {
            g.MayorApproval = 10;
            Planalto.Core.Career.CareerFlow.AdvanceWeek(c.State, null, c.Legislature, governance: hooked);
        }

        Assert.True(g.Cassations.Single().Passed);
        Assert.NotNull(received);
        Assert.Equal(mayorId, received!.HolderId);
        Assert.True(g.Vacancies.Single().Handled);
        // Com o gancho ligado, a substituição é de quem cuida da sucessão: o vice não assume por aqui.
        Assert.Equal(mayorId, c.Mandate.MayorId);
        Assert.DoesNotContain(g.Feed, f => f.Text.Contains("assume a prefeitura"));
    }

    [Fact]
    public void Opcoes_de_instrumento_trazem_previsao_e_cada_uma_pode_ser_usada()
    {
        GovKit.Council c = GovKit.Councillor();
        c.State.Player.Resources.PoliticalCapital = 30;
        c.Weeks(6);
        IReadOnlyList<InstrumentOption> options = c.Gov.CouncilOptions();
        Assert.Contains(options, o => o.InstrumentId == InstrumentDefinition.Indicacao);
        Assert.Contains(options, o => o.InstrumentId == InstrumentDefinition.Emenda);
        Assert.Contains(options, o => o.InstrumentId == InstrumentDefinition.Projeto);
        Assert.Contains(options, o => o.InstrumentId == InstrumentDefinition.Requerimento);
        Assert.All(options, o =>
        {
            Assert.Null(o.Forecast.Blocked);
            Assert.NotEmpty(o.Forecast.Explanation);
            Assert.NotEmpty(o.Forecast.Lines);
        });

        foreach (string instrument in options.Select(o => o.InstrumentId).Distinct().ToList())
        {
            GovKit.Council copy = GovKit.Load(Planalto.Core.Save.GameStateSerializer.Serialize(c.State));
            InstrumentOption option = copy.Gov.CouncilOptions().First(o => o.InstrumentId == instrument);
            GovResult result = copy.Gov.Use(option);
            Assert.True(result.Ok, $"{instrument}: {result.Failure} {result.Text}");
            Assert.False(string.IsNullOrWhiteSpace(result.Text));
            Assert.Equal(Instruments.Regras.TurnosPorSemana - 1, copy.G.TurnsLeft);
        }
    }

    [Fact]
    public void Projeto_de_lei_e_protocolado_pelo_instrumento_e_tramita_no_mandato()
    {
        GovKit.Council c = GovKit.Councillor();
        c.State.Player.Resources.PoliticalCapital = 10;
        BillTemplate template = c.Gov.ProposableBills().First();
        GovForecast forecast = c.Gov.ForecastProjeto(template.Id);
        Assert.Null(forecast.Blocked);
        Assert.Equal(1, forecast.Outcomes.Sum(o => o.Probability), 6);
        int capital = c.State.Player.Resources.PoliticalCapital;
        GovResult result = c.Gov.Projeto(template.Id);
        Assert.True(result.Ok);
        Bill bill = c.Mandate.Bills.Single(b => b.Id == result.CreatedId);
        Assert.Equal(RaceSetup.PlayerId, bill.AuthorId);
        Assert.Equal(BillStage.Comissao, bill.Stage);
        Assert.Equal(capital - Instruments.Instrument(InstrumentDefinition.Projeto).Capital, c.State.Player.Resources.PoliticalCapital);
        Assert.Equal(GovFailure.AlvoInvalido, c.Gov.ForecastProjeto(template.Id).Blocked);
    }
}
