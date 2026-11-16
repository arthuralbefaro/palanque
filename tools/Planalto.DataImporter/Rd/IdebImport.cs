using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Planalto.DataImporter.Rd;

/// <summary>
/// IDEB dos anos iniciais do ensino fundamental por município e rede (INEP). A planilha da edição mais recente traz
/// a série inteira (colunas VL_OBSERVADO_AAAA); só a rede municipal é lida. Células sem nota ("-", "ND" e marcas)
/// ficam de fora, e o município sem nota segue a regra de substituição do GDD 17.17.
/// </summary>
public static partial class IdebImport
{
    public const int Edicao = 2025;
    public const string Url = "https://download.inep.gov.br/ideb/resultados/divulgacao_anos_iniciais_municipios_2025.zip";
    public const string CacheName = "rd/ideb_anos_iniciais_municipios_2025.zip";
    private const string Rede = "Municipal";
    private const string RedeEstadual = "Estadual";

    public static async Task<IdebInput> ReadAsync(SourceCache cache)
    {
        CachedFile file = await cache.GetAsync(CacheName, Url);
        using ZipArchive zip = ZipFile.OpenRead(file.Path);
        ZipArchiveEntry entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            ?? throw new ImportException($"IDEB: nenhum .xlsx em {file.Path}.");

        // O leitor de .xlsx precisa de um fluxo com posição: a planilha (12 MB) vai para a memória.
        using var buffer = new MemoryStream();
        await using (Stream input = entry.Open())
        {
            await input.CopyToAsync(buffer);
        }

        buffer.Position = 0;
        string sheet = Xlsx.SheetNames(buffer)[0];
        buffer.Position = 0;

        var result = new Dictionary<int, Dictionary<int, decimal>>();
        var estadual = new Dictionary<int, Dictionary<int, decimal>>();
        int codeColumn = -1;
        int redeColumn = -1;
        var yearColumns = new List<(int Column, int Year)>();
        foreach (string?[] row in Xlsx.ReadRows(buffer, sheet))
        {
            if (codeColumn < 0)
            {
                // A linha dos nomes técnicos das colunas é a que começa por SG_UF.
                if (row.Length > 0 && row[0] == "SG_UF")
                {
                    codeColumn = Array.IndexOf(row, "CO_MUNICIPIO");
                    redeColumn = Array.IndexOf(row, "REDE");
                    for (int c = 0; c < row.Length; c++)
                    {
                        if (row[c] is { } name && Observado().Match(name) is { Success: true } m)
                        {
                            yearColumns.Add((c, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)));
                        }
                    }

                    if (codeColumn < 0 || redeColumn < 0 || yearColumns.All(y => y.Year != Edicao))
                    {
                        throw new ImportException("IDEB: colunas CO_MUNICIPIO, REDE e VL_OBSERVADO da edição não encontradas.");
                    }
                }

                continue;
            }

            if (row.Length <= redeColumn || row[redeColumn] is not (Rede or RedeEstadual) || !int.TryParse(row[codeColumn], NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
            {
                continue;
            }

            var serie = new Dictionary<int, decimal>();
            foreach (var (column, year) in yearColumns)
            {
                if (column < row.Length && decimal.TryParse(row[column], NumberStyles.Float, CultureInfo.InvariantCulture, out decimal nota))
                {
                    serie[year] = Math.Round(nota, 1);
                }
            }

            if (serie.Count > 0)
            {
                (row[redeColumn] == Rede ? result : estadual)[code] = serie;
            }
        }

        if (codeColumn < 0)
        {
            throw new ImportException("IDEB: linha de cabeçalho (SG_UF) não encontrada.");
        }

        return new IdebInput(Edicao, result, estadual, file);
    }

    [GeneratedRegex(@"^VL_OBSERVADO_(\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex Observado();
}
