using Planalto.Core.Ideology;
using Planalto.Core.State;

namespace Planalto.Core.Electorate;

/// <summary>Candidato como o modelo de voto o vê (GDD 5.3).</summary>
public sealed class VoteCandidate
{
    public required string Id { get; init; }

    public required string PartyId { get; init; }

    public required IdeologicalPosition Position { get; init; }

    /// <summary>
    /// Posição em cada tema (-2 a 2); temas ausentes são derivados da posição nos eixos. Pode ser fracionária: é a
    /// posição que o eleitorado enxerga, mistura da posição do candidato (quem a conhece) com a do partido (GDD 17.9).
    /// </summary>
    public IReadOnlyDictionary<string, double> Themes { get; init; } = new Dictionary<string, double>();

    /// <summary>Reconhecimento no município (0 a 1).</summary>
    public required double Recognition { get; init; }

    /// <summary>Imagem: carisma, escândalos e mídia (-1 a 1).</summary>
    public double Image { get; init; }

    /// <summary>Apoio de líderes, igrejas, sindicatos etc., por grupo de segmento (0 a 1).</summary>
    public IReadOnlyDictionary<string, double> Support { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Quantos candidatos semelhantes esta opção representa (1 para uma pessoa). No softmax a fatia é multiplicada
    /// por esse número: é a agregação de vários candidatos com a mesma utilidade.
    /// </summary>
    public double Multiplicity { get; init; } = 1;
}

/// <summary>Intenção de um segmento: fração de cada candidato entre os votos válidos, brancos e nulos e comparecimento.</summary>
public sealed record SegmentIntention(Segment Segment, IReadOnlyDictionary<string, double> Shares, double BlankNull, double Turnout);

/// <summary>Resultado agregado do município, em votos (não inteiros).</summary>
public sealed class VoteProjection
{
    public required IReadOnlyList<SegmentIntention> Segments { get; init; }

    public required IReadOnlyDictionary<string, double> Votes { get; init; }

    public required double BlankNull { get; init; }

    public required double Abstention { get; init; }

    public double Turnout => Votes.Values.Sum() + BlankNull;

    public double Valid => Votes.Values.Sum();

    /// <summary>Fração de cada candidato entre os votos válidos.</summary>
    public IReadOnlyDictionary<string, double> ValidShares =>
        Votes.ToDictionary(v => v.Key, v => Valid <= 0 ? 0 : v.Value / Valid, StringComparer.Ordinal);
}

/// <summary>
/// Utilidade = a·proximidade ideológica + b·afinidade de temas + c·reconhecimento + d·imagem
/// + e·força do partido no segmento + f·apoios + ruído; intenção = softmax (GDD 5.3, DECISOES D-014),
/// ponderado pelo conjunto de consideração (reconhecimento, DECISOES D-026).
/// </summary>
public static class VoteModel
{
    private static readonly double MaxDistance = Math.Sqrt(2) * (AxisRange.AxisMax - AxisRange.AxisMin);

    /// <param name="rng">Ruído da eleição ou da rodada de simulação; null para a intenção média, sem ruído.</param>
    public static VoteProjection Project(
        MunicipalElectorate electorate,
        IReadOnlyList<VoteCandidate> candidates,
        VoteModelConfig config,
        ThemeCatalog themes,
        Rng? rng = null) =>
        Project(electorate.Segments, candidates, config, themes, Noise(candidates, config, rng));

    /// <summary>Um ruído por candidato: o "humor" da rodada, comum a todos os segmentos (e a todas as zonas).</summary>
    public static Dictionary<string, double> Noise(IReadOnlyList<VoteCandidate> candidates, VoteModelConfig config, Rng? rng) =>
        candidates.ToDictionary(c => c.Id, _ => rng is null ? 0 : ElectorateBuilder.Gaussian(rng) * config.NoiseSd, StringComparer.Ordinal);

    /// <summary>Projeção sobre uma lista de segmentos (o município inteiro ou uma zona), com o ruído já sorteado.</summary>
    public static VoteProjection Project(
        IReadOnlyList<Segment> segments,
        IReadOnlyList<VoteCandidate> candidates,
        VoteModelConfig config,
        ThemeCatalog themes,
        IReadOnlyDictionary<string, double> noise)
    {
        if (candidates.Count == 0)
        {
            throw new ArgumentException("Sem candidatos.", nameof(candidates));
        }

        var themePositions = candidates.ToDictionary(c => c.Id, c => ThemePositions(c, themes, config), StringComparer.Ordinal);
        var multiplicity = candidates.ToDictionary(c => c.Id, c => Math.Max(1, c.Multiplicity), StringComparer.Ordinal);

        // Conjunto de consideração (vale para todos os segmentos): quem não é conhecido quase não recebe voto.
        var awareness = candidates.ToDictionary(
            c => c.Id,
            c => config.Awareness.Exponent == 0 ? 1 : Math.Pow(Math.Clamp(c.Recognition, config.Awareness.Floor, 1), config.Awareness.Exponent),
            StringComparer.Ordinal);

        var intentions = new List<SegmentIntention>();
        var votes = candidates.ToDictionary(c => c.Id, _ => 0.0, StringComparer.Ordinal);
        double blankNull = 0;
        double abstention = 0;

        foreach (Segment segment in segments)
        {
            double maxAffinity = segment.Affinity.Count == 0 ? 0 : segment.Affinity.Values.Max();
            var utilities = candidates.ToDictionary(
                c => c.Id,
                c => Utility(segment, c, themePositions[c.Id], maxAffinity, config) + noise[c.Id],
                StringComparer.Ordinal);

            double best = utilities.Values.Max();
            var exp = utilities.ToDictionary(u => u.Key, u => Math.Exp((u.Value - best) / config.Temperature) * multiplicity[u.Key] * awareness[u.Key], StringComparer.Ordinal);
            double sum = exp.Values.Sum();
            var shares = exp.ToDictionary(e => e.Key, e => e.Value / sum, StringComparer.Ordinal);

            // Brancos e nulos crescem quando nem o melhor candidato agrada ao segmento.
            double satisfaction = Math.Clamp(best / config.Weights.Total, 0, 1);
            double bn = Math.Clamp(config.BlankNullBase + (config.BlankNullLowUtility * (1 - satisfaction)), 0, config.BlankNullMax);

            double voters = segment.Voters * segment.Turnout;
            foreach (var (id, share) in shares)
            {
                votes[id] += voters * (1 - bn) * share;
            }

            blankNull += voters * bn;
            abstention += segment.Voters - voters;
            intentions.Add(new SegmentIntention(segment, shares, bn, segment.Turnout));
        }

        return new VoteProjection { Segments = intentions, Votes = votes, BlankNull = blankNull, Abstention = abstention };
    }

    public static double Utility(Segment segment, VoteCandidate candidate, IReadOnlyDictionary<string, double> themePositions, double maxAffinity, VoteModelConfig config)
    {
        VoteWeights w = config.Weights;
        double proximity = 1 - (segment.Ideology.DistanceTo(AxisPoint.From(candidate.Position)) / MaxDistance);

        double themes = 0;
        foreach (var (theme, priority) in segment.ThemePriority)
        {
            double distance = Math.Abs(themePositions.GetValueOrDefault(theme) - segment.ThemePreference.GetValueOrDefault(theme));
            themes += priority * (1 - (distance / (ThemeDefinition.MaxPosition - ThemeDefinition.MinPosition)));
        }

        double party = maxAffinity <= 0 ? 0 : segment.Affinity.GetValueOrDefault(candidate.PartyId) / maxAffinity;
        double support = Math.Min(1, segment.Groups.Values.Sum(g => candidate.Support.GetValueOrDefault(g)));

        return (w.Ideology * proximity)
            + (w.Themes * themes)
            + (w.Recognition * Math.Clamp(candidate.Recognition, 0, 1))
            + (w.Image * Math.Clamp(candidate.Image, -1, 1))
            + (w.Party * party)
            + (w.Support * support);
    }

    private static Dictionary<string, double> ThemePositions(VoteCandidate candidate, ThemeCatalog themes, VoteModelConfig config) =>
        themes.Themes.ToDictionary(
            t => t.Id,
            t => candidate.Themes.TryGetValue(t.Id, out double position)
                ? position
                : ElectorateBuilder.ThemePreference(t, AxisPoint.From(candidate.Position), config),
            StringComparer.Ordinal);
}
