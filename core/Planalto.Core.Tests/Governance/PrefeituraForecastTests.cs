using Planalto.Core.Campaign;
using Planalto.Core.Executive;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Xunit;
using Xunit.Abstractions;

namespace Planalto.Core.Tests.Governance;

/// <summary>
/// Princípio f (GDD 17.1) nos comandos da prefeitura e do mandato que tinham o "o que faz" montado na tela: em 1.000
/// execuções (cada uma com um cenário sorteado por uma semente diferente), o resultado do comando cai na faixa que a
/// previsão do Core deu antes do clique em pelo menos 80% das vezes, e a previsão nunca vem vazia.
/// </summary>
public sealed class PrefeituraForecastTests(ITestOutputHelper output)
{
    private const int Runs = 1_000;

    private const double Eps = 1e-6;

    /// <summary>Linha ausente na previsão significa "não muda": só o zero cabe.</summary>
    private static bool Within(GovForecast forecast, string kind, string? target, double actual) => forecast.Contains(kind, target, actual);

    private void Report(string command, int inside, int runs)
    {
        output.WriteLine($"{command}: {inside} de {runs} dentro da faixa ({100.0 * inside / runs:0.0}%).");
        Assert.True(inside >= runs * 0.8, $"{command}: só {inside} de {runs} resultados dentro da faixa prevista.");
    }

    private static void AssertExplained(GovForecast forecast)
    {
        Assert.Null(forecast.Blocked);
        Assert.NotEmpty(forecast.Explanation);
        Assert.All(forecast.Explanation, e => Assert.False(string.IsNullOrWhiteSpace(e)));
    }

    private static void Scramble(ExecutiveState e, Rng scenario)
    {
        e.Image = scenario.NextDouble(-0.6, 0.6);
        e.Recognition = scenario.NextDouble(5, 80);
        foreach (string group in e.Support.Keys.ToList())
        {
            e.Support[group] = scenario.NextDouble(0, 1);
        }

        foreach (ChamberMember m in e.Chamber)
        {
            m.Relation = Math.Clamp(scenario.NextDouble(-90, 90), -100, 100);
        }
    }

    [Fact]
    public void Politica_por_lei_tem_votos_e_chance_de_aprovar_e_o_placar_cai_na_faixa()
    {
        GovKit.CityHall city = GovKit.Mayor();
        ExecutiveEngine mayor = city.Executive;
        ExecutiveState e = city.E;
        e.Crisis = null;
        List<PolicyDefinition> laws = mayor.Context.Content.Policies.Policies.Where(p => p.RequiresLaw).ToList();
        Assert.NotEmpty(laws);
        var policies = e.Policies.ToList();
        e.Allocation[ExecutiveConfig.ReserveArea] += 0.2;
        int inside = 0, approved = 0;
        double predicted = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 1);
            Scramble(e, scenario);
            e.Policies.Clear();
            e.Policies.AddRange(policies);
            e.EnergyLeft = mayor.Context.Config.WeeklyEnergy;
            PolicyDefinition policy = laws[i % laws.Count];
            GovForecast forecast = mayor.ForecastEnact(policy.Id);
            AssertExplained(forecast);
            Assert.NotNull(forecast.Line(GovForecast.VotosSim));
            double pass = mayor.VoteDistribution(policy).Approve;
            Assert.Equal(pass is > 0 and < 1 ? 2 : 0, forecast.Outcomes.Count);
            double image = e.Image;
            var support = new Dictionary<string, double>(e.Support);
            EnactResult result = mayor.Enact(policy.Id);
            Assert.True(result.Ok);
            approved += result.Approved ? 1 : 0;
            predicted += pass;
            bool ok = Within(forecast, GovForecast.VotosSim, null, result.Yes)
                && Within(forecast, GovForecast.Imagem, null, e.Image - image)
                && support.Keys.Union(e.Support.Keys).All(g => Within(forecast, GovForecast.Apoio, g, e.Support.GetValueOrDefault(g) - support.GetValueOrDefault(g)))
                && Within(forecast, GovForecast.Energia, null, -mayor.Context.Config.EnactEnergy);
            inside += ok ? 1 : 0;
        }

        Report("Enviar à Câmara (política com lei)", inside, Runs);

        // A chance de aprovar mostrada antes do clique bate com o que a Câmara faz.
        output.WriteLine($"Aprovadas: {approved} de {Runs}; chance média prevista: {predicted / Runs:P1}.");
        Assert.InRange(approved / (double)Runs, (predicted / Runs) - 0.05, (predicted / Runs) + 0.05);
    }

    [Fact]
    public void Decreto_e_revogacao_mudam_imagem_apoio_e_reserva_como_previsto()
    {
        GovKit.CityHall city = GovKit.Mayor();
        ExecutiveEngine mayor = city.Executive;
        ExecutiveState e = city.E;
        e.Crisis = null;
        List<PolicyDefinition> decrees = mayor.Context.Content.Policies.Policies.Where(p => !p.RequiresLaw).ToList();
        var policies = e.Policies.ToList();
        e.Allocation[ExecutiveConfig.ReserveArea] += 0.2;
        int enactInside = 0, revokeInside = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 5_000);
            Scramble(e, scenario);
            e.Policies.Clear();
            e.Policies.AddRange(policies);
            e.EnergyLeft = mayor.Context.Config.WeeklyEnergy;
            PolicyDefinition policy = decrees[i % decrees.Count];
            if (mayor.CanEnact(policy) is not null)
            {
                e.Policies.RemoveAll(p => p.PolicyId == policy.Id);
            }

            GovForecast forecast = mayor.ForecastEnact(policy.Id);
            AssertExplained(forecast);
            Assert.Empty(forecast.Outcomes);
            double image = e.Image, reserve = mayor.FreeReserve;
            var support = new Dictionary<string, double>(e.Support);
            Assert.True(mayor.Enact(policy.Id).Approved);
            enactInside += Within(forecast, GovForecast.Imagem, null, e.Image - image)
                && Within(forecast, GovForecast.Reserva, null, mayor.FreeReserve - reserve)
                && support.Keys.Union(e.Support.Keys).All(g => Within(forecast, GovForecast.Apoio, g, e.Support.GetValueOrDefault(g) - support.GetValueOrDefault(g))) ? 1 : 0;

            GovForecast revoke = mayor.ForecastRevoke(policy.Id);
            AssertExplained(revoke);
            image = e.Image;
            reserve = mayor.FreeReserve;
            support = new Dictionary<string, double>(e.Support);
            Assert.True(mayor.Revoke(policy.Id));
            revokeInside += Within(revoke, GovForecast.Imagem, null, e.Image - image)
                && Within(revoke, GovForecast.Reserva, null, mayor.FreeReserve - reserve)
                && support.Keys.Union(e.Support.Keys).All(g => Within(revoke, GovForecast.Apoio, g, e.Support.GetValueOrDefault(g) - support.GetValueOrDefault(g))) ? 1 : 0;
            Assert.NotNull(mayor.ForecastRevoke(policy.Id).Blocked);
        }

        Report("Decretar", enactInside, Runs);
        Report("Revogar", revokeInside, Runs);
    }

    private static Dictionary<string, double> RandomAllocation(ExecutiveConfig config, Rng scenario)
    {
        var raw = config.Areas.ToDictionary(a => a.Id, a => a.ReferenceShare * scenario.NextDouble(0.3, 1.8) + (a.Id == ExecutiveConfig.ReserveArea ? scenario.NextDouble(0, 0.05) : 0));
        double sum = raw.Values.Sum();
        return raw.ToDictionary(x => x.Key, x => x.Value / sum);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Orcamento_muda_o_alvo_dos_indicadores_e_a_reserva_como_previsto(bool realIndicators)
    {
        ExecutiveEngine mayor;
        ExecutiveState e;
        Linked? linked = null;
        Func<ExecutiveEngine, Dictionary<string, double>> measure;
        if (realIndicators)
        {
            linked = Linked.Career(3, mayor: true);
            Assert.NotNull(linked.Executive);
            mayor = linked.Executive!;
            e = linked.State.Executive!;
            Assert.True(mayor.UsesRealIndicators);
            measure = m =>
            {
                // O orçamento vira pressão nos indicadores reais na semana; o indicador sem mapa segue o alvo antigo.
                m.ResolveWeek(automatic: true);
                Dictionary<string, double> targets = m.Targets();
                return m.Context.Config.Indicators.ToDictionary(x => x.Id, x => linked!.Evolution.Components(x.Id).Any()
                    ? linked!.Real.Pressoes.FirstOrDefault(p => p.Ate is null && p.Origem == ExecutiveEngine.BudgetOrigin + x.Id)?.Pontos ?? 0
                    : targets[x.Id]);
            };
        }
        else
        {
            GovKit.CityHall city = GovKit.Mayor();
            mayor = city.Executive;
            e = city.E;
            Assert.False(mayor.UsesRealIndicators);
            measure = m => m.Targets();
        }

        int inside = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 9_000);
            Dictionary<string, double> draft = RandomAllocation(mayor.Context.Config, scenario);
            Dictionary<string, double> before = measure(mayor);
            GovForecast forecast = mayor.ForecastAllocation(draft);
            AssertExplained(forecast);
            double reserve = mayor.FreeReserve;
            Assert.Null(mayor.SetAllocation(draft));
            Dictionary<string, double> after = measure(mayor);
            inside += mayor.Context.Config.Indicators.All(x => Within(forecast, GovForecast.IndicadorAlvo, x.Id, after[x.Id] - before[x.Id]))
                && Within(forecast, GovForecast.Reserva, null, mayor.FreeReserve - reserve) ? 1 : 0;
        }

        Assert.NotNull(mayor.ForecastAllocation(new Dictionary<string, double> { [ExecutiveConfig.ReserveArea] = 1 }).Blocked);
        Report(realIndicators ? "Aplicar o orçamento (indicadores reais)" : "Aplicar o orçamento", inside, Runs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resposta_a_crise_muda_indicadores_e_imagem_como_previsto(bool realIndicators)
    {
        ExecutiveEngine mayor;
        ExecutiveState e;
        if (realIndicators)
        {
            Linked linked = Linked.Career(3, mayor: true);
            mayor = linked.Executive!;
            e = linked.State.Executive!;
            Assert.True(mayor.UsesRealIndicators);
        }
        else
        {
            GovKit.CityHall city = GovKit.Mayor();
            mayor = city.Executive;
            e = city.E;
        }

        List<CrisisDefinition> crises = mayor.Context.Content.Crises.Crises;
        int inside = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 13_000);
            CrisisDefinition crisis = crises[i % crises.Count];
            int choice = (i / crises.Count) % crisis.Choices.Count;
            e.Image = scenario.NextDouble(-0.9, 0.9);
            if (!realIndicators)
            {
                foreach (string id in e.Indicators.Keys.ToList())
                {
                    e.Indicators[id] = scenario.NextDouble(5, 95);
                }
            }

            e.Crisis = new PendingCrisis(crisis.Id, mayor.Context.State.Calendar.WeekStart);
            GovForecast forecast = mayor.ForecastCrisis(choice);
            AssertExplained(forecast);
            var before = new Dictionary<string, double>(e.Indicators);
            double image = e.Image;
            mayor.ResolveCrisis(choice);
            inside += e.Indicators.All(x => Within(forecast, GovForecast.Indicador, x.Key, x.Value - before[x.Key])) && Within(forecast, GovForecast.Imagem, null, e.Image - image) ? 1 : 0;
        }

        Assert.NotNull(mayor.ForecastCrisis(0).Blocked);
        Report(realIndicators ? "Responder à crise (indicadores reais)" : "Responder à crise", inside, Runs);
    }

    [Fact]
    public void Atos_do_gabinete_mudam_reconhecimento_imagem_apoio_e_relacao_como_previsto()
    {
        GovKit.CityHall city = GovKit.Mayor();
        ExecutiveEngine mayor = city.Executive;
        ExecutiveState e = city.E;
        List<MandateActionDefinition> actions = mayor.Context.Content.Actions.Actions;
        int inside = 0, tested = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 17_000);
            Scramble(e, scenario);
            e.EnergyLeft = mayor.Context.Config.WeeklyEnergy;
            MandateActionDefinition action = actions[i % actions.Count];
            if (action.Test is { } test)
            {
                mayor.Context.State.Player.Attributes.Set(test.Attribute, scenario.NextInt(0, 101));
                tested++;
            }

            string? target = action.Kind == MandateActionKind.Articular ? e.Chamber[i % e.Chamber.Count].Id
                : action.Targets.Count == 0 ? null : action.Targets[i % action.Targets.Count].Id;
            GovForecast forecast = mayor.ForecastAction(action.Id, target);
            AssertExplained(forecast);
            double recognition = e.Recognition, image = e.Image;
            var support = new Dictionary<string, double>(e.Support);
            var relations = e.Chamber.ToDictionary(m => m.Id, m => m.Relation);
            mayor.Execute(action.Id, target);
            inside += Within(forecast, GovForecast.Reconhecimento, null, e.Recognition - recognition)
                && Within(forecast, GovForecast.Imagem, null, e.Image - image)
                && support.Keys.Union(e.Support.Keys).All(g => Within(forecast, GovForecast.Apoio, g, e.Support.GetValueOrDefault(g) - support.GetValueOrDefault(g)))
                && e.Chamber.All(m => Within(forecast, GovForecast.Relacao, m.Id, m.Relation - relations[m.Id])) ? 1 : 0;
        }

        Assert.True(tested > 0);
        e.EnergyLeft = 0;
        Assert.NotNull(mayor.ForecastAction(actions[0].Id, actions[0].Targets.FirstOrDefault()?.Id).Blocked);
        Report("Atos do gabinete", inside, Runs);
    }

    [Fact]
    public void Demitir_assessor_tem_previsao_com_quem_vai_lembrar_e_a_relacao_cai_como_previsto()
    {
        GovKit.Council council = GovKit.Councillor();
        GovernanceEngine gov = council.Gov;
        GovernanceState g = council.G;
        List<Aide> pool = g.Aides.ToList();
        Assert.NotEmpty(pool);
        int inside = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 21_000);
            Aide aide = pool[i % pool.Count];
            foreach (Aide other in g.Aides.Where(a => a.Appointed))
            {
                other.Appointed = false;
            }

            bool atBase = scenario.NextDouble() < 0.5;
            aide.Left = false;
            aide.Appointed = true;
            aide.Role = atBase ? AideRole.Base : AideRole.Chefe;
            aide.ZoneId = atBase ? g.Zones[i % g.Zones.Count] : null;
            GovForecast forecast = gov.ForecastDismissAide(aide.PersonId);
            AssertExplained(forecast);
            Assert.Contains(gov.NameOf(aide.PersonId), forecast.Remembered);
            double own = gov.RelationOf(aide.PersonId);
            double? patron = aide.NomineeOf is { } p ? gov.RelationOf(p) : null;
            Assert.True(gov.DismissAide(aide.PersonId).Ok);
            inside += Within(forecast, GovForecast.Relacao, aide.PersonId, gov.RelationOf(aide.PersonId) - own)
                && (patron is null || Within(forecast, GovForecast.Relacao, aide.NomineeOf, gov.RelationOf(aide.NomineeOf!) - patron.Value)) ? 1 : 0;
            Assert.NotNull(gov.ForecastDismissAide(aide.PersonId).Blocked);
        }

        Report("Demitir assessor", inside, Runs);
    }

    [Fact]
    public void Repasse_a_Camara_muda_relacao_e_contas_como_previsto()
    {
        GovKit.CityHall city = GovKit.Mayor();
        GovernanceEngine gov = city.Gov;
        GovernanceState g = city.G;
        ExecutiveState e = city.E;
        int inside = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 25_000);
            Scramble(e, scenario);
            e.Indicators["contas"] = scenario.NextDouble(5, 95);
            double cap = GovernanceQueries.Fiscal(gov).ChamberCap;
            g.Fiscal.ChamberShare = scenario.NextDouble(0.002, cap - 0.002);
            double target = g.Fiscal.ChamberShare + (i % 2 == 0 ? -0.0025 : 0.0025);
            target = Math.Clamp(target, 0, cap);
            GovForecast forecast = gov.ForecastChamberTransfer(target);
            AssertExplained(forecast);
            var relations = e.Chamber.ToDictionary(m => m.Id, m => m.Relation);
            double accounts = e.Indicators["contas"];
            Assert.True(gov.SetChamberTransfer(target).Ok);
            inside += e.Chamber.All(m => forecast.Line(GovForecast.Relacao, m.Id) is not null
                    ? Within(forecast, GovForecast.Relacao, m.Id, m.Relation - relations[m.Id])
                    : Within(forecast, GovForecast.RelacaoCamara, null, m.Relation - relations[m.Id]))
                && Within(forecast, GovForecast.Indicador, "contas", e.Indicators["contas"] - accounts) ? 1 : 0;
        }

        Assert.NotNull(gov.ForecastChamberTransfer(1).Blocked);
        Report("Repasse à Câmara", inside, Runs);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Entregar_sem_cerimonia_rende_o_previsto(bool mayorRole)
    {
        GovernanceEngine gov;
        GovernanceState g;
        GameState state;
        if (mayorRole)
        {
            GovKit.CityHall city = GovKit.Mayor();
            (gov, g, state) = (city.Gov, city.G, city.State);
        }
        else
        {
            GovKit.Council council = GovKit.Councillor();
            (gov, g, state) = (council.Gov, council.G, council.State);
        }

        if (!mayorRole && !g.Works.Any(w => w.SponsorId == RaceSetup.PlayerId))
        {
            // Obra do próprio vereador (emenda): é a que ele pode entregar.
            state.Player.Resources.PoliticalCapital = 1_000;
            g.TurnsLeft = 10;
            Assert.Contains(CampaignKit.C.Governance.Works.Tipos.SelectMany(t => g.Zones.Select(z => (t, z))), x => gov.Emenda(x.t.Id, x.z).Ok);
        }

        if (mayorRole)
        {
            // Obras da prefeitura de tipos diferentes (com e sem folha, com e sem indicador), cada uma na primeira zona que cabe.
            foreach (WorkType type in CampaignKit.C.Governance.Works.Tipos)
            {
                if (g.Zones.FirstOrDefault(z => gov.ForecastStartWork(type.Id, z).Blocked is null) is { } zone)
                {
                    Assert.True(gov.StartWork(type.Id, zone).Ok);
                }
            }
        }

        List<Work> works = g.Works.Where(w => mayorRole || w.SponsorId == RaceSetup.PlayerId).ToList();
        Assert.NotEmpty(works);
        int inside = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 29_000);
            Work work = works[i % works.Count];
            work.Stage = WorkStage.Pronta;
            int zone = g.IndexOf(work.ZoneId);
            g.ZoneApproval[zone] = scenario.NextDouble(-40, 40);
            g.Presence[zone] = scenario.NextDouble(0, 90);
            g.Payroll.Share = scenario.NextDouble(0.35, 0.56);
            if (state.Executive is { } e)
            {
                foreach (string id in e.Indicators.Keys.ToList())
                {
                    e.Indicators[id] = scenario.NextDouble(5, 95);
                }
            }

            GovForecast forecast = gov.ForecastDeliverQuietly(work.Id);
            AssertExplained(forecast);
            double approval = g.ZoneApproval[zone], presence = g.Presence[zone], payroll = g.Payroll.Share;
            var indicators = state.Executive is { } ex ? new Dictionary<string, double>(ex.Indicators) : [];
            Assert.True(gov.DeliverQuietly(work.Id).Ok);
            bool ok = Within(forecast, GovForecast.Folha, null, g.Payroll.Share - payroll)
                && (mayorRole ? Within(forecast, GovForecast.AprovacaoZona, work.ZoneId, g.ZoneApproval[zone] - approval) : Within(forecast, GovForecast.Presenca, work.ZoneId, g.Presence[zone] - presence))
                && indicators.All(x => Within(forecast, GovForecast.Indicador, x.Key, state.Executive!.Indicators[x.Key] - x.Value));
            inside += ok ? 1 : 0;
            Assert.NotNull(gov.ForecastDeliverQuietly(work.Id).Blocked);
        }

        Report(mayorRole ? "Entregar sem cerimônia (prefeito)" : "Entregar sem cerimônia (vereador)", inside, Runs);
    }

    [Fact]
    public void Recusar_convenio_nao_muda_numeros_e_explica_o_que_fica_livre()
    {
        GovKit.CityHall city = GovKit.Mayor();
        GovernanceEngine gov = city.Gov;
        GovernanceState g = city.G;
        WorkType type = CampaignKit.C.Governance.Works.Tipos[0];
        DateOnly week = city.State.Calendar.WeekStart;
        int inside = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 33_000);
            var offer = new Agreement { Id = $"conv_previsao_{i}", OriginId = i % 2 == 0 ? "estado" : "uniao", TypeId = type.Id, Value = (decimal)scenario.NextDouble(1_000, 900_000), Counterpart = scenario.NextDouble(0.05, 0.4), Offered = week, OfferExpires = week.AddDays(280), Weeks = 90 };
            g.Agreements.Add(offer);
            GovForecast forecast = gov.ForecastDeclineAgreement(offer.Id);
            AssertExplained(forecast);
            decimal investment = GovernanceQueries.Fiscal(gov).InvestmentLeft;
            double governor = g.GovernorRelation;
            Assert.True(gov.DeclineAgreement(offer.Id).Ok);
            inside += Within(forecast, GovForecast.Investimento, null, (double)(GovernanceQueries.Fiscal(gov).InvestmentLeft - investment)) && Within(forecast, GovForecast.Relacao, null, g.GovernorRelation - governor) ? 1 : 0;
            Assert.NotNull(gov.ForecastDeclineAgreement(offer.Id).Blocked);
        }

        Report("Recusar convênio", inside, Runs);
    }

    [Fact]
    public void Voto_do_vereador_muda_fidelidade_apoio_imagem_e_relacoes_como_previsto()
    {
        GovKit.Council council = GovKit.Councillor();
        LegislatureEngine engine = council.Legislature;
        MandateState mandate = council.Mandate;
        Assert.NotNull(mandate.Player.PartyId);
        List<BillTemplate> templates = CampaignKit.C.Legislature.MunicipalBills.Bills;
        List<ChamberMember> colleagues = mandate.Members.Where(m => !m.IsPlayer).ToList();
        // A sessão de um projeto só, como a semana faria (o resto da semana não entra na conta do voto).
        System.Reflection.MethodInfo vote = typeof(LegislatureEngine).GetMethod("Vote", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        DateOnly week = council.State.Calendar.WeekStart;
        var choices = new[] { VoteChoice.Sim, VoteChoice.Nao, VoteChoice.Abstencao };
        int inside = 0;
        for (int i = 0; i < Runs; i++)
        {
            var scenario = new Rng((ulong)i + 37_000);
            BillTemplate template = templates[i % templates.Count];
            ChamberMember author = colleagues[i % colleagues.Count];
            var bill = new Bill
            {
                Id = $"previsao_{i}", TemplateId = template.Id, Title = template.Title, AuthorId = template.Author == BillAuthorKind.Executivo ? Bill.MayorAuthor : author.Id,
                AuthorKind = template.Author, Filed = week, Stage = BillStage.Plenario, CommitteeUntil = week,
            };
            mandate.Bills.Add(bill);
            mandate.Fidelity = scenario.NextInt(60, 101);
            mandate.MayorRelation = scenario.NextDouble(-95, 95);
            mandate.Image = scenario.NextDouble(-0.9, 0.9);
            foreach (string group in mandate.Support.Keys.ToList())
            {
                mandate.Support[group] = scenario.NextDouble(0, 1);
            }

            author.Relation = scenario.NextDouble(-98, 98);
            VoteChoice choice = choices[i % choices.Length];
            GovForecast forecast = engine.ForecastVote(bill.Id, choice);
            AssertExplained(forecast);
            int fidelity = mandate.Fidelity, capital = council.State.Player.Resources.PoliticalCapital;
            double mayor = mandate.MayorRelation, image = mandate.Image, relation = author.Relation;
            var support = new Dictionary<string, double>(mandate.Support);
            engine.CastVote(bill.Id, choice);
            vote.Invoke(engine, [bill, false]);
            bool ok = Within(forecast, GovForecast.Fidelidade, null, mandate.Fidelity - fidelity)
                && Within(forecast, GovForecast.Capital, null, council.State.Player.Resources.PoliticalCapital - capital)
                && Within(forecast, GovForecast.RelacaoPrefeito, null, mandate.MayorRelation - mayor)
                && Within(forecast, GovForecast.Imagem, null, mandate.Image - image)
                && support.Keys.Union(mandate.Support.Keys).All(g => Within(forecast, GovForecast.Apoio, g, mandate.Support.GetValueOrDefault(g) - support.GetValueOrDefault(g)))
                && (bill.AuthorKind == BillAuthorKind.Executivo || Within(forecast, GovForecast.Relacao, author.Id, author.Relation - relation));
            inside += ok ? 1 : 0;
            mandate.Bills.Remove(bill);
        }

        Report("Votar na pauta", inside, Runs);
    }

    [Fact]
    public void Instrucao_de_voto_explica_o_que_muda_e_nao_mexe_nos_numeros()
    {
        GovKit.Council council = GovKit.Councillor();
        GovernanceEngine gov = council.Gov;
        MandateState mandate = council.Mandate;
        var instructions = Enum.GetValues<VoteInstruction>();
        int inside = 0;
        for (int i = 0; i < Runs; i++)
        {
            VoteInstruction instruction = instructions[i % instructions.Length];
            GovForecast forecast = gov.ForecastInstruction(instruction);
            AssertExplained(forecast);
            int fidelity = mandate.Fidelity, capital = council.State.Player.Resources.PoliticalCapital;
            double image = mandate.Image;
            Assert.True(gov.SetInstruction(instruction).Ok);
            inside += Within(forecast, GovForecast.Fidelidade, null, mandate.Fidelity - fidelity) && Within(forecast, GovForecast.Capital, null, council.State.Player.Resources.PoliticalCapital - capital)
                && Within(forecast, GovForecast.Imagem, null, mandate.Image - image) && council.G.Instruction == instruction ? 1 : 0;
        }

        Report("Instrução de voto", inside, Runs);
    }
}
