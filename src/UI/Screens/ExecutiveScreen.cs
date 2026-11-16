using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Executive;
using Planalto.Core.Legislature;
using Planalto.Core.State;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Prefeitura (GDD 5.9): orçamento por área com os mínimos legais, indicadores e seus alvos, políticas, coalizão
/// por secretarias, gabinete, notícias e crises.
/// </summary>
public partial class ExecutiveScreen : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private Game _game = null!;
    private ExecutiveEngine _engine = null!;
    private VBoxContainer _root = null!;
    private Dictionary<string, double> _draft = [];
    private readonly Dictionary<string, string> _targets = [];
    private readonly List<string> _log = [];
    private string? _crisisResult;
    private ColorRect? _crisisShade;

    private GameState State => _game.State!;

    private ExecutiveState Executive => State.Executive!;

    private ExecutiveConfig Config => _game.ExecutiveContent.Config;

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.State is null)
        {
            _game.GoTo(Game.Scenes.MainMenu);
            return;
        }

        if (_game.Prefeitura is null || _game.State.Campaign is not null)
        {
            _game.GoToCareer();
            return;
        }

        _engine = _game.Prefeitura;
        _draft = new Dictionary<string, double>(Executive.Allocation);
        _root = Ui.Screen(this, 20);
        Refresh();
    }

    private string Sigla(string? partyId) => partyId is null ? Tr("sem partido") : State.Party(partyId)?.Sigla ?? partyId;

    private string Money(double share) => string.Format(PtBr, "R$ {0:N1} mi", (double)_engine.AnnualRevenue * share / 1_000_000);

    private void Refresh()
    {
        Ui.Clear(_root);
        BuildTopBar();
        HBoxContainer body = Ui.Row(_root, 18);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        var leftScroll = body.Add(new ScrollContainer { CustomMinimumSize = new Vector2(520, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        VBoxContainer left = Ui.Column(leftScroll, 6);
        left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildBudget(left);
        BuildIndicators(left);
        var middleScroll = body.Add(new ScrollContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        VBoxContainer middle = Ui.Column(middleScroll, 6);
        middle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildPolicies(middle);
        BuildNews(middle);
        var rightScroll = body.Add(new ScrollContainer { CustomMinimumSize = new Vector2(430, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        VBoxContainer right = Ui.Column(rightScroll, 6);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildCabinet(right);
        BuildCoalition(right);
        BuildBottom();
        if (Executive.Crisis is not null || _crisisResult is not null)
        {
            ShowCrisis();
        }
        else if (_crisisShade is not null && IsInstanceValid(_crisisShade))
        {
            _crisisShade.QueueFree();
            _crisisShade = null;
        }
    }

    private void BuildTopBar()
    {
        HBoxContainer bar = Ui.Row(_root, 20);
        string office = _game.People.Offices.Name(Core.Elections.Office.Prefeito, State.Player.Gender);
        Ui.Value(bar, string.Create(PtBr, $"{State.Calendar.WeekStart:dd/MM/yyyy}"), 26);
        Ui.Value(bar, $"{office} · {_engine.Context.Municipio.Nome} ({_engine.Context.Municipio.Uf})", 20);
        Ui.Value(bar, string.Format(Tr("{0}º mandato, até {1}"), Executive.Term, MandateTexts.Date(Executive.End)), 18);
        bar.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Ui.Button(bar, "Salvar", () => { SaveManager.Instance.Autosave(); _log.Insert(0, Tr("Jogo salvo.")); Refresh(); });
        Ui.Button(bar, "Mapa", () => _game.GoTo(Game.Scenes.Map));
        Ui.Button(bar, "Menu", () => { SaveManager.Instance.Autosave(); _game.GoTo(Game.Scenes.MainMenu); });

        HBoxContainer stats = Ui.Row(_root, 26);
        Ui.Value(stats, string.Format(PtBr, "{0} {1:0}%", Tr("Aprovação"), Executive.Approval), 20);
        Ui.Value(stats, string.Format(Tr("Energia {0}/{1}"), Executive.EnergyLeft, Config.WeeklyEnergy), 17);
        Ui.Value(stats, string.Format(PtBr, "{0} {1:+0.00;-0.00;0.00}", Tr("Imagem"), Executive.Image), 17);
        Ui.Value(stats, string.Format(PtBr, "{0} {1:0}%", Tr("Reconhecimento"), Executive.Recognition), 17);
        Ui.Value(stats, string.Format(PtBr, "{0} {1:N0} mi", Tr("Orçamento anual R$"), (double)_engine.AnnualRevenue / 1_000_000), 17);
        Ui.Value(stats, string.Format(Tr("Partido: {0}"), Sigla(Executive.PartyId)), 17);
    }

    private void BuildBudget(VBoxContainer column)
    {
        Ui.Label(column, "Orçamento", 24);
        Ui.Label(column, "Mudanças valem aos poucos: os indicadores seguem o gasto com atraso.", 14, Ui.Muted);
        foreach (BudgetArea area in Config.Areas)
        {
            double share = _draft.GetValueOrDefault(area.Id);
            string minimum = area.MinimumShare > 0 ? string.Format(PtBr, Tr(" · mínimo {0:0}%"), area.MinimumShare * 100) : "";
            Label label = Ui.Value(column, string.Format(PtBr, "{0}: {1:0.0}% · {2}{3}", area.Name, share * 100, Money(share), minimum), 15);
            if (share + 1e-9 < area.MinimumShare)
            {
                label.AddThemeColorOverride("font_color", Ui.Error);
            }

            var slider = Ui.Slider(column, share, v => { _draft[area.Id] = v; Refresh(); }, 0, 0.5, 0.005);
            slider.CustomMinimumSize = new Vector2(480, 0);
        }

        double sum = _draft.Values.Sum();
        Label total = Ui.Value(column, string.Format(PtBr, Tr("Total: {0:0.0}%"), sum * 100), 16);
        if (Math.Abs(sum - 1) > 0.001)
        {
            total.AddThemeColorOverride("font_color", Ui.Warning);
        }

        HBoxContainer actions = Ui.Row(column, 10);
        Ui.Button(actions, "Ajustar para 100%", () =>
        {
            double s = Math.Max(1e-9, _draft.Values.Sum());
            _draft = _draft.ToDictionary(d => d.Key, d => d.Value / s);
            Refresh();
        }).AddThemeFontSizeOverride("font_size", 16);
        var apply = Ui.Button(actions, "Aplicar orçamento", () =>
        {
            ExecutiveFailure? failure = _engine.SetAllocation(_draft);
            _log.Insert(0, failure is null ? Tr("Orçamento atualizado.") : ExecutiveTexts.Failure(failure.Value));
            Refresh();
        });
        apply.AddThemeFontSizeOverride("font_size", 16);
        apply.Disabled = Math.Abs(sum - 1) > 0.001;
        Ui.Value(column, string.Format(PtBr, Tr("Reserva livre para programas: {0:0.0}% ({1})"), _engine.FreeReserve * 100, Money(Math.Max(0, _engine.FreeReserve))), 15);
    }

    private void BuildIndicators(VBoxContainer column)
    {
        Ui.Label(column, "Indicadores", 24);
        Dictionary<string, double> targets = _engine.Targets();
        foreach (IndicatorDefinition indicator in Config.Indicators)
        {
            double value = Executive.Indicators[indicator.Id];
            double target = targets[indicator.Id];
            string arrow = target > value + 0.5 ? "↑" : target < value - 0.5 ? "↓" : "→";
            Label label = Ui.Value(column, string.Format(PtBr, "{0}: {1:0} {2} {3:0}", indicator.Name, value, arrow, target), 16);
            label.AddThemeColorOverride("font_color", value >= 55 ? Ui.Ok : value <= 40 ? Ui.Error : Ui.Muted);
        }
    }

    private void BuildPolicies(VBoxContainer column)
    {
        Ui.Label(column, "Programas e políticas", 24);
        foreach (PolicyDefinition policy in _game.ExecutiveContent.Policies.Policies)
        {
            EnactedPolicy? enacted = Executive.Policies.LastOrDefault(p => p.PolicyId == policy.Id);
            PanelContainer panel = Ui.Panel(column, 10);
            VBoxContainer box = Ui.Column(panel, 4);
            string cost = policy.Cost >= 0
                ? string.Format(PtBr, Tr("custa {0:0.0}% ao ano ({1})"), policy.Cost * 100, Money(policy.Cost))
                : string.Format(PtBr, Tr("libera {0:0.0}% ao ano ({1})"), -policy.Cost * 100, Money(-policy.Cost));
            string law = policy.RequiresLaw ? string.Format(Tr("precisa de lei ({0})"), MandateTexts.Quorum(policy.Quorum)) : Tr("por decreto");
            Ui.Value(box, policy.Name, 18);
            Ui.Paragraph(box, $"{policy.Description} · {cost} · {law} · {string.Format(Tr("{0} semanas para funcionar"), policy.ImplementationWeeks)}", 760, 14);
            HBoxContainer row = Ui.Row(box, 10);
            if (enacted is { Counts: true })
            {
                Ui.Value(row, ExecutiveTexts.Status(enacted.Status), 15);
                Ui.Button(row, "Revogar", () => { _engine.Revoke(policy.Id); Refresh(); }).AddThemeFontSizeOverride("font_size", 14);
                continue;
            }

            if (enacted?.Status == PolicyStatus.Rejeitada)
            {
                Ui.Value(row, string.Format(Tr("rejeitada por {0} a {1}"), enacted.No, enacted.Yes), 14);
            }

            ExecutiveFailure? failure = _engine.CanEnact(policy);
            var button = Ui.Button(row, policy.RequiresLaw ? "Enviar à Câmara" : "Decretar", () =>
            {
                EnactResult result = _engine.Enact(policy.Id);
                _log.Insert(0, !result.Ok ? ExecutiveTexts.Failure(result.Failure!.Value)
                    : policy.RequiresLaw ? string.Format(Tr(result.Approved ? "Aprovado por {0} a {1}." : "Rejeitado: {0} a favor, {1} contra."), result.Yes, result.No)
                    : Tr("Decreto assinado."));
                Refresh();
            });
            button.AddThemeFontSizeOverride("font_size", 15);
            button.Disabled = failure is not null;
            if (failure is not null)
            {
                button.TooltipText = ExecutiveTexts.Failure(failure.Value);
            }
        }
    }

    private void BuildNews(VBoxContainer column)
    {
        Ui.Label(column, "Notícias da prefeitura", 22);
        foreach (string line in _log.Take(4))
        {
            Ui.Paragraph(column, "· " + line, 760, 15);
        }

        foreach (ExecutiveNews news in Executive.News.TakeLast(8).Reverse())
        {
            Ui.Paragraph(column, $"{news.Week:dd/MM} · {ExecutiveTexts.News(news)}", 760, 15);
        }
    }

    private void BuildCabinet(VBoxContainer column)
    {
        Ui.Label(column, "Gabinete", 24);
        foreach (MandateActionDefinition action in _game.ExecutiveContent.Actions.Actions)
        {
            List<(string Id, string Text)> targets = action.Kind == MandateActionKind.Articular
                ? Executive.Chamber.OrderByDescending(m => m.Relation).Select(m => (m.Id, string.Format(PtBr, "{0} ({1}) · {2:+0;-0;0}", m.Name, Sigla(m.PartyId), m.Relation))).ToList()
                : action.Targets.Select(t => (t.Id, t.Name)).ToList();
            string? target = targets.Count == 0 ? null : _targets.TryGetValue(action.Id, out string? chosen) && targets.Any(t => t.Id == chosen) ? chosen : targets[0].Id;
            var button = Ui.Button(column, $"{action.Name} · {action.Energy} {Tr("energia")}", () =>
            {
                string text = _engine.Execute(action.Id, target);
                _log.Insert(0, $"{action.Name}: " + (text.Length > 0 ? text : Tr("feito.")));
                Refresh();
            }, action.Description);
            button.Alignment = HorizontalAlignment.Left;
            button.AddThemeFontSizeOverride("font_size", 16);
            button.Disabled = _engine.CanExecute(action, target) is not null;
            if (targets.Count > 0)
            {
                Ui.Options(column, targets, target, id => { _targets[action.Id] = id; Refresh(); }, translate: false).AddThemeFontSizeOverride("font_size", 14);
            }
        }
    }

    private void BuildCoalition(VBoxContainer column)
    {
        Ui.Label(column, "Câmara e secretarias", 24);
        int baseSeats = Executive.Chamber.Count(m => Executive.InBase(m.PartyId));
        Ui.Value(column, string.Format(Tr("Base do governo: {0} de {1} vereadores"), baseSeats, Executive.Chamber.Count), 16);
        Ui.Value(column, string.Format(Tr("Secretarias: {0} no total"), Config.Secretarias), 15);
        foreach (var group in Executive.Chamber.Where(m => m.PartyId is not null).GroupBy(m => m.PartyId!).OrderByDescending(g => g.Count()))
        {
            HBoxContainer row = Ui.Row(column, 8);
            int secretarias = Executive.Secretarias.GetValueOrDefault(group.Key);
            double relation = group.Average(m => m.Relation);
            Ui.Value(row, string.Format(PtBr, "{0}{1}: {2} {3} · {4} {5} · {6:+0;-0;0}", Executive.InBase(group.Key) ? "● " : "○ ", Sigla(group.Key), group.Count(), Tr("cadeiras"), secretarias, Tr("secr."), relation), 15);
            if (group.Key != Executive.PartyId)
            {
                Ui.Button(row, "+", () => { Note(_engine.GiveSecretaria(group.Key)); }).AddThemeFontSizeOverride("font_size", 14);
                Ui.Button(row, "−", () => { Note(_engine.TakeSecretaria(group.Key)); }).AddThemeFontSizeOverride("font_size", 14);
            }
        }

        Ui.Paragraph(column, Tr("Dar uma secretaria põe o partido na base e melhora a relação da bancada; tirar irrita."), 400, 13);
    }

    private void Note(ExecutiveFailure? failure)
    {
        if (failure is not null)
        {
            _log.Insert(0, ExecutiveTexts.Failure(failure.Value));
        }

        Refresh();
    }

    private void BuildBottom()
    {
        HBoxContainer bottom = Ui.Row(_root, 16);
        bottom.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Ui.Button(bottom, "Fundar um partido", ShowFounding);
        Ui.Button(bottom, "Avançar semana", Advance).Disabled = Executive.Crisis is not null;
    }

    private void Advance()
    {
        Game.WeekNote note = _game.AdvanceWeek();
        SaveManager.Instance.Autosave();
        if (note is Game.WeekNote.CampanhaAberta or Game.WeekNote.MandatoEncerrado || State.Executive is null)
        {
            _game.GoToCareer();
            return;
        }

        if (note == Game.WeekNote.SemPartidoNoPrazo)
        {
            _log.Insert(0, Tr("Passou o prazo de filiação sem partido válido: você não disputa a eleição deste ano."));
        }

        _draft = new Dictionary<string, double>(Executive.Allocation);
        Refresh();
    }

    private void ShowCrisis()
    {
        // Um único overlay por vez, por cima da tela.
        if (_crisisShade is not null && IsInstanceValid(_crisisShade))
        {
            _crisisShade.QueueFree();
        }

        var shade = this.Add(new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Stop });
        _crisisShade = shade;
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = shade.Add(new CenterContainer());
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        PanelContainer panel = Ui.Panel(center, 24);
        panel.CustomMinimumSize = new Vector2(900, 0);
        VBoxContainer box = Ui.Column(panel, 12);
        if (_crisisResult is not null)
        {
            Ui.Paragraph(box, _crisisResult, 840, 20);
            Ui.Button(box, "Continuar", () => { _crisisResult = null; Refresh(); });
            return;
        }

        CrisisDefinition crisis = _game.ExecutiveContent.Crises.Find(Executive.Crisis!.CrisisId)!;
        Ui.Value(box, crisis.Name, 30);
        Ui.Paragraph(box, crisis.Text, 840, 20);
        foreach (var (choice, index) in crisis.Choices.Select((c, i) => (c, i)))
        {
            string cost = choice.Cost > 0 ? string.Format(PtBr, " ({0})", Money(choice.Cost)) : "";
            var button = Ui.Button(box, choice.Label + cost, () =>
            {
                _crisisResult = _engine.ResolveCrisis(index);
                Refresh();
            });
            button.Alignment = HorizontalAlignment.Left;
        }
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
