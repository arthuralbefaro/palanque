using System.Globalization;
using Godot;
using Planalto.Core.Campaign;

namespace Planalto.UI;

/// <summary>Previsão antes do clique em texto (princípio f): faixa de cada efeito e os desfechos sorteados com a chance.</summary>
public static class ForecastText
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Tr(string text) => TranslationServer.Translate(text);

    private static string Signed(double v, string format) => (v >= 0 ? "+" : "−") + Math.Abs(v).ToString(format, PtBr);

    private static string Range(double min, double max, string format) =>
        Math.Abs(max - min) < 0.0005 ? Signed(min, format) : $"{Signed(min, format)} {Tr("a")} {Signed(max, format)}";

    /// <summary>Uma linha por efeito: "reconhecimento +1,2 a +3,0".</summary>
    public static List<string> Effects(Forecast f, Func<string, string> groupName)
    {
        var lines = new List<string>();
        foreach (ForecastLine l in f.Lines)
        {
            lines.Add(l.Kind switch
            {
                "recognition" => string.Format(Tr("reconhecimento {0}"), Range(l.Min, l.Max, "0.0")),
                "image" => string.Format(Tr("imagem {0}"), Range(l.Min, l.Max, "0.00")),
                "money" => Math.Abs(l.Max - l.Min) < 0.5
                    ? (l.Min >= 0 ? "+" : "−") + "R$ " + Math.Abs(l.Min).ToString("N0", PtBr)
                    : $"R$ {Signed(l.Min, "N0")} {Tr("a")} {Signed(l.Max, "N0")}",
                _ => string.Format(Tr("apoio no grupo {0}: {1} pontos"), groupName(l.Target ?? ""), Range(l.Min * 100, l.Max * 100, "0")),
            });
        }

        return lines;
    }

    /// <summary>Desfechos sorteados: "12%: Comício esvaziado...".</summary>
    public static List<string> Outcomes(Forecast f) =>
        f.Outcomes.Select(o => $"{o.Probability.ToString("P0", PtBr)}: {o.Text}").ToList();

    /// <summary>Texto completo para tooltip e cartão de decisão.</summary>
    public static string Full(Forecast f, Func<string, string> groupName)
    {
        var effects = Effects(f, groupName);
        var outcomes = Outcomes(f);
        string text = effects.Count == 0 ? Tr("sem efeito nos números") : string.Join("; ", effects);
        return outcomes.Count == 0 ? text : $"{text}. {Tr("Chances")}: {string.Join(" · ", outcomes)}";
    }
}
