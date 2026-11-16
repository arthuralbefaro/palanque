using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.Core.Indicators;

public sealed class IndicatorDefinition
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string Unidade { get; init; }

    public required bool MaiorMelhor { get; init; }

    /// <summary>Faixa plausível declarada, na unidade real.</summary>
    public required decimal Min { get; init; }

    public required decimal Max { get; init; }

    public required string Fonte { get; init; }
}

/// <summary>data/indicators/definitions.json: unidade, sentido e faixa plausível de cada indicador real (GDD 17.17).</summary>
public sealed class IndicatorDefinitions : IValidatable
{
    public const string File = "indicators/definitions.json";

    public const string Ideb = "ideb";
    public const string MortalidadeInfantil = "mortalidade_infantil";
    public const string Leitos = "leitos";
    public const string Homicidios = "homicidios";
    public const string RedeAgua = "rede_agua";
    public const string RedeEsgoto = "rede_esgoto";
    public const string EmpregoFormal = "emprego_formal";
    public const string PessoalRcl = "pessoal_rcl";
    public const string ReceitaPerCapita = "receita_per_capita";
    public const string PibPerCapita = "pib_per_capita";

    public static readonly IReadOnlyList<string> Ids =
        [Ideb, MortalidadeInfantil, Leitos, Homicidios, RedeAgua, RedeEsgoto, EmpregoFormal, PessoalRcl, ReceitaPerCapita, PibPerCapita];

    public required string Note { get; init; }

    public required int MinNascidosMortalidadeInfantil { get; init; }

    public required List<IndicatorDefinition> Indicadores { get; init; }

    public IndicatorDefinition Get(string id) => Indicadores.First(i => i.Id == id);

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Indicadores.Select(i => i.Id), "Indicador"))
        {
            yield return error;
        }

        foreach (string id in Ids.Where(id => Indicadores.All(i => i.Id != id)))
        {
            yield return $"indicador '{id}' ausente.";
        }

        foreach (IndicatorDefinition i in Indicadores.Where(i => i.Min >= i.Max))
        {
            yield return $"indicador '{i.Id}': min deve ser menor que max.";
        }

        if (MinNascidosMortalidadeInfantil <= 0)
        {
            yield return "min_nascidos_mortalidade_infantil deve ser positivo.";
        }
    }

    /// <summary>
    /// Valores de partida de um município na unidade real de cada indicador (id → valor); indicador sem dado fica de
    /// fora. As taxas por habitante usam a população do período do dado: o Censo 2022 para óbitos de 2022 a 2024 e a
    /// estimativa mais recente para leitos, emprego e finanças.
    /// </summary>
    public static Dictionary<string, decimal> RealValues(IndicadoresMunicipio i, Municipio m)
    {
        var values = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [Ideb] = i.Ideb.Valor,
            [MortalidadeInfantil] = i.Saude.MortalidadeInfantil,
        };
        decimal pop = m.Populacao;
        decimal popCenso = m.Perfil.PopulacaoCenso;
        if (pop > 0)
        {
            values[Leitos] = i.Saude.Leitos * 1000m / pop;
            values[EmpregoFormal] = i.Emprego.Vinculos * 100m / pop;
            values[ReceitaPerCapita] = i.Financas.ReceitaCorrente / pop;
            if (i.Pib is { } pib)
            {
                values[PibPerCapita] = pib.ValorMil * 1000m / pop;
            }
        }

        if (popCenso > 0 && i.Saude.Anos.Count > 0)
        {
            values[Homicidios] = i.Saude.ObitosAgressao * 100_000m / i.Saude.Anos.Count / popCenso;
        }

        if (i.Saneamento is { Domicilios: > 0 } s)
        {
            values[RedeAgua] = s.RedeAgua * 100m / s.Domicilios;
            values[RedeEsgoto] = s.RedeEsgoto * 100m / s.Domicilios;
        }

        if (i.Financas.PessoalPctRcl is { } pessoal)
        {
            values[PessoalRcl] = pessoal;
        }

        return values;
    }
}
