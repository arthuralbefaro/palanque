using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Data;

namespace Planalto.Core.Events;

public enum Rarity
{
    Comum,
    Incomum,
    Raro,
    Lendario,
}

/// <summary>Condições de um evento (GDD 15.3). Campos null não restringem.</summary>
public sealed class EventConditions
{
    /// <summary>Chance de o evento entrar no sorteio da semana quando as demais condições valem.</summary>
    public double Chance { get; init; } = 1;

    public decimal? MoneyLt { get; init; }

    public decimal? MoneyGt { get; init; }

    public double? RecognitionLt { get; init; }

    public double? RecognitionGt { get; init; }

    public double? ImageLt { get; init; }

    public double? ImageGt { get; init; }

    public int? StressGt { get; init; }

    public List<string> Porte { get; init; } = [];

    public List<string> Regiao { get; init; } = [];

    public string? Trait { get; init; }

    public string? NotTrait { get; init; }

    public string? Flag { get; init; }

    public string? NotFlag { get; init; }

    public Dictionary<AttributeId, int> AttributeMin { get; init; } = [];

    public int? WeeksToElectionLt { get; init; }

    public int? WeeksToElectionGt { get; init; }

    /// <summary>O evento cita um rival ({rival.nome}).</summary>
    public bool NeedsRival { get; init; }

    public bool NeedsSkeleton { get; init; }
}

public sealed class ChoiceRequirement
{
    public decimal? MoneyMin { get; init; }

    public Dictionary<AttributeId, int> AttributeMin { get; init; } = [];

    public string? Flag { get; init; }

    public int? PoliticalCapitalMin { get; init; }
}

/// <summary>Resultado sorteado: chance de sucesso = atributo/100 + bônus (GDD 15.3, "resultados com risco").</summary>
public sealed class ChoiceTest
{
    public required AttributeId Attribute { get; init; }

    public double Bonus { get; init; }

    public required string SuccessText { get; init; }

    public required Effects Success { get; init; }

    public required string FailureText { get; init; }

    public required Effects Failure { get; init; }
}

public sealed class EventChoice
{
    public required string Label { get; init; }

    public ChoiceRequirement? Requires { get; init; }

    public Effects Effects { get; init; } = new();

    public ChoiceTest? Test { get; init; }

    public string? Result { get; init; }
}

/// <summary>Evento escrito (GDD 7.5 e 15.5).</summary>
public sealed class EventDefinition
{
    public required string Id { get; init; }

    public required string Category { get; init; }

    public required List<string> Phases { get; init; }

    public required Rarity Rarity { get; init; }

    public required int CooldownWeeks { get; init; }

    public bool Unique { get; init; }

    /// <summary>Só acontece quando agendado por outro evento (continuação de cadeia).</summary>
    public bool ChainOnly { get; init; }

    public EventConditions Conditions { get; init; } = new();

    public required string Title { get; init; }

    /// <summary>De 1 a 4 versões do texto, para reduzir repetição.</summary>
    public required List<string> Variants { get; init; }

    public required List<EventChoice> Choices { get; init; }

    /// <summary>Retrato exibido: "rival", "jogador" ou null.</summary>
    public string? Portrait { get; init; }

    public Expression PortraitExpression { get; init; } = Expression.Neutra;
}

public sealed class EventFile : IValidatable
{
    public required List<EventDefinition> Events { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Events is null)
        {
            yield return "a lista 'events' está ausente.";
            yield break;
        }

        foreach (EventDefinition e in Events)
        {
            foreach (string error in EventValidation.Check(e))
            {
                yield return error;
            }
        }
    }
}

/// <summary>Conteúdo de data/events/config.json: pesos de raridade e eventos por semana por frequência.</summary>
public sealed class EventConfig : IValidatable
{
    public required Dictionary<Rarity, double> RarityWeights { get; init; }

    /// <summary>Eventos esperados por semana para cada frequência do mundo (baixa, normal, alta, caotica).</summary>
    public required Dictionary<string, double> EventsPerWeek { get; init; }

    public required int MaxPerWeek { get; init; }

    public IEnumerable<string> Validate()
    {
        if (RarityWeights is null || Enum.GetValues<Rarity>().Any(r => !RarityWeights.ContainsKey(r) || RarityWeights[r] <= 0))
        {
            yield return "rarity_weights precisa de peso positivo para todas as raridades.";
        }

        if (EventsPerWeek is null || new[] { "baixa", "normal", "alta", "caotica" }.Any(f => !EventsPerWeek.ContainsKey(f) || EventsPerWeek[f] < 0) || MaxPerWeek is < 0 or > 3)
        {
            yield return "events_per_week para baixa, normal, alta e caotica; max_per_week de 0 a 3 (GDD 15.3).";
        }
    }
}

public static class EventValidation
{
    public static readonly string[] Phases = ["pre_campanha", "campanha"];

    public static readonly string[] Variables =
    [
        "municipio.nome", "municipio.uf", "municipio.porte", "rival.nome", "rival.partido", "partido.sigla", "partido.nome",
        "jogador.nome", "jogador.pronome", "jogador.o", "ano", "valor", "semanas",
    ];

    public static IEnumerable<string> Check(EventDefinition e)
    {
        string where = $"evento '{e.Id}'";
        foreach (string error in DataChecks.Id(e.Id, where).Concat(DataChecks.Text(e.Title, "title", where)))
        {
            yield return error;
        }

        if (e.Variants is null || e.Variants.Count is < 1 or > 4 || e.Variants.Any(string.IsNullOrWhiteSpace))
        {
            yield return $"{where}: de 1 a 4 variantes de texto.";
        }

        if (e.Choices is null || e.Choices.Count is < 2 or > 4)
        {
            yield return $"{where}: de 2 a 4 escolhas (GDD 15.3).";
        }

        if (e.Phases is null || e.Phases.Count == 0 || e.Phases.Any(p => !Phases.Contains(p)))
        {
            yield return $"{where}: fases válidas são {string.Join(", ", Phases)}.";
        }

        if (e.CooldownWeeks < 0 || e.Conditions.Chance is <= 0 or > 1)
        {
            yield return $"{where}: cooldown não negativo e chance em (0, 1].";
        }
    }
}
