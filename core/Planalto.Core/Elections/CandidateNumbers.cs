using System.Globalization;
using Planalto.Core.State;

namespace Planalto.Core.Elections;

/// <summary>Número de urna: número do partido seguido dos dígitos finais do cargo.</summary>
public readonly record struct CandidateNumber(Office Office, int PartyNumber, int Suffix, int SuffixDigits)
{
    /// <summary>Como aparece na urna, com zeros à esquerda nos dígitos finais ("45007").</summary>
    public string Text => SuffixDigits == 0
        ? PartyNumber.ToString(CultureInfo.InvariantCulture)
        : PartyNumber.ToString(CultureInfo.InvariantCulture) + Suffix.ToString(new string('0', SuffixDigits), CultureInfo.InvariantCulture);

    public override string ToString() => Text;
}

public enum CandidateNumberIssue
{
    NaoNumerico,
    QuantidadeDeDigitosErrada,
    NumeroDePartidoInvalido,
    PrefixoDeOutroPartido,
    FinalForaDaFaixa,
    JaEmUso,
    SemNumeroLivre,
}

public sealed record CandidateNumberChange(CandidateNumber Previous, CandidateNumber Current, bool KeptSuffix);

/// <summary>Montagem e validação de números de candidato (GDD 16.2), sem estado.</summary>
public static class CandidateNumbers
{
    public static CandidateNumberIssue? CheckSuffix(Office office, int partyNumber, int suffix, CandidateNumberRulesConfig? rules = null)
    {
        rules ??= CandidateNumberRulesConfig.Default;
        if (!rules.IsValidPartyNumber(partyNumber))
        {
            return CandidateNumberIssue.NumeroDePartidoInvalido;
        }

        return suffix < 0 || suffix >= rules.SuffixCountFor(office) ? CandidateNumberIssue.FinalForaDaFaixa : null;
    }

    public static CandidateNumber Create(Office office, int partyNumber, int suffix, CandidateNumberRulesConfig? rules = null)
    {
        rules ??= CandidateNumberRulesConfig.Default;
        CandidateNumberIssue? issue = CheckSuffix(office, partyNumber, suffix, rules);
        if (issue is not null)
        {
            throw new ArgumentException($"Número inválido para {office}: {issue}.");
        }

        return new CandidateNumber(office, partyNumber, suffix, rules.SuffixDigitsFor(office));
    }

    /// <summary>
    /// Lê um número digitado e confere o formato do cargo e o prefixo do partido.
    /// Devolve o problema encontrado, ou null com o número em <paramref name="number"/>.
    /// </summary>
    public static CandidateNumberIssue? TryParse(string text, Office office, int partyNumber, out CandidateNumber number, CandidateNumberRulesConfig? rules = null)
    {
        rules ??= CandidateNumberRulesConfig.Default;
        number = default;
        if (string.IsNullOrEmpty(text) || !text.All(char.IsAsciiDigit))
        {
            return CandidateNumberIssue.NaoNumerico;
        }

        if (text.Length != rules.DigitsFor(office))
        {
            return CandidateNumberIssue.QuantidadeDeDigitosErrada;
        }

        if (!rules.IsValidPartyNumber(partyNumber))
        {
            return CandidateNumberIssue.NumeroDePartidoInvalido;
        }

        int prefix = int.Parse(text.AsSpan(0, rules.PartyNumberDigits), CultureInfo.InvariantCulture);
        if (prefix != partyNumber)
        {
            return CandidateNumberIssue.PrefixoDeOutroPartido;
        }

        int suffix = text.Length == rules.PartyNumberDigits ? 0 : int.Parse(text.AsSpan(rules.PartyNumberDigits), CultureInfo.InvariantCulture);
        number = new CandidateNumber(office, partyNumber, suffix, rules.SuffixDigitsFor(office));
        return null;
    }

    /// <summary>
    /// Lê os dígitos finais digitados pelo jogador (a parte do candidato, depois do número do partido): exatamente
    /// os dígitos do cargo, com zeros à esquerda e dígitos repetidos permitidos ("000", "111"), e ainda livres no
    /// partido. Devolve o problema encontrado, ou null com o número completo em <paramref name="number"/>.
    /// </summary>
    /// <param name="occupied">Finais já usados por outras candidaturas do mesmo partido, cargo e circunscrição.</param>
    public static CandidateNumberIssue? TryParseSuffix(string? text, Office office, int partyNumber, IReadOnlyCollection<int> occupied, out CandidateNumber number, CandidateNumberRulesConfig? rules = null)
    {
        rules ??= CandidateNumberRulesConfig.Default;
        number = default;
        if (string.IsNullOrEmpty(text) || !text.All(char.IsAsciiDigit))
        {
            return CandidateNumberIssue.NaoNumerico;
        }

        if (text.Length != rules.SuffixDigitsFor(office))
        {
            return CandidateNumberIssue.QuantidadeDeDigitosErrada;
        }

        if (!rules.IsValidPartyNumber(partyNumber))
        {
            return CandidateNumberIssue.NumeroDePartidoInvalido;
        }

        int suffix = int.Parse(text, CultureInfo.InvariantCulture);
        if (occupied.Contains(suffix))
        {
            return CandidateNumberIssue.JaEmUso;
        }

        number = new CandidateNumber(office, partyNumber, suffix, rules.SuffixDigitsFor(office));
        return null;
    }

    /// <summary>Dígitos finais de um número completo já montado ("45007" → "007"); vazio se o texto não tem o tamanho do cargo.</summary>
    public static string SuffixOf(string? fullNumber, Office office, CandidateNumberRulesConfig? rules = null)
    {
        rules ??= CandidateNumberRulesConfig.Default;
        return fullNumber is not null && fullNumber.Length == rules.DigitsFor(office) ? fullNumber[rules.PartyNumberDigits..] : "";
    }
}

/// <summary>
/// Números em uso numa circunscrição (município, UF ou país) para uma eleição. Dentro do mesmo cargo e
/// partido nenhum número se repete; nos cargos majoritários o partido tem um único número, o dele.
/// </summary>
public sealed class CandidateNumberBook(CandidateNumberRulesConfig? rules = null)
{
    private readonly Dictionary<(string Constituency, Office Office, int Party), HashSet<int>> _used = [];

    public CandidateNumberRulesConfig Rules { get; } = rules ?? CandidateNumberRulesConfig.Default;

    public bool IsFree(string constituency, Office office, int partyNumber, int suffix) =>
        !_used.TryGetValue((constituency, office, partyNumber), out HashSet<int>? used) || !used.Contains(suffix);

    /// <summary>Dígitos finais já reservados no partido, para conferir o número que o jogador digita.</summary>
    public IReadOnlyCollection<int> UsedSuffixes(string constituency, Office office, int partyNumber) =>
        _used.TryGetValue((constituency, office, partyNumber), out HashSet<int>? used) ? used : [];

    /// <summary>Dígitos finais livres no partido, em ordem crescente, para o jogador escolher.</summary>
    public IReadOnlyList<int> FreeSuffixes(string constituency, Office office, int partyNumber)
    {
        if (!Rules.IsValidPartyNumber(partyNumber))
        {
            return [];
        }

        _used.TryGetValue((constituency, office, partyNumber), out HashSet<int>? used);
        return Enumerable.Range(0, Rules.SuffixCountFor(office)).Where(s => used is null || !used.Contains(s)).ToList();
    }

    public CandidateNumberIssue? TryReserve(string constituency, Office office, int partyNumber, int suffix, out CandidateNumber number)
    {
        number = default;
        CandidateNumberIssue? issue = CandidateNumbers.CheckSuffix(office, partyNumber, suffix, Rules);
        if (issue is not null)
        {
            return issue;
        }

        var key = (constituency, office, partyNumber);
        if (!_used.TryGetValue(key, out HashSet<int>? used))
        {
            used = [];
            _used[key] = used;
        }

        if (!used.Add(suffix))
        {
            return CandidateNumberIssue.JaEmUso;
        }

        number = new CandidateNumber(office, partyNumber, suffix, Rules.SuffixDigitsFor(office));
        return null;
    }

    /// <summary>Sorteia e reserva um final livre; null se o partido não tiver mais números no cargo.</summary>
    public CandidateNumber? ReserveRandom(Rng rng, string constituency, Office office, int partyNumber)
    {
        IReadOnlyList<int> free = FreeSuffixes(constituency, office, partyNumber);
        if (free.Count == 0)
        {
            return null;
        }

        TryReserve(constituency, office, partyNumber, rng.Pick(free), out CandidateNumber number);
        return number;
    }

    public bool Release(string constituency, CandidateNumber number) =>
        _used.TryGetValue((constituency, number.Office, number.PartyNumber), out HashSet<int>? used) && used.Remove(number.Suffix);

    /// <summary>
    /// Troca de partido: libera o número antigo e tenta manter os mesmos dígitos finais no partido novo;
    /// se estiverem ocupados, sorteia outro final livre. Null (sem liberar o antigo) se o partido novo não tiver número livre.
    /// </summary>
    public CandidateNumberChange? ChangeParty(Rng rng, string constituency, CandidateNumber current, int newPartyNumber)
    {
        if (!Rules.IsValidPartyNumber(newPartyNumber) || FreeSuffixes(constituency, current.Office, newPartyNumber).Count == 0)
        {
            return null;
        }

        Release(constituency, current);
        if (TryReserve(constituency, current.Office, newPartyNumber, current.Suffix, out CandidateNumber same) is null)
        {
            return new CandidateNumberChange(current, same, KeptSuffix: true);
        }

        CandidateNumber other = ReserveRandom(rng, constituency, current.Office, newPartyNumber)!.Value;
        return new CandidateNumberChange(current, other, KeptSuffix: false);
    }
}
