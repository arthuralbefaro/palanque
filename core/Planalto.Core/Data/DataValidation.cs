using Planalto.Core.Characters;
using Planalto.Core.Municipios;
using Planalto.Core.Scenarios;
using Planalto.Core.World;

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
            Check<Career.FoundingConfig>(dataDirectory, Career.FoundingConfig.File, config => config.Intensities.Count, out _),
            Check<Debate.DebateContent>(dataDirectory, Debate.DebateContent.File, content => content.Cards.Count, out _),
        };

        reports.AddRange(WorldContent.Load(dataDirectory, out WorldContent? world));

        if (portes is null)
        {
            reports.Add(new DataFileReport($"{MunicipiosDataSet.DirectoryName}/", 0, ["não validado: portes.json inválido."]));
            return reports;
        }

        var (dataSet, errors) = MunicipiosDataSet.Load(dataDirectory, portes);
        reports.Add(new DataFileReport($"{MunicipiosDataSet.DirectoryName}/", dataSet?.All.Count() ?? 0, errors));

        string scenario = $"{ScenarioPaths.Brasil2026Directory}/";
        if (dataSet is null || world is null)
        {
            reports.Add(new DataFileReport(scenario, 0, ["não validado: municípios ou partidos inválidos."]));
        }
        else
        {
            var (loaded, scenarioErrors) = Brasil2026Scenario.Load(dataDirectory, dataSet, world.Parties);
            reports.Add(new DataFileReport(scenario, loaded?.States.Values.Sum(s => s.Municipios.Count) ?? 0, scenarioErrors));
        }

        reports.AddRange(Electorate.ElectorateContent.Load(dataDirectory, world?.Parties.Themes, dataSet, out Electorate.ElectorateContent? electorate));
        var people = PeopleContent.Load(dataDirectory, out _);
        reports.AddRange(people.Where(r => !reports.Any(x => x.File == r.File)));
        reports.AddRange(AppearanceContent.Load(dataDirectory, out _));
        reports.AddRange(Campaign.CampaignContent.Load(dataDirectory, out _));
        reports.AddRange(Events.EventContent.Load(dataDirectory, out _));
        reports.AddRange(Legislature.LegislatureContent.Load(
            dataDirectory,
            electorate?.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal),
            world?.Parties.Themes.Themes.Select(t => t.Id).ToHashSet(StringComparer.Ordinal),
            out _));
        reports.AddRange(Executive.ExecutiveContent.Load(
            dataDirectory,
            electorate?.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal),
            out _));
        if (dataSet is not null)
        {
            reports.AddRange(ValidateGeo(dataDirectory, dataSet));
        }

        return reports;
    }

    /// <summary>Um contorno por UF em data/geo/, cobrindo todos os municípios da UF (GDD 13.1).</summary>
    private static IEnumerable<DataFileReport> ValidateGeo(string dataDirectory, MunicipiosDataSet dataSet)
    {
        foreach (string uf in dataSet.All.Select(m => m.Uf).Distinct().Order(StringComparer.Ordinal))
        {
            string file = $"{Geo.GeoPaths.Directory}/{uf}.json";
            DataLoadResult<Geo.GeoUf> result = Geo.GeoContent.Load(dataDirectory, uf);
            if (result.Value is null || !result.IsValid)
            {
                yield return new DataFileReport(file, 0, result.Errors);
                continue;
            }

            yield return new DataFileReport(file, result.Value.Municipios.Count, Geo.GeoContent.Validate(result.Value, dataSet, uf).ToList());
        }
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
