namespace Planalto.Core.Characters;

public sealed class Character
{
    public required string Id { get; init; }

    public required string CivilName { get; set; }

    public required string BallotName { get; set; }

    public required Gender Gender { get; set; }

    public required DateOnly BirthDate { get; init; }

    public string? ProfessionId { get; set; }

    public List<string> TraitIds { get; init; } = [];

    public Attributes Attributes { get; init; } = new();

    public Resources Resources { get; init; } = new();

    public int AgeOn(DateOnly date)
    {
        int age = date.Year - BirthDate.Year;
        return date < BirthDate.AddYears(age) ? age - 1 : age;
    }
}
