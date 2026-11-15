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
        CustomMinimumSize = new Vector2(900, 0);
        var style = new StyleBoxFlat { BgColor = new Color("#13233C"), BorderColor = new Color("#E9C46A"), CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10 };
        style.SetBorderWidthAll(2);
        style.SetContentMarginAll(24);
        AddThemeStyleboxOverride("panel", style);
        Show(choosing: true, result: null);
    }

    private void Show(bool choosing, string? result)
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
            var portrait = row.Add(new PortraitView { CustomMinimumSize = new Vector2(220, 220) });
            portrait.Show(face, new PortraitOptions(definition.PortraitExpression, WardrobeContext.Campanha), game.People.Appearance);
        }

        VBoxContainer column = Ui.Column(row, 12);
        Ui.Value(column, _pending.Title, 30);
        Ui.Paragraph(column, choosing ? _pending.Text : result ?? "", 620, 20);
        if (!choosing)
        {
            Ui.Button(column, "Continuar", () =>
            {
                QueueFree();
                _onClosed();
            });
            return;
        }

        foreach (var (choice, index) in definition.Choices.Select((c, i) => (c, i)))
        {
            string? blocked = EventEngine.Blocked(_engine.Context, choice);
            string label = EventEngine.Resolve(choice.Label, _pending.Variables) + (blocked is null ? "" : $"  ({blocked})");
            var button = Ui.Button(column, label, () =>
            {
                EventResolution resolution = EventEngine.Resolve(_engine.Context, _pending, index, _engine.Context.State.Rng);
                Show(choosing: false, result: resolution.Text.Length > 0 ? resolution.Text : Tr("Decisão registrada."));
            });
            button.Disabled = blocked is not null;
            button.Alignment = HorizontalAlignment.Left;
        }
    }
}
