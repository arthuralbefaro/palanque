using Planalto.Core.Data;

namespace Planalto.Core.Indicators;

/// <summary>PIB do município (IBGE, PIB dos Municípios), em mil reais correntes.</summary>
public sealed class PibMunicipal
{
    public required int Ano { get; init; }

    public required long ValorMil { get; init; }

    public required long AgropecuariaMil { get; init; }

    public required long IndustriaMil { get; init; }

    public required long ServicosMil { get; init; }

    public required long AdministracaoPublicaMil { get; init; }
}

/// <summary>Estoque de emprego formal em 31/12 (MTE, RAIS, estabelecimentos: vínculos ativos).</summary>
public sealed class EmpregoFormal
{
    public required int Ano { get; init; }

    public required long Vinculos { get; init; }
}

/// <summary>
/// Finanças do ente (Tesouro Nacional, SICONFI). <see cref="Fonte"/> diz de onde veio: "declarado" (DCA do exercício
/// mais recente), "exercicio_anterior" (DCA de até 2 anos antes) ou "estimativa" (mediana por habitante dos municípios
/// do mesmo porte na UF; não é dado do ente). Valores em reais.
/// </summary>
public sealed class FinancasEnte
{
    public const string Declarado = "declarado";
    public const string ExercicioAnterior = "exercicio_anterior";
    public const string Estimativa = "estimativa";

    public required string Fonte { get; init; }

    public required int Exercicio { get; init; }

    /// <summary>Receitas correntes realizadas, líquidas das deduções (DCA, Anexo I-C).</summary>
    public required decimal ReceitaCorrente { get; init; }

    /// <summary>Despesa empenhada por função de governo (DCA, Anexo I-E), pela chave de dois dígitos da função.</summary>
    public required Dictionary<string, decimal> DespesaPorFuncao { get; init; }

    /// <summary>Receita corrente líquida do RGF (Anexo 01); null se o ente não entregou o RGF.</summary>
    public decimal? ReceitaCorrenteLiquida { get; init; }

    /// <summary>Despesa total com pessoal do Executivo, em % da RCL ajustada, como publicada no RGF.</summary>
    public decimal? PessoalPctRcl { get; init; }

    /// <summary>Exercício do RGF usado (pode diferir do da DCA).</summary>
    public int? RgfExercicio { get; init; }
}

/// <summary>
/// IDEB dos anos iniciais na rede municipal (INEP). <see cref="Fonte"/>: "municipio" (edição mais recente),
/// "edicao_anterior" (o município não teve nota na mais recente) ou "uf" (média da rede municipal na UF; não é dado do município).
/// </summary>
public sealed class IdebMunicipal
{
    public const string DoMunicipio = "municipio";
    public const string EdicaoAnterior = "edicao_anterior";
    public const string DaUf = "uf";

    /// <summary>UF sem rede municipal (Distrito Federal): nota da rede estadual do próprio município.</summary>
    public const string RedeEstadual = "rede_estadual";

    public required string Fonte { get; init; }

    public required int Ano { get; init; }

    public required decimal Valor { get; init; }

    /// <summary>Notas publicadas das edições lidas (ano → nota), para a inclinação inicial (GDD 17.17).</summary>
    public required Dictionary<int, decimal> Serie { get; init; }
}

/// <summary>
/// Saúde e segurança por município de residência (Ministério da Saúde: SIM, SINASC e CNES). As contagens são a soma
/// dos anos de <see cref="Anos"/>. A taxa de mortalidade infantil é a do município quando os nascimentos do período
/// alcançam o mínimo declarado em data/indicators/definitions.json; abaixo dele, a da região geográfica imediata.
/// </summary>
public sealed class SaudeMunicipal
{
    public const string DoMunicipio = "municipio";
    public const string DaRegiaoImediata = "regiao_imediata";

    public required List<int> Anos { get; init; }

    public required long ObitosInfantis { get; init; }

    public required long NascidosVivos { get; init; }

    /// <summary>Óbitos de menores de 1 ano por mil nascidos vivos.</summary>
    public required decimal MortalidadeInfantil { get; init; }

    public required string MortalidadeInfantilFonte { get; init; }

    /// <summary>Óbitos por agressão (CID-10 X85 a Y09) no período.</summary>
    public required long ObitosAgressao { get; init; }

    public required int LeitosCompetencia { get; init; }

    public required long Leitos { get; init; }
}

/// <summary>Saneamento (IBGE, Censo 2022): domicílios particulares permanentes ocupados.</summary>
public sealed class SaneamentoMunicipal
{
    public required long Domicilios { get; init; }

    /// <summary>Com ligação à rede geral de distribuição de água.</summary>
    public required long RedeAgua { get; init; }

    /// <summary>Com esgotamento por rede geral, rede pluvial ou fossa ligada à rede.</summary>
    public required long RedeEsgoto { get; init; }
}

public sealed class IndicadoresMunicipio
{
    public required int CodigoIbge { get; init; }

    /// <summary>Null quando a fonte não traz o município (criado depois do ano de referência); a lacuna fica em _fontes.json.</summary>
    public required PibMunicipal? Pib { get; init; }

    public required EmpregoFormal Emprego { get; init; }

    public required FinancasEnte Financas { get; init; }

    public required IdebMunicipal Ideb { get; init; }

    public required SaudeMunicipal Saude { get; init; }

    /// <summary>Null quando o Censo 2022 não traz o município (criado depois); a lacuna fica em _fontes.json.</summary>
    public required SaneamentoMunicipal? Saneamento { get; init; }
}

/// <summary>data/indicadores_reais/{UF}.json: ponto de partida oficial de cada município (GDD 17.17).</summary>
public sealed class IndicadoresUfFile : IValidatable
{
    public required string Uf { get; init; }

    public required List<IndicadoresMunicipio> Municipios { get; init; }

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Municipios.Select(m => m.CodigoIbge.ToString(System.Globalization.CultureInfo.InvariantCulture)), "Município"))
        {
            yield return error;
        }

        foreach (IndicadoresMunicipio m in Municipios)
        {
            if (m.Financas.Fonte is not (FinancasEnte.Declarado or FinancasEnte.ExercicioAnterior or FinancasEnte.Estimativa))
            {
                yield return $"{m.CodigoIbge}: fonte de finanças '{m.Financas.Fonte}' desconhecida.";
            }

            if (m.Ideb.Fonte is not (IdebMunicipal.DoMunicipio or IdebMunicipal.EdicaoAnterior or IdebMunicipal.DaUf or IdebMunicipal.RedeEstadual))
            {
                yield return $"{m.CodigoIbge}: fonte de IDEB '{m.Ideb.Fonte}' desconhecida.";
            }

            if (m.Saude.MortalidadeInfantilFonte is not (SaudeMunicipal.DoMunicipio or SaudeMunicipal.DaRegiaoImediata))
            {
                yield return $"{m.CodigoIbge}: fonte de mortalidade infantil '{m.Saude.MortalidadeInfantilFonte}' desconhecida.";
            }

            if (m.Saneamento is { } s && (s.RedeAgua > s.Domicilios || s.RedeEsgoto > s.Domicilios))
            {
                yield return $"{m.CodigoIbge}: saneamento com mais domicílios ligados do que domicílios.";
            }

            if (m.Financas.ReceitaCorrente <= 0)
            {
                yield return $"{m.CodigoIbge}: receita corrente não positiva.";
            }
        }
    }
}

/// <summary>Contexto da UF: finanças do estado, desemprego (PNAD Contínua) e médias usadas nas substituições.</summary>
public sealed class IndicadoresUf
{
    public required string Uf { get; init; }

    public required FinancasEnte? Financas { get; init; }

    /// <summary>Taxa de desocupação da UF (IBGE, PNAD Contínua trimestral), em %.</summary>
    public required decimal Desocupacao { get; init; }

    /// <summary>Trimestre da taxa, no formato AAAATT do SIDRA (ex.: 202602).</summary>
    public required int DesocupacaoPeriodo { get; init; }

    /// <summary>Média do IDEB dos anos iniciais da rede municipal na UF (média simples dos municípios com nota).</summary>
    public required decimal IdebMedioRedeMunicipal { get; init; }
}

/// <summary>data/indicadores_reais/_ufs.json.</summary>
public sealed class IndicadoresUfsFile : IValidatable
{
    public required List<IndicadoresUf> Ufs { get; init; }

    public IEnumerable<string> Validate() => DataChecks.UniqueIds(Ufs.Select(u => u.Uf), "UF");
}

public static class IndicadoresPaths
{
    public const string Directory = "indicadores_reais";

    public const string UfsFileName = "_ufs.json";

    public const string FontesFileName = "_fontes.json";

    public static string File(string dataDirectory, string uf) => Path.Combine(dataDirectory, Directory, $"{uf}.json");

    public static DataLoadResult<IndicadoresUfFile> Load(string dataDirectory, string uf) => JsonData.LoadFile<IndicadoresUfFile>(File(dataDirectory, uf));

    public static DataLoadResult<IndicadoresUfsFile> LoadUfs(string dataDirectory) =>
        JsonData.LoadFile<IndicadoresUfsFile>(Path.Combine(dataDirectory, Directory, UfsFileName));
}
