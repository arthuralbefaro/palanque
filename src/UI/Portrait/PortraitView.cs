using Godot;
using Planalto.Core.Characters;

namespace Planalto.UI.Portrait;

/// <summary>
/// Retrato em camadas (GDD 7.4): desenha a pilha calculada por <see cref="PortraitComposition"/>, cada camada
/// tingida pelo shader. Quadrado; o conteúdo se ajusta ao tamanho do controle.
/// </summary>
public partial class PortraitView : Control
{
    private static readonly Shader TintShader = GD.Load<Shader>("res://src/UI/Portrait/tint.gdshader");

    private readonly List<(TextureRect Rect, LayerSpec Spec)> _layers = [];

    public override void _Ready()
    {
        ClipContents = true;
        Resized += Relayout;
    }

    public void Show(Appearance appearance, PortraitOptions options, AppearanceContent content)
    {
        foreach (var (rect, _) in _layers)
        {
            RemoveChild(rect);
            rect.QueueFree();
        }

        _layers.Clear();
        foreach (LayerSpec spec in PortraitComposition.Compose(appearance, options, content))
        {
            var material = new ShaderMaterial { Shader = TintShader };
            material.SetShaderParameter("tint", new Color(spec.Tint));
            material.SetShaderParameter("alpha", (float)spec.Alpha);
            var rect = new TextureRect
            {
                Texture = GD.Load<Texture2D>(spec.File),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore,
                Material = material,
            };
            AddChild(rect);
            _layers.Add((rect, spec));
        }

        Relayout();
    }

    private void Relayout()
    {
        float side = Mathf.Min(Size.X, Size.Y);
        var offset = new Vector2((Size.X - side) / 2, (Size.Y - side) / 2);
        foreach (var (rect, spec) in _layers)
        {
            rect.Position = offset;
            rect.Size = new Vector2(side, side);
            rect.PivotOffset = new Vector2((float)(spec.PivotX / 1024 * side), (float)(spec.PivotY / 1024 * side));
            rect.Scale = new Vector2((float)spec.ScaleX, (float)spec.ScaleY);
        }
    }
}
