using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Parties;

namespace Planalto.Core.Campaign;

/// <summary>Perfil de campanha de um rival (GDD 17.11): um estilo, sorteado sem relação com ideologia ou partido.</summary>
public sealed class RivalProfile
{
    public const string Base = "base";
    public const string Midia = "midia";
    public const string Maquina = "maquina";
    public const string Causa = "causa";
    public const string Estrutura = "estrutura";

    public static readonly IReadOnlyList<string> Required = [Base, Midia, Maquina, Causa, Estrutura];

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    public required double Weight { get; init; }

    /// <summary>Só para quem está no cargo ou é do partido da prefeitura (campanha de máquina).</summary>
    public bool ExigeSituacao { get; init; }

    /// <summary>Zonas-alvo do plano (2 ou 3).</summary>
    public required int ZonasAlvo { get; init; }

    /// <summary>Quantas vezes uma zona-alvo pesa mais que outra de mesma população na escolha de onde ir.</summary>
    public required double Foco { get; init; }

    /// <summary>Mistura de compromissos públicos (tipo → peso).</summary>
    public required Dictionary<string, double> Compromissos { get; init; }

    /// <summary>Mistura de canais de verba (canal → fração).</summary>
    public required Dictionary<string, double> Canais { get; init; }

    public required double VerbaFator { get; init; }

    /// <summary>Multiplica a chance semanal de procurar uma liderança.</summary>
    public required double LiderancaFator { get; init; }

    /// <summary>0 a 1: propensão a atacar quem está logo à frente.</summary>
    public required double Agressividade { get; init; }

    /// <summary>Os atos públicos tratam do tema de interesse do rival (campanha de causa).</summary>
    public bool UsaTema { get; init; }
}

public sealed class RivalRules
{
    /// <summary>Compromissos públicos por semana, antes do empenho de cada rival.</summary>
    public required IntRange CompromissosPorSemana { get; init; }

    /// <summary>Presença que o jogador ganha no reduto de um rival para ele trocar de plano (GDD 17.11).</summary>
    public required double GatilhoPresencaReduto { get; init; }

    /// <summary>Queda, em pontos, entre duas pesquisas públicas que faz o rival trocar de plano.</summary>
    public required double GatilhoQuedaPesquisa { get; init; }

    /// <summary>O líder da disputa ignora um ataque quando tem este múltiplo dos votos de quem atacou.</summary>
    public required double MuitoAFrente { get; init; }

    public required IntRange RevideDias { get; init; }

    public required IntRange VitimaDias { get; init; }

    public required IntRange JusticaDias { get; init; }

    /// <summary>Imagem que ganha quem se faz de vítima.</summary>
    public required double VitimaImagem { get; init; }

    /// <summary>Chance semanal de um rival de agressividade 1 atacar quem está logo à frente.</summary>
    public required double ChanceAtaqueSemanal { get; init; }

    /// <summary>Chance semanal de um rival calculista ou de estrutura descobrir um esqueleto do jogador.</summary>
    public required double ChanceInvestigar { get; init; }

    /// <summary>Caixa de partida e arrecadação semanal de um rival, no porte médio, antes do empenho e do perfil.</summary>
    public required decimal CaixaInicial { get; init; }

    public required decimal ArrecadacaoSemanal { get; init; }

    /// <summary>Meta de votos = esta folga × o maior entre os votos de hoje e os do último eleito da chapa.</summary>
    public required double MetaFolga { get; init; }

    public required IntRange FraquezasPorRival { get; init; }

    public required decimal InvestigarCusto { get; init; }

    public required double InvestigarChance { get; init; }

    /// <summary>Chance de o rival ficar sabendo de uma investigação que não achou nada.</summary>
    public required double InvestigarDescoberto { get; init; }

    public required double PactoChanceBase { get; init; }

    public required double PactoChancePorRelacao { get; init; }

    public required double PactoMesmoPartido { get; init; }

    public required double PactoCombativo { get; init; }

    public required int PactoSemanasEntrePropostas { get; init; }

    /// <summary>Presença semanal, na cidade inteira, que a dobradinha rende por ponto de reconhecimento do candidato a prefeito.</summary>
    public required double DobradinhaPresenca { get; init; }

    /// <summary>Fração da diferença de imagem que o jogador herda do parceiro a cada semana.</summary>
    public required double DobradinhaImagem { get; init; }

    /// <summary>Confiança mínima do coordenador para a ficha mostrar as zonas-alvo; e para mostrar a postura.</summary>
    public required double ConfiancaZonas { get; init; }

    public required double ConfiancaPostura { get; init; }

    /// <summary>Dias mínimos entre dois ataques de um candidato ao mesmo alvo.</summary>
    public required int AtaqueIntervaloDias { get; init; }

    /// <summary>Quanto rende a mais um ato do rival na zona em que ele concentra a campanha (a de oportunidade ou a que defende).</summary>
    public required double FatorConcentracao { get; init; }
}

/// <summary>data/campaign/rival_profiles.json.</summary>
public sealed class RivalCatalog : IValidatable
{
    public const string File = "campaign/rival_profiles.json";

    public required string Note { get; init; }

    public required RivalRules Regras { get; init; }

    public required List<RivalProfile> Perfis { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public required Dictionary<string, MemoryDefinition> Lembrancas { get; init; }

    /// <summary>Ganho e custo de cada proposta de pacto, por natureza (princípio e).</summary>
    public required Dictionary<string, DecisionTrade> Pactos { get; init; }

    /// <summary>Pontos fracos possíveis de um rival (tipo → textos).</summary>
    public required Dictionary<string, List<string>> Fraquezas { get; init; }

    public RivalProfile Profile(string id) => Perfis.First(p => p.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public MemoryDefinition Memory(string key) => Lembrancas[key];

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "fato_caminhada", "fato_reuniao", "fato_comicio", "fato_entrevista", "fato_gravacao", "fato_vitima", "fato_justica_aceita", "fato_justica_negada",
        "pacto_nao_agressao", "pacto_dobradinha", "pacto_recusado", "pacto_quebrado", "pacto_explica", "pacto_divida", "investigar_explica", "investigar_achou",
        "investigar_nada", "investigar_descoberto", "plano_expandir", "plano_defender", "plano_atacar", "plano_recompor", "chapa", "resposta_revide",
        "resposta_vitima", "resposta_justica", "resposta_ignora", "dobradinha_semana", "fato_queda",
    ];

    public static readonly IReadOnlyList<string> MemoryKeys = ["ataque", "pacto", "pacto_quebrado", "investigado", "proposta_recusada", "dobradinha"];

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Perfis.Select(p => p.Id), "Perfil"))
        {
            yield return error;
        }

        foreach (string id in RivalProfile.Required.Where(id => Perfis.All(p => p.Id != id)))
        {
            yield return $"falta o perfil '{id}'.";
        }

        foreach (RivalProfile p in Perfis)
        {
            if (p.Weight <= 0 || p.ZonasAlvo is < 2 or > 3 || p.Foco < 1 || p.Compromissos.Count == 0 || p.Compromissos.Values.Any(v => v < 0) || p.Compromissos.Values.Sum() <= 0
                || p.Canais.Values.Any(v => v < 0) || Math.Abs(p.Canais.Values.Sum() - 1) > 1e-6 || p.VerbaFator < 0 || p.LiderancaFator < 0 || p.Agressividade is < 0 or > 1)
            {
                yield return $"perfil '{p.Id}': peso positivo, 2 ou 3 zonas-alvo, mistura de compromissos e canais somando 1.";
            }

            foreach (string channel in new[] { ChannelDefinition.Rua, ChannelDefinition.Redes, ChannelDefinition.Material, ChannelDefinition.Cabos }.Where(c => !p.Canais.ContainsKey(c)))
            {
                yield return $"perfil '{p.Id}': falta o canal '{channel}'.";
            }

            if (p.Nome.Length > 28)
            {
                yield return $"perfil '{p.Id}': nome com mais de 28 caracteres.";
            }
        }

        if (Perfis.Where(p => !p.ExigeSituacao).Sum(p => p.Weight) <= 0)
        {
            yield return "perfis: pelo menos um perfil sem exigência de situação.";
        }

        RivalRules r = Regras;
        if (r.CompromissosPorSemana.Min < 1 || r.CompromissosPorSemana.Max < r.CompromissosPorSemana.Min || r.GatilhoPresencaReduto <= 0 || r.MuitoAFrente <= 1
            || r.RevideDias.Min < 3 || r.RevideDias.Max > 7 || r.VitimaDias.Min < 1 || r.VitimaDias.Max > 14 || r.JusticaDias.Min < 1
            || r.ChanceAtaqueSemanal is < 0 or > 1 || r.ChanceInvestigar is < 0 or > 1 || r.MetaFolga < 1 || r.FraquezasPorRival.Min < 0 || r.FraquezasPorRival.Max < r.FraquezasPorRival.Min
            || r.InvestigarChance is < 0 or > 1 || r.InvestigarCusto < 0 || r.PactoSemanasEntrePropostas < 1)
        {
            yield return "regras: valores fora do intervalo (o revide é de 3 a 7 dias).";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }

        foreach (string key in MemoryKeys.Where(k => !Lembrancas.ContainsKey(k)))
        {
            yield return $"lembrancas: falta '{key}'.";
        }

        foreach (string kind in new[] { "nao_agressao", "dobradinha" })
        {
            if (!Pactos.TryGetValue(kind, out DecisionTrade? trade))
            {
                yield return $"pactos: falta '{kind}'.";
                continue;
            }

            foreach (string error in DecisionNature.Check(trade.Ganho, trade.Custo, $"pacto '{kind}'"))
            {
                yield return error;
            }
        }

        foreach (string type in new[] { AttackType.Incoerencia, AttackType.Esqueleto }.Where(t => !Fraquezas.TryGetValue(t, out var list) || list.Count == 0))
        {
            yield return $"fraquezas: falta '{type}'.";
        }
    }
}

/// <summary>Tipo de ataque (GDD 17.11): por incoerência, por esqueleto ou por tema.</summary>
public sealed class AttackType
{
    public const string Incoerencia = "incoerencia";
    public const string Esqueleto = "esqueleto";
    public const string Tema = "tema";

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    /// <summary>Imagem que o alvo perde (0 a 1), antes do canal, da prova e da sorte.</summary>
    public required double Dano { get; init; }

    /// <summary>O ataque afirma um fato: com prova rende mais; sem prova, há risco de processo.</summary>
    public required bool UsaProva { get; init; }

    public double SemProvaFator { get; init; } = 1;

    /// <summary>Chance de processo quando o ataque afirma um fato sem prova.</summary>
    public double RiscoProcesso { get; init; }

    /// <summary>Texto do fato, com {ator}, {alvo}, {tema} e {assunto}.</summary>
    public required string Fato { get; init; }
}

public sealed class AttackChannel
{
    public const string Redes = "redes";
    public const string Imprensa = "imprensa";
    public const string Palanque = "palanque";

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required double Fator { get; init; }

    /// <summary>Custo no porte médio.</summary>
    public required decimal Custo { get; init; }

    /// <summary>O ataque sai num veículo, por pauta ao jornalista.</summary>
    public bool ExigeVeiculo { get; init; }

    /// <summary>Só na campanha oficial (palanque).</summary>
    public bool SoCampanha { get; init; }

    public required int Visibilidade { get; init; }
}

/// <summary>Resposta possível a um ataque (GDD 17.13): decisão com prazo de 48 horas.</summary>
public sealed class AttackResponse
{
    public const string MesmoVeiculo = "mesmo_veiculo";
    public const string OutroCanal = "outro_canal";
    public const string DireitoDeResposta = "direito_resposta";
    public const string ContraAtaque = "contra_ataque";
    public const string Silencio = "silencio";

    public static readonly IReadOnlyList<string> Required = [MesmoVeiculo, OutroCanal, DireitoDeResposta, ContraAtaque, Silencio];

    public required string Id { get; init; }

    /// <summary>Rótulo da escolha (até 40 caracteres).</summary>
    public required string Nome { get; init; }

    /// <summary>Rótulo quando o ataque é um boato (desmentir, processar, ignorar).</summary>
    public string? NomeBoato { get; init; }

    public required string Descricao { get; init; }

    /// <summary>Teste de atributo; null quando a resposta não depende do candidato.</summary>
    public AttributeId? Teste { get; init; }

    public double Bonus { get; init; }

    /// <summary>A chance é a do advogado da equipe (direito de resposta).</summary>
    public bool Advogado { get; init; }

    /// <summary>Dias até o resultado sair (direito de resposta: 2 a 4).</summary>
    public IntRange? Dias { get; init; }

    public decimal Custo { get; init; }

    /// <summary>Fração do dano que a resposta acrescenta na hora, por dar sobrevida ao assunto.</summary>
    public double Sobrevida { get; init; }

    public required DecisionTrade Troca { get; init; }
}

public sealed class AttackRules
{
    public required DoubleRange Sorte { get; init; }

    /// <summary>Prazo para o jogador responder a um ataque, em horas (GDD 17.13).</summary>
    public required int PrazoRespostaHoras { get; init; }

    /// <summary>Semanas até um ataque bem respondido se apagar, e um ataque sem resposta.</summary>
    public required int SemanasRespondido { get; init; }

    public required int SemanasSemResposta { get; init; }

    /// <summary>Fração do dano que sobra depois de uma boa resposta.</summary>
    public required double RespondidoFator { get; init; }

    /// <summary>Imagem que quem ataca perde entre quem rejeita campanha negativa.</summary>
    public required double Rejeicao { get; init; }

    /// <summary>Multa de um processo perdido, no porte médio, e a imagem que se perde com ele.</summary>
    public required decimal MultaProcesso { get; init; }

    public required double ProcessoImagem { get; init; }

    /// <summary>Dano por ponto de gravidade da prova (1 a 3): fator = base + passo × gravidade.</summary>
    public required double ProvaBase { get; init; }

    public required double ProvaPorGravidade { get; init; }

    /// <summary>Chance de um boato de rede ser falso, e o dano dele em relação ao ataque comum.</summary>
    public required double BoatoChanceFalso { get; init; }

    public required double BoatoFator { get; init; }

    /// <summary>Chance a mais de desmentir um boato que é falso.</summary>
    public required double BoatoFalsoBonus { get; init; }

    /// <summary>Chance de um ataque sem material de um rival sair como boato de rede.</summary>
    public required double ChanceBoato { get; init; }

    /// <summary>Imagem que o autor perde quando o alvo ganha o direito de resposta.</summary>
    public required double DireitoDeRespostaImagem { get; init; }
}

/// <summary>data/campaign/attacks.json.</summary>
public sealed class AttackCatalog : IValidatable
{
    public const string File = "campaign/attacks.json";

    public required string Note { get; init; }

    public required AttackRules Regras { get; init; }

    public required List<AttackType> Tipos { get; init; }

    public required List<AttackChannel> Canais { get; init; }

    public required List<AttackResponse> Respostas { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public AttackType? Type(string id) => Tipos.FirstOrDefault(t => t.Id == id);

    public AttackChannel? Channel(string id) => Canais.FirstOrDefault(c => c.Id == id);

    public AttackResponse? Response(string id) => Respostas.FirstOrDefault(r => r.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "explica_dano", "explica_rejeicao", "explica_custo", "explica_sem_prova", "explica_com_prova", "explica_pacto", "processo", "processo_resultado",
        "resposta_explica", "resposta_prazo", "resposta_boa", "resposta_ruim", "resposta_pedida", "resposta_silencio", "resposta_contra", "direito_concedido",
        "direito_negado", "boato", "assunto_incoerencia", "assunto_esqueleto", "assunto_tema", "prazo_vencido",
    ];

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Tipos.Select(t => t.Id), "Tipo de ataque")
            .Concat(DataChecks.UniqueIds(Canais.Select(c => c.Id), "Canal de ataque"))
            .Concat(DataChecks.UniqueIds(Respostas.Select(r => r.Id), "Resposta")))
        {
            yield return error;
        }

        foreach (string id in new[] { AttackType.Incoerencia, AttackType.Esqueleto, AttackType.Tema }.Where(id => Type(id) is null))
        {
            yield return $"falta o tipo de ataque '{id}'.";
        }

        foreach (string id in new[] { AttackChannel.Redes, AttackChannel.Imprensa, AttackChannel.Palanque }.Where(id => Channel(id) is null))
        {
            yield return $"falta o canal de ataque '{id}'.";
        }

        foreach (string id in AttackResponse.Required.Where(id => Response(id) is null))
        {
            yield return $"falta a resposta '{id}'.";
        }

        foreach (AttackType t in Tipos.Where(t => t.Dano is <= 0 or > 1 || t.SemProvaFator is <= 0 or > 1 || t.RiscoProcesso is < 0 or > 1 || (!t.UsaProva && t.RiscoProcesso > 0)))
        {
            yield return $"ataque '{t.Id}': dano em (0, 1], fator sem prova em (0, 1] e risco de processo só para quem afirma um fato.";
        }

        foreach (AttackChannel c in Canais.Where(c => c.Fator <= 0 || c.Custo < 0 || c.Visibilidade is < 1 or > 3 || c.Nome.Length > 28))
        {
            yield return $"canal de ataque '{c.Id}': fator positivo, custo não negativo, visibilidade de 1 a 3 e nome até 28 caracteres.";
        }

        foreach (AttackResponse r in Respostas)
        {
            if (r.Nome.Length > 40 || (r.NomeBoato?.Length ?? 0) > 40)
            {
                yield return $"resposta '{r.Id}': rótulo com mais de 40 caracteres.";
            }

            foreach (string error in DecisionNature.Check(r.Troca.Ganho, r.Troca.Custo, $"resposta '{r.Id}'"))
            {
                yield return error;
            }

            if (r.Custo < 0 || r.Sobrevida < 0 || (r.Dias is { } d && (d.Min < 1 || d.Max < d.Min)))
            {
                yield return $"resposta '{r.Id}': custo, sobrevida e dias não negativos.";
            }
        }

        if (Response(AttackResponse.DireitoDeResposta) is { } direito && (direito.Dias is not { Min: >= 2, Max: <= 4 } || !direito.Advogado))
        {
            yield return "resposta 'direito_resposta': pelo advogado, em 2 a 4 dias (GDD 17.13).";
        }

        AttackRules x = Regras;
        if (x.PrazoRespostaHoras != 48 || x.SemanasRespondido != 4 || x.SemanasSemResposta != 26)
        {
            yield return "regras: prazo de 48 horas, 4 semanas para o ataque respondido e 26 para o sem resposta (GDD 17.13).";
        }

        if (x.Sorte.Min <= 0 || x.Sorte.Max < x.Sorte.Min || x.RespondidoFator is < 0 or > 1 || x.Rejeicao < 0 || x.MultaProcesso < 0 || x.ProcessoImagem < 0
            || x.BoatoChanceFalso is < 0 or > 1 || x.BoatoFator <= 0 || x.ChanceBoato is < 0 or > 1 || x.ProvaBase <= 0)
        {
            yield return "regras: valores fora do intervalo.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

/// <summary>Postura de um rival diante de um concorrente (GDD 17.11).</summary>
public enum Posture
{
    Ignorar,
    DisputarZona,
    Atacar,
    Compor,
}

/// <summary>Ato da semana de um rival: o quê, em que dia e em que zona.</summary>
public sealed class RivalAction
{
    public const string Lideranca = "lideranca";
    public const string Ataque = "ataque";
    public const string Investigar = "investigar";

    /// <summary>0 = segunda … 6 = domingo.</summary>
    public required int Day { get; init; }

    /// <summary>Tipo de compromisso público, ou procurar liderança, atacar, investigar.</summary>
    public required string Kind { get; init; }

    /// <summary>Índice da zona; −1 quando o ato não tem zona.</summary>
    public int Zone { get; init; } = -1;

    public string? TargetId { get; init; }

    /// <summary>Quantas "saídas" este ato vale (o empenho e o porte concentram mais estrutura em cada ato).</summary>
    public double Strength { get; init; } = 1;

    public bool Done { get; set; }
}

/// <summary>Plano de um rival, refeito toda segunda (GDD 17.11).</summary>
public sealed class RivalPlan
{
    public const string Rotina = "rotina";
    public const string GatilhoPesquisa = "pesquisa";
    public const string GatilhoReduto = "reduto";
    public const string GatilhoAtaque = "ataque";
    public const string GatilhoLideranca = "lideranca";

    public const string Expandir = "expandir";
    public const string Defender = "defender";
    public const string AtacarEstrategia = "atacar";
    public const string Recompor = "recompor";

    public required DateOnly Week { get; set; }

    /// <summary>O que fez o plano mudar pela última vez.</summary>
    public string Trigger { get; set; } = Rotina;

    /// <summary>expandir, defender, atacar ou recompor.</summary>
    public string Strategy { get; set; } = Expandir;

    /// <summary>Quando a estratégia mudou pela última vez.</summary>
    public required DateOnly ChangedOn { get; set; }

    public double VoteGoal { get; set; }

    public List<int> TargetZones { get; set; } = [];

    /// <summary>Zona que o plano defende (o reduto em que o jogador entrou), ou −1.</summary>
    public int DefendZone { get; set; } = -1;

    /// <summary>Zona fora do reduto em que ele está mais perto de passar quem lidera: é onde concentra a campanha; −1 se não há.</summary>
    public int OpportunityZone { get; set; } = -1;

    public Dictionary<string, Posture> Postures { get; set; } = [];

    /// <summary>Tema dos atos públicos (campanha de causa).</summary>
    public string? Theme { get; set; }

    public List<RivalAction> Actions { get; set; } = [];

    /// <summary>Presença do jogador em cada reduto quando a estratégia foi definida (zona → pontos).</summary>
    public Dictionary<int, double> PlayerPresenceBase { get; set; } = [];

    /// <summary>O jogador já estava à frente no reduto quando a estratégia foi definida.</summary>
    public List<int> PlayerAheadAtBase { get; set; } = [];
}

/// <summary>Ponto fraco de um rival, que a investigação pode revelar e que serve de prova num ataque.</summary>
public sealed class RivalWeakness
{
    public required string Id { get; init; }

    /// <summary>"incoerencia" ou "esqueleto".</summary>
    public required string Type { get; init; }

    /// <summary>1 a 3.</summary>
    public required int Severity { get; init; }

    public required string Text { get; init; }

    public bool Known { get; set; }
}

/// <summary>Estado de campanha de um rival nomeado: perfil, plano, caixa e verba por canal e alvo.</summary>
public sealed class RivalRecord
{
    public required string CandidateId { get; init; }

    public required string ProfileId { get; init; }

    public decimal Money { get; set; }

    public decimal Spent { get; set; }

    public RivalPlan? Plan { get; set; }

    /// <summary>Verba semanal por canal e alvo (substitui a estimativa sem estado da R2b).</summary>
    public List<ChannelAllocation> Budget { get; set; } = [];

    public List<RivalWeakness> Weaknesses { get; init; } = [];

    /// <summary>Gatilho que aconteceu durante a semana e vale na próxima segunda.</summary>
    public string? PendingTrigger { get; set; }

    /// <summary>Intenção na última pesquisa pública.</summary>
    public double? LastPoll { get; set; }

    /// <summary>Votos projetados quando o plano da semana foi feito (na disputa de outro cargo, o reconhecimento).</summary>
    public double Votes { get; set; }
}

public enum PactKind
{
    NaoAgressao,
    Dobradinha,
}

public sealed class Pact
{
    public required string Id { get; init; }

    public required string RivalId { get; init; }

    public required PactKind Kind { get; init; }

    public required DateOnly Since { get; init; }

    /// <summary>Zonas que o jogador se comprometeu a não disputar (os redutos do rival).</summary>
    public List<string> ClosedZones { get; init; } = [];

    public bool Broken { get; set; }
}

/// <summary>Ataque em curso: o dano de imagem se apaga em 4 semanas se bem respondido, em 26 se não.</summary>
public sealed class Attack
{
    public required string Id { get; init; }

    public required string AttackerId { get; init; }

    public required string TargetId { get; init; }

    public required string Type { get; init; }

    public required string Channel { get; init; }

    public string? OutletId { get; init; }

    public required DateOnly Date { get; init; }

    /// <summary>Dia a partir do qual o dano de hoje se apaga em linha reta até <see cref="Ends"/>.</summary>
    public required DateOnly FadeStart { get; set; }

    public bool Proof { get; init; }

    public string? Theme { get; init; }

    public string? SkeletonId { get; init; }

    /// <summary>Imagem que o alvo perde hoje, antes de o ataque se apagar.</summary>
    public double Damage { get; set; }

    public DateOnly Ends { get; set; }

    /// <summary>Até quando o jogador pode responder (só nos ataques contra ele).</summary>
    public DateOnly? Deadline { get; set; }

    /// <summary>Resposta dada (id), ou null enquanto o prazo corre.</summary>
    public string? Response { get; set; }

    public bool WellAnswered { get; set; }

    /// <summary>Direito de resposta pedido: o dia em que o resultado sai.</summary>
    public DateOnly? RulingOn { get; set; }

    public bool Rumor { get; init; }

    public bool False { get; init; }

    public required string FactId { get; init; }

    public required string Text { get; init; }
}

/// <summary>Reação marcada de um rival a um ataque: revide, fazer-se de vítima ou acionar a Justiça Eleitoral.</summary>
public sealed class RivalReaction
{
    public const string Revide = "revide";
    public const string Vitima = "vitima";
    public const string Justica = "justica";

    public required string RivalId { get; init; }

    public required string Kind { get; init; }

    public required DateOnly Due { get; init; }

    public required string AttackId { get; init; }
}

/// <summary>Rivais na campanha (GDD 17.11): registros, pactos, ataques e reações marcadas.</summary>
public sealed class RivalsState
{
    public List<RivalRecord> Records { get; init; } = [];

    public List<Pact> Pacts { get; init; } = [];

    public List<Attack> Attacks { get; init; } = [];

    public List<RivalReaction> Reactions { get; init; } = [];

    /// <summary>Última proposta de pacto a cada rival.</summary>
    public Dictionary<string, DateOnly> LastProposal { get; init; } = [];

    public int NextId { get; set; } = 1;

    /// <summary>Votos projetados do jogador quando os planos da semana foram feitos.</summary>
    public double PlayerVotes { get; set; }

    public RivalRecord? Record(string candidateId) => Records.FirstOrDefault(r => r.CandidateId == candidateId);

    public Pact? PactWith(string rivalId) => Pacts.FirstOrDefault(p => p.RivalId == rivalId && !p.Broken);
}

/// <summary>O que se pede num ataque do jogador a um rival.</summary>
public sealed record AttackRequest(string RivalId, string Type, string Channel, string? WeaknessId = null, string? Theme = null, string? OutletId = null);

/// <summary>Resultado de um ataque do jogador, já explicado.</summary>
public sealed record AttackResult(Attack Attack, double RivalImage, double Image, decimal Money, bool Sued, string? ExpectedReaction, string? PactBroken, IReadOnlyList<string> Explanation);

/// <summary>Resultado da resposta a um ataque.</summary>
public sealed record ResponseResult(Attack Attack, string Response, bool? Passed, double DamageBefore, double DamageAfter, int WeeksLeft, decimal Money, string Text);

public sealed record PactResult(bool Accepted, Pact? Pact, People.Memory Memory, string Text);

public sealed record InvestigationResult(RivalWeakness? Found, bool Discovered, decimal Money, string Text);

/// <summary>Painel da chapa (GDD 17.11): "seu partido hoje faria N cadeiras; você é o Kº da chapa".</summary>
public sealed record SlateRow(string CandidateId, string Name, long Votes, bool Seated, bool IsPlayer);

public sealed record SlateView(string PartyId, int Seats, int Rank, int ListSize, long Votes, long VotesToSeat, string Text, IReadOnlyList<SlateRow> Top);

/// <summary>Ficha do rival para a tela: reduto, plano estimado com a confiança do coordenador, lembranças.</summary>
public sealed record RivalSheet(
    string CandidateId,
    string Name,
    string? ProfileId,
    string? ProfileName,
    People.Temperament Temperament,
    double Relation,
    IReadOnlyList<string> Redutos,
    double Confidence,
    IReadOnlyList<string> EstimatedZones,
    Posture? EstimatedPosture,
    string? EstimatedStrategy,
    string? StrategyText,
    IReadOnlyList<People.Memory> Memories,
    IReadOnlyList<RivalWeakness> KnownWeaknesses,
    Pact? Pact,
    IReadOnlyList<RivalAction> Tomorrow);

/// <summary>Ataque contra o jogador à espera de resposta, com o prazo e as respostas possíveis.</summary>
public sealed record PendingAttackView(Attack Attack, string AttackerName, string? OutletLabel, DateOnly Deadline, int HoursLeft, double DamageNow, IReadOnlyList<(AttackResponse Response, string Label, CommandFailure? Blocked)> Options);
