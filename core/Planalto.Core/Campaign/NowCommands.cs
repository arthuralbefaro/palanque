using Planalto.Core.State;

namespace Planalto.Core.Campaign;

/// <summary>
/// "Fazer agora" (RL): onde o compromisso cabe já. Hoje, no turno em curso ou num seguinte; sem nenhum hoje, o
/// primeiro livre de amanhã, marcado como amanhã (a tela pede a escolha explícita e nunca pula o dia sozinha).
/// </summary>
/// <param name="Failure">Motivo do bloqueio; null quando dá para fazer.</param>
/// <param name="Forecast">Previsão no dia e turno escolhidos (com o estresse do terceiro turno, se for o caso).</param>
public sealed record NowPlan(DateOnly Date, Turno Turno, bool Tomorrow, ScheduleFailure? Failure, CommitmentForecast? Forecast);

/// <summary>O que o "Fazer agora" fez: o compromisso marcado e os resultados (vazios se o dia não chegou nele).</summary>
public sealed record NowResult(NowPlan Plan, Commitment? Commitment, IReadOnlyList<CommitmentResult> Results);

public sealed partial class CampaignEngine
{
    /// <summary>Avisa a tela que o estado da campanha mudou (compromisso marcado ou feito, turno ou dia que passou).</summary>
    public event Action? Changed;

    /// <summary>Dispara <see cref="Changed"/>; a tela chama quando vira o dia fora do motor.</summary>
    public void NotifyChanged() => Changed?.Invoke();

    /// <summary>
    /// Onde um compromisso "agora" cabe (o dia e o turno do pedido são ignorados): o primeiro turno de hoje que não
    /// passou nem está ocupado; sem nenhum, o primeiro de amanhã. Outro bloqueio (dinheiro, fase, decisão na mesa)
    /// vem como <see cref="NowPlan.Failure"/>, com a previsão do turno.
    /// </summary>
    public NowPlan PlanNow(CommitmentRequest r)
    {
        foreach (DateOnly day in new[] { Today, Today.AddDays(1) })
        {
            foreach (Turno turno in Enum.GetValues<Turno>())
            {
                CommitmentRequest request = r with { Date = day, Turno = turno };
                ScheduleFailure? failure = CanSchedule(request);
                if (failure is ScheduleFailure.TurnoPassou or ScheduleFailure.TurnoOcupado)
                {
                    continue;
                }

                return new NowPlan(day, turno, day != Today, failure, Forecast(request));
            }
        }

        return new NowPlan(Today.AddDays(1), Turno.Noite, true, ScheduleFailure.TurnoOcupado, null);
    }

    /// <summary>
    /// "Fazer agora": marca o compromisso no turno de <see cref="PlanNow"/> e avança o relógio até o fim dele, quando
    /// ele acontece (o que estava marcado antes no dia acontece junto). Se o turno é amanhã, <paramref name="advanceDay"/>
    /// vira o dia (a tela passa o avanço da carreira inteira) e devolve false se o dia parou numa decisão: o
    /// compromisso fica marcado e acontece quando o tempo voltar a correr.
    /// </summary>
    public NowResult DoNow(CommitmentRequest r, Func<bool> advanceDay)
    {
        NowPlan plan = PlanNow(r);
        if (plan.Failure is not null || Time is null)
        {
            return new NowResult(plan, null, []);
        }

        var (failure, commitment) = Schedule(r with { Date = plan.Date, Turno = plan.Turno });
        if (failure is not null || commitment is null)
        {
            return new NowResult(plan with { Failure = failure }, null, []);
        }

        if (plan.Tomorrow && (!advanceDay() || Today != commitment.Date))
        {
            return new NowResult(plan, commitment, []);
        }

        Clock.SkipToEndOf(Time, State, LastTurnOf(commitment));
        return new NowResult(plan, commitment, RunDue());
    }
}
