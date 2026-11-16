using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Elections;
using Planalto.Core.Executive;
using Planalto.Core.Legislature;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Xunit;

namespace Planalto.Core.Tests.Executive;

public class ExecutiveTests
{
    private static int SmallTown() =>
        CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == "pequeno").OrderBy(m => m.CodigoIbge).Skip(7).First().CodigoIbge;

    /// <summary>Carreira eleita prefeito numa cidade pequena, já empossada.</summary>
    internal static (GameState State, ExecutiveEngine Engine) ElectedMayor(ulong firstSeed = 3)
    {
        for (ulong seed = firstSeed; seed < firstSeed + 30; seed++)
        {
            CampaignEngine campaign = CampaignKit.NewCareer(SmallTown(), seed, strongestParty: true);
            GameState state = campaign.Context.State;
            state.Player.Attributes.Set(Planalto.Core.Characters.AttributeId.Carisma, 70);
            if (CareerFlow.SwitchCandidacy(campaign.Context, Office.Prefeito).Issue is not null)
            {
                continue;
            }

            CampaignAutoplayTests.Play(campaign, seed);
            if (!state.Campaign!.Outcome!.PlayerElected)
            {
                continue;
            }

            CareerFlow.ConcludeElection(campaign.Context, CampaignKit.LegislatureContext(state), CampaignKit.ExecutiveContext(state));
            return (state, new ExecutiveEngine(CampaignKit.ExecutiveContext(state)));
        }

        throw new InvalidOperationException("Nenhuma semente elegeu o jogador prefeito.");
    }

    [Fact]
    public void Candidatura_a_prefeito_troca_de_lista_e_respeita_a_idade_minima()
    {
        CampaignEngine campaign = CampaignKit.NewCareer(SmallTown(), 4, strongestParty: true);
        GameState state = campaign.Context.State;
        int mayorsBefore = state.Campaign!.ForOffice(Office.Prefeito).Count();
        bool ownPartyHadMayor = state.Campaign.ForOffice(Office.Prefeito).Any(c => c.PartyId == state.Player.PartyId);

        var (issue, renounced) = CareerFlow.SwitchCandidacy(campaign.Context, Office.Prefeito);
        Assert.Null(issue);
        Assert.False(renounced);
        Candidate player = state.Campaign.Player;
        Assert.Equal(Office.Prefeito, player.Office);
        Assert.Equal(state.Party(player.PartyId)!.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), player.Number);
        Assert.Equal(ownPartyHadMayor ? mayorsBefore : mayorsBefore + 1, state.Campaign.ForOffice(Office.Prefeito).Count());
        Assert.Single(state.Campaign.ForOffice(Office.Prefeito), c => c.PartyId == player.PartyId);
        Assert.Equal(state.Campaign.Candidates.Count, state.Campaign.Candidates.Select(c => c.Id).Distinct().Count());
        Assert.Equal(state.Campaign.ForOffice(Office.Vereador).Count(), state.Campaign.ForOffice(Office.Vereador).Select(c => c.Number).Distinct().Count());

        // Volta para vereador; e quem tem menos de 21 anos na posse não pode disputar a prefeitura.
        Assert.Null(CareerFlow.SwitchCandidacy(campaign.Context, Office.Vereador).Issue);
        Assert.Equal(Office.Vereador, state.Campaign.Player.Office);
        var young = new DateOnly(state.Campaign.ElectionDate.Year - 19, 6, 1);
        var copy = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        typeof(Planalto.Core.Characters.Character).GetProperty(nameof(Planalto.Core.Characters.Character.BirthDate))!.SetValue(copy.Player, young);
        Assert.Equal(CandidacyIssue.IdadeMinima, RaceSetup.CanRunFor(CampaignKit.Context(copy), Office.Prefeito));
    }

    [Fact]
    public void Posse_de_prefeito_monta_orcamento_camara_e_coalizao()
    {
        var (state, engine) = ElectedMayor();
        ExecutiveState e = state.Executive!;
        Assert.Null(state.Mandate);
        Assert.Equal(CareerMode.Prefeitura, CareerFlow.Mode(state));
        Assert.Equal(1, e.Allocation.Values.Sum(), 6);
        Assert.Equal(CampaignKit.Municipio(e.Municipio).VagasVereador, e.Chamber.Count);
        Assert.DoesNotContain(e.Chamber, m => m.IsPlayer);
        Assert.Equal(CampaignKit.C.Executive.Config.Secretarias, e.Secretarias.Values.Sum());
        Assert.Contains(state.Player.PartyId!, e.BaseParties);
        Assert.Equal(RaceSetup.PlayerId, state.Local!.MayorId);
        Assert.Empty(engine.BelowMinimum);
        Assert.InRange(engine.ComputeApproval(), 0, 100);
    }

    [Fact]
    public void Gastar_mais_numa_area_sobe_o_alvo_e_descumprir_o_minimo_rejeita_as_contas()
    {
        var (state, engine) = ElectedMayor();
        ExecutiveState e = state.Executive!;
        double before = engine.Targets()["saude"];
        var allocation = new Dictionary<string, double>(e.Allocation) { ["saude"] = e.Allocation["saude"] + 0.05, ["custeio"] = e.Allocation["custeio"] - 0.05 };
        Assert.Null(engine.SetAllocation(allocation));
        Assert.True(engine.Targets()["saude"] > before);

        Assert.Equal(ExecutiveFailure.OrcamentoInvalido, engine.SetAllocation(new Dictionary<string, double>(e.Allocation) { ["saude"] = 0.9 }));

        allocation = new Dictionary<string, double>(e.Allocation) { ["educacao"] = 0.15, ["obras"] = e.Allocation["obras"] + (e.Allocation["educacao"] - 0.15) };
        Assert.Null(engine.SetAllocation(allocation));
        Assert.Contains(engine.BelowMinimum, a => a.Id == "educacao");
        double image = e.Image;
        state.Calendar.JumpTo(new DateOnly(e.Start.Year, 12, 31));
        e.Crisis = null;
        engine.ResolveWeek(automatic: true);
        Assert.Contains(e.News, n => n.Kind == ExecutiveNewsKind.MinimoDescumprido);
        Assert.Contains(e.News, n => n.Kind == ExecutiveNewsKind.ContasRejeitadas);
        Assert.True(e.Image < image + 0.05);
    }

    [Fact]
    public void Politica_por_decreto_entra_em_vigor_e_lei_passa_pela_Camara()
    {
        var (state, engine) = ElectedMayor();
        ExecutiveState e = state.Executive!;
        PolicyDefinition decree = CampaignKit.C.Executive.Policies.Policies.First(p => !p.RequiresLaw && p.Cost > 0 && p.Cost <= engine.FreeReserve);
        EnactResult result = engine.Enact(decree.Id);
        Assert.True(result.Ok && result.Approved);
        Assert.Equal(ExecutiveFailure.JaAdotada, engine.CanEnact(decree));
        double target = engine.Targets()[decree.Boosts.First().Key];

        for (int i = 0; i <= decree.ImplementationWeeks; i++)
        {
            e.Crisis = null;
            engine.AdvanceWeek();
        }

        Assert.Equal(PolicyStatus.Ativa, e.Policies.Single(p => p.PolicyId == decree.Id).Status);
        Assert.True(engine.Targets()[decree.Boosts.First().Key] > target - 1e-9);

        e.EnergyLeft = 100;
        PolicyDefinition law = CampaignKit.C.Executive.Policies.Policies.First(p => p.RequiresLaw && p.Cost <= engine.FreeReserve);
        EnactResult vote = engine.Enact(law.Id);
        Assert.True(vote.Ok);
        Assert.True(vote.Yes + vote.No <= e.Chamber.Count);
        Assert.Equal(vote.Approved, LegislatureEngine.Approved(law.Quorum, vote.Yes, vote.No, e.Chamber.Count));
        Assert.Contains(e.News, n => n.Kind is ExecutiveNewsKind.PoliticaAprovada or ExecutiveNewsKind.PoliticaRejeitada);

        // Sem reserva não há política nova que custe dinheiro.
        var allocation = new Dictionary<string, double>(e.Allocation);
        allocation["custeio"] += allocation["reserva"];
        allocation["reserva"] = 0;
        Assert.Null(engine.SetAllocation(allocation));
        e.EnergyLeft = 100;
        PolicyDefinition costly = CampaignKit.C.Executive.Policies.Policies.First(p => p.Cost > 0 && !e.Policies.Any(x => x.PolicyId == p.Id && x.Counts));
        Assert.Equal(ExecutiveFailure.SemReserva, engine.CanEnact(costly));
    }

    [Fact]
    public void Secretaria_poe_partido_na_base_e_crise_pede_decisao()
    {
        var (state, engine) = ElectedMayor();
        ExecutiveState e = state.Executive!;
        string? outsider = e.Chamber.Select(m => m.PartyId).FirstOrDefault(p => p is not null && !e.InBase(p));
        if (outsider is not null)
        {
            double relation = e.Chamber.Where(m => m.PartyId == outsider).Average(m => m.Relation);
            Assert.Null(engine.GiveSecretaria(outsider));
            Assert.True(e.InBase(outsider));
            Assert.True(e.Chamber.Where(m => m.PartyId == outsider).Average(m => m.Relation) > relation);
            Assert.Null(engine.TakeSecretaria(outsider));
            Assert.False(e.InBase(outsider));
        }

        Assert.Equal(CampaignKit.C.Executive.Config.Secretarias, e.Secretarias.Values.Sum());

        e.Crisis = new PendingCrisis("enchente", state.Calendar.WeekStart);
        Assert.Throws<InvalidOperationException>(() => engine.AdvanceWeek());
        double image = e.Image;
        string text = engine.ResolveCrisis(2);
        Assert.NotEmpty(text);
        Assert.Null(e.Crisis);
        Assert.True(e.Image < image);
    }

    [Theory]
    [InlineData(3UL)]
    [InlineData(6UL)]
    public void Mandato_de_prefeito_inteiro_e_reeleicao_sem_travar(ulong seed)
    {
        var (state, engine) = ElectedMayor(seed);
        var policy = new Rng(seed * 17);
        int weeks = 0;
        while (!CareerFlow.ShouldStartCampaign(state))
        {
            Assert.True(++weeks < 260, "O mandato não chegou ao ano da eleição.");
            ExecutiveState e = state.Executive!;
            if (e.Crisis is not null)
            {
                engine.ResolveCrisis(policy.NextInt(3));
            }

            if (policy.Chance(0.15))
            {
                PolicyDefinition p = policy.Pick(CampaignKit.C.Executive.Policies.Policies);
                engine.Enact(p.Id);
            }

            if (policy.Chance(0.05) && e.Chamber.Select(m => m.PartyId).FirstOrDefault(x => x is not null && !e.InBase(x)) is { } party)
            {
                engine.GiveSecretaria(party);
            }

            foreach (MandateActionDefinition action in CampaignKit.C.Executive.Actions.Actions)
            {
                string? target = action.Kind == MandateActionKind.Articular ? policy.Pick(e.Chamber).Id : action.Targets.Count > 0 ? policy.Pick(action.Targets).Id : null;
                engine.Execute(action.Id, target);
            }

            CareerFlow.AdvanceWeek(state, null, null, null, engine);
            AssertExecutive(state);
            if (weeks % 26 == 0)
            {
                string json = GameStateSerializer.Serialize(state);
                Assert.Equal(json, GameStateSerializer.Serialize(GameStateSerializer.Deserialize(json)));
            }
        }

        // Reeleição: começa candidato a prefeito, como incumbente, com a prefeitura seguindo sozinha.
        CampaignEngine campaign = CampaignEngine.Start(CampaignKit.Context(state));
        Assert.Equal(Office.Prefeito, state.Campaign!.Player.Office);
        Assert.True(state.Campaign.Player.Incumbent);
        Assert.Equal(CareerMode.CampanhaEmMandato, CareerFlow.Mode(state));
        CareerFlow.AdvanceWeek(state, campaign, null, null, engine);
        AssertExecutive(state);
    }

    private static void AssertExecutive(GameState state)
    {
        ExecutiveState e = state.Executive!;
        ExecutiveConfig config = CampaignKit.C.Executive.Config;
        Assert.All(e.Indicators.Values, v => Assert.InRange(v, config.IndicatorBounds.Min, config.IndicatorBounds.Max));
        Assert.InRange(e.Approval, 0, 100);
        Assert.Equal(1, e.Allocation.Values.Sum(), 6);
        Assert.Equal(config.Secretarias, e.Secretarias.Values.Sum());
        Assert.InRange(e.Image, -1, 1);
        Assert.All(e.Support.Values, v => Assert.InRange(v, 0, 1));
        Assert.All(e.Chamber, m => Assert.InRange(m.Relation, -100, 100));
    }
}
