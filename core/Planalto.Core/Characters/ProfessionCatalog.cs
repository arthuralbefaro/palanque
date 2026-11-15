using Planalto.Core.Data;

namespace Planalto.Core.Characters;

/// <summary>Conteúdo de data/professions.json.</summary>
public sealed class ProfessionCatalog : IValidatable
{
    public required List<ProfessionDefinition> Professions { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Professions is null || Professions.Count == 0)
        {
            yield return "a lista 'professions' está vazia.";
            yield break;
        }

        for (int i = 0; i < Professions.Count; i++)
        {
            ProfessionDefinition? profession = Professions[i];
            string where = $"professions[{i}]";
            if (profession is null)
            {
                yield return $"{where}: item null.";
                continue;
            }

            where = $"professions[{i}] '{profession.Id}'";
            foreach (string error in DataChecks.Id(profession.Id, where)
                .Concat(DataChecks.Gendered(profession.Name, "name", where))
                .Concat(DataChecks.Text(profession.Description, "description", where))
                .Concat(DataChecks.Modifiers(profession.AttributeModifiers, where)))
            {
                yield return error;
            }
        }

        foreach (string error in DataChecks.UniqueIds(Professions.Select(p => p?.Id), "Profissão"))
        {
            yield return error;
        }
    }
}
