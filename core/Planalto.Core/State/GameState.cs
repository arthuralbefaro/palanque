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

    /// <summary>Registro no TSE de um partido fundado durante a carreira; null para os que já existiam.</summary>
    public DateOnly? RegisteredOn { get; set; }

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

    /// <summary>Dia da semana dentro do calendário (tempo corrente, GDD 17.4).</summary>
    public TimeState Time { get; init; } = new();

    public required Rng Rng { get; init; }

    public required Character Player { get; init; }

    public WorldState? World { get; init; }

    public List<GameParty> Parties { get; init; } = [];

    /// <summary>Município em simulação detalhada (GDD 12.5); 0 antes da criação.</summary>
    public int FocusMunicipio { get; set; }

    /// <summary>NPCs gerados quando o município entra em foco, persistidos no save (GDD 12.5).</summary>
    public List<Character> Npcs { get; init; } = [];

    /// <summary>Campanha em curso; null fora de campanha.</summary>
    public Campaign.CampaignState? Campaign { get; set; }

    /// <summary>Mandato em curso; null fora de mandato (GDD 5.8).</summary>
    public Legislature.MandateState? Mandate { get; set; }

    /// <summary>Mandato de prefeito em curso; null fora dele (GDD 5.9).</summary>
    public Executive.ExecutiveState? Executive { get; set; }

    /// <summary>Governo da cidade durante o mandato do jogador: obras, secretários, folha, gabinete e cobranças (GDD 17.14 e 17.15).</summary>
    public Governance.GovernanceState? Governance { get; set; }

    /// <summary>Câmara e prefeitura do município em foco depois da última eleição jogada.</summary>
    public Legislature.LocalPolitics? Local { get; set; }

    /// <summary>A cidade em foco como lugar com gente: pessoas com memória e lideranças (GDD 17.2 e 17.10).</summary>
    public Campaign.CityState? City { get; set; }

    /// <summary>Livro de compromissos: promessas, dívidas, posições assumidas e esqueletos; atravessa eleições (GDD 17.2).</summary>
    public People.Ledger Ledger { get; init; } = new();

    /// <summary>Contas da última eleição disputada: julgamento e representação depois da apuração (GDD 17.12).</summary>
    public Campaign.AccountsCase? Accounts { get; set; }

    /// <summary>Fundação de partido em curso ou concluída (GDD 14.2 B).</summary>
    public Career.PartyFoundingState? Founding { get; set; }

    /// <summary>Indicadores do município em foco em unidade real, com o modelo de evolução (GDD 17.17); null em saves anteriores.</summary>
    public Indicators.MunicipalIndicatorsState? Indicators { get; set; }

    /// <summary>Renovação do mundo político do município em foco: cadeiras, suplentes, prefeito e vice, saídas e entradas (GDD 17.16); null em saves anteriores.</summary>
    public Renewal.RenewalState? Renewal { get; set; }

    /// <summary>Entressafra (fora de mandato e fora de campanha): presença por zona, turnos e posições; null fora dela.</summary>
    public Career.OffSeasonState? OffSeason { get; set; }

    public GameParty? Party(string? id) => Parties.FirstOrDefault(p => p.Id == id);
}
