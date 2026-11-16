using System.Globalization;
using Planalto.Core.Data;
using Planalto.Core.Territory;
using Planalto.DataImporter.Geo;

namespace Planalto.DataImporter.Tests;

/// <summary>
/// Confere data/zonas/ contra os setores do Censo 2022 no cache (agregado básico, com distrito e situação de cada
/// setor, e o de-para setor → zona que o modo "zonas" grava): a zona rural está dividida pelos distritos do IBGE e
/// cada distrito leva o nome oficial. Precisa do cache (variável PLANALTO_CACHE ou a pasta padrão) e do modo
/// "zonas" rodado com ele.
/// </summary>
public class ZonasDistritosDataTests
{
    private sealed record SetorInfo(int Municipio, string Distrito, string DistritoNome, bool Rural, long Pop);

    private static readonly Lazy<(Dictionary<string, SetorInfo> Setores, Dictionary<string, string> Zona, Dictionary<string, Zona> Zonas, TerritoryConfig Config)> Data = new(Load);

    private static (Dictionary<string, SetorInfo>, Dictionary<string, string>, Dictionary<string, Zona>, TerritoryConfig) Load()
    {
        string dataDirectory = DataPaths.FindDataDirectory(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Pasta data/ não encontrada.");
        string cache = SourceCache.Locate(Directory.GetParent(dataDirectory)!.FullName);
        string basico = Path.Combine(cache, ZonasImport.CacheFolder, ZonasImport.BasicoFile);
        string dePara = Path.Combine(cache, ZonasImport.CacheFolder, "setor_zona.csv");
        Assert.True(File.Exists(basico), $"Agregado básico não encontrado em {basico}. Rode o Planalto.DataImporter para preencher o cache.");
        Assert.True(File.Exists(dePara), $"De-para setor → zona não encontrado em {dePara}. Rode o modo 'zonas' com este cache.");

        var setores = new Dictionary<string, SetorInfo>(StringComparer.Ordinal);
        foreach (string[] r in Csv.ReadColumnsFromZip(basico, _ => true, "CD_SETOR", "CD_SIT", "CD_MUN", "CD_DIST", "NM_DIST", "v0001"))
        {
            if (int.TryParse(r[2], CultureInfo.InvariantCulture, out int mun) && long.TryParse(r[5], CultureInfo.InvariantCulture, out long pop))
            {
                setores[r[0]] = new SetorInfo(mun, r[3], r[4], r[1] is not ("1" or "2" or "3"), pop);
            }
        }

        var zona = File.ReadLines(dePara).Skip(1).Select(l => l.Split(';')).ToDictionary(p => p[0], p => p[1], StringComparer.Ordinal);
        var zonas = Directory.EnumerateFiles(Path.Combine(dataDirectory, ZonasPaths.Directory), "*.json")
            .Where(f => !Path.GetFileName(f).StartsWith('_'))
            .SelectMany(f => JsonData.LoadFile<ZonasUf>(f).GetValueOrThrow().Municipios.SelectMany(m => m.Zonas))
            .ToDictionary(z => z.Id, StringComparer.Ordinal);
        TerritoryConfig config = JsonData.LoadFile<TerritoryConfig>(Path.Combine(dataDirectory, TerritoryConfig.File)).GetValueOrThrow();
        return (setores, zona, zonas, config);
    }

    private static string Clean(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    [Fact]
    public void Todo_setor_com_municipio_esta_numa_zona_que_existe()
    {
        var (setores, zona, zonas, _) = Data.Value;
        var semZona = setores.Keys.Where(s => !zona.ContainsKey(s)).Take(5).ToList();
        Assert.Empty(semZona);
        Assert.All(zona.Values.Distinct(), z => Assert.True(zonas.ContainsKey(z), $"{z} do de-para não está em data/zonas/"));
    }

    /// <summary>
    /// Onde a divisão cabe inteira (de 2 até 1 + zonas_extras_max grupos de distritos com o mínimo de moradores rurais), cada
    /// um desses distritos tem a zona rural própria, sem outro distrito grande dentro, e o distrito fora da sede dá o
    /// nome oficial à zona.
    /// </summary>
    [Fact]
    public void Zona_rural_dividida_pelos_distritos_com_o_nome_oficial()
    {
        var (setores, zona, zonas, config) = Data.Value;
        long minimo = config.RuralPorDistrito.PopulacaoMinima;
        int divididos = 0;
        var falhas = new List<string>();
        foreach (var municipio in setores.Where(p => zona.ContainsKey(p.Key)).GroupBy(p => p.Value.Municipio))
        {
            var rurais = municipio.Where(p => p.Value.Rural).ToList();
            var grandes = rurais.GroupBy(p => p.Value.Distrito).Where(g => g.Sum(p => p.Value.Pop) >= minimo).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);

            // Distritos pequenos podem se juntar entre si num grupo próprio: no pior caso, um grupo a mais por mínimo.
            long pequenos = rurais.Where(p => !grandes.Contains(p.Value.Distrito)).Sum(p => p.Value.Pop);
            long grupos = grandes.Count + (pequenos / Math.Max(1, minimo));
            if (grandes.Count < 2 || grupos > config.RuralPorDistrito.ZonasExtrasMax + 1)
            {
                continue;
            }

            divididos++;
            string sede = municipio.Where(p => !p.Value.Rural).DefaultIfEmpty(municipio.First())
                .GroupBy(p => p.Value.Distrito).MaxBy(g => g.Sum(p => p.Value.Pop))!.Key;

            // Nenhuma zona rural junta dois distritos grandes.
            foreach (var z in rurais.GroupBy(p => zona[p.Key]))
            {
                var dentro = z.Select(p => p.Value.Distrito).Where(grandes.Contains).Distinct().ToList();
                if (dentro.Count > 1)
                {
                    falhas.Add($"{z.Key} junta os distritos {string.Join(", ", dentro)}");
                }
            }

            // Cada distrito grande fora da sede dá nome a uma zona rural.
            foreach (string d in grandes.Where(d => d != sede))
            {
                string nome = Clean(rurais.First(p => p.Value.Distrito == d).Value.DistritoNome);
                var zonasDoDistrito = rurais.Where(p => p.Value.Distrito == d).Select(p => zonas[zona[p.Key]]).Distinct().ToList();
                if (!zonasDoDistrito.Any(z => z.IsRural && z.NomeFonte == Zona.NomeDistrito && z.Nome.StartsWith(nome, StringComparison.Ordinal)))
                {
                    falhas.Add($"{municipio.Key}: distrito {d} ({nome}) sem zona rural com o nome dele ({string.Join(", ", zonasDoDistrito.Select(z => z.Nome))})");
                }
            }
        }

        Assert.True(divididos > 1000, $"só {divididos} municípios com a zona rural dividida por distrito");
        Assert.True(falhas.Count == 0, $"{falhas.Count} falhas:\n" + string.Join("\n", falhas.Take(20)));
    }

    /// <summary>Município sem distrito além da sede (ou com os outros abaixo do mínimo) não ganha zona rural de distrito.</summary>
    [Fact]
    public void Municipio_so_com_a_sede_nao_ganha_zona_de_distrito()
    {
        var (setores, zona, zonas, config) = Data.Value;
        foreach (var municipio in setores.Where(p => zona.ContainsKey(p.Key)).GroupBy(p => p.Value.Municipio))
        {
            if (municipio.Select(p => p.Value.Distrito).Distinct().Count() > 1)
            {
                continue;
            }

            var rurais = municipio.Where(p => p.Value.Rural).Select(p => zonas[zona[p.Key]]).Distinct().ToList();
            Assert.All(rurais, z => Assert.NotEqual(Zona.NomeDistrito, z.NomeFonte));
        }
    }
}
