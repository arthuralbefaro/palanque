using Planalto.Core.Elections;
using Planalto.Core.Ideology;

namespace Planalto.Core.Legislature;

public enum BillStage
{
    Comissao,
    Plenario,
    Aprovado,
    Rejeitado,
    Arquivado,
}

public enum VoteChoice
{
    Sim,
    Nao,
    Abstencao,
    Ausente,
}

/// <summary>Projeto de lei em tramitação na Câmara.</summary>
public sealed class Bill
{
    public const string MayorAuthor = "prefeito";

    public required string Id { get; init; }

    public required string TemplateId { get; init; }

    public required string Title { get; init; }

    /// <summary>Id do membro autor, <see cref="MayorAuthor"/> ou o id do jogador.</summary>
    public required string AuthorId { get; init; }

    public required BillAuthorKind AuthorKind { get; init; }

    public required DateOnly Filed { get; init; }

    public BillStage Stage { get; set; } = BillStage.Comissao;

    /// <summary>Semana em que sai da comissão para o plenário.</summary>
    public required DateOnly CommitteeUntil { get; set; }

    public DateOnly? Decided { get; set; }

    public int Yes { get; set; }

    public int No { get; set; }

    public int Abstain { get; set; }

    /// <summary>Voto que o jogador marcou para a sessão; vira o registro quando a semana é resolvida.</summary>
    public VoteChoice? PlayerVote { get; set; }

    /// <summary>Orientação do partido do jogador na votação (-1 não, 0 liberado, 1 sim), registrada na sessão.</summary>
    public int? Orientation { get; set; }
}

/// <summary>Vereador na Câmara. A relação é com o jogador (-100 a 100).</summary>
public sealed class ChamberMember
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string? PartyId { get; set; }

    public required IdeologicalPosition Position { get; init; }

    public bool IsPlayer { get; init; }

    public double Relation { get; set; }

    /// <summary>Líder da bancada do partido na Câmara.</summary>
    public bool Leader { get; set; }

    /// <summary>Votos que recebeu na eleição (desempate e ordem de liderança).</summary>
    public long Votes { get; init; }
}

/// <summary>Tipo de notícia do mandato; a interface monta o texto (Tr) a partir do tipo e dos dados.</summary>
public enum MandateNewsKind
{
    ProjetoProtocolado,
    ProjetoAprovado,
    ProjetoRejeitado,
    ProjetoArquivado,
    Urgencia,
    Advertencia,
    Sancao,
    Expulsao,
    Ausencia,
    Emenda,
    Articulacao,
    MudancaDePartido,
}

/// <summary>Notícia do mandato: <paramref name="Subject"/> é o título do projeto ou o nome envolvido.</summary>
public sealed record MandateNews(DateOnly Week, MandateNewsKind Kind, string Subject, int Yes = 0, int No = 0, bool PlayerAuthor = false);

/// <summary>Mandato em curso do jogador (GDD 5.8). Por enquanto, vereador.</summary>
public sealed class MandateState
{
    public required Office Office { get; init; }

    public required int Municipio { get; init; }

    public required DateOnly Start { get; init; }

    /// <summary>Último dia do mandato (31 de dezembro).</summary>
    public required DateOnly End { get; init; }

    public required List<ChamberMember> Members { get; init; }

    /// <summary>Muda se o prefeito perde o mandato e o vice assume.</summary>
    public required string MayorId { get; set; }

    public required string MayorName { get; set; }

    public required string MayorPartyId { get; init; }

    /// <summary>Partidos da base do prefeito na Câmara.</summary>
    public required List<string> BaseParties { get; init; }

    public string? PresidentId { get; set; }

    /// <summary>Comissão do jogador; null depois de uma sanção.</summary>
    public string? Committee { get; set; }

    public List<Bill> Bills { get; init; } = [];

    public int NextBillNumber { get; set; } = 1;

    public double MayorRelation { get; set; }

    /// <summary>Fidelidade ao partido (0 a 100): cai ao votar contra a orientação.</summary>
    public int Fidelity { get; set; }

    public int Warnings { get; set; }

    public bool Sanctioned { get; set; }

    public bool Expelled { get; set; }

    public int EnergyLeft { get; set; }

    public int EmendasLeft { get; set; }

    public int EmendasYear { get; set; }

    /// <summary>Reconhecimento na cidade (0 a 100).</summary>
    public double Recognition { get; set; }

    public double Image { get; set; }

    /// <summary>Apoio por grupo de eleitores (0 a 1), herdado pela próxima campanha.</summary>
    public Dictionary<string, double> Support { get; init; } = [];

    public List<MandateNews> News { get; init; } = [];

    public ChamberMember Player => Members.First(m => m.IsPlayer);

    public ChamberMember? Member(string id) => Members.FirstOrDefault(m => m.Id == id);

    public bool InBase(string? partyId) => partyId is not null && BaseParties.Contains(partyId);
}

/// <summary>
/// Política do município em foco depois de uma eleição jogada: quem está na Câmara e na prefeitura. A próxima
/// disputa usa esses incumbentes no lugar dos dados do cenário.
/// </summary>
public sealed class LocalPolitics
{
    public required int Municipio { get; init; }

    public required int ElectionYear { get; init; }

    public required Dictionary<string, int> ChamberSeats { get; init; }

    /// <summary>Ids dos vereadores eleitos (personagens; o jogador aparece pelo id dele).</summary>
    public required List<string> Councillors { get; init; }

    /// <summary>Votos de cada vereador eleito, pelo id do personagem.</summary>
    public required Dictionary<string, long> CouncillorVotes { get; init; }

    public string? MayorId { get; set; }

    public string? MayorPartyId { get; set; }

    /// <summary>Prefeito no 2º mandato seguido, sem direito a nova reeleição.</summary>
    public bool MayorReelected { get; set; }

    /// <summary>Imagem e apoios do jogador na cidade, levados para a próxima campanha.</summary>
    public double PlayerImage { get; set; }

    public Dictionary<string, double> PlayerSupport { get; init; } = [];
}
