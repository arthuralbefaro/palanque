using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Planalto.Core.Data;
using Planalto.Core.Geo;
using Planalto.Core.Municipios;
using Planalto.Core.Territory;
using Planalto.DataImporter.Geo;

namespace Planalto.DataImporter.Rd;

/// <summary>
/// Modo "zonas-rd" (GDD 17.5 e 17.20, itens 4 e 6): completa data/zonas/ com o que não existe por setor.
/// <list type="bullet">
/// <item>Religião e nível de instrução pelas áreas de ponderação do Censo 2022, nos municípios com duas ou mais:
/// cada área é repartida entre as zonas pela população dos setores em comum.</item>
/// <item>Eleitores por zona pelos locais de votação do TSE (2024) com coordenada: cada local cai na zona que o
/// contém; o total é reescalado para o eleitorado atual do município.</item>
/// </list>
/// Roda depois do modo "zonas" (precisa do de-para setor → zona que ele grava no cache) e falha, sem gravar, se
/// data/zonas/ passar do teto.
/// </summary>
public static class ZonasRdImport
{
    public const string ComposicaoUrl = "https://ftp.ibge.gov.br/Censos/Censo_Demografico_2022/Microdados_e_Areas_de_Ponderacao/Documentacao/%c3%81reas%20de%20pondera%c3%a7%c3%a3o/Composi%c3%a7%c3%a3o%20das%20%c3%81reas%20de%20Pondera%c3%a7%c3%a3o.xlsx";
    public const string TabelasUrl = "https://ftp.ibge.gov.br/Censos/Censo_Demografico_2022/Microdados_e_Areas_de_Ponderacao/Areas_de_Ponderacao/tabelas_xlsx_20261106.zip";
    public const string LocaisUrl = "https://cdn.tse.jus.br/estatistica/sead/odsele/eleitorado_locais_votacao/eleitorado_local_votacao_2024.zip";
    public const string ReligiaoTabela = "Tab4_1";
    public const string InstrucaoTabela = "Tab2_2";

    private sealed class Acc
    {
        public double[]? Religiao;
        public double[]? Escolaridade;
        public long PopComArea;
        public double Eleitores2024;
    }

    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory)
    {
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        MunicipiosDataSet municipios = MunicipiosDataSet.Load(dataDirectory, portes).DataSet
            ?? throw new ImportException("data/municipios/ inválido.");
        TerritoryConfig config = JsonData.LoadFile<TerritoryConfig>(Path.Combine(dataDirectory, TerritoryConfig.File)).GetValueOrThrow();
        var byCode = municipios.All.ToDictionary(m => m.CodigoIbge);

        string setorZonaFile = ZonasImport.SetorZonaFile(cache);
        if (!File.Exists(setorZonaFile))
        {
            throw new ImportException($"{setorZonaFile} não existe: rode antes o modo 'zonas', que grava a zona de cada setor.");
        }

        // Setor → (zona, população).
        var setorZona = new Dictionary<string, (string Zona, long Pop)>(StringComparer.Ordinal);
        foreach (string line in File.ReadLines(setorZonaFile).Skip(1))
        {
            string[] p = line.Split(';');
            setorZona[p[0]] = (p[1], long.Parse(p[2], CultureInfo.InvariantCulture));
        }

        var uffiles = new SortedDictionary<string, ZonasUf>(StringComparer.Ordinal);
        foreach (string uf in municipios.All.Select(m => m.Uf).Distinct())
        {
            uffiles[uf] = ZonasPaths.Load(dataDirectory, uf).GetValueOrThrow();
        }

        var acc = uffiles.Values.SelectMany(f => f.Municipios).SelectMany(m => m.Zonas).ToDictionary(z => z.Id, _ => new Acc(), StringComparer.Ordinal);
        foreach (string zona in setorZona.Values.Select(v => v.Zona).Distinct())
        {
            if (!acc.ContainsKey(zona))
            {
                throw new ImportException($"O de-para do cache cita a zona {zona}, que não está em data/zonas/: rode o modo 'zonas' de novo.");
            }
        }

        // ---------------------------------------------------------------- áreas de ponderação
        Console.WriteLine("IBGE — Censo 2022, áreas de ponderação (religião e nível de instrução)");
        CachedFile composicao = await cache.GetAsync("rd/censo2022_composicao_areas_ponderacao.xlsx", ComposicaoUrl);
        CachedFile tabelas = await cache.GetAsync("rd/censo2022_areas_ponderacao_tabelas_xlsx.zip", TabelasUrl);

        // Área → zona → população em comum; e população da área (só setores que estão em alguma zona).
        var comum = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        int semZona = 0;
        string composicaoSheet = Xlsx.SheetNames(composicao.Path)[0];
        foreach (string?[] row in Xlsx.ReadRows(composicao.Path, composicaoSheet).Skip(1))
        {
            if (row.Length < 3 || row[0] is not { } setor || row[2] is not { } area)
            {
                continue;
            }

            if (!setorZona.TryGetValue(setor, out var z))
            {
                semZona++;
                continue;
            }

            if (!comum.TryGetValue(area, out Dictionary<string, long>? zonas))
            {
                comum[area] = zonas = new Dictionary<string, long>(StringComparer.Ordinal);
            }

            zonas[z.Zona] = zonas.GetValueOrDefault(z.Zona) + z.Pop;
            acc[z.Zona].PopComArea += z.Pop;
        }

        var areasPorMunicipio = comum.Keys.GroupBy(a => a[..7]).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        Dictionary<string, long[]> religiao = ReadAreaTable(tabelas.Path, ReligiaoTabela, PerfilCategorias.Religiao.Count);
        Dictionary<string, long[]> instrucao = ReadAreaTable(tabelas.Path, InstrucaoTabela, PerfilCategorias.Escolaridade.Count);
        foreach (var (area, zonas) in comum)
        {
            if (areasPorMunicipio[area[..7]] < 2)
            {
                continue;
            }

            long popArea = zonas.Values.Sum();
            if (popArea == 0)
            {
                continue;
            }

            foreach (var (zona, pop) in zonas)
            {
                double share = pop / (double)popArea;
                Acc a = acc[zona];
                if (religiao.TryGetValue(area, out long[]? r))
                {
                    a.Religiao ??= new double[r.Length];
                    for (int k = 0; k < r.Length; k++)
                    {
                        a.Religiao[k] += r[k] * share;
                    }
                }

                if (instrucao.TryGetValue(area, out long[]? e))
                {
                    a.Escolaridade ??= new double[e.Length];
                    for (int k = 0; k < e.Length; k++)
                    {
                        a.Escolaridade[k] += e[k] * share;
                    }
                }
            }
        }

        // ---------------------------------------------------------------- locais de votação
        Console.WriteLine("TSE — eleitorado por local de votação (2024)");
        CachedFile locais = await cache.GetAsync("rd/tse_eleitorado_local_votacao_2024.zip", LocaisUrl);
        CachedFile tseIbge = await cache.GetAsync("tse_municipio_tse_ibge.zip", Sources.TseMunicipiosUrl);
        var ibgePorTse = new Dictionary<int, int>();
        foreach (var row in Csv.ReadFromZip(tseIbge.Path, _ => true))
        {
            ibgePorTse[int.Parse(row["CD_MUNICIPIO_TSE"], CultureInfo.InvariantCulture)] = int.Parse(row["CD_MUNICIPIO_IBGE"], CultureInfo.InvariantCulture);
        }

        // Município → locais (coordenada → eleitores). Só o 1º turno; cada linha é uma seção.
        var comCoordenada = new Dictionary<int, Dictionary<(double Lat, double Lon), long>>();
        var semCoordenada = new Dictionary<int, long>();
        foreach (string[] r in Csv.ReadColumnsFromZip(locais.Path, n => n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase), "NR_TURNO", "CD_MUNICIPIO", "NR_LATITUDE", "NR_LONGITUDE", "QT_ELEITOR_SECAO"))
        {
            if (r[0] != "1" || !int.TryParse(r[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int tse) || !ibgePorTse.TryGetValue(tse, out int ibge) || !byCode.ContainsKey(ibge))
            {
                continue; // 2º turno, exterior ou município fora da lista
            }

            long eleitores = long.Parse(r[4], CultureInfo.InvariantCulture);
            double lon = 0;
            bool ok = double.TryParse(r[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double lat)
                && double.TryParse(r[3], NumberStyles.Float, CultureInfo.InvariantCulture, out lon)
                && lat is not (-1) && lon is not (-1) && Math.Abs(lat) <= 90 && Math.Abs(lon) <= 180;
            if (!ok)
            {
                semCoordenada[ibge] = semCoordenada.GetValueOrDefault(ibge) + eleitores;
                continue;
            }

            if (!comCoordenada.TryGetValue(ibge, out var points))
            {
                comCoordenada[ibge] = points = [];
            }

            points[(lat, lon)] = points.GetValueOrDefault((lat, lon)) + eleitores;
        }

        long foraDoMunicipio = 0;
        long eleitoresLocalizados = 0;
        var updated = new SortedDictionary<string, ZonasUf>(StringComparer.Ordinal);
        int zonasComAmostra = 0;
        int municipiosPorLocais = 0;
        foreach (var (uf, file) in uffiles)
        {
            GeoZonasUf geo = ZonasPaths.LoadGeo(dataDirectory, uf).GetValueOrThrow();
            var polygons = geo.Zonas.ToDictionary(z => z.Id, z => z.Polygons, StringComparer.Ordinal);
            var novos = new List<ZonasMunicipio>();
            foreach (ZonasMunicipio zm in file.Municipios)
            {
                Municipio m = byCode[zm.CodigoIbge];

                // Locais com coordenada: zona que contém o ponto; ponto fora de todas (contorno simplificado ou
                // coordenada errada, mas dentro da caixa do município) vai para a zona de centro mais próximo.
                if (comCoordenada.TryGetValue(zm.CodigoIbge, out var points))
                {
                    var (minLat, maxLat, minLon, maxLon) = Box(zm.Zonas.SelectMany(z => polygons[z.Id]), geo.Scale);
                    foreach (var ((lat, lon), eleitores) in points)
                    {
                        Zona? hit = zm.Zonas.FirstOrDefault(z => polygons[z.Id].Any(p => Contains(p, lon * geo.Scale, lat * geo.Scale)));
                        if (hit is null)
                        {
                            const double Margin = 0.02;
                            if (lat < minLat - Margin || lat > maxLat + Margin || lon < minLon - Margin || lon > maxLon + Margin)
                            {
                                // Coordenada fora do município: conta como local sem coordenada.
                                semCoordenada[zm.CodigoIbge] = semCoordenada.GetValueOrDefault(zm.CodigoIbge) + eleitores;
                                foraDoMunicipio += eleitores;
                                continue;
                            }

                            hit = zm.Zonas.MinBy(z => ((z.Lat - lat) * (z.Lat - lat)) + ((z.Lon - lon) * (z.Lon - lon)))!;
                        }

                        acc[hit.Id].Eleitores2024 += eleitores;
                        eleitoresLocalizados += eleitores;
                    }
                }

                // Pesos: eleitores localizados; os sem coordenada são repartidos pela população de 16 anos ou mais.
                double[] pop16 = zm.Zonas.Select(Pop16).ToArray();
                double pop16Total = pop16.Sum();
                double localizados = zm.Zonas.Sum(z => acc[z.Id].Eleitores2024);
                bool porLocais = localizados > 0;
                double resto = porLocais ? semCoordenada.GetValueOrDefault(zm.CodigoIbge) : 1;
                double[] weights = zm.Zonas.Select((z, i) => acc[z.Id].Eleitores2024 + (pop16Total > 0 ? resto * pop16[i] / pop16Total : resto / zm.Zonas.Count)).ToArray();
                long[] eleitoresZona = Apportion(weights, m.Eleitorado);
                municipiosPorLocais += porLocais ? 1 : 0;

                bool variasAreas = areasPorMunicipio.GetValueOrDefault(zm.CodigoIbge.ToString(CultureInfo.InvariantCulture)) >= 2;
                var zonas = new List<Zona>();
                for (int i = 0; i < zm.Zonas.Count; i++)
                {
                    Zona z = zm.Zonas[i];
                    Acc a = acc[z.Id];
                    bool amostra = variasAreas && z.Populacao > 0 && a.Religiao is not null && a.Escolaridade is not null
                        && a.PopComArea / (double)z.Populacao >= config.CoberturaMinima;
                    zonasComAmostra += amostra ? 1 : 0;
                    zonas.Add(new Zona
                    {
                        Id = z.Id,
                        Nome = z.Nome,
                        NomeFonte = z.NomeFonte,
                        Situacao = z.Situacao,
                        Populacao = z.Populacao,
                        Domicilios = z.Domicilios,
                        Setores = z.Setores,
                        AreaKm2 = z.AreaKm2,
                        Lat = z.Lat,
                        Lon = z.Lon,
                        Vizinhas = z.Vizinhas,
                        Eleitores = eleitoresZona[i],
                        EleitoresFonte = porLocais ? Zona.EleitoresLocais : Zona.EleitoresPopulacao,
                        Perfil = new PerfilZona
                        {
                            Idade = z.Perfil.Idade,
                            IdadeCobertura = z.Perfil.IdadeCobertura,
                            IdadeFonte = z.Perfil.IdadeFonte,
                            RendaResponsavel = z.Perfil.RendaResponsavel,
                            RendaCobertura = z.Perfil.RendaCobertura,
                            RendaFonte = z.Perfil.RendaFonte,
                            Religiao = amostra ? Named(PerfilCategorias.Religiao, a.Religiao!) : null,
                            Escolaridade = amostra ? Named(PerfilCategorias.Escolaridade, a.Escolaridade!) : null,
                        },
                    });
                }

                novos.Add(new ZonasMunicipio { CodigoIbge = zm.CodigoIbge, Zonas = zonas });
            }

            var novo = new ZonasUf { Uf = file.Uf, Fonte = file.Fonte, Metodo = file.Metodo, Versao = file.Versao, Municipios = novos };
            List<string> invalid = novo.Validate().ToList();
            if (invalid.Count > 0)
            {
                throw new ImportException($"{uf}: {string.Join("; ", invalid.Take(5))}");
            }

            updated[uf] = novo;
        }

        // ---------------------------------------------------------------- gravação, com o teto de data/zonas/
        var options = new JsonSerializerOptions(JsonData.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var json = updated.ToDictionary(p => p.Key, p => ZonasImport.SerializeZonas(p.Value, options));
        long bytes = json.Values.Sum(j => (long)Encoding.UTF8.GetByteCount(j));

        string fontesPath = Path.Combine(dataDirectory, ZonasPaths.Directory, "_fontes.json");
        JsonObject fontes = JsonNode.Parse(await File.ReadAllTextAsync(fontesPath))!.AsObject();
        long semCoordenadaTotal = semCoordenada.Where(p => byCode.ContainsKey(p.Key)).Sum(p => p.Value);
        fontes["atributos_bytes"] = bytes;
        fontes["teto_atributos_bytes"] = ZonasPaths.MaxZonasBytes;
        fontes["zonas_rd"] = JsonSerializer.SerializeToNode(
            new
            {
                GeradoEm = DateTimeOffset.UtcNow,
                Fontes = new[]
                {
                    new { Campo = "perfil.religiao, perfil.escolaridade", Fonte = $"IBGE, Censo Demográfico 2022, resultados da amostra por áreas de ponderação (tabelas {ReligiaoTabela} e {InstrucaoTabela}) e composição das áreas de ponderação", Ano = 2022, Url = $"{TabelasUrl}; {ComposicaoUrl}", BaixadoEm = tabelas.DownloadedAt },
                    new { Campo = "eleitores", Fonte = "TSE, eleitorado por local de votação, eleições de 2024 (1º turno), reescalado para o eleitorado atual do município", Ano = 2024, Url = LocaisUrl, BaixadoEm = locais.DownloadedAt },
                },
                Metodo = $"Religião (10 anos ou mais) e nível de instrução (25 anos ou mais): só nos municípios com duas ou mais áreas de ponderação; cada área é repartida entre as zonas pela população dos setores em comum; zona com menos de {config.CoberturaMinima:P0} da população em setores com área usa o perfil do município (campos ausentes). "
                    + "Eleitores: cada local de votação com coordenada conta na zona que o contém (ou na de centro mais próximo, se cair fora de todos os contornos simplificados); os eleitores de locais sem coordenada, ou com coordenada fora do município, são repartidos pela população de 16 anos ou mais; o resultado é reescalado, pelo método dos maiores restos, para o eleitorado atual do município.",
                ZonasComReligiaoEEscolaridade = zonasComAmostra,
                MunicipiosComDuasOuMaisAreas = areasPorMunicipio.Count(p => p.Value >= 2),
                SetoresDaComposicaoSemZona = semZona,
                MunicipiosComEleitoresPorLocais = municipiosPorLocais,
                Eleitores2024Localizados = eleitoresLocalizados,
                Eleitores2024SemCoordenada = semCoordenadaTotal,
                Eleitores2024ComCoordenadaForaDoMunicipio = foraDoMunicipio,
            },
            options);
        string fontesJson = fontes.ToJsonString(new JsonSerializerOptions(options) { WriteIndented = true }) + "\n";

        long total = bytes + Encoding.UTF8.GetByteCount(fontesJson);
        Console.WriteLine($"Atributos: {total / 1024 / 1024.0:0.0} MB (teto {ZonasPaths.MaxZonasBytes / 1024 / 1024} MB); {zonasComAmostra} zonas com religião e escolaridade; {municipiosPorLocais} municípios com eleitores por locais de votação");
        if (total > ZonasPaths.MaxZonasBytes)
        {
            throw new ImportException($"data/zonas/ teria {total} bytes, acima do teto de {ZonasPaths.MaxZonasBytes}; nada foi gravado.");
        }

        var utf8 = new UTF8Encoding(false);
        foreach (var (uf, text) in json)
        {
            await File.WriteAllTextAsync(ZonasPaths.File(dataDirectory, uf), text, utf8);
        }

        await File.WriteAllTextAsync(fontesPath, fontesJson, utf8);
        Console.WriteLine("Gravado: data/zonas/ com eleitores, religião e escolaridade por zona.");
        return 0;
    }

    /// <summary>Lê uma tabela por área de ponderação: área → contagens das categorias (as colunas depois de "Total"); "-" é zero.</summary>
    private static Dictionary<string, long[]> ReadAreaTable(string zipPath, string table, int categories)
    {
        using ZipArchive zip = ZipFile.OpenRead(zipPath);
        ZipArchiveEntry entry = zip.Entries.FirstOrDefault(e => Path.GetFileNameWithoutExtension(e.Name) == table)
            ?? throw new ImportException($"Áreas de ponderação: {table}.xlsx não está em {zipPath}.");
        using var buffer = new MemoryStream();
        using (Stream input = entry.Open())
        {
            input.CopyTo(buffer);
        }

        buffer.Position = 0;
        string sheet = Xlsx.SheetNames(buffer)[0];
        buffer.Position = 0;
        var result = new Dictionary<string, long[]>(StringComparer.Ordinal);
        int areaColumn = -1;
        int totalColumn = -1;
        foreach (string?[] row in Xlsx.ReadRows(buffer, sheet))
        {
            if (areaColumn < 0)
            {
                areaColumn = Array.FindIndex(row, c => c is not null && c.StartsWith("Área de ponderação", StringComparison.OrdinalIgnoreCase));
                totalColumn = Array.IndexOf(row, "Total");
                if (areaColumn >= 0 && (totalColumn < 0 || row.Length < totalColumn + 1 + categories))
                {
                    throw new ImportException($"{table}: cabeçalho sem 'Total' seguido de {categories} categorias.");
                }

                continue;
            }

            if (row.Length <= areaColumn || row[areaColumn] is not { Length: > 7 } area || !area.All(char.IsDigit))
            {
                continue; // linha do Brasil e notas
            }

            var values = new long[categories];
            for (int k = 0; k < categories; k++)
            {
                string? cell = totalColumn + 1 + k < row.Length ? row[totalColumn + 1 + k] : null;
                values[k] = cell is null or "-" ? 0 : (long)Math.Round(double.Parse(cell, NumberStyles.Float, CultureInfo.InvariantCulture));
            }

            result[area] = values;
        }

        if (result.Count == 0)
        {
            throw new ImportException($"{table}: nenhuma área de ponderação lida.");
        }

        return result;
    }

    private static List<long> Named(IReadOnlyList<string> keys, double[] values) =>
        keys.Select((_, i) => (long)Math.Round(values[i])).ToList();

    /// <summary>
    /// População de 16 anos ou mais da zona pelas faixas publicadas por setor: 4/5 da faixa de 15 a 19 anos e as
    /// faixas seguintes (mesma repartição uniforme do <see cref="ZoneProjection"/>). Com o perfil do município na zona,
    /// vale a proporção do município aplicada à população da zona.
    /// </summary>
    private static double Pop16(Zona z)
    {
        double total = z.Perfil.Idade.Values.Sum();
        if (total <= 0)
        {
            return z.Populacao;
        }

        double jovens = z.Perfil.Idade.GetValueOrDefault("0_4") + z.Perfil.Idade.GetValueOrDefault("5_9") + z.Perfil.Idade.GetValueOrDefault("10_14")
            + (0.2 * z.Perfil.Idade.GetValueOrDefault("15_19"));
        return z.Populacao * (1 - (jovens / total));
    }

    /// <summary>Reparte um total inteiro na proporção dos pesos, pelo método dos maiores restos.</summary>
    public static long[] Apportion(double[] weights, long total)
    {
        double sum = weights.Sum();
        if (sum <= 0)
        {
            weights = weights.Select(_ => 1.0).ToArray();
            sum = weights.Length;
        }

        double[] exact = weights.Select(w => w / sum * total).ToArray();
        long[] result = exact.Select(e => (long)Math.Floor(e)).ToArray();
        long left = total - result.Sum();
        foreach (int i in Enumerable.Range(0, weights.Length).OrderByDescending(i => exact[i] - result[i]).ThenBy(i => i).Take((int)left))
        {
            result[i]++;
        }

        return result;
    }

    private static (double MinLat, double MaxLat, double MinLon, double MaxLon) Box(IEnumerable<GeoPolygon> polygons, int scale)
    {
        double minLat = double.MaxValue, maxLat = double.MinValue, minLon = double.MaxValue, maxLon = double.MinValue;
        foreach (GeoPolygon p in polygons)
        {
            for (int i = 0; i + 1 < p.Outer.Count; i += 2)
            {
                double lon = p.Outer[i] / (double)scale;
                double lat = p.Outer[i + 1] / (double)scale;
                minLat = Math.Min(minLat, lat);
                maxLat = Math.Max(maxLat, lat);
                minLon = Math.Min(minLon, lon);
                maxLon = Math.Max(maxLon, lon);
            }
        }

        return (minLat, maxLat, minLon, maxLon);
    }

    /// <summary>Ponto (em unidades inteiras do arquivo) dentro do contorno externo e fora dos furos.</summary>
    public static bool Contains(GeoPolygon polygon, double x, double y) =>
        InRing(polygon.Outer, x, y) && !polygon.Holes.Any(h => InRing(h, x, y));

    private static bool InRing(IReadOnlyList<int> ring, double x, double y)
    {
        bool inside = false;
        int n = ring.Count / 2;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            double xi = ring[2 * i], yi = ring[(2 * i) + 1];
            double xj = ring[2 * j], yj = ring[(2 * j) + 1];
            if ((yi > y) != (yj > y) && x < ((xj - xi) * (y - yi) / (yj - yi)) + xi)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
