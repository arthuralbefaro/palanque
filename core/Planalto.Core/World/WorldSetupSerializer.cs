using System.Text.Json;
using Planalto.Core.Data;

namespace Planalto.Core.World;

/// <summary>Formato do cenário salvo (user://scenarios/ na UI).</summary>
public sealed class WorldSetupFile
{
    public int FormatVersion { get; init; } = WorldSetupSerializer.CurrentFormatVersion;

    public required string PresetId { get; init; }

    public required int NextSerial { get; init; }

    public required WorldOptions Options { get; init; }

    public required List<PartyDraft> Parties { get; init; }
}

public enum ScenarioFileIssue
{
    JsonInvalido,
    VersaoMaisNova,
    VersaoNaoSuportada,
    ConteudoIncompleto,
    IdsDePartidoRepetidos,
}

/// <summary>
/// Resultado da leitura. <see cref="Setup"/> null quando o arquivo não pôde ser lido; a validade do mundo
/// carregado (siglas, números etc.) vem de <see cref="WorldSetup.Validate"/>.
/// </summary>
public sealed record WorldSetupLoadResult(WorldSetup? Setup, ScenarioFileIssue? Issue, string? Detail = null);

/// <summary>Salva e carrega cenários da tela "Novo Mundo", com versão de formato e travas.</summary>
public static class WorldSetupSerializer
{
    public const int CurrentFormatVersion = 1;

    public static string Serialize(WorldSetup setup)
    {
        var file = new WorldSetupFile
        {
            PresetId = setup.PresetId,
            NextSerial = setup.NextSerial,
            Options = setup.Options.Clone(),
            Parties = setup.Parties.Select(p => p.Clone(p.Id)).ToList(),
        };
        return JsonSerializer.Serialize(file, JsonData.Options);
    }

    public static WorldSetupLoadResult Deserialize(string json, WorldContent content)
    {
        WorldSetupFile? file;
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("format_version", out JsonElement versionElement)
                || !versionElement.TryGetInt32(out int version))
            {
                return new WorldSetupLoadResult(null, ScenarioFileIssue.ConteudoIncompleto, "format_version");
            }

            if (version > CurrentFormatVersion)
            {
                return new WorldSetupLoadResult(null, ScenarioFileIssue.VersaoMaisNova, version.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            // Versões antigas serão migradas aqui quando o formato mudar.
            if (version < CurrentFormatVersion)
            {
                return new WorldSetupLoadResult(null, ScenarioFileIssue.VersaoNaoSuportada, version.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            file = document.RootElement.Deserialize<WorldSetupFile>(JsonData.Options);
        }
        catch (JsonException ex)
        {
            return new WorldSetupLoadResult(null, ScenarioFileIssue.JsonInvalido, ex.Message);
        }

        if (file?.Parties is null || file.Options is null || file.Parties.Any(p => p is null))
        {
            return new WorldSetupLoadResult(null, ScenarioFileIssue.ConteudoIncompleto);
        }

        string? duplicate = file.Parties.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1)?.Key;
        if (duplicate is not null)
        {
            return new WorldSetupLoadResult(null, ScenarioFileIssue.IdsDePartidoRepetidos, duplicate);
        }

        // Garante que ids novos nunca colidam com os salvos, mesmo num arquivo editado à mão.
        int highest = file.Parties
            .Select(p => p.Id.Length > 1 && p.Id[0] == 'p' && int.TryParse(p.Id.AsSpan(1), out int n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
        int nextSerial = Math.Max(file.NextSerial, highest + 1);

        return new WorldSetupLoadResult(WorldSetup.Restore(content, file.PresetId, file.Options, file.Parties, nextSerial), null);
    }
}
