using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.State;
using Planalto.UI.Map;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Alvo "rl" (leitura mínima), com janela, em Mantena (MG) e em 1366x768, 1920x1080 e 2560x1080: o relógio anda na
/// barra de cima com a linha do dia, o painel Você mostra os números do Core com tooltip em três partes, e o painel de
/// zonas fica sincronizado com o mapa nos dois sentidos.
/// </summary>
public partial class UiShot
{
    private const int MantenaMg = 3139607;

    private static readonly Vector2I[] ReadingSizes = [new(1366, 768), new(1920, 1080), new(2560, 1080)];

    private async Task PlayReading(string? shot)
    {
        if (NeedsWindow("da leitura mínima"))
        {
            return;
        }

        UI.Settings.ResetForTest();
        var report = new List<string>();
        foreach (Vector2I size in ReadingSizes)
        {
            GetTree().Root.Size = size;
            DisplayServer.WindowSetSize(size);
            await Frames(4);
            string tag = $"{size.X}x{size.Y}";
            HubScreen hub = await OpenHubIn(MantenaMg, 11);
            if (!await CheckReading(hub, tag, shot, report))
            {
                return;
            }

            RemoveChild(hub);
            hub.QueueFree();
            await Frames(2);
        }

        GD.Print("Smoke rl: ok (" + string.Join("; ", report) + ")");
        GetTree().Quit(0);
    }

    private async Task<bool> CheckReading(HubScreen hub, string tag, string? shot, List<string> report)
    {
        Game game = Game.Instance;
        CampaignEngine engine = game.Engine!;
        ClearDecisions();
        game.State!.Player.Resources.Money = 50_000;
        PrepareBusyWeek(hub);
        ClearDecisions();
        hub.Refresh();
        await Frames(2);
        await Click(Named<Button>(hub, "Area_territorio")!);

        // 1. Relógio: anda em 1x na barra de cima, com o marcador na linha do dia; os três turnos estão na linha.
        if (Named<Label>(hub, "Relogio") is null || Named<Control>(hub, "Turnos") is null || FindAll<Button>(hub).Count(b => b.Name.ToString().StartsWith("Linha_", StringComparison.Ordinal)) != 3)
        {
            Fail($"{tag}: a barra de cima não mostra o relógio e a linha do dia com os três turnos.");
            return false;
        }

        string before = hub.ClockText;
        float needle = hub.NeedleFraction;
        await Click(FindAll<Button>(hub).First(b => b.Text == "1x"));
        await WaitUntil(() => hub.ClockText != before && hub.NeedleFraction > needle, $"o relógio andar em 1x ({tag}; parado em {before})");
        await Click(Named<Button>(hub, "Pausa")!);
        var (hour, minute) = Clock.TimeOfDay(game.Time, game.State);
        if (hub.ClockText != $"{hour:00}:{minute:00}")
        {
            Fail($"{tag}: o relógio da tela ({hub.ClockText}) não é a hora do Core ({hour:00}:{minute:00}).");
            return false;
        }

        report.Add($"{tag}: relógio de {before} a {hub.ClockText}");

        // 2. Painel Você com os números do Core e tooltip em três partes em cada linha.
        YouStatus you = engine.You();
        var expected = new (string Name, string Value)[]
        {
            ("Voce_TurnosHoje", $"{you.FreeToday} de {you.Today.Count}"),
            ("Voce_TurnosSemana", $"{you.FreeWeek} de {you.WeekTurns}"),
            ("Voce_Dinheiro", string.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), $"R$ {you.Money:N0}")),
        };
        foreach (var (name, value) in expected)
        {
            Control? row = Named<Control>(hub, name);
            string? shown = row is null ? null : Named<Label>(row, "Valor")?.Text;
            if (shown != value || !ThreePartTip(row))
            {
                Fail($"{tag}: painel Você, {name}: mostra \"{shown}\", o Core diz \"{value}\" (ou falta o tooltip em três partes).");
                return false;
            }
        }

        Control? stress = Named<Control>(hub, "Voce_Estresse");
        if (stress is null || !ThreePartTip(stress) || !TextOf(stress).Contains(you.StressLimit.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            || Named<Control>(hub, "BarraEstresse") is null || !ThreePartTip(Named<Control>(hub, "Voce_Eleicao")))
        {
            Fail($"{tag}: painel Você sem o estresse com o limite ({you.StressLimit}), a barra ou a eleição com tooltip.");
            return false;
        }

        // 3. Painel de zonas: todas as zonas, ordenável e sincronizado com o mapa nos dois sentidos.
        ZoneMapView map = hub.ZoneMap;
        List<PanelContainer> cards = ZoneCards(hub);
        if (cards.Count != map.Zonas.Count)
        {
            Fail($"{tag}: o painel de zonas tem {cards.Count} linhas para {map.Zonas.Count} zonas.");
            return false;
        }

        string fromList = cards[^1].Name.ToString()["ZonaLinha_".Length..];
        await ClickCard(cards[^1]);
        if (map.Selected != fromList || hub.SelectedZoneRow != fromList || Named<Button>(ZoneCard(hub, fromList)!, "VerFicha") is null)
        {
            Fail($"{tag}: o clique na linha de {fromList} não destacou a zona no mapa (selecionada: {map.Selected}).");
            return false;
        }

        string? fromMap = ClickableZone(map, except: fromList);
        if (fromMap is null)
        {
            Fail($"{tag}: nenhuma outra zona com o centro clicável no mapa.");
            return false;
        }

        await ClickAt(map, map.GlobalPosition + map.ScreenCenterOf(fromMap));
        await Frames(2);
        if (hub.SelectedZoneRow != fromMap || map.Selected != fromMap || hub.SheetName != "Nenhuma" || Named<Button>(ZoneCard(hub, fromMap)!, "VerFicha") is null)
        {
            Fail($"{tag}: o clique no mapa em {fromMap} não destacou a linha (linha: {hub.SelectedZoneRow}, ficha: {hub.SheetName}).");
            return false;
        }

        // A linha destacada pelo mapa fica à vista na lista (a lista rola até ela).
        await WaitUntil(() => ZoneCard(hub, fromMap) is { } card && card.GetParent()?.GetParent() is ScrollContainer scroll && scroll.GetGlobalRect().Grow(1).Encloses(card.GetGlobalRect()), $"a linha de {fromMap} aparecer na lista ({tag})");

        foreach (string column in new[] { "líder:", "você:", "demanda:", "lideranças:", "cansaço" })
        {
            if (!TextOf(ZoneCard(hub, fromMap)!).Contains(column, StringComparison.Ordinal))
            {
                Fail($"{tag}: a linha da zona não mostra \"{column}\".");
                return false;
            }
        }

        OptionButton sort = Named<OptionButton>(hub, "OrdemZonas")!;
        sort.Select(1);
        sort.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
        await Frames(2);
        var byVoters = engine.ZoneBoard().OrderByDescending(r => r.Voters).Select(r => "ZonaLinha_" + r.ZoneId).ToList();
        if (!ZoneCards(hub).Select(c => c.Name.ToString()).SequenceEqual(byVoters))
        {
            Fail($"{tag}: ordenar por eleitores não reordenou a lista.");
            return false;
        }

        sort = Named<OptionButton>(hub, "OrdemZonas")!;
        sort.Select(0);
        sort.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
        await Frames(2);

        var problems = Overflow(hub, $"rl {tag}");
        if (problems.Count > 0)
        {
            Fail($"{tag}: controles fora da janela: {string.Join("; ", problems.Take(5))}");
            return false;
        }

        // Para a captura: o relógio anda até a tarde pelo Core (o compromisso da manhã acontece ao meio-dia).
        int logBefore = game.State.Campaign!.Agenda.Log.Count;
        while (Clock.TimeOfDay(game.Time, game.State).Hour < 14)
        {
            game.TickCampaignClock(0.05, 4);
        }

        hub.Refresh();
        await Frames(3);
        if (game.State.Campaign.Agenda.Log.Count <= logBefore || Named<Button>(hub, "Linha_0")?.Text.Contains(Tr("feito"), StringComparison.Ordinal) != true)
        {
            Fail($"{tag}: ao meio-dia a caminhada da manhã não aconteceu ou a linha do dia não mostra \"feito\".");
            return false;
        }

        Save(shot, "_" + tag);

        // A virada do dia aparece sobre o mapa.
        hub.StepDay();
        await Frames(2);
        if (hub.DayTurnText is null || Named<Control>(hub, "ViradaDoDia") is null)
        {
            Fail($"{tag}: a virada do dia não apareceu sobre o mapa.");
            return false;
        }

        report.Add($"{tag}: Você e zonas conferidos ({cards.Count} zonas), virada \"{hub.DayTurnText}\"");
        return true;
    }

    private static void ClearDecisions()
    {
        var campaign = Game.Instance.State!.Campaign!;
        campaign.Events.Pending.Clear();
        campaign.Team.Pending.Clear();
        campaign.Agenda.PendingRequest = null;
        campaign.Debate = null;
    }

    /// <summary>Uma zona cujo centro está livre de botões, da legenda e da faixa, para o clique no mapa.</summary>
    private static string? ClickableZone(ZoneMapView map, string? except = null)
    {
        Rect2 safe = new Rect2(map.GlobalPosition, map.Size).Grow(-120);
        foreach (var zona in map.Zonas.Values.OrderByDescending(z => z.Populacao))
        {
            Vector2 at = map.ScreenCenterOf(zona.Id);
            if (zona.Id != except && map.Hit(at) == zona.Id && safe.HasPoint(map.GlobalPosition + at) && at.Y < map.Size.Y - 420)
            {
                return zona.Id;
            }
        }

        return null;
    }
}
