using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Planalto.Core.Data;

/// <summary>
/// Nenhum texto mostrado ao jogador cita documento interno do projeto (o documento de desenho, o registro de
/// decisões, o roteiro, as pendências). Confere os textos de data/ e, pelo teste de fumaça, os textos de tela.
/// </summary>
public static partial class InternalReferenceLint
{
    /// <summary>Campos de data/ que são anotação de quem edita o arquivo e nunca vão à tela.</summary>
    public static readonly IReadOnlySet<string> InternalFields = new HashSet<string>(StringComparer.Ordinal) { "note", "notes", "method" };

    [GeneratedRegex(@"\bGDD\b|\bDECIS[OÕ]ES\b|\bD-\d{3}\b|\bROADMAP\b|\bPEND[EÊ]NCIAS\b|\bP-\d{2}\b|\bART_GUIDE\b|\bCONTENT_GUIDE\b|\bAUDITORIA_\w+|\bPROGRESSO\.md\b|\bCONTEXTO\.md\b|\bROTEIRO_TESTE\w*", RegexOptions.CultureInvariant)]
    private static partial Regex Reference();

    /// <summary>Primeira citação de documento interno no texto, ou null.</summary>
    public static string? Find(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        Match match = Reference().Match(text);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Percorre todos os .json de data/ e devolve cada texto que cita documento interno, fora dos campos de
    /// anotação e dos arquivos de fontes (nome começado por "_"), que não vão à tela.
    /// </summary>
    public static IReadOnlyList<string> Run(string dataDirectory, out int textsChecked)
    {
        var errors = new List<string>();
        textsChecked = 0;
        foreach (string path in Directory.EnumerateFiles(dataDirectory, "*.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            if (Path.GetFileName(path).StartsWith('_'))
            {
                continue;
            }

            string file = Path.GetRelativePath(dataDirectory, path).Replace('\\', '/');
            textsChecked += Scan(File.ReadAllBytes(path), file, errors);
        }

        return errors;
    }

    /// <summary>Confere os textos de um JSON; devolve quantos textos leu.</summary>
    public static int Scan(ReadOnlySpan<byte> json, string file, List<string> errors)
    {
        if (json.StartsWith(Encoding.UTF8.Preamble))
        {
            json = json[Encoding.UTF8.Preamble.Length..];
        }

        var reader = new Utf8JsonReader(json, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        int texts = 0;

        // Campo em que cada nível aberto está: o texto dentro de uma lista ou de um objeto de anotação também é anotação.
        var fields = new Stack<string?>();
        string? field = null;
        try
        {
            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.PropertyName:
                        field = reader.GetString();
                        break;
                    case JsonTokenType.StartObject or JsonTokenType.StartArray:
                        fields.Push(field);
                        break;
                    case JsonTokenType.EndObject or JsonTokenType.EndArray:
                        field = fields.Pop();
                        break;
                    case JsonTokenType.String:
                        bool note = (field is not null && InternalFields.Contains(field)) || fields.Any(f => f is not null && InternalFields.Contains(f));
                        if (!note)
                        {
                            texts++;
                            string? value = reader.GetString();
                            if (Find(value) is { } found)
                            {
                                errors.Add($"{file}: o campo '{field}' cita documento interno (\"{found}\") em \"{Excerpt(value!, found)}\"; texto de jogo não cita documento do projeto.");
                            }
                        }

                        break;
                }
            }
        }
        catch (JsonException e)
        {
            errors.Add($"{file}: JSON inválido ({e.Message}).");
        }

        return texts;
    }

    private static string Excerpt(string text, string found)
    {
        int at = text.IndexOf(found, StringComparison.Ordinal);
        int from = Math.Max(0, at - 30);
        int to = Math.Min(text.Length, at + found.Length + 30);
        return (from > 0 ? "…" : "") + text[from..to] + (to < text.Length ? "…" : "");
    }
}
