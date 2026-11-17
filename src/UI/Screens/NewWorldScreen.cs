using Godot;
using Planalto.Autoload;
using Planalto.Core.Parties;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;
using FileAccess = Godot.FileAccess;

namespace Planalto.UI.Screens;

/// <summary>Tela "Novo Mundo" (GDD 16.1): predefinição, lista de partidos editável com sorteio e travas, opções do mundo.</summary>
public partial class NewWorldScreen : Control
{
    private static string ScenarioFolder => Autoload.UserPaths.Of("scenarios");

    // Amostra de cor da tabela de partidos: menor que a do resto do jogo, para a linha não crescer.
    private static readonly Vector2 TableSwatch = new(40, 32);

    private Game _game = null!;
    private WorldSetup _setup = null!;
    private Rng _rng = null!;
    private VBoxContainer _rows = null!;
    private VBoxContainer _issues = null!;
    private HBoxContainer _options = null!;
    private Label _summary = null!;
    private Button _continue = null!;
    private readonly List<(Button Button, string Tip)> _structural = [];

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
            ChangePreset).TooltipText = Palette.Tip(Tr("Predefinição do mundo: quantos partidos há e como eles se repartem."), Tr("troca a lista de partidos pela da predefinição escolhida; ano, dificuldade e as outras opções do mundo ficam como estão."), Tr("nada; as edições feitas na lista se perdem."));
        _summary = Ui.Label(top, "", color: Ui.Muted);
        HBoxContainer actions = Ui.Row(screen, 12);
        top = actions;
        Ui.Button(top, "Sortear tudo", () => { _setup.RandomizeAll(_rng); Rebuild(); }, Palette.Tip(Tr("Sorteia todos os campos que não estão travados."), Tr("gera de novo nome, sigla, número, família, posição, tamanho, cores e cúpula de cada partido; o cadeado protege um campo do sorteio."), Tr("nada.")));
        Ui.Button(top, "Restaurar predefinição", () => { _setup.RestorePreset(_rng); Rebuild(); }, Palette.Tip(Tr("Volta a lista de partidos ao padrão da predefinição."), Tr("desfaz as edições e os sorteios feitos nesta tela."), Tr("nada; as edições se perdem.")));
        string addTip = Palette.Tip(Tr("Acrescenta um partido sorteado à lista."), Tr("o partido novo entra no fim da tabela, com todos os campos sorteados e editáveis."), Tr("nada."));
        _structural.Add((Ui.Button(top, "Adicionar partido", () => { _setup.AddParty(_rng); Rebuild(); }, addTip), addTip));
        Ui.Button(top, "Salvar cenário", SaveScenario, Palette.Tip(Tr("Grava esta lista de partidos e as opções num arquivo."), Tr("dá para carregar depois e começar outras carreiras no mesmo mundo."), Tr("nada.")));
        Ui.Button(top, "Carregar cenário", LoadScenario, Palette.Tip(Tr("Abre um mundo salvo antes."), Tr("troca a lista de partidos e as opções pelas do arquivo escolhido."), Tr("nada; o que está na tela é substituído.")));

        _options = Ui.Row(screen, 16);
        BuildOptions();

        // A tabela cabe na largura da tela (sem rolagem horizontal); só rola na vertical.
        var scroll = screen.Add(new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        VBoxContainer table = Ui.Column(scroll, 4);
        HBoxContainer header = Ui.Row(table, 6);
        foreach (var (text, width) in Columns)
        {
            Label l = Ui.Label(header, text, 16, Ui.Muted);
            l.CustomMinimumSize = new Vector2(width, 0);
        }

        _rows = Ui.Column(table, 4);

        HBoxContainer bottom = Ui.Row(screen, 16);
        var issueScroll = bottom.Add(new ScrollContainer { CustomMinimumSize = new Vector2(0, 110), SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _issues = Ui.Column(issueScroll, 2);
        Ui.Button(bottom, "Voltar", () => _game.GoTo(Game.Scenes.MainMenu), Palette.Tip(Tr("Volta ao menu principal."), Tr("sai do Novo Mundo sem criar a carreira."), Tr("nada; o mundo desta tela não é guardado (use Salvar cenário).")));
        _continue = Ui.Button(bottom, "Continuar", Continue);

        Rebuild();
    }

    private static readonly (string Text, int Width)[] Columns =
    [
        ("Logo", 44), ("Sigla", 144), ("Nome", 270), ("Número", 150), ("Família", 240), ("Posição", 152), ("Tamanho", 182), ("Cores e logotipo", 250), ("Cúpula", 194), ("", 108),
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
        Ui.Options(_options, years, o.StartYear.ToString(), y => { o.StartYear = int.Parse(y); RefreshIssues(); }, translate: false).TooltipText =
            Palette.Tip(Tr("Ano em que a carreira começa."), Tr("a primeira eleição é a municipal desse ano; só aparecem anos de eleição municipal."), Tr("nada."));

        Ui.Label(_options, "Dificuldade");
        Ui.Options(_options, catalog.Difficulties.Select(d => (d.Id, d.Name)), o.Difficulty, d => { o.Difficulty = d; RefreshIssues(); }).TooltipText =
            Palette.Tip(Tr("Dificuldade do mundo; a escolha do município sugere uma pelo porte."), Tr("fica gravada no mundo; nesta versão ela ainda não muda a simulação, que é a mesma em qualquer dificuldade."), Tr("nada."));

        Ui.Label(_options, "Eventos");
        Ui.Options(_options, Enum.GetValues<EventFrequency>().Select(f => (f.ToString(), WorldTexts.Frequency(f))), o.EventFrequency.ToString(), f => o.EventFrequency = Enum.Parse<EventFrequency>(f), translate: false).TooltipText =
            Palette.Tip(Tr("Frequência dos acontecimentos sorteados na campanha."), Tr("com mais eventos, mais decisões chegam fora da agenda; com menos, a campanha fica mais calma."), Tr("nada."));

        // Nome da opção, não o termo "jurídico" do glossário (o departamento jurídico da campanha): sem link.
        Glossary.Unlinked(Ui.Label(_options, "Realismo jurídico"));
        Ui.Options(_options, Enum.GetValues<LegalRealism>().Select(r => (r.ToString(), WorldTexts.Realism(r))), o.LegalRealism.ToString(), r => o.LegalRealism = Enum.Parse<LegalRealism>(r), translate: false).TooltipText =
            Palette.Tip(Tr("Rigor das regras de lei no mundo (Relaxado ou Rigoroso)."), Tr("fica gravado no mundo; nesta versão ainda não muda a simulação: prazos, limites e punições seguem sempre a lei vigente."), Tr("nada."));

        var ironman = _options.Add(new CheckBox { Text = Tr("Modo Ironman"), ButtonPressed = o.Ironman });
        ironman.TooltipText = Palette.Tip(Tr("Modo Ironman: um único save, gravado automaticamente."), Tr("não há slots manuais nem volta atrás; cada decisão fica como foi tomada."), Tr("nada."));
        ironman.Toggled += on => o.Ironman = on;
    }

    private void Rebuild()
    {
        Ui.Clear(_rows);
        foreach (PartyDraft party in _setup.Parties)
        {
            BuildRow(party);
        }

        foreach (var (b, tip) in _structural)
        {
            b.Disabled = _setup.IsScenario;
            b.TooltipText = b.Disabled ? Palette.Blocked(Tr("o cenário espelhado tem os partidos fixos: edite os campos, sem adicionar nem remover partidos."), tip) : tip;
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

        void Tools(HBoxContainer cell, PartyField field, string fieldName)
        {
            Ui.IconButton(cell, Ui.Icons.Sortear, Palette.Tip(string.Format(Tr("Sorteia só o campo {0} deste partido."), fieldName), Tr("troca o valor por um sorteado, dentro das regras do mundo (sem nome nem sigla de partido real)."), Tr("nada.")), () => { _setup.RandomizeField(party.Id, field, _rng); Rebuild(); });
            bool locked = party.IsLocked(field);
            Ui.IconButton(cell, locked ? Ui.Icons.Travado : Ui.Icons.Destravado, locked
                ? Palette.Tip(string.Format(Tr("Campo {0} travado: o sorteio geral não mexe nele."), fieldName), Tr("clique para destravar; o sorteio só desse campo continua valendo."), Tr("nada."))
                : Palette.Tip(string.Format(Tr("Trava o campo {0} antes do sorteio geral."), fieldName), Tr("Sortear tudo e o sorteio da linha deixam esse campo como está."), Tr("nada.")), () =>
            {
                party.SetLocked(field, !party.IsLocked(field));
                Rebuild();
            });
        }

        HBoxContainer sigla = Cell(144);
        var siglaEdit = Ui.Compact(sigla.Add(new LineEdit { Text = party.Sigla, MaxLength = 12 }), 70);
        siglaEdit.TextChanged += t => { party.Sigla = t; RefreshIssues(); };
        siglaEdit.TooltipText = Palette.Tip(string.Format(Tr("Sigla do partido: {0}."), party.Sigla), Tr("aparece no mapa, na apuração e ao lado dos candidatos; de 2 a 8 letras, nunca a de um partido real nem as mesmas letras em outra ordem."), Tr("nada."));
        Tools(sigla, PartyField.Sigla, Tr("sigla"));

        HBoxContainer name = Cell(270);
        var nameEdit = Ui.Compact(name.Add(new LineEdit { Text = party.Name }), 196);
        nameEdit.TextChanged += t => { party.Name = t; RefreshIssues(); };
        nameEdit.TooltipText = Palette.Tip(string.Format(Tr("Nome do partido: {0}."), party.Name), Tr("aparece na urna, no santinho e no noticiário; não pode conter o nome de um partido real."), Tr("nada."));
        Tools(name, PartyField.Name, Tr("nome"));

        HBoxContainer number = Cell(150);
        var numberEdit = Ui.Compact(number.Add(new SpinBox { MinValue = 10, MaxValue = 99, Value = party.Number }), 76);
        numberEdit.ValueChanged += v => { party.Number = (int)v; RefreshIssues(); };
        numberEdit.TooltipText = Palette.Tip(string.Format(Tr("Número do partido: {0}."), party.Number), Tr("é o começo do número de urna de todos os candidatos do partido; de 10 a 99, sem repetir."), Tr("nada."));
        Tools(number, PartyField.Number, Tr("número"));

        HBoxContainer family = Cell(240);
        Small(Ui.Options(family, _game.World.Parties.Families.Families.Select(f => (f.Id, f.Name)), party.FamilyId, f => { party.FamilyId = f; RefreshIssues(); }), 166, Tr("Família ideológica:"), Tr("define a faixa de posição do partido nos eixos econômico e de costumes; posição fora da faixa é erro do mundo."));
        Tools(family, PartyField.Family, Tr("família"));

        HBoxContainer position = Cell(152);
        Label positionLabel = Ui.Value(position, $"{party.Position.Economic:+0;-0;0} / {party.Position.Social:+0;-0;0}", 16);
        positionLabel.TooltipText = Tr("Econômico / Costumes");
        positionLabel.CustomMinimumSize = new Vector2(78, 0);
        Tools(position, PartyField.Position, Tr("posição"));

        HBoxContainer size = Cell(182);
        Small(Ui.Options(size, Enum.GetValues<PartySize>().Select(s => (s.ToString(), WorldTexts.Size(s))), party.Size.ToString(), s => { party.Size = Enum.Parse<PartySize>(s); RefreshIssues(); }, translate: false), 108, Tr("Tamanho do partido:"), Tr("pesa na força de partida do partido em cada município."));
        Tools(size, PartyField.Size, Tr("tamanho"));

        HBoxContainer visual = Cell(250);
        Ui.Compact(Ui.ColorPicker(visual, party.Colors.Primary, c => { party.Colors = party.Colors with { Primary = c }; badge.Set(party.LogoIcon, party.Colors.Primary, party.Colors.Secondary); RefreshIssues(); }, TableSwatch), 30).TooltipText =
            Palette.Tip(Tr("Cor principal do partido."), Tr("pinta o partido no mapa, na legenda e no logotipo; cores muito parecidas entre partidos geram aviso."), Tr("nada."));
        Ui.Compact(Ui.ColorPicker(visual, party.Colors.Secondary, c => { party.Colors = party.Colors with { Secondary = c }; badge.Set(party.LogoIcon, party.Colors.Primary, party.Colors.Secondary); }, TableSwatch), 30).TooltipText =
            Palette.Tip(Tr("Cor secundária do partido."), Tr("aparece no logotipo e no material de campanha dos candidatos do partido."), Tr("nada."));
        Small(Ui.Options(visual, _game.World.Parties.LogoIcons.Icons.Select(i => (i.Id, i.Name)), party.LogoIcon, i => { party.LogoIcon = i; badge.Set(party.LogoIcon, party.Colors.Primary, party.Colors.Secondary); }), 104, Tr("Logotipo:"), Tr("o símbolo genérico do partido, no logotipo e no material de campanha."));
        Tools(visual, PartyField.Visual, Tr("cores e logotipo"));

        HBoxContainer leadership = Cell(194);
        Small(Ui.Options(leadership, Enum.GetValues<LeadershipStyle>().Select(l => (l.ToString(), WorldTexts.Leadership(l))), party.Leadership.ToString(), l => party.Leadership = Enum.Parse<LeadershipStyle>(l), translate: false), 120, Tr("Cúpula do partido:"), Tr("o estilo de quem manda no partido; fica gravado no mundo, mas nesta versão ainda não muda a simulação."));
        Tools(leadership, PartyField.Leadership, Tr("cúpula"));

        Ui.IconButton(row, Ui.Icons.Sortear, Palette.Tip(string.Format(Tr("Sorteia a linha inteira do {0}."), party.Sigla), Tr("troca todos os campos deste partido; os campos travados ficam."), Tr("nada.")), () => { _setup.RandomizeParty(party.Id, _rng); Rebuild(); });
        string duplicateTip = Palette.Tip(string.Format(Tr("Duplica o {0}."), party.Sigla), Tr("acrescenta uma cópia no fim da lista, com nome, sigla e número novos."), Tr("nada."));
        _structural.Add((Ui.IconButton(row, Ui.Icons.Duplicar, duplicateTip, () => { _setup.DuplicateParty(party.Id, _rng); Rebuild(); }), duplicateTip));
        string removeTip = Palette.Tip(string.Format(Tr("Remove o {0} do mundo."), party.Sigla), Tr("tira o partido da lista; o mundo precisa de pelo menos 2 partidos."), Tr("nada; dá para Restaurar predefinição."));
        _structural.Add((Ui.IconButton(row, Ui.Icons.Remover, removeTip, () => { _setup.RemoveParty(party.Id); Rebuild(); }), removeTip));
    }

    /// <summary>
    /// Lista suspensa compacta: fonte menor, largura fixa e texto cortado com reticências no próprio botão; o tooltip
    /// traz o valor inteiro e as três partes.
    /// </summary>
    private OptionButton Small(OptionButton options, int width, string field, string does)
    {
        Ui.Compact(options, width);
        options.ClipText = true;
        options.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        string value = options.Selected >= 0 ? options.GetItemText(options.Selected) : "";
        options.TooltipText = Palette.Tip($"{field} {value}.", does, Tr("nada."));
        return options;
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
        string continueTip = Palette.Tip(Tr("Segue para a criação do personagem com este mundo."), Tr("os partidos e as opções ficam valendo para a carreira inteira."), Tr("nada."));
        _continue.TooltipText = _continue.Disabled ? Palette.Blocked(Tr("há erros no mundo, listados à esquerda (em vermelho); corrija para continuar."), continueTip) : continueTip;
        Glossary.LinkTerms(this);
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
