using Planalto.Core.Characters;
using Planalto.Core.Parties;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Characters;

/// <summary>Biografia sugerida na criação: idade, partido com o artigo certo e quem ainda não trabalhou.</summary>
public class BiographyTests
{
    private static readonly Lazy<PeopleContent> Lazy = new(() => PeopleContent.LoadOrThrow(TestContent.DataDirectory));
    private static PeopleContent People => Lazy.Value;
    private static readonly DateOnly Today = new(2028, 1, 1);

    private static Character Person(string profession, int age = 18)
    {
        Character c = PersonGenerator.Generate(new Rng(5), People, new PersonRequest("jogador", "MG", null, Today, Age: age));
        c.ProfessionId = profession;
        return c;
    }

    [Theory]
    [InlineData("União Solidária", "pela União Solidária", "a União Solidária")]
    [InlineData("Frente Popular", "pela Frente Popular", "a Frente Popular")]
    [InlineData("Partido Renovador", "pelo Partido Renovador", "o Partido Renovador")]
    [InlineData("Movimento Cívico", "pelo Movimento Cívico", "o Movimento Cívico")]
    public void O_artigo_do_partido_concorda_com_o_nome(string party, string pelo, string artigo)
    {
        GrammaticalGender gender = TestContent.Parties.NameTerms.GenderOf(party);
        Character c = Person("professor");
        for (ulong seed = 0; seed < 20; seed++)
        {
            string bio = People.Biography.Write(c, new Rng(seed), "Mantena", party, "Professor", Today, gender);
            Assert.True(bio.Contains(pelo, StringComparison.Ordinal) || bio.Contains("escolheu " + artigo, StringComparison.Ordinal), bio);
            Assert.DoesNotContain("{", bio, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Estudante_nao_trabalhou_como_estudante()
    {
        Character c = Person("estudante");
        for (ulong seed = 0; seed < 20; seed++)
        {
            string bio = People.Biography.Write(c, new Rng(seed), "Mantena", "Partido Renovador", "Estudante", Today);
            Assert.DoesNotContain("como estudante", bio, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("estud", bio, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(18)]
    [InlineData(42)]
    public void A_idade_da_biografia_e_a_do_personagem(int age)
    {
        Character c = Person("professor", age);
        string bio = People.Biography.Write(c, new Rng(1), "Mantena", "Partido Renovador", "Professor", Today);
        Assert.Contains($"{c.AgeOn(Today)} anos", bio, StringComparison.Ordinal);
    }

    [Fact]
    public void Modelos_usam_o_artigo_variavel_antes_do_partido() =>
        Assert.Empty(People.Biography.Validate());
}
