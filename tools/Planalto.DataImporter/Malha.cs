using System.Globalization;
using System.IO.Compression;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using NetTopologySuite.IO.Esri;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter;

public sealed record PontoMunicipio(double Lat, double Lon, double AreaKm2, string Metodo);

/// <summary>
/// Coordenadas e área a partir da Malha Municipal do IBGE (shapefile, SIRGAS 2000, graus).
/// O ponto é o centroide do polígono; se ele cair fora (formas irregulares), usa um ponto interior.
/// </summary>
public static class Malha
{
    public const int Ano = 2025;
    public const string Url =
        "https://geoftp.ibge.gov.br/organizacao_do_territorio/malhas_territoriais/malhas_municipais/municipio_2025/Brasil/BR_Municipios_2025.zip";

    /// <summary>5 casas decimais em graus: cerca de 1 metro.</summary>
    public const int CoordinateDecimals = 5;

    public const string MetodoDescricao =
        "Centroide do polígono do município na Malha Municipal 2025 (SIRGAS 2000, graus), arredondado a 5 casas; "
        + "quando o centroide arredondado cai fora do polígono, usa-se um ponto interior (InteriorPoint). "
        + "Todo ponto gravado foi conferido dentro do polígono do próprio município. Área (km²) do campo AREA_KM2 da mesma malha.";

    private static readonly GeometryFactory Factory = new();

    /// <summary>Lê a malha do ZIP (extraído uma vez dentro do cache) e calcula o ponto de cada município.</summary>
    public static Dictionary<int, PontoMunicipio> Read(string zipPath)
    {
        string extractDirectory = Path.Combine(Path.GetDirectoryName(zipPath)!, Path.GetFileNameWithoutExtension(zipPath));
        string? shp = Directory.Exists(extractDirectory)
            ? Directory.EnumerateFiles(extractDirectory, "*.shp", SearchOption.AllDirectories).FirstOrDefault()
            : null;
        if (shp is null)
        {
            Console.WriteLine($"  extraindo {Path.GetFileName(zipPath)}");
            ZipFile.ExtractToDirectory(zipPath, extractDirectory, overwriteFiles: true);
            shp = Directory.EnumerateFiles(extractDirectory, "*.shp", SearchOption.AllDirectories).FirstOrDefault()
                ?? throw new ImportException($"{Path.GetFileName(zipPath)}: nenhum .shp dentro do ZIP.");
        }

        var result = new Dictionary<int, PontoMunicipio>();
        foreach (var feature in Shapefile.ReadAllFeatures(shp))
        {
            string rawCode = Convert.ToString(feature.Attributes["CD_MUN"], CultureInfo.InvariantCulture) ?? string.Empty;
            if (!int.TryParse(rawCode, NumberStyles.None, CultureInfo.InvariantCulture, out int code))
            {
                throw new ImportException($"Malha: código de município inválido '{rawCode}'.");
            }

            double area = Convert.ToDouble(feature.Attributes["AREA_KM2"], CultureInfo.InvariantCulture);
            var (lat, lon, metodo) = RepresentativePoint(feature.Geometry, $"{code}");

            if (!result.TryAdd(code, new PontoMunicipio(lat, lon, area, metodo)))
            {
                throw new ImportException($"Malha: município {code} repetido.");
            }
        }

        return result;
    }

    /// <summary>Centroide arredondado se estiver dentro do polígono; senão, ponto interior arredondado.</summary>
    public static (double Lat, double Lon, string Metodo) RepresentativePoint(Geometry geometry, string where)
    {
        // Polígonos com pequenos defeitos topológicos são corrigidos só para o teste de inclusão.
        Geometry area = geometry.IsValid ? geometry : GeometryFixer.Fix(geometry);

        Point centroid = Rounded(area.Centroid);
        if (area.Contains(centroid))
        {
            return (centroid.Y, centroid.X, Municipio.CoordenadaCentroide);
        }

        Point interior = Rounded(area.InteriorPoint);
        if (area.Contains(interior))
        {
            return (interior.Y, interior.X, Municipio.CoordenadaPontoInterior);
        }

        throw new ImportException($"Malha: {where}: nem o centroide nem o ponto interior arredondados ficam dentro do polígono.");
    }

    private static Point Rounded(Point p) =>
        Factory.CreatePoint(new Coordinate(
            Math.Round(p.X, CoordinateDecimals, MidpointRounding.AwayFromZero),
            Math.Round(p.Y, CoordinateDecimals, MidpointRounding.AwayFromZero)));
}
