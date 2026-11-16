using System.IO.Compression;
using System.Text;

namespace Planalto.DataImporter;

/// <summary>Leitura dos CSV do TSE: separador ';', campos entre aspas, codificação Latin-1.</summary>
public static class Csv
{
    private static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>Lê só as colunas pedidas, na ordem pedida; serve para os CSV grandes do TSE.</summary>
    public static IEnumerable<string[]> ReadColumnsFromZip(string zipPath, Func<string, bool> entryFilter, params string[] columns)
    {
        int[]? indexes = null;
        foreach (string[] fields in ReadRawFromZip(zipPath, entryFilter, header =>
        {
            indexes = columns.Select(c => Array.IndexOf(header, c)).ToArray();
            int missing = Array.IndexOf(indexes, -1);
            if (missing >= 0)
            {
                throw new ImportException($"{Path.GetFileName(zipPath)}: coluna '{columns[missing]}' não encontrada.");
            }
        }))
        {
            yield return indexes!.Select(i => fields[i]).ToArray();
        }
    }

    /// <summary>Lê, dentro do ZIP, o primeiro CSV cujo nome satisfaz o filtro, linha a linha.</summary>
    public static IEnumerable<Dictionary<string, string>> ReadFromZip(string zipPath, Func<string, bool> entryFilter)
    {
        string[] header = [];
        foreach (string[] fields in ReadRawFromZip(zipPath, entryFilter, h => header = h))
        {
            var row = new Dictionary<string, string>(header.Length, StringComparer.Ordinal);
            for (int i = 0; i < header.Length; i++)
            {
                row[header[i]] = fields[i];
            }

            yield return row;
        }
    }

    private static IEnumerable<string[]> ReadRawFromZip(string zipPath, Func<string, bool> entryFilter, Action<string[]> onHeader)
    {
        using ZipArchive archive = ZipFile.OpenRead(zipPath);
        ZipArchiveEntry entry = archive.Entries
            .Where(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && entryFilter(e.Name))
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .FirstOrDefault()
            ?? throw new ImportException($"{Path.GetFileName(zipPath)}: nenhum CSV correspondente dentro do ZIP.");

        using var reader = new StreamReader(entry.Open(), Latin1);
        string? headerLine = reader.ReadLine() ?? throw new ImportException($"{entry.Name}: arquivo vazio.");
        string[] header = SplitLine(headerLine);
        onHeader(header);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            // Campo entre aspas com quebra de linha dentro (acontece em endereços): a linha continua na seguinte.
            while (line.Count(c => c == '"') % 2 == 1 && reader.ReadLine() is { } next)
            {
                line += " " + next;
            }

            string[] fields = SplitLine(line);
            if (fields.Length != header.Length)
            {
                throw new ImportException($"{entry.Name}: linha com {fields.Length} campos, cabeçalho com {header.Length}.");
            }

            yield return fields;
        }
    }

    public static string[] SplitLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                quoted = true;
            }
            else if (c == ';')
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        fields.Add(current.ToString());
        return [.. fields];
    }
}

public sealed class ImportException(string message) : Exception(message);
