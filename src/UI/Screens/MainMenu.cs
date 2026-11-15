using Godot;

namespace Planalto.UI.Screens;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%QuitButton").Pressed += OnQuitPressed;
        var newGame = GetNode<Button>("%NewGameButton");
        newGame.Disabled = false;
        newGame.Pressed += () =>
        {
            Autoload.Game.Instance.PendingWorld = null;
            Autoload.Game.Instance.GoTo(Autoload.Game.Scenes.NewWorld);
        };
    }

    private void OnQuitPressed() => GetTree().Quit();
}
