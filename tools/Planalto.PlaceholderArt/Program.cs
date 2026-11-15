using System.Globalization;
using System.Text;
using Planalto.Core.Characters;
using Planalto.Core.Data;

// Uso: dotnet run --project tools/Planalto.PlaceholderArt
// Gera a arte placeholder dos retratos (GDD 7.4, Fase 2) a partir de data/appearance/parts.json:
// um SVG em tons de cinza por camada (branco = cor do tingimento, cinza = sombra), 1024 de coordenada.
string? data = DataPaths.FindDataDirectory(Directory.GetCurrentDirectory()) ?? DataPaths.FindDataDirectory(AppContext.BaseDirectory);
if (data is null)
{
    Console.Error.WriteLine("Pasta data/ não encontrada.");
    return 1;
}

AppearanceCatalog catalog = JsonData.LoadFile<AppearanceCatalog>(Path.Combine(data, AppearanceContent.PartsFile)).GetValueOrThrow();
string root = Path.Combine(Directory.GetParent(data)!.FullName, catalog.ArtRoot.Replace("res://", "", StringComparison.Ordinal));
// Só os SVGs obsoletos são apagados; os .import (com os UIDs do Godot) dos que continuam são preservados.
var generated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
int files = 0;
foreach (PartDefinition part in catalog.Parts)
{
    int variant = catalog.InCategory(part.Category).ToList().IndexOf(part);
    foreach (PartLayer layer in part.Layers)
    {
        IEnumerable<Expression> expressions = part.Expressions ? Enum.GetValues<Expression>() : [Expression.Neutra];
        foreach (Expression expression in expressions)
        {
            string body = Art.Draw(part, layer.Slot, variant, expression);
            string path = Path.Combine(root, layer.FileFor(expression).Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, Art.Svg(body), new UTF8Encoding(false));
            generated.Add(Path.GetFullPath(path));
            files++;
        }
    }
}

foreach (string stale in Directory.EnumerateFiles(root, "*.svg", SearchOption.AllDirectories).Where(f => !generated.Contains(Path.GetFullPath(f))))
{
    File.Delete(stale);
    File.Delete(stale + ".import");
}

Console.WriteLine($"{files} camadas geradas em {root}");

// Ícones de logotipo (data/parties/logo_icons.json): brancos para receber a cor do partido.
var icons = JsonData.LoadFile<Planalto.Core.Parties.LogoIconCatalog>(Path.Combine(data, "parties/logo_icons.json")).GetValueOrThrow();
string iconRoot = Path.Combine(Directory.GetParent(data)!.FullName, "assets", "icons", "logo");
Directory.CreateDirectory(iconRoot);
foreach (var icon in icons.Icons)
{
    File.WriteAllText(Path.Combine(iconRoot, $"{icon.Id}.svg"), Art.Icon(icon.Id), new UTF8Encoding(false));
}

Console.WriteLine($"{icons.Icons.Count} ícones de logotipo gerados em {iconRoot}");
return 0;

/// <summary>Desenhos vetoriais simples por categoria. Coordenadas no quadro 1024×1024; cabeça centrada em (512, 400).</summary>
internal static class Art
{
    private const string Shade = "#C8C8C8";
    private const string Dark = "#2A2A2A";
    private const string Mid = "#8A8A8A";

    public static string Svg(string body) =>
        "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"512\" height=\"512\" viewBox=\"0 0 1024 1024\">" + body + "</svg>\n";

    public static string Draw(PartDefinition part, string slot, int v, Expression e) => part.Category switch
    {
        "body" => Body(v),
        "face" => Face(v),
        "ears" => Ears(v),
        "eyes" => slot == "eyes_white" ? EyesWhite(v, e) : EyesIris(v, e),
        "brows" => Brows(v, e),
        "nose" => Nose(v),
        "mouth" => Mouth(v, e, lipstick: false),
        "hair" => slot == "hair_back" ? HairBack(part) : HairFront(part),
        "beard" => Beard(v),
        "mustache" => Mustache(v),
        "scar" => Scar(v),
        "marks" => Marks(part.Id),
        "makeup" => part.Id switch
        {
            "makeup_batom" => Mouth(0, e, lipstick: true),
            "makeup_sombra" => Ellipse(430, 382, 62, 22, "#FFFFFF", 0.9) + Ellipse(594, 382, 62, 22, "#FFFFFF", 0.9),
            _ => Path("M370 398 Q430 372 492 396", Dark, 6) + Path("M532 396 Q594 372 654 398", Dark, 6),
        },
        "glasses" => Glasses(part.Id, v),
        "earrings" => Earrings(v),
        "piercing" => Piercing(part.Id),
        "necklace" => Necklace(v),
        "pin" => Circle(392, 900, 20, "#FFFFFF") + Circle(392, 900, 20, "none", Dark, 3),
        "outfit" => slot switch
        {
            "outfit" => OutfitMain(part, v),
            "outfit_detail" => OutfitDetail(part),
            _ => OutfitFront(part),
        },
        "background" => Background(part.Id),
        _ => "",
    };

    private static string Body(int v)
    {
        int w = 250 + (v * 20);
        return Rect(452, 560, 120, 160, "#FFFFFF") + Rect(472, 600, 80, 40, Shade, opacity: 0.6)
            + Path($"M{512 - w} 1024 Q{512 - w} 760 {512 - 150} 720 L{512 + 150} 720 Q{512 + w} 760 {512 + w} 1024 Z", "none", fill: "#FFFFFF");
    }

    private static string Face(int v) => v switch
    {
        0 => Ellipse(512, 400, 170, 215, "#FFFFFF"),
        1 => Ellipse(512, 410, 185, 200, "#FFFFFF"),
        2 => Path("M352 260 Q352 200 420 190 L604 190 Q672 200 672 260 L672 520 Q672 610 512 620 Q352 610 352 520 Z", "none", fill: "#FFFFFF"),
        3 => Ellipse(512, 400, 158, 235, "#FFFFFF"),
        4 => Path("M342 330 Q342 190 512 190 Q682 190 682 330 Q682 470 512 625 Q342 470 342 330 Z", "none", fill: "#FFFFFF"),
        _ => Path("M380 230 Q512 160 644 230 Q690 420 640 540 Q600 615 512 620 Q424 615 384 540 Q334 420 380 230 Z", "none", fill: "#FFFFFF"),
    } + Ellipse(512, 560, 110, 40, Shade, 0.35);

    private static string Ears(int v)
    {
        int rx = 26 + (v * 6);
        int ry = v == 3 ? 62 : 48 + (v * 6);
        return Ellipse(342, 420, rx, ry, "#FFFFFF") + Ellipse(682, 420, rx, ry, "#FFFFFF")
            + Ellipse(346, 420, rx / 2, ry / 2, Shade, 0.7) + Ellipse(678, 420, rx / 2, ry / 2, Shade, 0.7);
    }

    private static double Openness(Expression e) => e switch
    {
        Expression.Surpreso => 1.35,
        Expression.Cansado => 0.45,
        Expression.Sorriso or Expression.SorrisoLargo => 0.75,
        Expression.Bravo => 0.8,
        Expression.Triste => 0.85,
        _ => 1,
    };

    private static string EyeShape(double cx, int v, Expression e, string fill, string stroke)
    {
        double w = 44 + ((v % 4) * 4);
        double h = (18 + (v / 2 * 2)) * Openness(e);
        double tilt = v == 2 ? -8 : v == 3 ? 8 : 0;
        return Path($"M{F(cx - w)} {F(400 + tilt)} Q{F(cx)} {F(400 - (2 * h))} {F(cx + w)} {F(400 - tilt)} Q{F(cx)} {F(400 + (1.4 * h))} {F(cx - w)} {F(400 + tilt)} Z", stroke, 3, fill);
    }

    private static string EyesWhite(int v, Expression e) => EyeShape(430, v, e, "#FFFFFF", Dark) + EyeShape(594, v, e, "#FFFFFF", Dark);

    private static string EyesIris(int v, Expression e)
    {
        double r = 15 * Math.Min(1.1, Openness(e) + 0.2);
        return Circle(430, 398, r, "#FFFFFF") + Circle(594, 398, r, "#FFFFFF") + Circle(430, 398, r * 0.45, Dark) + Circle(594, 398, r * 0.45, Dark);
    }

    private static string Brows(int v, Expression e)
    {
        double thick = 8 + (v % 5 * 3);
        var (inner, outer, lift) = e switch
        {
            Expression.Bravo => (22.0, -6.0, 0.0),
            Expression.Preocupado or Expression.Triste => (-14.0, 8.0, 0.0),
            Expression.Surpreso => (0.0, 0.0, -22.0),
            Expression.Cansado => (4.0, 6.0, 4.0),
            _ => (0.0, 0.0, 0.0),
        };
        double arch = v is 1 or 9 ? 16 : v == 4 ? 10 : 6;
        double y = 345 + lift;
        string Brow(double xInner, double xOuter) =>
            Path($"M{F(xInner)} {F(y + inner)} Q{F((xInner + xOuter) / 2)} {F(y - arch)} {F(xOuter)} {F(y + outer)}", "#FFFFFF", thick);
        return Brow(482, 380) + Brow(542, 644) + (v == 6 ? Path($"M482 {F(y + inner)} L542 {F(y + inner)}", "#FFFFFF", thick * 0.5) : "");
    }

    private static string Nose(int v)
    {
        double w = 26 + (v % 5 * 5);
        double len = 70 + (v % 3 * 10);
        return Path($"M512 {F(400)} L{F(512 - (v == 4 ? 10 : 0))} {F(400 + len)}", Mid, 4)
            + Path($"M{F(512 - w)} {F(410 + len)} Q512 {F(430 + len)} {F(512 + w)} {F(410 + len)}", Mid, 5)
            + Ellipse(512, 410 + len, w * 0.9, 12, Shade, 0.5);
    }

    private static string Mouth(int v, Expression e, bool lipstick)
    {
        double w = 58 + (v % 4 * 8);
        double t = 10 + (v % 3 * 4);
        const double y = 540;
        string fill = "#FFFFFF";
        return e switch
        {
            Expression.Sorriso => Path($"M{F(512 - w)} {F(y - 6)} Q512 {F(y + 34)} {F(512 + w)} {F(y - 6)} Q512 {F(y + 8 + t)} {F(512 - w)} {F(y - 6)} Z", "none", fill: fill),
            Expression.SorrisoLargo => Path($"M{F(512 - w - 8)} {F(y - 10)} Q512 {F(y + 56)} {F(512 + w + 8)} {F(y - 10)} Z", "none", fill: fill)
                + (lipstick ? "" : Path($"M{F(512 - w)} {F(y - 4)} Q512 {F(y + 34)} {F(512 + w)} {F(y - 4)} Z", "none", fill: Dark)),
            Expression.Triste or Expression.Preocupado => Path($"M{F(512 - w)} {F(y + 14)} Q512 {F(y - 16)} {F(512 + w)} {F(y + 14)} Q512 {F(y - 2 + t)} {F(512 - w)} {F(y + 14)} Z", "none", fill: fill),
            Expression.Bravo => Path($"M{F(512 - w)} {F(y + 4)} L{F(512 + w)} {F(y + 4)} L{F(512 + w - 6)} {F(y + 4 + t)} L{F(512 - w + 6)} {F(y + 4 + t)} Z", "none", fill: fill),
            Expression.Surpreso => Ellipse(512, y + 8, 26, 34, fill) + (lipstick ? "" : Ellipse(512, y + 8, 16, 24, Dark)),
            Expression.Cansado => Path($"M{F(512 - w + 10)} {F(y + 6)} Q512 {F(y + 2)} {F(512 + w - 10)} {F(y + 8)} Q512 {F(y + 6 + t)} {F(512 - w + 10)} {F(y + 6)} Z", "none", fill: fill),
            _ => Path($"M{F(512 - w)} {F(y)} Q512 {F(y - t)} {F(512 + w)} {F(y)} Q512 {F(y + (1.6 * t))} {F(512 - w)} {F(y)} Z", "none", fill: fill),
        };
    }

    private static bool Tag(PartDefinition p, string tag) => p.Tags.Contains(tag);

    private static string HairBack(PartDefinition p)
    {
        if (Tag(p, "volume") || p.Id == "hair_black_power")
        {
            return Circle(512, 330, 300, "#FFFFFF") + Circle(512, 330, 300, "none", Mid, 4);
        }

        if (Tag(p, "preso"))
        {
            return p.Id == "hair_rabo_de_cavalo"
                ? Path("M600 300 Q760 420 700 720 Q660 760 640 700 Q690 480 590 360 Z", "none", fill: "#FFFFFF")
                : Circle(512, 170, 80, "#FFFFFF");
        }

        double bottom = Tag(p, "longo") ? 900 : Tag(p, "medio") ? 680 : 600;
        return Path($"M330 330 Q330 150 512 150 Q694 150 694 330 L{F(720)} {F(bottom)} L{F(304)} {F(bottom)} Z", "none", fill: "#FFFFFF");
    }

    private static string HairFront(PartDefinition p)
    {
        if (Tag(p, "calvicie"))
        {
            double top = p.BaldingStage == 1 ? 230 : 320;
            return Path($"M342 {F(top + 120)} Q342 {F(top)} 400 {F(top - 10)} L410 {F(top + 140)} Z", "none", fill: "#FFFFFF")
                + Path($"M682 {F(top + 120)} Q682 {F(top)} 624 {F(top - 10)} L614 {F(top + 140)} Z", "none", fill: "#FFFFFF")
                + (p.BaldingStage == 1 ? Path("M400 220 Q512 250 624 220 Q600 170 512 170 Q424 170 400 220 Z", "none", fill: "#FFFFFF") : "");
        }

        if (p.Id is "hair_raspado" or "hair_militar")
        {
            return Path("M346 330 Q346 180 512 178 Q678 180 678 330 Q600 250 512 250 Q424 250 346 330 Z", "none", fill: "#FFFFFF", opacity: p.Id == "hair_raspado" ? 0.55 : 0.9);
        }

        string cap = Path("M336 360 Q330 160 512 156 Q694 160 688 360 Q640 250 512 246 Q384 250 336 360 Z", "none", fill: "#FFFFFF");
        string texture = "";
        if (Tag(p, "cacheado") || Tag(p, "crespo"))
        {
            for (int i = 0; i < 9; i++)
            {
                texture += Circle(372 + (i * 35), 230 + (i % 2 * 18), 26, "#FFFFFF") + Circle(372 + (i * 35), 230 + (i % 2 * 18), 26, "none", Mid, 2);
            }
        }

        if (Tag(p, "trancas") || Tag(p, "dreads"))
        {
            for (int i = 0; i < 7; i++)
            {
                texture += Path($"M{F(380 + (i * 44))} 200 L{F(380 + (i * 44))} 320", Mid, 5);
            }
        }

        string fringe = p.Id == "hair_franja" ? Path("M360 300 Q512 360 664 300 L664 260 Q512 300 360 260 Z", "none", fill: "#FFFFFF") : "";
        string topete = p.Id is "hair_topete" or "hair_moicano" ? Path("M440 180 Q512 90 584 180 Z", "none", fill: "#FFFFFF") : "";
        string sides = Tag(p, "longo") || Tag(p, "medio")
            ? Path("M336 340 Q320 480 340 560 L370 560 Q360 450 380 340 Z", "none", fill: "#FFFFFF") + Path("M688 340 Q704 480 684 560 L654 560 Q664 450 644 340 Z", "none", fill: "#FFFFFF")
            : "";
        return cap + texture + fringe + topete + sides + Path("M380 240 Q512 200 644 240", Mid, 3);
    }

    private static string Beard(int v)
    {
        double fullness = v switch { 0 => 0.4, 7 => 0.3, 1 => 0.6, 2 or 8 => 1, 3 => 1.2, _ => 0.7 };
        string jaw = Path($"M352 470 Q352 {F(620 + (60 * fullness))} 512 {F(640 + (80 * fullness))} Q672 {F(620 + (60 * fullness))} 672 470 L640 470 Q620 590 512 600 Q404 590 384 470 Z", "none", fill: "#FFFFFF", opacity: 0.5 + (0.4 * Math.Min(1, fullness)));
        return v switch
        {
            4 => Path("M470 580 Q512 650 554 580 L540 570 Q512 600 484 570 Z", "none", fill: "#FFFFFF"),
            6 => Rect(340, 400, 30, 130, "#FFFFFF") + Rect(654, 400, 30, 130, "#FFFFFF"),
            10 => Ellipse(512, 620, 60, 30, "#FFFFFF"),
            _ => jaw,
        };
    }

    private static string Mustache(int v)
    {
        double w = 50 + (v % 4 * 10);
        double h = v is 2 or 1 ? 26 : v == 3 ? 8 : 16;
        string body = Path($"M{F(512 - w)} 520 Q512 {F(500 - h)} {F(512 + w)} 520 Q512 {F(510)} {F(512 - w)} 520 Z", "none", fill: "#FFFFFF");
        return v is 4 or 5 ? body + Path($"M{F(512 - w)} 520 L{F(512 - w - 4)} 580", "#FFFFFF", 10) + Path($"M{F(512 + w)} 520 L{F(512 + w + 4)} 580", "#FFFFFF", 10) : body;
    }

    private static string Scar(int v) => v switch
    {
        0 => Path("M580 450 L630 500", Mid, 5),
        1 => Path("M600 330 L612 372", Mid, 5),
        _ => Path("M500 600 L530 612", Mid, 5),
    };

    private static string Marks(string id) => id switch
    {
        "marks_sardas" => string.Concat(Enumerable.Range(0, 24).Select(i => Circle(430 + ((i * 37) % 180) + (i > 11 ? 0 : 0), 450 + ((i * 23) % 50), 4, "#9A7A66"))),
        "marks_pintas" => Circle(600, 470, 6, "#4A3A30") + Circle(440, 520, 5, "#4A3A30"),
        "marks_rugas" => Path("M440 300 Q512 290 584 300", Mid, 3) + Path("M450 318 Q512 310 574 318", Mid, 3) + Path("M450 520 Q440 560 460 590", Mid, 3) + Path("M574 520 Q584 560 564 590", Mid, 3),
        "marks_olheiras" => Path("M392 430 Q430 452 468 430", "#7A6A70", 6) + Path("M556 430 Q594 452 632 430", "#7A6A70", 6),
        _ => Path("M440 560 Q446 570 452 560", Mid, 3) + Path("M572 560 Q578 570 584 560", Mid, 3),
    };

    private static string Glasses(string id, int v)
    {
        bool round = id.Contains("redondo") || id.Contains("oval");
        string Lens(double cx) => round
            ? Ellipse(cx, 400, 62, 52, id.Contains("escuros") ? Mid : "none", 1, "#FFFFFF", 8)
            : Rect(cx - 66, 360, 132, 82 + (v % 3 * 6), id.Contains("escuros") ? Mid : "none", "#FFFFFF", 8);
        return Lens(430) + Lens(594) + Path("M496 396 Q512 386 528 396", "#FFFFFF", 7) + Path("M364 392 L340 380", "#FFFFFF", 7) + Path("M660 392 L684 380", "#FFFFFF", 7);
    }

    private static string Earrings(int v) => v switch
    {
        0 => Circle(342, 480, 16, "none", "#FFFFFF", 5) + Circle(682, 480, 16, "none", "#FFFFFF", 5),
        1 => Circle(342, 470, 7, "#FFFFFF") + Circle(682, 470, 7, "#FFFFFF"),
        2 => Rect(338, 470, 8, 40, "#FFFFFF") + Rect(678, 470, 8, 40, "#FFFFFF") + Circle(342, 515, 10, "#FFFFFF") + Circle(682, 515, 10, "#FFFFFF"),
        _ => Circle(342, 500, 30, "none", "#FFFFFF", 5) + Circle(682, 500, 30, "none", "#FFFFFF", 5),
    };

    private static string Piercing(string id) => id switch
    {
        "piercing_nariz_argola" => Circle(540, 488, 9, "none", "#FFFFFF", 4),
        "piercing_nariz_ponto" => Circle(540, 482, 5, "#FFFFFF"),
        _ => Circle(630, 340, 6, "#FFFFFF") + Circle(640, 360, 6, "#FFFFFF"),
    };

    private static string Necklace(int v) =>
        Path("M440 730 Q512 820 584 730", "#FFFFFF", v == 1 ? 10 : 5) + (v == 2 ? Circle(512, 800, 14, "#FFFFFF") : "");

    private static string OutfitMain(PartDefinition p, int v)
    {
        double w = 330;
        string shirt = Path($"M{F(512 - w)} 1024 Q{F(512 - w)} 770 362 730 L440 720 Q512 790 584 720 L662 730 Q{F(512 + w)} 770 {F(512 + w)} 1024 Z", "none", fill: "#FFFFFF")
            + Path("M380 860 L380 1024", Shade, 4) + Path("M644 860 L644 1024", Shade, 4);
        bool jacket = p.Id.Contains("terno") || p.Id.Contains("blazer") || p.Id.Contains("tailleur");
        return shirt + (jacket ? Path("M440 720 L500 1024 L420 1024 Z", Shade, 2, "none") + Path("M584 720 L524 1024 L604 1024 Z", Shade, 2, "none") : "")
            + (v % 3 == 0 ? Path("M360 900 L664 900", Shade, 3) : "");
    }

    private static string OutfitDetail(PartDefinition p)
    {
        if (p.Id.Contains("terno") || p.Id.Contains("blazer") || p.Id.Contains("tailleur"))
        {
            return Path("M470 726 L512 1024 L554 726 Q512 760 470 726 Z", "none", fill: "#FFFFFF");
        }

        if (p.Id.Contains("numero"))
        {
            return Rect(452, 860, 120, 90, "#FFFFFF");
        }

        if (p.Id.Contains("time"))
        {
            return Rect(200, 820, 624, 40, "#FFFFFF") + Rect(200, 900, 624, 40, "#FFFFFF");
        }

        return p.Id.Contains("xadrez")
            ? string.Concat(Enumerable.Range(0, 6).Select(i => Rect(220 + (i * 100), 780, 30, 244, "#FFFFFF", opacity: 0.6)))
            : Rect(300, 990, 424, 34, "#FFFFFF", opacity: 0.8);
    }

    private static string OutfitFront(PartDefinition p)
    {
        string collar = Path("M440 720 L512 790 L470 830 Z", "none", fill: "#FFFFFF") + Path("M584 720 L512 790 L554 830 Z", "none", fill: "#FFFFFF");
        return p.Id.Contains("gravata") ? collar + Path("M496 790 L528 790 L540 960 L512 990 L484 960 Z", "none", fill: "#FFFFFF") : p.Id.Contains("camiseta") || p.Id.Contains("moletom") ? Path("M440 720 Q512 780 584 720", "#FFFFFF", 12) : collar;
    }

    private static string Background(string id) => id switch
    {
        "background_listras" => Rect(0, 0, 1024, 1024, "#FFFFFF") + Rect(0, 0, 1024, 340, Shade) + Rect(0, 684, 1024, 340, Shade),
        "background_gabinete" => Rect(0, 0, 1024, 1024, "#FFFFFF") + Rect(80, 120, 260, 360, Shade) + Rect(700, 120, 240, 520, Shade) + Rect(0, 820, 1024, 204, Mid, opacity: 0.4),
        "background_cidade" => Rect(0, 0, 1024, 1024, "#FFFFFF") + string.Concat(Enumerable.Range(0, 9).Select(i => Rect(i * 118, 520 - (i * 47 % 200), 96, 600, Shade, opacity: 0.8))),
        _ => Rect(0, 0, 1024, 1024, "#FFFFFF"),
    };

    /// <summary>Ícone de logotipo em 256×256, branco sobre transparente.</summary>
    public static string Icon(string id)
    {
        string body = id switch
        {
            "sol" => Circle(128, 128, 48, "#FFFFFF") + string.Concat(Enumerable.Range(0, 12).Select(i =>
                Path($"M{F(128 + (70 * Math.Cos(i * Math.PI / 6)))} {F(128 + (70 * Math.Sin(i * Math.PI / 6)))} L{F(128 + (100 * Math.Cos(i * Math.PI / 6)))} {F(128 + (100 * Math.Sin(i * Math.PI / 6)))}", "#FFFFFF", 12))),
            "folha" => Path("M60 200 Q60 60 200 56 Q196 196 60 200 Z", "none", fill: "#FFFFFF") + Path("M70 190 L180 70", "#13233C", 6),
            "arvore" => Circle(128, 100, 70, "#FFFFFF") + Rect(112, 150, 32, 80, "#FFFFFF"),
            "onda" => Path("M30 140 Q70 90 110 140 T190 140 T230 140", "#FFFFFF", 22) + Path("M30 190 Q70 140 110 190 T190 190 T230 190", "#FFFFFF", 22),
            "montanha" => Path("M20 220 L100 70 L150 150 L180 110 L236 220 Z", "none", fill: "#FFFFFF"),
            "ponte" => Path("M20 180 Q128 40 236 180", "#FFFFFF", 22) + Rect(20, 176, 216, 22, "#FFFFFF"),
            "coluna" => Rect(80, 50, 96, 20, "#FFFFFF") + Rect(96, 70, 64, 130, "#FFFFFF") + Rect(72, 200, 112, 20, "#FFFFFF"),
            "livro" => Path("M40 70 Q90 50 128 80 Q166 50 216 70 L216 200 Q166 180 128 206 Q90 180 40 200 Z", "none", fill: "#FFFFFF") + Path("M128 80 L128 206", "#13233C", 6),
            "engrenagem" => Circle(128, 128, 70, "#FFFFFF") + string.Concat(Enumerable.Range(0, 8).Select(i => Circle(128 + (80 * Math.Cos(i * Math.PI / 4)), 128 + (80 * Math.Sin(i * Math.PI / 4)), 20, "#FFFFFF"))) + Circle(128, 128, 26, "#13233C"),
            "espiga" => Path("M128 230 L128 40", "#FFFFFF", 10) + string.Concat(Enumerable.Range(0, 5).Select(i => Ellipse(110, 60 + (i * 32), 16, 26, "#FFFFFF") + Ellipse(146, 60 + (i * 32), 16, 26, "#FFFFFF"))),
            "mao" => Rect(80, 110, 100, 110, "#FFFFFF") + string.Concat(Enumerable.Range(0, 4).Select(i => Rect(82 + (i * 26), 40, 20, 90, "#FFFFFF"))) + Rect(170, 120, 50, 22, "#FFFFFF"),
            "casa" => Path("M40 120 L128 40 L216 120 Z", "none", fill: "#FFFFFF") + Rect(64, 120, 128, 100, "#FFFFFF"),
            "coracao" => Path("M128 220 L40 120 Q20 60 80 50 Q118 46 128 90 Q138 46 176 50 Q236 60 216 120 Z", "none", fill: "#FFFFFF"),
            "gota" => Path("M128 30 Q200 130 190 170 Q180 226 128 226 Q76 226 66 170 Q56 130 128 30 Z", "none", fill: "#FFFFFF"),
            "farol" => Path("M100 220 L112 70 L144 70 L156 220 Z", "none", fill: "#FFFFFF") + Rect(100, 40, 56, 30, "#FFFFFF"),
            "ancora" => Path("M128 50 L128 210 M60 160 Q128 250 196 160", "#FFFFFF", 20) + Circle(128, 50, 20, "none", "#FFFFFF", 14) + Rect(88, 90, 80, 18, "#FFFFFF"),
            "catavento" => string.Concat(Enumerable.Range(0, 4).Select(i => Path($"M128 128 L{F(128 + (90 * Math.Cos(i * Math.PI / 2)))} {F(128 + (90 * Math.Sin(i * Math.PI / 2)))} L{F(128 + (60 * Math.Cos((i * Math.PI / 2) + 0.6)))} {F(128 + (60 * Math.Sin((i * Math.PI / 2) + 0.6)))} Z", "none", fill: "#FFFFFF"))),
            "triangulo" => Path("M128 30 L230 210 L26 210 Z", "none", fill: "#FFFFFF"),
            "hexagono" => Path("M128 24 L218 76 L218 180 L128 232 L38 180 L38 76 Z", "none", fill: "#FFFFFF"),
            "seta" => Path("M40 150 L160 150 L160 200 L230 128 L160 56 L160 106 L40 106 Z", "none", fill: "#FFFFFF"),
            "raio" => Path("M150 20 L60 140 L120 140 L100 236 L196 104 L134 104 Z", "none", fill: "#FFFFFF"),
            "estrela" => Path(StarPath(128, 132, 104, 44, 5), "none", fill: "#FFFFFF"),
            "tucano" => Ellipse(110, 150, 70, 60, "#FFFFFF") + Path("M160 120 Q236 120 236 150 Q200 170 160 160 Z", "none", fill: "#FFFFFF") + Circle(140, 128, 8, "#13233C"),
            "punho_cerrado" => Rect(78, 90, 100, 90, "#FFFFFF") + Rect(100, 180, 56, 56, "#FFFFFF") + string.Concat(Enumerable.Range(0, 4).Select(i => Path($"M{F(84 + (i * 24))} 92 L{F(84 + (i * 24))} 150", "#13233C", 4))),
            "rosa" => Circle(128, 100, 52, "#FFFFFF") + Path("M128 100 m-26 0 a26 26 0 1 0 52 0 a26 26 0 1 0 -52 0", "#13233C", 5) + Path("M128 150 L128 236", "#FFFFFF", 10) + Ellipse(100, 200, 24, 12, "#FFFFFF"),
            "pomba" => Path("M40 150 Q100 90 170 110 L230 80 L200 130 Q180 190 100 190 Q60 190 40 150 Z", "none", fill: "#FFFFFF"),
            "foice_e_martelo" => Path("M70 80 Q190 60 190 170", "#FFFFFF", 18) + Path("M80 210 L170 120", "#FFFFFF", 18) + Rect(150, 96, 60, 26, "#FFFFFF"),
            "girassol" => string.Concat(Enumerable.Range(0, 12).Select(i => Ellipse(128 + (66 * Math.Cos(i * Math.PI / 6)), 128 + (66 * Math.Sin(i * Math.PI / 6)), 22, 22, "#FFFFFF"))) + Circle(128, 128, 48, "#FFFFFF") + Circle(128, 128, 40, "#13233C", "none", 0),
            "chama" => Path("M128 230 Q50 200 70 130 Q90 90 110 40 Q130 100 160 110 Q210 150 180 200 Q160 232 128 230 Z", "none", fill: "#FFFFFF"),
            "ramo_de_oliveira" => Path("M50 210 Q128 150 200 40", "#FFFFFF", 10) + string.Concat(Enumerable.Range(0, 5).Select(i => Ellipse(80 + (i * 26), 180 - (i * 30), 22, 10, "#FFFFFF"))),
            "maos_dadas" => Rect(30, 110, 110, 50, "#FFFFFF") + Rect(116, 110, 110, 50, "#FFFFFF") + Circle(128, 135, 36, "#FFFFFF"),
            _ => Circle(128, 128, 96, "#FFFFFF"),
        };
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"256\" height=\"256\" viewBox=\"0 0 256 256\">{body}</svg>\n";
    }

    private static string StarPath(double cx, double cy, double outer, double inner, int points)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < points * 2; i++)
        {
            double r = i % 2 == 0 ? outer : inner;
            double a = (Math.PI / points * i) - (Math.PI / 2);
            sb.Append(i == 0 ? "M" : "L").Append(F(cx + (r * Math.Cos(a)))).Append(' ').Append(F(cy + (r * Math.Sin(a)))).Append(' ');
        }

        return sb.Append('Z').ToString();
    }

    private static string F(double value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    private static string Path(string d, string stroke, double width = 0, string fill = "none", double opacity = 1) =>
        $"<path d=\"{d}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{F(width)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" opacity=\"{F(opacity)}\"/>";

    private static string Ellipse(double cx, double cy, double rx, double ry, string fill, double opacity = 1, string stroke = "none", double width = 0) =>
        $"<ellipse cx=\"{F(cx)}\" cy=\"{F(cy)}\" rx=\"{F(rx)}\" ry=\"{F(ry)}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{F(width)}\" opacity=\"{F(opacity)}\"/>";

    private static string Circle(double cx, double cy, double r, string fill, string stroke = "none", double width = 0) =>
        $"<circle cx=\"{F(cx)}\" cy=\"{F(cy)}\" r=\"{F(r)}\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{F(width)}\"/>";

    private static string Rect(double x, double y, double w, double h, string fill, string stroke = "none", double width = 0, double opacity = 1) =>
        $"<rect x=\"{F(x)}\" y=\"{F(y)}\" width=\"{F(w)}\" height=\"{F(h)}\" rx=\"6\" fill=\"{fill}\" stroke=\"{stroke}\" stroke-width=\"{F(width)}\" opacity=\"{F(opacity)}\"/>";
}
