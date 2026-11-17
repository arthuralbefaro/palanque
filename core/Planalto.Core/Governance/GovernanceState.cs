using Planalto.Core.Campaign;

namespace Planalto.Core.Governance;

/// <summary>Papel do jogador no governo da cidade.</summary>
public enum GovRole
{
    Vereador,
    Prefeito,
}

public enum WorkStage
{
    /// <summary>Emenda destinada, esperando a prefeitura liberar.</summary>
    Liberacao,
    Projeto,

    /// <summary>Projeto pronto, esperando a escolha da modalidade (só quando o jogador é o prefeito).</summary>
    Modalidade,
    Licitacao,
    Execucao,

    /// <summary>Obra pronta, esperando a inauguração.</summary>
    Pronta,
    Entregue,
    Cancelada,
}

/// <summary>Obra ou serviço municipal numa zona (GDD 17.14 e 17.15).</summary>
public sealed class Work
{
    public const string DaPrefeitura = "prefeitura";
    public const string DeEmenda = "emenda";
    public const string DeIndicacao = "indicacao";
    public const string DeConvenio = "convenio";
    public const string DeAcordo = "acordo";

    /// <summary>Patrocinador quando a obra é do próprio governo.</summary>
    public const string Prefeito = "prefeito";

    public required string Id { get; init; }

    public required string TypeId { get; init; }

    public required string ZoneId { get; init; }

    public required string Origin { get; init; }

    /// <summary>Quem leva o crédito: o id do jogador, o de um vereador ou <see cref="Prefeito"/>.</summary>
    public required string SponsorId { get; init; }

    public required decimal Cost { get; set; }

    public required DateOnly Created { get; init; }

    public WorkStage Stage { get; set; }

    public DateOnly StageStart { get; set; }

    /// <summary>Fim previsto da etapa atual (o que a tela mostra na barra).</summary>
    public DateOnly? StageEnd { get; set; }

    public ProcurementMode? Mode { get; set; }

    public string? CompanyId { get; set; }

    /// <summary>Semanas de atraso acumuladas.</summary>
    public int DelayWeeks { get; set; }

    public bool Impugned { get; set; }

    /// <summary>Irregularidade escondida (sobrepreço, contratação sem base): só aparece com requerimento ou fiscalização.</summary>
    public bool Irregular { get; set; }

    /// <summary>Sobrepreço, em fração do custo (escondido).</summary>
    public double Overprice { get; set; }

    /// <summary>O jogador já conhece os dados escondidos da obra.</summary>
    public bool Revealed { get; set; }

    public bool Denounced { get; set; }

    public string? AgreementId { get; set; }

    /// <summary>Rendimento de quem patrocinou, fixado quando a obra foi pedida (cansaço, assessor).</summary>
    public double Yield { get; set; } = 1;

    public DateOnly? Delivered { get; set; }

    /// <summary>Houve inauguração com cerimônia.</summary>
    public bool Ceremony { get; set; }

    /// <summary>O jogador compareceu à inauguração.</summary>
    public bool PlayerAttended { get; set; }

    public bool Open => Stage is not (WorkStage.Entregue or WorkStage.Cancelada);
}

/// <summary>Empresa fictícia que disputa as licitações da cidade.</summary>
public sealed class Company
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>1 a 5: decide atraso e aditivo.</summary>
    public required int Quality { get; init; }

    /// <summary>Dono (id de pessoa), que lembra de denúncia e de acordo.</summary>
    public required string OwnerId { get; init; }
}

/// <summary>Titular (ou nome oferecido) de uma secretaria (GDD 17.15).</summary>
public sealed class Secretary
{
    public required string PersonId { get; init; }

    /// <summary>Pasta que ocupa; null enquanto é só um nome oferecido.</summary>
    public string? SecretariaId { get; set; }

    public string? PartyId { get; init; }

    /// <summary>1 a 5.</summary>
    public required int Competence { get; init; }

    /// <summary>0 a 100.</summary>
    public required int Loyalty { get; set; }

    /// <summary>1 a 5, oculta.</summary>
    public required int Integrity { get; init; }

    public required string Agenda { get; init; }

    /// <summary>Zona favorecida (agenda "zona").</summary>
    public string? AgendaZone { get; init; }

    /// <summary>Tipo de obra de estimação (agenda "projeto").</summary>
    public string? AgendaWork { get; init; }

    public DateOnly? Since { get; set; }

    /// <summary>Saiu ou foi demitido: não volta a ser oferecido.</summary>
    public bool Left { get; set; }
}

/// <summary>Assessor do gabinete do vereador, ou candidato a uma vaga (GDD 17.14).</summary>
public sealed class Aide
{
    public const string DaCampanha = "campanha";
    public const string DeLideranca = "lideranca";
    public const string DeVereador = "vereador";
    public const string Parente = "parente";
    public const string DeMercado = "mercado";

    public required string PersonId { get; init; }

    /// <summary>Quem a pessoa é para o jogador: campanha, indicação de liderança ou de vereador, parente, mercado.</summary>
    public required string Origin { get; init; }

    /// <summary>Quem indicou (liderança ou vereador), quando há.</summary>
    public string? NomineeOf { get; init; }

    /// <summary>1 a 5.</summary>
    public required int Quality { get; init; }

    public string? Role { get; set; }

    /// <summary>Zona do assessor de base.</summary>
    public string? ZoneId { get; set; }

    public bool Appointed { get; set; }

    public DateOnly? Since { get; set; }

    /// <summary>Última semana em que o gabinete fez algo na zona dele (assessor de base).</summary>
    public DateOnly? LastSupported { get; set; }

    public bool Neglected { get; set; }

    /// <summary>Ficou de fora quando o gabinete fechou (já guardou a lembrança).</summary>
    public bool PassedOver { get; set; }

    public bool Left { get; set; }
}

public sealed class InfoRequest
{
    public const string SobreObra = "obra";
    public const string SobreSecretaria = "secretaria";

    public required string Id { get; init; }

    public required string TargetKind { get; init; }

    public required string TargetId { get; init; }

    public required DateOnly Filed { get; init; }

    public required DateOnly Due { get; init; }

    public double Yield { get; init; } = 1;

    public bool Answered { get; set; }

    /// <summary>O que a resposta mostrou: "normal", "atraso" ou "irregular" (obra); "secretaria" nos demais.</summary>
    public string? Finding { get; set; }
}

public enum IndicationState
{
    Pendente,
    Atendida,
    Negada,
}

public sealed class Indication
{
    public required string Id { get; init; }

    public required string TypeId { get; init; }

    public required string ZoneId { get; init; }

    public required DateOnly Filed { get; init; }

    public required DateOnly DecideOn { get; init; }

    /// <summary>Chance de atendimento calculada no pedido (a mesma da previsão).</summary>
    public required double Chance { get; init; }

    public double Yield { get; init; } = 1;

    public IndicationState State { get; set; }

    public string? WorkId { get; set; }
}

public sealed class Inspection
{
    public const string Normal = "normal";
    public const string Atraso = "atraso";
    public const string Irregular = "irregular";

    public required string Id { get; init; }

    public required string WorkId { get; init; }

    public required DateOnly Date { get; init; }

    public required string Finding { get; init; }

    /// <summary>Prazo para decidir o que fazer com o achado; null quando não há o que decidir.</summary>
    public DateOnly? DecisionDeadline { get; set; }

    /// <summary>Id da opção escolhida (denunciar, negociar, tribunal).</summary>
    public string? Decision { get; set; }

    /// <summary>Quando sai o resultado do Tribunal de Contas.</summary>
    public DateOnly? TribunalDue { get; set; }

    /// <summary>Rendimento do vereador no dia da fiscalização (cansaço, assessor), que vale também para o achado.</summary>
    public double Yield { get; init; } = 1;

    public bool Closed { get; set; }

    public bool Pending => !Closed && Decision is null && DecisionDeadline is not null;
}

/// <summary>Acordo de voto com um vereador (GDD 17.14 e 17.15): o que ele pediu fica no Livro.</summary>
public sealed class VoteDeal
{
    public required string Id { get; init; }

    /// <summary>Projeto (mandato de vereador) ou política (prefeitura).</summary>
    public required string SubjectId { get; init; }

    /// <summary>O vereador do acordo. Muda quando ele deixa a cadeira e o suplente herda o acordo (GDD 17.16).</summary>
    public required string MemberId { get; set; }

    /// <summary>Com quem o acordo foi fechado, quando passou a um sucessor.</summary>
    public string? OriginalMemberId { get; set; }

    /// <summary>O vereador deixou a cadeira sem sucessor: o acordo e a promessa dele se extinguiram.</summary>
    public bool Extinct { get; set; }

    /// <summary>+1 para SIM, −1 para NÃO.</summary>
    public required int Side { get; init; }

    /// <summary>O que o acordo soma (com sinal) à tendência de voto dele.</summary>
    public required double Logit { get; init; }

    public required string RequestId { get; init; }

    public string? ZoneId { get; init; }

    /// <summary>Projeto dele que o jogador se comprometeu a apoiar.</summary>
    public string? SupportBillId { get; init; }

    public required string PromiseId { get; init; }

    public required DateOnly Date { get; init; }

    /// <summary>A votação já aconteceu.</summary>
    public bool Voted { get; set; }
}

/// <summary>Cobrança de quem lembra (GDD 17.14): promessa, dívida ou presença na base.</summary>
public sealed class Demand
{
    public const string Promessa = "promessa";
    public const string Divida = "divida";
    public const string Presenca = "presenca";

    public required string Id { get; init; }

    public required string Kind { get; init; }

    /// <summary>Id da promessa ou da dívida no Livro; a zona, na cobrança de presença.</summary>
    public required string RefId { get; init; }

    public string? PersonId { get; init; }

    public string? ZoneId { get; init; }

    public required DateOnly Date { get; init; }

    /// <summary>Prazo da cobrança de presença.</summary>
    public DateOnly? Deadline { get; init; }

    public required string Text { get; init; }

    /// <summary>Atendida (true), vencida (false) ou ainda aberta (null). Só a de presença se fecha por aqui.</summary>
    public bool? Met { get; set; }
}

public enum NegotiationOutcome
{
    Pendente,
    Acordo,
    Fracassou,
}

/// <summary>Uma rodada de negociação com uma categoria. A greve aponta para a rodada que falhou.</summary>
public sealed class Negotiation
{
    public required string Id { get; init; }

    public required string TableId { get; init; }

    public required DateOnly Date { get; init; }

    public required DateOnly Deadline { get; init; }

    /// <summary>Reajuste pedido (fração).</summary>
    public required double Asked { get; init; }

    /// <summary>Reajuste que vale como revisão geral anual neste ano.</summary>
    public required double GeneralRevision { get; init; }

    public string? Answer { get; set; }

    public double Granted { get; set; }

    public NegotiationOutcome Outcome { get; set; }

    public DateOnly? Closed { get; set; }

    /// <summary>Registro da greve que nasceu desta negociação.</summary>
    public string? StrikeRecordId { get; set; }
}

public enum AgreementState
{
    Oferta,
    Assinado,
    Cumprido,
    Devolvido,
    Recusado,
    Expirado,
}

public sealed class Agreement
{
    public required string Id { get; init; }

    public required string OriginId { get; init; }

    public required string TypeId { get; init; }

    /// <summary>Valor que o convênio paga.</summary>
    public required decimal Value { get; init; }

    /// <summary>Contrapartida do município: fração do custo da obra que o convênio não paga.</summary>
    public required double Counterpart { get; init; }

    public required DateOnly Offered { get; init; }

    public required DateOnly OfferExpires { get; init; }

    /// <summary>Semanas para entregar, contadas da assinatura.</summary>
    public required int Weeks { get; init; }

    public DateOnly? Deadline { get; set; }

    public AgreementState State { get; set; }

    public string? WorkId { get; set; }
}

/// <summary>Crise do governo com a causa que a produziu (GDD 17.15: nenhuma crise é só sorteio).</summary>
public sealed class CrisisRecord
{
    public const string CausaNegociacao = "negociacao_falhou";
    public const string CausaZonaSemDrenagem = "zona_sem_drenagem";
    public const string CausaSecretario = "secretario";
    public const string CausaLicitacao = "licitacao_irregular";
    public const string CausaIndicador = "indicador_baixo";
    public const string CausaReceita = "receita_em_queda";

    public required string Id { get; init; }

    public required string CrisisId { get; init; }

    public required DateOnly Date { get; init; }

    public string? ZoneId { get; init; }

    public required string CauseKind { get; init; }

    /// <summary>O fato do estado que causou a crise: id da negociação, da obra, da pessoa ou do indicador.</summary>
    public required string CauseRef { get; init; }

    /// <summary>A causa por extenso, como aparece no cartão da crise.</summary>
    public required string CauseText { get; init; }

    /// <summary>Tamanho do dano (1 = cheio); menor onde a causa foi atenuada (drenagem entregue).</summary>
    public double Severity { get; init; } = 1;

    /// <summary>A crise já chegou à mesa do prefeito (o choque foi aplicado e a notícia saiu).</summary>
    public bool Presented { get; set; }

    public int? Choice { get; set; }

    public bool Resolved { get; set; }
}

public enum CpiOutcome
{
    EmCurso,
    Arquivada,
    Indiciou,
}

public sealed class Cpi
{
    public required string Id { get; init; }

    public required string Subject { get; init; }

    public required DateOnly Opened { get; init; }

    public required DateOnly Ends { get; init; }

    public required int Signatures { get; init; }

    public required bool ByPlayer { get; init; }

    /// <summary>Obra investigada, quando há.</summary>
    public string? WorkId { get; init; }

    /// <summary>0 a 1.</summary>
    public double Evidence { get; set; }

    public CpiOutcome Outcome { get; set; }
}

public sealed record CassationVote(DateOnly Date, int Yes, int No, int Needed, bool Passed);

/// <summary>
/// Vaga aberta no governo por decisão da Câmara (cassação). É o fato que o sistema de sucessão e vacância lê:
/// <see cref="Handled"/> diz se alguém de fora já resolveu a substituição.
/// </summary>
public sealed class OfficeVacancy
{
    public const string Cassacao = "cassacao";

    public required string Id { get; init; }

    public required DateOnly Date { get; init; }

    public required Elections.Office Office { get; init; }

    /// <summary>Quem perdeu o cargo (id do personagem, ou o do jogador).</summary>
    public required string HolderId { get; init; }

    public required string Cause { get; init; }

    /// <summary>CPI que levou à cassação.</summary>
    public string? CpiId { get; init; }

    /// <summary>A sucessão foi resolvida pelo sistema de vacância (true) ou pelo padrão provisório: o vice assume (false).</summary>
    public bool Handled { get; set; }

    /// <summary>O que a regra de substituição determinou, quando a renovação do mundo resolveu a vaga.</summary>
    public Renewal.SuccessionOutcome? Outcome { get; set; }

    /// <summary>Quem assumiu (vice ou suplente), quando alguém assumiu.</summary>
    public string? SuccessorId { get; set; }

    /// <summary>Dispositivo que fundamenta o desfecho.</summary>
    public string? Rule { get; set; }

    /// <summary>Data da nova eleição, quando a regra manda fazer uma.</summary>
    public DateOnly? ElectionOn { get; set; }
}

public sealed class MesaState
{
    public string? PresidentId { get; set; }

    public DateOnly? Elected { get; set; }

    public required DateOnly NextElection { get; set; }

    /// <summary>O jogador é candidato na próxima eleição da Mesa.</summary>
    public bool PlayerRunning { get; set; }
}

public sealed class PayrollState
{
    /// <summary>Despesa com pessoal do Executivo sobre a receita corrente líquida.</summary>
    public double Share { get; set; }

    /// <summary>True quando o ponto de partida veio do RGF do município; false quando é estimativa por porte.</summary>
    public bool FromRgf { get; init; }

    public PayrollBand Band { get; set; }

    /// <summary>Quando a folha passou do limite (corre o prazo do art. 23).</summary>
    public DateOnly? ExcessSince { get; set; }

    /// <summary>Excesso (fração da RCL) no dia em que começou.</summary>
    public double ExcessAtStart { get; set; }

    public bool OneThirdChecked { get; set; }

    /// <summary>As consequências do art. 23, § 3º, estão valendo.</summary>
    public bool Restricted { get; set; }

    /// <summary>Reajuste pedido e não concedido, que volta no pedido do ano seguinte.</summary>
    public double Loss { get; set; }
}

public sealed class FiscalState
{
    /// <summary>Receita corrente líquida anual, em reais.</summary>
    public decimal Rcl { get; set; }

    /// <summary>True quando a receita é a declarada pelo município; false quando é estimativa.</summary>
    public bool Declared { get; init; }

    /// <summary>Impostos e transferências de impostos sobre a receita (a base dos mínimos e do teto da Câmara).</summary>
    public double TaxBaseShare { get; init; }

    public int Year { get; set; }

    /// <summary>Investimento em obra nova já comprometido no ano.</summary>
    public decimal InvestmentUsed { get; set; }

    /// <summary>Repasse à Câmara, em fração da base de impostos do ano anterior.</summary>
    public double ChamberShare { get; set; }

    public double GeneralRevision { get; set; }

    /// <summary>Crescimento nominal da receita no ano corrente (negativo = queda).</summary>
    public double Growth { get; set; }
}

/// <summary>Decisão pendente de um evento de mandato, já ligada à pessoa, à zona e à obra do fato que o disparou.</summary>
public sealed class TermDecision
{
    public required string Id { get; init; }

    public required string EventId { get; init; }

    public required DateOnly Date { get; init; }

    public required DateOnly Deadline { get; init; }

    public string? PersonId { get; init; }

    public string? ZoneId { get; init; }

    public string? WorkId { get; init; }

    public required string Title { get; init; }

    public required string Text { get; init; }
}

public enum VoteInstruction
{
    /// <summary>Sem instrução: quem não vota falta.</summary>
    Nenhuma,
    SeguirOrientacao,
    SeguirPlataforma,
}

/// <summary>
/// Governo da cidade em foco durante um mandato do jogador (GDD 17.14 e 17.15): obras, secretários, folha, convênios,
/// Câmara, gabinete e cobranças. Vale para vereador e para prefeito; quem não é o jogador é conduzido pelo motor.
/// </summary>
public sealed class GovernanceState
{
    public const int MaxFeed = 400;

    public required int Municipio { get; init; }

    public required GovRole Role { get; init; }

    public required DateOnly Start { get; init; }

    public required DateOnly End { get; init; }

    /// <summary>Ids das zonas, na ordem de data/zonas/ (a mesma dos vetores abaixo).</summary>
    public required List<string> Zones { get; init; }

    public required List<long> ZonePopulation { get; init; }

    /// <summary>Presença do jogador em cada zona (0 a 100), herdada da campanha.</summary>
    public required List<double> Presence { get; init; }

    /// <summary>Presença de cada zona na posse (a régua da cobrança da base).</summary>
    public required List<double> PresenceAtStart { get; init; }

    /// <summary>Zonas que formam a base do jogador (índices).</summary>
    public required List<int> BaseZones { get; init; }

    /// <summary>Aprovação do governo em cada zona, em pontos acima ou abaixo da aprovação da cidade.</summary>
    public required List<double> ZoneApproval { get; init; }

    /// <summary>Aprovação do prefeito NPC (0 a 100); com o jogador prefeito vale a do <see cref="Executive.ExecutiveState"/>.</summary>
    public double MayorApproval { get; set; }

    public bool MayorRemoved { get; set; }

    /// <summary>O jogador perdeu o mandato (cassação): o governo fica no estado só para a tela contar o que houve.</summary>
    public bool PlayerRemoved { get; set; }

    /// <summary>Bônus na chance de o projeto do jogador sair da comissão (assessor legislativo, presidência da Câmara).</summary>
    public double CommitteeBonus { get; set; }

    /// <summary>
    /// Vice-prefeito. Com a renovação do mundo ligada (GDD 17.16), é o vice eleito de <see cref="Renewal.RenewalState"/>
    /// (vazio se o cargo está vago) e a sucessão segue a regra de lá; sem ela, é um nome provisório do próprio governo.
    /// </summary>
    public required string ViceId { get; init; }

    /// <summary>Último fato da renovação do mundo já levado ao governo (id de <see cref="Renewal.RenewalEvent"/>).</summary>
    public int RenewalSeen { get; set; }

    /// <summary>Prefeito que o governo conhecia na última semana (mandato de vereador), para notar a troca.</summary>
    public string? MayorSeenId { get; set; }

    /// <summary>Relação com o governador (−100 a 100), para os convênios.</summary>
    public double GovernorRelation { get; set; }

    /// <summary>Reduto de cada vereador (id do membro → id da zona).</summary>
    public Dictionary<string, string> MemberZones { get; init; } = [];

    public List<Secretary> Secretaries { get; init; } = [];

    /// <summary>Secretários cuja competência o jogador já conhece (por requerimento).</summary>
    public List<string> KnownSecretaries { get; init; } = [];

    /// <summary>Pasta sem titular → desde quando.</summary>
    public Dictionary<string, DateOnly> VacantSince { get; init; } = [];

    public List<Work> Works { get; init; } = [];

    public List<Company> Companies { get; init; } = [];

    public List<Aide> Aides { get; init; } = [];

    public List<InfoRequest> Requests { get; init; } = [];

    public List<Indication> Indications { get; init; } = [];

    public List<Inspection> Inspections { get; init; } = [];

    public List<VoteDeal> Deals { get; init; } = [];

    public List<Demand> Demands { get; init; } = [];

    public List<Negotiation> Negotiations { get; init; } = [];

    public List<Agreement> Agreements { get; init; } = [];

    public DateOnly? AgreementsBlockedUntil { get; set; }

    public List<CrisisRecord> Crises { get; init; } = [];

    public Cpi? Cpi { get; set; }

    public List<Cpi> CpiHistory { get; init; } = [];

    public List<CassationVote> Cassations { get; init; } = [];

    /// <summary>Vagas abertas por cassação, para o sistema de sucessão e vacância.</summary>
    public List<OfficeVacancy> Vacancies { get; init; } = [];

    public required MesaState Mesa { get; init; }

    public required PayrollState Payroll { get; init; }

    public required FiscalState Fiscal { get; init; }

    public VoteInstruction Instruction { get; set; }

    public int TurnsLeft { get; set; }

    /// <summary>Cansaço por instrumento ("requerimento") e por instrumento e zona ("requerimento@zona").</summary>
    public Dictionary<string, double> Fatigue { get; init; } = [];

    /// <summary>Zonas em que o assessor de base trouxe demanda (bônus para a próxima indicação ali).</summary>
    public List<string> ZoneLeads { get; init; } = [];

    public TermDecision? Decision { get; set; }

    /// <summary>Evento → data a partir da qual pode voltar.</summary>
    public Dictionary<string, DateOnly> EventCooldown { get; init; } = [];

    /// <summary>Vereador → data a partir da qual volta a negociar voto (depois de enganado).</summary>
    public Dictionary<string, DateOnly> NoDealUntil { get; init; } = [];

    /// <summary>Multiplicador da velocidade de cada indicador, pela competência do secretário da pasta.</summary>
    public Dictionary<string, double> AreaSpeed { get; init; } = [];

    /// <summary>Empresas ligadas a doadores da campanha do prefeito (preenchido pelo sistema de doadores).</summary>
    public List<string> DonorCompanies { get; init; } = [];

    public List<FeedItem> Feed { get; init; } = [];

    /// <summary>Promessas que já chegaram abertas a este mandato (para medir a cobrança).</summary>
    public List<string> PromisesAtStart { get; init; } = [];

    public int NextId { get; set; } = 1;

    public string NewId(string prefix) => $"{prefix}{NextId++}";

    public int IndexOf(string zoneId) => Zones.IndexOf(zoneId);

    public Work? Work(string id) => Works.FirstOrDefault(w => w.Id == id);

    public Company? Company(string? id) => Companies.FirstOrDefault(c => c.Id == id);

    public Secretary? SecretaryOf(string secretariaId) => Secretaries.FirstOrDefault(s => s.SecretariaId == secretariaId && !s.Left);

    public IEnumerable<Aide> Cabinet => Aides.Where(a => a.Appointed && !a.Left);

    public Aide? AideIn(string role) => Cabinet.FirstOrDefault(a => a.Role == role);

    /// <summary>Presença na cidade: média das zonas pesada pela população.</summary>
    public double CityPresence()
    {
        double pop = ZonePopulation.Sum(p => (double)p);
        return pop <= 0 ? (Presence.Count == 0 ? 0 : Presence.Average()) : Presence.Select((v, i) => v * ZonePopulation[i]).Sum() / pop;
    }

    /// <summary>O que um acordo de voto soma (com sinal) à tendência de um vereador neste projeto ou política.</summary>
    public double DealLogit(string subjectId, string memberId) =>
        Deals.FirstOrDefault(d => d.SubjectId == subjectId && d.MemberId == memberId && !d.Voted)?.Logit ?? 0;

    /// <summary>A crise que estava na mesa do prefeito foi respondida (chamado pelo motor da prefeitura).</summary>
    public void CrisisResolved(string crisisId, int choice)
    {
        if (Crises.FirstOrDefault(c => c.Presented && !c.Resolved && c.CrisisId == crisisId) is { } record)
        {
            record.Resolved = true;
            record.Choice = choice;
        }
    }
}
