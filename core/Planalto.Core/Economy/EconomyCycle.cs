using Planalto.Core.Data;
using Planalto.Core.State;

namespace Planalto.Core.Economy;

/// <summary>
/// data/economy/cycle.json: ciclo econômico simples da R4 (GDD 17.17). Crescimento = tendência + componente cíclico
/// autorregressivo + choque; o hiato (crescimento menos a tendência, em pontos percentuais) move emprego, receita e o
/// humor do eleitor pelos pesos de data/indicators/model.json. O modelo nacional completo é da Fase 7.
/// </summary>
public sealed class EconomyCycleConfig : IValidatable
{
    public const string File = "economy/cycle.json";

    public required string Note { get; init; }

    /// <summary>Crescimento anual de tendência do PIB, em %.</summary>
    public required double CrescimentoTendencia { get; init; }

    /// <summary>Fração do hiato que sobrevive de um ano para o outro (0 a 1).</summary>
    public required double PersistenciaAnual { get; init; }

    /// <summary>Desvio-padrão do hiato no longo prazo, em pontos percentuais.</summary>
    public required double DesvioHiato { get; init; }

    /// <summary>Limite do hiato, em módulo.</summary>
    public required double HiatoMaximo { get; init; }

    public IEnumerable<string> Validate()
    {
        if (!(PersistenciaAnual > 0 && PersistenciaAnual < 1))
        {
            yield return "persistencia_anual maior que 0 e menor que 1.";
        }

        if (DesvioHiato < 0 || HiatoMaximo <= 0)
        {
            yield return "desvio_hiato não negativo e hiato_maximo positivo.";
        }
    }
}

/// <summary>Estado do ciclo no save.</summary>
public sealed class EconomyCycleState
{
    /// <summary>Crescimento menos a tendência, em pontos percentuais.</summary>
    public double Hiato { get; set; }
}

public static class EconomyCycle
{
    private const int WeeksPerYear = 52;

    /// <summary>Hiato de partida: o crescimento oficial mais recente menos a tendência, dentro do limite.</summary>
    public static EconomyCycleState Start(EconomyCycleConfig config, EconomyStart? start) => new()
    {
        Hiato = start is null ? 0 : Math.Clamp((double)start.PibVariacao - config.CrescimentoTendencia, -config.HiatoMaximo, config.HiatoMaximo),
    };

    /// <summary>Uma semana do ciclo: o hiato perde força e recebe um choque sorteado.</summary>
    public static void StepWeek(EconomyCycleState state, EconomyCycleConfig config, Rng rng)
    {
        double rho = Math.Pow(config.PersistenciaAnual, 1.0 / WeeksPerYear);
        double sigma = config.DesvioHiato * Math.Sqrt(1 - (rho * rho));
        state.Hiato = Math.Clamp((rho * state.Hiato) + (sigma * Normal(rng)), -config.HiatoMaximo, config.HiatoMaximo);
    }

    /// <summary>Crescimento anualizado do momento, em %.</summary>
    public static double Growth(EconomyCycleState state, EconomyCycleConfig config) => config.CrescimentoTendencia + state.Hiato;

    /// <summary>Normal padrão pelo Rng do Core (Box–Muller).</summary>
    public static double Normal(Rng rng)
    {
        double u1 = 1 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
