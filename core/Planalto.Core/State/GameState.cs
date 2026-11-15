using Planalto.Core.Characters;
using Planalto.Core.Ideology;
using Planalto.Core.Parties;
using Planalto.Core.World;

namespace Planalto.Core.State;

/// <summary>Partido em jogo: a identidade vem da tela "Novo Mundo"; o estado da carreira muda com o tempo.</summary>
public sealed class GameParty
{
    public required string Id { get; init; }

    public required string Name { get; set; }

    public required string Sigla { get; set; }

    public required int Number { get; set; }

    public required string FamilyId { get; set; }

    public required IdeologicalPosition Position { get; set; }

    public required PartySize Size { get; set; }

    public required PartyColors Colors { get; set; }

    public required string LogoIcon { get; set; }

    public required LeadershipStyle Leadership { get; set; }

    public PartyCharter? Charter { get; set; }

    public static GameParty From(PartyDraft draft) => new()
    {
        Id = draft.Id,
        Name = draft.Name,
        Sigla = draft.Sigla,
        Number = draft.Number,
        FamilyId = draft.FamilyId,
        Position = draft.Position,
        Size = draft.Size,
        Colors = draft.Colors,
        LogoIcon = draft.LogoIcon,
        Leadership = draft.Leadership,
        Charter = draft.Charter,
    };
}

/// <summary>Configuração do mundo escolhida na tela "Novo Mundo".</summary>
public sealed class WorldState
{
    public required string PresetId { get; init; }

    /// <summary>Cenário espelhado do real (ex.: "brasil_2026"), ou null num mundo sorteado.</summary>
    public string? ScenarioId { get; init; }

    public required WorldOptions Options { get; init; }

    /// <summary>Semente do mundo, para gerar de novo o que é derivado (ex.: eleitorado de um município).</summary>
    public required ulong Seed { get; init; }
}

public sealed class GameState
{
    public const int CurrentFormatVersion = 2;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required Calendar Calendar { get; init; }

    public required Rng Rng { get; init; }

    public required Character Player { get; init; }

    public WorldState? World { get; init; }

    public List<GameParty> Parties { get; init; } = [];

    /// <summary>Município em simulação detalhada (GDD 12.5); 0 antes da criação.</summary>
    public int FocusMunicipio { get; set; }

    /// <summary>NPCs gerados quando o município entra em foco, persistidos no save (GDD 12.5).</summary>
    public List<Character> Npcs { get; init; } = [];

    public GameParty? Party(string? id) => Parties.FirstOrDefault(p => p.Id == id);
}
