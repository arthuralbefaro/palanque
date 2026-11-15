using Godot;
using Planalto.Autoload;
using Planalto.Core.Characters;
using Planalto.Core.State;
using Planalto.UI.Portrait;

namespace Planalto.UI.Screens;

/// <summary>Hub semanal (GDD 6.1). Nesta etapa mostra o resumo da carreira recém-criada.</summary>
public partial class HubScreen : Control
{
    public override void _Ready()
    {
        Game game = Game.Instance;
        if (game.State is null)
        {
            game.GoTo(Game.Scenes.MainMenu);
            return;
        }

        GameState state = game.State;
        VBoxContainer screen = Ui.Screen(this);
        HBoxContainer row = Ui.Row(screen, 24);
        var portrait = row.Add(new PortraitView { CustomMinimumSize = new Vector2(300, 300) });
        GameParty? party = state.Party(state.Player.PartyId);
        portrait.Show(state.Player.Appearance!, new PortraitOptions(Planalto.Core.Characters.Expression.Sorriso, WardrobeContext.Casual, party?.Colors.Primary), game.People.Appearance);
        VBoxContainer info = Ui.Column(row, 6);
        Ui.Value(info, state.Player.BallotName, 36);
        Ui.Value(info, $"{party?.Sigla} · {state.Calendar.WeekStart:dd/MM/yyyy}", 20);
        Ui.Paragraph(info, state.Player.Biography ?? "", 900, 16);
    }
}
