using Godot;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

public partial class MainMenu : Control
{
    public override void _Ready()
    {
        var quit = GetNode<Button>("%QuitButton");
        quit.Pressed += OnQuitPressed;
        quit.TooltipText = Palette.Tip(Tr("Fecha o jogo."), Tr("sai para a área de trabalho; a carreira em andamento fica no último save automático."), Tr("nada."));
        var load = GetNode<Button>("%LoadButton");
        load.Disabled = false;
        load.Pressed += ShowLoadMenu;
        load.TooltipText = Palette.Tip(Tr("Abre uma carreira salva."), Tr("lista o save automático, o da última eleição, o do Ironman e os manuais; escolha um para continuar de onde parou."), Tr("nada."));
        var newGame = GetNode<Button>("%NewGameButton");
        newGame.Disabled = false;
        newGame.Pressed += () =>
        {
            Autoload.Game.Instance.PendingWorld = null;
            Autoload.Game.Instance.GoTo(Autoload.Game.Scenes.NewWorld);
        };
        newGame.TooltipText = Palette.Tip(Tr("Começa uma carreira nova."), Tr("abre o Novo Mundo, onde você escolhe os partidos e as opções do mundo; depois vem a criação do personagem."), Tr("nada; a carreira salva continua nos saves."));
        var options = GetNode<Button>("%OptionsButton");
        options.Disabled = false;
        options.Pressed += () => OptionsDialog.Open(GetTree().Root);
        options.TooltipText = Palette.Tip(Tr("Abre as Opções."), Tr("na aba Tempo, liga ou desliga cada pausa automática e religa as dicas de primeira vez."), Tr("nada."));
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

        menu.IndexPressed += i => TryLoad(existing[(int)i]);
        AddChild(menu);
        menu.PopupCentered();
    }

    /// <summary>
    /// Abre o save e segue para a carreira; se o save não abre (versão, divisão de zonas antiga, arquivo corrompido),
    /// mostra o motivo e fica no menu. Devolve a mensagem de erro, ou null se abriu.
    /// </summary>
    public string? TryLoad(string slot)
    {
        string? error = Autoload.SaveManager.Instance.Load(slot);
        if (error is null)
        {
            Autoload.Game.Instance.GoToCareer();
            return null;
        }

        var dialog = new AcceptDialog
        {
            Name = "ErroAoCarregar",
            Title = Tr("Não foi possível abrir o jogo salvo"),
            DialogText = Tr(error),
            OkButtonText = Tr("Voltar ao menu"),
            DialogAutowrap = true,
        };
        dialog.Confirmed += dialog.QueueFree;
        dialog.Canceled += dialog.QueueFree;
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(560, 0));
        return error;
    }
}
