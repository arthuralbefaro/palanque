using System.Globalization;
using System.Text;
using Planalto.Core.Data;

namespace Planalto.Core.Parties;

public enum BlocklistGroup
{
    Registrado,
    /// <summary>Extinto, incorporado ou renomeado desde 1979 (inclui nomes e siglas anteriores).</summary>
    Historico,
    /// <summary>Partidos de 1945 a 1965.</summary>
    QuartaRepublica,
    Federacao,
}

public sealed class BlocklistSource
{
    public required string Id { get; init; }

    public string? Url { get; init; }

    public required DateOnly ConsultedOn { get; init; }

    /// <summary>O que veio desta fonte, ou por que nada pôde ser conferido nela.</summary>
    public required string Result { get; init; }
}

public sealed class BlocklistEntry
{
    /// <summary>Sigla real; null quando só o nome é conhecido (ex.: federações).</summary>
    public string? Sigla { get; init; }

    public required string Name { get; init; }

    public required BlocklistGroup Group { get; init; }

    public required List<string> Sources { get; init; }

    /// <summary>True só quando conferido na página oficial salva.</summary>
    public required bool Confirmed { get; init; }

    public string? Note { get; init; }
}

/// <summary>Conteúdo de data/parties/blocklist.json: partidos reais que nomes e siglas fictícios não podem repetir.</summary>
public sealed class BlocklistFile : IValidatable
{
    public required List<BlocklistSource> Sources { get; init; }

    /// <summary>Preposições, artigos e conjunções ignorados ao comparar nomes.</summary>
    public required List<string> IgnoredWords { get; init; }

    public required List<BlocklistEntry> Entries { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Sources is null || Sources.Count == 0)
        {
            yield return "a lista 'sources' está vazia.";
            yield break;
        }

        if (Entries is null || Entries.Count == 0)
        {
            yield return "a lista 'entries' está vazia.";
            yield break;
        }

        for (int i = 0; i < Sources.Count; i++)
        {
            BlocklistSource? source = Sources[i];
            if (source is null)
            {
                yield return $"sources[{i}]: item null.";
                continue;
            }

            foreach (string error in DataChecks.Id(source.Id, $"sources[{i}]").Concat(DataChecks.Text(source.Result, "result", $"sources[{i}] '{source.Id}'")))
            {
                yield return error;
            }
        }

        foreach (string error in DataChecks.UniqueIds(Sources.Select(s => s?.Id), "Fonte"))
        {
            yield return error;
        }

        var sourceIds = Sources.Where(s => s is not null).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var ignored = (IgnoredWords ?? []).ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<(string, string)>();
        for (int i = 0; i < Entries.Count; i++)
        {
            BlocklistEntry? entry = Entries[i];
            string where = $"entries[{i}]";
            if (entry is null)
            {
                yield return $"{where}: item null.";
                continue;
            }

            where = $"entries[{i}] '{entry.Name}'";
            foreach (string error in DataChecks.Text(entry.Name, "name", where))
            {
                yield return error;
            }

            if (!string.IsNullOrWhiteSpace(entry.Name) && PartyBlocklist.NormalizeName(entry.Name, ignored).Length == 0)
            {
                yield return $"{where}: o nome fica vazio depois de tirar as palavras ignoradas.";
            }

            if (entry.Sigla is not null && PartyBlocklist.NormalizeSigla(entry.Sigla).Length == 0)
            {
                yield return $"{where}: sigla '{entry.Sigla}' sem letras.";
            }

            if (entry.Sources is null || entry.Sources.Count == 0)
            {
                yield return $"{where}: sem fonte.";
            }
            else
            {
                foreach (string missing in entry.Sources.Where(s => !sourceIds.Contains(s)))
                {
                    yield return $"{where}: fonte '{missing}' não declarada em 'sources'.";
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.Name))
            {
                var key = (PartyBlocklist.NormalizeSigla(entry.Sigla ?? ""), PartyBlocklist.NormalizeName(entry.Name, ignored));
                if (!seen.Add(key))
                {
                    yield return $"{where}: entrada repetida (mesma sigla e mesmo nome normalizado).";
                }
            }
        }
    }
}

public enum BlockReason
{
    SiglaIgual,
    SiglaComLetrasReordenadas,
    NomeIgual,
    NomeContemNomeBloqueado,
}

public sealed record BlockMatch(BlockReason Reason, BlocklistEntry Entry);

/// <summary>
/// Compara nomes e siglas com a lista de bloqueio: sigla exata, sigla que só muda a ordem das letras
/// e nome normalizado (sem acentos, sem caixa e sem as palavras ignoradas) igual a um real ou que o contenha.
/// </summary>
public sealed class PartyBlocklist
{
    private readonly HashSet<string> _ignoredWords;
    private readonly Dictionary<string, BlocklistEntry> _siglas = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BlocklistEntry> _siglaLetters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BlocklistEntry> _names = new(StringComparer.Ordinal);

    public PartyBlocklist(BlocklistFile file)
    {
        File = file;
        _ignoredWords = file.IgnoredWords.Select(w => NormalizeWord(w)).ToHashSet(StringComparer.Ordinal);
        foreach (BlocklistEntry entry in file.Entries)
        {
            if (entry.Sigla is not null)
            {
                string sigla = NormalizeSigla(entry.Sigla);
                _siglas.TryAdd(sigla, entry);
                _siglaLetters.TryAdd(SortLetters(sigla), entry);
            }

            string name = NormalizeName(entry.Name, _ignoredWords);
            if (name.Length > 0)
            {
                _names.TryAdd(name, entry);
            }
        }
    }

    public BlocklistFile File { get; }

    public BlockMatch? CheckSigla(string sigla)
    {
        string normalized = NormalizeSigla(sigla);
        if (_siglas.TryGetValue(normalized, out BlocklistEntry? exact))
        {
            return new BlockMatch(BlockReason.SiglaIgual, exact);
        }

        return _siglaLetters.TryGetValue(SortLetters(normalized), out BlocklistEntry? anagram)
            ? new BlockMatch(BlockReason.SiglaComLetrasReordenadas, anagram)
            : null;
    }

    /// <summary>
    /// Bloqueia o nome igual a um real ou que contenha um nome real inteiro como sequência de palavras,
    /// depois da normalização ("Partido Liberal Brasileiro" contém "partido liberal").
    /// </summary>
    public BlockMatch? CheckName(string name)
    {
        string[] words = NormalizeName(name, _ignoredWords).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (_names.TryGetValue(string.Join(' ', words), out BlocklistEntry? exact))
        {
            return new BlockMatch(BlockReason.NomeIgual, exact);
        }

        for (int length = words.Length - 1; length >= 1; length--)
        {
            for (int start = 0; start + length <= words.Length; start++)
            {
                if (_names.TryGetValue(string.Join(' ', words, start, length), out BlocklistEntry? contained))
                {
                    return new BlockMatch(BlockReason.NomeContemNomeBloqueado, contained);
                }
            }
        }

        return null;
    }

    public bool IsBlocked(string name, string sigla) => CheckName(name) is not null || CheckSigla(sigla) is not null;

    /// <summary>Nome normalizado com as palavras ignoradas desta lista.</summary>
    public string NormalizeName(string name) => NormalizeName(name, _ignoredWords);

    /// <summary>Sigla em maiúsculas, sem acentos e só com letras e números ("PCdoB" vira "PCDOB").</summary>
    public static string NormalizeSigla(string sigla)
    {
        var builder = new StringBuilder(sigla.Length);
        foreach (char c in RemoveDiacritics(sigla))
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    public static string NormalizeName(string name, IReadOnlySet<string> ignoredWords)
    {
        IEnumerable<string> words = SplitWords(NormalizeWord(name)).Where(w => !ignoredWords.Contains(w));
        return string.Join(' ', words);
    }

    private static string SortLetters(string normalizedSigla)
    {
        char[] letters = normalizedSigla.ToCharArray();
        Array.Sort(letters);
        return new string(letters);
    }

    private static string NormalizeWord(string text) => RemoveDiacritics(text).ToLowerInvariant();

    private static IEnumerable<string> SplitWords(string text)
    {
        var word = new StringBuilder();
        foreach (char c in text)
        {
            if (char.IsLetterOrDigit(c))
            {
                word.Append(c);
            }
            else if (word.Length > 0)
            {
                yield return word.ToString();
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            yield return word.ToString();
        }
    }

    private static string RemoveDiacritics(string text)
    {
        string decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
