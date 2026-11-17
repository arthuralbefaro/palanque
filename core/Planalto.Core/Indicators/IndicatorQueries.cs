namespace Planalto.Core.Indicators;

/// <summary>Um indicador como a interface mostra: valor na unidade real, unidade, fonte e ano do dado de partida.</summary>
/// <param name="Simulado">False enquanto o valor ainda é o de partida; depois, "a partir daí, simulado".</param>
/// <param name="Jogo">O mesmo valor na escala de jogo (0 = pior limite, 100 = melhor).</param>
public sealed record IndicatorReading(
    string Id, string Nome, double Valor, string Unidade, string Fonte, int AnoFonte, bool Estimativa, string? RegraEstimativa,
    bool MaiorMelhor, double Partida, bool Simulado, double Jogo);

/// <summary>Variação entre dois momentos; <paramref name="Melhorou"/> é null quando não mudou.</summary>
public sealed record IndicatorChange(DateOnly Desde, double Antes, double Agora, double Delta, bool? Melhorou);

/// <summary>Comparação com a mediana dos municípios do mesmo porte na UF.</summary>
public sealed record IndicatorComparison(string Uf, string Porte, int Municipios, double Valor, double Referencia, double Diferenca, bool? Melhor);

/// <summary>
/// Consultas de leitura para a interface (GDD 17.17): valor atual em unidade real com unidade e fonte, variação no
/// mandato, comparação com a UF e os fatores da última variação. Não mudam o estado.
/// </summary>
public static class IndicatorQueries
{
    /// <summary>Diferenças menores que isto, na escala de jogo, contam como "igual".</summary>
    private const double Tie = 1e-9;

    public static IndicatorReading Current(MunicipalIndicatorsState state, IndicatorDefinitions definitions, string id)
    {
        RealIndicatorDefinition d = definitions.Get(id);
        IndicatorValue v = state.Get(id);
        return new IndicatorReading(
            id, d.Nome, v.Valor, d.Unidade, d.Fonte, v.AnoFonte, v.Estimativa, v.RegraEstimativa, d.MaiorMelhor, v.Partida,
            state.UltimoPasso > State.Calendar.ForDate(state.Inicio).WeekStart || v.Valor != v.Partida, IndicatorScale.ToGame(d, v.Valor));
    }

    public static List<IndicatorReading> All(MunicipalIndicatorsState state, IndicatorDefinitions definitions) =>
        state.Indicadores.Select(v => Current(state, definitions, v.Id)).ToList();

    /// <summary>Variação desde o início do mandato em curso.</summary>
    public static IndicatorChange MandateChange(MunicipalIndicatorsState state, IndicatorDefinitions definitions, string id)
    {
        IndicatorValue v = state.Get(id);
        return Change(definitions.Get(id), state.MandatoDesde, v.InicioMandato, v.Valor);
    }

    /// <summary>Variação desde o dado de partida.</summary>
    public static IndicatorChange SinceStart(MunicipalIndicatorsState state, IndicatorDefinitions definitions, string id)
    {
        IndicatorValue v = state.Get(id);
        return Change(definitions.Get(id), state.Inicio, v.Partida, v.Valor);
    }

    /// <summary>Comparação com os municípios do mesmo porte na UF (a UF inteira quando são poucos).</summary>
    public static IndicatorComparison CompareUf(MunicipalIndicatorsState state, IndicatorDefinitions definitions, string id)
    {
        RealIndicatorDefinition d = definitions.Get(id);
        IndicatorValue v = state.Get(id);
        double diff = IndicatorScale.ToGame(d, v.Valor) - IndicatorScale.ToGame(d, v.Referencia);
        return new IndicatorComparison(state.Uf, state.Porte, v.ReferenciaMunicipios, v.Valor, v.Referencia, v.Valor - v.Referencia, Math.Abs(diff) < Tie ? null : diff > 0);
    }

    /// <summary>Fatores da última variação (a semana mais recente ou o último choque), do maior para o menor.</summary>
    public static IndicatorVariation? LastChange(MunicipalIndicatorsState state, string id) => Sorted(state.Get(id).UltimaVariacao);

    /// <summary>Fatores acumulados no ano em curso.</summary>
    public static IndicatorVariation? YearChange(MunicipalIndicatorsState state, string id) => Sorted(state.Get(id).VariacaoNoAno);

    /// <summary>Fatores acumulados no último ano fechado.</summary>
    public static IndicatorVariation? PreviousYearChange(MunicipalIndicatorsState state, string id) => Sorted(state.Get(id).VariacaoAnoAnterior);

    /// <summary>
    /// Nota de um indicador real como o eleitor julga (GDD 17.20, item 20): neutro, mais a variação desde o início do
    /// mandato e a diferença para os municípios semelhantes, cada uma com o seu peso. Não é o nível absoluto.
    /// </summary>
    public static double Judged(MunicipalIndicatorsState state, IndicatorDefinitions definitions, IndicatorModelConfig model, string id)
    {
        RealIndicatorDefinition d = definitions.Get(id);
        IndicatorValue v = state.Get(id);
        double now = IndicatorScale.ToGame(d, v.Valor);
        return model.Julgamento.Neutro
            + (model.Julgamento.PesoVariacao * (now - IndicatorScale.ToGame(d, v.InicioMandato)))
            + (model.Julgamento.PesoComparacao * (now - IndicatorScale.ToGame(d, v.Referencia)));
    }

    /// <summary>
    /// Escala de jogo de um indicador da prefeitura (saúde, educação…), derivada dos valores reais: média das notas
    /// dos indicadores reais que o compõem, com os pesos do modelo. Null se o indicador não tem mapa.
    /// </summary>
    public static double? GameScore(MunicipalIndicatorsState state, IndicatorDefinitions definitions, IndicatorModelConfig model, string gameIndicatorId)
    {
        if (!model.Jogo.TryGetValue(gameIndicatorId, out Dictionary<string, double>? parts))
        {
            return null;
        }

        return parts.Sum(p => p.Value * Judged(state, definitions, model, p.Key)) / parts.Values.Sum();
    }

    private static IndicatorChange Change(RealIndicatorDefinition d, DateOnly since, double before, double now)
    {
        double points = IndicatorScale.ToGame(d, now) - IndicatorScale.ToGame(d, before);
        return new IndicatorChange(since, before, now, now - before, Math.Abs(points) < Tie ? null : points > 0);
    }

    private static IndicatorVariation? Sorted(IndicatorVariation? variation)
    {
        if (variation is null)
        {
            return null;
        }

        var copy = new IndicatorVariation { De = variation.De, Ate = variation.Ate, Antes = variation.Antes, Depois = variation.Depois };
        copy.Fatores.AddRange(variation.Fatores.OrderByDescending(f => Math.Abs(f.Delta)).ThenBy(f => f.Tipo, StringComparer.Ordinal).ThenBy(f => f.Origem, StringComparer.Ordinal));
        return copy;
    }
}
