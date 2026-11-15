using Planalto.Core.Data;

namespace Planalto.Core.Characters;

public sealed class SocialOriginDefinition
{
    public required string Id { get; init; }

    public required GenderedText Name { get; init; }

    public required string Description { get; init; }

    public required decimal Money { get; init; }

    public required int PoliticalCapital { get; init; }

    /// <summary>Reconhecimento inicial na cidade natal (0 a 100).</summary>
    public required int Recognition { get; init; }

    public Dictionary<AttributeId, int> AttributeModifiers { get; init; } = [];
}

public sealed class StartingAttributes
{
    public required int Base { get; init; }

    public required int Spread { get; init; }
}

public sealed class AgeRange
{
    public required int Min { get; init; }

    public required int Max { get; init; }
}

/// <summary>Conteúdo de data/social_origins.json (GDD 3.3).</summary>
public sealed class SocialOriginCatalog : IValidatable
{
    public required List<SocialOriginDefinition> Origins { get; init; }

    public required StartingAttributes StartingAttributes { get; init; }

    public required AgeRange StartingAge { get; init; }

    public required int StartingEnergy { get; init; }

    public required int StartingHealth { get; init; }

    public SocialOriginDefinition Get(string id) => Origins.First(o => o.Id == id);

    public IEnumerable<string> Validate()
    {
        if (Origins is null || Origins.Count == 0)
        {
            yield return "a lista 'origins' está vazia.";
            yield break;
        }

        foreach (string error in DataChecks.UniqueIds(Origins.Select(o => o?.Id), "Origem"))
        {
            yield return error;
        }

        foreach (SocialOriginDefinition? origin in Origins)
        {
            if (origin is null)
            {
                continue;
            }

            foreach (string error in DataChecks.Id(origin.Id, "origem").Concat(DataChecks.Gendered(origin.Name, "name", origin.Id)).Concat(DataChecks.Modifiers(origin.AttributeModifiers, origin.Id)))
            {
                yield return error;
            }

            if (origin.Money < 0 || origin.PoliticalCapital < 0 || origin.Recognition is < 0 or > 100)
            {
                yield return $"origem '{origin.Id}': dinheiro e capital não negativos, reconhecimento entre 0 e 100.";
            }
        }

        if (StartingAge is null || StartingAge.Min < 18 || StartingAge.Max < StartingAge.Min || StartingAttributes is null || StartingAttributes.Base is < 0 or > 100)
        {
            yield return "idade inicial a partir de 18 e atributos-base entre 0 e 100.";
        }
    }
}

public sealed class NamedOption
{
    public required string Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>Conteúdo de data/campaign_identity.json (GDD 3.4).</summary>
public sealed class CampaignIdentityCatalog : IValidatable
{
    public required List<NamedOption> Fonts { get; init; }

    public required List<NamedOption> Jingles { get; init; }

    public required List<NamedOption> Poses { get; init; }

    /// <summary>Sugestões de slogan, com {cidade}.</summary>
    public required List<string> Slogans { get; init; }

    public IEnumerable<string> Validate()
    {
        foreach (var (name, list, expected) in new[] { ("fonts", Fonts, 6), ("jingles", Jingles, 6), ("poses", Poses, 3) })
        {
            if (list is null || list.Count != expected)
            {
                yield return $"'{name}' precisa de {expected} opções (GDD 3.4).";
                continue;
            }

            foreach (string error in DataChecks.UniqueIds(list.Select(o => o.Id), name))
            {
                yield return error;
            }
        }

        if (Slogans is null || Slogans.Count == 0)
        {
            yield return "'slogans' vazio.";
        }
    }
}

/// <summary>Conteúdo para gerar pessoas (jogador e NPCs): nomes, profissões, traços, origens e aparência.</summary>
public sealed class PeopleContent
{
    public const string FirstNamesFile = "names/first_names.json";
    public const string NamesFile = "names/names.json";
    public const string OriginsFile = "social_origins.json";
    public const string CampaignFile = "campaign_identity.json";

    public required FirstNamesFile FirstNames { get; init; }

    public required NameStyleCatalog Names { get; init; }

    public required ProfessionCatalog Professions { get; init; }

    public required TraitCatalog Traits { get; init; }

    public required SocialOriginCatalog Origins { get; init; }

    public required CampaignIdentityCatalog Campaign { get; init; }

    public required AppearanceContent Appearance { get; init; }

    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, out PeopleContent? content)
    {
        var reports = new List<DataFileReport>();
        FirstNamesFile? first = Check<FirstNamesFile>(dataDirectory, FirstNamesFile, f => f.Ufs.Count, reports);
        NameStyleCatalog? names = Check<NameStyleCatalog>(dataDirectory, NamesFile, n => n.Surnames.Count, reports);
        SocialOriginCatalog? origins = Check<SocialOriginCatalog>(dataDirectory, OriginsFile, o => o.Origins.Count, reports);
        CampaignIdentityCatalog? campaign = Check<CampaignIdentityCatalog>(dataDirectory, CampaignFile, c => c.Slogans.Count, reports);
        var professions = JsonData.LoadFile<ProfessionCatalog>(Path.Combine(dataDirectory, "professions.json"));
        var traits = JsonData.LoadFile<TraitCatalog>(Path.Combine(dataDirectory, "traits.json"));
        var appearanceReports = AppearanceContent.Load(dataDirectory, out AppearanceContent? appearance);

        if (names is not null)
        {
            var extra = names.Titles.Keys.Where(k => professions.Value?.Professions.All(p => p.Id != k) ?? false)
                .Select(k => $"names.json: título para profissão inexistente '{k}'.").ToList();
            int i = reports.FindIndex(r => r.File == NamesFile);
            reports[i] = reports[i] with { Errors = [.. reports[i].Errors, .. extra] };
        }

        content = reports.All(r => r.IsValid) && professions.IsValid && traits.IsValid && appearance is not null
            ? new PeopleContent
            {
                FirstNames = first!,
                Names = names!,
                Professions = professions.Value!,
                Traits = traits.Value!,
                Origins = origins!,
                Campaign = campaign!,
                Appearance = appearance,
            }
            : null;
        return reports;
    }

    public static PeopleContent LoadOrThrow(string dataDirectory)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, out PeopleContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }

    private static T? Check<T>(string dataDirectory, string file, Func<T, int> count, List<DataFileReport> reports)
        where T : class, IValidatable
    {
        DataLoadResult<T> result = JsonData.LoadFile<T>(Path.Combine(dataDirectory, file));
        reports.Add(new DataFileReport(file, result.Value is null ? 0 : count(result.Value), result.Errors));
        return result.IsValid ? result.Value : null;
    }
}
