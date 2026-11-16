using System.Globalization;
using System.Text.Json;

namespace Planalto.DataImporter.Rd;

/// <summary>Consultas do SIDRA usadas na RD (GDD 17.17): PIB dos Municípios, saneamento do Censo 2022, PNAD Contínua e Contas Nacionais.</summary>
public static class SidraRd
{
    public const int PibTable = 5938;
    public const int PibVar = 37;
    public const int PibAgroVar = 513;
    public const int PibIndustriaVar = 517;
    public const int PibServicosVar = 6575;
    public const int PibAdmVar = 525;

    public const int AguaTable = 6803;
    public const int EsgotoTable = 6805;
    public const int DomiciliosVar = 381;
    public const int AguaClass = 1821;
    public const int AguaTotal = 72129;
    public static readonly int[] AguaLigada = [72144, 72145];
    public const int EsgotoClass = 11558;
    public const int EsgotoTotal = 46292;
    public const int EsgotoRede = 46290;

    public const int DesocupacaoTable = 4099;
    public const int DesocupacaoVar = 4099;

    public const int PibNacionalTable = 6784;
    public const int PibNacionalValorVar = 9808;
    public const int PibNacionalVolumeVar = 9810;

    public static string Url(int table, string period, string variables, string localidades, string? classificacao = null) =>
        $"{Ibge.AgregadosUrl}/{table}/periodos/{period}/variaveis/{variables}?localidades={localidades}"
        + (classificacao is null ? string.Empty : $"&classificacao={classificacao}");

    /// <summary>
    /// Lê uma resposta com uma ou mais variáveis: (variável, categoria) → localidade → texto publicado.
    /// A categoria é a da classificação informada, ou 0 sem classificação.
    /// </summary>
    public static Dictionary<(int Variable, int Category), Dictionary<string, string>> Read(string path, string period, int? classificationId)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            throw new ImportException($"{Path.GetFileName(path)}: resposta do SIDRA sem resultados.");
        }

        var result = new Dictionary<(int, int), Dictionary<string, string>>();
        foreach (JsonElement variable in document.RootElement.EnumerateArray())
        {
            int variableId = int.Parse(variable.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);
            foreach (JsonElement resultado in variable.GetProperty("resultados").EnumerateArray())
            {
                int category = 0;
                if (classificationId is int classId)
                {
                    JsonElement classification = resultado.GetProperty("classificacoes").EnumerateArray()
                        .First(c => int.Parse(c.GetProperty("id").GetString()!, CultureInfo.InvariantCulture) == classId);
                    category = int.Parse(classification.GetProperty("categoria").EnumerateObject().Single().Name, CultureInfo.InvariantCulture);
                }

                if (!result.TryGetValue((variableId, category), out Dictionary<string, string>? byLocal))
                {
                    byLocal = new Dictionary<string, string>(StringComparer.Ordinal);
                    result[(variableId, category)] = byLocal;
                }

                foreach (JsonElement serie in resultado.GetProperty("series").EnumerateArray())
                {
                    byLocal[serie.GetProperty("localidade").GetProperty("id").GetString()!] =
                        serie.GetProperty("serie").GetProperty(period).GetString() ?? string.Empty;
                }
            }
        }

        return result;
    }

    /// <summary>Inteiro publicado; "-" é zero; sigilo ou ausência ("X", "..", "...") é null.</summary>
    public static long? Whole(string raw) =>
        raw == "-" ? 0 : long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long v) ? v : null;

    public static decimal? Number(string raw) =>
        raw == "-" ? 0 : decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal v) ? v : null;
}
