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

        // O que atinge a cidade inteira (evento, debate, notícia) muda a presença em todas as zonas; o
        // reconhecimento é a média delas (GDD 17.5).
        if (Math.Abs(effects.Recognition) > 1e-12)
        {
            campaign.Territory.GainEverywhere(player.Id, effects.Recognition * recognitionScale);
            player.Recognition = campaign.Territory.Recognition(player.Id);
        }

        player.Image = Math.Clamp(player.Image + effects.Image, -1, 1);
        foreach (var (group, delta) in effects.Support)
        {
            player.Support[group] = Math.Clamp(player.Support.GetValueOrDefault(group) + delta, 0, 1);
        }

        character.Resources.Money += effects.Money;
        player.Money = character.Resources.Money;
        character.Resources.Stress += effects.Stress - (int)Math.Round(effects.Energy / 4.0);
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

            // O esqueleto também entra no Livro de compromissos, com quem sabe (o rival ou a pessoa envolvida).
            state.Ledger.AddSkeleton(skeleton.Type, skeleton.Type, skeleton.Severity, Clock.Today(state), rivalId is null ? [] : [rivalId]);
        }

        foreach (ScheduledEvent scheduled in effects.Schedule)
        {
            events.Scheduled.Add(new DueEvent(scheduled.Event, state.Calendar.WeekStart.AddDays(7 * Math.Max(1, scheduled.DelayWeeks))));
        }

        if (rivalId is not null && campaign.Find(rivalId) is { } rival)
        {
            rival.Image = Math.Clamp(rival.Image + effects.RivalImage, -1, 1);
            if (Math.Abs(effects.RivalRecognition) > 1e-12)
            {
                campaign.Territory.GainEverywhere(rival.Id, effects.RivalRecognition);
                rival.Recognition = campaign.Territory.Recognition(rival.Id);
            }
            if (effects.Relation != 0)
            {
                events.Relations[rivalId] = Math.Clamp(events.Relations.GetValueOrDefault(rivalId) + effects.Relation, -100, 100);
            }
        }

        // O reconhecimento do personagem na cidade acompanha o da campanha.
        character.Resources.SetRecognition(campaign.Municipio.ToString(CultureInfo.InvariantCulture), (int)Math.Round(player.Recognition));
    }
}
