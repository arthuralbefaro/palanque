using Planalto.Core.Campaign;
using Planalto.Core.Executive;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Xunit;

namespace Planalto.Core.Tests.Governance;

/// <summary>Prefeitura (GDD 17.15): secretários, obras com licitação, folha e limites fiscais, convênios, Câmara que negocia, crises com causa, CPI e cassação.</summary>
public class CityHallTests
{
    private static GovernanceContent Content => CampaignKit.C.Governance;

    private static WorkType SmallWork(GovKit.CityHall h, string zone) => Content.Works.Tipos.First(t => t.Pequena && h.Gov.Fits(t, zone));

    private static WorkType BigWork(GovKit.CityHall h, string zone) => Content.Works.Tipos.First(t => !t.Pequena && !t.Servico && h.Gov.Fits(t, zone));

    [Fact]
    public void Posse_do_prefeito_tem_secretarios_pela_coalizao_e_nomes_oferecidos_pelos_partidos()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        Assert.Equal(GovRole.Prefeito, g.Role);
        List<Secretary> seated = g.Secretaries.Where(s => s.SecretariaId is not null).ToList();
        Assert.Equal(Content.Secretaries.Secretarias.Count, seated.Count);
        Assert.Equal(seated.Count, seated.Select(s => s.SecretariaId).Distinct().Count());
        // Os titulares seguem a repartição de secretarias entre os partidos da base.
        foreach (var (party, count) in h.E.Secretarias)
        {
            Assert.Equal(count, seated.Count(s => (s.PartyId ?? h.E.PartyId) == party));
        }

        // Cada partido com cadeira oferece nomes, e há nomes técnicos.
        foreach (string party in h.E.Chamber.Select(m => m.PartyId).OfType<string>().Distinct())
        {
            Assert.Equal(Content.Secretaries.Regras.OfertasPorPartido, h.Gov.SecretaryOffers(party).Count());
        }

        Assert.Contains(h.Gov.SecretaryOffers(), s => s.PartyId is null);
        Assert.All(GovernanceQueries.Secretaries(h.Gov), v => Assert.True(v.CompetenceMax - v.CompetenceMin <= 2 && v.Agenda.Length > 0));
        Assert.Empty(g.Aides);
    }

    [Fact]
    public void Nomear_e_demitir_secretario_mexe_na_base_e_fica_na_memoria()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        string pasta = Content.Secretaries.Secretarias[0].Id;
        Secretary before = g.SecretaryOf(pasta)!;
        Secretary technician = h.Gov.SecretaryOffers().First(s => s.PartyId is null);

        GovForecast forecast = h.Gov.ForecastAppointSecretary(pasta, technician.PersonId);
        Assert.Null(forecast.Blocked);
        Assert.Contains(h.Gov.NameOf(technician.PersonId), forecast.Remembered);
        Assert.Contains(h.Gov.NameOf(before.PersonId), forecast.Remembered);
        Assert.True(h.Gov.AppointSecretary(pasta, technician.PersonId).Ok);
        Assert.Equal(technician.PersonId, g.SecretaryOf(pasta)!.PersonId);
        Assert.True(before.Left);
        Assert.Contains(h.State.City!.Person(before.PersonId)!.Memories, m => m.Fact == "demitido" && m.Pontos < 0);
        Assert.Contains(h.State.City.Person(technician.PersonId)!.Memories, m => m.Fact == "nomeado" && m.Pontos > 0);
        Assert.Equal(Content.Secretaries.Secretarias.Count, h.E.Secretarias.Values.Sum());

        // Entregar uma pasta a um partido de fora da base põe o partido na base e melhora a bancada.
        string? outsider = h.E.Chamber.Select(m => m.PartyId).OfType<string>().Distinct().FirstOrDefault(p => !h.E.InBase(p));
        if (outsider is null)
        {
            return;
        }

        string other = Content.Secretaries.Secretarias[1].Id;
        Secretary offered = h.Gov.SecretaryOffers(outsider).First();
        List<ChamberMember> bench = h.E.Chamber.Where(m => m.PartyId == outsider).ToList();
        double relation = bench[0].Relation;
        Assert.True(h.Gov.AppointSecretary(other, offered.PersonId).Ok);
        Assert.True(h.E.InBase(outsider));
        Assert.True(bench[0].Relation > relation);
        // O outro nome que o partido oferecia foi preterido e lembra.
        Assert.Contains(h.Gov.SecretaryOffers(outsider), s => h.State.City.Person(s.PersonId)!.Memories.Any(m => m.Fact == "preterido"));

        // Demitir o único secretário do partido tira o partido da base.
        relation = bench[0].Relation;
        GovForecast dismiss = h.Gov.ForecastDismissSecretary(other);
        Assert.Contains(dismiss.Explanation, x => x.Contains("sai da base"));
        Assert.True(h.Gov.DismissSecretary(other).Ok);
        Assert.False(h.E.InBase(outsider));
        Assert.True(bench[0].Relation < relation);
        Assert.Null(g.SecretaryOf(other));

        // Pasta vaga não fica vaga para sempre: o partido do prefeito indica alguém.
        h.Weeks(Content.Secretaries.Regras.SemanasPastaVaga + 1);
        Assert.NotNull(g.SecretaryOf(other));
    }

    [Fact]
    public void Competencia_do_secretario_muda_a_velocidade_do_indicador_da_pasta()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        SecretariaDefinition pasta = Content.Secretaries.Secretarias.First(s => s.Area == "saude");
        typeof(Secretary).GetProperty(nameof(Secretary.Competence))!.SetValue(g.SecretaryOf(pasta.Id), 5);
        h.Week();
        Assert.Equal(Content.Secretaries.Regras.VelocidadePorCompetencia[4], g.AreaSpeed["saude"], 6);
        typeof(Secretary).GetProperty(nameof(Secretary.Competence))!.SetValue(g.SecretaryOf(pasta.Id), 1);
        h.Week();
        Assert.Equal(Content.Secretaries.Regras.VelocidadePorCompetencia[0], g.AreaSpeed["saude"], 6);
    }

    [Fact]
    public void Obra_do_prefeito_passa_por_projeto_modalidade_licitacao_execucao_e_inauguracao()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        string zone = g.Zones[0];
        WorkType type = BigWork(h, zone);
        decimal left = h.Gov.InvestmentLeft;

        GovForecast forecast = h.Gov.ForecastStartWork(type.Id, zone);
        Assert.Null(forecast.Blocked);
        Assert.NotNull(forecast.Line(GovForecast.Investimento));
        GovResult started = h.Gov.StartWork(type.Id, zone);
        Assert.True(started.Ok);
        Work work = g.Work(started.CreatedId!)!;
        Assert.Equal(h.Gov.CostOf(type), work.Cost);
        Assert.Equal(left - work.Cost, h.Gov.InvestmentLeft);
        Assert.Equal(WorkStage.Projeto, work.Stage);
        Assert.Equal(GovFailure.SemObraPronta, h.Gov.Inaugurate(work.Id).Failure);

        for (int i = 0; i < 20 && work.Stage == WorkStage.Projeto; i++)
        {
            h.Week();
        }

        // Projeto pronto: a obra espera a escolha da modalidade.
        Assert.Equal(WorkStage.Modalidade, work.Stage);
        var modes = h.Gov.ModesFor(work);
        Assert.Equal(4, modes.Count);

        // Dispensa por valor: barrada acima do teto da lei, com o artigo no motivo.
        Assert.True(work.Cost > ProcurementLawConfig.Default.DispensaObras);
        GovForecast dispensa = h.Gov.ForecastMode(work.Id, ProcurementMode.DispensaPorValor);
        Assert.Equal(GovFailure.LimiteLegal, dispensa.Blocked);
        Assert.Contains("art. 75", dispensa.BlockedReason);
        Assert.Equal(GovFailure.LimiteLegal, h.Gov.ChooseMode(work.Id, ProcurementMode.DispensaPorValor).Failure);
        Assert.Equal(WorkStage.Modalidade, work.Stage);

        // Pregão não serve para obra grande (regra de jogo); concorrência serve.
        Assert.Equal(GovFailure.AlvoInvalido, h.Gov.ForecastMode(work.Id, ProcurementMode.Pregao).Blocked);
        Assert.True(h.Gov.ChooseMode(work.Id, ProcurementMode.Concorrencia).Ok);
        Assert.Equal(WorkStage.Licitacao, work.Stage);
        Assert.False(work.Irregular);

        for (int i = 0; i < 160 && work.Stage != WorkStage.Pronta; i++)
        {
            h.Week();
        }

        Assert.Equal(WorkStage.Pronta, work.Stage);
        Assert.NotNull(work.CompanyId);
        int index = g.IndexOf(zone);
        double approval = g.ZoneApproval[index];
        GovForecast inaugurate = h.Gov.ForecastInaugurate(work.Id);
        Assert.Null(inaugurate.Blocked);
        Assert.True(h.Gov.Inaugurate(work.Id).Ok);
        Assert.Equal(WorkStage.Entregue, work.Stage);
        GovKit.AssertWithin(inaugurate, GovForecast.AprovacaoZona, zone, g.ZoneApproval[index] - approval);
        Assert.True(g.ZoneApproval[index] > approval);
    }

    [Fact]
    public void Dispensa_por_valor_cabe_em_obra_pequena_de_cidade_pequena()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        string zone = g.Zones[0];
        WorkType type = SmallWork(h, zone);
        Work work = g.Work(h.Gov.StartWork(type.Id, zone).CreatedId!)!;
        Assert.True(work.Cost <= ProcurementLawConfig.Default.DispensaObras, "A obra pequena de cidade pequena tem de caber na dispensa por valor.");
        work.Stage = WorkStage.Modalidade;
        Assert.Null(h.Gov.ForecastMode(work.Id, ProcurementMode.DispensaPorValor).Blocked);
        Assert.True(h.Gov.ChooseMode(work.Id, ProcurementMode.DispensaPorValor).Ok);
        Assert.Equal(ProcurementMode.DispensaPorValor, work.Mode);
        Assert.False(work.Irregular);
    }

    [Fact]
    public void Emergencial_sem_emergencia_e_irregularidade_e_com_emergencia_nao()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        string zone = g.Zones[0];
        WorkType type = Content.Works.Tipos.First(t => !t.Servico && !t.Pequena && t.ExecucaoSemanas.Max <= 50 && h.Gov.Fits(t, zone));

        // Sem emergência reconhecida: a previsão avisa, e a contratação passa como irregular e vira esqueleto.
        Work irregular = g.Work(h.Gov.StartWork(type.Id, zone).CreatedId!)!;
        irregular.Stage = WorkStage.Modalidade;
        Assert.False(h.Gov.EmergencyIn(zone));
        GovForecast forecast = h.Gov.ForecastMode(irregular.Id, ProcurementMode.Emergencial);
        Assert.Null(forecast.Blocked);
        Assert.Contains(forecast.Explanation, x => x.Contains("irregular") && x.Contains("art. 75, VIII"));
        Assert.True(h.Gov.ChooseMode(irregular.Id, ProcurementMode.Emergencial).Ok);
        Assert.True(irregular.Irregular);
        int skeletons = h.State.Ledger.Skeletons.Count;
        for (int i = 0; i < 6 && irregular.Stage == WorkStage.Licitacao; i++)
        {
            h.Week();
        }

        Assert.Equal(WorkStage.Execucao, irregular.Stage);
        Assert.Contains(h.State.Ledger.Skeletons.Skip(skeletons), s => s.Type == "contratacao_emergencial_irregular" && s.KnownBy.Contains(g.Company(irregular.CompanyId)!.OwnerId));

        // Com crise recente na zona, a emergência está reconhecida e a contratação é regular.
        GovKit.CityHall flooded = GovKit.Mayor();
        string z = flooded.G.Zones[0];
        flooded.G.Crises.Add(new CrisisRecord { Id = "crise_teste", CrisisId = "enchente", Date = flooded.State.Calendar.WeekStart, ZoneId = z, CauseKind = CrisisRecord.CausaZonaSemDrenagem, CauseRef = z, CauseText = "teste", Presented = true, Resolved = true });
        Work regular = flooded.G.Work(flooded.Gov.StartWork(type.Id, z).CreatedId!)!;
        regular.Stage = WorkStage.Modalidade;
        Assert.True(flooded.Gov.EmergencyIn(z));
        Assert.DoesNotContain(flooded.Gov.ForecastMode(regular.Id, ProcurementMode.Emergencial).Explanation, x => x.Contains("irregular"));
        Assert.True(flooded.Gov.ChooseMode(regular.Id, ProcurementMode.Emergencial).Ok);
        Assert.False(regular.Irregular);
    }

    [Fact]
    public void Investimento_e_capacidade_de_execucao_limitam_as_obras()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        string zone = g.Zones[0];
        WorkType type = SmallWork(h, zone);
        int capacity = Content.Works.Regras.CapacidadeDeExecucao["pequeno"];
        for (int i = 0; i < capacity; i++)
        {
            Assert.True(h.Gov.StartWork(type.Id, zone).Ok);
        }

        GovForecast full = h.Gov.ForecastStartWork(type.Id, zone);
        Assert.Equal(GovFailure.SemVaga, full.Blocked);
        Assert.False(string.IsNullOrWhiteSpace(full.BlockedReason));

        GovKit.CityHall broke = GovKit.Mayor();
        broke.G.Fiscal.InvestmentUsed = broke.G.Fiscal.Rcl;
        GovForecast none = broke.Gov.ForecastStartWork(type.Id, zone);
        Assert.Equal(GovFailure.SemInvestimento, none.Blocked);
        Assert.Contains("R$", none.BlockedReason);
    }

    [Fact]
    public void Greve_so_nasce_de_negociacao_que_falhou()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        g.Payroll.Share = 0.45;
        NegotiationTable table = Content.Personnel.Mesas.First(m => m.AfetaFolha);
        h.State.Calendar.JumpTo(new DateOnly(g.Start.Year, table.Mes, 10));
        h.Week();
        Negotiation negotiation = Assert.Single(g.Negotiations, n => n.TableId == table.Id);
        Assert.Equal(NegotiationOutcome.Pendente, negotiation.Outcome);
        Assert.Same(negotiation, h.Gov.PendingNegotiation);
        Assert.Empty(g.Crises);

        // Cada resposta tem previsão, com a chance de a categoria aceitar.
        foreach (string answer in NegotiationAnswer.Ids)
        {
            GovForecast forecast = h.Gov.ForecastNegotiation(negotiation.Id, answer);
            Assert.Null(forecast.Blocked);
            Assert.NotEmpty(forecast.Explanation);
        }

        Assert.Equal(1, h.Gov.AcceptChance(negotiation, NegotiationAnswer.Conceder), 9);
        Assert.True(h.Gov.AcceptChance(negotiation, NegotiationAnswer.Contraproposta) > h.Gov.AcceptChance(negotiation, NegotiationAnswer.Negar));

        // Conceder: acordo, a folha sobe pelo reajuste, e não há greve.
        GovKit.CityHall generous = GovKit.LoadMayor(GameStateSerializer.Serialize(h.State));
        Negotiation same = generous.G.Negotiations.Single();
        double share = generous.G.Payroll.Share;
        GovForecast concede = generous.Gov.ForecastNegotiation(same.Id, NegotiationAnswer.Conceder);
        Assert.True(generous.Gov.AnswerNegotiation(same.Id, NegotiationAnswer.Conceder).Ok);
        Assert.Equal(NegotiationOutcome.Acordo, same.Outcome);
        Assert.Equal(share * (1 + same.Asked), generous.G.Payroll.Share, 9);
        GovKit.AssertWithin(concede, GovForecast.Folha, null, generous.G.Payroll.Share - share);
        Assert.Empty(generous.G.Crises);

        // Negar: a mesa falha e a greve começa, apontando para a negociação que a causou.
        share = g.Payroll.Share;
        Assert.True(h.Gov.AnswerNegotiation(negotiation.Id, NegotiationAnswer.Negar).Ok);
        Assert.Equal(NegotiationOutcome.Fracassou, negotiation.Outcome);
        Assert.Equal(share, g.Payroll.Share, 9);
        CrisisRecord strike = Assert.Single(g.Crises);
        Assert.Equal(table.Crise, strike.CrisisId);
        Assert.Equal(CrisisRecord.CausaNegociacao, strike.CauseKind);
        Assert.Equal(negotiation.Id, strike.CauseRef);
        Assert.Equal(strike.Id, negotiation.StrikeRecordId);
        Assert.True(strike.Date >= negotiation.Date);
        Assert.Contains("sem acordo", strike.CauseText);

        // Na semana seguinte a crise chega à mesa do prefeito, pela tela de crises de sempre.
        h.Week();
        Assert.Equal(table.Crise, h.E.Crisis?.CrisisId);
        Assert.True(strike.Presented && !strike.Resolved);
        h.Executive.ResolveCrisis(0);
        Assert.True(strike.Resolved);
        Assert.Equal(0, strike.Choice);
        Assert.Contains(GovernanceQueries.Crises(h.Gov), v => v.Record.Id == strike.Id && v.Cause.Length > 0);
    }

    [Fact]
    public void Acima_do_prudencial_o_art_22_veda_reajuste_cargo_e_pessoal_novo()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        FiscalLawConfig law = FiscalLawConfig.Default;
        g.Payroll.Share = 0.52;
        g.Payroll.Band = PayrollBand.Normal;
        g.Feed.Clear();
        g.Fiscal.GeneralRevision = 0.03;
        NegotiationTable table = Content.Personnel.Mesas.First(m => m.AfetaFolha);
        h.State.Calendar.JumpTo(new DateOnly(g.Start.Year, table.Mes, 10));
        h.Week();
        Assert.Equal(PayrollBand.Prudencial, g.Payroll.Band);
        Assert.Contains(g.Feed, f => f.Kind == "folha" && f.Text.Contains("limite prudencial") && f.Text.Contains("art. 22"));
        Negotiation negotiation = g.Negotiations.Single(n => n.TableId == table.Id);

        // Reajuste acima da revisão geral: vedado, com o artigo no motivo. A revisão geral continua possível.
        Assert.True(negotiation.Asked > negotiation.GeneralRevision);
        GovForecast barred = h.Gov.ForecastNegotiation(negotiation.Id, NegotiationAnswer.Conceder);
        Assert.Equal(GovFailure.VedadoArt22, barred.Blocked);
        Assert.Contains("art. 22", barred.BlockedReason);
        Assert.Equal(GovFailure.VedadoArt22, h.Gov.AnswerNegotiation(negotiation.Id, NegotiationAnswer.Conceder).Failure);
        Assert.Equal(NegotiationOutcome.Pendente, negotiation.Outcome);

        Assert.Null(h.Gov.ForecastNegotiation(negotiation.Id, NegotiationAnswer.RevisaoGeral).Blocked);

        // Cargo para quem tinha a vaga prometida: vedado também.
        Leader leader = h.State.City!.Leaders[0];
        Promise post = h.State.Ledger.AddPromise(Promise.ParaPessoa, leader.PersonId, "vaga_gabinete", "Vaga para um indicado", leader.Zones[0], g.Start, g.Start.AddDays(400));
        Assert.Equal(GovFailure.VedadoArt22, h.Gov.ForecastGrantPost(post.Id).Blocked);

        // Obra que precisa de equipe abre sem pessoal novo, e a folha não cresce.
        string zone = g.Zones[0];
        WorkType staffed = Content.Works.Tipos.First(t => t.Folha > 0 && !t.Servico && h.Gov.Fits(t, zone));
        Work work = g.Work(h.Gov.StartWork(staffed.Id, zone).CreatedId!)!;
        work.Stage = WorkStage.Pronta;
        double share = g.Payroll.Share;
        GovResult opened = h.Gov.Inaugurate(work.Id);
        Assert.True(opened.Ok);
        Assert.Equal(share, g.Payroll.Share, 9);
        Assert.Contains(opened.Changes, x => x.Contains("art. 22"));

        // Abaixo do prudencial, o cargo sai: a promessa fica cumprida e a folha sobe.
        g.Payroll.Share = 0.45;
        Assert.Null(h.Gov.ForecastGrantPost(post.Id).Blocked);
        Assert.True(h.Gov.GrantPost(post.Id).Ok);
        Assert.Equal(PromiseState.Cumprida, post.State);
        Assert.True(g.Payroll.Share > 0.45);
        Assert.Equal(law.PrudencialExecutivo, GovernanceQueries.Fiscal(h.Gov).PrudentialAt, 9);
    }

    [Fact]
    public void Folha_acima_do_limite_corre_o_prazo_do_art_23_e_trava_os_convenios()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        FiscalLawConfig law = FiscalLawConfig.Default;
        h.State.Calendar.JumpTo(new DateOnly(g.Start.Year + 1, 6, 7));
        g.Fiscal.Year = g.Start.Year + 1;
        g.Payroll.Share = 0.58;
        h.Week();
        Assert.Equal(PayrollBand.Excedido, g.Payroll.Band);
        DateOnly since = g.Payroll.ExcessSince!.Value;
        Assert.False(g.Payroll.Restricted);
        FiscalView view = GovernanceQueries.Fiscal(h.Gov);
        Assert.Equal(law.PrazoParaEliminar(since), view.ExcessDeadline);
        Assert.True(view.RaisesBarred);
        Assert.Equal(5, view.Art22Bars.Count);

        // O prazo passa com a folha ainda acima do limite: valem as consequências.
        while (h.State.Calendar.WeekStart <= law.PrazoParaEliminar(since))
        {
            g.Payroll.Share = 0.58;
            h.Week();
        }

        g.Payroll.Share = 0.58;
        h.Week();
        Assert.True(g.Payroll.Restricted);
        Assert.Contains(g.Feed, f => f.Kind == "folha" && f.Text.Contains("transferências voluntárias") && f.Text.Contains("art. 23"));
        Assert.Equal(3, GovernanceQueries.Fiscal(h.Gov).Art23Consequences.Count);

        // Com a restrição, nenhum convênio novo aparece.
        int offers = g.Agreements.Count;
        for (int i = 0; i < 60; i++)
        {
            g.Payroll.Share = 0.58;
            h.Week();
        }

        Assert.Equal(offers, g.Agreements.Count);
        Assert.Contains(g.Feed, f => f.Kind == "convenio" && f.Text.Contains("transferências voluntárias"));

        // Cortar pessoal baixa a folha, custa aprovação e o sindicato lembra; de volta ao limite, a restrição cai.
        GovForecast cut = h.Gov.ForecastCutPayroll();
        double share = g.Payroll.Share;
        Assert.True(h.Gov.CutPayroll().Ok);
        GovKit.AssertWithin(cut, GovForecast.Folha, null, g.Payroll.Share - share);
        g.Payroll.Share = 0.50;
        h.Week();
        Assert.False(g.Payroll.Restricted);
        Assert.Null(g.Payroll.ExcessSince);
    }

    [Fact]
    public void Minimos_de_saude_e_educacao_valem_sobre_a_base_de_impostos_e_transferencias()
    {
        GovKit.CityHall h = GovKit.Mayor();
        ExecutiveConfig config = CampaignKit.C.Executive.Config;
        FiscalLawConfig law = FiscalLawConfig.Default;
        double baseShare = config.TaxBaseShare["pequeno"];
        Assert.Equal(baseShare, h.G.Fiscal.TaxBaseShare, 9);
        Assert.Equal(baseShare, h.Executive.TaxBaseShare, 9);
        BudgetArea health = config.Area("saude"), education = config.Area("educacao");
        Assert.Equal(0.15 * baseShare, h.Executive.MinimumShare(health), 9);
        Assert.Equal(0.25 * baseShare, h.Executive.MinimumShare(education), 9);
        Assert.Empty(h.Executive.BelowMinimum);

        // 20% do orçamento em educação cumpre o mínimo (25% da base, que é 62% da receita = 15,5%); 14% não cumpre.
        var allocation = new Dictionary<string, double>(h.E.Allocation) { ["educacao"] = 0.20, ["obras"] = h.E.Allocation["obras"] + (h.E.Allocation["educacao"] - 0.20) };
        Assert.Null(h.Executive.SetAllocation(allocation));
        Assert.DoesNotContain(h.Executive.BelowMinimum, a => a.Id == "educacao");
        allocation = new Dictionary<string, double>(h.E.Allocation) { ["educacao"] = 0.14, ["obras"] = h.E.Allocation["obras"] + (h.E.Allocation["educacao"] - 0.14) };
        Assert.Null(h.Executive.SetAllocation(allocation));
        Assert.Contains(h.Executive.BelowMinimum, a => a.Id == "educacao");

        FiscalView view = GovernanceQueries.Fiscal(h.Gov);
        Assert.Equal(h.G.Fiscal.Rcl * (decimal)baseShare, view.TaxBase);
        Assert.Equal(law.MinimoEmReais("saude", view.TaxBase), view.HealthMinimum);
        Assert.Equal(law.MinimoEmReais("educacao", view.TaxBase), view.EducationMinimum);
        Assert.True(view.HealthMinimum < h.G.Fiscal.Rcl * 0.15m, "O mínimo sobre a base de impostos é menor que 15% do orçamento todo.");
    }

    [Fact]
    public void Repasse_a_Camara_nao_passa_do_teto_do_art_29_A()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        double cap = h.Gov.ChamberCap;
        Assert.Equal(0.07, cap, 9);
        Assert.True(g.Fiscal.ChamberShare <= cap);
        GovResult over = h.Gov.SetChamberTransfer(cap + 0.001);
        Assert.Equal(GovFailure.LimiteLegal, over.Failure);
        Assert.Contains("29-A", over.Text);

        double relation = h.E.Chamber[0].Relation;
        Assert.True(h.Gov.SetChamberTransfer(cap).Ok);
        Assert.Equal(cap, g.Fiscal.ChamberShare, 9);
        Assert.True(h.E.Chamber[0].Relation > relation);
        relation = h.E.Chamber[0].Relation;
        Assert.True(h.Gov.SetChamberTransfer(cap * 0.5).Ok);
        Assert.True(h.E.Chamber[0].Relation < relation);
        FiscalView view = GovernanceQueries.Fiscal(h.Gov);
        Assert.Equal(view.ChamberBudget * 0.70m, view.ChamberPayrollCap);
    }

    [Fact]
    public void Convenio_paga_parte_da_obra_e_devolve_o_dinheiro_se_o_prazo_vencer()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        string zone = g.Zones[0];
        WorkType type = BigWork(h, zone);
        DateOnly week = h.State.Calendar.WeekStart;
        var offer = new Agreement { Id = "conv_teste", OriginId = "estado", TypeId = type.Id, Value = Math.Round(h.Gov.CostOf(type) * 0.8m, 2), Counterpart = 0.2, Offered = week, OfferExpires = week.AddDays(28), Weeks = 6 };
        g.Agreements.Add(offer);
        decimal left = h.Gov.InvestmentLeft;
        GovForecast forecast = h.Gov.ForecastSignAgreement(offer.Id, zone);
        Assert.Null(forecast.Blocked);
        Assert.Equal(4, forecast.Explanation.Count);
        GovResult signed = h.Gov.SignAgreement(offer.Id, zone);
        Assert.True(signed.Ok);
        Work work = g.Work(signed.CreatedId!)!;
        Assert.Equal(offer.Id, work.AgreementId);
        Assert.Equal(AgreementState.Assinado, offer.State);
        // O município entra só com a contrapartida: o que o convênio não cobre.
        decimal own = left - h.Gov.InvestmentLeft;
        Assert.Equal(work.Cost - offer.Value, own);
        GovKit.AssertWithin(forecast, GovForecast.Investimento, null, -(double)own);

        // Seis semanas não bastam: o prazo vence, o dinheiro volta e novos convênios ficam bloqueados.
        h.Weeks(8);
        Assert.Equal(AgreementState.Devolvido, offer.State);
        Assert.NotNull(g.AgreementsBlockedUntil);
        Assert.Contains(g.Feed, f => f.Kind == "convenio" && f.Text.Contains("devolve"));
        Assert.Equal(left - work.Cost, h.Gov.InvestmentLeft);

        // Nos três meses antes do pleito não há transferência voluntária: o convênio não pode ser assinado (Lei 9.504, art. 73, VI).
        GovKit.CityHall election = GovKit.Mayor();
        election.State.Calendar.JumpTo(new DateOnly(election.G.End.Year, 8, 10));
        DateOnly august = election.State.Calendar.WeekStart;
        election.G.Agreements.Add(new Agreement { Id = "conv_eleicao", OriginId = "estado", TypeId = type.Id, Value = 1000m, Counterpart = 0.1, Offered = august, OfferExpires = august.AddDays(280), Weeks = 90 });
        GovForecast barred = election.Gov.ForecastSignAgreement("conv_eleicao", election.G.Zones[0]);
        Assert.Equal(GovFailure.PeriodoVedado, barred.Blocked);
        Assert.Contains("art. 73, VI", barred.BlockedReason);

        // Recusar uma oferta é possível e fica no feed.
        var other = new Agreement { Id = "conv_teste2", OriginId = "uniao", TypeId = type.Id, Value = 1000m, Counterpart = 0.1, Offered = week, OfferExpires = week.AddDays(280), Weeks = 90 };
        g.Agreements.Add(other);
        Assert.True(h.Gov.DeclineAgreement(other.Id).Ok);
        Assert.Equal(AgreementState.Recusado, other.State);
    }

    [Fact]
    public void Camara_negocia_o_prefeito_atende_o_pedido_de_um_indeciso_e_ganha_o_voto()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        h.State.Player.Resources.PoliticalCapital = 30;
        (PolicyDefinition Policy, NominalCount Count, CountLine Line)? found = null;
        foreach (PolicyDefinition policy in CampaignKit.C.Executive.Policies.Policies.Where(p => p.RequiresLaw))
        {
            NominalCount count = h.Gov.CountPolicy(policy.Id)!;
            Assert.Equal(h.E.Chamber.Count, count.Lines.Count);
            Assert.Equal(count.Members, count.Yes + count.No + count.Undecided);
            if (count.Lines.FirstOrDefault(l => l.Lean == NominalCount.Indeciso && l.RequestText is not null) is { } line)
            {
                found = (policy, count, line);
                break;
            }
        }

        Assert.Null(h.Gov.CountPolicy(CampaignKit.C.Executive.Policies.Policies.First(p => !p.RequiresLaw).Id));
        if (found is null)
        {
            // Sem indeciso nesta Câmara: força um, pondo um vereador no limiar.
            PolicyDefinition policy = CampaignKit.C.Executive.Policies.Policies.First(p => p.RequiresLaw);
            ChamberMember swing = h.E.Chamber[0];
            for (double relation = -100; relation <= 100 && found is null; relation += 5)
            {
                swing.Relation = relation;
                NominalCount count = h.Gov.CountPolicy(policy.Id)!;
                if (count.Lines.First(l => l.MemberId == swing.Id) is { Lean: NominalCount.Indeciso, RequestText: not null } line)
                {
                    found = (policy, count, line);
                }
            }
        }

        Assert.True(found is not null, "Não foi possível achar um vereador indeciso.");
        var (target, _, undecided) = found!.Value;
        ChamberMember member = h.E.Chamber.First(m => m.Id == undecided.MemberId);
        int promises = h.State.Ledger.Promises.Count;
        GovForecast forecast = h.Gov.ForecastPolicyDeal(target.Id, member.Id);
        Assert.Null(forecast.Blocked);
        GovResult deal = h.Gov.NegotiatePolicyVote(target.Id, member.Id);
        Assert.True(deal.Ok);
        Assert.Equal(promises + 1, h.State.Ledger.Promises.Count);
        Assert.Equal(NominalCount.Sim, h.Gov.CountPolicy(target.Id)!.Lines.First(l => l.MemberId == member.Id).Lean);
        Assert.Contains(h.State.City!.Person(member.Id)!.Memories, m => m.Fact == "acordo");
        Assert.True(g.DealLogit(target.Id, member.Id) > 0);

        // Pedido de obra no reduto: abrir a obra ali cumpre o combinado.
        VoteDeal record = g.Deals.Single();
        Promise promise = h.State.Ledger.Promises[^1];
        if (record.ZoneId is { } zone)
        {
            Assert.Equal(PromiseState.Aberta, promise.State);
            Assert.True(h.Gov.StartWork(SmallWork(h, zone).Id, zone).Ok);
            Assert.Equal(PromiseState.Cumprida, promise.State);
            Assert.Contains(h.State.City.Person(member.Id)!.Memories, m => m.Fact == "acordo_cumprido");
        }
        else
        {
            // Cargo para o indicado: sai na hora, pela folha.
            Assert.Equal(PromiseState.Cumprida, promise.State);
        }
    }

    [Fact]
    public void Aprovacao_baixa_com_escandalo_abre_CPI_e_a_cassacao_tira_o_mandato()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        // Escândalo recente com causa registrada, aprovação no chão e a Câmara toda contra.
        g.Crises.Add(new CrisisRecord
        {
            Id = "crise_teste", CrisisId = "escandalo_secretario", Date = h.State.Calendar.WeekStart, CauseKind = CrisisRecord.CausaSecretario, CauseRef = g.Secretaries[0].PersonId,
            CauseText = "Secretário contratava sem controle", Presented = true, Resolved = true,
        });
        void Sink()
        {
            h.E.Image = -1;
            foreach (string indicator in h.E.Indicators.Keys.ToList())
            {
                h.E.Indicators[indicator] = 5;
            }

            foreach (ChamberMember m in h.E.Chamber)
            {
                m.Relation = -100;
                m.PartyId = null;
            }

            foreach (string party in h.E.Secretarias.Keys.Where(p => p != h.E.PartyId).ToList())
            {
                h.E.Secretarias[party] = 0;
            }
        }

        Sink();
        h.Week();
        Sink();
        h.Week();
        Assert.True(h.E.Approval < Content.Chamber.Regras.CpiAprovacaoAbaixo);
        Cpi cpi = g.Cpi!;
        Assert.False(cpi.ByPlayer);
        Assert.Contains("Secretário contratava sem controle", cpi.Subject);
        Assert.Contains(g.Feed, f => f.Kind == "cpi" && f.Text.Contains("CPI instalada"));

        // A CPI vira decisão do prefeito (evento de mandato ligado ao fato).
        // Outra decisão pode chegar antes (uma cobrança do Livro, por exemplo): vale a opção padrão e a espera segue.
        for (int i = 0; i < 12 && g.Decision?.EventId != "cpi_contra_o_governo"; i++)
        {
            if (GovernanceQueries.Decision(h.Gov) is { } other)
            {
                Assert.True(h.Gov.Decide(other.Default.Id).Ok);
                continue;
            }

            Sink();
            h.Week();
        }

        DecisionView view = GovernanceQueries.Decision(h.Gov)!;
        Assert.Equal("cpi_contra_o_governo", view.Decision.EventId);
        Assert.All(view.Options, o => Assert.NotEmpty(o.Forecast.Explanation));
        double evidence = cpi.Evidence;
        Assert.True(h.Gov.Decide("cooperar").Ok);
        Assert.True(cpi.Evidence < evidence);
        Assert.Null(g.Decision);

        // Prova suficiente e aprovação baixa no fim: indiciamento, votação e perda do mandato.
        for (int i = 0; i < 30 && g.Cpi is not null && h.State.Executive is not null; i++)
        {
            cpi.Evidence = 0.95;
            g.Decision = null;
            Sink();
            h.Week();
        }

        Assert.Equal(CpiOutcome.Indiciou, g.CpiHistory.Single().Outcome);
        CassationVote vote = g.Cassations.Single();
        Assert.True(vote.Passed, $"Placar {vote.Yes} a {vote.No}; precisava de {vote.Needed}.");
        Assert.Null(h.State.Executive);
        Assert.True(g.PlayerRemoved);
        Assert.False(h.Gov.Active);
        Assert.Equal(Planalto.Core.Career.CareerMode.ForaDeMandato, Planalto.Core.Career.CareerFlow.Mode(h.State));
        Assert.Contains(g.Feed, f => f.Kind == "cpi" && f.Text.Contains("cassou o seu mandato"));
    }

    [Fact]
    public void Prefeitura_de_quatro_anos_so_tem_crise_com_causa_registrada()
    {
        GovKit.CityHall h = GovKit.Mayor();
        GovernanceState g = h.G;
        ExecutiveState e = h.E;
        var seen = new HashSet<string>();
        int weeks = 0;
        while (h.State.Executive is not null && weeks++ < 215)
        {
            if (h.E.Crisis is { } pending)
            {
                // Toda crise que chega à mesa do prefeito tem registro com causa.
                CrisisRecord record = g.Crises.Single(c => c.Presented && !c.Resolved);
                Assert.Equal(pending.CrisisId, record.CrisisId);
                Assert.False(string.IsNullOrWhiteSpace(record.CauseText));
                seen.Add(record.Id);
            }

            if (g.Decision is not null)
            {
                Assert.True(h.Gov.Decide(h.Gov.DecisionDefinition!.Opcoes.First(o => o.Padrao).Id).Ok);
            }

            if (h.Gov.PendingNegotiation is { } negotiation)
            {
                h.Gov.AnswerNegotiation(negotiation.Id, weeks % 2 == 0 ? NegotiationAnswer.Negar : NegotiationAnswer.RevisaoGeral);
            }

            h.Week();
        }

        Assert.NotEmpty(g.Crises);
        Assert.All(g.Crises, c => Assert.False(string.IsNullOrWhiteSpace(c.CauseRef)));
        // Nenhuma greve sem negociação anterior que tenha falhado.
        foreach (CrisisRecord strike in g.Crises.Where(c => c.CrisisId.StartsWith("greve", StringComparison.Ordinal)))
        {
            Negotiation cause = g.Negotiations.Single(n => n.Id == strike.CauseRef);
            Assert.Equal(NegotiationOutcome.Fracassou, cause.Outcome);
            Assert.True(cause.Date <= strike.Date);
        }

        Assert.Contains(g.Crises, c => c.CrisisId.StartsWith("greve", StringComparison.Ordinal));
        Assert.True(seen.Count > 0, "Nenhuma crise chegou à mesa do prefeito em 4 anos.");
        Assert.All(e.Indicators.Values, v => Assert.InRange(v, 0, 100));
        Assert.InRange(g.Payroll.Share, 0.2, 0.9);
    }
}
