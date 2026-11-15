namespace Planalto.Core.Characters;

/// <summary>Profissão anterior carregada de data/professions.json.</summary>
public sealed class ProfessionDefinition
{
    public required string Id { get; init; }

    public required GenderedText Name { get; init; }

    public required string Description { get; init; }

    public Dictionary<AttributeId, int> AttributeModifiers { get; init; } = [];
}
