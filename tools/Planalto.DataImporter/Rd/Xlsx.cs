using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Planalto.DataImporter.Rd;

/// <summary>
/// Leitor mínimo de planilhas .xlsx, sem pacote externo: o arquivo é um zip com XML. Lê os nomes das planilhas
/// (xl/workbook.xml e as relações), as strings compartilhadas e as linhas de uma planilha, em fluxo (as tábuas de
/// mortalidade do IBGE têm uma planilha de mais de 100 MB descompactada). Só valores: estilos, fórmulas e datas
/// formatadas ficam de fora (número vem como o texto gravado no arquivo, em formato invariante).
/// </summary>
public static class Xlsx
{
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
    };

    /// <summary>Nomes das planilhas, na ordem da pasta de trabalho.</summary>
    public static IReadOnlyList<string> SheetNames(string path)
    {
        using FileStream file = File.OpenRead(path);
        return SheetNames(file);
    }

    /// <summary>Nomes das planilhas de um .xlsx recebido como fluxo (por exemplo, uma entrada de um .zip).</summary>
    public static IReadOnlyList<string> SheetNames(Stream stream)
    {
        using ZipArchive zip = Open(stream);
        return Sheets(zip).Select(s => s.Name).ToList();
    }

    /// <summary>
    /// Linhas da planilha, na ordem do arquivo. Cada célula fica na posição da sua coluna (A = 0), pela referência
    /// da célula ("C7"); célula vazia ou ausente é null. Linhas que o arquivo não grava não são devolvidas.
    /// </summary>
    public static IEnumerable<string?[]> ReadRows(string path, string sheetName)
    {
        using FileStream file = File.OpenRead(path);
        foreach (string?[] row in ReadRows(file, sheetName))
        {
            yield return row;
        }
    }

    /// <summary>Linhas da planilha de um .xlsx recebido como fluxo; o fluxo não é fechado.</summary>
    public static IEnumerable<string?[]> ReadRows(Stream stream, string sheetName)
    {
        using ZipArchive zip = Open(stream);
        List<(string Name, string Entry)> sheets = Sheets(zip);
        int index = sheets.FindIndex(s => s.Name == sheetName);
        if (index < 0)
        {
            throw new ImportException($"Planilha '{sheetName}' não existe no arquivo (há: {string.Join(", ", sheets.Select(s => s.Name))}).");
        }

        ZipArchiveEntry entry = zip.GetEntry(sheets[index].Entry)
            ?? throw new ImportException($"Planilha '{sheetName}': entrada {sheets[index].Entry} ausente no arquivo.");
        List<string> shared = SharedStrings(zip);

        using Stream sheet = entry.Open();
        using XmlReader reader = XmlReader.Create(sheet, Settings);
        foreach (string?[] row in Rows(reader, shared, sheetName))
        {
            yield return row;
        }
    }

    /// <summary>Índice da coluna (A = 0) de uma referência de célula como "C7" ou "AB12".</summary>
    public static int ColumnIndex(string cellReference)
    {
        int column = 0;
        int letters = 0;
        foreach (char c in cellReference)
        {
            if (c is < 'A' or > 'Z')
            {
                break;
            }

            column = (column * 26) + (c - 'A' + 1);
            letters++;
        }

        if (letters == 0)
        {
            throw new ImportException($"Referência de célula inválida: '{cellReference}'.");
        }

        return column - 1;
    }

    // ZipArchive precisa de fluxo com posição; o que vem de dentro de outro zip é copiado para a memória.
    private static ZipArchive Open(Stream stream)
    {
        if (stream.CanSeek)
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
        }

        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        return new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
    }

    private static List<(string Name, string Entry)> Sheets(ZipArchive zip)
    {
        var targets = new Dictionary<string, string>(StringComparer.Ordinal);
        using (XmlReader rels = XmlReader.Create(Required(zip, "xl/_rels/workbook.xml.rels").Open(), Settings))
        {
            while (rels.Read())
            {
                if (rels.NodeType == XmlNodeType.Element && rels.LocalName == "Relationship"
                    && rels.GetAttribute("Id") is { } id && rels.GetAttribute("Target") is { } target)
                {
                    // O alvo é relativo a xl/, ou absoluto a partir da raiz do zip.
                    targets[id] = target.StartsWith('/') ? target[1..] : "xl/" + target;
                }
            }
        }

        var sheets = new List<(string Name, string Entry)>();
        using XmlReader workbook = XmlReader.Create(Required(zip, "xl/workbook.xml").Open(), Settings);
        while (workbook.Read())
        {
            if (workbook.NodeType != XmlNodeType.Element || workbook.LocalName != "sheet")
            {
                continue;
            }

            string name = workbook.GetAttribute("name") ?? throw new ImportException("xl/workbook.xml: planilha sem nome.");
            string? relation = workbook.GetAttribute("id", RelationshipsNamespace);
            if (relation is null || !targets.TryGetValue(relation, out string? entry))
            {
                throw new ImportException($"xl/workbook.xml: planilha '{name}' sem relação para o arquivo dela.");
            }

            sheets.Add((name, entry));
        }

        return sheets;
    }

    private static ZipArchiveEntry Required(ZipArchive zip, string name) =>
        zip.GetEntry(name) ?? throw new ImportException($"Arquivo .xlsx sem a entrada {name}.");

    private static List<string> SharedStrings(ZipArchive zip)
    {
        var strings = new List<string>();
        ZipArchiveEntry? entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return strings;
        }

        using XmlReader reader = XmlReader.Create(entry.Open(), Settings);
        var text = new StringBuilder();
        bool inText = false;
        int phonetic = 0;
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element when reader.LocalName == "si":
                    text.Clear();
                    if (reader.IsEmptyElement)
                    {
                        strings.Add(string.Empty);
                    }

                    break;
                case XmlNodeType.Element when reader.LocalName == "rPh" && !reader.IsEmptyElement:
                    phonetic++;
                    break;
                case XmlNodeType.Element when reader.LocalName == "t" && !reader.IsEmptyElement:
                    inText = phonetic == 0;
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "t":
                    inText = false;
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "rPh":
                    phonetic--;
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "si":
                    strings.Add(text.ToString());
                    break;
                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace when inText:
                    text.Append(reader.Value);
                    break;
            }
        }

        return strings;
    }

    private static IEnumerable<string?[]> Rows(XmlReader reader, List<string> shared, string sheetName)
    {
        var cells = new List<string?>();
        var value = new StringBuilder();
        var inline = new StringBuilder();
        string? type = null;
        int column = -1;
        bool hasValue = false;
        bool inValue = false;
        bool inInlineText = false;
        bool inCell = false;
        int phonetic = 0;

        void Store()
        {
            string? content = type switch
            {
                "inlineStr" => inline.ToString(),
                "s" when hasValue => SharedAt(shared, value.ToString(), sheetName),
                _ when hasValue => value.ToString(),
                _ => null,
            };
            if (content is null)
            {
                return;
            }

            while (cells.Count <= column)
            {
                cells.Add(null);
            }

            cells[column] = content;
        }

        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element when reader.LocalName == "row":
                    cells.Clear();
                    column = -1;
                    if (reader.IsEmptyElement)
                    {
                        yield return [];
                    }

                    break;
                case XmlNodeType.Element when reader.LocalName == "c":
                    // Sem referência, a célula ocupa a coluna seguinte à anterior.
                    column = reader.GetAttribute("r") is { Length: > 0 } reference ? ColumnIndex(reference) : column + 1;
                    type = reader.GetAttribute("t");
                    value.Clear();
                    inline.Clear();
                    hasValue = false;
                    inCell = !reader.IsEmptyElement;
                    break;
                case XmlNodeType.Element when inCell && reader.LocalName == "v":
                    hasValue = true;
                    inValue = !reader.IsEmptyElement;
                    break;
                case XmlNodeType.Element when inCell && reader.LocalName == "rPh" && !reader.IsEmptyElement:
                    phonetic++;
                    break;
                case XmlNodeType.Element when inCell && reader.LocalName == "t" && !reader.IsEmptyElement:
                    inInlineText = phonetic == 0;
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "v":
                    inValue = false;
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "t":
                    inInlineText = false;
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "rPh":
                    phonetic--;
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "c":
                    Store();
                    inCell = false;
                    break;
                case XmlNodeType.EndElement when reader.LocalName == "row":
                    yield return cells.ToArray();
                    break;
                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace:
                    if (inValue)
                    {
                        value.Append(reader.Value);
                    }
                    else if (inInlineText)
                    {
                        inline.Append(reader.Value);
                    }

                    break;
            }
        }
    }

    private static string SharedAt(List<string> shared, string index, string sheetName) =>
        int.TryParse(index, NumberStyles.None, CultureInfo.InvariantCulture, out int i) && i < shared.Count
            ? shared[i]
            : throw new ImportException($"Planilha '{sheetName}': string compartilhada {index} não existe.");
}
