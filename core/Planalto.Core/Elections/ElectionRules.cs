namespace Planalto.Core.Elections;

/// <summary>
/// Regras de apuração brasileiras. Recebe apenas votos válidos (nominais e de legenda);
/// brancos e nulos ficam de fora. Todas as contas usam inteiros, sem arredondamento de ponto flutuante.
/// </summary>
public static class ElectionRules
{
    /// <summary>CF, art. 29, II: 2º turno para prefeito só acima de 200 mil eleitores.</summary>
    public const long MunicipalRunoffElectorateThreshold = 200_000;

    // CF, art. 29, IV (EC 58/2009): limite superior de população de cada faixa e número de vagas.
    private static readonly (long MaxPopulation, int Seats)[] CouncilSeatBands =
    [
        (15_000, 9),
        (30_000, 11),
        (50_000, 13),
        (80_000, 15),
        (120_000, 17),
        (160_000, 19),
        (300_000, 21),
        (450_000, 23),
        (600_000, 25),
        (750_000, 27),
        (900_000, 29),
        (1_050_000, 31),
        (1_200_000, 33),
        (1_350_000, 35),
        (1_500_000, 37),
        (1_800_000, 39),
        (2_400_000, 41),
        (3_000_000, 43),
        (4_000_000, 45),
        (5_000_000, 47),
        (6_000_000, 49),
        (7_000_000, 51),
        (8_000_000, 53),
        (long.MaxValue, 55),
    ];

    public static int VagasVereador(long populacao)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(populacao);
        return CouncilSeatBands.First(band => populacao <= band.MaxPopulation).Seats;
    }

    public static bool TemSegundoTurno(long eleitorado)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(eleitorado);
        return eleitorado > MunicipalRunoffElectorateThreshold;
    }

    /// <summary>
    /// Código Eleitoral, art. 106: votos válidos ÷ vagas, desprezada a fração se igual ou
    /// inferior a meio, equivalente a um se superior.
    /// </summary>
    public static long ElectoralQuotient(long validVotes, int seats)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(validVotes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seats);

        long quotient = Math.DivRem(validVotes, seats, out long remainder);
        return remainder * 2 > seats ? quotient + 1 : quotient;
    }

    /// <summary>Eleição proporcional (vereador, deputado) pelas três etapas dos arts. 106 a 109.</summary>
    public static ProportionalResult Proportional(int seats, IReadOnlyList<PartyList> parties, ProportionalRulesConfig? config = null)
    {
        config ??= ProportionalRulesConfig.Default;
        config.Validate();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(seats);
        ValidateParties(parties);

        var tallies = parties.Select(p => new PartyTally(p)).ToList();
        long validVotes = tallies.Sum(t => t.Votes);
        if (validVotes == 0)
        {
            return new ProportionalResult(
                0, 0, tallies.ToDictionary(t => t.PartyId, _ => 0L), [], seats, tallies.ToDictionary(t => t.PartyId, _ => 0));
        }

        long quotient = ElectoralQuotient(validVotes, seats);
        var elected = new List<ElectedCandidate>();

        // 1ª etapa (arts. 107 e 108): cada partido leva o quociente partidário, ocupado só por quem
        // tem a votação nominal mínima; o que sobrar vai para as sobras.
        var partyQuotients = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (PartyTally tally in tallies.OrderByDescending(t => t.Votes).ThenBy(t => t.PartyId, StringComparer.Ordinal))
        {
            // Quociente zero só ocorre com menos votos válidos que meia vaga; aí ninguém tem QP.
            long partyQuotient = quotient == 0 ? 0 : tally.Votes / quotient;
            partyQuotients[tally.PartyId] = partyQuotient;

            // A média usa votos ÷ (quociente partidário + 1), mesmo quando parte das vagas do QP
            // não pôde ser preenchida pela votação nominal mínima.
            tally.SeatsObtained = (int)Math.Min(partyQuotient, seats);

            for (long i = 0; i < partyQuotient && elected.Count < seats; i++)
            {
                CandidateVotes? next = tally.NextCandidate;
                if (next is null || !MeetsPercent(next.Votes, quotient, config.CandidateMinimumPercent))
                {
                    break;
                }

                elected.Add(tally.Elect(SeatStage.PartyQuotient));
            }
        }

        // 2ª etapa (art. 109, I, II e §2º): disputam os partidos com a cláusula. Quem tem a maior média
        // obtém a sobra; se não tiver candidato com a votação mínima, a sobra conta no divisor dele
        // mesmo não preenchida e a vaga volta para a disputa.
        List<PartyTally> remainderParties = tallies
            .Where(t => MeetsPercent(t.Votes, quotient, config.PartyRemainderMinimumPercent))
            .ToList();
        bool CanFillRemainder(PartyTally t) =>
            t.NextCandidate is { } next && MeetsPercent(next.Votes, quotient, config.CandidateRemainderMinimumPercent);

        DistributeRemainders(remainderParties, CanFillRemainder, SeatStage.Remainder, seats, elected);

        // 3ª etapa (art. 109, III, com a decisão do STF): sem piso individual; o partido ocupa a vaga
        // com o seu mais votado ainda não eleito.
        List<PartyTally> finalParties = config.FinalStageOpenToAllParties ? tallies : remainderParties;
        DistributeRemainders(finalParties, t => t.NextCandidate is not null, SeatStage.FinalRemainder, seats, elected);

        return new ProportionalResult(
            validVotes,
            quotient,
            partyQuotients,
            elected,
            seats - elected.Count,
            tallies.ToDictionary(t => t.PartyId, t => t.UnfilledRemainders, StringComparer.Ordinal));
    }

    private static void DistributeRemainders(
        List<PartyTally> parties,
        Func<PartyTally, bool> canFill,
        SeatStage stage,
        int seats,
        List<ElectedCandidate> elected)
    {
        // Só continua enquanto algum partido capaz de ocupar a vaga tem média positiva; os demais
        // perdem média a cada sobra não preenchida, então o laço sempre termina.
        while (elected.Count < seats && parties.Any(t => t.Votes > 0 && canFill(t)))
        {
            PartyTally winner = BestAverage(parties);
            winner.SeatsObtained++;

            if (canFill(winner))
            {
                elected.Add(winner.Elect(stage));
            }
            else
            {
                winner.UnfilledRemainders++;
            }
        }
    }

    /// <summary>
    /// 1º turno de eleição majoritária. Com 2º turno previsto, só elege quem tem maioria absoluta
    /// dos votos válidos; sem 2º turno, elege o mais votado.
    /// </summary>
    public static MajoritarianResult Majoritarian(IReadOnlyList<CandidateVotes> candidates, bool hasRunoff)
    {
        List<CandidateVotes> ranked = RankCandidates(candidates);
        long validVotes = ranked.Sum(c => c.Votes);
        if (validVotes == 0)
        {
            return new MajoritarianResult(MajoritarianOutcome.NoValidVotes, 0, null, []);
        }

        CandidateVotes first = ranked[0];
        if (!hasRunoff || first.Votes * 2 > validVotes || ranked.Count == 1)
        {
            return new MajoritarianResult(MajoritarianOutcome.Elected, validVotes, first.CandidateId, []);
        }

        // Empate no 2º lugar: classifica o mais idoso (CF, art. 77, §5º), já garantido pela ordenação.
        return new MajoritarianResult(
            MajoritarianOutcome.Runoff, validVotes, null, [ranked[0].CandidateId, ranked[1].CandidateId]);
    }

    /// <summary>2º turno entre os dois classificados: vence o mais votado; empate, o mais idoso.</summary>
    public static MajoritarianResult Runoff(IReadOnlyList<CandidateVotes> finalists)
    {
        if (finalists.Count != 2)
        {
            throw new ArgumentException("O 2º turno tem exatamente dois candidatos.", nameof(finalists));
        }

        return Majoritarian(finalists, hasRunoff: false);
    }

    /// <summary>Senado: maioria simples, renovando 1 ou 2 vagas por eleição.</summary>
    public static IReadOnlyList<string> Senate(IReadOnlyList<CandidateVotes> candidates, int seats)
    {
        if (seats is not (1 or 2))
        {
            throw new ArgumentOutOfRangeException(nameof(seats), seats, "O Senado renova 1 ou 2 vagas por estado.");
        }

        return RankCandidates(candidates)
            .Where(c => c.Votes > 0)
            .Take(seats)
            .Select(c => c.CandidateId)
            .ToList();
    }

    private static bool MeetsPercent(long votes, long quotient, int percent) => votes * 100 >= quotient * percent;

    /// <summary>
    /// Partido com a maior média: votos ÷ (QP + sobras já obtidas, preenchidas ou não, + 1).
    /// Empate de médias: maior votação do partido; depois, candidato mais idoso.
    /// </summary>
    private static PartyTally BestAverage(List<PartyTally> tallies)
    {
        PartyTally best = tallies[0];
        foreach (PartyTally tally in tallies.Skip(1))
        {
            if (Compare(tally, best) > 0)
            {
                best = tally;
            }
        }

        return best;
    }

    private static int Compare(PartyTally a, PartyTally b)
    {
        // a.Votes / (a.Seats + 1) comparado com b.Votes / (b.Seats + 1), por multiplicação cruzada.
        int byAverage = (a.Votes * (b.SeatsObtained + 1)).CompareTo(b.Votes * (a.SeatsObtained + 1));
        if (byAverage != 0)
        {
            return byAverage;
        }

        int byVotes = a.Votes.CompareTo(b.Votes);
        if (byVotes != 0)
        {
            return byVotes;
        }

        if (a.NextCandidate is { } nextA && b.NextCandidate is { } nextB)
        {
            int byAge = nextB.BirthDate.CompareTo(nextA.BirthDate);
            if (byAge != 0)
            {
                return byAge;
            }
        }

        return string.CompareOrdinal(b.PartyId, a.PartyId);
    }

    /// <summary>Ordem de votação nominal; empate favorece o mais idoso (Código Eleitoral, art. 110).</summary>
    private static List<CandidateVotes> RankCandidates(IReadOnlyList<CandidateVotes> candidates)
    {
        if (candidates.Count == 0)
        {
            throw new ArgumentException("A eleição precisa de ao menos um candidato.", nameof(candidates));
        }

        ValidateCandidates(candidates);
        return candidates
            .OrderByDescending(c => c.Votes)
            .ThenBy(c => c.BirthDate)
            .ThenBy(c => c.CandidateId, StringComparer.Ordinal)
            .ToList();
    }

    private static void ValidateParties(IReadOnlyList<PartyList> parties)
    {
        if (parties.Count == 0)
        {
            throw new ArgumentException("A eleição precisa de ao menos um partido.", nameof(parties));
        }

        if (parties.Select(p => p.PartyId).Distinct(StringComparer.Ordinal).Count() != parties.Count)
        {
            throw new ArgumentException("Partido repetido na eleição.", nameof(parties));
        }

        foreach (PartyList party in parties)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(party.LegendVotes);
        }

        ValidateCandidates(parties.SelectMany(p => p.Candidates).ToList());
    }

    private static void ValidateCandidates(IReadOnlyList<CandidateVotes> candidates)
    {
        if (candidates.Select(c => c.CandidateId).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
        {
            throw new ArgumentException("Candidato repetido na eleição.", nameof(candidates));
        }

        foreach (CandidateVotes candidate in candidates)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(candidate.Votes);
        }
    }

    private sealed class PartyTally(PartyList list)
    {
        private readonly List<CandidateVotes> _ranked = list.Candidates.Count == 0 ? [] : RankCandidates(list.Candidates);
        private int _nextIndex;

        public string PartyId { get; } = list.PartyId;

        public long Votes { get; } = list.TotalVotes;

        /// <summary>Vagas que contam no divisor da média: QP mais sobras obtidas, preenchidas ou não.</summary>
        public int SeatsObtained { get; set; }

        public int UnfilledRemainders { get; set; }

        public CandidateVotes? NextCandidate => _nextIndex < _ranked.Count ? _ranked[_nextIndex] : null;

        public ElectedCandidate Elect(SeatStage stage)
        {
            CandidateVotes candidate = _ranked[_nextIndex++];
            return new ElectedCandidate(PartyId, candidate.CandidateId, candidate.Votes, stage);
        }
    }
}
