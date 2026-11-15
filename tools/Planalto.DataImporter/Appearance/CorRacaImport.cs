using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter.Appearance;

/// <summary>
/// Modo "aparencia": população por cor ou raça e UF do Censo 2022 (SIDRA, tabela 9605), usada para os pesos
/// regionais dos tons de pele dos NPCs (GDD 3.8, DECISOES D-016).
/// </summary>
public static class CorRacaImport
{
    public const string Url = "https://apisidra.ibge.gov.br/values/t/9605/n3/all/v/93/p/2022/c86/allxt";

    public const string OutputFile = "appearance/cor_raca_uf.json";

    // Códigos das categorias da classificação 86 (identificadores da tabela, não dados).
    private static readonly Dictionary<string, string> Categories = new(StringComparer.Ordinal)
    {
        ["2776"] = "branca",
        ["2777"] = "preta",
        ["2778"] = "amarela",
        ["2779"] = "parda",
        ["2780"] = "indigena",
    };

    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory)
    {
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        MunicipiosDataSet municipios = MunicipiosDataSet.Load(dataDirectory, portes).DataSet
            ?? throw new ImportException("data/municipios/ inválido; rode o importador de municípios antes.");
        var ufPorCodigo = municipios.All
            .GroupBy(m => m.CodigoIbge / 100_000)
            .ToDictionary(g => g.Key.ToString(CultureInfo.InvariantCulture), g => g.First().Uf, StringComparer.Ordinal);

        CachedFile file = await cache.GetAsync("sidra_9605_cor_raca_ufs.json", Url);
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(file.Path));

        var ufs = new SortedDictionary<string, Dictionary<string, long>>(StringComparer.Ordinal);
        foreach (JsonElement row in document.RootElement.EnumerateArray().Skip(1))
        {
            string uf = ufPorCodigo[row.GetProperty("D1C").GetString()!];
            string code = row.GetProperty("D4C").GetString()!;
            if (!Categories.TryGetValue(code, out string? category))
            {
                continue;
            }

            string value = row.GetProperty("V").GetString()!;
            if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long count))
            {
                throw new ImportException($"SIDRA 9605: valor '{value}' sem número para {uf}/{category}.");
            }

            ufs.TryAdd(uf, []);
            ufs[uf][category] = count;
        }

        var output = new CorRacaUfFile
        {
            Fonte = "IBGE, Censo Demográfico 2022, tabela 9605 do SIDRA (população residente por cor ou raça).",
            Url = Url,
            ConsultadoEm = DateOnly.FromDateTime(file.DownloadedAt.UtcDateTime),
            Ufs = new Dictionary<string, Dictionary<string, long>>(ufs),
        };

        string error = string.Join("; ", output.Validate());
        if (error.Length > 0)
        {
            throw new ImportException(error);
        }

        var options = new JsonSerializerOptions(JsonData.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        string path = Path.Combine(dataDirectory, OutputFile);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(output, options).ReplaceLineEndings("\n") + "\n");
        Console.WriteLine($"{ufs.Count} UFs gravadas em {path}");
        return 0;
    }
}
