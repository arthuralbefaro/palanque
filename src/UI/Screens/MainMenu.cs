using Godot;

namespace Planalto.UI.Screens;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        GetNode<Button>("%QuitButton").Pressed += OnQuitPressed;
        var load = GetNode<Button>("%LoadButton");
        load.Disabled = false;
        load.Pressed += ShowLoadMenu;
        var newGame = GetNode<Button>("%NewGameButton");
        newGame.Disabled = false;
        newGame.Pressed += () =>
        {
            Autoload.Game.Instance.PendingWorld = null;
            Autoload.Game.Instance.GoTo(Autoload.Game.Scenes.NewWorld);
        };
    }

    private void OnQuitPressed() => GetTree().Quit();

    private void ShowLoadMenu()
    {
        Autoload.SaveManager saves = Autoload.SaveManager.Instance;
        string[] slots = [Autoload.SaveManager.AutosaveSlot, Autoload.SaveManager.ElectionSlot, Autoload.SaveManager.IronmanSlot, .. saves.ManualSlots()];
        var existing = slots.Where(saves.Exists).ToList();
        var menu = new PopupMenu();
        foreach (string slot in existing)
        {
            menu.AddItem(slot);
        }

        if (existing.Count == 0)
        {
            menu.AddItem(Tr("Nenhum save encontrado"));
            menu.SetItemDisabled(0, true);
        }

        menu.IndexPressed += i =>
        {
            if (saves.Load(existing[(int)i]) is null)
            {
                Autoload.Game.Instance.GoTo(Autoload.Game.Scenes.Hub);
            }
        };
        AddChild(menu);
        menu.PopupCentered();
    }
}
