using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Career;
using Planalto.Core.State;
using Planalto.UI.Widgets;
using Calendar = Planalto.Core.State.Calendar;

namespace Planalto.UI.Screens;

/// <summary>
/// Entressafra (GDD 6.2): fora de mandato até a próxima eleição municipal. Dá para trocar de partido, fundar um
/// partido, avançar semana a semana ou saltar até a abertura da pré-campanha.
/// </summary>
public partial class OutOfOfficeScreen : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private Game _game = null!;
    private VBoxContainer _root = null!;
    private string? _switchTo;
    private string _message = "";

    private GameState State => _game.State!;

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.State is null)
        {
            _game.GoTo(Game.Scenes.MainMenu);
            return;
        }

        if (CareerFlow.Mode(_game.State) != CareerMode.ForaDeMandato)
        {
            _game.GoToCareer();
            return;
        }

        _root = Ui.Screen(this, 32);
        Refresh();
    }

    private void Refresh()
    {
        Ui.Clear(_root);
        HBoxContainer bar = Ui.Row(_root, 20);
        Ui.Value(bar, string.Create(PtBr, $"{State.Calendar.WeekStart:dd/MM/yyyy}"), 26);
        string city = _game.Municipios.All.First(m => m.CodigoIbge == State.FocusMunicipio).Nome;
        Ui.Value(bar, string.Format(Tr("Fora de mandato · {0}"), city), 20);
        Ui.Value(bar, string.Format(Tr("Partido: {0}"), State.Party(State.Player.PartyId)?.Sigla ?? Tr("sem partido")), 20);
        Ui.Value(bar, string.Format(PtBr, "R$ {0:N0}", State.Player.Resources.Money), 20);
        Ui.Value(bar, string.Format(Tr("Capital político {0}"), State.Player.Resources.PoliticalCapital), 20);
        bar.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Ui.Button(bar, "Salvar", () => { SaveManager.Instance.Autosave(); _message = Tr("Jogo salvo."); Refresh(); });
        Ui.Button(bar, "Menu", () => { SaveManager.Instance.Autosave(); _game.GoTo(Game.Scenes.MainMenu); });

        int year = CareerFlow.NextMunicipalElectionYear(State);
        DateOnly opens = CareerFlow.CampaignOpens(year);
        Ui.Value(_root, string.Format(Tr("Próxima eleição municipal: {0}"), Calendar.ElectionFor(year).FirstRound.ToString("dd/MM/yyyy", PtBr)), 30);
        Ui.Paragraph(_root, string.Format(Tr("A pré-campanha abre em {0}, no prazo de filiação: até lá, quem quiser disputar precisa estar filiado a um partido registrado."), MandateTexts.Date(opens)), 1200, 18);
        if (!PartyAffiliation.CanRun(State, year))
        {
            Ui.Label(_root, "Do jeito que está, você não pode disputar: falta partido registrado e filiação dentro do prazo.", 18, Ui.Warning);
        }

        HBoxContainer actions = Ui.Row(_root, 16);
        Ui.Button(actions, "Avançar semana", () => Handle(_game.AdvanceWeek()));
        Ui.Button(actions, string.Format(Tr("Ir para a pré-campanha de {0}"), year), () => Handle(_game.SkipToNextElection()));
        Ui.Button(actions, "Fundar um partido", ShowFounding);

        Ui.Label(_root, "Partido", 24);
        var options = State.Parties.Where(p => p.Id != State.Player.PartyId).Select(p => (p.Id, $"{p.Number} · {p.Sigla} — {p.Name}")).ToList();
        _switchTo ??= options.FirstOrDefault().Id;
        HBoxContainer party = Ui.Row(_root, 12);
        Ui.Options(party, options, _switchTo, id => _switchTo = id, translate: false).CustomMinimumSize = new Vector2(620, 0);
        Ui.Button(party, "Filiar-se", () =>
        {
            if (_switchTo is not null)
            {
                _game.JoinParty(_switchTo);
                _message = string.Format(Tr("Você se filiou ao {0}."), State.Party(_switchTo)?.Sigla);
                _switchTo = null;
                SaveManager.Instance.Autosave();
                Refresh();
            }
        });

        if (_message.Length > 0)
        {
            Ui.Paragraph(_root, _message, 1200, 18);
        }
    }

    private void Handle(Game.WeekNote note)
    {
        SaveManager.Instance.Autosave();
        if (note == Game.WeekNote.CampanhaAberta)
        {
            _game.GoToCareer();
            return;
        }

        _message = note == Game.WeekNote.SemPartidoNoPrazo
            ? Tr("Passou o prazo de filiação sem partido válido: você não disputa esta eleição.")
            : "";
        Refresh();
    }

    private void ShowFounding()
    {
        var shade = this.Add(new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Stop });
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = shade.Add(new CenterContainer());
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        center.Add(new FoundingDialog(() =>
        {
            shade.QueueFree();
            Refresh();
        }));
    }
}
