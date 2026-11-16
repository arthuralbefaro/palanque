using Planalto.Core.Data;
using Planalto.Core.Ideology;
using Planalto.Core.Legislature;

namespace Planalto.Core.Executive;

public sealed class IndicatorDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }
}

/// <summary>Área do orçamento: parcela de referência (indicador em 50), mínimo legal e indicadores que move.</summary>
public sealed class BudgetArea
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required double ReferenceShare { get; init; }

    /// <summary>Mínimo constitucional ou legal (saúde 15%, educação 25%); 0 se não houver.</summary>
    public required double MinimumShare { get; init; }

    public required Dictionary<string, double> Effects { get; init; }
}

public sealed class ApprovalConfig
{
    public required double Base { get; init; }

    public required double IndicatorWeight { get; init; }

    public required double ImageWeight { get; init; }

    public required double SupportWeight { get; init; }

    public required int HoneymoonWeeks { get; init; }

    public required double HoneymoonBonus { get; init; }
}

public sealed class BoundsConfig
{
    public required double Min { get; init; }

    public required double Max { get; init; }
}

/// <summary>Parâmetros do mandato de prefeito (data/executive/config.json).</summary>
public sealed class ExecutiveConfig : IValidatable
{
    public const string ReserveArea = "reserva";

    public required int WeeklyEnergy { get; init; }

    public required string RevenuePerCapitaNote { get; init; }

    public required Dictionary<string, decimal> RevenuePerCapita { get; init; }

    public required List<IndicatorDefinition> Indicators { get; init; }

    public required List<BudgetArea> Areas { get; init; }

    /// <summary>Pontos de indicador por 100% acima (ou abaixo) da parcela de referência.</summary>
    public required double TargetSensitivity { get; init; }

    public required BoundsConfig IndicatorBounds { get; init; }

    /// <summary>Fração da distância até o alvo que o indicador anda por semana (o atraso dos indicadores).</summary>
    public required double IndicatorSpeedPerWeek { get; init; }

    /// <summary>Pontos de "contas" por 100% do orçamento de reserva não usada.</summary>
    public required double UnusedReserveToAccounts { get; init; }

    public required ApprovalConfig Approval { get; init; }

    /// <summary>Peso de cada indicador para cada grupo de eleitores.</summary>
    public required Dictionary<string, Dictionary<string, double>> GroupWeights { get; init; }

    public required int EnactEnergy { get; init; }

    public required double SupportPerPoint { get; init; }

    public required double ImagePerPoint { get; init; }

    public required double RelationDriftPerWeek { get; init; }

    public required double SupportDecayPerWeek { get; init; }

    public required double RecognitionDecayPerWeek { get; init; }

    public required int Secretarias { get; init; }

    public required int SecretariaRelation { get; init; }

    public required double MinimumViolationImage { get; init; }

    public required double CrisisChancePerWeek { get; init; }

    /// <summary>Escolha usada quando a crise se resolve sozinha (ano de campanha).</summary>
    public required int AutoCrisisChoice { get; init; }

    public required double EmendasImpositivasShare { get; init; }

    public BudgetArea Area(string id) => Areas.First(a => a.Id == id);

    public IEnumerable<string> Validate()
    {
        var indicators = Indicators.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        if (Math.Abs(Areas.Sum(a => a.ReferenceShare) - 1) > 0.001)
        {
            yield return "areas: as parcelas de referência devem somar 1.";
        }

        if (Areas.All(a => a.Id != ReserveArea))
        {
            yield return $"areas: falta a área '{ReserveArea}'.";
        }

        foreach (BudgetArea a in Areas)
        {
            if (a.ReferenceShare <= 0 && a.Id != ReserveArea || a.MinimumShare is < 0 or > 1)
            {
                yield return $"{a.Id}: parcela de referência positiva e mínimo entre 0 e 1.";
            }

            foreach (string i in a.Effects.Keys.Where(i => !indicators.Contains(i)))
            {
                yield return $"{a.Id}: indicador '{i}' inexistente.";
            }
        }

        foreach (var (group, weights) in GroupWeights)
        {
            foreach (string i in weights.Keys.Where(i => !indicators.Contains(i)))
            {
                yield return $"group_weights.{group}: indicador '{i}' inexistente.";
            }
        }

        if (IndicatorSpeedPerWeek is <= 0 or > 1 || CrisisChancePerWeek is < 0 or > 1 || Secretarias < 1 || WeeklyEnergy <= 0)
        {
            yield return "velocidade dos indicadores e chance de crise em (0, 1]; secretarias e energia positivas.";
        }
    }
}

/// <summary>Carta de política (GDD 5.9): custo anual tirado da reserva (negativo = libera), efeito no alvo dos indicadores.</summary>
public sealed class PolicyDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Description { get; init; }

    /// <summary>Fração do orçamento por ano (negativa quando arrecada ou economiza).</summary>
    public required double Cost { get; init; }

    public required int ImplementationWeeks { get; init; }

    /// <summary>Precisa de lei: vai a votação na Câmara.</summary>
    public required bool RequiresLaw { get; init; }

    public required Quorum Quorum { get; init; }

    public required IdeologicalPosition Position { get; init; }

    public required Dictionary<string, double> Boosts { get; init; }

    public required Dictionary<string, int> Popularity { get; init; }

    public required double Salience { get; init; }
}

public sealed class PolicyCatalog : IValidatable
{
    public required List<PolicyDefinition> Policies { get; init; }

    public PolicyDefinition? Find(string id) => Policies.FirstOrDefault(p => p.Id == id);

    public IEnumerable<string> Validate()
    {
        foreach (string id in Policies.GroupBy(p => p.Id).Where(g => g.Count() > 1).Select(g => g.Key))
        {
            yield return $"política '{id}' repetida.";
        }

        foreach (PolicyDefinition p in Policies.Where(p => p.ImplementationWeeks < 0 || p.Salience is < 0 or > 1 || p.Popularity.Values.Any(v => v is < -2 or > 2)))
        {
            yield return $"{p.Id}: prazo não negativo, saliência entre 0 e 1 e popularidade de -2 a 2.";
        }
    }
}

public sealed class CrisisChoice
{
    public required string Label { get; init; }

    public required string Result { get; init; }

    /// <summary>Fração do orçamento gasta uma vez, tirada da reserva.</summary>
    public required double Cost { get; init; }

    public required Dictionary<string, double> Indicators { get; init; }

    public required double Image { get; init; }
}

public sealed class CrisisDefinition
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Text { get; init; }

    /// <summary>Meses em que pode ocorrer; vazio = o ano todo.</summary>
    public required List<int> Months { get; init; }

    public required double Weight { get; init; }

    /// <summary>Indicadores abaixo destes valores dobram a chance da crise.</summary>
    public required Dictionary<string, double> WorseWhenBelow { get; init; }

    public required Dictionary<string, double> Shock { get; init; }

    public required List<CrisisChoice> Choices { get; init; }
}

public sealed class CrisisCatalog : IValidatable
{
    public required List<CrisisDefinition> Crises { get; init; }

    public CrisisDefinition? Find(string id) => Crises.FirstOrDefault(c => c.Id == id);

    public IEnumerable<string> Validate()
    {
        foreach (CrisisDefinition c in Crises.Where(c => c.Choices.Count == 0 || c.Weight < 0 || c.Months.Any(m => m is < 1 or > 12)))
        {
            yield return $"{c.Id}: ao menos uma escolha, peso não negativo e meses de 1 a 12.";
        }
    }
}

/// <summary>Conteúdo do mandato de prefeito: configuração, políticas, crises e ações de gabinete.</summary>
public sealed class ExecutiveContent
{
    public const string ConfigFile = "executive/config.json";
    public const string PoliciesFile = "executive/policies.json";
    public const string CrisesFile = "executive/crises.json";
    public const string ActionsFile = "executive/actions.json";

    public required ExecutiveConfig Config { get; init; }

    public required PolicyCatalog Policies { get; init; }

    public required CrisisCatalog Crises { get; init; }

    public required MandateActionCatalog Actions { get; init; }

    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, IReadOnlySet<string>? groups, out ExecutiveContent? content)
    {
        var config = JsonData.LoadFile<ExecutiveConfig>(Path.Combine(dataDirectory, ConfigFile));
        var policies = JsonData.LoadFile<PolicyCatalog>(Path.Combine(dataDirectory, PoliciesFile));
        var crises = JsonData.LoadFile<CrisisCatalog>(Path.Combine(dataDirectory, CrisesFile));
        var actions = JsonData.LoadFile<MandateActionCatalog>(Path.Combine(dataDirectory, ActionsFile));

        var indicators = config.Value?.Indicators.Select(i => i.Id).ToHashSet(StringComparer.Ordinal) ?? [];
        var policyErrors = policies.Errors.ToList();
        var crisisErrors = crises.Errors.ToList();
        var configErrors = config.Errors.ToList();
        policyErrors.AddRange((policies.Value?.Policies ?? []).SelectMany(p => p.Boosts.Keys.Where(i => !indicators.Contains(i)).Select(i => $"{p.Id}: indicador '{i}' inexistente.")));
        crisisErrors.AddRange((crises.Value?.Crises ?? []).SelectMany(c => c.Shock.Keys.Concat(c.WorseWhenBelow.Keys).Concat(c.Choices.SelectMany(ch => ch.Indicators.Keys))
            .Where(i => !indicators.Contains(i)).Select(i => $"{c.Id}: indicador '{i}' inexistente.")));
        if (groups is not null)
        {
            policyErrors.AddRange((policies.Value?.Policies ?? []).SelectMany(p => p.Popularity.Keys.Where(g => !groups.Contains(g)).Select(g => $"{p.Id}: grupo '{g}' inexistente.")));
            configErrors.AddRange((config.Value?.GroupWeights.Keys ?? Enumerable.Empty<string>()).Where(g => !groups.Contains(g)).Select(g => $"group_weights: grupo '{g}' inexistente."));
        }

        var reports = new List<DataFileReport>
        {
            new(ConfigFile, config.Value?.Areas.Count ?? 0, configErrors),
            new(PoliciesFile, policies.Value?.Policies.Count ?? 0, policyErrors),
            new(CrisesFile, crises.Value?.Crises.Count ?? 0, crisisErrors),
            new(ActionsFile, actions.Value?.Actions.Count ?? 0, actions.Errors),
        };
        content = reports.All(r => r.IsValid) && config.Value is not null && policies.Value is not null && crises.Value is not null && actions.Value is not null
            ? new ExecutiveContent { Config = config.Value, Policies = policies.Value, Crises = crises.Value, Actions = actions.Value }
            : null;
        return reports;
    }

    public static ExecutiveContent LoadOrThrow(string dataDirectory, IReadOnlySet<string>? groups = null)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, groups, out ExecutiveContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }
}
