using Planalto.Core.State;
using Planalto.Core.Tests.Parties;
using Planalto.Core.World;

namespace Planalto.Core.Tests.World;

public class WorldSetupSerializerTests
{
    private static WorldContent Content => TestContent.World;

    [Fact]
    public void IdaEVolta_PreservaPartidosTravasEOpcoes()
    {
        WorldSetup setup = WorldSetup.Create(Content, "brasil_realista", new Rng(40)).Setup!;
        setup.SetLocked(setup.Parties[3].Id, PartyField.Sigla, true);
        setup.SetLocked(setup.Parties[3].Id, PartyField.Visual, true);
        setup.Options.StartYear = 2032;
        setup.Options.Ironman = true;
        setup.Options.EventFrequency = EventFrequency.Caotica;
        setup.Options.LegalRealism = LegalRealism.Relaxado;
        setup.Options.Difficulty = "dificil";

        string json = WorldSetupSerializer.Serialize(setup);
        WorldSetupLoadResult loaded = WorldSetupSerializer.Deserialize(json, Content);

        Assert.Null(loaded.Issue);
        WorldSetup copy = loaded.Setup!;
        Assert.Equal(json, WorldSetupSerializer.Serialize(copy));
        Assert.Equal(setup.PresetId, copy.PresetId);
        Assert.Equal(setup.Parties.Count, copy.Parties.Count);
        Assert.True(copy.Parties[3].IsLocked(PartyField.Sigla));
        Assert.True(copy.Parties[3].IsLocked(PartyField.Visual));
        Assert.Equal(2032, copy.Options.StartYear);
        Assert.Equal(EventFrequency.Caotica, copy.Options.EventFrequency);
        Assert.Equal(LegalRealism.Relaxado, copy.Options.LegalRealism);
        Assert.True(copy.Validate().IsValid);
    }

    [Fact]
    public void Carregado_ContinuaEditavelSemRepetirIds()
    {
        WorldSetup setup = WorldSetup.Create(Content, "poucos_partidos", new Rng(41)).Setup!;
        WorldSetup copy = WorldSetupSerializer.Deserialize(WorldSetupSerializer.Serialize(setup), Content).Setup!;

        PartyDraft added = copy.AddParty(new Rng(42))!;

        Assert.DoesNotContain(setup.Parties, p => p.Id == added.Id);
        Assert.Equal(copy.Parties.Count, copy.Parties.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void Serializado_UsaSnakeCaseESoIdsFicticios()
    {
        WorldSetup setup = WorldSetup.Create(Content, "multipartidario_moderado", new Rng(43)).Setup!;

        string json = WorldSetupSerializer.Serialize(setup);

        Assert.Contains("\"format_version\": 1", json);
        Assert.Contains("\"preset_id\": \"multipartidario_moderado\"", json);
        Assert.Contains("\"family_id\"", json);
    }

    [Theory]
    [InlineData("não é json", ScenarioFileIssue.JsonInvalido)]
    [InlineData("""{ "preset_id": "x" }""", ScenarioFileIssue.ConteudoIncompleto)]
    [InlineData("""{ "format_version": 99, "preset_id": "x" }""", ScenarioFileIssue.VersaoMaisNova)]
    [InlineData("""{ "format_version": 0, "preset_id": "x" }""", ScenarioFileIssue.VersaoNaoSuportada)]
    [InlineData("""{ "format_version": 1, "preset_id": "x", "next_serial": 1 }""", ScenarioFileIssue.JsonInvalido)]
    public void ArquivoInvalido_RetornaProblemaSemExcecao(string json, ScenarioFileIssue expected)
    {
        WorldSetupLoadResult result = WorldSetupSerializer.Deserialize(json, Content);

        Assert.Null(result.Setup);
        Assert.Equal(expected, result.Issue);
    }

    [Fact]
    public void IdsRepetidos_SaoRejeitados()
    {
        WorldSetup setup = WorldSetup.Create(Content, "bipartidario", new Rng(44)).Setup!;
        string json = WorldSetupSerializer.Serialize(setup).Replace($"\"id\": \"{setup.Parties[1].Id}\"", $"\"id\": \"{setup.Parties[0].Id}\"");

        Assert.Equal(ScenarioFileIssue.IdsDePartidoRepetidos, WorldSetupSerializer.Deserialize(json, Content).Issue);
    }
}
