namespace Planalto.Core.Campaign;

/// <summary>Consultas só de leitura que a tela central usa para explicar um bloqueio; não mudam o estado.</summary>
public sealed partial class CampaignEngine
{
    /// <summary>
    /// Verba de Rua que vale para um compromisso na data e na zona (a paga, nesta semana; a do plano, nas seguintes):
    /// é a que o comício compara com <see cref="ComicioRuaMinimum"/>.
    /// </summary>
    public decimal RuaBudgetFor(DateOnly date, string zoneId) => RuaBudgetOn(date, zoneId);
}
