using Planalto.Core.Data;
using Planalto.Core.Parties;

namespace Planalto.Core.Tests.Parties;

/// <summary>Conteúdo real de data/, carregado uma vez para todos os testes.</summary>
internal static class TestContent
{
    private static readonly Lazy<PartyContent> Lazy = new(() => PartyContent.LoadOrThrow(DataDirectory));

    public static string DataDirectory =>
        DataPaths.FindDataDirectory(AppContext.BaseDirectory)
        ?? throw new InvalidOperationException("Pasta data/ não encontrada a partir dos testes.");

    public static PartyContent Parties => Lazy.Value;
}
