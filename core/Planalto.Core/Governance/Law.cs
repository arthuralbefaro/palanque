using Planalto.Core.State;

namespace Planalto.Core.Governance;

/// <summary>Em que faixa da Lei de Responsabilidade Fiscal está a despesa com pessoal do Executivo municipal.</summary>
public enum PayrollBand
{
    Normal,

    /// <summary>90% do limite: o Tribunal de Contas alerta (LC 101/2000, art. 59, § 1º, II).</summary>
    Alerta,

    /// <summary>95% do limite: valem as vedações do art. 22, parágrafo único.</summary>
    Prudencial,

    /// <summary>Acima do limite: corre o prazo do art. 23.</summary>
    Excedido,
}

/// <summary>
/// Limites fiscais do município, com o padrão da lei vigente e o artigo citado. Conferidos no texto compilado em
/// 16/11/2026 (docs/referencias/LEIS_CONFERIDAS.md).
/// </summary>
public sealed class FiscalLawConfig
{
    public static FiscalLawConfig Default { get; } = new();

    /// <summary>Os números e artigos deste objeto foram conferidos no texto oficial.</summary>
    public bool ConferidoNoTextoOficial { get; init; } = true;

    public string Conferencia { get; init; } = "Conferido no texto compilado do Planalto em 16/11/2026 (docs/referencias/LEIS_CONFERIDAS.md).";

    // ---- Despesa com pessoal (LC 101/2000)

    public string ArtigoLimitePessoal { get; init; } = "LC 101/2000, arts. 19, III, e 20, III";

    /// <summary>Despesa total com pessoal do município: fração da receita corrente líquida (art. 19, III).</summary>
    public double LimitePessoalMunicipio { get; init; } = 0.60;

    /// <summary>Parte do Executivo (art. 20, III, b).</summary>
    public double LimitePessoalExecutivo { get; init; } = 0.54;

    /// <summary>Parte do Legislativo, incluído o Tribunal de Contas do Município, quando houver (art. 20, III, a).</summary>
    public double LimitePessoalLegislativo { get; init; } = 0.06;

    public string ArtigoAlerta { get; init; } = "LC 101/2000, art. 59, § 1º, II";

    /// <summary>Fração do limite em que o Tribunal de Contas alerta.</summary>
    public double FracaoAlerta { get; init; } = 0.90;

    public string ArtigoPrudencial { get; init; } = "LC 101/2000, art. 22, parágrafo único";

    /// <summary>Fração do limite a partir da qual valem as vedações do art. 22.</summary>
    public double FracaoPrudencial { get; init; } = 0.95;

    /// <summary>O que fica vedado acima do limite prudencial (art. 22, parágrafo único, I a V).</summary>
    public IReadOnlyList<string> VedacoesPrudencial { get; init; } =
    [
        "Aumento, reajuste ou vantagem, salvo sentença judicial, determinação legal ou a revisão geral anual",
        "Criação de cargo, emprego ou função",
        "Alteração de carreira que aumente a despesa",
        "Admissão de pessoal, salvo reposição em educação, saúde e segurança",
        "Hora extra",
    ];

    public string ArtigoExcesso { get; init; } = "LC 101/2000, art. 23";

    /// <summary>Quadrimestres para eliminar o excesso (art. 23, caput).</summary>
    public int QuadrimestresParaEliminar { get; init; } = 2;

    /// <summary>Fração mínima do excesso a eliminar no primeiro quadrimestre (art. 23, caput).</summary>
    public double FracaoNoPrimeiroQuadrimestre { get; init; } = 1.0 / 3.0;

    /// <summary>O que o ente perde se não eliminar o excesso no prazo, enquanto ele durar (art. 23, § 3º).</summary>
    public IReadOnlyList<string> ConsequenciasExcesso { get; init; } =
    [
        "Não recebe transferências voluntárias",
        "Não obtém garantia de outro ente",
        "Não contrata operação de crédito",
    ];

    /// <summary>
    /// As restrições valem de imediato se o excesso ocorrer no primeiro quadrimestre do último ano do mandato
    /// (art. 23, § 4º).
    /// </summary>
    public bool ImediatoNoUltimoAnoDoMandato { get; init; } = true;

    public const int MesesPorQuadrimestre = 4;

    public double AlertaExecutivo => LimitePessoalExecutivo * FracaoAlerta;

    public double PrudencialExecutivo => LimitePessoalExecutivo * FracaoPrudencial;

    /// <summary>Faixa em que está a folha do Executivo, como fração da receita corrente líquida.</summary>
    public PayrollBand Faixa(double folhaSobreRcl) =>
        folhaSobreRcl > LimitePessoalExecutivo + 1e-9 ? PayrollBand.Excedido
        : folhaSobreRcl > PrudencialExecutivo + 1e-9 ? PayrollBand.Prudencial
        : folhaSobreRcl > AlertaExecutivo + 1e-9 ? PayrollBand.Alerta
        : PayrollBand.Normal;

    /// <summary>Reajuste, cargo novo e admissão estão vedados (art. 22, parágrafo único)?</summary>
    public bool VedadoAumentar(double folhaSobreRcl) => Faixa(folhaSobreRcl) >= PayrollBand.Prudencial;

    /// <summary>Data-limite para eliminar o excesso que começou em <paramref name="inicio"/>.</summary>
    public DateOnly PrazoParaEliminar(DateOnly inicio) => inicio.AddMonths(QuadrimestresParaEliminar * MesesPorQuadrimestre);

    /// <summary>Data em que ao menos um terço do excesso já tem de ter sido eliminado.</summary>
    public DateOnly PrazoDoPrimeiroTerco(DateOnly inicio) => inicio.AddMonths(MesesPorQuadrimestre);

    /// <summary>
    /// As consequências do art. 23, § 3º, estão valendo? Valem com o prazo vencido e o excesso ainda de pé, ou de
    /// imediato quando o excesso surge no primeiro quadrimestre do último ano do mandato (§ 4º).
    /// </summary>
    public bool RestricoesAtivas(double folhaSobreRcl, DateOnly? inicioDoExcesso, DateOnly hoje, int ultimoAnoDoMandato)
    {
        if (inicioDoExcesso is not { } inicio || Faixa(folhaSobreRcl) != PayrollBand.Excedido)
        {
            return false;
        }

        bool imediato = ImediatoNoUltimoAnoDoMandato && inicio.Year == ultimoAnoDoMandato && inicio.Month <= MesesPorQuadrimestre;
        return imediato || hoje > PrazoParaEliminar(inicio);
    }

    // ---- Mínimos de saúde e de educação

    public string ArtigoSaude { get; init; } = "CF, art. 198, § 2º, III; LC 141/2012, art. 7º";

    public double MinimoSaude { get; init; } = 0.15;

    public string ArtigoEducacao { get; init; } = "CF, art. 212";

    public double MinimoEducacao { get; init; } = 0.25;

    /// <summary>Sobre o que os mínimos incidem: não é o orçamento todo.</summary>
    public string BaseDosMinimos { get; init; } = "Receita de impostos próprios mais as transferências constitucionais de impostos";

    /// <summary>Área do orçamento do jogo a que cada mínimo se aplica.</summary>
    public string AreaSaude { get; init; } = "saude";

    public string AreaEducacao { get; init; } = "educacao";

    /// <summary>Percentual mínimo da área sobre a base de impostos e transferências; 0 para as demais áreas.</summary>
    public double MinimoDaArea(string areaId) => areaId == AreaSaude ? MinimoSaude : areaId == AreaEducacao ? MinimoEducacao : 0;

    /// <summary>
    /// Parcela mínima do orçamento todo que a área precisa ter, dada a fatia do orçamento que é base dos mínimos
    /// (impostos e transferências de impostos ÷ receita).
    /// </summary>
    public double MinimoSobreOrcamento(string areaId, double baseSobreReceita) => MinimoDaArea(areaId) * Math.Clamp(baseSobreReceita, 0, 1);

    /// <summary>Valor mínimo em reais, dada a base de impostos e transferências do ano.</summary>
    public decimal MinimoEmReais(string areaId, decimal baseDeImpostos) => baseDeImpostos * (decimal)MinimoDaArea(areaId);

    // ---- Teto de gasto da Câmara Municipal

    public string ArtigoCamara { get; init; } = "CF, art. 29-A";

    /// <summary>
    /// Teto da despesa da Câmara sobre a receita tributária e as transferências do ano anterior, por faixa de
    /// população (art. 29-A, I a VI): população máxima da faixa → fração.
    /// </summary>
    public IReadOnlyList<(long PopulacaoAte, double Teto)> TetoCamaraPorPopulacao { get; init; } =
    [
        (100_000, 0.07),
        (300_000, 0.06),
        (500_000, 0.05),
        (3_000_000, 0.045),
        (8_000_000, 0.04),
        (long.MaxValue, 0.035),
    ];

    /// <summary>A Câmara não gasta mais do que esta fração da sua receita com folha (art. 29-A, § 1º).</summary>
    public double FolhaMaximaDaCamara { get; init; } = 0.70;

    public double TetoCamara(long populacao) => TetoCamaraPorPopulacao.First(f => populacao <= f.PopulacaoAte).Teto;

    public decimal TetoCamaraEmReais(long populacao, decimal baseDeImpostosDoAnoAnterior) => baseDeImpostosDoAnoAnterior * (decimal)TetoCamara(populacao);

    public decimal FolhaMaximaDaCamaraEmReais(decimal receitaDaCamara) => receitaDaCamara * (decimal)FolhaMaximaDaCamara;
}

/// <summary>Modalidade de contratação de uma obra ou serviço.</summary>
public enum ProcurementMode
{
    Pregao,
    Concorrencia,

    /// <summary>Dispensa por valor (Lei 14.133/2021, art. 75, I e II).</summary>
    DispensaPorValor,

    /// <summary>Dispensa por emergência ou calamidade (art. 75, VIII).</summary>
    Emergencial,
}

/// <summary>
/// Regras de licitação (Lei 14.133/2021), com o padrão da lei vigente e o artigo citado. Conferidas no texto
/// compilado em 16/11/2026; os valores da dispensa são os do Decreto 12.807/2025, vigentes em 2026.
/// </summary>
public sealed class ProcurementLawConfig
{
    public static ProcurementLawConfig Default { get; } = new();

    public bool ConferidoNoTextoOficial { get; init; } = true;

    public string Conferencia { get; init; } = "Conferido no texto compilado do Planalto em 16/11/2026 (docs/referencias/LEIS_CONFERIDAS.md).";

    public string ArtigoModalidades { get; init; } = "Lei 14.133/2021, art. 28";

    /// <summary>Modalidades de licitação que existem na lei (art. 28). Tomada de preços e convite não existem mais.</summary>
    public IReadOnlyList<string> Modalidades { get; init; } = ["pregão", "concorrência", "concurso", "leilão", "diálogo competitivo"];

    public string ArtigoDispensaValor { get; init; } = "Lei 14.133/2021, art. 75, I e II; Decreto 12.807/2025";

    /// <summary>Teto da dispensa para obras e serviços de engenharia (art. 75, I), valor vigente em 2026.</summary>
    public decimal DispensaObras { get; init; } = 130_984.20m;

    /// <summary>Teto da dispensa para outros serviços e compras (art. 75, II), valor vigente em 2026.</summary>
    public decimal DispensaOutros { get; init; } = 65_492.11m;

    /// <summary>Os tetos são atualizados todo 1º de janeiro pelo IPCA-E; o objeto guarda o ano dos valores.</summary>
    public int AnoDosValores { get; init; } = 2026;

    public string ArtigoEmergencia { get; init; } = "Lei 14.133/2021, art. 75, VIII";

    /// <summary>A contratação emergencial só cobre parcelas concluídas neste prazo (art. 75, VIII).</summary>
    public int EmergenciaPrazoMaximoMeses { get; init; } = 12;

    /// <summary>Vedadas a prorrogação do contrato e a recontratação da mesma empresa (art. 75, VIII).</summary>
    public bool EmergenciaVedaProrrogacao { get; init; } = true;

    public bool EmergenciaVedaRecontratacao { get; init; } = true;

    public decimal TetoDispensa(bool engenharia) => engenharia ? DispensaObras : DispensaOutros;

    /// <summary>O valor cabe na dispensa por valor?</summary>
    public bool CabeDispensaPorValor(decimal valor, bool engenharia) => valor <= TetoDispensa(engenharia);

    /// <summary>
    /// A modalidade pode ser usada? Dispensa por valor só até o teto; emergencial só com emergência ou calamidade
    /// reconhecida e prazo de conclusão dentro do máximo.
    /// </summary>
    public bool Permitida(ProcurementMode modo, decimal valor, bool engenharia, bool emergenciaReconhecida, int semanasDeExecucao) => modo switch
    {
        ProcurementMode.DispensaPorValor => CabeDispensaPorValor(valor, engenharia),
        ProcurementMode.Emergencial => emergenciaReconhecida && semanasDeExecucao * 7 <= EmergenciaPrazoMaximoMeses * 30.5,
        _ => true,
    };

    public string ArtigoDe(ProcurementMode modo) => modo switch
    {
        ProcurementMode.DispensaPorValor => ArtigoDispensaValor,
        ProcurementMode.Emergencial => ArtigoEmergencia,
        _ => ArtigoModalidades,
    };
}

/// <summary>
/// Conduta vedada na inauguração de obra pública (Lei 9.504/1997, art. 77), com o padrão da lei vigente. Conferida no
/// texto compilado em 16/11/2026.
/// </summary>
public sealed class InaugurationLawConfig
{
    public static InaugurationLawConfig Default { get; } = new();

    public bool ConferidoNoTextoOficial { get; init; } = true;

    public string Conferencia { get; init; } = "Conferido no texto compilado do Planalto em 16/11/2026 (docs/referencias/LEIS_CONFERIDAS.md).";

    public string Artigo { get; init; } = "Lei 9.504/1997, art. 77";

    /// <summary>Meses antes do pleito em que o candidato não comparece a inauguração de obra pública.</summary>
    public int MesesAntesDoPleito { get; init; } = 3;

    public string Pena { get; init; } = "Cassação do registro ou do diploma";

    /// <summary>Primeiro dia do período vedado para a eleição que cai em <paramref name="pleito"/>.</summary>
    public DateOnly InicioDoPeriodo(DateOnly pleito) => pleito.AddMonths(-MesesAntesDoPleito);

    /// <summary>A data cai nos meses anteriores ao pleito em que o comparecimento é vedado (até o dia do pleito)?</summary>
    public bool NoPeriodoVedado(DateOnly data, DateOnly pleito) => data >= InicioDoPeriodo(pleito) && data <= pleito;

    /// <summary>Período vedado da eleição do ano da data, se houver eleição nesse ano.</summary>
    public bool NoPeriodoVedado(DateOnly data)
    {
        if (Calendar.ElectionKindOf(data.Year) is null)
        {
            return false;
        }

        return NoPeriodoVedado(data, Calendar.ElectionFor(data.Year).FirstRound);
    }
}

/// <summary>
/// Condutas vedadas ao agente público em ano de eleição (Lei 9.504/1997, art. 73, V a VIII) e a regra da reeleição
/// (CF, art. 14, § 5º), com o padrão da lei vigente. Conferidas no texto compilado em 16/11/2026 (segunda leitura de
/// docs/referencias/LEIS_CONFERIDAS.md). As exceções das alíneas e as penas (§§ 4º e 5º) não foram lidas: o objeto
/// guarda só o que foi conferido.
/// </summary>
public sealed class ElectionConductLawConfig
{
    public static ElectionConductLawConfig Default { get; } = new();

    public bool ConferidoNoTextoOficial { get; init; } = true;

    public string Conferencia { get; init; } = "Conferido no texto compilado do Planalto em 16/11/2026 (docs/referencias/LEIS_CONFERIDAS.md, segunda leitura). Exceções das alíneas e penas (§§ 4º e 5º) não lidas.";

    public string Artigo { get; init; } = "Lei 9.504/1997, art. 73";

    /// <summary>Meses antes do pleito em que valem as vedações dos incisos V e VI.</summary>
    public int MesesAntesDoPleito { get; init; } = 3;

    public string ArtigoPessoal { get; init; } = "Lei 9.504/1997, art. 73, V";

    /// <summary>Inciso V: nos três meses antes do pleito e até a posse.</summary>
    public string VedacaoPessoal { get; init; } = "Nomear, contratar, admitir, demitir sem justa causa e mexer em vantagens, com as exceções das alíneas";

    /// <summary>A vedação do inciso V vai até a posse dos eleitos.</summary>
    public bool PessoalAteAPosse { get; init; } = true;

    public string ArtigoTransferencias { get; init; } = "Lei 9.504/1997, art. 73, VI";

    /// <summary>Inciso VI: nos três meses antes do pleito.</summary>
    public IReadOnlyList<string> VedacoesNosTresMeses { get; init; } =
    [
        "Transferências voluntárias",
        "Publicidade institucional",
        "Pronunciamento em cadeia de rádio e televisão",
    ];

    public string ArtigoPublicidade { get; init; } = "Lei 9.504/1997, art. 73, VII";

    /// <summary>
    /// Inciso VII: no primeiro semestre do ano de eleição, não se empenha despesa de publicidade acima deste número de
    /// vezes a média mensal dos três últimos anos.
    /// </summary>
    public int PublicidadeVezesAMediaMensal { get; init; } = 6;

    public int PublicidadeAnosDaMedia { get; init; } = 3;

    public string ArtigoRevisao { get; init; } = "Lei 9.504/1997, art. 73, VIII";

    /// <summary>Inciso VIII: revisão geral de remuneração acima da recomposição da inflação.</summary>
    public string VedacaoRevisao { get; init; } = "Revisão geral da remuneração acima da recomposição da perda da inflação";

    public string ArtigoReeleicao { get; init; } = "CF, art. 14, § 5º";

    /// <summary>Prefeito, e quem o sucedeu ou substituiu no mandato, pode ser reeleito para um único período subsequente.</summary>
    public int ReeleicoesSeguidas { get; init; } = 1;

    public DateOnly InicioDoPeriodo(DateOnly pleito) => pleito.AddMonths(-MesesAntesDoPleito);

    /// <summary>A data cai nos três meses antes do pleito (inciso VI: transferências voluntárias, publicidade)?</summary>
    public bool NosTresMeses(DateOnly data, DateOnly pleito) => data >= InicioDoPeriodo(pleito) && data <= pleito;

    /// <summary>O mesmo, para a eleição municipal ou geral do ano da data, se houver.</summary>
    public bool NosTresMeses(DateOnly data) =>
        Calendar.ElectionKindOf(data.Year) is not null && NosTresMeses(data, Calendar.ElectionFor(data.Year).FirstRound);

    /// <summary>A data cai no período do inciso V (dos três meses antes do pleito até a posse, em 1º de janeiro)?</summary>
    public bool PessoalVedado(DateOnly data, DateOnly pleito) =>
        data >= InicioDoPeriodo(pleito) && data < (PessoalAteAPosse ? new DateOnly(pleito.Year + 1, 1, 1) : pleito.AddDays(1));

    /// <summary>Teto do empenho de publicidade no primeiro semestre do ano de eleição, dada a média mensal dos três últimos anos.</summary>
    public decimal TetoPublicidadeNoPrimeiroSemestre(decimal mediaMensalDosTresAnos) => mediaMensalDosTresAnos * PublicidadeVezesAMediaMensal;

    /// <summary>Quem está no mandato de número <paramref name="mandatoSeguido"/> (1 = primeiro) ainda pode disputar o seguinte?</summary>
    public bool PodeDisputarReeleicao(int mandatoSeguido) => mandatoSeguido <= ReeleicoesSeguidas;
}

/// <summary>
/// Regras da Câmara usadas na Mesa, na CPI e na cassação. ATENÇÃO: estes dispositivos NÃO estão em
/// docs/referencias/LEIS_CONFERIDAS.md; foram citados de memória e <see cref="ConferidoNoTextoOficial"/> fica falso
/// até a conferência no texto oficial.
/// </summary>
public sealed class ChamberRulesConfig
{
    public static ChamberRulesConfig Default { get; } = new();

    public bool ConferidoNoTextoOficial { get; init; }

    public string Conferencia { get; init; } = "Artigos citados de memória; a conferir no texto oficial antes de valer como regra de lei.";

    public string ArtigoCpi { get; init; } = "CF, art. 58, § 3º (aplicado às Câmaras por simetria)";

    /// <summary>Fração dos membros que precisa assinar o requerimento de CPI.</summary>
    public double AssinaturasCpi { get; init; } = 1.0 / 3.0;

    public string ArtigoCassacao { get; init; } = "Decreto-Lei 201/1967, art. 5º, VI";

    /// <summary>Fração dos membros da Câmara para cassar o mandato do prefeito.</summary>
    public double QuorumCassacao { get; init; } = 2.0 / 3.0;

    public string ArtigoMesa { get; init; } = "CF, art. 57, § 4º (mandato de dois anos, por simetria; a lei orgânica de cada município regula)";

    /// <summary>Duração do mandato da Mesa Diretora, em anos.</summary>
    public int MandatoDaMesaAnos { get; init; } = 2;

    public int AssinaturasNecessarias(int membros) => (int)Math.Ceiling(membros * AssinaturasCpi - 1e-9);

    public int VotosParaCassar(int membros) => (int)Math.Ceiling(membros * QuorumCassacao - 1e-9);
}
