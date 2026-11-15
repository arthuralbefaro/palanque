using Planalto.Core.Data;
using Planalto.Core.Ideology;
using Planalto.Core.Municipios;
using Planalto.Core.Parties;

namespace Planalto.Core.Scenarios;

/// <summary>Cenário "Brasil 2026" (GDD 16.4): arquivo nacional e 27 estaduais, já cruzados com os municípios.</summary>
public sealed class Brasil2026Scenario
{
    public const string GapPrefeito = "prefeito";
    public const string GapCamara = "camara";
    public const string GapAssembleia = "assembleia";
    public const string GapCamaraSemFonte = "camara_sem_fonte";

    private static readonly HashSet<string> KnownGaps = [GapPrefeito, GapCamara, GapAssembleia, GapCamaraSemFonte];

    private readonly Dictionary<int, MunicipalScenario> _municipios;

    private Brasil2026Scenario(NationalScenarioFile national, List<StateScenarioFile> states)
    {
        National = national;
        States = states.ToDictionary(s => s.Uf, StringComparer.Ordinal);
        _municipios = states.SelectMany(s => s.Municipios).ToDictionary(m => m.Ibge);
    }

    public NationalScenarioFile National { get; }

    public IReadOnlyDictionary<string, StateScenarioFile> States { get; }

    public MunicipalScenario? Municipio(int codigoIbge) => _municipios.GetValueOrDefault(codigoIbge);

    public static bool Exists(string dataDirectory) =>
        File.Exists(Path.Combine(dataDirectory, ScenarioPaths.Brasil2026Directory, ScenarioPaths.NationalFile));

    /// <summary>Só o arquivo nacional, suficiente para montar a lista de partidos da tela "Novo Mundo".</summary>
    public static (NationalScenarioFile? National, IReadOnlyList<string> Errors) LoadNational(string dataDirectory)
    {
        string path = Path.Combine(dataDirectory, ScenarioPaths.Brasil2026Directory, ScenarioPaths.NationalFile);
        if (!File.Exists(path))
        {
            return (null, [$"{ScenarioPaths.NationalFile}: arquivo não encontrado em {path}."]);
        }

        try
        {
            var national = System.Text.Json.JsonSerializer.Deserialize<NationalScenarioFile>(File.ReadAllText(path), JsonData.Options);
            return national is null ? (null, ["nacional.json vazio."]) : (national, []);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return (null, [$"nacional.json: JSON inválido. {ex.Message}"]);
        }
    }

    /// <summary>Carrega e valida o cenário inteiro; devolve null com os erros se algo não fechar.</summary>
    public static (Brasil2026Scenario? Scenario, IReadOnlyList<string> Errors) Load(string dataDirectory, MunicipiosDataSet municipios, PartyContent parties)
    {
        var (national, nationalErrors) = LoadNational(dataDirectory);
        if (national is null)
        {
            return (null, nationalErrors);
        }

        var errors = new List<string>();
        var states = new List<StateScenarioFile>();
        string directory = Path.Combine(dataDirectory, ScenarioPaths.Brasil2026Directory);
        foreach (string uf in municipios.Files.Select(f => f.Uf).Order(StringComparer.Ordinal))
        {
            string path = Path.Combine(directory, $"{uf}.json");
            if (!File.Exists(path))
            {
                errors.Add($"{uf}.json: arquivo não encontrado.");
                continue;
            }

            try
            {
                StateScenarioFile? state = System.Text.Json.JsonSerializer.Deserialize<StateScenarioFile>(File.ReadAllText(path), JsonData.Options);
                if (state is null)
                {
                    errors.Add($"{uf}.json: vazio.");
                }
                else
                {
                    states.Add(state);
                }
            }
            catch (System.Text.Json.JsonException ex)
            {
                errors.Add($"{uf}.json: JSON inválido. {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return (null, errors);
        }

        errors.AddRange(Validate(national, states, municipios, parties));
        return errors.Count > 0 ? (null, errors) : (new Brasil2026Scenario(national, states), errors);
    }

    public static IEnumerable<string> Validate(NationalScenarioFile national, List<StateScenarioFile> states, MunicipiosDataSet municipios, PartyContent content)
    {
        var errors = new List<string>();
        void Error(string message) => errors.Add(message);

        if (national.FormatVersion != NationalScenarioFile.CurrentFormatVersion)
        {
            Error($"nacional.json: versão de formato {national.FormatVersion} não suportada.");
        }

        var ids = national.Parties.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        if (ids.Count != national.Parties.Count)
        {
            Error("nacional.json: ids de partido repetidos.");
        }

        ValidateParties(national, content, Error);

        void CheckId(string? id, string where)
        {
            if (id is null || !ids.Contains(id))
            {
                Error($"{where}: partido '{id}' não existe no cenário.");
            }
        }

        foreach (ScenarioFederation federation in national.Federations)
        {
            if (federation.Members.Count < 2)
            {
                Error($"nacional.json: federação '{federation.Id}' com menos de dois partidos.");
            }

            federation.Members.ForEach(m => CheckId(m, $"federação '{federation.Id}'"));
        }

        CheckSeats(national.Camara, "nacional.json: Câmara", CheckId, Error);
        CheckSeats(national.Senado, "nacional.json: Senado", CheckId, Error);
        national.CamaraVotes.Keys.ToList().ForEach(k => CheckId(k, "nacional.json: votos para a Câmara"));
        CheckId(national.Presidente.Party, "nacional.json: presidente");
        CheckId(national.Presidente.ViceParty, "nacional.json: vice-presidente");
        national.BaseGovernista.ForEach(p => CheckId(p, "nacional.json: base"));
        national.Oposicao.ForEach(p => CheckId(p, "nacional.json: oposição"));
        if (!national.BaseGovernista.Contains(national.Presidente.Party))
        {
            Error("nacional.json: a base governista não contém o partido do presidente.");
        }

        foreach (ScenarioGap gap in national.Gaps.Where(g => !KnownGaps.Contains(g.Kind)))
        {
            Error($"nacional.json: lacuna de tipo desconhecido '{gap.Kind}'.");
        }

        bool HasGap(string kind, int ibge) => national.Gaps.Any(g => g.Kind == kind && g.Ibge == ibge);

        var expectedUfs = municipios.Files.Select(f => f.Uf).ToHashSet(StringComparer.Ordinal);
        if (!expectedUfs.SetEquals(states.Select(s => s.Uf)))
        {
            Error("cenário: os arquivos estaduais não correspondem às 27 UFs.");
        }

        int senators = 0;
        foreach (StateScenarioFile state in states)
        {
            string where = $"{state.Uf}.json";
            CheckId(state.Governador.Party, $"{where}: governador");
            CheckId(state.Governador.ViceParty, $"{where}: vice-governador");
            state.Senadores.ForEach(s => CheckId(s.Party, $"{where}: senador"));
            senators += state.Senadores.Count;
            if (state.Senadores.Count != 3 || state.Senadores.Count(s => s.ElectedIn == national.GeneralElectionYear) != 2)
            {
                Error($"{where}: esperados 3 senadores, 2 eleitos em {national.GeneralElectionYear}.");
            }

            CheckSeats(state.Assembleia, $"{where}: Assembleia", CheckId, Error);

            var byIbge = municipios.Files.First(f => f.Uf == state.Uf).Municipios.ToDictionary(m => m.CodigoIbge);
            var seen = new HashSet<int>();
            foreach (MunicipalScenario m in state.Municipios)
            {
                string at = $"{where}: município {m.Ibge}";
                if (!byIbge.TryGetValue(m.Ibge, out Municipio? municipio) || !municipio.EleicoesMunicipais)
                {
                    Error($"{at}: não é um município com eleição municipal da UF.");
                    continue;
                }

                if (!seen.Add(m.Ibge))
                {
                    Error($"{at}: repetido.");
                }

                if (m.Prefeito is null)
                {
                    if (!HasGap(GapPrefeito, m.Ibge))
                    {
                        Error($"{at}: sem prefeito e sem lacuna registrada.");
                    }
                }
                else
                {
                    CheckId(m.Prefeito, $"{at} prefeito");
                    CheckId(m.Vice, $"{at} vice");
                    if (!m.Base.Contains(m.Prefeito))
                    {
                        Error($"{at}: a base não contém o partido do prefeito.");
                    }
                }

                int seats = m.Camara.Values.Sum();
                if (seats != municipio.VagasVereador && !(seats == 0 && HasGap(GapCamaraSemFonte, m.Ibge)))
                {
                    Error($"{at}: Câmara com {seats} cadeiras para {municipio.VagasVereador} vagas.");
                }

                m.Camara.Keys.ToList().ForEach(p => CheckId(p, $"{at} Câmara"));
                m.Base.ForEach(p => CheckId(p, $"{at} base"));
                m.Aliancas.SelectMany(a => a).ToList().ForEach(p => CheckId(p, $"{at} aliança"));
                CheckBasisPoints(m.ForcaVereador, $"{at} força para vereador", allowEmpty: false, CheckId, Error);
                CheckBasisPoints(m.ForcaPrefeito, $"{at} força para prefeito", allowEmpty: m.Prefeito is null, CheckId, Error);
            }

            foreach (Municipio missing in byIbge.Values.Where(m => m.EleicoesMunicipais && !seen.Contains(m.CodigoIbge)))
            {
                Error($"{where}: município {missing.CodigoIbge} ({missing.Nome}) ausente do cenário.");
            }
        }

        if (senators != national.Senado.Seats)
        {
            Error($"cenário: {senators} senadores nos estados para {national.Senado.Seats} no Senado.");
        }

        return errors;
    }

    private static void ValidateParties(NationalScenarioFile national, PartyContent content, Action<string> error)
    {
        var numbers = new HashSet<int>();
        var siglas = new HashSet<string>(StringComparer.Ordinal);
        foreach (ScenarioParty party in national.Parties)
        {
            string where = $"nacional.json: partido '{party.Id}'";

            // Nenhum nome ou sigla real no arquivo final (GDD 16.4).
            if (content.Blocklist.CheckName(party.Name) is BlockMatch name)
            {
                error($"{where}: nome coincide com partido real ({name.Reason}).");
            }

            if (content.Blocklist.CheckSigla(party.Sigla) is BlockMatch sigla)
            {
                error($"{where}: sigla coincide com partido real ({sigla.Reason}).");
            }

            if (!siglas.Add(PartyBlocklist.NormalizeSigla(party.Sigla)) || !numbers.Add(party.Number))
            {
                error($"{where}: sigla ou número repetido.");
            }

            if (party.Number is < 10 or > 99)
            {
                error($"{where}: número {party.Number} fora de 10 a 99.");
            }

            if (content.Families.Find(party.FamilyId) is null || !party.Position.IsValid)
            {
                error($"{where}: família ou posição inválida.");
            }

            if (party.Classified != (party.SourceScore is not null) || (party.SocialVariation is not null) != party.Classified)
            {
                error($"{where}: escore e variação devem existir só nos partidos classificados.");
            }

            if (party.SocialVariation is int variation && Math.Abs(variation) > content.Generator.Scenario.SocialVariationMax)
            {
                error($"{where}: variação de costumes {variation} acima do limite.");
            }

            if (!OkColor.IsValidHex(party.Colors.Primary) || !OkColor.IsValidHex(party.Colors.Secondary) || !content.LogoIcons.Contains(party.LogoIcon))
            {
                error($"{where}: cores ou logotipo inválidos.");
            }
        }
    }

    private static void CheckSeats(SeatTable table, string where, Action<string?, string> checkId, Action<string> error)
    {
        table.ByParty.Keys.ToList().ForEach(p => checkId(p, where));
        if (table.ByParty.Values.Any(v => v < 0) || table.ByParty.Values.Sum() != table.Seats)
        {
            error($"{where}: soma das cadeiras ({table.ByParty.Values.Sum()}) diferente de {table.Seats}.");
        }
    }

    private static void CheckBasisPoints(Dictionary<string, int> shares, string where, bool allowEmpty, Action<string?, string> checkId, Action<string> error)
    {
        shares.Keys.ToList().ForEach(p => checkId(p, where));
        if (shares.Count == 0 && allowEmpty)
        {
            return;
        }

        if (shares.Values.Any(v => v <= 0) || shares.Values.Sum() != MunicipalScenario.BasisPoints)
        {
            error($"{where}: soma {shares.Values.Sum()} pontos-base (esperado {MunicipalScenario.BasisPoints}).");
        }
    }
}
