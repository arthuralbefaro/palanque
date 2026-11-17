using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.State;
using Planalto.UI.Map;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Alvo "rl" (leitura mínima), com janela, em Mantena (MG) e em 1366x768, 1920x1080 e 2560x1080: a tela abre no
/// painel de zonas; o relógio anda na barra de cima com a linha do dia e as marcas de hora; a barra de cima fica só com
/// data, relógio, tempo, fase e contagem; o painel Você mostra os números do Core e fica à vista com qualquer área ou
/// janela aberta; o painel de zonas fica sincronizado com o mapa; "Fazer agora" conclui sem abrir a agenda, com o
/// estresse do terceiro turno na previsão e "Fazer amanhã de manhã" sem turno hoje; o número flutuante sobe da zona;
/// a pilha de janelas só deixa clicar na de cima, prende o foco na modal e fecha com Esc; os painéis se refazem pelo
/// evento do Core; a faixa da virada do dia some na primeira hora; e o feed tem retratos.
/// Com "--semanas=pasta", joga uma campanha em Mantena e grava a tela central nas semanas 1, 10 e 20, nas três resoluções.
/// </summary>
public partial class UiShot
{
    private const int MantenaMg = 3139607;

    private static readonly Vector2I[] ReadingSizes = [new(1366, 768), new(1920, 1080), new(2560, 1080)];

    private static readonly CultureInfo PtBrCulture = CultureInfo.GetCultureInfo("pt-BR");

    private sealed class ReadingFailure(string message) : Exception(message);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new ReadingFailure(message);
        }
    }

    private async Task PlayReading(string? shot)
    {
        if (NeedsWindow("da leitura mínima"))
        {
            return;
        }

        UI.Settings.ResetForTest();
        string? weeks = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--semanas=", StringComparison.Ordinal))?.Split('=', 2)[1];
        var report = new List<string>();
        try
        {
            if (weeks is not null)
            {
                await CaptureWeeks(weeks, report);
            }
            else
            {
                foreach (Vector2I size in ReadingSizes)
                {
                    await Resize(size);
                    HubScreen hub = await OpenHubIn(MantenaMg, 11);
                    await CheckReading(hub, $"{size.X}x{size.Y}", shot, report);
                    RemoveChild(hub);
                    hub.QueueFree();
                    await Frames(2);
                }
            }
        }
        catch (Exception e) when (e is ReadingFailure or InvalidOperationException)
        {
            Fail(e is ReadingFailure ? e.Message : e.ToString());
            return;
        }

        GD.Print("Smoke rl: ok (" + string.Join("; ", report) + ")");
        GetTree().Quit(0);
    }

    private async Task Resize(Vector2I size)
    {
        GetTree().Root.Size = size;
        DisplayServer.WindowSetSize(size);
        await Frames(4);
    }

    private async Task CheckReading(HubScreen hub, string tag, string? shot, List<string> report)
    {
        Game game = Game.Instance;
        CampaignEngine engine = game.Engine!;
        ZoneMapView map = hub.ZoneMap;
        ClearDecisions();
        game.State!.Player.Resources.Money = 50_000;
        PrepareBusyWeek(hub);
        ClearDecisions();
        hub.Refresh();
        await Frames(2);

        // Resposta do autor 1: a campanha abre na área Território, com o painel de zonas.
        Require(Named<Button>(hub, "Area_territorio") is { ButtonPressed: true } && ZoneCards(hub).Count == map.Zonas.Count, $"{tag}: a tela não abriu no painel de zonas (área Território).");

        // Resposta do autor 3: a barra de cima fica com data, relógio, tempo, fase e contagem; o resto está no Você.
        Control top = Named<Button>(hub, "Pausa")!.GetParent<Control>();
        string topText = TextOf(top);
        Require(!topText.Contains(Tr("conhecem você"), StringComparison.Ordinal) && !topText.Contains(Tr("como te veem"), StringComparison.Ordinal) && !topText.Contains("R$", StringComparison.Ordinal),
            $"{tag}: a barra de cima ainda mostra caixa, reconhecimento ou imagem: {topText.Replace('\n', ' ')}");
        Require(Named<Control>(hub, "PainelVoce") is { } voce && Named<Control>(voce, "Imagem") is not null && Named<Control>(voce, "Voce_Reconhecimento") is not null && Named<Control>(voce, "Voce_Dinheiro") is not null && Named<Control>(voce, "Voce_Estresse") is not null,
            $"{tag}: o painel Você não tem caixa, estresse, reconhecimento e imagem.");

        // 1. Relógio e linha do dia, com as marcas de hora (resposta do autor 2).
        Require(Named<Label>(hub, "Relogio") is not null && FindAll<Button>(hub).Count(b => b.Name.ToString().StartsWith("Linha_", StringComparison.Ordinal)) == 3, $"{tag}: sem relógio ou sem os três turnos.");
        foreach (int hour in new[] { 6, 9, 12, 15, 18, 22 })
        {
            Require(Named<Label>(hub, $"Hora_{hour}") is { } mark && mark.IsVisibleInTree(), $"{tag}: falta a marca das {hour}h sob a linha do dia.");
        }

        string before = hub.ClockText;
        float needle = hub.NeedleFraction;
        int updates = hub.PanelUpdatesFromCore;
        await Click(FindAll<Button>(hub).First(b => b.Text == "1x"));
        await WaitUntil(() => hub.ClockText != before && hub.NeedleFraction > needle, $"o relógio andar em 1x ({tag}; parado em {before})");
        await Click(Named<Button>(hub, "Pausa")!);
        var (hour0, minute0) = Clock.TimeOfDay(game.Time, game.State);
        Require(hub.ClockText == $"{hour0:00}:{minute0:00}", $"{tag}: relógio da tela {hub.ClockText}, do Core {hour0:00}:{minute0:00}.");
        report.Add($"{tag}: relógio de {before} a {hub.ClockText}");

        // Correção do autor: a legenda de Minha força é a escala de 0 a 100, sem os valores de cada faixa.
        Require(Named<Control>(hub, "EscalaPresenca") is { } scale && scale.IsVisibleInTree() && Named<Control>(hub, "MarcasPresenca") is not null, $"{tag}: a legenda de Minha força não tem a escala de 0 a 100.");

        // 2. Painel Você com os números do Core.
        CheckYou(hub, engine, tag);

        // Painéis por evento do Core: um turno que fecha refaz a linha do dia e o Você sem redesenhar a tela inteira.
        game.TickCampaignClock(Clock.SecondsPerDay(game.Time, Clock.ModoCampanha, 1) * (game.Time.Dia.EndOf(Turno.Manha) - game.State.Time.Minute + 1) / game.Time.Dia.Minutes, 1);
        await WaitUntil(() => hub.PanelUpdatesFromCore > updates, $"os painéis se refazerem pelo evento do Core ({tag})");
        Require(Named<Button>(hub, "Linha_0")?.Text.Contains(Tr("feito"), StringComparison.Ordinal) == true, $"{tag}: depois do meio-dia, a manhã não aparece como feita na linha do dia.");
        CheckYou(hub, engine, tag);

        // 3. Painel de zonas sincronizado com o mapa nos dois sentidos.
        List<PanelContainer> cards = ZoneCards(hub);
        string fromList = cards[^1].Name.ToString()["ZonaLinha_".Length..];
        await ClickCard(cards[^1]);
        Require(map.Selected == fromList && hub.SelectedZoneRow == fromList && Named<Button>(ZoneCard(hub, fromList)!, "FazerAgoraLinha") is not null, $"{tag}: a linha de {fromList} não destacou a zona no mapa ou não tem Fazer agora.");
        string fromMap = ClickableZone(map, except: fromList) ?? throw new ReadingFailure($"{tag}: nenhuma outra zona com o centro clicável no mapa.");
        await ClickAt(map, map.GlobalPosition + map.ScreenCenterOf(fromMap));
        await Frames(2);
        Require(hub.SelectedZoneRow == fromMap && map.Selected == fromMap && hub.SheetName == "Zona" && hub.WindowNames.SequenceEqual(["FichaZona"]), $"{tag}: o clique no mapa em {fromMap} não destacou a linha e abriu a ficha flutuante (linha {hub.SelectedZoneRow}, janelas {string.Join(",", hub.WindowNames)}).");
        await WaitUntil(() => ZoneCard(hub, fromMap) is { } card && card.GetParent()?.GetParent() is ScrollContainer scroll && scroll.GetGlobalRect().Grow(1).Encloses(card.GetGlobalRect()), $"a linha de {fromMap} aparecer na lista ({tag})");
        foreach (string column in new[] { "líder:", "você:", "demanda:", "lideranças", "cansaço" })
        {
            Require(TextOf(ZoneCard(hub, fromMap)!).Contains(column, StringComparison.Ordinal), $"{tag}: a linha da zona não mostra \"{column}\".");
        }

        OptionButton sort = Named<OptionButton>(hub, "OrdemZonas")!;
        sort.Select(1);
        sort.EmitSignal(OptionButton.SignalName.ItemSelected, 1);
        await Frames(2);
        Require(ZoneCards(hub).Select(c => c.Name.ToString()).SequenceEqual(engine.ZoneBoard().OrderByDescending(r => r.Voters).Select(r => "ZonaLinha_" + r.ZoneId)), $"{tag}: ordenar por eleitores não reordenou a lista.");
        sort = Named<OptionButton>(hub, "OrdemZonas")!;
        sort.Select(0);
        sort.EmitSignal(OptionButton.SignalName.ItemSelected, 0);
        await Frames(2);

        // 4 e 5. Fazer agora na ficha: conclui sem abrir a agenda, avança o relógio e solta o número flutuante.
        await DoNowUntilTomorrow(hub, engine, fromMap, tag, shot, report);

        // 7. Pilha de janelas: a marcação abre por cima da ficha, modal; só a de cima recebe clique; Esc volta.
        await CheckWindowStack(hub, tag);

        // Resposta do autor 3: o Você fica à vista com qualquer área aberta.
        foreach (string name in FindAll<Button>(hub).Select(b => b.Name.ToString()).Where(n => n.StartsWith("Area_", StringComparison.Ordinal)).Distinct().ToList())
        {
            await Click(Named<Button>(hub, name)!);
            Require(Named<Control>(hub, "Voce") is { } you && you.IsVisibleInTree() && GetViewport().GetVisibleRect().Encloses(you.GetGlobalRect()), $"{tag}: o painel Você some ou sai da janela com a área {name} aberta.");
        }

        await Click(Named<Button>(hub, "Area_territorio")!);
        var problems = Overflow(hub, $"rl {tag}");
        Require(problems.Count == 0, $"{tag}: controles fora da janela: {string.Join("; ", problems.Take(5))}");

        // A faixa da virada do dia aparece quando o dia vira e some na primeira hora do dia novo.
        ClearDecisions();
        hub.StepDay();
        await Frames(2);
        Require(hub.DayTurnText is not null && hub.DayTurnVisible, $"{tag}: a virada do dia não apareceu sobre o mapa.");
        while (game.State.Time.Minute < 90)
        {
            game.TickCampaignClock(0.05, 1);
        }

        hub.Refresh();
        await Frames(2);
        Require(!hub.DayTurnVisible, $"{tag}: a faixa da virada do dia continua na tela às {hub.ClockText}.");

        // 6. Retratos pelo cache nos cartões do feed que falam de alguém.
        await WaitUntil(() => FindAll<TextureRect>(hub).Any(t => t.Name == "Retrato" && t.Texture is not null && t.IsVisibleInTree()), $"o retrato aparecer num cartão do feed ({tag})");
        report.Add($"{tag}: Você, zonas, Fazer agora, flutuante, pilha com Esc, virada e retratos conferidos");
    }

    private void CheckYou(HubScreen hub, CampaignEngine engine, string tag)
    {
        YouStatus you = engine.You();
        var expected = new (string Name, string Value)[]
        {
            ("Voce_TurnosHoje", $"{you.FreeToday} de {you.Today.Count}"),
            ("Voce_TurnosSemana", $"{you.FreeWeek} de {you.WeekTurns}"),
            ("Voce_Dinheiro", Money(you.Money)),
            ("Voce_Reconhecimento", string.Format(PtBrCulture, "{0:0}%", Game.Instance.State!.Campaign!.Player.Recognition)),
        };
        foreach (var (name, value) in expected)
        {
            Control? row = Named<Control>(hub, name);
            string? shown = row is null ? null : Named<Label>(row, "Valor")?.Text;
            Require(shown == value && ThreePartTip(row), $"{tag}: painel Você, {name}: mostra \"{shown}\", o Core diz \"{value}\" (ou falta o tooltip em três partes).");
        }

        Control? stress = Named<Control>(hub, "Voce_Estresse");
        Require(stress is not null && ThreePartTip(stress) && TextOf(stress).Contains(you.StressLimit.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            && Named<Control>(hub, "BarraEstresse") is not null && ThreePartTip(Named<Control>(hub, "Voce_Eleicao")) && ThreePartTip(Named<Control>(hub, "Imagem")),
            $"{tag}: painel Você sem o estresse com o limite ({you.StressLimit}), a barra, a imagem ou a eleição com tooltip.");
    }

    /// <summary>
    /// "Fazer agora" pela ficha até não sobrar turno hoje: cada clique conclui o compromisso sem abrir a agenda, com o
    /// número flutuante; o terceiro turno mostra o estresse dele na previsão; sem turno hoje, o botão vira "Fazer amanhã
    /// de manhã" e só então o dia vira.
    /// </summary>
    private async Task DoNowUntilTomorrow(HubScreen hub, CampaignEngine engine, string zone, string tag, string? shot, List<string> report)
    {
        Game game = Game.Instance;
        AgendaRules rules = engine.Context.Campaign.Commitments.Regras;
        bool thirdSeen = false;
        for (int i = 0; i < 4; i++)
        {
            ClearDecisions();
            NowPlan plan = engine.PlanNow(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha));
            if (plan.Tomorrow)
            {
                break;
            }

            Require(plan.Failure is null && Named<Button>(hub, "FazerAgora") is { Disabled: false } button && button.Text == Tr("Fazer agora"), $"{tag}: com turno livre hoje, o botão da ficha não é \"Fazer agora\" ({plan.Failure}).");
            int extra = plan.Forecast!.Stress - engine.Context.Campaign.Commitments.Get("caminhada").Estresse;
            if (extra > 0)
            {
                // Terceiro turno do dia: continua "Fazer agora" e a previsão mostra o custo, com o número do Core.
                string expected = "+" + extra.ToString(CultureInfo.InvariantCulture);
                Require(Named<Control>(hub, "EstresseTurno") is { } warn && TextOf(warn).Contains(expected, StringComparison.Ordinal), $"{tag}: o terceiro turno não mostra \"{expected}\" de estresse na previsão.");
                thirdSeen = true;
            }

            int log = engine.Agenda.Log.Count;
            int floats = hub.ZoneMap.FloatTexts.Count;
            int stress = game.State!.Player.Resources.Stress;
            await Click(Named<Button>(hub, "FazerAgora")!);
            Require(engine.Agenda.Log.Count > log && hub.SheetName == "Zona" && hub.ZoneMap.FloatTexts.Count > floats && hub.ZoneMap.FloatTexts.Skip(floats).Any(t => t.Contains(Tr("presença"), StringComparison.Ordinal)),
                $"{tag}: Fazer agora não concluiu o compromisso na ficha ou não soltou o número flutuante (ficha {hub.SheetName}).");
            Require(hub.ZoneMap.FloatsOnScreen > 0, $"{tag}: o número flutuante não apareceu no mapa.");
            CommitmentResult done = engine.Agenda.Log.Last(r => r.Commitment.ZoneId == zone);
            Require(done.Commitment.Turno == plan.Turno && done.Stress == plan.Forecast.Stress && game.State.Player.Resources.Stress == stress + engine.Agenda.Log.Skip(log).Sum(r => r.Stress),
                $"{tag}: Fazer agora fez o compromisso em {done.Commitment.Turno} (previsto {plan.Turno}) com estresse {done.Stress} (previsto {plan.Forecast.Stress}).");
            if (i == 0)
            {
                // Correção do autor: a ficha fica no painel da esquerda, fora do mapa.
                Rect2 mapRect = hub.ZoneMap.GetGlobalRect();
                Control ficha = Named<Control>(hub, "FichaZona")!;
                Require(!ficha.GetGlobalRect().Intersects(mapRect), $"{tag}: a ficha da zona cobre o mapa ({ficha.GetGlobalRect()} sobre {mapRect}).");
                Save(shot, "_" + tag);
            }
        }

        Require(thirdSeen, $"{tag}: nenhum Fazer agora caiu no terceiro turno do dia.");

        // Sem turno hoje: "Fazer amanhã de manhã", com a previsão dizendo que é amanhã.
        foreach (Commitment c in engine.Agenda.Scheduled.Where(c => c.Date == engine.Today.AddDays(1)).ToList())
        {
            engine.Cancel(c.Id);
        }

        hub.Refresh();
        await Frames(2);
        Require(Named<Button>(hub, "FazerAgora") is { } tomorrow && tomorrow.Text == Tr("Fazer amanhã de manhã")
            && Named<Control>(hub, "PrevisaoAgora") is { } forecast && TextOf(forecast).Contains(Tr("amanhã"), StringComparison.Ordinal),
            $"{tag}: sem turno livre hoje, o botão deveria ser \"Fazer amanhã de manhã\" com a previsão de amanhã ({Named<Button>(hub, "FazerAgora")?.Text}).");
        DateOnly today = engine.Today;
        int count = engine.Agenda.Log.Count;
        ClearDecisions();
        await Click(Named<Button>(hub, "FazerAgora")!);
        Require((engine.Today == today.AddDays(1) && engine.Agenda.Log.Count > count) || engine.DecisionPending, $"{tag}: Fazer amanhã de manhã não virou o dia nem fez o compromisso.");
        ClearDecisions();
        hub.Refresh();
        await Frames(2);
        report.Add($"{tag}: Fazer agora três vezes e amanhã de manhã");
    }

    private async Task CheckWindowStack(HubScreen hub, string tag)
    {
        Require(hub.WindowNames.SequenceEqual(["FichaZona"]), $"{tag}: a ficha da zona não está aberta antes da pilha ({string.Join(",", hub.WindowNames)}).");
        await Click(Named<Button>(hub.TopWindow!, "MarcarAqui")!);
        Require(hub.WindowNames.SequenceEqual(["FichaZona", "JanelaAgendar"]) && hub.SheetName == "Marcar", $"{tag}: Agendar não abriu por cima da ficha ({string.Join(",", hub.WindowNames)}).");

        // Só a de cima recebe clique: o ponto sobre o botão da ficha de baixo cai no bloqueio.
        Control lower = Named<Control>(hub, "FichaZona")!;
        Button below = Named<Button>(lower, "Voltar")!;
        Vector2 point = below.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, inLocalCoords: true);
        await Frames(1);
        Control? hovered = GetViewport().GuiGetHoveredControl();
        Require(hovered is not null && hovered != below && !below.IsAncestorOf(hovered), $"{tag}: o botão da janela de baixo recebe o mouse com a de cima aberta ({hovered?.Name}).");

        // Foco preso na modal: o foco levado para fora volta para dentro dela.
        Named<Button>(hub, "Area_agenda")!.GrabFocus();
        await Frames(3);
        Control? focus = GetViewport().GuiGetFocusOwner();
        Require(focus is not null && hub.TopWindow!.IsAncestorOf(focus), $"{tag}: o foco saiu da janela modal ({focus?.Name}).");

        // Esc fecha a de cima, depois a ficha.
        await PressEscape();
        Require(hub.WindowNames.SequenceEqual(["FichaZona"]) && hub.SheetName == "Zona", $"{tag}: Esc não voltou para a ficha ({string.Join(",", hub.WindowNames)}).");
        await PressEscape();
        Require(hub.WindowNames.Count == 0 && hub.SheetName == "Nenhuma", $"{tag}: o segundo Esc não fechou a ficha ({string.Join(",", hub.WindowNames)}).");
    }

    private async Task PressEscape()
    {
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
        GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
        await Frames(2);
    }

    /// <summary>
    /// Campanha em Mantena jogada por um robô simples (caminhadas nas zonas menos cansadas e de menor presença, o resto pelo Core) e
    /// capturas da tela central nas semanas 1, 10 e 20, nas três resoluções, com a ficha da zona e o "Fazer agora".
    /// </summary>
    private async Task CaptureWeeks(string folder, List<string> report)
    {
        Game game = Game.Instance;
        await Resize(ReadingSizes[1]);
        HubScreen hub = await OpenHubIn(MantenaMg, 11);
        CampaignEngine engine = game.Engine!;
        DateOnly start = game.State!.Calendar.WeekStart;
        foreach (int week in new[] { 1, 10, 20 })
        {
            while (game.State.Calendar.WeekStart < start.AddDays(7 * (week - 1)))
            {
                ClearDecisions();
                game.State.Player.Resources.Money = Math.Max(game.State.Player.Resources.Money, 30_000);
                var rows = engine.ZoneBoard().OrderByDescending(r => Math.Round(r.WorstYield, 1)).ThenBy(r => r.Presence).ToList();
                engine.Schedule(new CommitmentRequest("caminhada", rows[0].ZoneId, engine.Today, Turno.Manha));
                engine.Schedule(new CommitmentRequest("caminhada", rows[1].ZoneId, engine.Today, Turno.Tarde));
                Game.DayReport day = game.AdvanceDay();
                if (day.Stopped)
                {
                    ClearDecisions();
                }
            }

            ClearDecisions();
            game.State.Player.Resources.Money = Math.Max(game.State.Player.Resources.Money, 30_000);
            string zone = engine.ZoneBoard().OrderBy(r => r.Presence).First().ZoneId;
            hub.ZoneMap.EmitZoneClickForTest(zone);
            hub.Refresh();
            await Frames(2);
            await Click(Named<Button>(hub, "FazerAgora")!);
            hub.ZoneMap.EmitZoneClickForTest(zone);
            await Frames(2);
            foreach (Vector2I size in ReadingSizes)
            {
                await Resize(size);
                hub.Refresh();
                await Frames(3);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                string file = $"{folder.TrimEnd('/', '\\')}/semana_{week:00}_{size.X}x{size.Y}.png";
                GetViewport().GetTexture().GetImage().SavePng(file);
            }

            await Resize(ReadingSizes[1]);
            report.Add($"semana {week}: {hub.ClockText}, presença na cidade {game.State.Campaign!.Player.Recognition:0}%");
        }
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
            if (zona.Id != except && map.Hit(at) == zona.Id && safe.HasPoint(map.GlobalPosition + at) && at.Y < map.Size.Y - 420 && at.X > 480)
            {
                return zona.Id;
            }
        }

        return null;
    }
}
