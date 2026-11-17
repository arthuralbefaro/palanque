using Planalto.Core.Elections;
using Planalto.Core.Electorate;
using Planalto.Core.State;

namespace Planalto.Core.Campaign;

/// <summary>Uma linha da estimativa da equipe: o candidato, o ponto central e a faixa, em % dos entrevistados.</summary>
public sealed record EstimateLine(string CandidateId, double Share, double Low, double High);

/// <summary>
/// Estimativa da equipe (RL): o que a campanha acha da intenção de voto sem nenhuma pesquisa. Mesma conta da
/// pesquisa (percentual sobre quem iria votar, com os indecisos da semana), sem amostra e sem viés de instituto, mas
/// com uma faixa larga, porque ninguém foi entrevistado (data/electorate/vote_model.json, poll.team_estimate). A tela
/// mostra como estimativa, nunca como pesquisa.
/// </summary>
public sealed record IntentionEstimate(IReadOnlyList<EstimateLine> Lines, double Undecided, DateOnly Week);

/// <summary>Estimativa da equipe: só lê o estado.</summary>
public sealed partial class CampaignEngine
{
    /// <summary>Estimativa da intenção de voto na disputa do jogador, do mais votado ao menos votado.</summary>
    public IntentionEstimate TeamEstimate()
    {
        Office office = Campaign.Player.Office;
        PollConfig poll = Context.ElectorateContent.VoteModel.Poll;
        TeamEstimateConfig config = poll.TeamEstimate;

        // A projeção de hoje, sem ruído (a da pesquisa é a do começo da semana): a estimativa acompanha cada compromisso.
        VoteProjection projection = Project(office, noise: null);
        double undecided = Polling.Undecided(WeeksToElection, poll);
        double turnout = projection.Turnout;
        var lines = new List<EstimateLine>();
        foreach (Candidate c in Campaign.ForOffice(office).Where(c => !c.IsBulk))
        {
            double share = turnout <= 0 ? 0 : 100 * (1 - undecided) * projection.Votes.GetValueOrDefault(c.Id) / turnout;
            double margin = Math.Max(config.MarginMinPoints, config.MarginRelative * share);
            lines.Add(new EstimateLine(c.Id, Math.Round(share, 1), Math.Round(Math.Max(0, share - margin), 1), Math.Round(Math.Min(100, share + margin), 1)));
        }

        return new IntentionEstimate(
            lines.OrderByDescending(l => l.Share).ThenBy(l => l.CandidateId, StringComparer.Ordinal).ToList(),
            Math.Round(100 * undecided, 1),
            State.Calendar.WeekStart);
    }
}
