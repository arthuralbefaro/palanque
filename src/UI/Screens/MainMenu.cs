using Godot;

namespace Planalto.UI.Screens;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%QuitButton").Pressed += OnQuitPressed;
    }

    private void OnQuitPressed() => GetTree().Quit();
}
