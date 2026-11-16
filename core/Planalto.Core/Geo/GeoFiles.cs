using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.Core.Geo;

/// <summary>
/// Contornos simplificados dos municípios de uma UF (GDD 13.1), gerados pelo DataImporter a partir da Malha
/// Municipal do IBGE. Coordenadas inteiras: graus multiplicados por <see cref="Scale"/>.
/// </summary>
public sealed class GeoUf : IValidatable
{
    public required string Uf { get; init; }

    public required string Source { get; init; }

    public required string Method { get; init; }

    /// <summary>Quantas unidades inteiras há em um grau (10.000: cerca de 11 m).</summary>
    public required int Scale { get; init; }

    public required List<GeoMunicipio> Municipios { get; init; }

    /// <summary>Só a estrutura; a conferência com os municípios é <see cref="GeoContent.Validate"/>.</summary>
    public IEnumerable<string> Validate()
    {
        if (Scale <= 0)
        {
            yield return "scale deve ser positivo.";
        }

        if (Municipios.Count == 0)
        {
            yield return "sem municípios.";
        }
    }
}

public sealed class GeoMunicipio
{
    public required int CodigoIbge { get; init; }

    /// <summary>Partes do município (ilhas e áreas separadas são partes próprias).</summary>
    public required List<GeoPolygon> Polygons { get; init; }
}

/// <summary>
/// Um polígono com furos. Anéis em lista plana lon,lat,lon,lat…, sem repetir o primeiro ponto. Os triângulos da
/// face de cima indexam os vértices do contorno externo seguidos dos vértices de cada furo, na ordem.
/// </summary>
public sealed class GeoPolygon
{
    public required List<int> Outer { get; init; }

    public List<List<int>> Holes { get; init; } = [];

    public required List<int> Triangles { get; init; }

    public int VertexCount => (Outer.Count + Holes.Sum(h => h.Count)) / 2;

    /// <summary>Vértice i (em graus) na numeração dos triângulos.</summary>
    public (double Lon, double Lat) Vertex(int index, int scale)
    {
        List<int> ring = Outer;
        int i = index;
        int h = 0;
        while (i >= ring.Count / 2)
        {
            i -= ring.Count / 2;
            ring = Holes[h++];
        }

        return ((double)ring[2 * i] / scale, (double)ring[(2 * i) + 1] / scale);
    }

    /// <summary>Área (graus²) do contorno externo menos os furos.</summary>
    public double Area(int scale) => Math.Abs(RingArea(Outer, scale)) - Holes.Sum(hole => Math.Abs(RingArea(hole, scale)));

    /// <summary>Área (graus²) somada dos triângulos.</summary>
    public double TriangleArea(int scale)
    {
        double total = 0;
        for (int t = 0; t + 2 < Triangles.Count; t += 3)
        {
            var (ax, ay) = Vertex(Triangles[t], scale);
            var (bx, by) = Vertex(Triangles[t + 1], scale);
            var (cx, cy) = Vertex(Triangles[t + 2], scale);
            total += Math.Abs(((bx - ax) * (cy - ay)) - ((cx - ax) * (by - ay))) / 2;
        }

        return total;
    }

    public static double RingArea(IReadOnlyList<int> ring, int scale)
    {
        double sum = 0;
        int n = ring.Count / 2;
        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            sum += ((double)ring[2 * i] * ring[(2 * j) + 1]) - ((double)ring[2 * j] * ring[(2 * i) + 1]);
        }

        return sum / 2 / ((double)scale * scale);
    }
}

public static class GeoPaths
{
    public const string Directory = "geo";

    public static string File(string dataDirectory, string uf) => Path.Combine(dataDirectory, Directory, $"{uf}.json");
}

public static class GeoContent
{
    /// <summary>Tolerância entre a área dos triângulos e a do polígono (arredondamento das coordenadas).</summary>
    public const double AreaTolerance = 0.02;

    public static DataLoadResult<GeoUf> Load(string dataDirectory, string uf) => JsonData.LoadFile<GeoUf>(GeoPaths.File(dataDirectory, uf));

    /// <summary>Confere a UF, a presença de cada município uma vez, os índices e se os triângulos cobrem o polígono.</summary>
    public static IEnumerable<string> Validate(GeoUf geo, MunicipiosDataSet municipios, string expectedUf)
    {
        if (geo.Uf != expectedUf)
        {
            yield return $"geo/{expectedUf}.json: UF '{geo.Uf}' no arquivo.";
        }

        if (geo.Scale <= 0)
        {
            yield return $"geo/{expectedUf}.json: escala inválida.";
            yield break;
        }

        var expected = municipios.All.Where(m => m.Uf == expectedUf).Select(m => m.CodigoIbge).ToHashSet();
        var seen = new HashSet<int>();
        foreach (GeoMunicipio m in geo.Municipios)
        {
            string where = $"geo/{expectedUf}.json: {m.CodigoIbge}";
            if (!expected.Contains(m.CodigoIbge))
            {
                yield return $"{where}: município que não é da UF.";
            }

            if (!seen.Add(m.CodigoIbge))
            {
                yield return $"{where}: repetido.";
            }

            if (m.Polygons.Count == 0)
            {
                yield return $"{where}: sem polígonos.";
            }

            foreach (GeoPolygon p in m.Polygons)
            {
                if (p.Outer.Count < 6 || p.Outer.Count % 2 != 0 || p.Holes.Any(h => h.Count < 6 || h.Count % 2 != 0))
                {
                    yield return $"{where}: anel com menos de 3 vértices ou coordenada sem par.";
                    continue;
                }

                if (p.Triangles.Count == 0 || p.Triangles.Count % 3 != 0 || p.Triangles.Any(i => i < 0 || i >= p.VertexCount))
                {
                    yield return $"{where}: triângulos inválidos.";
                    continue;
                }

                double area = p.Area(geo.Scale);
                double triangles = p.TriangleArea(geo.Scale);
                if (area <= 0 || Math.Abs(triangles - area) > AreaTolerance * area)
                {
                    yield return $"{where}: triângulos cobrem {triangles:E3} de {area:E3} graus².";
                }
            }
        }

        foreach (int missing in expected.Except(seen).Order())
        {
            yield return $"geo/{expectedUf}.json: falta o município {missing}.";
        }
    }
}
