using Planalto.Core.Electorate;
using Planalto.Core.State;

namespace Planalto.Core.Territory;

/// <summary>
/// Território da campanha em curso (GDD 17.5): a presença de cada candidato em cada zona (0 a 100), o apoio que o
/// jogador tem por grupo em cada zona e o cansaço de cada trio (tipo de compromisso, zona, público). O
/// reconhecimento na cidade deixa de ser um número próprio: é a média das presenças pesada pela população.
/// </summary>
public sealed class TerritoryState
{
    /// <summary>Ids das zonas, na ordem de data/zonas/ (a mesma dos vetores abaixo).</summary>
    public required List<string> Zones { get; init; }

    /// <summary>População de cada zona, para a média que dá o reconhecimento.</summary>
    public required List<long> Population { get; init; }

    /// <summary>Candidato → presença em cada zona.</summary>
    public Dictionary<string, double[]> Presence { get; init; } = [];

    /// <summary>
    /// Apoio por zona: candidato → (uma entrada por zona) grupo de eleitores → apoio (0 a 1), somado ao apoio da cidade
    /// inteira. É derivado: as lideranças alinhadas e as reuniões o recompõem (ver LeaderEngine.RefreshSupport).
    /// </summary>
    public Dictionary<string, List<Dictionary<string, double>>> Support { get; init; } = [];

    /// <summary>Apoio que o jogador juntou em reuniões, por zona e grupo; perde força a cada semana.</summary>
    public List<Dictionary<string, double>> MeetingSupport { get; init; } = [];

    /// <summary>Zona em que o jogador mora.</summary>
    public int HomeZone { get; init; }

    /// <summary>Redutos de cada rival (índices de zona).</summary>
    public Dictionary<string, List<int>> Redutos { get; init; } = [];

    /// <summary>Zonas em que o candidato teve compromisso nesta semana (não decaem na virada).</summary>
    public Dictionary<string, List<int>> ActiveThisWeek { get; init; } = [];

    public int IndexOf(string zoneId) => Zones.IndexOf(zoneId);

    public double PresenceOf(string candidateId, int zone) => Presence.TryGetValue(candidateId, out double[]? p) ? p[zone] : 0;

    /// <summary>Reconhecimento na cidade: média das presenças pesada pela população das zonas.</summary>
    public double Recognition(string candidateId)
    {
        if (!Presence.TryGetValue(candidateId, out double[]? p))
        {
            return 0;
        }

        double pop = Population.Sum(x => (double)x);
        return pop <= 0 ? p.Average() : p.Select((v, i) => v * Population[i]).Sum() / pop;
    }

    /// <summary>
    /// Espalha um reconhecimento de partida pelas zonas: as zonas fortes (zona natal ou redutos) ficam
    /// <paramref name="factor"/> vezes acima das outras, e a média pela população reproduz o reconhecimento.
    /// </summary>
    public void Seed(string candidateId, double recognition, IReadOnlyCollection<int> strong, double factor)
    {
        double pop = Population.Sum(x => (double)x);
        double strongPop = strong.Sum(i => (double)Population[i]);
        double weightSum = pop <= 0 ? Zones.Count : ((pop - strongPop) + (factor * strongPop)) / pop;
        double baseLevel = weightSum <= 0 ? recognition : recognition / weightSum;
        Presence[candidateId] = Zones.Select((_, i) => Math.Clamp(baseLevel * (strong.Contains(i) ? factor : 1), 0, 100)).ToArray();
    }

    /// <summary>
    /// Ganho de presença numa zona, com retorno decrescente, e o transbordo para as vizinhas (repartido igualmente).
    /// Devolve o que mudou em cada zona tocada.
    /// </summary>
    public List<(int Zone, double Delta)> Gain(string candidateId, int zone, double amount, double spill, IReadOnlyList<int> neighbors)
    {
        double[] p = Presence[candidateId];
        var changes = new List<(int, double)>();
        void Add(int z, double delta)
        {
            double before = p[z];
            p[z] = Campaign.EffectApplier.Gain(before, delta);
            if (Math.Abs(p[z] - before) > 1e-9)
            {
                changes.Add((z, p[z] - before));
            }
        }

        Add(zone, amount);
        if (amount > 0 && neighbors.Count > 0 && spill > 0)
        {
            foreach (int n in neighbors)
            {
                Add(n, amount * spill / neighbors.Count);
            }
        }

        MarkActive(candidateId, zone);
        return changes;
    }

    /// <summary>Mudança igual em todas as zonas (eventos, debate, notícia: o que atinge a cidade inteira).</summary>
    public void GainEverywhere(string candidateId, double amount)
    {
        if (!Presence.TryGetValue(candidateId, out double[]? p))
        {
            return;
        }

        for (int z = 0; z < p.Length; z++)
        {
            p[z] = Campaign.EffectApplier.Gain(p[z], amount);
        }
    }

    public void MarkActive(string candidateId, int zone)
    {
        if (!ActiveThisWeek.TryGetValue(candidateId, out List<int>? zones))
        {
            ActiveThisWeek[candidateId] = zones = [];
        }

        if (!zones.Contains(zone))
        {
            zones.Add(zone);
        }
    }

    /// <summary>Virada da semana: a presença cai nas zonas em que o candidato não esteve, nunca abaixo do piso.</summary>
    public void Decay(string candidateId, double amount, double floor)
    {
        if (!Presence.TryGetValue(candidateId, out double[]? p))
        {
            return;
        }

        List<int>? active = ActiveThisWeek.GetValueOrDefault(candidateId);
        for (int z = 0; z < p.Length; z++)
        {
            if (active is null || !active.Contains(z))
            {
                p[z] = Math.Max(Math.Min(p[z], floor), p[z] - amount);
            }
        }
    }
}

/// <summary>Voto por zona (GDD 17.5): o mesmo modelo de voto, rodado em cada zona com a presença e o apoio dela.</summary>
public static class TerritoryVote
{
    /// <summary>Candidato como a zona o vê: reconhecimento = presença na zona; apoio = o da cidade mais o da zona.</summary>
    public static VoteCandidate InZone(VoteCandidate c, TerritoryState territory, int zone)
    {
        IReadOnlyDictionary<string, double> support = c.Support;
        if (territory.Support.TryGetValue(c.Id, out List<Dictionary<string, double>>? byZone) && zone < byZone.Count && byZone[zone].Count > 0)
        {
            var merged = new Dictionary<string, double>(c.Support, StringComparer.Ordinal);
            foreach (var (group, value) in byZone[zone])
            {
                merged[group] = Math.Clamp(merged.GetValueOrDefault(group) + value, 0, 1);
            }

            support = merged;
        }

        return new VoteCandidate
        {
            Id = c.Id,
            PartyId = c.PartyId,
            Position = c.Position,
            Themes = c.Themes,
            Recognition = territory.Presence.TryGetValue(c.Id, out double[]? p) ? p[zone] / 100 : c.Recognition,
            Image = c.Image,
            Support = support,
            Multiplicity = c.Multiplicity,
        };
    }

    /// <summary>Projeção de uma zona.</summary>
    public static VoteProjection ProjectZone(ZoneElectorate zone, TerritoryState territory, IReadOnlyList<VoteCandidate> candidates, VoteModelConfig config, Ideology.ThemeCatalog themes, IReadOnlyDictionary<string, double> noise) =>
        VoteModel.Project(zone.Segments, candidates.Select(c => InZone(c, territory, zone.Index)).ToList(), config, themes, noise);

    /// <summary>Projeção de cada zona, com o mesmo ruído por candidato em todas, e a da cidade, que é a soma delas.</summary>
    public static (VoteProjection City, IReadOnlyList<VoteProjection> Zones) Project(
        IReadOnlyList<ZoneElectorate> zones, TerritoryState territory, IReadOnlyList<VoteCandidate> candidates, VoteModelConfig config, Ideology.ThemeCatalog themes, Rng? rng)
    {
        Dictionary<string, double> noise = VoteModel.Noise(candidates, config, rng);
        List<VoteProjection> perZone = zones.Select(z => ProjectZone(z, territory, candidates, config, themes, noise)).ToList();
        var votes = candidates.ToDictionary(c => c.Id, c => perZone.Sum(p => p.Votes[c.Id]), StringComparer.Ordinal);
        var city = new VoteProjection
        {
            Segments = perZone.SelectMany(p => p.Segments).ToList(),
            Votes = votes,
            BlankNull = perZone.Sum(p => p.BlankNull),
            Abstention = perZone.Sum(p => p.Abstention),
        };
        return (city, perZone);
    }
}
