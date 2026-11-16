using Planalto.Core.Data;
using Planalto.Core.State;

namespace Planalto.Core.Characters;

public enum FamilyRelation
{
    Conjuge,
    Filho,
}

/// <summary>Cônjuge ou filho do personagem (GDD 3.3), com idade coerente com a dele.</summary>
public sealed class FamilyMember
{
    public required FamilyRelation Relation { get; init; }

    public required string Name { get; init; }

    public required Gender Gender { get; init; }

    public required DateOnly BirthDate { get; init; }

    public int AgeOn(DateOnly date)
    {
        int age = date.Year - BirthDate.Year;
        return BirthDate.AddYears(age) > date ? age - 1 : age;
    }
}

public sealed class ChildrenCap
{
    /// <summary>A partir desta idade do personagem...</summary>
    public required int Age { get; init; }

    /// <summary>...o teto de filhos é este.</summary>
    public required int Max { get; init; }
}

public sealed class SpouseAgeGap
{
    public required int Min { get; init; }

    public required int Max { get; init; }
}

/// <summary>Regras de família da criação de personagem (data/family.json).</summary>
public sealed class FamilyConfig : IValidatable
{
    public const string File = "family.json";

    public required string Note { get; init; }

    public required List<ChildrenCap> ChildrenCapByAge { get; init; }

    /// <summary>Idade mínima do personagem no nascimento de um filho.</summary>
    public required int MinParentAge { get; init; }

    public required int ChildSpacingYears { get; init; }

    /// <summary>Idade mínima por estado civil, com a chave no mesmo formato do JSON ("uniao_estavel").</summary>
    public required Dictionary<string, int> MaritalMinAge { get; init; }

    public static string Key(MaritalStatus status) => System.Text.Json.JsonNamingPolicy.SnakeCaseLower.ConvertName(status.ToString());

    public int MinAgeFor(MaritalStatus status) => MaritalMinAge.GetValueOrDefault(Key(status));

    public required SpouseAgeGap SpouseAgeGap { get; init; }

    public required int SpouseMinAge { get; init; }

    public IEnumerable<string> Validate()
    {
        if (ChildrenCapByAge.Count == 0 || ChildrenCapByAge.Any(c => c.Max < 0) || ChildrenCapByAge.Zip(ChildrenCapByAge.Skip(1)).Any(p => p.Second.Age <= p.First.Age || p.Second.Max < p.First.Max))
        {
            yield return "children_cap_by_age: idades crescentes e tetos que não diminuem.";
        }

        if (MinParentAge < 10 || ChildSpacingYears < 0 || SpouseMinAge < 16 || SpouseAgeGap.Min > SpouseAgeGap.Max)
        {
            yield return "min_parent_age, child_spacing_years, spouse_min_age e spouse_age_gap inválidos.";
        }

        // Com o teto, sempre tem de caber cada filho numa idade de nascimento válida.
        foreach (ChildrenCap cap in ChildrenCapByAge.Where(c => c.Max > 0))
        {
            int slots = ((cap.Age - MinParentAge) / Math.Max(1, ChildSpacingYears)) + 1;
            if (cap.Age < MinParentAge || slots < cap.Max)
            {
                yield return $"children_cap_by_age: {cap.Max} filhos aos {cap.Age} anos não cabem com min_parent_age e child_spacing_years.";
            }
        }

        foreach (MaritalStatus status in Enum.GetValues<MaritalStatus>().Where(s => !MaritalMinAge.ContainsKey(Key(s))))
        {
            yield return $"marital_min_age: falta '{Key(status)}'.";
        }

        foreach (string key in MaritalMinAge.Keys.Where(k => Enum.GetValues<MaritalStatus>().All(s => Key(s) != k)))
        {
            yield return $"marital_min_age: estado civil '{key}' desconhecido.";
        }
    }
}

/// <summary>Teto de filhos, estado civil possível e geração de cônjuge e filhos pela idade do personagem.</summary>
public static class FamilyRules
{
    public static int MaxChildren(FamilyConfig config, int age) =>
        config.ChildrenCapByAge.Where(c => age >= c.Age).Select(c => c.Max).DefaultIfEmpty(0).Max();

    public static bool Allows(FamilyConfig config, MaritalStatus status, int age) => age >= config.MinAgeFor(status);

    public static IEnumerable<MaritalStatus> AllowedStatuses(FamilyConfig config, int age) =>
        Enum.GetValues<MaritalStatus>().Where(s => Allows(config, s, age));

    public static bool HasSpouse(MaritalStatus status) => status is MaritalStatus.Casado or MaritalStatus.UniaoEstavel;

    /// <summary>
    /// Ajusta estado civil e número de filhos ao que a idade permite e gera cônjuge e filhos: o cônjuge na faixa de
    /// diferença de idade; cada filho nascido com o personagem já em idade mínima, com intervalo entre eles.
    /// </summary>
    public static void Generate(Rng rng, PeopleContent content, Character character, DateOnly today)
    {
        FamilyConfig config = content.Family;
        int age = character.AgeOn(today);
        if (!Allows(config, character.MaritalStatus, age))
        {
            character.MaritalStatus = MaritalStatus.Solteiro;
        }

        character.Family.Clear();
        string uf = character.HomeUf ?? "SP";
        if (HasSpouse(character.MaritalStatus))
        {
            int spouseAge = Math.Max(config.SpouseMinAge, age + rng.NextInt(config.SpouseAgeGap.Min, config.SpouseAgeGap.Max + 1));
            DateOnly birth = today.AddYears(-spouseAge).AddDays(-rng.NextInt(365));
            Gender gender = PersonGenerator.PickGender(rng);
            character.Family.Add(new FamilyMember
            {
                Relation = FamilyRelation.Conjuge,
                Name = PersonGenerator.FirstName(rng, content, uf, gender, birth.Year),
                Gender = gender,
                BirthDate = birth,
            });
        }

        // Idades do personagem em cada nascimento, espaçadas, entre a mínima e a atual.
        var parentAges = new List<int>();
        foreach (int candidate in rng.Shuffled(Enumerable.Range(config.MinParentAge, Math.Max(0, age - config.MinParentAge + 1)).ToList()))
        {
            if (parentAges.Count >= Math.Clamp(character.Children, 0, MaxChildren(config, age)))
            {
                break;
            }

            if (parentAges.All(p => Math.Abs(p - candidate) >= config.ChildSpacingYears))
            {
                parentAges.Add(candidate);
            }
        }

        foreach (int parentAge in parentAges.Order())
        {
            DateOnly birth = character.BirthDate.AddYears(parentAge).AddDays(rng.NextInt(0, 300));
            if (birth >= today)
            {
                birth = today.AddDays(-1);
            }

            Gender gender = rng.Chance(0.5) ? Gender.Feminino : Gender.Masculino;
            character.Family.Add(new FamilyMember
            {
                Relation = FamilyRelation.Filho,
                Name = PersonGenerator.FirstName(rng, content, uf, gender, birth.Year),
                Gender = gender,
                BirthDate = birth,
            });
        }

        character.Children = parentAges.Count;
    }

    /// <summary>O que está incoerente na família do personagem para a idade dele (vazio = tudo certo).</summary>
    public static IEnumerable<string> Problems(FamilyConfig config, Character character, DateOnly today)
    {
        int age = character.AgeOn(today);
        if (!Allows(config, character.MaritalStatus, age))
        {
            yield return $"estado civil {character.MaritalStatus} aos {age} anos.";
        }

        var children = character.Family.Where(f => f.Relation == FamilyRelation.Filho).ToList();
        if (children.Count != character.Children || character.Children > MaxChildren(config, age))
        {
            yield return $"{character.Children} filhos ({children.Count} gerados) aos {age} anos; teto {MaxChildren(config, age)}.";
        }

        foreach (FamilyMember child in children.Where(c => c.BirthDate < character.BirthDate.AddYears(config.MinParentAge) || c.BirthDate > today))
        {
            yield return $"filho {child.Name} nascido em {child.BirthDate}, fora do intervalo possível.";
        }

        var spouses = character.Family.Where(f => f.Relation == FamilyRelation.Conjuge).ToList();
        if (spouses.Count != (HasSpouse(character.MaritalStatus) ? 1 : 0))
        {
            yield return $"{spouses.Count} cônjuges para o estado civil {character.MaritalStatus}.";
        }

        foreach (FamilyMember spouse in spouses.Where(s => s.AgeOn(today) < config.SpouseMinAge || s.AgeOn(today) > age + config.SpouseAgeGap.Max + 1))
        {
            yield return $"cônjuge com {spouse.AgeOn(today)} anos para personagem de {age}.";
        }
    }
}
