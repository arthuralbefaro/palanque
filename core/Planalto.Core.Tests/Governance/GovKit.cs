using System.Collections.Concurrent;
using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Elections;
using Planalto.Core.Executive;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Xunit;

namespace Planalto.Core.Tests.Governance;

/// <summary>
/// Carreiras já empossadas (vereador e prefeito) com o governo da R4 montado. A campanha é jogada uma vez por semente
/// e guardada como save; cada teste parte de uma cópia.
/// </summary>
internal static class GovKit
{
    private static readonly ConcurrentDictionary<ulong, Lazy<string>> Councillors = new();
    private static readonly ConcurrentDictionary<ulong, Lazy<string>> Mayors = new();

    public static int SmallTown() =>
        CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == "pequeno").OrderBy(m => m.CodigoIbge).Skip(7).First().CodigoIbge;

    /// <summary>Campanha de vereador jogada com equipe, promessas e pedidos de liderança aceitos, até a posse.</summary>
    private static string PlayCouncillor(ulong firstSeed)
    {
        for (ulong seed = firstSeed; seed < firstSeed + 30; seed++)
        {
            CampaignEngine campaign = CampaignKit.NewCareer(SmallTown(), seed, strongestParty: true);
            var robot = new CampaignRobot(campaign, seed, new RobotOptions { R2b = true, Answer = LeaderAnswer.Aceitar });
            robot.PlayToElection();
            robot.Vote();
            GameState state = campaign.Context.State;
            if (!state.Campaign!.Outcome!.PlayerElected)
            {
                continue;
            }

            CareerFlow.ConcludeElection(campaign.Context, CampaignKit.LegislatureContext(state), governanceContext: CampaignKit.GovernanceContext(state));
            return GameStateSerializer.Serialize(state);
        }

        throw new InvalidOperationException("Nenhuma semente elegeu o jogador vereador.");
    }

    private static string PlayMayor(ulong firstSeed)
    {
        for (ulong seed = firstSeed; seed < firstSeed + 40; seed++)
        {
            CampaignEngine campaign = CampaignKit.NewCareer(SmallTown(), seed, strongestParty: true);
            GameState state = campaign.Context.State;
            state.Player.Attributes.Set(Planalto.Core.Characters.AttributeId.Carisma, 70);
            if (CareerFlow.SwitchCandidacy(campaign.Context, Office.Prefeito).Issue is not null)
            {
                continue;
            }

            CampaignAutoplayTests.Play(campaign, seed, new RobotOptions { Answer = LeaderAnswer.Aceitar });
            if (!state.Campaign!.Outcome!.PlayerElected)
            {
                continue;
            }

            CareerFlow.ConcludeElection(campaign.Context, CampaignKit.LegislatureContext(state), CampaignKit.ExecutiveContext(state), CampaignKit.GovernanceContext(state));
            return GameStateSerializer.Serialize(state);
        }

        throw new InvalidOperationException("Nenhuma semente elegeu o jogador prefeito.");
    }

    public sealed record Council(GameState State, GovernanceEngine Gov, LegislatureEngine Legislature)
    {
        public GovernanceState G => State.Governance!;

        public MandateState Mandate => State.Mandate!;

        /// <summary>Uma semana do mandato: votações, governo da cidade e calendário.</summary>
        public void Week() => CareerFlow.AdvanceWeek(State, null, Legislature, governance: Gov);

        public void Weeks(int n)
        {
            for (int i = 0; i < n && State.Mandate is not null; i++)
            {
                Week();
            }
        }
    }

    public sealed record CityHall(GameState State, GovernanceEngine Gov, ExecutiveEngine Executive)
    {
        public GovernanceState G => State.Governance!;

        public ExecutiveState E => State.Executive!;

        /// <summary>Uma semana da prefeitura; a crise pendente é respondida pela primeira opção, como faria um jogador apressado.</summary>
        public void Week()
        {
            if (State.Executive is { Crisis: not null })
            {
                Executive.ResolveCrisis(0);
            }

            CareerFlow.AdvanceWeek(State, null, null, executive: Executive, governance: Gov);
        }

        public void Weeks(int n)
        {
            for (int i = 0; i < n && State.Executive is not null; i++)
            {
                Week();
            }
        }
    }

    /// <summary>Vereador empossado, com governo, numa cópia própria do estado.</summary>
    public static Council Councillor(ulong seed = 3)
    {
        string json = Councillors.GetOrAdd(seed, s => new Lazy<string>(() => PlayCouncillor(s))).Value;
        return Load(json);
    }

    public static Council Load(string json)
    {
        GameState state = GameStateSerializer.Deserialize(json);
        Assert.NotNull(state.Governance);
        return new Council(state, new GovernanceEngine(CampaignKit.GovernanceContext(state)), new LegislatureEngine(CampaignKit.LegislatureContext(state)));
    }

    /// <summary>Prefeito empossado, com governo, numa cópia própria do estado.</summary>
    public static CityHall Mayor(ulong seed = 3)
    {
        string json = Mayors.GetOrAdd(seed, s => new Lazy<string>(() => PlayMayor(s))).Value;
        return LoadMayor(json);
    }

    public static CityHall LoadMayor(string json)
    {
        GameState state = GameStateSerializer.Deserialize(json);
        Assert.NotNull(state.Governance);
        return new CityHall(state, new GovernanceEngine(CampaignKit.GovernanceContext(state)), new ExecutiveEngine(CampaignKit.ExecutiveContext(state)));
    }

    /// <summary>Primeira obra aberta que atende ao filtro; falha com mensagem se não houver.</summary>
    public static Work OpenWork(GovernanceState g, Func<Work, bool>? filter = null) =>
        g.Works.FirstOrDefault(w => w.Open && (filter is null || filter(w))) ?? throw new InvalidOperationException("Nenhuma obra aberta com o filtro pedido.");

    /// <summary>O valor realizado cabe na faixa prevista para a linha (princípio f: o resultado sempre cai dentro da previsão).</summary>
    public static void AssertWithin(GovForecast forecast, string kind, string? target, double actual)
    {
        Assert.True(forecast.Contains(kind, target, actual), $"{kind}/{target}: {actual} fora da faixa prevista ({forecast.Line(kind, target)}).");
    }
}
