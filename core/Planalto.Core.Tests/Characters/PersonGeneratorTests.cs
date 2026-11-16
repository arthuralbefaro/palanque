using System.Text.Json;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Characters;

public class PersonGeneratorTests
{
    private static readonly Lazy<PeopleContent> Lazy = new(() => PeopleContent.LoadOrThrow(TestContent.DataDirectory));

    private static PeopleContent Content => Lazy.Value;

    private static readonly DateOnly Today = new(2028, 1, 3);

    [Fact]
    public void Gera_PessoasCompletasEValidas()
    {
        var rng = new Rng(1);
        for (int i = 0; i < 200; i++)
        {
            Character c = PersonGenerator.Generate(rng, Content, new PersonRequest($"npc_{i}", "PE", 2611606, Today));

            Assert.False(string.IsNullOrWhiteSpace(c.CivilName));
            Assert.False(string.IsNullOrWhiteSpace(c.BallotName));
            Assert.InRange(c.AgeOn(Today), Content.Origins.StartingAge.Min, Content.Origins.StartingAge.Max);
            Assert.Equal(2, c.TraitIds.Count);
            Assert.NotNull(c.Appearance);
            Assert.Contains(c.ProfessionId, Content.Professions.Professions.Select(p => p.Id));
            Assert.DoesNotContain("{", c.BallotName);
            Assert.DoesNotContain("Filha", c.CivilName.Split(' ')[0]);
        }
    }

    [Fact]
    public void Prenomes_VemDoRankingDaUfComAcentos()
    {
        var rng = new Rng(2);
        var names = Enumerable.Range(0, 400).Select(_ => PersonGenerator.FirstName(rng, Content, "SP", Gender.Masculino)).ToHashSet();

        Assert.Contains("José", names);
        Assert.DoesNotContain("JOSE", names);
        Assert.All(names, n => Assert.True(char.IsUpper(n[0]) && n[1..] == n[1..].ToLowerInvariant() || n.Any(c => c > 127), n));
    }

    [Theory]
    [InlineData(Gender.Masculino)]
    [InlineData(Gender.Feminino)]
    [Trait("Categoria", "Lento")]
    public void Prenomes_TemAoMenos150DistintosPorSexoNoBrasil(Gender gender)
    {
        var rng = new Rng(3);
        string[] ufs = Content.FirstNames.Ufs.Keys.ToArray();
        var names = new HashSet<string>();
        for (int i = 0; i < 40_000; i++)
        {
            names.Add(PersonGenerator.FirstName(rng, Content, rng.Pick(ufs), gender, rng.NextInt(1925, 2010)));
        }

        Assert.True(names.Count >= 150, $"{names.Count} prenomes distintos");
    }

    [Fact]
    public void Prenomes_SeguemADecadaDeNascimento()
    {
        int Count(string name, int birthYear)
        {
            var rng = new Rng(4);
            return Enumerable.Range(0, 3000).Count(_ => PersonGenerator.FirstName(rng, Content, "SP", Gender.Feminino, birthYear) == name);
        }

        // Vitória quase não aparece entre as nascidas nos anos 1950 e é comum entre as nascidas nos anos 2000.
        Assert.True(Count("Vitória", 2005) > Count("Vitória", 1955) + 20);
    }

    [Fact]
    public void Prenomes_ComplementaresTemAcento()
    {
        var rng = new Rng(5);
        var names = Enumerable.Range(0, 20_000).Select(_ => PersonGenerator.FirstName(rng, Content, "MG", Gender.Masculino, rng.NextInt(1950, 2005))).ToHashSet();

        Assert.DoesNotContain("Mauricio", names);
        Assert.DoesNotContain("Filho", names);
    }

    [Fact]
    public void Geracao_EDeterministicaPelaSemente()
    {
        Character a = PersonGenerator.Generate(new Rng(5), Content, new PersonRequest("x", "RS", null, Today));
        Character b = PersonGenerator.Generate(new Rng(5), Content, new PersonRequest("x", "RS", null, Today));

        Assert.Equal(JsonSerializer.Serialize(a, JsonData.Options), JsonSerializer.Serialize(b, JsonData.Options));
    }

    [Fact]
    public void PedidoFixo_RespeitaGeneroIdadeEProfissao()
    {
        Character c = PersonGenerator.Generate(new Rng(6), Content, new PersonRequest("y", "BA", null, Today, Gender.Feminino, 33, "medico"));

        Assert.Equal(Gender.Feminino, c.Gender);
        Assert.Equal(33, c.AgeOn(Today));
        Assert.Equal("medico", c.ProfessionId);
    }

    [Fact]
    public void Origem_DefineRecursosIniciais()
    {
        Character c = PersonGenerator.Generate(new Rng(7), Content, new PersonRequest("z", "MG", 3106200, Today));
        SocialOriginDefinition origin = Content.Origins.Get(c.SocialOrigin!);

        Assert.Equal(origin.Money, c.Resources.Money);
        Assert.Equal(origin.Recognition, c.Resources.GetRecognition("3106200"));
    }
}
