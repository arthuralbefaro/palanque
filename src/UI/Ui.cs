using Godot;

namespace Planalto.UI;

/// <summary>Atalhos para montar telas em código com o tema base (DECISOES D-018). Todo texto passa por Tr().</summary>
public static class Ui
{
    public static readonly Color Error = new("#E76F51");
    public static readonly Color Warning = new("#E9C46A");
    public static readonly Color Ok = new("#2A9D8F");
    public static readonly Color Muted = new("#9AA5B1");

    public static T Add<T>(this Node parent, T child)
        where T : Node
    {
        parent.AddChild(child);
        return child;
    }

    /// <summary>Rótulo traduzido. Sem quebra automática: dentro de linhas horizontais ela reduziria a largura a zero.</summary>
    public static Label Label(Node parent, string text, int size = 0, Color? color = null)
    {
        var label = parent.Add(new Label { Text = TranslationServer.Translate(text) });
        if (size > 0)
        {
            label.AddThemeFontSizeOverride("font_size", size);
        }

        if (color is Color c)
        {
            label.AddThemeColorOverride("font_color", c);
        }

        return label;
    }

    /// <summary>Rótulo com texto que não deve ser traduzido (nomes, números, dados).</summary>
    /// <summary>Texto longo com quebra de linha e largura mínima, para colunas.</summary>
    public static Label Paragraph(Node parent, string text, int width, int size = 0)
    {
        Label label = Value(parent, text, size);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        label.CustomMinimumSize = new Vector2(width, 0);
        return label;
    }

    public static Label Value(Node parent, string text, int size = 0)
    {
        var label = parent.Add(new Label { Text = text });
        if (size > 0)
        {
            label.AddThemeFontSizeOverride("font_size", size);
        }

        return label;
    }

    public static Button Button(Node parent, string text, Action onPressed, string? tooltip = null)
    {
        var button = parent.Add(new Button { Text = TranslationServer.Translate(text), TooltipText = tooltip is null ? "" : TranslationServer.Translate(tooltip) });
        button.Pressed += onPressed;
        return button;
    }

    /// <summary>Ícones de interface (assets/icons/ui), sempre com tooltip explicando a ação.</summary>
    public static class Icons
    {
        public const string Sortear = "res://assets/icons/ui/dado.svg";
        public const string Travado = "res://assets/icons/ui/cadeado.svg";
        public const string Destravado = "res://assets/icons/ui/cadeado_aberto.svg";
        public const string Duplicar = "res://assets/icons/ui/copiar.svg";
        public const string Remover = "res://assets/icons/ui/lixeira.svg";
    }

    /// <summary>
    /// Versão compacta de um controle para tabelas densas: fonte 16 e margens internas pequenas (o tema tem
    /// margens para texto grande). Vale para botões, listas, campos de texto e seletores de cor.
    /// </summary>
    public static T Compact<T>(T control, int width = 0)
        where T : Control
    {
        Control target = control is SpinBox spin ? spin.GetLineEdit() : control;
        target.AddThemeFontSizeOverride("font_size", 16);
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus", "read_only" })
        {
            if (target.HasThemeStylebox(state) && target.GetThemeStylebox(state)?.Duplicate() is StyleBox style)
            {
                style.ContentMarginLeft = style.ContentMarginRight = 6;
                style.ContentMarginTop = style.ContentMarginBottom = 3;
                target.AddThemeStyleboxOverride(state, style);
            }
        }

        if (width > 0)
        {
            control.CustomMinimumSize = new Vector2(width, control.CustomMinimumSize.Y);
        }

        return control;
    }

    /// <summary>Botão só com ícone; o tooltip diz o que ele faz.</summary>
    public static Button IconButton(Node parent, string icon, string tooltip, Action onPressed)
    {
        var button = parent.Add(new Button
        {
            Icon = GD.Load<Texture2D>(icon),
            TooltipText = TranslationServer.Translate(tooltip),
            CustomMinimumSize = new Vector2(34, 34),
            IconAlignment = HorizontalAlignment.Center,
        });

        // O estilo do tema tem margens para texto grande; o botão de ícone usa a mesma cara com margem pequena.
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            if (button.GetThemeStylebox(state)?.Duplicate() is StyleBox style)
            {
                style.ContentMarginLeft = style.ContentMarginRight = style.ContentMarginTop = style.ContentMarginBottom = 3;
                button.AddThemeStyleboxOverride(state, style);
            }
        }

        button.Pressed += onPressed;
        return button;
    }

    public static HBoxContainer Row(Node parent, int separation = 8)
    {
        var row = parent.Add(new HBoxContainer());
        row.AddThemeConstantOverride("separation", separation);
        return row;
    }

    public static VBoxContainer Column(Node parent, int separation = 8)
    {
        var column = parent.Add(new VBoxContainer());
        column.AddThemeConstantOverride("separation", separation);
        return column;
    }

    public static OptionButton Options(Node parent, IEnumerable<(string Id, string Text)> items, string? selected, Action<string> onSelected, bool translate = true)
    {
        var button = parent.Add(new OptionButton { FitToLongestItem = false });
        var ids = new List<string>();
        foreach (var (id, text) in items)
        {
            button.AddItem(translate ? TranslationServer.Translate(text) : text);
            ids.Add(id);
        }

        int index = selected is null ? -1 : ids.IndexOf(selected);
        button.Selected = index;
        button.ItemSelected += i => onSelected(ids[(int)i]);
        return button;
    }

    public static HSlider Slider(Node parent, double value, Action<double> onChanged, double min = 0, double max = 1, double step = 0.01)
    {
        var slider = parent.Add(new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(180, 24), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        slider.ValueChanged += v => onChanged(v);
        return slider;
    }

    public static ColorPickerButton ColorPicker(Node parent, string hex, Action<string> onChanged)
    {
        var picker = parent.Add(new ColorPickerButton { Color = new Color(hex), CustomMinimumSize = new Vector2(40, 32), EditAlpha = false });
        picker.ColorChanged += c => onChanged("#" + c.ToHtml(false).ToUpperInvariant());
        return picker;
    }

    /// <summary>Campo rotulado: rótulo à esquerda (largura fixa) e controle à direita.</summary>
    public static HBoxContainer Field(Node parent, string label, int labelWidth = 180)
    {
        HBoxContainer row = Row(parent);
        var l = Label(row, label);
        l.CustomMinimumSize = new Vector2(labelWidth, 0);
        l.AutowrapMode = TextServer.AutowrapMode.Off;
        return row;
    }

    public static PanelContainer Panel(Node parent, int margin = 16)
    {
        var panel = parent.Add(new PanelContainer());
        var style = new StyleBoxFlat { BgColor = new Color("#13233C"), CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8, CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8 };
        style.SetContentMarginAll(margin);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    public static void Clear(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    /// <summary>Raiz de tela: fundo, margens e coluna principal.</summary>
    public static VBoxContainer Screen(Control root, int margin = 24)
    {
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var background = root.Add(new Panel { ThemeTypeVariation = "BackgroundPanel" });
        background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var margins = root.Add(new MarginContainer());
        margins.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "top", "right", "bottom" })
        {
            margins.AddThemeConstantOverride($"margin_{side}", margin);
        }

        return Column(margins, 12);
    }
}
