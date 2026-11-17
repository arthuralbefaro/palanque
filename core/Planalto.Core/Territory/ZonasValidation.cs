using System.Text.Json;
using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.Core.Territory;

/// <summary>
/// DataValidator de data/zonas/: cada arquivo de UF (fonte do nome conhecida, ids e vizinhas) e, se alguma zona tem nome
/// do OpenStreetMap, o registro da base derivada em _fontes.json (D-058, licença ODbL: fonte, licença, data da extração
/// e contagem igual à dos arquivos).
/// </summary>
public static class ZonasValidation
{
    public const string FontesFile = "_fontes.json";

    public static IEnumerable<DataFileReport> Validate(string dataDirectory, MunicipiosDataSet dataSet)
    {
        int osm = 0;
        foreach (string uf in dataSet.All.Select(m => m.Uf).Distinct().Order(StringComparer.Ordinal))
        {
            string file = $"{ZonasPaths.Directory}/{uf}.json";
            DataLoadResult<ZonasUf> result = ZonasPaths.Load(dataDirectory, uf);
            osm += result.Value?.Municipios.Sum(m => m.Zonas.Count(z => z.NomeFonte == Zona.NomeOsm)) ?? 0;
            yield return new DataFileReport(file, result.Value?.Municipios.Sum(m => m.Zonas.Count) ?? 0, result.Errors);
        }

        string fontes = Path.Combine(dataDirectory, ZonasPaths.Directory, FontesFile);
        yield return new DataFileReport($"{ZonasPaths.Directory}/{FontesFile}", osm, File.Exists(fontes) ? CheckOsmRecord(File.ReadAllText(fontes), osm).ToList() : ["arquivo não encontrado."]);
    }

    /// <summary>Erros do registro do OpenStreetMap em _fontes.json, dado o número de zonas com nome de lá.</summary>
    public static IEnumerable<string> CheckOsmRecord(string fontesJson, int zonasOsm)
    {
        if (zonasOsm == 0)
        {
            yield break;
        }

        using JsonDocument doc = JsonDocument.Parse(fontesJson);
        if (!doc.RootElement.TryGetProperty("openstreetmap", out JsonElement o) || o.ValueKind != JsonValueKind.Object)
        {
            yield return $"{zonasOsm} zonas com nome do OpenStreetMap, mas sem o bloco \"openstreetmap\" (fonte, licença ODbL e data da extração).";
            yield break;
        }

        string? Text(string key) => o.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        if (Text("licenca") is not { } licenca || !licenca.Contains("ODbL", StringComparison.Ordinal))
        {
            yield return "openstreetmap.licenca: falta a licença ODbL.";
        }

        if (Text("credito") != ZoneNameOrigin.CreditoOsm || string.IsNullOrWhiteSpace(Text("url")) || string.IsNullOrWhiteSpace(Text("extraido_em")))
        {
            yield return "openstreetmap: falta o crédito, a URL da fonte ou a data da extração.";
        }

        if (!o.TryGetProperty("zonas_com_nome_osm", out JsonElement count) || !count.TryGetInt32(out int registered) || registered != zonasOsm)
        {
            yield return $"openstreetmap.zonas_com_nome_osm não confere com os arquivos ({zonasOsm} zonas com nome_fonte {Zona.NomeOsm}).";
        }
    }
}
