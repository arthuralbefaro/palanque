using Planalto.Core.Characters;

namespace Planalto.Core.State;

public sealed class GameState
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required Calendar Calendar { get; init; }

    public required Rng Rng { get; init; }

    public required Character Player { get; init; }
}
