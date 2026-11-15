using Planalto.Core.Characters;

namespace Planalto.Core.Tests.Characters;

public class CharacterTests
{
    [Theory]
    [InlineData(-10, 0)]
    [InlineData(0, 0)]
    [InlineData(55, 55)]
    [InlineData(100, 100)]
    [InlineData(140, 100)]
    public void Atributos_SaoLimitadosEntreZeroECem(int value, int expected)
    {
        var attributes = new Attributes();

        foreach (AttributeId id in Enum.GetValues<AttributeId>())
        {
            attributes.Set(id, value);
            Assert.Equal(expected, attributes.Get(id));
        }
    }

    [Fact]
    public void Apply_SomaModificadoresRespeitandoOLimite()
    {
        var attributes = new Attributes { Oratoria = 98, Carisma = 3 };

        attributes.Apply(new Dictionary<AttributeId, int>
        {
            [AttributeId.Oratoria] = 5,
            [AttributeId.Carisma] = -5,
            [AttributeId.Gestao] = 5,
        });

        Assert.Equal(100, attributes.Oratoria);
        Assert.Equal(0, attributes.Carisma);
        Assert.Equal(5, attributes.Gestao);
    }

    [Fact]
    public void Recursos_RespeitamLimites()
    {
        var resources = new Resources
        {
            Money = -15000m,
            PoliticalCapital = -3,
            Energy = 120,
            Health = -1,
            Stress = 50,
        };
        resources.SetRecognition("NE", 130);
        resources.AddRecognition("SE", -10);

        Assert.Equal(-15000m, resources.Money);
        Assert.Equal(0, resources.PoliticalCapital);
        Assert.Equal(100, resources.Energy);
        Assert.Equal(0, resources.Health);
        Assert.Equal(50, resources.Stress);
        Assert.Equal(100, resources.GetRecognition("NE"));
        Assert.Equal(0, resources.GetRecognition("SE"));
        Assert.Equal(0, resources.GetRecognition("S"));
    }

    [Theory]
    [InlineData("2028-03-14", 29)]
    [InlineData("2028-03-15", 30)]
    [InlineData("2029-01-01", 30)]
    public void AgeOn_ConsideraSeOAniversarioJaPassou(string date, int expected)
    {
        var character = new Character
        {
            Id = "jogador",
            CivilName = "Ana Souza",
            BallotName = "Ana da Saúde",
            Gender = Gender.Feminino,
            BirthDate = new DateOnly(1998, 3, 15),
        };

        Assert.Equal(expected, character.AgeOn(DateOnly.Parse(date)));
    }

    [Fact]
    public void GenderedText_EscolheAFormaPeloGenero()
    {
        var text = new GenderedText { Masculine = "Médico", Feminine = "Médica", Neutral = "Médico(a)" };

        Assert.Equal("Médico", text.For(Gender.Masculino));
        Assert.Equal("Médica", text.For(Gender.Feminino));
        Assert.Equal("Médico(a)", text.For(Gender.NaoBinario));
    }
}
