using Planalto.Core.Elections;
using Planalto.Core.Electorate;

namespace Planalto.Core.Campaign;

/// <summary>Verba semanal que se vê de um rival num canal, arredondada ao passo da verba ("cerca de").</summary>
public sealed record RivalBudgetLine(string ChannelId, decimal Weekly, IReadOnlyList<string> Targets);

/// <summary>Contas da chapa pela projeção de hoje: votos do partido (com a legenda), quociente eleitoral e votos válidos.</summary>
public sealed record SlateTotals(long PartyVotes, long LegendVotes, long Quotient, long ValidVotes);

/// <summary>Disputa de presença numa zona: quem lidera, quem vem atrás e a diferença entre os dois.</summary>
public sealed record ZoneContest(string ZoneId, string? LeaderId, string? RunnerUpId, double LeaderPresence, double Margin);

/// <summary>
/// Consultas de leitura para as telas da R3 (rivais, chapa e mapa da disputa). Nenhuma muda o estado nem cria regra:
/// só juntam o que os motores já calculam.
/// </summary>
public sealed partial class CampaignEngine
{
    /// <summary>
    /// Verba semanal de um rival por canal, como a campanha do jogador a enxerga: o total do canal arredondado ao
    /// passo da verba e os alvos em que ele gasta. Canal sem verba não aparece.
    /// </summary>
    public IReadOnlyList<RivalBudgetLine> RivalBudgetSeen(string rivalId)
    {
        if (Rivals.Record(rivalId) is not { } record)
        {
            return [];
        }

        decimal step = Math.Max(1, ChannelStep);
        return record.Budget.Where(a => a.Amount > 0).GroupBy(a => a.ChannelId)
            .Select(g => new RivalBudgetLine(g.Key, Math.Round(g.Sum(a => a.Amount) / step, MidpointRounding.AwayFromZero) * step, g.OrderByDescending(a => a.Amount).Select(a => a.TargetId).Distinct().ToList()))
            .Where(l => l.Weekly > 0)
            .OrderByDescending(l => l.Weekly)
            .ToList();
    }

    /// <summary>Votos do partido do jogador, da legenda e o quociente eleitoral na projeção de hoje (a mesma conta do painel da chapa).</summary>
    public SlateTotals SlateTotals()
    {
        VoteProjection projection = Project(Office.Vereador, null);
        var votes = projection.Votes.ToDictionary(v => v.Key, v => (long)Math.Round(v.Value), StringComparer.Ordinal);
        List<PartyList> lists = SlateLists(votes, [], null);
        ProportionalResult result = ElectionRules.Proportional(Context.SeatsIn(Campaign.ElectionDate.Year), lists);
        PartyList? own = lists.FirstOrDefault(l => l.PartyId == Campaign.Player.PartyId);
        long party = own is null ? 0 : own.LegendVotes + own.Candidates.Sum(c => c.Votes);
        return new SlateTotals(party, own?.LegendVotes ?? 0, result.ElectoralQuotient, result.ValidVotes);
    }

    /// <summary>Em cada zona, quem lidera a presença na disputa do jogador, quem vem em segundo e a diferença.</summary>
    public IReadOnlyList<ZoneContest> ZoneContests() =>
        Territory.Zones.Select((id, z) =>
        {
            var top = TopPresence(z, 2);
            return new ZoneContest(id, top.Count > 0 ? top[0].Candidate.Id : null, top.Count > 1 ? top[1].Candidate.Id : null,
                top.Count > 0 ? top[0].Presence : 0, top.Count > 1 ? top[0].Presence - top[1].Presence : top.Count > 0 ? top[0].Presence : 0);
        }).ToList();

    /// <summary>
    /// Depuração e testes de fumaça: um rival da disputa do jogador o ataca agora, pelas mesmas regras do ataque
    /// semanal (o prazo de resposta começa a correr). Devolve null se não há rival nomeado ou canal aberto.
    /// </summary>
    public Attack? TriggerRivalAttack(string? rivalId = null)
    {
        Candidate? attacker = Campaign.ForOffice(Campaign.Player.Office).Where(c => !c.IsPlayer && !c.IsBulk && (rivalId is null || c.Id == rivalId))
            .OrderByDescending(c => c.Recognition).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
        if (attacker is null || !CampaignRunning)
        {
            return null;
        }

        int before = Rivals.Attacks.Count;
        RivalAttack(attacker, Campaign.Player);
        return Rivals.Attacks.Count > before ? Rivals.Attacks[^1] : null;
    }
}
