using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.State;
using Planalto.UI.Portrait;

namespace Planalto.UI.Screens;

/// <summary>
/// A urna (GDD 16.3): estilizada, sem brasões ou marcas oficiais. Teclas 0–9, BRANCO, CORRIGE e CONFIRMA, também
/// pelo teclado (Enter confirma, Backspace corrige). Cargos na ordem real das eleições municipais: vereador e prefeito.
/// </summary>
public partial class UrnaScreen : Control
{
    private static readonly Office[] Order = [Office.Vereador, Office.Prefeito];

    private Game _game = null!;
    private CampaignEngine _engine = null!;
    private int _officeIndex;
    private string _digits = "";
    private bool _blank;
    private readonly List<UrnaVote> _votes = [];
    private VBoxContainer _display = null!;
    private bool _finished;
    private bool _resolved;

    private CampaignState Campaign => _game.State!.Campaign!;

    private Office Current => Order[_officeIndex];

    private int Digits => _engine.Context.State.Campaign!.ForOffice(Current).First(c => !c.IsBulk).Number.Length;

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.Engine is null || _game.State?.Campaign?.Phase != CampaignPhase.Votacao)
        {
            _game.GoTo(Game.Scenes.Hub);
            return;
        }

        _engine = _game.Engine;
        VBoxContainer screen = Ui.Screen(this, 32);
        Ui.Value(screen, string.Format(Tr("Seção eleitoral numa escola de {0}. O mesário confere seu documento e libera a urna."), _engine.Context.Municipio.Nome), 20);

        HBoxContainer urna = Ui.Row(screen, 28);
        urna.SizeFlagsVertical = SizeFlags.ExpandFill;
        PanelContainer displayPanel = Panel(urna, "#F2F2EE", new Vector2(980, 620));
        _display = Ui.Column(displayPanel, 14);
        BuildKeypad(Panel(urna, "#2B2B2B", new Vector2(520, 620)));

        HBoxContainer skip = Ui.Row(screen, 16);
        Ui.Button(skip, "Pular: votar em mim mesmo", () =>
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            _engine.CastPlayerVote(new UrnaVote(Office.Vereador, Campaign.Player.Number, false));
            Finish();
        });
        Ui.Button(skip, "Pular: não votar", () =>
        {
            _finished = true;
            Finish();
        });
        Refresh();
    }

    private static PanelContainer Panel(Node parent, string color, Vector2 size)
    {
        var panel = parent.Add(new PanelContainer { CustomMinimumSize = size });
        var style = new StyleBoxFlat { BgColor = new Color(color), CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12, CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12 };
        style.SetContentMarginAll(28);
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private void BuildKeypad(PanelContainer panel)
    {
        VBoxContainer column = Ui.Column(panel, 14);
        var grid = column.Add(new GridContainer { Columns = 3 });
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 14);
        foreach (string key in new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "", "0", "" })
        {
            if (key.Length == 0)
            {
                grid.Add(new Control());
                continue;
            }

            var b = Ui.Button(grid, key, () => Type(key[0]));
            b.CustomMinimumSize = new Vector2(130, 80);
            b.AddThemeFontSizeOverride("font_size", 34);
        }

        HBoxContainer special = Ui.Row(column, 10);
        Key(special, "BRANCO", "#F5F5F5", "#111111", PressBlank);
        Key(special, "CORRIGE", "#E76F2E", "#111111", Correct);
        Key(special, "CONFIRMA", "#2A9D4F", "#111111", Confirm);
    }

    private static void Key(HBoxContainer row, string text, string background, string foreground, Action action)
    {
        var b = Ui.Button(row, text, action);
        b.CustomMinimumSize = new Vector2(146, 70);
        b.AddThemeFontSizeOverride("font_size", 20);
        var style = new StyleBoxFlat { BgColor = new Color(background), CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6 };
        foreach (string state in new[] { "normal", "hover", "pressed" })
        {
            b.AddThemeStyleboxOverride(state, style);
        }

        b.AddThemeColorOverride("font_color", new Color(foreground));
        b.AddThemeColorOverride("font_hover_color", new Color(foreground));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key || _finished)
        {
            return;
        }

        if (key.Keycode is >= Godot.Key.Key0 and <= Godot.Key.Key9)
        {
            Type((char)('0' + (key.Keycode - Godot.Key.Key0)));
        }
        else if (key.Keycode is >= Godot.Key.Kp0 and <= Godot.Key.Kp9)
        {
            Type((char)('0' + (key.Keycode - Godot.Key.Kp0)));
        }
        else if (key.Keycode is Godot.Key.Enter or Godot.Key.KpEnter)
        {
            Confirm();
        }
        else if (key.Keycode == Godot.Key.Backspace)
        {
            Correct();
        }
    }

    private void Type(char digit)
    {
        if (_finished || _blank || _digits.Length >= Digits)
        {
            return;
        }

        _digits += digit;
        Refresh();
    }

    private void PressBlank()
    {
        if (_digits.Length == 0)
        {
            _blank = true;
            Refresh();
        }
    }

    private void Correct()
    {
        _digits = "";
        _blank = false;
        Refresh();
    }

    private void Confirm()
    {
        bool legend = Current == Office.Vereador && _digits.Length == 2 && PartyByNumber(_digits) is not null;
        if (!_blank && _digits.Length != Digits && !legend)
        {
            return;
        }

        _votes.Add(new UrnaVote(Current, _blank ? null : _digits, _blank));
        _digits = "";
        _blank = false;
        if (++_officeIndex < Order.Length)
        {
            Refresh();
            return;
        }

        foreach (UrnaVote vote in _votes)
        {
            _engine.CastPlayerVote(vote);
        }

        _finished = true;
        Ui.Clear(_display);
        Ui.Value(_display, "FIM", 160).HorizontalAlignment = HorizontalAlignment.Center;
        GetTree().CreateTimer(1.6).Timeout += () =>
        {
            // A tela pode ter sido liberada nesse intervalo (troca de cena); o timer é da árvore, não da tela.
            if (IsInstanceValid(this) && IsInsideTree())
            {
                Finish();
            }
        };
    }

    private void Finish()
    {
        if (_resolved)
        {
            return;
        }

        _resolved = true;
        _engine.ResolveElection();
        SaveManager.Instance.Autosave();
        _game.GoTo(Game.Scenes.ElectionNight);
    }

    private GameParty? PartyByNumber(string digits) =>
        _game.State!.Parties.FirstOrDefault(p => p.Number.ToString(CultureInfo.InvariantCulture) == digits && Campaign.ForOffice(Office.Vereador).Any(c => c.PartyId == p.Id));

    private void Refresh()
    {
        Ui.Clear(_display);
        var ink = new Color("#1A1A1A");
        Label Line(string text, int size)
        {
            Label l = Ui.Value(_display, text, size);
            l.AddThemeColorOverride("font_color", ink);
            return l;
        }

        Line(Tr("SEU VOTO PARA"), 20);
        Line(_engine.Context.People.Offices.Name(Current, Gender.NaoBinario).ToUpperInvariant(), 38);

        HBoxContainer boxes = Ui.Row(_display, 10);
        for (int i = 0; i < Digits; i++)
        {
            var box = boxes.Add(new PanelContainer { CustomMinimumSize = new Vector2(62, 78) });
            var style = new StyleBoxFlat { BgColor = new Color("#FFFFFF"), BorderColor = ink };
            style.SetBorderWidthAll(2);
            box.AddThemeStyleboxOverride("panel", style);
            Label digit = Ui.Value(box, i < _digits.Length ? _digits[i].ToString() : "", 48);
            digit.HorizontalAlignment = HorizontalAlignment.Center;
            digit.AddThemeColorOverride("font_color", ink);
        }

        if (_blank)
        {
            Line(Tr("VOTO EM BRANCO"), 40);
        }
        else if (_digits.Length == Digits)
        {
            Candidate? candidate = Campaign.ForOffice(Current).FirstOrDefault(c => !c.IsBulk && c.Number == _digits);
            if (candidate is null)
            {
                Line(Tr("NÚMERO ERRADO"), 30);
                Line(Tr("VOTO NULO"), 40);
            }
            else
            {
                HBoxContainer who = Ui.Row(_display, 18);
                Appearance? face = candidate.IsPlayer ? _game.State!.Player.Appearance : _game.State!.Npcs.FirstOrDefault(n => n.Id == candidate.CharacterId)?.Appearance;
                if (face is not null)
                {
                    var portrait = who.Add(new PortraitView { CustomMinimumSize = new Vector2(180, 180) });
                    portrait.Show(face, new PortraitOptions(Planalto.Core.Characters.Expression.Sorriso, WardrobeContext.Institucional), _game.People.Appearance);
                }

                VBoxContainer info = Ui.Column(who, 6);
                Label name = Ui.Value(info, $"{Tr("Nome")}: {candidate.Name}", 26);
                name.AddThemeColorOverride("font_color", ink);
                Label party = Ui.Value(info, $"{Tr("Partido")}: {_game.State!.Party(candidate.PartyId)?.Sigla}", 26);
                party.AddThemeColorOverride("font_color", ink);
            }
        }
        else if (Current == Office.Vereador && _digits.Length == 2 && PartyByNumber(_digits) is { } p)
        {
            Line($"{Tr("Partido")}: {p.Sigla} — {p.Name}", 24);
            Line(Tr("Aperte CONFIRMA para votar na legenda."), 20);
        }

        Line("", 10);
        Line(Tr("Aperte a tecla CONFIRMA para CONFIRMAR este voto ou CORRIGE para REINICIAR este voto."), 18);
    }
}
