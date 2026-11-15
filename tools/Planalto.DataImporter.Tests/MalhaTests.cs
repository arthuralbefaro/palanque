using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter.Tests;

public class MalhaTests
{
    private static readonly GeometryFactory Factory = new();

    private static Polygon Polygon(params (double X, double Y)[] points) =>
        Factory.CreatePolygon(points.Append(points[0]).Select(p => new Coordinate(p.X, p.Y)).ToArray());

    [Fact]
    public void PoligonoConvexo_UsaOCentroide()
    {
        Polygon quadrado = Polygon((-50, -10), (-49, -10), (-49, -9), (-50, -9));

        var (lat, lon, metodo) = Malha.RepresentativePoint(quadrado, "quadrado");

        Assert.Equal(Municipio.CoordenadaCentroide, metodo);
        Assert.Equal(-9.5, lat);
        Assert.Equal(-49.5, lon);
    }

    [Fact]
    public void PoligonoEmU_CentroideCaiForaEUsaPontoInterior()
    {
        // Formato de U: o centroide fica no vão entre as duas hastes.
        Polygon u = Polygon((0, 0), (3, 0), (3, 3), (2, 3), (2, 1), (1, 1), (1, 3), (0, 3));
        Assert.False(u.Contains(u.Centroid));

        var (lat, lon, metodo) = Malha.RepresentativePoint(u, "u");

        Assert.Equal(Municipio.CoordenadaPontoInterior, metodo);
        Assert.True(u.Contains(Factory.CreatePoint(new Coordinate(lon, lat))));
    }

    [Fact]
    public void IlhasSeparadas_PontoFicaDentroDeUmaDelas()
    {
        MultiPolygon ilhas = Factory.CreateMultiPolygon(
        [
            Polygon((0, 0), (1, 0), (1, 1), (0, 1)),
            Polygon((10, 0), (12, 0), (12, 2), (10, 2)),
        ]);

        var (lat, lon, metodo) = Malha.RepresentativePoint(ilhas, "ilhas");

        Assert.Equal(Municipio.CoordenadaPontoInterior, metodo);
        Assert.True(ilhas.Contains(Factory.CreatePoint(new Coordinate(lon, lat))));
    }

    /// <summary>
    /// Confere os dados gerados contra a malha do cache: todo município de data/municipios tem
    /// polígono e o ponto gravado fica dentro dele. Precisa do cache (rode o importador antes).
    /// </summary>
    [Fact]
    public void DadosGerados_TodoPontoDentroDoPoligonoDoProprioMunicipio()
    {
        string dataDirectory = DataPaths.FindDataDirectory(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Pasta data/ não encontrada.");
        string shp = Path.Combine(Directory.GetParent(dataDirectory)!.FullName,
            "tools", "Planalto.DataImporter", "cache", $"BR_Municipios_{Malha.Ano}", $"BR_Municipios_{Malha.Ano}.shp");
        Assert.True(File.Exists(shp), $"Malha não encontrada em {shp}. Rode o Planalto.DataImporter para preencher o cache.");

        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        var (dataSet, errors) = MunicipiosDataSet.Load(dataDirectory, portes);
        Assert.Empty(errors);

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var poligonos = Shapefile.ReadAllFeatures(shp).ToDictionary(
            f => int.Parse(Convert.ToString(f.Attributes["CD_MUN"])!),
            f => f.Geometry.IsValid ? f.Geometry : NetTopologySuite.Geometries.Utilities.GeometryFixer.Fix(f.Geometry));

        List<Municipio> municipios = dataSet!.All.ToList();
        Assert.Equal(dataSet.Fontes.TotaisOficiais.TotalMunicipios, municipios.Count);

        var fora = municipios
            .Where(m => !poligonos.TryGetValue(m.CodigoIbge, out Geometry? g) || !g.Contains(Factory.CreatePoint(new Coordinate(m.Lon, m.Lat))))
            .Select(m => $"{m.CodigoIbge} {m.Nome}")
            .ToList();

        Assert.True(fora.Count == 0, "Pontos fora do polígono: " + string.Join(", ", fora.Take(20)));
    }
}
