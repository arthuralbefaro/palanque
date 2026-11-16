using System.Globalization;
using System.Text.RegularExpressions;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.State;
using Planalto.Core.World;

namespace Planalto.Core.Events;

/// <summary>Resultado de uma escolha, com o texto já resolvido.</summary>
public sealed record EventResolution(string Text, bool TestPassed);

/// <summary>
/// Motor de eventos (GDD 15.3): sorteio semanal por condições, raridade e cooldown, eventos únicos,
/// cadeias agendadas, variáveis de texto, escolhas com requisito e resultados com risco.
/// </summary>
public static partial class EventEngine
{
    public static string PhaseId(CampaignPhase phase) => phase == CampaignPhase.Campanha ? "campanha" : "pre_campanha";

    /// <summary>Sorteia os eventos da semana: primeiro as continuações vencidas, depois 0 a 3 novos.</summary>
    public static void Roll(CampaignContext ctx, Rng rng)
    {
        if (ctx.Events is null)
        {
            return;
        }

        CampaignState campaign = ctx.State.Campaign!;
        EventState state = campaign.Events;
        DateOnly week = ctx.State.Calendar.WeekStart;

        foreach (DueEvent due in state.Scheduled.Where(d => d.Due <= ctx.State.Calendar.WeekEnd).ToList())
        {
            state.Scheduled.Remove(due);
            if (ctx.Events.Find(due.EventId) is { } chained)
            {
                Fire(ctx, chained, rng);
            }
        }

        EventFrequency frequency = ctx.State.World?.Options.EventFrequency ?? EventFrequency.Normal;
        double expected = ctx.Events.Config.EventsPerWeek[FrequencyKey(frequency)];
        int count = Math.Min(ctx.Events.Config.MaxPerWeek - state.Pending.Count, Poisson(rng, expected));
        for (int i = 0; i < count; i++)
        {
            var eligible = ctx.Events.Events.Where(e => IsEligible(ctx, e, week) && state.Pending.All(p => p.EventId != e.Id)).ToList();
            if (eligible.Count == 0)
            {
                break;
            }

            EventDefinition chosen = rng.PickWeighted(eligible, e => ctx.Events.Config.RarityWeights[e.Rarity] * e.Conditions.Chance);
            Fire(ctx, chosen, rng);
        }
    }

    public static string FrequencyKey(EventFrequency f) => f switch
    {
        EventFrequency.Baixa => "baixa",
        EventFrequency.Normal => "normal",
        EventFrequency.Alta => "alta",
        _ => "caotica",
    };

    public static bool IsEligible(CampaignContext ctx, EventDefinition e, DateOnly week)
    {
        CampaignState campaign = ctx.State.Campaign!;
        EventState state = campaign.Events;
        if (e.ChainOnly || !e.Phases.Contains(PhaseId(campaign.Phase)))
        {
            return false;
        }

        if (e.Unique && state.History.Any(h => h.EventId == e.Id))
        {
            return false;
        }

        if (state.LastFired.TryGetValue(e.Id, out DateOnly last) && week < last.AddDays(7 * e.CooldownWeeks))
        {
            return false;
        }

        EventConditions c = e.Conditions;
        Candidate player = campaign.Player;
        Character character = ctx.State.Player;
        int weeksToElection = (campaign.ElectionDate.DayNumber - week.DayNumber) / 7;
        return (c.MoneyLt is null || character.Resources.Money < c.MoneyLt)
            && (c.MoneyGt is null || character.Resources.Money > c.MoneyGt)
            && (c.RecognitionLt is null || player.Recognition < c.RecognitionLt)
            && (c.RecognitionGt is null || player.Recognition > c.RecognitionGt)
            && (c.ImageLt is null || player.Image < c.ImageLt)
            && (c.ImageGt is null || player.Image > c.ImageGt)
            && (c.StressGt is null || character.Resources.Stress > c.StressGt)
            && (c.Porte.Count == 0 || c.Porte.Contains(ctx.Municipio.Porte))
            && (c.Regiao.Count == 0 || c.Regiao.Contains(ctx.Municipio.Regiao))
            && (c.Trait is null || character.TraitIds.Contains(c.Trait))
            && (c.NotTrait is null || !character.TraitIds.Contains(c.NotTrait))
            && (c.Flag is null || state.Flags.Contains(c.Flag))
            && (c.NotFlag is null || !state.Flags.Contains(c.NotFlag))
            && c.AttributeMin.All(a => character.Attributes.Get(a.Key) >= a.Value)
            && (c.WeeksToElectionLt is null || weeksToElection < c.WeeksToElectionLt)
            && (c.WeeksToElectionGt is null || weeksToElection > c.WeeksToElectionGt)
            && (!c.NeedsRival || Rivals(campaign).Any())
            && (!c.NeedsSkeleton || state.Skeletons.Count > 0);
    }

    private static IEnumerable<Candidate> Rivals(CampaignState campaign) =>
        campaign.Candidates.Where(x => !x.IsPlayer && !x.IsBulk && x.Office == campaign.Player.Office);

    /// <summary>Dispara um evento pelo id, fora do sorteio (cadeias forçadas, depuração e testes de fumaça).</summary>
    public static PendingEvent Trigger(CampaignContext ctx, string eventId, Rng rng)
    {
        EventDefinition e = ctx.Events?.Find(eventId) ?? throw new ArgumentException($"Evento '{eventId}' inexistente.", nameof(eventId));
        Fire(ctx, e, rng);
        return ctx.State.Campaign!.Events.Pending[^1];
    }

    private static void Fire(CampaignContext ctx, EventDefinition e, Rng rng)
    {
        CampaignState campaign = ctx.State.Campaign!;
        Candidate? rival = e.Conditions.NeedsRival || e.Variants.Any(v => v.Contains("{rival.", StringComparison.Ordinal))
            ? Rivals(campaign).OrderByDescending(r => r.Recognition).Take(4).ToList() is { Count: > 0 } top ? rng.Pick(top) : null
            : null;
        Dictionary<string, string> vars = Variables(ctx, rival, rng);
        campaign.Events.Pending.Add(new PendingEvent
        {
            EventId = e.Id,
            Title = Resolve(e.Title, vars),
            Text = Resolve(rng.Pick(e.Variants), vars),
            RivalId = rival?.Id,
            Variables = vars,
        });
        campaign.Events.LastFired[e.Id] = ctx.State.Calendar.WeekStart;
    }

    /// <summary>Escolha disponível? Devolve o requisito em texto quando bloqueada (o jogo mostra o motivo).</summary>
    public static string? Blocked(CampaignContext ctx, EventChoice choice)
    {
        ChoiceRequirement? r = choice.Requires;
        if (r is null)
        {
            return null;
        }

        Character c = ctx.State.Player;
        if (r.MoneyMin is decimal money && c.Resources.Money < money)
        {
            return string.Create(CultureInfo.GetCultureInfo("pt-BR"), $"Requer R$ {money:N0}");
        }

        foreach (var (attribute, min) in r.AttributeMin.Where(a => c.Attributes.Get(a.Key) < a.Value))
        {
            return $"Requer {attribute} {min}";
        }

        if (r.PoliticalCapitalMin is int capital && c.Resources.PoliticalCapital < capital)
        {
            return $"Requer capital político {capital}";
        }

        return r.Flag is not null && !ctx.State.Campaign!.Events.Flags.Contains(r.Flag) ? "Requisito não cumprido" : null;
    }

    public static EventResolution Resolve(CampaignContext ctx, PendingEvent pending, int choiceIndex, Rng rng)
    {
        EventDefinition e = ctx.Events!.Find(pending.EventId)!;
        EventChoice choice = e.Choices[choiceIndex];
        if (Blocked(ctx, choice) is not null)
        {
            throw new InvalidOperationException("Escolha bloqueada.");
        }

        CampaignState campaign = ctx.State.Campaign!;
        EffectApplier.Apply(ctx, choice.Effects, pending.RivalId);
        string text = choice.Result is null ? "" : Resolve(choice.Result, pending.Variables);
        bool passed = true;
        if (choice.Test is { } test)
        {
            double chance = Math.Clamp((ctx.State.Player.Attributes.Get(test.Attribute) / 100.0) + test.Bonus, 0.05, 0.95);
            passed = rng.Chance(chance);
            EffectApplier.Apply(ctx, passed ? test.Success : test.Failure, pending.RivalId);
            text = (text + " " + Resolve(passed ? test.SuccessText : test.FailureText, pending.Variables)).Trim();
        }

        campaign.Events.Pending.Remove(pending);
        campaign.Events.History.Add(new EventLogEntry(ctx.State.Calendar.WeekStart, e.Id, choice.Label, text));
        return new EventResolution(text, passed);
    }

    public static Dictionary<string, string> Variables(CampaignContext ctx, Candidate? rival, Rng rng)
    {
        Character player = ctx.State.Player;
        GameParty? party = ctx.State.Party(player.PartyId);
        double cost = ctx.Config.PorteCostMultiplier.GetValueOrDefault(ctx.Municipio.Porte, 1);
        var vars = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["municipio.nome"] = ctx.Municipio.Nome,
            ["municipio.uf"] = ctx.Municipio.Uf,
            ["municipio.porte"] = ctx.Portes?.Get(ctx.Municipio.Porte).Name.ToLowerInvariant() ?? ctx.Municipio.Porte,
            ["partido.sigla"] = party?.Sigla ?? "",
            ["partido.nome"] = party?.Name ?? "",
            ["jogador.nome"] = player.BallotName,
            ["jogador.pronome"] = player.Gender switch { Gender.Masculino => "ele", Gender.Feminino => "ela", _ => "elu" },
            ["jogador.o"] = player.Gender switch { Gender.Masculino => "o", Gender.Feminino => "a", _ => "e" },
            ["ano"] = ctx.State.Calendar.Year.ToString(CultureInfo.InvariantCulture),
            ["valor"] = string.Create(CultureInfo.GetCultureInfo("pt-BR"), $"R$ {Math.Round(10000 * cost / 100) * 100:N0}"),
            ["semanas"] = ((ctx.State.Campaign!.ElectionDate.DayNumber - ctx.State.Calendar.WeekStart.DayNumber) / 7).ToString(CultureInfo.InvariantCulture),
        };
        if (rival is not null)
        {
            vars["rival.nome"] = rival.Name;
            vars["rival.partido"] = ctx.State.Party(rival.PartyId)?.Sigla ?? "";
        }

        return vars;
    }

    public static string Resolve(string text, IReadOnlyDictionary<string, string> vars) =>
        VariablePattern().Replace(text, m => vars.TryGetValue(m.Groups[1].Value, out string? v) ? v : m.Value);

    private static int Poisson(Rng rng, double lambda)
    {
        double l = Math.Exp(-lambda);
        double p = 1;
        int k = 0;
        do
        {
            k++;
            p *= rng.NextDouble();
        }
        while (p > l);
        return k - 1;
    }

    [GeneratedRegex(@"\{([a-z_]+\.[a-z_]+|[a-z_]+)\}")]
    private static partial Regex VariablePattern();
}
