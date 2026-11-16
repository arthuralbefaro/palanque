namespace Planalto.Core.Geo;

/// <summary>
/// Projeção equiretangular local de uma UF em quilômetros, centrada no retângulo envolvente. Basta para o mapa
/// de um estado: a distorção de forma fica abaixo de alguns por cento mesmo nas UFs mais altas.
/// </summary>
public sealed class GeoProjection
{
    public const double KmPerDegree = 111.32;

    private readonly double _cosLat;

    public GeoProjection(double minLon, double minLat, double maxLon, double maxLat)
    {
        CenterLon = (minLon + maxLon) / 2;
        CenterLat = (minLat + maxLat) / 2;
        _cosLat = Math.Cos(CenterLat * Math.PI / 180);
        (double x0, double y0) = Project(minLon, minLat);
        (double x1, double y1) = Project(maxLon, maxLat);
        WidthKm = x1 - x0;
        HeightKm = y1 - y0;
    }

    public double CenterLon { get; }

    public double CenterLat { get; }

    public double WidthKm { get; }

    public double HeightKm { get; }

    /// <summary>Leste positivo em X, norte positivo em Y.</summary>
    public (double X, double Y) Project(double lon, double lat) =>
        ((lon - CenterLon) * _cosLat * KmPerDegree, (lat - CenterLat) * KmPerDegree);

    public static GeoProjection For(GeoUf geo)
    {
        double minLon = double.MaxValue, minLat = double.MaxValue, maxLon = double.MinValue, maxLat = double.MinValue;
        foreach (List<int> ring in geo.Municipios.SelectMany(m => m.Polygons).Select(p => p.Outer))
        {
            for (int i = 0; i + 1 < ring.Count; i += 2)
            {
                double lon = (double)ring[i] / geo.Scale, lat = (double)ring[i + 1] / geo.Scale;
                minLon = Math.Min(minLon, lon);
                maxLon = Math.Max(maxLon, lon);
                minLat = Math.Min(minLat, lat);
                maxLat = Math.Max(maxLat, lat);
            }
        }

        return new GeoProjection(minLon, minLat, maxLon, maxLat);
    }
}
