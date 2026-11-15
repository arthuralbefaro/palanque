namespace Planalto.Core.World;

public enum WorldIssueSeverity
{
    Error,
    Warning,
}

/// <summary>Problemas da configuração do mundo. A UI traduz cada código com Tr().</summary>
public enum WorldIssueCode
{
    // Erros
    PresetDesconhecida,
    PresetIndisponivel,
    QuantidadeDePartidosForaDaFaixa,
    NomeVazio,
    NomeBloqueado,
    NomeRepetido,
    SiglaComTamanhoInvalido,
    SiglaComCaracteresInvalidos,
    SiglaBloqueada,
    SiglaBloqueadaPorOrdemDasLetras,
    SiglaRepetida,
    NumeroForaDaFaixa,
    NumeroRepetido,
    FamiliaDesconhecida,
    PosicaoForaDosEixos,
    CorInvalida,
    LogotipoDesconhecido,
    AnoInicialInvalido,
    DificuldadeDesconhecida,

    // Avisos
    EspectroSemEsquerdaEconomica,
    EspectroSemDireitaEconomica,
    PosicaoForaDaFaixaDaFamilia,
    CoresParecidas,
    NenhumPartidoPrincipal,
    DominantesDemais,
    NanicoGrandeDemais,
}

/// <param name="PartyId">Partido envolvido, quando houver.</param>
/// <param name="OtherPartyId">Segundo partido, nos problemas de repetição.</param>
/// <param name="Detail">Valor que causou o problema (sigla, número, nome real bloqueado etc.).</param>
public sealed record WorldIssue(
    WorldIssueCode Code,
    WorldIssueSeverity Severity,
    string? PartyId = null,
    string? OtherPartyId = null,
    string? Detail = null);

/// <summary>Resultado da validação: erros impedem começar o jogo, avisos não.</summary>
public sealed class WorldValidationReport(IReadOnlyList<WorldIssue> issues)
{
    public IReadOnlyList<WorldIssue> Issues { get; } = issues;

    public IReadOnlyList<WorldIssue> Errors => Issues.Where(i => i.Severity == WorldIssueSeverity.Error).ToList();

    public IReadOnlyList<WorldIssue> Warnings => Issues.Where(i => i.Severity == WorldIssueSeverity.Warning).ToList();

    public bool IsValid => Issues.All(i => i.Severity != WorldIssueSeverity.Error);

    public bool Has(WorldIssueCode code) => Issues.Any(i => i.Code == code);
}
