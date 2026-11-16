using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using NetTopologySuite.Algorithm;
using NetTopologySuite.Coverage;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.Precision;
using NetTopologySuite.Simplify;
using NetTopologySuite.Triangulate.Polygon;
using Planalto.Core.Data;
using Planalto.Core.Geo;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter.Geo;

/// <summary>
/// Modo "geo": contornos simplificados dos municípios para o mapa 2.5D (GDD 13.1), um arquivo por UF em data/geo/.
/// A simplificação é feita sobre a cobertura da UF inteira, para que vizinhos continuem com a mesma divisa.
/// </summary>
public static class GeoImport
{
    /// <summary>Tolerância de simplificação, em graus (0,01° ≈ 1,1 km; cerca de 1 pixel na visão de MG).</summary>
    public const double Tolerance = 0.01;

    /// <summary>Coordenadas gravadas como inteiros: graus × 10.000 (≈ 11 m).</summary>
    public const int Scale = 10_000;

    /// <summary>Partes menores que esta fração da área do município (ilhotas) são descartadas; a maior sempre fica.</summary>
    public const double MinPartFraction = 0.002;

    public static string Method =>
        string.Create(CultureInfo.InvariantCulture, $"Polígonos da Malha Municipal {Malha.Ano} simplificados por cobertura da UF (CoverageSimplifier, tolerância {Tolerance}°), ")
        + string.Create(CultureInfo.InvariantCulture, $"coordenadas arredondadas a 1/{Scale} de grau, partes menores que {MinPartFraction * 100}% da área do município descartadas ")
        + "(a maior parte sempre fica) e face de cima triangulada por recorte de orelhas com furos (PolygonTriangulator).";

    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory)
    {
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        MunicipiosDataSet municipios = MunicipiosDataSet.Load(dataDirectory, portes).DataSet
            ?? throw new ImportException("data/municipios/ inválido; rode o importador de municípios antes.");
        var ufOf = municipios.All.ToDictionary(m => m.CodigoIbge, m => m.Uf);

        Console.WriteLine($"IBGE — Malha Municipal {Malha.Ano} (contornos)");
        CachedFile file = await cache.GetAsync(Path.GetFileName(new Uri(Malha.Url).LocalPath), Malha.Url);
        string shp = Malha.ExtractedShapefile(file.Path);

        var byUf = new SortedDictionary<string, List<(int Code, Geometry Geometry)>>(StringComparer.Ordinal);
        foreach (var feature in Shapefile.ReadAllFeatures(shp))
        {
            int code = Malha.Code(feature.Attributes["CD_MUN"]);
            if (!ufOf.TryGetValue(code, out string? uf))
            {
                // A malha traz áreas que não são municípios (Lagoa dos Patos e Lagoa Mirim, 4300001 e 4300002).
                Console.WriteLine($"  {code} ({feature.Attributes["NM_MUN"]}): fora de data/municipios/, ignorado.");
                continue;
            }

            Geometry g = feature.Geometry.IsValid ? feature.Geometry : GeometryFixer.Fix(feature.Geometry);
            byUf.TryAdd(uf, []);
            byUf[uf].Add((code, g));
        }

        string directory = Path.Combine(dataDirectory, GeoPaths.Directory);
        Directory.CreateDirectory(directory);
        string source = $"IBGE, Malha Municipal {Malha.Ano} ({Malha.Url})";
        long totalBytes = 0;
        int totalVertices = 0;
        var errors = new List<string>();
        foreach (var (uf, features) in byUf)
        {
            Geometry[] simplified = Simplify(features.Select(f => f.Geometry).ToArray(), uf);
            var reducer = new PrecisionModel(Scale);
            var list = new List<GeoMunicipio>();
            for (int i = 0; i < features.Count; i++)
            {
                Geometry reduced = GeometryPrecisionReducer.Reduce(simplified[i], reducer);
                if (reduced.IsEmpty)
                {
                    // Município minúsculo que sumiu na simplificação: usa o original, só arredondado.
                    reduced = GeometryPrecisionReducer.Reduce(features[i].Geometry, reducer);
                }

                list.Add(new GeoMunicipio { CodigoIbge = features[i].Code, Polygons = Polygons(reduced, features[i].Code) });
            }

            var geo = new GeoUf { Uf = uf, Source = source, Method = Method, Scale = Scale, Municipios = list.OrderBy(m => m.CodigoIbge).ToList() };
            errors.AddRange(GeoContent.Validate(geo, municipios, uf));
            string path = GeoPaths.File(dataDirectory, uf);
            string json = Serialize(geo);
            await File.WriteAllTextAsync(path, json, new UTF8Encoding(false));
            int vertices = geo.Municipios.Sum(m => m.Polygons.Sum(p => p.VertexCount));
            totalVertices += vertices;
            totalBytes += Encoding.UTF8.GetByteCount(json);
            Console.WriteLine($"  {uf}: {geo.Municipios.Count} municípios, {vertices} vértices, {json.Length / 1024} KB");
        }

        Console.WriteLine($"Total: {totalVertices} vértices, {totalBytes / 1024} KB em data/geo/");
        foreach (string error in errors)
        {
            Console.Error.WriteLine($"  ERRO {error}");
        }

        return errors.Count == 0 ? 0 : 1;
    }

    private static Geometry[] Simplify(Geometry[] coverage, string uf)
    {
        try
        {
            return CoverageSimplifier.Simplify(coverage, Tolerance);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or TopologyException)
        {
            // Se a malha da UF não formar uma cobertura limpa, simplifica cada município sozinho.
            Console.WriteLine($"  {uf}: cobertura não simplificável ({ex.Message}); simplificando por município.");
            return coverage.Select(g => TopologyPreservingSimplifier.Simplify(g, Tolerance)).ToArray();
        }
    }

    internal static List<GeoPolygon> Polygons(Geometry geometry, int code)
    {
        var parts = Enumerable.Range(0, geometry.NumGeometries)
            .Select(geometry.GetGeometryN)
            .OfType<Polygon>()
            .Where(p => !p.IsEmpty && p.Area > 0)
            .OrderByDescending(p => p.Area)
            .ToList();
        if (parts.Count == 0)
        {
            throw new ImportException($"Malha: {code} sem polígono depois da simplificação.");
        }

        double total = parts.Sum(p => p.Area);
        var result = new List<GeoPolygon>();
        foreach (Polygon part in parts.Where((p, i) => i == 0 || p.Area >= MinPartFraction * total))
        {
            result.Add(Triangulate(part, code));
        }

        return result;
    }

    private static GeoPolygon Triangulate(Polygon polygon, int code)
    {
        // Contorno externo anti-horário e furos horários (com norte para cima).
        Coordinate[] outer = Ring(polygon.Shell, counterClockwise: true);
        var holes = Enumerable.Range(0, polygon.NumInteriorRings)
            .Select(i => Ring(polygon.GetInteriorRingN(i), counterClockwise: false))
            .Where(r => r.Length >= 3)
            .ToList();

        var index = new Dictionary<(long, long), int>();
        int next = 0;
        foreach (Coordinate c in holes.Prepend(outer).SelectMany(r => r))
        {
            index.TryAdd(Key(c), next);
            next++;
        }

        Geometry triangles;
        try
        {
            triangles = PolygonTriangulator.Triangulate(polygon);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or TopologyException)
        {
            throw new ImportException($"Malha: {code}: triangulação falhou ({ex.Message}).");
        }

        var list = new List<int>();
        for (int t = 0; t < triangles.NumGeometries; t++)
        {
            Coordinate[] tri = triangles.GetGeometryN(t).Coordinates;
            if (!index.TryGetValue(Key(tri[0]), out int a) || !index.TryGetValue(Key(tri[1]), out int b) || !index.TryGetValue(Key(tri[2]), out int c))
            {
                throw new ImportException($"Malha: {code}: triângulo com vértice fora dos anéis.");
            }

            // Todos os triângulos anti-horários, como o contorno externo.
            double cross = ((tri[1].X - tri[0].X) * (tri[2].Y - tri[0].Y)) - ((tri[2].X - tri[0].X) * (tri[1].Y - tri[0].Y));
            if (cross == 0)
            {
                continue;
            }

            list.AddRange(cross > 0 ? [a, b, c] : [a, c, b]);
        }

        return new GeoPolygon { Outer = Flat(outer), Holes = holes.Select(Flat).ToList(), Triangles = list };
    }

    /// <summary>Anel sem o ponto de fechamento, na orientação pedida.</summary>
    private static Coordinate[] Ring(LineString ring, bool counterClockwise)
    {
        Coordinate[] coordinates = ring.Coordinates;
        bool ccw = Orientation.IsCCW(coordinates);
        Coordinate[] open = coordinates.Take(coordinates.Length - 1).ToArray();
        return ccw == counterClockwise ? open : open.Reverse().ToArray();
    }

    private static (long, long) Key(Coordinate c) => ((long)Math.Round(c.X * Scale), (long)Math.Round(c.Y * Scale));

    private static List<int> Flat(Coordinate[] ring) =>
        ring.SelectMany(c => new[] { (int)Math.Round(c.X * Scale), (int)Math.Round(c.Y * Scale) }).ToList();

    /// <summary>Um município por linha: arquivo compacto, mas com diferenças legíveis no controle de versão.</summary>
    private static string Serialize(GeoUf geo)
    {
        var options = new JsonSerializerOptions(JsonData.Options) { WriteIndented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"uf\": {JsonSerializer.Serialize(geo.Uf, options)},\n");
        sb.Append($"  \"source\": {JsonSerializer.Serialize(geo.Source, options)},\n");
        sb.Append($"  \"method\": {JsonSerializer.Serialize(geo.Method, options)},\n");
        sb.Append(CultureInfo.InvariantCulture, $"  \"scale\": {geo.Scale},\n");
        sb.Append("  \"municipios\": [\n");
        for (int i = 0; i < geo.Municipios.Count; i++)
        {
            sb.Append("    ").Append(JsonSerializer.Serialize(geo.Municipios[i], options)).Append(i < geo.Municipios.Count - 1 ? ",\n" : "\n");
        }

        sb.Append("  ]\n}\n");
        return sb.ToString();
    }
}
