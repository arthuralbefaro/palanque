using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Campaign;

/// <summary>
/// Consultas que a tela faz ao motor (princípios f e j): só leem o estado, com as mesmas regras que o motor aplica,
/// para a interface não repetir conta de jogo.
/// </summary>
public sealed partial class CampaignEngine
{
    /// <summary>Quanto renderia hoje mais um compromisso igual (tipo, zona, público), só pelo cansaço já acumulado.</summary>
    public double FatigueYield(string fatigueKey) =>
        1 / (1 + (Rules.CansacoFator * Agenda.Fatigue.GetValueOrDefault(fatigueKey)));

    /// <summary>Candidatos nomeados da disputa do jogador com mais presença na zona, do maior para o menor.</summary>
    public IReadOnlyList<(Candidate Candidate, double Presence)> TopPresence(int zone, int count) =>
        Campaign.ForOffice(Campaign.Player.Office).Where(c => !c.IsBulk)
            .Select(c => (Candidate: c, Presence: Territory.PresenceOf(c.Id, zone)))
            .OrderByDescending(x => x.Presence).ThenBy(x => x.Candidate.Id, StringComparer.Ordinal)
            .Take(count).ToList();

    /// <summary>O jogador tem a maior presença da zona entre os candidatos nomeados da sua disputa.</summary>
    public bool PlayerLeadsPresence(int zone) =>
        zone >= 0 && TopPresence(zone, 1) is [{ Candidate.IsPlayer: true }, ..];

    /// <summary>Prazo da promessa que nasce de aceitar o pedido: antes da eleição, ou tantas semanas depois da posse.</summary>
    public DateOnly PromiseDeadline(LeaderRequestDefinition request) =>
        request.PrazoSemanas <= 0
            ? Campaign.ElectionDate
            : new DateOnly(Campaign.ElectionDate.Year + 1, 1, 1).AddDays(7 * request.PrazoSemanas);

    /// <summary>O que a resposta ao pedido da liderança ganha e custa, por natureza, para a tela mostrar a troca.</summary>
    public DecisionTrade? LeaderAnswerTrade(Leader leader, LeaderAnswer answer) =>
        LeaderContent.TradeOf(LeaderContent.Request(leader.RequestId), LeaderCatalog.AnswerKey(answer));

    /// <summary>Quanto sai do caixa ao aceitar um pedido de dinheiro (gasto irregular), no porte da cidade.</summary>
    public decimal RequestMoneyCost() => Math.Round(1500m * (decimal)CostScale / 100) * 100;

    /// <summary>
    /// A liderança passaria a apoiar o jogador se o pedido fosse atendido e a relação ganhasse esses pontos (a mesma
    /// regra da mudança de lado: relação no preço dela e nenhum rival com favor maior).
    /// </summary>
    public bool WouldAlignWithPlayer(Leader leader, int relationPoints)
    {
        double relation = Math.Clamp(RelationOf(leader.PersonId) + relationPoints, People.Person.MinRelation, People.Person.MaxRelation);
        double bestFavor = leader.Favor.Count == 0 ? 0 : leader.Favor.Values.Max();
        return relation >= leader.Price && relation >= bestFavor;
    }

    /// <summary>Primeiro turno livre a partir de hoje, dentro do prazo de marcação (para "Marcar compromisso aqui").</summary>
    public (DateOnly Date, Turno Turno) NextFreeSlot()
    {
        for (int d = 0; d <= Rules.AntecedenciaDias; d++)
        {
            DateOnly date = Today.AddDays(d);
            foreach (Turno turno in Enum.GetValues<Turno>())
            {
                if (!TurnoPassed(date, turno) && Agenda.Scheduled.All(c => c.Delegado || c.Date != date || !TurnsOf(Commitments.Get(c.Type), c.Turno).Contains(turno)))
                {
                    return (date, turno);
                }
            }
        }

        return (Today, Turno.Manha);
    }

    /// <summary>Compromisso marcado que ocupa o turno (o comício ocupa dois).</summary>
    public Commitment? Occupying(DateOnly date, Turno turno) =>
        Agenda.Scheduled.FirstOrDefault(c => !c.Delegado && c.Date == date && TurnsOf(Commitments.Get(c.Type), c.Turno).Contains(turno));

    /// <summary>
    /// Previsão de um compromisso já marcado, como se fosse marcado agora: ele sai da agenda só durante a conta,
    /// para não bloquear o próprio turno nem contar duas vezes no estresse do dia.
    /// </summary>
    public CommitmentForecast ForecastOf(Commitment c)
    {
        int index = Agenda.Scheduled.IndexOf(c);
        if (index >= 0)
        {
            Agenda.Scheduled.RemoveAt(index);
        }

        try
        {
            return Forecast(RequestOf(c));
        }
        finally
        {
            if (index >= 0)
            {
                Agenda.Scheduled.Insert(index, c);
            }
        }
    }

    /// <summary>Grupo de eleitores que recebe o apoio do compromisso (o do evento, o escolhido ou o fixo do tipo); null se não há.</summary>
    public string? SupportGroupOf(CommitmentRequest r) => SupportGroup(Commitments.Get(r.Type), r);
}
