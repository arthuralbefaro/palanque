using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Legislature;
using Planalto.Core.State;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Mandato de vereador (GDD 5.8): pauta da semana com o voto do jogador, ações de gabinete, notícias da Câmara,
/// projetos próprios e o plenário com relações, base do prefeito e lideranças.
/// </summary>
public partial class MandateScreen : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private Game _game = null!;
    private LegislatureEngine _engine = null!;
    private VBoxContainer _root = null!;
    private readonly List<string> _log = [];
    private readonly Dictionary<string, string> _targets = [];
    private bool _confirmSwitch;
    private string? _switchTo;

    private GameState State => _game.State!;

    private MandateState Mandate => State.Mandate!;

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.State is null)
        {
            _game.GoTo(Game.Scenes.MainMenu);
            return;
        }

        if (_game.Mandate is null || _game.State.Campaign is not null)
        {
            _game.GoToCareer();
            return;
        }

        _engine = _game.Mandate;
        _root = Ui.Screen(this, 20);
        Refresh();
    }

    public void Refresh()
    {
        Ui.Clear(_root);
        BuildTopBar();
        BuildNotices();
        HBoxContainer body = Ui.Row(_root, 18);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        var leftScroll = body.Add(new ScrollContainer { CustomMinimumSize = new Vector2(480, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        VBoxContainer left = Ui.Column(leftScroll, 6);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildCabinet(left);
        var middleScroll = body.Add(new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        VBoxContainer middle = Ui.Column(middleScroll, 8);
        middle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildPauta(middle);
        BuildNews(middle);
        BuildOwnBills(middle);
        var rightScroll = body.Add(new ScrollContainer { CustomMinimumSize = new Vector2(420, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        VBoxContainer right = Ui.Column(rightScroll, 4);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildChamber(right);
        BuildBottom();
    }

    private string Sigla(string? partyId) => partyId is null ? Tr("sem partido") : State.Party(partyId)?.Sigla ?? partyId;

    private void BuildTopBar()
    {
        // Duas linhas: identidade e navegação em cima, indicadores embaixo (numa linha só não cabe em 1920 px).
        HBoxContainer bar = Ui.Row(_root, 20);
        string office = _game.People.Offices.Name(Core.Elections.Office.Vereador, State.Player.Gender);
        Ui.Value(bar, string.Create(PtBr, $"{State.Calendar.WeekStart:dd/MM/yyyy}"), 26);
        Ui.Value(bar, $"{office} · {_engine.Context.Municipio.Nome} ({_engine.Context.Municipio.Uf})", 20);
        Ui.Value(bar, string.Format(Tr("Mandato até {0}"), MandateTexts.Date(Mandate.End)), 18);
        bar.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Ui.Button(bar, "Salvar", () => { SaveManager.Instance.Autosave(); _log.Insert(0, Tr("Jogo salvo.")); Refresh(); });
        Ui.Button(bar, "Mapa", () => _game.GoTo(Game.Scenes.Map));
        Ui.Button(bar, "Menu", () => { SaveManager.Instance.Autosave(); _game.GoTo(Game.Scenes.MainMenu); });

        HBoxContainer stats = Ui.Row(_root, 26);
        Ui.Value(stats, string.Format(Tr("Energia {0}/{1}"), Mandate.EnergyLeft, _game.Legislature.Config.WeeklyEnergy), 17);
        Ui.Value(stats, string.Format(Tr("Capital político {0}"), State.Player.Resources.PoliticalCapital), 17);
        Ui.Value(stats, string.Format(Tr("Fidelidade {0}"), Mandate.Fidelity), 17);
        Ui.Value(stats, string.Format(PtBr, "{0} {1:+0;-0;0}", Tr("Relação com o prefeito"), Mandate.MayorRelation), 17);
        Ui.Value(stats, string.Format(PtBr, "{0} {1:0}%", Tr("Reconhecimento"), Mandate.Recognition), 17);
        Ui.Value(stats, string.Format(PtBr, "{0} {1:+0.00;-0.00;0.00}", Tr("Imagem"), Mandate.Image), 17);
        Ui.Value(stats, string.Format(Tr("Partido: {0}"), Sigla(State.Player.PartyId)), 17);
    }

    private void BuildNotices()
    {
        if (_engine.InRecess)
        {
            Ui.Label(_root, "Recesso parlamentar: sem sessões nesta semana. O gabinete continua aberto.", 16, Ui.Muted);
        }

        if (Mandate.Expelled)
        {
            Ui.Label(_root, "Você está sem partido: o mandato continua, mas só disputa a reeleição quem estiver filiado até o prazo de abril.", 16, Ui.Warning);
        }
        else if (Mandate.Sanctioned)
        {
            Ui.Label(_root, "Sancionado pelo partido: fora da comissão e da liderança.", 16, Ui.Warning);
        }

        if (PartyAffiliation.InWindow(State))
        {
            Ui.Label(_root, "Janela partidária aberta: dá para trocar de partido sem perder o mandato.", 16, Ui.Ok);
        }
    }

    private void BuildCabinet(VBoxContainer column)
    {
        Ui.Label(column, "Gabinete", 24);
        foreach (MandateActionDefinition action in _game.Legislature.Actions.Actions)
        {
            var targets = Targets(action);
            if (action.Kind == MandateActionKind.Urgencia && targets.Count == 0)
            {
                continue;
            }

            string? target = targets.Count == 0 ? null : _targets.TryGetValue(action.Id, out string? chosen) && targets.Any(t => t.Id == chosen) ? chosen : targets[0].Id;
            string cost = action.Capital > 0 ? string.Format(Tr(" · {0} de capital"), action.Capital) : "";
            string extra = action.Kind == MandateActionKind.Emenda ? string.Format(Tr(" · restam {0}"), Mandate.EmendasLeft) : "";
            MandateActionFailure? failure = _engine.CanExecute(action, target);
            var button = Ui.Button(column, $"{action.Name} · {action.Energy} {Tr("energia")}{cost}{extra}", () =>
            {
                MandateActionResult result = _engine.Execute(action.Id, target);
                _log.Insert(0, $"{action.Name}: " + (result.Text.Length > 0 ? result.Text : Tr("feito.")));
                Refresh();
            }, action.Description);
            button.Alignment = HorizontalAlignment.Left;
            button.AddThemeFontSizeOverride("font_size", 17);
            button.Disabled = failure is not null;
            if (failure is not null)
            {
                button.TooltipText = $"{action.Description}\n{MandateTexts.Failure(failure.Value)}";
            }

            if (targets.Count > 0)
            {
                Ui.Options(column, targets, target, id => { _targets[action.Id] = id; Refresh(); }, translate: false)
                    .AddThemeFontSizeOverride("font_size", 15);
            }

            if (action.Kind == MandateActionKind.Propor && target is not null && _game.Legislature.MunicipalBills.Find(target) is { } bill)
            {
                Ui.Paragraph(column, bill.Summary, 450, 14);
            }
        }

        Ui.Button(column, "Fundar um partido", ShowFounding).AddThemeFontSizeOverride("font_size", 17);
        BuildPartySwitch(column);

        if (_log.Count > 0)
        {
            Ui.Label(column, "Resultados", 18, Ui.Muted);
        }

        foreach (string line in _log.Take(6))
        {
            Ui.Paragraph(column, "· " + line, 450, 14);
        }
    }

    private List<(string Id, string Text)> Targets(MandateActionDefinition action) => action.Kind switch
    {
        MandateActionKind.Propor => _engine.ProposableBills().Select(b => (b.Id, b.Title)).ToList(),
        MandateActionKind.Urgencia => Mandate.Bills.Where(b => b.AuthorId == RaceSetup.PlayerId && b.Stage == BillStage.Comissao).Select(b => (b.Id, b.Title)).ToList(),
        MandateActionKind.Articular => Mandate.Members.Where(m => !m.IsPlayer).OrderByDescending(m => m.Relation).Select(m => (m.Id, string.Format(PtBr, "{0} ({1}) · {2:+0;-0;0}", m.Name, Sigla(m.PartyId), m.Relation))).ToList(),
        _ => action.Targets.Select(t => (t.Id, t.Name)).ToList(),
    };

    /// <summary>Trocar de partido: livre sem partido ou na janela; fora dela custa o mandato e pede confirmação.</summary>
    private void BuildPartySwitch(VBoxContainer column)
    {
        var options = State.Parties.Where(p => p.Id != State.Player.PartyId).Select(p => (p.Id, $"{p.Number} · {p.Sigla} — {p.Name}")).ToList();
        _switchTo ??= options.FirstOrDefault().Id;
        bool loses = PartyAffiliation.LosesMandate(State);
        Ui.Label(column, loses ? "Trocar de partido (fora da janela: perde o mandato)" : "Filiar-se a outro partido", 16, loses ? Ui.Error : Ui.Muted);
        Ui.Options(column, options, _switchTo, id => { _switchTo = id; _confirmSwitch = false; Refresh(); }, translate: false).AddThemeFontSizeOverride("font_size", 15);
        Ui.Button(column, loses && _confirmSwitch ? "Confirmar e perder o mandato" : "Filiar-se", () =>
        {
            if (_switchTo is null)
            {
                return;
            }

            if (loses && !_confirmSwitch)
            {
                _confirmSwitch = true;
                Refresh();
                return;
            }

            _game.JoinParty(_switchTo);
            SaveManager.Instance.Autosave();
            if (State.Mandate is null)
            {
                _game.GoToCareer();
                return;
            }

            _confirmSwitch = false;
            _switchTo = null;
            Refresh();
        }).AddThemeFontSizeOverride("font_size", 15);
    }

    private void BuildPauta(VBoxContainer column)
    {
        Ui.Label(column, "Pauta da semana", 24);
        var pauta = _engine.Pauta.ToList();
        if (pauta.Count == 0)
        {
            Ui.Label(column, _engine.InRecess ? "Recesso: sem votações." : "Nenhum projeto em votação nesta semana.", 16, Ui.Muted);
        }

        foreach (Bill bill in pauta)
        {
            BillTemplate template = _engine.Template(bill);
            PanelContainer panel = Ui.Panel(column, 12);
            VBoxContainer box = Ui.Column(panel, 4);
            Ui.Value(box, bill.Title, 20);
            string author = bill.AuthorId == Bill.MayorAuthor ? Tr("Prefeitura")
                : bill.AuthorId == RaceSetup.PlayerId ? Tr("Você")
                : Mandate.Member(bill.AuthorId)?.Name ?? bill.AuthorId;
            Ui.Value(box, string.Format(Tr("Autor: {0} · Quórum: {1}"), author, MandateTexts.Quorum(template.Quorum)), 14);
            Ui.Paragraph(box, template.Summary, 760, 15);
            int orientation = _engine.PlayerOrientation(bill);
            string line = Mandate.Player.Leader
                ? Tr("Você é líder da bancada: seu voto orienta os colegas de partido.")
                : Mandate.Player.PartyId is null ? Tr("Sem partido: você vota livre.")
                : string.Format(Tr("Orientação do {0}: {1}"), Sigla(Mandate.Player.PartyId), MandateTexts.Orientation(orientation));
            Ui.Value(box, line, 15);
            HBoxContainer votes = Ui.Row(box, 10);
            foreach (var (choice, text) in new[] { (VoteChoice.Sim, "Sim"), (VoteChoice.Nao, "Não"), (VoteChoice.Abstencao, "Abstenção") })
            {
                var b = Ui.Button(votes, text, () => { _engine.CastVote(bill.Id, choice); Refresh(); });
                b.ToggleMode = true;
                b.ButtonPressed = bill.PlayerVote == choice;
                b.AddThemeFontSizeOverride("font_size", 16);
            }

            if (bill.PlayerVote is null)
            {
                Ui.Label(box, "Sem voto marcado: avançar a semana conta como falta.", 14, Ui.Warning);
            }
        }
    }

    private void BuildNews(VBoxContainer column)
    {
        Ui.Label(column, "Notícias da Câmara", 22);
        var recent = Mandate.News.TakeLast(8).Reverse().ToList();
        if (recent.Count == 0)
        {
            Ui.Label(column, "Nada ainda: a legislatura está começando.", 15, Ui.Muted);
        }

        foreach (MandateNews news in recent)
        {
            Ui.Paragraph(column, $"{news.Week:dd/MM} · {MandateTexts.News(news, State.Player.Gender)}", 780, 15);
        }
    }

    private void BuildOwnBills(VBoxContainer column)
    {
        var own = Mandate.Bills.Where(b => b.AuthorId == RaceSetup.PlayerId).Reverse().Take(6).ToList();
        if (own.Count == 0)
        {
            return;
        }

        Ui.Label(column, "Seus projetos", 22);
        foreach (Bill bill in own)
        {
            Ui.Paragraph(column, $"{bill.Title} · {MandateTexts.Stage(bill.Stage)}", 780, 15);
        }
    }

    private void BuildChamber(VBoxContainer column)
    {
        Ui.Label(column, "Câmara Municipal", 24);
        Ui.Paragraph(column, string.Format(Tr("Prefeito: {0} ({1}). Base: {2}."), Mandate.MayorName, Sigla(Mandate.MayorPartyId), string.Join(", ", Mandate.BaseParties.Select(Sigla))), 390, 15);
        if (Mandate.Member(Mandate.PresidentId ?? "") is { } president)
        {
            Ui.Value(column, string.Format(Tr("Presidente da Câmara: {0}"), president.Name), 15);
        }

        string committee = _game.Legislature.Config.Committees.FirstOrDefault(c => c.Id == Mandate.Committee)?.Name ?? Tr("nenhuma");
        Ui.Paragraph(column, string.Format(Tr("Sua comissão: {0}"), committee), 390, 15);
        int baseSeats = Mandate.Members.Count(m => Mandate.InBase(m.PartyId));
        Ui.Value(column, string.Format(Tr("Base do governo: {0} de {1} cadeiras"), baseSeats, Mandate.Members.Count), 15);
        foreach (ChamberMember m in Mandate.Members.OrderByDescending(m => Mandate.InBase(m.PartyId)).ThenBy(m => m.PartyId).ThenByDescending(m => m.Votes))
        {
            string marks = (m.Leader ? " · " + Tr("líder") : "") + (m.Id == Mandate.PresidentId ? " · " + Tr("presidente") : "");
            string relation = m.IsPlayer ? Tr("você") : string.Format(PtBr, "{0:+0;-0;0}", m.Relation);
            Label label = Ui.Paragraph(column, $"{(Mandate.InBase(m.PartyId) ? "● " : "○ ")}{m.Name} ({Sigla(m.PartyId)}){marks} · {relation}", 390, 14);
            if (m.IsPlayer)
            {
                label.AddThemeColorOverride("font_color", Ui.Warning);
            }
            else if (m.Relation >= 25)
            {
                label.AddThemeColorOverride("font_color", Ui.Ok);
            }
            else if (m.Relation <= -25)
            {
                label.AddThemeColorOverride("font_color", Ui.Error);
            }
        }

        Ui.Paragraph(column, Tr("● base do prefeito · ○ oposição ou independente"), 390, 13);
    }

    private void BuildBottom()
    {
        HBoxContainer bottom = Ui.Row(_root, 16);
        bottom.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Ui.Button(bottom, "Avançar semana", Advance);
    }

    private void Advance()
    {
        Game.WeekNote note = _game.AdvanceWeek();
        SaveManager.Instance.Autosave();
        switch (note)
        {
            case Game.WeekNote.CampanhaAberta:
            case Game.WeekNote.MandatoEncerrado:
                _game.GoToCareer();
                return;
            case Game.WeekNote.SemPartidoNoPrazo:
                _log.Insert(0, Tr("Passou o prazo de filiação sem partido válido: você não disputa a eleição deste ano."));
                break;
        }

        if (State.Mandate is null)
        {
            _game.GoToCareer();
            return;
        }

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
