using Planalto.Core.Campaign;

namespace Planalto.Core.Events;

public sealed record EventLogEntry(DateOnly Week, string EventId, string? Choice, string Text);

/// <summary>Evento sorteado que espera a decisão do jogador, com o texto já resolvido.</summary>
public sealed class PendingEvent
{
    public required string EventId { get; init; }

    public required string Title { get; init; }

    public required string Text { get; init; }

    /// <summary>Candidato rival citado ({rival.nome}), se houver.</summary>
    public string? RivalId { get; init; }

    /// <summary>Variáveis resolvidas, para os textos dos resultados.</summary>
    public required Dictionary<string, string> Variables { get; init; }
}

public sealed record DueEvent(string EventId, DateOnly Due);

/// <summary>Memória do motor de eventos (GDD 15.3): histórico, cooldowns, flags, cadeias e esqueletos.</summary>
public sealed class EventState
{
    public List<EventLogEntry> History { get; init; } = [];

    public Dictionary<string, DateOnly> LastFired { get; init; } = [];

    public HashSet<string> Flags { get; init; } = [];

    public List<DueEvent> Scheduled { get; init; } = [];

    public List<PendingEvent> Pending { get; init; } = [];

    public List<Skeleton> Skeletons { get; init; } = [];

    /// <summary>Relação com NPCs (id → -100 a 100): eles lembram favores e traições.</summary>
    public Dictionary<string, int> Relations { get; init; } = [];
}
