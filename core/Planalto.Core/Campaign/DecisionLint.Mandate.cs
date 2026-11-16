using Planalto.Core.Governance;

namespace Planalto.Core.Campaign;

/// <summary>
/// Extensão do lint das decisões para os mandatos (GDD 17.1, princípios d e e): instrumentos do vereador, saídas da
/// fiscalização, pedidos de voto e opções dos eventos de mandato passam pela mesma regra da campanha.
/// </summary>
public static partial class DecisionLint
{
    public static IReadOnlyList<string> Mandate(GovernanceContent content) =>
        [.. GovernanceLint.Instruments(content.Instruments), .. GovernanceLint.Events(content.Events)];

    /// <summary>Quantas opções de decisão de mandato o lint cobre.</summary>
    public static int MandateCount(GovernanceContent content) => GovernanceLint.Count(content);
}
