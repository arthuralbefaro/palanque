using Planalto.Core.Data;
using Planalto.Core.Ideology;

namespace Planalto.Core.Parties;

/// <summary>Todo o conteúdo de data/ usado para gerar partidos, já validado e cruzado.</summary>
public sealed class PartyContent
{
    public required ThemeCatalog Themes { get; init; }

    public required FamilyCatalog Families { get; init; }

    public required PartyBlocklist Blocklist { get; init; }

    public required NameTermCatalog NameTerms { get; init; }

    public required LogoIconCatalog LogoIcons { get; init; }

    public required PartyGeneratorConfig Generator { get; init; }

    /// <summary>Carrega e valida os arquivos; devolve um relatório por arquivo e o conteúdo, se tudo for válido.</summary>
    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, out PartyContent? content)
    {
        var reports = new List<DataFileReport>();
        ThemeCatalog? themes = Check<ThemeCatalog>(dataDirectory, PartyPaths.Themes, c => c.Themes.Count, reports);
        FamilyCatalog? families = Check<FamilyCatalog>(dataDirectory, PartyPaths.Families, c => c.Families.Count, reports);
        BlocklistFile? blocklist = Check<BlocklistFile>(dataDirectory, PartyPaths.Blocklist, c => c.Entries.Count, reports);
        NameTermCatalog? terms = Check<NameTermCatalog>(dataDirectory, PartyPaths.NameTerms, c => c.Qualifiers.Count, reports);
        LogoIconCatalog? icons = Check<LogoIconCatalog>(dataDirectory, PartyPaths.LogoIcons, c => c.Icons.Count, reports);
        PartyGeneratorConfig? generator = Check<PartyGeneratorConfig>(dataDirectory, PartyPaths.Generator, _ => 1, reports);

        if (families is not null && themes is not null)
        {
            AddErrors(reports, PartyPaths.Families, families.ValidateAgainst(themes));
        }

        if (families is not null && terms is not null)
        {
            AddErrors(reports, PartyPaths.NameTerms, terms.ValidateAgainst(families));
        }

        content = reports.All(r => r.IsValid)
            ? new PartyContent
            {
                Themes = themes!,
                Families = families!,
                Blocklist = new PartyBlocklist(blocklist!),
                NameTerms = terms!,
                LogoIcons = icons!,
                Generator = generator!,
            }
            : null;
        return reports;
    }

    public static PartyContent LoadOrThrow(string dataDirectory)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, out PartyContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }

    private static T? Check<T>(string dataDirectory, string file, Func<T, int> count, List<DataFileReport> reports)
        where T : class, IValidatable
    {
        DataLoadResult<T> result = JsonData.LoadFile<T>(Path.Combine(dataDirectory, file));
        reports.Add(new DataFileReport(file, result.Value is null ? 0 : count(result.Value), result.Errors));
        return result.IsValid ? result.Value : null;
    }

    private static void AddErrors(List<DataFileReport> reports, string file, IEnumerable<string> errors)
    {
        int index = reports.FindIndex(r => r.File == file);
        string name = Path.GetFileName(file);
        reports[index] = reports[index] with { Errors = [.. reports[index].Errors, .. errors.Select(e => $"{name}: {e}")] };
    }
}
