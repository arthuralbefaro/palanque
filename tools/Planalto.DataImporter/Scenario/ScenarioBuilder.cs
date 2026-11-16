using Planalto.Core.Ideology;
using Planalto.Core.Municipios;
using Planalto.Core.Parties;
using Planalto.Core.Scenarios;
using Planalto.Core.State;

namespace Planalto.DataImporter.Scenario;

public sealed class ScenarioBuildResult
{
    public required NationalScenarioFile National { get; init; }

    public required List<StateScenarioFile> States { get; init; }
}

/// <summary>Dados brutos do TSE já lidos, com os partidos na sigla de 2026.</summary>
public sealed class ScenarioInputs
{
    public required IReadOnlySet<string> Parties2026 { get; init; }

    public required IReadOnlySet<int> RealNumbers2026 { get; init; }

    public required List<CandidateRow> Candidates2026 { get; init; }

    public required List<CandidateRow> Candidates2022 { get; init; }

    public required List<CandidateRow> Candidates2024 { get; init; }

    /// <summary>Prefeitos e vereadores de 2020 (reeleição e Câmara substituta, D-007 e D-011).</summary>
    public required List<CandidateRow> Municipais2020 { get; init; }

    public required List<PartyVotes> Votes2026 { get; init; }

    public required List<PartyVotes> Votes2024 { get; init; }

    public required List<CoalitionMember> Coalitions2026 { get; init; }

    public required List<CoalitionMember> Coalitions2024 { get; init; }

    public required Dictionary<(string Uf, int Cargo), int> Seats2026 { get; init; }

    public required List<ScenarioSource> Sources { get; init; }
}

/// <summary>
/// Gera o cenário "Brasil 2026" (GDD 16.4). A correspondência partido real → fictício só existe
/// dentro deste método; a saída tem apenas ids, números e identidades fictícias.
/// </summary>
public static class ScenarioBuilder
{
    public const string ScenarioId = "brasil_2026";

    // Semente fixa: o cenário gerado é sempre o mesmo para os mesmos dados.
    private const ulong Seed = 2026_10_04;

    // Famílias que a regra diagonal pode atribuir a partidos reais (DECISOES D-004).
    private static readonly string[] GeneralFamilies =
        ["esquerda_socialista", "esquerda", "centro_esquerda", "centro", "centro_direita", "direita_liberal", "direita_conservadora", "liberal_conservador"];

    public static ScenarioBuildResult Build(ScenarioInputs input, PartyContent content, MunicipiosDataSet municipios, IReadOnlyDictionary<int, int> tsePorIbge)
    {
        var gaps = new List<ScenarioGap>();
        var rng = new Rng(Seed);

        // Ids fictícios em ordem embaralhada, para a ordem não revelar a correspondência com os reais.
        var ids = rng.Shuffled(input.Parties2026.Order(StringComparer.Ordinal))
            .Select((real, i) => (Real: real, Id: $"p{i + 1:00}"))
            .ToDictionary(x => x.Real, x => x.Id, StringComparer.Ordinal);

        SeatTable camara = BuildCamara(input, ids, gaps);
        var senado = BuildSenado(input, ids);
        List<ScenarioParty> parties = BuildParties(input, content, ids, camara, rng);

        var winner = Latest(input.Candidates2026.Where(c => c.Cargo == TseCargo.Presidente && c.Elected))
            ?? throw new ImportException("Presidente eleito em 2026 não encontrado.");
        CandidateRow vice = Latest(input.Candidates2026.Where(c => c.Cargo == TseCargo.VicePresidente && c.Elected))
            ?? throw new ImportException("Vice-presidente eleito em 2026 não encontrado.");
        CandidateRow? runnerUp = input.Candidates2026
            .Where(c => c.Cargo == TseCargo.Presidente && c.Turno == 2 && !c.Elected)
            .FirstOrDefault();

        var national = new NationalScenarioFile
        {
            Id = ScenarioId,
            GeneralElectionYear = 2026,
            MunicipalElectionYear = 2024,
            Sources = input.Sources,
            Ideology = new ScenarioIdeologySource
            {
                Citation = RealParties.IdeologyCitation,
                Doi = RealParties.IdeologyDoi,
                Url = RealParties.IdeologyUrl,
                Method = "Escore esquerda–direita de 2022 (0 a 10, Tabela 1). Eixo econômico = (escore − 5) × 20; eixo de costumes = o mesmo valor "
                    + $"mais {ScenarioParty.SocialVariationNote}; família geral que contém o ponto (DECISOES D-001 a D-004).",
            },
            Parties = parties,
            Federations = BuildFederations(input, ids),
            Camara = camara,
            Senado = senado,
            CamaraVotes = ids.ToDictionary(
                p => p.Value,
                p => input.Votes2026.Where(v => !v.Supplementary && v.Cargo == TseCargo.DeputadoFederal && v.Party == p.Key).Sum(v => v.Votes)),
            CamaraValidVotes = input.Votes2026.Where(v => !v.Supplementary && v.Cargo == TseCargo.DeputadoFederal).Sum(v => v.Votes),
            Presidente = new ExecutiveSeat { Party = ids[winner.Party], ViceParty = ids[vice.Party] },
            BaseGovernista = CoalitionOf(input.Coalitions2026, winner, ids),
            Oposicao = runnerUp is null ? [] : CoalitionOf(input.Coalitions2026, runnerUp, ids),
            Gaps = gaps,
        };

        var prefeitos2020 = input.Municipais2020
            .Where(c => c.Elected && c.Cargo == TseCargo.Prefeito)
            .GroupBy(c => c.Ue)
            .ToDictionary(g => g.Key, g => g.Select(c => c.Titulo).ToHashSet(StringComparer.Ordinal));
        var index = new MunicipalIndex(
            input.Candidates2024.ToLookup(c => c.Ue),
            input.Votes2024.ToLookup(v => v.Ue),
            input.Coalitions2024.ToLookup(c => c.Ue),
            prefeitos2020,
            input.Municipais2020.Where(c => c.Elected && c.Cargo == TseCargo.Vereador).ToLookup(c => c.Ue));
        var states = municipios.Files
            .OrderBy(f => f.Uf, StringComparer.Ordinal)
            .Select(f => BuildState(f, input, ids, tsePorIbge, index, gaps))
            .ToList();

        return new ScenarioBuildResult { National = national, States = states };
    }

    private static SeatTable BuildCamara(ScenarioInputs input, Dictionary<string, string> ids, List<ScenarioGap> gaps)
    {
        var byParty = ids.Values.ToDictionary(id => id, _ => 0);
        int total = 0;
        foreach (string uf in input.Seats2026.Keys.Where(k => k.Cargo == TseCargo.DeputadoFederal).Select(k => k.Uf).Order(StringComparer.Ordinal))
        {
            int seats = input.Seats2026[(uf, TseCargo.DeputadoFederal)];
            var elected = ElectedOrPrevious(input, uf, TseCargo.DeputadoFederal, seats, gaps, "camara");
            foreach (CandidateRow row in elected)
            {
                byParty[ids[row.Party]]++;
            }

            total += seats;
        }

        return new SeatTable { Seats = total, ByParty = byParty };
    }

    /// <summary>
    /// Eleitos de 2026 na UF; se a UF não tiver nenhum eleito (resultado não publicado), os eleitos de 2022 (D-005).
    /// </summary>
    private static List<CandidateRow> ElectedOrPrevious(ScenarioInputs input, string uf, int cargo, int seats, List<ScenarioGap> gaps, string kind)
    {
        var elected = input.Candidates2026.Where(c => c.Uf == uf && c.Cargo == cargo && c.Elected).ToList();
        if (elected.Count == 0)
        {
            elected = input.Candidates2022.Where(c => c.Uf == uf && c.Cargo == cargo && c.Elected).ToList();
            gaps.Add(new ScenarioGap
            {
                Kind = kind,
                Uf = uf,
                Note = $"Sem eleitos de 2026 no consulta_cand (todas as candidaturas com situação #NULO); composição eleita em 2022 (DECISOES D-005).",
            });
        }

        if (elected.Count != seats)
        {
            throw new ImportException($"{uf}, cargo {cargo}: {elected.Count} eleitos para {seats} vagas.");
        }

        return elected;
    }

    private static SeatTable BuildSenado(ScenarioInputs input, Dictionary<string, string> ids)
    {
        var elected = input.Candidates2026.Concat(input.Candidates2022).Where(c => c.Cargo == TseCargo.Senador && c.Elected).ToList();
        var byParty = ids.Values.ToDictionary(id => id, _ => 0);
        foreach (CandidateRow row in elected)
        {
            byParty[ids[row.Party]]++;
        }

        return new SeatTable { Seats = elected.Count, ByParty = byParty };
    }

    private static List<ScenarioParty> BuildParties(ScenarioInputs input, PartyContent content, Dictionary<string, string> ids, SeatTable camara, Rng rng)
    {
        var generator = new PartyGenerator(content);
        var taken = new TakenIdentities(content.Blocklist);
        ScenarioMirrorConfig mirror = content.Generator.Scenario;
        var parties = new List<ScenarioParty>();

        foreach (var (real, id) in ids.OrderBy(p => p.Value, StringComparer.Ordinal))
        {
            FamilyDefinition family;
            IdeologicalPosition position;
            int? variation = null;
            double? score = RealParties.IdeologyScore2022.TryGetValue(real, out double s) ? s : null;
            if (score is double published)
            {
                int economic = Math.Clamp((int)Math.Round((published - 5) * 20, MidpointRounding.AwayFromZero), AxisRange.AxisMin, AxisRange.AxisMax);
                variation = rng.NextInt(-mirror.SocialVariationMax, mirror.SocialVariationMax + 1);
                position = new IdeologicalPosition(economic, Math.Clamp(economic + variation.Value, AxisRange.AxisMin, AxisRange.AxisMax));
                family = GeneralFamilyFor(position, content.Families);
            }
            else
            {
                // Sem classificação publicada: sorteio como num partido gerado (D-003).
                family = rng.PickWeighted(content.Families.Families, f => f.Weight);
                position = generator.GeneratePosition(rng, family);
            }

            GeneratedName name = generator.GenerateName(rng, family, taken)
                ?? throw new ImportException($"Sem nome livre para o partido {id}.");
            var free = Enumerable.Range(10, 90).Where(n => !input.RealNumbers2026.Contains(n) && !taken.Numbers.Contains(n)).ToList();
            int number = rng.Pick(free);
            PartyColors colors = generator.GenerateColors(rng, taken);
            taken.Add(name.Name, name.Sigla, number, colors);

            double share = camara.Seats == 0 ? 0 : camara.ByParty[id] / (double)camara.Seats;
            parties.Add(new ScenarioParty
            {
                Id = id,
                Name = name.Name,
                Sigla = name.Sigla,
                Number = number,
                FamilyId = family.Id,
                Position = position,
                Classified = score is not null,
                SourceScore = score,
                SocialVariation = variation,
                SocialVariationOrigin = variation is null ? null : ScenarioParty.SocialVariationNote,
                Size = mirror.SizeForSeatShare(share),
                Colors = colors,
                LogoIcon = generator.PickLogoIcon(rng, family.Id),
                Leadership = generator.PickLeadership(rng),
            });
        }

        return parties;
    }

    /// <summary>Família geral que contém o ponto; senão, a de retângulo mais próximo; empate, o centro mais próximo.</summary>
    public static FamilyDefinition GeneralFamilyFor(IdeologicalPosition point, FamilyCatalog families) =>
        GeneralFamilies
            .Select(families.Get)
            .OrderBy(f => DistanceToRange(point, f))
            .ThenBy(f => DistanceToCenter(point, f))
            .First();

    private static double DistanceToRange(IdeologicalPosition p, FamilyDefinition f)
    {
        double dx = Math.Max(Math.Max(f.Economic!.Min - p.Economic, 0), p.Economic - f.Economic.Max);
        double dy = Math.Max(Math.Max(f.Social!.Min - p.Social, 0), p.Social - f.Social.Max);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static double DistanceToCenter(IdeologicalPosition p, FamilyDefinition f)
    {
        double dx = p.Economic - ((f.Economic!.Min + f.Economic.Max) / 2.0);
        double dy = p.Social - ((f.Social!.Min + f.Social.Max) / 2.0);
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static List<ScenarioFederation> BuildFederations(ScenarioInputs input, Dictionary<string, string> ids) =>
        input.Candidates2026
            .Where(c => c.Federation > 0)
            .GroupBy(c => c.Federation)
            .OrderBy(g => g.Key)
            .Select((g, i) => new ScenarioFederation
            {
                Id = $"f{i + 1}",
                Members = g.Select(c => ids[c.Party]).Distinct().Order(StringComparer.Ordinal).ToList(),
            })
            .ToList();

    private static List<string> CoalitionOf(List<CoalitionMember> coalitions, CandidateRow candidate, Dictionary<string, string> ids)
    {
        var members = coalitions
            .Where(c => c.Cargo == candidate.Cargo && c.Coligacao == candidate.Coligacao && c.Supplementary == candidate.Supplementary
                && (candidate.Ue == 0 || c.Ue == candidate.Ue))
            .Select(c => ids[c.Party])
            .ToHashSet(StringComparer.Ordinal);
        members.Add(ids[candidate.Party]);
        return members.Order(StringComparer.Ordinal).ToList();
    }

    private static CandidateRow? Latest(IEnumerable<CandidateRow> rows) =>
        rows.OrderByDescending(r => r.Date).ThenByDescending(r => r.Turno).FirstOrDefault();

    private static StateScenarioFile BuildState(
        MunicipioUfFile file,
        ScenarioInputs input,
        Dictionary<string, string> ids,
        IReadOnlyDictionary<int, int> tsePorIbge,
        MunicipalIndex index,
        List<ScenarioGap> gaps)
    {
        string uf = file.Uf;
        CandidateRow governador = Latest(input.Candidates2026.Where(c => c.Uf == uf && c.Cargo == TseCargo.Governador && c.Elected))
            ?? throw new ImportException($"{uf}: governador eleito em 2026 não encontrado.");
        CandidateRow viceGov = Latest(input.Candidates2026.Where(c => c.Uf == uf && c.Cargo == TseCargo.ViceGovernador && c.Elected))
            ?? throw new ImportException($"{uf}: vice-governador eleito em 2026 não encontrado.");

        var senadores = input.Candidates2022.Where(c => c.Uf == uf && c.Cargo == TseCargo.Senador && c.Elected)
            .Select(c => new SenatorSeat { Party = ids[c.Party], ElectedIn = 2022 })
            .Concat(input.Candidates2026.Where(c => c.Uf == uf && c.Cargo == TseCargo.Senador && c.Elected)
                .Select(c => new SenatorSeat { Party = ids[c.Party], ElectedIn = 2026 }))
            .OrderBy(s => s.ElectedIn).ThenBy(s => s.Party, StringComparer.Ordinal)
            .ToList();
        if (senadores.Count != 3)
        {
            throw new ImportException($"{uf}: {senadores.Count} senadores eleitos (esperado 3).");
        }

        int cargoAssembleia = input.Seats2026.ContainsKey((uf, TseCargo.DeputadoDistrital)) ? TseCargo.DeputadoDistrital : TseCargo.DeputadoEstadual;
        int seats = input.Seats2026[(uf, cargoAssembleia)];
        int gapsBefore = gaps.Count;
        var assembleia = ElectedOrPrevious(input, uf, cargoAssembleia, seats, gaps, "assembleia");

        return new StateScenarioFile
        {
            Uf = uf,
            Governador = new ExecutiveSeat { Party = ids[governador.Party], ViceParty = ids[viceGov.Party] },
            Senadores = senadores,
            Assembleia = new SeatTable
            {
                Seats = seats,
                ByParty = Count(assembleia.Select(c => ids[c.Party])),
                SourceYear = gaps.Count > gapsBefore ? 2022 : null,
            },
            Municipios = file.Municipios
                .Where(m => m.EleicoesMunicipais)
                .Select(m => BuildMunicipio(m, tsePorIbge[m.CodigoIbge], index, ids, gaps))
                .ToList(),
        };
    }

    private static MunicipalScenario BuildMunicipio(
        Municipio municipio,
        int tse,
        MunicipalIndex index,
        Dictionary<string, string> ids,
        List<ScenarioGap> gaps)
    {
        var local = index.Candidates[tse].ToList();
        CandidateRow? prefeito = Latest(local.Where(c => c.Cargo == TseCargo.Prefeito && c.Elected));
        if (prefeito is null)
        {
            gaps.Add(new ScenarioGap
            {
                Kind = "prefeito",
                Uf = municipio.Uf,
                Ibge = municipio.CodigoIbge,
                Note = "Eleição de prefeito de 2024 anulada (candidaturas #NULO) e sem suplementar com resultado no consulta_cand_2024; prefeito pendente (D-011).",
            });
        }

        CandidateRow? vice = prefeito is null
            ? null
            : local.FirstOrDefault(c => c.Cargo == TseCargo.VicePrefeito && c.Elected && c.Date == prefeito.Date);

        var vereadores = local.Where(c => c.Cargo == TseCargo.Vereador && c.Elected && !c.Supplementary).ToList();
        if (vereadores.Count == 0)
        {
            vereadores = local.Where(c => c.Cargo == TseCargo.Vereador && c.Elected).ToList();
        }

        int? camaraFonteAno = null;
        if (vereadores.Count == 0)
        {
            // Sem eleitos de 2024 na fonte: a Câmara eleita em 2020, se tiver o mesmo número de cadeiras (D-011).
            var anteriores = index.Vereadores2020[tse].ToList();
            if (anteriores.Count == municipio.VagasVereador)
            {
                vereadores = anteriores;
                camaraFonteAno = 2020;
                gaps.Add(new ScenarioGap
                {
                    Kind = "camara",
                    Uf = municipio.Uf,
                    Ibge = municipio.CodigoIbge,
                    Note = "Sem vereadores eleitos de 2024 no consulta_cand (candidaturas #NULO); Câmara eleita em 2020 (D-011).",
                });
            }
        }

        if (vereadores.Count != municipio.VagasVereador)
        {
            gaps.Add(new ScenarioGap
            {
                Kind = "camara_sem_fonte",
                Uf = municipio.Uf,
                Ibge = municipio.CodigoIbge,
                Note = $"{vereadores.Count} vereadores de 2024 e {index.Vereadores2020[tse].Count()} de 2020 na fonte para {municipio.VagasVereador} vagas.",
            });
        }

        var coalitions = index.Coalitions[tse].ToList();
        var aliancas = coalitions
            .Where(c => c.Cargo == TseCargo.Prefeito && !c.Supplementary)
            .GroupBy(c => c.Coligacao)
            .Select(g => g.Select(c => ids[c.Party]).Distinct().Order(StringComparer.Ordinal).ToList())
            .Where(g => g.Count >= 2)
            .OrderBy(g => string.Join(',', g), StringComparer.Ordinal)
            .ToList();

        bool reeleito = prefeito is { Supplementary: false }
            && index.Prefeitos2020.TryGetValue(tse, out HashSet<string>? eleitos2020)
            && eleitos2020.Contains(prefeito.Titulo);

        return new MunicipalScenario
        {
            Ibge = municipio.CodigoIbge,
            Prefeito = prefeito is null ? null : ids[prefeito.Party],
            Vice = prefeito is null ? null : ids[(vice ?? prefeito).Party],
            PrefeitoReeleito = reeleito,
            EleicaoPrefeito = prefeito?.Date,
            Camara = Count(vereadores.Select(c => ids[c.Party])),
            CamaraFonteAno = camaraFonteAno,
            Base = prefeito is null ? [] : CoalitionOf(coalitions, prefeito, ids),
            ForcaVereador = BasisPoints(index.Votes[tse].Where(v => !v.Supplementary && v.Cargo == TseCargo.Vereador), ids),
            ForcaPrefeito = MayorStrength(index.Votes[tse], prefeito, ids),
            Aliancas = aliancas,
        };
    }

    private sealed record MunicipalIndex(
        ILookup<int, CandidateRow> Candidates,
        ILookup<int, PartyVotes> Votes,
        ILookup<int, CoalitionMember> Coalitions,
        Dictionary<int, HashSet<string>> Prefeitos2020,
        ILookup<int, CandidateRow> Vereadores2020);

    /// <summary>
    /// Votos válidos para prefeito da eleição que definiu o prefeito: 1º turno; se a fonte não tiver votos
    /// válidos dele, o 2º turno; se o prefeito veio de suplementar, os votos da suplementar (D-011).
    /// </summary>
    private static Dictionary<string, int> MayorStrength(IEnumerable<PartyVotes> votes, CandidateRow? prefeito, Dictionary<string, string> ids)
    {
        var mayor = votes.Where(v => v.Cargo == TseCargo.Prefeito && v.Votes > 0).ToList();
        bool supplementary = prefeito?.Supplementary ?? false;
        foreach (int turno in new[] { 1, 2 })
        {
            var round = mayor.Where(v => v.Supplementary == supplementary && v.Turno == turno).ToList();
            if (round.Count > 0)
            {
                return BasisPoints(round, ids);
            }
        }

        return [];
    }

    private static Dictionary<string, int> Count(IEnumerable<string> partyIds) =>
        partyIds.GroupBy(p => p).OrderBy(g => g.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count());

    /// <summary>Votos por partido em pontos-base que somam exatamente 10.000 (maiores restos).</summary>
    private static Dictionary<string, int> BasisPoints(IEnumerable<PartyVotes> votes, Dictionary<string, string> ids)
    {
        var byParty = votes.GroupBy(v => ids[v.Party]).ToDictionary(g => g.Key, g => g.Sum(v => v.Votes));
        long total = byParty.Values.Sum();
        if (total == 0)
        {
            return [];
        }

        var exact = byParty.Where(p => p.Value > 0).ToDictionary(p => p.Key, p => p.Value * (double)MunicipalScenario.BasisPoints / total);
        var points = exact.ToDictionary(p => p.Key, p => (int)Math.Floor(p.Value));
        int remaining = MunicipalScenario.BasisPoints - points.Values.Sum();
        foreach (string id in exact.OrderByDescending(p => p.Value - Math.Floor(p.Value)).ThenBy(p => p.Key, StringComparer.Ordinal).Take(remaining).Select(p => p.Key))
        {
            points[id]++;
        }

        return points.Where(p => p.Value > 0).OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key, p => p.Value);
    }
}
