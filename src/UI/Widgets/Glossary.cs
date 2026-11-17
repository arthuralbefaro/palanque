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

    /// <summary>
    /// Telas fora da tela central (menu, Novo Mundo, criação, urna, apuração, debate, mapa do estado): troca cada Label
    /// que cita um termo do glossário por texto com o termo sublinhado e clicável, com a mesma fonte, tamanho, cor,
    /// quebra e tooltip do Label. Só o link muda; o visual de cada tela fica como está. Rótulo que o código ainda
    /// altera depois de montado não pode citar termo (a troca o tiraria da tela).
    /// </summary>
    public static void LinkTerms(Node root)
    {
        foreach (Label label in All<Label>(root).Where(l => TermsIn(l.Text).Any()).ToList())
        {
            Replace(label, Linkify(label.Text));
        }
    }

    /// <summary>
    /// Igual a <see cref="LinkTerms"/>, mas sem link: para nomes (de opção, de partido, de pessoa) que contêm uma palavra
    /// do glossário sem ser o termo, como "Realismo jurídico".
    /// </summary>
    public static RichTextLabel Unlinked(Label label) => Replace(label, Escape(label.Text));

    private static RichTextLabel Replace(Label label, string bbcode)
    {
        var rich = new RichTextLabel
        {
            Name = label.Name,
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = label.AutowrapMode,
            CustomMinimumSize = label.CustomMinimumSize,
            SizeFlagsHorizontal = label.SizeFlagsHorizontal,
            SizeFlagsVertical = label.SizeFlagsVertical,
            TooltipText = label.TooltipText,
            MouseFilter = label.TooltipText.Length > 0 ? Control.MouseFilterEnum.Stop : Control.MouseFilterEnum.Pass,
        };
        rich.AddThemeFontSizeOverride("normal_font_size", label.GetThemeFontSize("font_size"));
        rich.AddThemeColorOverride("default_color", label.GetThemeColor("font_color"));
        rich.AddThemeFontOverride("normal_font", label.GetThemeFont("font"));
        rich.Text = bbcode;
        rich.MetaClicked += meta =>
        {
            string value = meta.AsString();
            if (value.StartsWith(MetaPrefix, StringComparison.Ordinal))
            {
                EncyclopediaDialog.Open(rich.GetTree().Root, value[MetaPrefix.Length..]);
            }
        };
        Node parent = label.GetParent();
        int index = label.GetIndex();
        parent.RemoveChild(label);
        label.QueueFree();
        parent.AddChild(rich);
        parent.MoveChild(rich, index);
        return rich;
    }

    private static IEnumerable<T> All<T>(Node root)
        where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (T inner in All<T>(child))
            {
                yield return inner;
            }
        }
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
