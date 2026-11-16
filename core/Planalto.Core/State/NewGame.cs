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

        Calendar calendar = StartCalendar(world.Options.StartYear, player.BirthDate);
        player.AffiliatedOn ??= calendar.WeekStart;
        return new GameState
        {
            Calendar = calendar,
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

    /// <summary>
    /// Semana em que a carreira começa: a semana ISO 1 do ano inicial (DECISOES D-019), já filiado (antes do prazo de
    /// abril). Quem ainda não tem idade para a primeira eleição começa na semana 1 do ano da primeira eleição que
    /// pode disputar, direto na pré-campanha e já com a idade daquele ano: o calendário avança até lá na criação.
    /// </summary>
    public static Calendar StartCalendar(int startYear, DateOnly birthDate) =>
        Calendar.FromIsoWeek(Career.CareerFlow.CareerStartYear(birthDate, startYear), 1);
}
