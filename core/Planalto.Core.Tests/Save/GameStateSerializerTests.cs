using Planalto.Core.Characters;
using Planalto.Core.Save;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Save;

public class GameStateSerializerTests
{
    private static GameState CreateState()
    {
        var rng = new Rng(987654321);
        for (int i = 0; i < 37; i++)
        {
            rng.NextUInt64();
        }

        var player = new Character
        {
            Id = "jogador",
            CivilName = "José Carlos Lima",
            BallotName = "Zé da Saúde",
            Gender = Gender.Masculino,
            BirthDate = new DateOnly(1994, 7, 2),
            ProfessionId = "medico",
            TraitIds = ["bom_de_papo", "idealista"],
            Attributes = new Attributes { Carisma = 55, Oratoria = 62, Articulacao = 40, Gestao = 58, Integridade = 71 },
            Resources = new Resources { Money = 12500.50m, PoliticalCapital = 8, Energy = 80, Health = 90, Stress = 15 },
        };
        player.Resources.SetRecognition("NE", 12);
        player.Resources.SetRecognition("2927408", 30);

        var calendar = Calendar.FromIsoWeek(2028, 10);
        calendar.AdvanceWeek();

        return new GameState { Calendar = calendar, Rng = rng, Player = player };
    }

    [Fact]
    public void SalvarECarregar_ReproduzOEstadoOriginal()
    {
        GameState original = CreateState();

        string json = GameStateSerializer.Serialize(original);
        GameState loaded = GameStateSerializer.Deserialize(json);

        Assert.Equal(json, GameStateSerializer.Serialize(loaded));
        Assert.Equal(GameState.CurrentFormatVersion, loaded.FormatVersion);
        Assert.Equal(original.Calendar.WeekStart, loaded.Calendar.WeekStart);
        Assert.Equal(original.Player.BallotName, loaded.Player.BallotName);
        Assert.Equal(original.Player.TraitIds, loaded.Player.TraitIds);
        Assert.Equal(71, loaded.Player.Attributes.Integridade);
        Assert.Equal(12500.50m, loaded.Player.Resources.Money);
        Assert.Equal(30, loaded.Player.Resources.GetRecognition("2927408"));
        Assert.Equal(original.Rng.GetState(), loaded.Rng.GetState());
    }

    [Fact]
    public void RngCarregado_ContinuaAMesmaSequencia()
    {
        GameState original = CreateState();
        GameState loaded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(original));

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(original.Rng.NextUInt64(), loaded.Rng.NextUInt64());
        }
    }

    [Fact]
    public void Json_TemCampoDeVersao()
    {
        string json = GameStateSerializer.Serialize(CreateState());

        Assert.Contains($"\"format_version\": {GameState.CurrentFormatVersion}", json);
    }

    [Theory]
    [InlineData("""{ "calendar": { "week_start": "2028-03-06" } }""")]
    [InlineData("""{ "format_version": 99 }""")]
    [InlineData("""{ "format_version": 0 }""")]
    [InlineData("""{ "format_version": 1, "calendar": """)]
    [InlineData("""{ "format_version": 1 }""")]
    public void SaveInvalido_LancaSaveFormatException(string json)
    {
        Assert.Throws<SaveFormatException>(() => GameStateSerializer.Deserialize(json));
    }
}
