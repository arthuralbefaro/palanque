using System.Text.Json;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter;

public sealed record IbgeMunicipio(
    int Id,
    string Nome,
    int UfId,
    string UfSigla,
    string RegiaoSigla,
    RegiaoRef? Microrregiao,
    RegiaoRef? RegiaoImediata);

/// <summary>Leitura das respostas das APIs do IBGE (Localidades e Agregados/SIDRA).</summary>
public static class Ibge
{
    public const string LocalidadesUrl = "https://servicodados.ibge.gov.br/api/v1/localidades/municipios";
    public const string AgregadosUrl = "https://servicodados.ibge.gov.br/api/v3/agregados";

    public static string SidraUrl(int table, string period, int variable, string localidades, string? classificacao = null) =>
        $"{AgregadosUrl}/{table}/periodos/{period}/variaveis/{variable}?localidades={localidades}"
        + (classificacao is null ? string.Empty : $"&classificacao={classificacao}");

    public static List<IbgeMunicipio> ReadLocalidades(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        var result = new List<IbgeMunicipio>();

        foreach (JsonElement m in document.RootElement.EnumerateArray())
        {
            RegiaoRef? micro = ReadRef(m, "microrregiao");
            RegiaoRef? imediata = ReadRef(m, "regiao-imediata");

            // UF e região vêm da hierarquia que existir (região imediata cobre todos os municípios).
            JsonElement uf = m.TryGetProperty("regiao-imediata", out JsonElement ri) && ri.ValueKind == JsonValueKind.Object
                ? ri.GetProperty("regiao-intermediaria").GetProperty("UF")
                : m.GetProperty("microrregiao").GetProperty("mesorregiao").GetProperty("UF");

            result.Add(new IbgeMunicipio(
                m.GetProperty("id").GetInt32(),
                m.GetProperty("nome").GetString()!,
                uf.GetProperty("id").GetInt32(),
                uf.GetProperty("sigla").GetString()!,
                uf.GetProperty("regiao").GetProperty("sigla").GetString()!,
                micro,
                imediata));
        }

        return result;
    }

    /// <summary>Último período disponível de uma tabela do SIDRA, lido dos metadados.</summary>
    public static int ReadLastPeriod(string metadataPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(metadataPath));
        return document.RootElement.GetProperty("periodicidade").GetProperty("fim").GetInt32();
    }

    /// <summary>
    /// Lê uma consulta do SIDRA como localidade → (categoria da classificação informada → valor).
    /// Sem classificação, a categoria é 0. "-" no SIDRA significa zero; valores sigilosos ou
    /// ausentes ("X", "..", "...") ficam de fora e são listados em <paramref name="missing"/>.
    /// </summary>
    public static Dictionary<string, Dictionary<int, long>> ReadSidra(
        string path, string period, int? classificationId, List<string> missing)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
        {
            throw new ImportException($"{Path.GetFileName(path)}: resposta do SIDRA sem resultados.");
        }

        var result = new Dictionary<string, Dictionary<int, long>>(StringComparer.Ordinal);
        foreach (JsonElement resultado in document.RootElement[0].GetProperty("resultados").EnumerateArray())
        {
            int category = 0;
            if (classificationId is int classId)
            {
                JsonElement classification = resultado.GetProperty("classificacoes").EnumerateArray()
                    .First(c => int.Parse(c.GetProperty("id").GetString()!) == classId);
                category = int.Parse(classification.GetProperty("categoria").EnumerateObject().Single().Name);
            }

            foreach (JsonElement serie in resultado.GetProperty("series").EnumerateArray())
            {
                string localidade = serie.GetProperty("localidade").GetProperty("id").GetString()!;
                string raw = serie.GetProperty("serie").GetProperty(period).GetString() ?? string.Empty;

                long? value = raw == "-" ? 0 : long.TryParse(raw, out long parsed) ? parsed : null;
                if (value is null)
                {
                    missing.Add($"{Path.GetFileName(path)}: localidade {localidade}, categoria {category}: '{raw}'");
                    continue;
                }

                if (!result.TryGetValue(localidade, out Dictionary<int, long>? byCategory))
                {
                    byCategory = [];
                    result[localidade] = byCategory;
                }

                byCategory[category] = value.Value;
            }
        }

        return result;
    }

    private static RegiaoRef? ReadRef(JsonElement municipio, string property)
    {
        if (!municipio.TryGetProperty(property, out JsonElement element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new RegiaoRef { Id = element.GetProperty("id").GetInt32(), Nome = element.GetProperty("nome").GetString()! };
    }
}
