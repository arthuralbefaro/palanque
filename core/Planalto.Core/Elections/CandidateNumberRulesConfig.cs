namespace Planalto.Core.Elections;

/// <summary>
/// Regras de numeração de partidos e candidatos (GDD 16.2). O padrão segue a Lei 9.504/1997, art. 15
/// (majoritários com o número do partido; deputado federal com mais 2 algarismos; estadual e distrital
/// com mais 3) e as resoluções do TSE para senador (mais 1) e vereador (mais 3).
/// </summary>
public sealed record CandidateNumberRulesConfig
{
    public static CandidateNumberRulesConfig Default { get; } = new();

    /// <summary>Menor número de partido (dois dígitos, GDD 16.1).</summary>
    public int PartyNumberMin { get; init; } = 10;

    /// <summary>Maior número de partido (dois dígitos, GDD 16.1).</summary>
    public int PartyNumberMax { get; init; } = 99;

    /// <summary>Total de dígitos do número do candidato, por cargo.</summary>
    public IReadOnlyDictionary<Office, int> Digits { get; init; } = new Dictionary<Office, int>
    {
        [Office.Prefeito] = 2,
        [Office.Governador] = 2,
        [Office.Presidente] = 2,
        [Office.Senador] = 3,
        [Office.DeputadoFederal] = 4,
        [Office.Vereador] = 5,
        [Office.DeputadoEstadual] = 5,
        [Office.DeputadoDistrital] = 5,
    };

    public int PartyNumberDigits => PartyNumberMax.ToString(System.Globalization.CultureInfo.InvariantCulture).Length;

    public int DigitsFor(Office office) =>
        Digits.TryGetValue(office, out int digits)
            ? digits
            : throw new ArgumentOutOfRangeException(nameof(office), office, "Cargo sem regra de numeração.");

    /// <summary>Dígitos que o candidato escolhe depois do número do partido (0 nos cargos majoritários).</summary>
    public int SuffixDigitsFor(Office office) => DigitsFor(office) - PartyNumberDigits;

    /// <summary>Quantidade de finais possíveis no cargo (1 nos majoritários: o próprio número do partido).</summary>
    public int SuffixCountFor(Office office) => (int)Math.Pow(10, SuffixDigitsFor(office));

    public bool IsValidPartyNumber(int number) => number >= PartyNumberMin && number <= PartyNumberMax;
}
