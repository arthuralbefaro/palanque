using Planalto.Core.Data;

namespace Planalto.Core.Parties;

/// <summary>Primeira palavra do nome ("Partido", "Aliança"), que define a concordância.</summary>
public sealed class NameHead
{
    public required string Text { get; init; }

    public required GrammaticalGender Gender { get; init; }

    public required int Weight { get; init; }
}

/// <summary>Adjetivo do nome, com as duas formas; sem famílias, serve a qualquer uma.</summary>
public sealed class NameQualifier
{
    public required string Masculine { get; init; }

    public required string Feminine { get; init; }

    public required List<string> Families { get; init; }

    /// <summary>Primeiras palavras com que o termo soa redundante ("União Unida").</summary>
    public List<string> ExcludedHeads { get; init; } = [];

    public bool IsGeneric => Families.Count == 0;

    public string For(GrammaticalGender gender) => gender == GrammaticalGender.Masculine ? Masculine : Feminine;
}

public sealed class NameSuffix
{
    public required string Masculine { get; init; }

    public required string Feminine { get; init; }

    public string For(GrammaticalGender gender) => gender == GrammaticalGender.Masculine ? Masculine : Feminine;
}

/// <summary>Conteúdo de data/parties/name_terms.json (termos do GDD 14.1).</summary>
public sealed class NameTermCatalog : IValidatable
{
    public required List<NameHead> Heads { get; init; }

    public required List<NameQualifier> Qualifiers { get; init; }

    public required List<NameSuffix> Suffixes { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Heads is null || Heads.Count == 0)
        {
            yield return "a lista 'heads' está vazia.";
        }
        else
        {
            for (int i = 0; i < Heads.Count; i++)
            {
                NameHead? head = Heads[i];
                if (head is null)
                {
                    yield return $"heads[{i}]: item null.";
                    continue;
                }

                foreach (string error in DataChecks.Text(head.Text, "text", $"heads[{i}]"))
                {
                    yield return error;
                }

                if (head.Weight <= 0)
                {
                    yield return $"heads[{i}] '{head.Text}': 'weight' deve ser maior que zero.";
                }
            }
        }

        if (Qualifiers is null || Qualifiers.Count < 2)
        {
            yield return "a lista 'qualifiers' precisa de pelo menos dois termos.";
        }
        else
        {
            for (int i = 0; i < Qualifiers.Count; i++)
            {
                NameQualifier? qualifier = Qualifiers[i];
                if (qualifier is null)
                {
                    yield return $"qualifiers[{i}]: item null.";
                    continue;
                }

                foreach (string error in DataChecks.Text(qualifier.Masculine, "masculine", $"qualifiers[{i}]")
                    .Concat(DataChecks.Text(qualifier.Feminine, "feminine", $"qualifiers[{i}]")))
                {
                    yield return error;
                }

                foreach (string head in (qualifier.ExcludedHeads ?? []).Where(h => Heads is not null && !Heads.Any(x => x?.Text == h)))
                {
                    yield return $"qualifiers[{i}] '{qualifier.Masculine}': 'excluded_heads' cita '{head}', que não está em 'heads'.";
                }

                if (qualifier.Families is null)
                {
                    yield return $"qualifiers[{i}] '{qualifier.Masculine}': lista 'families' ausente (use [] para termo genérico).";
                }
            }

            if (!Qualifiers.Any(q => q?.Families is { Count: 0 }))
            {
                yield return "é preciso pelo menos um qualificador genérico (families vazio).";
            }
        }

        foreach (var (suffix, i) in (Suffixes ?? []).Select((s, i) => (s, i)))
        {
            if (suffix is null)
            {
                yield return $"suffixes[{i}]: item null.";
                continue;
            }

            foreach (string error in DataChecks.Text(suffix.Masculine, "masculine", $"suffixes[{i}]")
                .Concat(DataChecks.Text(suffix.Feminine, "feminine", $"suffixes[{i}]")))
            {
                yield return error;
            }
        }
    }

    public IEnumerable<string> ValidateAgainst(FamilyCatalog families)
    {
        foreach (NameQualifier? qualifier in Qualifiers ?? [])
        {
            foreach (string family in qualifier?.Families ?? [])
            {
                if (families.Find(family) is null)
                {
                    yield return $"qualificador '{qualifier!.Masculine}': família '{family}' não existe em families.json.";
                }
            }
        }
    }
}
