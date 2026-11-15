using Planalto.Core.Data;
using Planalto.Core.Parties;

namespace Planalto.Core.Characters;

/// <summary>Camada de uma peça: em que posição da pilha entra, arquivo e canal de cor.</summary>
public sealed class PartLayer
{
    public const string ExpressionToken = "{expr}";

    public required string Slot { get; init; }

    /// <summary>Caminho relativo a art_root; com {expr} quando a peça varia com a expressão.</summary>
    public required string File { get; init; }

    /// <summary>Canal de tingimento (skin, hair, eyes, outfit_primary...) ou "none".</summary>
    public required string Tint { get; init; }

    public string FileFor(Expression expression) => File.Replace(ExpressionToken, ExpressionId(expression), StringComparison.Ordinal);

    public static string ExpressionId(Expression expression) => expression switch
    {
        Expression.Neutra => "neutra",
        Expression.Sorriso => "sorriso",
        Expression.SorrisoLargo => "sorriso_largo",
        Expression.Preocupado => "preocupado",
        Expression.Bravo => "bravo",
        Expression.Triste => "triste",
        Expression.Surpreso => "surpreso",
        Expression.Cansado => "cansado",
        _ => throw new ArgumentOutOfRangeException(nameof(expression)),
    };
}

/// <summary>Peça de aparência (GDD 7.4, catálogo data/appearance/parts.json).</summary>
public sealed class PartDefinition
{
    public required string Id { get; init; }

    public required string Category { get; init; }

    public required string Name { get; init; }

    public required List<PartLayer> Layers { get; init; }

    public required List<string> Tags { get; init; }

    /// <summary>Peso de sorteio: "default" e, opcionalmente, por gênero (masculino, feminino, nao_binario).</summary>
    public required Dictionary<string, double> Weights { get; init; }

    public bool Expressions { get; init; }

    /// <summary>Contextos do guarda-roupa em que a roupa pode ser usada.</summary>
    public List<WardrobeContext> Contexts { get; init; } = [];

    /// <summary>Estágio de calvície (1 entradas, 2 parcial, 3 careca), para o envelhecimento.</summary>
    public int? BaldingStage { get; init; }

    public double WeightFor(Gender gender) =>
        Weights.TryGetValue(GenderKey(gender), out double w) ? w : Weights.GetValueOrDefault("default", 1);

    public static string GenderKey(Gender gender) => gender switch
    {
        Gender.Masculino => "masculino",
        Gender.Feminino => "feminino",
        _ => "nao_binario",
    };
}

/// <summary>Conteúdo de data/appearance/parts.json.</summary>
public sealed class AppearanceCatalog : IValidatable
{
    public static readonly string[] Categories =
        ["body", "face", "ears", "eyes", "brows", "nose", "mouth", "hair", "beard", "mustache", "scar", "marks", "makeup", "glasses", "earrings", "piercing", "necklace", "pin", "outfit", "background"];

    public static readonly string[] Tints =
        ["none", "skin", "lips", "hair", "brows", "eyes", "facial_hair", "outfit_primary", "outfit_secondary", "lipstick", "eyeshadow", "glasses", "metal", "party", "background"];

    public required int Canvas { get; init; }

    public required string ArtRoot { get; init; }

    public required List<string> LayerOrder { get; init; }

    public required List<PartDefinition> Parts { get; init; }

    public PartDefinition? Find(string? id) => id is null ? null : Parts.FirstOrDefault(p => p.Id == id);

    public IReadOnlyList<PartDefinition> InCategory(string category) => Parts.Where(p => p.Category == category).ToList();

    public IEnumerable<string> Validate()
    {
        if (Parts is null || Parts.Count == 0 || LayerOrder is null || LayerOrder.Count == 0)
        {
            yield return "'parts' e 'layer_order' não podem ficar vazios.";
            yield break;
        }

        if (Canvas <= 0 || string.IsNullOrWhiteSpace(ArtRoot))
        {
            yield return "'canvas' positivo e 'art_root' obrigatório.";
        }

        foreach (string error in DataChecks.UniqueIds(Parts.Select(p => p?.Id), "Peça"))
        {
            yield return error;
        }

        var slots = LayerOrder.ToHashSet(StringComparer.Ordinal);
        foreach (PartDefinition? part in Parts)
        {
            if (part is null)
            {
                yield return "peça null.";
                continue;
            }

            string where = $"peça '{part.Id}'";
            foreach (string error in DataChecks.Id(part.Id, where).Concat(DataChecks.Text(part.Name, "name", where)))
            {
                yield return error;
            }

            if (!Categories.Contains(part.Category))
            {
                yield return $"{where}: categoria '{part.Category}' desconhecida.";
            }

            if (part.Weights is null || part.Weights.Values.Any(w => w < 0) || !part.Weights.ContainsKey("default"))
            {
                yield return $"{where}: 'weights' precisa de 'default' e pesos não negativos.";
            }

            if (part.Category == "outfit" && part.Contexts.Count == 0)
            {
                yield return $"{where}: roupa sem contexto do guarda-roupa.";
            }

            foreach (PartLayer? layer in part.Layers ?? [])
            {
                if (layer is null || !slots.Contains(layer.Slot) || !Tints.Contains(layer.Tint) || string.IsNullOrWhiteSpace(layer.File))
                {
                    yield return $"{where}: camada com slot, tint ou arquivo inválido.";
                }
                else if (layer.File.Contains(PartLayer.ExpressionToken, StringComparison.Ordinal) != part.Expressions)
                {
                    yield return $"{where}: '{{expr}}' no arquivo só e sempre quando 'expressions' é true.";
                }
            }
        }

        foreach (string category in Categories.Where(c => !Parts.Any(p => p?.Category == c)))
        {
            yield return $"nenhuma peça na categoria '{category}'.";
        }

        foreach (WardrobeContext context in Enum.GetValues<WardrobeContext>().Where(c => !Parts.Any(p => p?.Category == "outfit" && p.Contexts.Contains(c))))
        {
            yield return $"nenhuma roupa para o contexto '{context}'.";
        }
    }
}

/// <summary>Conteúdo de data/appearance/palettes.json.</summary>
public sealed class AppearancePalettes : IValidatable
{
    public required List<string> Skin { get; init; }

    /// <summary>Peso de cada tom de pele por cor ou raça do Censo (DECISOES D-016).</summary>
    public required Dictionary<string, List<double>> SkinByCorRaca { get; init; }

    public required List<string> HairNatural { get; init; }

    public required List<string> HairDyed { get; init; }

    public required string Gray { get; init; }

    public required List<string> Eyes { get; init; }

    public required List<string> MakeupLipstick { get; init; }

    public required List<string> MakeupEyeshadow { get; init; }

    public required List<string> Outfit { get; init; }

    public required List<string> Background { get; init; }

    public required string Metal { get; init; }

    public required string Glasses { get; init; }

    public IEnumerable<string> Validate()
    {
        var lists = new (string Name, List<string>? Colors)[]
        {
            ("skin", Skin), ("hair_natural", HairNatural), ("hair_dyed", HairDyed), ("eyes", Eyes), ("makeup_lipstick", MakeupLipstick),
            ("makeup_eyeshadow", MakeupEyeshadow), ("outfit", Outfit), ("background", Background),
        };
        foreach (var (name, colors) in lists)
        {
            if (colors is null || colors.Count == 0 || colors.Any(c => !OkColor.IsValidHex(c)))
            {
                yield return $"'{name}': lista vazia ou com cor inválida (use #RRGGBB).";
            }
        }

        foreach (var (name, color) in new[] { ("gray", Gray), ("metal", Metal), ("glasses", Glasses) })
        {
            if (!OkColor.IsValidHex(color))
            {
                yield return $"'{name}': cor inválida.";
            }
        }

        if (Skin is { Count: not 24 })
        {
            yield return $"'skin' precisa de 24 tons (GDD 3.1), tem {Skin.Count}.";
        }

        foreach (var (category, weights) in SkinByCorRaca ?? [])
        {
            if (weights is null || weights.Count != (Skin?.Count ?? 0) || weights.Any(w => w < 0) || weights.Sum() <= 0)
            {
                yield return $"skin_by_cor_raca '{category}': um peso não negativo por tom, com soma positiva.";
            }
        }
    }
}

public sealed class AgingConfig
{
    public required int GrayStartAge { get; init; }

    public required int GrayFullAge { get; init; }

    public required int WrinkleStartAge { get; init; }

    public required int WrinkleFullAge { get; init; }

    public required int EyebagsStartAge { get; init; }

    public required int EyebagsFullAge { get; init; }

    /// <summary>Estresse (0–100) acelera o envelhecimento visual: anos extras = estresse/100 × fator × idade.</summary>
    public required double StressAgingFactor { get; init; }

    /// <summary>Chance, por década acima dos 30 anos, de a calvície avançar um estágio, por gênero.</summary>
    public required Dictionary<string, double> BaldingChancePerDecade { get; init; }
}

/// <summary>Conteúdo de data/appearance/generator.json: chances de cada elemento e envelhecimento.</summary>
public sealed class AppearanceGeneratorConfig : IValidatable
{
    public required Dictionary<string, Dictionary<string, double>> Presence { get; init; }

    public required AgingConfig Aging { get; init; }

    public double Chance(string element, Gender gender) =>
        Presence.TryGetValue(element, out var byGender) ? byGender.GetValueOrDefault(PartDefinition.GenderKey(gender)) : 0;

    public IEnumerable<string> Validate()
    {
        foreach (var (element, byGender) in Presence ?? [])
        {
            if (byGender is null || byGender.Values.Any(v => v is < 0 or > 1))
            {
                yield return $"presence '{element}': probabilidades entre 0 e 1.";
            }
        }

        if (Aging is null || Aging.GrayFullAge <= Aging.GrayStartAge || Aging.WrinkleFullAge <= Aging.WrinkleStartAge
            || Aging.EyebagsFullAge <= Aging.EyebagsStartAge || Aging.StressAgingFactor < 0
            || (Aging.BaldingChancePerDecade ?? []).Values.Any(v => v is < 0 or > 1))
        {
            yield return "aging: idades finais maiores que as iniciais, fator de estresse não negativo, chances entre 0 e 1.";
        }
    }
}

/// <summary>Conteúdo de data/appearance/cor_raca_uf.json (Censo 2022, SIDRA 9605), gerado pelo DataImporter.</summary>
public sealed class CorRacaUfFile : IValidatable
{
    public required string Fonte { get; init; }

    public required string Url { get; init; }

    public required DateOnly ConsultadoEm { get; init; }

    /// <summary>UF → categoria (branca, preta, amarela, parda, indigena) → pessoas.</summary>
    public required Dictionary<string, Dictionary<string, long>> Ufs { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Ufs is null || Ufs.Count != 27)
        {
            yield return $"'ufs' deve ter as 27 UFs (tem {Ufs?.Count ?? 0}).";
            yield break;
        }

        foreach (var (uf, counts) in Ufs)
        {
            if (counts is null || counts.Count == 0 || counts.Values.Any(v => v < 0) || counts.Values.Sum() <= 0)
            {
                yield return $"UF {uf}: contagens vazias ou negativas.";
            }
        }
    }
}
