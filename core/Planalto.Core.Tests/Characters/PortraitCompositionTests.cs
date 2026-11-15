using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Characters;

public class PortraitCompositionTests
{
    private static readonly Lazy<AppearanceContent> Lazy = new(() => AppearanceContent.LoadOrThrow(TestContent.DataDirectory));

    private static AppearanceContent Content => Lazy.Value;

    private static string AssetsRoot => Path.Combine(Directory.GetParent(TestContent.DataDirectory)!.FullName);

    [Fact]
    public void Camadas_SeguemAOrdemDaPilhaDoGdd()
    {
        Appearance a = AppearanceGenerator.Random(new Rng(1), Content, Gender.Masculino, 40, "SP");

        IReadOnlyList<LayerSpec> layers = PortraitComposition.Compose(a, new PortraitOptions(), Content);

        Assert.Equal(layers.Select(l => l.Order).Order(), layers.Select(l => l.Order));
        int head = layers.First(l => l.File.Contains("/face/")).Order;
        int hairFront = layers.First(l => l.File.Contains("_front.svg") && l.File.Contains("/hair/")).Order;
        Assert.True(head < hairFront || Content.Catalog.Find(a.Hair)!.Layers.All(x => x.Slot != "hair_front"));
        Assert.StartsWith("res://assets/portrait/placeholder/background/", layers[0].File);
    }

    [Fact]
    public void Arquivos_DeTodasAsCamadasExistem()
    {
        var rng = new Rng(2);
        foreach (Gender gender in Enum.GetValues<Gender>())
        {
            for (int i = 0; i < 60; i++)
            {
                Appearance a = AppearanceGenerator.Random(rng, Content, gender, rng.NextInt(25, 80), null);
                var options = new PortraitOptions(rng.Pick(Enum.GetValues<Expression>()), rng.Pick(Enum.GetValues<WardrobeContext>()), "#1D4E89");
                foreach (LayerSpec layer in PortraitComposition.Compose(a, options, Content))
                {
                    string path = Path.Combine(AssetsRoot, layer.File.Replace("res://", "", StringComparison.Ordinal));
                    Assert.True(File.Exists(path), path);
                }
            }
        }
    }

    [Fact]
    public void Expressao_TrocaSoOlhosSobrancelhasEBoca()
    {
        Appearance a = AppearanceGenerator.Random(new Rng(3), Content, Gender.Feminino, 30, null);

        var neutral = PortraitComposition.Compose(a, new PortraitOptions(Expression.Neutra), Content).Select(l => l.File).ToList();
        var angry = PortraitComposition.Compose(a, new PortraitOptions(Expression.Bravo), Content).Select(l => l.File).ToList();

        var changed = neutral.Zip(angry).Where(p => p.First != p.Second).Select(p => p.First.Split('/')[^2]).Distinct().ToList();
        Assert.NotEmpty(changed);
        Assert.All(changed, folder => Assert.Contains(folder, new[] { "eyes", "brows", "mouth", "makeup" }));
    }

    [Fact]
    public void Grisalho_ClareiaOCabelo()
    {
        Appearance a = AppearanceGenerator.Random(new Rng(4), Content, Gender.Masculino, 30, null) with { HairColor = "#0E0B09", HairGray = 0 };
        string young = PortraitComposition.Compose(a, new PortraitOptions(), Content).First(l => l.File.Contains("/brows/")).Tint;
        string old = PortraitComposition.Compose(a with { HairGray = 1 }, new PortraitOptions(), Content).First(l => l.File.Contains("/brows/")).Tint;

        Assert.NotEqual(young, old);
        Assert.Equal("#BDBDBD", PortraitComposition.Mix("#0E0B09", "#BDBDBD", 1));
    }

    [Fact]
    public void Roupa_MudaComOContexto()
    {
        Appearance a = AppearanceGenerator.Random(new Rng(5), Content, Gender.NaoBinario, 35, null);

        string institucional = PortraitComposition.Compose(a, new PortraitOptions(Context: WardrobeContext.Institucional), Content).First(l => l.File.Contains("/outfit/")).File;
        string campanha = PortraitComposition.Compose(a, new PortraitOptions(Context: WardrobeContext.Campanha), Content).First(l => l.File.Contains("/outfit/")).File;

        Assert.NotEqual(institucional, campanha);
    }
}
