using Planalto.Core.Characters;
using Planalto.Core.Data;

namespace Planalto.Core.Tests.Data;

public class JsonDataTests
{
    private static string DataDirectory =>
        DataPaths.FindDataDirectory(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("Pasta data/ não encontrada a partir dos testes.");

    [Fact]
    public void ArquivosDoProjeto_SaoValidos()
    {
        IReadOnlyList<DataFileReport> reports = DataValidation.ValidateAll(DataDirectory);

        Assert.All(reports, report => Assert.True(report.IsValid, string.Join("\n", report.Errors)));
    }

    [Fact]
    public void Traits_CarregaOsTracosDoGddComModificadores()
    {
        TraitCatalog catalog = JsonData.LoadFile<TraitCatalog>(Path.Combine(DataDirectory, "traits.json")).GetValueOrThrow();

        Assert.Equal(13, catalog.Traits.Count);
        TraitDefinition bomDePapo = catalog.Traits.Single(t => t.Id == "bom_de_papo");
        Assert.Equal(5, bomDePapo.AttributeModifiers[AttributeId.Oratoria]);
        Assert.Equal("Boa de papo", bomDePapo.Name.For(Gender.Feminino));
        Assert.False(catalog.Traits.Single(t => t.Id == "maleavel").SelectableAtCreation);
    }

    [Fact]
    public void Professions_CarregaAsDozeProfissoesDoGddMaisEstudante()
    {
        ProfessionCatalog catalog = JsonData.LoadFile<ProfessionCatalog>(Path.Combine(DataDirectory, "professions.json")).GetValueOrThrow();

        Assert.Equal(13, catalog.Professions.Count);
        Assert.Equal(17, catalog.Professions.Single(p => p.Id == "estudante").MinAge);
        Assert.All(catalog.Professions, p => Assert.NotEmpty(p.AttributeModifiers));
        Assert.Equal("Servidora pública", catalog.Professions.Single(p => p.Id == "servidor_publico").Name.For(Gender.Feminino));
    }

    [Fact]
    public void IdDuplicado_EReportado()
    {
        const string json = """
            { "traits": [
              { "id": "x", "name": { "masculine": "A", "feminine": "A", "neutral": "A" }, "description": "d" },
              { "id": "x", "name": { "masculine": "B", "feminine": "B", "neutral": "B" }, "description": "d" }
            ] }
            """;

        DataLoadResult<TraitCatalog> result = JsonData.Parse<TraitCatalog>(json, "teste.json");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("duplicado") && e.Contains("'x'"));
    }

    [Theory]
    [InlineData("""{ "traits": [ { "id": "x", "description": "d" } ] }""")]
    [InlineData("""{ "traits": [ { "id": "x", "name": { "masculine": "A", "feminine": "A", "neutral": "A" }, "description": "d", "extra": 1 } ] }""")]
    [InlineData("""{ "traits": [ { "id": "x", "name": { "masculine": "A", "feminine": "A", "neutral": "A" }, "description": "d", "attribute_modifiers": { "sorte": 5 } } ] }""")]
    [InlineData("""{ "traits": [ """)]
    public void JsonMalFormado_CampoFaltandoOuDesconhecido_EReportado(string json)
    {
        DataLoadResult<TraitCatalog> result = JsonData.Parse<TraitCatalog>(json, "teste.json");

        Assert.False(result.IsValid);
        Assert.Throws<DataValidationException>(() => result.GetValueOrThrow());
    }

    [Theory]
    [InlineData("Id Com Espaco", "A", 5)]
    [InlineData("x", "", 5)]
    [InlineData("x", "A", 0)]
    [InlineData("x", "A", 150)]
    public void RegrasDeConteudo_SaoValidadas(string id, string name, int modifier)
    {
        string json = $$"""
            { "traits": [ { "id": "{{id}}", "name": { "masculine": "{{name}}", "feminine": "A", "neutral": "A" },
              "description": "d", "attribute_modifiers": { "carisma": {{modifier}} } } ] }
            """;

        DataLoadResult<TraitCatalog> result = JsonData.Parse<TraitCatalog>(json, "teste.json");

        Assert.False(result.IsValid);
        Assert.NotNull(result.Value);
    }
}
