using Planalto.Core.Data;

namespace Planalto.Core.Parties;

public sealed class LogoIconDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>Símbolo que o catálogo nunca pode oferecer, com o motivo.</summary>
public sealed class ExcludedLogoIcon
{
    public required string Id { get; init; }

    public required string Reason { get; init; }
}

/// <summary>Conteúdo de data/parties/logo_icons.json.</summary>
public sealed class LogoIconCatalog : IValidatable
{
    public required List<LogoIconDefinition> Icons { get; init; }

    public required List<ExcludedLogoIcon> Excluded { get; init; }

    public bool Contains(string? id) => Icons.Any(i => i.Id == id);

    public IEnumerable<string> Validate()
    {
        if (Icons is null || Icons.Count == 0)
        {
            yield return "a lista 'icons' está vazia.";
            yield break;
        }

        for (int i = 0; i < Icons.Count; i++)
        {
            LogoIconDefinition? icon = Icons[i];
            if (icon is null)
            {
                yield return $"icons[{i}]: item null.";
                continue;
            }

            foreach (string error in DataChecks.Id(icon.Id, $"icons[{i}]").Concat(DataChecks.Text(icon.Name, "name", $"icons[{i}] '{icon.Id}'")))
            {
                yield return error;
            }
        }

        foreach (string error in DataChecks.UniqueIds(Icons.Select(i => i?.Id), "Ícone"))
        {
            yield return error;
        }

        var excluded = (Excluded ?? []).Where(e => e is not null).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (LogoIconDefinition icon in Icons.Where(i => i is not null && excluded.Contains(i.Id)))
        {
            yield return $"ícone '{icon.Id}' está na lista 'excluded' e não pode ser oferecido.";
        }
    }
}
