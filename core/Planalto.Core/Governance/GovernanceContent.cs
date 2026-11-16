using Planalto.Core.Campaign;
using Planalto.Core.Data;

namespace Planalto.Core.Governance;

/// <summary>Faixa de valores reais (mínimo e máximo) lida de data/.</summary>
public sealed class Range
{
    public required double Min { get; init; }

    public required double Max { get; init; }

    public bool Valid => Max >= Min;
}

// ============================================================ obras (data/works/types.json)

/// <summary>Tipo de obra ou de serviço municipal (GDD 17.14 e 17.15).</summary>
public sealed class WorkType
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>Chaves de promessa que a entrega cumpre (o campo "obra" de data/campaign/promises.json).</summary>
    public required List<string> Cumpre { get; init; }

    /// <summary>Secretaria responsável (id de data/executive/secretaries.json).</summary>
    public required string Secretaria { get; init; }

    /// <summary>"urbana", "rural" ou "qualquer".</summary>
    public required string Situacao { get; init; }

    /// <summary>Obra ou serviço de engenharia (decide o teto da dispensa por valor).</summary>
    public required bool Engenharia { get; init; }

    /// <summary>Serviço: não tem licitação de obra nem inauguração; começa a valer quando fica pronto.</summary>
    public required bool Servico { get; init; }

    /// <summary>Pequena o bastante para uma indicação de vereador.</summary>
    public required bool Pequena { get; init; }

    /// <summary>Pode ser objeto de emenda de vereador.</summary>
    public required bool Emenda { get; init; }

    /// <summary>Custo em reais numa cidade pequena; os demais portes multiplicam (regras.custo_por_porte).</summary>
    public required decimal Custo { get; init; }

    public required IntRange ProjetoSemanas { get; init; }

    public required IntRange ExecucaoSemanas { get; init; }

    /// <summary>Presença (vereador) ou aprovação na zona (prefeito) que a entrega rende a quem a patrocinou.</summary>
    public required double Prestigio { get; init; }

    /// <summary>Quanto a operação acrescenta à folha, em fração da receita corrente líquida.</summary>
    public required double Folha { get; init; }

    /// <summary>Indicador do município que a entrega empurra (id de data/executive/config.json), ou null.</summary>
    public string? Indicador { get; init; }

    public double IndicadorPontos { get; init; }

    /// <summary>Reduz o dano de enchente na zona (drenagem).</summary>
    public bool ProtegeDeEnchente { get; init; }
}

public sealed class WorkRules
{
    public required Dictionary<string, double> CustoPorPorte { get; init; }

    /// <summary>Obras que a prefeitura NPC toca ao mesmo tempo, por porte.</summary>
    public required Dictionary<string, int> ObrasSimultaneasNpc { get; init; }

    /// <summary>Obras que a prefeitura do jogador consegue tocar ao mesmo tempo, por porte.</summary>
    public required Dictionary<string, int> CapacidadeDeExecucao { get; init; }

    public required double NpcChanceNovaObra { get; init; }

    /// <summary>Chance de a prefeitura NPC abrir, depois de uma enchente, a obra que protege a zona atingida.</summary>
    public required double NpcChanceObraAposCrise { get; init; }

    /// <summary>Chance semanal de atraso na execução, antes da empresa, do secretário e da chuva.</summary>
    public required double ChanceAtrasoSemanal { get; init; }

    public required IntRange AtrasoSemanas { get; init; }

    public required List<int> MesesDeChuva { get; init; }

    public required double ChuvaFatorAtraso { get; init; }

    /// <summary>Semanas que uma obra pronta espera a inauguração antes de ser entregue sem cerimônia.</summary>
    public required int SemanasParaEntregaSemCerimonia { get; init; }

    /// <summary>Fração do prestígio que rende a entrega sem a presença de quem patrocinou.</summary>
    public required double PrestigioSemCerimonia { get; init; }

    /// <summary>Fração do prestígio que transborda para cada zona vizinha.</summary>
    public required double Transbordo { get; init; }

    /// <summary>Fração da área "obras" do orçamento que pode virar obra nova no ano.</summary>
    public required double FracaoInvestimento { get; init; }

    /// <summary>Semanas que a obra espera o prefeito escolher a modalidade antes de seguir pela padrão.</summary>
    public required int SemanasParaModalidadePadrao { get; init; }

    /// <summary>Chance semanal de a prefeitura liberar uma emenda parada, antes da relação com o prefeito.</summary>
    public required double ChanceLiberacaoEmenda { get; init; }

    public required double LiberacaoPorRelacao { get; init; }

    /// <summary>Semanas depois das quais a emenda é liberada de qualquer jeito (execução obrigatória).</summary>
    public required int SemanasLiberacaoObrigatoria { get; init; }

    /// <summary>Aprovação do governo na zona por ponto de prestígio de obra entregue.</summary>
    public required double AprovacaoPorPrestigio { get; init; }

    /// <summary>Semanas de atraso a partir das quais a obra vira notícia.</summary>
    public required int AtrasoQueViraNoticia { get; init; }
}

public sealed class WorkCatalog : IValidatable
{
    public const string File = "works/types.json";

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "anunciada", "projeto_pronto", "licitacao_aberta", "impugnada", "contratada", "atraso", "pronta", "inaugurada", "inaugurada_sem_voce", "entregue_sem_cerimonia",
        "emenda_destinada", "emenda_liberada", "emenda_retida", "a_pedido", "periodo_vedado", "sem_obra", "emergencial_irregular", "empresa_doadora", "servico_em_vigor",
        "previsao_inauguracao", "previsao_entrega", "previsao_modalidade", "previsao_irregular", "previsao_dispensa_teto", "previsao_promessa", "previsao_folha",
        "fator_cansaco", "fator_secretario", "abre_sem_pessoal", "sem_investimento", "sem_capacidade",
    ];

    public required string Note { get; init; }

    public required WorkRules Regras { get; init; }

    public required List<WorkType> Tipos { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public WorkType Type(string id) => Tipos.First(t => t.Id == id);

    public WorkType? Find(string id) => Tipos.FirstOrDefault(t => t.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Tipos.Select(t => t.Id), "Tipo de obra"))
        {
            yield return error;
        }

        foreach (string porte in GovernanceContent.Portes)
        {
            if (!Regras.CustoPorPorte.TryGetValue(porte, out double factor) || factor <= 0 || !Regras.ObrasSimultaneasNpc.TryGetValue(porte, out int n) || n < 1
                || !Regras.CapacidadeDeExecucao.TryGetValue(porte, out int capacity) || capacity < 1)
            {
                yield return $"regras: custo_por_porte, obras_simultaneas_npc e capacidade_de_execucao positivos para '{porte}'.";
            }
        }

        WorkRules r = Regras;
        if (r.NpcChanceNovaObra is <= 0 or > 1 || r.NpcChanceObraAposCrise is < 0 or > 1 || r.ChanceAtrasoSemanal is < 0 or > 1 || r.AtrasoSemanas.Min < 1 || r.AtrasoSemanas.Max < r.AtrasoSemanas.Min
            || r.MesesDeChuva.Any(m => m is < 1 or > 12) || r.ChuvaFatorAtraso < 1 || r.SemanasParaEntregaSemCerimonia < 1 || r.PrestigioSemCerimonia is < 0 or > 1
            || r.Transbordo is < 0 or > 1 || r.FracaoInvestimento is <= 0 or > 1 || r.SemanasParaModalidadePadrao < 1 || r.ChanceLiberacaoEmenda is < 0 or > 1
            || r.SemanasLiberacaoObrigatoria < 1 || r.AprovacaoPorPrestigio < 0 || r.AtrasoQueViraNoticia < 1)
        {
            yield return "regras: valores fora do intervalo.";
        }

        foreach (WorkType t in Tipos)
        {
            if (t.Nome.Length is 0 or > 28 || t.Cumpre.Count == 0 || t.Custo <= 0 || t.Prestigio <= 0 || t.Folha < 0
                || t.ProjetoSemanas.Min < 1 || t.ProjetoSemanas.Max < t.ProjetoSemanas.Min || t.ExecucaoSemanas.Min < 1 || t.ExecucaoSemanas.Max < t.ExecucaoSemanas.Min
                || t.Situacao is not ("urbana" or "rural" or "qualquer"))
            {
                yield return $"tipo '{t.Id}': nome de até 28 caracteres, chave de promessa, custo, prestígio e prazos válidos.";
            }
        }

        if (!Tipos.Any(t => t.Pequena) || !Tipos.Any(t => t.Emenda) || !Tipos.Any(t => t.ProtegeDeEnchente))
        {
            yield return "é preciso ao menos um tipo pequeno (indicação), um de emenda e um que proteja de enchente.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

// ============================================================ licitação (data/executive/procurement.json)

public sealed class ProcurementOption
{
    public required ProcurementMode Modo { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    public required IntRange Semanas { get; init; }

    /// <summary>Chance de um perdedor impugnar o resultado.</summary>
    public required double ChanceImpugnacao { get; init; }

    public required IntRange ImpugnacaoSemanas { get; init; }

    /// <summary>Regra de jogo: a modalidade só serve para serviços e obras pequenas.</summary>
    public bool SoPequenasEServicos { get; init; }

    /// <summary>Chance de a contratação direta sair com sobrepreço (antes da integridade do secretário).</summary>
    public double ChanceSobrepreco { get; init; }

    public required List<string> Ganho { get; init; }

    public required List<string> Custo { get; init; }
}

public sealed class ProcurementRules
{
    /// <summary>Semanas depois de uma crise na zona em que a emergência ainda vale para contratar sem licitação.</summary>
    public required int EmergenciaValidadeSemanas { get; init; }

    public required int GravidadeEmergencialIrregular { get; init; }

    public required Range Sobrepreco { get; init; }

    /// <summary>Chance de sobrepreço numa licitação comum, por ponto de integridade abaixo de 3 do secretário.</summary>
    public required double SobreprecoPorIntegridade { get; init; }

    public required Dictionary<string, int> EmpresasPorPorte { get; init; }

    /// <summary>Peso da qualidade na escolha do vencedor: maior = a melhor empresa vence mais.</summary>
    public required double PesoQualidade { get; init; }

    /// <summary>Aprovação que o governo perde quando a empresa de um doador vence uma licitação (pontos, negativo).</summary>
    public required double ImagemEmpresaDoadora { get; init; }

    /// <summary>Chance de um secretário de pouca integridade do governo NPC forçar a contratação emergencial sem emergência.</summary>
    public required double NpcChanceEmergencialIrregular { get; init; }
}

public sealed class CompanyNames
{
    public required List<string> Tipos { get; init; }

    public required List<string> Nucleos { get; init; }

    public required List<string> Complementos { get; init; }
}

public sealed class ProcurementCatalog : IValidatable
{
    public const string File = "executive/procurement.json";

    public required string Note { get; init; }

    public required ProcurementRules Regras { get; init; }

    public required List<ProcurementOption> Modalidades { get; init; }

    public required CompanyNames Empresas { get; init; }

    public ProcurementOption Option(ProcurementMode mode) => Modalidades.First(m => m.Modo == mode);

    public IEnumerable<string> Validate()
    {
        foreach (ProcurementMode mode in Enum.GetValues<ProcurementMode>())
        {
            if (Modalidades.Count(m => m.Modo == mode) != 1)
            {
                yield return $"modalidades: é preciso exatamente uma entrada para '{mode}'.";
            }
        }

        foreach (ProcurementOption m in Modalidades)
        {
            string where = $"modalidade '{m.Modo}'";
            if (m.Semanas.Min < 1 || m.Semanas.Max < m.Semanas.Min || m.ChanceImpugnacao is < 0 or > 1 || m.ChanceSobrepreco is < 0 or > 1 || m.Nome.Length is 0 or > 28)
            {
                yield return $"{where}: prazos, chances e nome de até 28 caracteres.";
            }

            foreach (string error in DecisionNature.Check(m.Ganho, m.Custo, where))
            {
                yield return error;
            }
        }

        ProcurementRules r = Regras;
        if (r.EmergenciaValidadeSemanas < 1 || r.GravidadeEmergencialIrregular is < 1 or > 3 || !r.Sobrepreco.Valid || r.Sobrepreco.Min <= 0
            || r.SobreprecoPorIntegridade is < 0 or > 1 || r.PesoQualidade < 0 || GovernanceContent.Portes.Any(p => r.EmpresasPorPorte.GetValueOrDefault(p) < 2))
        {
            yield return "regras: valores fora do intervalo (ao menos 2 empresas por porte).";
        }

        if (Empresas.Tipos.Count == 0 || Empresas.Nucleos.Count < 6 || Empresas.Complementos.Count == 0)
        {
            yield return "empresas: listas de nomes fictícios insuficientes.";
        }
    }
}

// ============================================================ secretários (data/executive/secretaries.json)

public sealed class SecretariaDefinition
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>Área do orçamento e indicador que a pasta move (id de data/executive/config.json), ou null.</summary>
    public string? Area { get; init; }
}

public sealed class SecretaryAgenda
{
    public const string Candidato = "candidato";
    public const string Zona = "zona";
    public const string Projeto = "projeto";
    public const string Cacique = "cacique";

    public required string Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>Texto da ficha; aceita {zona} e {obra}.</summary>
    public required string Texto { get; init; }

    public required double Weight { get; init; }
}

public sealed class SecretaryRules
{
    /// <summary>Multiplicador da velocidade do indicador da pasta, por competência (1 a 5).</summary>
    public required List<double> VelocidadePorCompetencia { get; init; }

    /// <summary>Multiplicador da duração do projeto e da chance de atraso das obras da pasta, por competência.</summary>
    public required List<double> AtrasoPorCompetencia { get; init; }

    public required IntRange LealdadeInicial { get; init; }

    public required int LealdadeDoProprioPartido { get; init; }

    /// <summary>Integridade (1 a 5) abaixo ou igual à qual o secretário pode causar escândalo.</summary>
    public required int IntegridadeDeRisco { get; init; }

    /// <summary>Chance semanal de o secretário com agenda de candidato aparecer por conta própria.</summary>
    public required double ChanceAparicao { get; init; }

    /// <summary>Lealdade abaixo da qual a aparição é contra o prefeito.</summary>
    public required int LealdadeDeCritica { get; init; }

    public required double ImagemCritica { get; init; }

    public required int RelacaoBancadaAoNomear { get; init; }

    public required int RelacaoBancadaAoDemitir { get; init; }

    public required int OfertasPorPartido { get; init; }

    /// <summary>Semanas no cargo até o prefeito conhecer a competência exata.</summary>
    public required int SemanasParaConhecer { get; init; }

    /// <summary>Fator de velocidade das obras na zona favorecida e nas demais (agenda "zona").</summary>
    public required double ZonaFavorecida { get; init; }

    public required double ZonaPreterida { get; init; }

    /// <summary>Semanas que uma pasta fica vaga antes de o partido do prefeito indicar alguém.</summary>
    public required int SemanasPastaVaga { get; init; }

    /// <summary>Aprovação de partida do prefeito NPC, e quanto ela (e a de cada zona) volta ao centro por semana.</summary>
    public required Range AprovacaoInicialNpc { get; init; }

    public required double AprovacaoNpcRetornoSemanal { get; init; }

    /// <summary>Aprovação que uma crise tira do prefeito NPC, e da zona atingida (nos dois papéis).</summary>
    public required double CriseNpcDano { get; init; }

    public required double CriseZonaDano { get; init; }

    /// <summary>Chance de o prefeito NPC dar à crise a resposta de melhor imagem.</summary>
    public required double NpcBoaEscolha { get; init; }
}

public sealed class SecretaryCatalog : IValidatable
{
    public const string File = "executive/secretaries.json";

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "nomeado", "demitido", "saiu_da_base", "aparicao", "critica", "escandalo_causa", "previsao_nomear", "previsao_demitir", "previsao_demitir_base", "previsao_competencia",
        "previsao_agenda", "vinculo_partido", "vinculo_sem_partido", "posse_npc",
    ];

    public static readonly IReadOnlyList<string> MemoryKeys = ["nomeado", "demitido", "preterido"];

    public required string Note { get; init; }

    public required SecretaryRules Regras { get; init; }

    public required List<SecretariaDefinition> Secretarias { get; init; }

    public required List<SecretaryAgenda> Agendas { get; init; }

    public required Dictionary<string, MemoryDefinition> Lembrancas { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public SecretariaDefinition Secretaria(string id) => Secretarias.First(s => s.Id == id);

    public SecretaryAgenda Agenda(string id) => Agendas.First(a => a.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Secretarias.Select(s => s.Id), "Secretaria"))
        {
            yield return error;
        }

        SecretaryRules r = Regras;
        if (r.VelocidadePorCompetencia.Count != 5 || r.AtrasoPorCompetencia.Count != 5 || r.VelocidadePorCompetencia.Any(v => v <= 0) || r.AtrasoPorCompetencia.Any(v => v <= 0)
            || r.LealdadeInicial.Min < 0 || r.LealdadeInicial.Max > 100 || r.LealdadeInicial.Max < r.LealdadeInicial.Min || r.IntegridadeDeRisco is < 1 or > 5
            || r.ChanceAparicao is < 0 or > 1 || r.OfertasPorPartido < 1 || r.SemanasParaConhecer < 1 || r.ZonaFavorecida <= 0 || r.ZonaPreterida <= 0
            || r.SemanasPastaVaga < 1 || !r.AprovacaoInicialNpc.Valid || r.AprovacaoNpcRetornoSemanal is < 0 or > 1 || r.CriseNpcDano < 0 || r.CriseZonaDano < 0 || r.NpcBoaEscolha is < 0 or > 1)
        {
            yield return "regras: valores fora do intervalo.";
        }

        foreach (string id in new[] { SecretaryAgenda.Candidato, SecretaryAgenda.Zona, SecretaryAgenda.Projeto, SecretaryAgenda.Cacique }.Where(id => Agendas.All(a => a.Id != id)))
        {
            yield return $"agendas: falta '{id}'.";
        }

        if (Agendas.Any(a => a.Weight <= 0 || a.Nome.Length is 0 or > 28))
        {
            yield return "agendas: peso positivo e nome de até 28 caracteres.";
        }

        foreach (string key in MemoryKeys.Where(k => !Lembrancas.ContainsKey(k)))
        {
            yield return $"lembrancas: falta '{key}'.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

// ============================================================ servidores e folha (data/executive/personnel.json)

/// <summary>Mesa de negociação anual com uma categoria (data-base). A greve só nasce de uma negociação que falhou.</summary>
public sealed class NegotiationTable
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>Quem senta do outro lado, para o texto ("o sindicato dos servidores").</summary>
    public required string Categoria { get; init; }

    public required int Mes { get; init; }

    /// <summary>Crise (id de data/executive/crises.json) que começa se a negociação falhar.</summary>
    public required string Crise { get; init; }

    /// <summary>O reajuste concedido entra na folha da prefeitura.</summary>
    public required bool AfetaFolha { get; init; }

    /// <summary>Quando não afeta a folha: custo do acordo, em fração do orçamento por ponto percentual concedido.</summary>
    public double CustoPorPonto { get; init; }

    public required Range Pedido { get; init; }
}

public sealed class NegotiationAnswer
{
    public const string Conceder = "conceder";
    public const string Contraproposta = "contraproposta";
    public const string RevisaoGeral = "revisao_geral";
    public const string Negar = "negar";

    public static readonly IReadOnlyList<string> Ids = [Conceder, Contraproposta, RevisaoGeral, Negar];

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    public required List<string> Ganho { get; init; }

    public required List<string> Custo { get; init; }
}

public sealed class PersonnelRules
{
    /// <summary>Folha do Executivo sobre a RCL quando o RGF do município não foi importado (estimativa de jogo).</summary>
    public required Dictionary<string, Range> FolhaInicialPorPorte { get; init; }

    /// <summary>Crescimento vegetativo da folha por ano (progressões, quinquênios), em fração da própria folha.</summary>
    public required double CrescimentoVegetativoAnual { get; init; }

    /// <summary>Crescimento nominal da RCL por ano, até o modelo de evolução dos indicadores fornecer o número.</summary>
    public required Range RclCrescimentoAnual { get; init; }

    /// <summary>Revisão geral anual (reposição da inflação), sorteada a cada ano na faixa.</summary>
    public required Range RevisaoGeral { get; init; }

    /// <summary>Semanas que a mesa fica aberta antes de valer a resposta padrão.</summary>
    public required int PrazoSemanas { get; init; }

    /// <summary>Chance de o sindicato aceitar = base + peso × (oferta ÷ pedido) + relação × por_relacao.</summary>
    public required double AceiteBase { get; init; }

    public required double AceitePorOferta { get; init; }

    public required double AceitePorRelacao { get; init; }

    /// <summary>Fração do pedido que a contraproposta oferece.</summary>
    public required double ContrapropostaFracao { get; init; }

    /// <summary>Quanto o que não foi concedido se soma ao pedido do ano seguinte.</summary>
    public required double PerdaAcumulada { get; init; }

    /// <summary>Corte de pessoal (comissionados, horas extras): fração da folha por corte.</summary>
    public required double CorteFracao { get; init; }

    public required double CorteAprovacao { get; init; }

    /// <summary>Aprovação que o governo perde por semana com o prazo do art. 23 vencido.</summary>
    public required double RestricaoAprovacaoSemanal { get; init; }

    /// <summary>Folha (fração da RCL) a partir da qual o prefeito NPC só oferece a revisão geral.</summary>
    public required double NpcFolhaConfortavel { get; init; }

    /// <summary>Repasse à Câmara no começo do mandato, como fração do teto do art. 29-A.</summary>
    public required double RepasseCamaraInicial { get; init; }

    /// <summary>Tipo de liderança (id de data/campaign/leaders.json) que fala pelos servidores na mesa.</summary>
    public required string LiderancaSindical { get; init; }

    public required int RelacaoCamaraPorCorteDeRepasse { get; init; }
}

public sealed class PersonnelCatalog : IValidatable
{
    public const string File = "executive/personnel.json";

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "mesa_aberta", "acordo", "fracasso", "greve", "alerta", "prudencial", "excedido", "restricoes", "normalizou", "corte", "revisao_anual", "vedado_art22", "repasse_camara",
        "repasse_acima_do_teto", "previsao_conceder", "previsao_contraproposta", "previsao_revisao_geral", "previsao_negar", "previsao_folha", "previsao_corte", "sem_resposta",
    ];

    public static readonly IReadOnlyList<string> MemoryKeys = ["acordo", "fracasso", "corte", "repasse"];

    public required string Note { get; init; }

    public required PersonnelRules Regras { get; init; }

    public required List<NegotiationTable> Mesas { get; init; }

    public required List<NegotiationAnswer> Respostas { get; init; }

    public required Dictionary<string, MemoryDefinition> Lembrancas { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public NegotiationTable Mesa(string id) => Mesas.First(m => m.Id == id);

    public NegotiationAnswer Resposta(string id) => Respostas.First(r => r.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Mesas.Select(m => m.Id), "Mesa de negociação"))
        {
            yield return error;
        }

        PersonnelRules r = Regras;
        foreach (string porte in GovernanceContent.Portes)
        {
            if (!r.FolhaInicialPorPorte.TryGetValue(porte, out Range? range) || !range.Valid || range.Min <= 0 || range.Max >= 1)
            {
                yield return $"regras: folha_inicial_por_porte válida para '{porte}'.";
            }
        }

        if (r.CrescimentoVegetativoAnual < 0 || !r.RclCrescimentoAnual.Valid || !r.RevisaoGeral.Valid || r.RevisaoGeral.Min < 0 || r.PrazoSemanas < 1
            || r.ContrapropostaFracao is <= 0 or >= 1 || r.PerdaAcumulada is < 0 or > 1 || r.CorteFracao is <= 0 or >= 1 || r.RepasseCamaraInicial is <= 0 or > 1
            || r.NpcFolhaConfortavel is <= 0 or >= 1)
        {
            yield return "regras: valores fora do intervalo.";
        }

        if (Mesas.Count == 0 || !Mesas.Any(m => m.AfetaFolha))
        {
            yield return "mesas: é preciso ao menos a mesa dos servidores (afeta_folha).";
        }

        foreach (NegotiationTable m in Mesas.Where(m => m.Mes is < 1 or > 12 || !m.Pedido.Valid || m.Pedido.Min <= 0 || (!m.AfetaFolha && m.CustoPorPonto <= 0)))
        {
            yield return $"mesa '{m.Id}': mês de 1 a 12, pedido positivo e custo por ponto quando não afeta a folha.";
        }

        foreach (string id in NegotiationAnswer.Ids)
        {
            NegotiationAnswer? answer = Respostas.FirstOrDefault(a => a.Id == id);
            if (answer is null)
            {
                yield return $"respostas: falta '{id}'.";
                continue;
            }

            foreach (string error in DecisionNature.Check(answer.Ganho, answer.Custo, $"negociação, resposta '{id}'"))
            {
                yield return error;
            }
        }

        foreach (string key in MemoryKeys.Where(k => !Lembrancas.ContainsKey(k)))
        {
            yield return $"lembrancas: falta '{key}'.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

// ============================================================ convênios (data/executive/agreements.json)

public sealed class AgreementOrigin
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required double Weight { get; init; }
}

public sealed class AgreementRules
{
    public required double ChanceOfertaSemanal { get; init; }

    /// <summary>Quanto a relação com o governador (−100 a 100) muda a chance de oferta.</summary>
    public required double OfertaPorRelacao { get; init; }

    /// <summary>Contrapartida do município: fração do custo da obra que o convênio não paga.</summary>
    public required Range Contrapartida { get; init; }

    /// <summary>Folga, em semanas, entre o prazo normal da obra e o prazo do convênio.</summary>
    public required IntRange FolgaSemanas { get; init; }

    public required int OfertaValeSemanas { get; init; }

    /// <summary>Semanas sem convênio novo depois de devolver o dinheiro de um.</summary>
    public required int BloqueioPorDevolucao { get; init; }

    public required double ImagemDevolucao { get; init; }

    public required Range RelacaoGovernadorInicial { get; init; }

    public required int RelacaoPorConvenioCumprido { get; init; }

    public required int RelacaoPorDevolucao { get; init; }

    public required int MaximoAbertos { get; init; }
}

public sealed class AgreementCatalog : IValidatable
{
    public const string File = "executive/agreements.json";

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "oferta", "assinado", "recusado", "expirou", "cumprido", "devolvido", "bloqueado_lrf", "bloqueado_devolucao", "bloqueado_eleicao", "previsao_assinar", "previsao_contrapartida", "previsao_prazo",
        "previsao_risco",
    ];

    public required string Note { get; init; }

    public required AgreementRules Regras { get; init; }

    public required List<AgreementOrigin> Origens { get; init; }

    public required List<string> Ganho { get; init; }

    public required List<string> Custo { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        AgreementRules r = Regras;
        if (r.ChanceOfertaSemanal is < 0 or > 1 || !r.Contrapartida.Valid || r.Contrapartida.Min <= 0 || r.Contrapartida.Max >= 1 
            || r.FolgaSemanas.Max < r.FolgaSemanas.Min || r.OfertaValeSemanas < 1 || r.BloqueioPorDevolucao < 1 || !r.RelacaoGovernadorInicial.Valid || r.MaximoAbertos < 1)
        {
            yield return "regras: valores fora do intervalo.";
        }

        if (Origens.Count == 0 || Origens.Any(o => o.Weight <= 0))
        {
            yield return "origens: ao menos uma, com peso positivo.";
        }

        foreach (string error in DecisionNature.Check(Ganho, Custo, "convênio"))
        {
            yield return error;
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

// ============================================================ instrumentos do vereador (data/legislature/instruments.json)

/// <summary>Instrumento do mandato de vereador (GDD 17.14). Os números são parâmetros de jogo.</summary>
public sealed class InstrumentDefinition
{
    public const string Requerimento = "requerimento";
    public const string Indicacao = "indicacao";
    public const string Fiscalizacao = "fiscalizacao";
    public const string Emenda = "emenda";
    public const string Projeto = "projeto";
    public const string NegociarVoto = "negociar_voto";

    public static readonly IReadOnlyList<string> Ids = [Requerimento, Indicacao, Fiscalizacao, Emenda, Projeto, NegociarVoto];

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    /// <summary>Turnos da semana que ocupa.</summary>
    public required int Turnos { get; init; }

    /// <summary>Capital político que custa.</summary>
    public required int Capital { get; init; }

    /// <summary>Mudança na relação com o prefeito ao usar.</summary>
    public required int RelacaoPrefeito { get; init; }

    /// <summary>Presença que rende na zona na hora (a visita, o anúncio), antes dos fatores.</summary>
    public required double Presenca { get; init; }

    /// <summary>Imagem na hora, antes dos fatores.</summary>
    public required double Imagem { get; init; }

    /// <summary>Semanas até a resposta ou a decisão do outro lado.</summary>
    public IntRange? PrazoSemanas { get; init; }

    /// <summary>Chance de dar certo antes da relação com o prefeito (indicação) ou de achar o problema (fiscalização).</summary>
    public double ChanceBase { get; init; } = 1;

    public double ChancePorRelacao { get; init; }

    public required List<string> Ganho { get; init; }

    public required List<string> Custo { get; init; }
}

/// <summary>O que fazer com o que a fiscalização achou (GDD 17.14).</summary>
public sealed class FindingOption
{
    public const string Denunciar = "denunciar";
    public const string Negociar = "negociar";
    public const string Tribunal = "tribunal";

    public static readonly IReadOnlyList<string> Ids = [Denunciar, Negociar, Tribunal];

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    public required double Imagem { get; init; }

    public required double Presenca { get; init; }

    public required int RelacaoPrefeito { get; init; }

    /// <summary>Gravidade do esqueleto que a opção cria (0 = nenhum).</summary>
    public required int Esqueleto { get; init; }

    /// <summary>A prefeitura abre uma obra pequena na zona a pedido do vereador.</summary>
    public required bool GanhaObra { get; init; }

    /// <summary>Semanas até o resultado (Tribunal de Contas).</summary>
    public IntRange? Semanas { get; init; }

    public required List<string> Ganho { get; init; }

    public required List<string> Custo { get; init; }

    /// <summary>Lembrança em quem é atingido (prefeito e dono da empresa) ou em quem fecha o acordo.</summary>
    public required MemoryDefinition Lembranca { get; init; }
}

/// <summary>Função de assessor no gabinete (GDD 17.14).</summary>
public sealed class AideRole
{
    public const string Chefe = "chefe";
    public const string Legislativo = "legislativo";
    public const string Comunicacao = "comunicacao";
    public const string Base = "base";

    public static readonly IReadOnlyList<string> Ids = [Chefe, Legislativo, Comunicacao, Base];

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    /// <summary>Efeito por qualidade (1 a 5); o que ele multiplica ou soma depende da função.</summary>
    public required List<double> PorQualidade { get; init; }

    /// <summary>Quantos podem ser nomeados na função (os de base, um por zona).</summary>
    public required int Maximo { get; init; }

    public double At(int quality) => PorQualidade[Math.Clamp(quality, 1, PorQualidade.Count) - 1];
}

/// <summary>Pedido de um vereador indeciso em troca do voto (GDD 17.14 e 17.15).</summary>
public sealed class VoteRequest
{
    public const string ApoioProjeto = "apoio_projeto";
    public const string EmendaReduto = "emenda_reduto";
    public const string Cargo = "cargo";
    public const string ObraReduto = "obra_reduto";
    public const string LiberarEmenda = "liberar_emenda";

    public required string Id { get; init; }

    /// <summary>"vereador", "prefeito" ou os dois: quem pode receber esse pedido.</summary>
    public required List<string> Papeis { get; init; }

    /// <summary>Texto do pedido; aceita {zona} e {projeto}.</summary>
    public required string Texto { get; init; }

    public required int PrazoSemanas { get; init; }

    public required double Weight { get; init; }

    public required List<string> Ganho { get; init; }

    public required List<string> Custo { get; init; }
}

public sealed class InstrumentRules
{
    public required int TurnosPorSemana { get; init; }

    /// <summary>Rendimento do instrumento repetido: fator elevado ao cansaço acumulado.</summary>
    public required double CansacoFator { get; init; }

    /// <summary>O mesmo, para o mesmo instrumento na mesma zona.</summary>
    public required double CansacoZonaFator { get; init; }

    /// <summary>Quanto de cansaço some por semana.</summary>
    public required double CansacoRecuperacaoSemanal { get; init; }

    public required Dictionary<string, int> GabinetePorPorte { get; init; }

    /// <summary>Presença que se perde por semana em zona sem assessor de base, e o piso (fração da presença da posse).</summary>
    public required double PresencaDecaimentoSemanal { get; init; }

    public required double PresencaPiso { get; init; }

    /// <summary>Zonas que formam a base: as de maior presença na posse.</summary>
    public required int ZonasDaBase { get; init; }

    /// <summary>A base cobra presença quando a zona cai abaixo desta fração da presença da posse.</summary>
    public required double BaseCobraAbaixo { get; init; }

    public required int BaseCobraPrazoSemanas { get; init; }

    /// <summary>Chance semanal de o assessor de base trazer uma demanda da zona (bônus para indicar ali).</summary>
    public required double ChanceDemandaDeBase { get; init; }

    public required double BonusDemandaDeBase { get; init; }

    /// <summary>Semanas sem nada do gabinete na zona até o assessor de base ficar negligenciado.</summary>
    public required int SemanasDeNegligencia { get; init; }

    /// <summary>Probabilidade de voto a partir da qual a contagem dá o vereador como certo (e o complemento, contra).</summary>
    public required double ContagemCerteza { get; init; }

    /// <summary>Bônus no logito do voto de quem fechou acordo.</summary>
    public required double AcordoLogito { get; init; }

    public required int SemanasSemNegociarDepoisDeEnganado { get; init; }

    /// <summary>Chance de a obra em andamento esconder irregularidade, antes da empresa e do secretário.</summary>
    public required double ChanceIrregularidadeBase { get; init; }

    /// <summary>Semanas até a mesma obra poder ser fiscalizada de novo.</summary>
    public required int SemanasEntreFiscalizacoes { get; init; }

    /// <summary>Semanas para decidir o que fazer com o achado antes de valer o padrão (Tribunal de Contas).</summary>
    public required int PrazoDoAchadoSemanas { get; init; }

    /// <summary>Gravidade do esqueleto de nomear parente no gabinete.</summary>
    public required int GravidadeNepotismo { get; init; }

    /// <summary>Candidatos de mercado por vaga do gabinete.</summary>
    public required int CandidatosPorVaga { get; init; }

    /// <summary>Imagem que rende o requerimento cuja resposta revela irregularidade.</summary>
    public required double ImagemRequerimentoRevelador { get; init; }

    /// <summary>Fração do efeito do achado quando ele é só atraso, e não irregularidade.</summary>
    public required double AchadoAtrasoFator { get; init; }

    /// <summary>Quanto um cargo de confiança acrescenta à folha, em fração da RCL.</summary>
    public required double CargoFolha { get; init; }
}

public sealed class InstrumentCatalog : IValidatable
{
    public const string File = "legislature/instruments.json";

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "requerimento_protocolado", "requerimento_resposta_normal", "requerimento_resposta_atraso", "requerimento_resposta_irregular", "requerimento_secretaria",
        "indicacao_protocolada", "indicacao_atendida", "indicacao_negada", "fiscalizacao_normal", "fiscalizacao_atraso", "fiscalizacao_irregular", "achado_denunciar",
        "achado_negociar", "achado_tribunal", "achado_so_irregularidade", "tribunal_resultado", "esqueleto_acordo", "projeto_protocolado", "acordo_fechado", "acordo_cumprido", "acordo_quebrado",
        "acordo_livro", "assessor_nomeado", "assessor_exonerado", "assessor_preterido", "assessor_negligenciado", "esqueleto_nepotismo", "demanda_de_base", "base_cobra",
        "base_cobra_atendida", "base_cobra_vencida", "instrucao", "sem_turno", "sem_capital", "contagem",
        "motivo_lider", "motivo_governo", "motivo_oposicao", "motivo_ideologia", "motivo_relacao", "motivo_pedido", "motivo_acordo",
        "previsao_requerimento", "previsao_indicacao", "previsao_fiscalizacao", "previsao_emenda", "previsao_projeto", "previsao_negociar", "previsao_prefeito",
        "previsao_quem_lembra", "previsao_livro", "fator_cansaco", "fator_cansaco_zona", "fator_comunicacao", "fator_demanda",
        "conflito_orientacao", "conflito_base", "conflito_acordo",
    ];

    public static readonly IReadOnlyList<string> MemoryKeys =
        ["nomeado", "exonerado", "preterido", "acordo", "acordo_cumprido", "acordo_quebrado", "indicacao", "indicacao_negada", "emenda", "requerimento", "base_cobra_vencida", "base_cobra_atendida"];

    public required string Note { get; init; }

    public required InstrumentRules Regras { get; init; }

    public required List<InstrumentDefinition> Instrumentos { get; init; }

    public required List<FindingOption> Achados { get; init; }

    public required List<AideRole> Funcoes { get; init; }

    public required List<VoteRequest> Pedidos { get; init; }

    public required Dictionary<string, MemoryDefinition> Lembrancas { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public InstrumentDefinition Instrument(string id) => Instrumentos.First(i => i.Id == id);

    public FindingOption Finding(string id) => Achados.First(a => a.Id == id);

    public AideRole Role(string id) => Funcoes.First(f => f.Id == id);

    public VoteRequest Request(string id) => Pedidos.First(p => p.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        foreach (string id in InstrumentDefinition.Ids.Where(id => Instrumentos.Count(i => i.Id == id) != 1))
        {
            yield return $"instrumentos: é preciso exatamente um '{id}'.";
        }

        foreach (string id in FindingOption.Ids.Where(id => Achados.Count(a => a.Id == id) != 1))
        {
            yield return $"achados: é preciso exatamente um '{id}'.";
        }

        foreach (string id in AideRole.Ids.Where(id => Funcoes.Count(f => f.Id == id) != 1))
        {
            yield return $"funcoes: é preciso exatamente uma '{id}'.";
        }

        foreach (string papel in new[] { GovernanceContent.PapelVereador, GovernanceContent.PapelPrefeito }.Where(p => !Pedidos.Any(x => x.Papeis.Contains(p) && x.Weight > 0)))
        {
            yield return $"pedidos: nenhum pedido para o papel '{papel}'.";
        }

        InstrumentRules r = Regras;
        if (r.TurnosPorSemana < 1 || r.CansacoFator is <= 0 or > 1 || r.CansacoZonaFator is <= 0 or > 1 || r.CansacoRecuperacaoSemanal <= 0
            || GovernanceContent.Portes.Any(p => r.GabinetePorPorte.GetValueOrDefault(p) < 1) || r.PresencaDecaimentoSemanal < 0 || r.PresencaPiso is < 0 or > 1
            || r.ZonasDaBase < 1 || r.BaseCobraAbaixo is <= 0 or > 1 || r.BaseCobraPrazoSemanas < 1 || r.ChanceDemandaDeBase is < 0 or > 1 || r.SemanasDeNegligencia < 1
            || r.ContagemCerteza is <= 0.5 or >= 1 || r.AcordoLogito <= 0 || r.ChanceIrregularidadeBase is < 0 or > 1 || r.PrazoDoAchadoSemanas < 1
            || r.GravidadeNepotismo is < 1 or > 3 || r.CandidatosPorVaga < 1 || r.AchadoAtrasoFator is < 0 or > 1 || r.CargoFolha < 0 || r.SemanasEntreFiscalizacoes < 1)
        {
            yield return "regras: valores fora do intervalo.";
        }

        foreach (AideRole f in Funcoes.Where(f => f.PorQualidade.Count != 5 || f.Maximo < 1 || f.Nome.Length is 0 or > 28))
        {
            yield return $"função '{f.Id}': cinco valores por qualidade, máximo positivo e nome de até 28 caracteres.";
        }

        foreach (string error in GovernanceLint.Instruments(this))
        {
            yield return error;
        }

        foreach (string key in MemoryKeys.Where(k => !Lembrancas.ContainsKey(k)))
        {
            yield return $"lembrancas: falta '{key}'.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

// ============================================================ Mesa, CPI e cassação (data/legislature/chamber.json)

public sealed class ChamberRules
{
    /// <summary>Peso de estar na base (ou fora dela) no voto para a Mesa.</summary>
    public required double MesaPesoBase { get; init; }

    public required double MesaPesoRelacao { get; init; }

    public required int MesaCapitalParaConcorrer { get; init; }

    /// <summary>Capital político que o presidente da Câmara ganha por mês.</summary>
    public required int MesaCapitalMensal { get; init; }

    /// <summary>Bônus do presidente na chance de o próprio projeto sair da comissão.</summary>
    public required double MesaBonusComissao { get; init; }

    public required int MesaRelacaoDosEleitores { get; init; }

    public required IntRange CpiSemanas { get; init; }

    /// <summary>Relação com o jogador a partir da qual um vereador de fora da oposição assina a CPI dele.</summary>
    public required int CpiAssinaComRelacao { get; init; }

    /// <summary>Prova inicial pela gravidade do fato, e quanto um requerimento respondido acrescenta.</summary>
    public required double CpiProvaPorGravidade { get; init; }

    public required double CpiProvaPorRequerimento { get; init; }

    /// <summary>Prova (0 a 1) a partir da qual o relatório indicia.</summary>
    public required double CpiIndiciaAcima { get; init; }

    public required double CpiAprovacaoPorSemana { get; init; }

    public required double CpiAprovacaoSeIndicia { get; init; }

    public required double CpiImagemDoAutor { get; init; }

    public required double CpiPresencaDoAutor { get; init; }

    /// <summary>Contra o prefeito jogador: aprovação abaixo da qual um escândalo abre CPI.</summary>
    public required double CpiAprovacaoAbaixo { get; init; }

    /// <summary>Semanas em que um escândalo (crise ou esqueleto exposto) ainda sustenta uma CPI.</summary>
    public required int CpiEscandaloValeSemanas { get; init; }

    /// <summary>Aprovação abaixo da qual a CPI que indiciou vira pedido de cassação.</summary>
    public required double CassacaoAprovacaoAbaixo { get; init; }

    public required double CassacaoPesoBase { get; init; }

    public required double CassacaoPesoRelacao { get; init; }

    public required double CassacaoPesoAprovacao { get; init; }

    public required int SemanasEntreCpis { get; init; }
}

public sealed class ChamberCatalog : IValidatable
{
    public const string File = "legislature/chamber.json";

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "mesa_eleita", "mesa_eleita_jogador", "mesa_derrota_jogador", "cpi_aberta", "cpi_sem_assinaturas", "cpi_semana", "cpi_arquivada", "cpi_indiciou", "cassacao_pedido",
        "cassacao_aprovada", "cassacao_rejeitada", "vice_assume", "cassado_jogador", "previsao_mesa", "previsao_cpi", "previsao_cpi_assinaturas", "previsao_cassacao",
        "cpi_motivo_obra", "cpi_motivo_escandalo",
    ];

    public static readonly IReadOnlyList<string> MemoryKeys = ["mesa_contra", "cpi_autor", "vice_grato"];

    public required string Note { get; init; }

    public required ChamberRules Regras { get; init; }

    public required Dictionary<string, MemoryDefinition> Lembrancas { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        ChamberRules r = Regras;
        if (r.CpiSemanas.Min < 1 || r.CpiSemanas.Max < r.CpiSemanas.Min || r.CpiIndiciaAcima is <= 0 or >= 1 || r.CpiProvaPorGravidade <= 0 || r.CpiAprovacaoAbaixo is <= 0 or >= 100
            || r.CassacaoAprovacaoAbaixo is <= 0 or >= 100 || r.CpiEscandaloValeSemanas < 1 || r.MesaCapitalParaConcorrer < 0 || r.SemanasEntreCpis < 1)
        {
            yield return "regras: valores fora do intervalo.";
        }

        foreach (string key in MemoryKeys.Where(k => !Lembrancas.ContainsKey(k)))
        {
            yield return $"lembrancas: falta '{key}'.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

// ============================================================ cobrança do Livro (data/mandate/collection.json)

public sealed class CollectionRules
{
    /// <summary>Semanas depois da posse em que quem lembra faz a primeira cobrança.</summary>
    public required IntRange PrimeiraCobrancaSemanas { get; init; }

    /// <summary>Frações do prazo em que a promessa volta a ser cobrada.</summary>
    public required List<double> CobrancasNoPrazo { get; init; }

    /// <summary>Semanas entre cobranças de uma dívida.</summary>
    public required int DividaIntervaloSemanas { get; init; }

    public required int DividaCobrancasMaximas { get; init; }

    /// <summary>Presença que se perde na zona quando a promessa vence.</summary>
    public required double PresencaPromessaVencida { get; init; }

    public required double ImagemPromessaVencida { get; init; }

    public required double AprovacaoPromessaVencida { get; init; }

    public required double PresencaPromessaCumprida { get; init; }

    public required double ImagemPromessaCumprida { get; init; }

    /// <summary>Semanas depois da posse para nomear quem tem cargo prometido, quando a promessa não traz prazo útil.</summary>
    public required int PrazoMinimoSemanas { get; init; }
}

public sealed class CollectionCatalog : IValidatable
{
    public const string File = "mandate/collection.json";

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "cobranca_pessoa", "cobranca_zona", "cobranca_grupo", "cobranca_divida", "cumprida", "vencida", "divida_quitada", "divida_esquecida", "lideranca_desafiante",
        "quem_moradores", "quem_grupo",
    ];

    public static readonly IReadOnlyList<string> MemoryKeys = ["cobrou", "cumpriu", "venceu", "divida_quitada", "divida_ignorada", "obra_anunciada"];

    public required string Note { get; init; }

    public required CollectionRules Regras { get; init; }

    /// <summary>
    /// Pedido de liderança ou de equipe (o campo "object" da promessa) → chaves de obra que o cumprem. As promessas da
    /// plataforma trazem a chave no próprio catálogo (data/campaign/promises.json).
    /// </summary>
    public required Dictionary<string, List<string>> PromessaCumpre { get; init; }

    /// <summary>Objetos de promessa que se cumprem com uma nomeação (gabinete ou cargo).</summary>
    public required List<string> PromessaDeCargo { get; init; }

    /// <summary>Objetos de promessa que se cumprem com o mandato presente na zona.</summary>
    public required List<string> PromessaDePresenca { get; init; }

    /// <summary>Objetos de promessa que se cumprem defendendo o tema (projeto de lei no tema da liderança).</summary>
    public required List<string> PromessaDePosicao { get; init; }

    /// <summary>Objetos de promessa que se cumprem com qualquer obra ou serviço entregue na zona.</summary>
    public required List<string> PromessaDeObraNaZona { get; init; }

    public required Dictionary<string, MemoryDefinition> Lembrancas { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        CollectionRules r = Regras;
        if (r.PrimeiraCobrancaSemanas.Min < 0 || r.PrimeiraCobrancaSemanas.Max < r.PrimeiraCobrancaSemanas.Min || r.CobrancasNoPrazo.Any(f => f is <= 0 or >= 1)
            || r.DividaIntervaloSemanas < 1 || r.DividaCobrancasMaximas < 1 || r.PrazoMinimoSemanas < 1)
        {
            yield return "regras: valores fora do intervalo.";
        }

        foreach (string key in MemoryKeys.Where(k => !Lembrancas.ContainsKey(k)))
        {
            yield return $"lembrancas: falta '{key}'.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

// ============================================================ textos do governo (data/mandate/texts.json)

public sealed class TermTextCatalog : IValidatable
{
    public const string File = "mandate/texts.json";

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "mud_presenca", "mud_imagem", "mud_relacao", "mud_relacao_bancada", "mud_capital", "mud_aprovacao", "mud_aprovacao_zona", "mud_dinheiro", "mud_estresse",
        "mud_folha", "mud_folha_pontos", "mud_investimento", "mud_obra_prazo", "mud_lealdade", "mud_esqueleto", "mud_esqueleto_sem_testemunha", "mud_promessa",
        "mud_divida", "mud_cpi", "mud_lembranca", "mud_sai", "mud_acordo_cumprido", "mud_voto", "mud_etapa_ate", "mud_tribunal", "mud_convenio_prazo",
        "mud_obra_a_pedido", "mud_assume", "mud_repasse", "mud_subsidio", "mud_prova", "bancada_melhora", "etapa_liberacao", "etapa_projeto", "etapa_modalidade",
        "etapa_licitacao", "etapa_execucao", "etapa_pronta", "etapa_entregue", "etapa_cancelada", "faixa_normal", "faixa_alerta", "faixa_prudencial", "faixa_excedido",
        "instrucao_nenhuma", "instrucao_orientacao", "instrucao_plataforma", "voto_sim", "voto_nao", "placar", "faixa_de_semanas", "e", "ou", "alvo_obra", "alvo_tipo",
        "alvo_voto", "funcao_na_zona", "papel_secretario", "papel_vereador", "papel_vereador_sem_partido", "papel_empresa", "comissao_passa", "comissao_arquiva",
        "como_cargo", "como_presenca", "como_posicao", "como_voto", "como_obra", "como_obra_qualquer", "onde_cidade", "onde_zona", "noticia_crise",
        "noticia_crise_resposta", "noticia_decisao", "noticia_decisao_resposta", "promessa_de_evento", "semana_obra", "semana_folha", "semana_aprovacao",
        "semana_presenca", "semana_camara",
    ];

    public required string Note { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

// ============================================================ eventos de mandato (data/mandate/events/*.json)

/// <summary>Efeitos de uma opção de evento de mandato. Tudo é opcional; o lint confere com o ganho e o custo declarados.</summary>
public sealed class TermEffects
{
    public double Imagem { get; init; }

    public int Capital { get; init; }

    /// <summary>Relação com a pessoa do evento.</summary>
    public int Relacao { get; init; }

    public int RelacaoPrefeito { get; init; }

    /// <summary>Presença do vereador na zona do evento.</summary>
    public double Presenca { get; init; }

    /// <summary>Aprovação do governo na zona do evento (prefeito).</summary>
    public double AprovacaoZona { get; init; }

    /// <summary>Aprovação do governo na cidade (prefeito), em pontos.</summary>
    public double Aprovacao { get; init; }

    public decimal Dinheiro { get; init; }

    public int Estresse { get; init; }

    /// <summary>Folha, em fração da RCL (prefeito).</summary>
    public double Folha { get; init; }

    /// <summary>Investimento do ano, em fração da RCL (negativo = gasta).</summary>
    public double Investimento { get; init; }

    /// <summary>Semanas somadas (ou tiradas) do prazo da obra do evento.</summary>
    public int ObraSemanas { get; init; }

    /// <summary>Lealdade do secretário ou assessor do evento.</summary>
    public int Lealdade { get; init; }

    /// <summary>Gravidade do esqueleto criado (0 = nenhum); a pessoa do evento passa a saber.</summary>
    public int Esqueleto { get; init; }

    public string? EsqueletoTipo { get; init; }

    /// <summary>Promessa no Livro à pessoa do evento, com este prazo em semanas (0 = nenhuma).</summary>
    public int PromessaSemanas { get; init; }

    /// <summary>O que foi prometido (decide como se cumpre: ver data/mandate/collection.json). Sem ele, a promessa leva o id do evento.</summary>
    public string? PromessaObjeto { get; init; }

    /// <summary>Dívida no Livro com a pessoa do evento.</summary>
    public bool Divida { get; init; }

    /// <summary>Muda a prova da CPI em curso.</summary>
    public double CpiProva { get; init; }

    /// <summary>A pessoa do evento deixa o cargo (secretário ou assessor).</summary>
    public bool Sai { get; init; }

    public MemoryDefinition? Lembranca { get; init; }
}

public sealed class TermEventOption
{
    public required string Id { get; init; }

    public required string Rotulo { get; init; }

    public required string Resultado { get; init; }

    public required List<string> Ganho { get; init; }

    public required List<string> Custo { get; init; }

    public required TermEffects Efeitos { get; init; }

    /// <summary>Capital político mínimo para escolher.</summary>
    public int CapitalMinimo { get; init; }

    /// <summary>Opção que vale se o prazo vencer sem resposta.</summary>
    public bool Padrao { get; init; }
}

/// <summary>
/// Evento de mandato (GDD 17.14 e 17.15): nasce de um fato do save (o gatilho), cita a pessoa, a zona ou a obra desse
/// fato e pede uma decisão com troca.
/// </summary>
public sealed class TermEventDefinition
{
    public required string Id { get; init; }

    /// <summary>"vereador" e/ou "prefeito".</summary>
    public required List<string> Papeis { get; init; }

    /// <summary>Fato do save que dispara o evento (ver <see cref="TermTrigger"/>).</summary>
    public required string Gatilho { get; init; }

    /// <summary>Chance por semana enquanto o gatilho vale.</summary>
    public required double Chance { get; init; }

    public required int CooldownSemanas { get; init; }

    public required int PrazoSemanas { get; init; }

    public required string Titulo { get; init; }

    /// <summary>Aceita {pessoa}, {papel}, {zona}, {obra}, {prefeito}.</summary>
    public required string Texto { get; init; }

    public required List<TermEventOption> Opcoes { get; init; }
}

/// <summary>Gatilhos de evento de mandato: cada um é um fato conferido no estado do jogo.</summary>
public static class TermTrigger
{
    public const string ObraAtrasada = "obra_atrasada";
    public const string ObraDaBasePronta = "obra_pronta";
    public const string AssessorNegligenciado = "assessor_negligenciado";
    public const string VereadorComProjeto = "vereador_com_projeto";
    public const string LiderancaAliada = "lideranca_aliada";
    public const string PromessaPertoDoPrazo = "promessa_perto_do_prazo";
    public const string PrefeitoPedeVoto = "prefeito_pede_voto";
    public const string SecretarioCandidato = "secretario_candidato";
    public const string SecretarioProjeto = "secretario_projeto";
    public const string SecretarioDesleal = "secretario_desleal";
    public const string FolhaNoPrudencial = "folha_no_prudencial";
    public const string VereadorDaBaseInsatisfeito = "vereador_insatisfeito";
    public const string EmpresaComAtraso = "empresa_com_atraso";
    public const string CpiEmCurso = "cpi_em_curso";
    public const string CamaraPedeRepasse = "camara_pede_repasse";
    public const string ZonaDescontente = "zona_descontente";

    public static readonly IReadOnlyList<string> All =
    [
        ObraAtrasada, ObraDaBasePronta, AssessorNegligenciado, VereadorComProjeto, LiderancaAliada, PromessaPertoDoPrazo, PrefeitoPedeVoto, SecretarioCandidato,
        SecretarioProjeto, SecretarioDesleal, FolhaNoPrudencial, VereadorDaBaseInsatisfeito, EmpresaComAtraso, CpiEmCurso, CamaraPedeRepasse, ZonaDescontente,
    ];
}

public sealed class TermEventFile : IValidatable
{
    public required List<TermEventDefinition> Eventos { get; init; }

    public IEnumerable<string> Validate()
    {
        foreach (TermEventDefinition e in Eventos)
        {
            string where = $"evento de mandato '{e.Id}'";
            foreach (string error in DataChecks.Id(e.Id, where).Concat(DataChecks.Text(e.Titulo, "titulo", where)).Concat(DataChecks.Text(e.Texto, "texto", where)))
            {
                yield return error;
            }

            if (e.Papeis.Count == 0 || e.Papeis.Any(p => p is not (GovernanceContent.PapelVereador or GovernanceContent.PapelPrefeito)))
            {
                yield return $"{where}: papéis válidos são 'vereador' e 'prefeito'.";
            }

            if (!TermTrigger.All.Contains(e.Gatilho))
            {
                yield return $"{where}: gatilho '{e.Gatilho}' desconhecido.";
            }

            if (e.Chance is <= 0 or > 1 || e.CooldownSemanas < 0 || e.PrazoSemanas < 1 || e.Opcoes.Count is < 2 or > 4 || e.Opcoes.Count(o => o.Padrao) != 1)
            {
                yield return $"{where}: chance em (0, 1], prazo positivo, de 2 a 4 opções e exatamente uma padrão.";
            }

            foreach (string error in DataChecks.UniqueIds(e.Opcoes.Select(o => o.Id), $"{where}: opção"))
            {
                yield return error;
            }
        }
    }
}

public sealed class TermEventCatalog
{
    public const string Directory = "mandate/events";

    public required List<TermEventDefinition> Eventos { get; init; }

    public TermEventDefinition? Find(string id) => Eventos.FirstOrDefault(e => e.Id == id);
}

// ============================================================ conjunto

/// <summary>Conteúdo dos mandatos da R4 (GDD 17.14 e 17.15), já validado.</summary>
public sealed class GovernanceContent
{
    public const string PapelVereador = "vereador";
    public const string PapelPrefeito = "prefeito";

    public static readonly IReadOnlyList<string> Portes = ["pequeno", "medio", "grande", "metropole"];

    public required WorkCatalog Works { get; init; }

    public required ProcurementCatalog Procurement { get; init; }

    public required SecretaryCatalog Secretaries { get; init; }

    public required PersonnelCatalog Personnel { get; init; }

    public required AgreementCatalog Agreements { get; init; }

    public required InstrumentCatalog Instruments { get; init; }

    public required ChamberCatalog Chamber { get; init; }

    public required CollectionCatalog Collection { get; init; }

    public required TermTextCatalog Texts { get; init; }

    public required TermEventCatalog Events { get; init; }

    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, Executive.ExecutiveContent? executive, PromiseCatalog? promises, out GovernanceContent? content)
    {
        var works = JsonData.LoadFile<WorkCatalog>(Path.Combine(dataDirectory, WorkCatalog.File));
        var procurement = JsonData.LoadFile<ProcurementCatalog>(Path.Combine(dataDirectory, ProcurementCatalog.File));
        var secretaries = JsonData.LoadFile<SecretaryCatalog>(Path.Combine(dataDirectory, SecretaryCatalog.File));
        var personnel = JsonData.LoadFile<PersonnelCatalog>(Path.Combine(dataDirectory, PersonnelCatalog.File));
        var agreements = JsonData.LoadFile<AgreementCatalog>(Path.Combine(dataDirectory, AgreementCatalog.File));
        var instruments = JsonData.LoadFile<InstrumentCatalog>(Path.Combine(dataDirectory, InstrumentCatalog.File));
        var chamber = JsonData.LoadFile<ChamberCatalog>(Path.Combine(dataDirectory, ChamberCatalog.File));
        var collection = JsonData.LoadFile<CollectionCatalog>(Path.Combine(dataDirectory, CollectionCatalog.File));
        var texts = JsonData.LoadFile<TermTextCatalog>(Path.Combine(dataDirectory, TermTextCatalog.File));

        var workErrors = works.Errors.ToList();
        var personnelErrors = personnel.Errors.ToList();
        var collectionErrors = collection.Errors.ToList();
        if (works.Value is { } w && secretaries.Value is { } s)
        {
            workErrors.AddRange(w.Tipos.Where(t => s.Secretarias.All(x => x.Id != t.Secretaria)).Select(t => $"tipo '{t.Id}': secretaria '{t.Secretaria}' inexistente."));
        }

        if (works.Value is { } w2 && executive is not null)
        {
            var indicators = executive.Config.Indicators.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
            workErrors.AddRange(w2.Tipos.Where(t => t.Indicador is not null && !indicators.Contains(t.Indicador)).Select(t => $"tipo '{t.Id}': indicador '{t.Indicador}' inexistente."));
        }

        if (personnel.Value is { } p && executive is not null)
        {
            personnelErrors.AddRange(p.Mesas.Where(m => executive.Crises.Find(m.Crise) is null).Select(m => $"mesa '{m.Id}': crise '{m.Crise}' inexistente."));
        }

        if (works.Value is { } w3 && collection.Value is { } c)
        {
            var keys = w3.Tipos.SelectMany(t => t.Cumpre).ToHashSet(StringComparer.Ordinal);
            collectionErrors.AddRange(c.PromessaCumpre.SelectMany(x => x.Value.Where(k => !keys.Contains(k)).Select(k => $"promessa_cumpre.{x.Key}: nenhum tipo de obra cumpre '{k}'.")));
            if (promises is not null)
            {
                // Toda promessa da plataforma precisa de um tipo de obra ou serviço que a cumpra no mandato.
                collectionErrors.AddRange(promises.Promessas.Where(x => !keys.Contains(x.Obra)).Select(x => $"promessa '{x.Id}': nenhum tipo de obra cumpre '{x.Obra}'."));
            }
        }

        var reports = new List<DataFileReport>
        {
            new(WorkCatalog.File, works.Value?.Tipos.Count ?? 0, workErrors),
            new(ProcurementCatalog.File, procurement.Value?.Modalidades.Count ?? 0, procurement.Errors),
            new(SecretaryCatalog.File, secretaries.Value?.Secretarias.Count ?? 0, secretaries.Errors),
            new(PersonnelCatalog.File, personnel.Value?.Mesas.Count ?? 0, personnelErrors),
            new(AgreementCatalog.File, agreements.Value?.Origens.Count ?? 0, agreements.Errors),
            new(InstrumentCatalog.File, instruments.Value?.Instrumentos.Count ?? 0, instruments.Errors),
            new(ChamberCatalog.File, 1, chamber.Errors),
            new(CollectionCatalog.File, collection.Value?.PromessaCumpre.Count ?? 0, collectionErrors),
            new(TermTextCatalog.File, texts.Value?.Textos.Count ?? 0, texts.Errors),
        };

        var events = new List<TermEventDefinition>();
        string eventsDirectory = Path.Combine(dataDirectory, TermEventCatalog.Directory);
        if (!System.IO.Directory.Exists(eventsDirectory))
        {
            reports.Add(new DataFileReport($"{TermEventCatalog.Directory}/", 0, ["pasta não encontrada."]));
        }
        else
        {
            foreach (string path in System.IO.Directory.GetFiles(eventsDirectory, "*.json").Order(StringComparer.Ordinal))
            {
                var file = JsonData.LoadFile<TermEventFile>(path);
                reports.Add(new DataFileReport($"{TermEventCatalog.Directory}/{Path.GetFileName(path)}", file.Value?.Eventos.Count ?? 0, file.Errors));
                events.AddRange(file.Value?.Eventos ?? []);
            }

            var catalog = new TermEventCatalog { Eventos = events };
            var lint = DataChecks.UniqueIds(events.Select(e => e.Id), "Evento de mandato").Concat(GovernanceLint.Events(catalog)).ToList();
            reports.Add(new DataFileReport("lint: decisões de mandato com ganho e custo", events.Sum(e => e.Opcoes.Count), lint));
        }

        content = reports.All(r => r.IsValid)
            ? new GovernanceContent
            {
                Works = works.Value!,
                Procurement = procurement.Value!,
                Secretaries = secretaries.Value!,
                Personnel = personnel.Value!,
                Agreements = agreements.Value!,
                Instruments = instruments.Value!,
                Chamber = chamber.Value!,
                Collection = collection.Value!,
                Texts = texts.Value!,
                Events = new TermEventCatalog { Eventos = events },
            }
            : null;
        return reports;
    }

    public static GovernanceContent LoadOrThrow(string dataDirectory, Executive.ExecutiveContent? executive = null, PromiseCatalog? promises = null)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, executive, promises, out GovernanceContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }
}
