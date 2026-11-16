using Planalto.Core.Campaign;
using Planalto.Core.Electorate;
using Planalto.Core.Municipios;
using Planalto.Core.Parties;
using Planalto.Core.Scenarios;
using Planalto.Core.State;

namespace Planalto.Core.Geo;

/// <summary>Uma célula do mapa: força de cada partido, partido mais forte e margem sobre o segundo.</summary>
public sealed class MapCell
{
    public MapCell(int codigoIbge, IReadOnlyDictionary<string, double> strength)
    {
        CodigoIbge = codigoIbge;
        Strength = strength;
        var ranked = strength.OrderByDescending(s => s.Value).ThenBy(s => s.Key, StringComparer.Ordinal).Take(2).ToList();
        LeaderPartyId = ranked[0].Key;
        LeaderShare = ranked[0].Value;
        Margin = ranked[0].Value - (ranked.Count > 1 ? ranked[1].Value : 0);
    }

    public int CodigoIbge { get; }

    public IReadOnlyDictionary<string, double> Strength { get; }

    public string LeaderPartyId { get; }

    public double LeaderShare { get; }

    public double Margin { get; }

    public double ShareOf(string? partyId) => partyId is null ? 0 : Strength.GetValueOrDefault(partyId);
}

/// <summary>
/// Camadas do mapa eleitoral (GDD 13.1): altura pela força de um partido (o do jogador, por padrão), cor pelo
/// partido mais forte com intensidade pela margem. Mesma força que a simulação detalhada usaria em cada município.
/// </summary>
public static class ElectoralMap
{
    public static List<MapCell> Cells(
        GameState state,
        IEnumerable<Municipio> municipios,
        ElectorateContent electorate,
        IReadOnlyDictionary<PartySize, int> sizeWeights,
        Func<int, MunicipalScenario?> local)
    {
        var parties = CampaignContext.ElectoralParties(state, sizeWeights);
        return municipios
            .Select(m => new MapCell(m.CodigoIbge, CampaignContext.PartyStrength(state, m.CodigoIbge, electorate, parties, local(m.CodigoIbge))))
            .ToList();
    }

    /// <summary>
    /// Altura relativa (0 a 1) de cada célula pela força do partido: o topo da escala é o percentil pedido, não o
    /// máximo, porque a força costuma se concentrar em poucos municípios.
    /// </summary>
    public static Dictionary<int, double> Relief(IReadOnlyCollection<MapCell> cells, string? partyId, double percentile)
    {
        var shares = cells.Select(c => c.ShareOf(partyId)).Order().ToList();
        double top = shares.Count == 0 ? 0 : shares[(int)Math.Floor((shares.Count - 1) * percentile)];
        if (top <= 0)
        {
            // Partido ausente em mais municípios do que o percentil cobre: a escala passa a ser o máximo.
            top = shares.Count == 0 ? 0 : shares[^1];
        }

        return cells.ToDictionary(c => c.CodigoIbge, c => top <= 0 ? 0 : Math.Min(1, c.ShareOf(partyId) / top));
    }
}
