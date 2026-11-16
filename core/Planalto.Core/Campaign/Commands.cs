using System.Globalization;

namespace Planalto.Core.Campaign;

/// <summary>Por que um comando da R2b (contratar, repartir verba, mudar posição, prometer) não pode ser dado agora.</summary>
public enum CommandFailure
{
    NaoEncontrado,
    SemVaga,
    FuncaoOcupada,
    SemDinheiro,
    CanalFechado,
    LimiteLegal,
    GastoVedado,
    AlvoInvalido,
    ValorInvalido,
    JaPrometido,
    MesmaPosicao,
    BandeirasDemais,
    DecisaoPendente,
    ForaDaCampanha,
}

/// <summary>
/// Previsão de um comando do jogador (princípio f): linhas com faixa (o quê, para quem, mínimo, máximo e esperado),
/// desfechos sorteados com a chance, e a explicação por extenso (o quê, onde, quem, por quê), nunca vazia.
/// </summary>
public sealed record CommandForecast(
    CommandFailure? Blocked,
    IReadOnlyList<ForecastLine> Lines,
    IReadOnlyList<ForecastOutcome> Outcomes,
    IReadOnlyList<string> Explanation)
{
    public ForecastLine? Line(string kind, string? target = null) => Lines.FirstOrDefault(l => l.Kind == kind && l.Target == target);

    /// <summary>O valor cabe na faixa da linha (com folga de arredondamento); sem a linha, só o zero cabe.</summary>
    public bool Contains(string kind, string? target, double value)
    {
        ForecastLine? line = Line(kind, target);
        return line is null ? Math.Abs(value) < 1e-6 : value >= line.Min - 1e-6 && value <= line.Max + 1e-6;
    }
}

/// <summary>Naturezas de ganho e de custo de uma decisão (GDD 17.1, princípio e).</summary>
public static class DecisionNature
{
    public const string Dinheiro = "dinheiro";
    public const string Tempo = "tempo";
    public const string Relacao = "relacao";
    public const string Zona = "zona";
    public const string Risco = "risco";
    public const string Posicao = "posicao";

    public static readonly IReadOnlyList<string> All = [Dinheiro, Tempo, Relacao, Zona, Risco, Posicao];

    /// <summary>
    /// Regra do lint: a opção tem pelo menos um ganho e um custo, todos de naturezas conhecidas, e nenhuma natureza
    /// aparece dos dois lados (ganhar e perder a mesma coisa não é troca).
    /// </summary>
    public static IEnumerable<string> Check(IReadOnlyCollection<string>? gain, IReadOnlyCollection<string>? cost, string where)
    {
        if (gain is null || cost is null || gain.Count == 0 || cost.Count == 0)
        {
            yield return $"{where}: falta o ganho ou o custo.";
            yield break;
        }

        foreach (string nature in gain.Concat(cost).Where(n => !All.Contains(n)))
        {
            yield return $"{where}: natureza '{nature}' desconhecida.";
        }

        if (gain.Intersect(cost).Any())
        {
            yield return $"{where}: ganho e custo da mesma natureza.";
        }
    }
}

/// <summary>Lembrança definida em data/: peso (−3 a 3), pontos de relação e texto da ficha.</summary>
public sealed class MemoryDefinition
{
    public required int Peso { get; init; }

    public required int Pontos { get; init; }

    public required string Texto { get; init; }
}

internal static class Texts
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Money(decimal value) => "R$ " + Math.Round(value).ToString("N0", PtBr);

    public static string Number(double value, string format = "0.#") => value.ToString(format, PtBr);

    public static string Percent(double fraction) => (fraction * 100).ToString("0", PtBr) + "%";

    public static string Date(DateOnly date) => date.ToString("d 'de' MMMM", PtBr);

    /// <summary>Troca {chave} pelo valor; chaves sem valor ficam como estão (o validador de dados confere as conhecidas).</summary>
    public static string Fill(string template, params (string Key, string Value)[] values)
    {
        foreach (var (key, value) in values)
        {
            template = template.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        return template;
    }
}
