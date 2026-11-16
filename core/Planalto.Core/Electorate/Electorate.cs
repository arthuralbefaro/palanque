using Planalto.Core.Ideology;
using Planalto.Core.Municipios;
using Planalto.Core.State;

namespace Planalto.Core.Electorate;

/// <summary>Posição ideológica contínua (segmentos têm médias, não inteiros).</summary>
public readonly record struct AxisPoint(double Economic, double Social)
{
    public double DistanceTo(AxisPoint other)
    {
        double de = Economic - other.Economic;
        double ds = Social - other.Social;
        return Math.Sqrt((de * de) + (ds * ds));
    }

    public static AxisPoint From(IdeologicalPosition p) => new(p.Economic, p.Social);
}

/// <summary>Grupo de eleitores de um município (GDD 5.3).</summary>
public sealed class Segment
{
    public required string Id { get; init; }

    /// <summary>Dimensão → grupo (ex.: "renda" → "baixa").</summary>
    public required IReadOnlyDictionary<string, string> Groups { get; init; }

    /// <summary>Eleitores aptos (fração do eleitorado do TSE, não inteiro).</summary>
    public required double Voters { get; init; }

    public required AxisPoint Ideology { get; init; }

    /// <summary>Prioridade de cada tema, somando 1.</summary>
    public required IReadOnlyDictionary<string, double> ThemePriority { get; init; }

    /// <summary>Posição preferida em cada tema (-2 a 2), derivada dos eixos.</summary>
    public required IReadOnlyDictionary<string, double> ThemePreference { get; init; }

    /// <summary>Comparecimento esperado (0 a 1), já com o fator do voto facultativo.</summary>
    public required double Turnout { get; init; }

    public required bool Facultative { get; init; }

    /// <summary>Afinidade com cada partido, somando 1 no segmento.</summary>
    public required Dictionary<string, double> Affinity { get; init; }

    public bool Has(string groupId) => Groups.Values.Contains(groupId);
}

/// <summary>Partido como o eleitorado o vê: posição e tamanho relativo.</summary>
public sealed record ElectoralParty(string Id, IdeologicalPosition Position, double SizeWeight);

/// <summary>Eleitorado de um município: segmentos e centro ideológico.</summary>
public sealed class MunicipalElectorate
{
    public required int CodigoIbge { get; init; }

    public required AxisPoint Center { get; init; }

    public required IReadOnlyList<Segment> Segments { get; init; }

    /// <summary>Força de cada partido no município (somando 1), que a afinidade média reproduz.</summary>
    public required IReadOnlyDictionary<string, double> PartyStrength { get; init; }

    public double Voters => Segments.Sum(s => s.Voters);
}

/// <summary>Monta os segmentos de um município a partir do perfil do Censo (GDD 12.4, DECISOES D-012 e D-013).</summary>
public static class ElectorateBuilder
{
    /// <param name="localStrength">
    /// Força local por partido (cenário Brasil 2026, em qualquer escala); null nos demais cenários, em que a
    /// força é gerada pelo tamanho e pela proximidade ideológica com o Rng.
    /// </param>
    public static MunicipalElectorate Build(
        Municipio municipio,
        SegmentCatalog catalog,
        VoteModelConfig config,
        ThemeCatalog themes,
        IReadOnlyList<ElectoralParty> parties,
        IReadOnlyDictionary<string, int>? localStrength,
        Rng rng)
    {
        var (center, strength) = Strength(parties, config, localStrength, rng);
        var segments = BuildSegments(municipio, catalog, config, themes, center);
        FitAffinity(segments, parties, strength, config);
        return new MunicipalElectorate { CodigoIbge = municipio.CodigoIbge, Center = center, Segments = segments, PartyStrength = strength };
    }

    /// <summary>
    /// Só o centro ideológico e a força dos partidos (primeira etapa de <see cref="Build"/>, com o mesmo uso do Rng),
    /// para quem precisa da força de muitos municípios sem montar os segmentos, como o mapa.
    /// </summary>
    public static (AxisPoint Center, Dictionary<string, double> Strength) Strength(
        IReadOnlyList<ElectoralParty> parties,
        VoteModelConfig config,
        IReadOnlyDictionary<string, int>? localStrength,
        Rng rng)
    {
        Dictionary<string, double> strength;
        AxisPoint center;
        if (localStrength is { Count: > 0 })
        {
            strength = Normalize(parties.ToDictionary(p => p.Id, p => (double)localStrength.GetValueOrDefault(p.Id)));
            center = new AxisPoint(
                parties.Sum(p => strength[p.Id] * p.Position.Economic),
                parties.Sum(p => strength[p.Id] * p.Position.Social));
        }
        else
        {
            center = new AxisPoint(Gaussian(rng) * config.RandomWorldCenterSd, Gaussian(rng) * config.RandomWorldCenterSd);
            strength = Normalize(parties.ToDictionary(
                p => p.Id,
                p => p.SizeWeight
                    * Math.Exp(-center.DistanceTo(AxisPoint.From(p.Position)) / config.AffinityProximityScale)
                    * Math.Exp(Gaussian(rng) * config.RandomWorldStrengthNoiseSd)));
        }

        return (center, strength);
    }

    /// <summary>Fatia de cada grupo: soma das categorias dividida pela soma das categorias do bloco.</summary>
    public static Dictionary<string, double> GroupShares(SegmentDimension dimension, IReadOnlyDictionary<string, long> block)
    {
        var excluded = dimension.ExcludedCategories.ToHashSet(StringComparer.Ordinal);
        double total = block.Where(c => !excluded.Contains(c.Key)).Sum(c => (double)c.Value);
        return dimension.Groups.ToDictionary(
            g => g.Id,
            g => total <= 0 ? 1.0 / dimension.Groups.Count : g.Categories.Sum(c => (double)block.GetValueOrDefault(c)) / total);
    }

    private static List<Segment> BuildSegments(Municipio municipio, SegmentCatalog catalog, VoteModelConfig config, ThemeCatalog themes, AxisPoint center)
    {
        var shares = catalog.Dimensions.Select(d => GroupShares(d, Block(municipio.Perfil, d.ProfileBlock))).ToList();
        var segments = new List<Segment>();

        // Produto cartesiano dos grupos de todas as dimensões (independência entre dimensões, D-012).
        void Visit(int depth, List<SegmentGroup> chosen, double share)
        {
            if (share <= 0)
            {
                return;
            }

            if (depth == catalog.Dimensions.Count)
            {
                segments.Add(MakeSegment(catalog, chosen, share * municipio.Eleitorado, config, themes, center));
                return;
            }

            foreach (SegmentGroup group in catalog.Dimensions[depth].Groups)
            {
                chosen.Add(group);
                Visit(depth + 1, chosen, share * shares[depth][group.Id]);
                chosen.RemoveAt(chosen.Count - 1);
            }
        }

        Visit(0, [], 1.0);
        return segments;
    }

    private static Segment MakeSegment(SegmentCatalog catalog, List<SegmentGroup> groups, double voters, VoteModelConfig config, ThemeCatalog themes, AxisPoint center)
    {
        var ideology = new AxisPoint(
            Math.Clamp(center.Economic + groups.Sum(g => g.Ideology.Economic), AxisRange.AxisMin, AxisRange.AxisMax),
            Math.Clamp(center.Social + groups.Sum(g => g.Ideology.Social), AxisRange.AxisMin, AxisRange.AxisMax));

        var priority = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var (theme, weight) in groups.SelectMany(g => g.Themes))
        {
            priority[theme] = priority.GetValueOrDefault(theme) + weight;
        }

        double prioritySum = priority.Values.Sum();
        bool facultative = groups.Any(g => g.Facultative);
        double turnout = Math.Clamp(config.BaseTurnout + groups.Sum(g => g.Turnout), 0, 1) * (facultative ? config.FacultativeTurnoutFactor : 1);

        return new Segment
        {
            Id = string.Join('.', groups.Select(g => g.Id)),
            Groups = catalog.Dimensions.Zip(groups).ToDictionary(x => x.First.Id, x => x.Second.Id, StringComparer.Ordinal),
            Voters = voters,
            Ideology = ideology,
            ThemePriority = prioritySum <= 0 ? priority : priority.ToDictionary(p => p.Key, p => p.Value / prioritySum, StringComparer.Ordinal),
            ThemePreference = themes.Themes.ToDictionary(t => t.Id, t => ThemePreference(t, ideology, config), StringComparer.Ordinal),
            Turnout = turnout,
            Facultative = facultative,
            Affinity = [],
        };
    }

    /// <summary>Posição preferida no tema a partir do eixo a que ele se liga; temas sem eixo ficam em 0.</summary>
    public static double ThemePreference(ThemeDefinition theme, AxisPoint ideology, VoteModelConfig config)
    {
        double axis = theme.Axis switch
        {
            ThemeAxis.Economic => ideology.Economic,
            ThemeAxis.Social => ideology.Social,
            _ => 0,
        };
        return Math.Clamp(axis / config.ThemeAxisDivisor, ThemeDefinition.MinPosition, ThemeDefinition.MaxPosition);
    }

    /// <summary>
    /// Ajuste proporcional iterativo: cada segmento soma 1 e a média ponderada pelos eleitores reproduz a força
    /// do partido no município, partindo de força × proximidade ideológica.
    /// </summary>
    private static void FitAffinity(List<Segment> segments, IReadOnlyList<ElectoralParty> parties, Dictionary<string, double> strength, VoteModelConfig config)
    {
        double totalVoters = segments.Sum(s => s.Voters);
        foreach (Segment segment in segments)
        {
            foreach (ElectoralParty party in parties)
            {
                double proximity = Math.Exp(-segment.Ideology.DistanceTo(AxisPoint.From(party.Position)) / config.AffinityProximityScale);
                segment.Affinity[party.Id] = strength[party.Id] * proximity;
            }
        }

        for (int iteration = 0; iteration < config.AffinityIpfIterations; iteration++)
        {
            foreach (ElectoralParty party in parties)
            {
                double current = segments.Sum(s => s.Voters * s.Affinity[party.Id]) / totalVoters;
                double factor = current <= 0 ? 0 : strength[party.Id] / current;
                foreach (Segment segment in segments)
                {
                    segment.Affinity[party.Id] *= factor;
                }
            }

            foreach (Segment segment in segments)
            {
                double sum = segment.Affinity.Values.Sum();
                if (sum > 0)
                {
                    foreach (string id in segment.Affinity.Keys.ToList())
                    {
                        segment.Affinity[id] /= sum;
                    }
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, long> Block(PerfilCenso perfil, string block) => block switch
    {
        "situacao" => perfil.Situacao,
        "idade" => perfil.Idade,
        "renda" => perfil.Renda,
        "religiao" => perfil.Religiao,
        "escolaridade" => perfil.Escolaridade,
        _ => throw new ArgumentOutOfRangeException(nameof(block), block, "Bloco do perfil desconhecido."),
    };

    private static Dictionary<string, double> Normalize(Dictionary<string, double> values)
    {
        double sum = values.Values.Sum();
        return sum <= 0
            ? values.ToDictionary(v => v.Key, _ => 1.0 / values.Count, StringComparer.Ordinal)
            : values.ToDictionary(v => v.Key, v => v.Value / sum, StringComparer.Ordinal);
    }

    /// <summary>Normal padrão pelo método de Box–Muller, a partir do Rng.</summary>
    public static double Gaussian(Rng rng)
    {
        double u1 = 1.0 - rng.NextDouble();
        double u2 = rng.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }
}
