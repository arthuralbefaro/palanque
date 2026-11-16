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
            Check<TraitCatalog>(dataDirectory, "traits.json", catalog => catalog.Traits.Count, out TraitCatalog? traits),
            Check<ProfessionCatalog>(dataDirectory, "professions.json", catalog => catalog.Professions.Count, out _),
            Check<State.TimeConfig>(dataDirectory, State.TimeConfig.File, c => c.Pausas.Count, out _),
            Check<Territory.TerritoryConfig>(dataDirectory, Territory.TerritoryConfig.File, c => c.ZonasPorPorte.Count, out _),
            Check<Encyclopedia.EncyclopediaContent>(dataDirectory, Encyclopedia.EncyclopediaContent.File, c => c.Termos.Count, out _),
            Check<PorteCatalog>(dataDirectory, "portes.json", catalog => catalog.Portes.Count, out PorteCatalog? portes),
            Check<Career.FoundingConfig>(dataDirectory, Career.FoundingConfig.File, config => config.Intensities.Count, out _),
            Check<Debate.DebateContent>(dataDirectory, Debate.DebateContent.File, content => content.Cards.Count, out _),
            Check<PopulationProjectionConfig>(dataDirectory, PopulationProjectionConfig.File, _ => 1, out _),
            Check<Demography.MortalityTable>(dataDirectory, Demography.MortalityTable.File, t => t.Tabuas.Count, out _),
            Check<Demography.DemographyConfig>(dataDirectory, Demography.DemographyConfig.File, _ => 1, out _),
            Check<Demography.ProjecaoUf>(dataDirectory, Demography.ProjecaoUf.File, p => p.Populacao.Count, out _),
            Check<Demography.FontesDemografia>(dataDirectory, Demography.FontesDemografia.File, f => f.Fontes.Count, out _),
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
        // A campanha lê também o território e a memória das pessoas, que podem já estar na lista.
        reports.AddRange(Campaign.CampaignContent.Load(dataDirectory, out Campaign.CampaignContent? campaign).Where(r => !reports.Any(x => x.File == r.File)));
        reports.AddRange(Events.EventContent.Load(dataDirectory, out Events.EventContent? events));
        if (campaign is not null && events is not null)
        {
            // Lint das decisões (GDD 17.1, princípio e): ganho e custo de naturezas diferentes em toda opção.
            reports.Add(new DataFileReport("lint: decisões com ganho e custo", Campaign.DecisionLint.Count(campaign, events), [.. Campaign.DecisionLint.Run(campaign, events)]));

            // Lint de rastro (princípio d): escolha relevante sem entrada no Livro nem lembrança em alguém reprova.
            reports.Add(new DataFileReport("lint: rastro das escolhas relevantes", events.Events.Sum(e => e.Choices.Count(c => c.Relevant)), [.. Campaign.DecisionLint.Trace(events)]));
            if (traits is not null)
            {
                reports.Add(new DataFileReport("lint: eventos", events.Events.Count, [.. Events.EventLint.Run(events, portes, traits).Errors]));
            }

            // Referências entre os arquivos da R3: temas, grupos de eleitores, compromissos e canais citados existem.
            reports.Add(new DataFileReport("campanha: referências da R3", 1, [.. Campaign.CampaignContent.CrossCheck(
                campaign,
                world?.Parties.Themes.Themes.Select(t => t.Id).ToHashSet(StringComparer.Ordinal),
                electorate?.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal))]));
        }
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
            reports.AddRange(Indicators.IndicadoresContent.Validate(dataDirectory, dataSet));
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
