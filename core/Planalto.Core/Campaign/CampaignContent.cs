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

    /// <summary>Quantas vezes um rival a vereador prefere uma zona do seu reduto a outra de mesma população.</summary>
    public required double NpcRedutoWeight { get; init; }

    /// <summary>Fração do efeito de uma saída que a campanha espalhada (prefeito, candidato coletivo) deixa em cada zona.</summary>
    public required double NpcSpreadFactor { get; init; }

    /// <summary>Imagem que cada saída rende ao rival.</summary>
    public required double NpcImagePerAction { get; init; }

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
        if (PartiesInRace < 2 || NamedRivals < 0 || BulkCandidatesPerParty < 1 || LegendVoteShare is < 0 or >= 1)
        {
            yield return "partidos na disputa (>= 2), candidatos anônimos (>= 1) e fatia de legenda em [0, 1).";
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

/// <summary>Alvo de uma ação de mandato (as ações de gabinete ainda usam; saem na R4).</summary>
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

/// <summary>Fases em que um compromisso pode ser marcado (ids usados em data/).</summary>
public static class CampaignActionDefinition
{
    public const string PreCampanha = "pre_campanha";
    public const string Campanha = "campanha";
}

/// <summary>Conteúdo da campanha já validado.</summary>
public sealed class CampaignContent
{
    public const string ConfigFile = "campaign/config.json";

    public required CampaignConfig Config { get; init; }

    /// <summary>Compromissos da agenda (GDD 17.6).</summary>
    public required CommitmentCatalog Commitments { get; init; }

    /// <summary>Lideranças locais (GDD 17.10).</summary>
    public required LeaderCatalog Leaders { get; init; }

    public required World.CityEventCatalog CityEvents { get; init; }

    public required Territory.TerritoryConfig Territory { get; init; }

    public required People.PeopleConfig People { get; init; }

    /// <summary>Equipe (GDD 17.7).</summary>
    public required StaffCatalog Staff { get; init; }

    /// <summary>Verba por canal (GDD 17.8).</summary>
    public required ChannelCatalog Channels { get; init; }

    /// <summary>Plataforma e promessas locais (GDD 17.9).</summary>
    public required PlatformCatalog Platform { get; init; }

    public required PromiseCatalog Promises { get; init; }

    /// <summary>Gerador de objetivos (GDD 17.2, peça 6).</summary>
    public required ObjectiveCatalog Objectives { get; init; }

    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, out CampaignContent? content)
    {
        var config = JsonData.LoadFile<CampaignConfig>(Path.Combine(dataDirectory, ConfigFile));
        var commitments = JsonData.LoadFile<CommitmentCatalog>(Path.Combine(dataDirectory, CommitmentCatalog.File));
        var leaders = JsonData.LoadFile<LeaderCatalog>(Path.Combine(dataDirectory, LeaderCatalog.File));
        var cityEvents = JsonData.LoadFile<World.CityEventCatalog>(Path.Combine(dataDirectory, World.CityEventCatalog.File));
        var territory = JsonData.LoadFile<Territory.TerritoryConfig>(Path.Combine(dataDirectory, Planalto.Core.Territory.TerritoryConfig.File));
        var people = JsonData.LoadFile<People.PeopleConfig>(Path.Combine(dataDirectory, Planalto.Core.People.PeopleConfig.File));
        var staff = JsonData.LoadFile<StaffCatalog>(Path.Combine(dataDirectory, StaffCatalog.File));
        var channels = JsonData.LoadFile<ChannelCatalog>(Path.Combine(dataDirectory, ChannelCatalog.File));
        var platform = JsonData.LoadFile<PlatformCatalog>(Path.Combine(dataDirectory, PlatformCatalog.File));
        var promises = JsonData.LoadFile<PromiseCatalog>(Path.Combine(dataDirectory, PromiseCatalog.File));
        var objectives = JsonData.LoadFile<ObjectiveCatalog>(Path.Combine(dataDirectory, ObjectiveCatalog.File));
        var reports = new List<DataFileReport>
        {
            new(ObjectiveCatalog.File, objectives.Value?.Tipos.Count ?? 0, objectives.Errors),
            new(PlatformCatalog.File, platform.Value?.Falas.Count ?? 0, platform.Errors),
            new(PromiseCatalog.File, promises.Value?.Promessas.Count ?? 0, promises.Errors),
            new(ChannelCatalog.File, channels.Value?.Canais.Count ?? 0, channels.Errors),
            new(StaffCatalog.File, staff.Value?.Funcoes.Count ?? 0, staff.Errors),
            new(ConfigFile, 1, config.Errors),
            new(CommitmentCatalog.File, commitments.Value?.Tipos.Count ?? 0, commitments.Errors),
            new(LeaderCatalog.File, leaders.Value?.Tipos.Count ?? 0, leaders.Errors),
            new(World.CityEventCatalog.File, cityEvents.Value?.Tipos.Count ?? 0, cityEvents.Errors),
            new(Planalto.Core.Territory.TerritoryConfig.File, 1, territory.Errors),
            new(Planalto.Core.People.PeopleConfig.File, 1, people.Errors),
        };
        content = reports.All(r => r.Errors.Count == 0)
            ? new CampaignContent { Config = config.Value!, Commitments = commitments.Value!, Leaders = leaders.Value!, CityEvents = cityEvents.Value!, Territory = territory.Value!, People = people.Value!, Staff = staff.Value!, Channels = channels.Value!, Platform = platform.Value!, Promises = promises.Value!, Objectives = objectives.Value! }
            : null;
        return reports;
    }

    public static CampaignContent LoadOrThrow(string dataDirectory)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, out CampaignContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }
}
