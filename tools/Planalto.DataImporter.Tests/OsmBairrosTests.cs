using NetTopologySuite.Geometries;
using Planalto.Core.Territory;
using Planalto.DataImporter.Geo;

namespace Planalto.DataImporter.Tests;

/// <summary>Nomes de bairro do OpenStreetMap (D-058): leitura, filtro, ponto no polígono e escolha por zona, sem rede.</summary>
public class OsmBairrosTests
{
    private static readonly OsmBairrosConfig Config = new()
    {
        Lugares = new Dictionary<string, int> { ["suburb"] = 0, ["quarter"] = 1, ["neighbourhood"] = 2 },
        AdminLevel = "10",
        AdminLevelPrioridade = 0,
        ZonasRurais = false,
        ComprimentoMaximo = 60,
        PrefixosExcluidos = ["rua ", "avenida ", "av ", "quadra ", "edificio "],
        NomesExcluidos = ["zona rural", "bairro"],
        PrefixosRemovidos = ["bairro"],
        SinaisExcluidos = [":", "(", ")"],
        Minusculas = ["de", "da", "das", "do", "dos", "e"],
    };

    private static OsmLugar L(string nome, double lon, double lat, int prioridade = 0, long id = 1, string tipo = "node") =>
        new(tipo, id, nome, "place=suburb", prioridade, lon, lat);

    private static ZonaOsm Z(int key, long pop, IReadOnlyList<OsmLugar> lugares, bool elegivel = true, string? gerado = "Zona Norte", params SetorPonto[] setores) => new()
    {
        Key = key,
        Pop = pop,
        X = setores.Length > 0 ? setores.Average(s => s.X) : 0,
        Y = setores.Length > 0 ? setores.Average(s => s.Y) : 0,
        Elegivel = elegivel,
        NomeGerado = gerado,
        Setores = setores.Length > 0 ? setores : [new SetorPonto(0, 0, pop)],
        Lugares = lugares,
    };

    [Fact]
    public void Leitura_pega_os_tipos_de_bairro_com_nome_e_ponto()
    {
        const string Json = """
        {
          "osm3s": { "timestamp_osm_base": "2026-11-17T09:21:05Z" },
          "elements": [
            { "type": "node", "id": 5, "lat": -18.1, "lon": -40.1, "tags": { "name": "Centro", "place": "neighbourhood" } },
            { "type": "node", "id": 3, "lat": -18.2, "lon": -40.2, "tags": { "name": "Vila Nova", "place": "suburb" } },
            { "type": "node", "id": 4, "lat": -18.2, "lon": -40.2, "tags": { "name": "Mantena", "place": "town" } },
            { "type": "node", "id": 6, "lat": -18.2, "lon": -40.2, "tags": { "place": "suburb" } },
            { "type": "way", "id": 7, "center": { "lat": -18.3, "lon": -40.3 }, "tags": { "name": "Jardim", "place": "quarter" } },
            { "type": "way", "id": 8, "tags": { "name": "Sem centro", "place": "quarter" } },
            { "type": "relation", "id": 9, "center": { "lat": -18.4, "lon": -40.4 }, "tags": { "name": "São José", "boundary": "administrative", "admin_level": "10" } },
            { "type": "relation", "id": 10, "center": { "lat": -18.4, "lon": -40.4 }, "tags": { "name": "Distrito", "boundary": "administrative", "admin_level": "9" } }
          ]
        }
        """;

        List<OsmLugar> lugares = OsmBairros.Parse(Json, Config);

        Assert.Equal(["node:3", "node:5", "relation:9", "way:7"], lugares.Select(l => $"{l.Tipo}:{l.Id}"));
        Assert.Equal([0, 2, 0, 1], lugares.Select(l => l.Prioridade));
        Assert.Equal(-40.3, lugares.Single(l => l.Id == 7).Lon);
        Assert.Equal("2026-11-17T09:21:05Z", OsmBairros.BaseTimestamp(Json));
    }

    [Fact]
    public void Resposta_com_erro_de_execucao_nao_vai_para_o_cache()
    {
        string dir = Directory.CreateTempSubdirectory("osm").FullName;
        try
        {
            string bom = Path.Combine(dir, "bom.json");
            string erro = Path.Combine(dir, "erro.json");
            string html = Path.Combine(dir, "html.json");
            File.WriteAllText(bom, """{ "elements": [] }""");
            File.WriteAllText(erro, """{ "elements": [], "remark": "runtime error: Query timed out in \"query\" at line 1 after 900 seconds." }""");
            File.WriteAllText(html, "<html>at capacity</html>");

            Assert.Null(OsmBairros.ContentError(bom));
            Assert.NotNull(OsmBairros.ContentError(erro));
            Assert.NotNull(OsmBairros.ContentError(html));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("123", null)]
    [InlineData("12-B", "12-B")]
    [InlineData("Rua das Flores", null)]
    [InlineData("Av. Brasil", null)]
    [InlineData("Quadra 5", null)]
    [InlineData("Edifício Central", null)]
    [InlineData("ZONA RURAL", null)]
    [InlineData("Bairro", null)]
    [InlineData("Mantena", null)]
    [InlineData("MANTÉNA", null)]
    [InlineData("Ruas Altas", "Ruas Altas")]
    [InlineData("  Jardim   América ", "Jardim América")]
    [InlineData("Bairro São José", "São José")]
    [InlineData("BAIRRO CENTRO", "Centro")]
    [InlineData("Bairro Mantena", null)]
    [InlineData("Vila Nova (casas populares)", null)]
    [InlineData("CEP: 47.604-790", null)]
    [InlineData("JARDIM DAS FLORES II", "Jardim das Flores II")]
    [InlineData("SÃO JOÃO", "São João")]
    [InlineData("BNH", "BNH")]
    public void Filtro_tira_nome_vazio_numerico_e_que_nao_e_bairro(string nome, string? esperado)
    {
        Assert.Equal(esperado, OsmBairros.Filtrar(nome, "Mantena", Config));
    }

    [Fact]
    public void Filtro_tira_nome_longo_demais()
    {
        Assert.Null(OsmBairros.Filtrar(new string('a', 61), "Mantena", Config));
        Assert.NotNull(OsmBairros.Filtrar(new string('a', 60), "Mantena", Config));
    }

    [Fact]
    public void Ponto_no_poligono_acha_o_setor_e_na_divisa_fica_o_de_menor_codigo()
    {
        var f = new GeometryFactory();
        Polygon Square(double x0, double y0, double x1, double y1) =>
            f.CreatePolygon([new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1), new(x0, y0)]);
        var setores = new List<(string, Geometry)> { ("B", Square(1, 0, 2, 1)), ("A", Square(0, 0, 1, 1)) };

        var result = OsmBairros.Localizar([L("Leste", 1.5, 0.5, id: 1), L("Oeste", 0.5, 0.5, id: 2), L("Divisa", 1, 0.5, id: 3), L("Fora", 5, 5, id: 4)], setores);

        Assert.Equal(["Oeste", "Divisa"], result["A"].Select(l => l.Nome));
        Assert.Equal(["Leste"], result["B"].Select(l => l.Nome));
        Assert.DoesNotContain(result.Values.SelectMany(v => v), l => l.Nome == "Fora");
    }

    [Fact]
    public void Zona_com_bairro_do_IBGE_nao_recebe_nome_e_nome_do_IBGE_nao_se_repete()
    {
        var ibge = Z(1, 5000, [L("Vila Nova", 0, 0)], elegivel: false, gerado: null);
        var gerada = Z(2, 3000, [L("São Pedro", 0, 0, id: 2), L("Jardim Floresta", 0.01, 0, id: 3)], setores: [new SetorPonto(0, 0, 2000), new SetorPonto(0.01, 0, 1000)]);

        var escolhidos = OsmBairros.Escolher([ibge, gerada], reservados: ["SAO PEDRO"], xScale: 1);

        Assert.False(escolhidos.ContainsKey(1));
        Assert.Equal("Jardim Floresta", escolhidos[2]);
    }

    [Fact]
    public void Com_varios_na_zona_vale_o_tipo_de_maior_prioridade_e_depois_o_de_mais_moradores_perto()
    {
        // Dois bairros (suburb) e um sub-bairro (neighbourhood) ao lado do setor mais populoso.
        var lugares = new[]
        {
            L("Vizinho Grande", 0.0, 0, prioridade: 2, id: 1),
            L("Alto", 0.1, 0, prioridade: 0, id: 2),
            L("Baixo", 0.9, 0, prioridade: 0, id: 3),
        };
        var zona = Z(1, 1000, lugares, setores: [new SetorPonto(0, 0, 700), new SetorPonto(1, 0, 300)]);

        Assert.Equal("Alto", OsmBairros.Escolher([zona], [], 1)[1]);

        // No mesmo tipo, quem tem mais moradores perto ganha, mesmo longe do centro da zona.
        var outra = Z(1, 1000, [L("Alto", 0.1, 0, id: 2), L("Baixo", 0.9, 0, id: 3)], setores: [new SetorPonto(0, 0, 100), new SetorPonto(1, 0, 900)]);
        Assert.Equal("Baixo", OsmBairros.Escolher([outra], [], 1)[1]);
    }

    [Fact]
    public void Mesmo_nome_nao_vai_para_duas_zonas_e_a_mais_populosa_escolhe_primeiro()
    {
        var grande = Z(1, 9000, [L("Santa Rita", 0, 0, id: 1)], gerado: "Centro");
        var pequena = Z(2, 2000, [L("santa rita", 1, 1, id: 2), L("Boa Vista", 1, 1.01, id: 3)], gerado: "Zona Norte", setores: [new SetorPonto(1, 1, 2000)]);
        var sozinha = Z(3, 1000, [L("Santa Rita", 2, 2, id: 4)], gerado: "Zona Sul");

        var escolhidos = OsmBairros.Escolher([sozinha, pequena, grande], [], 1);

        Assert.Equal("Santa Rita", escolhidos[1]);
        Assert.Equal("Boa Vista", escolhidos[2]);
        Assert.False(escolhidos.ContainsKey(3));
    }

    [Fact]
    public void Nome_gerado_de_outra_zona_nao_e_usado()
    {
        var a = Z(1, 5000, [L("Centro", 0, 0)], gerado: "Zona Norte");
        var centro = Z(2, 4000, [], gerado: "Centro");

        Assert.Empty(OsmBairros.Escolher([a, centro], [], 1));
        Assert.Equal("Centro", OsmBairros.Escolher([Z(2, 4000, [L("Centro", 0, 0)], gerado: "Centro")], [], 1)[2]);
    }

    [Fact]
    public void Escolha_nao_depende_da_ordem_da_entrada()
    {
        var zonas = Enumerable.Range(1, 12).Select(k => Z(
            k,
            1000 + (k % 4 * 500),
            Enumerable.Range(0, 5).Select(j => L($"Bairro {(char)('A' + ((k + j) % 7))}", j * 0.1, k * 0.1, prioridade: j % 3, id: (k * 10) + j)).ToList(),
            gerado: $"Zona {k}",
            setores: Enumerable.Range(0, 4).Select(j => new SetorPonto(j * 0.13, k * 0.1, 100 + (j * 37 % 5))).ToArray())).ToList();

        var first = OsmBairros.Escolher(zonas, ["Bairro C"], 1).OrderBy(p => p.Key).ToList();
        var rng = new Random(7);
        for (int round = 0; round < 5; round++)
        {
            var shuffled = zonas.OrderBy(_ => rng.Next()).Select(z => Z(z.Key, z.Pop, z.Lugares.OrderBy(_ => rng.Next()).ToList(), gerado: z.NomeGerado, setores: z.Setores.ToArray())).ToList();
            Assert.Equal(first, OsmBairros.Escolher(shuffled, ["Bairro C"], 1).OrderBy(p => p.Key).ToList());
        }

        Assert.NotEmpty(first);
        Assert.Equal(first.Count, first.Select(p => OsmBairros.Chave(p.Value)).Distinct().Count());
    }
}
