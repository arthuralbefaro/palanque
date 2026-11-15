using System.Text.RegularExpressions;
using Planalto.Core.Characters;

namespace Planalto.Core.Data;

/// <summary>Verificações reutilizadas pelos catálogos de dados.</summary>
public static partial class DataChecks
{
    public const int MaxAttributeModifier = 100;

    public static IEnumerable<string> UniqueIds(IEnumerable<string?> ids, string kind)
    {
        return ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .GroupBy(id => id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => $"{kind} com id duplicado: '{group.Key}'.");
    }

    public static IEnumerable<string> Id(string? id, string where)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            yield return $"{where}: id vazio.";
        }
        else if (!IdPattern().IsMatch(id))
        {
            yield return $"{where}: id '{id}' deve usar só letras minúsculas, números e '_', começando por letra.";
        }
    }

    public static IEnumerable<string> Text(string? text, string field, string where)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return $"{where}: campo '{field}' vazio.";
        }
    }

    public static IEnumerable<string> Gendered(GenderedText? text, string field, string where)
    {
        if (text is null)
        {
            return [$"{where}: campo '{field}' ausente."];
        }

        return Text(text.Masculine, $"{field}.masculine", where)
            .Concat(Text(text.Feminine, $"{field}.feminine", where))
            .Concat(Text(text.Neutral, $"{field}.neutral", where));
    }

    public static IEnumerable<string> Modifiers(IReadOnlyDictionary<AttributeId, int>? modifiers, string where)
    {
        if (modifiers is null)
        {
            yield break;
        }

        foreach (var (attribute, value) in modifiers)
        {
            if (value == 0 || Math.Abs(value) > MaxAttributeModifier)
            {
                yield return $"{where}: modificador de '{attribute}' deve ser diferente de zero e estar entre -{MaxAttributeModifier} e {MaxAttributeModifier} (valor: {value}).";
            }
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*$")]
    private static partial Regex IdPattern();
}
