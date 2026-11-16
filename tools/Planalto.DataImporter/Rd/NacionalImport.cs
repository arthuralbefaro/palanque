using System.Globalization;
using System.Text.Json;
using Planalto.Core.Economy;

namespace Planalto.DataImporter.Rd;

/// <summary>Séries nacionais de partida (GDD 17.17): Contas Nacionais e PNAD Contínua do IBGE e séries do SGS do Banco Central.</summary>
public static class NacionalImport
{
    public const int SgsIpca = 433;
    public const int SgsSelic = 432;
    public const int SgsCambio = 1;
    public const int SgsDividaBruta = 13762;
    public const int SgsDividaLiquida = 4513;
    public const int PibAno = 2024;

    public sealed record Result(EconomyStart Start, List<(string Campo, string Fonte, CachedFile File)> Fontes);

    public static string SgsUrl(int serie, DateOnly from, DateOnly to) =>
        string.Create(CultureInfo.InvariantCulture, $"https://api.bcb.gov.br/dados/serie/bcdata.sgs.{serie}/dados?formato=json&dataInicial={from:dd/MM/yyyy}&dataFinal={to:dd/MM/yyyy}");

    public static async Task<Result> ReadAsync(SourceCache cache, string desocupacaoPeriodo)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly from = today.AddMonths(-20);
        var fontes = new List<(string, string, CachedFile)>();

        async Task<List<(DateOnly Date, decimal Value)>> Sgs(int serie, string campo, string nome)
        {
            CachedFile file = await cache.GetAsync($"rd/bcb_sgs_{serie}.json", SgsUrl(serie, from, today));
            fontes.Add((campo, $"Banco Central do Brasil, SGS, série {serie} ({nome})", file));
            DateOnly limit = DateOnly.FromDateTime(file.DownloadedAt.UtcDateTime);
            using JsonDocument doc = JsonDocument.Parse(await File.ReadAllBytesAsync(file.Path));
            return doc.RootElement.EnumerateArray()
                .Select(e => (
                    Date: DateOnly.ParseExact(e.GetProperty("data").GetString()!, "dd/MM/yyyy", CultureInfo.InvariantCulture),
                    Value: decimal.Parse(e.GetProperty("valor").GetString()!, CultureInfo.InvariantCulture)))
                .Where(p => p.Date <= limit) // a série da Selic traz datas futuras (GDD 17.17)
                .OrderBy(p => p.Date)
                .ToList();
        }

        var ipca = await Sgs(SgsIpca, "ipca_12_meses", "IPCA, variação mensal");
        var selic = await Sgs(SgsSelic, "selic", "meta da taxa Selic");
        var cambio = await Sgs(SgsCambio, "cambio", "dólar comercial, venda");
        var bruta = await Sgs(SgsDividaBruta, "divida_bruta_pct_pib", "dívida bruta do governo geral, % do PIB");
        var liquida = await Sgs(SgsDividaLiquida, "divida_liquida_pct_pib", "dívida líquida do setor público, % do PIB");
        if (ipca.Count < 12 || selic.Count == 0 || cambio.Count == 0 || bruta.Count == 0 || liquida.Count == 0)
        {
            throw new ImportException("SGS: série sem valores suficientes no período consultado.");
        }

        decimal acumulado = ipca.TakeLast(12).Aggregate(1m, (acc, p) => acc * (1 + p.Value / 100m));

        string ano = PibAno.ToString(CultureInfo.InvariantCulture);
        CachedFile pibFile = await cache.GetAsync($"rd/sidra_{SidraRd.PibNacionalTable}_{ano}.json",
            SidraRd.Url(SidraRd.PibNacionalTable, ano, $"{SidraRd.PibNacionalValorVar}|{SidraRd.PibNacionalVolumeVar}", "N1[all]"));
        fontes.Add(("pib_milhoes, pib_variacao", $"IBGE, Sistema de Contas Nacionais, SIDRA {SidraRd.PibNacionalTable}", pibFile));
        var pib = SidraRd.Read(pibFile.Path, ano, null);

        CachedFile desFile = await cache.GetAsync($"rd/sidra_{SidraRd.DesocupacaoTable}_{desocupacaoPeriodo}.json",
            SidraRd.Url(SidraRd.DesocupacaoTable, desocupacaoPeriodo, SidraRd.DesocupacaoVar.ToString(CultureInfo.InvariantCulture), "N1[all]|N3[all]"));
        fontes.Add(("desocupacao", $"IBGE, PNAD Contínua trimestral, SIDRA {SidraRd.DesocupacaoTable}", desFile));
        var des = SidraRd.Read(desFile.Path, desocupacaoPeriodo, null);

        var start = new EconomyStart
        {
            Referencia = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            PibAno = PibAno,
            PibMilhoes = SidraRd.Number(pib[(SidraRd.PibNacionalValorVar, 0)]["1"]) ?? throw new ImportException("PIB nacional ausente."),
            PibVariacao = SidraRd.Number(pib[(SidraRd.PibNacionalVolumeVar, 0)]["1"]) ?? throw new ImportException("Variação do PIB ausente."),
            Ipca12Meses = Math.Round((acumulado - 1) * 100m, 2),
            IpcaMes = ipca[^1].Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            Selic = selic[^1].Value,
            Cambio = cambio[^1].Value,
            CambioData = cambio[^1].Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Desocupacao = SidraRd.Number(des[(SidraRd.DesocupacaoVar, 0)]["1"]) ?? throw new ImportException("Desocupação do Brasil ausente."),
            DesocupacaoPeriodo = int.Parse(desocupacaoPeriodo, CultureInfo.InvariantCulture),
            DividaBrutaPctPib = bruta[^1].Value,
            DividaLiquidaPctPib = liquida[^1].Value,
            DividaMes = bruta[^1].Date.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        };
        return new Result(start, fontes);
    }
}
