using Godot;
using Planalto.Autoload;
using Planalto.Core.Characters;
using Planalto.Core.State;
using Planalto.UI.Portrait;
using Expression = Planalto.Core.Characters.Expression;

namespace Planalto.UI.Debug;

/// <summary>
/// Tela de conferência visual dos retratos: grade de pessoas sorteadas em expressões variadas.
/// Com "-- --screenshot=caminho.png", salva a imagem e fecha (usado para revisar a arte placeholder).
/// </summary>
public partial class PortraitSheet : Control
{
    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
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

        string? path = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--screenshot=", StringComparison.Ordinal))?.Split('=', 2)[1];
        if (path is not null)
        {
            CaptureAndQuit(path);
        }
    }

    private async void CaptureAndQuit(string path)
    {
        for (int i = 0; i < 4; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }

        GetViewport().GetTexture().GetImage().SavePng(path);
        GetTree().Quit();
    }
}
