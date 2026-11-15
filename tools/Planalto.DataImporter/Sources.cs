using System.Globalization;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter;

/// <summary>
/// Um bloco do perfil do Censo 2022: tabela do SIDRA, classificação que varia e como suas
/// categorias (ids do IBGE) se agrupam nas chaves de <see cref="PerfilCategorias"/>.
/// </summary>
public sealed record CensoBlock(
    string Name,
    int Table,
    int Variable,
    int ClassificationId,
    string Classificacao,
    int TotalCategory,
    IReadOnlyList<(string Key, int[] Categories)> Map,
    bool QueryPerUf = false,
    bool FromSample = false);

public sealed class CensoData
{
    /// <summary>Localidade (município N6 ou UF N3) → chave do perfil → contagem.</summary>
    public required Dictionary<string, Dictionary<string, long>> Values { get; init; }

    public required Dictionary<string, long> Totals { get; init; }
}

public sealed class RawSources
{
    public required List<IbgeMunicipio> Municipios { get; init; }

    /// <summary>Código IBGE → ponto representativo e área, da Malha Municipal.</summary>
    public required Dictionary<int, PontoMunicipio> Pontos { get; init; }

    public required int EstimativaAno { get; init; }

    /// <summary>Município (7 dígitos) ou UF (2 dígitos) → população estimada.</summary>
    public required Dictionary<string, long> Estimativa { get; init; }

    public required Dictionary<string, CensoData> Censo { get; init; }

    public required Dictionary<int, int> TseCodePorIbge { get; init; }

    public required Dictionary<int, long> EleitoradoPorTse { get; init; }

    public required DateOnly EleitoradoDataGeracao { get; init; }

    public required Dictionary<int, int> VagasVereador2024PorTse { get; init; }

    public required long VagasVereador2024Total { get; init; }

    public required Dictionary<string, CachedFile> Files { get; init; }
}

public static class Sources
{
    public const string TseMunicipiosUrl = "https://cdn.tse.jus.br/estatistica/sead/odsele/municipio_tse_ibge/municipio_tse_ibge.zip";
    public const string TseEleitoradoUrl = "https://cdn.tse.jus.br/estatistica/sead/odsele/perfil_eleitorado/perfil_eleitorado_ATUAL.zip";
    public const string TseVagasUrl = "https://cdn.tse.jus.br/estatistica/sead/odsele/consulta_vagas/consulta_vagas_2024.zip";

    private const int EstimativaTable = 6579;
    private const int EstimativaVariable = 9324;
    private const string CensoPeriod = "2022";

    // Ids de categoria do SIDRA (identificadores das tabelas, não dados).
    public static readonly IReadOnlyList<CensoBlock> CensoBlocks =
    [
        new("situacao", 10211, 93, 1, "2661[32776]|1[6795,1,2]", 6795,
        [
            ("urbana", [1]),
            ("rural", [2]),
        ]),
        new("idade", 9514, 93, 287,
            "2[6794]|286[113635]|287[100362,93070,93084,93085,6572,6573,6574,6575,6576,93087,93088,93089,93090,93091,93092,93093,93094,93095,93096,93097,93098,49108,49109,60040,60041,6653]",
            100362,
        [
            ("0_15", [93070, 93084, 93085, 6572]), // 0 a 4, 5 a 9, 10 a 14, 15 anos
            ("16_17", [6573, 6574]),
            ("18_24", [6575, 6576, 93087]), // 18, 19, 20 a 24
            ("25_34", [93088, 93089]),
            ("35_44", [93090, 93091]),
            ("45_59", [93092, 93093, 93094]),
            ("60_69", [93095, 93096]),
            ("70_mais", [93097, 93098, 49108, 49109, 60040, 60041, 6653]),
        ], QueryPerUf: true),
        new("renda", 10296, 13604, 386, "2[6794]|86[95251]|386[9680,9681,9682,9683,9684,9685,9686,9687,9688,9689,9690,9692]", 9680,
        [
            ("sem_rendimento", [9692]),
            ("ate_1_4_sm", [9681]),
            ("1_4_a_1_2_sm", [9682]),
            ("1_2_a_1_sm", [9683]),
            ("1_a_2_sm", [9684]),
            ("2_a_3_sm", [9685]),
            ("3_a_5_sm", [9686]),
            ("5_a_10_sm", [9687]),
            ("10_a_15_sm", [9688]),
            ("15_a_20_sm", [9689]),
            ("mais_de_20_sm", [9690]),
        ], FromSample: true),
        new("religiao", 10198, 950, 133, "133[95278,95263,95277,2826,2827,95274,95275,2836,12890,2837]|58[95253]|59[93024]", 95278,
        [
            ("catolica", [95263]),
            ("evangelica", [95277]),
            ("espirita", [2826]),
            ("umbanda_candomble", [2827]),
            ("tradicoes_indigenas", [95274]),
            ("outras", [95275]),
            ("sem_religiao", [2836]),
            ("nao_sabe", [12890]),
            ("sem_declaracao", [2837]),
        ], FromSample: true),
        new("escolaridade", 10061, 2667, 1568, "1568[120704,9493,9494,9495,99713]|58[95253]|2[6794]|86[95251]", 120704,
        [
            ("sem_instrucao_fundamental_incompleto", [9493]),
            ("fundamental_completo_medio_incompleto", [9494]),
            ("medio_completo_superior_incompleto", [9495]),
            ("superior_completo", [99713]),
        ], FromSample: true),
    ];

    /// <summary>Observação gravada em _fontes.json para os blocos que vêm da amostra do Censo.</summary>
    public const string SampleNote =
        "Dado da amostra do Censo 2022: cada categoria é estimada e arredondada separadamente pelo IBGE, "
        + "então a soma das categorias pode diferir do total publicado em algumas unidades. Os valores são gravados como publicados.";

    public static async Task<RawSources> FetchAsync(SourceCache cache)
    {
        var files = new Dictionary<string, CachedFile>(StringComparer.Ordinal);

        async Task<CachedFile> Get(string key, string fileName, string url)
        {
            CachedFile file = await cache.GetAsync(fileName, url);
            files.TryAdd(key, file);
            return file;
        }

        Console.WriteLine("IBGE — Localidades");
        List<IbgeMunicipio> municipios = Ibge.ReadLocalidades(
            (await Get("localidades", "ibge_localidades_municipios.json", Ibge.LocalidadesUrl)).Path);
        int[] ufIds = municipios.Select(m => m.UfId).Distinct().Order().ToArray();

        Console.WriteLine($"IBGE — Malha Municipal {Malha.Ano} (coordenadas e área)");
        CachedFile malhaFile = await Get("malha", Path.GetFileName(new Uri(Malha.Url).LocalPath), Malha.Url);
        Dictionary<int, PontoMunicipio> pontos = Malha.Read(malhaFile.Path);

        Console.WriteLine("IBGE — Estimativas da população");
        CachedFile estimativaMeta = await Get("estimativa_meta", $"sidra_{EstimativaTable}_metadados.json",
            $"{Ibge.AgregadosUrl}/{EstimativaTable}/metadados");
        int ano = Ibge.ReadLastPeriod(estimativaMeta.Path);
        var missing = new List<string>();
        CachedFile estimativaFile = await Get("estimativa", $"sidra_{EstimativaTable}_{ano}.json",
            Ibge.SidraUrl(EstimativaTable, ano.ToString(CultureInfo.InvariantCulture), EstimativaVariable, "N6[all]|N3[all]"));
        Dictionary<string, long> estimativa = Ibge.ReadSidra(estimativaFile.Path, ano.ToString(CultureInfo.InvariantCulture), null, missing)
            .ToDictionary(kv => kv.Key, kv => kv.Value[0], StringComparer.Ordinal);

        Console.WriteLine("IBGE — Censo 2022");
        var censo = new Dictionary<string, CensoData>(StringComparer.Ordinal);
        foreach (CensoBlock block in CensoBlocks)
        {
            var paths = new List<string>();
            if (block.QueryPerUf)
            {
                foreach (int ufId in ufIds)
                {
                    paths.Add((await Get($"censo_{block.Name}", $"sidra_{block.Table}_uf{ufId}.json",
                        Ibge.SidraUrl(block.Table, CensoPeriod, block.Variable, $"N6[N3[{ufId}]]", block.Classificacao))).Path);
                }

                paths.Add((await Get($"censo_{block.Name}", $"sidra_{block.Table}_ufs.json",
                    Ibge.SidraUrl(block.Table, CensoPeriod, block.Variable, "N3[all]", block.Classificacao))).Path);
            }
            else
            {
                paths.Add((await Get($"censo_{block.Name}", $"sidra_{block.Table}.json",
                    Ibge.SidraUrl(block.Table, CensoPeriod, block.Variable, "N6[all]|N3[all]", block.Classificacao))).Path);
            }

            censo[block.Name] = ReadCensoBlock(block, paths, missing);
        }

        if (missing.Count > 0)
        {
            throw new ImportException("Valores ausentes ou sigilosos no SIDRA:" + Environment.NewLine + string.Join(Environment.NewLine, missing.Take(30)));
        }

        Console.WriteLine("TSE — códigos, eleitorado e vagas");
        CachedFile mapFile = await Get("tse_codigos", "tse_municipio_tse_ibge.zip", TseMunicipiosUrl);
        var tsePorIbge = new Dictionary<int, int>();
        foreach (var row in Csv.ReadFromZip(mapFile.Path, _ => true))
        {
            tsePorIbge[int.Parse(row["CD_MUNICIPIO_IBGE"], CultureInfo.InvariantCulture)] =
                int.Parse(row["CD_MUNICIPIO_TSE"], CultureInfo.InvariantCulture);
        }

        CachedFile eleitoradoFile = await Get("eleitorado", "tse_perfil_eleitorado_ATUAL.zip", TseEleitoradoUrl);
        var eleitorado = new Dictionary<int, long>();
        DateOnly dataGeracao = default;
        Console.WriteLine("  somando o perfil do eleitorado por município (arquivo grande)");
        foreach (string[] row in Csv.ReadColumnsFromZip(eleitoradoFile.Path, _ => true, "SG_UF", "DT_GERACAO", "CD_MUNICIPIO", "QT_ELEITORES"))
        {
            if (row[0] == "ZZ")
            {
                continue; // eleitorado no exterior
            }

            if (dataGeracao == default)
            {
                dataGeracao = DateOnly.ParseExact(row[1], "dd/MM/yyyy", CultureInfo.InvariantCulture);
            }

            int codigo = int.Parse(row[2], CultureInfo.InvariantCulture);
            eleitorado[codigo] = eleitorado.GetValueOrDefault(codigo) + long.Parse(row[3], CultureInfo.InvariantCulture);
        }

        CachedFile vagasFile = await Get("vagas", "tse_consulta_vagas_2024.zip", TseVagasUrl);
        var vagas = new Dictionary<int, int>();
        long vagasTotal = 0;
        foreach (var row in Csv.ReadFromZip(vagasFile.Path, name => name.Contains("BRASIL", StringComparison.OrdinalIgnoreCase)))
        {
            // Só a eleição ordinária de 2024; suplementares repetiriam municípios.
            if (row["CD_TIPO_ELEICAO"] != "2" || row["CD_CARGO"] != "13")
            {
                continue;
            }

            int codigo = int.Parse(row["SG_UE"], CultureInfo.InvariantCulture);
            int qt = int.Parse(row["QT_VAGA"], CultureInfo.InvariantCulture);
            if (!vagas.TryAdd(codigo, qt))
            {
                throw new ImportException($"consulta_vagas_2024: município TSE {codigo} repetido na eleição ordinária.");
            }

            vagasTotal += qt;
        }

        return new RawSources
        {
            Municipios = municipios,
            Pontos = pontos,
            EstimativaAno = ano,
            Estimativa = estimativa,
            Censo = censo,
            TseCodePorIbge = tsePorIbge,
            EleitoradoPorTse = eleitorado,
            EleitoradoDataGeracao = dataGeracao,
            VagasVereador2024PorTse = vagas,
            VagasVereador2024Total = vagasTotal,
            Files = files,
        };
    }

    private static CensoData ReadCensoBlock(CensoBlock block, List<string> paths, List<string> missing)
    {
        var values = new Dictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        var totals = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (string path in paths)
        {
            foreach (var (localidade, byCategory) in Ibge.ReadSidra(path, CensoPeriod, block.ClassificationId, missing))
            {
                var grouped = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (var (key, categories) in block.Map)
                {
                    long sum = 0;
                    foreach (int category in categories)
                    {
                        if (!byCategory.TryGetValue(category, out long v))
                        {
                            throw new ImportException($"{block.Name}: localidade {localidade} sem a categoria {category} da tabela {block.Table}.");
                        }

                        sum += v;
                    }

                    grouped[key] = sum;
                }

                long total = byCategory.TryGetValue(block.TotalCategory, out long t)
                    ? t
                    : throw new ImportException($"{block.Name}: localidade {localidade} sem o total da tabela {block.Table}.");

                // Só os blocos do universo (situação e idade) têm de fechar exatamente com o total.
                if (!block.FromSample && grouped.Values.Sum() != total)
                {
                    throw new ImportException($"{block.Name}: localidade {localidade}: soma das categorias {grouped.Values.Sum()} diferente do total {total}.");
                }

                values[localidade] = grouped;
                totals[localidade] = total;
            }
        }

        return new CensoData { Values = values, Totals = totals };
    }
}
