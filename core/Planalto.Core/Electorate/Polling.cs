using Planalto.Core.State;

namespace Planalto.Core.Electorate;

/// <summary>Resultado publicado de uma pesquisa (percentuais de 0 a 100).</summary>
public sealed class PollResult
{
    public required string PollsterId { get; init; }

    public required int SampleSize { get; init; }

    /// <summary>Margem de erro em pontos percentuais, para mais ou para menos, a 95% de confiança.</summary>
    public required double MarginOfError { get; init; }

    public required IReadOnlyDictionary<string, double> Candidates { get; init; }

    public required double BlankNull { get; init; }

    public required double Undecided { get; init; }

    /// <summary>Só na pesquisa interna: intenção por grupo de segmento (ex.: "evangelica").</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>? ByGroup { get; init; }
}

/// <summary>Pesquisas com amostragem simulada, viés de casa e margem de erro (GDD 5.3, DECISOES D-014).</summary>
public static class Polling
{
    public const string BlankNullKey = "branco_nulo";
    public const string UndecidedKey = "nao_sabe";

    /// <summary>Margem de erro máxima (p = 0,5), em pontos percentuais.</summary>
    public static double MarginOfError(int sampleSize, double designEffect, double z) =>
        100 * z * Math.Sqrt(0.25 / sampleSize) * Math.Sqrt(designEffect);

    /// <summary>Fração de indecisos: decresce linearmente do início da campanha até a eleição.</summary>
    public static double Undecided(int weeksToElection, PollConfig config)
    {
        double progress = 1 - (Math.Clamp(weeksToElection, 0, config.CampaignWeeks) / (double)config.CampaignWeeks);
        return config.UndecidedAtCampaignStart + ((config.UndecidedAtElection - config.UndecidedAtCampaignStart) * progress);
    }

    /// <summary>
    /// Sorteia os entrevistados entre os prováveis eleitores (peso = eleitores × comparecimento), inclina as
    /// intenções pelo viés do instituto e devolve os percentuais da amostra.
    /// </summary>
    public static PollResult Run(
        VoteProjection truth,
        IReadOnlyDictionary<string, VoteCandidate> candidates,
        PollsterDefinition pollster,
        int weeksToElection,
        VoteModelConfig config,
        Rng rng)
    {
        int n = rng.NextInt(pollster.SampleMin, pollster.SampleMax + 1);
        double undecided = Undecided(weeksToElection, config.Poll);

        // Distribuição de respostas de cada segmento, com o viés de casa aplicado.
        var segments = truth.Segments
            .Select(s => (Weight: s.Segment.Voters * s.Turnout, Answers: Answers(s, candidates, pollster.HouseBias, undecided)))
            .Where(s => s.Weight > 0)
            .ToList();

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < n; i++)
        {
            var respondent = rng.PickWeighted(segments, s => s.Weight);
            string answer = rng.PickWeighted(respondent.Answers, a => a.Probability).Key;
            counts[answer] = counts.GetValueOrDefault(answer) + 1;
        }

        double Percent(string key) => 100.0 * counts.GetValueOrDefault(key) / n;
        return new PollResult
        {
            PollsterId = pollster.Id,
            SampleSize = n,
            MarginOfError = MarginOfError(n, pollster.DesignEffect, config.Poll.ConfidenceZ),
            Candidates = candidates.Keys.ToDictionary(id => id, Percent, StringComparer.Ordinal),
            BlankNull = Percent(BlankNullKey),
            Undecided = Percent(UndecidedKey),
            ByGroup = pollster.Internal ? GroupBreakdown(truth, candidates, undecided) : null,
        };
    }

    private static List<(string Key, double Probability)> Answers(
        SegmentIntention intention,
        IReadOnlyDictionary<string, VoteCandidate> candidates,
        double houseBias,
        double undecided)
    {
        // Viés de casa: multiplica cada candidato por e^(viés × eixo econômico / 100) e renormaliza.
        var tilted = intention.Shares.ToDictionary(
            s => s.Key,
            s => s.Value * Math.Exp(houseBias * candidates[s.Key].Position.Economic / 100.0),
            StringComparer.Ordinal);
        double sum = tilted.Values.Sum();
        double decided = (1 - intention.BlankNull) * (1 - undecided);

        var answers = tilted.Select(t => (t.Key, decided * t.Value / sum)).ToList();
        answers.Add((BlankNullKey, intention.BlankNull * (1 - undecided)));
        answers.Add((UndecidedKey, undecided));
        return answers;
    }

    /// <summary>Intenção esperada por grupo (sem amostragem), exclusiva da pesquisa interna.</summary>
    private static Dictionary<string, IReadOnlyDictionary<string, double>> GroupBreakdown(
        VoteProjection truth,
        IReadOnlyDictionary<string, VoteCandidate> candidates,
        double undecided)
    {
        var result = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);
        foreach (string group in truth.Segments.SelectMany(s => s.Segment.Groups.Values).Distinct())
        {
            var members = truth.Segments.Where(s => s.Segment.Has(group)).ToList();
            double weight = members.Sum(s => s.Segment.Voters * s.Turnout);
            result[group] = candidates.Keys.ToDictionary(
                id => id,
                id => weight <= 0 ? 0 : 100 * members.Sum(s => s.Segment.Voters * s.Turnout * (1 - s.BlankNull) * (1 - undecided) * s.Shares[id]) / weight,
                StringComparer.Ordinal);
        }

        return result;
    }
}
