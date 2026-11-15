namespace Planalto.Core.Characters;

/// <summary>Expressões do retrato (GDD 3.6); trocam só olhos, sobrancelhas e boca.</summary>
public enum Expression
{
    Neutra,
    Sorriso,
    SorrisoLargo,
    Preocupado,
    Bravo,
    Triste,
    Surpreso,
    Cansado,
}

/// <summary>Contextos do guarda-roupa (GDD 3.2).</summary>
public enum WardrobeContext
{
    Institucional,
    Campanha,
    Debate,
    Casual,
    Regional,
}

public enum SkinUndertone
{
    Quente,
    Neutro,
    Frio,
}

/// <summary>Peça de roupa escolhida para um contexto, com as duas cores.</summary>
public sealed record OutfitChoice(string PartId, string PrimaryColor, string SecondaryColor);

/// <summary>
/// Aparência do personagem (GDD 3.1 e 7.4): ids de peça por camada, cores em hexadecimal e parâmetros
/// de 0 a 1. Nunca altera atributos nem votos (CLAUDE.md, Neutralidade).
/// </summary>
public sealed record Appearance
{
    public required string Body { get; init; }

    public double ShoulderWidth { get; init; } = 0.5;

    /// <summary>Altura relativa: afeta só o enquadramento.</summary>
    public double Height { get; init; } = 0.5;

    public required string SkinColor { get; init; }

    public SkinUndertone Undertone { get; init; } = SkinUndertone.Neutro;

    public required string Face { get; init; }

    public double Jaw { get; init; } = 0.5;

    public double Chin { get; init; } = 0.5;

    public double Cheekbones { get; init; } = 0.5;

    public required string Eyes { get; init; }

    public double EyeSize { get; init; } = 0.5;

    /// <summary>Inclinação dos olhos, de -1 a 1.</summary>
    public double EyeTilt { get; init; }

    public required string EyeColor { get; init; }

    public required string Brows { get; init; }

    public double BrowThickness { get; init; } = 0.5;

    public required string BrowColor { get; init; }

    public required string Nose { get; init; }

    public double NoseWidth { get; init; } = 0.5;

    public required string Mouth { get; init; }

    public double LipThickness { get; init; } = 0.5;

    public required string Ears { get; init; }

    public required string Hair { get; init; }

    public required string HairColor { get; init; }

    /// <summary>Grisalho de 0 a 1.</summary>
    public double HairGray { get; init; }

    public string? Beard { get; init; }

    public string? Mustache { get; init; }

    public required string FacialHairColor { get; init; }

    public double FacialHairGray { get; init; }

    public double Freckles { get; init; }

    public bool Moles { get; init; }

    public string? Scar { get; init; }

    public double Wrinkles { get; init; }

    public double EyeBags { get; init; }

    public bool Dimples { get; init; }

    public string? LipstickColor { get; init; }

    public double LipstickIntensity { get; init; }

    public string? EyeshadowColor { get; init; }

    public double EyeshadowIntensity { get; init; }

    public double EyelinerIntensity { get; init; }

    public string? Glasses { get; init; }

    public string? Earrings { get; init; }

    public string? Piercing { get; init; }

    public string? Necklace { get; init; }

    /// <summary>Broche do partido na lapela.</summary>
    public bool PartyPin { get; init; }

    /// <summary>Sorteada na criação (0 a 1): quanto menor, mais cedo a calvície avança com a idade.</summary>
    public double BaldingPropensity { get; init; } = 1;

    public required Dictionary<WardrobeContext, OutfitChoice> Wardrobe { get; init; }
}
