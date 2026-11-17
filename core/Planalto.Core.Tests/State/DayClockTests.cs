using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;

namespace Planalto.Core.Tests.State;

/// <summary>Relógio do dia e linha do dia (parte RL, princípio i).</summary>
public class DayClockTests
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
    public void Relogio_vai_das_6_as_22_com_os_turnos_em_sequencia()
    {
        Assert.Empty(Time.Validate());
        Assert.Equal(6, Time.Dia.HoraInicio);
        Assert.Equal(22, Time.Dia.HoraFim);
        Assert.Equal(16 * 60, Time.Dia.Minutes);
        Assert.Equal(0, Time.Dia.StartOf(Turno.Manha));
        Assert.Equal(Time.Dia.EndOf(Turno.Manha), Time.Dia.StartOf(Turno.Tarde));
        Assert.Equal(Time.Dia.EndOf(Turno.Tarde), Time.Dia.StartOf(Turno.Noite));
        Assert.Equal(Time.Dia.Minutes, Time.Dia.EndOf(Turno.Noite));
    }

    [Fact]
    public void Dia_com_buraco_entre_turnos_reprova()
    {
        var day = new DayHours
        {
            Note = "",
            HoraInicio = 6,
            HoraFim = 22,
            Turnos =
            [
                new TurnoHours { Turno = Turno.Manha, Inicio = 6, Fim = 12 },
                new TurnoHours { Turno = Turno.Tarde, Inicio = 13, Fim = 18 },
                new TurnoHours { Turno = Turno.Noite, Inicio = 18, Fim = 22 },
            ],
        };
        Assert.Contains(day.Validate(), e => e.Contains("tarde", StringComparison.Ordinal));
    }

    [Fact]
    public void Tick_anda_a_hora_no_ritmo_e_avisa_o_fim_de_cada_turno()
    {
        GameState state = Career(41).Context.State;
        double day = Clock.SecondsPerDay(Time, Clock.ModoCampanha, 1);
        Assert.Equal((6, 0), Clock.TimeOfDay(Time, state));

        // Um quarto do dia em 1x: 4 horas de jogo, ainda de manhã.
        ClockTick first = Clock.Tick(Time, state, Clock.ModoCampanha, 1, day / 4);
        Assert.Empty(first.EndedTurns);
        Assert.False(first.DayEnded);
        Assert.Equal((10, 0), Clock.TimeOfDay(Time, state));
        Assert.Equal(Turno.Manha, Clock.CurrentTurno(Time, state));

        // Em 4x o mesmo tempo real anda quatro vezes mais: passa do meio-dia e das 18h, até o fim do dia.
        ClockTick second = Clock.Tick(Time, state, Clock.ModoCampanha, 4, day / 4);
        Assert.Equal([Turno.Manha, Turno.Tarde, Turno.Noite], second.EndedTurns);
        Assert.True(second.DayEnded);
        Assert.Equal((22, 0), Clock.TimeOfDay(Time, state));
        Assert.Null(Clock.CurrentTurno(Time, state));

        // Virar o dia volta o relógio para as 6h.
        Clock.AdvanceDay(state, () => { });
        Assert.Equal((6, 0), Clock.TimeOfDay(Time, state));
    }

    [Fact]
    public void Turno_que_ja_passou_nao_aceita_compromisso_e_o_em_curso_aceita()
    {
        CampaignEngine engine = Career(42);
        GameState state = engine.Context.State;
        string zone = engine.Territory.Zones[engine.Territory.HomeZone];
        Clock.Tick(Time, state, Clock.ModoCampanha, 1, Clock.SecondsPerDay(Time, Clock.ModoCampanha, 1) * 0.45);
        Assert.Equal(Turno.Tarde, Clock.CurrentTurno(Time, state));

        Assert.Equal(ScheduleFailure.TurnoPassou, engine.CanSchedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha)));
        Assert.Null(engine.CanSchedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Tarde)));
        Assert.Null(engine.CanSchedule(new CommitmentRequest("caminhada", zone, engine.Today.AddDays(1), Turno.Manha)));
        Assert.Equal((engine.Today, Turno.Tarde), engine.NextFreeSlot());
    }

    [Fact]
    public void Compromisso_acontece_no_fim_do_turno_e_o_dia_da_o_mesmo_que_de_uma_vez()
    {
        // Mesma semente e mesmos compromissos: o dia com o relógio correndo (cada compromisso no fim do turno) termina
        // no mesmo estado que o dia inteiro de uma vez na virada.
        CampaignEngine byClock = Career(43);
        CampaignEngine atOnce = Career(43);
        foreach (CampaignEngine engine in new[] { byClock, atOnce })
        {
            string zone = engine.Territory.Zones[engine.Territory.HomeZone];
            Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha)).Failure);
            Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Noite)).Failure);
        }

        GameState state = byClock.Context.State;
        double step = Clock.SecondsPerDay(Time, Clock.ModoCampanha, 1) / 40;
        var done = new List<CommitmentResult>();
        bool dayEnded = false;
        while (!dayEnded)
        {
            ClockTick tick = Clock.Tick(Time, state, Clock.ModoCampanha, 1, step);
            if (tick.EndedTurns.Count > 0)
            {
                List<CommitmentResult> results = byClock.RunDue();
                if (results.Count > 0)
                {
                    // A manhã acontece ao meio-dia, com a noite ainda marcada.
                    Assert.Equal(done.Count == 0 ? Turno.Manha : Turno.Noite, Assert.Single(results).Commitment.Turno);
                    Assert.Equal(done.Count == 0 ? 1 : 0, byClock.Agenda.Scheduled.Count);
                }

                done.AddRange(results);
            }

            dayEnded = tick.DayEnded;
        }

        Assert.Equal(2, done.Count);
        Assert.Empty(byClock.AdvanceDay());
        Assert.Equal(2, atOnce.AdvanceDay().Count);
        Assert.Equal(GameStateSerializer.Serialize(atOnce.Context.State), GameStateSerializer.Serialize(state));
    }

    [Fact]
    public void Linha_do_dia_mostra_marcado_em_andamento_feito_e_passou()
    {
        CampaignEngine engine = Career(44);
        GameState state = engine.Context.State;
        string zone = engine.Territory.Zones[engine.Territory.HomeZone];
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Noite)).Failure);

        IReadOnlyList<TurnoSlot> morning = engine.DayLine(engine.Today);
        Assert.Equal([TurnoSituacao.EmAndamento, TurnoSituacao.Livre, TurnoSituacao.Marcado], morning.Select(s => s.Situacao));
        Assert.Equal([6, 12, 18], morning.Select(s => s.HoraInicio));

        // Às 19h: a manhã foi feita, a tarde passou vazia, a noite está em andamento.
        Clock.Tick(Time, state, Clock.ModoCampanha, 1, Clock.SecondsPerDay(Time, Clock.ModoCampanha, 1) * 13 / 16);
        Assert.Single(engine.RunDue());
        IReadOnlyList<TurnoSlot> evening = engine.DayLine(engine.Today);
        Assert.Equal([TurnoSituacao.Feito, TurnoSituacao.Passou, TurnoSituacao.EmAndamento], evening.Select(s => s.Situacao));
        Assert.NotNull(evening[0].Result);

        // O dia seguinte começa todo livre.
        Assert.All(engine.DayLine(engine.Today.AddDays(1)), s => Assert.Equal(TurnoSituacao.Livre, s.Situacao));
    }

    [Fact]
    public void Save_guarda_a_hora_do_dia()
    {
        CampaignEngine engine = Career(48);
        GameState state = engine.Context.State;
        string zone = engine.Territory.Zones[engine.Territory.HomeZone];
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha)).Failure);
        Clock.Tick(Time, state, Clock.ModoCampanha, 1, Clock.SecondsPerDay(Time, Clock.ModoCampanha, 1) / 2);
        Assert.Single(engine.RunDue());
        GameState loaded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        Assert.Equal(state.Time.Minute, loaded.Time.Minute);
        Assert.Equal((14, 0), Clock.TimeOfDay(Time, loaded));
    }
}
