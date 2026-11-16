using Planalto.Core.Electorate;
using Planalto.Core.Municipios;

namespace Planalto.Core.Territory;

/// <summary>A maior carência medida da zona, em unidade real, comparada com a cidade (GDD 17.5).</summary>
public sealed record ZoneDemand(ZoneDemandRule Rule, double Value, double CityValue);

/// <summary>Uma zona como o modelo de voto a vê: os mesmos segmentos do município, com os eleitores que moram nela.</summary>
public sealed class ZoneElectorate
{
    public required Zona Zona { get; init; }

    public required int Index { get; init; }

    public required IReadOnlyList<Segment> Segments { get; init; }

    public required ZoneDemand Demand { get; init; }

    public double Voters => Segments.Sum(s => s.Voters);

    /// <summary>Eleitores da zona num grupo (ex.: "evangelica", "idade_18_34").</summary>
    public double VotersIn(string groupId) => Segments.Where(s => s.Has(groupId)).Sum(s => s.Voters);
}

/// <summary>
/// Reparte os segmentos do município pelas zonas (GDD 17.5). Cada zona pesa os segmentos pelo próprio perfil:
/// situação (a zona é toda urbana ou toda rural), idade (setores do Censo), renda (estimativa de jogo pela razão
/// entre a renda da zona e a da cidade) e religião e escolaridade (áreas de ponderação, onde existem). Depois um
/// ajuste proporcional iterativo fecha as duas contas: cada zona soma os seus eleitores e cada segmento soma, nas
/// zonas, exatamente os eleitores que tem no município.
/// </summary>
public static class ZoneElectorateBuilder
{
    private const int Iterations = 12;

    public static IReadOnlyList<ZoneElectorate> Build(MunicipalElectorate electorate, ZonasMunicipio zonas, Municipio municipio, SegmentCatalog catalog, TerritoryConfig config)
    {
        IReadOnlyList<Segment> segments = electorate.Segments;
        IReadOnlyList<Zona> list = zonas.Zonas;
        double total = segments.Sum(s => s.Voters);
        double[] rowTarget = ZoneVoters(list, total);
        double cityIncome = CityIncome(list);

        // Fatia de cada grupo na cidade (entre os eleitores) e em cada zona.
        var city = catalog.Dimensions.ToDictionary(
            d => d.Id,
            d => d.Groups.ToDictionary(g => g.Id, g => total <= 0 ? 0 : segments.Where(s => s.Groups[d.Id] == g.Id).Sum(s => s.Voters) / total, StringComparer.Ordinal),
            StringComparer.Ordinal);

        var w = new double[list.Count][];
        for (int z = 0; z < list.Count; z++)
        {
            var zone = catalog.Dimensions.ToDictionary(d => d.Id, d => ZoneShares(d, list[z], city[d.Id], cityIncome, config), StringComparer.Ordinal);
            w[z] = new double[segments.Count];
            for (int s = 0; s < segments.Count; s++)
            {
                double ratio = 1;
                foreach (var (dimension, group) in segments[s].Groups)
                {
                    double cityShare = city[dimension].GetValueOrDefault(group);
                    ratio *= cityShare <= 0 ? 0 : zone[dimension].GetValueOrDefault(group) / cityShare;
                }

                w[z][s] = segments[s].Voters * ratio * (total <= 0 ? 0 : rowTarget[z] / total);
            }
        }

        for (int i = 0; i < Iterations; i++)
        {
            for (int z = 0; z < list.Count; z++)
            {
                double sum = w[z].Sum();
                if (sum > 0)
                {
                    double f = rowTarget[z] / sum;
                    for (int s = 0; s < segments.Count; s++)
                    {
                        w[z][s] *= f;
                    }
                }
            }

            ScaleColumns(w, segments, rowTarget);
        }

        var result = new List<ZoneElectorate>();
        for (int z = 0; z < list.Count; z++)
        {
            ZoneDemand demand = Demand(list[z], list, cityIncome, config);
            var zoneSegments = new List<Segment>();
            for (int s = 0; s < segments.Count; s++)
            {
                if (w[z][s] > 0)
                {
                    zoneSegments.Add(Copy(segments[s], w[z][s], demand, config));
                }
            }

            result.Add(new ZoneElectorate { Zona = list[z], Index = z, Segments = zoneSegments, Demand = demand });
        }

        return result;
    }

    /// <summary>
    /// Eleitores de cada zona, somando o eleitorado do município: os do importador (locais de votação do TSE), quando
    /// todas as zonas os têm; senão, a repartição pela população de 16 anos ou mais.
    /// </summary>
    public static double[] ZoneVoters(IReadOnlyList<Zona> zonas, double total)
    {
        double[] weights = zonas.All(z => z.Eleitores is not null)
            ? zonas.Select(z => (double)z.Eleitores!.Value).ToArray()
            : zonas.Select(Pop16).ToArray();
        double sum = weights.Sum();
        return weights.Select(x => sum <= 0 ? total / zonas.Count : x / sum * total).ToArray();
    }

    /// <summary>População de 16 anos ou mais pelas faixas dos setores (4/5 da faixa de 15 a 19 anos e as seguintes).</summary>
    public static double Pop16(Zona z)
    {
        double all = z.Perfil.Idade.Values.Sum();
        if (all <= 0)
        {
            return z.Populacao;
        }

        double young = z.Perfil.Idade.GetValueOrDefault("0_4") + z.Perfil.Idade.GetValueOrDefault("5_9") + z.Perfil.Idade.GetValueOrDefault("10_14")
            + (0.2 * z.Perfil.Idade.GetValueOrDefault("15_19"));
        return z.Populacao * (1 - (young / all));
    }

    /// <summary>Renda média do responsável na cidade: média das zonas pesada pelos domicílios (ou pela população).</summary>
    public static double CityIncome(IReadOnlyList<Zona> zonas)
    {
        var with = zonas.Where(z => z.Perfil.RendaResponsavel is > 0).ToList();
        double weight = with.Sum(z => (double)Math.Max(1, z.Domicilios));
        return weight <= 0 ? 0 : with.Sum(z => z.Perfil.RendaResponsavel!.Value * Math.Max(1, z.Domicilios)) / weight;
    }

    private static Dictionary<string, double> ZoneShares(SegmentDimension dimension, Zona zona, Dictionary<string, double> city, double cityIncome, TerritoryConfig config)
    {
        switch (dimension.ProfileBlock)
        {
            case "situacao":
                return dimension.Groups.ToDictionary(g => g.Id, g => g.Categories.Contains(zona.Situacao) ? 1.0 : 0.0, StringComparer.Ordinal);
            case "idade" when zona.Perfil.IdadeFonte == PerfilZona.FonteZona:
                Dictionary<string, double> age = ZoneProjection.AgeShares(zona);
                return age.Values.Sum() > 0 ? age : city;
            case "renda" when zona.Perfil.RendaFonte == PerfilZona.FonteZona && zona.Perfil.RendaResponsavel is > 0 && cityIncome > 0:
                // Estimativa de jogo (GDD 17.5): a distribuição da cidade deslocada pela razão das rendas. O primeiro
                // grupo da dimensão é o de renda mais baixa e o último, o de mais alta.
                double ratio = zona.Perfil.RendaResponsavel!.Value / cityIncome;
                int n = dimension.Groups.Count;
                var shifted = dimension.Groups.Select((g, i) => (g.Id, V: city.GetValueOrDefault(g.Id) * Math.Pow(ratio, config.Simulacao.RendaExpoente * ((2.0 * i / Math.Max(1, n - 1)) - 1)))).ToList();
                double sum = shifted.Sum(x => x.V);
                return sum <= 0 ? city : shifted.ToDictionary(x => x.Id, x => x.V / sum, StringComparer.Ordinal);
            case "religiao" when zona.Perfil.ReligiaoPorCategoria() is { } religiao && religiao.Values.Sum() > 0:
                return ElectorateBuilder.GroupShares(dimension, religiao);
            case "escolaridade" when zona.Perfil.EscolaridadePorCategoria() is { } escolaridade && escolaridade.Values.Sum() > 0:
                return ElectorateBuilder.GroupShares(dimension, escolaridade);
            default:
                return city;
        }
    }

    /// <summary>Cada segmento soma, nas zonas, os eleitores que tem no município; coluna sem zona possível segue os eleitores das zonas.</summary>
    private static void ScaleColumns(double[][] w, IReadOnlyList<Segment> segments, double[] rowTarget)
    {
        double rows = rowTarget.Sum();
        for (int s = 0; s < segments.Count; s++)
        {
            double sum = 0;
            for (int z = 0; z < w.Length; z++)
            {
                sum += w[z][s];
            }

            for (int z = 0; z < w.Length; z++)
            {
                w[z][s] = sum > 0 ? w[z][s] * segments[s].Voters / sum : rows <= 0 ? 0 : segments[s].Voters * rowTarget[z] / rows;
            }
        }
    }

    private static Segment Copy(Segment s, double voters, ZoneDemand demand, TerritoryConfig config)
    {
        var priority = s.ThemePriority.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        priority[demand.Rule.Tema] = priority.GetValueOrDefault(demand.Rule.Tema) + config.Simulacao.DemandaPeso;
        double sum = priority.Values.Sum();
        return new Segment
        {
            Id = s.Id,
            Groups = s.Groups,
            Voters = voters,
            Ideology = s.Ideology,
            ThemePriority = priority.ToDictionary(p => p.Key, p => p.Value / sum, StringComparer.Ordinal),
            ThemePreference = s.ThemePreference,
            Turnout = s.Turnout,
            Facultative = s.Facultative,
            Affinity = s.Affinity,
        };
    }

    /// <summary>A demanda da zona: a regra que mais se destaca em relação à cidade; sem destaque, a padrão.</summary>
    public static ZoneDemand Demand(Zona zona, IReadOnlyList<Zona> all, double cityIncome, TerritoryConfig config)
    {
        TerritorySimulation sim = config.Simulacao;
        double cityPop = all.Sum(z => (double)z.Populacao);
        double cityArea = all.Sum(z => z.AreaKm2);
        double density = zona.AreaKm2 <= 0 ? 0 : zona.Populacao / zona.AreaKm2;
        double cityDensity = cityArea <= 0 ? 0 : cityPop / cityArea;
        ZoneDemandRule? Rule(string regra) => sim.Demandas.FirstOrDefault(d => d.Regra == regra);

        var candidates = new List<(double Score, ZoneDemand Demand)>();
        if (Rule(ZoneDemandRule.RendaBaixa) is { } renda && zona.Perfil.RendaFonte == PerfilZona.FonteZona && zona.Perfil.RendaResponsavel is > 0 && cityIncome > 0)
        {
            double ratio = zona.Perfil.RendaResponsavel!.Value / cityIncome;
            if (ratio <= sim.RendaBaixaRazao)
            {
                candidates.Add(((sim.RendaBaixaRazao - ratio) * 100, new ZoneDemand(renda, zona.Perfil.RendaResponsavel.Value, cityIncome)));
            }
        }

        if (zona.Perfil.IdadeFonte == PerfilZona.FonteZona && zona.Perfil.Idade.Values.Sum() > 0)
        {
            double Share(Zona z, params string[] keys) => z.Perfil.Idade.Values.Sum() <= 0 ? 0 : 100.0 * keys.Sum(k => z.Perfil.Idade.GetValueOrDefault(k)) / z.Perfil.Idade.Values.Sum();
            double CityShare(params string[] keys)
            {
                var own = all.Where(z => z.Perfil.IdadeFonte == PerfilZona.FonteZona).ToList();
                double sum = own.Sum(z => (double)z.Perfil.Idade.Values.Sum());
                return sum <= 0 ? 0 : 100.0 * own.Sum(z => keys.Sum(k => (double)z.Perfil.Idade.GetValueOrDefault(k))) / sum;
            }

            string[] old = ["60_69", "70_mais"];
            string[] young = ["0_4", "5_9", "10_14", "15_19"];
            if (Rule(ZoneDemandRule.Idosos) is { } idosos && Share(zona, old) - CityShare(old) >= sim.IdadeDestaque)
            {
                candidates.Add((Share(zona, old) - CityShare(old), new ZoneDemand(idosos, Share(zona, old), CityShare(old))));
            }

            if (Rule(ZoneDemandRule.Jovens) is { } jovens && Share(zona, young) - CityShare(young) >= sim.IdadeDestaque)
            {
                candidates.Add((Share(zona, young) - CityShare(young), new ZoneDemand(jovens, Share(zona, young), CityShare(young))));
            }
        }

        if (candidates.Count > 0)
        {
            return candidates.MaxBy(c => c.Score).Demand;
        }

        if (zona.IsRural && Rule(ZoneDemandRule.Rural) is { } rural)
        {
            return new ZoneDemand(rural, density, cityDensity);
        }

        return new ZoneDemand(sim.Demandas.First(d => d.Regra == ZoneDemandRule.Padrao), density, cityDensity);
    }
}
