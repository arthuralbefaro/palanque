using Planalto.Core.Electorate;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Electorate;

public class PollingTests
{
    private static ElectorateTests.Fixture F => FixtureHolder.Value;

    private static readonly Lazy<ElectorateTests.Fixture> FixtureHolder = new(() => new ElectorateTests.Fixture());

    [Theory]
    [InlineData(1000, 1.0, 3.10)]
    [InlineData(2500, 1.0, 1.96)]
    [InlineData(1000, 1.44, 3.72)]
    public void MargemDeErro_SegueAFormulaComEfeitoDeDesenho(int n, double deff, double expected)
    {
        Assert.Equal(expected, Polling.MarginOfError(n, deff, 1.96), 2);
    }

    [Fact]
    public void Indecisos_DiminuemAteAEleicao()
    {
        PollConfig config = F.Electorate.VoteModel.Poll;

        Assert.Equal(config.UndecidedAtCampaignStart, Polling.Undecided(config.CampaignWeeks, config), 9);
        Assert.Equal(config.UndecidedAtElection, Polling.Undecided(0, config), 9);
        Assert.True(Polling.Undecided(3, config) < Polling.Undecided(8, config));
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void PesquisaInterna_SemViesConvergeParaAIntencaoReal()
    {
        var (truth, candidates) = Truth();
        PollsterDefinition interna = F.Electorate.Pollsters.Pollsters.Single(p => p.Internal);
        var rng = new Rng(21);
        const int rounds = 120;
        var sums = candidates.Keys.ToDictionary(k => k, _ => 0.0);

        for (int i = 0; i < rounds; i++)
        {
            PollResult poll = Polling.Run(truth, candidates, interna, 0, F.Electorate.VoteModel, rng);
            Assert.InRange(poll.SampleSize, interna.SampleMin, interna.SampleMax);
            Assert.NotNull(poll.ByGroup);
            foreach (string id in candidates.Keys)
            {
                sums[id] += poll.Candidates[id];
            }
        }

        double undecided = F.Electorate.VoteModel.Poll.UndecidedAtElection;
        foreach (string id in candidates.Keys)
        {
            double expected = 100 * truth.Votes[id] / truth.Turnout * (1 - undecided);
            Assert.True(Math.Abs((sums[id] / rounds) - expected) < 1.0, $"{id}: {sums[id] / rounds:0.0} vs {expected:0.0}");
        }
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void VieseDeCasa_FavoreceOLadoDoInstituto()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);
        string party = F.Parties[0].Id;
        var candidates = new[]
        {
            new VoteCandidate { Id = "esq", PartyId = party, Position = new Ideology.IdeologicalPosition(-60, -40), Recognition = 0.5 },
            new VoteCandidate { Id = "dir", PartyId = party, Position = new Ideology.IdeologicalPosition(60, 40), Recognition = 0.5 },
        }.ToDictionary(c => c.Id);
        VoteProjection truth = VoteModel.Project(electorate, candidates.Values.ToList(), F.Electorate.VoteModel, F.Themes);
        const string right = "dir";
        PollsterDefinition leftLeaning = F.Electorate.Pollsters.Pollsters.MinBy(p => p.HouseBias)!;
        PollsterDefinition rightLeaning = F.Electorate.Pollsters.Pollsters.MaxBy(p => p.HouseBias)!;

        double Mean(PollsterDefinition pollster)
        {
            var rng = new Rng(33);
            return Enumerable.Range(0, 60).Average(_ => Polling.Run(truth, candidates, pollster, 0, F.Electorate.VoteModel, rng).Candidates[right]);
        }

        Assert.True(Mean(rightLeaning) > Mean(leftLeaning) + 1);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void PesquisaPublica_SomaCemENaoDetalhaGrupos()
    {
        var (truth, candidates) = Truth();
        PollsterDefinition pollster = F.Electorate.Pollsters.Pollsters.First(p => !p.Internal);

        PollResult poll = Polling.Run(truth, candidates, pollster, 6, F.Electorate.VoteModel, new Rng(4));

        Assert.Equal(100, poll.Candidates.Values.Sum() + poll.BlankNull + poll.Undecided, 6);
        Assert.Null(poll.ByGroup);
        Assert.True(poll.Undecided > 5);
    }

    private static (VoteProjection Truth, Dictionary<string, VoteCandidate> Candidates) Truth()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);
        List<VoteCandidate> candidates = F.Candidates();
        return (VoteModel.Project(electorate, candidates, F.Electorate.VoteModel, F.Themes), candidates.ToDictionary(c => c.Id));
    }
}
