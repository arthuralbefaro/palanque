using Planalto.Core.Campaign;
using Planalto.Core.Elections;
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
    [Trait("Categoria", "Lento")]
    public void CampanhaInteira_SemTravamentoNemEstadoInvalido(int ibge, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed);
        Play(engine, seed);
        AssertOutcome(engine);
    }

    /// <summary>A mesma campanha com três turnos por dia (estresse do turno extra, descanso) e sem recusar convite.</summary>
    [Theory]
    [InlineData(3106200, 41UL)]
    [InlineData(1100015, 42UL)]
    [Trait("Categoria", "Lento")]
    public void CampanhaInteira_ComAgendaCheia(int ibge, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed);
        Play(engine, seed, new RobotOptions { TurnsPerDay = 3, AcceptInvite = 1, Answer = LeaderAnswer.Aceitar });
        AssertOutcome(engine);
    }

    [Theory]
    [InlineData(3304557, 5UL)]
    [InlineData(2927408, 6UL)]
    [Trait("Categoria", "Lento")]
    public void CampanhaInteira_EmMundoSorteado(int ibge, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed, "multipartidario_moderado");
        Play(engine, seed);
        AssertOutcome(engine);
    }

    /// <summary>
    /// Política automática: marca compromissos toda semana, resolve eventos e pedidos de liderança, encomenda
    /// pesquisas, avança dia a dia e vota em si. Confere o estado a cada semana e o save a cada seis.
    /// </summary>
    internal static void Play(CampaignEngine engine, ulong seed, RobotOptions? options = null)
    {
        var robot = new CampaignRobot(engine, seed, options);
        robot.PlayToElection(week =>
        {
            AssertState(engine);
            if (week % 6 == 0)
            {
                RoundTrip(engine);
            }
        });
        AssertState(engine);
        robot.Vote();
        RoundTrip(engine);
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

    internal static void AssertState(CampaignEngine engine)
    {
        GameState state = engine.Context.State;
        CampaignState c = state.Campaign!;
        Assert.All(c.Candidates, x =>
        {
            Assert.InRange(x.Recognition, 0, 100);
            Assert.InRange(x.Image, -1, 1);
            Assert.All(x.Support.Values, v => Assert.InRange(v, 0, 1));
            Assert.Equal(c.Territory.Recognition(x.Id), x.Recognition, 6);
            Assert.All(c.Territory.Presence[x.Id], p => Assert.InRange(p, 0, 100));
        });
        Assert.Equal(c.Candidates.Count, c.Candidates.Select(x => x.Id).Distinct().Count());
        Assert.Single(c.Candidates, x => x.IsPlayer);

        // Território e agenda: apoio por zona entre 0 e 1, cansaço positivo, nenhum turno com dois compromissos.
        Assert.All(c.Territory.Support.Values.SelectMany(zones => zones).SelectMany(z => z.Values), v => Assert.InRange(v, 0, 1));
        Assert.All(c.Agenda.Fatigue.Values, v => Assert.True(v > 0));
        var turns = c.Agenda.Scheduled.Where(x => !x.Delegado)
            .SelectMany(x => Enumerable.Range((int)x.Turno, engine.Context.Campaign.Commitments.Get(x.Type).Turnos).Select(t => (x.Date, Turno: t)))
            .ToList();
        Assert.Equal(turns.Count, turns.Distinct().Count());
        Assert.All(turns, t => Assert.InRange(t.Turno, 0, 2));
        Assert.InRange(state.Player.Resources.Stress, 0, 100);

        // Cidade: toda liderança é uma pessoa com personagem, relação e memória dentro dos limites.
        Assert.All(state.City!.Leaders, l =>
        {
            Planalto.Core.People.Person person = state.City.Person(l.PersonId)!;
            Assert.Contains(state.Npcs, n => n.Id == l.PersonId);
            Assert.InRange(person.Relation(engine.Today, engine.Context.Campaign.People), -100, 100);
            Assert.True(person.Memories.Count <= engine.Context.Campaign.People.MaxMemories);
            Assert.All(l.Favor.Values, f => Assert.InRange(f, 0, 100));
        });

        // R2b: equipe, verba, plataforma e objetivos dentro dos limites.
        Assert.All(state.City.Staff, s =>
        {
            Assert.InRange(s.Loyalty, 0, 100);
            Assert.True(s.Salary > 0);
            Assert.Contains(state.Npcs, n => n.Id == s.PersonId);
            Assert.NotNull(state.City.Person(s.PersonId));
            Assert.False(s.Hired && s.Left);
        });
        Assert.True(state.City.Staff.Where(s => s.Hired).GroupBy(s => s.Role).All(g => g.Count() == 1), "Uma pessoa por função.");
        Assert.True(engine.TeamSlotsUsed <= engine.TeamSlots, "Equipe acima das vagas do porte.");
        Assert.All(c.Budget.Plan.Concat(c.Budget.Active), a =>
        {
            Assert.True(a.Amount > 0);
            Assert.NotNull(engine.Context.Campaign.Channels.Find(a.ChannelId));
        });
        Assert.True(engine.CabosPlanned <= engine.CabosLimit, "Cabos acima do limite legal.");
        Assert.All(c.Platform.Known.Values, k => Assert.InRange(k, 0, 1));
        Assert.All(c.Platform.Positions.Values, p => Assert.InRange(p, Planalto.Core.Ideology.ThemeDefinition.MinPosition, Planalto.Core.Ideology.ThemeDefinition.MaxPosition));
        Assert.True(c.Platform.Flags.Count <= engine.Context.Campaign.Platform.Regras.BandeirasMax);
        Assert.InRange(engine.Credibility, engine.Context.Campaign.Promises.Regras.CredibilidadeMinima, 1);

        if (c.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha)
        {
            ObjectiveTests.AssertPanel(engine);
        }
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
