using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.Ideology;

namespace Planalto.Core.Campaign;

/// <summary>
/// Efeitos de uma ação ou escolha de evento (GDD 5.6 e 15.3). Recursos em pontos; imagem de -1 a 1;
/// apoio por grupo de segmento (ids de data/electorate/segments.json) de 0 a 1.
/// </summary>
public sealed class Effects
{
    public double Recognition { get; init; }

    public double Image { get; init; }

    public decimal Money { get; init; }

    /// <summary>
    /// Herança das 6 ações: a energia semanal saiu com a agenda (GDD 17.6). Nos eventos que ainda a citam, cada
    /// 4 pontos de energia perdidos viram 1 de estresse (e o inverso), para a escolha continuar tendo custo.
    /// </summary>
    public int Energy { get; init; }

    public int Stress { get; init; }

    public int Health { get; init; }

    public int PoliticalCapital { get; init; }

    public Dictionary<string, double> Support { get; init; } = [];

    public Dictionary<AttributeId, int> Attributes { get; init; } = [];

    public List<string> SetFlags { get; init; } = [];

    public List<string> ClearFlags { get; init; } = [];

    /// <summary>Esqueleto no armário: algo que pode vir à tona depois (GDD 5.11).</summary>
    public Skeleton? AddSkeleton { get; init; }

    /// <summary>Eventos de continuação agendados (encadeamento, GDD 15.3).</summary>
    public List<ScheduledEvent> Schedule { get; init; } = [];

    /// <summary>Efeito na imagem do rival envolvido no evento.</summary>
    public double RivalImage { get; init; }

    public double RivalRecognition { get; init; }

    /// <summary>Mudança na relação com o NPC envolvido (-100 a 100).</summary>
    public int Relation { get; init; }

    public static Effects None { get; } = new();
}

public sealed record Skeleton(string Type, int Severity, DateOnly Created = default);

public sealed record ScheduledEvent(string Event, int DelayWeeks);

/// <summary>Candidato numa disputa municipal: o jogador, um rival nomeado ou o conjunto dos demais de um partido.</summary>
public sealed class Candidate
{
    public required string Id { get; init; }

    public required string Name { get; set; }

    public required string PartyId { get; init; }

    public required Office Office { get; init; }

    public required IdeologicalPosition Position { get; init; }

    public required DateOnly BirthDate { get; init; }

    public Gender Gender { get; init; }

    /// <summary>Número de urna (vereador: 5 dígitos; prefeito: o do partido).</summary>
    public required string Number { get; init; }

    /// <summary>Reconhecimento na cidade (0 a 100).</summary>
    public double Recognition { get; set; }

    public double Image { get; set; }

    public Dictionary<string, double> Support { get; init; } = [];

    public decimal Money { get; set; }

    public bool IsPlayer { get; init; }

    /// <summary>Representa os demais candidatos do partido (sem nome próprio); os votos são repartidos na apuração.</summary>
    public bool IsBulk { get; init; }

    /// <summary>Candidatos que o coletivo representa (lista cheia menos os nomeados); 1 para pessoas.</summary>
    public double Multiplicity { get; set; } = 1;

    public bool Incumbent { get; init; }

    /// <summary>NPC em <see cref="State.GameState.Npcs"/>, quando for uma pessoa.</summary>
    public string? CharacterId { get; init; }

    /// <summary>Quanto o rival investe na campanha (0 a 1): define o ritmo das ações da IA.</summary>
    public double Drive { get; init; } = 0.5;
}

public enum CampaignPhase
{
    PreCampanha,
    Campanha,
    Votacao,
    Encerrada,
}

public sealed record Headline(DateOnly Week, string Text, string Kind);

public sealed record PollRecord(DateOnly Week, string PollsterId, int SampleSize, double MarginOfError, Dictionary<string, double> Candidates, double BlankNull, double Undecided, bool Internal);

/// <summary>Voto do jogador na urna (GDD 16.3): número digitado, branco, nulo ou não votou.</summary>
public sealed record UrnaVote(Office Office, string? Number, bool Blank);

/// <summary>Estado de uma etapa parcial da apuração, para a noite da apuração.</summary>
public sealed record CountSnapshot(double PercentCounted, Dictionary<string, long> Votes);

public sealed class OfficeOutcome
{
    public required Office Office { get; init; }

    public required Dictionary<string, long> Votes { get; init; }

    public required long Blank { get; init; }

    public required long Null { get; init; }

    public required long Abstention { get; init; }

    public required List<string> Elected { get; init; }

    /// <summary>Cadeiras por partido (vereador).</summary>
    public Dictionary<string, int> SeatsByParty { get; init; } = [];

    /// <summary>Quociente eleitoral (vereador).</summary>
    public long ElectoralQuotient { get; init; }

    /// <summary>Etapas parciais da apuração, em ordem.</summary>
    public required List<CountSnapshot> Snapshots { get; init; }

    public bool HadRunoff { get; init; }

    public List<string> RunoffCandidates { get; init; } = [];
}

public sealed class ElectionOutcome
{
    public required DateOnly Date { get; init; }

    public required OfficeOutcome Vereador { get; init; }

    public required OfficeOutcome Prefeito { get; init; }

    public bool PlayerElected { get; init; }

    /// <summary>Votos dos candidatos anônimos de cada partido (sobra do candidato coletivo).</summary>
    public Dictionary<string, List<long>> BulkVotes { get; init; } = [];
}

/// <summary>Estado da campanha municipal em curso (GDD 5.6 e 6).</summary>
public sealed class CampaignState
{
    public required int Municipio { get; init; }

    public required DateOnly ElectionDate { get; init; }

    public required DateOnly CampaignStart { get; init; }

    public CampaignPhase Phase { get; set; } = CampaignPhase.PreCampanha;

    public required List<Candidate> Candidates { get; init; }

    /// <summary>Presença e apoio por zona (GDD 17.5).</summary>
    public required Territory.TerritoryState Territory { get; init; }

    /// <summary>Compromissos, eventos da cidade, convites, cansaço e feed (GDD 17.6).</summary>
    public AgendaState Agenda { get; init; } = new();

    public List<Headline> Headlines { get; init; } = [];

    public List<PollRecord> Polls { get; init; } = [];

    public List<UrnaVote> PlayerVotes { get; init; } = [];

    public ElectionOutcome? Outcome { get; set; }

    public Events.EventState Events { get; init; } = new();

    /// <summary>Debate de TV marcado para esta semana (só na disputa de prefeito).</summary>
    public Debate.DebateState? Debate { get; set; }

    /// <summary>Datas de debate já realizadas ou recusadas.</summary>
    public List<DateOnly> DebatesHeld { get; init; } = [];

    public Candidate Player => Candidates.First(c => c.IsPlayer);

    public IEnumerable<Candidate> ForOffice(Office office) => Candidates.Where(c => c.Office == office);

    public Candidate? Find(string id) => Candidates.FirstOrDefault(c => c.Id == id);
}
