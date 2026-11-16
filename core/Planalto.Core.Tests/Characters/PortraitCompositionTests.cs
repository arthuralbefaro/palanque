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

    /// <summary>
    /// Em todo rosto e toda orelha: as orelhas vão para a largura do rosto na linha das orelhas, os brincos vão junto
    /// e descem até o lóbulo daquela orelha, e ficam na frente do cabelo (nunca soltos sobre a mecha).
    /// </summary>
    [Fact]
    public void Orelhas_e_brincos_se_encaixam_em_todo_rosto_e_toda_orelha()
    {
        AppearanceCatalog catalog = Content.Catalog;
        FitReference reference = catalog.FitReference;
        Appearance person = AppearanceGenerator.Random(new Rng(6), Content, Gender.Feminino, 30, "SP");
        foreach (PartDefinition face in catalog.InCategory("face"))
        {
            foreach (PartDefinition ears in catalog.InCategory("ears"))
            {
                foreach (PartDefinition earrings in catalog.InCategory("earrings"))
                {
                    foreach (PartDefinition hair in catalog.InCategory("hair"))
                    {
                        Appearance a = person with { Face = face.Id, Ears = ears.Id, Earrings = earrings.Id, Hair = hair.Id };
                        IReadOnlyList<LayerSpec> layers = PortraitComposition.Compose(a, new PortraitOptions(), Content);
                        LayerSpec ear = layers.Single(l => l.File.Contains("/ears/", StringComparison.Ordinal));
                        LayerSpec ring = layers.Single(l => l.File.Contains("/earrings/", StringComparison.Ordinal));
                        LayerSpec head = layers.Single(l => l.File.Contains("/face/", StringComparison.Ordinal));

                        // O centro da orelha (desenhado em center_x ± ear_x de referência) cai na borda deste rosto.
                        double earCenter = reference.CenterX - (reference.EarX * ear.ScaleX);
                        Assert.Equal(reference.CenterX - face.Fit!.EarX!.Value, earCenter, 6);
                        Assert.Equal(reference.CenterX, ear.PivotX);

                        // O brinco acompanha a orelha na horizontal e pende do lóbulo dela.
                        Assert.Equal(ear.ScaleX, ring.ScaleX);
                        Assert.Equal(ear.PivotX, ring.PivotX);
                        Assert.Equal(ears.Fit!.LobeY!.Value - reference.LobeY, ring.OffsetY, 6);
                        Assert.Equal(0, ear.OffsetY);

                        // Pilha: rosto, cabelo da frente, orelha, brinco.
                        int Index(LayerSpec layer) => layers.ToList().IndexOf(layer);
                        Assert.True(Index(head) < Index(ear));
                        Assert.True(Index(ear) < Index(ring));
                        foreach (LayerSpec front in layers.Where(l => l.File.Contains("/hair/", StringComparison.Ordinal) && l.File.Contains("_front", StringComparison.Ordinal)))
                        {
                            Assert.True(Index(front) < Index(ear), $"{hair.Id}: o cabelo da frente cobre a orelha");
                        }
                    }
                }
            }
        }
    }

    /// <summary>O cabelo (frente e fundo) estica junto, com a escala do rosto, em torno do centro e da base da touca.</summary>
    [Fact]
    public void Cabelo_estica_para_cobrir_o_alto_de_cada_rosto()
    {
        AppearanceCatalog catalog = Content.Catalog;
        Appearance person = AppearanceGenerator.Random(new Rng(6), Content, Gender.Feminino, 30, "SP");
        foreach (PartDefinition face in catalog.InCategory("face"))
        {
            foreach (PartDefinition hair in catalog.InCategory("hair").Where(h => h.Layers.Count > 0))
            {
                var layers = PortraitComposition.Compose(person with { Face = face.Id, Hair = hair.Id }, new PortraitOptions(), Content)
                    .Where(l => l.File.Contains("/hair/", StringComparison.Ordinal)).ToList();
                Assert.Equal(hair.Layers.Count, layers.Count);
                Assert.All(layers, l =>
                {
                    Assert.Equal(face.Fit!.HairScaleX!.Value, l.ScaleX);
                    Assert.Equal(face.Fit.HairScaleY!.Value, l.ScaleY);
                    Assert.Equal(catalog.FitReference.CenterX, l.PivotX);
                    Assert.Equal(catalog.FitReference.HairPivotY, l.PivotY);
                });
            }
        }

        // O rosto de referência não mexe em nada.
        PartDefinition oval = catalog.Find("face_oval")!;
        Assert.Equal(catalog.FitReference.EarX, oval.Fit!.EarX);
        Assert.Equal(1, oval.Fit.HairScaleX);
        Assert.Equal(1, oval.Fit.HairScaleY);
    }

    [Fact]
    public void Catalogo_sem_encaixe_ou_com_a_orelha_atras_do_cabelo_e_reportado()
    {
        string json = File.ReadAllText(Path.Combine(TestContent.DataDirectory, AppearanceContent.PartsFile));
        AppearanceCatalog Load(string text) => System.Text.Json.JsonSerializer.Deserialize<AppearanceCatalog>(text, JsonData.Options)!;

        Assert.Empty(Load(json).Validate());
        Assert.Contains(Load(json.Replace(", \"fit\": {\"lobe_y\": 474}", "", StringComparison.Ordinal)).Validate(), e => e.Contains("ears_2", StringComparison.Ordinal) && e.Contains("lobe_y", StringComparison.Ordinal));
        Assert.Contains(Load(json.Replace("\"ear_x\": 185, ", "", StringComparison.Ordinal)).Validate(), e => e.Contains("face_redondo", StringComparison.Ordinal) && e.Contains("ear_x", StringComparison.Ordinal));
        Assert.Contains(
            Load(json.Replace("\"hair_front\", \"ears\", ", "\"ears\", \"hair_front\", ", StringComparison.Ordinal)).Validate(),
            e => e.Contains("layer_order", StringComparison.Ordinal));
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
