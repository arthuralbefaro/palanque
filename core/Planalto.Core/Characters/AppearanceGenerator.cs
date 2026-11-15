using Planalto.Core.Data;
using Planalto.Core.State;

namespace Planalto.Core.Characters;

/// <summary>Conteúdo de data/appearance/ já validado.</summary>
public sealed class AppearanceContent
{
    public const string PartsFile = "appearance/parts.json";
    public const string PalettesFile = "appearance/palettes.json";
    public const string GeneratorFile = "appearance/generator.json";
    public const string CorRacaFile = "appearance/cor_raca_uf.json";

    public required AppearanceCatalog Catalog { get; init; }

    public required AppearancePalettes Palettes { get; init; }

    public required AppearanceGeneratorConfig Generator { get; init; }

    public required CorRacaUfFile CorRaca { get; init; }

    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, out AppearanceContent? content)
    {
        var reports = new List<DataFileReport>();
        AppearanceCatalog? catalog = Check<AppearanceCatalog>(dataDirectory, PartsFile, c => c.Parts.Count, reports);
        AppearancePalettes? palettes = Check<AppearancePalettes>(dataDirectory, PalettesFile, c => c.Skin.Count, reports);
        AppearanceGeneratorConfig? generator = Check<AppearanceGeneratorConfig>(dataDirectory, GeneratorFile, c => c.Presence.Count, reports);
        CorRacaUfFile? corRaca = Check<CorRacaUfFile>(dataDirectory, CorRacaFile, c => c.Ufs.Count, reports);

        if (palettes is not null && corRaca is not null)
        {
            var missing = corRaca.Ufs.Values.SelectMany(c => c.Keys).Distinct().Where(c => !palettes.SkinByCorRaca.ContainsKey(c)).ToList();
            if (missing.Count > 0)
            {
                int i = reports.FindIndex(r => r.File == PalettesFile);
                reports[i] = reports[i] with { Errors = [.. reports[i].Errors, .. missing.Select(m => $"palettes.json: falta skin_by_cor_raca para '{m}'.")] };
            }
        }

        content = reports.All(r => r.IsValid)
            ? new AppearanceContent { Catalog = catalog!, Palettes = palettes!, Generator = generator!, CorRaca = corRaca! }
            : null;
        return reports;
    }

    public static AppearanceContent LoadOrThrow(string dataDirectory)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, out AppearanceContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }

    private static T? Check<T>(string dataDirectory, string file, Func<T, int> count, List<DataFileReport> reports)
        where T : class, IValidatable
    {
        DataLoadResult<T> result = JsonData.LoadFile<T>(Path.Combine(dataDirectory, file));
        reports.Add(new DataFileReport(file, result.Value is null ? 0 : count(result.Value), result.Errors));
        return result.IsValid ? result.Value : null;
    }
}

/// <summary>
/// Sorteio de aparência com pesos (GDD 3.8): peças pelo gênero, tom de pele pela cor ou raça da UF
/// (Censo 2022), e envelhecimento aplicado pela idade. Determinístico pelo Rng.
/// </summary>
public static class AppearanceGenerator
{
    /// <param name="uf">UF de origem do personagem, para os pesos de tom de pele; null usa o total do Brasil.</param>
    public static Appearance Random(Rng rng, AppearanceContent content, Gender gender, int age, string? uf)
    {
        AppearanceCatalog catalog = content.Catalog;
        AppearancePalettes palettes = content.Palettes;
        AppearanceGeneratorConfig config = content.Generator;
        bool Has(string element) => rng.Chance(config.Chance(element, gender));
        string Part(string category) => Pick(rng, catalog.InCategory(category), gender).Id;
        string? Maybe(string element, string category) => Has(element) ? Part(category) : null;
        double Param() => Math.Round(rng.NextDouble(0.2, 0.8), 3);

        int tone = SkinTone(rng, content, uf);
        string hairColor = Has("dyed_hair") ? rng.Pick(palettes.HairDyed) : NaturalHair(rng, palettes, tone);
        bool makeup = Has("makeup");

        var appearance = new Appearance
        {
            Body = Part("body"),
            ShoulderWidth = Param(),
            Height = Param(),
            SkinColor = palettes.Skin[tone],
            Undertone = rng.Pick(Enum.GetValues<SkinUndertone>()),
            Face = Part("face"),
            Jaw = Param(),
            Chin = Param(),
            Cheekbones = Param(),
            Eyes = Part("eyes"),
            EyeSize = Param(),
            EyeTilt = Math.Round(rng.NextDouble(-0.5, 0.5), 3),
            EyeColor = WeightedTowardFirst(rng, palettes.Eyes),
            Brows = Part("brows"),
            BrowThickness = Param(),
            BrowColor = hairColor,
            Nose = Part("nose"),
            NoseWidth = Param(),
            Mouth = Part("mouth"),
            LipThickness = Param(),
            Ears = Part("ears"),
            Hair = Pick(rng, catalog.InCategory("hair").Where(h => h.BaldingStage is null).ToList(), gender).Id,
            HairColor = hairColor,
            Beard = Maybe("beard", "beard"),
            Mustache = Maybe("mustache", "mustache"),
            FacialHairColor = hairColor,
            Freckles = Has("freckles") ? Param() : 0,
            Moles = Has("moles"),
            Scar = Maybe("scar", "scar"),
            Dimples = Has("dimples"),
            LipstickColor = makeup ? rng.Pick(palettes.MakeupLipstick) : null,
            LipstickIntensity = makeup ? Param() : 0,
            EyeshadowColor = makeup && rng.Chance(0.6) ? rng.Pick(palettes.MakeupEyeshadow) : null,
            EyeshadowIntensity = makeup ? Param() : 0,
            EyelinerIntensity = makeup && rng.Chance(0.5) ? Param() : 0,
            Glasses = Maybe("glasses", "glasses"),
            Earrings = Maybe("earrings", "earrings"),
            Piercing = Maybe("piercing", "piercing"),
            Necklace = Maybe("necklace", "necklace"),
            BaldingPropensity = Math.Round(rng.NextDouble(), 4),
            Wardrobe = Enum.GetValues<WardrobeContext>().ToDictionary(c => c, c => Outfit(rng, content, gender, c)),
        };

        return AppearanceAging.Apply(appearance, age, stress: 0, gender, content);
    }

    public static OutfitChoice Outfit(Rng rng, AppearanceContent content, Gender gender, WardrobeContext context)
    {
        var options = content.Catalog.InCategory("outfit").Where(p => p.Contexts.Contains(context)).ToList();
        string primary = rng.Pick(content.Palettes.Outfit);
        string secondary = rng.Pick(content.Palettes.Outfit.Where(c => c != primary).ToList());
        return new OutfitChoice(Pick(rng, options, gender).Id, primary, secondary);
    }

    /// <summary>Índice do tom de pele: mistura dos pesos de cada cor ou raça pela composição da UF (ou do Brasil).</summary>
    public static int SkinTone(Rng rng, AppearanceContent content, string? uf)
    {
        var counts = uf is not null && content.CorRaca.Ufs.TryGetValue(uf, out var byUf)
            ? byUf
            : content.CorRaca.Ufs.Values.SelectMany(c => c).GroupBy(c => c.Key).ToDictionary(g => g.Key, g => g.Sum(c => c.Value));
        double total = counts.Values.Sum();
        var weights = new double[content.Palettes.Skin.Count];
        foreach (var (category, count) in counts)
        {
            List<double> toneWeights = content.Palettes.SkinByCorRaca[category];
            double toneSum = toneWeights.Sum();
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] += count / total * toneWeights[i] / toneSum;
            }
        }

        return rng.PickWeighted(Enumerable.Range(0, weights.Length).ToList(), i => weights[i]);
    }

    private static PartDefinition Pick(Rng rng, IReadOnlyList<PartDefinition> parts, Gender gender) =>
        rng.PickWeighted(parts, p => p.WeightFor(gender));

    // Cabelo natural tende a escurecer com o tom de pele: sorteio ponderado pela distância de posição na paleta.
    private static string NaturalHair(Rng rng, AppearancePalettes palettes, int tone)
    {
        double darkness = 1 - (tone / (double)(palettes.Skin.Count - 1));
        var indexes = Enumerable.Range(0, palettes.HairNatural.Count).ToList();
        int chosen = rng.PickWeighted(indexes, i => Math.Exp(-Math.Pow((i / (double)(indexes.Count - 1)) - (darkness * 0.8), 2) / 0.08));
        return palettes.HairNatural[chosen];
    }

    private static string WeightedTowardFirst(Rng rng, IReadOnlyList<string> colors) =>
        colors[rng.PickWeighted(Enumerable.Range(0, colors.Count).ToList(), i => 1.0 / (1 + i))];
}

/// <summary>Envelhecimento visual pela idade e pelo estresse (GDD 3.7). Nunca rejuvenesce.</summary>
public static class AppearanceAging
{
    /// <param name="stress">Estresse acumulado de 0 a 100.</param>
    public static Appearance Apply(Appearance appearance, int age, double stress, Gender gender, AppearanceContent content)
    {
        AgingConfig aging = content.Generator.Aging;
        double effective = age + (Math.Clamp(stress, 0, 100) / 100 * aging.StressAgingFactor * age);

        double Ramp(int start, int full) => Math.Clamp((effective - start) / (full - start), 0, 1);

        double gray = Math.Max(appearance.HairGray, Math.Round(Ramp(aging.GrayStartAge, aging.GrayFullAge), 3));
        return appearance with
        {
            HairGray = gray,
            FacialHairGray = Math.Max(appearance.FacialHairGray, gray),
            Wrinkles = Math.Max(appearance.Wrinkles, Math.Round(Ramp(aging.WrinkleStartAge, aging.WrinkleFullAge), 3)),
            EyeBags = Math.Max(appearance.EyeBags, Math.Round(Ramp(aging.EyebagsStartAge, aging.EyebagsFullAge) * 0.8, 3)),
            Hair = Balding(appearance, effective, gender, content),
        };
    }

    /// <summary>Avança a calvície (entradas → parcial → careca) quando a propensão sorteada fica abaixo da chance acumulada.</summary>
    private static string Balding(Appearance appearance, double age, Gender gender, AppearanceContent content)
    {
        double perDecade = content.Generator.Aging.BaldingChancePerDecade.GetValueOrDefault(PartDefinition.GenderKey(gender));
        double decades = Math.Max(0, (age - 30) / 10);
        double cumulative = 1 - Math.Pow(1 - perDecade, decades);
        if (appearance.BaldingPropensity >= cumulative)
        {
            return appearance.Hair;
        }

        // Quanto mais abaixo da chance acumulada, mais avançado o estágio.
        int stage = Math.Clamp(1 + (int)Math.Floor((cumulative - appearance.BaldingPropensity) / Math.Max(cumulative, 1e-9) * 3), 1, 3);
        int current = content.Catalog.Find(appearance.Hair)?.BaldingStage ?? 0;
        if (stage <= current)
        {
            return appearance.Hair;
        }

        return content.Catalog.Parts.FirstOrDefault(p => p.Category == "hair" && p.BaldingStage == stage)?.Id ?? appearance.Hair;
    }
}
