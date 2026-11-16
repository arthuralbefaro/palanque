using Planalto.Core.Data;
using Planalto.Core.Parties;

namespace Planalto.Core.Campaign;

/// <summary>
/// Substituição marcada da tabela de limites de gastos do TSE (que não pôde ser baixada): um valor por eleitor, com
/// piso e teto, por porte. É parâmetro de jogo, não número oficial; a regra da lei está em
/// <see cref="Elections.CampaignFinanceLawConfig"/>.
/// </summary>
public sealed class SpendingLimitRule
{
    public required decimal PorEleitor { get; init; }

    public required decimal Piso { get; init; }

    public required decimal Teto { get; init; }
}

/// <summary>O que um doador pede em troca (vira dívida no Livro e cobrança no mandato).</summary>
public sealed class DonorRequestDefinition
{
    public required string Id { get; init; }

    /// <summary>Texto do pedido; aceita {zona} e {tema}.</summary>
    public required string Texto { get; init; }

    public double Weight { get; init; } = 1;
}

public sealed class FinanceRules
{
    /// <summary>Doadores pessoa física nomeados por porte (GDD 17.12: de 4 a 12).</summary>
    public required Dictionary<string, IntRange> DoadoresPorPorte { get; init; }

    /// <summary>Rendimentos brutos anuais de um doador, no porte médio (a capacidade dele).</summary>
    public required DoubleRange RendaAnual { get; init; }

    /// <summary>Chance semanal de um doador fazer uma oferta, antes da relação e da viabilidade.</summary>
    public required double ChanceOfertaSemanal { get; init; }

    public required double ChanceOfertaPorRelacao { get; init; }

    /// <summary>Fração do teto legal ainda livre que o doador oferece de cada vez.</summary>
    public required DoubleRange OfertaFracaoDoTeto { get; init; }

    /// <summary>Chance de a oferta oficial passar do teto legal do doador, e por quanto.</summary>
    public required double ChanceAcimaDoTeto { get; init; }

    public required double AcimaDoTetoFator { get; init; }

    /// <summary>Chance de a oferta ser "por fora", e o tamanho dela como fração do limite de gastos.</summary>
    public required double ChancePorFora { get; init; }

    public required DoubleRange PorForaFracaoDoLimite { get; init; }

    /// <summary>Quanto o doador aceita dar com recibo quando o jogador recusa o "por fora" (fração da oferta).</summary>
    public required double OficialNoLugarDoPorFora { get; init; }

    public required int OfertaPrazoDias { get; init; }

    public required int MaxOfertasAbertas { get; init; }

    /// <summary>Cota do fundo eleitoral no porte médio, quando a direção dá tudo; e os pesos da decisão do dirigente.</summary>
    public required decimal FundoBase { get; init; }

    public required double FundoPiso { get; init; }

    public required double FundoPesoRelacao { get; init; }

    public required double FundoPesoViabilidade { get; init; }

    public required double FundoPesoLinha { get; init; }

    /// <summary>Financiamento coletivo por semana, no porte médio, com reconhecimento de 100%.</summary>
    public required decimal ColetivoSemanal { get; init; }

    /// <summary>Diplomação dos eleitos ("MM-dd"): parâmetro de jogo, não consta da conferência dos artigos.</summary>
    public required string Diplomacao { get; init; }

    /// <summary>Pontos de risco por tipo de irregularidade: por 1% do limite de gastos, quando ela tem valor.</summary>
    public required Dictionary<string, double> RiscoPorPercentual { get; init; }

    /// <summary>Pontos de risco por ponto de gravidade, quando a irregularidade não tem valor.</summary>
    public required Dictionary<string, double> RiscoPorGravidade { get; init; }

    /// <summary>Risco a partir do qual as contas saem com ressalvas, e desaprovadas.</summary>
    public required int RiscoRessalvas { get; init; }

    public required int RiscoDesaprovacao { get; init; }

    public required IntRange JulgamentoSemanas { get; init; }

    /// <summary>Chance semanal de um esqueleto vir à tona = base × gravidade × pessoas hostis que sabem × exposição.</summary>
    public required double EsqueletoBase { get; init; }

    /// <summary>Relação (−100 a 100) e lealdade (0 a 100) abaixo das quais quem sabe pode falar.</summary>
    public required int RelacaoHostil { get; init; }

    public required int LealdadeHostil { get; init; }

    /// <summary>Exposição pelo cargo disputado e pela fatia na pesquisa (1 + fator × fatia de votos).</summary>
    public required Dictionary<string, double> ExposicaoPorCargo { get; init; }

    public required double ExposicaoPorFatia { get; init; }

    public required double EsqueletoImagem { get; init; }

    /// <summary>Chance de a prestação parcial chamar a atenção de um jornalista para uma irregularidade, por ponto de risco.</summary>
    public required double ParcialChancePorRisco { get; init; }

    /// <summary>Irregularidade grave: dinheiro "por fora" a partir desta fração do limite de gastos.</summary>
    public required double GraveFracaoDoLimite { get; init; }

    /// <summary>Chance de denúncia: base pela imprensa (× fração "por fora" do limite, até 1) e por pessoa hostil que sabe.</summary>
    public required double DenunciaImprensa { get; init; }

    public required double DenunciaPorHostil { get; init; }

    public required double DenunciaSeExposto { get; init; }

    /// <summary>Chance de cassação na representação: base + por fração "por fora" do limite, até o máximo.</summary>
    public required double CassacaoBase { get; init; }

    public required double CassacaoPorFracao { get; init; }

    public required double CassacaoMaxima { get; init; }

    public required IntRange RepresentacaoSemanas { get; init; }

    /// <summary>Gravidade do esqueleto de dinheiro "por fora": 1 + fração do limite ÷ este passo, até 3.</summary>
    public required double GravidadePasso { get; init; }
}

/// <summary>data/campaign/finance.json (GDD 17.12).</summary>
public sealed class FinanceCatalog : IValidatable
{
    public const string File = "campaign/finance.json";

    public const string Fundo = "fundo";
    public const string PessoaFisica = "pessoa_fisica";
    public const string Proprios = "proprios";
    public const string Coletivo = "coletivo";
    public const string PorFora = "por_fora";
    public const string Jantar = "jantar";
    public const string Evento = "evento";

    public const string CaixaDois = "caixa_dois";
    public const string GastoNaoDeclarado = "gasto_nao_declarado";
    public const string ErroContabil = "erro_contabil";
    public const string DoacaoAcimaDoLimite = "doacao_acima_limite";
    public const string RecursosPropriosAcima = "recursos_proprios_acima";
    public const string GastoAcimaDoLimite = "gasto_acima_limite";

    /// <summary>Tipos de esqueleto que são irregularidade de contas.</summary>
    public static readonly IReadOnlyList<string> Irregularidades = [CaixaDois, GastoNaoDeclarado, ErroContabil, DoacaoAcimaDoLimite, RecursosPropriosAcima, GastoAcimaDoLimite];

    /// <summary>Tipos que são captação ou gasto ilícito para a representação do art. 30-A.</summary>
    public static readonly IReadOnlyList<string> PorForaTipos = [CaixaDois, GastoNaoDeclarado];

    public required string Note { get; init; }

    /// <summary>Registro da lacuna: a tabela do TSE não foi importada e o limite abaixo é parâmetro de jogo.</summary>
    public required string Lacuna { get; init; }

    /// <summary>Limite de gastos de vereador por porte (substituição marcada).</summary>
    public required Dictionary<string, SpendingLimitRule> LimiteVereador { get; init; }

    /// <summary>O limite de prefeito é este múltiplo do de vereador (parâmetro de jogo).</summary>
    public required decimal LimitePrefeitoFator { get; init; }

    public required FinanceRules Regras { get; init; }

    public required List<DonorRequestDefinition> Pedidos { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public required Dictionary<string, MemoryDefinition> Lembrancas { get; init; }

    /// <summary>Ganho e custo de cada resposta a uma oferta de doação (princípio e).</summary>
    public required Dictionary<string, DecisionTrade> Respostas { get; init; }

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public MemoryDefinition Memory(string key) => Lembrancas[key];

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "oferta_oficial", "oferta_por_fora", "explica_oficial", "explica_teto", "explica_acima", "explica_por_fora", "explica_quem_sabe", "explica_divida", "explica_recusa",
        "divida_livro", "esqueleto_por_fora", "esqueleto_acima", "esqueleto_proprios", "esqueleto_limite", "fundo", "fundo_divida", "fundo_explica", "coletivo_liberado",
        "coletivo_semana", "parcial", "parcial_jornalista", "veio_a_tona", "aprovadas", "ressalvas", "desaprovadas", "representacao", "cassado", "absolvido", "prestacao_final",
        "irregularidade_caixa_dois", "irregularidade_gasto_nao_declarado", "irregularidade_erro_contabil", "irregularidade_doacao_acima_limite",
        "irregularidade_recursos_proprios_acima", "irregularidade_gasto_acima_limite",
    ];

    public static readonly IReadOnlyList<string> MemoryKeys = ["doacao_aceita", "doacao_por_fora", "doacao_recusada", "oferta_ignorada", "fundo"];

    public static readonly IReadOnlyList<string> AnswerKeys = ["oficial", "oficial_integral", "por_fora", "recusar"];

    public IEnumerable<string> Validate()
    {
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            if (!LimiteVereador.TryGetValue(porte, out SpendingLimitRule? rule) || rule.PorEleitor <= 0 || rule.Piso <= 0 || rule.Teto < rule.Piso)
            {
                yield return $"limite_vereador.{porte}: valor por eleitor e piso positivos, teto maior ou igual ao piso.";
            }

            if (!Regras.DoadoresPorPorte.TryGetValue(porte, out IntRange? donors) || donors.Min < 4 || donors.Max > 12 || donors.Max < donors.Min)
            {
                yield return $"regras.doadores_por_porte.{porte}: de 4 a 12 (GDD 17.12).";
            }
        }

        if (string.IsNullOrWhiteSpace(Lacuna) || LimitePrefeitoFator < 1)
        {
            yield return "lacuna: registre que a tabela do TSE não foi importada; limite_prefeito_fator >= 1.";
        }

        if (Pedidos.Count == 0 || Pedidos.Any(p => p.Weight <= 0 || string.IsNullOrWhiteSpace(p.Texto)))
        {
            yield return "pedidos: ao menos um, com texto e peso positivo.";
        }

        foreach (string error in DataChecks.UniqueIds(Pedidos.Select(p => p.Id), "Pedido de doador"))
        {
            yield return error;
        }

        FinanceRules r = Regras;
        if (r.RendaAnual.Min <= 0 || r.RendaAnual.Max < r.RendaAnual.Min || r.ChanceOfertaSemanal is < 0 or > 1 || r.OfertaFracaoDoTeto.Min <= 0 || r.OfertaFracaoDoTeto.Max > 1
            || r.ChancePorFora is < 0 or > 1 || r.PorForaFracaoDoLimite.Min <= 0 || r.PorForaFracaoDoLimite.Max < r.PorForaFracaoDoLimite.Min || r.OfertaPrazoDias < 1
            || r.FundoBase < 0 || r.ColetivoSemanal < 0 || r.RiscoRessalvas < 1 || r.RiscoDesaprovacao <= r.RiscoRessalvas || r.RiscoDesaprovacao > 100
            || r.EsqueletoBase is < 0 or > 1 || r.GraveFracaoDoLimite <= 0 || r.CassacaoMaxima is < 0 or > 1 || r.CassacaoBase < 0 || r.GravidadePasso <= 0
            || r.JulgamentoSemanas.Min < 1 || r.RepresentacaoSemanas.Min < 1 || r.RepresentacaoSemanas.Max > 100 || r.OficialNoLugarDoPorFora is < 0 or > 1
            || !DateOnly.TryParseExact($"2028-{r.Diplomacao}", "yyyy-MM-dd", out _))
        {
            yield return "regras: valores fora do intervalo (a representação se resolve em até 2 anos).";
        }

        foreach (string type in Irregularidades.Where(t => !r.RiscoPorPercentual.ContainsKey(t) && !r.RiscoPorGravidade.ContainsKey(t)))
        {
            yield return $"regras: falta o peso de risco de '{type}'.";
        }

        foreach (string office in new[] { "vereador", "prefeito" }.Where(o => !r.ExposicaoPorCargo.ContainsKey(o)))
        {
            yield return $"regras.exposicao_por_cargo: falta '{office}'.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }

        foreach (string key in MemoryKeys.Where(k => !Lembrancas.ContainsKey(k)))
        {
            yield return $"lembrancas: falta '{key}'.";
        }

        foreach (string key in AnswerKeys)
        {
            if (!Respostas.TryGetValue(key, out DecisionTrade? trade))
            {
                yield return $"respostas: falta '{key}'.";
                continue;
            }

            foreach (string error in DecisionNature.Check(trade.Ganho, trade.Custo, $"doação, resposta '{key}'"))
            {
                yield return error;
            }
        }
    }
}

/// <summary>Doador pessoa física (GDD 17.12). Os dados de pessoa ficam em <see cref="People.Person"/>, com o mesmo id.</summary>
public sealed class Donor
{
    public required string PersonId { get; init; }

    /// <summary>Rendimentos brutos do ano anterior: a doação com recibo vai até 10% disso (art. 23, § 1º).</summary>
    public required decimal Income { get; init; }

    public required string RequestId { get; init; }

    /// <summary>O pedido por extenso, já com a zona ou o tema.</summary>
    public required string RequestText { get; init; }

    public string? ZoneId { get; init; }

    public string? Theme { get; init; }

    /// <summary>Quanto já doou com recibo nesta eleição.</summary>
    public decimal Donated { get; set; }

    public decimal OffBooks { get; set; }

    public string? DebtId { get; set; }
}

public sealed class DonorOffer
{
    public required string Id { get; init; }

    public required string DonorId { get; init; }

    public required decimal Amount { get; init; }

    /// <summary>O doador propõe entregar sem recibo.</summary>
    public required bool PorFora { get; init; }

    public required DateOnly Deadline { get; init; }
}

public enum DonationAnswer
{
    /// <summary>Com recibo, até o teto legal do doador.</summary>
    Oficial,

    /// <summary>Com recibo, o valor inteiro, mesmo acima do teto legal (vira irregularidade).</summary>
    OficialIntegral,

    PorFora,
    Recusar,
}

public sealed record FinanceEntry(DateOnly Date, string Source, decimal Amount, string? DonorId, bool Declared);

/// <summary>Dinheiro da campanha (GDD 17.12): o que entrou, de quem, o que saiu e as ofertas em aberto.</summary>
public sealed class FinanceState
{
    public const int MaxEntries = 300;

    /// <summary>Começo da contabilidade desta eleição: irregularidades anteriores não entram nas contas.</summary>
    public DateOnly? Since { get; set; }

    public List<FinanceEntry> Receipts { get; init; } = [];

    /// <summary>Total que entrou por fonte.</summary>
    public Dictionary<string, decimal> BySource { get; init; } = [];

    /// <summary>Gasto total de campanha (declarado ou não).</summary>
    public decimal Spent { get; set; }

    public List<DonorOffer> Offers { get; init; } = [];

    public int NextId { get; set; } = 1;

    /// <summary>Financiamento coletivo arrecadado antes do registro, ainda retido.</summary>
    public decimal CrowdHeld { get; set; }

    public bool CrowdReleased { get; set; }

    public bool FundPaid { get; set; }

    public bool PartialDone { get; set; }

    /// <summary>Quem denunciou ou falou das contas (ids de pessoas), para a representação.</summary>
    public List<string> Denuncias { get; init; } = [];

    public decimal Declared => BySource.Where(s => s.Key != FinanceCatalog.PorFora).Sum(s => s.Value);

    public decimal OffBooks => BySource.GetValueOrDefault(FinanceCatalog.PorFora);
}

/// <summary>Irregularidade das contas, como a tela mostra: o que é, quanto, e quem sabe.</summary>
public sealed record IrregularityView(string Type, string Text, decimal Amount, int Severity, double Risk, IReadOnlyList<string> KnownBy, IReadOnlyList<string> KnownByNames, bool Exposed, string? SkeletonId);

/// <summary>Painel Contas (GDD 17.12): arrecadado, gasto, barra do limite, irregularidades e o risco de contas.</summary>
public sealed record AccountsView(
    decimal Raised,
    IReadOnlyDictionary<string, decimal> BySource,
    decimal Spent,
    decimal Limit,
    double LimitUsed,
    decimal OwnResources,
    decimal OwnResourcesLimit,
    decimal OffBooks,
    double OffBooksShare,
    IReadOnlyList<IrregularityView> Irregularities,
    int Risk,
    DateOnly PartialReport,
    DateOnly FinalReport,
    bool LimitIsGameParameter);

public sealed record DonorView(Donor Donor, string Name, double Relation, decimal LegalCap, decimal CapLeft, string RequestText, DonorOffer? Offer);

public sealed record DonationResult(decimal Money, int RiskBefore, int RiskAfter, People.Debt? Debt, People.LedgerSkeleton? Skeleton, People.Memory Memory, string Text);

public enum AccountsStage
{
    AguardandoPrestacao,
    AguardandoJulgamento,
    Julgadas,
    Representacao,
    Encerrado,
}

public enum AccountsVerdict
{
    Aprovadas,
    AprovadasComRessalvas,
    Desaprovadas,

    /// <summary>Quarto desfecho do art. 30 da Lei 9.504: o jogo sempre presta as contas, então hoje não acontece.</summary>
    NaoPrestadas,
}

/// <summary>
/// As contas de uma eleição depois da apuração (GDD 17.12): prestação final, julgamento e, havendo irregularidade
/// grave e denúncia, a representação do art. 30-A, que pode cassar o diploma depois da posse. Fica no estado do jogo
/// porque a campanha já acabou quando isso se resolve.
/// </summary>
public sealed class AccountsCase
{
    public required int Municipio { get; init; }

    public required DateOnly ElectionDate { get; init; }

    public required bool Elected { get; init; }

    public required decimal Limit { get; init; }

    public required decimal Spent { get; init; }

    public required decimal OffBooks { get; init; }

    public required int Risk { get; init; }

    /// <summary>Pessoas hostis que sabem de dinheiro "por fora".</summary>
    public required int HostileKnowers { get; init; }

    public required bool Exposed { get; init; }

    public required int Denuncias { get; init; }

    public AccountsStage Stage { get; set; } = AccountsStage.AguardandoPrestacao;

    public AccountsVerdict? Verdict { get; set; }

    public DateOnly? JudgmentOn { get; set; }

    public bool RepresentationChecked { get; set; }

    public bool RepresentationOpen { get; set; }

    public DateOnly? RulingOn { get; set; }

    /// <summary>O diploma foi cassado: o mandato acabou.</summary>
    public bool MandateLost { get; set; }

    public DateOnly? MandateLostOn { get; set; }

    public List<Headline> Log { get; init; } = [];

    public double OffBooksShare => Limit <= 0 ? 0 : (double)(OffBooks / Limit);
}
