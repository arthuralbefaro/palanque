using Planalto.Core.Characters;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;
using Planalto.Core.World;

namespace Planalto.Core.Tests.State;

public class NewGameTests
{
    private static readonly Lazy<PeopleContent> People = new(() => PeopleContent.LoadOrThrow(TestContent.DataDirectory));

    [Fact]
    public void Cria_EstadoComMundoJogadorEFoco()
    {
        WorldSetup world = WorldSetup.Create(TestContent.World, "brasil_2026", new Rng(1)).Setup!;
        Character player = PersonGenerator.Generate(new Rng(2), People.Value, new PersonRequest("jogador", "SP", 3550308, new DateOnly(2028, 1, 3), PartyId: world.Parties[0].Id));

        GameState state = NewGame.Create(world, player, seed: 42);

        Assert.Equal(2028, state.Calendar.Year);
        Assert.Equal(1, state.Calendar.Week);
        Assert.Equal(3550308, state.FocusMunicipio);
        Assert.Equal(30, state.Parties.Count);
        Assert.Equal("brasil_2026", state.World!.ScenarioId);
        Assert.Equal(world.Parties[0].Sigla, state.Party(player.PartyId)!.Sigla);

        GameState loaded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        Assert.Equal(GameStateSerializer.Serialize(state), GameStateSerializer.Serialize(loaded));
    }

    [Fact]
    public void SemCidadeOuPartido_ERecusado()
    {
        WorldSetup world = WorldSetup.Create(TestContent.World, "poucos_partidos", new Rng(1)).Setup!;
        Character player = PersonGenerator.Generate(new Rng(2), People.Value, new PersonRequest("jogador", "SP", null, new DateOnly(2028, 1, 3)));

        Assert.Throws<ArgumentException>(() => NewGame.Create(world, player, 1));
        player.HomeMunicipio = 3550308;
        Assert.Throws<ArgumentException>(() => NewGame.Create(world, player, 1));
    }
}
