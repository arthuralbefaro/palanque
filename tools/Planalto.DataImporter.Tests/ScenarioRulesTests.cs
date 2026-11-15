using Planalto.Core.Data;
using Planalto.Core.Ideology;
using Planalto.Core.Parties;
using Planalto.DataImporter.Scenario;

namespace Planalto.DataImporter.Tests;

public class ScenarioRulesTests
{
    private static readonly Lazy<FamilyCatalog> Families = new(() =>
    {
        string data = DataPaths.FindDataDirectory(AppContext.BaseDirectory)!;
        return JsonData.LoadFile<FamilyCatalog>(Path.Combine(data, PartyPaths.Families)).GetValueOrThrow();
    });

    private static readonly HashSet<string> Parties2026 =
        ["PT", "PRD", "UNIAO", "PODE", "SOLIDARIEDADE", "AGIR", "MOBILIZA", "DEMOCRATA", "PCDOB", "MISSAO", "PL"];

    [Theory]
    [InlineData("PTB", "PRD")]
    [InlineData("PATRIOTA", "PRD")]
    [InlineData("PSL", "UNIAO")]
    [InlineData("DEM", "UNIAO")]
    [InlineData("PSC", "PODE")]
    [InlineData("PROS", "SOLIDARIEDADE")]
    [InlineData("PTC", "AGIR")]
    [InlineData("PMN", "MOBILIZA")]
    [InlineData("PMB", "DEMOCRATA")]
    [InlineData("PC do B", "PCDOB")]
    [InlineData("UNIÃO", "UNIAO")]
    [InlineData("MISSÃO", "MISSAO")]
    public void Linhagem_LevaSiglasAntigasAoPartidoDe2026(string sigla, string expected)
    {
        Assert.Equal(expected, RealParties.Canonical(sigla, Parties2026));
    }

    [Fact]
    public void Linhagem_SiglaDesconhecidaInterrompeAImportacao()
    {
        Assert.Throws<ImportException>(() => RealParties.Canonical("XYZ", Parties2026));
    }

    [Fact]
    public void Escores_PrdEAMediaDePtbEPatriota()
    {
        Assert.Equal((7.72 + 8.60) / 2, RealParties.IdeologyScore2022["PRD"], 9);
        Assert.False(RealParties.IdeologyScore2022.ContainsKey("MISSAO"));
        Assert.Equal(29, RealParties.IdeologyScore2022.Count);
    }

    [Theory]
    [InlineData(-90, -85, "esquerda_socialista")]
    [InlineData(-46, -50, "esquerda")]
    [InlineData(-23, -20, "centro_esquerda")]
    [InlineData(0, 5, "centro")]
    [InlineData(30, 27, "centro_direita")]
    [InlineData(76, 76, "liberal_conservador")]
    [InlineData(44, 44, "centro_direita")]
    public void Familia_EAGeralQueContemOuEstaMaisPerto(int economic, int social, string expected)
    {
        Assert.Equal(expected, ScenarioBuilder.GeneralFamilyFor(new IdeologicalPosition(economic, social), Families.Value).Id);
    }

    [Theory]
    [InlineData(39, 39)]
    [InlineData(10, 30)]
    [InlineData(-20, -40)]
    public void Familia_NuncaEEspecifica(int economic, int social)
    {
        string[] specific = ["ambientalista", "trabalhista", "democracia_crista", "agrario", "centro_pragmatico", "libertario", "nacional_populista", "regionalista"];

        Assert.DoesNotContain(ScenarioBuilder.GeneralFamilyFor(new IdeologicalPosition(economic, social), Families.Value).Id, specific);
    }
}
