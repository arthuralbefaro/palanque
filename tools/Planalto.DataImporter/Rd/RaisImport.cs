using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Planalto.DataImporter.Rd;

/// <summary>
/// Estoque de emprego formal por município (MTE, RAIS, microdados de estabelecimentos): soma de "Qtd Vínculos Ativos"
/// em 31/12. O bruto (.7z, 144 MB; 1,4 GB extraído) fica só no cache (decisão do autor, GDD 17.20, item 18).
/// O .7z é aberto pelo tar do sistema (bsdtar), que acompanha o Windows 10 ou mais novo, o macOS e a maioria dos Linux.
/// </summary>
public static class RaisImport
{
    public const int Ano = 2025;
    public const string Url = "ftp://ftp.mtps.gov.br/pdet/microdados/RAIS/2025/RAIS_ESTAB_PUB.7z";
    public const string CacheName = "rd/RAIS_ESTAB_PUB_2025.7z";
    private const string ExtractedFolder = "rais_estab_2025";
    private const string AggregateName = "rais_estab_2025_agregado.json";

    public sealed record Result(Dictionary<int, long> VinculosPorMunicipio6, long Estabelecimentos, CachedFile Fonte);

    public static async Task<Result> ReadAsync(SourceCache cache)
    {
        CachedFile archive = await cache.GetAsync(CacheName, Url);
        string rd = Path.GetDirectoryName(archive.Path)!;
        string aggregate = Path.Combine(rd, AggregateName);
        if (File.Exists(aggregate) && File.GetLastWriteTimeUtc(aggregate) >= File.GetLastWriteTimeUtc(archive.Path))
        {
            var saved = JsonSerializer.Deserialize<Dictionary<string, long>>(await File.ReadAllTextAsync(aggregate))!;
            long estabs = saved["_estabelecimentos"];
            return new Result(
                saved.Where(p => p.Key[0] != '_').ToDictionary(p => int.Parse(p.Key, CultureInfo.InvariantCulture), p => p.Value),
                estabs,
                archive);
        }

        string folder = Path.Combine(rd, ExtractedFolder);
        string? extracted = Directory.Exists(folder) ? Directory.EnumerateFiles(folder).FirstOrDefault() : null;
        if (extracted is null)
        {
            Directory.CreateDirectory(folder);
            Extract(archive, folder);
            extracted = Directory.EnumerateFiles(folder).FirstOrDefault()
                ?? throw new ImportException($"RAIS: nada extraído de {archive.Path}.");
        }

        Console.WriteLine("  somando vínculos ativos por município (arquivo grande)");
        var totals = new Dictionary<int, long>();
        long rows = 0;
        using (var reader = new StreamReader(extracted, Encoding.Latin1))
        {
            string[] header = (await reader.ReadLineAsync())!.Split(',').Select(h => h.Trim('"')).ToArray();
            int ativos = Array.IndexOf(header, "Qtd Vínculos Ativos");
            int municipio = Array.IndexOf(header, "Município - Código");
            if (ativos < 0 || municipio < 0)
            {
                throw new ImportException("RAIS: colunas 'Qtd Vínculos Ativos' e 'Município - Código' não encontradas.");
            }

            while (await reader.ReadLineAsync() is { } line)
            {
                // As colunas usadas são numéricas e vêm antes de qualquer texto com vírgula: basta cortar nas vírgulas.
                string[] parts = line.Split(',');
                if (parts.Length <= Math.Max(ativos, municipio))
                {
                    continue;
                }

                int code = int.Parse(parts[municipio], CultureInfo.InvariantCulture);
                totals[code] = totals.GetValueOrDefault(code) + long.Parse(parts[ativos], CultureInfo.InvariantCulture);
                rows++;
            }
        }

        var toSave = totals.ToDictionary(p => p.Key.ToString(CultureInfo.InvariantCulture), p => p.Value);
        toSave["_estabelecimentos"] = rows;
        await File.WriteAllTextAsync(aggregate, JsonSerializer.Serialize(toSave));
        return new Result(totals, rows, archive);
    }

    private static void Extract(CachedFile archive, string folder)
    {
        string tar = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe")
            : "tar";
        Console.WriteLine("  extraindo o .7z da RAIS com o tar do sistema");
        try
        {
            using Process process = Process.Start(new ProcessStartInfo(tar, $"-xf \"{archive.Path}\" -C \"{folder}\"") { UseShellExecute = false })
                ?? throw new ImportException("RAIS: não foi possível iniciar o tar.");
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new DownloadBlockedException(archive.Url, Path.GetFileName(archive.Path), folder, $"tar terminou com código {process.ExitCode}; extraia o .7z manualmente para a pasta de destino");
            }
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new DownloadBlockedException(archive.Url, Path.GetFileName(archive.Path), folder, $"tar não encontrado ({ex.Message}); extraia o .7z manualmente para a pasta de destino");
        }
    }
}
