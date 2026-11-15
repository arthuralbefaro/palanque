using System.Globalization;
using Planalto.Core.Characters;
using Planalto.Core.Events;
using Planalto.Core.State;

namespace Planalto.Core.Campaign;

/// <summary>Aplica efeitos de ações e eventos ao jogador, à campanha e ao rival envolvido.</summary>
public static class EffectApplier
{
    /// <summary>Ganho de reconhecimento com retorno decrescente: cada ponto vale menos quanto mais gente já conhece.</summary>
    public static double Gain(double current, double delta) =>
        Math.Clamp(delta > 0 ? current + (delta * (1 - (current / 100))) : current + delta, 0, 100);

    public static void Apply(CampaignContext ctx, Effects effects, string? rivalId = null, double recognitionScale = 1)
    {
        GameState state = ctx.State;
        CampaignState campaign = state.Campaign!;
        Candidate player = campaign.Player;
        Character character = state.Player;

        player.Recognition = Gain(player.Recognition, effects.Recognition * recognitionScale);
        player.Image = Math.Clamp(player.Image + effects.Image, -1, 1);
        foreach (var (group, delta) in effects.Support)
        {
            player.Support[group] = Math.Clamp(player.Support.GetValueOrDefault(group) + delta, 0, 1);
        }

        character.Resources.Money += effects.Money;
        player.Money = character.Resources.Money;
        campaign.EnergyLeft = Math.Clamp(campaign.EnergyLeft + effects.Energy, 0, ctx.Config.WeeklyEnergy * 2);
        character.Resources.Stress += effects.Stress;
        character.Resources.Health += effects.Health;
        character.Resources.PoliticalCapital += effects.PoliticalCapital;
        foreach (var (attribute, delta) in effects.Attributes)
        {
            character.Attributes.Set(attribute, character.Attributes.Get(attribute) + delta);
        }

        EventState events = campaign.Events;
        events.Flags.UnionWith(effects.SetFlags);
        events.Flags.ExceptWith(effects.ClearFlags);
        if (effects.AddSkeleton is { } skeleton)
        {
            events.Skeletons.Add(skeleton with { Created = state.Calendar.WeekStart });
        }

        foreach (ScheduledEvent scheduled in effects.Schedule)
        {
            events.Scheduled.Add(new DueEvent(scheduled.Event, state.Calendar.WeekStart.AddDays(7 * Math.Max(1, scheduled.DelayWeeks))));
        }

        if (rivalId is not null && campaign.Find(rivalId) is { } rival)
        {
            rival.Image = Math.Clamp(rival.Image + effects.RivalImage, -1, 1);
            rival.Recognition = Math.Clamp(rival.Recognition + effects.RivalRecognition, 0, 100);
            if (effects.Relation != 0)
            {
                events.Relations[rivalId] = Math.Clamp(events.Relations.GetValueOrDefault(rivalId) + effects.Relation, -100, 100);
            }
        }

        // O reconhecimento do personagem na cidade acompanha o da campanha.
        character.Resources.SetRecognition(campaign.Municipio.ToString(CultureInfo.InvariantCulture), (int)Math.Round(player.Recognition));
    }
}
