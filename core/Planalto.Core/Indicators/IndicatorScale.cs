namespace Planalto.Core.Indicators;

/// <summary>
/// Conversão de ida e volta entre a unidade real de um indicador e a escala de jogo (GDD 17.17). Na escala de jogo, 0 é
/// o pior limite plausível e 100 o melhor; valor fora dos limites dá número abaixo de 0 ou acima de 100, sem corte,
/// para que a volta devolva o mesmo valor real. Os motores cortam ao ler, se precisarem.
/// </summary>
public static class IndicatorScale
{
    public const string Linear = "linear";
    public const string Log = "log";

    /// <summary>Da unidade real para a escala de jogo.</summary>
    public static double ToGame(RealIndicatorDefinition d, double real)
    {
        double fraction = d.Escala == Log
            ? Math.Log(Math.Max(real, double.Epsilon) / d.Piso) / Math.Log(d.Teto / d.Piso)
            : (real - d.Piso) / (d.Teto - d.Piso);
        return 100 * (d.MaiorMelhor ? fraction : 1 - fraction);
    }

    /// <summary>Da escala de jogo para a unidade real.</summary>
    public static double FromGame(RealIndicatorDefinition d, double game)
    {
        double fraction = d.MaiorMelhor ? game / 100 : 1 - (game / 100);
        return d.Escala == Log
            ? d.Piso * Math.Exp(fraction * Math.Log(d.Teto / d.Piso))
            : d.Piso + (fraction * (d.Teto - d.Piso));
    }

    /// <summary>
    /// Limites da evolução de um indicador que partiu de <paramref name="partida"/>: os limites plausíveis, alargados
    /// até o valor de partida quando o município já começa fora deles, e nunca além da faixa de conferência.
    /// </summary>
    public static (double Min, double Max) Bounds(RealIndicatorDefinition d, double partida) =>
        (Math.Max((double)d.Min, Math.Min(d.Piso, partida)), Math.Min((double)d.Max, Math.Max(d.Teto, partida)));
}
