namespace Planalto.Core.Elections;

/// <summary>Votação nominal de um candidato; a data de nascimento desempata a favor do mais idoso.</summary>
public sealed record CandidateVotes(string CandidateId, long Votes, DateOnly BirthDate);

/// <summary>Partido (ou federação, que conta como um só) numa eleição proporcional.</summary>
public sealed record PartyList(string PartyId, long LegendVotes, IReadOnlyList<CandidateVotes> Candidates)
{
    public long TotalVotes => LegendVotes + Candidates.Sum(c => c.Votes);
}

public enum SeatStage
{
    /// <summary>Vaga do quociente partidário (art. 108).</summary>
    PartyQuotient = 1,

    /// <summary>Sobra pelas maiores médias, com cláusula de desempenho (art. 109, I, II e §2º).</summary>
    Remainder = 2,

    /// <summary>Sobra residual pelas maiores médias (art. 109, III).</summary>
    FinalRemainder = 3,
}

public sealed record ElectedCandidate(string PartyId, string CandidateId, long Votes, SeatStage Stage);

public sealed record ProportionalResult(
    long ValidVotes,
    long ElectoralQuotient,
    IReadOnlyDictionary<string, long> PartyQuotients,
    IReadOnlyList<ElectedCandidate> Elected,
    int VacantSeats,
    IReadOnlyDictionary<string, int> UnfilledRemainders)
{
    public int SeatsOf(string partyId) => Elected.Count(e => e.PartyId == partyId);

    public bool IsElected(string candidateId) => Elected.Any(e => e.CandidateId == candidateId);
}

public enum MajoritarianOutcome
{
    Elected,
    Runoff,
    NoValidVotes,
}

public sealed record MajoritarianResult(
    MajoritarianOutcome Outcome,
    long ValidVotes,
    string? WinnerId,
    IReadOnlyList<string> RunoffCandidateIds);
