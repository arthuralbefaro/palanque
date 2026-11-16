using Godot;
using Planalto.Autoload;
using Planalto.Core.Characters;
using Planalto.Core.State;
using Planalto.UI.Portrait;
using Expression = Planalto.Core.Characters.Expression;

namespace Planalto.UI.Debug;

/// <summary>
/// Tela de conferência visual dos retratos. Sem opções: grade de pessoas sorteadas em expressões variadas.
/// Com "-- --sheet=encaixes --page=0|1|2": folha de encaixes, uma linha por formato de rosto e uma coluna por
/// cabelo (dez por página), com orelhas e brincos variando, para conferir brinco e cabelo em cada rosto.
/// Com "--size=LxA" a janela fica exatamente desse tamanho; com "--screenshot=caminho.png", salva a imagem e fecha.
/// </summary>
public partial class PortraitSheet : Control
{
    private const int HairsPerPage = 10;

    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        string? Arg(string name) => args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?.Split('=', 2)[1];
        if (Arg("size")?.Split('x') is [var width, var height])
        {
            var size = new Vector2I(int.Parse(width), int.Parse(height));
            WindowFit.ExactForCapture(size);
            GetTree().Root.Size = size;
        }

        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        if (Arg("sheet") == "encaixes")
        {
            BuildFitSheet(int.Parse(Arg("page") ?? "0"));
        }
        else
        {
            BuildRandomSheet();
        }

        if (Arg("screenshot") is { } path)
        {
            CaptureAndQuit(path);
        }
    }

    private void BuildRandomSheet()
    {
        var grid = this.Add(new GridContainer { Columns = 6 });
        grid.SetAnchorsPreset(LayoutPreset.FullRect);
        var rng = new Rng(2028);
        Expression[] expressions = Enum.GetValues<Expression>();
        string[] ufs = ["SP", "BA", "RS", "AM", "PE", "SC", "MG", "PA", "GO", "RJ", "CE", "PR"];
        for (int i = 0; i < 12; i++)
        {
            Gender gender = (Gender)(i % 3);
            Appearance a = AppearanceGenerator.Random(rng, Game.Instance.People.Appearance, gender, 25 + (i * 4), ufs[i]);
            var view = grid.Add(new PortraitView { CustomMinimumSize = new Vector2(300, 300) });
            view.Show(a, new PortraitOptions(expressions[i % expressions.Length], (WardrobeContext)(i % 5), "#1D4E89", "background_solido", "#457B9D"), Game.Instance.People.Appearance);
        }
    }

    /// <summary>Seis rostos (linhas) por dez cabelos (colunas): a mesma pessoa, só trocando rosto, cabelo, orelha e brinco.</summary>
    private void BuildFitSheet(int page)
    {
        AppearanceContent content = Game.Instance.People.Appearance;
        var faces = content.Catalog.InCategory("face");
        var hairs = content.Catalog.InCategory("hair").Skip(page * HairsPerPage).Take(HairsPerPage).ToList();
        var ears = content.Catalog.InCategory("ears");
        var earrings = content.Catalog.InCategory("earrings");
        Appearance person = AppearanceGenerator.Random(new Rng(9), content, Gender.Feminino, 30, "SP") with
        {
            Beard = null, Mustache = null, Glasses = null, Scar = null, Piercing = null, Necklace = null,
            HairGray = 0, HairColor = "#3B2314", BrowColor = "#3B2314", PartyPin = false, Freckles = 0, Wrinkles = 0, EyeBags = 0,
            LipstickColor = null, EyeshadowColor = null, EyelinerIntensity = 0, ShoulderWidth = 0.5,
        };

        var background = this.Add(new ColorRect { Color = new Color("#0E1116") });
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var rows = this.Add(new VBoxContainer());
        rows.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        rows.AddThemeConstantOverride("separation", 2);
        for (int row = 0; row < faces.Count; row++)
        {
            var line = rows.Add(new HBoxContainer());
            line.AddThemeConstantOverride("separation", 2);
            var name = line.Add(new Label { Text = faces[row].Name, CustomMinimumSize = new Vector2(132, 0), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            name.AddThemeFontSizeOverride("font_size", 20);
            for (int column = 0; column < hairs.Count; column++)
            {
                var ear = ears[(row + column) % ears.Count];
                var earring = earrings[column % earrings.Count];
                Appearance a = person with
                {
                    Face = faces[row].Id,
                    Hair = hairs[column].Id,
                    Ears = ear.Id,
                    Earrings = earring.Id,
                    SkinColor = content.Palettes.Skin[(4 + (row * 3)) % content.Palettes.Skin.Count],
                };

                // A célula recorta o retrato ampliado na cabeça, que é onde ficam os encaixes.
                var cell = line.Add(new Control { CustomMinimumSize = new Vector2(176, 176), ClipContents = true });
                var view = cell.Add(new PortraitView { Position = new Vector2(-62, -26), Size = new Vector2(300, 300) });
                view.Show(a, new PortraitOptions(Expression.Neutra, WardrobeContext.Institucional, "#1D4E89", "background_solido", "#B9C6D3"), content);
                var caption = cell.Add(new Label { Text = $"{hairs[column].Name}\n{earring.Name}", Position = new Vector2(4, 138) });
                caption.AddThemeFontSizeOverride("font_size", 13);
                caption.AddThemeConstantOverride("line_spacing", -3);
                caption.AddThemeColorOverride("font_color", new Color("#ECEFF4"));
                caption.AddThemeColorOverride("font_outline_color", new Color("#0E1116"));
                caption.AddThemeConstantOverride("outline_size", 4);
            }
        }
    }

    private async void CaptureAndQuit(string path)
    {
        for (int i = 0; i < 6; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }

        GetViewport().GetTexture().GetImage().SavePng(path);
        GetTree().Quit();
    }
}
