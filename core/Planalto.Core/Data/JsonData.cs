using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planalto.Core.Data;

/// <summary>Leitura genérica dos JSON de data/, com validação na carga.</summary>
public static class JsonData
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static DataLoadResult<T> Parse<T>(string json, string sourceName)
        where T : class, IValidatable
    {
        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException ex)
        {
            return new DataLoadResult<T>(null, [$"{sourceName}: JSON inválido. {ex.Message}"]);
        }

        if (value is null)
        {
            return new DataLoadResult<T>(null, [$"{sourceName}: o arquivo está vazio ou contém null."]);
        }

        var errors = value.Validate().Select(error => $"{sourceName}: {error}").ToList();
        return new DataLoadResult<T>(value, errors);
    }

    public static DataLoadResult<T> LoadFile<T>(string path)
        where T : class, IValidatable
    {
        string sourceName = Path.GetFileName(path);
        if (!File.Exists(path))
        {
            return new DataLoadResult<T>(null, [$"{sourceName}: arquivo não encontrado em {path}."]);
        }

        return Parse<T>(File.ReadAllText(path), sourceName);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            AllowTrailingCommas = false,
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
