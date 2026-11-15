using Godot;
using Planalto.Autoload;
using Planalto.Core.Parties;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.Widgets;
using FileAccess = Godot.FileAccess;

namespace Planalto.UI.Screens;

/// <summary>Tela "Novo Mundo" (GDD 16.1): predefinição, lista de partidos editável com sorteio e travas, opções do mundo.</summary>
public partial class NewWorldScreen : Control
{
    private const string ScenarioFolder = "user://scenarios";

    private Game _game = null!;
    private WorldSetup _setup = null!;
    private Rng _rng = null!;
    private VBoxContainer _rows = null!;
    private VBoxContainer _issues = null!;
    private HBoxContainer _options = null!;
    private Label _summary = null!;
    private Button _continue = null!;
    private readonly List<Button> _structural = [];

    public override void _Ready()
    {
        _game = Game.Instance;
        _game.PendingSeed = Time.GetTicksUsec();
        _rng = new Rng(_game.PendingSeed);
        _setup = _game.PendingWorld ?? WorldSetup.Create(_game.World, _game.World.Presets.DefaultPreset, _rng).Setup!;

        VBoxContainer screen = Ui.Screen(this);
        Ui.Label(screen, "Novo Mundo", 40);

        HBoxContainer top = Ui.Row(screen, 12);
        Ui.Label(top, "Predefinição");
        Ui.Options(
            top,
            _game.World.Presets.Presets.Where(p => p.Available).Select(p => (p.Id, p.Name)),
            _setup.PresetId,
            ChangePreset);
        _summary = Ui.Label(top, "", color: Ui.Muted);
        HBoxContainer actions = Ui.Row(screen, 12);
        top = actions;
        Ui.Button(top, "Sortear tudo", () => { _setup.RandomizeAll(_rng); Rebuild(); }, "Sorteia todos os campos que não estão travados.");
        Ui.Button(top, "Restaurar predefinição", () => { _setup.RestorePreset(_rng); Rebuild(); });
        _structural.Add(Ui.Button(top, "Adicionar partido", () => { _setup.AddParty(_rng); Rebuild(); }));
        Ui.Button(top, "Salvar cenário", SaveScenario);
        Ui.Button(top, "Carregar cenário", LoadScenario);

        _options = Ui.Row(screen, 16);
        BuildOptions();

        // Cabeçalho e linhas rolam juntos na horizontal: as linhas são mais largas que telas menores.
        var scroll = screen.Add(new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        VBoxContainer table = Ui.Column(scroll, 4);
        HBoxContainer header = Ui.Row(table, 6);
        foreach (var (text, width) in Columns)
        {
            Label l = Ui.Label(header, text, color: Ui.Muted);
            l.CustomMinimumSize = new Vector2(width, 0);
        }

        _rows = Ui.Column(table, 4);

        HBoxContainer bottom = Ui.Row(screen, 16);
        var issueScroll = bottom.Add(new ScrollContainer { CustomMinimumSize = new Vector2(0, 110), SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _issues = Ui.Column(issueScroll, 2);
        Ui.Button(bottom, "Voltar", () => _game.GoTo(Game.Scenes.MainMenu));
        _continue = Ui.Button(bottom, "Continuar", Continue);

        Rebuild();
    }

    private static readonly (string Text, int Width)[] Columns =
    [
        ("Logo", 48), ("Sigla", 150), ("Nome", 330), ("Número", 130), ("Família", 270), ("Posição", 150), ("Tamanho", 180), ("Cores e logotipo", 260), ("Cúpula", 210), ("", 200),
    ];

    private void ChangePreset(string presetId)
    {
        WorldSetupCreation creation = WorldSetup.Create(_game.World, presetId, _rng);
        if (creation.Setup is not null)
        {
            WorldOptions options = _setup.Options;
            _setup = creation.Setup;
            CopyOptions(options, _setup.Options);
        }

        Rebuild();
    }

    private static void CopyOptions(WorldOptions from, WorldOptions to)
    {
        to.StartYear = from.StartYear;
        to.Difficulty = from.Difficulty;
        to.EventFrequency = from.EventFrequency;
        to.Ironman = from.Ironman;
        to.LegalRealism = from.LegalRealism;
    }

    private void BuildOptions()
    {
        Ui.Clear(_options);
        WorldOptions o = _setup.Options;
        WorldOptionsCatalog catalog = _game.World.Options;

        Ui.Label(_options, "Ano inicial");
        var years = Enumerable.Range(catalog.MinStartYear, 41).Where(catalog.IsValidStartYear).Select(y => (y.ToString(), y.ToString()));
        Ui.Options(_options, years, o.StartYear.ToString(), y => { o.StartYear = int.Parse(y); RefreshIssues(); }, translate: false);

        Ui.Label(_options, "Dificuldade");
        Ui.Options(_options, catalog.Difficulties.Select(d => (d.Id, d.Name)), o.Difficulty, d => { o.Difficulty = d; RefreshIssues(); });

        Ui.Label(_options, "Eventos");
        Ui.Options(_options, Enum.GetValues<EventFrequency>().Select(f => (f.ToString(), WorldTexts.Frequency(f))), o.EventFrequency.ToString(), f => o.EventFrequency = Enum.Parse<EventFrequency>(f), translate: false);

        Ui.Label(_options, "Realismo jurídico");
        Ui.Options(_options, Enum.GetValues<LegalRealism>().Select(r => (r.ToString(), WorldTexts.Realism(r))), o.LegalRealism.ToString(), r => o.LegalRealism = Enum.Parse<LegalRealism>(r), translate: false);

        var ironman = _options.Add(new CheckBox { Text = Tr("Modo Ironman"), ButtonPressed = o.Ironman, TooltipText = Tr("Um único save, gravado automaticamente.") });
        ironman.Toggled += on => o.Ironman = on;
    }

    private void Rebuild()
    {
        Ui.Clear(_rows);
        foreach (PartyDraft party in _setup.Parties)
        {
            BuildRow(party);
        }

        foreach (Button b in _structural)
        {
            b.Disabled = _setup.IsScenario;
        }

        _summary.Text = string.Format(Tr("{0} partidos"), _setup.Parties.Count) + (_setup.IsScenario ? " · " + Tr("cenário espelhado: edite os campos, sem adicionar ou remover partidos") : "");
        BuildOptions();
        RefreshIssues();
    }

    private void BuildRow(PartyDraft party)
    {
        HBoxContainer row = Ui.Row(_rows, 6);
        var badge = row.Add(new PartyBadge());
        badge.Set(party.LogoIcon, party.Colors.Primary, party.Colors.Secondary);

        HBoxContainer Cell(int width)
        {
            var cell = Ui.Row(row, 2);
            cell.CustomMinimumSize = new Vector2(width, 0);
            return cell;
        }

        void Tools(HBoxContainer cell, PartyField field)
        {
            Ui.Button(cell, "S", () => { _setup.RandomizeField(party.Id, field, _rng); Rebuild(); }, "Sortear este campo");
            var lockBox = cell.Add(new CheckBox { ButtonPressed = party.IsLocked(field), TooltipText = Tr("Travar antes do sorteio geral") });
            lockBox.Toggled += on => party.SetLocked(field, on);
        }

        HBoxContainer sigla = Cell(150);
        var siglaEdit = sigla.Add(new LineEdit { Text = party.Sigla, CustomMinimumSize = new Vector2(90, 0), MaxLength = 12 });
        siglaEdit.TextChanged += t => { party.Sigla = t; RefreshIssues(); };
        Tools(sigla, PartyField.Sigla);

        HBoxContainer name = Cell(330);
        var nameEdit = name.Add(new LineEdit { Text = party.Name, CustomMinimumSize = new Vector2(270, 0) });
        nameEdit.TextChanged += t => { party.Name = t; RefreshIssues(); };
        Tools(name, PartyField.Name);

        HBoxContainer number = Cell(130);
        var numberEdit = number.Add(new SpinBox { MinValue = 10, MaxValue = 99, Value = party.Number, CustomMinimumSize = new Vector2(80, 0) });
        numberEdit.ValueChanged += v => { party.Number = (int)v; RefreshIssues(); };
        Tools(number, PartyField.Number);

        HBoxContainer family = Cell(270);
        Ui.Options(family, _game.World.Parties.Families.Families.Select(f => (f.Id, f.Name)), party.FamilyId, f => { party.FamilyId = f; RefreshIssues(); });
        Tools(family, PartyField.Family);

        HBoxContainer position = Cell(150);
        Ui.Value(position, $"{party.Position.Economic:+0;-0;0} / {party.Position.Social:+0;-0;0}").TooltipText = Tr("Econômico / Costumes");
        Tools(position, PartyField.Position);

        HBoxContainer size = Cell(180);
        Ui.Options(size, Enum.GetValues<PartySize>().Select(s => (s.ToString(), WorldTexts.Size(s))), party.Size.ToString(), s => { party.Size = Enum.Parse<PartySize>(s); RefreshIssues(); }, translate: false);
        Tools(size, PartyField.Size);

        HBoxContainer visual = Cell(260);
        Ui.ColorPicker(visual, party.Colors.Primary, c => { party.Colors = party.Colors with { Primary = c }; badge.Set(party.LogoIcon, party.Colors.Primary, party.Colors.Secondary); RefreshIssues(); });
        Ui.ColorPicker(visual, party.Colors.Secondary, c => { party.Colors = party.Colors with { Secondary = c }; badge.Set(party.LogoIcon, party.Colors.Primary, party.Colors.Secondary); });
        Ui.Options(visual, _game.World.Parties.LogoIcons.Icons.Select(i => (i.Id, i.Name)), party.LogoIcon, i => { party.LogoIcon = i; badge.Set(party.LogoIcon, party.Colors.Primary, party.Colors.Secondary); });
        Tools(visual, PartyField.Visual);

        HBoxContainer leadership = Cell(210);
        Ui.Options(leadership, Enum.GetValues<LeadershipStyle>().Select(l => (l.ToString(), WorldTexts.Leadership(l))), party.Leadership.ToString(), l => party.Leadership = Enum.Parse<LeadershipStyle>(l), translate: false);
        Tools(leadership, PartyField.Leadership);

        Ui.Button(row, "Sortear linha", () => { _setup.RandomizeParty(party.Id, _rng); Rebuild(); });
        _structural.Add(Ui.Button(row, "Duplicar", () => { _setup.DuplicateParty(party.Id, _rng); Rebuild(); }));
        _structural.Add(Ui.Button(row, "Remover", () => { _setup.RemoveParty(party.Id); Rebuild(); }));
    }

    private void RefreshIssues()
    {
        Ui.Clear(_issues);
        WorldValidationReport report = _setup.Validate();
        string PartyName(string? id) => id is null ? "" : _setup.Find(id)?.Sigla is { Length: > 0 } s ? s : id;
        foreach (WorldIssue issue in report.Errors)
        {
            Ui.Value(_issues, "✕ " + WorldTexts.Issue(issue, PartyName)).AddThemeColorOverride("font_color", Ui.Error);
        }

        foreach (WorldIssue issue in report.Warnings)
        {
            Ui.Value(_issues, "! " + WorldTexts.Issue(issue, PartyName)).AddThemeColorOverride("font_color", Ui.Warning);
        }

        if (report.Issues.Count == 0)
        {
            Ui.Label(_issues, "Tudo certo.", color: Ui.Ok);
        }

        _continue.Disabled = !report.IsValid;
    }

    private void SaveScenario()
    {
        DirAccess.MakeDirRecursiveAbsolute(ScenarioFolder);
        var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.SaveFile, Access = FileDialog.AccessEnum.Userdata, CurrentDir = ScenarioFolder, Filters = ["*.json"] };
        dialog.FileSelected += path =>
        {
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreString(WorldSetupSerializer.Serialize(_setup));
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }

    private void LoadScenario()
    {
        DirAccess.MakeDirRecursiveAbsolute(ScenarioFolder);
        var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Userdata, CurrentDir = ScenarioFolder, Filters = ["*.json"] };
        dialog.FileSelected += path =>
        {
            WorldSetupLoadResult result = WorldSetupSerializer.Deserialize(FileAccess.GetFileAsString(path), _game.World);
            if (result.Setup is not null)
            {
                _setup = result.Setup;
                Rebuild();
            }
            else
            {
                Ui.Clear(_issues);
                Ui.Value(_issues, Tr("Não foi possível ler o cenário: ") + result.Issue).AddThemeColorOverride("font_color", Ui.Error);
            }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }

    private void Continue()
    {
        if (!_setup.Validate().IsValid)
        {
            return;
        }

        _game.PendingWorld = _setup;
        _game.GoTo(Game.Scenes.CharacterCreator);
    }
}
