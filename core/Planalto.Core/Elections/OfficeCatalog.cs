using Planalto.Core.Characters;
using Planalto.Core.Data;

namespace Planalto.Core.Elections;

public sealed class OfficeDefinition
{
    public required string Id { get; init; }

    public required GenderedText Name { get; init; }

    /// <summary>Nível na escada de cargos (GDD 4.2).</summary>
    public required int Level { get; init; }
}

/// <summary>Conteúdo de data/offices.json: nome de cada cargo nas três formas de gênero.</summary>
public sealed class OfficeCatalog : IValidatable
{
    public const string File = "offices.json";

    public required List<OfficeDefinition> Offices { get; init; }

    public static string IdOf(Office office) => office switch
    {
        Office.Vereador => "vereador",
        Office.Prefeito => "prefeito",
        Office.DeputadoEstadual => "deputado_estadual",
        Office.DeputadoDistrital => "deputado_distrital",
        Office.DeputadoFederal => "deputado_federal",
        Office.Senador => "senador",
        Office.Governador => "governador",
        _ => "presidente",
    };

    public string Name(Office office, Gender gender) => Offices.First(o => o.Id == IdOf(office)).Name.For(gender);

    public IEnumerable<string> Validate()
    {
        if (Offices is null)
        {
            yield return "a lista 'offices' está vazia.";
            yield break;
        }

        foreach (Office office in Enum.GetValues<Office>().Where(o => Offices.All(x => x?.Id != IdOf(o))))
        {
            yield return $"falta o cargo '{IdOf(office)}'.";
        }

        foreach (OfficeDefinition? office in Offices)
        {
            foreach (string error in DataChecks.Gendered(office?.Name, "name", $"cargo '{office?.Id}'"))
            {
                yield return error;
            }
        }
    }
}
