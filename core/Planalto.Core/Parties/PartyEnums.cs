namespace Planalto.Core.Parties;

/// <summary>Tamanho inicial do partido (GDD 16.1).</summary>
public enum PartySize
{
    Nanico,
    Pequeno,
    Medio,
    Grande,
    Dominante,
}

/// <summary>Personalidade da cúpula partidária (GDD 16.1).</summary>
public enum LeadershipStyle
{
    Ideologica,
    Pragmatica,
    Fisiologica,
    Personalista,
}

public enum GrammaticalGender
{
    Masculine,
    Feminine,
}

/// <summary>Cores do partido em hexadecimal sRGB ("#RRGGBB").</summary>
public sealed record PartyColors(string Primary, string Secondary);
