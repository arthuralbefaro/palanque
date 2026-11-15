using Planalto.Core.Data;

namespace Planalto.Core.Ideology;

/// <summary>Eixo ideológico a que o tema se liga, para derivar a posição preferida a partir dos eixos.</summary>
public enum ThemeAxis
{
    Economic,
    Social,

    /// <summary>Tema sem lado (ex.: transparência): a preferência é a mesma em todo o espectro.</summary>
    None,
}

/// <summary>Tema de posição (GDD 5.2). -2 é o polo negativo, +2 o positivo.</summary>
public sealed class ThemeDefinition
{
    public const int MinPosition = -2;
    public const int MaxPosition = 2;

    public required string Id { get; init; }

    public required ThemeAxis Axis { get; init; }

    public required string Name { get; init; }

    public required string NegativePole { get; init; }

    public required string PositivePole { get; init; }
}

/// <summary>Conteúdo de data/themes.json.</summary>
public sealed class ThemeCatalog : IValidatable
{
    public required List<ThemeDefinition> Themes { get; init; }

    public bool Contains(string id) => Themes.Any(t => t.Id == id);

    public IEnumerable<string> Validate()
    {
        if (Themes is null || Themes.Count == 0)
        {
            yield return "a lista 'themes' está vazia.";
            yield break;
        }

        for (int i = 0; i < Themes.Count; i++)
        {
            ThemeDefinition? theme = Themes[i];
            string where = $"themes[{i}]";
            if (theme is null)
            {
                yield return $"{where}: item null.";
                continue;
            }

            where = $"themes[{i}] '{theme.Id}'";
            foreach (string error in DataChecks.Id(theme.Id, where)
                .Concat(DataChecks.Text(theme.Name, "name", where))
                .Concat(DataChecks.Text(theme.NegativePole, "negative_pole", where))
                .Concat(DataChecks.Text(theme.PositivePole, "positive_pole", where)))
            {
                yield return error;
            }
        }

        foreach (string error in DataChecks.UniqueIds(Themes.Select(t => t?.Id), "Tema"))
        {
            yield return error;
        }
    }
}
