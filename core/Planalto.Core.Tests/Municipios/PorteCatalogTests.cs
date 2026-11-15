using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.Core.Tests.Municipios;

public class PorteCatalogTests
{
    private static PorteCatalog LoadProjectPortes()
    {
        string dataDirectory = DataPaths.FindDataDirectory(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Pasta data/ não encontrada.");
        return JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
    }

    // Limites do GDD 12.4, lidos de data/portes.json: até 20 mil, 100 mil e 500 mil habitantes.
    [Theory]
    [InlineData(0, "pequeno")]
    [InlineData(20_000, "pequeno")]
    [InlineData(20_001, "medio")]
    [InlineData(100_000, "medio")]
    [InlineData(100_001, "grande")]
    [InlineData(500_000, "grande")]
    [InlineData(500_001, "metropole")]
    [InlineData(12_000_000, "metropole")]
    public void Classify_UsaOsLimitesDoArquivo(long populacao, string porte)
    {
        Assert.Equal(porte, LoadProjectPortes().Classify(populacao));
    }

    [Theory]
    [InlineData("""{ "portes": [ { "id": "a", "name": "A", "max_population": 10 }, { "id": "b", "name": "B", "max_population": 20 } ] }""")]
    [InlineData("""{ "portes": [ { "id": "a", "name": "A" }, { "id": "b", "name": "B" } ] }""")]
    [InlineData("""{ "portes": [ { "id": "a", "name": "A", "max_population": 20 }, { "id": "b", "name": "B", "max_population": 10 }, { "id": "c", "name": "C" } ] }""")]
    [InlineData("""{ "portes": [ { "id": "a", "name": "A", "max_population": 10 }, { "id": "a", "name": "B" } ] }""")]
    public void FaixasInvalidas_SaoReportadas(string json)
    {
        Assert.False(JsonData.Parse<PorteCatalog>(json, "portes.json").IsValid);
    }
}
