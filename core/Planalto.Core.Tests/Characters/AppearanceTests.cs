using System.Text.Json;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Characters;

public class AppearanceTests
{
    private static readonly Lazy<AppearanceContent> Lazy = new(() => AppearanceContent.LoadOrThrow(TestContent.DataDirectory));

    private static AppearanceContent Content => Lazy.Value;

    [Fact]
    public void Catalogo_CumpreAsQuantidadesDoGdd()
    {
        AppearanceCatalog catalog = Content.Catalog;

        Assert.True(catalog.InCategory("hair").Count >= 30);
        Assert.Equal(12, catalog.InCategory("beard").Count);
        Assert.Equal(8, catalog.InCategory("mustache").Count);
        Assert.Equal(11, catalog.InCategory("glasses").Count);
        Assert.Equal(6, catalog.InCategory("face").Count);
        Assert.Equal(24, Content.Palettes.Skin.Count);
    }

    [Theory]
    [InlineData(Gender.Masculino)]
    [InlineData(Gender.Feminino)]
    [InlineData(Gender.NaoBinario)]
    public void Sorteio_UsaSoPecasDoCatalogoNasCategoriasCertas(Gender gender)
    {
        var rng = new Rng(1);
        for (int i = 0; i < 300; i++)
        {
            Appearance a = AppearanceGenerator.Random(rng, Content, gender, rng.NextInt(25, 46), "BA");
            AssertCategory(a.Body, "body");
            AssertCategory(a.Face, "face");
            AssertCategory(a.Eyes, "eyes");
            AssertCategory(a.Brows, "brows");
            AssertCategory(a.Nose, "nose");
            AssertCategory(a.Mouth, "mouth");
            AssertCategory(a.Ears, "ears");
            AssertCategory(a.Hair, "hair");
            AssertCategory(a.Beard, "beard", optional: true);
            AssertCategory(a.Glasses, "glasses", optional: true);
            Assert.Contains(a.SkinColor, Content.Palettes.Skin);
            Assert.All(Enum.GetValues<WardrobeContext>(), c =>
            {
                OutfitChoice outfit = a.Wardrobe[c];
                Assert.Contains(c, Content.Catalog.Find(outfit.PartId)!.Contexts);
                Assert.NotEqual(outfit.PrimaryColor, outfit.SecondaryColor);
            });
        }
    }

    [Fact]
    public void Sorteio_EDeterministicoPelaSemente()
    {
        Appearance a = AppearanceGenerator.Random(new Rng(7), Content, Gender.Feminino, 30, "SP");
        Appearance b = AppearanceGenerator.Random(new Rng(7), Content, Gender.Feminino, 30, "SP");

        Assert.Equal(JsonSerializer.Serialize(a, JsonData.Options), JsonSerializer.Serialize(b, JsonData.Options));
    }

    [Fact]
    public void TomDePele_SeguePesosRegionaisDoCenso()
    {
        double MeanTone(string uf)
        {
            var rng = new Rng(3);
            return Enumerable.Range(0, 3000).Average(_ => AppearanceGenerator.SkinTone(rng, Content, uf));
        }

        // SC tem a maior fatia de brancos do Censo 2022 e BA uma das maiores de pretos e pardos.
        Assert.True(MeanTone("SC") + 2 < MeanTone("BA"));
    }

    [Fact]
    public void Envelhecimento_AumentaGrisalhoERugasENuncaRejuvenesce()
    {
        Appearance young = AppearanceGenerator.Random(new Rng(5), Content, Gender.Masculino, 28, null);
        Appearance old = AppearanceAging.Apply(young, 70, 0, Gender.Masculino, Content);
        Appearance stressed = AppearanceAging.Apply(young, 50, 100, Gender.Masculino, Content);
        Appearance calm = AppearanceAging.Apply(young, 50, 0, Gender.Masculino, Content);
        Appearance backToYoung = AppearanceAging.Apply(old, 28, 0, Gender.Masculino, Content);

        Assert.True(old.HairGray > young.HairGray);
        Assert.True(old.Wrinkles > young.Wrinkles);
        Assert.True(stressed.HairGray > calm.HairGray);
        Assert.Equal(old.HairGray, backToYoung.HairGray);
    }

    [Fact]
    public void Envelhecimento_CalviceAvancaParaParteDosHomens()
    {
        var rng = new Rng(11);
        int bald = 0;
        for (int i = 0; i < 400; i++)
        {
            Appearance a = AppearanceGenerator.Random(rng, Content, Gender.Masculino, 25, null);
            Appearance aged = AppearanceAging.Apply(a, 75, 0, Gender.Masculino, Content);
            if (Content.Catalog.Find(aged.Hair)!.BaldingStage is not null && Content.Catalog.Find(a.Hair)!.BaldingStage is null)
            {
                bald++;
            }
        }

        Assert.InRange(bald, 40, 320);
    }

    [Fact]
    public void Serializacao_IdaEVolta()
    {
        Appearance a = AppearanceGenerator.Random(new Rng(9), Content, Gender.NaoBinario, 40, "AM");

        string json = JsonSerializer.Serialize(a, JsonData.Options);
        Appearance back = JsonSerializer.Deserialize<Appearance>(json, JsonData.Options)!;

        Assert.Equal(json, JsonSerializer.Serialize(back, JsonData.Options));
        Assert.Contains("\"wardrobe\"", json);
    }

    [Fact]
    public void Camadas_ResolvemOArquivoDaExpressao()
    {
        PartLayer layer = Content.Catalog.Find("mouth_cheia")!.Layers[0];

        Assert.Equal("mouth/cheia_sorriso_largo.svg", layer.FileFor(Expression.SorrisoLargo));
    }

    private static void AssertCategory(string? id, string category, bool optional = false)
    {
        if (id is null)
        {
            Assert.True(optional);
            return;
        }

        Assert.Equal(category, Content.Catalog.Find(id)?.Category);
    }
}
