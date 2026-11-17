using Planalto.Core.Indicators;

namespace Planalto.Core.Executive;

/// <summary>
/// Ligação da prefeitura com os indicadores em unidade real (GDD 17.17). Com <see cref="ExecutiveContext.IndicatorEvolution"/>
/// e <see cref="State.GameState.Indicators"/>, o orçamento, as políticas e as crises viram pressões e choques nos
/// indicadores reais, e <see cref="ExecutiveState.Indicators"/> passa a ser a escala de jogo derivada deles (a nota
/// que o eleitor julga: variação no mandato e comparação com os semelhantes). Sem isso, vale a regra da D-031.
/// </summary>
public sealed partial class ExecutiveEngine
{
    /// <summary>Origem das pressões do orçamento ("orcamento:{indicador da prefeitura}").</summary>
    public const string BudgetOrigin = "orcamento:";

    /// <summary>Origem das pressões das políticas em vigor ("politica:{id}:{indicador da prefeitura}").</summary>
    public const string PolicyOrigin = "politica:";

    public const string CrisisOrigin = "crise";

    /// <summary>True quando os indicadores da prefeitura são derivados dos valores reais do save.</summary>
    public bool UsesRealIndicators => Real is not null;

    private MunicipalIndicatorsState? Real => RealFor(Context, Executive);

    private static MunicipalIndicatorsState? RealFor(ExecutiveContext ctx, ExecutiveState executive) =>
        ctx.IndicatorEvolution is not null && ctx.State.Indicators is { } real && real.Municipio == executive.Municipio ? real : null;

    /// <summary>Posse: a escala de jogo de partida é a derivada dos valores reais, não mais 50 ± 8.</summary>
    private static void LinkRealIndicators(ExecutiveContext ctx, ExecutiveState executive)
    {
        if (RealFor(ctx, executive) is { } real)
        {
            ReadScores(ctx, real, executive);
        }
    }

    private static void ReadScores(ExecutiveContext ctx, MunicipalIndicatorsState real, ExecutiveState executive)
    {
        IndicatorContent content = ctx.IndicatorEvolution!.Content;
        foreach (IndicatorDefinition indicator in ctx.Config.Indicators)
        {
            if (IndicatorQueries.GameScore(real, content.Definitions, content.Model, indicator.Id) is double score)
            {
                executive.Indicators[indicator.Id] = Math.Clamp(score, ctx.Config.IndicatorBounds.Min, ctx.Config.IndicatorBounds.Max);
            }
        }
    }

    /// <summary>
    /// Semana dos indicadores reais: o orçamento e as políticas em vigor viram pressões, os indicadores andam até a
    /// semana e a escala de jogo é relida. Devolve false quando o estado não tem indicadores reais.
    /// </summary>
    private bool StepRealIndicators()
    {
        if (Real is not { } real)
        {
            return false;
        }

        IndicatorEvolution evolution = Context.IndicatorEvolution!;
        ExecutiveState e = Executive;
        if (real.MandatoDesde != e.Start)
        {
            // Primeiro passo deste mandato: a base da variação é o valor da posse, e as pressões do governo anterior saem.
            evolution.AdvanceTo(real, Week.AddDays(-7));
            IndicatorEvolution.MarkMandateStart(real, e.Start);
            real.Pressoes.RemoveAll(p => p.Ate is null && (p.Origem.StartsWith(BudgetOrigin, StringComparison.Ordinal) || p.Origem.StartsWith(PolicyOrigin, StringComparison.Ordinal)));
        }

        Dictionary<string, double> budget = BudgetPressure(e.Allocation);
        foreach (IndicatorDefinition indicator in Config.Indicators)
        {
            foreach (string realId in evolution.Components(indicator.Id))
            {
                evolution.SetStandingPressure(real, realId, budget.GetValueOrDefault(indicator.Id), Week, BudgetOrigin + indicator.Id, indicator.Name);
            }
        }

        var active = new HashSet<string>(StringComparer.Ordinal);
        foreach (EnactedPolicy p in e.Policies.Where(p => p.Status == PolicyStatus.Ativa))
        {
            PolicyDefinition policy = Context.Content.Policies.Find(p.PolicyId)!;
            foreach (var (indicator, boost) in policy.Boosts)
            {
                string origin = $"{PolicyOrigin}{policy.Id}:{indicator}";
                active.Add(origin);
                foreach (string realId in evolution.Components(indicator))
                {
                    evolution.SetStandingPressure(real, realId, boost, Week, origin, policy.Name);
                }
            }
        }

        real.Pressoes.RemoveAll(p => p.Ate is null && p.Origem.StartsWith(PolicyOrigin, StringComparison.Ordinal) && !active.Contains(p.Origem));
        evolution.AdvanceTo(real, Week);

        // Indicador da prefeitura sem mapa para os reais segue a regra antiga.
        Dictionary<string, double>? targets = null;
        foreach (IndicatorDefinition indicator in Config.Indicators.Where(i => !evolution.Components(i.Id).Any()))
        {
            targets ??= Targets();
            e.Indicators[indicator.Id] += Config.IndicatorSpeedPerWeek * (targets[indicator.Id] - e.Indicators[indicator.Id]);
        }

        ReadScores(Context, real, e);
        return true;
    }

    /// <summary>
    /// Pressão do orçamento em cada indicador da prefeitura (pontos da escala de jogo): gasto relativo à referência e
    /// reserva não usada. A previsão do rascunho usa a mesma conta com a repartição nova.
    /// </summary>
    private Dictionary<string, double> BudgetPressure(IReadOnlyDictionary<string, double> allocation)
    {
        double freeReserve = allocation.GetValueOrDefault(ExecutiveConfig.ReserveArea) - PolicyCost;
        var budget = Config.Indicators.ToDictionary(i => i.Id, _ => 0.0, StringComparer.Ordinal);
        foreach (BudgetArea area in Config.Areas.Where(a => a.ReferenceShare > 0))
        {
            double relative = (allocation.GetValueOrDefault(area.Id) / area.ReferenceShare) - 1;
            foreach (var (indicator, weight) in area.Effects)
            {
                budget[indicator] = budget.GetValueOrDefault(indicator) + (weight * Config.TargetSensitivity * relative);
            }
        }

        if (budget.ContainsKey("contas"))
        {
            budget["contas"] += (Config.UnusedReserveToAccounts * Math.Max(0, freeReserve)) - (Config.UnusedReserveToAccounts * Math.Max(0, -freeReserve) * 2);
        }

        return budget;
    }

    /// <summary>Choque de crise nos indicadores reais; false quando não há indicadores reais ou o indicador não tem mapa.</summary>
    private bool ShiftReal(string indicator, double delta)
    {
        if (Real is not { } real || !Context.IndicatorEvolution!.Components(indicator).Any())
        {
            return false;
        }

        Context.IndicatorEvolution.ApplyGameShock(real, indicator, delta, Week, CrisisOrigin, Config.Indicators.FirstOrDefault(i => i.Id == indicator)?.Name ?? indicator);
        ReadScores(Context, real, Executive);
        return true;
    }
}
