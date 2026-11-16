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
    string? PartyId = null,
    AgeRange? Ages = null);

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
        // Jogador: faixa de idade inicial; NPCs: faixa própria (DECISOES D-036).
        AgeRange ages = request.Ages ?? content.Origins.NpcAge;
        int age = request.Age ?? rng.NextInt(ages.Min, ages.Max + 1);
        DateOnly birth = request.Today.AddYears(-age).AddDays(-rng.NextInt(365));

        string first = FirstName(rng, content, request.Uf, gender, birth.Year);
        string surname = Surname(rng, content);
        string second = rng.Chance(0.6) ? Surname(rng, content) : "";
        string civil = string.Join(' ', new[] { first, second, surname }.Where(s => s.Length > 0).Distinct());

        // Só profissões possíveis na idade (formação e registro profissional).
        ProfessionDefinition profession = request.ProfessionId is null
            ? rng.Pick(content.Professions.Professions.Where(p => p.AvailableAt(age)).ToList())
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
            MaritalStatus = rng.Pick(FamilyRules.AllowedStatuses(content.Family, age).ToList()),
            Children = rng.NextInt(0, FamilyRules.MaxChildren(content.Family, age) + 1),
        };

        foreach (string trait in rng.Shuffled(content.Traits.Traits.Where(t => t.SelectableAtCreation).Select(t => t.Id)).Take(2))
        {
            character.TraitIds.Add(trait);
        }

        ApplyStartingStats(character, rng, content, origin, profession);
        character.Appearance = AppearanceGenerator.Random(rng, content.Appearance, gender, age, request.Uf);
        FamilyRules.Generate(rng, content, character, request.Today);
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

    /// <summary>
    /// Prenome pela frequência do IBGE na década de nascimento e na UF (caindo para o ranking geral da UF e
    /// depois para o do Brasil se faltar dado), somado aos prenomes complementares, cuja frequência nacional na
    /// década é escalada pelo peso da UF. Pessoas não binárias sorteiam entre as duas listas (DECISOES D-017).
    /// </summary>
    public static string FirstName(Rng rng, PeopleContent content, string uf, Gender gender, int? birthYear = null)
    {
        FirstNamesFile names = content.FirstNames;
        bool masculine = gender switch
        {
            Gender.Masculino => true,
            Gender.Feminino => false,
            _ => rng.Chance(0.5),
        };

        static List<RankedName> Pick(NamesBySex s, bool m) => m ? s.Masculino : s.Feminino;

        int decade = names.DecadeKey(birthYear ?? 1980);
        NamesByPlace? place = names.ByDecade.GetValueOrDefault(decade);
        List<RankedName>? local = place is not null && place.Ufs.TryGetValue(uf, out NamesBySex? byUfDecade) ? Pick(byUfDecade, masculine) : null;
        local ??= names.Ufs.TryGetValue(uf, out NamesBySex? byUf) ? Pick(byUf, masculine) : Pick(names.Brasil, masculine);
        var options = local
            .Where(n => !content.Names.Excluded.Contains(n.Nome))
            .Select(n => (n.Nome, Weight: (double)n.Frequencia))
            .ToList();

        // Escala dos complementares: quanto a lista da UF pesa frente à do Brasil na mesma década.
        List<RankedName> national = place is not null ? Pick(place.Brasil, masculine) : Pick(names.Brasil, masculine);
        double scale = national.Sum(n => (double)n.Frequencia) is double total and > 0 ? local.Sum(n => (double)n.Frequencia) / total : 0;
        var known = options.Select(o => o.Nome).ToHashSet(StringComparer.Ordinal);
        foreach (DecadeName extra in masculine ? names.Complementary.Masculino : names.Complementary.Feminino)
        {
            if (!known.Contains(extra.Nome) && extra.ByDecade.GetValueOrDefault(decade) is long f and > 0)
            {
                options.Add((extra.Nome, f * scale));
            }
        }

        return content.Names.Display(rng.PickWeighted(options, o => o.Weight).Nome);
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
