using Godot;
using Planalto.UI.Theme;

namespace Planalto.UI.Map;

/// <summary>
/// Número flutuante (RL, ART_GUIDE seção 8, "Efeito numa zona"): a etiqueta ("+2,1 presença") nasce no centro da zona,
/// sobe 24 px e some em 900 ms; a das vizinhas é menor e sai um pouco depois. Não bloqueia clique.
/// </summary>
public partial class ZoneMapView
{
    private readonly List<string> _floatTexts = [];

    /// <summary>Etiquetas mostradas desde que o mapa abriu, da mais antiga à mais nova (testes de fumaça).</summary>
    public IReadOnlyList<string> FloatTexts => _floatTexts;

    /// <summary>Etiquetas ainda na tela.</summary>
    public int FloatsOnScreen => GetChildren().OfType<Label>().Count(l => l.Name.ToString().StartsWith("Flutuante", StringComparison.Ordinal) && !l.IsQueuedForDeletion());

    public void ShowFloat(string zoneId, string text, bool small, double delay)
    {
        if (!_zonas.ContainsKey(zoneId))
        {
            return;
        }

        _floatTexts.Add(text);
        var label = new Label
        {
            Name = "Flutuante" + _floatTexts.Count,
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1, 1, 1, delay > 0 ? 0 : 1),
        };
        // Texto liso sobre uma pílula escura, sem contorno: o contorno do tema sobre o verde do mapa desenhava cada
        // letra duas vezes. A pílula dá o contraste em qualquer cor de zona.
        label.AddThemeFontSizeOverride("font_size", small ? 18 : 24);
        label.AddThemeFontOverride("font", Palette.Weight(Palette.Inter, 600));
        label.AddThemeColorOverride("font_color", text.StartsWith('−') ? Palette.Negativo : Palette.Positivo);
        label.AddThemeConstantOverride("outline_size", 0);
        label.AddThemeConstantOverride("shadow_outline_size", 0);
        label.AddThemeColorOverride("font_shadow_color", Colors.Transparent);
        StyleBoxFlat pill = Palette.Box(new Color(Palette.Fundo, 0.9f), 8, 0, Palette.Linha);
        pill.ContentMarginLeft = pill.ContentMarginRight = small ? 6 : 8;
        pill.ContentMarginTop = pill.ContentMarginBottom = 1;
        label.AddThemeStyleboxOverride("normal", pill);
        AddChild(label);
        Vector2 size = label.GetMinimumSize();
        label.Size = size;

        // Posição em pixel inteiro: o texto não borra na subida.
        label.Position = (ScreenCenterOf(zoneId) - new Vector2(size.X / 2, size.Y + 16)).Round();
        Tween tween = label.CreateTween();
        if (delay > 0)
        {
            tween.TweenInterval(delay);
            tween.TweenProperty(label, "modulate:a", 1.0, 0.05);
        }

        // Sobe 24 px em 900 ms, nítido até 550 ms e some no resto (ART_GUIDE, "Efeito numa zona").
        tween.TweenMethod(Callable.From<float>(y => label.Position = new Vector2(label.Position.X, Mathf.Round(y))), label.Position.Y, label.Position.Y - 24, 0.9);
        tween.Parallel().TweenProperty(label, "modulate:a", 0.0, 0.35).SetDelay(0.55).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }
}
