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

/// <summary>
/// Material de campanha permitido (Lei 9.504/1997, art. 39; outdoor é proibido pelo § 8º): adesivo redondo com o
/// número, bandeira com o logotipo e peça para redes com retrato e slogan.
/// </summary>
public partial class MateriaisPermitidos : HBoxContainer
{
    public void Set(CampaignCard card)
    {
        Ui.Clear(this);
        AddThemeConstantOverride("separation", 12);
        CampaignIdentity identity = card.Character.Campaign!;
        Color primary = new(card.PartyPrimary);
        Color secondary = new(card.PartySecondary);

        // Adesivo: círculo com o número e o nome de urna.
        var sticker = Piece(new Vector2(150, 150), primary, 75);
        VBoxContainer s = Ui.Column(sticker, 0);
        s.Alignment = BoxContainer.AlignmentMode.Center;
        Label number = Ui.Value(s, identity.BallotNumber ?? "", 34);
        number.HorizontalAlignment = HorizontalAlignment.Center;
        number.AddThemeColorOverride("font_color", secondary);
        Label name = Ui.Value(s, card.Character.BallotName, 14);
        name.HorizontalAlignment = HorizontalAlignment.Center;
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.CustomMinimumSize = new Vector2(110, 0);
        sticker.TooltipText = TranslationServer.Translate("Adesivo: material permitido na campanha (Lei 9.504/1997, art. 38) (artigo a conferir).");

        // Bandeira: retângulo com o logotipo.
        var flag = Piece(new Vector2(200, 150), secondary, 6);
        VBoxContainer f = Ui.Column(flag, 4);
        f.Alignment = BoxContainer.AlignmentMode.Center;
        var logo = f.Add(new LogoView());
        logo.Set(identity, card.Character.BallotName, 26);
        Label flagNumber = Ui.Value(f, identity.BallotNumber ?? "", 22);
        flagNumber.AddThemeColorOverride("font_color", primary);
        flag.TooltipText = TranslationServer.Translate("Bandeira: permitida nas ruas durante a campanha, sem atrapalhar o trânsito (Lei 9.504/1997, art. 37, § 6º) (artigo a conferir).");

        // Peça para redes: retrato, slogan e número.
        var post = Piece(new Vector2(150, 150), primary, 10);
        VBoxContainer pc = Ui.Column(post, 2);
        var photo = pc.Add(new PortraitView { CustomMinimumSize = new Vector2(84, 84) });
        photo.Show(
            card.Character.Appearance!,
            new PortraitOptions(Expression.SorrisoLargo, WardrobeContext.Campanha, card.PartyPrimary, identity.PhotoBackground, card.PartySecondary),
            Game.Instance.People.Appearance);
        Label slogan = Ui.Value(pc, identity.Slogan, 12);
        slogan.AddThemeColorOverride("font_color", secondary);
        slogan.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        slogan.CustomMinimumSize = new Vector2(126, 0);
        Ui.Value(pc, $"{card.Office} · {card.PartySigla}", 12);
        post.TooltipText = TranslationServer.Translate("Peça para redes sociais: pode ser impulsionada com pagamento, dentro das regras (Lei 9.504/1997, art. 57-C) (artigo a conferir).");
    }

    private PanelContainer Piece(Vector2 size, Color color, int radius)
    {
        var panel = this.Add(new PanelContainer { CustomMinimumSize = size, MouseFilter = MouseFilterEnum.Stop });
        var style = new StyleBoxFlat { BgColor = color };
        style.SetCornerRadiusAll(radius);
        style.SetContentMarginAll(10);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }
}
