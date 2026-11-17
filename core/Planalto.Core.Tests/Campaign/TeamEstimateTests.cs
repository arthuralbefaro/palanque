using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Electorate;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Estimativa da equipe (RL): a intenção de voto sem pesquisa, marcada como estimativa, com faixa.</summary>
public class TeamEstimateTests
{
    private const int Mantena = 3139607;

    private static CampaignEngine Career(ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(Mantena, seed, clock: true);
        engine.Context.State.Campaign!.Events.Pending.Clear();
        engine.Context.State.Player.Resources.Money = 50_000;
        return engine;
    }

    [Fact]
    public void Estimativa_usa_a_conta_da_pesquisa_com_faixa_mais_larga_que_o_erro_da_pesquisa_interna()
    {
        CampaignEngine engine = Career(45);
        CampaignState campaign = engine.Context.State.Campaign!;
        Assert.Equal(CampaignPhase.PreCampanha, campaign.Phase);
        Assert.Empty(campaign.Polls);

        IntentionEstimate estimate = engine.TeamEstimate();
        PollConfig poll = engine.Context.ElectorateContent.VoteModel.Poll;
        Office office = campaign.Player.Office;
        var named = campaign.ForOffice(office).Where(c => !c.IsBulk).Select(c => c.Id).ToHashSet();

        // Todos os candidatos com nome da disputa do jogador, do mais votado ao menos votado, o jogador entre eles.
        Assert.Equal(named.OrderBy(i => i), estimate.Lines.Select(l => l.CandidateId).OrderBy(i => i));
        Assert.Contains(estimate.Lines, l => l.CandidateId == RaceSetup.PlayerId);
        Assert.True(estimate.Lines.Zip(estimate.Lines.Skip(1)).All(p => p.First.Share >= p.Second.Share));

        // Na pré-campanha, os indecisos do começo da campanha; o ponto central é o esperado de uma pesquisa sem viés.
        Assert.Equal(Math.Round(100 * poll.UndecidedAtCampaignStart, 1), estimate.Undecided);
        VoteProjection truth = engine.Project(office, noise: null);
        PollsterDefinition interna = engine.Context.ElectorateContent.Pollsters.Pollsters.Single(p => p.Internal);
        double internalMargin = Polling.MarginOfError(interna.SampleMax, interna.DesignEffect, poll.ConfidenceZ);
        foreach (EstimateLine line in estimate.Lines)
        {
            double expected = 100 * (1 - poll.UndecidedAtCampaignStart) * truth.Votes[line.CandidateId] / truth.Turnout;
            Assert.Equal(Math.Round(expected, 1), line.Share, 6);
            Assert.True(line.Low <= line.Share && line.Share <= line.High, $"{line.CandidateId}: {line.Low} a {line.High}, centro {line.Share}");
            double margin = Math.Max(poll.TeamEstimate.MarginMinPoints, poll.TeamEstimate.MarginRelative * expected);
            Assert.Equal(Math.Round(expected + margin, 1), line.High, 6);
            Assert.Equal(Math.Round(Math.Max(0, expected - margin), 1), line.Low, 6);

            // Ninguém foi entrevistado: a faixa é mais larga que o erro de amostra da pesquisa interna no mesmo ponto.
            double sampling = internalMargin * Math.Sqrt(expected / 100 * (1 - (expected / 100)) / 0.25);
            Assert.True(margin > sampling, $"{line.CandidateId}: faixa de {margin:0.0} pontos, erro da pesquisa interna {sampling:0.0}");
        }
    }

    [Fact]
    public void Estimativa_so_le_o_estado_e_acompanha_a_campanha()
    {
        CampaignEngine engine = Career(46);
        GameState state = engine.Context.State;
        RngState rng = state.Rng.GetState();
        IntentionEstimate first = engine.TeamEstimate();
        Assert.Equal(first.Lines, engine.TeamEstimate().Lines);
        Assert.Equal(rng, state.Rng.GetState());
        Assert.Empty(state.Campaign!.Polls);

        // Uma semana de caminhadas por toda a cidade: a projeção do jogador sobe e a estimativa vai junto.
        Office office = state.Campaign.Player.Office;
        PollConfig poll = engine.Context.ElectorateContent.VoteModel.Poll;
        double votesBefore = engine.Project(office, noise: null).Votes[RaceSetup.PlayerId];
        for (int day = 0; day < 6; day++)
        {
            state.Campaign.Events.Pending.Clear();
            state.Campaign.Team.Pending.Clear();
            state.Campaign.Agenda.PendingRequest = null;
            state.Player.Resources.Money = 50_000;
            foreach (Turno turno in new[] { Turno.Manha, Turno.Tarde })
            {
                string zone = engine.ZoneBoard().OrderBy(r => r.Presence).First().ZoneId;
                engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, turno));
            }

            engine.AdvanceDay();
        }

        VoteProjection truth = engine.Project(office, noise: null);
        Assert.True(truth.Votes[RaceSetup.PlayerId] > votesBefore, $"a projeção do jogador não subiu com a semana de caminhadas ({votesBefore:0.0} → {truth.Votes[RaceSetup.PlayerId]:0.0})");
        double undecided = Polling.Undecided(engine.WeeksToElection, poll);
        EstimateLine mine = engine.TeamEstimate().Lines.Single(l => l.CandidateId == RaceSetup.PlayerId);
        Assert.Equal(Math.Round(100 * (1 - undecided) * truth.Votes[RaceSetup.PlayerId] / truth.Turnout, 1), mine.Share, 6);
        Assert.True(mine.High >= mine.Share + poll.TeamEstimate.MarginMinPoints - 0.05);
    }
}
