using Planalto.Core.Electorate;

namespace Planalto.Core.Territory;

/// <summary>
/// Intenção de voto estimada por zona (R1, antes do voto por zona da R2a): a intenção de cada segmento do município
/// é repesada pelo perfil da zona nas dimensões que o Censo publica por setor, situação (urbana ou rural) e idade.
/// As demais dimensões (renda, religião, escolaridade) ficam com a mistura do município. É uma estimativa de jogo;
/// a tela diz isso no tooltip.
/// </summary>
public static class ZoneProjection
{
    /// <summary>Faixas publicadas por setor → grupos de idade dos segmentos, com a fração de cada faixa que cai no grupo.</summary>
    public static readonly IReadOnlyDictionary<string, (string Faixa, double Fracao)[]> AgeMap = new Dictionary<string, (string, double)[]>
    {
        // 15 a 19: 16 e 17 são 2 dos 5 anos; 18 e 19, outros 2. Distribuição uniforme dentro da faixa.
        ["idade_16_17"] = [("15_19", 0.4)],
        ["idade_18_34"] = [("15_19", 0.4), ("20_24", 1), ("25_29", 1), ("30_39", 0.5)],
        ["idade_35_59"] = [("30_39", 0.5), ("40_49", 1), ("50_59", 1)],
        ["idade_60_69"] = [("60_69", 1)],
        ["idade_70_mais"] = [("70_mais", 1)],
    };

    /// <summary>Fração de cada grupo de idade entre os eleitores (16 anos ou mais) da zona.</summary>
    public static Dictionary<string, double> AgeShares(Zona zona)
    {
        var raw = AgeMap.ToDictionary(g => g.Key, g => g.Value.Sum(f => zona.Perfil.Idade.GetValueOrDefault(f.Faixa) * f.Fracao));
        double total = raw.Values.Sum();
        return raw.ToDictionary(r => r.Key, r => total <= 0 ? 0 : r.Value / total);
    }

    /// <summary>Fração de cada candidato entre os votos válidos na zona.</summary>
    public static Dictionary<string, double> Shares(VoteProjection projection, Zona zona, string ageDimension = "idade", string zoneDimension = "zona")
    {
        // Peso do município em cada grupo de idade (entre os eleitores), para repesar só o que a zona tem diferente.
        var cityAge = new Dictionary<string, double>(StringComparer.Ordinal);
        double cityTotal = 0;
        foreach (SegmentIntention s in projection.Segments)
        {
            string age = s.Segment.Groups.GetValueOrDefault(ageDimension, "");
            cityAge[age] = cityAge.GetValueOrDefault(age) + s.Segment.Voters;
            cityTotal += s.Segment.Voters;
        }

        Dictionary<string, double> zoneAge = AgeShares(zona);
        bool useAge = zoneAge.Values.Sum() > 0;
        var votes = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (SegmentIntention s in projection.Segments)
        {
            if (s.Segment.Groups.GetValueOrDefault(zoneDimension) != zona.Situacao)
            {
                continue;
            }

            string age = s.Segment.Groups.GetValueOrDefault(ageDimension, "");
            double ageWeight = useAge && cityAge.GetValueOrDefault(age) > 0 ? zoneAge.GetValueOrDefault(age) / (cityAge[age] / cityTotal) : 1;
            double voters = s.Segment.Voters * ageWeight * s.Turnout * (1 - s.BlankNull);
            foreach (var (id, share) in s.Shares)
            {
                votes[id] = votes.GetValueOrDefault(id) + (voters * share);
            }
        }

        double sum = votes.Values.Sum();
        return votes.ToDictionary(v => v.Key, v => sum <= 0 ? 0 : v.Value / sum, StringComparer.Ordinal);
    }
}
