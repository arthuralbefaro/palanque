using Planalto.Core.Data;
using Planalto.Core.Ideology;
using Planalto.Core.Parties;

namespace Planalto.Core.Tests.Parties;

public class FamilyCatalogTests
{
    private static string DataDirectory =>
        DataPaths.FindDataDirectory(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("Pasta data/ não encontrada a partir dos testes.");

    [Fact]
    public void Families_TemAsDezesseisFamiliasDaTabela141()
    {
        FamilyCatalog catalog = JsonData.LoadFile<FamilyCatalog>(Path.Combine(DataDirectory, PartyPaths.Families)).GetValueOrThrow();

        Assert.Equal(16, catalog.Families.Count);
        FamilyDefinition socialista = catalog.Get("esquerda_socialista");
        Assert.Equal(new AxisRange(-100, -70), socialista.Economic);
        Assert.Equal(new AxisRange(-100, -50), socialista.Social);
        Assert.True(catalog.Get("regionalista").HasVariableRange);
        Assert.Single(catalog.Families, f => f.HasVariableRange);
    }

    [Fact]
    public void Families_UsamSoOsDezTemasDaSecao52()
    {
        ThemeCatalog themes = JsonData.LoadFile<ThemeCatalog>(Path.Combine(DataDirectory, "themes.json")).GetValueOrThrow();
        FamilyCatalog families = JsonData.LoadFile<FamilyCatalog>(Path.Combine(DataDirectory, PartyPaths.Families)).GetValueOrThrow();

        Assert.Equal(10, themes.Themes.Count);
        Assert.Empty(families.ValidateAgainst(themes));
    }

    [Fact]
    public void Families_CobremOsDoisLadosDoEixoEconomico()
    {
        FamilyCatalog catalog = JsonData.LoadFile<FamilyCatalog>(Path.Combine(DataDirectory, PartyPaths.Families)).GetValueOrThrow();

        Assert.Contains(catalog.FixedRangeFamilies, f => f.Economic!.Max < 0);
        Assert.Contains(catalog.FixedRangeFamilies, f => f.Economic!.Min > 0);
    }

    [Theory]
    [InlineData("""{ "min": -100, "max": -70 }""", """null""", "os dois fixos ou os dois null")]
    [InlineData("""{ "min": -120, "max": -70 }""", """{ "min": 0, "max": 10 }""", "entre -100 e 100")]
    [InlineData("""{ "min": 10, "max": -10 }""", """{ "min": 0, "max": 10 }""", "'min' maior que 'max'")]
    public void FaixaInvalida_EReportada(string economic, string social, string expected)
    {
        string json = $$"""
            { "families": [ {
              "id": "x", "name": "X", "economic": {{economic}}, "social": {{social}},
              "typical_themes": "t", "themes": [ { "theme": "armas", "position": 1 } ], "weight": 1
            } ] }
            """;

        DataLoadResult<FamilyCatalog> result = JsonData.Parse<FamilyCatalog>(json, "teste.json");

        Assert.Contains(result.Errors, e => e.Contains(expected));
    }

    [Fact]
    public void PosicaoDeTemaForaDeMenosDoisAMaisDois_EReportada()
    {
        const string json = """
            { "families": [ {
              "id": "x", "name": "X", "economic": { "min": 0, "max": 10 }, "social": { "min": 0, "max": 10 },
              "typical_themes": "t", "themes": [ { "theme": "armas", "position": 3 } ], "weight": 1
            } ] }
            """;

        DataLoadResult<FamilyCatalog> result = JsonData.Parse<FamilyCatalog>(json, "teste.json");

        Assert.Contains(result.Errors, e => e.Contains("entre -2 e 2"));
    }
}
