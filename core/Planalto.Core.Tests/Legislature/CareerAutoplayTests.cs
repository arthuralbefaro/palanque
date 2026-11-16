using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.Legislature;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Xunit;

namespace Planalto.Core.Tests.Legislature;

/// <summary>
/// Joga carreiras inteiras com escolhas automáticas: mandato de 4 anos, campanha de reeleição com o mandato em
/// segundo plano e entressafra depois de uma derrota. Confere que nada trava e o estado continua válido.
/// </summary>
public class CareerAutoplayTests
{
    [Theory]
    [InlineData(3UL)]
    [InlineData(4UL)]
    [InlineData(5UL)]
    [Trait("Categoria", "Lento")]
    public void Mandato_inteiro_e_reeleicao_sem_travar(ulong seed)
    {
        var (state, legislature) = LegislatureTests.ElectedCouncillor(seed);
        var policy = new Rng(seed * 31);
        int firstMandateYear = state.Mandate!.Start.Year;
        int weeks = 0;

        // Mandato até o ano da eleição seguinte.
        while (!CareerFlow.ShouldStartCampaign(state))
        {
            Assert.True(++weeks < 260, "O mandato não chegou ao ano da eleição.");
            PlayMandateWeek(legislature, policy);
            CareerFlow.AdvanceWeek(state, null, legislature);
            AssertMandate(state);
            if (weeks % 26 == 0)
            {
                RoundTrip(state);
            }
        }

        Assert.Equal(firstMandateYear + 3, state.Calendar.Year);
        Assert.True(state.Mandate!.Bills.Count(b => b.Decided is not null) > 20, "Pouca coisa votada em 4 anos.");

        // Campanha de reeleição: o mandato vota sozinho.
        CampaignEngine campaign = CampaignEngine.Start(CampaignKit.Context(state));
        Assert.Equal(CareerMode.CampanhaEmMandato, CareerFlow.Mode(state));
        Assert.True(state.Campaign!.Player.Incumbent, "O jogador disputa como vereador em exercício.");
        Assert.Contains(state.Campaign.Candidates, c => c.Incumbent && !c.IsPlayer && c.Office == Office.Vereador);
        int votedBefore = state.Mandate.Bills.Count(b => b.Decided is not null);
        PlayCampaign(state, campaign, legislature, policy);
        Assert.True(state.Mandate.Bills.Count(b => b.Decided is not null) > votedBefore, "O mandato parou durante a campanha.");

        bool reelected = state.Campaign!.Outcome!.PlayerElected;
        CareerFlow.ConcludeElection(campaign.Context, CampaignKit.LegislatureContext(state));
        Assert.Null(state.Campaign);
        Assert.Equal(reelected ? CareerMode.Mandato : CareerMode.ForaDeMandato, CareerFlow.Mode(state));
        Assert.Equal(firstMandateYear + 4, state.Calendar.WeekEnd.Year);
        RoundTrip(state);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Derrota_leva_a_entressafra_e_a_proxima_eleicao()
    {
        CampaignEngine campaign = CampaignKit.NewCareer(3550308, 8);
        CampaignAutoplayTests.Play(campaign, 8);
        GameState state = campaign.Context.State;
        Assert.False(state.Campaign!.Outcome!.PlayerElected, "Este caso precisa de uma derrota.");
        int electionYear = state.Campaign.ElectionDate.Year;

        CareerFlow.ConcludeElection(campaign.Context, CampaignKit.LegislatureContext(state));
        Assert.Equal(CareerMode.ForaDeMandato, CareerFlow.Mode(state));
        Assert.NotNull(state.Local);
        Assert.DoesNotContain(RaceSetup.PlayerId, state.Local!.Councillors);

        CareerFlow.SkipToNextElection(state);
        Assert.Equal(electionYear + 4, state.Calendar.Year);
        Assert.True(CareerFlow.ShouldStartCampaign(state));

        // A nova disputa traz os vereadores eleitos da anterior como incumbentes, e o prefeito se ainda pode.
        CampaignEngine next = CampaignEngine.Start(CampaignKit.Context(state));
        var incumbents = state.Campaign!.Candidates.Where(c => c.Incumbent && c.Office == Office.Vereador).ToList();
        Assert.NotEmpty(incumbents);
        Assert.All(incumbents, c => Assert.Contains(c.CharacterId, state.Local.Councillors));
        Assert.False(state.Campaign.Player.Incumbent);
        if (!state.Local.MayorReelected)
        {
            Assert.Contains(state.Campaign.Candidates, c => c.Office == Office.Prefeito && c.CharacterId == state.Local.MayorId && c.Incumbent);
        }

        Assert.Equal(CareerMode.Campanha, CareerFlow.Mode(state));
        Assert.NotNull(next);
    }

    internal static void PlayMandateWeek(LegislatureEngine engine, Rng policy)
    {
        GameState state = engine.Context.State;
        if (state.Player.PartyId is null)
        {
            // Expulso: filia-se a outro partido (sem perder o mandato).
            PartyAffiliation.Join(state, policy.Pick(state.Parties).Id);
            Assert.NotNull(state.Mandate);
        }

        foreach (Bill bill in engine.Pauta.ToList())
        {
            int orientation = engine.PlayerOrientation(bill);
            VoteChoice vote = policy.NextDouble() switch
            {
                < 0.6 => orientation >= 0 ? VoteChoice.Sim : VoteChoice.Nao,
                < 0.85 => policy.Chance(0.5) ? VoteChoice.Sim : VoteChoice.Nao,
                < 0.95 => VoteChoice.Abstencao,
                _ => VoteChoice.Ausente,
            };
            engine.CastVote(bill.Id, vote);
        }

        foreach (MandateActionDefinition action in engine.Context.Content.Actions.Actions)
        {
            string? target = action.Kind switch
            {
                MandateActionKind.Propor => engine.ProposableBills().Select(b => b.Id).FirstOrDefault(),
                MandateActionKind.Urgencia => engine.Mandate.Bills.FirstOrDefault(b => b.AuthorId == RaceSetup.PlayerId && b.Stage == BillStage.Comissao)?.Id,
                MandateActionKind.Articular => policy.Pick(engine.Mandate.Members.Where(m => !m.IsPlayer).ToList()).Id,
                _ => action.Targets.Count > 0 ? policy.Pick(action.Targets).Id : null,
            };
            if (policy.Chance(0.5))
            {
                engine.Execute(action.Id, target);
            }
        }
    }

    private static void PlayCampaign(GameState state, CampaignEngine campaign, LegislatureEngine legislature, Rng policy)
    {
        // A agenda é jogada dia a dia; na virada da semana anda a carreira inteira (mandato em segundo plano e campanha).
        var robot = new CampaignRobot(campaign, policy.NextUInt64() % 1_000_000);
        robot.PlayToElection(
            _ => AssertMandate(state),
            () => CareerFlow.AdvanceWeek(state, campaign, legislature),
            maxWeeks: 60);
        AssertMandate(state);
        robot.Vote();
    }

    private static void AssertMandate(GameState state)
    {
        MandateState m = state.Mandate!;
        Assert.InRange(m.Fidelity, 0, 100);
        Assert.InRange(m.MayorRelation, -100, 100);
        Assert.InRange(m.Recognition, 0, 100);
        Assert.InRange(m.Image, -1, 1);
        Assert.All(m.Support.Values, v => Assert.InRange(v, 0, 1));
        Assert.All(m.Members, x => Assert.InRange(x.Relation, -100, 100));
        Assert.Single(m.Members, x => x.IsPlayer);
        Assert.All(m.Bills, b => Assert.Equal(b.Stage is BillStage.Aprovado or BillStage.Rejeitado or BillStage.Arquivado, b.Decided is not null));
        Assert.All(m.Bills.Where(b => b.Stage is BillStage.Aprovado or BillStage.Rejeitado), b => Assert.Equal(m.Members.Count, b.Yes + b.No + b.Abstain + (b.PlayerVote == VoteChoice.Ausente ? 1 : 0)));
        Assert.Equal(m.Bills.Count, m.Bills.Select(b => b.Id).Distinct().Count());
        Assert.True(state.Player.Resources.PoliticalCapital >= 0);
    }

    private static void RoundTrip(GameState state)
    {
        string json = GameStateSerializer.Serialize(state);
        Assert.Equal(json, GameStateSerializer.Serialize(GameStateSerializer.Deserialize(json)));
    }
}
