using System.Globalization;
using Planalto.Core.Data;
using Planalto.Core.Territory;
using Planalto.DataImporter.Geo;

namespace Planalto.DataImporter.Tests;

/// <summary>
/// Confere data/zonas/ contra o cache (agregado básico, de-para setor → zona e extração do OpenStreetMap): o IBGE
/// prevalece (zona com setor em bairro do IBGE nunca leva nome do OpenStreetMap, e o nome do OpenStreetMap nunca repete
/// um bairro ou distrito do IBGE do município) e todo nome <c>bairro_osm</c> está na extração da UF. Precisa do cache
/// (variável PLANALTO_CACHE ou a pasta padrão) e do modo "zonas" rodado com ele.
/// </summary>
public class ZonasOsmDataTests
{
    private sealed record SetorInfo(int Municipio, string? Bairro, string? BairroNome, string DistritoNome);

    private static readonly Lazy<(Dictionary<string, SetorInfo> Setores, Dictionary<string, List<string>> SetoresDaZona, List<(string Uf, Zona Zona)> Zonas, string Cache, TerritoryConfig Config)> Data = new(Load);

    private static (Dictionary<string, SetorInfo>, Dictionary<string, List<string>>, List<(string, Zona)>, string, TerritoryConfig) Load()
    {
        string dataDirectory = DataPaths.FindDataDirectory(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Pasta data/ não encontrada.");
        string cache = SourceCache.Locate(Directory.GetParent(dataDirectory)!.FullName);
        string basico = Path.Combine(cache, ZonasImport.CacheFolder, ZonasImport.BasicoFile);
        string dePara = Path.Combine(cache, ZonasImport.CacheFolder, "setor_zona.csv");
        Assert.True(File.Exists(basico), $"Agregado básico não encontrado em {basico}. Rode o Planalto.DataImporter para preencher o cache.");
        Assert.True(File.Exists(dePara), $"De-para setor → zona não encontrado em {dePara}. Rode o modo 'zonas' com este cache.");

        var setores = new Dictionary<string, SetorInfo>(StringComparer.Ordinal);
        foreach (string[] r in Csv.ReadColumnsFromZip(basico, _ => true, "CD_SETOR", "CD_MUN", "CD_BAIRRO", "NM_BAIRRO", "NM_DIST"))
        {
            if (int.TryParse(r[1], CultureInfo.InvariantCulture, out int mun))
            {
                bool hasBairro = r[2] is not ("." or "");
                setores[r[0]] = new SetorInfo(mun, hasBairro ? r[2] : null, hasBairro ? r[3] : null, r[4]);
            }
        }

        var setoresDaZona = File.ReadLines(dePara).Skip(1).Select(l => l.Split(';'))
            .GroupBy(p => p[1], StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Select(p => p[0]).ToList(), StringComparer.Ordinal);
        var zonas = Directory.EnumerateFiles(Path.Combine(dataDirectory, ZonasPaths.Directory), "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith('_'))
            .SelectMany(f => JsonData.LoadFile<ZonasUf>(f).GetValueOrThrow() is var file ? file.Municipios.SelectMany(m => m.Zonas).Select(z => (file.Uf, z)) : [])
            .ToList();
        TerritoryConfig config = JsonData.LoadFile<TerritoryConfig>(Path.Combine(dataDirectory, TerritoryConfig.File)).GetValueOrThrow();
        return (setores, setoresDaZona, zonas, cache, config);
    }

    [Fact]
    public void IBGE_prevalece_sobre_o_OpenStreetMap()
    {
        var (setores, setoresDaZona, zonas, _, _) = Data.Value;
        var osm = zonas.Where(p => p.Zona.NomeFonte == Zona.NomeOsm).Select(p => p.Zona).ToList();
        Assert.NotEmpty(osm);
        var nomesIbge = setores.Values.GroupBy(s => s.Municipio).ToDictionary(
            g => g.Key,
            g => g.SelectMany(s => new[] { s.BairroNome, s.DistritoNome }).OfType<string>().Select(OsmBairros.Chave).ToHashSet(StringComparer.Ordinal));
        foreach (Zona z in osm)
        {
            List<SetorInfo> daZona = setoresDaZona[z.Id].Select(s => setores[s]).ToList();
            Assert.True(daZona.All(s => s.Bairro is null), $"{z.Id} ({z.Nome}): zona com bairro do IBGE e nome do OpenStreetMap");

            Assert.False(nomesIbge[daZona[0].Municipio].Contains(OsmBairros.Chave(z.Nome)), $"{z.Id} ({z.Nome}): nome do OpenStreetMap repete um bairro ou distrito do IBGE");
        }
    }

    [Fact]
    public void Todo_nome_do_OpenStreetMap_esta_na_extracao_da_UF()
    {
        var (_, _, zonas, cache, config) = Data.Value;
        foreach (var group in zonas.Where(p => p.Zona.NomeFonte == Zona.NomeOsm).GroupBy(p => p.Uf))
        {
            string file = Path.Combine(cache, OsmBairros.FileName(group.Key));
            Assert.True(File.Exists(file), $"{file} não está no cache.");
            // Como o importador grava: o nome filtrado (sem "Bairro" no começo, caixa normal).
            var nomes = OsmBairros.Parse(File.ReadAllText(file), config.OsmBairros)
                .Select(l => OsmBairros.Filtrar(l.Nome, "", config.OsmBairros)).OfType<string>()
                .Select(OsmBairros.Chave).ToHashSet(StringComparer.Ordinal);
            Assert.All(group, p => Assert.True(nomes.Contains(OsmBairros.Chave(p.Zona.Nome)), $"{p.Zona.Id} ({p.Zona.Nome}) não está na extração de {group.Key}"));
        }
    }
}
