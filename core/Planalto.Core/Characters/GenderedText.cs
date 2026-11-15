namespace Planalto.Core.Characters;

/// <summary>Texto com as três formas usadas conforme o gênero do personagem.</summary>
public sealed record GenderedText
{
    public required string Masculine { get; init; }

    public required string Feminine { get; init; }

    public required string Neutral { get; init; }

    public string For(Gender gender) => gender switch
    {
        Gender.Masculino => Masculine,
        Gender.Feminino => Feminine,
        _ => Neutral,
    };
}
