using System.Text;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Encyclopedia;
using Planalto.UI.Theme;

namespace Planalto.UI.Widgets;

/// <summary>
/// Texto com os termos do glossário sublinhados e clicáveis (princípio j). Todo texto que pode conter termo técnico
/// passa por aqui; o teste de interface reprova termo do glossário em Label simples.
/// </summary>
public static class Glossary
{
    public const string MetaPrefix = "termo:";

    /// <summary>Texto sem links: nomes próprios (zona, pessoa), que não são termos mesmo quando contêm um.</summary>
    public static RichTextLabel Plain(Node parent, string text, int size = 18, Color? color = null, Font? font = null)
    {
        RichTextLabel label = Rich(parent, "", size, color, font);
        label.Text = Escape(text);
        return label;
    }

    public static RichTextLabel Rich(Node parent, string text, int size = 18, Color? color = null, Font? font = null)
    {
        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };
        label.AddThemeFontSizeOverride("normal_font_size", size);
        label.AddThemeColorOverride("default_color", color ?? Palette.Texto2);
        label.AddThemeFontOverride("normal_font", font ?? Palette.Inter);
        label.Text = Linkify(text);
        label.MetaClicked += meta =>
        {
            string value = meta.AsString();
            if (value.StartsWith(MetaPrefix, StringComparison.Ordinal))
            {
                EncyclopediaDialog.Open(label.GetTree().Root, value[MetaPrefix.Length..]);
            }
        };
        parent.AddChild(label);
        return label;
    }

    /// <summary>Converte o texto em BBCode, com cada termo do glossário como link sublinhado.</summary>
    public static string Linkify(string text)
    {
        EncyclopediaContent glossary = Game.Instance.Encyclopedia;
        var sb = new StringBuilder();
        int at = 0;
        foreach (var (start, length, id) in glossary.Occurrences(text))
        {
            sb.Append(Escape(text[at..start]));
            sb.Append("[url=").Append(MetaPrefix).Append(id).Append("][u]").Append(Escape(text.Substring(start, length))).Append("[/u][/url]");
            at = start + length;
        }

        sb.Append(Escape(text[at..]));
        return sb.ToString();
    }

    /// <summary>Termos do glossário presentes num texto puro (o teste de interface usa para achar Label sem link).</summary>
    public static IEnumerable<string> TermsIn(string text) => Game.Instance.Encyclopedia.Occurrences(text).Select(h => h.TermId);

    private static string Escape(string s) => s.Replace("[", "[lb]", StringComparison.Ordinal);
}
