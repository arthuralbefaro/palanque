using System.Globalization;
using Planalto.Core.Campaign;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.Core.Governance;

/// <summary>
/// Cobrança do Livro de compromissos no mandato (GDD 17.14): quem lembra da promessa ou da dívida vem cobrar; os
/// instrumentos cumprem (obra entregue na zona, nomeação, posição assumida); promessa vencida custa relação, presença
/// e o apoio de quem cobrou.
/// </summary>
public sealed partial class GovernanceEngine
{
    private CollectionRules CollectionRules => Content.Collection.Regras;

    /// <summary>Chaves de obra ou serviço que cumprem a promessa (vazio quando ela se cumpre de outro jeito).</summary>
    private IReadOnlyList<string> WorkKeysOf(Promise promise) =>
        Context.Promises.Find(promise.Object) is { } definition ? [definition.Obra]
        : Content.Collection.PromessaCumpre.TryGetValue(promise.Object, out List<string>? keys) ? keys : [];

    /// <summary>
    /// Prazo que vale no mandato: o da promessa, nunca antes de algumas semanas depois da posse (promessa de campanha
    /// com prazo "antes da eleição" que chegou aberta ainda pode ser honrada, e é cobrada antes de vencer).
    /// </summary>
    public DateOnly DeadlineOf(Promise promise)
    {
        DateOnly floor = G.Start.AddDays(7 * CollectionRules.PrazoMinimoSemanas);
        return G.PromisesAtStart.Contains(promise.Id) && promise.Deadline < floor ? floor : promise.Deadline;
    }

    /// <summary>Quem lembra da promessa e vem cobrar: a pessoa a quem foi feita, ou a liderança da zona ou do grupo.</summary>
    private string? HolderOf(Promise promise) => promise.ToKind switch
    {
        Promise.ParaPessoa => promise.ToId,
        Promise.ParaZona => City.Leaders.Where(l => l.Zones.Contains(promise.ToId)).OrderByDescending(l => l.AlignedWith == RaceSetup.PlayerId).ThenByDescending(l => l.Influence).FirstOrDefault()?.PersonId,
        _ => City.Leaders.Where(l => l.Publico == promise.ToId).OrderByDescending(l => l.AlignedWith == RaceSetup.PlayerId).ThenByDescending(l => l.Influence).FirstOrDefault()?.PersonId,
    };

    private string PromiseHolder(Promise promise) => NameOf(HolderOf(promise));

    private static PersonRole HolderRole(Promise promise) => promise.Object.StartsWith("voto_", StringComparison.Ordinal) ? PersonRole.Vereador : PersonRole.Lideranca;

    /// <summary>Promessas abertas que a entrega desta obra cumpre: mesma chave de obra e, quando a promessa tem zona, a mesma zona.</summary>
    private List<Promise> PromisesFulfilledBy(Work work)
    {
        bool mine = work.SponsorId == RaceSetup.PlayerId || (Role == GovRole.Prefeito && work.SponsorId == Work.Prefeito);
        if (!mine || Content.Works.Find(work.TypeId) is not { } type)
        {
            return [];
        }

        return State.Ledger.Open
            .Where(p => (p.ZoneId is null || p.ZoneId == work.ZoneId)
                && (WorkKeysOf(p).Any(type.Cumpre.Contains) || (p.ZoneId == work.ZoneId && Content.Collection.PromessaDeObraNaZona.Contains(p.Object))))
            .ToList();
    }

    private List<string> OnWorkDelivered(Work work)
    {
        var changes = new List<string>();
        foreach (Promise promise in PromisesFulfilledBy(work))
        {
            changes.AddRange(Fulfil(promise));
        }

        bool mine = work.SponsorId == RaceSetup.PlayerId || (Role == GovRole.Prefeito && work.SponsorId == Work.Prefeito);
        if (mine)
        {
            // Obra entregue na zona de quem ajudou na campanha quita a dívida com essa pessoa.
            foreach (Leader leader in City.Leaders.Where(l => l.Zones.Contains(work.ZoneId)))
            {
                changes.AddRange(SettleDebts(leader.PersonId));
            }
        }

        return changes;
    }

    /// <summary>Marca a promessa como cumprida: quem cobrava lembra, e a zona responde.</summary>
    private List<string> Fulfil(Promise promise)
    {
        var changes = new List<string>();
        if (promise.State != PromiseState.Aberta)
        {
            return changes;
        }

        GovernanceState g = G;
        promise.State = PromiseState.Cumprida;
        string? holder = HolderOf(promise);
        MemoryDefinition memory = Content.Collection.Lembrancas["cumpriu"];
        Remember(holder, HolderRole(promise), "promessa_cumprida", memory, ("promessa", promise.Text));
        changes.Add(Fill(Content.Collection.Texto("cumprida"), ("promessa", promise.Text)));
        if (holder is not null)
        {
            changes.Add(ChRelation(NameOf(holder), memory.Pontos));
        }

        if (promise.ZoneId is { } zoneId && g.IndexOf(zoneId) is >= 0 and var zone)
        {
            if (Role == GovRole.Vereador)
            {
                GainPresence(zone, CollectionRules.PresencaPromessaCumprida);
            }
            else
            {
                ShiftZoneApproval(zone, CollectionRules.PresencaPromessaCumprida);
            }
        }

        ShiftImage(CollectionRules.ImagemPromessaCumprida);
        News("cobranca", Fill(Content.Collection.Texto("cumprida"), ("promessa", promise.Text), ("nome", NameOf(holder))), promise.ZoneId, holder);
        return changes;
    }

    /// <summary>Quita as dívidas com a pessoa (ela foi nomeada, ou a zona dela recebeu a obra).</summary>
    private List<string> SettleDebts(string personId)
    {
        var changes = new List<string>();
        foreach (Debt debt in State.Ledger.Debts.Where(d => d.ToPersonId == personId && !d.Settled))
        {
            debt.Settled = true;
            Remember(personId, PersonRole.Lideranca, "divida_quitada", Content.Collection.Lembrancas["divida_quitada"]);
            string text = Fill(Content.Collection.Texto("divida_quitada"), ("nome", NameOf(personId)), ("divida", debt.Text));
            changes.Add(text);
            News("cobranca", text, null, personId);
        }

        return changes;
    }

    /// <summary>Promessas de presença abertas numa zona: cumprem-se quando o mandato vai até lá.</summary>
    private List<Promise> PresencePromises(string zoneId) =>
        State.Ledger.Open.Where(p => p.ZoneId == zoneId && Content.Collection.PromessaDePresenca.Contains(p.Object)).ToList();

    /// <summary>Promessas de presença e de posição que chegaram abertas ao mandato se cumprem com o gabinete na zona ou com um projeto no tema.</summary>
    private void FulfilByPresence(string zoneId)
    {
        foreach (Promise promise in PresencePromises(zoneId))
        {
            Fulfil(promise);
        }
    }

    /// <summary>Promessas que a entrega da obra cumpre: as da própria obra e as de presença na zona.</summary>
    private List<Promise> PromisesOnDelivery(Work work) => PromisesFulfilledBy(work).Concat(PresencePromises(work.ZoneId)).Distinct().ToList();

    private void FulfilByTheme(string? theme)
    {
        if (theme is null)
        {
            return;
        }

        foreach (Promise promise in State.Ledger.Open.Where(p => Content.Collection.PromessaDePosicao.Contains(p.Object) && City.Leader(p.ToId)?.Theme == theme).ToList())
        {
            Fulfil(promise);
        }
    }

    // ---------------------------------------------------------------- prefeito: cargo prometido

    private Promise? PostPromise(string promiseId) =>
        State.Ledger.Open.FirstOrDefault(p => p.Id == promiseId && p.ToKind == Promise.ParaPessoa && Content.Collection.PromessaDeCargo.Contains(p.Object));

    public GovForecast ForecastGrantPost(string promiseId)
    {
        if (Role != GovRole.Prefeito)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        if (PostPromise(promiseId) is not { } promise)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        FiscalLawConfig law = Context.FiscalLaw;
        if (law.VedadoAumentar(G.Payroll.Share))
        {
            return GovForecast.Block(GovFailure.VedadoArt22, Fill(Content.Personnel.Texto("vedado_art22"), ("folha", Pct(G.Payroll.Share)), ("prudencial", Pct(law.PrudencialExecutivo)), ("artigo", law.ArtigoPrudencial)));
        }

        MemoryDefinition memory = Content.Collection.Lembrancas["cumpriu"];
        return new GovForecast(null, null,
        [
            new ForecastLine(GovForecast.Folha, null, CouncilRules.CargoFolha, CouncilRules.CargoFolha, CouncilRules.CargoFolha),
            new ForecastLine(GovForecast.Relacao, promise.ToId, memory.Pontos, memory.Pontos, memory.Pontos),
            new ForecastLine(GovForecast.PromessaCumprida, null, 1, 1, 1),
        ], [], [Fill(Content.Works.Texto("previsao_promessa"), ("promessa", promise.Text)), Fill(Content.Works.Texto("previsao_folha"), ("folha", Pct(CouncilRules.CargoFolha)))])
        { Remembered = [NameOf(promise.ToId)] };
    }

    /// <summary>O prefeito nomeia para um cargo de confiança quem tinha a vaga prometida: cumpre a promessa e pesa na folha.</summary>
    public GovResult GrantPost(string promiseId)
    {
        GovForecast forecast = ForecastGrantPost(promiseId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Promise promise = PostPromise(promiseId)!;
        G.Payroll.Share += CouncilRules.CargoFolha;
        List<string> changes = Fulfil(promise);
        changes.AddRange(SettleDebts(promise.ToId));
        changes.Add(Fill(Content.Works.Texto("previsao_folha"), ("folha", Pct(CouncilRules.CargoFolha))));
        return new GovResult(null, changes[0], changes, []);
    }

    // ---------------------------------------------------------------- semana

    private DateOnly FirstCollection(Promise promise)
    {
        IntRange range = CollectionRules.PrimeiraCobrancaSemanas;
        int weeks = range.Min + (int)(StableHash(promise.Id + promise.Text) % (ulong)(range.Max - range.Min + 1));
        DateOnly from = promise.Made > G.Start ? promise.Made : G.Start;
        return from.AddDays(7 * weeks);
    }

    /// <summary>Quantas cobranças a promessa já deveria ter recebido até hoje.</summary>
    private int CollectionsDue(Promise promise)
    {
        DateOnly first = FirstCollection(promise);
        if (Week < first)
        {
            return 0;
        }

        DateOnly deadline = DeadlineOf(promise);
        int span = deadline.DayNumber - promise.Made.DayNumber;
        return 1 + CollectionRules.CobrancasNoPrazo.Count(f => promise.Made.AddDays((int)(span * f)) is var date && date > first && date <= Week);
    }

    private void CollectionWeek()
    {
        GovernanceState g = G;
        foreach (Promise promise in State.Ledger.Open.ToList())
        {
            string? holder = HolderOf(promise);
            if (promise.Cobrancas < CollectionsDue(promise))
            {
                promise.Cobrancas++;
                string who = holder is not null ? NameOf(holder)
                    : promise.ToKind == Promise.ParaZona ? Fill(Content.Collection.Texto("quem_moradores"), ("zona", ZoneName(promise.ToId))) : Content.Collection.Texto("quem_grupo");
                string key = promise.ToKind switch { Promise.ParaPessoa => "cobranca_pessoa", Promise.ParaZona => "cobranca_zona", _ => "cobranca_grupo" };
                string text = Fill(Content.Collection.Texto(key), ("nome", who), ("promessa", promise.Text), ("prazo", Texts.Date(DeadlineOf(promise)) + " de " + DeadlineOf(promise).Year.ToString(CultureInfo.InvariantCulture)),
                    ("vez", promise.Cobrancas.ToString(CultureInfo.InvariantCulture)));
                g.Demands.Add(new Demand { Id = g.NewId("cob"), Kind = Demand.Promessa, RefId = promise.Id, PersonId = holder, ZoneId = promise.ZoneId, Date = Week, Deadline = DeadlineOf(promise), Text = text });
                Remember(holder, HolderRole(promise), "cobrou", Content.Collection.Lembrancas["cobrou"], ("promessa", promise.Text));
                News("cobranca", text, promise.ZoneId, holder);
            }

            // Os acordos de voto têm o próprio acerto (o vereador enganado deixa de negociar).
            if (DeadlineOf(promise) >= Week || g.Deals.Any(d => d.PromiseId == promise.Id))
            {
                continue;
            }

            promise.State = PromiseState.Vencida;
            Remember(holder, HolderRole(promise), "promessa_vencida", Content.Collection.Lembrancas["venceu"], ("promessa", promise.Text));
            ShiftImage(CollectionRules.ImagemPromessaVencida);
            if (promise.ZoneId is { } zoneId && g.IndexOf(zoneId) is >= 0 and var zone)
            {
                if (Role == GovRole.Vereador)
                {
                    GainPresence(zone, CollectionRules.PresencaPromessaVencida);
                }
                else
                {
                    ShiftZoneApproval(zone, CollectionRules.AprovacaoPromessaVencida);
                }
            }

            News("cobranca", Fill(Content.Collection.Texto("vencida"), ("promessa", promise.Text), ("nome", NameOf(holder))), promise.ZoneId, holder);
            if (holder is not null && City.Leader(holder) is { } leader && leader.AlignedWith == RaceSetup.PlayerId)
            {
                // A liderança deixa de estar com o jogador e passa a procurar um desafiante.
                leader.AlignedWith = null;
                News("cobranca", Fill(Content.Collection.Texto("lideranca_desafiante"), ("nome", NameOf(holder)), ("zona", ZoneName(leader.Zones[0]))), leader.Zones[0], holder);
            }
        }

        foreach (Debt debt in State.Ledger.Debts.Where(d => !d.Settled).ToList())
        {
            int asked = g.Demands.Count(d => d.Kind == Demand.Divida && d.RefId == debt.Id);
            DateOnly from = debt.Made > g.Start ? debt.Made : g.Start;
            if (asked > CollectionRules.DividaCobrancasMaximas || Week < from.AddDays(7 * CollectionRules.DividaIntervaloSemanas * (asked + 1)))
            {
                continue;
            }

            bool last = asked == CollectionRules.DividaCobrancasMaximas;
            string text = Fill(Content.Collection.Texto(last ? "divida_esquecida" : "cobranca_divida"), ("nome", NameOf(debt.ToPersonId)), ("divida", debt.Text));
            g.Demands.Add(new Demand { Id = g.NewId("cob"), Kind = Demand.Divida, RefId = debt.Id, PersonId = debt.ToPersonId, Date = Week, Text = text, Met = last ? false : null });
            Remember(debt.ToPersonId, PersonRole.Lideranca, last ? "divida_ignorada" : "cobrou", Content.Collection.Lembrancas[last ? "divida_ignorada" : "cobrou"], ("promessa", debt.Text));
            News("cobranca", text, null, debt.ToPersonId);
        }
    }

    /// <summary>
    /// Como anda a cobrança das promessas que chegaram abertas a este mandato: quantas eram e quantas já foram
    /// cobradas ao menos uma vez.
    /// </summary>
    public (int Promises, int Collected) CollectionStats()
    {
        List<Promise> atStart = State.Ledger.Promises.Where(p => G.PromisesAtStart.Contains(p.Id)).ToList();
        return (atStart.Count, atStart.Count(p => p.Cobrancas > 0));
    }
}
