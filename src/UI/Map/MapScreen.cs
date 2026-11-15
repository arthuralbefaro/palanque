using Godot;
using Planalto.Autoload;

namespace Planalto.UI.Map;

/// <summary>Mapa do estado em foco (protótipo 2.5D da Fase 3, GDD 13.1).</summary>
public partial class MapScreen : Control
{
    public override void _Ready()
    {
        VBoxContainer screen = Ui.Screen(this);
        Ui.Label(screen, "Mapa", 34);
        Ui.Button(screen, "Voltar", () => Game.Instance.GoTo(Game.Scenes.Hub));
    }
}
