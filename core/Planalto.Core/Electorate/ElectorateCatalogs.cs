using Planalto.Core.Data;
using Planalto.Core.Ideology;

namespace Planalto.Core.Electorate;

public sealed record AxisShift(double Economic, double Social);

/// <summary>Grupo de uma dimensão (ex.: renda "baixa"), formado por categorias do perfil do Censo.</summary>
public sealed class SegmentGroup
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required List<string> Categories { get; init; }

    /// <summary>Voto facultativo (16–17 e 70+): comparecimento multiplicado pelo fator de vote_model.json.</summary>
    public bool Facultative { get; init; }

    public required AxisShift Ideology { get; init; }

    /// <summary>Ajuste somado ao comparecimento base.</summary>
    public required double Turnout { get; init; }

    /// <summary>Peso de cada tema na prioridade do grupo.</summary>
    public required Dictionary<string, double> Themes { get; init; }
}

/// <summary>Dimensão dos segmentos, ligada a um bloco do perfil do município (situacao, idade, renda...).</summary>
public sealed class SegmentDimension
{
    public static readonly string[] ProfileBlocks = ["situacao", "idade", "renda", "religiao", "escolaridade"];

    public required string Id { get; init; }

    public required string ProfileBlock { get; init; }

    /// <summary>Categorias fora do denominador (ex.: 0–15 anos, "não sabe").</summary>
    public required List<string> ExcludedCategories { get; init; }

    public required List<SegmentGroup> Groups { get; init; }
}

/// <summary>Conteúdo de data/electorate/segments.json (DECISOES D-012).</summary>
public sealed class SegmentCatalog : IValidatable
{
    public required List<SegmentDimension> Dimensions { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Dimensions is null || Dimensions.Count == 0)
        {
            yield return "a lista 'dimensions' está vazia.";
            yield break;
        }

        foreach (string error in DataChecks.UniqueIds(Dimensions.Select(d => d?.Id), "Dimensão"))
        {
            yield return error;
        }

        foreach (SegmentDimension? dimension in Dimensions)
        {
            if (dimension is null)
            {
                yield return "dimensão null.";
                continue;
            }

            string where = $"dimensão '{dimension.Id}'";
            if (!SegmentDimension.ProfileBlocks.Contains(dimension.ProfileBlock))
            {
                yield return $"{where}: 'profile_block' '{dimension.ProfileBlock}' não é um bloco do perfil ({string.Join(", ", SegmentDimension.ProfileBlocks)}).";
            }

            if (dimension.Groups is null || dimension.Groups.Count < 2)
            {
                yield return $"{where}: precisa de ao menos dois grupos.";
                continue;
            }

            foreach (string error in DataChecks.UniqueIds(dimension.Groups.Select(g => g?.Id), $"{where}: grupo"))
            {
                yield return error;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SegmentGroup? group in dimension.Groups)
            {
                if (group is null)
                {
                    yield return $"{where}: grupo null.";
                    continue;
                }

                foreach (string error in DataChecks.Id(group.Id, $"{where}, grupo").Concat(DataChecks.Text(group.Name, "name", $"{where}, grupo '{group.Id}'")))
                {
                    yield return error;
                }

                if (group.Categories is null || group.Categories.Count == 0)
                {
                    yield return $"{where}, grupo '{group.Id}': sem categorias.";
                }

                foreach (string category in group.Categories ?? [])
                {
                    if (!seen.Add(category) || (dimension.ExcludedCategories ?? []).Contains(category))
                    {
                        yield return $"{where}: categoria '{category}' em mais de um grupo ou também excluída.";
                    }
                }

                if (Math.Abs(group.Ideology?.Economic ?? 0) > AxisRange.AxisMax || Math.Abs(group.Ideology?.Social ?? 0) > AxisRange.AxisMax)
                {
                    yield return $"{where}, grupo '{group.Id}': deslocamento ideológico fora de ±100.";
                }

                if (Math.Abs(group.Turnout) >= 1 || (group.Themes ?? []).Values.Any(w => w < 0))
                {
                    yield return $"{where}, grupo '{group.Id}': ajuste de comparecimento em (-1, 1) e pesos de tema não negativos.";
                }
            }
        }
    }

    /// <summary>Confere temas contra themes.json e categorias contra as chaves reais do perfil dos municípios.</summary>
    public IEnumerable<string> ValidateAgainst(ThemeCatalog themes, IReadOnlyDictionary<string, IReadOnlyCollection<string>> profileCategories)
    {
        foreach (SegmentDimension dimension in Dimensions)
        {
            foreach (SegmentGroup group in dimension.Groups)
            {
                foreach (string theme in group.Themes.Keys.Where(t => !themes.Contains(t)))
                {
                    yield return $"grupo '{group.Id}': tema '{theme}' não existe em themes.json.";
                }
            }

            if (!profileCategories.TryGetValue(dimension.ProfileBlock, out IReadOnlyCollection<string>? categories))
            {
                continue;
            }

            var covered = dimension.Groups.SelectMany(g => g.Categories).Concat(dimension.ExcludedCategories).ToHashSet(StringComparer.Ordinal);
            foreach (string missing in categories.Where(c => !covered.Contains(c)))
            {
                yield return $"dimensão '{dimension.Id}': categoria '{missing}' do perfil sem grupo nem exclusão.";
            }

            foreach (string unknown in covered.Where(c => !categories.Contains(c)))
            {
                yield return $"dimensão '{dimension.Id}': categoria '{unknown}' não existe no perfil.";
            }
        }
    }
}

public sealed class PollConfig
{
    public required double ConfidenceZ { get; init; }

    public required double UndecidedAtCampaignStart { get; init; }

    public required double UndecidedAtElection { get; init; }

    public required int CampaignWeeks { get; init; }
}

public sealed class VoteWeights
{
    public required double Ideology { get; init; }

    public required double Themes { get; init; }

    public required double Recognition { get; init; }

    public required double Image { get; init; }

    public required double Party { get; init; }

    public required double Support { get; init; }

    public double Total => Ideology + Themes + Recognition + Image + Party + Support;
}

/// <summary>Conteúdo de data/electorate/vote_model.json (DECISOES D-013 e D-014).</summary>
public sealed class VoteModelConfig : IValidatable
{
    public required VoteWeights Weights { get; init; }

    public required double Temperature { get; init; }

    public required double NoiseSd { get; init; }

    public required double BaseTurnout { get; init; }

    public required double FacultativeTurnoutFactor { get; init; }

    public required double BlankNullBase { get; init; }

    public required double BlankNullLowUtility { get; init; }

    public required double BlankNullMax { get; init; }

    /// <summary>Divisor que leva o eixo (-100 a 100) à escala dos temas (-2 a 2).</summary>
    public required double ThemeAxisDivisor { get; init; }

    public required double AffinityProximityScale { get; init; }

    public required int AffinityIpfIterations { get; init; }

    public required double RandomWorldCenterSd { get; init; }

    public required double RandomWorldStrengthNoiseSd { get; init; }

    public required PollConfig Poll { get; init; }

    /// <summary>
    /// Conjunto de consideração: o eleitor só vota em quem conhece. A fatia de cada candidato é multiplicada por
    /// max(reconhecimento, piso) elevado ao expoente (0 desliga). Com reconhecimento igual para todos, o fator se cancela.
    /// </summary>
    public required AwarenessConfig Awareness { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Awareness is null || Awareness.Exponent < 0 || Awareness.Floor is <= 0 or > 1)
        {
            yield return "awareness: expoente não negativo e piso em (0, 1].";
        }

        if (Weights is null || new[] { Weights.Ideology, Weights.Themes, Weights.Recognition, Weights.Image, Weights.Party, Weights.Support }.Any(w => w < 0) || Weights.Total <= 0)
        {
            yield return "weights: pesos não negativos com soma positiva.";
        }

        if (Temperature <= 0 || NoiseSd < 0 || ThemeAxisDivisor <= 0 || AffinityProximityScale <= 0 || AffinityIpfIterations < 1 || RandomWorldCenterSd < 0 || RandomWorldStrengthNoiseSd < 0)
        {
            yield return "temperature, theme_axis_divisor e affinity_proximity_scale positivos; ruídos não negativos; ao menos 1 iteração de IPF.";
        }

        foreach (var (value, name) in new[]
        {
            (BaseTurnout, "base_turnout"), (FacultativeTurnoutFactor, "facultative_turnout_factor"), (BlankNullBase, "blank_null_base"),
            (BlankNullLowUtility, "blank_null_low_utility"), (BlankNullMax, "blank_null_max"),
        })
        {
            if (value is < 0 or > 1)
            {
                yield return $"{name} deve ficar entre 0 e 1.";
            }
        }

        if (Poll is null || Poll.ConfidenceZ <= 0 || Poll.CampaignWeeks < 1
            || Poll.UndecidedAtCampaignStart is < 0 or > 1 || Poll.UndecidedAtElection is < 0 or > 1)
        {
            yield return "poll: confidence_z e campaign_weeks positivos; indecisos entre 0 e 1.";
        }
    }
}

public sealed class PollsterDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Pesquisa interna contratada pela campanha (GDD 5.3): mais cara e mais precisa.</summary>
    public required bool Internal { get; init; }

    public required int SampleMin { get; init; }

    public required int SampleMax { get; init; }

    public required long Cost { get; init; }

    /// <summary>Viés de casa: inclina as intenções pelo eixo econômico do candidato (negativo = favorece a esquerda).</summary>
    public required double HouseBias { get; init; }

    public required double DesignEffect { get; init; }
}

/// <summary>Conteúdo de data/electorate/pollsters.json.</summary>
public sealed class PollsterCatalog : IValidatable
{
    public required List<PollsterDefinition> Pollsters { get; init; }

    public PollsterDefinition Get(string id) =>
        Pollsters.FirstOrDefault(p => p.Id == id) ?? throw new KeyNotFoundException($"Instituto '{id}' não existe.");

    public IEnumerable<string> Validate()
    {
        if (Pollsters is null || Pollsters.Count == 0)
        {
            yield return "a lista 'pollsters' está vazia.";
            yield break;
        }

        foreach (string error in DataChecks.UniqueIds(Pollsters.Select(p => p?.Id), "Instituto"))
        {
            yield return error;
        }

        foreach (PollsterDefinition? p in Pollsters)
        {
            if (p is null)
            {
                yield return "instituto null.";
                continue;
            }

            foreach (string error in DataChecks.Id(p.Id, "instituto").Concat(DataChecks.Text(p.Name, "name", $"instituto '{p.Id}'")))
            {
                yield return error;
            }

            if (p.SampleMin < 100 || p.SampleMax < p.SampleMin || p.Cost < 0 || p.DesignEffect < 1 || Math.Abs(p.HouseBias) > 1)
            {
                yield return $"instituto '{p.Id}': amostra de ao menos 100 com 'min' até 'max', custo não negativo, efeito de desenho >= 1 e viés em [-1, 1].";
            }

            if (p.Internal && p.HouseBias != 0)
            {
                yield return $"instituto '{p.Id}': pesquisa interna não tem viés de casa.";
            }
        }

        // Neutralidade: os vieses dos institutos públicos se anulam.
        double bias = Pollsters.Where(p => p is { Internal: false }).Sum(p => p.HouseBias);
        if (Math.Abs(bias) > 1e-9)
        {
            yield return $"a soma dos vieses dos institutos públicos deve ser zero (é {bias:0.###}).";
        }

        if (!Pollsters.Any(p => p is { Internal: true }))
        {
            yield return "falta a pesquisa interna.";
        }
    }
}

public sealed class AwarenessConfig
{
    public required double Exponent { get; init; }

    /// <summary>Reconhecimento mínimo considerado: santinhos e a própria urna fazem alguém conhecer qualquer nome.</summary>
    public required double Floor { get; init; }
}
