using System.Globalization;

namespace Planalto.DataImporter.Scenario;

/// <summary>Códigos de cargo do TSE (CD_CARGO).</summary>
public static class TseCargo
{
    public const int Presidente = 1;
    public const int VicePresidente = 2;
    public const int Governador = 3;
    public const int ViceGovernador = 4;
    public const int Senador = 5;
    public const int DeputadoFederal = 6;
    public const int DeputadoEstadual = 7;
    public const int DeputadoDistrital = 8;
    public const int Prefeito = 11;
    public const int VicePrefeito = 12;
    public const int Vereador = 13;
}

/// <summary>Uma linha do consulta_cand, com o partido já convertido para a sigla de 2026.</summary>
public sealed record CandidateRow(
    bool Supplementary,
    int Turno,
    DateOnly Date,
    string Uf,
    int Ue,
    int Cargo,
    string Party,
    int Federation,
    string Coligacao,
    string Titulo,
    string Situacao)
{
    public bool Elected => Situacao.StartsWith("ELEITO", StringComparison.Ordinal);
}

/// <summary>Votos válidos de um partido num cargo e numa unidade eleitoral (somadas as zonas).</summary>
public sealed record PartyVotes(bool Supplementary, int Turno, string Uf, int Ue, int Cargo, string Party, long Votes);

public sealed record CoalitionMember(bool Supplementary, int Ue, int Cargo, string Coligacao, string Party);

/// <summary>Leitura dos CSV de dados abertos do TSE usados no cenário Brasil 2026.</summary>
public static class TseElectionData
{
    public const string BaseUrl = "https://cdn.tse.jus.br/estatistica/sead/odsele";

    // CD_TIPO_ELEICAO: 2 = ordinária; os demais (1 = suplementar) entram como suplementares.
    private const string Ordinaria = "2";

    public static string CandidatesUrl(int year) => $"{BaseUrl}/consulta_cand/consulta_cand_{year}.zip";

    public static string PartyVotesUrl(int year) => $"{BaseUrl}/votacao_partido_munzona/votacao_partido_munzona_{year}.zip";

    public static string CoalitionsUrl(int year) => $"{BaseUrl}/consulta_coligacao/consulta_coligacao_{year}.zip";

    public static string SeatsUrl(int year) => $"{BaseUrl}/consulta_vagas/consulta_vagas_{year}.zip";

    public static List<CandidateRow> ReadCandidates(string zipPath, int year, IReadOnlySet<string> parties2026, Func<int, bool> cargoFilter)
    {
        var rows = new List<CandidateRow>();
        foreach (string[] f in Csv.ReadColumnsFromZip(zipPath, n => n.EndsWith($"_{year}_BRASIL.csv", StringComparison.OrdinalIgnoreCase),
            "CD_TIPO_ELEICAO", "NR_TURNO", "DT_ELEICAO", "SG_UF", "SG_UE", "CD_CARGO", "SG_PARTIDO", "NR_FEDERACAO",
            "SQ_COLIGACAO", "NR_TITULO_ELEITORAL_CANDIDATO", "DS_SIT_TOT_TURNO"))
        {
            int cargo = Int(f[5]);
            if (!cargoFilter(cargo))
            {
                continue;
            }

            rows.Add(new CandidateRow(
                f[0] != Ordinaria,
                Int(f[1]),
                DateOnly.ParseExact(f[2], "dd/MM/yyyy", CultureInfo.InvariantCulture),
                f[3],
                UeCode(f[4]),
                cargo,
                RealParties.Canonical(f[6], parties2026),
                Int(f[7]),
                f[8],
                f[9],
                f[10]));
        }

        return rows;
    }

    /// <summary>Votos válidos por partido (nominais + legenda), somando as zonas de cada unidade eleitoral.</summary>
    public static List<PartyVotes> ReadPartyVotes(string zipPath, int year, IReadOnlySet<string> parties2026, Func<int, bool> cargoFilter)
    {
        var totals = new Dictionary<(bool, int, string, int, int, string), long>();
        foreach (string[] f in Csv.ReadColumnsFromZip(zipPath, n => n.EndsWith($"_{year}_BRASIL.csv", StringComparison.OrdinalIgnoreCase),
            "CD_TIPO_ELEICAO", "NR_TURNO", "SG_UF", "SG_UE", "CD_CARGO", "SG_PARTIDO", "QT_TOTAL_VOTOS_LEG_VALIDOS", "QT_VOTOS_NOMINAIS_VALIDOS"))
        {
            int cargo = Int(f[4]);
            if (!cargoFilter(cargo))
            {
                continue;
            }

            var key = (f[0] != Ordinaria, Int(f[1]), f[2], UeCode(f[3]), cargo, RealParties.Canonical(f[5], parties2026));
            totals[key] = totals.GetValueOrDefault(key) + Long(f[6]) + Long(f[7]);
        }

        return totals.Select(t => new PartyVotes(t.Key.Item1, t.Key.Item2, t.Key.Item3, t.Key.Item4, t.Key.Item5, t.Key.Item6, t.Value)).ToList();
    }

    public static List<CoalitionMember> ReadCoalitions(string zipPath, int year, IReadOnlySet<string> parties2026, Func<int, bool> cargoFilter)
    {
        var rows = new List<CoalitionMember>();
        foreach (string[] f in Csv.ReadColumnsFromZip(zipPath, n => n.EndsWith($"_{year}_BRASIL.csv", StringComparison.OrdinalIgnoreCase),
            "CD_TIPO_ELEICAO", "SG_UE", "CD_CARGO", "SQ_COLIGACAO", "SG_PARTIDO"))
        {
            int cargo = Int(f[2]);
            if (cargoFilter(cargo))
            {
                rows.Add(new CoalitionMember(f[0] != Ordinaria, UeCode(f[1]), cargo, f[3], RealParties.Canonical(f[4], parties2026)));
            }
        }

        return rows;
    }

    /// <summary>Vagas da eleição por (UF ou unidade eleitoral, cargo).</summary>
    public static Dictionary<(string Uf, int Cargo), int> ReadSeatsByUf(string zipPath, int year)
    {
        var seats = new Dictionary<(string, int), int>();
        foreach (string[] f in Csv.ReadColumnsFromZip(zipPath, n => n.EndsWith($"_{year}_BRASIL.csv", StringComparison.OrdinalIgnoreCase),
            "SG_UF", "CD_CARGO", "QT_VAGA"))
        {
            var key = (f[0], Int(f[1]));
            seats[key] = seats.GetValueOrDefault(key) + Int(f[2]);
        }

        return seats;
    }

    /// <summary>Siglas normalizadas dos partidos que disputaram a eleição de 2026.</summary>
    public static HashSet<string> ReadParties2026(string zipPath)
    {
        var parties = new HashSet<string>(StringComparer.Ordinal);
        foreach (string[] f in Csv.ReadColumnsFromZip(zipPath, n => n.EndsWith("_2026_BRASIL.csv", StringComparison.OrdinalIgnoreCase), "SG_PARTIDO"))
        {
            parties.Add(RealParties.Normalize(f[0]));
        }

        return parties;
    }

    /// <summary>Números reais em uso em 2026, para os fictícios não repetirem nenhum (D-008).</summary>
    public static HashSet<int> ReadPartyNumbers2026(string zipPath)
    {
        var numbers = new HashSet<int>();
        foreach (string[] f in Csv.ReadColumnsFromZip(zipPath, n => n.EndsWith("_2026_BRASIL.csv", StringComparison.OrdinalIgnoreCase), "NR_PARTIDO"))
        {
            numbers.Add(Int(f[0]));
        }

        return numbers;
    }

    // SG_UE é o código TSE do município nas eleições municipais e a sigla da UF (ou "BR") nas gerais.
    private static int UeCode(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code) ? code : 0;

    private static int Int(string value) => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);

    private static long Long(string value) => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
}
