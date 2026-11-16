using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Career;
using Planalto.Core.Parties;
using Planalto.Core.State;
using Planalto.UI.Screens;

namespace Planalto.UI.Widgets;

/// <summary>
/// Fundação de partido durante a carreira (GDD 14.2 B): proposta (família, nome, sigla, número), fundadores,
/// coleta semanal de apoio por estado, pedido de registro e filiação ao partido registrado.
/// </summary>
public partial class FoundingDialog : PanelContainer
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly Action _onClosed;
    private Game _game = null!;
    private GameParty? _draft;
    private string _intensity = "";
    private readonly HashSet<string> _states = new(StringComparer.Ordinal);
    private string _message = "";
    private bool _confirmLoss;

    public FoundingDialog(Action onClosed) => _onClosed = onClosed;

    private PartyFoundingEngine Engine => _game.FoundingEngine!;

    private GameState State => _game.State!;

    public override void _Ready()
    {
        _game = Game.Instance;
        _intensity = _game.Founding.Intensities[0].Id;
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        CustomMinimumSize = new Vector2(980, 0);
        var style = new StyleBoxFlat { BgColor = new Color("#13233C"), BorderColor = new Color("#E9C46A"), CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10 };
        style.SetBorderWidthAll(2);
        style.SetContentMarginAll(24);
        AddThemeStyleboxOverride("panel", style);
        Refresh();
    }

    private void Refresh()
    {
        Ui.Clear(this);
        VBoxContainer column = Ui.Column(this, 10);
        Ui.Value(column, Tr("Fundar um partido"), 30);
        PartyFoundingState? founding = State.Founding;
        switch (founding?.Stage)
        {
            case FoundingStage.Assinaturas:
                BuildCollection(column, founding);
                break;
            case FoundingStage.Registro:
                Ui.Paragraph(column, string.Format(Tr("O pedido de registro do {0} está em análise no TSE. Decisão prevista para {1}."), founding.Draft.Sigla, MandateTexts.Date(founding.ReviewUntil!.Value)), 900, 18);
                break;
            case FoundingStage.Registrado:
                BuildRegistered(column, founding);
                break;
            default:
                if (founding?.Stage == FoundingStage.Expirado)
                {
                    Ui.Label(column, "O prazo de dois anos para colher o apoio acabou e o processo anterior caducou.", 16, Ui.Warning);
                }

                BuildProposal(column);
                break;
        }

        if (_message.Length > 0)
        {
            Ui.Paragraph(column, _message, 900, 16);
        }

        Ui.Button(column, "Fechar", () =>
        {
            QueueFree();
            _onClosed();
        });
    }

    private void BuildProposal(VBoxContainer column)
    {
        Ui.Paragraph(column, string.Format(PtBr, Tr("Reunir {0} fundadores em ao menos {1} estados custa {2} de capital político e R$ {3:N0}. Depois, é preciso colher o apoio de eleitores não filiados em até dois anos e pedir o registro ao TSE."), _game.Founding.Founders, _game.Founding.MinStates, _game.Founding.CapitalCost, _game.Founding.MoneyCost), 900, 16);
        FamilyCatalog families = _game.World.Parties.Families;
        _draft ??= Engine.Propose(new Rng(State.Rng.NextUInt64()), families.Families[0].Id);
        GameParty draft = _draft;

        Ui.Options(Ui.Field(column, "Família"), families.Families.Select(f => (f.Id, f.Name)), draft.FamilyId, id =>
        {
            _draft = Engine.Propose(new Rng(State.Rng.NextUInt64()), id);
            Refresh();
        }, translate: false);
        var name = Ui.Field(column, "Nome").Add(new LineEdit { Text = draft.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        name.TextChanged += t => draft.Name = t;
        var sigla = Ui.Field(column, "Sigla").Add(new LineEdit { Text = draft.Sigla, MaxLength = 8, CustomMinimumSize = new Vector2(160, 0) });
        sigla.TextChanged += t => draft.Sigla = t.ToUpperInvariant();
        var number = Ui.Field(column, "Número").Add(new SpinBox { MinValue = 10, MaxValue = 99, Value = draft.Number });
        number.ValueChanged += v => draft.Number = (int)v;

        HBoxContainer actions = Ui.Row(column, 12);
        Ui.Button(actions, "Sugerir outro", () =>
        {
            _draft = Engine.Propose(new Rng(State.Rng.NextUInt64()), draft.FamilyId);
            Refresh();
        });
        Ui.Button(actions, "Reunir fundadores e começar", () =>
        {
            FoundingFailure? failure = Engine.Start(draft);
            _message = failure is null ? Tr("Fundadores reunidos. A coleta de apoio começou.") : MandateTexts.Founding(failure.Value);
            Refresh();
        });
    }

    private void BuildCollection(VBoxContainer column, PartyFoundingState founding)
    {
        GameParty draft = founding.Draft;
        Ui.Value(column, $"{draft.Sigla} — {draft.Name}", 22);
        Ui.Value(column, string.Format(PtBr, Tr("Assinaturas: {0:N0} de {1:N0} · estados com o mínimo: {2} de {3} · prazo: {4}"), founding.Total, Engine.NationalRequired, Engine.StatesDone, _game.Founding.MinStates, MandateTexts.Date(founding.Started.AddDays(7 * _game.Founding.DeadlineWeeks))), 16);
        var bar = column.Add(new ProgressBar { MinValue = 0, MaxValue = 100, Value = Math.Min(100, 100.0 * founding.Total / Engine.NationalRequired), CustomMinimumSize = new Vector2(0, 22), ShowPercentage = false });
        bar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (founding.Impugnations > 0)
        {
            Ui.Label(column, string.Format(Tr("Impugnações: {0}. Parte das assinaturas foi invalidada."), founding.Impugnations), 16, Ui.Warning);
        }

        Ui.Options(Ui.Field(column, "Coleta"), _game.Founding.Intensities.Select(i => (i.Id, string.Format(PtBr, "{0} · {1} {2} · R$ {3:N0}", i.Name, i.Energy, Tr("energia"), i.Money))), _intensity, id => { _intensity = id; Refresh(); }, translate: false);
        Ui.Label(column, string.Format(Tr("Estados desta semana (até {0}):"), _game.Founding.FocusStatesMax), 16);
        var grid = column.Add(new GridContainer { Columns = 9 });
        foreach (string uf in Engine.States.Order(StringComparer.Ordinal))
        {
            bool done = founding.Signatures.GetValueOrDefault(uf) >= Engine.StateRequired(uf);
            var check = grid.Add(new CheckBox { Text = uf + (done ? " ✓" : ""), ButtonPressed = _states.Contains(uf) });
            check.TooltipText = string.Format(PtBr, "{0:N0} / {1:N0}", founding.Signatures.GetValueOrDefault(uf), Engine.StateRequired(uf));
            check.Toggled += on =>
            {
                if (on)
                {
                    _states.Add(uf);
                }
                else
                {
                    _states.Remove(uf);
                }
            };
        }

        HBoxContainer actions = Ui.Row(column, 12);
        FoundingIntensity intensity = _game.Founding.Intensities.First(i => i.Id == _intensity);
        Ui.Button(actions, "Coletar nesta semana", () =>
        {
            FoundingFailure? failure = Engine.CanCollect(intensity, _states);
            _message = failure is null
                ? string.Format(PtBr, Tr("{0:N0} assinaturas colhidas."), Engine.Collect(intensity.Id, _states))
                : MandateTexts.Founding(failure.Value);
            Refresh();
        });
        var submit = Ui.Button(actions, "Pedir registro ao TSE", () =>
        {
            FoundingFailure? failure = Engine.Submit();
            _message = failure is null ? Tr("Pedido de registro protocolado no TSE.") : MandateTexts.Founding(failure.Value);
            Refresh();
        });
        submit.Disabled = !Engine.Ready;
    }

    private void BuildRegistered(VBoxContainer column, PartyFoundingState founding)
    {
        GameParty party = founding.Draft;
        Ui.Paragraph(column, string.Format(Tr("{0} — {1} registrado no TSE em {2}. Ele começa nanico: sem fundo relevante, pouco tempo de TV e sem bancada."), party.Sigla, party.Name, MandateTexts.Date(founding.RegisteredOn!.Value)), 900, 18);
        if (State.Player.PartyId == party.Id)
        {
            Ui.Label(column, "Você já é filiado ao partido que fundou.", 16, Ui.Ok);
            return;
        }

        bool loses = PartyAffiliation.LosesMandate(State);
        if (loses)
        {
            Ui.Label(column, "Fora da janela partidária, trocar de partido custa o seu mandato.", 16, Ui.Error);
        }

        Ui.Button(column, string.Format(Tr(loses && _confirmLoss ? "Confirmar: filiar-se ao {0} e perder o mandato" : "Filiar-se ao {0}"), party.Sigla), () =>
        {
            if (loses && !_confirmLoss)
            {
                // Perder o mandato pede um segundo clique.
                _confirmLoss = true;
                Refresh();
                return;
            }

            _game.JoinParty(party.Id);
            _message = string.Format(Tr("Você se filiou ao {0}."), party.Sigla);
            Refresh();
        });
    }
}
