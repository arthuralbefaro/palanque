using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.Parties;

namespace Planalto.Core.World;

/// <summary>Conteúdo usado pela tela "Novo Mundo": partidos, predefinições e opções.</summary>
public sealed class WorldContent
{
    public const string PresetsFile = "world_presets.json";
    public const string OptionsFile = "world_options.json";

    public required PartyContent Parties { get; init; }

    public required WorldPresetCatalog Presets { get; init; }

    public required WorldOptionsCatalog Options { get; init; }

    public CandidateNumberRulesConfig NumberRules { get; init; } = CandidateNumberRulesConfig.Default;

    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, out WorldContent? content)
    {
        var reports = PartyContent.Load(dataDirectory, out PartyContent? parties).ToList();

        DataLoadResult<WorldPresetCatalog> presets = JsonData.LoadFile<WorldPresetCatalog>(Path.Combine(dataDirectory, PresetsFile));
        var presetErrors = presets.Errors.ToList();
        if (presets.IsValid && parties is not null)
        {
            presetErrors.AddRange(presets.Value!.ValidateAgainst(parties.Families).Select(e => $"{PresetsFile}: {e}"));
        }

        reports.Add(new DataFileReport(PresetsFile, presets.Value?.Presets.Count ?? 0, presetErrors));

        DataLoadResult<WorldOptionsCatalog> options = JsonData.LoadFile<WorldOptionsCatalog>(Path.Combine(dataDirectory, OptionsFile));
        reports.Add(new DataFileReport(OptionsFile, options.Value?.Difficulties.Count ?? 0, options.Errors));

        content = reports.All(r => r.IsValid)
            ? new WorldContent { Parties = parties!, Presets = presets.Value!, Options = options.Value! }
            : null;
        return reports;
    }

    public static WorldContent LoadOrThrow(string dataDirectory)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, out WorldContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }
}
