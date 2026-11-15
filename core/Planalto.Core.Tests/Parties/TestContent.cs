using Planalto.Core.Data;
using Planalto.Core.Parties;
using Planalto.Core.World;

namespace Planalto.Core.Tests.Parties;

/// <summary>Conteúdo real de data/, carregado uma vez para todos os testes.</summary>
internal static class TestContent
{
    private static readonly Lazy<WorldContent> LazyWorld = new(() => WorldContent.LoadOrThrow(DataDirectory));

    public static WorldContent World => LazyWorld.Value;

    private static readonly Lazy<PartyContent> Lazy = new(() => World.Parties);

    public static string DataDirectory =>
        DataPaths.FindDataDirectory(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("Pasta data/ não encontrada a partir dos testes.");

    public static PartyContent Parties => Lazy.Value;
}
