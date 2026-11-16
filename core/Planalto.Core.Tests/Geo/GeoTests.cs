using System.Diagnostics;
using Planalto.Core.Campaign;
using Planalto.Core.Geo;
using Planalto.Core.Municipios;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;
using Xunit;

namespace Planalto.Core.Tests.Geo;

public class GeoTests
{
    /// <summary>Quadrado 4×4 (em unidades de 1/scale grau) com furo 2×2 no meio, triangulado à mão.</summary>
    private static GeoPolygon SquareWithHole() => new()
    {
        Outer = [0, 0, 4, 0, 4, 4, 0, 4],
        Holes = [[1, 1, 1, 3, 3, 3, 3, 1]],
        Triangles = [0, 1, 4, 1, 7, 4, 1, 2, 7, 2, 6, 7, 2, 3, 6, 3, 5, 6, 3, 0, 5, 0, 4, 5],
    };

    [Fact]
    public void Area_desconta_furos_e_triangulos_cobrem_o_poligono()
    {
        GeoPolygon p = SquareWithHole();
        Assert.Equal(8, p.VertexCount);
        Assert.Equal(12.0, p.Area(1), 9);
        Assert.Equal(12.0, p.TriangleArea(1), 9);
        Assert.Equal((3.0, 3.0), p.Vertex(6, 1));
    }

    [Fact]
    public void Validacao_aponta_indice_fora_e_municipio_faltando()
    {
        Municipio acre = CampaignKit.C.Municipios.All.First(m => m.Uf == "AC");
        GeoPolygon broken = SquareWithHole();
        broken.Triangles[0] = 99;
        var geo = new GeoUf
        {
            Uf = "AC",
            Source = "teste",
            Method = "teste",
            Scale = 1,
            Municipios = [new GeoMunicipio { CodigoIbge = acre.CodigoIbge, Polygons = [broken] }],
        };

        var errors = GeoContent.Validate(geo, CampaignKit.C.Municipios, "AC").ToList();
        Assert.Contains(errors, e => e.Contains("triângulos inválidos"));
        Assert.Contains(errors, e => e.Contains("falta o município"));
    }

    [Fact]
    public void Contornos_de_todas_as_UFs_cobrem_todos_os_municipios()
    {
        foreach (string uf in CampaignKit.C.Municipios.All.Select(m => m.Uf).Distinct())
        {
            GeoUf geo = GeoContent.Load(TestContent.DataDirectory, uf).GetValueOrThrow();
            Assert.Empty(GeoContent.Validate(geo, CampaignKit.C.Municipios, uf));
        }
    }

    [Fact]
    public void Projecao_preserva_proporcao_e_centra_a_UF()
    {
        GeoUf sp = GeoContent.Load(TestContent.DataDirectory, "SP").GetValueOrThrow();
        GeoProjection projection = GeoProjection.For(sp);

        // O retângulo envolvente de São Paulo tem cerca de 8,9° de longitude (≈ 920 km) e 5,5° de latitude (≈ 610 km).
        Assert.InRange(projection.WidthKm, 850, 1000);
        Assert.InRange(projection.HeightKm, 550, 680);
        var (x, y) = projection.Project(projection.CenterLon, projection.CenterLat);
        Assert.Equal(0, x, 9);
        Assert.Equal(0, y, 9);

        // Um grau de latitude vale o mesmo em qualquer longitude; um de longitude encolhe com o cosseno.
        var (_, north) = projection.Project(projection.CenterLon, projection.CenterLat + 1);
        var (east, _) = projection.Project(projection.CenterLon + 1, projection.CenterLat);
        Assert.Equal(GeoProjection.KmPerDegree, north, 6);
        Assert.True(east < north);
    }

    [Fact]
    public void Mapa_eleitoral_usa_a_mesma_forca_da_simulacao_detalhada_e_e_rapido()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3550308, 11);
        var state = engine.Context.State;
        var municipios = CampaignKit.C.Municipios.All.Where(m => m.Uf == "SP").ToList();

        var watch = Stopwatch.StartNew();
        var cells = ElectoralMap.Cells(state, municipios, CampaignKit.C.Electorate, CampaignKit.C.World.Parties.Generator.SizeShareWeights, CampaignKit.C.Brasil2026.Municipio);
        watch.Stop();

        Assert.Equal(645, cells.Count);
        Assert.True(watch.ElapsedMilliseconds < 2000, $"{watch.ElapsedMilliseconds} ms para 645 municípios");

        MapCell capital = cells.Single(c => c.CodigoIbge == 3550308);
        var strength = engine.Context.Electorate.PartyStrength;
        var leader = strength.MaxBy(s => s.Value);
        Assert.Equal(leader.Key, capital.LeaderPartyId);
        Assert.Equal(leader.Value, capital.LeaderShare, 9);
        Assert.Equal(strength.GetValueOrDefault(state.Player.PartyId!), capital.ShareOf(state.Player.PartyId), 9);
        Assert.All(cells, c => Assert.InRange(c.Margin, 0, c.LeaderShare));
    }

    [Fact]
    public void Relevo_usa_o_percentil_e_cai_para_o_maximo_quando_o_partido_quase_nao_existe()
    {
        // 20 municípios: força de "a" de 0,01 a 0,20; "b" só existe em um.
        var cells = Enumerable.Range(1, 20)
            .Select(i => new MapCell(i, new Dictionary<string, double> { ["a"] = i / 100.0, ["b"] = i == 7 ? 0.3 : 0, ["c"] = 0.5 }))
            .ToList();

        var relief = ElectoralMap.Relief(cells, "a", 0.9);
        Assert.Equal(1, relief[20], 9);
        Assert.Equal(1, relief[19], 9);
        Assert.Equal(0.1 / 0.18, relief[10], 9);

        var rare = ElectoralMap.Relief(cells, "b", 0.9);
        Assert.Equal(1, rare[7], 9);
        Assert.Equal(0, rare[1], 9);

        Assert.All(ElectoralMap.Relief(cells, null, 0.9).Values, v => Assert.Equal(0, v));
        Assert.Equal("c", cells[0].LeaderPartyId);
        Assert.Equal(0.5 - 0.01, cells[0].Margin, 9);
    }
}
