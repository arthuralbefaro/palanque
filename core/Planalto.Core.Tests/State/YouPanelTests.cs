using Planalto.Core.Campaign;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;

namespace Planalto.Core.Tests.State;

/// <summary>Painel Você (parte RL): o que limita o jogador, pelas regras do Core.</summary>
public class YouPanelTests
{
    private const int Mantena = 3139607;

    private static TimeConfig Time => CampaignKit.C.Time;

    private static CampaignEngine Career(ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(Mantena, seed, clock: true);
        engine.Context.State.Campaign!.Events.Pending.Clear();
        engine.Context.State.Player.Resources.Money = 50_000;
        return engine;
    }

    [Fact]
    public void Painel_voce_conta_turnos_estresse_dinheiro_e_prazo_pelas_regras()
    {
        CampaignEngine engine = Career(45);
        GameState state = engine.Context.State;
        AgendaRules rules = engine.Context.Campaign.Commitments.Regras;
        string zone = engine.Territory.Zones[engine.Territory.HomeZone];
        int daysLeft = 7 - state.Time.Day;
        YouStatus before = engine.You();
        Assert.Equal(3, before.FreeToday);
        Assert.Equal(3 * daysLeft, before.FreeWeek);
        Assert.Equal(21, before.WeekTurns);
        Assert.Equal(rules.TurnosSemCusto, before.FreeTodayWithoutStress);
        Assert.Equal(rules.EstresseLimite, before.StressLimit);
        Assert.Equal(state.Player.Resources.Stress, before.Stress);
        Assert.Equal(state.Player.Resources.Money, before.Money);
        Assert.Equal(engine.WeeksToElection, before.WeeksToElection);
        Assert.Equal(engine.Context.State.Campaign!.ElectionDate, before.ElectionDate);
        Assert.Equal(engine.BudgetSummary().PayrollWeekly + engine.BudgetSummary().PlanWeekly, before.WeeklyOutflow);

        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Tarde)).Failure);
        YouStatus after = engine.You();
        Assert.Equal(1, after.FreeToday);
        Assert.Equal(0, after.FreeTodayWithoutStress);
        Assert.Equal(2, after.UsedToday);
        Assert.Equal(before.FreeWeek - 2, after.FreeWeek);

        // Meio-dia: a manhã aconteceu e continua usada; nenhum turno livre se perdeu.
        Clock.Tick(Time, state, Clock.ModoCampanha, 1, Clock.SecondsPerDay(Time, Clock.ModoCampanha, 1) * 6.5 / 16);
        engine.RunDue();
        Assert.Equal(1, engine.You().FreeToday);
    }
}
