using Planalto.Core.State;

namespace Planalto.Core.Characters;

/// <summary>Pedido de geração: quem é a pessoa e de onde vem; campos null são sorteados.</summary>
public sealed record PersonRequest(
    string Id,
    string Uf,
    int? HomeMunicipio,
    DateOnly Today,
    Gender? Gender = null,
    int? Age = null,
    string? ProfessionId = null,
    string? PartyId = null);

/// <summary>
/// Gera jogador e NPCs com o mesmo sistema (GDD 3.8): prenomes do IBGE pela UF, sobrenomes, nome de urna,
/// profissão, traços, origem social, atributos e aparência com pesos regionais. Determinístico pelo Rng.
/// </summary>
public static class PersonGenerator
{
    /// <summary>Fração de pessoas não binárias entre as geradas (as demais se dividem igualmente).</summary>
    private const double NonBinaryShare = 0.02;

    public static Character Generate(Rng rng, PeopleContent content, PersonRequest request)
    {
        Gender gender = request.Gender ?? PickGender(rng);
        AgeRange ages = content.Origins.StartingAge;
        int age = request.Age ?? rng.NextInt(ages.Min, ages.Max + 1);
        DateOnly birth = request.Today.AddYears(-age).AddDays(-rng.NextInt(365));

        string first = FirstName(rng, content, request.Uf, gender);
        string surname = Surname(rng, content);
        string second = rng.Chance(0.6) ? Surname(rng, content) : "";
        string civil = string.Join(' ', new[] { first, second, surname }.Where(s => s.Length > 0).Distinct());

        ProfessionDefinition profession = request.ProfessionId is null
            ? rng.Pick(content.Professions.Professions)
            : content.Professions.Professions.First(p => p.Id == request.ProfessionId);
        SocialOriginDefinition origin = rng.Pick(content.Origins.Origins);

        var character = new Character
        {
            Id = request.Id,
            CivilName = civil,
            BallotName = BallotName(rng, content, first, surname, profession.Id, gender),
            Gender = gender,
            BirthDate = birth,
            ProfessionId = profession.Id,
            HomeUf = request.Uf,
            HomeMunicipio = request.HomeMunicipio,
            SocialOrigin = origin.Id,
            PartyId = request.PartyId,
            MaritalStatus = rng.Pick(Enum.GetValues<MaritalStatus>()),
            Children = rng.NextInt(0, 4),
        };

        foreach (string trait in rng.Shuffled(content.Traits.Traits.Where(t => t.SelectableAtCreation).Select(t => t.Id)).Take(2))
        {
            character.TraitIds.Add(trait);
        }

        ApplyStartingStats(character, rng, content, origin, profession);
        character.Appearance = AppearanceGenerator.Random(rng, content.Appearance, gender, age, request.Uf);
        return character;
    }

    /// <summary>Atributos iniciais (base ± dispersão, mais origem, profissão e traços) e recursos da origem.</summary>
    public static void ApplyStartingStats(Character character, Rng rng, PeopleContent content, SocialOriginDefinition origin, ProfessionDefinition profession)
    {
        StartingAttributes start = content.Origins.StartingAttributes;
        foreach (AttributeId id in Enum.GetValues<AttributeId>())
        {
            int value = start.Base + rng.NextInt(-start.Spread, start.Spread + 1)
                + origin.AttributeModifiers.GetValueOrDefault(id)
                + profession.AttributeModifiers.GetValueOrDefault(id)
                + character.TraitIds.Sum(t => content.Traits.Traits.First(x => x.Id == t).AttributeModifiers.GetValueOrDefault(id));
            character.Attributes.Set(id, value);
        }

        character.Resources.Money = origin.Money;
        character.Resources.PoliticalCapital = origin.PoliticalCapital;
        character.Resources.Energy = content.Origins.StartingEnergy;
        character.Resources.Health = content.Origins.StartingHealth;
        if (character.HomeMunicipio is int home)
        {
            character.Resources.SetRecognition(home.ToString(System.Globalization.CultureInfo.InvariantCulture), origin.Recognition);
        }
    }

    public static Gender PickGender(Rng rng) =>
        rng.Chance(NonBinaryShare) ? Gender.NaoBinario : rng.Chance(0.5) ? Gender.Feminino : Gender.Masculino;

    /// <summary>Prenome pela frequência do IBGE na UF; pessoas não binárias sorteiam entre as duas listas.</summary>
    public static string FirstName(Rng rng, PeopleContent content, string uf, Gender gender)
    {
        NamesBySex names = content.FirstNames.Ufs.TryGetValue(uf, out NamesBySex? byUf) ? byUf : content.FirstNames.Brasil;
        List<RankedName> list = gender switch
        {
            Gender.Masculino => names.Masculino,
            Gender.Feminino => names.Feminino,
            _ => rng.Chance(0.5) ? names.Masculino : names.Feminino,
        };
        var valid = list.Where(n => !content.Names.Excluded.Contains(n.Nome)).ToList();
        return content.Names.Display(rng.PickWeighted(valid, n => n.Frequencia).Nome);
    }

    public static string Surname(Rng rng, PeopleContent content) =>
        rng.PickWeighted(content.Names.Surnames.ToList(), s => s.Value).Key;

    public static string BallotName(Rng rng, PeopleContent content, string first, string surname, string professionId, Gender gender)
    {
        var patterns = content.Names.BallotPatterns
            .Where(p => !p.Contains("{titulo}", StringComparison.Ordinal) || content.Names.Titles.ContainsKey(professionId))
            .ToList();
        string pattern = rng.Pick(patterns);
        string title = content.Names.Titles.TryGetValue(professionId, out GenderedText? t) ? t.For(gender) : "";
        return pattern
            .Replace("{prenome}", first, StringComparison.Ordinal)
            .Replace("{sobrenome}", surname, StringComparison.Ordinal)
            .Replace("{inicial}", surname[..1], StringComparison.Ordinal)
            .Replace("{titulo}", title, StringComparison.Ordinal)
            .Trim();
    }
}
