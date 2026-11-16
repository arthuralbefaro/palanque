using Planalto.Core.Elections;
using Planalto.Core.Legislature;

namespace Planalto.Core.Executive;

public enum PolicyStatus
{
    Implantando,
    Ativa,
    Rejeitada,
    Revogada,
}

/// <summary>Política adotada pela prefeitura; com lei, guarda o placar da Câmara.</summary>
public sealed class EnactedPolicy
{
    public required string PolicyId { get; init; }

    public required DateOnly Started { get; init; }

    public required DateOnly ActiveFrom { get; init; }

    public PolicyStatus Status { get; set; }

    public int Yes { get; init; }

    public int No { get; init; }

    public bool Counts => Status is PolicyStatus.Implantando or PolicyStatus.Ativa;
}

public sealed record PendingCrisis(string CrisisId, DateOnly Week);

public enum ExecutiveNewsKind
{
    PoliticaAprovada,
    PoliticaRejeitada,
    PoliticaDecretada,
    PoliticaEmVigor,
    PoliticaRevogada,
    Crise,
    CriseResolvida,
    MinimoDescumprido,
    ContasRejeitadas,
    SecretariaEntregue,
    SecretariaRetirada,
    Articulacao,
}

/// <summary>Notícia da prefeitura; a interface monta o texto a partir do tipo e dos dados.</summary>
public sealed record ExecutiveNews(DateOnly Week, ExecutiveNewsKind Kind, string Subject, int Yes = 0, int No = 0);

/// <summary>Mandato de prefeito em curso (GDD 5.9).</summary>
public sealed class ExecutiveState
{
    public Office Office { get; init; } = Office.Prefeito;

    public required int Municipio { get; init; }

    public required DateOnly Start { get; init; }

    public required DateOnly End { get; init; }

    /// <summary>1 no primeiro mandato, 2 na reeleição (sem direito a outra).</summary>
    public required int Term { get; init; }

    /// <summary>Parcela do orçamento por área (soma 1).</summary>
    public required Dictionary<string, double> Allocation { get; init; }

    /// <summary>Indicadores de 0 a 100 (50 = média de referência).</summary>
    public required Dictionary<string, double> Indicators { get; init; }

    public List<EnactedPolicy> Policies { get; init; } = [];

    /// <summary>Vereadores (todos NPCs), com relação com o prefeito.</summary>
    public required List<ChamberMember> Chamber { get; init; }

    /// <summary>Secretarias entregues a cada partido; quem tem secretaria é base.</summary>
    public required Dictionary<string, int> Secretarias { get; init; }

    public required string PartyId { get; set; }

    public double Image { get; set; }

    public double Recognition { get; set; }

    public Dictionary<string, double> Support { get; init; } = [];

    public int EnergyLeft { get; set; }

    public PendingCrisis? Crisis { get; set; }

    public List<ExecutiveNews> News { get; init; } = [];

    public bool MinimumViolatedThisYear { get; set; }

    public double Approval { get; set; }

    public IEnumerable<string> BaseParties => Secretarias.Where(s => s.Value > 0).Select(s => s.Key).Append(PartyId).Distinct();

    public bool InBase(string? partyId) => partyId is not null && BaseParties.Contains(partyId);
}
