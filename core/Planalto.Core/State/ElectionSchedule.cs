namespace Planalto.Core.State;

public enum ElectionKind
{
    Municipal,
    General,
}

/// <summary>Datas legais de um ciclo eleitoral.</summary>
public sealed record ElectionSchedule(
    int Year,
    ElectionKind Kind,
    DateOnly FirstRound,
    DateOnly SecondRound,
    DateOnly CampaignStart,
    DateOnly PartyAffiliationDeadline);
