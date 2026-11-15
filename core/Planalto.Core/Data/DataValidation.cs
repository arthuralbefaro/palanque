using Planalto.Core.Characters;
using Planalto.Core.Municipios;

namespace Planalto.Core.Data;

public sealed record DataFileReport(string File, int ItemCount, IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>Lista dos arquivos de data/ conferidos pelo DataValidator e pelos testes.</summary>
public static class DataValidation
{
    public static IReadOnlyList<DataFileReport> ValidateAll(string dataDirectory)
    {
        var reports = new List<DataFileReport>
        {
            Check<TraitCatalog>(dataDirectory, "traits.json", catalog => catalog.Traits.Count, out _),
            Check<ProfessionCatalog>(dataDirectory, "professions.json", catalog => catalog.Professions.Count, out _),
            Check<PorteCatalog>(dataDirectory, "portes.json", catalog => catalog.Portes.Count, out PorteCatalog? portes),
        };

        if (portes is null)
        {
            reports.Add(new DataFileReport($"{MunicipiosDataSet.DirectoryName}/", 0, ["não validado: portes.json inválido."]));
            return reports;
        }

        var (dataSet, errors) = MunicipiosDataSet.Load(dataDirectory, portes);
        reports.Add(new DataFileReport($"{MunicipiosDataSet.DirectoryName}/", dataSet?.All.Count() ?? 0, errors));
        return reports;
    }

    private static DataFileReport Check<T>(string dataDirectory, string file, Func<T, int> count, out T? value)
        where T : class, IValidatable
    {
        DataLoadResult<T> result = JsonData.LoadFile<T>(Path.Combine(dataDirectory, file));
        value = result.IsValid ? result.Value : null;
        int items = result.Value is null ? 0 : count(result.Value);
        return new DataFileReport(file, items, result.Errors);
    }
}
