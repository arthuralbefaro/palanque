using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.Parties;
using Planalto.Core.Scenarios;

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

    /// <summary>Arquivo nacional do cenário "Brasil 2026", se gerado (os estaduais são carregados à parte).</summary>
    public NationalScenarioFile? Brasil2026 { get; init; }

    /// <summary>Arquivo nacional do cenário pedido por uma predefinição, ou null se não existir.</summary>
    public NationalScenarioFile? ScenarioFor(WorldPreset preset) =>
        preset.Scenario == Brasil2026?.Id ? Brasil2026 : null;

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

        NationalScenarioFile? brasil2026 = null;
        if (Brasil2026Scenario.Exists(dataDirectory))
        {
            var (national, errors) = Brasil2026Scenario.LoadNational(dataDirectory);
            brasil2026 = national;
            if (errors.Count > 0)
            {
                reports.Add(new DataFileReport($"{ScenarioPaths.Brasil2026Directory}/{ScenarioPaths.NationalFile}", 0, errors));
            }
        }

        content = reports.All(r => r.IsValid)
            ? new WorldContent { Parties = parties!, Presets = presets.Value!, Options = options.Value!, Brasil2026 = brasil2026 }
            : null;
        return reports;
    }

    public static WorldContent LoadOrThrow(string dataDirectory)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, out WorldContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }
}
