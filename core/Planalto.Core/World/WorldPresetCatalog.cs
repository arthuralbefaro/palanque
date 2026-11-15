using Planalto.Core.Data;
using Planalto.Core.Parties;

namespace Planalto.Core.World;

/// <summary>Predefinição da tela "Novo Mundo" (GDD 16.1).</summary>
public sealed class WorldPreset
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>False para predefinições registradas mas ainda sem dados (ex.: Brasil 2026 antes da 1D).</summary>
    public required bool Available { get; init; }

    public string? UnavailableReason { get; init; }

    /// <summary>Predefinição livre: vale como faixa geral do mundo quando o jogador edita fora da faixa de outra.</summary>
    public bool Custom { get; init; }

    public int? MinParties { get; init; }

    public int? MaxParties { get; init; }

    /// <summary>Predefinição cuja geração esta usa como ponto de partida (lista inicial, pesos e tamanhos).</summary>
    public string? StartsFrom { get; init; }

    /// <summary>Cenário espelhado do real que fornece a lista de partidos (GDD 16.4); null nos sorteados.</summary>
    public string? Scenario { get; init; }

    /// <summary>Multiplicador do peso de sorteio de cada família; as ausentes usam 1.</summary>
    public Dictionary<string, double> FamilyWeightMultipliers { get; init; } = [];

    /// <summary>Distribuição de tamanhos; a quantidade de cada um sai do rateio por maiores restos.</summary>
    public Dictionary<PartySize, int> SizeWeights { get; init; } = [];

    /// <summary>Tendência de famílias a receberem os maiores tamanhos; as ausentes usam 1.</summary>
    public Dictionary<string, double> LargeSizeFamilyBias { get; init; } = [];

    public double FamilyMultiplier(string familyId) => FamilyWeightMultipliers.GetValueOrDefault(familyId, 1);

    public double SizeBias(string familyId) => LargeSizeFamilyBias.GetValueOrDefault(familyId, 1);
}

/// <summary>Conteúdo de data/world_presets.json.</summary>
public sealed class WorldPresetCatalog : IValidatable
{
    public required string DefaultPreset { get; init; }

    public required List<WorldPreset> Presets { get; init; }

    public WorldPreset? Find(string? id) => Presets.FirstOrDefault(p => p.Id == id);

    /// <summary>A predefinição livre, cuja faixa de partidos é o limite geral do mundo.</summary>
    public WorldPreset Custom => Presets.First(p => p.Custom);

    /// <summary>Predefinição que fornece pesos, tamanhos e faixa para gerar a lista de <paramref name="preset"/>.</summary>
    public WorldPreset GenerationSourceOf(WorldPreset preset) =>
        preset.StartsFrom is null ? preset : Find(preset.StartsFrom) ?? preset;

    public IEnumerable<string> Validate()
    {
        if (Presets is null || Presets.Count == 0)
        {
            yield return "a lista 'presets' está vazia.";
            yield break;
        }

        for (int i = 0; i < Presets.Count; i++)
        {
            WorldPreset? preset = Presets[i];
            string where = $"presets[{i}]";
            if (preset is null)
            {
                yield return $"{where}: item null.";
                continue;
            }

            where = $"presets[{i}] '{preset.Id}'";
            foreach (string error in DataChecks.Id(preset.Id, where)
                .Concat(DataChecks.Text(preset.Name, "name", where))
                .Concat(DataChecks.Text(preset.Description, "description", where))
                .Concat(ValidatePreset(preset, where)))
            {
                yield return error;
            }
        }

        foreach (string error in DataChecks.UniqueIds(Presets.Select(p => p?.Id), "Predefinição"))
        {
            yield return error;
        }

        int customCount = Presets.Count(p => p is { Custom: true });
        if (customCount != 1)
        {
            yield return $"deve haver exatamente uma predefinição com 'custom': true (há {customCount}).";
        }

        if (Find(DefaultPreset) is not { Available: true })
        {
            yield return $"'default_preset' '{DefaultPreset}' não existe ou não está disponível.";
        }
    }

    public IEnumerable<string> ValidateAgainst(FamilyCatalog families)
    {
        foreach (WorldPreset? preset in Presets)
        {
            if (preset is null)
            {
                continue;
            }

            foreach (string family in preset.FamilyWeightMultipliers.Keys.Concat(preset.LargeSizeFamilyBias.Keys))
            {
                if (families.Find(family) is null)
                {
                    yield return $"predefinição '{preset.Id}': família '{family}' não existe em families.json.";
                }
            }
        }
    }

    private IEnumerable<string> ValidatePreset(WorldPreset preset, string where)
    {
        if (!preset.Available || preset.Scenario is not null)
        {
            if (string.IsNullOrWhiteSpace(preset.UnavailableReason))
            {
                yield return $"{where}: predefinição indisponível precisa de 'unavailable_reason'.";
            }

            if (preset.Custom)
            {
                yield return $"{where}: a predefinição livre não pode ficar indisponível.";
            }

            if (!preset.Available)
            {
                yield break;
            }
        }

        if (preset.Scenario is not null)
        {
            if (preset.Custom || preset.MinParties is not null || preset.MaxParties is not null || preset.StartsFrom is null)
            {
                yield return $"{where}: predefinição de cenário não é livre, não tem faixa de partidos e precisa de 'starts_from' para os sorteios.";
            }
        }
        else if (preset.MinParties is not int min || preset.MaxParties is not int max || min < 2 || max < min)
        {
            yield return $"{where}: 'min_parties' e 'max_parties' obrigatórios, com mínimo de 2 e 'min' até 'max'.";
        }

        if (preset.StartsFrom is not null)
        {
            WorldPreset? source = Find(preset.StartsFrom);
            if (source is null || source.StartsFrom is not null || !source.Available)
            {
                yield return $"{where}: 'starts_from' '{preset.StartsFrom}' deve ser uma predefinição disponível sem 'starts_from'.";
            }

            yield break;
        }

        if (preset.SizeWeights.Values.Any(w => w < 0) || preset.SizeWeights.Values.Sum() <= 0)
        {
            yield return $"{where}: 'size_weights' precisa de pesos não negativos e soma positiva.";
        }

        if (preset.FamilyWeightMultipliers.Values.Concat(preset.LargeSizeFamilyBias.Values).Any(w => w <= 0))
        {
            yield return $"{where}: multiplicadores de família devem ser maiores que zero.";
        }
    }
}
