using System.Text.Json;

namespace Planalto.DataImporter;

public sealed record CachedFile(string Path, string Url, DateTimeOffset DownloadedAt);

/// <summary>Download recusado ou falho; o importador para e pede o download manual.</summary>
public sealed class DownloadBlockedException(string url, string fileName, string targetPath, string reason)
    : Exception($"Download não concluído ({reason}).")
{
    public string Url { get; } = url;

    public string FileName { get; } = fileName;

    public string TargetPath { get; } = targetPath;
}

/// <summary>
/// Baixa cada fonte uma única vez para tools/Planalto.DataImporter/cache/. Se o arquivo já está lá
/// (inclusive colocado à mão), é reaproveitado. A data de download fica num .meta.json ao lado.
/// </summary>
public sealed class SourceCache(string directory, HttpClient http)
{
    private sealed record Meta(string Url, DateTimeOffset DownloadedAt, long Bytes);

    private static readonly JsonSerializerOptions MetaOptions = new() { WriteIndented = true };

    public string Directory { get; } = directory;

    public async Task<CachedFile> GetAsync(string fileName, string url)
    {
        System.IO.Directory.CreateDirectory(Directory);
        string path = Path.Combine(Directory, fileName);
        string metaPath = path + ".meta.json";

        if (File.Exists(path))
        {
            Meta? meta = File.Exists(metaPath)
                ? JsonSerializer.Deserialize<Meta>(await File.ReadAllTextAsync(metaPath), MetaOptions)
                : null;

            // Arquivo colocado à mão, sem .meta.json: a data de download é a do próprio arquivo.
            return new CachedFile(path, meta?.Url ?? url, meta?.DownloadedAt ?? File.GetLastWriteTimeUtc(path));
        }

        Console.WriteLine($"  baixando {fileName}");
        string partial = path + ".part";
        try
        {
            using HttpResponseMessage response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                throw new DownloadBlockedException(url, fileName, path, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            await using (FileStream output = File.Create(partial))
            {
                await response.Content.CopyToAsync(output);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            File.Delete(partial);
            throw new DownloadBlockedException(url, fileName, path, ex.Message);
        }
        catch (DownloadBlockedException)
        {
            File.Delete(partial);
            throw;
        }

        File.Move(partial, path);
        var downloaded = new Meta(url, DateTimeOffset.UtcNow, new FileInfo(path).Length);
        await File.WriteAllTextAsync(metaPath, JsonSerializer.Serialize(downloaded, MetaOptions));
        return new CachedFile(path, url, downloaded.DownloadedAt);
    }
}
