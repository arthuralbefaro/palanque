using Planalto.Core.Data;

namespace Planalto.Core.Economy;

/// <summary>
/// data/economy/start.json: números nacionais oficiais mais recentes na data da importação (GDD 17.17). São só o
/// ponto de partida; a partir do primeiro dia de jogo a economia evolui pela simulação.
/// </summary>
public sealed class EconomyStart : IValidatable
{
    public const string File = "economy/start.json";

    /// <summary>Data da importação (AAAA-MM-DD): nenhuma série usa valor posterior a ela.</summary>
    public required string Referencia { get; init; }

    public required int PibAno { get; init; }

    /// <summary>PIB a valores correntes, em milhões de reais (IBGE, Contas Nacionais).</summary>
    public required decimal PibMilhoes { get; init; }

    /// <summary>Variação anual do PIB em volume, em %.</summary>
    public required decimal PibVariacao { get; init; }

    /// <summary>IPCA acumulado nos 12 meses até <see cref="IpcaMes"/>, em % (BCB, SGS 433).</summary>
    public required decimal Ipca12Meses { get; init; }

    /// <summary>Último mês do IPCA usado (AAAA-MM).</summary>
    public required string IpcaMes { get; init; }

    /// <summary>Meta da taxa Selic, em % ao ano (BCB, SGS 432), vigente na data de referência.</summary>
    public required decimal Selic { get; init; }

    /// <summary>Dólar comercial de venda, em reais (BCB, SGS 1), na última cotação até a data de referência.</summary>
    public required decimal Cambio { get; init; }

    public required string CambioData { get; init; }

    /// <summary>Taxa de desocupação do Brasil, em % (IBGE, PNAD Contínua), no trimestre <see cref="DesocupacaoPeriodo"/>.</summary>
    public required decimal Desocupacao { get; init; }

    public required int DesocupacaoPeriodo { get; init; }

    /// <summary>Dívida bruta do governo geral, em % do PIB (BCB, SGS 13762).</summary>
    public required decimal DividaBrutaPctPib { get; init; }

    /// <summary>Dívida líquida do setor público, em % do PIB (BCB, SGS 4513).</summary>
    public required decimal DividaLiquidaPctPib { get; init; }

    public required string DividaMes { get; init; }

    public IEnumerable<string> Validate()
    {
        if (PibMilhoes <= 0)
        {
            yield return "pib_milhoes deve ser positivo.";
        }

        if (Selic <= 0 || Cambio <= 0 || Desocupacao <= 0 || DividaBrutaPctPib <= 0)
        {
            yield return "selic, cambio, desocupacao e divida_bruta_pct_pib devem ser positivos.";
        }
    }
}
