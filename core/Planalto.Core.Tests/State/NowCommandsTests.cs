using Planalto.Core.Campaign;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;

namespace Planalto.Core.Tests.State;

/// <summary>"Fazer agora" (parte RL): próximo turno livre, amanhã só por escolha explícita e o custo do terceiro turno.</summary>
public class NowCommandsTests
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

    private static CommitmentRequest Walk(CampaignEngine engine) =>
        new("caminhada", engine.Territory.Zones[engine.Territory.HomeZone], engine.Today, Turno.Manha);

    private static void SkipTo(GameState state, int hour) =>
        state.Time.Minute = Math.Max(state.Time.Minute, (hour - Time.Dia.HoraInicio) * 60);

    [Fact]
    public void Fazer_agora_usa_o_turno_em_curso_e_avanca_o_relogio_ate_ele_acontecer()
    {
        CampaignEngine engine = Career(51);
        GameState state = engine.Context.State;
        SkipTo(state, 8);
        int changes = 0;
        engine.Changed += () => changes++;
        NowPlan plan = engine.PlanNow(Walk(engine));
        Assert.Null(plan.Failure);
        Assert.False(plan.Tomorrow);
        Assert.Equal(Turno.Manha, plan.Turno);
        Assert.NotNull(plan.Forecast);

        DateOnly today = engine.Today;
        double before = engine.Territory.PresenceOf(RaceSetup.PlayerId, engine.Territory.HomeZone);
        NowResult done = engine.DoNow(Walk(engine), () => throw new InvalidOperationException("não vira o dia"));
        CommitmentResult result = Assert.Single(done.Results);
        Assert.Equal(today, engine.Today);
        Assert.Equal((12, 0), Clock.TimeOfDay(Time, state));
        Assert.True(engine.Territory.PresenceOf(RaceSetup.PlayerId, engine.Territory.HomeZone) > before);
        Assert.InRange(result.Presence[Walk(engine).ZoneId!], plan.Forecast!.PresenceMin - 1e-9, plan.Forecast.PresenceMax + 1e-9);
        Assert.Empty(engine.Agenda.Scheduled);
        Assert.True(changes >= 2);
    }

    [Fact]
    public void So_o_terceiro_turno_livre_continua_agora_e_a_previsao_traz_o_estresse_dele()
    {
        CampaignEngine engine = Career(52);
        AgendaRules rules = engine.Context.Campaign.Commitments.Regras;
        CommitmentType walk = engine.Context.Campaign.Commitments.Get("caminhada");
        Assert.Null(engine.DoNow(Walk(engine), () => false).Plan.Failure);
        Assert.Null(engine.DoNow(Walk(engine) with { ZoneId = engine.Territory.Zones[0] }, () => false).Plan.Failure);
        Assert.Equal(2, engine.Agenda.Log.Count);

        NowPlan third = engine.PlanNow(Walk(engine));
        Assert.Null(third.Failure);
        Assert.False(third.Tomorrow);
        Assert.Equal(Turno.Noite, third.Turno);
        Assert.Equal(walk.Estresse + rules.EstresseTurnoExtra, third.Forecast!.Stress);

        int stress = engine.Context.State.Player.Resources.Stress;
        CommitmentResult result = Assert.Single(engine.DoNow(Walk(engine), () => false).Results);
        Assert.Equal(third.Forecast.Stress, result.Stress);
        Assert.Equal(stress + result.Stress, engine.Context.State.Player.Resources.Stress);
    }

    [Fact]
    public void Sem_turno_livre_hoje_o_plano_e_amanha_e_so_vira_o_dia_com_a_escolha()
    {
        CampaignEngine engine = Career(53);
        GameState state = engine.Context.State;
        SkipTo(state, 19);
        Assert.Null(engine.Schedule(Walk(engine) with { Turno = Turno.Noite }).Failure);
        DateOnly today = engine.Today;

        NowPlan plan = engine.PlanNow(Walk(engine));
        Assert.True(plan.Tomorrow);
        Assert.Equal(today.AddDays(1), plan.Date);
        Assert.Equal(Turno.Manha, plan.Turno);
        Assert.Null(plan.Failure);

        // O dia parou numa decisão: o compromisso fica marcado para amanhã e nada acontece.
        NowResult stopped = engine.DoNow(Walk(engine), () => false);
        Assert.Empty(stopped.Results);
        Assert.Equal(today.AddDays(1), stopped.Commitment!.Date);
        Assert.Equal(today, engine.Today);
        Assert.True(engine.Cancel(stopped.Commitment.Id));

        // Com o dia virando: acontece amanhã de manhã, com o relógio ao meio-dia.
        NowResult done = engine.DoNow(Walk(engine), () =>
        {
            engine.AdvanceDay();
            return true;
        });
        Assert.Equal(today.AddDays(1), engine.Today);
        Assert.Equal(Turno.Manha, Assert.Single(done.Results).Commitment.Turno);
        Assert.Equal((12, 0), Clock.TimeOfDay(Time, state));
    }

    [Fact]
    public void Fazer_agora_bloqueado_diz_o_motivo_e_nao_marca_nada()
    {
        CampaignEngine engine = Career(54);
        engine.Context.State.Player.Resources.Money = 0;
        NowResult blocked = engine.DoNow(new CommitmentRequest("comicio", Walk(engine).ZoneId, engine.Today, Turno.Manha), () => true);
        Assert.NotNull(blocked.Plan.Failure);
        Assert.Null(blocked.Commitment);
        Assert.Empty(engine.Agenda.Scheduled);
        Assert.Equal((6, 0), Clock.TimeOfDay(Time, engine.Context.State));
    }
}
