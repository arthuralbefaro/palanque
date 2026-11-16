using Planalto.Core.Economy;
using Planalto.Core.State;

namespace Planalto.Core.Indicators;

/// <summary>
/// Pressão sobre o alvo de um indicador (GDD 17.17): efeito de orçamento, política pública, obra entregue ou crise,
/// em pontos da escala de jogo, por um prazo ou sem prazo. É o ponto de extensão das ações da prefeitura.
/// </summary>
public sealed class IndicatorPressure
{
    public required int Id { get; init; }

    public required string IndicatorId { get; init; }

    /// <summary>Pontos somados ao alvo enquanto a pressão vale; positivo melhora, negativo piora.</summary>
    public required double Pontos { get; set; }

    public required DateOnly Desde { get; init; }

    /// <summary>Último dia em que vale; null = sem prazo (até ser retirada).</summary>
    public DateOnly? Ate { get; init; }

    /// <summary>Chave de quem aplicou ("orcamento", "politica:saude_familia", "obra:12"), para retirar e para explicar.</summary>
    public required string Origem { get; init; }

    /// <summary>Texto para a explicação ("Programa Saúde da Família").</summary>
    public required string Texto { get; set; }

    public bool ActiveOn(DateOnly date) => Desde <= date && (Ate is null || date <= Ate);
}

/// <summary>Um fator da variação de um indicador: quantos pontos de jogo e quanto na unidade real ele respondeu.</summary>
public sealed record IndicatorFactor(string Tipo, string Origem, string Texto, double Pontos, double Delta)
{
    public const string Tendencia = "tendencia";
    public const string Pressao = "pressao";
    public const string Ciclo = "ciclo_economico";
    public const string Vizinho = "indicador_vizinho";
    public const string Choque = "choque";

    /// <summary>Inércia: sem nada empurrando, o indicador volta devagar ao patamar de partida.</summary>
    public const string Retorno = "retorno_ao_patamar";

    /// <summary>Parte da variação cortada pelo limite plausível.</summary>
    public const string Limite = "limite_plausivel";
}

/// <summary>Variação de um indicador num período, com os fatores que a explicam.</summary>
public sealed class IndicatorVariation
{
    public required DateOnly De { get; init; }

    public required DateOnly Ate { get; set; }

    public required double Antes { get; init; }

    public required double Depois { get; set; }

    public List<IndicatorFactor> Fatores { get; init; } = [];

    /// <summary>Soma um fator, juntando com o de mesmo tipo e origem.</summary>
    public void Add(IndicatorFactor factor)
    {
        int index = Fatores.FindIndex(f => f.Tipo == factor.Tipo && f.Origem == factor.Origem);
        if (index < 0)
        {
            Fatores.Add(factor);
        }
        else
        {
            IndicatorFactor old = Fatores[index];
            Fatores[index] = old with { Pontos = old.Pontos + factor.Pontos, Delta = old.Delta + factor.Delta };
        }
    }
}

/// <summary>Um indicador do município em foco, na unidade real (GDD 17.17).</summary>
public sealed class IndicatorValue
{
    public required string Id { get; init; }

    /// <summary>Valor de hoje, na unidade real.</summary>
    public required double Valor { get; set; }

    /// <summary>Valor de partida: o dado importado ou, na lacuna, o preenchimento da regra.</summary>
    public required double Partida { get; init; }

    /// <summary>True quando a partida não é dado do próprio município (substituição do importador ou lacuna preenchida).</summary>
    public required bool Estimativa { get; init; }

    /// <summary>Regra que deu a estimativa; null quando é dado do município.</summary>
    public string? RegraEstimativa { get; init; }

    /// <summary>Ano do dado de partida (0 no índice do jogo).</summary>
    public required int AnoFonte { get; init; }

    /// <summary>Inclinação inicial, em pontos de jogo por ano; perde força com o tempo.</summary>
    public required double TendenciaAnual { get; init; }

    /// <summary>Valor no início do mandato em curso, para a variação que o eleitor julga.</summary>
    public required double InicioMandato { get; set; }

    /// <summary>Referência de comparação: mediana dos municípios do mesmo porte na UF, evoluída só pela tendência e pelo ciclo.</summary>
    public required double Referencia { get; set; }

    public required double ReferenciaPartida { get; init; }

    /// <summary>Quantos municípios entraram na mediana de referência.</summary>
    public required int ReferenciaMunicipios { get; init; }

    /// <summary>A semana mais recente.</summary>
    public IndicatorVariation? UltimaVariacao { get; set; }

    /// <summary>Acumulado do ano em curso.</summary>
    public IndicatorVariation? VariacaoNoAno { get; set; }

    /// <summary>Acumulado do último ano fechado.</summary>
    public IndicatorVariation? VariacaoAnoAnterior { get; set; }
}

/// <summary>
/// Indicadores do município em foco no save (GDD 17.17): valores em unidade real, pressões em vigor, ciclo econômico,
/// população e o gerador próprio, para a evolução do mundo não mexer na sequência de sorteios da campanha.
/// </summary>
public sealed class MunicipalIndicatorsState
{
    public required int Municipio { get; init; }

    public required string Uf { get; init; }

    public required string Porte { get; init; }

    /// <summary>Primeiro dia da simulação: os valores de partida valem até aqui.</summary>
    public required DateOnly Inicio { get; init; }

    /// <summary>Segunda-feira da última semana simulada.</summary>
    public required DateOnly UltimoPasso { get; set; }

    public required DateOnly MandatoDesde { get; set; }

    public required long Populacao { get; set; }

    public required long PopulacaoPartida { get; init; }

    public required Rng Rng { get; init; }

    public required EconomyCycleState Ciclo { get; init; }

    public required List<IndicatorValue> Indicadores { get; init; }

    public List<IndicatorPressure> Pressoes { get; init; } = [];

    public int NextPressureId { get; set; } = 1;

    public IndicatorValue Get(string id) => Indicadores.First(i => i.Id == id);

    public IndicatorValue? Find(string id) => Indicadores.FirstOrDefault(i => i.Id == id);
}
