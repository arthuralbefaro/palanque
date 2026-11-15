using Planalto.Core.Data;

namespace Planalto.Core.Municipios;

/// <summary>Faixa de porte do município (GDD 12.4). A última faixa não tem limite.</summary>
public sealed class PorteDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>População máxima, inclusive; null na última faixa.</summary>
    public long? MaxPopulation { get; init; }
}

/// <summary>Conteúdo de data/portes.json.</summary>
public sealed class PorteCatalog : IValidatable
{
    public required List<PorteDefinition> Portes { get; init; }

    /// <summary>Id do porte para a população informada.</summary>
    public string Classify(long populacao)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(populacao);
        return Portes.First(p => p.MaxPopulation is null || populacao <= p.MaxPopulation).Id;
    }

    public IEnumerable<string> Validate()
    {
        if (Portes is null || Portes.Count == 0)
        {
            yield return "a lista 'portes' está vazia.";
            yield break;
        }

        for (int i = 0; i < Portes.Count; i++)
        {
            PorteDefinition? porte = Portes[i];
            string where = $"portes[{i}]";
            if (porte is null)
            {
                yield return $"{where}: item null.";
                continue;
            }

            foreach (string error in DataChecks.Id(porte.Id, where).Concat(DataChecks.Text(porte.Name, "name", where)))
            {
                yield return error;
            }

            bool isLast = i == Portes.Count - 1;
            if (isLast && porte.MaxPopulation is not null)
            {
                yield return $"{where}: a última faixa não pode ter 'max_population'.";
            }
            else if (!isLast && porte.MaxPopulation is null)
            {
                yield return $"{where}: só a última faixa pode ficar sem 'max_population'.";
            }
            else if (!isLast && i > 0 && Portes[i - 1]?.MaxPopulation is long previous && porte.MaxPopulation <= previous)
            {
                yield return $"{where}: 'max_population' deve ser maior que o da faixa anterior.";
            }
        }

        foreach (string error in DataChecks.UniqueIds(Portes.Select(p => p?.Id), "Porte"))
        {
            yield return error;
        }
    }
}
