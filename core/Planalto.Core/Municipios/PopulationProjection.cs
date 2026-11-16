using Planalto.Core.Data;
using Planalto.Core.Elections;

namespace Planalto.Core.Municipios;

/// <summary>Parâmetros da projeção da população (data/population_projection.json).</summary>
public sealed class PopulationProjectionConfig : IValidatable
{
    public const string File = "population_projection.json";

    public required string Note { get; init; }

    public required double MinAnnualGrowth { get; init; }

    public required double MaxAnnualGrowth { get; init; }

    public IEnumerable<string> Validate()
    {
        if (MinAnnualGrowth > MaxAnnualGrowth || MinAnnualGrowth <= -1)
        {
            yield return "min_annual_growth deve ser maior que -1 e menor ou igual a max_annual_growth.";
        }
    }
}

/// <summary>
/// Evolução da população dos municípios (Fase 6): taxa anual geométrica entre o Censo 2022 e a estimativa do IBGE,
/// limitada, projetada ano a ano. As vagas de vereador de cada eleição são o mínimo entre as vagas atuais e o teto
/// da CF, art. 29, IV, com a população do ano anterior à eleição (a estimativa que o TSE usa).
/// </summary>
public sealed class PopulationProjection
{
    public const int CensusYear = 2022;

    private readonly PopulationProjectionConfig _config;

    public PopulationProjection(int estimateYear, PopulationProjectionConfig config)
    {
        EstimateYear = estimateYear;
        _config = config;
    }

    /// <summary>Ano da estimativa gravada em data/municipios/ (campo "populacao" de _fontes.json).</summary>
    public int EstimateYear { get; }

    public static PopulationProjection From(MunicipiosDataSet data, PopulationProjectionConfig config)
    {
        int year = data.Fontes.Campos.FirstOrDefault(c => c.Campo.Split(',').Select(x => x.Trim()).Contains("populacao"))?.Ano
            ?? throw new InvalidOperationException("_fontes.json sem o ano da estimativa de população.");
        return new PopulationProjection(year, config);
    }

    public double AnnualGrowth(Municipio m)
    {
        long census = m.Perfil.PopulacaoCenso;
        int years = EstimateYear - CensusYear;
        if (census <= 0 || m.Populacao <= 0 || years <= 0)
        {
            return 0;
        }

        double growth = Math.Pow((double)m.Populacao / census, 1.0 / years) - 1;
        return Math.Clamp(growth, _config.MinAnnualGrowth, _config.MaxAnnualGrowth);
    }

    public long Population(Municipio m, int year) =>
        (long)Math.Round(m.Populacao * Math.Pow(1 + AnnualGrowth(m), year - EstimateYear));

    /// <summary>Vagas de vereador na eleição do ano: nunca acima das atuais; cai se a população passar abaixo da faixa.</summary>
    public int Seats(Municipio m, int electionYear) =>
        Math.Max(1, Math.Min(m.VagasVereador, ElectionRules.VagasVereador(Population(m, electionYear - 1))));
}
