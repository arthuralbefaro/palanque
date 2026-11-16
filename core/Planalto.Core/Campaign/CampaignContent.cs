using Planalto.Core.Characters;
using Planalto.Core.Data;

namespace Planalto.Core.Campaign;

public sealed class IntRange
{
    public required int Min { get; init; }

    public required int Max { get; init; }
}

/// <summary>Conteúdo de data/campaign/config.json: números de balanceamento da campanha.</summary>
public sealed class CampaignConfig : IValidatable
{
    public required int WeeklyEnergy { get; init; }

    public required int PartiesInRace { get; init; }

    public required int NamedRivals { get; init; }

    public required int BulkCandidatesPerParty { get; init; }

    public required double LegendVoteShare { get; init; }

    public required IntRange MayorCandidates { get; init; }

    public required double RecognitionDecayPerWeek { get; init; }

    public required double ImageDecayPerWeek { get; init; }

    public required double SupportDecayPerWeek { get; init; }

    public required IntRange NpcRecognition { get; init; }

    public required double BulkRecognition { get; init; }

    public required double IncumbentRecognitionBonus { get; init; }

    public required IntRange MayorRecognition { get; init; }

    public required int PublicPollEveryWeeks { get; init; }

    /// <summary>Início oficial da propaganda, "MM-dd" (Lei 9.504, art. 36).</summary>
    public required string CampaignStart { get; init; }

    public required string ConventionDate { get; init; }

    public required IntRange NpcActionsPerWeek { get; init; }

    public required double NpcActionEfficiency { get; init; }

    /// <summary>Empenho dos rivais a vereador (multiplica as ações por semana), sorteado por rival.</summary>
    public required Parties.DoubleRange NpcDrive { get; init; }

    /// <summary>
    /// Empenho do candidato coletivo de cada partido: ele representa candidatos anônimos que também fazem
    /// campanha, então ganha reconhecimento no turno dos rivais.
    /// </summary>
    public required double BulkDrive { get; init; }

    /// <summary>
    /// Multiplicador das ações semanais dos rivais por porte: em cidades maiores as campanhas têm equipe e
    /// estrutura, enquanto a energia do jogador é a de uma pessoa.
    /// </summary>
    public required Dictionary<string, double> NpcEffortByPorte { get; init; }

    /// <summary>Empenho dos candidatos a prefeito.</summary>
    public required Parties.DoubleRange MayorDrive { get; init; }

    public required Dictionary<string, double> PorteCostMultiplier { get; init; }

    public required Dictionary<string, double> PorteRecognitionMultiplier { get; init; }

    public required double ElectionNoiseSd { get; init; }

    public required int CountSnapshots { get; init; }

    public DateOnly DateIn(int year, string monthDay) =>
        DateOnly.ParseExact($"{year}-{monthDay}", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public IEnumerable<string> Validate()
    {
        if (WeeklyEnergy <= 0 || PartiesInRace < 2 || NamedRivals < 0 || BulkCandidatesPerParty < 1 || LegendVoteShare is < 0 or >= 1)
        {
            yield return "energia, partidos na disputa (>= 2), candidatos anônimos (>= 1) e fatia de legenda em [0, 1).";
        }

        if (MayorCandidates is null || MayorCandidates.Min < 2 || MayorCandidates.Max < MayorCandidates.Min)
        {
            yield return "mayor_candidates: mínimo 2, com 'min' até 'max'.";
        }

        foreach (string date in new[] { CampaignStart, ConventionDate })
        {
            if (!DateOnly.TryParseExact($"2028-{date}", "yyyy-MM-dd", out _))
            {
                yield return $"data '{date}' inválida (use MM-dd).";
            }
        }

        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            if (!PorteCostMultiplier.ContainsKey(porte) || !PorteRecognitionMultiplier.ContainsKey(porte))
            {
                yield return $"falta o multiplicador do porte '{porte}'.";
            }
        }

        if (NpcDrive.Min < 0 || NpcDrive.Max < NpcDrive.Min || MayorDrive.Min < 0 || MayorDrive.Max < MayorDrive.Min || BulkDrive < 0)
        {
            yield return "npc_drive e mayor_drive: mínimo >= 0 e máximo >= mínimo; bulk_drive >= 0.";
        }

        if (CountSnapshots < 2 || PublicPollEveryWeeks < 1 || ElectionNoiseSd < 0 || NpcActionEfficiency < 0)
        {
            yield return "count_snapshots >= 2, public_poll_every_weeks >= 1, ruído e eficiência não negativos.";
        }
    }
}

public sealed class ActionTarget
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Grupo de segmento que recebe o apoio.</summary>
    public required string Group { get; init; }

    public required double Support { get; init; }
}

public sealed class ActionRisk
{
    public required double Chance { get; init; }

    public required string Text { get; init; }

    public required Effects Effects { get; init; }
}

/// <summary>Teste de atributo: chance de sucesso = atributo / 100.</summary>
public sealed class ActionTest
{
    public required AttributeId Attribute { get; init; }

    public required string SuccessText { get; init; }

    public required Effects Success { get; init; }

    public required string FailureText { get; init; }

    public required Effects Failure { get; init; }
}

public sealed class MoneyPerAttribute
{
    public required AttributeId Attribute { get; init; }

    public required decimal Amount { get; init; }
}

/// <summary>Ação da agenda de campanha (GDD 5.6).</summary>
public sealed class CampaignActionDefinition
{
    public const string PreCampanha = "pre_campanha";
    public const string Campanha = "campanha";

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    public required List<string> Phases { get; init; }

    public required int Energy { get; init; }

    /// <summary>Custo em reais no porte médio (negativo = arrecada); escalado pelo porte.</summary>
    public required decimal Money { get; init; }

    public MoneyPerAttribute? MoneyPerAttribute { get; init; }

    public required Effects Effects { get; init; }

    public List<ActionTarget> Targets { get; init; } = [];

    public ActionTest? Test { get; init; }

    public required List<ActionRisk> Risks { get; init; }

    public bool AllowedIn(CampaignPhase phase) => phase switch
    {
        CampaignPhase.PreCampanha => Phases.Contains(PreCampanha),
        CampaignPhase.Campanha => Phases.Contains(Campanha),
        _ => false,
    };
}

/// <summary>Conteúdo de data/campaign/actions.json.</summary>
public sealed class CampaignActionCatalog : IValidatable
{
    public required List<CampaignActionDefinition> Actions { get; init; }

    public CampaignActionDefinition Get(string id) => Actions.First(a => a.Id == id);

    public IEnumerable<string> Validate()
    {
        if (Actions is null || Actions.Count != 6)
        {
            yield return $"a fatia vertical tem 6 ações (GDD, Fase 3); há {Actions?.Count ?? 0}.";
            yield break;
        }

        foreach (string error in DataChecks.UniqueIds(Actions.Select(a => a?.Id), "Ação"))
        {
            yield return error;
        }

        foreach (CampaignActionDefinition a in Actions)
        {
            if (a.Energy <= 0 || a.Phases.Count == 0 || a.Phases.Any(p => p is not (CampaignActionDefinition.PreCampanha or CampaignActionDefinition.Campanha)))
            {
                yield return $"ação '{a.Id}': energia positiva e fases válidas (pre_campanha, campanha).";
            }

            if (a.Risks.Any(r => r.Chance is < 0 or > 1))
            {
                yield return $"ação '{a.Id}': chance de risco entre 0 e 1.";
            }
        }
    }
}

/// <summary>Conteúdo da campanha já validado.</summary>
public sealed class CampaignContent
{
    public const string ConfigFile = "campaign/config.json";
    public const string ActionsFile = "campaign/actions.json";

    public required CampaignConfig Config { get; init; }

    public required CampaignActionCatalog Actions { get; init; }

    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, out CampaignContent? content)
    {
        var config = JsonData.LoadFile<CampaignConfig>(Path.Combine(dataDirectory, ConfigFile));
        var actions = JsonData.LoadFile<CampaignActionCatalog>(Path.Combine(dataDirectory, ActionsFile));
        var reports = new List<DataFileReport>
        {
            new(ConfigFile, 1, config.Errors),
            new(ActionsFile, actions.Value?.Actions.Count ?? 0, actions.Errors),
        };
        content = config.IsValid && actions.IsValid ? new CampaignContent { Config = config.Value!, Actions = actions.Value! } : null;
        return reports;
    }

    public static CampaignContent LoadOrThrow(string dataDirectory)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, out CampaignContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }
}
