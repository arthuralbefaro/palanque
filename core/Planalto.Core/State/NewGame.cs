using Planalto.Core.Characters;
using Planalto.Core.World;

namespace Planalto.Core.State;

/// <summary>Monta o estado inicial da carreira a partir da tela "Novo Mundo" e da criação de personagem.</summary>
public static class NewGame
{
    public static GameState Create(WorldSetup world, Character player, ulong seed)
    {
        if (player.HomeMunicipio is not int home)
        {
            throw new ArgumentException("O personagem precisa de cidade natal.", nameof(player));
        }

        if (world.Parties.All(p => p.Id != player.PartyId))
        {
            throw new ArgumentException("O personagem precisa estar filiado a um partido do mundo.", nameof(player));
        }

        // Começa na semana ISO 1 do ano inicial (DECISOES D-019).
        return new GameState
        {
            Calendar = Calendar.FromIsoWeek(world.Options.StartYear, 1),
            Rng = new Rng(seed),
            Player = player,
            World = new WorldState
            {
                PresetId = world.PresetId,
                ScenarioId = world.Preset.Scenario,
                Options = world.Options.Clone(),
                Seed = seed,
            },
            Parties = world.Parties.Select(GameParty.From).ToList(),
            FocusMunicipio = home,
        };
    }
}
