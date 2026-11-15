using Godot;
using Planalto.Autoload;
using Planalto.Core.Characters;
using Planalto.Core.State;
using Planalto.UI.Portrait;
using Planalto.UI.Widgets;
using Expression = Planalto.Core.Characters.Expression;

namespace Planalto.UI.CampaignMaterials;

/// <summary>Dados do material de campanha, montados a partir do personagem e do partido.</summary>
public sealed record CampaignCard(
    Character Character,
    string PartySigla,
    string PartyName,
    string PartyPrimary,
    string PartySecondary,
    string Office,
    string City);

/// <summary>Fontes do compositor de logotipo (GDD 3.4), mapeadas para fontes do sistema na arte placeholder.</summary>
public static class LogoFonts
{
    private static readonly Dictionary<string, (string[] Names, int Weight, bool Italic)> Map = new()
    {
        ["sans_forte"] = (["Arial Black", "Arial"], 900, false),
        ["sans_leve"] = (["Segoe UI Light", "Segoe UI", "Arial"], 300, false),
        ["condensada"] = (["Arial Narrow", "Bahnschrift Condensed", "Arial"], 700, false),
        ["serifada"] = (["Georgia", "Times New Roman"], 700, false),
        ["arredondada"] = (["Verdana", "Tahoma"], 700, false),
        ["manuscrita"] = (["Segoe Script", "Comic Sans MS"], 400, true),
    };

    public static Font For(string id)
    {
        var (names, weight, italic) = Map.TryGetValue(id, out var spec) ? spec : Map["sans_forte"];
        return new SystemFont { FontNames = names, FontWeight = weight, FontItalic = italic };
    }
}

/// <summary>Logotipo de campanha: ícone, nome de urna na fonte escolhida e número.</summary>
public partial class LogoView : HBoxContainer
{
    private readonly PartyBadge _badge = new(56);
    private readonly Label _name = new();
    private readonly Label _number = new();

    public LogoView()
    {
        AddThemeConstantOverride("separation", 10);
        AddChild(_badge);
        var column = new VBoxContainer();
        AddChild(column);
        column.AddChild(_name);
        column.AddChild(_number);
    }

    public void Set(CampaignIdentity identity, string ballotName, int nameSize = 28)
    {
        _badge.Set(identity.LogoIcon, identity.LogoPrimaryColor, identity.LogoSecondaryColor);
        _name.Text = ballotName;
        _name.AddThemeFontOverride("font", LogoFonts.For(identity.LogoFont));
        _name.AddThemeFontSizeOverride("font_size", nameSize);
        _name.AddThemeColorOverride("font_color", new Color(identity.LogoSecondaryColor));
        _number.Text = identity.BallotNumber ?? "";
        _number.AddThemeFontSizeOverride("font_size", nameSize);
        _number.AddThemeColorOverride("font_color", new Color(identity.LogoSecondaryColor));
    }
}

/// <summary>Santinho (frente e verso) com foto oficial, nome de urna, número, partido e slogan.</summary>
public partial class Santinho : HBoxContainer
{
    public void Set(CampaignCard card)
    {
        Ui.Clear(this);
        AddThemeConstantOverride("separation", 12);
        CampaignIdentity identity = card.Character.Campaign!;

        PanelContainer front = Card(card.PartyPrimary);
        VBoxContainer f = Ui.Column(front, 4);
        var photo = f.Add(new PortraitView { CustomMinimumSize = new Vector2(150, 150) });
        photo.Show(
            card.Character.Appearance!,
            new PortraitOptions(identity.PhotoExpression, WardrobeContext.Campanha, card.PartyPrimary, identity.PhotoBackground, card.PartySecondary),
            Game.Instance.People.Appearance);
        var logo = f.Add(new LogoView());
        logo.Set(identity, card.Character.BallotName, 22);
        Ui.Value(f, $"{card.Office} · {card.PartySigla}", 14);
        Label slogan = Ui.Value(f, identity.Slogan, 13);
        slogan.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        slogan.CustomMinimumSize = new Vector2(200, 0);

        PanelContainer back = Card(card.PartySecondary);
        VBoxContainer b = Ui.Column(back, 4);
        var ink = new Color(card.PartyPrimary);
        foreach (Label l in new[]
        {
            Ui.Value(b, TranslationServer.Translate("Para"), 14),
            Ui.Value(b, card.Office, 18),
            Ui.Value(b, TranslationServer.Translate("vote"), 14),
            Ui.Value(b, identity.BallotNumber ?? "", 48),
            Ui.Value(b, card.Character.BallotName, 18),
            Ui.Paragraph(b, $"{card.PartySigla} — {card.PartyName}", 180, 12),
            Ui.Value(b, card.City, 12),
        })
        {
            l.AddThemeColorOverride("font_color", ink);
        }
    }

    private PanelContainer Card(string color)
    {
        var panel = this.Add(new PanelContainer { CustomMinimumSize = new Vector2(210, 330) });
        var style = new StyleBoxFlat { BgColor = new Color(color), CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10 };
        style.SetContentMarginAll(14);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }
}

/// <summary>Outdoor: faixa larga com retrato, nome de urna, número e slogan.</summary>
public partial class Outdoor : PanelContainer
{
    public void Set(CampaignCard card)
    {
        Ui.Clear(this);
        CampaignIdentity identity = card.Character.Campaign!;
        var style = new StyleBoxFlat { BgColor = new Color(card.PartyPrimary) };
        style.SetContentMarginAll(12);
        AddThemeStyleboxOverride("panel", style);
        CustomMinimumSize = new Vector2(520, 150);

        HBoxContainer row = Ui.Row(this, 16);
        var photo = row.Add(new PortraitView { CustomMinimumSize = new Vector2(126, 126) });
        photo.Show(
            card.Character.Appearance!,
            new PortraitOptions(Expression.SorrisoLargo, WardrobeContext.Campanha, card.PartyPrimary, identity.PhotoBackground, card.PartySecondary),
            Game.Instance.People.Appearance);
        VBoxContainer text = Ui.Column(row, 2);
        var logo = text.Add(new LogoView());
        logo.Set(identity, card.Character.BallotName, 34);
        Label slogan = Ui.Value(text, identity.Slogan, 20);
        slogan.AddThemeColorOverride("font_color", new Color(card.PartySecondary));
        Ui.Value(text, $"{card.Office} · {card.PartySigla}", 16);
    }
}
