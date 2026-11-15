using System.Globalization;
using System.Text;

namespace Planalto.DataImporter.Scenario;

/// <summary>
/// Partidos reais e sua correspondência entre eleições. Esta tabela existe só no pipeline de dados:
/// os arquivos gerados em data/ guardam apenas ids e números fictícios (CLAUDE.md, Neutralidade).
/// </summary>
public static class RealParties
{
    public const string IdeologyCitation =
        "Bolognesi, B.; Ribeiro, E.; Codato, A.; Silva, B. F. O desaparecimento do centro ideológico no sistema partidário brasileiro: " +
        "a classificação mais atualizada dos experts. Opinião Pública, v. 31, e31120, 2025.";

    public const string IdeologyDoi = "10.1590/1807-0191202531120";

    public const string IdeologyUrl = "https://www.scielo.br/j/op/a/hv8GBg9hfCCZLwcWktfYhtC/";

    /// <summary>
    /// Sigla de partido extinto, incorporado ou renomeado → sigla do partido de 2026 que o sucede
    /// (fusões e incorporações deferidas pelo TSE; ver DECISOES D-002).
    /// </summary>
    private static readonly Dictionary<string, string> Successors = new(StringComparer.Ordinal)
    {
        ["PTB"] = "PRD",
        ["PATRIOTA"] = "PRD",
        ["PSL"] = "UNIAO",
        ["DEM"] = "UNIAO",
        ["PSC"] = "PODE",
        ["PROS"] = "SOLIDARIEDADE",
        ["PTC"] = "AGIR",
        ["PMN"] = "MOBILIZA",
        ["PMB"] = "DEMOCRATA",
    };

    /// <summary>
    /// Média esquerda–direita de 2022 (0 a 10) da Tabela 1 do artigo, pela sigla de 2026. Fusões usam a
    /// média das siglas de origem e renomeações mantêm o escore, como no próprio artigo (D-002).
    /// O Missão não consta da fonte e fica sem escore (D-003).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, double> IdeologyScore2022 = new Dictionary<string, double>(StringComparer.Ordinal)
    {
        ["PSTU"] = 0.51,
        ["PCO"] = 0.55,
        ["PCB"] = 0.69,
        ["PSOL"] = 1.41,
        ["UP"] = 1.63,
        ["PCDOB"] = 1.78,
        ["PT"] = 2.68,
        ["PSB"] = 3.59,
        ["REDE"] = 3.69,
        ["PDT"] = 3.86,
        ["PV"] = 4.12,
        ["SOLIDARIEDADE"] = 6.01,
        ["CIDADANIA"] = 6.17,
        ["AVANTE"] = 6.47,
        ["MDB"] = 6.50,
        ["MOBILIZA"] = 6.74,
        ["PSDB"] = 6.76,
        ["PSD"] = 6.94,
        ["DEMOCRATA"] = 7.29,
        ["PODE"] = 7.44,
        ["PRTB"] = 7.49,
        ["AGIR"] = 7.55,
        ["PP"] = 8.15,
        ["PRD"] = (7.72 + 8.60) / 2,
        ["DC"] = 8.21,
        ["REPUBLICANOS"] = 8.33,
        ["UNIAO"] = 8.49,
        ["NOVO"] = 8.67,
        ["PL"] = 8.80,
    };

    /// <summary>Sigla em maiúsculas, sem acentos e só com letras e números ("PC do B" → "PCDOB").</summary>
    public static string Normalize(string sigla)
    {
        var builder = new StringBuilder();
        foreach (char c in sigla.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Partido de 2026 (sigla normalizada) que corresponde à sigla de qualquer eleição.
    /// Lança <see cref="ImportException"/> se a sigla não tiver correspondência conhecida.
    /// </summary>
    public static string Canonical(string sigla, IReadOnlySet<string> parties2026)
    {
        string normalized = Normalize(sigla);
        if (parties2026.Contains(normalized))
        {
            return normalized;
        }

        if (Successors.TryGetValue(normalized, out string? successor) && parties2026.Contains(successor))
        {
            return successor;
        }

        throw new ImportException($"Sigla '{sigla}' sem correspondência com os partidos de 2026; atualize RealParties.Successors.");
    }
}
