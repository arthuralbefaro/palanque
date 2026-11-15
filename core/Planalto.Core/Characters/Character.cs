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

    /// <summary>Aparência do retrato; nunca altera atributos nem votos.</summary>
    public Appearance? Appearance { get; set; }

    public string? HomeUf { get; set; }

    /// <summary>Código IBGE da cidade natal.</summary>
    public int? HomeMunicipio { get; set; }

    /// <summary>Id de data/social_origins.json.</summary>
    public string? SocialOrigin { get; set; }

    /// <summary>Religião (opcional), com os mesmos ids dos grupos de segmentos; null = não declarada.</summary>
    public string? Religion { get; set; }

    public MaritalStatus MaritalStatus { get; set; } = MaritalStatus.Solteiro;

    public int Children { get; set; }

    public string? PartyId { get; set; }

    public string? Biography { get; set; }

    public CampaignIdentity? Campaign { get; set; }

    public int AgeOn(DateOnly date)
    {
        int age = date.Year - BirthDate.Year;
        return date < BirthDate.AddYears(age) ? age - 1 : age;
    }
}

public enum MaritalStatus
{
    Solteiro,
    Casado,
    UniaoEstavel,
    Divorciado,
    Viuvo,
}

/// <summary>Identidade visual de campanha (GDD 3.4).</summary>
public sealed class CampaignIdentity
{
    /// <summary>Número de urna completo; o prefixo é o número do partido.</summary>
    public string? BallotNumber { get; set; }

    public required string Slogan { get; set; }

    public required string LogoIcon { get; set; }

    public required string LogoFont { get; set; }

    public required string LogoPrimaryColor { get; set; }

    public required string LogoSecondaryColor { get; set; }

    public required string Jingle { get; set; }

    public required string PhotoPose { get; set; }

    public Expression PhotoExpression { get; set; } = Expression.Sorriso;

    public required string PhotoBackground { get; set; }
}
