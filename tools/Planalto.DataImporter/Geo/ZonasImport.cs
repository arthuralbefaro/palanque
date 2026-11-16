using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NetTopologySuite.Coverage;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.Operation.Union;
using NetTopologySuite.Precision;
using NetTopologySuite.Simplify;
using Planalto.Core.Data;
using Planalto.Core.Geo;
using Planalto.Core.Municipios;
using Planalto.Core.Territory;

namespace Planalto.DataImporter.Geo;

/// <summary>
/// Modo "zonas" (GDD 17.5): agrupa os setores censitários do Censo 2022 em zonas por município e grava
/// data/zonas/{UF}.json (atributos) e data/geo/zonas/{UF}.json (contornos). Falha, sem gravar nada, se a população
/// das zonas não bater com a do Censo ou se os contornos passarem do teto de tamanho.
/// </summary>
public static class ZonasImport
{
    public const string MalhaUrl = "https://geoftp.ibge.gov.br/organizacao_do_territorio/malhas_territoriais/malhas_de_setores_censitarios__divisoes_intramunicipais/censo_2022/setores/shp/UF/";
    public const string AgregadosUrl = "https://ftp.ibge.gov.br/Censos/Censo_Demografico_2022/Agregados_por_Setores_Censitarios/Agregados_por_Setor_csv/";
    public const string RendaUrl = "https://ftp.ibge.gov.br/Censos/Censo_Demografico_2022/Agregados_por_Setores_Censitarios_Rendimento_do_Responsavel/";
    public const string BasicoFile = "Agregados_por_setores_basico_BR_20260520.zip";
    public const string DemografiaFile = "Agregados_por_setores_demografia_BR.zip";
    public const string RendaFile = "Agregados_por_setores_renda_responsavel_BR_20260508_csv.zip";
    public const string CacheFolder = "setores2022";

    /// <summary>Faixas etárias publicadas por setor (V01031 a V01041).</summary>
    public static readonly (string Key, string Column)[] FaixasIdade =
    [
        ("0_4", "V01031"), ("5_9", "V01032"), ("10_14", "V01033"), ("15_19", "V01034"), ("20_24", "V01035"), ("25_29", "V01036"),
        ("30_39", "V01037"), ("40_49", "V01038"), ("50_59", "V01039"), ("60_69", "V01040"), ("70_mais", "V01041"),
    ];

    private static readonly string[] Direcoes = ["Leste", "Nordeste", "Norte", "Noroeste", "Oeste", "Sudoeste", "Sul", "Sudeste"];

    private sealed class Setor
    {
        public required string Code { get; init; }
        public required int Municipio { get; init; }
        public required bool Rural { get; init; }
        public required string Distrito { get; init; }
        public required string DistritoNome { get; init; }
        public required string? Bairro { get; init; }
        public required string? BairroNome { get; init; }
        public required long Pop { get; init; }
        public required long Dom { get; init; }
        public required double AreaKm2 { get; init; }
        public long[]? Idade { get; set; }
        public (double Media, long Responsaveis)? Renda { get; set; }
        public Geometry? Geometry { get; set; }
        public Point? Center { get; set; }
    }

    private sealed class Cluster
    {
        public required int Id { get; init; }
        public required bool Rural { get; init; }
        public List<Setor> Setores { get; } = [];
        public HashSet<int> Neighbors { get; } = [];
        public long Pop { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory)
    {
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        MunicipiosDataSet municipios = MunicipiosDataSet.Load(dataDirectory, portes).DataSet
            ?? throw new ImportException("data/municipios/ inválido; rode o importador de municípios antes.");
        TerritoryConfig config = JsonData.LoadFile<TerritoryConfig>(Path.Combine(dataDirectory, TerritoryConfig.File)).GetValueOrThrow();
        var byCode = municipios.All.ToDictionary(m => m.CodigoIbge);

        Console.WriteLine("IBGE — Censo 2022, agregados por setores censitários");
        CachedFile basico = await cache.GetAsync(Path.Combine(CacheFolder, BasicoFile), AgregadosUrl + BasicoFile);
        CachedFile demografia = await cache.GetAsync(Path.Combine(CacheFolder, DemografiaFile), AgregadosUrl + DemografiaFile);
        CachedFile renda = await cache.GetAsync(Path.Combine(CacheFolder, RendaFile), RendaUrl + RendaFile);

        Dictionary<string, Setor> setores = ReadBasico(basico.Path, byCode);
        ReadDemografia(demografia.Path, setores);
        ReadRenda(renda.Path, setores);

        // Regra de sigilo, parte 1: a população vem do básico, que não tem supressão, e tem de bater com o Censo.
        var errors = new List<string>();
        var porMunicipio = setores.Values.GroupBy(s => s.Municipio).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var (code, group) in porMunicipio)
        {
            long expected = byCode[code].Perfil.PopulacaoCenso;
            long sum = group.Sum(s => s.Pop);
            if (sum != expected)
            {
                errors.Add($"{code} ({byCode[code].Nome}): setores somam {sum}, Censo {expected}.");
            }
        }

        // Município criado depois do Censo (Boa Esperança do Norte/MT): sem setores; vira uma zona só, com o contorno
        // municipal de data/geo/ e o perfil do município, registrada como lacuna.
        var semSetores = municipios.All.Where(m => !porMunicipio.ContainsKey(m.CodigoIbge)).ToList();
        foreach (Municipio m in semSetores.Where(m => m.PerfilFonte != Municipio.PerfilFonteUf))
        {
            errors.Add($"{m.CodigoIbge} ({m.Nome}): sem setores no agregado básico e com perfil próprio do Censo.");
        }

        if (errors.Count > 0)
        {
            errors.ForEach(e => Console.Error.WriteLine($"  ERRO {e}"));
            throw new ImportException($"{errors.Count} municípios com população das zonas diferente do Censo; nada foi gravado.");
        }

        var targets = TargetCounts(municipios, config);
        var zonasFiles = new SortedDictionary<string, ZonasUf>(StringComparer.Ordinal);
        var geoJson = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var lacunas = new List<string>();
        var tolerancias = new List<double>();
        string source = $"IBGE, Malha de Setores Censitários e Agregados por Setores do Censo Demográfico 2022 ({MalhaUrl}; {AgregadosUrl}; {RendaUrl})";
        string method = Method(config);

        foreach (var uf in municipios.All.Select(m => m.Uf).Distinct().Order(StringComparer.Ordinal))
        {
            string fileName = $"{uf}_setores_CD2022.zip";
            CachedFile malha = await cache.GetAsync(Path.Combine(CacheFolder, fileName), MalhaUrl + fileName);
            int read = ReadGeometry(Malha.ExtractedShapefile(malha.Path), setores);
            var ufMunicipios = new List<ZonasMunicipio>();
            var ufGeo = new List<GeoZona>();
            foreach (Municipio m in municipios.All.Where(m => m.Uf == uf).OrderBy(m => m.CodigoIbge))
            {
                if (!porMunicipio.TryGetValue(m.CodigoIbge, out List<Setor>? list))
                {
                    var (zona, contorno) = WholeMunicipio(dataDirectory, m);
                    ufMunicipios.Add(new ZonasMunicipio { CodigoIbge = m.CodigoIbge, Zonas = [zona] });
                    ufGeo.Add(contorno);
                    lacunas.Add($"{zona.Id} município sem setores no Censo 2022 (criado depois): uma zona com o contorno municipal e o perfil do município");
                    continue;
                }

                if (list.Any(s => s.Geometry is null))
                {
                    throw new ImportException($"{m.CodigoIbge}: setor sem polígono na malha ({list.First(s => s.Geometry is null).Code}).");
                }

                var (zonas, geo, tolerance) = BuildMunicipio(m, list, targets[m.CodigoIbge], config, lacunas);
                tolerancias.Add(tolerance);
                ufMunicipios.Add(new ZonasMunicipio { CodigoIbge = m.CodigoIbge, Zonas = zonas });
                ufGeo.AddRange(geo);
            }

            var file = new ZonasUf { Uf = uf, Fonte = source, Metodo = method, Municipios = ufMunicipios };
            errors.AddRange(file.Validate());
            zonasFiles[uf] = file;
            geoJson[uf] = SerializeGeo(new GeoZonasUf { Uf = uf, Source = source, Method = method, Scale = GeoImport.Scale, Zonas = ufGeo });
            Console.WriteLine($"  {uf}: {read} setores, {ufMunicipios.Sum(m => m.Zonas.Count)} zonas, {Encoding.UTF8.GetByteCount(geoJson[uf]) / 1024} KB de contornos");

            // Libera a geometria da UF antes da próxima.
            foreach (Setor s in municipios.All.Where(m => m.Uf == uf && porMunicipio.ContainsKey(m.CodigoIbge)).SelectMany(m => porMunicipio[m.CodigoIbge]))
            {
                s.Geometry = null;
            }
        }

        long geoBytes = geoJson.Values.Sum(j => (long)Encoding.UTF8.GetByteCount(j));
        Console.WriteLine($"Contornos: {geoBytes / 1024 / 1024.0:0.0} MB (teto {ZonasPaths.MaxGeoBytes / 1024 / 1024} MB); zonas: {zonasFiles.Values.Sum(f => f.Municipios.Sum(m => m.Zonas.Count))}");
        if (geoBytes > ZonasPaths.MaxGeoBytes)
        {
            errors.Add($"data/geo/zonas/ teria {geoBytes} bytes, acima do teto de {ZonasPaths.MaxGeoBytes}: aumente a simplificação (vertices_por_zona) ou reduza as zonas por porte.");
        }

        // Teto de data/zonas/ (D-037): os atributos são serializados e medidos antes de qualquer gravação.
        var options = new JsonSerializerOptions(JsonData.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var zonasJson = zonasFiles.ToDictionary(p => p.Key, p => SerializeZonas(p.Value, options));
        long zonasBytes = zonasJson.Values.Sum(j => (long)Encoding.UTF8.GetByteCount(j));

        var fontes = new
        {
            GeradoEm = DateTimeOffset.UtcNow,
            Fontes = new[]
            {
                new { Campo = "contornos", Fonte = "IBGE, Malha de Setores Censitários do Censo Demográfico 2022 (versão definitiva)", Ano = 2022, Url = MalhaUrl, BaixadoEm = basico.DownloadedAt },
                new { Campo = "populacao, domicilios, situacao, distrito, bairro", Fonte = "IBGE, Agregados por Setores Censitários, básico", Ano = 2022, Url = basico.Url, BaixadoEm = basico.DownloadedAt },
                new { Campo = "perfil.idade", Fonte = "IBGE, Agregados por Setores Censitários, demografia", Ano = 2022, Url = demografia.Url, BaixadoEm = demografia.DownloadedAt },
                new { Campo = "perfil.renda_responsavel", Fonte = "IBGE, Agregados por Setores Censitários, rendimento do responsável", Ano = 2022, Url = renda.Url, BaixadoEm = renda.DownloadedAt },
            },
            Metodo = method,
            Tolerancia = new { Minima = tolerancias.Min(), Mediana = tolerancias.Order().ElementAt(tolerancias.Count / 2), Maxima = tolerancias.Max() },
            ContornosBytes = geoBytes,
            TetoBytes = ZonasPaths.MaxGeoBytes,
            AtributosBytes = zonasBytes,
            TetoAtributosBytes = ZonasPaths.MaxZonasBytes,
            Zonas = zonasFiles.Values.Sum(f => f.Municipios.Sum(m => m.Zonas.Count)),
            RegraDeSigilo = $"População e domicílios do agregado básico (sem supressão). Idade e renda: células com \"X\" ou setor sem linha não são estimados; cada bloco soma só os setores publicados, e a zona com cobertura abaixo de {config.CoberturaMinima:P0} usa o perfil do município.",
            Lacunas = lacunas,
        };
        string fontesJson = JsonSerializer.Serialize(fontes, new JsonSerializerOptions(options) { WriteIndented = true }) + "\n";
        long zonasDirectoryBytes = zonasBytes + Encoding.UTF8.GetByteCount(fontesJson);
        Console.WriteLine($"Atributos: {zonasDirectoryBytes / 1024 / 1024.0:0.0} MB (teto {ZonasPaths.MaxZonasBytes / 1024 / 1024} MB)");
        if (zonasDirectoryBytes > ZonasPaths.MaxZonasBytes)
        {
            errors.Add($"data/zonas/ teria {zonasDirectoryBytes} bytes, acima do teto de {ZonasPaths.MaxZonasBytes}: reduza os atributos gravados por zona.");
        }

        if (errors.Count > 0)
        {
            errors.Take(50).ToList().ForEach(e => Console.Error.WriteLine($"  ERRO {e}"));
            throw new ImportException($"{errors.Count} erros; nada foi gravado.");
        }

        Directory.CreateDirectory(Path.Combine(dataDirectory, ZonasPaths.Directory));
        Directory.CreateDirectory(ZonasPaths.GeoDirectory(dataDirectory));
        foreach (var (uf, json) in zonasJson)
        {
            await File.WriteAllTextAsync(ZonasPaths.File(dataDirectory, uf), json, new UTF8Encoding(false));
            await File.WriteAllTextAsync(ZonasPaths.GeoFile(dataDirectory, uf), geoJson[uf], new UTF8Encoding(false));
        }

        await File.WriteAllTextAsync(Path.Combine(dataDirectory, ZonasPaths.Directory, "_fontes.json"), fontesJson, new UTF8Encoding(false));
        Console.WriteLine($"Gravado: data/zonas/ e data/geo/zonas/ ({lacunas.Count} blocos de perfil substituídos pelo do município).");
        return 0;
    }

    public static string Method(TerritoryConfig c) =>
        string.Create(CultureInfo.InvariantCulture, $"Setores agrupados por município, sem misturar urbanos e rurais; urbanos partem dos bairros do IBGE onde existem. ")
        + string.Create(CultureInfo.InvariantCulture, $"Agrupamento guloso: o grupo de menor população se junta ao vizinho de menor população (vizinhança por divisa comum; sem vizinho, o mais próximo) até o número de zonas do porte. ")
        + string.Create(CultureInfo.InvariantCulture, $"Contornos dissolvidos e simplificados por cobertura do município (tolerância proporcional ao tamanho de cada zona, aumentada até {c.VerticesPorZona} vértices por zona em média, entre {c.ToleranciaInicialGraus / 10}° e {c.ToleranciaMaximaGraus}°), coordenadas a 1/{GeoImport.Scale} de grau.");

    private static Dictionary<string, Setor> ReadBasico(string zip, Dictionary<int, Municipio> byCode)
    {
        var result = new Dictionary<string, Setor>(StringComparer.Ordinal);
        foreach (string[] r in Csv.ReadColumnsFromZip(zip, _ => true, "CD_SETOR", "CD_SIT", "CD_MUN", "CD_DIST", "NM_DIST", "CD_BAIRRO", "NM_BAIRRO", "AREA_KM2", "v0001", "v0007"))
        {
            if (!int.TryParse(r[2], out int mun) || !byCode.ContainsKey(mun))
            {
                // Áreas das lagoas do RS sem município (fora do mapa também na D-025).
                continue;
            }

            bool hasBairro = r[5] is not ("." or "");
            result[r[0]] = new Setor
            {
                Code = r[0],
                Municipio = mun,
                Rural = r[1] is not ("1" or "2" or "3"),
                Distrito = r[3],
                DistritoNome = r[4],
                Bairro = hasBairro ? r[5] : null,
                BairroNome = hasBairro ? r[6] : null,
                AreaKm2 = double.Parse(r[7].Replace(',', '.'), CultureInfo.InvariantCulture),
                Pop = Number(r[8]) ?? throw new ImportException($"básico: setor {r[0]} com população suprimida."),
                Dom = Number(r[9]) ?? throw new ImportException($"básico: setor {r[0]} com domicílios suprimidos."),
            };
        }

        return result;
    }

    private static void ReadDemografia(string zip, Dictionary<string, Setor> setores)
    {
        string[] columns = FaixasIdade.Select(f => f.Column).Prepend("CD_setor").ToArray();
        foreach (string[] r in Csv.ReadColumnsFromZip(zip, _ => true, columns))
        {
            if (!setores.TryGetValue(r[0], out Setor? s))
            {
                continue;
            }

            long?[] values = r.Skip(1).Select(Number).ToArray();
            s.Idade = values.All(v => v.HasValue) ? values.Select(v => v!.Value).ToArray() : null;
        }
    }

    private static void ReadRenda(string zip, Dictionary<string, Setor> setores)
    {
        foreach (string[] r in Csv.ReadColumnsFromZip(zip, _ => true, "CD_SETOR", "V06001", "V06004"))
        {
            if (setores.TryGetValue(r[0], out Setor? s) && Number(r[1]) is long responsaveis
                && double.TryParse(r[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double media))
            {
                s.Renda = (media, responsaveis);
            }
        }
    }

    private static long? Number(string value) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long n) ? n : null;

    private static int ReadGeometry(string shp, Dictionary<string, Setor> setores)
    {
        int count = 0;
        // Alguns polígonos da malha (AM) vêm com anéis inválidos: o leitor corrige em vez de abortar, e o GeometryFixer garante o resto.
        var options = new NetTopologySuite.IO.Esri.Shapefiles.Readers.ShapefileReaderOptions { GeometryBuilderMode = NetTopologySuite.IO.Esri.Shapefiles.Readers.GeometryBuilderMode.FixInvalidShapes };
        foreach (var feature in Shapefile.ReadAllFeatures(shp, options))
        {
            string code = feature.Attributes["CD_SETOR"]?.ToString()?.Trim() ?? "";
            if (setores.TryGetValue(code, out Setor? s))
            {
                Geometry g = feature.Geometry.IsValid ? feature.Geometry : GeometryFixer.Fix(feature.Geometry);
                s.Geometry = g;
                s.Center = g.InteriorPoint;
                count++;
            }
        }

        return count;
    }

    /// <summary>Número de zonas de cada município: dentro da faixa do porte, pela posição na população entre os do mesmo porte.</summary>
    private static Dictionary<int, int> TargetCounts(MunicipiosDataSet municipios, TerritoryConfig config)
    {
        var result = new Dictionary<int, int>();
        foreach (var group in municipios.All.GroupBy(m => m.Porte))
        {
            var ordered = group.OrderBy(m => m.Perfil.PopulacaoCenso).ToList();
            Planalto.Core.Characters.AgeRange range = config.ZonasPorPorte[group.Key];
            for (int i = 0; i < ordered.Count; i++)
            {
                double fraction = ordered.Count == 1 ? 0.5 : i / (double)(ordered.Count - 1);
                result[ordered[i].CodigoIbge] = range.Min + (int)Math.Round((range.Max - range.Min) * fraction);
            }
        }

        return result;
    }

    private static (List<Zona> Zonas, List<GeoZona> Geo, double Tolerance) BuildMunicipio(Municipio m, List<Setor> setores, int target, TerritoryConfig config, List<string> lacunas)
    {
        List<HashSet<int>> adjacency = Adjacency(setores);

        long ruralPop = setores.Where(s => s.Rural).Sum(s => s.Pop);
        long totalPop = Math.Max(1, setores.Sum(s => s.Pop));
        bool hasRural = setores.Any(s => s.Rural);
        bool hasUrban = setores.Any(s => !s.Rural);
        int ruralZones = !hasRural ? 0 : !hasUrban ? target : Math.Clamp((int)Math.Round(target * ruralPop / (double)totalPop), 1, Math.Max(1, target - 1));
        int urbanZones = hasUrban ? Math.Max(1, target - ruralZones) : 0;

        // Unidades iniciais: bairros (urbanos) ou setores; cada setor aponta para o grupo em que está.
        var clusters = new Dictionary<int, Cluster>();
        var owner = new int[setores.Count];
        var byUnit = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < setores.Count; i++)
        {
            Setor s = setores[i];
            string unit = !s.Rural && s.Bairro is not null ? "b" + s.Bairro : "s" + s.Code;
            if (!byUnit.TryGetValue(unit, out int id))
            {
                id = clusters.Count;
                byUnit[unit] = id;
                clusters[id] = new Cluster { Id = id, Rural = s.Rural };
            }

            clusters[id].Setores.Add(s);
            owner[i] = id;
        }

        foreach (Cluster c in clusters.Values)
        {
            Recenter(c);
        }

        for (int i = 0; i < setores.Count; i++)
        {
            foreach (int j in adjacency[i].Where(j => owner[j] != owner[i]))
            {
                clusters[owner[i]].Neighbors.Add(owner[j]);
            }
        }

        Merge(clusters, rural: false, urbanZones);
        Merge(clusters, rural: true, ruralZones);

        var final = clusters.Values.OrderByDescending(c => c.Pop).ThenBy(c => c.Id).ToList();
        var names = Names(final, setores, config);
        var zoneOf = new Dictionary<int, string>();
        for (int i = 0; i < final.Count; i++)
        {
            zoneOf[final[i].Id] = $"{m.CodigoIbge}-{i + 1:00}";
        }

        // Contornos: união dos setores de cada zona e simplificação por cobertura com orçamento de vértices.
        Geometry[] unions = final.Select(c => Fix(UnaryUnionOp.Union(c.Setores.Select(s => s.Geometry!).ToList()))).ToArray();
        var (simplified, tolerance) = Simplify(unions, final.Count * config.VerticesPorZona, config);
        var reducer = new PrecisionModel(GeoImport.Scale);

        long[] idadeMunicipio = Sum(setores.Where(s => s.Idade is not null).Select(s => s.Idade!));
        double? rendaMunicipio = Mean(setores);
        var zonas = new List<Zona>();
        var geo = new List<GeoZona>();
        for (int i = 0; i < final.Count; i++)
        {
            Cluster c = final[i];
            string id = zoneOf[c.Id];
            Geometry reduced = GeometryPrecisionReducer.Reduce(simplified[i], reducer);
            if (reduced.IsEmpty)
            {
                reduced = GeometryPrecisionReducer.Reduce(unions[i], reducer);
            }

            geo.Add(new GeoZona { Id = id, Polygons = GeoImport.Polygons(reduced, m.CodigoIbge) });
            Point inside = unions[i].InteriorPoint;

            double idadeCobertura = c.Pop == 0 ? 0 : c.Setores.Where(s => s.Idade is not null).Sum(s => s.Pop) / (double)c.Pop;
            bool idadeZona = c.Pop > 0 && idadeCobertura >= config.CoberturaMinima;
            double rendaCobertura = c.Pop == 0 ? 0 : c.Setores.Where(s => s.Renda is not null).Sum(s => s.Pop) / (double)c.Pop;
            bool rendaZona = c.Pop > 0 && rendaCobertura >= config.CoberturaMinima && Mean(c.Setores) is not null;
            if (!idadeZona)
            {
                lacunas.Add($"{id} idade (cobertura {idadeCobertura:P0})");
            }

            if (!rendaZona)
            {
                lacunas.Add($"{id} renda (cobertura {rendaCobertura:P0})");
            }

            long[] idade = idadeZona ? Sum(c.Setores.Where(s => s.Idade is not null).Select(s => s.Idade!)) : idadeMunicipio;
            zonas.Add(new Zona
            {
                Id = id,
                Nome = names[c.Id].Nome,
                NomeFonte = names[c.Id].Fonte,
                Situacao = c.Rural ? Zona.Rural : Zona.Urbana,
                Populacao = c.Pop,
                Domicilios = c.Setores.Sum(s => s.Dom),
                Setores = c.Setores.Count,
                AreaKm2 = Math.Round(c.Setores.Sum(s => s.AreaKm2), 3),
                Lat = Math.Round(inside.Y, 5),
                Lon = Math.Round(inside.X, 5),
                Vizinhas = c.Neighbors.Where(zoneOf.ContainsKey).Select(n => zoneOf[n]).Order(StringComparer.Ordinal).ToList(),
                Perfil = new PerfilZona
                {
                    Idade = FaixasIdade.Select((f, k) => (f.Key, Value: idade.Length > k ? idade[k] : 0)).ToDictionary(p => p.Key, p => p.Value),
                    IdadeCobertura = Math.Round(idadeCobertura, 4),
                    IdadeFonte = idadeZona ? PerfilZona.FonteZona : PerfilZona.FonteMunicipio,
                    RendaResponsavel = rendaZona ? Mean(c.Setores) : rendaMunicipio,
                    RendaCobertura = Math.Round(rendaCobertura, 4),
                    RendaFonte = rendaZona ? PerfilZona.FonteZona : PerfilZona.FonteMunicipio,
                },
            });
        }

        return (zonas, geo, tolerance);
    }

    private static (Zona Zona, GeoZona Geo) WholeMunicipio(string dataDirectory, Municipio m)
    {
        GeoUf geo = GeoContent.Load(dataDirectory, m.Uf).GetValueOrThrow();
        GeoMunicipio contorno = geo.Municipios.Single(g => g.CodigoIbge == m.CodigoIbge);
        string id = $"{m.CodigoIbge}-01";
        var zona = new Zona
        {
            Id = id,
            Nome = "Município inteiro",
            NomeFonte = Zona.NomeGerado,
            Situacao = m.UrbanaPct >= 50 ? Zona.Urbana : Zona.Rural,
            Populacao = m.Perfil.PopulacaoCenso,
            Domicilios = 0,
            Setores = 1,
            AreaKm2 = m.AreaKm2,
            Lat = m.Lat,
            Lon = m.Lon,
            Vizinhas = [],
            Perfil = new PerfilZona
            {
                Idade = FaixasIdade.ToDictionary(f => f.Key, _ => 0L),
                IdadeCobertura = 0,
                IdadeFonte = PerfilZona.FonteMunicipio,
                RendaResponsavel = null,
                RendaCobertura = 0,
                RendaFonte = PerfilZona.FonteMunicipio,
            },
        };
        return (zona, new GeoZona { Id = id, Polygons = contorno.Polygons });
    }

    private static long[] Sum(IEnumerable<long[]> rows)
    {
        var total = new long[FaixasIdade.Length];
        foreach (long[] row in rows)
        {
            for (int k = 0; k < total.Length; k++)
            {
                total[k] += row[k];
            }
        }

        return total;
    }

    /// <summary>Rendimento médio do responsável, média dos setores pesada pelo número de responsáveis.</summary>
    private static double? Mean(IEnumerable<Setor> setores)
    {
        double weight = 0, sum = 0;
        foreach (var (media, responsaveis) in setores.Where(s => s.Renda is not null).Select(s => s.Renda!.Value))
        {
            sum += media * responsaveis;
            weight += responsaveis;
        }

        return weight <= 0 ? null : Math.Round(sum / weight, 2);
    }

    private static Geometry Fix(Geometry g) => g.IsValid ? g : GeometryFixer.Fix(g);

    /// <summary>Vizinhança por divisa comum: dois setores são vizinhos se compartilham dois ou mais vértices.</summary>
    private static List<HashSet<int>> Adjacency(List<Setor> setores)
    {
        var byVertex = new Dictionary<(long, long), List<int>>();
        for (int i = 0; i < setores.Count; i++)
        {
            foreach (Coordinate c in setores[i].Geometry!.Coordinates)
            {
                var key = ((long)Math.Round(c.X * 1e6), (long)Math.Round(c.Y * 1e6));
                if (!byVertex.TryGetValue(key, out List<int>? list))
                {
                    byVertex[key] = list = [];
                }

                if (list.Count == 0 || list[^1] != i)
                {
                    list.Add(i);
                }
            }
        }

        var shared = new Dictionary<(int, int), int>();
        foreach (List<int> list in byVertex.Values.Where(l => l.Count > 1))
        {
            for (int a = 0; a < list.Count; a++)
            {
                for (int b = a + 1; b < list.Count; b++)
                {
                    var key = list[a] < list[b] ? (list[a], list[b]) : (list[b], list[a]);
                    shared[key] = shared.GetValueOrDefault(key) + 1;
                }
            }
        }

        var adjacency = setores.Select(_ => new HashSet<int>()).ToList();
        foreach (var ((a, b), count) in shared.Where(p => p.Value >= 2))
        {
            adjacency[a].Add(b);
            adjacency[b].Add(a);
        }

        return adjacency;
    }

    private static void Recenter(Cluster c)
    {
        c.Pop = c.Setores.Sum(s => s.Pop);
        double weight = c.Setores.Sum(s => Math.Max(1, s.Pop));
        c.X = c.Setores.Sum(s => s.Center!.X * Math.Max(1, s.Pop)) / weight;
        c.Y = c.Setores.Sum(s => s.Center!.Y * Math.Max(1, s.Pop)) / weight;
    }

    /// <summary>Junta o grupo de menor população ao vizinho do mesmo tipo de menor população, até restarem <paramref name="count"/>.</summary>
    private static void Merge(Dictionary<int, Cluster> clusters, bool rural, int count)
    {
        var queue = new SortedSet<(long Pop, int Id)>(clusters.Values.Where(c => c.Rural == rural).Select(c => (c.Pop, c.Id)));
        while (queue.Count > Math.Max(count, 1))
        {
            var (_, id) = queue.Min;
            queue.Remove(queue.Min);
            Cluster small = clusters[id];
            Cluster? into = small.Neighbors.Select(n => clusters[n]).Where(n => n.Rural == rural).OrderBy(n => n.Pop).ThenBy(n => n.Id).FirstOrDefault()
                ?? clusters.Values.Where(c => c.Rural == rural && c.Id != id).OrderBy(c => Distance(c, small)).ThenBy(c => c.Id).First();

            queue.Remove((into.Pop, into.Id));
            into.Setores.AddRange(small.Setores);
            foreach (int n in small.Neighbors.Where(n => n != into.Id))
            {
                into.Neighbors.Add(n);
                clusters[n].Neighbors.Remove(id);
                clusters[n].Neighbors.Add(into.Id);
            }

            into.Neighbors.Remove(id);
            clusters.Remove(id);
            Recenter(into);
            queue.Add((into.Pop, into.Id));
        }
    }

    private static double Distance(Cluster a, Cluster b) => Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2);

    private static Dictionary<int, (string Nome, string Fonte)> Names(List<Cluster> clusters, List<Setor> setores, TerritoryConfig config)
    {
        // Sede: o distrito com mais população urbana. Centro: a zona urbana mais perto do centro populacional urbano.
        var urbanSetores = setores.Where(s => !s.Rural).ToList();
        string sede = (urbanSetores.Count > 0 ? urbanSetores : setores).GroupBy(s => s.Distrito).MaxBy(g => g.Sum(s => s.Pop))!.Key;
        var reference = urbanSetores.Count > 0 ? urbanSetores : setores;
        double weight = reference.Sum(s => Math.Max(1, s.Pop));
        double cx = reference.Sum(s => s.Center!.X * Math.Max(1, s.Pop)) / weight;
        double cy = reference.Sum(s => s.Center!.Y * Math.Max(1, s.Pop)) / weight;
        Cluster? centro = clusters.Where(c => !c.Rural).OrderBy(c => Math.Pow(c.X - cx, 2) + Math.Pow(c.Y - cy, 2)).FirstOrDefault();

        var result = new Dictionary<int, (string, string)>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Cluster c in clusters)
        {
            (string Nome, string Fonte) name;
            var bairros = c.Setores.Where(s => s.BairroNome is not null).GroupBy(s => s.BairroNome!).OrderByDescending(g => g.Sum(s => s.Pop)).ToList();
            var distrito = c.Setores.GroupBy(s => (s.Distrito, s.DistritoNome)).MaxBy(g => g.Sum(s => Math.Max(1, s.Pop)))!;
            bool dominante = distrito.Sum(s => Math.Max(1, s.Pop)) >= config.DistritoDominante * c.Setores.Sum(s => Math.Max(1, s.Pop));
            string direction = Direction(c.X - cx, c.Y - cy);
            if (!c.Rural && bairros.Count > 0)
            {
                name = (Clean(bairros[0].Key), Zona.NomeBairro);
            }
            else if (dominante && distrito.Key.Distrito != sede && !string.IsNullOrWhiteSpace(distrito.Key.DistritoNome))
            {
                name = (Clean(distrito.Key.DistritoNome) + (c.Rural && clusters.Any(o => o != c && !o.Rural && o.Setores.Any(s => s.Distrito == distrito.Key.Distrito)) ? " (rural)" : ""), Zona.NomeDistrito);
            }
            else if (c.Rural)
            {
                name = (clusters.Count(o => o.Rural) == 1 ? "Zona Rural" : "Rural " + direction, Zona.NomeGerado);
            }
            else
            {
                name = (c == centro ? "Centro" : "Zona " + direction, Zona.NomeGerado);
            }

            int n = used.GetValueOrDefault(name.Nome) + 1;
            used[name.Nome] = n;
            result[c.Id] = n == 1 ? name : ($"{name.Nome} {n}", name.Fonte);
        }

        return result;
    }

    private static string Clean(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static string Direction(double dx, double dy)
    {
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        int sector = (int)Math.Round(((angle + 360) % 360) / 45.0) % 8;
        return Direcoes[sector];
    }

    /// <summary>
    /// Simplificação por cobertura com tolerância proporcional ao tamanho de cada zona (raiz da área do retângulo
    /// envolvente): a zona rural enorme e o bairro pequeno ficam com detalhe parecido. O fator cresce até caber no
    /// orçamento de vértices do município. Devolve a mediana das tolerâncias usadas.
    /// </summary>
    private static (Geometry[] Result, double Tolerance) Simplify(Geometry[] coverage, int budget, TerritoryConfig config)
    {
        double[] sizes = coverage.Select(g => Math.Sqrt(Math.Max(1e-12, g.EnvelopeInternal.Area))).ToArray();
        double factor = 0.004;
        while (true)
        {
            double[] tolerances = sizes.Select(size => Math.Clamp(size * factor, config.ToleranciaInicialGraus / 10, config.ToleranciaMaximaGraus)).ToArray();
            Geometry[] result;
            try
            {
                result = CoverageSimplifier.Simplify(coverage, tolerances);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or TopologyException)
            {
                result = coverage.Select((g, k) => TopologyPreservingSimplifier.Simplify(g, tolerances[k])).ToArray();
            }

            int vertices = result.Sum(g => g.NumPoints);
            if (vertices <= budget || tolerances.All(t => t >= config.ToleranciaMaximaGraus))
            {
                return (result, tolerances.Order().ElementAt(tolerances.Length / 2));
            }

            factor *= 1.4;
        }
    }

    /// <summary>Um município por linha, como em data/municipios/.</summary>
    private static string SerializeZonas(ZonasUf file, JsonSerializerOptions options)
    {
        var compact = new JsonSerializerOptions(options) { WriteIndented = false };
        var sb = new StringBuilder("{\n");
        sb.Append($"  \"uf\": {JsonSerializer.Serialize(file.Uf, compact)},\n");
        sb.Append($"  \"fonte\": {JsonSerializer.Serialize(file.Fonte, compact)},\n");
        sb.Append($"  \"metodo\": {JsonSerializer.Serialize(file.Metodo, compact)},\n");
        sb.Append("  \"municipios\": [\n");
        for (int i = 0; i < file.Municipios.Count; i++)
        {
            sb.Append("    ").Append(JsonSerializer.Serialize(file.Municipios[i], compact)).Append(i < file.Municipios.Count - 1 ? ",\n" : "\n");
        }

        return sb.Append("  ]\n}\n").ToString();
    }

    private static string SerializeGeo(GeoZonasUf geo)
    {
        var options = new JsonSerializerOptions(JsonData.Options) { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var sb = new StringBuilder("{\n");
        sb.Append($"  \"uf\": {JsonSerializer.Serialize(geo.Uf, options)},\n");
        sb.Append($"  \"source\": {JsonSerializer.Serialize(geo.Source, options)},\n");
        sb.Append($"  \"method\": {JsonSerializer.Serialize(geo.Method, options)},\n");
        sb.Append(CultureInfo.InvariantCulture, $"  \"scale\": {geo.Scale},\n");
        sb.Append("  \"zonas\": [\n");
        for (int i = 0; i < geo.Zonas.Count; i++)
        {
            sb.Append("    ").Append(JsonSerializer.Serialize(geo.Zonas[i], options)).Append(i < geo.Zonas.Count - 1 ? ",\n" : "\n");
        }

        return sb.Append("  ]\n}\n").ToString();
    }
}
