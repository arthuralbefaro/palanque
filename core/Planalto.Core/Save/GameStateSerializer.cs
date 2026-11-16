using System.Text.Json;
using Planalto.Core.Data;
using Planalto.Core.State;

namespace Planalto.Core.Save;

public sealed class SaveFormatException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Salva e carrega o GameState inteiro em JSON, com versão de formato.</summary>
public static class GameStateSerializer
{
    private const string VersionProperty = "format_version";

    public static string Serialize(GameState state) => JsonSerializer.Serialize(state, JsonData.Options);

    public static GameState Deserialize(string json)
    {
        int version = ReadVersion(json);
        if (version > GameState.CurrentFormatVersion)
        {
            throw new SaveFormatException(
                $"Save na versão {version}, mais nova que a suportada ({GameState.CurrentFormatVersion}).");
        }

        // Versões antigas serão migradas aqui quando o formato mudar.
        if (version < GameState.CurrentFormatVersion)
        {
            throw new SaveFormatException($"Versão de save {version} não suportada.");
        }

        // Ids de zona não são migrados: save de outra divisão em zonas não abre (sem o campo, é da primeira).
        if ((ReadInt(json, ZonasVersionProperty) ?? 1) != Territory.ZonasPaths.Versao)
        {
            throw new SaveFormatException(SaveZoneCheck.Mensagem);
        }

        try
        {
            return JsonSerializer.Deserialize<GameState>(json, JsonData.Options)
                ?? throw new SaveFormatException("Save vazio.");
        }
        catch (JsonException ex)
        {
            throw new SaveFormatException($"Save corrompido: {ex.Message}", ex);
        }
    }

    private const string ZonasVersionProperty = "zonas_versao";

    private static int ReadVersion(string json) =>
        ReadInt(json, VersionProperty) ?? throw new SaveFormatException($"Save sem o campo '{VersionProperty}'.");

    private static int? ReadInt(string json, string property)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(property, out JsonElement element)
                && element.TryGetInt32(out int value) ? value : null;
        }
        catch (JsonException ex)
        {
            throw new SaveFormatException($"Save corrompido: {ex.Message}", ex);
        }
    }
}
