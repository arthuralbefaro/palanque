using Planalto.Core.Electorate;
using Planalto.Core.Territory;

namespace Planalto.Core.Campaign;

/// <summary>Cansaço de um público numa zona: quanto renderia hoje o próximo compromisso igual (o pior tipo dele).</summary>
public sealed record ZoneFatigue(string Publico, string TypeId, double Yield);

/// <summary>
/// Uma linha do painel de zonas (RL): presença, líder da intenção de voto e a posição do jogador na zona, lideranças
/// conhecidas, demanda principal, última visita e cansaço por público.
/// </summary>
public sealed record ZoneRow(
    string ZoneId,
    string Name,
    int Index,
    double Voters,
    double Presence,
    bool Home,
    string? LeaderId,
    double LeaderShare,
    int PlayerRank,
    int Ranked,
    double PlayerShare,
    int KnownLeaders,
    int AlliedLeaders,
    int UnknownLeaders,
    ZoneDemand Demand,
    DateOnly? LastVisit,
    int? DaysSinceVisit,
    IReadOnlyList<ZoneFatigue> Fatigue)
{
    /// <summary>Rendimento do compromisso mais cansado na zona (1 = nenhum cansaço).</summary>
    public double WorstYield => Fatigue.Count == 0 ? 1 : Fatigue.Min(f => f.Yield);
}

/// <summary>Painel de zonas (parte RL): só lê o estado.</summary>
public sealed partial class CampaignEngine
{
    /// <summary>
    /// Painel de zonas (RL): uma linha por zona, na ordem de data/zonas/. O líder e a posição saem da projeção de voto
    /// zona a zona sem ruído (a mesma do modo Partido líder), só entre os candidatos nomeados da disputa do jogador.
    /// </summary>
    public IReadOnlyList<ZoneRow> ZoneBoard()
    {
        var named = Campaign.ForOffice(Campaign.Player.Office).Where(c => !c.IsBulk).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<VoteProjection> zones = ProjectByZone(Campaign.Player.Office, noise: null).Zones;
        var rows = new List<ZoneRow>();
        foreach (ZoneElectorate electorate in Context.ZoneElectorates)
        {
            int z = electorate.Index;
            string id = electorate.Zona.Id;
            VoteProjection? projection = z < zones.Count ? zones[z] : null;
            double total = projection?.Votes.Values.Sum() ?? 0;
            List<KeyValuePair<string, double>> ranking = projection is null ? [] : projection.Votes.Where(v => named.Contains(v.Key))
                .OrderByDescending(v => v.Value).ThenBy(v => v.Key, StringComparer.Ordinal).ToList();
            int rank = ranking.FindIndex(v => v.Key == RaceSetup.PlayerId) + 1;
            var leaders = City.Leaders.Where(l => l.Zones.Contains(id)).ToList();
            DateOnly? visit = Agenda.LastVisit.TryGetValue(id, out DateOnly last) ? last
                : Agenda.Log.LastOrDefault(r => !r.Commitment.Delegado && r.Commitment.ZoneId == id)?.Commitment.Date;
            var fatigue = Agenda.Fatigue
                .Select(f => (Parts: f.Key.Split('|'), f.Value))
                .Where(f => f.Parts.Length == 3 && f.Parts[1] == id)
                .GroupBy(f => f.Parts[2], StringComparer.Ordinal)
                .Select(g => g.MaxBy(f => f.Value))
                .Select(f => new ZoneFatigue(f.Parts[2], f.Parts[0], 1 / (1 + (Rules.CansacoFator * f.Value))))
                .OrderBy(f => f.Yield).ThenBy(f => f.Publico, StringComparer.Ordinal)
                .ToList();
            rows.Add(new ZoneRow(
                id,
                electorate.Zona.Nome,
                z,
                electorate.Voters,
                Territory.PresenceOf(RaceSetup.PlayerId, z),
                z == Territory.HomeZone,
                ranking.Count > 0 ? ranking[0].Key : null,
                ranking.Count > 0 && total > 0 ? ranking[0].Value / total : 0,
                rank,
                ranking.Count,
                total > 0 && projection!.Votes.TryGetValue(RaceSetup.PlayerId, out double mine) ? mine / total : 0,
                leaders.Count(l => l.Known),
                leaders.Count(l => l.Known && l.AlignedWith == RaceSetup.PlayerId),
                leaders.Count(l => !l.Known),
                electorate.Demand,
                visit,
                visit is { } v ? Today.DayNumber - v.DayNumber : null,
                fatigue));
        }

        return rows;
    }
}
