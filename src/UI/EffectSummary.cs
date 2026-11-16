using System.Globalization;
using Godot;
using Planalto.Autoload;

namespace Planalto.UI;

/// <summary>Fotografia dos números que uma ação pode mexer, para mostrar o efeito real (antes × depois).</summary>
public sealed record EffectSnapshot(
    double Recognition,
    double Image,
    decimal Money,
    int Capital,
    IReadOnlyDictionary<string, double> Support,
    IReadOnlyDictionary<string, double> Relations,
    double? MayorRelation = null);

/// <summary>Texto do efeito de uma ação: "+3 de reconhecimento, −R$ 300". Nunca só "feito".</summary>
public static class EffectSummary
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Tr(string text) => TranslationServer.Translate(text);

    private static string Signed(double value, string format) =>
        (value >= 0 ? "+" : "−") + Math.Abs(value).ToString(format, PtBr);

    /// <param name="names">Nome de exibição de cada id (grupos de eleitores e pessoas da relação).</param>
    public static string Describe(EffectSnapshot before, EffectSnapshot after, Func<string, string> names)
    {
        var parts = new List<string>();
        double recognition = after.Recognition - before.Recognition;
        if (Math.Abs(recognition) >= 0.05)
        {
            parts.Add(string.Format(Tr("{0} de reconhecimento"), Signed(recognition, "0.#")));
        }

        double image = after.Image - before.Image;
        if (Math.Abs(image) >= 0.005)
        {
            parts.Add(string.Format(Tr("{0} de imagem"), Signed(image, "0.00")));
        }

        decimal money = after.Money - before.Money;
        if (money != 0)
        {
            parts.Add((money > 0 ? "+" : "−") + "R$ " + Math.Abs(money).ToString("N0", PtBr));
        }

        int capital = after.Capital - before.Capital;
        if (capital != 0)
        {
            parts.Add(string.Format(Tr("{0} de capital político"), Signed(capital, "0")));
        }

        foreach (string group in after.Support.Keys.Union(before.Support.Keys).Order(StringComparer.Ordinal))
        {
            double delta = (after.Support.GetValueOrDefault(group) - before.Support.GetValueOrDefault(group)) * 100;
            if (Math.Abs(delta) >= 0.5)
            {
                parts.Add(string.Format(Tr("{0} pontos de apoio no grupo {1}"), Signed(delta, "0"), names(group)));
            }
        }

        foreach (string id in after.Relations.Keys)
        {
            double delta = after.Relations[id] - before.Relations.GetValueOrDefault(id);
            if (Math.Abs(delta) >= 0.5)
            {
                parts.Add(string.Format(Tr("{0} de relação com {1}"), Signed(delta, "0"), names(id)));
            }
        }

        if (after.MayorRelation is { } mayor && before.MayorRelation is { } old && Math.Abs(mayor - old) >= 0.5)
        {
            parts.Add(string.Format(Tr("{0} de relação com o prefeito"), Signed(mayor - old, "0")));
        }

        return parts.Count == 0 ? Tr("sem efeito nos números desta vez") : string.Join(", ", parts);
    }

    /// <summary>Nome de exibição de um grupo de eleitores (data/electorate/segments.json), ou o próprio id.</summary>
    public static string GroupName(string id) =>
        Game.Instance.Electorate.Segments.Dimensions.SelectMany(d => d.Groups).FirstOrDefault(g => g.Id == id)?.Name ?? id;

    /// <summary>Linha do registro: nome da ação, texto do resultado (se houver) e o efeito medido.</summary>
    public static string Line(string action, string? text, string effect) =>
        string.IsNullOrWhiteSpace(text) ? $"{action}: {effect}." : $"{action}: {text} ({effect})";
}
