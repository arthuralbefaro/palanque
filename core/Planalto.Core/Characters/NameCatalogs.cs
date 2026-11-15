using Planalto.Core.Data;

namespace Planalto.Core.Characters;

public sealed class RankedName
{
    public required string Nome { get; init; }

    public required long Frequencia { get; init; }
}

public sealed class NamesBySex
{
    public required List<RankedName> Masculino { get; init; }

    public required List<RankedName> Feminino { get; init; }
}

/// <summary>Conteúdo de data/names/first_names.json (IBGE), gerado pelo DataImporter.</summary>
public sealed class FirstNamesFile : IValidatable
{
    public required string Fonte { get; init; }

    public required string Url { get; init; }

    public required DateOnly ConsultadoEm { get; init; }

    public required NamesBySex Brasil { get; init; }

    public required Dictionary<string, NamesBySex> Ufs { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Brasil is null || Brasil.Masculino.Count == 0 || Brasil.Feminino.Count == 0)
        {
            yield return "'brasil' sem nomes.";
        }

        if (Ufs is null || Ufs.Count != 27)
        {
            yield return $"'ufs' deve ter as 27 UFs (tem {Ufs?.Count ?? 0}).";
        }

        foreach (var (uf, names) in Ufs ?? [])
        {
            if (names.Masculino.Count == 0 || names.Feminino.Count == 0 || names.Masculino.Concat(names.Feminino).Any(n => n.Frequencia <= 0 || string.IsNullOrWhiteSpace(n.Nome)))
            {
                yield return $"UF {uf}: listas vazias ou com frequência inválida.";
            }
        }
    }
}

/// <summary>
/// Conteúdo de data/names/names.json: acentos dos prenomes do IBGE (que vêm sem acento), sobrenomes
/// frequentes e padrões de nome de urna. Conteúdo do jogo: nenhuma pessoa real é retratada.
/// </summary>
public sealed class NameStyleCatalog : IValidatable
{
    /// <summary>Entradas do ranking que não são prenomes (ex.: "FILHA", parte de nomes compostos).</summary>
    public required List<string> Excluded { get; init; }

    /// <summary>Prenome sem acento em maiúsculas → grafia exibida (ex.: "JOAO" → "João").</summary>
    public required Dictionary<string, string> Accents { get; init; }

    /// <summary>Título de profissão usado no nome de urna, com as formas de gênero.</summary>
    public required Dictionary<string, GenderedText> Titles { get; init; }

    /// <summary>Sobrenomes com peso de sorteio.</summary>
    public required Dictionary<string, double> Surnames { get; init; }

    /// <summary>Padrões de nome de urna, com {prenome}, {sobrenome} e {titulo} (título de profissão com gênero).</summary>
    public required List<string> BallotPatterns { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Surnames is null || Surnames.Count < 30 || Surnames.Values.Any(w => w <= 0))
        {
            yield return "'surnames' precisa de ao menos 30 sobrenomes com peso positivo.";
        }

        if (BallotPatterns is null || BallotPatterns.Count == 0 || BallotPatterns.Any(p => !p.Contains("{prenome}", StringComparison.Ordinal)))
        {
            yield return "'ballot_patterns' vazio ou com padrão sem {prenome}.";
        }

        foreach (var (raw, shown) in Accents ?? [])
        {
            if (raw != raw.ToUpperInvariant() || !string.Equals(Strip(shown).ToUpperInvariant(), raw, StringComparison.Ordinal))
            {
                yield return $"acento '{raw}' → '{shown}': a grafia sem acento deve ser igual à chave.";
            }
        }
    }

    /// <summary>"JOAO" → "João"; sem entrada no mapa, só a capitalização.</summary>
    public string Display(string raw) =>
        Accents.TryGetValue(raw, out string? shown) ? shown : string.Concat(raw[..1], raw[1..].ToLowerInvariant());

    private static string Strip(string text) =>
        new(text.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
}
