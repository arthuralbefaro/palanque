using Planalto.Core.Characters;
using Planalto.Core.Data;

namespace Planalto.Core.Territory;

/// <summary>
/// Uma demanda possível de zona: a carência medida que mais se destaca em relação à cidade. Pesa no tema dos
/// eleitores dali e é o assunto que lideranças levantam (GDD 17.5).
/// </summary>
public sealed class ZoneDemandRule
{
    public const string RendaBaixa = "renda_baixa";
    public const string Idosos = "idosos";
    public const string Jovens = "jovens";
    public const string Rural = "rural";
    public const string Padrao = "padrao";

    public required string Id { get; init; }

    /// <summary>Nome curto ("Emprego e renda").</summary>
    public required string Nome { get; init; }

    /// <summary>Como medir: renda_baixa, idosos, jovens, rural ou padrao (vale quando nenhuma outra se destaca).</summary>
    public required string Regra { get; init; }

    /// <summary>Tema dos eleitores que a demanda reforça.</summary>
    public required string Tema { get; init; }

    /// <summary>Frase com {valor} e {cidade}, na unidade real ("Renda média do responsável: {valor} (cidade: {cidade})").</summary>
    public required string Texto { get; init; }
}

public sealed class TerritorySimulation
{
    /// <summary>Pontos de presença perdidos por semana em zona sem compromisso do candidato.</summary>
    public required double DecaimentoSemanal { get; init; }

    /// <summary>Fração de cada ganho de presença que transborda, repartida entre as zonas vizinhas.</summary>
    public required double Transbordo { get; init; }

    /// <summary>Quanto a razão entre a renda da zona e a da cidade desloca as faixas de renda (estimativa de jogo).</summary>
    public required double RendaExpoente { get; init; }

    /// <summary>Peso somado à prioridade do tema da demanda entre os eleitores da zona (antes de normalizar).</summary>
    public required double DemandaPeso { get; init; }

    /// <summary>Quantas vezes a presença inicial na zona natal do jogador é maior que nas outras.</summary>
    public required double ZonaNatalFator { get; init; }

    /// <summary>Quantas vezes a presença inicial de um rival no reduto é maior que nas outras zonas.</summary>
    public required double RedutoFator { get; init; }

    /// <summary>Diferença mínima, em pontos percentuais, da fatia de idosos ou de jovens para virar a demanda da zona.</summary>
    public required double IdadeDestaque { get; init; }

    /// <summary>Razão máxima entre a renda da zona e a da cidade para "renda baixa" virar a demanda.</summary>
    public required double RendaBaixaRazao { get; init; }

    public required List<ZoneDemandRule> Demandas { get; init; }
}

/// <summary>Parâmetros do território (data/territory/config.json, GDD 17.5).</summary>
public sealed class TerritoryConfig : IValidatable
{
    public const string File = "territory/config.json";

    public required string Note { get; init; }

    public required Dictionary<string, AgeRange> ZonasPorPorte { get; init; }

    public required int VerticesPorZona { get; init; }

    public required double ToleranciaInicialGraus { get; init; }

    public required double ToleranciaMaximaGraus { get; init; }

    /// <summary>Cobertura mínima de um bloco do perfil para a zona usar o próprio dado (regra de sigilo).</summary>
    public required double CoberturaMinima { get; init; }

    /// <summary>Fração da população num só distrito para a zona levar o nome dele.</summary>
    public required double DistritoDominante { get; init; }

    /// <summary>Regras do território na simulação (presença, transbordo, demanda da zona).</summary>
    public required TerritorySimulation Simulacao { get; init; }

    public IEnumerable<string> Validate()
    {
        TerritorySimulation s = Simulacao;
        if (s.DecaimentoSemanal < 0 || s.Transbordo is < 0 or >= 1 || s.RendaExpoente < 0 || s.DemandaPeso is < 0 or > 1
            || s.ZonaNatalFator < 1 || s.RedutoFator < 1 || s.Demandas.Count == 0)
        {
            yield return "simulacao: decaimento e expoente não negativos, transbordo em [0, 1), peso da demanda em [0, 1], fatores de zona natal e reduto maiores ou iguais a 1 e ao menos uma demanda.";
        }

        foreach (ZoneDemandRule d in s.Demandas.Where(d => d.Regra is not (ZoneDemandRule.RendaBaixa or ZoneDemandRule.Idosos or ZoneDemandRule.Jovens or ZoneDemandRule.Rural or ZoneDemandRule.Padrao)))
        {
            yield return $"simulacao.demandas '{d.Id}': regra '{d.Regra}' desconhecida.";
        }

        if (s.Demandas.All(d => d.Regra != ZoneDemandRule.Padrao))
        {
            yield return "simulacao.demandas: falta a demanda de regra 'padrao'.";
        }

        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            if (!ZonasPorPorte.TryGetValue(porte, out AgeRange? range) || range.Min < 1 || range.Max < range.Min)
            {
                yield return $"zonas_por_porte.{porte}: mínimo de 1 e máximo maior ou igual ao mínimo.";
            }
        }

        if (VerticesPorZona < 8 || ToleranciaInicialGraus <= 0 || ToleranciaMaximaGraus < ToleranciaInicialGraus)
        {
            yield return "vertices_por_zona (8 ou mais) e tolerâncias positivas e crescentes.";
        }

        if (CoberturaMinima is <= 0 or > 1 || DistritoDominante is <= 0.5 or > 1)
        {
            yield return "cobertura_minima entre 0 e 1; distrito_dominante entre 0,5 e 1.";
        }
    }
}
