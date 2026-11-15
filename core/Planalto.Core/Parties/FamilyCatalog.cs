using Planalto.Core.Data;
using Planalto.Core.Ideology;

namespace Planalto.Core.Parties;

public sealed record ThemePosition(string Theme, int Position);

/// <summary>Família ideológica (GDD 14.1).</summary>
public sealed class FamilyDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Faixa no eixo Econômico; null quando variável (família regionalista).</summary>
    public AxisRange? Economic { get; init; }

    /// <summary>Faixa no eixo Costumes; null quando variável (família regionalista).</summary>
    public AxisRange? Social { get; init; }

    /// <summary>Temas típicos da tabela 14.1, em texto, incluindo os que não estão entre os 10 temas da 5.2.</summary>
    public required string TypicalThemes { get; init; }

    public required List<ThemePosition> Themes { get; init; }

    /// <summary>Peso relativo no sorteio de famílias.</summary>
    public required int Weight { get; init; }

    /// <summary>
    /// Faixa variável: no sorteio a família empresta as faixas e os temas de outra família,
    /// pois um partido regionalista pode ter qualquer orientação.
    /// </summary>
    public bool HasVariableRange => Economic is null && Social is null;

    public bool Contains(IdeologicalPosition position) =>
        HasVariableRange || (Economic!.Contains(position.Economic) && Social!.Contains(position.Social));
}

/// <summary>Conteúdo de data/parties/families.json.</summary>
public sealed class FamilyCatalog : IValidatable
{
    public required List<FamilyDefinition> Families { get; init; }

    public FamilyDefinition? Find(string? id) => Families.FirstOrDefault(f => f.Id == id);

    public FamilyDefinition Get(string id) =>
        Find(id) ?? throw new KeyNotFoundException($"Família ideológica '{id}' não existe.");

    /// <summary>Famílias com faixa fixa, que podem emprestar faixa às de faixa variável.</summary>
    public IEnumerable<FamilyDefinition> FixedRangeFamilies => Families.Where(f => !f.HasVariableRange);

    public IEnumerable<string> Validate()
    {
        if (Families is null || Families.Count == 0)
        {
            yield return "a lista 'families' está vazia.";
            yield break;
        }

        for (int i = 0; i < Families.Count; i++)
        {
            FamilyDefinition? family = Families[i];
            string where = $"families[{i}]";
            if (family is null)
            {
                yield return $"{where}: item null.";
                continue;
            }

            where = $"families[{i}] '{family.Id}'";
            foreach (string error in DataChecks.Id(family.Id, where)
                .Concat(DataChecks.Text(family.Name, "name", where))
                .Concat(DataChecks.Text(family.TypicalThemes, "typical_themes", where))
                .Concat(ValidateRanges(family, where))
                .Concat(ValidateThemes(family, where)))
            {
                yield return error;
            }

            if (family.Weight <= 0)
            {
                yield return $"{where}: 'weight' deve ser maior que zero (valor: {family.Weight}).";
            }
        }

        foreach (string error in DataChecks.UniqueIds(Families.Select(f => f?.Id), "Família"))
        {
            yield return error;
        }

        if (Families.All(f => f is null || f.HasVariableRange))
        {
            yield return "pelo menos uma família precisa ter faixa fixa.";
        }
    }

    /// <summary>Confere se os temas citados existem em data/themes.json.</summary>
    public IEnumerable<string> ValidateAgainst(ThemeCatalog themes)
    {
        foreach (FamilyDefinition? family in Families)
        {
            foreach (ThemePosition? position in family?.Themes ?? [])
            {
                if (position is not null && !themes.Contains(position.Theme))
                {
                    yield return $"família '{family!.Id}': tema '{position.Theme}' não existe em themes.json.";
                }
            }
        }
    }

    private static IEnumerable<string> ValidateRanges(FamilyDefinition family, string where)
    {
        if ((family.Economic is null) != (family.Social is null))
        {
            yield return $"{where}: 'economic' e 'social' devem ser os dois fixos ou os dois null (faixa variável).";
            yield break;
        }

        foreach (string error in (family.Economic?.Validate($"{where}.economic") ?? [])
            .Concat(family.Social?.Validate($"{where}.social") ?? []))
        {
            yield return error;
        }
    }

    private static IEnumerable<string> ValidateThemes(FamilyDefinition family, string where)
    {
        if (family.Themes is null)
        {
            yield return $"{where}: lista 'themes' ausente.";
            yield break;
        }

        // A família de faixa variável empresta os temas da família sorteada.
        if (family.Themes.Count == 0 && !family.HasVariableRange)
        {
            yield return $"{where}: família com faixa fixa precisa de temas típicos.";
        }

        foreach (ThemePosition? theme in family.Themes)
        {
            if (theme is null || string.IsNullOrWhiteSpace(theme.Theme))
            {
                yield return $"{where}: tema vazio.";
            }
            else if (theme.Position < ThemeDefinition.MinPosition || theme.Position > ThemeDefinition.MaxPosition)
            {
                yield return $"{where}: posição do tema '{theme.Theme}' deve estar entre {ThemeDefinition.MinPosition} e {ThemeDefinition.MaxPosition} (valor: {theme.Position}).";
            }
        }

        foreach (var duplicate in family.Themes.Where(t => t is not null).GroupBy(t => t.Theme).Where(g => g.Count() > 1))
        {
            yield return $"{where}: tema '{duplicate.Key}' repetido.";
        }
    }
}
