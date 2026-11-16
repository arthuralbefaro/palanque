using Godot;

namespace Planalto.UI.Theme;

/// <summary>Cores, fontes e caixas da interface (docs/ART_GUIDE.md, seção Interface). Nenhuma tela escreve cor própria.</summary>
public static class Palette
{
    public static readonly Color Fundo = new("#0E1116");
    public static readonly Color Painel = new("#161A21");
    public static readonly Color Cartao = new("#1E232C");
    public static readonly Color CartaoAlto = new("#272D38");
    public static readonly Color Linha = new("#2F3642");
    public static readonly Color Texto = new("#ECEFF4");
    public static readonly Color Texto2 = new("#AAB3C0");
    public static readonly Color Texto3 = new("#8E99A8");
    public static readonly Color Acao = new("#4C8DF6");
    public static readonly Color Positivo = new("#3FB68B");
    public static readonly Color Negativo = new("#E5645A");
    public static readonly Color Atencao = new("#E9B44C");
    public static readonly Color NeutroDado = new("#8A94A3");

    /// <summary>Escala sequencial do modo Perfil (5 faixas, de #2A2620 a #E0A458).</summary>
    public static Color Perfil(double t) => new Color("#2A2620").Lerp(new Color("#E0A458"), (float)Math.Clamp(t, 0, 1));

    /// <summary>Escala sequencial do modo Minha força (5 faixas, de #1B2A2A a #2FD0A5).</summary>
    public static Color Forca(double t) => new Color("#1B2A2A").Lerp(new Color("#2FD0A5"), (float)Math.Clamp(t, 0, 1));

    private static FontFile? _inter, _oswald, _serif;

    public static Font Inter => _inter ??= Load("res://assets/fonts/Inter.ttf");

    public static Font Oswald => _oswald ??= Load("res://assets/fonts/Oswald.ttf");

    public static Font Serif => _serif ??= Load("res://assets/fonts/SourceSerif4.ttf");

    private static FontFile Load(string path) => GD.Load<FontFile>(path);

    /// <summary>Fonte variável com peso escolhido (400, 500, 600).</summary>
    public static Font Weight(Font font, int weight)
    {
        var variation = new FontVariation { BaseFont = font };
        variation.VariationOpentype = new Godot.Collections.Dictionary { [TextServerManager.GetPrimaryInterface().NameToTag("wght")] = weight };
        return variation;
    }

    public static StyleBoxFlat Box(Color background, int radius = 8, int margin = 16, Color? border = null, int borderWidth = 1)
    {
        var box = new StyleBoxFlat { BgColor = background };
        box.SetCornerRadiusAll(radius);
        box.SetContentMarginAll(margin);
        if (border is { } b)
        {
            box.BorderColor = b;
            box.SetBorderWidthAll(borderWidth);
        }

        return box;
    }

    /// <summary>Tema da tela central: fonte Inter, botões, painéis e tooltip (ART_GUIDE, estados de controle).</summary>
    public static Godot.Theme CentralTheme()
    {
        var theme = new Godot.Theme { DefaultFont = Weight(Inter, 400), DefaultFontSize = 20 };
        theme.SetColor("font_color", "Label", Texto2);
        theme.SetColor("font_color", "Button", Texto);
        theme.SetColor("font_hover_color", "Button", Texto);
        theme.SetColor("font_pressed_color", "Button", Texto);
        theme.SetColor("font_focus_color", "Button", Texto);
        theme.SetColor("font_disabled_color", "Button", new Color(Texto, 0.4f));
        theme.SetFont("font", "Button", Weight(Inter, 500));
        theme.SetFontSize("font_size", "Button", 18);
        theme.SetStylebox("normal", "Button", Box(Cartao, 8, 10, Linha));
        theme.SetStylebox("hover", "Button", Box(CartaoAlto, 8, 10, Linha));
        theme.SetStylebox("pressed", "Button", Box(new Color(Acao, 0.2f), 8, 10, Acao));
        theme.SetStylebox("disabled", "Button", Box(new Color(Cartao, 0.4f), 8, 10, new Color(Linha, 0.4f)));
        var focus = Box(new Color(0, 0, 0, 0), 8, 10, Texto, 2);
        focus.SetExpandMarginAll(2);
        theme.SetStylebox("focus", "Button", focus);
        theme.SetStylebox("panel", "TooltipPanel", Box(CartaoAlto, 8, 12, Linha));
        theme.SetColor("font_color", "TooltipLabel", Texto);
        theme.SetFontSize("font_size", "TooltipLabel", 18);
        theme.SetFont("font", "TooltipLabel", Weight(Inter, 400));
        theme.SetStylebox("panel", "PanelContainer", Box(Painel, 0, 16));

        // Listas de escolha (tipo de compromisso, zona, público): mesma cara dos cartões.
        theme.SetStylebox("panel", "PopupMenu", Box(CartaoAlto, 8, 8, Linha));
        theme.SetStylebox("hover", "PopupMenu", Box(new Color(Acao, 0.2f), 4, 4));
        theme.SetColor("font_color", "PopupMenu", Texto);
        theme.SetColor("font_hover_color", "PopupMenu", Texto);
        theme.SetColor("font_disabled_color", "PopupMenu", new Color(Texto, 0.4f));
        theme.SetFont("font", "PopupMenu", Weight(Inter, 400));
        theme.SetFontSize("font_size", "PopupMenu", 18);
        theme.SetConstant("v_separation", "PopupMenu", 12);
        return theme;
    }

    /// <summary>Botão primário (ART_GUIDE, estados de controle): fundo acao, texto fundo.</summary>
    public static void Primary(Button button)
    {
        button.AddThemeStyleboxOverride("normal", Box(Acao, 8, 10));
        button.AddThemeStyleboxOverride("hover", Box(Acao.Lightened(0.08f), 8, 10));
        button.AddThemeStyleboxOverride("pressed", Box(Acao.Darkened(0.08f), 8, 10));
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color" })
        {
            button.AddThemeColorOverride(color, Fundo);
        }
    }

    /// <summary>Linha de lista clicável (ART_GUIDE): fundo cartao, cartao_alto sob o mouse, sem contorno.</summary>
    public static void ListRow(Button button, bool selected = false)
    {
        var normal = Box(Cartao, 8, 8);
        if (selected)
        {
            normal.BorderColor = Acao;
            normal.BorderWidthLeft = 3;
        }

        button.AddThemeStyleboxOverride("normal", normal);
        button.AddThemeStyleboxOverride("hover", Box(CartaoAlto, 8, 8));
        button.AddThemeStyleboxOverride("pressed", Box(CartaoAlto, 8, 8));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(Cartao, 0.4f), 8, 8));
    }

    /// <summary>Ícone da interface (assets/icons/ui), pelo nome do arquivo sem extensão.</summary>
    public static Texture2D Icon(string name) => GD.Load<Texture2D>($"res://assets/icons/ui/{name}.svg");

    /// <summary>Tooltip em três partes (princípio j): o que é, o que faz (com faixa), quanto custa.</summary>
    public static string Tip(string what, string does, string cost) =>
        $"{what}\n\n{TranslationServer.Translate("O que faz:")} {does}\n{TranslationServer.Translate("Quanto custa:")} {cost}";

    /// <summary>Tooltip de controle bloqueado: o motivo e o que falta vêm antes das três partes.</summary>
    public static string Blocked(string reason, string tip) => $"{TranslationServer.Translate("Bloqueado:")} {reason}\n\n{tip}";
}
