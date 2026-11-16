using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.Municipios;
using Planalto.Core.Save;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Campaign;

/// <summary>
/// Joga campanhas de vereador inteiras com escolhas automáticas e confere que nada trava, nenhuma exceção
/// escapa e o estado continua válido semana a semana (inclusive ida e volta pelo save).
/// </summary>
public class CampaignAutoplayTests
{
    /// <summary>Um município por porte, mais um com prefeitura pendente na fonte (D-011).</summary>
    public static TheoryData<int, ulong> Cases()
    {
        var data = new TheoryData<int, ulong>();
        int[] cities = Cities();
        foreach (int ibge in cities)
        {
            foreach (ulong seed in new ulong[] { 11, 22, 33 })
            {
                data.Add(ibge, seed);
            }
        }

        return data;
    }

    private static int[] Cities()
    {
        var all = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais).OrderBy(m => m.CodigoIbge).ToList();
        string[] portes = ["pequeno", "medio", "grande", "metropole"];
        var picked = portes.Select(p => all.First(m => m.Porte == p).CodigoIbge).ToList();
        picked.Add(3550308); // São Paulo: a maior Câmara do país.
        picked.Add(5200852); // Americano do Brasil (GO): prefeito pendente na fonte.
        return [.. picked.Distinct()];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CampanhaInteira_SemTravamentoNemEstadoInvalido(int ibge, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed);
        Play(engine, seed);
        AssertOutcome(engine);
    }

    [Theory]
    [InlineData(3304557, 5UL)]
    [InlineData(2927408, 6UL)]
    public void CampanhaInteira_EmMundoSorteado(int ibge, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed, "multipartidario_moderado");
        Play(engine, seed);
        AssertOutcome(engine);
    }

    /// <summary>Política automática: resolve eventos, gasta a energia em ações possíveis, encomenda pesquisas e vota em si.</summary>
    internal static void Play(CampaignEngine engine, ulong seed)
    {
        var policy = new Rng(seed * 7919);
        GameState state = engine.Context.State;
        int weeks = 0;
        while (state.Campaign!.Phase != CampaignPhase.Votacao)
        {
            Assert.True(++weeks < 80, "A campanha não chegou à eleição.");
            ResolveEvents(engine, policy);

            var actions = engine.Context.Campaign.Actions.Actions;
            for (int tries = 0; tries < 12; tries++)
            {
                CampaignActionDefinition action = policy.Pick(actions);
                string? target = action.Targets.Count > 0 ? policy.Pick(action.Targets).Id : null;
                engine.Execute(action.Id, target);
            }

            if (policy.Chance(0.1))
            {
                engine.CommissionPoll("interna");
            }

            AssertState(engine);
            if (weeks % 6 == 0)
            {
                RoundTrip(engine);
            }

            engine.AdvanceWeek();
        }

        ResolveEvents(engine, policy);
        engine.CastPlayerVote(new UrnaVote(Office.Vereador, state.Campaign.Player.Number, false));
        engine.CastPlayerVote(new UrnaVote(Office.Prefeito, null, true));
        engine.ResolveElection();
        RoundTrip(engine);
    }

    private static void ResolveEvents(CampaignEngine engine, Rng policy)
    {
        CampaignState campaign = engine.Context.State.Campaign!;
        PlayDebate(engine, policy);
        while (campaign.Events.Pending.Count > 0)
        {
            PendingEvent pending = campaign.Events.Pending[0];
            EventDefinition definition = engine.Context.Events!.Find(pending.EventId)!;
            var open = Enumerable.Range(0, definition.Choices.Count).Where(i => EventEngine.Blocked(engine.Context, definition.Choices[i]) is null).ToList();
            Assert.NotEmpty(open);
            EventEngine.Resolve(engine.Context, pending, policy.Pick(open), engine.Context.State.Rng);
        }
    }

    /// <summary>Joga o debate marcado (se houver): carta sorteada da mão; ataques miram o primeiro rival.</summary>
    internal static void PlayDebate(CampaignEngine engine, Rng policy)
    {
        CampaignState campaign = engine.Context.State.Campaign!;
        while (campaign.Debate is { } debate)
        {
            if (debate.Finished)
            {
                Planalto.Core.Debate.DebateEngine.Close(campaign);
                break;
            }

            string card = policy.Pick(debate.Hand);
            string? target = engine.Context.Debate!.Card(card).Targeted ? debate.Participants[1] : null;
            Planalto.Core.Debate.DebateEngine.Play(engine.Context, engine.Context.Debate!, card, target, engine.Context.State.Rng);
        }
    }

    private static void AssertState(CampaignEngine engine)
    {
        CampaignState c = engine.Context.State.Campaign!;
        Assert.InRange(c.EnergyLeft, 0, engine.Context.Config.WeeklyEnergy * 2);
        Assert.All(c.Candidates, x =>
        {
            Assert.InRange(x.Recognition, 0, 100);
            Assert.InRange(x.Image, -1, 1);
            Assert.All(x.Support.Values, v => Assert.InRange(v, 0, 1));
        });
        Assert.Equal(c.Candidates.Count, c.Candidates.Select(x => x.Id).Distinct().Count());
        Assert.Single(c.Candidates, x => x.IsPlayer);
    }

    private static void RoundTrip(CampaignEngine engine)
    {
        string json = GameStateSerializer.Serialize(engine.Context.State);
        Assert.Equal(json, GameStateSerializer.Serialize(GameStateSerializer.Deserialize(json)));
    }

    private static void AssertOutcome(CampaignEngine engine)
    {
        CampaignState c = engine.Context.State.Campaign!;
        Municipio m = engine.Context.Municipio;
        ElectionOutcome outcome = c.Outcome!;
        Assert.Equal(CampaignPhase.Encerrada, c.Phase);
        int seats = engine.Context.SeatsIn(c.ElectionDate.Year);
        Assert.True(seats <= m.VagasVereador);
        Assert.True(outcome.Vereador.Elected.Count <= seats);
        Assert.True(outcome.Vereador.Elected.Count >= seats - 2, "Vagas demais sem preencher.");
        Assert.Equal(outcome.Vereador.Elected.Count, outcome.Vereador.SeatsByParty.Values.Sum());
        Assert.Single(outcome.Prefeito.Elected);
        Assert.All(outcome.Vereador.Votes.Values, v => Assert.True(v >= 0));
        long total = outcome.Vereador.Votes.Values.Sum() + outcome.Vereador.Blank + outcome.Vereador.Null + outcome.Vereador.Abstention;
        Assert.InRange(total, m.Eleitorado * 0.98, m.Eleitorado * 1.02);
        Assert.Equal(100, outcome.Vereador.Snapshots[^1].PercentCounted);
        Assert.Equal(outcome.Vereador.Votes, outcome.Vereador.Snapshots[^1].Votes);
        Assert.Equal(outcome.PlayerElected, outcome.Vereador.Elected.Contains(RaceSetup.PlayerId));
    }
}
