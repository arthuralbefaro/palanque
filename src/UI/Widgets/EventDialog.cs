using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Events;
using Planalto.UI.Portrait;
using Planalto.UI.Theme;

namespace Planalto.UI.Widgets;

/// <summary>
/// Cartão de decisão de um evento (GDD 15.3 e 17.3): retrato, texto, quem está envolvido (pessoas, zona e veículo do
/// save, com o nome clicável para a ficha), as escolhas com a previsão de cada uma (bloqueadas mostram o requisito)
/// e, depois da escolha, o que mudou.
/// </summary>
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

    /// <summary>Nome fictício com o que é ("Fulana (presidente de associação rural)"); sem isto, vale só o nome.</summary>
    public Func<string, string>? PersonLabel { get; init; }

    /// <summary>A tela tem ficha para essa pessoa (o nome vira link).</summary>
    public Func<string, bool>? CanOpenPerson { get; init; }

    public Action<string>? OpenPerson { get; init; }

    public Action<string>? OpenZone { get; init; }

    public Action<string>? OpenOutlet { get; init; }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        CustomMinimumSize = new Vector2(720, 0);
        Name = "CartaoEvento";
        AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 12, 24, Palette.Atencao, 2));
        Show(choosing: true, result: null, changes: null);
    }

    private EffectSnapshot Snapshot()
    {
        var player = _engine.Context.State.Campaign!.Player;
        var state = _engine.Context.State;
        return new EffectSnapshot(player.Recognition, player.Image, state.Player.Resources.Money, state.Player.Resources.PoliticalCapital,
            new Dictionary<string, double>(player.Support), new Dictionary<string, double>());
    }

    private Appearance? FaceOf(string id)
    {
        var state = _engine.Context.State;
        string characterId = state.Campaign!.Find(id)?.CharacterId ?? id;
        return state.Npcs.FirstOrDefault(n => n.Id == characterId)?.Appearance;
    }

    /// <summary>Pessoas do evento, sem repetir: o rival citado e as dos papéis (liderança, jornalista, doador, colega, dirigente, equipe).</summary>
    private List<string> People()
    {
        var ids = new List<string>();
        if (_pending.RivalId is not null)
        {
            ids.Add(_pending.RivalId);
        }

        ids.AddRange(_pending.People.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => p.Value));
        return ids.Distinct(StringComparer.Ordinal).ToList();
    }

    private static Button Link(Node parent, string text, Action onPressed, string tip)
    {
        Button b = Ui.Button(parent, text, onPressed, tip);
        b.Alignment = HorizontalAlignment.Left;
        b.ClipText = true;
        b.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        b.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        Palette.ListRow(b);
        return b;
    }

    /// <summary>Quem e o que o evento envolve: retrato pequeno e nome com o que é; clicar abre a ficha no painel da esquerda.</summary>
    private void Involved(Node column)
    {
        Game game = Game.Instance;
        List<string> people = People();
        string? zone = _pending.ZoneId is not null && _engine.Context.Zonas.Zonas.Any(z => z.Id == _pending.ZoneId) ? _pending.ZoneId : null;
        Planalto.Core.Media.Outlet? outlet = _pending.OutletId is null ? null : _engine.City.Outlet(_pending.OutletId);
        if (people.Count == 0 && zone is null && outlet is null && _pending.Theme is null)
        {
            return;
        }

        VBoxContainer box = Ui.Column(column, 4);
        box.Name = "Envolvidos";
        foreach (string id in people)
        {
            HBoxContainer row = Ui.Row(box, 8);
            if (FaceOf(id) is { } face)
            {
                var portrait = row.Add(new PortraitView { CustomMinimumSize = new Vector2(40, 40), MouseFilter = MouseFilterEnum.Ignore });
                portrait.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                portrait.Show(face, new PortraitOptions(), game.People.Appearance);
            }

            string label = PersonLabel?.Invoke(id) ?? _engine.NameOf(id);
            if (OpenPerson is not null && (CanOpenPerson?.Invoke(id) ?? true))
            {
                Link(row, label, () => OpenPerson(id), Palette.Tip(label + ".", Tr("abre a ficha no painel da esquerda: relação, interesses e lembranças; o cartão da decisão continua aberto."), Tr("nada."))).Name = "Envolvido_" + id;
            }
            else
            {
                Glossary.Plain(row, label, 18, Palette.Texto).SizeFlagsVertical = SizeFlags.ShrinkCenter;
            }
        }

        if (zone is not null || outlet is not null)
        {
            HBoxContainer row = Ui.Row(box, 8);
            if (zone is not null)
            {
                string name = _engine.ZoneName(zone);
                string label = string.Format(Tr("Zona: {0}"), name);
                if (OpenZone is not null)
                {
                    Link(row, label, () => OpenZone(zone), Palette.Tip(string.Format(Tr("{0}, a zona onde isto acontece."), name), Tr("centraliza o mapa na zona e abre a ficha dela no painel da esquerda; o cartão da decisão continua aberto."), Tr("nada."))).Name = "ZonaDoEvento";
                }
                else
                {
                    Glossary.Plain(row, label, 18, Palette.Texto);
                }
            }

            if (outlet is not null)
            {
                string label = _engine.OutletLabel(outlet);
                if (OpenOutlet is not null)
                {
                    Link(row, label, () => OpenOutlet(outlet.Id), Palette.Tip(label + ".", Tr("abre a ficha do veículo no painel da esquerda: linha, alcance, dono e jornalista; o cartão da decisão continua aberto."), Tr("nada."))).Name = "VeiculoDoEvento";
                }
                else
                {
                    Glossary.Plain(row, label, 18, Palette.Texto);
                }
            }
        }

        if (_pending.Theme is { } theme && _engine.Context.Themes.Themes.FirstOrDefault(t => t.Id == theme) is { } definition)
        {
            bool hot = _engine.HotThemeOn(theme, _pending.ZoneId, _engine.Today) is not null;
            Glossary.Rich(box, string.Format(Tr(hot ? "Tema: {0} (em alta nesta semana)." : "Tema: {0}."), definition.Name), 18, Palette.Texto2);
        }
    }

    private void Show(bool choosing, string? result, string? changes)
    {
        Ui.Clear(this);
        Game game = Game.Instance;
        EventDefinition definition = game.Events.Find(_pending.EventId)!;
        HBoxContainer row = Ui.Row(this, 20);

        Appearance? face = definition.Portrait switch
        {
            "rival" when _pending.RivalId is not null => FaceOf(_pending.RivalId),
            _ => game.State!.Player.Appearance,
        };
        if (face is not null)
        {
            var portrait = row.Add(new PortraitView { CustomMinimumSize = new Vector2(160, 160) });
            portrait.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            portrait.Show(face, new PortraitOptions(definition.PortraitExpression, WardrobeContext.Campanha), game.People.Appearance);
        }

        VBoxContainer column = Ui.Column(row, 10);
        column.CustomMinimumSize = new Vector2(480, 0);
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        RichTextLabel title = Glossary.Plain(column, _pending.Title, 32, Palette.Texto, Palette.Weight(Palette.Oswald, 600));
        title.Name = "TituloDecisao";
        Glossary.Rich(column, choosing ? _pending.Text : result ?? "", 22, Palette.Texto, Palette.Serif);
        if (!choosing)
        {
            Glossary.Plain(column, Tr("O que mudou"), 20, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            Glossary.Rich(column, changes ?? "", 18);
            Ui.Button(column, "Continuar", () =>
            {
                QueueFree();
                _onClosed();
            }, Palette.Tip(Tr("Fecha o cartão da decisão."), Tr("volta ao mapa; o jogo continua pausado até você soltar o tempo."), Tr("nada.")));
            return;
        }

        Involved(column);
        foreach (var (choice, index) in definition.Choices.Select((c, i) => (c, i)))
        {
            string? blocked = EventEngine.Blocked(_engine.Context, choice);
            string label = EventEngine.Resolve(choice.Label, _pending.Variables) + (blocked is null ? "" : $"  ({blocked})");

            // A previsão conhece o evento em curso: a presença de zona dos efeitos vale na zona dele.
            string forecast = ForecastText.Full(Forecaster.ForChoice(_engine.Context, choice, _pending), EffectSummary.GroupName);
            string tip = Palette.Tip(EventEngine.Resolve(choice.Label, _pending.Variables) + ".", forecast + ".", Tr("o que está na previsão; o jogo continua pausado."));
            var button = Ui.Button(column, label, () =>
            {
                EffectSnapshot before = Snapshot();
                EventResolution resolution = EventEngine.Resolve(_engine.Context, _pending, index, _engine.Context.State.Rng);
                string changes = EffectSummary.Describe(before, Snapshot(), EffectSummary.GroupName);
                Show(choosing: false, result: resolution.Text.Length > 0 ? resolution.Text : Tr("Decisão registrada."), changes: char.ToUpper(changes[0]) + changes[1..] + ".");
            }, blocked is null ? tip : Palette.Blocked(string.Format(Tr("{0}; falta cumprir o requisito."), blocked), tip));
            button.Name = "Escolha_" + index;
            button.Disabled = blocked is not null;
            button.Alignment = HorizontalAlignment.Left;
            Glossary.Rich(column, Tr("Previsão") + ": " + forecast, 16, Palette.Texto3);
        }
    }
}
