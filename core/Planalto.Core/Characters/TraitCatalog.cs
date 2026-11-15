using Planalto.Core.Data;

namespace Planalto.Core.Characters;

/// <summary>Conteúdo de data/traits.json.</summary>
public sealed class TraitCatalog : IValidatable
{
    public required List<TraitDefinition> Traits { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Traits is null || Traits.Count == 0)
        {
            yield return "a lista 'traits' está vazia.";
            yield break;
        }

        for (int i = 0; i < Traits.Count; i++)
        {
            TraitDefinition? trait = Traits[i];
            string where = $"traits[{i}]";
            if (trait is null)
            {
                yield return $"{where}: item null.";
                continue;
            }

            where = $"traits[{i}] '{trait.Id}'";
            foreach (string error in DataChecks.Id(trait.Id, where)
                .Concat(DataChecks.Gendered(trait.Name, "name", where))
                .Concat(DataChecks.Text(trait.Description, "description", where))
                .Concat(DataChecks.Modifiers(trait.AttributeModifiers, where)))
            {
                yield return error;
            }
        }

        foreach (string error in DataChecks.UniqueIds(Traits.Select(t => t?.Id), "Traço"))
        {
            yield return error;
        }
    }
}
