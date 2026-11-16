using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.Core.Indicators;

public sealed class FonteIndicador
{
    public required string Campo { get; init; }

    public required string Fonte { get; init; }

    public required int Ano { get; init; }

    public required string Url { get; init; }

    public required DateTimeOffset BaixadoEm { get; init; }
}

public sealed class LacunaIndicador
{
    public required int CodigoIbge { get; init; }

    public required string Nome { get; init; }

    public required string Uf { get; init; }

    public required string Indicador { get; init; }

    public required string Regra { get; init; }
}

public sealed class TotalUf
{
    /// <summary>PIB da UF como publicado (mil reais).</summary>
    public required long PibMil { get; init; }

    public required long PibMilSomaMunicipios { get; init; }

    public required long Domicilios { get; init; }
}

/// <summary>data/indicadores_reais/_fontes.json: de onde veio cada campo, as regras de substituição e as lacunas (GDD 17.17).</summary>
public sealed class FontesIndicadores : IValidatable
{
    /// <summary>Campos de <see cref="IndicadoresMunicipio"/> que precisam de fonte registrada (nenhum número escrito à mão).</summary>
    public static readonly IReadOnlyList<string> CamposObrigatorios = ["pib", "emprego", "financas", "ideb", "saude", "saneamento"];

    public required DateTimeOffset GeradoEm { get; init; }

    public required List<FonteIndicador> Fontes { get; init; }

    public required Dictionary<string, string> Regras { get; init; }

    public required Dictionary<string, TotalUf> TotaisUf { get; init; }

    public required Dictionary<string, int> Resumo { get; init; }

    public required List<LacunaIndicador> Lacunas { get; init; }

    public IEnumerable<string> Validate()
    {
        foreach (string campo in CamposObrigatorios.Where(c => !Fontes.Any(f => f.Campo.Split(',', StringSplitOptions.TrimEntries).Any(x => x == c || x.StartsWith(c + ".", StringComparison.Ordinal)))))
        {
            yield return $"campo '{campo}' sem fonte registrada.";
        }

        foreach (FonteIndicador f in Fontes.Where(f => string.IsNullOrWhiteSpace(f.Fonte) || string.IsNullOrWhiteSpace(f.Url)))
        {
            yield return $"fonte de '{f.Campo}' sem órgão ou endereço.";
        }

        if (TotaisUf.Count != MunicipiosDataSet.UfCount)
        {
            yield return $"totais_uf: esperadas {MunicipiosDataSet.UfCount} UFs, há {TotaisUf.Count}.";
        }
    }
}

/// <summary>Carga e conferência de data/indicadores_reais/ contra os municípios e as faixas plausíveis.</summary>
public static class IndicadoresContent
{
    public static IEnumerable<DataFileReport> Validate(string dataDirectory, MunicipiosDataSet municipios)
    {
        DataLoadResult<IndicatorDefinitions> definitions = JsonData.LoadFile<IndicatorDefinitions>(Path.Combine(dataDirectory, IndicatorDefinitions.File));
        yield return new DataFileReport(IndicatorDefinitions.File, definitions.Value?.Indicadores.Count ?? 0, definitions.Errors);

        string fontesFile = $"{IndicadoresPaths.Directory}/{IndicadoresPaths.FontesFileName}";
        DataLoadResult<FontesIndicadores> fontes = JsonData.LoadFile<FontesIndicadores>(Path.Combine(dataDirectory, IndicadoresPaths.Directory, IndicadoresPaths.FontesFileName));
        yield return new DataFileReport(fontesFile, fontes.Value?.Fontes.Count ?? 0, fontes.Errors);

        DataLoadResult<IndicadoresUfsFile> ufs = IndicadoresPaths.LoadUfs(dataDirectory);
        var ufErrors = ufs.Errors.ToList();
        if (ufs.Value is not null && ufs.Value.Ufs.Count != MunicipiosDataSet.UfCount)
        {
            ufErrors.Add($"esperadas {MunicipiosDataSet.UfCount} UFs, há {ufs.Value.Ufs.Count}.");
        }

        yield return new DataFileReport($"{IndicadoresPaths.Directory}/{IndicadoresPaths.UfsFileName}", ufs.Value?.Ufs.Count ?? 0, ufErrors);

        var lacunas = (fontes.Value?.Lacunas ?? []).Select(l => (l.CodigoIbge, l.Indicador)).ToHashSet();
        foreach (var group in municipios.All.GroupBy(m => m.Uf).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            string file = $"{IndicadoresPaths.Directory}/{group.Key}.json";
            DataLoadResult<IndicadoresUfFile> result = IndicadoresPaths.Load(dataDirectory, group.Key);
            if (result.Value is null || !result.IsValid || definitions.Value is null)
            {
                yield return new DataFileReport(file, 0, result.Errors);
                continue;
            }

            var errors = new List<string>();
            var byCode = result.Value.Municipios.ToDictionary(i => i.CodigoIbge);
            foreach (Municipio m in group)
            {
                if (!byCode.TryGetValue(m.CodigoIbge, out IndicadoresMunicipio? i))
                {
                    errors.Add($"{m.CodigoIbge} ({m.Nome}): sem indicadores.");
                    continue;
                }

                // Todo indicador tem valor ou lacuna registrada (GDD 17.17).
                if (i.Pib is null && !lacunas.Contains((m.CodigoIbge, "pib")))
                {
                    errors.Add($"{m.CodigoIbge}: sem PIB e sem lacuna registrada.");
                }

                if (i.Saneamento is null && !lacunas.Contains((m.CodigoIbge, "saneamento")))
                {
                    errors.Add($"{m.CodigoIbge}: sem saneamento e sem lacuna registrada.");
                }

                if (i.Financas.Fonte != FinancasEnte.Declarado && !lacunas.Contains((m.CodigoIbge, "financas")))
                {
                    errors.Add($"{m.CodigoIbge}: finanças substituídas sem lacuna registrada.");
                }

                if (i.Ideb.Fonte != IdebMunicipal.DoMunicipio && !lacunas.Contains((m.CodigoIbge, "ideb")))
                {
                    errors.Add($"{m.CodigoIbge}: IDEB substituído sem lacuna registrada.");
                }

                if (i.Saude.MortalidadeInfantilFonte != SaudeMunicipal.DoMunicipio && !lacunas.Contains((m.CodigoIbge, "mortalidade_infantil")))
                {
                    errors.Add($"{m.CodigoIbge}: mortalidade infantil substituída sem lacuna registrada.");
                }

                foreach (var (id, value) in IndicatorDefinitions.RealValues(i, m))
                {
                    RealIndicatorDefinition d = definitions.Value.Get(id);
                    if (value < d.Min || value > d.Max)
                    {
                        errors.Add($"{m.CodigoIbge} ({m.Nome}): {id} = {value:0.##} fora da faixa plausível [{d.Min}, {d.Max}].");
                    }
                }
            }

            if (byCode.Count != group.Count())
            {
                errors.Add($"{byCode.Count} municípios no arquivo, {group.Count()} na UF.");
            }

            // Totais por UF iguais aos oficiais, onde a fonte publica o total (PIB com folga de arredondamento; domicílios, exatos).
            if (fontes.Value is not null && fontes.Value.TotaisUf.TryGetValue(group.Key, out TotalUf? total))
            {
                long pib = result.Value.Municipios.Sum(i => i.Pib?.ValorMil ?? 0);
                if (Math.Abs(pib - total.PibMil) > result.Value.Municipios.Count)
                {
                    errors.Add($"soma do PIB dos municípios ({pib}) diferente do total da UF ({total.PibMil}).");
                }

                long domicilios = result.Value.Municipios.Sum(i => i.Saneamento?.Domicilios ?? 0);
                if (domicilios != total.Domicilios)
                {
                    errors.Add($"soma dos domicílios ({domicilios}) diferente do total da UF ({total.Domicilios}).");
                }
            }

            yield return new DataFileReport(file, byCode.Count, errors);
        }

        DataLoadResult<Economy.EconomyStart> economy = JsonData.LoadFile<Economy.EconomyStart>(Path.Combine(dataDirectory, Economy.EconomyStart.File));
        yield return new DataFileReport(Economy.EconomyStart.File, economy.Value is null ? 0 : 1, economy.Errors);
    }
}
