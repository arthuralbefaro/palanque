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
            if (ctx.Events.Find(due.EventId) is { } chained && CanBind(ctx, chained))
            {
                Fire(ctx, chained, rng, null, due);
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

    public static bool IsEligible(CampaignContext ctx, EventDefinition e, DateOnly week) =>
        !e.ChainOnly && e.Conditions.Trigger is null && Fits(ctx, e, week);

    /// <summary>As condições do evento valem agora (fase, cooldown, o que ele precisa encontrar no save), fora o modo de disparo.</summary>
    public static bool Fits(CampaignContext ctx, EventDefinition e, DateOnly week)
    {
        CampaignState campaign = ctx.State.Campaign!;
        EventState state = campaign.Events;
        if (!e.Phases.Contains(PhaseId(campaign.Phase)) || !CanBind(ctx, e))
        {
            return false;
        }

        if (e.Conditions.NeedsCommitment is { } commitment && !campaign.Agenda.Scheduled.Any(c => c.Type == commitment && !c.Delegado))
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

    private static List<Media.Outlet> OutletsFor(CampaignContext ctx, EventDefinition e)
    {
        Media.OutletCatalog catalog = ctx.Campaign.Media.Outlets;
        return (ctx.State.City?.Outlets ?? []).Where(o => e.Conditions.OutletKind switch
        {
            "emissora" => catalog.Type(o.Type).Emissora,
            "escrita" => catalog.Type(o.Type).Escrita,
            "rede" => catalog.Type(o.Type).Rede,
            _ => true,
        }).ToList();
    }

    private static List<Candidate> Colleagues(CampaignState campaign) =>
        campaign.Candidates.Where(c => !c.IsPlayer && !c.IsBulk && c.Office == campaign.Player.Office && c.PartyId == campaign.Player.PartyId).ToList();

    /// <summary>O save tem o que o evento envolve (liderança, veículo, doador, colega de chapa, dirigente, rival)?</summary>
    public static bool CanBind(CampaignContext ctx, EventDefinition e)
    {
        CampaignState campaign = ctx.State.Campaign!;
        CityState? city = ctx.State.City;
        foreach (string need in EventValidation.Needs(e))
        {
            bool ok = need switch
            {
                EventRole.Lideranca => city is { Leaders.Count: > 0 },
                EventRole.Jornalista or "veiculo" => OutletsFor(ctx, e).Count > 0,
                EventRole.Doador => city is { Donors.Count: > 0 },
                EventRole.Colega => Colleagues(campaign).Count > 0,
                EventRole.Dirigente => city?.Bosses.ContainsKey(campaign.Player.PartyId) == true,
                EventRole.Equipe => city?.Staff.Any(s => s.Hired) == true,
                EventRole.Rival => Rivals(campaign).Any(),
                "zona" => e.Conditions.ZoneKind is not { } kind || ctx.Zonas.Zonas.Any(z => z.Situacao == kind),
                _ => true,
            };
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Dispara um evento a partir de um fato (GDD 15.4): as pessoas, a zona e o tema vêm do fato.</summary>
    public static void FireFromFact(CampaignContext ctx, EventDefinition e, Rng rng, Fact origin) => Fire(ctx, e, rng, origin, null);

    private static void Fire(CampaignContext ctx, EventDefinition e, Rng rng, Fact? origin = null, DueEvent? due = null)
    {
        CampaignState campaign = ctx.State.Campaign!;
        CityState? city = ctx.State.City;
        HashSet<string> needs = EventValidation.Needs(e);
        var people = new Dictionary<string, string>(due?.People ?? [], StringComparer.Ordinal);

        // O rival: o da cadeia, o do fato de origem, ou um dos mais conhecidos da disputa.
        Candidate? rival = null;
        if (needs.Contains(EventRole.Rival))
        {
            rival = (due?.RivalId is { } chained ? campaign.Find(chained) : null)
                ?? Rivals(campaign).FirstOrDefault(r => r.Id == origin?.ActorId || r.Id == origin?.TargetId)
                ?? (Rivals(campaign).OrderByDescending(r => r.Recognition).Take(4).ToList() is { Count: > 0 } top ? rng.Pick(top) : null);
        }

        Leader? leader = null;
        if (needs.Contains(EventRole.Lideranca) && city is not null)
        {
            leader = (people.TryGetValue(EventRole.Lideranca, out string? same) ? city.Leader(same) : null)
                ?? (origin?.PersonId is { } fromFact ? city.Leader(fromFact) : null);
            if (leader is null && city.Leaders.Count > 0)
            {
                List<Leader> known = city.Leaders.Where(l => l.Known).ToList();
                leader = rng.Pick(known.Count > 0 ? known : city.Leaders);
                leader.Known = true;
            }

            if (leader is not null)
            {
                people[EventRole.Lideranca] = leader.PersonId;
            }
        }

        // Num disparo forçado (depuração) o save pode não ter o que o evento envolve: o papel fica sem pessoa.
        Media.Outlet? outlet = null;
        if (needs.Contains("veiculo") && OutletsFor(ctx, e) is { Count: > 0 } outlets)
        {
            outlet = rng.Pick(outlets);
            people[EventRole.Jornalista] = outlet.JournalistId;
        }

        if (needs.Contains(EventRole.Doador) && !people.ContainsKey(EventRole.Doador) && city is { Donors.Count: > 0 })
        {
            people[EventRole.Doador] = origin?.ActorId is { } giver && city.Donor(giver) is not null ? giver : rng.Pick(city.Donors).PersonId;
        }

        if (needs.Contains(EventRole.Colega) && !people.ContainsKey(EventRole.Colega) && Colleagues(campaign) is { Count: > 0 } colleagues)
        {
            people[EventRole.Colega] = rng.Pick(colleagues).Id;
        }

        if (needs.Contains(EventRole.Dirigente) && city is not null && city.Bosses.TryGetValue(campaign.Player.PartyId, out string? boss))
        {
            people[EventRole.Dirigente] = boss;
        }

        if (needs.Contains(EventRole.Equipe) && !people.ContainsKey(EventRole.Equipe) && city?.Staff.Where(s => s.Hired).ToList() is { Count: > 0 } team)
        {
            people[EventRole.Equipe] = rng.Pick(team).PersonId;
        }

        string? zoneId = due?.ZoneId ?? origin?.ZoneId ?? leader?.Zones[0];
        if (needs.Contains("zona") && (zoneId is null || (e.Conditions.ZoneKind is { } wanted && ctx.Zonas.Zonas.FirstOrDefault(z => z.Id == zoneId)?.Situacao != wanted)))
        {
            Territory.TerritoryState t = campaign.Territory;
            List<int> zones = Enumerable.Range(0, t.Zones.Count).Where(z => e.Conditions.ZoneKind is not { } kind || ctx.Zonas.Zonas[z].Situacao == kind).ToList();
            if (zones.Count == 0)
            {
                zones = Enumerable.Range(0, t.Zones.Count).ToList();
            }

            zoneId = t.Zones[rng.PickWeighted(zones, z => Math.Max(1, t.Population[z]))];
        }

        string? theme = origin?.Theme ?? (needs.Contains("tema") ? rng.Pick(ctx.Themes.Themes).Id : null);
        Dictionary<string, string> vars = Variables(ctx, rival, rng);
        string NameOf(string id) => campaign.Find(id)?.Name ?? ctx.State.Npcs.FirstOrDefault(n => n.Id == id)?.BallotName ?? id;
        foreach (var (role, id) in people)
        {
            vars[$"{role}.nome"] = NameOf(id);
        }

        if (leader is not null)
        {
            vars["lideranca.papel"] = ctx.Campaign.Leaders.Type(leader.Type).Papel;
        }

        if (outlet is not null)
        {
            // Nome fictício sempre com o que é (GDD 17.1, princípio j).
            Media.OutletCatalog catalog = ctx.Campaign.Media.Outlets;
            vars["veiculo.nome"] = $"{outlet.Name} ({catalog.Type(outlet.Type).Nome}, {catalog.Line(outlet.Line).Nome})";
        }

        if (zoneId is not null)
        {
            vars["zona.nome"] = ctx.Zonas.Zonas.FirstOrDefault(z => z.Id == zoneId)?.Nome ?? zoneId;
        }

        if (theme is not null)
        {
            vars["tema.nome"] = ctx.Themes.Themes.FirstOrDefault(t => t.Id == theme)?.Name ?? theme;
        }

        campaign.Events.Pending.Add(new PendingEvent
        {
            EventId = e.Id,
            Title = Resolve(e.Title, vars),
            Text = Resolve(rng.Pick(e.Variants), vars),
            RivalId = rival?.Id,
            Variables = vars,
            People = people,
            ZoneId = zoneId,
            OutletId = outlet?.Id,
            Theme = theme,
            FactId = origin?.Id,
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
        EffectApplier.Apply(ctx, choice.Effects, pending.RivalId, pending: pending);
        string text = choice.Result is null ? "" : Resolve(choice.Result, pending.Variables);
        bool passed = true;
        if (choice.Test is { } test)
        {
            double chance = Math.Clamp((ctx.State.Player.Attributes.Get(test.Attribute) / 100.0) + test.Bonus, 0.05, 0.95);
            passed = rng.Chance(chance);
            EffectApplier.Apply(ctx, passed ? test.Success : test.Failure, pending.RivalId, pending: pending);
            text = (text + " " + Resolve(passed ? test.SuccessText : test.FailureText, pending.Variables)).Trim();
        }

        campaign.Events.Pending.Remove(pending);
        campaign.Events.History.Add(new EventLogEntry(ctx.State.Calendar.WeekStart, e.Id, choice.Label, text));

        // O evento vira fato de bastidor (visibilidade 0, só conhecido por quem estava presente); só é notícia
        // quando a escolha emite um fato próprio.
        new CampaignEngine(ctx).Emit(FactKind.Evento, pending.Title, RaceSetup.PlayerId, pending.ZoneId, pending.People.GetValueOrDefault(EventRole.Lideranca),
            pending.Theme, pending.RivalId, sub: e.Id, visibility: 0, feed: false);
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
