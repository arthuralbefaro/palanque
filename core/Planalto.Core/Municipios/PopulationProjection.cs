using System.Collections.Concurrent;
using Planalto.Core.Data;
using Planalto.Core.Demography;
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
/// Evolução da população dos municípios (D-033 e GDD 17.17): taxa anual geométrica entre o Censo 2022 e a estimativa
/// do IBGE, limitada, projetada ano a ano. Com a projeção por UF (IBGE, Projeções da População 2024), essa taxa vale
/// só como distribuição relativa: a cada ano, os municípios de cada UF são reescalados para que a soma acompanhe a
/// projeção da UF. As vagas de vereador de cada eleição são o mínimo entre as vagas atuais e o teto da CF, art. 29, IV,
/// com a população do ano anterior à eleição (a estimativa que o TSE usa).
/// </summary>
public sealed class PopulationProjection
{
    public const int CensusYear = 2022;

    private readonly PopulationProjectionConfig _config;
    private readonly ProjecaoUf? _ufProjection;
    private readonly Dictionary<string, List<Municipio>> _byUf = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Uf, int Year), double> _scales = new();

    /// <summary>Projeção só pela taxa de cada município (comportamento original da D-033, sem reescala por UF).</summary>
    public PopulationProjection(int estimateYear, PopulationProjectionConfig config)
    {
        EstimateYear = estimateYear;
        _config = config;
    }

    private PopulationProjection(int estimateYear, PopulationProjectionConfig config, MunicipiosDataSet data, ProjecaoUf ufProjection)
        : this(estimateYear, config)
    {
        _ufProjection = ufProjection;
        foreach (IGrouping<string, Municipio> uf in data.All.GroupBy(m => m.Uf, StringComparer.Ordinal))
        {
            _byUf[uf.Key] = uf.ToList();
        }
    }

    /// <summary>Ano da estimativa gravada em data/municipios/ (campo "populacao" de _fontes.json).</summary>
    public int EstimateYear { get; }

    /// <summary>True quando os municípios são reescalados pela projeção da UF.</summary>
    public bool RescaledByUf => _ufProjection is not null;

    /// <param name="data">Municípios, com o ano da estimativa em _fontes.json.</param>
    /// <param name="config">Limites da taxa anual.</param>
    /// <param name="ufProjection">Projeção por UF; sem ela, vale a projeção só pela taxa do município.</param>
    public static PopulationProjection From(MunicipiosDataSet data, PopulationProjectionConfig config, ProjecaoUf? ufProjection = null)
    {
        int year = data.Fontes.Campos.FirstOrDefault(c => c.Campo.Split(',').Select(x => x.Trim()).Contains("populacao"))?.Ano
            ?? throw new InvalidOperationException("_fontes.json sem o ano da estimativa de população.");
        return ufProjection is null ? new PopulationProjection(year, config) : new PopulationProjection(year, config, data, ufProjection);
    }

    /// <summary>
    /// Carrega a configuração e, se existir, data/demography/projecao_uf.json. Sem o arquivo, vale a projeção só pela
    /// taxa do município; arquivo presente e inválido é erro.
    /// </summary>
    public static PopulationProjection Load(string dataDirectory, MunicipiosDataSet data)
    {
        PopulationProjectionConfig config = JsonData.LoadFile<PopulationProjectionConfig>(Path.Combine(dataDirectory, PopulationProjectionConfig.File)).GetValueOrThrow();
        string ufFile = Path.Combine(dataDirectory, ProjecaoUf.File);
        ProjecaoUf? ufProjection = System.IO.File.Exists(ufFile) ? JsonData.LoadFile<ProjecaoUf>(ufFile).GetValueOrThrow() : null;
        return From(data, config, ufProjection);
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

    public long Population(Municipio m, int year) => (long)Math.Round(Trend(m, year) * UfScale(m.Uf, year));

    /// <summary>
    /// Alvo da UF no ano, pela projeção por UF; null sem ela ou sem a UF. A soma de <see cref="Population"/> dos
    /// municípios da UF difere do alvo só pelo arredondamento de cada município para inteiro (até meia pessoa por
    /// município).
    /// </summary>
    public double? UfTarget(string uf, int year) => _ufProjection?.Alvo(uf, year);

    /// <summary>Vagas de vereador na eleição do ano: nunca acima das atuais; cai se a população passar abaixo da faixa.</summary>
    public int Seats(Municipio m, int electionYear) =>
        Math.Max(1, Math.Min(m.VagasVereador, ElectionRules.VagasVereador(Population(m, electionYear - 1))));

    /// <summary>População pela taxa do próprio município, sem reescala.</summary>
    private double Trend(Municipio m, int year) => m.Populacao * Math.Pow(1 + AnnualGrowth(m), year - EstimateYear);

    /// <summary>Fator que leva a soma das tendências dos municípios da UF ao alvo da projeção da UF naquele ano.</summary>
    private double UfScale(string uf, int year)
    {
        if (_ufProjection is null || !_byUf.TryGetValue(uf, out List<Municipio>? municipios))
        {
            return 1;
        }

        return _scales.GetOrAdd((uf, year), key =>
        {
            double? target = _ufProjection.Alvo(key.Uf, key.Year);
            double trend = municipios.Sum(m => Trend(m, key.Year));
            return target is null || trend <= 0 ? 1 : target.Value / trend;
        });
    }
}
