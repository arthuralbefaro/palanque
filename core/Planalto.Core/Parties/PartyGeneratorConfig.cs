using Planalto.Core.Data;

namespace Planalto.Core.Parties;

public sealed record DoubleRange(double Min, double Max);

/// <summary>Faixa de luminosidade e croma OKLCh de um tom de cor secundária.</summary>
public sealed class ToneConfig
{
    public required DoubleRange Lightness { get; init; }

    public required DoubleRange Chroma { get; init; }
}

public sealed class SiglaRules
{
    public required int MinLength { get; init; }

    public required int MaxLength { get; init; }
}

public sealed class NameGenerationConfig
{
    public required double SecondQualifierChance { get; init; }

    public required double SuffixChance { get; init; }

    /// <summary>Peso dos termos ligados à família do partido.</summary>
    public required double FamilyTermWeight { get; init; }

    /// <summary>Peso dos termos genéricos, que servem a qualquer família.</summary>
    public required double GenericTermWeight { get; init; }

    public required int MaxAttempts { get; init; }
}

/// <summary>Geração de cores no espaço OKLCh, com distância mínima entre cores primárias do mundo.</summary>
public sealed class ColorGenerationConfig
{
    public required DoubleRange Lightness { get; init; }

    public required DoubleRange Chroma { get; init; }

    /// <summary>Distância OKLab mínima desejada entre primárias.</summary>
    public required double MinDistance { get; init; }

    /// <summary>Distância mínima depois de todos os relaxamentos; abaixo dela, nunca aceita.</summary>
    public required double MinDistanceFloor { get; init; }

    /// <summary>Fator aplicado à distância quando não acha cor livre em <see cref="AttemptsPerLevel"/> tentativas.</summary>
    public required double RelaxFactor { get; init; }

    public required int AttemptsPerLevel { get; init; }

    /// <summary>Distância OKLab mínima entre a primária e a secundária do mesmo partido.</summary>
    public required double SecondaryMinDistance { get; init; }

    /// <summary>Secundária neutra clara.</summary>
    public required ToneConfig SecondaryLight { get; init; }

    /// <summary>Secundária neutra escura.</summary>
    public required ToneConfig SecondaryDark { get; init; }
}

public sealed class SizeWarningConfig
{
    public required int MaxDominant { get; init; }

    /// <summary>Participação implícita máxima de um nanico (peso dele sobre a soma dos pesos).</summary>
    public required double MaxNanicoShare { get; init; }

    /// <summary>Pelo menos um partido precisa ter este tamanho ou maior.</summary>
    public required PartySize MinMainSize { get; init; }
}

/// <summary>Conteúdo de data/parties/generator.json.</summary>
public sealed class PartyGeneratorConfig : IValidatable
{
    public required SiglaRules Sigla { get; init; }

    public required NameGenerationConfig Names { get; init; }

    public required ColorGenerationConfig Colors { get; init; }

    /// <summary>Peso relativo de cada tamanho, usado para a participação implícita.</summary>
    public required Dictionary<PartySize, int> SizeShareWeights { get; init; }

    public required SizeWarningConfig SizeWarnings { get; init; }

    public required Dictionary<LeadershipStyle, int> LeadershipWeights { get; init; }

    public double ShareOf(PartySize size, IEnumerable<PartySize> allSizes)
    {
        double total = allSizes.Sum(s => SizeShareWeights[s]);
        return total <= 0 ? 0 : SizeShareWeights[size] / total;
    }

    public IEnumerable<string> Validate()
    {
        if (Sigla is null || Sigla.MinLength < 1 || Sigla.MaxLength < Sigla.MinLength)
        {
            yield return "sigla: 'min_length' deve ser ao menos 1 e não maior que 'max_length'.";
        }

        if (Names is null)
        {
            yield return "'names' ausente.";
        }
        else
        {
            foreach (string error in Probability(Names.SecondQualifierChance, "names.second_qualifier_chance")
                .Concat(Probability(Names.SuffixChance, "names.suffix_chance")))
            {
                yield return error;
            }

            if (Names.FamilyTermWeight <= 0 || Names.GenericTermWeight <= 0 || Names.MaxAttempts <= 0)
            {
                yield return "names: pesos e 'max_attempts' devem ser maiores que zero.";
            }
        }

        foreach (string error in ValidateColors(Colors))
        {
            yield return error;
        }

        foreach (string error in AllPositive(SizeShareWeights, "size_share_weights")
            .Concat(AllPositive(LeadershipWeights, "leadership_weights")))
        {
            yield return error;
        }

        if (SizeWarnings is null)
        {
            yield return "'size_warnings' ausente.";
        }
        else
        {
            if (SizeWarnings.MaxDominant < 1)
            {
                yield return "size_warnings.max_dominant deve ser ao menos 1.";
            }

            foreach (string error in Probability(SizeWarnings.MaxNanicoShare, "size_warnings.max_nanico_share"))
            {
                yield return error;
            }
        }
    }

    private static IEnumerable<string> ValidateColors(ColorGenerationConfig? colors)
    {
        if (colors is null)
        {
            yield return "'colors' ausente.";
            yield break;
        }

        if (colors.Lightness is null || colors.Lightness.Min < 0 || colors.Lightness.Max > 1 || colors.Lightness.Min >= colors.Lightness.Max)
        {
            yield return "colors.lightness deve ficar em [0, 1] com 'min' menor que 'max'.";
        }

        if (colors.Chroma is null || colors.Chroma.Min < 0 || colors.Chroma.Min >= colors.Chroma.Max)
        {
            yield return "colors.chroma deve ter 'min' >= 0 e menor que 'max'.";
        }

        if (colors.MinDistanceFloor <= 0 || colors.MinDistance < colors.MinDistanceFloor)
        {
            yield return "colors: 'min_distance_floor' deve ser positivo e não maior que 'min_distance'.";
        }

        if (colors.RelaxFactor is <= 0 or >= 1)
        {
            yield return "colors.relax_factor deve ficar entre 0 e 1 (exclusive).";
        }

        foreach (var (tone, field) in new[] { (colors.SecondaryLight, "secondary_light"), (colors.SecondaryDark, "secondary_dark") })
        {
            if (tone?.Lightness is null || tone.Chroma is null
                || tone.Lightness.Min < 0 || tone.Lightness.Max > 1 || tone.Lightness.Min > tone.Lightness.Max
                || tone.Chroma.Min < 0 || tone.Chroma.Min > tone.Chroma.Max)
            {
                yield return $"colors.{field}: faixas de 'lightness' em [0, 1] e 'chroma' >= 0, com 'min' até 'max'.";
            }
        }

        if (colors.AttemptsPerLevel <= 0 || colors.SecondaryMinDistance <= 0)
        {
            yield return "colors: 'attempts_per_level' e 'secondary_min_distance' devem ser maiores que zero.";
        }
    }

    private static IEnumerable<string> Probability(double value, string field)
    {
        if (value is < 0 or > 1)
        {
            yield return $"{field} deve ficar entre 0 e 1 (valor: {value}).";
        }
    }

    private static IEnumerable<string> AllPositive<TKey>(Dictionary<TKey, int>? weights, string field)
        where TKey : struct, Enum
    {
        if (weights is null)
        {
            yield return $"'{field}' ausente.";
            yield break;
        }

        foreach (TKey key in Enum.GetValues<TKey>())
        {
            if (!weights.TryGetValue(key, out int weight) || weight <= 0)
            {
                yield return $"{field}: falta peso positivo para '{key.ToString().ToLowerInvariant()}'.";
            }
        }
    }
}
