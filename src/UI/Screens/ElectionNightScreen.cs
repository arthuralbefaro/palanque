using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.State;

namespace Planalto.UI.Screens;

/// <summary>
/// Noite da apuração (GDD 8.2): as parciais sobem seção a seção até o resultado final, com o ranking de
/// vereadores, cadeiras por partido e a disputa pela prefeitura.
/// </summary>
public partial class ElectionNightScreen : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private Game _game = null!;
    private CampaignState _campaign = null!;
    private VBoxContainer _root = null!;
    private int _step;
    private Godot.Timer _timer = null!;

    private GameState State => _game.State!;

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.State?.Campaign?.Outcome is null)
        {
            if (_game.State is null)
            {
                _game.GoTo(Game.Scenes.MainMenu);
            }
            else
            {
                _game.GoToCareer();
            }

            return;
        }

        _campaign = _game.State.Campaign;
        _root = Ui.Screen(this, 24);
        _timer = this.Add(new Godot.Timer { WaitTime = 0.9, Autostart = true });
        _timer.Timeout += () =>
        {
            if (_step < _campaign.Outcome!.Vereador.Snapshots.Count - 1)
            {
                _step++;
                Refresh();
            }
            else
            {
                _timer.Stop();
            }
        };
        Refresh();
    }

    private string Sigla(string partyId) => State.Party(partyId)?.Sigla ?? partyId;

    private void Refresh()
    {
        Ui.Clear(_root);
        ElectionOutcome outcome = _campaign.Outcome!;
        CountSnapshot vereador = outcome.Vereador.Snapshots[_step];
        CountSnapshot prefeito = outcome.Prefeito.Snapshots[Math.Min(_step, outcome.Prefeito.Snapshots.Count - 1)];
        bool final = _step == outcome.Vereador.Snapshots.Count - 1;

        HBoxContainer header = Ui.Row(_root, 20);
        Ui.Value(header, string.Format(Tr("Noite da apuração em {0}"), _game.Engine!.Context.Municipio.Nome), 34);
        header.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        if (!final)
        {
            Ui.Button(header, "Pular para o resultado", () => { _step = outcome.Vereador.Snapshots.Count - 1; Refresh(); });
        }

        var bar = _root.Add(new ProgressBar { MinValue = 0, MaxValue = 100, Value = vereador.PercentCounted, CustomMinimumSize = new Vector2(0, 30) });
        bar.ShowPercentage = false;
        Ui.Value(_root, string.Format(PtBr, Tr("{0:0.0}% das seções apuradas"), vereador.PercentCounted), 22);

        HBoxContainer body = Ui.Row(_root, 40);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        VBoxContainer left = Ui.Column(body, 4);
        left.CustomMinimumSize = new Vector2(820, 0);
        Ui.Label(left, "Vereadores mais votados", 24);
        long totalNominal = vereador.Votes.Values.Sum();
        int position = 0;
        foreach (var (id, votes) in vereador.Votes.Where(v => !_campaign.Find(v.Key)!.IsBulk).OrderByDescending(v => v.Value).Take(16))
        {
            Candidate c = _campaign.Find(id)!;
            position++;
            string elected = final && outcome.Vereador.Elected.Contains(id) ? " · " + Tr("eleito(a)") : "";
            Label line = Ui.Value(left, string.Format(PtBr, "{0,2}. {1} ({2}) {3:N0} {4} ({5:0.00}%){6}", position, c.Name, Sigla(c.PartyId), votes, Tr("votos"), totalNominal == 0 ? 0 : 100.0 * votes / totalNominal, elected), 18);
            if (c.IsPlayer)
            {
                line.AddThemeColorOverride("font_color", Ui.Warning);
            }
        }

        if (vereador.Votes.Where(v => !_campaign.Find(v.Key)!.IsBulk).OrderByDescending(v => v.Value).Take(16).All(v => v.Key != RaceSetup.PlayerId))
        {
            Label me = Ui.Value(left, string.Format(PtBr, "… {0}: {1:N0} {2}", _campaign.Player.Name, vereador.Votes[RaceSetup.PlayerId], Tr("votos")), 18);
            me.AddThemeColorOverride("font_color", Ui.Warning);
        }

        VBoxContainer right = Ui.Column(body, 6);
        Ui.Label(right, "Prefeitura", 24);
        long totalMayor = prefeito.Votes.Values.Sum();
        foreach (var (id, votes) in prefeito.Votes.OrderByDescending(v => v.Value))
        {
            Candidate c = _campaign.Find(id)!;
            string won = final && outcome.Prefeito.Elected.Contains(id) ? " · " + (outcome.Prefeito.HadRunoff ? Tr("eleito(a) no 2º turno") : Tr("eleito(a)")) : "";
            Ui.Value(right, string.Format(PtBr, "{0} ({1}) {2:0.0}%{3}", c.Name, Sigla(c.PartyId), totalMayor == 0 ? 0 : 100.0 * votes / totalMayor, won), 18);
        }

        if (final)
        {
            Ui.Label(right, "Cadeiras na Câmara", 24);
            foreach (var (party, seats) in outcome.Vereador.SeatsByParty.Where(s => s.Value > 0).OrderByDescending(s => s.Value))
            {
                Ui.Value(right, $"{Sigla(party)}: {seats}", 18);
            }

            Ui.Value(right, string.Format(PtBr, "{0}: {1:N0}", Tr("Quociente eleitoral"), outcome.Vereador.ElectoralQuotient), 16);
            BuildVerdict(outcome);
        }
    }

    private void BuildVerdict(ElectionOutcome outcome)
    {
        Character player = State.Player;
        string o = player.Gender switch { Gender.Masculino => "o", Gender.Feminino => "a", _ => "e" };
        long votes = outcome.Vereador.Votes[RaceSetup.PlayerId];
        string verdict = outcome.PlayerElected
            ? string.Format(PtBr, Tr("Você foi eleit{0} com {1:N0} votos!"), o, votes)
            : string.Format(PtBr, Tr("Não foi desta vez: {0:N0} votos."), votes);
        Label label = Ui.Value(_root, verdict, 36);
        label.AddThemeColorOverride("font_color", outcome.PlayerElected ? Ui.Ok : Ui.Warning);
        HBoxContainer actions = Ui.Row(_root, 16);
        Ui.Button(actions, outcome.PlayerElected ? "Tomar posse" : "Continuar a carreira", () =>
        {
            // Posse em 1º de janeiro (ou fim da campanha): o calendário salta e a carreira segue.
            _game.ConcludeElection();
            SaveManager.Instance.Autosave();
            _game.GoToCareer();
        });
        Ui.Button(actions, "Nova carreira", () => _game.GoTo(Game.Scenes.MainMenu));
    }
}
