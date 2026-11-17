using Planalto.Core.Campaign;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;

namespace Planalto.Core.Tests.State;

/// <summary>Painel de zonas (parte RL): uma linha por zona com os números do Core.</summary>
public class ZoneBoardTests
{
    private const int Mantena = 3139607;

    private static TimeConfig Time => CampaignKit.C.Time;

    private static CampaignEngine Career(ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(Mantena, seed, clock: true);
        engine.Context.State.Campaign!.Events.Pending.Clear();
        engine.Context.State.Player.Resources.Money = 50_000;
        return engine;
    }

    [Fact]
    public void Painel_de_zonas_traz_lider_posicao_visita_e_cansaco_de_cada_zona()
    {
        CampaignEngine engine = Career(46);
        IReadOnlyList<ZoneRow> rows = engine.ZoneBoard();
        Assert.Equal(engine.Territory.Zones, rows.Select(r => r.ZoneId));
        var (_, zones) = engine.ProjectByZone(engine.Context.State.Campaign!.Player.Office, noise: null);
        var named = engine.Context.State.Campaign.ForOffice(engine.Context.State.Campaign.Player.Office).Where(c => !c.IsBulk).Select(c => c.Id).ToHashSet();
        foreach (ZoneRow row in rows)
        {
            var ranking = zones[row.Index].Votes.Where(v => named.Contains(v.Key)).OrderByDescending(v => v.Value).ThenBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Key).ToList();
            Assert.Equal(ranking[0], row.LeaderId);
            Assert.Equal(ranking.IndexOf(RaceSetup.PlayerId) + 1, row.PlayerRank);
            Assert.Equal(ranking.Count, row.Ranked);
            Assert.Equal(engine.Territory.PresenceOf(RaceSetup.PlayerId, row.Index), row.Presence);
            Assert.Equal(engine.City.Leaders.Count(l => l.Known && l.Zones.Contains(row.ZoneId)), row.KnownLeaders);
            Assert.Null(row.DaysSinceVisit);
            Assert.Empty(row.Fatigue);
            Assert.False(string.IsNullOrWhiteSpace(row.Demand.Rule.Nome));
        }

        // Uma caminhada: a zona ganha a data da visita e o cansaço do público geral.
        string zone = engine.Territory.Zones[engine.Territory.HomeZone];
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha)).Failure);
        DateOnly day = engine.Today;
        engine.AdvanceDay();
        engine.Context.State.Campaign.Events.Pending.Clear();
        engine.AdvanceDay();
        ZoneRow visited = engine.ZoneBoard().Single(r => r.ZoneId == zone);
        Assert.Equal(day, visited.LastVisit);
        Assert.Equal(2, visited.DaysSinceVisit);
        ZoneFatigue fatigue = Assert.Single(visited.Fatigue);
        Assert.Equal(CommitmentType.PublicoGeral, fatigue.Publico);
        Assert.Equal("caminhada", fatigue.TypeId);
        Assert.Equal(engine.FatigueYield(AgendaState.FatigueKey("caminhada", zone, CommitmentType.PublicoGeral)), fatigue.Yield, 6);
        Assert.True(fatigue.Yield < 1);
    }

    [Fact]
    public void Save_guarda_a_ultima_visita_de_cada_zona()
    {
        CampaignEngine engine = Career(48);
        string zone = engine.Territory.Zones[engine.Territory.HomeZone];
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha)).Failure);
        DateOnly day = engine.Today;
        engine.AdvanceDay();
        GameState loaded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(engine.Context.State));
        Assert.Equal(day, loaded.Campaign!.Agenda.LastVisit[zone]);
    }
}
