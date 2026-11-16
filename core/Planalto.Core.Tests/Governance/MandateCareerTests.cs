using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Xunit;

namespace Planalto.Core.Tests.Governance;

/// <summary>
/// Testes de carreira da R4 (ROADMAP): campanha e mandato em 3 sementes, mandato de 4 anos sem comando e save no meio
/// do mandato.
/// </summary>
public class MandateCareerTests
{
    /// <summary>Um mandatário automático: usa os instrumentos de maior presença prevista, nomeia o gabinete e responde ao que aparece.</summary>
    internal static void PlayWeek(GovKit.Council c, Rng policy)
    {
        GovernanceState g = c.G;
        if (c.State.Player.PartyId is null)
        {
            PartyAffiliation.Join(c.State, policy.Pick(c.State.Parties).Id);
        }

        foreach (Aide candidate in g.Aides.Where(a => !a.Appointed && !a.Left).OrderByDescending(a => a.Origin == Aide.DaCampanha || a.Origin == Aide.DeLideranca).ThenByDescending(a => a.Quality).ToList())
        {
            string role = g.AideIn(AideRole.Comunicacao) is null ? AideRole.Comunicacao : AideRole.Base;
            string? zone = role == AideRole.Base ? g.Zones.FirstOrDefault(z => !g.Cabinet.Any(a => a.ZoneId == z)) : null;
            c.Gov.AppointAide(candidate.PersonId, role, zone);
        }

        if (g.Decision is not null)
        {
            TermEventDefinition def = c.Gov.DecisionDefinition!;
            TermEventOption option = policy.Pick(def.Opcoes);
            if (!c.Gov.Decide(option.Id).Ok)
            {
                Assert.True(c.Gov.Decide(def.Opcoes.First(o => o.Padrao).Id).Ok);
            }
        }

        foreach (Inspection inspection in g.Inspections.Where(i => i.Pending).ToList())
        {
            List<string> allowed = FindingOption.Ids.Where(id => c.Gov.ForecastFinding(inspection.Id, id).Blocked is null).ToList();
            Assert.True(c.Gov.DecideFinding(inspection.Id, policy.Pick(allowed)).Ok);
        }

        foreach (WorkView ready in GovernanceQueries.Works(c.Gov).Where(w => w.CanInaugurate))
        {
            Assert.True(c.Gov.Inaugurate(ready.Work.Id).Ok);
        }

        while (g.TurnsLeft > 0)
        {
            IReadOnlyList<InstrumentOption> options = c.Gov.CouncilOptions();
            if (options.Count == 0)
            {
                break;
            }

            // Metade das vezes o de maior ganho previsto; na outra, qualquer um (para passar por todos os instrumentos).
            InstrumentOption pick = policy.Chance(0.5) ? options.MaxBy(Value)! : policy.Pick(options);
            GovForecast forecast = pick.Forecast;
            double before = pick.ZoneId is null ? 0 : g.Presence[g.IndexOf(pick.ZoneId)];
            GovResult result = c.Gov.Use(pick);
            Assert.True(result.Ok, $"{pick.InstrumentId} ({pick.Label}): {result.Failure}");
            if (pick.ZoneId is not null && forecast.Line(GovForecast.Presenca, pick.ZoneId) is not null)
            {
                // Princípio f: o resultado cai dentro da faixa prevista.
                GovKit.AssertWithin(forecast, GovForecast.Presenca, pick.ZoneId, g.Presence[g.IndexOf(pick.ZoneId)] - before);
            }
        }

        foreach (Bill bill in c.Legislature.Pauta.ToList())
        {
            if (policy.Chance(0.7))
            {
                c.Legislature.CastVote(bill.Id, policy.Chance(0.6) ? VoteChoice.Sim : VoteChoice.Nao);
            }
        }
    }

    private static double Value(InstrumentOption option) =>
        option.Forecast.Lines.Where(l => l.Kind is GovForecast.Presenca or GovForecast.PresencaDepois).Sum(l => l.Expected)
        + (4 * option.Forecast.Lines.Where(l => l.Kind == GovForecast.PromessaCumprida).Sum(l => l.Expected));

    private static void AssertGovernance(GovKit.Council c)
    {
        GovernanceState g = c.G;
        Assert.All(g.Presence, p => Assert.InRange(p, 0, 100));
        Assert.InRange(g.MayorApproval, 0, 100);
        Assert.InRange(g.Payroll.Share, 0.2, 0.9);
        Assert.InRange(g.TurnsLeft, 0, CampaignKit.C.Governance.Instruments.Regras.TurnosPorSemana);
        Assert.True(g.Cabinet.Count() <= c.Gov.CabinetSlots);
        Assert.Equal(g.Works.Count, g.Works.Select(w => w.Id).Distinct().Count());
        Assert.All(g.Works.Where(w => w.Stage == WorkStage.Entregue), w => Assert.NotNull(w.Delivered));
        Assert.All(c.Mandate.Members, m => Assert.InRange(m.Relation, -100, 100));
        Assert.True(c.State.Player.Resources.PoliticalCapital >= 0);
    }

    /// <summary>
    /// Carreira (campanha + mandato) em 3 sementes: 70% ou mais das promessas geram cobrança; nenhuma inauguração sem
    /// obra entregue; nenhuma greve sem negociação anterior.
    /// </summary>
    [Theory]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void Carreira_com_mandato_cobra_as_promessas_e_respeita_obra_e_negociacao(ulong seed)
    {
        GovKit.Council c = GovKit.Councillor(seed);
        GovernanceState g = c.G;
        var policy = new Rng(seed * 977);
        int promisesAtStart = g.PromisesAtStart.Count;
        Assert.True(promisesAtStart > 0, "A campanha tem de chegar ao mandato com promessas abertas no Livro.");
        int weeks = 0;
        while (c.State.Mandate is not null && !CareerFlow.ShouldStartCampaign(c.State))
        {
            Assert.True(++weeks < 260, "O mandato não chegou ao ano da eleição.");
            PlayWeek(c, policy);
            // Nenhuma obra aparece inaugurada sem ter passado por pronta e entregue.
            foreach (Work work in g.Works.Where(w => w.Ceremony))
            {
                Assert.Equal(WorkStage.Entregue, work.Stage);
                Assert.NotNull(work.Delivered);
            }

            c.Week();
            AssertGovernance(c);
        }

        // 70% ou mais das promessas que chegaram ao mandato foram cobradas por quem lembra.
        var (promises, collected) = c.Gov.CollectionStats();
        Assert.Equal(promisesAtStart, promises);
        Assert.True(collected >= 0.7 * promises, $"Só {collected} de {promises} promessas geraram cobrança.");
        Assert.True(g.Demands.Count(d => d.Kind == Demand.Promessa) >= collected);
        // Quem cobrou é gente do save: toda cobrança com pessoa aponta para alguém com ficha.
        Assert.All(g.Demands.Where(d => d.PersonId is not null), d => Assert.NotNull(c.State.City!.Person(d.PersonId!)));
        // Nenhuma promessa do começo ficou esquecida aberta além do prazo.
        Assert.DoesNotContain(c.State.Ledger.Promises, p => p.State == PromiseState.Aberta && c.Gov.DeadlineOf(p) < c.State.Calendar.WeekStart.AddDays(-14));
        Assert.Contains(c.State.Ledger.Promises, p => g.PromisesAtStart.Contains(p.Id) && p.State is PromiseState.Cumprida or PromiseState.Vencida);

        // Nenhuma inauguração sem obra entregue: toda notícia de inauguração tem a obra entregue na zona, na mesma semana.
        List<Work> ceremonies = g.Works.Where(w => w.Ceremony).ToList();
        Assert.All(ceremonies, w => Assert.True(w.Stage == WorkStage.Entregue && w.Delivered is not null && w.CompanyId is not null));
        foreach (FeedItem news in g.Feed.Where(f => f.Kind == "obra" && (f.Text.Contains("inaugurada") || f.Text.Contains("inaugurou"))))
        {
            Assert.Contains(g.Works, w => w.Stage == WorkStage.Entregue && w.ZoneId == news.ZoneId && w.Delivered == news.Date);
        }

        // Nenhuma greve sem negociação anterior: toda greve aponta para a mesa que falhou antes dela.
        foreach (CrisisRecord strike in g.Crises.Where(x => x.CrisisId.StartsWith("greve", StringComparison.Ordinal)))
        {
            Assert.Equal(CrisisRecord.CausaNegociacao, strike.CauseKind);
            Negotiation negotiation = g.Negotiations.Single(n => n.Id == strike.CauseRef);
            Assert.Equal(NegotiationOutcome.Fracassou, negotiation.Outcome);
            Assert.True(negotiation.Date <= strike.Date);
            Assert.Equal(strike.Id, negotiation.StrikeRecordId);
        }

        Assert.All(g.Negotiations.Where(n => n.Outcome == NegotiationOutcome.Acordo), n => Assert.Null(n.StrikeRecordId));
        Assert.All(g.Crises, x => Assert.False(string.IsNullOrWhiteSpace(x.CauseText)));

        // A carreira segue: campanha de reeleição com a presença do mandato repartida por zona.
        Assert.True(weeks > 150);
        CampaignEngine campaign = CampaignEngine.Start(CampaignKit.Context(c.State));
        GovernanceEngine.CarryToCampaign(c.State);
        Assert.Equal(g.Presence, c.State.Campaign!.Territory.Presence[RaceSetup.PlayerId].ToList());
        Assert.Equal(CareerMode.CampanhaEmMandato, CareerFlow.Mode(c.State));
        Assert.NotNull(campaign);
    }

    /// <summary>Mandato de 4 anos sem comando: obras do prefeito NPC andam, pelo menos 1 crise com causa, notícias semanais.</summary>
    [Theory]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    public void Mandato_de_quatro_anos_sem_comando_o_mundo_anda(ulong seed)
    {
        GovKit.Council c = GovKit.Councillor(seed);
        GovernanceState g = c.G;
        int weeks = 0;
        var weeksWithNews = new HashSet<DateOnly>();
        var weeksPlayed = new List<DateOnly>();
        while (c.State.Mandate is not null && weeks < 208)
        {
            weeks++;
            weeksPlayed.Add(c.State.Calendar.WeekStart);
            c.Week();
            AssertGovernance(c);
        }

        foreach (FeedItem item in g.Feed)
        {
            weeksWithNews.Add(item.Date);
        }

        // Obras do prefeito NPC andam: há entregues, com todas as etapas cumpridas, e outras em andamento.
        List<Work> delivered = g.Works.Where(w => w.SponsorId == Work.Prefeito && w.Stage == WorkStage.Entregue).ToList();
        Assert.True(delivered.Count >= 3, $"Só {delivered.Count} obra(s) do prefeito entregue(s) em 4 anos.");
        Assert.All(delivered.Where(w => !CampaignKit.C.Governance.Works.Type(w.TypeId).Servico), w => Assert.True(w.CompanyId is not null && w.Mode is not null));
        Assert.Contains(g.Works, w => w.SponsorId == Work.Prefeito && w.Created > g.Start.AddDays(365));

        // Pelo menos uma crise, e toda crise com a causa registrada no estado.
        Assert.NotEmpty(g.Crises);
        Assert.All(g.Crises, x =>
        {
            Assert.False(string.IsNullOrWhiteSpace(x.CauseText));
            Assert.True(x.Resolved, "A prefeitura NPC responde às próprias crises.");
            switch (x.CauseKind)
            {
                case CrisisRecord.CausaNegociacao:
                    Assert.Contains(g.Negotiations, n => n.Id == x.CauseRef && n.Outcome == NegotiationOutcome.Fracassou && n.Date <= x.Date);
                    break;
                case CrisisRecord.CausaZonaSemDrenagem:
                    Assert.Equal(x.ZoneId, x.CauseRef);
                    Assert.DoesNotContain(g.Works, w => w.ZoneId == x.ZoneId && w.Delivered < x.Date && CampaignKit.C.Governance.Works.Type(w.TypeId).ProtegeDeEnchente);
                    break;
                case CrisisRecord.CausaSecretario:
                    Assert.Contains(g.Secretaries, s => s.PersonId == x.CauseRef && s.Integrity <= CampaignKit.C.Governance.Secretaries.Regras.IntegridadeDeRisco);
                    break;
                case CrisisRecord.CausaReceita:
                    // A receita caiu naquele ano: o ano fica registrado como a causa.
                    Assert.Equal(x.Date.Year.ToString(System.Globalization.CultureInfo.InvariantCulture), x.CauseRef);
                    Assert.Contains(g.Feed, f => f.Kind == "folha" && f.Date.Year == x.Date.Year && f.Text.Contains("a receita variou -"));
                    break;
                default:
                    Assert.Fail($"Causa inesperada sem o jogador agir: {x.CauseKind}.");
                    break;
            }
        });

        // Notícias toda semana.
        Assert.All(weeksPlayed, w => Assert.Contains(w, weeksWithNews));

        // Sem comando: a Mesa foi eleita duas vezes, as mesas de negociação abriram todo ano e a presença caiu.
        Assert.Equal(2, g.Feed.Count(f => f.Kind == "mesa"));
        Assert.True(g.Negotiations.Count >= 6);
        Assert.True(g.CityPresence() < g.PresenceAtStart.Select((p, i) => p * g.ZonePopulation[i]).Sum() / g.ZonePopulation.Sum(x => (double)x));
        Assert.DoesNotContain(g.Works, w => w.SponsorId == RaceSetup.PlayerId);
        // As promessas da campanha foram cobradas e, sem resposta, venceram.
        if (g.PromisesAtStart.Count > 0)
        {
            Assert.Contains(c.State.Ledger.Promises, p => g.PromisesAtStart.Contains(p.Id) && p.State == PromiseState.Vencida);
        }
    }

    /// <summary>Save e load no meio do mandato terminando igual.</summary>
    [Theory]
    [InlineData(3UL, 60)]
    [InlineData(4UL, 110)]
    public void Save_e_load_no_meio_do_mandato_terminam_iguais(ulong seed, int saveAt)
    {
        GovKit.Council original = GovKit.Councillor(seed);
        var policy = new Rng(seed * 31);
        for (int week = 0; week < saveAt; week++)
        {
            PlayWeek(original, policy);
            original.Week();
        }

        // O estado salvo carrega igual; e as duas linhas, jogadas com a mesma política, terminam iguais.
        string json = GameStateSerializer.Serialize(original.State);
        GovKit.Council loaded = GovKit.Load(json);
        Assert.Equal(json, GameStateSerializer.Serialize(loaded.State));
        Assert.NotNull(loaded.State.Governance);
        Assert.Equal(original.G.Works.Count, loaded.G.Works.Count);

        var policyA = new Rng(seed * 53);
        var policyB = new Rng(seed * 53);
        for (int week = 0; week < 70; week++)
        {
            PlayWeek(original, policyA);
            original.Week();
            PlayWeek(loaded, policyB);
            loaded.Week();
        }

        Assert.Equal(CampaignKit.Canonical(original.State), CampaignKit.Canonical(loaded.State));
    }

    [Fact]
    public void Save_e_load_no_meio_da_prefeitura_terminam_iguais()
    {
        GovKit.CityHall original = GovKit.Mayor();
        string zone = original.G.Zones[0];
        WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => !t.Servico && original.Gov.Fits(t, zone));
        Assert.True(original.Gov.StartWork(type.Id, zone).Ok);
        original.Weeks(45);
        string json = GameStateSerializer.Serialize(original.State);
        GovKit.CityHall loaded = GovKit.LoadMayor(json);
        Assert.Equal(json, GameStateSerializer.Serialize(loaded.State));
        original.Weeks(60);
        loaded.Weeks(60);
        Assert.Equal(CampaignKit.Canonical(original.State), CampaignKit.Canonical(loaded.State));
    }

    /// <summary>Promessa de obra numa zona se cumpre com emenda ou obra entregue ali, e só ali (ROADMAP R4, cobrança do Livro).</summary>
    [Fact]
    public void Promessa_de_obra_numa_zona_se_cumpre_com_a_obra_entregue_ali()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        PromiseDefinition definition = CampaignKit.C.Campaign.Promises.Promessas.First(p => p.Tipo == PromiseDefinition.TipoObra && p.Alvos.Contains(Promise.ParaZona));
        WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => t.Cumpre.Contains(definition.Obra) && t.Emenda);
        string zone = g.Zones.First(z => c.Gov.Fits(type, z));
        string other = g.Zones.First(z => z != zone);
        DateOnly week = c.State.Calendar.WeekStart;
        Promise promise = c.State.Ledger.AddPromise(Promise.ParaZona, zone, definition.Id, "Promessa de obra na zona", zone, week, week.AddDays(7 * 104));
        Promise elsewhere = c.State.Ledger.AddPromise(Promise.ParaZona, other, definition.Id, "Promessa de obra em outra zona", other, week, week.AddDays(7 * 104));

        // A previsão da emenda avisa que cumpre a promessa, e de quem.
        GovForecast forecast = c.Gov.ForecastEmenda(type.Id, zone);
        Assert.NotNull(forecast.Line(GovForecast.PromessaCumprida, zone));
        Assert.Contains(forecast.Explanation, x => x.Contains(promise.Text));
        Work work = g.Work(c.Gov.Emenda(type.Id, zone).CreatedId!)!;
        Assert.Equal(PromiseState.Aberta, promise.State);

        // A cobrança chega antes da entrega.
        c.Weeks(8);
        Assert.True(promise.Cobrancas >= 1);
        Demand demand = g.Demands.First(d => d.Kind == Demand.Promessa && d.RefId == promise.Id);
        Assert.Equal(zone, demand.ZoneId);
        Assert.Contains(GovernanceQueries.Promises(c.Gov), v => v.Promise.Id == promise.Id && v.HowToFulfil.Contains(type.Nome));

        // Entregue a obra na zona, a promessa fica cumprida; a da outra zona, não.
        work.Stage = WorkStage.Pronta;
        GovResult opened = c.Gov.Inaugurate(work.Id);
        Assert.True(opened.Ok);
        Assert.Equal(PromiseState.Cumprida, promise.State);
        Assert.Equal(PromiseState.Aberta, elsewhere.State);
        Assert.Contains(opened.Changes, x => x.Contains("Promessa cumprida"));
        Assert.Contains(g.Feed, f => f.Kind == "cobranca" && f.Text.Contains("Promessa cumprida"));

        // Sem obra, a promessa vence: quem cobrou lembra e a zona responde.
        c.Weeks(110);
        Assert.Equal(PromiseState.Vencida, elsewhere.State);
        Assert.True(elsewhere.Cobrancas >= 2);
        Assert.Contains(g.Feed, f => f.Kind == "cobranca" && f.Text.Contains("Promessa vencida") && f.ZoneId == other);
    }

    [Fact]
    public void Divida_de_campanha_e_cobrada_e_se_quita_com_obra_na_zona_de_quem_ajudou()
    {
        GovKit.Council c = GovKit.Councillor();
        GovernanceState g = c.G;
        Leader leader = c.State.City!.Leaders.First();
        DateOnly week = c.State.Calendar.WeekStart;
        var debt = c.State.Ledger.AddDebt(leader.PersonId, "Apoio na campanha", week);
        c.Weeks(CampaignKit.C.Governance.Collection.Regras.DividaIntervaloSemanas + 1);
        Assert.Contains(g.Demands, d => d.Kind == Demand.Divida && d.RefId == debt.Id && d.PersonId == leader.PersonId);
        Assert.False(debt.Settled);

        string zone = leader.Zones[0];
        WorkType type = CampaignKit.C.Governance.Works.Tipos.First(t => t.Emenda && c.Gov.Fits(t, zone));
        c.Mandate.EmendasLeft = 1;
        Work work = g.Work(c.Gov.Emenda(type.Id, zone).CreatedId!)!;
        work.Stage = WorkStage.Pronta;
        Assert.True(c.Gov.Inaugurate(work.Id).Ok);
        Assert.True(debt.Settled);
        Assert.Contains(c.State.City.Person(leader.PersonId)!.Memories, m => m.Fact == "divida_quitada" && m.Pontos > 0);
    }
}
