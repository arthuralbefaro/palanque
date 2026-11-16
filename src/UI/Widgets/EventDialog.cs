using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Events;
using Planalto.UI.Portrait;

namespace Planalto.UI.Widgets;

/// <summary>Janela de evento (GDD 15.3): retrato, texto, escolhas (bloqueadas mostram o requisito) e resultado.</summary>
public partial class EventDialog : PanelContainer
{
    private readonly CampaignEngine _engine;
    private readonly PendingEvent _pending;
    private readonly Action _onClosed;

    public EventDialog(CampaignEngine engine, PendingEvent pending, Action onClosed)
    {
        _engine = engine;
        _pending = pending;
        _onClosed = onClosed;
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        CustomMinimumSize = new Vector2(720, 0);
        AddThemeStyleboxOverride("panel", UI.Theme.Palette.Box(UI.Theme.Palette.CartaoAlto, 12, 24, UI.Theme.Palette.Atencao, 2));
        Show(choosing: true, result: null, changes: null);
    }

    private EffectSnapshot Snapshot()
    {
        var player = _engine.Context.State.Campaign!.Player;
        var state = _engine.Context.State;
        return new EffectSnapshot(player.Recognition, player.Image, state.Player.Resources.Money, state.Player.Resources.PoliticalCapital,
            new Dictionary<string, double>(player.Support), new Dictionary<string, double>());
    }

    private void Show(bool choosing, string? result, string? changes)
    {
        Ui.Clear(this);
        Game game = Game.Instance;
        EventDefinition definition = game.Events.Find(_pending.EventId)!;
        HBoxContainer row = Ui.Row(this, 20);

        Appearance? face = definition.Portrait switch
        {
            "rival" when _pending.RivalId is not null => game.State!.Npcs.FirstOrDefault(n => n.Id == _engine.Context.State.Campaign!.Find(_pending.RivalId)?.CharacterId)?.Appearance,
            _ => game.State!.Player.Appearance,
        };
        if (face is not null)
        {
            var portrait = row.Add(new PortraitView { CustomMinimumSize = new Vector2(160, 160) });
            portrait.Show(face, new PortraitOptions(definition.PortraitExpression, WardrobeContext.Campanha), game.People.Appearance);
        }

        VBoxContainer column = Ui.Column(row, 10);
        column.CustomMinimumSize = new Vector2(480, 0);
        Label title = Ui.Value(column, _pending.Title, 32);
        title.AddThemeFontOverride("font", UI.Theme.Palette.Weight(UI.Theme.Palette.Oswald, 600));
        title.AddThemeColorOverride("font_color", UI.Theme.Palette.Texto);
        Glossary.Rich(column, choosing ? _pending.Text : result ?? "", 22, UI.Theme.Palette.Texto, UI.Theme.Palette.Serif);
        if (!choosing)
        {
            Label heading = Ui.Label(column, "O que mudou", 20);
            heading.AddThemeColorOverride("font_color", UI.Theme.Palette.Texto);
            Glossary.Rich(column, changes ?? "", 18);
            Ui.Button(column, "Continuar", () =>
            {
                QueueFree();
                _onClosed();
            }, UI.Theme.Palette.Tip(Tr("Fecha o cartão da decisão."), Tr("volta ao mapa; o jogo continua pausado até você soltar o tempo."), Tr("nada.")));
            return;
        }

        foreach (var (choice, index) in definition.Choices.Select((c, i) => (c, i)))
        {
            string? blocked = EventEngine.Blocked(_engine.Context, choice);
            string label = EventEngine.Resolve(choice.Label, _pending.Variables) + (blocked is null ? "" : $"  ({blocked})");
            string forecast = ForecastText.Full(Forecaster.ForChoice(_engine.Context, choice), EffectSummary.GroupName);
            string tip = UI.Theme.Palette.Tip(EventEngine.Resolve(choice.Label, _pending.Variables) + ".", forecast + ".", Tr("o que está na previsão; o jogo continua pausado."));
            var button = Ui.Button(column, label, () =>
            {
                EffectSnapshot before = Snapshot();
                EventResolution resolution = EventEngine.Resolve(_engine.Context, _pending, index, _engine.Context.State.Rng);
                string changes = EffectSummary.Describe(before, Snapshot(), EffectSummary.GroupName);
                Show(choosing: false, result: resolution.Text.Length > 0 ? resolution.Text : Tr("Decisão registrada."), changes: char.ToUpper(changes[0]) + changes[1..] + ".");
            }, blocked is null ? tip : UI.Theme.Palette.Blocked(string.Format(Tr("{0}; falta cumprir o requisito."), blocked), tip));
            button.Disabled = blocked is not null;
            button.Alignment = HorizontalAlignment.Left;
            Glossary.Rich(column, Tr("Previsão") + ": " + forecast, 16, UI.Theme.Palette.Texto3);
        }
    }
}
