using System.Diagnostics;
using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.State;

namespace Planalto.BalanceSim;

/// <summary>
/// O robô nos sistemas da R3 (GDD 17.11 a 17.13): responde às ofertas de doadores (limpo: só com recibo; "sujo":
/// aceita todo dinheiro por fora), responde aos ataques pela resposta de menor dano esperado, ataca de vez em quando
/// quem está logo à frente na pesquisa, dá entrevista no veículo de maior ganho previsto e trata do tema em alta.
/// Depois da eleição, simula o julgamento das contas e a representação por dois anos.
/// </summary>
public sealed partial class CampaignRobot
{
    /// <summary>Réplicas do processo de contas depois da eleição, por campanha.</summary>
    public const int AccountsReplicas = 40;

    /// <summary>Semanas entre dois ataques do robô (só há a quem atacar depois da primeira pesquisa pública).</summary>
    private const int AttackEveryWeeks = 2;

    /// <summary>O robô "sujo" aceita todo dinheiro por fora que lhe oferecem; o limpo pede recibo ou recusa.</summary>
    public bool Dirty { get; init; }

    private readonly HashSet<string> _attacksSeen = new(StringComparer.Ordinal);

    private readonly Stopwatch _clock = new();

    /// <summary>Todo dia: ofertas de doação e ataques com prazo correndo.</summary>
    private void ManageR3Day()
    {
        foreach (DonorOffer offer in _engine.Finance.Offers.ToList())
        {
            DonationAnswer answer = offer.PorFora && Dirty ? DonationAnswer.PorFora
                : _engine.CanAnswerDonation(offer.Id, DonationAnswer.Oficial) is null ? DonationAnswer.Oficial : DonationAnswer.Recusar;
            DonationResult? result = _engine.AnswerDonation(offer.Id, answer).Result;
            if (result is not null)
            {
                Trace.DonationAnswers[answer.ToString()] = Trace.DonationAnswers.GetValueOrDefault(answer.ToString()) + 1;
                Decision("oferta de doação");
            }
        }

        foreach (PendingAttackView pending in _engine.PendingAttacks())
        {
            if (_attacksSeen.Add(pending.Attack.Id))
            {
                Trace.AttacksReceived++;
                Trace.RumorsReceived += pending.Attack.Rumor ? 1 : 0;
            }

            // A resposta de menor dano esperado; no empate, a mais barata. O contra-ataque fica para o plano de ataque.
            var best = pending.Options
                .Where(o => o.Blocked is null && o.Response.Id != AttackResponse.ContraAtaque)
                .Select(o => (o.Response, Forecast: _engine.ForecastResponse(pending.Attack.Id, o.Response.Id)))
                .OrderBy(x => x.Forecast.Line("attack_damage", pending.Attack.Id)?.Expected ?? double.MaxValue)
                .ThenByDescending(x => x.Forecast.Line("money")?.Expected ?? 0)
                .FirstOrDefault();
            if (best.Response is not null && _engine.RespondToAttack(pending.Attack.Id, best.Response.Id).Result is not null)
            {
                Trace.Responses[best.Response.Id] = Trace.Responses.GetValueOrDefault(best.Response.Id) + 1;
                Decision("resposta a ataque");
            }
        }
    }

    /// <summary>Começo da semana: estatística do mundo e, de tempos em tempos, um ataque a quem está logo à frente.</summary>
    private void ManageR3Week()
    {
        DateOnly from = State.Calendar.WeekStart.AddDays(-7);
        Trace.NewsByWeek.Add(Campaign.Media.News.Count(n => n.Date >= from && n.Date < State.Calendar.WeekStart && !n.CitaJogador));
        Trace.HotThemeWeeks += _engine.HotThemes().Count > 0 ? 1 : 0;
        foreach (RivalRecord record in Campaign.Rivals.Records)
        {
            if (record.Plan is { } plan && plan.ChangedOn >= from && plan.Trigger != RivalPlan.Rotina)
            {
                Trace.PlanChanges[plan.Trigger] = Trace.PlanChanges.GetValueOrDefault(plan.Trigger) + 1;
            }
        }

        if (_weeksPlayed % AttackEveryWeeks != 0 || Campaign.Polls.LastOrDefault(p => !p.Internal) is not { } poll || !poll.Candidates.TryGetValue(RaceSetup.PlayerId, out double own))
        {
            return;
        }

        string? ahead = poll.Candidates.Where(c => c.Key != RaceSetup.PlayerId && c.Value > own && Campaign.Find(c.Key) is { IsBulk: false }).OrderBy(c => c.Value).Select(c => c.Key).FirstOrDefault();
        if (ahead is null || _engine.Rivals.PactWith(ahead) is not null)
        {
            return;
        }

        RivalWeakness? weakness = Campaign.Rivals.Record(ahead)?.Weaknesses.FirstOrDefault(w => w.Known);
        string theme = _engine.Platform.Flags.FirstOrDefault() ?? _engine.Context.Themes.Themes[0].Id;
        var request = weakness is not null
            ? new AttackRequest(ahead, weakness.Type, AttackChannel.Redes, weakness.Id)
            : new AttackRequest(ahead, AttackType.Tema, AttackChannel.Redes, Theme: theme);
        if (_engine.AttackRival(request).Result is not null)
        {
            Trace.AttacksMade++;
            Decision("ataque");
        }
    }

    /// <summary>
    /// Fecha as contas da campanha e simula o que vem depois da eleição por dois anos: prestação final, julgamento e
    /// representação do art. 30-A. O processo é repetido em réplicas (como se a campanha tivesse sido eleita), para a
    /// fração de mandatos perdidos não depender de poucas campanhas.
    /// </summary>
    private void AccountsAfterElection()
    {
        AccountsView accounts = _engine.Accounts();
        Trace.Limit = accounts.Limit;
        Trace.CampaignSpent = accounts.Spent;
        Trace.OffBooksShare = accounts.OffBooksShare;
        Trace.Risk = accounts.Risk;
        foreach (var (source, amount) in accounts.BySource)
        {
            Trace.Raised[source] = amount;
        }

        if (State.Accounts is not { } original)
        {
            return;
        }

        FinanceCatalog content = _engine.Context.Campaign.Finance;
        CampaignFinanceLawConfig law = _engine.Context.FinanceLaw;
        for (int replica = 0; replica < AccountsReplicas; replica++)
        {
            var copy = new AccountsCase
            {
                Municipio = original.Municipio,
                ElectionDate = original.ElectionDate,
                Elected = true,
                Limit = original.Limit,
                Spent = original.Spent,
                OffBooks = original.OffBooks,
                Risk = original.Risk,
                HostileKnowers = original.HostileKnowers,
                Exposed = original.Exposed,
                Denuncias = original.Denuncias,
            };
            var rng = new Rng(State.Rng.Seed ^ (ulong)((replica + 1) * 7919));
            for (DateOnly day = original.ElectionDate; day <= original.ElectionDate.AddDays(7 * 118); day = day.AddDays(7))
            {
                AccountsEngine.Advance(copy, content, law, day, rng);
            }

            // Dois anos de mandato: da posse (1º de janeiro) ao fim do segundo ano.
            Trace.Replicas++;
            Trace.MandatesLost += copy.MandateLost && copy.MandateLostOn < new DateOnly(original.ElectionDate.Year + 3, 1, 1) ? 1 : 0;
            Trace.Representations += copy.Log.Any(h => h.Kind == "representacao") ? 1 : 0;
            Trace.Verdicts[copy.Verdict?.ToString() ?? "sem julgamento"] = Trace.Verdicts.GetValueOrDefault(copy.Verdict?.ToString() ?? "sem julgamento") + 1;
        }
    }
}
