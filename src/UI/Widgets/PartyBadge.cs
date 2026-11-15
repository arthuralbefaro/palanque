using Godot;

namespace Planalto.UI.Widgets;

/// <summary>Logotipo de partido: fundo na cor primária e ícone na secundária.</summary>
public partial class PartyBadge : PanelContainer
{
    private readonly TextureRect _icon = new() { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
    private readonly StyleBoxFlat _style = new() { CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6 };

    public PartyBadge(int size = 40)
    {
        CustomMinimumSize = new Vector2(size, size);
        _style.SetContentMarginAll(size / 8f);
        AddThemeStyleboxOverride("panel", _style);
        AddChild(_icon);
    }

    public void Set(string icon, string primary, string secondary)
    {
        _style.BgColor = new Color(primary);
        string path = $"res://assets/icons/logo/{icon}.svg";
        _icon.Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
        _icon.Modulate = new Color(secondary);
    }
}
