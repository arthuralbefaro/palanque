using Planalto.Core.Data;
using Planalto.Core.State;

namespace Planalto.Core.World;

/// <summary>Frequência de eventos aleatórios (GDD 16.1).</summary>
public enum EventFrequency
{
    Baixa,
    Normal,
    Alta,
    Caotica,
}

/// <summary>Nível de realismo jurídico (GDD 16.1).</summary>
public enum LegalRealism
{
    Relaxado,
    Rigoroso,
}

/// <summary>Opções gerais do mundo (GDD 16.1).</summary>
public sealed class WorldOptions
{
    public required int StartYear { get; set; }

    /// <summary>Id de uma dificuldade de data/world_options.json.</summary>
    public required string Difficulty { get; set; }

    public required EventFrequency EventFrequency { get; set; }

    public required bool Ironman { get; set; }

    public required LegalRealism LegalRealism { get; set; }

    public WorldOptions Clone() => (WorldOptions)MemberwiseClone();
}

public sealed class DifficultyDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>Conteúdo de data/world_options.json: dificuldades, ano mínimo e valores padrão.</summary>
public sealed class WorldOptionsCatalog : IValidatable
{
    /// <summary>Primeiro ano inicial permitido; o ano inicial precisa ser de eleição.</summary>
    public required int MinStartYear { get; init; }

    public required List<DifficultyDefinition> Difficulties { get; init; }

    public required WorldOptions Defaults { get; init; }

    public bool IsValidStartYear(int year) => year >= MinStartYear && Calendar.ElectionKindOf(year) is not null;

    public bool HasDifficulty(string? id) => Difficulties.Any(d => d.Id == id);

    public IEnumerable<string> Validate()
    {
        if (Difficulties is null || Difficulties.Count == 0)
        {
            yield return "a lista 'difficulties' está vazia.";
            yield break;
        }

        for (int i = 0; i < Difficulties.Count; i++)
        {
            DifficultyDefinition? difficulty = Difficulties[i];
            if (difficulty is null)
            {
                yield return $"difficulties[{i}]: item null.";
                continue;
            }

            foreach (string error in DataChecks.Id(difficulty.Id, $"difficulties[{i}]")
                .Concat(DataChecks.Text(difficulty.Name, "name", $"difficulties[{i}] '{difficulty.Id}'")))
            {
                yield return error;
            }
        }

        foreach (string error in DataChecks.UniqueIds(Difficulties.Select(d => d?.Id), "Dificuldade"))
        {
            yield return error;
        }

        if (Calendar.ElectionKindOf(MinStartYear) is null)
        {
            yield return $"'min_start_year' {MinStartYear} não é ano de eleição.";
        }

        if (Defaults is null)
        {
            yield return "'defaults' ausente.";
            yield break;
        }

        if (!IsValidStartYear(Defaults.StartYear))
        {
            yield return $"defaults.start_year {Defaults.StartYear} deve ser ano de eleição a partir de {MinStartYear}.";
        }

        if (!HasDifficulty(Defaults.Difficulty))
        {
            yield return $"defaults.difficulty '{Defaults.Difficulty}' não existe em 'difficulties'.";
        }
    }
}
