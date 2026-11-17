using Planalto.Core.State;

namespace Planalto.Core.Campaign;

/// <summary>
/// Painel "Você" (RL): o que limita o jogador agora. Turnos, estresse com o limite que derruba os testes, dinheiro
/// com o que sai toda segunda-feira e semanas até a eleição. Tudo do estado e das regras; a tela só escreve.
/// </summary>
public sealed record YouStatus(
    IReadOnlyList<TurnoSlot> Today,
    int FreeToday,
    int FreeTodayWithoutStress,
    int UsedToday,
    int FreeWeek,
    int WeekTurns,
    int TurnsWithoutCost,
    int ExtraTurnStress,
    int Stress,
    int StressLimit,
    double StressPenalty,
    IReadOnlyList<string> TestedTypes,
    decimal Money,
    decimal WeeklyOutflow,
    decimal Payroll,
    decimal BudgetPlan,
    double WeeksOfCash,
    decimal CheapestCommitment,
    int WeeksToElection,
    int DaysToElection,
    DateOnly ElectionDate,
    DateOnly CampaignStart,
    CampaignPhase Phase);

/// <summary>Painel Você (parte RL): só lê o estado.</summary>
public sealed partial class CampaignEngine
{
    /// <summary>O que limita o jogador agora: turnos, estresse, dinheiro e prazo (painel Você).</summary>
    public YouStatus You()
    {
        IReadOnlyList<TurnoSlot> today = DayLine(Today);
        int freeToday = today.Count(s => s.Situacao == TurnoSituacao.Livre);
        int usedToday = today.Count(s => s.Situacao is TurnoSituacao.Marcado or TurnoSituacao.EmAndamento or TurnoSituacao.Feito);
        int withoutStress = Math.Min(freeToday, Math.Max(0, Rules.TurnosSemCusto - usedToday));
        int freeWeek = freeToday;
        for (DateOnly d = Today.AddDays(1); d <= State.Calendar.WeekEnd; d = d.AddDays(1))
        {
            freeWeek += DayLine(d).Count(s => s.Situacao == TurnoSituacao.Livre);
        }

        BudgetView budget = BudgetSummary();
        decimal cheapest = Commitments.Tipos.Where(t => t.AllowedIn(Campaign.Phase)).Select(CostOf).Where(c => c > 0).DefaultIfEmpty(0).Min();
        return new YouStatus(
            today,
            freeToday,
            withoutStress,
            usedToday,
            freeWeek,
            7 * today.Count,
            Rules.TurnosSemCusto,
            Rules.EstresseTurnoExtra,
            State.Player.Resources.Stress,
            Rules.EstresseLimite,
            Rules.EstressePenalidade,
            Commitments.Tipos.Where(t => t.Teste is not null).Select(t => t.Nome).ToList(),
            State.Player.Resources.Money,
            budget.PlanWeekly + budget.PayrollWeekly,
            budget.PayrollWeekly,
            budget.PlanWeekly,
            budget.WeeksOfCash,
            cheapest,
            WeeksToElection,
            Math.Max(0, Campaign.ElectionDate.DayNumber - Today.DayNumber),
            Campaign.ElectionDate,
            Campaign.CampaignStart,
            Campaign.Phase);
    }
}
