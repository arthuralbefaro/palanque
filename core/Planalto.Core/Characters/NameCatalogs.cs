using Planalto.Core.Data;

namespace Planalto.Core.Characters;

public sealed class RankedName
{
    public required string Nome { get; init; }

    public required long Frequencia { get; init; }
}

/// <summary>Prenome complementar com a frequência nacional por década (mesma chave de <see cref="FirstNamesFile.ByDecade"/>).</summary>
public sealed class DecadeName
{
    public required string Nome { get; init; }

    public required Dictionary<int, long> ByDecade { get; init; }
}

public sealed class ComplementaryNames
{
    public required List<DecadeName> Masculino { get; init; }

    public required List<DecadeName> Feminino { get; init; }
}

public sealed class NamesByPlace
{
    public required NamesBySex Brasil { get; init; }

    public required Dictionary<string, NamesBySex> Ufs { get; init; }
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

    /// <summary>
    /// Ranking por década de nascimento, com a chave do parâmetro "decada" da API do IBGE: a chave D cobre quem
    /// nasceu em [D − 10, D); a primeira (1930) cobre quem nasceu antes de 1930.
    /// </summary>
    public required Dictionary<int, NamesByPlace> ByDecade { get; init; }

    /// <summary>
    /// Prenomes fora dos rankings (que só trazem os 20 primeiros), com frequência oficial por década consultada
    /// nome a nome na mesma API; ampliam a variedade (DECISOES D-017).
    /// </summary>
    public required ComplementaryNames Complementary { get; init; }

    /// <summary>Chave de década da API para um ano de nascimento, limitada às décadas importadas.</summary>
    public int DecadeKey(int birthYear)
    {
        int key = ((birthYear / 10) + 1) * 10;
        return Math.Clamp(key, ByDecade.Keys.Min(), ByDecade.Keys.Max());
    }

    public IEnumerable<string> Validate()
    {
        if (ByDecade is null || ByDecade.Count == 0)
        {
            yield return "'by_decade' vazio.";
        }

        foreach (var (decade, place) in ByDecade ?? [])
        {
            if (place?.Brasil is null || place.Brasil.Masculino.Count == 0 || place.Brasil.Feminino.Count == 0 || place.Ufs is null || place.Ufs.Count != 27)
            {
                yield return $"década {decade}: falta o Brasil ou alguma das 27 UFs.";
            }
        }
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

    /// <summary>Frequência nacional mínima (soma das décadas) para um candidato complementar entrar.</summary>
    public required long ComplementaryMinFrequency { get; init; }

    /// <summary>Prenomes candidatos por sexo ("masculino", "feminino"), consultados no IBGE pelo importador.</summary>
    public required Dictionary<string, List<string>> ComplementaryCandidates { get; init; }

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
