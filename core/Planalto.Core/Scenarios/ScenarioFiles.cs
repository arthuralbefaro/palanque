using Planalto.Core.Ideology;
using Planalto.Core.Parties;

namespace Planalto.Core.Scenarios;

/// <summary>Fonte oficial usada para gerar o cenário (sem nomes de partidos reais).</summary>
public sealed class ScenarioSource
{
    public required string Id { get; init; }

    public string? Url { get; init; }

    public required DateOnly ConsultedOn { get; init; }

    public required string Note { get; init; }
}

/// <summary>Pesquisa acadêmica que sustenta a família ideológica dos partidos espelhados (DECISOES D-001 a D-004).</summary>
public sealed class ScenarioIdeologySource
{
    public required string Citation { get; init; }

    public required string Doi { get; init; }

    public required string Url { get; init; }

    public required string Method { get; init; }
}

/// <summary>Partido fictício que espelha um partido real; só ids e números fictícios.</summary>
public sealed class ScenarioParty
{
    public const string SocialVariationNote = "variação sorteada, não derivada da fonte";

    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Sigla { get; init; }

    public required int Number { get; init; }

    public required string FamilyId { get; init; }

    public required IdeologicalPosition Position { get; init; }

    /// <summary>False quando a fonte não classifica o partido e família e posição foram sorteadas (D-003).</summary>
    public required bool Classified { get; init; }

    /// <summary>Escore esquerda–direita publicado (0 a 10), quando classificado.</summary>
    public double? SourceScore { get; init; }

    /// <summary>Variação sorteada somada ao eixo de costumes (D-004).</summary>
    public int? SocialVariation { get; init; }

    public string? SocialVariationOrigin { get; init; }

    public required PartySize Size { get; init; }

    public required PartyColors Colors { get; init; }

    public required string LogoIcon { get; init; }

    public required LeadershipStyle Leadership { get; init; }
}

public sealed class ScenarioFederation
{
    public required string Id { get; init; }

    public required List<string> Members { get; init; }
}

/// <summary>Composição de uma casa legislativa por partido.</summary>
public sealed class SeatTable
{
    public required int Seats { get; init; }

    public required Dictionary<string, int> ByParty { get; init; }

    /// <summary>Ano da eleição de onde veio a composição, quando diferente do cenário (D-005).</summary>
    public int? SourceYear { get; init; }
}

public sealed class ExecutiveSeat
{
    public required string Party { get; init; }

    public required string ViceParty { get; init; }
}

public sealed class SenatorSeat
{
    public required string Party { get; init; }

    public required int ElectedIn { get; init; }
}

/// <summary>Conteúdo de data/parties/scenarios/brasil_2026/nacional.json.</summary>
public sealed class NationalScenarioFile
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required string Id { get; init; }

    public required int GeneralElectionYear { get; init; }

    public required int MunicipalElectionYear { get; init; }

    public required List<ScenarioSource> Sources { get; init; }

    public required ScenarioIdeologySource Ideology { get; init; }

    public required List<ScenarioParty> Parties { get; init; }

    public required List<ScenarioFederation> Federations { get; init; }

    public required SeatTable Camara { get; init; }

    public required SeatTable Senado { get; init; }

    /// <summary>Votos válidos (nominais + legenda) para a Câmara na última eleição geral, por partido.</summary>
    public required Dictionary<string, long> CamaraVotes { get; init; }

    /// <summary>
    /// Total de votos válidos (nominais + legenda) para deputado federal na última eleição geral, somando todos os
    /// partidos (TSE, votacao_partido_munzona). Base da meta de apoio para fundar partido (Lei 9.096, art. 7º, § 1º).
    /// </summary>
    public required long CamaraValidVotes { get; init; }

    public required ExecutiveSeat Presidente { get; init; }

    public required List<string> BaseGovernista { get; init; }

    public required List<string> Oposicao { get; init; }

    /// <summary>Lacunas e substituições da fonte (UF sem resultado, município sem prefeito eleito etc.).</summary>
    public required List<ScenarioGap> Gaps { get; init; }
}

/// <summary>Lacuna da fonte oficial e a alternativa adotada.</summary>
public sealed class ScenarioGap
{
    public required string Kind { get; init; }

    public string? Uf { get; init; }

    public int? Ibge { get; init; }

    public required string Note { get; init; }
}

/// <summary>Fatos estruturais de um município (GDD 16.4), com percentuais em pontos-base (soma 10.000).</summary>
public sealed class MunicipalScenario
{
    public const int BasisPoints = 10_000;

    public required int Ibge { get; init; }

    /// <summary>Partido do prefeito; null quando a eleição foi anulada e a suplementar não tem resultado na fonte (D-011).</summary>
    public required string? Prefeito { get; init; }

    public required string? Vice { get; init; }

    /// <summary>Prefeito no 2º mandato consecutivo: inelegível para prefeito na eleição seguinte (D-007).</summary>
    public required bool PrefeitoReeleito { get; init; }

    /// <summary>Data da eleição que definiu o prefeito (a ordinária ou a suplementar mais recente); null se pendente.</summary>
    public required DateOnly? EleicaoPrefeito { get; init; }

    public required Dictionary<string, int> Camara { get; init; }

    /// <summary>Ano da eleição de onde veio a Câmara, quando não for a de 2024 (D-011).</summary>
    public int? CamaraFonteAno { get; init; }

    /// <summary>Partidos da coligação ou federação do prefeito.</summary>
    public required List<string> Base { get; init; }

    public required Dictionary<string, int> ForcaVereador { get; init; }

    public required Dictionary<string, int> ForcaPrefeito { get; init; }

    /// <summary>Coligações de prefeito com dois ou mais partidos.</summary>
    public required List<List<string>> Aliancas { get; init; }
}

/// <summary>Conteúdo de data/parties/scenarios/brasil_2026/{UF}.json.</summary>
public sealed class StateScenarioFile
{
    public int FormatVersion { get; init; } = NationalScenarioFile.CurrentFormatVersion;

    public required string Uf { get; init; }

    public required ExecutiveSeat Governador { get; init; }

    public required List<SenatorSeat> Senadores { get; init; }

    public required SeatTable Assembleia { get; init; }

    public required List<MunicipalScenario> Municipios { get; init; }
}
