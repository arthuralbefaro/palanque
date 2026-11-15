using System.Globalization;

namespace Planalto.Core.Parties;

/// <summary>Cor no espaço OKLab, onde a distância euclidiana acompanha a diferença percebida.</summary>
public readonly record struct OkLab(double L, double A, double B)
{
    public double DistanceTo(OkLab other)
    {
        double dl = L - other.L;
        double da = A - other.A;
        double db = B - other.B;
        return Math.Sqrt((dl * dl) + (da * da) + (db * db));
    }
}

/// <summary>Conversões entre sRGB hexadecimal, OKLab e OKLCh (Björn Ottosson, 2020).</summary>
public static class OkColor
{
    public static OkLab FromOkLch(double lightness, double chroma, double hueDegrees)
    {
        double h = hueDegrees * Math.PI / 180.0;
        return new OkLab(lightness, chroma * Math.Cos(h), chroma * Math.Sin(h));
    }

    /// <summary>Hexadecimal "#RRGGBB" da cor, ou null se ela estiver fora do gamut sRGB.</summary>
    public static string? ToHex(OkLab color)
    {
        double l = Cube(color.L + (0.3963377774 * color.A) + (0.2158037573 * color.B));
        double m = Cube(color.L - (0.1055613458 * color.A) - (0.0638541728 * color.B));
        double s = Cube(color.L - (0.0894841775 * color.A) - (1.2914855480 * color.B));

        double r = (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s);
        double g = (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s);
        double b = (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s);

        const double tolerance = 1e-6;
        if (r < -tolerance || r > 1 + tolerance || g < -tolerance || g > 1 + tolerance || b < -tolerance || b > 1 + tolerance)
        {
            return null;
        }

        return $"#{Encode(r):X2}{Encode(g):X2}{Encode(b):X2}";
    }

    public static bool TryParseHex(string? hex, out OkLab color)
    {
        color = default;
        if (hex is null || hex.Length != 7 || hex[0] != '#'
            || !int.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int rgb))
        {
            return false;
        }

        double r = Decode((rgb >> 16) & 0xFF);
        double g = Decode((rgb >> 8) & 0xFF);
        double b = Decode(rgb & 0xFF);

        double l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        double m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        double s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));

        color = new OkLab(
            (0.2104542553 * l) + (0.7936177850 * m) - (0.0040720468 * s),
            (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s),
            (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s));
        return true;
    }

    public static bool IsValidHex(string? hex) => TryParseHex(hex, out _);

    private static double Cube(double x) => x * x * x;

    private static int Encode(double linear)
    {
        double c = Math.Clamp(linear, 0, 1);
        double encoded = c <= 0.0031308 ? 12.92 * c : (1.055 * Math.Pow(c, 1 / 2.4)) - 0.055;
        return (int)Math.Round(encoded * 255);
    }

    private static double Decode(int channel)
    {
        double c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
