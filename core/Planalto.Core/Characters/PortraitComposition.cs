using Planalto.Core.Parties;

namespace Planalto.Core.Characters;

/// <summary>
/// Uma camada pronta para desenhar: arquivo, cor de tingimento, opacidade, escala em torno de um pivô e
/// deslocamento, tudo em pontos do quadro.
/// </summary>
public sealed record LayerSpec(string File, string Tint, double Alpha, int Order, double ScaleX, double ScaleY, double PivotX, double PivotY, double OffsetX = 0, double OffsetY = 0);

/// <summary>Opções de enquadramento do retrato que não fazem parte da aparência.</summary>
public sealed record PortraitOptions(
    Expression Expression = Expression.Neutra,
    WardrobeContext Context = WardrobeContext.Institucional,
    string? PartyColor = null,
    string Background = "background_solido",
    string? BackgroundColor = null);

/// <summary>
/// Converte uma <see cref="Appearance"/> na pilha de camadas do retrato (GDD 7.4), na ordem de layer_order,
/// com as cores de cada canal de tingimento. O Godot só desenha a lista.
/// </summary>
public static class PortraitComposition
{
    public static IReadOnlyList<LayerSpec> Compose(Appearance a, PortraitOptions options, AppearanceContent content)
    {
        AppearanceCatalog catalog = content.Catalog;
        AppearancePalettes palettes = content.Palettes;
        var specs = new List<LayerSpec>();
        OutfitChoice outfit = a.Wardrobe.TryGetValue(options.Context, out OutfitChoice? chosen) ? chosen : a.Wardrobe.Values.First();
        double shoulders = 0.85 + (Math.Clamp(a.ShoulderWidth, 0, 1) * 0.3);
        double eyes = 0.9 + (Math.Clamp(a.EyeSize, 0, 1) * 0.2);

        string TintColor(string tint) => tint switch
        {
            "skin" => a.SkinColor,
            "lips" => Shift(a.SkinColor, -0.10, 0.02),
            "hair" => Mix(a.HairColor, palettes.Gray, a.HairGray),
            "brows" => Mix(a.BrowColor, palettes.Gray, a.HairGray * 0.8),
            "eyes" => a.EyeColor,
            "facial_hair" => Mix(a.FacialHairColor, palettes.Gray, a.FacialHairGray),
            "outfit_primary" => outfit.PrimaryColor,
            "outfit_secondary" => outfit.SecondaryColor,
            "lipstick" => a.LipstickColor ?? "#FFFFFF",
            "eyeshadow" => a.EyeshadowColor ?? "#FFFFFF",
            "glasses" => palettes.Glasses,
            "metal" => palettes.Metal,
            "party" => options.PartyColor ?? palettes.Metal,
            "background" => options.BackgroundColor ?? palettes.Background[0],
            _ => "#FFFFFF",
        };

        // Encaixe no rosto e na orelha escolhidos: orelhas e brincos vão para a largura do rosto na linha das orelhas,
        // o brinco desce até o lóbulo daquela orelha, e o cabelo estica para cobrir o alto daquela cabeça.
        FitReference reference = catalog.FitReference;
        PartFit? face = catalog.Find(a.Face)?.Fit;
        double earScale = (face?.EarX ?? reference.EarX) / reference.EarX;
        double lobeDrop = (catalog.Find(a.Ears)?.Fit?.LobeY ?? reference.LobeY) - reference.LobeY;
        double hairScaleX = face?.HairScaleX ?? 1;
        double hairScaleY = face?.HairScaleY ?? 1;

        void Add(string? partId, double alpha = 1, double scaleX = 1, double scaleY = 1, double pivotX = 512, double pivotY = 512, double offsetY = 0)
        {
            PartDefinition? part = catalog.Find(partId);
            if (part is null || alpha <= 0)
            {
                return;
            }

            foreach (PartLayer layer in part.Layers)
            {
                specs.Add(new LayerSpec(
                    catalog.ArtRoot.TrimEnd('/') + "/" + layer.FileFor(options.Expression),
                    TintColor(layer.Tint),
                    Math.Clamp(alpha, 0, 1),
                    catalog.LayerOrder.IndexOf(layer.Slot),
                    scaleX,
                    scaleY,
                    pivotX,
                    pivotY,
                    OffsetY: offsetY));
            }
        }

        Add(options.Background);
        Add(a.Body, scaleX: shoulders, pivotY: 1024);
        Add(outfit.PartId, scaleX: shoulders, pivotY: 1024);
        Add(a.Face);
        Add(a.Ears, scaleX: earScale, pivotX: reference.CenterX);
        Add("marks_sardas", a.Freckles);
        Add(a.Moles ? "marks_pintas" : null);
        Add("marks_rugas", a.Wrinkles);
        Add("marks_olheiras", a.EyeBags);
        Add(a.Dimples ? "marks_covinhas" : null);
        Add(a.Scar);
        Add(a.Eyes, scaleX: eyes, scaleY: eyes, pivotY: 400);
        Add(a.Brows, alpha: 0.6 + (0.4 * a.BrowThickness));
        Add(a.Nose, scaleX: 0.85 + (a.NoseWidth * 0.3), pivotY: 470);
        Add(a.Mouth);
        Add(a.LipstickColor is null ? null : "makeup_batom", a.LipstickIntensity);
        Add(a.EyeshadowColor is null ? null : "makeup_sombra", a.EyeshadowIntensity);
        Add("makeup_delineado", a.EyelinerIntensity);
        Add(a.Beard);
        Add(a.Mustache);
        Add(a.Hair, scaleX: hairScaleX, scaleY: hairScaleY, pivotX: reference.CenterX, pivotY: reference.HairPivotY);
        Add(a.Glasses);
        Add(a.Earrings, scaleX: earScale, pivotX: reference.CenterX, offsetY: lobeDrop);
        Add(a.Piercing);
        Add(a.Necklace);
        Add(a.PartyPin ? "pin_partido" : null);

        // Ordem estável: pela posição do slot na pilha e, no mesmo slot, pela ordem de inclusão.
        return specs.Select((s, i) => (s, i)).OrderBy(x => x.s.Order).ThenBy(x => x.i).Select(x => x.s).ToList();
    }

    /// <summary>Mistura linear em sRGB entre duas cores (t = 0 só a primeira).</summary>
    public static string Mix(string from, string to, double t)
    {
        t = Math.Clamp(t, 0, 1);
        int Channel(string hex, int i) => Convert.ToInt32(hex.Substring(1 + (i * 2), 2), 16);
        return "#" + string.Concat(Enumerable.Range(0, 3).Select(i => ((int)Math.Round(Channel(from, i) + ((Channel(to, i) - Channel(from, i)) * t))).ToString("X2")));
    }

    /// <summary>Escurece ou clareia a cor no OKLab, mantendo o matiz.</summary>
    public static string Shift(string hex, double lightness, double chroma)
    {
        if (!OkColor.TryParseHex(hex, out OkLab lab))
        {
            return hex;
        }

        double c = Math.Sqrt((lab.A * lab.A) + (lab.B * lab.B));
        double h = Math.Atan2(lab.B, lab.A);
        for (double l = lab.L + lightness; l > 0; l -= 0.01)
        {
            string? result = OkColor.ToHex(OkColor.FromOkLch(Math.Clamp(l, 0, 1), c + chroma, h * 180 / Math.PI));
            if (result is not null)
            {
                return result;
            }
        }

        return hex;
    }
}
