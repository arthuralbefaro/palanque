namespace Planalto.Core.Municipios;

/// <summary>Referência a uma divisão regional do IBGE (microrregião ou região imediata).</summary>
public sealed record RegiaoRef
{
    public required int Id { get; init; }

    public required string Nome { get; init; }
}

/// <summary>Município gerado pelo DataImporter em data/municipios/{UF}.json (GDD 12.3).</summary>
public sealed class Municipio
{
    public const string PerfilFonteMunicipio = "municipio";
    public const string PerfilFonteUf = "uf";
    public const string VagasFonteTse2024 = "tse_2024";
    public const string VagasFonteTeto = "teto_constitucional";
    public const string VagasFonteSemEleicao = "sem_eleicao_municipal";

    public required int CodigoIbge { get; init; }

    public required string Nome { get; init; }

    public required string Uf { get; init; }

    public required string Regiao { get; init; }

    /// <summary>Null para municípios criados depois do congelamento das microrregiões (2017).</summary>
    public required RegiaoRef? Microrregiao { get; init; }

    public required RegiaoRef RegiaoImediata { get; init; }

    public required double Lat { get; init; }

    public required double Lon { get; init; }

    public required long Populacao { get; init; }

    public required long Eleitorado { get; init; }

    public required double UrbanaPct { get; init; }

    public required string PerfilFonte { get; init; }

    public required PerfilCenso Perfil { get; init; }

    /// <summary>False no Distrito Federal e em Fernando de Noronha: sem prefeito nem vereadores.</summary>
    public required bool EleicoesMunicipais { get; init; }

    public required int VagasVereador { get; init; }

    public required int VagasVereadorMax { get; init; }

    public required string VagasFonte { get; init; }

    public required bool SegundoTurnoPrefeito { get; init; }

    public required string Porte { get; init; }
}

/// <summary>Contagens inteiras do Censo 2022; os percentuais são calculados a partir delas.</summary>
public sealed class PerfilCenso
{
    public required long PopulacaoCenso { get; init; }

    public required Dictionary<string, long> Situacao { get; init; }

    public required Dictionary<string, long> Idade { get; init; }

    public required Dictionary<string, long> Renda { get; init; }

    public required Dictionary<string, long> Religiao { get; init; }

    public required Dictionary<string, long> Escolaridade { get; init; }

    /// <summary>Percentual da população em situação urbana, com 1 casa decimal.</summary>
    public static double UrbanaPct(long urbana, long total) =>
        total <= 0 ? 0 : Math.Round(urbana * 100.0 / total, 1, MidpointRounding.AwayFromZero);
}

/// <summary>Chaves fixas de cada bloco do perfil, na ordem em que aparecem nos arquivos.</summary>
public static class PerfilCategorias
{
    public static readonly IReadOnlyList<string> Situacao = ["urbana", "rural"];

    /// <summary>16–17 e 70+ são as faixas de voto facultativo.</summary>
    public static readonly IReadOnlyList<string> Idade =
        ["0_15", "16_17", "18_24", "25_34", "35_44", "45_59", "60_69", "70_mais"];

    /// <summary>Rendimento nominal mensal domiciliar per capita, em salários mínimos de 2022.</summary>
    public static readonly IReadOnlyList<string> Renda =
    [
        "sem_rendimento", "ate_1_4_sm", "1_4_a_1_2_sm", "1_2_a_1_sm", "1_a_2_sm", "2_a_3_sm",
        "3_a_5_sm", "5_a_10_sm", "10_a_15_sm", "15_a_20_sm", "mais_de_20_sm",
    ];

    /// <summary>Pessoas de 15 anos ou mais.</summary>
    public static readonly IReadOnlyList<string> Religiao =
    [
        "catolica", "evangelica", "espirita", "umbanda_candomble", "tradicoes_indigenas",
        "outras", "sem_religiao", "nao_sabe", "sem_declaracao",
    ];

    /// <summary>Pessoas de 18 anos ou mais, por nível de instrução.</summary>
    public static readonly IReadOnlyList<string> Escolaridade =
        ["sem_instrucao_fundamental_incompleto", "fundamental_completo_medio_incompleto", "medio_completo_superior_incompleto", "superior_completo"];
}
