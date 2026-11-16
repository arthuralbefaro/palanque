using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.State;

namespace Planalto.Core.Campaign;

/// <summary>Regras gerais da agenda (GDD 17.6), em data/campaign/commitments.json.</summary>
public sealed class AgendaRules
{
    /// <summary>Turnos por dia sem custo de estresse; o seguinte soma <see cref="EstresseTurnoExtra"/>.</summary>
    public required int TurnosSemCusto { get; init; }

    public required int EstresseTurnoExtra { get; init; }

    /// <summary>Rendimento = 1 ÷ (1 + fator × cansaço) (princípio g).</summary>
    public required double CansacoFator { get; init; }

    /// <summary>Quanto o cansaço de cada trio (tipo, zona, público) cai por semana.</summary>
    public required double CansacoDecaimentoSemanal { get; init; }

    public required Parties.DoubleRange Sorte { get; init; }

    /// <summary>Perda no segundo compromisso do dia em zona não vizinha, nos portes de <see cref="PortesComDeslocamento"/>.</summary>
    public required double DeslocamentoPerda { get; init; }

    public required List<string> PortesComDeslocamento { get; init; }

    /// <summary>Bônus com liderança aliada da zona junto.</summary>
    public required double LiderancaBonus { get; init; }

    /// <summary>Bônus num evento da cidade, com o público dele.</summary>
    public required double EventoBonus { get; init; }

    /// <summary>Presença mínima na zona para o comício encher; abaixo dela rende <see cref="ComicioVazioFator"/>.</summary>
    public required double ComicioPresencaMinima { get; init; }

    public required double ComicioVazioFator { get; init; }

    /// <summary>Chance de chuva num dia e a faixa de perda nos compromissos de rua.</summary>
    public required double ChuvaChance { get; init; }

    public required Parties.DoubleRange ChuvaPerda { get; init; }

    /// <summary>Estresse acima do qual os testes de atributo perdem <see cref="EstressePenalidade"/> de chance.</summary>
    public required int EstresseLimite { get; init; }

    public required double EstressePenalidade { get; init; }

    /// <summary>Com quantos dias de antecedência dá para marcar.</summary>
    public required int AntecedenciaDias { get; init; }

    /// <summary>Limites do alcance relativo de uma zona (zona pequena rende mais por turno; zona grande, menos).</summary>
    public required Parties.DoubleRange Alcance { get; init; }

    /// <summary>Teto do apoio que conversas (reunião, evento, gravação) juntam de um público; perto dele, cada conversa rende menos.</summary>
    public required double ApoioTeto { get; init; }

    /// <summary>Presença mínima na zona para marcar reunião sem liderança conhecida ali.</summary>
    public required double ReuniaoPresencaMinima { get; init; }
}

/// <summary>Tipo de compromisso (GDD 17.6).</summary>
public sealed class CommitmentType
{
    public const string PublicoGeral = "geral";

    public required string Id { get; init; }

    public required string Nome { get; init; }

    public required string Descricao { get; init; }

    public required List<string> Fases { get; init; }

    public required int Turnos { get; init; }

    /// <summary>O compromisso acontece numa zona.</summary>
    public required bool ExigeZona { get; init; }

    /// <summary>O jogador escolhe um grupo de eleitores como público (senão o público é geral).</summary>
    public bool EscolhePublico { get; init; }

    /// <summary>Grupo de eleitores que recebe o apoio, quando não é escolhido nem vem do evento (ex.: gravação para as redes).</summary>
    public string? PublicoFixo { get; init; }

    /// <summary>É uma visita a uma liderança (o alvo é a pessoa).</summary>
    public bool Visita { get; init; }

    /// <summary>Compromisso de rua: chuva atrapalha e liderança junto ajuda.</summary>
    public bool Rua { get; init; }

    /// <summary>Só existe como evento da cidade (no dia e na zona do evento).</summary>
    public bool SoEvento { get; init; }

    /// <summary>Presença de base na zona (pontos, no porte médio, antes dos fatores).</summary>
    public double Presenca { get; init; }

    /// <summary>Presença de base em todas as zonas (entrevista, gravação).</summary>
    public double PresencaCidade { get; init; }

    /// <summary>Apoio de base do público na zona (0 a 1).</summary>
    public double Apoio { get; init; }

    public double Imagem { get; init; }

    /// <summary>Custo em reais no porte médio; escalado pelo porte.</summary>
    public decimal Dinheiro { get; init; }

    /// <summary>Arrecadação por ponto do atributo (jantar), escalada pelo porte.</summary>
    public MoneyPerAttribute? Arrecada { get; init; }

    public int Estresse { get; init; }

    public ActionTest? Teste { get; init; }

    /// <summary>O coordenador pode mandar representante (GDD 17.6, "Delegar").</summary>
    public bool Delegavel { get; init; }

    /// <summary>Função da equipe cujo efeito multiplica o rendimento (ex.: marqueteiro no comício), e qual efeito.</summary>
    public string? EquipeFuncao { get; init; }

    public string? EquipeEfeito { get; init; }

    /// <summary>O compromisso pode pegar mal nas redes (chance pelo social media).</summary>
    public bool RiscoViral { get; init; }

    public bool AllowedIn(CampaignPhase phase) => phase switch
    {
        CampaignPhase.PreCampanha => Fases.Contains(CampaignActionDefinition.PreCampanha),
        CampaignPhase.Campanha => Fases.Contains(CampaignActionDefinition.Campanha),
        _ => false,
    };
}

/// <summary>data/campaign/commitments.json: substitui as 6 ações de efeito fixo (GDD 17.6).</summary>
public sealed class CommitmentCatalog : IValidatable
{
    public const string File = "campaign/commitments.json";
    public const string Comicio = "comicio";
    public const string Visita = "visita";
    public const string Reuniao = "reuniao";
    public const string EventoCidade = "evento_cidade";
    public const string Descanso = "descanso";

    public required string Note { get; init; }

    public required AgendaRules Regras { get; init; }

    public required List<CommitmentType> Tipos { get; init; }

    public CommitmentType Get(string id) => Tipos.First(t => t.Id == id);

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Tipos.Select(t => t.Id), "Compromisso"))
        {
            yield return error;
        }

        foreach (string id in new[] { Comicio, Visita, Reuniao, EventoCidade, Descanso }.Where(id => Tipos.All(t => t.Id != id)))
        {
            yield return $"falta o compromisso '{id}'.";
        }

        foreach (CommitmentType t in Tipos)
        {
            if (t.Turnos is < 1 or > 2 || t.Fases.Count == 0 || t.Fases.Any(f => f is not (CampaignActionDefinition.PreCampanha or CampaignActionDefinition.Campanha)))
            {
                yield return $"compromisso '{t.Id}': 1 ou 2 turnos e fases válidas.";
            }

            // Limites de texto do ART_GUIDE (Interface, seção 9).
            if (t.Nome.Length > 28)
            {
                yield return $"compromisso '{t.Id}': nome com mais de 28 caracteres.";
            }

            if ((t.EquipeFuncao is null) != (t.EquipeEfeito is null)
                || (t.EquipeFuncao is not null && (!StaffRole.Required.TryGetValue(t.EquipeFuncao, out string[]? effects) || !effects.Contains(t.EquipeEfeito))))
            {
                yield return $"compromisso '{t.Id}': função e efeito da equipe desconhecidos.";
            }

            if (t.Presenca < 0 || t.PresencaCidade < 0 || t.Apoio is < 0 or > 1)
            {
                yield return $"compromisso '{t.Id}': presença e apoio não negativos.";
            }
        }

        AgendaRules r = Regras;
        if (r.TurnosSemCusto < 1 || r.CansacoFator <= 0 || r.CansacoDecaimentoSemanal <= 0 || r.Sorte.Min <= 0 || r.Sorte.Max < r.Sorte.Min
            || r.DeslocamentoPerda is < 0 or >= 1 || r.ChuvaChance is < 0 or > 1 || r.ChuvaPerda.Min < 0 || r.ChuvaPerda.Max >= 1
            || r.AntecedenciaDias < 1 || r.Alcance.Min <= 0 || r.Alcance.Max < r.Alcance.Min)
        {
            yield return "regras: valores fora do intervalo.";
        }
    }
}

/// <summary>Item da agenda: o quê, em que zona, em que dia e turno, para que público, com quem (GDD 17.2, peça 3).</summary>
public sealed class Commitment
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public string? ZoneId { get; init; }

    public required DateOnly Date { get; init; }

    public required Turno Turno { get; init; }

    /// <summary>"geral" ou o id de um grupo de eleitores.</summary>
    public string Publico { get; init; } = CommitmentType.PublicoGeral;

    /// <summary>Liderança que acompanha (compromisso de rua) ou que é visitada.</summary>
    public string? PersonId { get; init; }

    /// <summary>Evento da cidade em que o compromisso acontece.</summary>
    public string? CityEventId { get; init; }

    /// <summary>Veio de um convite (dia e turno fixos).</summary>
    public string? InviteId { get; init; }

    /// <summary>Um representante vai no lugar do candidato: rende menos e não ocupa o turno dele.</summary>
    public bool Delegado { get; init; }

    /// <summary>Tema tratado (entrevista, gravação, comício): torna a posição do candidato mais conhecida.</summary>
    public string? Tema { get; init; }
}

/// <summary>O que se pede ao marcar: os mesmos campos do compromisso, sem id.</summary>
public sealed record CommitmentRequest(string Type, string? ZoneId, DateOnly Date, Turno Turno, string Publico = CommitmentType.PublicoGeral, string? PersonId = null, string? CityEventId = null, string? InviteId = null, bool Delegado = false, string? Tema = null);

/// <summary>Um fator do rendimento, com o nome que a tela mostra e o multiplicador ("terceira caminhada no Centro em duas semanas", 0,45).</summary>
public sealed record YieldFactor(string Id, string Text, double Multiplier);

public enum ScheduleFailure
{
    FaseNaoPermite,
    ForaDoPrazo,
    TurnoOcupado,
    SemDinheiro,
    ZonaInvalida,
    PublicoInvalido,
    SemLideranca,
    SemApoiadorNaZona,
    EventoInvalido,
    DecisaoPendente,
    NaoDelegavel,
    SemCoordenador,
    SemDelegacao,
    SemVerbaDeRua,
    TemaInvalido,
}

/// <summary>
/// Previsão de um compromisso antes de marcar (princípio f): efeito esperado e faixa, fatores do rendimento (com o
/// cansaço, princípio g) e os desfechos sorteados. O resultado real cai sempre dentro da faixa.
/// </summary>
public sealed record CommitmentForecast(
    ScheduleFailure? Blocked,
    double PresenceMin,
    double PresenceMax,
    double PresenceExpected,
    double SupportMin,
    double SupportMax,
    double ImageMin,
    double ImageMax,
    decimal MoneyMin,
    decimal MoneyMax,
    int Stress,
    int RelationPoints,
    double FatigueFactor,
    IReadOnlyList<YieldFactor> Factors,
    IReadOnlyList<ForecastOutcome> Outcomes)
{
    /// <summary>Produto dos fatores, sem a sorte.</summary>
    public double Yield => Factors.Aggregate(1.0, (acc, f) => acc * f.Multiplier);
}

/// <summary>Resultado explicado de um compromisso concluído (GDD 17.3, "Resultado explicado").</summary>
public sealed class CommitmentResult
{
    public required Commitment Commitment { get; init; }

    /// <summary>Mudança de presença por zona (id → pontos), com o transbordo para as vizinhas.</summary>
    public required Dictionary<string, double> Presence { get; init; }

    /// <summary>Apoio ganho do público na zona.</summary>
    public double Support { get; init; }

    public double Image { get; init; }

    public decimal Money { get; init; }

    public int Stress { get; init; }

    public int RelationPoints { get; init; }

    public required List<YieldFactor> Factors { get; init; }

    /// <summary>Texto do desfecho sorteado (teste de atributo), se houve.</summary>
    public string Text { get; init; } = "";
}

/// <summary>Evento da cidade numa data, zona e turno (GDD 17.6): só existe no dia.</summary>
public sealed class CityEventInstance
{
    public required string Id { get; init; }

    public required string TypeId { get; init; }

    public required string Nome { get; init; }

    public required string ZoneId { get; init; }

    public required DateOnly Date { get; init; }

    public required Turno Turno { get; init; }

    /// <summary>Grupo de eleitores presente ("geral" quando é a zona toda).</summary>
    public required string Publico { get; init; }
}

/// <summary>Convite com dia e turno fixos; recusar (ou deixar vencer) fica na memória de quem convidou.</summary>
public sealed class Invite
{
    public required string Id { get; init; }

    public required string FromPersonId { get; init; }

    public required string Text { get; init; }

    public required string ZoneId { get; init; }

    public required DateOnly Date { get; init; }

    public required Turno Turno { get; init; }

    public required string Publico { get; init; }

    public bool Answered { get; set; }
}

/// <summary>Uma linha do feed: o que aconteceu, onde e com quem (o fato de origem das notícias da R3).</summary>
public sealed record FeedItem(DateOnly Date, string Kind, string Text, string? ZoneId = null, string? PersonId = null, string? CandidateId = null);

/// <summary>Agenda da campanha (GDD 17.6): compromissos marcados, eventos da cidade, convites, cansaço e chuva prevista.</summary>
public sealed class AgendaState
{
    public const int MaxLog = 80;
    public const int MaxFeed = 200;

    public List<Commitment> Scheduled { get; init; } = [];

    public List<CityEventInstance> CityEvents { get; init; } = [];

    public List<Invite> Invites { get; init; } = [];

    /// <summary>Cansaço por trio "tipo|zona|público".</summary>
    public Dictionary<string, double> Fatigue { get; init; } = [];

    /// <summary>Dias com chuva prevista → perda nos compromissos de rua (0 a 1).</summary>
    public Dictionary<DateOnly, double> Rain { get; init; } = [];

    public List<CommitmentResult> Log { get; init; } = [];

    public List<FeedItem> Feed { get; init; } = [];

    /// <summary>Liderança cujo pedido espera resposta do jogador (decisão imediata).</summary>
    public string? PendingRequest { get; set; }

    public int NextId { get; set; } = 1;

    /// <summary>Chave do cansaço: tipo, lugar (a zona ou, na visita, a pessoa visitada) e público.</summary>
    public static string FatigueKey(string type, string? zoneId, string publico, string? personId = null) => $"{type}|{zoneId ?? personId ?? "-"}|{publico}";

    public static string FatigueKey(Commitment c, bool visit) => FatigueKey(c.Type, c.ZoneId, c.Publico, visit ? c.PersonId : null);

    public Commitment? At(DateOnly date, Turno turno) => Scheduled.FirstOrDefault(c => c.Date == date && c.Turno == turno && !c.Delegado);

    public void AddFeed(FeedItem item)
    {
        Feed.Add(item);
        if (Feed.Count > MaxFeed)
        {
            Feed.RemoveRange(0, Feed.Count - MaxFeed);
        }
    }
}
