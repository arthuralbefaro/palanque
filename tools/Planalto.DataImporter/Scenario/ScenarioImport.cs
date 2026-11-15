using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Planalto.Core.Data;
using Planalto.Core.Municipios;
using Planalto.Core.Parties;
using Planalto.Core.Scenarios;

namespace Planalto.DataImporter.Scenario;

/// <summary>Modo "cenario" do importador: gera data/parties/scenarios/brasil_2026/.</summary>
public static class ScenarioImport
{
    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory)
    {
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        var (municipios, errors) = MunicipiosDataSet.Load(dataDirectory, portes);
        if (municipios is null || errors.Count > 0)
        {
            Console.Error.WriteLine("data/municipios/ inválido; rode o importador de municípios antes.");
            return 1;
        }

        PartyContent content = PartyContent.LoadOrThrow(dataDirectory);
        var sources = new List<ScenarioSource>();

        async Task<string> Get(string id, string fileName, string url, string note)
        {
            CachedFile file = await cache.GetAsync(fileName, url);
            sources.Add(new ScenarioSource { Id = id, Url = url, ConsultedOn = DateOnly.FromDateTime(file.DownloadedAt.UtcDateTime), Note = note });
            return file.Path;
        }

        Console.WriteLine("TSE — candidaturas, votos, coligações e vagas");
        string cand2026 = await Get("tse_cand_2026", "tse_consulta_cand_2026.zip", TseElectionData.CandidatesUrl(2026), "Candidaturas e eleitos de 2026 (todas as UFs).");
        string cand2024 = await Get("tse_cand_2024", "tse_consulta_cand_2024.zip", TseElectionData.CandidatesUrl(2024), "Candidaturas e eleitos de 2024, com as eleições suplementares posteriores.");
        string cand2022 = await Get("tse_cand_2022", "tse_consulta_cand_2022.zip", TseElectionData.CandidatesUrl(2022), "Senadores eleitos em 2022 e composição proporcional usada onde 2026 não tem resultado (D-005).");
        string cand2020 = await Get("tse_cand_2020", "tse_consulta_cand_2020.zip", TseElectionData.CandidatesUrl(2020), "Prefeitos e vereadores eleitos em 2020: reeleição em 2024 (D-007) e Câmara onde 2024 não tem eleitos (D-011).");
        string votes2026 = await Get("tse_votos_2026", "tse_votacao_partido_munzona_2026.zip", TseElectionData.PartyVotesUrl(2026), "Votos válidos por partido para a Câmara em 2026.");
        string votes2024 = await Get("tse_votos_2024", "tse_votacao_partido_munzona_2024.zip", TseElectionData.PartyVotesUrl(2024), "Votos válidos por partido para vereador e prefeito em 2024 (força local).");
        string colig2026 = await Get("tse_coligacoes_2026", "tse_consulta_coligacao_2026.zip", TseElectionData.CoalitionsUrl(2026), "Coligações presidenciais de 2026 (base e oposição, D-006).");
        string colig2024 = await Get("tse_coligacoes_2024", "tse_consulta_coligacao_2024.zip", TseElectionData.CoalitionsUrl(2024), "Coligações de prefeito de 2024 (base do prefeito e alianças).");
        string vagas2026 = await Get("tse_vagas_2026", "tse_consulta_vagas_2026.zip", TseElectionData.SeatsUrl(2026), "Vagas de 2026 por UF e cargo.");
        CachedFile codigos = await cache.GetAsync("tse_municipio_tse_ibge.zip", Sources.TseMunicipiosUrl);

        var tsePorIbge = new Dictionary<int, int>();
        foreach (var row in Csv.ReadFromZip(codigos.Path, _ => true))
        {
            tsePorIbge[int.Parse(row["CD_MUNICIPIO_IBGE"], CultureInfo.InvariantCulture)] = int.Parse(row["CD_MUNICIPIO_TSE"], CultureInfo.InvariantCulture);
        }

        HashSet<string> parties = TseElectionData.ReadParties2026(cand2026);
        Console.WriteLine($"  {parties.Count} partidos em 2026; lendo candidaturas e votos");
        var input = new ScenarioInputs
        {
            Parties2026 = parties,
            RealNumbers2026 = TseElectionData.ReadPartyNumbers2026(cand2026),
            Candidates2026 = TseElectionData.ReadCandidates(cand2026, 2026, parties, c => c <= TseCargo.DeputadoDistrital),
            Candidates2022 = TseElectionData.ReadCandidates(cand2022, 2022, parties, c => c is >= TseCargo.Senador and <= TseCargo.DeputadoDistrital),
            Candidates2024 = TseElectionData.ReadCandidates(cand2024, 2024, parties, c => c is >= TseCargo.Prefeito and <= TseCargo.Vereador),
            Municipais2020 = TseElectionData.ReadCandidates(cand2020, 2020, parties, c => c is TseCargo.Prefeito or TseCargo.Vereador),
            Votes2026 = TseElectionData.ReadPartyVotes(votes2026, 2026, parties, c => c == TseCargo.DeputadoFederal),
            Votes2024 = TseElectionData.ReadPartyVotes(votes2024, 2024, parties, c => c is TseCargo.Prefeito or TseCargo.Vereador),
            Coalitions2026 = TseElectionData.ReadCoalitions(colig2026, 2026, parties, c => c == TseCargo.Presidente),
            Coalitions2024 = TseElectionData.ReadCoalitions(colig2024, 2024, parties, c => c == TseCargo.Prefeito),
            Seats2026 = TseElectionData.ReadSeatsByUf(vagas2026, 2026),
            Sources = sources,
        };

        ScenarioBuildResult result = ScenarioBuilder.Build(input, content, municipios, tsePorIbge);
        Write(dataDirectory, result);
        return 0;
    }

    private static void Write(string dataDirectory, ScenarioBuildResult result)
    {
        string directory = Path.Combine(dataDirectory, ScenarioPaths.Brasil2026Directory);
        Directory.CreateDirectory(directory);
        foreach (string stale in Directory.EnumerateFiles(directory, "*.json"))
        {
            File.Delete(stale);
        }

        var indented = new JsonSerializerOptions(JsonData.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var compact = new JsonSerializerOptions(indented) { WriteIndented = false };

        File.WriteAllText(Path.Combine(directory, ScenarioPaths.NationalFile), JsonSerializer.Serialize(result.National, indented).ReplaceLineEndings("\n") + "\n");
        foreach (StateScenarioFile state in result.States)
        {
            File.WriteAllText(Path.Combine(directory, $"{state.Uf}.json"), JsonSerializer.Serialize(state, compact) + "\n");
        }

        long bytes = Directory.EnumerateFiles(directory, "*.json").Sum(f => new FileInfo(f).Length);
        Console.WriteLine();
        Console.WriteLine($"{result.States.Count} UFs, {result.States.Sum(s => s.Municipios.Count)} municípios, {bytes / 1024.0 / 1024.0:F2} MB em {directory}");
        Console.WriteLine($"Câmara {result.National.Camara.Seats}, Senado {result.National.Senado.Seats}, {result.National.Parties.Count} partidos, {result.National.Federations.Count} federações");
        Console.WriteLine($"Lacunas: {result.National.Gaps.Count}");
        foreach (var group in result.National.Gaps.GroupBy(g => g.Kind))
        {
            Console.WriteLine($"  {group.Key}: {group.Count()}");
            foreach (ScenarioGap gap in group.Take(12))
            {
                Console.WriteLine($"    {gap.Uf} {gap.Ibge} {gap.Note}");
            }
        }
    }
}
