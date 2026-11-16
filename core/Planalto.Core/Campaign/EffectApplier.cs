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

    /// <param name="pending">Evento que originou os efeitos: dá as pessoas e a zona do save que ele envolve.</param>
    public static void Apply(CampaignContext ctx, Effects effects, string? rivalId = null, double recognitionScale = 1, PendingEvent? pending = null)
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

        // Dinheiro de evento entra na contabilidade da campanha: o que sai é gasto; o que entra é receita (ou "por
        // fora", quando vem junto com o esqueleto).
        FinanceState finance = campaign.Finance;
        if (effects.Money < 0)
        {
            finance.Spent -= effects.Money;
        }
        else if (effects.Money > 0)
        {
            string source = effects.AddSkeleton is { } irregular && FinanceCatalog.PorForaTipos.Contains(irregular.Type) ? FinanceCatalog.PorFora : FinanceCatalog.Evento;
            finance.BySource[source] = finance.BySource.GetValueOrDefault(source) + effects.Money;
            finance.Receipts.Add(new FinanceEntry(Clock.Today(state), source, effects.Money, null, source != FinanceCatalog.PorFora));
        }

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
            IEnumerable<string> known = effects.SkeletonKnownBy.Select(role => PersonOf(role, rivalId, pending)).Where(id => id is not null).Select(id => id!);
            string skeletonText = pending is not null && ctx.Events?.Find(pending.EventId) is { } origin ? EventEngine.Resolve(origin.Title, pending.Variables) : skeleton.Type;
            state.Ledger.AddSkeleton(skeleton.Type, skeletonText, skeleton.Severity, Clock.Today(state), (rivalId is null ? [] : new[] { rivalId }).Concat(known), Math.Max(0, effects.Money));
        }

        foreach (ScheduledEvent scheduled in effects.Schedule)
        {
            // A continuação volta com as mesmas pessoas e a mesma zona do evento que a agendou.
            events.Scheduled.Add(new DueEvent(
                scheduled.Event, state.Calendar.WeekStart.AddDays(7 * Math.Max(1, scheduled.DelayWeeks)),
                pending is { People.Count: > 0 } ? new Dictionary<string, string>(pending.People, StringComparer.Ordinal) : null, pending?.RivalId ?? rivalId, pending?.ZoneId));
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

        Trace(ctx, effects, rivalId, pending);

        // O reconhecimento do personagem na cidade acompanha o da campanha.
        player.Recognition = campaign.Territory.Recognition(player.Id);
        character.Resources.SetRecognition(campaign.Municipio.ToString(CultureInfo.InvariantCulture), (int)Math.Round(player.Recognition));
    }

    /// <summary>
    /// Zona de um evento (a dele, ou a zona natal do jogador) e o quanto 1 ponto de presença "na cidade inteira" vale
    /// ali: a escala do porte vezes o alcance da zona, como nos compromissos.
    /// </summary>
    public static (int Zone, double Factor) EventZone(CampaignContext ctx, PendingEvent? pending)
    {
        Territory.TerritoryState t = ctx.State.Campaign!.Territory;
        int zone = pending?.ZoneId is { } zoneId && t.IndexOf(zoneId) >= 0 ? t.IndexOf(zoneId) : t.HomeZone;
        double scale = ctx.Config.PorteRecognitionMultiplier.GetValueOrDefault(ctx.Municipio.Porte, 1);
        double voters = ctx.ZoneElectorates.Sum(z => z.Voters);
        double own = ctx.ZoneElectorates[zone].Voters;
        double reach = Math.Clamp(own <= 0 ? 1 : voters / own, ctx.Campaign.Commitments.Regras.Alcance.Min, ctx.Campaign.Commitments.Regras.Alcance.Max);
        return (zone, scale * reach);
    }

    /// <summary>Pessoa do save que ocupa o papel no evento, ou null.</summary>
    private static string? PersonOf(string role, string? rivalId, PendingEvent? pending) =>
        role == EventRole.Rival ? rivalId ?? pending?.RivalId : pending?.People.GetValueOrDefault(role);

    /// <summary>
    /// O rastro da escolha (GDD 17.1, princípio d): lembranças nas pessoas envolvidas, promessa e dívida no Livro;
    /// presença na zona do evento; compromisso desmarcado; e o fato que a escolha emite.
    /// </summary>
    private static void Trace(CampaignContext ctx, Effects effects, string? rivalId, PendingEvent? pending)
    {
        GameState state = ctx.State;
        CampaignState campaign = state.Campaign!;
        DateOnly today = Clock.Today(state);
        IReadOnlyDictionary<string, string> vars = pending?.Variables ?? new Dictionary<string, string>();
        foreach (MemoryEffect memory in effects.Memories)
        {
            if (PersonOf(memory.Quem, rivalId, pending) is { } id && state.City?.Person(id) is { } person)
            {
                person.LastContact = today;
                person.Remember(today, memory.Fato, memory.Peso, memory.Pontos, EventEngine.Resolve(memory.Texto, vars), ctx.Campaign.People);
            }
        }

        if (effects.Promise is { Para: "zona" } toZone && pending?.ZoneId is { } promisedZone)
        {
            DateOnly due = toZone.PrazoSemanas <= 0 ? campaign.ElectionDate : new DateOnly(campaign.ElectionDate.Year + 1, 1, 1).AddDays(7 * toZone.PrazoSemanas);
            state.Ledger.AddPromise(People.Promise.ParaZona, promisedZone, toZone.Objeto, EventEngine.Resolve(toZone.Texto, vars), promisedZone, today, due);
        }
        else if (effects.Promise is { } promise && PersonOf(promise.Para, rivalId, pending) is { } promisedTo)
        {
            DateOnly deadline = promise.PrazoSemanas <= 0 ? campaign.ElectionDate : new DateOnly(campaign.ElectionDate.Year + 1, 1, 1).AddDays(7 * promise.PrazoSemanas);
            state.Ledger.AddPromise(People.Promise.ParaPessoa, promisedTo, promise.Objeto, EventEngine.Resolve(promise.Texto, vars), pending?.ZoneId, today, deadline);
        }

        if (effects.Debt is { } debt && PersonOf(debt.Para, rivalId, pending) is { } owedTo)
        {
            state.Ledger.AddDebt(owedTo, EventEngine.Resolve(debt.Texto, vars), today);
        }

        if (Math.Abs(effects.ZonePresence) > 1e-12)
        {
            var (zone, factor) = EventZone(ctx, pending);
            double[] presence = campaign.Territory.Presence[campaign.Player.Id];
            presence[zone] = Gain(presence[zone], effects.ZonePresence * factor);
        }

        if (effects.CancelCommitment is { } type && campaign.Agenda.Scheduled.Where(c => c.Type == type).OrderBy(c => c.Date).FirstOrDefault() is { } cancelled)
        {
            campaign.Agenda.Scheduled.Remove(cancelled);
        }

        if (effects.Fact is { } fact)
        {
            new CampaignEngine(ctx).Emit(fact.Tipo, EventEngine.Resolve(fact.Texto, vars), campaign.Player.Id, pending?.ZoneId,
                pending?.People.GetValueOrDefault(EventRole.Lideranca), pending?.Theme, rivalId ?? pending?.RivalId, sub: pending?.EventId, visibility: fact.Visibilidade);
        }
    }
}
