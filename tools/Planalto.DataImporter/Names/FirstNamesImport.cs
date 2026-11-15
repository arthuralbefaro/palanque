using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter.Names;

/// <summary>
/// Modo "nomes": os 20 prenomes mais frequentes por sexo, no Brasil e em cada UF (IBGE, Censo 2010,
/// API de nomes), usados nos NPCs (GDD 3.8). Grava data/names/first_names.json.
/// </summary>
public static class FirstNamesImport
{
    public const string BaseUrl = "https://servicodados.ibge.gov.br/api/v2/censos/nomes/ranking";

    public const string OutputFile = "names/first_names.json";

    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory)
    {
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        MunicipiosDataSet municipios = MunicipiosDataSet.Load(dataDirectory, portes).DataSet
            ?? throw new ImportException("data/municipios/ inválido; rode o importador de municípios antes.");
        var ufs = municipios.All.GroupBy(m => m.Uf).ToDictionary(g => g.Key, g => g.First().CodigoIbge / 100_000);

        async Task<List<RankedName>> Fetch(string sexo, int? ufCode)
        {
            string url = ufCode is null ? $"{BaseUrl}?sexo={sexo}" : $"{BaseUrl}?sexo={sexo}&localidade={ufCode}";
            string file = $"ibge_nomes_{sexo}_{(ufCode is null ? "BR" : ufCode.Value.ToString(CultureInfo.InvariantCulture))}.json";
            CachedFile cached = await cache.GetAsync(file, url);
            using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(cached.Path));
            return doc.RootElement[0].GetProperty("res").EnumerateArray()
                .Select(r => new RankedName { Nome = r.GetProperty("nome").GetString()!, Frequencia = r.GetProperty("frequencia").GetInt64() })
                .ToList();
        }

        var output = new FirstNamesFile
        {
            Fonte = "IBGE, Censo Demográfico 2010, API de nomes (ranking dos prenomes mais frequentes por sexo e UF).",
            Url = BaseUrl,
            ConsultadoEm = DateOnly.FromDateTime(DateTime.UtcNow),
            Brasil = new NamesBySex { Masculino = await Fetch("M", null), Feminino = await Fetch("F", null) },
            Ufs = [],
        };

        foreach (var (uf, code) in ufs.OrderBy(u => u.Key, StringComparer.Ordinal))
        {
            output.Ufs[uf] = new NamesBySex { Masculino = await Fetch("M", code), Feminino = await Fetch("F", code) };
        }

        var errors = output.Validate().ToList();
        if (errors.Count > 0)
        {
            throw new ImportException(string.Join("; ", errors));
        }

        var options = new JsonSerializerOptions(JsonData.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        string path = Path.Combine(dataDirectory, OutputFile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(output, options).ReplaceLineEndings("\n") + "\n");
        Console.WriteLine($"{output.Ufs.Count} UFs gravadas em {path}");
        return 0;
    }
}
