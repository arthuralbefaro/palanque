using Planalto.Core.Data;

namespace Planalto.Core.Indicators;

/// <summary>Parâmetros da evolução de um indicador (data/indicators/model.json). Pontos são da escala de jogo.</summary>
public sealed class IndicatorModelEntry
{
    public const string MedianaPorteUf = "mediana_porte_uf";
    public const string ValorFixo = "valor_fixo";

    /// <summary>Inércia: fração da distância até o alvo percorrida por semana.</summary>
    public required double VelocidadeSemanal { get; init; }

    /// <summary>Inclinação inicial, em pontos por ano, quando não há série publicada de onde tirá-la.</summary>
    public required double TendenciaAnual { get; init; }

    /// <summary>Teto, em módulo, da inclinação inicial tirada da série (pontos por ano).</summary>
    public required double TendenciaMaxAnual { get; init; }

    /// <summary>Chance semanal de um choque sorteado.</summary>
    public required double ChoqueChanceSemanal { get; init; }

    /// <summary>Desvio-padrão do choque, em pontos.</summary>
    public required double ChoqueDesvio { get; init; }

    /// <summary>Pontos no alvo por ponto percentual do hiato do ciclo econômico.</summary>
    public required double SensibilidadeCiclo { get; init; }

    /// <summary>Indicadores vizinhos: pontos no alvo por ponto de variação do vizinho desde a partida.</summary>
    public Dictionary<string, double> Vizinhos { get; init; } = [];

    /// <summary>Regra de preenchimento da lacuna: "mediana_porte_uf" ou "valor_fixo".</summary>
    public required string Preenchimento { get; init; }

    /// <summary>Valor de partida na unidade real quando a regra é "valor_fixo".</summary>
    public double? Valor { get; init; }
}

/// <summary>Como o eleitor julga (GDD 17.17 e 17.20, item 20): pela variação no mandato e pela comparação, não pelo nível.</summary>
public sealed class IndicatorJudgement
{
    /// <summary>Nota de quem não melhorou nem piorou e está igual aos semelhantes.</summary>
    public required double Neutro { get; init; }

    /// <summary>Pontos na nota por ponto de variação desde o início do mandato.</summary>
    public required double PesoVariacao { get; init; }

    /// <summary>Pontos na nota por ponto de diferença para os municípios do mesmo porte na UF.</summary>
    public required double PesoComparacao { get; init; }
}

/// <summary>
/// data/indicators/model.json: modelo de evolução (GDD 17.17). valor(t+1) = valor(t) + velocidade × (alvo − valor) +
/// choque, limitado aos limites plausíveis; alvo = partida + tendência + pressões (orçamento, políticas, obras) +
/// contexto (ciclo econômico e indicadores vizinhos). A conta é feita na escala de jogo; o save guarda a unidade real.
/// </summary>
public sealed class IndicatorModelConfig : IValidatable
{
    public const string File = "indicators/model.json";

    public required string Note { get; init; }

    /// <summary>Anos até a inclinação inicial perder toda a força.</summary>
    public required int AnosTendencia { get; init; }

    /// <summary>Mínimo de municípios do mesmo porte na UF para a mediana valer; abaixo dele, a UF inteira.</summary>
    public required int MinMunicipiosReferencia { get; init; }

    public required IndicatorJudgement Julgamento { get; init; }

    /// <summary>Indicador real → parâmetros.</summary>
    public required Dictionary<string, IndicatorModelEntry> Indicadores { get; init; }

    /// <summary>Indicador da prefeitura (D-031) → indicadores reais que o compõem, com peso.</summary>
    public required Dictionary<string, Dictionary<string, double>> Jogo { get; init; }

    public IndicatorModelEntry Of(string id) => Indicadores[id];

    public IEnumerable<string> Validate()
    {
        if (AnosTendencia < 1 || MinMunicipiosReferencia < 1)
        {
            yield return "anos_tendencia e min_municipios_referencia devem ser positivos.";
        }

        if (Julgamento is null || Julgamento.PesoVariacao < 0 || Julgamento.PesoComparacao < 0)
        {
            yield return "julgamento: pesos não negativos.";
        }

        foreach (string id in IndicatorDefinitions.Ids.Where(id => !Indicadores.ContainsKey(id)))
        {
            yield return $"indicadores: falta '{id}'.";
        }

        foreach (var (id, e) in Indicadores)
        {
            if (!IndicatorDefinitions.Ids.Contains(id))
            {
                yield return $"indicadores: '{id}' não existe em definitions.json.";
            }

            if (!(e.VelocidadeSemanal > 0 && e.VelocidadeSemanal <= 1))
            {
                yield return $"indicadores.{id}: velocidade_semanal maior que 0 e até 1.";
            }

            if (e.TendenciaMaxAnual < 0 || Math.Abs(e.TendenciaAnual) > e.TendenciaMaxAnual)
            {
                yield return $"indicadores.{id}: tendencia_anual dentro de tendencia_max_anual.";
            }

            if (e.ChoqueChanceSemanal is < 0 or > 1 || e.ChoqueDesvio < 0)
            {
                yield return $"indicadores.{id}: choque_chance_semanal entre 0 e 1 e choque_desvio não negativo.";
            }

            if (e.Preenchimento is not (IndicatorModelEntry.MedianaPorteUf or IndicatorModelEntry.ValorFixo))
            {
                yield return $"indicadores.{id}: preenchimento '{e.Preenchimento}' desconhecido.";
            }

            if (e.Preenchimento == IndicatorModelEntry.ValorFixo && e.Valor is null)
            {
                yield return $"indicadores.{id}: valor_fixo exige 'valor'.";
            }

            foreach (string v in e.Vizinhos.Keys.Where(v => v == id || !IndicatorDefinitions.Ids.Contains(v)))
            {
                yield return $"indicadores.{id}: vizinho '{v}' inválido.";
            }
        }

        foreach (var (game, parts) in Jogo)
        {
            if (parts.Count == 0 || parts.Values.Any(w => w <= 0))
            {
                yield return $"jogo.{game}: ao menos um indicador real, com peso positivo.";
            }

            foreach (string id in parts.Keys.Where(id => !IndicatorDefinitions.Ids.Contains(id)))
            {
                yield return $"jogo.{game}: indicador real '{id}' não existe.";
            }
        }
    }
}

/// <summary>Conteúdo dos indicadores: definições, modelo de evolução e ciclo econômico.</summary>
public sealed class IndicatorContent
{
    public required IndicatorDefinitions Definitions { get; init; }

    public required IndicatorModelConfig Model { get; init; }

    public required Economy.EconomyCycleConfig Cycle { get; init; }

    /// <summary>Números nacionais de partida; dão o hiato inicial do ciclo.</summary>
    public Economy.EconomyStart? EconomyStart { get; init; }

    public static IndicatorContent LoadOrThrow(string dataDirectory) => new()
    {
        Definitions = JsonData.LoadFile<IndicatorDefinitions>(Path.Combine(dataDirectory, IndicatorDefinitions.File)).GetValueOrThrow(),
        Model = JsonData.LoadFile<IndicatorModelConfig>(Path.Combine(dataDirectory, IndicatorModelConfig.File)).GetValueOrThrow(),
        Cycle = JsonData.LoadFile<Economy.EconomyCycleConfig>(Path.Combine(dataDirectory, Economy.EconomyCycleConfig.File)).GetValueOrThrow(),
        EconomyStart = JsonData.LoadFile<Economy.EconomyStart>(Path.Combine(dataDirectory, Economy.EconomyStart.File)).Value,
    };

    /// <summary>Relatórios do DataValidator para os arquivos do modelo (as definições já são conferidas com os dados reais).</summary>
    public static IEnumerable<DataFileReport> Validate(string dataDirectory)
    {
        DataLoadResult<IndicatorModelConfig> model = JsonData.LoadFile<IndicatorModelConfig>(Path.Combine(dataDirectory, IndicatorModelConfig.File));
        yield return new DataFileReport(IndicatorModelConfig.File, model.Value?.Indicadores.Count ?? 0, model.Errors);
        DataLoadResult<Economy.EconomyCycleConfig> cycle = JsonData.LoadFile<Economy.EconomyCycleConfig>(Path.Combine(dataDirectory, Economy.EconomyCycleConfig.File));
        yield return new DataFileReport(Economy.EconomyCycleConfig.File, cycle.Value is null ? 0 : 1, cycle.Errors);
    }
}
