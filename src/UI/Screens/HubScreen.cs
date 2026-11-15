using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Electorate;
using Planalto.Core.Elections;
using Planalto.Core.State;
using Planalto.UI.Portrait;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Hub semanal (GDD 6.1): manchetes, eventos, agenda com Energia e Dinheiro, painéis de pesquisa e rivais,
/// avançar semana. Lê o estado e chama o motor da campanha; nenhuma regra fica aqui.
/// </summary>
public partial class HubScreen : Control
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private Game _game = null!;
    private CampaignEngine _engine = null!;
    private VBoxContainer _root = null!;
    private readonly List<string> _log = [];
    private string? _visitTarget;

    private GameState State => _game.State!;

    private CampaignState Campaign => State.Campaign!;

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.State is null || _game.Engine is null)
        {
            _game.GoTo(Game.Scenes.MainMenu);
            return;
        }

        _engine = _game.Engine;
        _root = Ui.Screen(this, 20);
        Refresh();
    }

    public void Refresh()
    {
        Ui.Clear(_root);
        BuildTopBar();
        HBoxContainer body = Ui.Row(_root, 18);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        BuildAgenda(Ui.Column(body, 6));
        VBoxContainer middle = Ui.Column(body, 10);
        middle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        BuildHeadlines(middle);
        BuildPolls(middle);
        BuildRivals(Ui.Column(body, 6));
        BuildBottom();
        ShowPendingEvent();
    }

    private string Money(decimal value) => string.Create(PtBr, $"R$ {value:N0}");

    private string PartySigla(string partyId) => State.Party(partyId)?.Sigla ?? partyId;

    private string NameOf(Candidate c) => c.IsBulk ? string.Format(Tr("Demais candidatos do {0}"), PartySigla(c.PartyId)) : c.Name;

    private void BuildTopBar()
    {
        HBoxContainer bar = Ui.Row(_root, 22);
        string phase = Campaign.Phase switch
        {
            CampaignPhase.PreCampanha => Tr("Pré-campanha"),
            CampaignPhase.Campanha => Tr("Campanha"),
            CampaignPhase.Votacao => Tr("Semana da eleição"),
            _ => Tr("Eleição encerrada"),
        };
        Ui.Value(bar, string.Create(PtBr, $"{State.Calendar.WeekStart:dd/MM/yyyy}"), 26);
        Ui.Value(bar, $"{phase} · {_engine.Context.Municipio.Nome} ({_engine.Context.Municipio.Uf})", 20);
        Ui.Value(bar, string.Format(Tr("Faltam {0} semanas"), _engine.WeeksToElection), 20);
        Ui.Value(bar, string.Format(Tr("Energia {0}/{1}"), Campaign.EnergyLeft, _engine.Context.Config.WeeklyEnergy), 20);
        Ui.Value(bar, Money(State.Player.Resources.Money), 20);
        Ui.Value(bar, string.Format(PtBr, "{0} {1:0}%", Tr("Reconhecimento"), Campaign.Player.Recognition), 20);
        Ui.Value(bar, string.Format(PtBr, "{0} {1:+0.00;-0.00;0.00}", Tr("Imagem"), Campaign.Player.Image), 20);
        bar.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Ui.Button(bar, "Salvar", ShowSaveMenu);
        Ui.Button(bar, "Mapa", () => _game.GoTo(Game.Scenes.Map));
        Ui.Button(bar, "Menu", () => { SaveManager.Instance.Autosave(); _game.GoTo(Game.Scenes.MainMenu); });
    }

    private void BuildAgenda(VBoxContainer column)
    {
        column.CustomMinimumSize = new Vector2(470, 0);
        Ui.Label(column, "Agenda da semana", 24);
        foreach (CampaignActionDefinition action in _game.Campaign.Actions.Actions)
        {
            decimal cost = _engine.CostOf(action);
            string money = cost >= 0 ? Money(cost) : "+" + Money(-cost);
            string? target = action.Targets.Count > 0 ? _visitTarget ?? action.Targets[0].Id : null;
            ActionFailure? failure = _engine.CanExecute(action, target);
            var button = Ui.Button(column, $"{action.Name} · {action.Energy} {Tr("energia")} · {money}", () =>
            {
                ActionResult result = _engine.Execute(action.Id, target);
                _log.Insert(0, $"{action.Name}: " + (result.Text.Length > 0 ? result.Text : Tr("feito.")));
                Refresh();
            }, action.Description);
            button.Alignment = HorizontalAlignment.Left;
            button.AddThemeFontSizeOverride("font_size", 18);
            button.Disabled = failure is not null;
            if (failure is not null)
            {
                button.TooltipText = $"{action.Description}\n{FailureText(failure.Value)}";
            }

            if (action.Targets.Count > 0)
            {
                Ui.Options(column, action.Targets.Select(t => (t.Id, t.Name)), _visitTarget ?? action.Targets[0].Id, id => { _visitTarget = id; Refresh(); }, translate: false)
                    .AddThemeFontSizeOverride("font_size", 16);
            }
        }

        if (_log.Count > 0)
        {
            Ui.Label(column, "Resultados", 18, Ui.Muted);
        }

        foreach (string line in _log.Take(8))
        {
            Ui.Paragraph(column, "· " + line, 450, 14);
        }
    }

    private string FailureText(ActionFailure failure) => Tr(failure switch
    {
        ActionFailure.FaseNaoPermite => "Não permitido nesta fase (a propaganda começa em 16 de agosto).",
        ActionFailure.SemEnergia => "Sem energia nesta semana.",
        ActionFailure.SemDinheiro => "Sem dinheiro em caixa.",
        ActionFailure.EventoPendente => "Resolva o evento pendente.",
        _ => "Escolha onde ir.",
    });

    private void BuildHeadlines(VBoxContainer column)
    {
        Ui.Label(column, "Manchetes", 24);
        var recent = Campaign.Events.History.Where(h => h.Week >= State.Calendar.WeekStart.AddDays(-14)).TakeLast(4).ToList();
        foreach (var entry in recent)
        {
            string title = _game.Events.Find(entry.EventId)?.Title ?? entry.EventId;
            Ui.Paragraph(column, $"{title}: {entry.Text}", 860, 16);
        }

        if (Campaign.Polls.LastOrDefault(p => !p.Internal) is { } poll && poll.Week >= State.Calendar.WeekStart.AddDays(-7))
        {
            PollsterDefinition pollster = _game.Electorate.Pollsters.Get(poll.PollsterId);
            var leader = poll.Candidates.MaxBy(c => c.Value);
            Ui.Paragraph(column, string.Format(PtBr, Tr("{0}: {1} lidera com {2:0.0}% entre os candidatos a vereador."), pollster.Name, Campaign.Find(leader.Key)?.Name, leader.Value), 860, 16);
        }

        if (recent.Count == 0 && Campaign.Polls.Count == 0)
        {
            Ui.Label(column, "Semana tranquila na cidade.", 16, Ui.Muted);
        }
    }

    private void BuildPolls(VBoxContainer column)
    {
        Ui.Label(column, "Pesquisas", 24);

        // Botões em fluxo: com vários institutos, a linha quebra em vez de passar da borda da tela.
        var header = column.Add(new HFlowContainer());
        header.AddThemeConstantOverride("h_separation", 10);
        header.AddThemeConstantOverride("v_separation", 8);
        foreach (PollsterDefinition p in _game.Electorate.Pollsters.Pollsters.Where(p => p.Internal || p.HouseBias == 0))
        {
            decimal cost = Math.Round(p.Cost * (decimal)_engine.Context.Config.PorteCostMultiplier.GetValueOrDefault(_engine.Context.Municipio.Porte, 1) / 100) * 100;
            var b = Ui.Button(header, $"{Tr("Encomendar")}: {p.Name} ({Money(cost)})", () =>
            {
                if (_engine.CommissionPoll(p.Id) is null)
                {
                    _log.Insert(0, Tr("Sem dinheiro para a pesquisa."));
                }

                Refresh();
            });
            b.AddThemeFontSizeOverride("font_size", 16);
            b.Disabled = State.Player.Resources.Money < cost || Campaign.Phase is CampaignPhase.Votacao or CampaignPhase.Encerrada;
        }

        foreach (PollRecord poll in Campaign.Polls.TakeLast(3).Reverse())
        {
            PollsterDefinition pollster = _game.Electorate.Pollsters.Get(poll.PollsterId);
            Ui.Value(column, string.Format(PtBr, "{0} · {1:dd/MM} · n={2} · ±{3:0.0} p.p. · {4}: {5:0.0}%", pollster.Name, poll.Week, poll.SampleSize, poll.MarginOfError, Tr("não sabe"), poll.Undecided), 15);
            var grid = column.Add(new GridContainer { Columns = 3 });
            // Os 8 primeiros e, se estiver fora deles, a linha do próprio jogador.
            var shown = poll.Candidates.OrderByDescending(c => c.Value).Take(8).ToList();
            if (shown.All(c => c.Key != RaceSetup.PlayerId) && poll.Candidates.TryGetValue(RaceSetup.PlayerId, out double mine))
            {
                shown.Add(new KeyValuePair<string, double>(RaceSetup.PlayerId, mine));
            }

            foreach (var (id, value) in shown)
            {
                Candidate? c = Campaign.Find(id);
                Label l = Ui.Value(grid, $"{(c?.IsPlayer == true ? "► " : "")}{c?.Name} ({PartySigla(c?.PartyId ?? "")}) {value:0.0}%", 14);
                l.CustomMinimumSize = new Vector2(290, 0);
                if (c?.IsPlayer == true)
                {
                    l.AddThemeColorOverride("font_color", Ui.Warning);
                }
            }
        }

        if (Campaign.Polls.Count == 0)
        {
            Ui.Label(column, "Nenhuma pesquisa ainda. Institutos publicam durante a campanha; a pesquisa interna sai na hora.", 15, Ui.Muted);
        }
    }

    private void BuildRivals(VBoxContainer column)
    {
        column.CustomMinimumSize = new Vector2(430, 0);
        Ui.Label(column, "Principais rivais", 24);
        foreach (Candidate rival in Campaign.ForOffice(Office.Vereador).Where(c => !c.IsPlayer && !c.IsBulk).OrderByDescending(c => c.Recognition).Take(6))
        {
            HBoxContainer row = Ui.Row(column, 8);
            AddFace(row, rival, 56);
            Ui.Value(row, string.Format(PtBr, "{0} ({1}){2}\n{3} {4:0}%", rival.Name, PartySigla(rival.PartyId), rival.Incumbent ? " · " + Tr("vereador(a)") : "", Tr("reconhecimento"), rival.Recognition), 14);
        }

        Ui.Label(column, "Prefeitura", 22);
        foreach (Candidate mayor in Campaign.ForOffice(Office.Prefeito))
        {
            HBoxContainer row = Ui.Row(column, 8);
            AddFace(row, mayor, 48);
            Ui.Value(row, $"{mayor.Number} · {mayor.Name} ({PartySigla(mayor.PartyId)}){(mayor.Incumbent ? " · " + Tr("prefeito(a)") : "")}", 14);
        }
    }

    private void AddFace(HBoxContainer row, Candidate candidate, int size)
    {
        Appearance? appearance = State.Npcs.FirstOrDefault(n => n.Id == candidate.CharacterId)?.Appearance;
        if (appearance is null)
        {
            return;
        }

        var view = row.Add(new PortraitView { CustomMinimumSize = new Vector2(size, size) });
        GameParty? party = State.Party(candidate.PartyId);
        view.Show(appearance, new PortraitOptions(Planalto.Core.Characters.Expression.Neutra, WardrobeContext.Campanha, party?.Colors.Primary, "background_solido", party?.Colors.Secondary), _game.People.Appearance);
    }

    private void BuildBottom()
    {
        HBoxContainer bottom = Ui.Row(_root, 16);
        bottom.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        switch (Campaign.Phase)
        {
            case CampaignPhase.Votacao:
                Ui.Button(bottom, "Ir para a urna", () => { SaveManager.Instance.Save(SaveManager.ElectionSlot); _game.GoTo(Game.Scenes.Urna); });
                break;
            case CampaignPhase.Encerrada:
                Ui.Button(bottom, "Ver a apuração", () => _game.GoTo(Game.Scenes.ElectionNight));
                break;
            default:
                Ui.Button(bottom, "Avançar semana", () => Advance(untilSomething: false)).Disabled = Campaign.Events.Pending.Count > 0;
                Ui.Button(bottom, "Avançar até algo acontecer", () => Advance(untilSomething: true)).Disabled = Campaign.Events.Pending.Count > 0;
                break;
        }
    }

    private void Advance(bool untilSomething)
    {
        if (untilSomething)
        {
            _engine.AdvanceUntilSomethingHappens();
        }
        else
        {
            _engine.AdvanceWeek();
        }

        SaveManager.Instance.Autosave();
        Refresh();
    }

    private void ShowPendingEvent()
    {
        if (Campaign.Events.Pending.FirstOrDefault() is { } pending)
        {
            var shade = this.Add(new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Stop });
            shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            var center = shade.Add(new CenterContainer());
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            center.Add(new EventDialog(_engine, pending, Refresh));
        }
    }

    private void ShowSaveMenu()
    {
        if (State.World?.Options.Ironman == true)
        {
            SaveManager.Instance.Autosave();
            _log.Insert(0, Tr("Modo Ironman: a carreira foi salva no save único."));
            Refresh();
            return;
        }

        var menu = new PopupMenu();
        foreach (string slot in SaveManager.Instance.ManualSlots())
        {
            menu.AddItem(string.Format(Tr("Salvar no {0}"), slot));
        }

        menu.IndexPressed += i =>
        {
            string slot = SaveManager.Instance.ManualSlots().ElementAt((int)i);
            SaveManager.Instance.Save(slot);
            _log.Insert(0, string.Format(Tr("Carreira salva no {0}."), slot));
            Refresh();
        };
        AddChild(menu);
        menu.PopupCentered();
    }
}
