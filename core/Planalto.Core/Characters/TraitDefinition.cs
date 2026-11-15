namespace Planalto.Core.Characters;

/// <summary>Traço de personalidade carregado de data/traits.json.</summary>
public sealed class TraitDefinition
{
    public required string Id { get; init; }

    public required GenderedText Name { get; init; }

    public required string Description { get; init; }

    /// <summary>False para traços que só se ganham durante a carreira.</summary>
    public bool SelectableAtCreation { get; init; } = true;

    public Dictionary<AttributeId, int> AttributeModifiers { get; init; } = [];
}
