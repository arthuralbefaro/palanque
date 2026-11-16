using Godot;
using Planalto.Autoload;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Teste de fumaça e captura das telas: "-- --target=perfil|newworld|creator|creator17|hub|explicacao|picker|campaign|map|mandate|prefeitura|eventos|layout|agenda|zona|lideranca|equipe|verba|plataforma|objetivos|rivais|midia|contas [--perfil=nome] [--screenshot=arquivo.png]".
/// Os dados de usuário da rodada ficam em user://smoke/&lt;perfil&gt;/, apagado no início; sem --perfil, vale o nome do branch git.
/// Prepara o estado necessário, abre a tela, exercita os botões principais e fecha com código de saída.
/// </summary>
public partial class UiShot : Node
{
    public override async void _Ready()
    {
        try
        {
            await Run();
        }
        catch (Exception e)
        {
            // Erro no próprio teste: sem isto a janela ficaria aberta para sempre.
            Fail("o teste parou com um erro: " + e);
        }
    }

    private async Task Run()
    {
        string[] args = OS.GetCmdlineUserArgs();
        string target = args.FirstOrDefault(a => a.StartsWith("--target=", StringComparison.Ordinal))?.Split('=', 2)[1] ?? "creator";
        string? shot = args.FirstOrDefault(a => a.StartsWith("--screenshot=", StringComparison.Ordinal))?.Split('=', 2)[1];
        string profile = UserPaths.UseSmokeProfile(
            args.FirstOrDefault(a => a.StartsWith("--perfil=", StringComparison.Ordinal))?.Split('=', 2)[1] ?? CurrentBranch());
        UI.Settings.Reload();
        if (target == "perfil")
        {
            await ProfileIsolation(profile);
            return;
        }

        Game game = Game.Instance;
        game.SuppressNavigation = true;

        // O ponteiro do mouse de quem roda o teste pode estar sobre a janela: sem isto, o tooltip do controle sob ele entra nas capturas.
        ProjectSettings.SetSetting("gui/timers/tooltip_delay_sec", 3600.0);
        game.PendingSeed = 2028;
        game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(2028)).Setup;

        if (target == "picker")
        {
            Core.Municipios.Municipio? chosen = null;
            var picker = new Widgets.MunicipioPicker(m => chosen = m);
            AddChild(picker);
            picker.PopupCentered();
            await Frames(5);
            picker.ChooseFirstForTest("Salvador");
            await Frames(2);
            if (chosen?.Nome != "Salvador")
            {
                GD.PushError("Smoke: o seletor de município não escolheu Salvador.");
                GetTree().Quit(1);
                return;
            }

            GD.Print("Smoke picker: ok");
            GetTree().Quit(0);
            return;
        }

        if (target == "agenda")
        {
            await PlayAgenda(shot);
            return;
        }

        if (target == "zona")
        {
            await PlayZone(shot);
            return;
        }

        if (target == "lideranca")
        {
            await PlayLeader(shot);
            return;
        }

        if (target == "equipe")
        {
            await PlayTeam(shot);
            return;
        }

        if (target == "verba")
        {
            await PlayBudget(shot);
            return;
        }

        if (target == "plataforma")
        {
            await PlayPlatform(shot);
            return;
        }

        if (target == "objetivos")
        {
            await PlayObjectives(shot);
            return;
        }

        if (target == "rivais")
        {
            await PlayRivals(shot);
            return;
        }

        if (target == "midia")
        {
            await PlayMedia(shot);
            return;
        }

        if (target == "contas")
        {
            await PlayAccounts(shot);
            return;
        }

        if (target == "explicacao")
        {
            await CheckExplanations(shot);
            return;
        }

        if (target == "layout")
        {
            await CheckLayout(shot);
            return;
        }

        if (target == "eventos")
        {
            await PlayAllEvents();
            return;
        }

        if (target == "prefeitura")
        {
            await PlayMayor(shot);
            return;
        }

        if (target == "mandate")
        {
            await PlayMandate(shot);
            return;
        }

        if (target == "map")
        {
            await ShowMaps(shot);
            return;
        }

        if (target == "campaign")
        {
            await PlayCampaign(shot);
            return;
        }

        Node screen = target switch
        {
            "newworld" => GD.Load<PackedScene>(Game.Scenes.NewWorld).Instantiate(),
            _ => GD.Load<PackedScene>(Game.Scenes.CharacterCreator).Instantiate(),
        };
        AddChild(screen);
        await Frames(5);

        if (screen is CharacterCreatorScreen creator)
        {
            creator.RandomizeEverything();
            await Frames(3);
            if (target == "creator17")
            {
                // 17 anos com aniversário em dezembro: sem 18 anos no prazo do registro de 2028.
                creator.SetAgeForTest(17, 12);
                await Frames(3);
                bool warned = creator.AgeWarningsForTest.Count > 0;
                bool started = creator.TryStartCareer();
                if (!warned || !started || game.State is null || game.Engine is not null || game.LastNavigation != Game.Scenes.OutOfOffice)
                {
                    Fail($"personagem de 17 anos: aviso {warned}, começou {started}, campanha {(game.Engine is null ? "nenhuma" : "aberta")}, tela {game.LastNavigation}.");
                    return;
                }

                if (Planalto.Core.Career.CareerFlow.NextEligibleElectionYear(game.State) != 2032)
                {
                    Fail("a primeira eleição possível deveria ser a de 2032.");
                    return;
                }
            }

            if (target == "hub")
            {
                bool started = creator.TryStartCareer();
                if (!started || game.State is null || game.LastNavigation != Game.Scenes.Hub)
                {
                    GD.PushError("Smoke: a carreira não começou.");
                    GetTree().Quit(1);
                    return;
                }

                RemoveChild(screen);
                screen.QueueFree();
                var hub = (HubScreen)GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate();
                AddChild(hub);
                await Frames(5);

                // Cada tipo de compromisso que não pede zona nem pessoa especial: marca pela tela, deixa o dia correr e
                // confere o resultado explicado no feed (nunca só "feito").
                game.State.Player.Resources.Money = 100_000;
                game.State.Campaign!.Events.Pending.Clear();
                hub.Refresh();
                int turno = 0;
                foreach (string type in new[] { "caminhada", "entrevista", "gravacao" })
                {
                    Named<Button>(hub, "Area_agenda")!.EmitSignal(BaseButton.SignalName.Pressed);
                    await Frames(2);
                    string cell = $"Turno_{game.Engine!.Today:yyyyMMdd}_{turno++}";
                    Named<Button>(hub, cell)!.EmitSignal(BaseButton.SignalName.Pressed);
                    await Frames(2);
                    if (!SelectOption(hub, "Tipo", type))
                    {
                        Fail($"a marcação não oferece o tipo {type}.");
                        return;
                    }

                    await Frames(2);
                    if (Named<Control>(hub, "Previsao") is null || Named<Button>(hub, "Confirmar") is not { Disabled: false } confirm)
                    {
                        Fail($"marcação de {type}: sem previsão ou com Confirmar bloqueado.");
                        return;
                    }

                    confirm.EmitSignal(BaseButton.SignalName.Pressed);
                    await Frames(2);
                }

                if (game.State.Campaign.Agenda.Scheduled.Count != 3)
                {
                    Fail($"esperava 3 compromissos marcados pela tela, há {game.State.Campaign.Agenda.Scheduled.Count}.");
                    return;
                }

                hub.StepDay();
                await Frames(3);
                var cards = FeedTexts(hub);
                if (game.State.Campaign.Agenda.Log.Count != 3 || cards.Count(t => t.Contains(Tr("Sua agenda"), StringComparison.Ordinal) && t.Any(char.IsDigit)) < 3)
                {
                    Fail($"os 3 compromissos do dia não viraram 3 cartões de resultado no feed ({game.State.Campaign.Agenda.Log.Count} no registro).");
                    return;
                }

                foreach (string line in cards.Take(3))
                {
                    GD.Print("  " + line.Replace("\n", " | ", StringComparison.Ordinal));
                }

                // A tela continua inteira e no tamanho da janela depois de tudo isso.
            }
        }

        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot);
        }

        GD.Print($"Smoke {target}: ok");
        GetTree().Quit(0);
    }

    /// <summary>Abre o mapa 2.5D de MG (a UF com mais municípios), SP e AC; confere as malhas e mede tempo e FPS.</summary>
    private async Task ShowMaps(string? shot)
    {
        Game game = Game.Instance;
        var creator = (CharacterCreatorScreen)GD.Load<PackedScene>(Game.Scenes.CharacterCreator).Instantiate();
        AddChild(creator);
        await Frames(3);
        creator.TryStartCareer();
        RemoveChild(creator);
        creator.QueueFree();

        foreach (string uf in new[] { "MG", "SP", "AC" })
        {
            var map = (UI.Map.MapScreen)GD.Load<PackedScene>(Game.Scenes.Map).Instantiate();
            map.Uf = uf;
            AddChild(map);
            await Frames(10);
            await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
            int expected = game.Municipios.All.Count(m => m.Uf == uf);
            GD.Print($"Mapa {uf}: {map.MeshCount}/{expected} municípios, montagem {map.BuildMilliseconds} ms, {Engine.GetFramesPerSecond():0} FPS");
            if (map.MeshCount != expected)
            {
                GD.PushError($"Smoke: mapa de {uf} com {map.MeshCount} de {expected} municípios.");
                GetTree().Quit(1);
                return;
            }

            if (shot is not null)
            {
                GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", $"_mapa_{uf}.png"));
            }

            // Troca a altura para o partido presente em mais municípios e remonta as malhas.
            var rebuild = System.Diagnostics.Stopwatch.StartNew();
            map.ShowStrengthOf(map.MostPresentParty());
            rebuild.Stop();
            await Frames(10);
            GD.Print($"Mapa {uf}: remontagem {rebuild.ElapsedMilliseconds} ms, {map.MeshCount} malhas");
            if (shot is not null)
            {
                GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", $"_mapa_{uf}_forca.png"));
            }

            RemoveChild(map);
            map.QueueFree();
        }

        GD.Print("Smoke map: ok");
        GetTree().Quit(0);
    }

    /// <summary>
    /// Nenhum controle cortado: em 1920x1080 e 1366x768, abre a criação de personagem (todas as abas), o Novo
    /// Mundo e o hub, tira print e falha se algum controle visível passar dos limites da janela.
    /// </summary>
    private async Task CheckLayout(string? shot)
    {
        Game game = Game.Instance;
        var problems = new List<string>();
        foreach (Vector2I size in new[] { new Vector2I(1920, 1080), new Vector2I(1366, 768) })
        {
            GetTree().Root.Size = size;
            DisplayServer.WindowSetSize(size);
            await Frames(4);
            string tag = $"{size.X}x{size.Y}";

            game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(2028)).Setup;
            var world = GD.Load<PackedScene>(Game.Scenes.NewWorld).Instantiate<Control>();
            AddChild(world);
            await Frames(5);
            problems.AddRange(Overflow(world, $"Novo Mundo {tag}"));
            Save(shot, $"_novo_mundo_{tag}");
            RemoveChild(world);
            world.QueueFree();

            game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(2028)).Setup;
            var creator = GD.Load<PackedScene>(Game.Scenes.CharacterCreator).Instantiate<CharacterCreatorScreen>();
            AddChild(creator);
            await Frames(5);
            var tabs = FindAll<TabContainer>(creator).First();
            for (int tab = 0; tab < tabs.GetTabCount(); tab++)
            {
                tabs.CurrentTab = tab;
                await Frames(4);
                problems.AddRange(Overflow(creator, $"Criação, aba {tabs.GetTabTitle(tab)}, {tag}"));
                Save(shot, $"_criacao_{tab}_{tag}");
            }

            creator.TryStartCareer();
            RemoveChild(creator);
            creator.QueueFree();
            var first = GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate<Control>();
            AddChild(first);
            await Frames(5);
            problems.AddRange(Overflow(first, $"Hub {tag}"));
            Save(shot, $"_hub_{tag}");
            RemoveChild(first);
            first.QueueFree();

            // Tela central da R2a em três portes: cada área do trilho, as fichas, a marcação, o pedido e os modos do mapa.
            foreach (var (code, label) in new[] { (SmallCity, "pequena"), (MediumCity, "media"), (Metropolis, "metropole") })
            {
                UI.Settings.ResetForTest();
                HubScreen hub = await OpenHubIn(code, 5);
                PrepareBusyWeek(hub);
                await Frames(3);
                foreach (string area in AreaIds)
                {
                    Named<Button>(hub, "Area_" + area)!.EmitSignal(BaseButton.SignalName.Pressed);
                    await Frames(3);
                    problems.AddRange(Overflow(hub, $"Hub {label}, área {area}, {tag}"));
                    if (area == "agenda")
                    {
                        Save(shot, $"_agenda_{label}_{tag}");
                    }
                    else if (area == "pessoas")
                    {
                        Save(shot, $"_pessoas_{label}_{tag}");
                    }
                }

                foreach (string mode in new[] { "lider", "renda", "forca" })
                {
                    Named<Button>(hub, "Modo_" + mode)!.EmitSignal(BaseButton.SignalName.Pressed);
                    await Frames(3);
                    problems.AddRange(Overflow(hub, $"Hub {label}, mapa {mode}, {tag}"));
                    Save(shot, $"_mapa_{mode}_{label}_{tag}");
                }

                // Ficha da zona e a marcação a partir dela.
                string zone = game.State!.Campaign!.Territory.Zones[game.State.Campaign.Territory.HomeZone];
                Named<Button>(hub, "Area_territorio")!.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(2);
                Named<Button>(hub, "Zona_" + zone)!.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(3);
                problems.AddRange(Overflow(hub, $"Hub {label}, ficha da zona, {tag}"));
                Save(shot, $"_ficha_zona_{label}_{tag}");
                Named<Button>(hub, "MarcarAqui")!.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(3);
                problems.AddRange(Overflow(hub, $"Hub {label}, marcação, {tag}"));
                Save(shot, $"_marcar_{label}_{tag}");
                Named<Button>(hub, "Cancelar")!.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(2);

                // Ficha da pessoa e o cartão do pedido.
                var leader = game.Engine!.City.Leaders.Where(l => l.Known).MaxBy(l => game.Engine.RelationOf(l.PersonId))!;
                Named<Button>(hub, "Area_pessoas")!.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(2);
                await ClickCard(Named<Control>(hub, "Lideranca_" + leader.PersonId)!);
                await Frames(2);
                if (hub.SheetName != "Pessoa")
                {
                    problems.Add($"Hub {label}, {tag}: o clique na liderança não abriu a ficha da pessoa");
                }

                problems.AddRange(Overflow(hub, $"Hub {label}, ficha da pessoa, {tag}"));
                Save(shot, $"_ficha_pessoa_{label}_{tag}");
                game.State.Campaign.Agenda.PendingRequest = leader.PersonId;
                hub.Refresh();
                await Frames(3);
                problems.AddRange(Overflow(hub, $"Hub {label}, pedido, {tag}"));
                if (Named<Control>(hub, "CartaoPedido") is { } card && card.Size.Y > 640 + 1)
                {
                    problems.Add($"Hub {label}, {tag}: o cartão do pedido mede {card.Size.Y:0} de altura (máximo 640)");
                }

                Save(shot, $"_pedido_{label}_{tag}");
                game.State.Campaign.Agenda.PendingRequest = null;
                hub.Refresh();
                await Frames(2);

                // Telas da R2b (os três blocos, Equipe, decisão da equipe, Verba, Plataforma e a coluna com Objetivos) e
                // da R3 (rivais, chapa, ataque, Mídia, veículo, Contas, oferta, modo Disputa e cartão de evento).
                Func<string, string?, Task> measure = (where, suffix) =>
                {
                    problems.AddRange(Overflow(hub, $"Hub {label}, {where}, {tag}"));
                    if (hub.LeftPanelWidth > 401)
                    {
                        problems.Add($"Hub {label}, {where}, {tag}: o painel da área mede {hub.LeftPanelWidth:0} de largura (deveria ter 400): algum conteúdo não quebra a linha");
                    }

                    if (hub.RightPanelWidth > 401)
                    {
                        problems.Add($"Hub {label}, {where}, {tag}: a coluna direita mede {hub.RightPanelWidth:0} de largura (deveria ter 400): algum conteúdo não quebra a linha");
                    }

                    if (Named<Control>(hub, "CartaoEquipe") is { } staffCard && staffCard.Size.Y > 640 + 1)
                    {
                        problems.Add($"Hub {label}, {tag}: o cartão da decisão da equipe mede {staffCard.Size.Y:0} de altura (máximo 640)");
                    }

                    if (Named<Control>(hub, "CartaoEvento") is { } eventCard && eventCard.Size.Y > 640 + 1)
                    {
                        problems.Add($"Hub {label}, {tag}: o cartão do evento mede {eventCard.Size.Y:0} de altura (máximo 640)");
                    }

                    if (suffix is not null)
                    {
                        Save(shot, $"_{suffix}_{label}_{tag}");
                    }

                    return Task.CompletedTask;
                };
                await VisitCampaignPanels(hub, measure);
                await VisitR3Panels(hub, measure);
                RemoveChild(hub);
                hub.QueueFree();
            }
        }

        if (problems.Count > 0)
        {
            foreach (var group in problems.GroupBy(p => p[..p.IndexOf(':')]))
            {
                GD.PushError($"  {group.Key}: {group.Count()} controles; o mais largo: {group.First()}");
            }

            foreach (string problem in problems.Where(p => p.Contains("Label") || p.Contains("Button") || p.Contains("CheckBox")).Take(12))
            {
                GD.PushError("  " + problem);
            }

            Fail($"{problems.Count} controles passam da janela.");
            return;
        }

        GD.Print("Smoke layout: ok (1920x1080 e 1366x768: criação em todas as abas, Novo Mundo e a tela central em cidade pequena, média e metrópole, com as 7 áreas, 3 modos do mapa, ficha da zona, marcação, ficha da pessoa, cartão do pedido, as telas da R2b: Campanha com os três blocos, Equipe, decisão da equipe, Verba, canais, modo Verba, Plataforma, tema, prometer e Objetivos, e as da R3: Pessoas com a chapa, painel da chapa, ficha do rival, atacar, Mídia, ficha de veículo, entrevista com veículo, ataque com prazo, Finanças com o bloco Contas, painel Contas, oferta de doação, modo Disputa e cartão de evento com pessoas)");
        GetTree().Quit(0);
    }

    private void Save(string? shot, string suffix)
    {
        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", suffix + ".png"));
        }
    }

    /// <summary>
    /// Controles visíveis que passam dos limites da janela. O que está dentro de um ScrollContainer (rola) ou de
    /// um controle que recorta o conteúdo não conta; o contêiner em si, sim.
    /// </summary>
    private List<string> Overflow(Node root, string where)
    {
        Rect2 window = GetViewport().GetVisibleRect();
        var problems = new List<string>();
        void Visit(Node node, bool contained)
        {
            if (node is Window)
            {
                return;
            }

            if (node is Control control && control.IsVisibleInTree() && !contained && control.Size.X > 0 && control.Size.Y > 0)
            {
                Rect2 rect = control.GetGlobalRect();
                if (rect.Position.X < window.Position.X - 1 || rect.Position.Y < window.Position.Y - 1 || rect.End.X > window.End.X + 1 || rect.End.Y > window.End.Y + 1)
                {
                    string label = control switch { Button b => $" \"{b.Text}\"", Label l => $" \"{l.Text[..Math.Min(40, l.Text.Length)]}\"", _ => "" };
                    problems.Add($"{where}: {control.GetType().Name}{label} em {rect} (janela {window.Size})");
                }
            }

            bool inner = contained || node is ScrollContainer || node is Control { ClipContents: true };
            foreach (Node child in node.GetChildren())
            {
                Visit(child, inner);
            }
        }

        Visit(root, false);

        // Painel que só rola na vertical: nada dentro dele pode passar da largura (seria cortado sem aviso).
        foreach (ScrollContainer scroll in FindAll<ScrollContainer>(root).Where(sc => sc.IsVisibleInTree() && sc.HorizontalScrollMode == ScrollContainer.ScrollMode.Disabled))
        {
            Rect2 frame = scroll.GetGlobalRect();
            foreach (Control inside in FindAll<Control>(scroll).Where(c => c.IsVisibleInTree() && c.Size.X > 0 && c is not ScrollBar))
            {
                // Filho de controle que recorta o conteúdo (camadas do retrato) não aparece fora dele; o que está numa
                // janela própria (o tooltip que o Godot abre sob o ponteiro do mouse) tem coordenadas dela e não conta.
                bool clipped = false;
                for (Node? up = inside.GetParent(); up is not null && up != scroll; up = up.GetParent())
                {
                    clipped |= up is Control { ClipContents: true } or Window;
                }

                if (clipped)
                {
                    continue;
                }

                Rect2 rect = inside.GetGlobalRect();
                if (rect.Position.X < frame.Position.X - 1 || rect.End.X > frame.End.X + 1)
                {
                    string label = inside switch { Button b => $" \"{b.Text}\"", Label l => $" \"{l.Text[..Math.Min(40, l.Text.Length)]}\"", _ => $" {inside.Name}" };
                    problems.Add($"{where}: {inside.GetType().Name}{label} passa da largura do painel ({rect.Position.X:0} a {rect.End.X:0}, painel {frame.Position.X:0} a {frame.End.X:0})");
                }
            }
        }

        return problems;
    }

    /// <summary>
    /// Todos os eventos, todas as opções, com cliques de mouse de verdade: abre o evento no hub (Mantena/MG,
    /// fevereiro de 2028), clica a opção e "Continuar", e confere que o fundo escuro saiu e que um clique no hub
    /// volta a chegar aos botões.
    /// </summary>
    private async Task PlayAllEvents()
    {
        Game game = Game.Instance;
        if (DisplayServer.GetName() == "headless")
        {
            // Sem janela, o Godot não entrega cliques de mouse à interface: este teste precisa rodar com janela.
            Fail("o teste de eventos clica com o mouse e precisa de janela (rode sem --headless).");
            return;
        }

        var world = game.PendingWorld!;
        var city = game.Municipios.All.First(m => m.CodigoIbge == 3139607);
        var player = Core.Characters.PersonGenerator.Generate(new Rng(11), game.People, new Core.Characters.PersonRequest(Core.Campaign.RaceSetup.PlayerId, city.Uf, city.CodigoIbge, new DateOnly(world.Options.StartYear, 1, 1), Age: 35, PartyId: world.Parties[0].Id));
        game.StartCareer(NewGame.Create(world, player, game.PendingSeed));
        var engine = game.Engine!;
        var campaign = game.State!.Campaign!;
        while (game.State.Calendar.WeekStart.Month < 2)
        {
            campaign.Events.Pending.Clear();
            game.AdvanceWeek();
        }

        campaign.Events.Pending.Clear();
        var hub = (HubScreen)GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate();
        AddChild(hub);
        await Frames(3);

        {
            var probe = FindAll<Button>(hub).First(b => b.Text == Tr("Mapa"));
            game.ResetNavigationForTest();
            await Click(probe);
            if (game.LastNavigation != Game.Scenes.Map)
            {
                Fail($"o clique simulado não acertou o botão Mapa num hub novo (sob o mouse: {GetViewport().GuiGetHoveredControl()?.GetPath()}).");
                return;
            }
        }

        int clicked = 0, blocked = 0;
        foreach (var definition in game.Events.Events)
        {
            for (int choice = 0; choice < definition.Choices.Count; choice++)
            {
                game.State.Player.Resources.Money = 50_000;
                Core.Events.EventEngine.Trigger(engine.Context, definition.Id, game.State.Rng);
                hub.Refresh();
                await Frames(3);
                var dialog = FindAll<Widgets.EventDialog>(hub).SingleOrDefault();
                if (dialog is null || !hub.EventOpen)
                {
                    Fail($"o evento {definition.Id} não abriu.");
                    return;
                }

                // Só as escolhas: o cartão também tem os nomes clicáveis de quem está envolvido.
                var buttons = FindAll<Button>(dialog).Where(b => b.Name.ToString().StartsWith("Escolha_", StringComparison.Ordinal)).ToList();
                if (buttons.Count != definition.Choices.Count)
                {
                    Fail($"o evento {definition.Id} mostra {buttons.Count} escolhas; a definição tem {definition.Choices.Count}.");
                    return;
                }

                if (buttons[choice].Disabled)
                {
                    blocked++;
                    Core.Events.EventEngine.Resolve(engine.Context, campaign.Events.Pending[0], Enumerable.Range(0, definition.Choices.Count).First(i => !buttons[i].Disabled), game.State.Rng);
                    hub.Refresh();
                    await Frames(3);
                    continue;
                }

                await Click(buttons[choice]);
                var proceed = FindAll<Button>(dialog).FirstOrDefault(b => b.Text == Tr("Continuar"));
                if (proceed is null)
                {
                    Fail($"{definition.Id}, opção {choice + 1}: sem o botão Continuar depois de escolher.");
                    return;
                }

                await Click(proceed);
                await Frames(3);

                // Evento dinâmico disparado pelo fato desta escolha (GDD 15.4): é outro cartão, que tem a própria volta
                // neste teste. Aqui ele precisa ter aberto no lugar do primeiro; resolve-se pelo Core e a conferência segue.
                while (campaign.Events.Pending.FirstOrDefault() is { FactId: not null } chained)
                {
                    if (!hub.EventOpen)
                    {
                        Fail($"{definition.Id}, opção {choice + 1}: o evento em cadeia {chained.EventId} ficou pendente sem cartão.");
                        return;
                    }

                    var next = game.Events.Find(chained.EventId)!;
                    Core.Events.EventEngine.Resolve(engine.Context, chained, Enumerable.Range(0, next.Choices.Count).First(i => Core.Events.EventEngine.Blocked(engine.Context, next.Choices[i]) is null), game.State.Rng);
                    hub.Refresh();
                    await Frames(3);
                }

                if (hub.EventOpen || campaign.Events.Pending.Count > 0)
                {
                    Fail($"{definition.Id}, opção {choice + 1}: o fundo do evento continuou na tela.");
                    return;
                }

                var map = FindAll<Button>(hub).First(b => b.Text == Tr("Mapa"));
                game.ResetNavigationForTest();
                await Click(map);
                if (game.LastNavigation != Game.Scenes.Map)
                {
                    Fail($"{definition.Id}, opção {choice + 1}: o hub não aceitou o clique depois do evento (botão em {map.GetGlobalRect()}, sob o mouse: {GetViewport().GuiGetHoveredControl()?.GetPath()}, janela {GetViewport().GetVisibleRect().Size}).");
                    return;
                }

                clicked++;
            }
        }

        GD.Print($"Smoke eventos: ok ({game.Events.Events.Count} eventos, {clicked} opções clicadas, {blocked} bloqueadas por requisito)");
        GetTree().Quit(0);
    }

    /// <summary>
    /// Princípio j (GDD 17.1): na tela central, em todas as áreas do trilho e modos do mapa, todo controle interativo tem
    /// tooltip em três partes, todo controle bloqueado diz o motivo, nenhum termo do glossário fica em Label sem link,
    /// e F1 abre a Enciclopédia. Precisa de janela (o F1 entra como tecla).
    /// </summary>
    private async Task CheckExplanations(string? shot)
    {
        Game game = Game.Instance;
        UI.Settings.ResetForTest();
        var world = game.PendingWorld!;
        var city = game.Municipios.All.First(m => m.CodigoIbge == 3106200);
        var player = Core.Characters.PersonGenerator.Generate(new Rng(5), game.People, new Core.Characters.PersonRequest(Core.Campaign.RaceSetup.PlayerId, city.Uf, city.CodigoIbge, new DateOnly(world.Options.StartYear, 1, 1), Age: 35, PartyId: world.Parties[0].Id));
        game.StartCareer(NewGame.Create(world, player, game.PendingSeed));
        game.State!.Campaign!.Events.Pending.Clear();
        var hub = (HubScreen)GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate();
        AddChild(hub);
        await Frames(5);

        var problems = new List<string>();
        void Check(Node root, string where)
        {
            foreach (Control c in FindAll<Control>(root).Where(c => c.IsVisibleInTree()))
            {
                if (c is BaseButton or LineEdit or SpinBox or PanelContainer { MouseDefaultCursorShape: Control.CursorShape.PointingHand })
                {
                    string tip = c.TooltipText;
                    string name = c is Button b ? b.Text : c.Name;
                    if (!tip.Contains(Tr("O que faz:"), StringComparison.Ordinal) || !tip.Contains(Tr("Quanto custa:"), StringComparison.Ordinal))
                    {
                        problems.Add($"{where}: controle '{name}' sem tooltip em três partes");
                    }

                    if (c is BaseButton { Disabled: true } && !tip.StartsWith(Tr("Bloqueado:"), StringComparison.Ordinal))
                    {
                        problems.Add($"{where}: controle bloqueado '{name}' sem motivo");
                    }
                }
                else if (c is Label label && !label.Name.ToString().StartsWith("TituloVerbete", StringComparison.Ordinal) && Widgets.Glossary.TermsIn(label.Text).FirstOrDefault() is { } term)
                {
                    problems.Add($"{where}: termo '{term}' sem link em \"{label.Text}\"");
                }
            }
        }

        PrepareBusyWeek(hub);
        int areas = AreaIds.Length;
        foreach (string area in AreaIds)
        {
            Named<Button>(hub, "Area_" + area)!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            Check(hub, $"área {area}");
        }

        var modes = FindAll<Button>(hub).Where(b => b.Name.ToString().StartsWith("Modo_", StringComparison.Ordinal)).Select(b => b.Name.ToString()).ToList();
        foreach (string mode in modes)
        {
            Named<Button>(hub, mode)!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            Check(hub, $"mapa {mode}");
        }

        // Ficha de uma zona, a marcação a partir dela e o detalhe de um compromisso marcado.
        var zoneMap = hub.ZoneMap;
        string? biggest = zoneMap.Zonas.Values.OrderByDescending(z => z.Populacao).FirstOrDefault()?.Id;
        if (biggest is null)
        {
            Fail("a tela central não carregou as zonas da cidade.");
            return;
        }

        Named<Button>(hub, "Area_territorio")!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Named<Button>(hub, "Zona_" + biggest)!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (hub.SheetName != "Zona")
        {
            Fail("a ficha da zona não abriu.");
            return;
        }

        Check(hub, "ficha da zona");
        Named<Button>(hub, "MarcarAqui")!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        foreach (string type in game.Campaign.Commitments.Tipos.Select(t => t.Id))
        {
            if (SelectOption(hub, "Tipo", type))
            {
                await Frames(2);
                Check(hub, $"marcação de {type}");
            }
        }

        Named<Button>(hub, "Cancelar")!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Named<Button>(hub, "Area_agenda")!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        var marked = game.State.Campaign.Agenda.Scheduled[0];
        Named<Button>(hub, $"Turno_{marked.Date:yyyyMMdd}_{(int)marked.Turno}")!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        if (hub.SheetName != "Compromisso")
        {
            Fail("o clique num turno marcado não abriu o compromisso.");
            return;
        }

        Check(hub, "compromisso marcado");

        // Ficha de uma pessoa, o cartão do pedido e a lista "O que mudou".
        var known = game.Engine!.City.Leaders.Where(l => l.Known).MaxBy(l => game.Engine.RelationOf(l.PersonId))!;
        Named<Button>(hub, "Area_pessoas")!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        await ClickCard(Named<Control>(hub, "Lideranca_" + known.PersonId)!);
        await Frames(2);
        if (hub.SheetName != "Pessoa")
        {
            Fail("o clique na liderança não abriu a ficha da pessoa.");
            return;
        }

        Check(hub, "ficha da pessoa");
        game.State.Campaign.Agenda.PendingRequest = known.PersonId;
        hub.Refresh();
        await Frames(2);
        if (!hub.RequestOpen || Named<Control>(hub, "FaixaDecisao") is null)
        {
            Fail("pedido de liderança sem o cartão ou sem a faixa de decisão.");
            return;
        }

        Check(hub, "cartão do pedido");
        Named<Button>(hub, "Resposta_NaoSeComprometer")!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Check(hub, "o que mudou do pedido");
        Named<Button>(hub, "Continuar")!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Check(hub, "ficha da pessoa com lembrança");

        // Telas da R2b: Campanha com os três blocos, Equipe, decisão da equipe, marcação com representante e tema,
        // Verba e canais, Plataforma, tema, prometer e a coluna com Objetivos.
        int panels = 0;
        await VisitCampaignPanels(hub, (where, _) =>
        {
            panels++;
            Check(hub, where);
            return Task.CompletedTask;
        });
        if (panels < 20)
        {
            Fail($"o teste de explicação só passou por {panels} telas da R2b (esperado: 20 ou mais).");
            return;
        }

        // Telas da R3: Pessoas com a chapa, ficha do rival, atacar, Mídia, veículos, ataque com prazo, Contas, oferta, modo Disputa e evento.
        int r3 = 0;
        await VisitR3Panels(hub, (where, _) =>
        {
            r3++;
            Check(hub, where);
            return Task.CompletedTask;
        });
        if (r3 < 18)
        {
            Fail($"o teste de explicação só passou por {r3} telas da R3 (esperado: 18 ou mais).");
            return;
        }

        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F1, PhysicalKeycode = Key.F1, Pressed = true });
        await Frames(4);
        if (!Widgets.EncyclopediaDialog.IsOpen(GetTree().Root))
        {
            Fail("F1 não abriu a Enciclopédia.");
            return;
        }

        Node encyclopedia = GetTree().Root.FindChild("Enciclopedia", recursive: true, owned: false)!;
        Check(encyclopedia, "Enciclopédia");
        foreach (string tab in new[] { "Como funciona", "Este mundo", "Sua carreira" })
        {
            FindAll<Button>(encyclopedia).First(b => b.Text == Tr(tab)).EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            Check(encyclopedia, $"Enciclopédia, {tab}");
        }

        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot);
        }

        if (problems.Count > 0)
        {
            foreach (string p in problems.Distinct().Take(40))
            {
                GD.PushError("  " + p);
            }

            Fail($"{problems.Distinct().Count()} problemas de explicação.");
            return;
        }

        GD.Print($"Smoke explicacao: ok ({areas} áreas, {zoneMap.Zonas.Count} zonas, {panels} telas da R2b, {r3} telas da R3, Enciclopédia aberta com F1)");
        GetTree().Quit(0);
    }

    private const int SmallCity = 3102803;
    private const int MediumCity = 3139607;
    private const int Metropolis = 3106200;

    private static readonly string[] AreaIds = ["campanha", "pessoas", "agenda", "territorio", "financas", "midia", "partido"];

    /// <summary>Começa uma carreira na cidade e abre a tela central, sem evento pendente.</summary>
    private async Task<HubScreen> OpenHubIn(int codigoIbge, ulong seed)
    {
        Game game = Game.Instance;
        game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(2028)).Setup;
        var world = game.PendingWorld!;
        var city = game.Municipios.All.First(m => m.CodigoIbge == codigoIbge);
        var player = Core.Characters.PersonGenerator.Generate(new Rng(seed), game.People, new Core.Characters.PersonRequest(Core.Campaign.RaceSetup.PlayerId, city.Uf, city.CodigoIbge, new DateOnly(world.Options.StartYear, 1, 1), Age: 35, PartyId: world.Parties[0].Id));
        game.StartCareer(NewGame.Create(world, player, game.PendingSeed));
        game.State!.Campaign!.Events.Pending.Clear();
        var hub = (HubScreen)GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate();
        AddChild(hub);
        await Frames(5);
        return hub;
    }

    /// <summary>
    /// Enche a semana para as capturas e para o teste de explicação: compromissos marcados, um já concluído, um
    /// convite e um rival visto, pelo Core, e a tela redesenhada.
    /// </summary>
    private static void PrepareBusyWeek(HubScreen hub)
    {
        Game game = Game.Instance;
        var engine = game.Engine!;
        var campaign = game.State!.Campaign!;
        game.State.Player.Resources.Money = 50_000;
        DateOnly today = engine.Today;
        string home = campaign.Territory.Zones[campaign.Territory.HomeZone];
        string other = campaign.Territory.Zones[(campaign.Territory.HomeZone + 1) % campaign.Territory.Zones.Count];
        engine.Schedule(new Core.Campaign.CommitmentRequest("caminhada", home, today, Turno.Manha));
        game.AdvanceDay();
        campaign.Events.Pending.Clear();
        today = engine.Today;
        engine.Schedule(new Core.Campaign.CommitmentRequest("caminhada", home, today, Turno.Manha));
        engine.Schedule(new Core.Campaign.CommitmentRequest("caminhada", home, today.AddDays(1), Turno.Manha));
        engine.Schedule(new Core.Campaign.CommitmentRequest("caminhada", other, today.AddDays(1), Turno.Tarde));
        engine.Schedule(new Core.Campaign.CommitmentRequest("entrevista", null, today.AddDays(2), Turno.Noite));
        var leader = engine.City.Leaders.First(l => l.Known);
        campaign.Agenda.Invites.Add(new Core.Campaign.Invite
        {
            Id = "cv_teste",
            FromPersonId = leader.PersonId,
            Text = $"{engine.NameOf(leader.PersonId)} convida você para um encontro em {engine.ZoneName(leader.Zones[0])}",
            ZoneId = leader.Zones[0],
            Date = today.AddDays(3),
            Turno = Turno.Tarde,
            Publico = leader.Publico,
        });
        var rival = campaign.Candidates.First(c => !c.IsPlayer && !c.IsBulk && c.Office == campaign.Player.Office);
        campaign.Agenda.AddFeed(new Core.Campaign.FeedItem(today, "rival_zona", $"{rival.Name} fez campanha em {engine.ZoneName(home)}", home, null, rival.Id));
        hub.Refresh();
    }

    private static T? Named<T>(Node root, string name)
        where T : Node =>
        FindAll<T>(root).FirstOrDefault(n => n.Name == name && IsInstanceValid(n) && !n.IsQueuedForDeletion());

    /// <summary>Escolhe um item de uma lista de escolha pelo começo do texto ou pela posição do id (como o jogador faria no menu).</summary>
    private static bool SelectOption(Node root, string name, string typeId)
    {
        if (Named<OptionButton>(root, name) is not { } options)
        {
            return false;
        }

        string label = Game.Instance.Campaign.Commitments.Tipos.FirstOrDefault(t => t.Id == typeId)?.Nome ?? typeId;
        for (int i = 0; i < options.ItemCount; i++)
        {
            if (options.GetItemText(i).StartsWith(label, StringComparison.Ordinal))
            {
                options.Select(i);
                options.EmitSignal(OptionButton.SignalName.ItemSelected, i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Texto de um controle e dos filhos (rótulos, textos ricos e botões), para conferir o que a tela diz.</summary>
    private static string TextOf(Node root)
    {
        var parts = new List<string>();
        foreach (Node n in FindAll<Node>(root).Prepend(root))
        {
            switch (n)
            {
                case RichTextLabel rich: parts.Add(rich.GetParsedText()); break;
                case Label label: parts.Add(label.Text); break;
                case Button button: parts.Add(button.Text); break;
            }
        }

        return string.Join("\n", parts);
    }

    /// <summary>Texto de cada cartão do feed da coluna direita, do mais recente ao mais antigo.</summary>
    private static List<string> FeedTexts(Node hub) =>
        FindAll<PanelContainer>(hub).Where(p => p.Name.ToString().StartsWith("Feed_", StringComparison.Ordinal) && !p.IsQueuedForDeletion()).Select(TextOf).ToList();

    /// <summary>
    /// Agenda (GDD 17.6), com cliques de mouse: marca três caminhadas iguais clicando nos turnos, confere que a
    /// previsão da terceira mostra o cansaço, tenta um comício fora de época (bloqueado com motivo), desmarca um
    /// compromisso, deixa o tempo correr e confere os cartões de resultado no feed.
    /// </summary>
    private async Task PlayAgenda(string? shot)
    {
        Game game = Game.Instance;
        if (DisplayServer.GetName() == "headless")
        {
            Fail("o teste da agenda clica com o mouse e precisa de janela (rode sem --headless).");
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(MediumCity, 11);
        var campaign = game.State!.Campaign!;
        var engine = game.Engine!;
        game.State.Player.Resources.Money = 20_000;
        hub.Refresh();
        await Frames(2);
        await Click(Named<Button>(hub, "Area_agenda")!);
        if (FindAll<Button>(hub).Count(b => b.Name.ToString().StartsWith("Turno_", StringComparison.Ordinal)) != 21)
        {
            Fail("a Agenda não mostra a grade de 7 dias por 3 turnos.");
            return;
        }

        DateOnly today = engine.Today;
        string home = campaign.Territory.Zones[campaign.Territory.HomeZone];
        for (int i = 0; i < 3; i++)
        {
            await Click(Named<Button>(hub, $"Turno_{today.AddDays(i):yyyyMMdd}_0")!);
            if (hub.SheetName != "Marcar" || Named<Control>(hub, "Previsao") is not { } forecast)
            {
                Fail($"o clique no turno livre do dia {i + 1} não abriu a marcação com a previsão.");
                return;
            }

            string text = TextOf(forecast);
            bool fatigue = Named<Control>(forecast, "Cansaco") is not null;
            if (!text.Contains(Tr("Presença em"), StringComparison.OrdinalIgnoreCase) || !text.Contains(" a +", StringComparison.Ordinal))
            {
                Fail($"a previsão da caminhada {i + 1} não mostra a faixa de presença: {text}");
                return;
            }

            if (fatigue != (i > 0))
            {
                Fail($"caminhada {i + 1}: cansaço {(fatigue ? "mostrado" : "ausente")} na previsão.");
                return;
            }

            if (i == 2)
            {
                var third = engine.Forecast(new Core.Campaign.CommitmentRequest("caminhada", home, today.AddDays(2), Turno.Manha));
                string expected = third.FatigueFactor.ToString("P0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
                string card = TextOf(Named<Control>(forecast, "Cansaco")!);
                if (third.FatigueFactor > 0.5 || !card.Contains("3º", StringComparison.Ordinal) || !card.Contains(expected, StringComparison.Ordinal))
                {
                    Fail($"terceira caminhada igual: fator {third.FatigueFactor:0.00}; a previsão diz \"{card}\".");
                    return;
                }

                GD.Print("  " + card);
                Save(shot, "_previsao_cansaco");
            }

            await Click(Named<Button>(hub, "Confirmar")!);
        }

        if (campaign.Agenda.Scheduled.Count != 3 || campaign.Agenda.Scheduled.Any(c => c.Type != "caminhada" || c.ZoneId != home))
        {
            Fail($"esperava 3 caminhadas marcadas na zona natal; há {campaign.Agenda.Scheduled.Count} compromissos.");
            return;
        }

        // Comício na pré-campanha: bloqueado, com o motivo e o que falta.
        await Click(Named<Button>(hub, $"Turno_{today:yyyyMMdd}_1")!);
        SelectOption(hub, "Tipo", "comicio");
        await Frames(2);
        var blocked = Named<Button>(hub, "Confirmar");
        if (blocked is not { Disabled: true } || !blocked.TooltipText.StartsWith(Tr("Bloqueado:"), StringComparison.Ordinal) || Named<Control>(hub, "Bloqueio") is null)
        {
            Fail("comício na pré-campanha deveria estar bloqueado com o motivo.");
            return;
        }

        GD.Print("  " + blocked.TooltipText.Split('\n')[0]);
        await Click(Named<Button>(hub, "Cancelar")!);

        // Marca um descanso e desmarca pelo detalhe do compromisso.
        await Click(Named<Button>(hub, $"Turno_{today.AddDays(3):yyyyMMdd}_2")!);
        SelectOption(hub, "Tipo", "descanso");
        await Frames(2);
        await Click(Named<Button>(hub, "Confirmar")!);
        await Click(Named<Button>(hub, $"Turno_{today.AddDays(3):yyyyMMdd}_2")!);
        if (hub.SheetName != "Compromisso" || Named<Button>(hub, "Desmarcar") is not { } cancel)
        {
            Fail("o clique no turno marcado não abriu o compromisso com Desmarcar.");
            return;
        }

        await Click(cancel);
        if (campaign.Agenda.Scheduled.Count != 3)
        {
            Fail("Desmarcar não tirou o compromisso da agenda.");
            return;
        }

        Save(shot, "_agenda");

        // O tempo corre: um dia com o relógio de verdade em 4x, o resto dia a dia.
        await Click(FindAll<Button>(hub).First(b => b.Text == "4x"));
        if (!hub.Running)
        {
            Fail($"o tempo não começou a correr em 4x ({hub.BlockReason()}).");
            return;
        }

        for (int waited = 0; waited < 600 && campaign.Agenda.Log.Count == 0; waited++)
        {
            await Frames(1);
        }

        await Click(Named<Button>(hub, "Pausa")!);
        if (hub.Running)
        {
            Fail("o botão de pausa não parou o tempo.");
            return;
        }

        if (campaign.Agenda.Log.Count == 0)
        {
            Fail("em 4x, o primeiro dia não passou e nenhum compromisso aconteceu.");
            return;
        }

        for (int day = 0; day < 4 && campaign.Agenda.Log.Count < 3; day++)
        {
            campaign.Events.Pending.Clear();
            hub.StepDay();
            await Frames(2);
        }

        var results = campaign.Agenda.Log;
        var cards = FeedTexts(hub).Where(t => t.Contains(Tr("Sua agenda"), StringComparison.Ordinal) && t.Contains(Tr("Caminhada"), StringComparison.Ordinal)).ToList();
        if (results.Count != 3 || cards.Count != 3)
        {
            Fail($"esperava 3 resultados e 3 cartões no feed; há {results.Count} resultados e {cards.Count} cartões.");
            return;
        }

        foreach (string card in cards.Take(3).Reverse())
        {
            if (!card.Contains(Tr("de presença em"), StringComparison.Ordinal) || !card.Contains(Tr("Fatores:"), StringComparison.Ordinal))
            {
                Fail($"cartão de resultado sem o que mudou ou sem os fatores: {card}");
                return;
            }

            GD.Print("  " + card.Replace("\n", " | ", StringComparison.Ordinal));
        }

        // Princípio g: só pelo cansaço, a terceira igual rende no máximo metade da primeira (chuva e sorte à parte).
        double fatigueFactor = results[2].Factors.FirstOrDefault(f => f.Id == "cansaco")?.Multiplier ?? 1;
        if (results[0].Factors.Any(f => f.Id == "cansaco") || fatigueFactor > 0.5)
        {
            Fail($"a terceira caminhada igual teve fator de cansaço {fatigueFactor:0.00} (máximo: 0,50) ou a primeira já veio cansada.");
            return;
        }

        if (!cards[0].Contains("3º", StringComparison.Ordinal))
        {
            Fail($"o cartão da terceira caminhada não cita o cansaço: {cards[0]}");
            return;
        }

        Save(shot, "_feed");
        GD.Print($"Smoke agenda: ok (3 caminhadas marcadas por clique, cansaço na terceira: fator {fatigueFactor:0.00}; comício bloqueado com motivo; desmarcar; {cards.Count} cartões de resultado no feed)");
        GetTree().Quit(0);
    }

    /// <summary>
    /// Ficha da zona (GDD 17.3), com cliques de mouse: clica numa zona no mapa, confere a ficha (demanda, presença,
    /// lideranças, cansaço), usa "Marcar compromisso aqui", confirma e confere o marcador no mapa.
    /// </summary>
    private async Task PlayZone(string? shot)
    {
        Game game = Game.Instance;
        if (DisplayServer.GetName() == "headless")
        {
            Fail("o teste da ficha da zona clica no mapa e precisa de janela (rode sem --headless).");
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(MediumCity, 11);
        var campaign = game.State!.Campaign!;
        var map = hub.ZoneMap;
        if (Named<Button>(hub, "Modo_forca") is not { ButtonPressed: true })
        {
            Fail("a tela central não abre no modo Minha força.");
            return;
        }

        // Uma zona cujo centro esteja livre de botões e da legenda.
        string? target = null;
        Rect2 safe = new Rect2(map.GlobalPosition, map.Size).Grow(-120);
        foreach (var zona in map.Zonas.Values.OrderByDescending(z => z.Populacao))
        {
            Vector2 at = map.ScreenCenterOf(zona.Id);
            if (map.Hit(at) == zona.Id && safe.HasPoint(map.GlobalPosition + at) && at.Y < map.Size.Y - 420)
            {
                target = zona.Id;
                break;
            }
        }

        if (target is null)
        {
            Fail("nenhuma zona com o centro clicável no enquadramento inicial do mapa.");
            return;
        }

        await ClickAt(map.GlobalPosition + map.ScreenCenterOf(target));
        if (hub.SheetName != "Zona" || map.Selected != target)
        {
            Fail($"o clique na zona não abriu a ficha (ficha: {hub.SheetName}, selecionada: {map.Selected}).");
            return;
        }

        string sheet = TextOf(hub);
        foreach (string expected in new[] { "Demanda da zona", "Presença aqui (0 a 100)", "Lideranças", "O que você já fez aqui", "Cansaço", "Marcar compromisso aqui" })
        {
            if (!sheet.Contains(Tr(expected), StringComparison.Ordinal))
            {
                Fail($"a ficha da zona não mostra \"{expected}\".");
                return;
            }
        }

        Save(shot, "_ficha_zona");
        await Click(Named<Button>(hub, "MarcarAqui")!);
        var zoneOptions = Named<OptionButton>(hub, "Zona");
        string zoneName = game.Engine!.ZoneName(target);
        if (hub.SheetName != "Marcar" || zoneOptions is null || !zoneOptions.GetItemText(zoneOptions.Selected).StartsWith(zoneName, StringComparison.Ordinal))
        {
            Fail("\"Marcar compromisso aqui\" não abriu a marcação com a zona escolhida.");
            return;
        }

        int markersBefore = map.Markers.Count(m => m.ZoneId == target);
        await Click(Named<Button>(hub, "Confirmar")!);
        if (campaign.Agenda.Scheduled.SingleOrDefault() is not { } marked || marked.ZoneId != target || hub.SheetName != "Zona")
        {
            Fail("a marcação pela ficha não gravou o compromisso na zona ou não voltou à ficha.");
            return;
        }

        if (map.Markers.Count(m => m.ZoneId == target) != markersBefore + 1)
        {
            Fail("o compromisso marcado não virou marcador no mapa.");
            return;
        }

        // O compromisso acontece e entra em "O que você já fez aqui"; o cansaço aparece na ficha.
        for (int day = 0; day < 3 && campaign.Agenda.Log.Count == 0; day++)
        {
            campaign.Events.Pending.Clear();
            hub.StepDay();
            await Frames(2);
        }

        if (campaign.Agenda.Log.Count == 0)
        {
            Fail("o compromisso marcado pela ficha da zona não aconteceu quando o dia passou.");
            return;
        }

        sheet = TextOf(hub);
        if (!sheet.Contains(Tr("o próximo igual rende"), StringComparison.Ordinal) || sheet.Contains(Tr("Nada ainda."), StringComparison.Ordinal))
        {
            Fail("depois da caminhada, a ficha não mostra o que foi feito ou o cansaço.");
            return;
        }

        // Clicar no cartão do feed centraliza o mapa na zona.
        await Click(Named<Button>(hub, "Voltar")!);
        var feedCard = FindAll<PanelContainer>(hub).First(p => p.Name.ToString().StartsWith("Feed_", StringComparison.Ordinal) && !p.IsQueuedForDeletion() && TextOf(p).Contains(Tr("Sua agenda"), StringComparison.Ordinal));
        await ClickCard(feedCard);
        Vector2 centered = map.ScreenCenterOf(target);
        if (centered.DistanceTo(map.Size / 2) > 4 || map.Selected != target)
        {
            Fail($"o clique no cartão do feed não centralizou o mapa na zona (centro em {centered}, mapa {map.Size}).");
            return;
        }

        Save(shot, "_zona");
        GD.Print($"Smoke zona: ok (clique no mapa abriu a ficha de {zoneName}; Marcar compromisso aqui gravou {marked.Type}; marcador no mapa; resultado e cansaço na ficha; feed centraliza o mapa)");
        GetTree().Quit(0);
    }

    /// <summary>
    /// Lideranças (GDD 17.10), com cliques de mouse: abre a ficha de uma liderança, marca a visita, deixa o dia correr,
    /// responde ao pedido aceitando, confere a lembrança na ficha e a promessa no Livro pela Enciclopédia.
    /// </summary>
    private async Task PlayLeader(string? shot)
    {
        Game game = Game.Instance;
        if (DisplayServer.GetName() == "headless")
        {
            Fail("o teste das lideranças clica com o mouse e precisa de janela (rode sem --headless).");
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(MediumCity, 11);
        var campaign = game.State!.Campaign!;
        var engine = game.Engine!;
        var leader = engine.City.Leaders.FirstOrDefault(l => l.Known && game.Campaign.Leaders.Request(l.RequestId).Kind != Core.Campaign.LeaderRequestDefinition.Dinheiro);
        if (leader is null)
        {
            Fail("nenhuma liderança conhecida com pedido que vira promessa nesta cidade.");
            return;
        }

        string name = engine.NameOf(leader.PersonId);
        await Click(Named<Button>(hub, "Area_pessoas")!);
        string list = TextOf(hub);
        if (!list.Contains($"{name} ({game.Campaign.Leaders.Type(leader.Type).Papel})", StringComparison.Ordinal))
        {
            Fail("a área Pessoas não mostra a liderança com o que ela é.");
            return;
        }

        await ClickCard(Named<Control>(hub, "Lideranca_" + leader.PersonId)!);
        if (hub.SheetName != "Pessoa")
        {
            Fail("o clique na liderança não abriu a ficha da pessoa.");
            return;
        }

        Save(shot, "_ficha_pessoa");
        await Click(Named<Button>(hub, "MarcarVisita")!);
        if (hub.SheetName != "Marcar" || !TextOf(Named<Control>(hub, "Previsao")!).Contains(name, StringComparison.Ordinal))
        {
            Fail("Marcar visita não abriu a marcação com a previsão de relação com a liderança.");
            return;
        }

        await Click(Named<Button>(hub, "Confirmar")!);
        if (campaign.Agenda.Scheduled.SingleOrDefault() is not { Type: "visita" } visit || visit.PersonId != leader.PersonId)
        {
            Fail("a visita não foi marcada.");
            return;
        }

        // O dia corre e o pedido chega: jogo pausado, faixa de decisão e cartão sobre o mapa.
        hub.StepDay();
        await Frames(3);
        if (campaign.Agenda.PendingRequest != leader.PersonId || !hub.RequestOpen || hub.Running || Named<Control>(hub, "FaixaDecisao") is null || engine.Today != visit.Date)
        {
            Fail("a visita não trouxe o pedido com o jogo pausado, a faixa de decisão e o cartão.");
            return;
        }

        if (FindAll<Button>(hub).First(b => b.Text == "4x") is not { Disabled: true } speed || !speed.TooltipText.StartsWith(Tr("Bloqueado:"), StringComparison.Ordinal))
        {
            Fail("com o pedido aberto, a velocidade deveria estar bloqueada com o motivo.");
            return;
        }

        var card = Named<Control>(hub, "CartaoPedido")!;
        string cardText = TextOf(card);
        if (FindAll<Button>(card).Count(b => b.Name.ToString().StartsWith("Resposta_", StringComparison.Ordinal)) != 4 || !cardText.Contains(Tr("Quem vai lembrar:"), StringComparison.Ordinal) || !cardText.Contains("Livro de compromissos", StringComparison.Ordinal))
        {
            Fail("o cartão do pedido não mostra as quatro respostas com a previsão e quem vai lembrar.");
            return;
        }

        Save(shot, "_pedido");
        await Click(Named<Button>(hub, "Resposta_Aceitar")!);
        var promise = game.State.Ledger.Promises.SingleOrDefault();
        string changed = TextOf(Named<Control>(hub, "CartaoPedido")!);
        if (promise is null || promise.ToId != leader.PersonId || !changed.Contains(Tr("O que mudou"), StringComparison.Ordinal) || !changed.Contains(promise.Text, StringComparison.Ordinal))
        {
            Fail("aceitar o pedido não gravou a promessa no Livro ou o cartão não mostrou o que mudou.");
            return;
        }

        GD.Print("  " + changed.Replace("\n", " | ", StringComparison.Ordinal));
        await Click(Named<Button>(hub, "Continuar")!);
        if (hub.RequestOpen || hub.BlockReason() is not null)
        {
            Fail("depois de Continuar, o cartão do pedido ficou na tela.");
            return;
        }

        // A lembrança está na ficha da pessoa.
        await Click(Named<Button>(hub, "Area_pessoas")!);
        await ClickCard(Named<Control>(hub, "Lideranca_" + leader.PersonId)!);
        string memory = game.Campaign.Leaders.Texto("promessa");
        if (!TextOf(hub).Contains(memory, StringComparison.Ordinal))
        {
            Fail($"a ficha da pessoa não mostra a lembrança \"{memory}\".");
            return;
        }

        // A promessa está no Livro de compromissos, na Enciclopédia (F1 → Sua carreira).
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F1, PhysicalKeycode = Key.F1, Pressed = true });
        await Frames(4);
        Node? encyclopedia = GetTree().Root.FindChild("Enciclopedia", recursive: true, owned: false);
        if (encyclopedia is null)
        {
            Fail("F1 não abriu a Enciclopédia.");
            return;
        }

        await Click(FindAll<Button>(encyclopedia).First(b => b.Text == Tr("Sua carreira")));
        string book = TextOf(encyclopedia);
        if (!book.Contains(promise.Text, StringComparison.Ordinal) || !book.Contains(promise.Deadline.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), StringComparison.Ordinal) || !book.Contains(name, StringComparison.Ordinal))
        {
            Fail("o Livro de compromissos na Enciclopédia não mostra a promessa com a quem, o quê e o prazo.");
            return;
        }

        Save(shot, "_livro");
        GD.Print($"Smoke lideranca: ok (visita a {name} marcada pela ficha; pedido respondido com Aceitar; lembrança na ficha; promessa \"{promise.Text}\" no Livro até {promise.Deadline:dd/MM/yyyy})");
        GetTree().Quit(0);
    }

    /// <summary>Rola o painel até o controle aparecer, como o jogador faria antes de clicar.</summary>
    private async Task Reveal(Control control)
    {
        for (Node? n = control.GetParent(); n is not null; n = n.GetParent())
        {
            if (n is ScrollContainer scroll)
            {
                scroll.EnsureControlVisible(control);
                await Frames(1);
                return;
            }
        }
    }

    /// <summary>Clique no canto de um cartão clicável, fora do texto (um termo sublinhado abriria o verbete).</summary>
    private async Task ClickCard(Control card)
    {
        await Reveal(card);
        await ClickAt(card.GlobalPosition + new Vector2(30, 30));
    }

    /// <summary>Clique de mouse de verdade num ponto da janela (coordenadas do viewport).</summary>
    private async Task ClickAt(Vector2 point)
    {
        Viewport viewport = GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, inLocalCoords: true);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point }, inLocalCoords: true);
        await Frames(1);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, inLocalCoords: true);
        await Frames(2);
    }

    private void Fail(string message)
    {
        GD.PushError("Smoke: " + message);
        GetTree().Quit(1);
    }

    /// <summary>Clique de mouse de verdade no centro do controle (passa pelo roteamento de entrada da interface).</summary>
    private async Task Click(Control control)
    {
        // Direto na janela: sem janela de verdade (headless), Input.ParseInputEvent não chega à interface.
        await Reveal(control);
        Vector2 center = control.GetGlobalRect().GetCenter();
        Viewport viewport = GetViewport();
        // Coordenadas do próprio viewport: com a janela num tamanho diferente da tela base, as da janela erram o alvo.
        viewport.PushInput(new InputEventMouseMotion { Position = center, GlobalPosition = center }, inLocalCoords: true);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = center, GlobalPosition = center }, inLocalCoords: true);
        await Frames(1);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = center, GlobalPosition = center }, inLocalCoords: true);
        await Frames(2);
    }

    private static IEnumerable<T> FindAll<T>(Node root)
        where T : Node
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (T inner in FindAll<T>(child))
            {
                yield return inner;
            }
        }
    }

    /// <summary>
    /// Prefeito numa cidade média: candidatura, debates de TV pela tela do debate, eleição, posse, orçamento,
    /// políticas, secretaria, crise e save/load.
    /// </summary>
    private async Task PlayMayor(string? shot)
    {
        Game game = Game.Instance;
        var world = game.PendingWorld!;
        var city = game.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == "medio").OrderBy(m => m.CodigoIbge).Skip(3).First();
        var parties = Core.Campaign.CampaignContext.ElectoralParties(world.Parties.Select(GameParty.From), game.World.Parties.Generator.SizeShareWeights);
        var local = world.Preset.Scenario == "brasil_2026" ? game.Brasil2026?.Municipio(city.CodigoIbge) : null;
        string party = Core.Campaign.CampaignContext.PartyStrength(game.PendingSeed, city.CodigoIbge, game.Electorate, parties, local).MaxBy(x => x.Value).Key;
        var player = Core.Characters.PersonGenerator.Generate(new Rng(7), game.People, new Core.Characters.PersonRequest(Core.Campaign.RaceSetup.PlayerId, city.Uf, city.CodigoIbge, new DateOnly(world.Options.StartYear, 1, 1), Age: 40, PartyId: party));
        foreach (var attribute in Enum.GetValues<Core.Characters.AttributeId>())
        {
            player.Attributes.Set(attribute, 70);
        }

        game.StartCareer(NewGame.Create(world, player, game.PendingSeed));
        if (game.SwitchCandidacy(Core.Elections.Office.Prefeito).Issue is { } issue)
        {
            GD.PushError($"Smoke: candidatura a prefeito recusada ({issue}).");
            GetTree().Quit(1);
            return;
        }

        var campaign = game.State!.Campaign!;
        int debates = 0;
        while (campaign.Phase != Core.Campaign.CampaignPhase.Votacao)
        {
            while (campaign.Events.Pending.Count > 0)
            {
                var pending = campaign.Events.Pending[0];
                var definition = game.Events.Find(pending.EventId)!;
                Core.Events.EventEngine.Resolve(game.Engine!.Context, pending, Enumerable.Range(0, definition.Choices.Count).First(i => Core.Events.EventEngine.Blocked(game.Engine.Context, definition.Choices[i]) is null), game.State.Rng);
            }

            if (campaign.Debate is { } debate)
            {
                debates++;
                var screen = GD.Load<PackedScene>(Game.Scenes.Debate).Instantiate();
                AddChild(screen);
                await Frames(3);
                while (!debate.Finished)
                {
                    string card = debate.Hand[0];
                    Core.Debate.DebateEngine.Play(game.Engine!.Context, game.Debate, card, game.Debate.Card(card).Targeted ? debate.Participants[1] : null, game.State.Rng);
                }

                screen.QueueFree();
                screen = GD.Load<PackedScene>(Game.Scenes.Debate).Instantiate();
                AddChild(screen);
                await Frames(3);
                if (shot is not null && debates == 1)
                {
                    GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", "_debate.png"));
                }

                screen.QueueFree();
                Core.Debate.DebateEngine.Close(campaign);
            }

            game.AdvanceWeek();
        }

        if (debates != game.Debate.WeeksBeforeElection.Count)
        {
            GD.PushError($"Smoke: {debates} debates em vez de {game.Debate.WeeksBeforeElection.Count}.");
            GetTree().Quit(1);
            return;
        }

        // Só neste teste: garante a eleição para exercitar a prefeitura.
        Array.Fill(campaign.Territory.Presence[Core.Campaign.RaceSetup.PlayerId], 100);
        campaign.Player.Recognition = 100;
        campaign.Player.Image = 1;
        game.Engine!.CastPlayerVote(new Core.Campaign.UrnaVote(Core.Elections.Office.Prefeito, campaign.Player.Number, false));
        game.Engine.ResolveElection();
        var night = GD.Load<PackedScene>(Game.Scenes.ElectionNight).Instantiate();
        AddChild(night);
        await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
        night.QueueFree();
        if (!campaign.Outcome!.PlayerElected)
        {
            GD.PushError("Smoke: o jogador não se elegeu prefeito nem com reconhecimento máximo.");
            GetTree().Quit(1);
            return;
        }

        game.ConcludeElection();
        game.GoToCareer();
        if (game.LastNavigation != Game.Scenes.Executive || game.Prefeitura is null)
        {
            GD.PushError("Smoke: a posse não levou à prefeitura.");
            GetTree().Quit(1);
            return;
        }

        var engine = game.Prefeitura;
        var decree = game.ExecutiveContent.Policies.Policies.First(p => !p.RequiresLaw && p.Cost > 0 && p.Cost <= engine.FreeReserve);
        engine.Enact(decree.Id);
        var law = game.ExecutiveContent.Policies.Policies.First(p => p.RequiresLaw && p.Cost <= engine.FreeReserve);
        engine.Enact(law.Id);
        if (game.State.Executive!.Chamber.Select(m => m.PartyId).FirstOrDefault(p => p is not null && !game.State.Executive.InBase(p)) is { } outsider)
        {
            engine.GiveSecretaria(outsider);
        }

        for (int week = 0; week < 20; week++)
        {
            if (game.State.Executive!.Crisis is not null)
            {
                engine.ResolveCrisis(0);
            }

            game.AdvanceWeek();
        }

        var executive = GD.Load<PackedScene>(Game.Scenes.Executive).Instantiate();
        AddChild(executive);
        await Frames(3);
        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", "_prefeitura.png"));
        }

        executive.QueueFree();
        game.State.Executive!.Crisis = new Core.Executive.PendingCrisis("enchente", game.State.Calendar.WeekStart);
        executive = GD.Load<PackedScene>(Game.Scenes.Executive).Instantiate();
        AddChild(executive);
        await Frames(3);
        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", "_crise.png"));
        }

        executive.QueueFree();
        engine.ResolveCrisis(0);
        SaveManager.Instance.Save("smoke_prefeitura");
        if (SaveManager.Instance.Load("smoke_prefeitura") is not null || game.Prefeitura is null || game.State!.Executive is null)
        {
            GD.PushError("Smoke: save/load da prefeitura falhou.");
            GetTree().Quit(1);
            return;
        }

        GD.Print($"Smoke prefeitura: ok ({debates} debates, aprovação {game.State.Executive.Approval:0}%, {game.State.Executive.Policies.Count} políticas)");
        GetTree().Quit(0);
    }

    /// <summary>
    /// Carreira depois da urna, pela UI: posse, semanas de mandato com votos e ações, fundação de partido,
    /// save/load, troca de partido fora da janela (perde o mandato), entressafra e abertura da próxima campanha.
    /// </summary>
    private async Task PlayMandate(string? shot)
    {
        Game game = Game.Instance;
        var creator = (CharacterCreatorScreen)GD.Load<PackedScene>(Game.Scenes.CharacterCreator).Instantiate();
        AddChild(creator);
        await Frames(3);
        creator.ChooseStrongestPartyForTest();
        creator.TryStartCareer();
        RemoveChild(creator);
        creator.QueueFree();
        var engine = game.Engine!;
        var campaign = game.State!.Campaign!;
        while (campaign.Phase != Core.Campaign.CampaignPhase.Votacao)
        {
            while (campaign.Events.Pending.Count > 0)
            {
                var pending = campaign.Events.Pending[0];
                var definition = game.Events.Find(pending.EventId)!;
                Core.Events.EventEngine.Resolve(engine.Context, pending, Enumerable.Range(0, definition.Choices.Count).First(i => Core.Events.EventEngine.Blocked(engine.Context, definition.Choices[i]) is null), game.State.Rng);
            }

            game.AdvanceWeek();
        }

        // Só neste teste: garante a eleição para exercitar o mandato.
        Array.Fill(campaign.Territory.Presence[Core.Campaign.RaceSetup.PlayerId], 100);
        campaign.Player.Recognition = 100;
        campaign.Player.Image = 1;
        engine.CastPlayerVote(new Core.Campaign.UrnaVote(Core.Elections.Office.Vereador, campaign.Player.Number, false));
        engine.ResolveElection();
        var night = GD.Load<PackedScene>(Game.Scenes.ElectionNight).Instantiate();
        AddChild(night);
        await Frames(3);
        if (!campaign.Outcome!.PlayerElected)
        {
            GD.PushError("Smoke: o jogador não se elegeu nem com reconhecimento máximo.");
            GetTree().Quit(1);
            return;
        }

        game.ConcludeElection();
        game.GoToCareer();
        night.QueueFree();
        if (game.LastNavigation != Game.Scenes.Mandate || game.Mandate is null)
        {
            GD.PushError("Smoke: a posse não levou à tela do mandato.");
            GetTree().Quit(1);
            return;
        }

        Node? screen = null;
        int voted = 0;
        for (int week = 0; week < 30; week++)
        {
            foreach (var bill in game.Mandate!.Pauta.ToList())
            {
                game.Mandate.CastVote(bill.Id, week % 3 == 0 ? Core.Legislature.VoteChoice.Nao : Core.Legislature.VoteChoice.Sim);
                voted++;
            }

            if (week % 10 == 9)
            {
                screen?.QueueFree();
                screen = GD.Load<PackedScene>(Game.Scenes.Mandate).Instantiate();
                AddChild(screen);
                await Frames(3);
                if (shot is not null && week == 19)
                {
                    GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", "_mandato.png"));
                }
            }

            game.Mandate.Execute("tribuna", null);
            if (week == 5)
            {
                game.Mandate.Execute("propor_projeto", game.Mandate.ProposableBills().First().Id);
            }

            game.AdvanceWeek();
        }

        screen?.QueueFree();
        if (voted == 0 || game.State!.Mandate!.Bills.Count(b => b.Decided is not null) == 0)
        {
            GD.PushError("Smoke: nenhuma votação em 30 semanas de mandato.");
            GetTree().Quit(1);
            return;
        }

        // Fundação de partido pelo diálogo.
        game.State.Player.Resources.PoliticalCapital = 50;
        game.State.Player.Resources.Money = 100_000;
        bool closed = false;
        var dialog = new Widgets.FoundingDialog(() => closed = true);
        AddChild(dialog);
        await Frames(3);
        var draft = game.FoundingEngine!.Propose(new Rng(5), game.State.Parties[0].FamilyId);
        if (game.FoundingEngine.Start(draft) is { } failure)
        {
            GD.PushError($"Smoke: a fundação não começou ({failure}).");
            GetTree().Quit(1);
            return;
        }

        dialog.QueueFree();
        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.Size = GetViewport().GetVisibleRect().Size;
        dialog = new Widgets.FoundingDialog(() => closed = true);
        center.AddChild(dialog);
        await Frames(3);
        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", "_fundacao.png"));
        }

        center.QueueFree();
        SaveManager.Instance.Save("smoke_mandato");
        if (SaveManager.Instance.Load("smoke_mandato") is { } error || game.Mandate is null || game.State!.Founding is null)
        {
            GD.PushError("Smoke: save/load do mandato falhou.");
            GetTree().Quit(1);
            return;
        }

        // Troca de partido fora da janela: o mandato acaba e a carreira vai para a entressafra.
        game.JoinParty(game.State.Parties.First(p => p.Id != game.State.Player.PartyId).Id);
        game.GoToCareer();
        if (game.State.Mandate is not null || game.LastNavigation != Game.Scenes.OutOfOffice)
        {
            GD.PushError("Smoke: trocar de partido fora da janela não encerrou o mandato.");
            GetTree().Quit(1);
            return;
        }

        var outOfOffice = GD.Load<PackedScene>(Game.Scenes.OutOfOffice).Instantiate();
        AddChild(outOfOffice);
        await Frames(3);
        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", "_entressafra.png"));
        }

        outOfOffice.QueueFree();
        Game.WeekNote note = game.SkipToNextElection();
        if (note != Game.WeekNote.CampanhaAberta || game.Engine is null)
        {
            GD.PushError($"Smoke: a próxima campanha não abriu ({note}).");
            GetTree().Quit(1);
            return;
        }

        AddChild(GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate());
        await Frames(3);
        GD.Print($"Smoke mandate: ok ({voted} votos, {game.State.Calendar.WeekStart:yyyy-MM-dd}, fundação {game.State.Founding?.Stage}, aberto {closed})");
        GetTree().Quit(0);
    }

    /// <summary>Joga uma campanha inteira pela UI: hub, eventos, urna com teclas simuladas, apuração e save.</summary>
    private async Task PlayCampaign(string? shot)
    {
        Game game = Game.Instance;
        var creator = (CharacterCreatorScreen)GD.Load<PackedScene>(Game.Scenes.CharacterCreator).Instantiate();
        AddChild(creator);
        await Frames(3);
        creator.TryStartCareer();
        RemoveChild(creator);
        creator.QueueFree();
        var engine = game.Engine!;
        var campaign = game.State!.Campaign!;
        var policy = new Rng(99);

        // Seis semanas dia a dia pela tela central: compromissos que acontecem, visitas com o pedido respondido pelo
        // cartão (as quatro respostas, em rodízio) e as pausas automáticas com a faixa de decisão.
        UI.Settings.ResetForTest();
        var daily = (HubScreen)GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate();
        AddChild(daily);
        await Frames(2);
        int requests = 0, notices = 0;
        string[] answers = ["Aceitar", "Negociar", "NaoSeComprometer", "Recusar"];
        game.State.Player.Resources.Money = 30_000;
        for (int step = 0; step < 46; step++)
        {
            campaign.Events.Pending.Clear();
            if (game.State.Time.Day == 0)
            {
                DateOnly start = game.State.Calendar.WeekStart;
                engine.Schedule(new Core.Campaign.CommitmentRequest("caminhada", policy.Pick(campaign.Territory.Zones), start, Turno.Manha));
                engine.Schedule(new Core.Campaign.CommitmentRequest("gravacao", null, start.AddDays(1), Turno.Tarde));
                if (engine.City.Leaders.FirstOrDefault(l => l.Known && !l.RequestMet) is { } target)
                {
                    engine.Schedule(new Core.Campaign.CommitmentRequest("visita", target.Zones[0], start.AddDays(3), Turno.Manha, PersonId: target.PersonId));
                }
            }

            daily.StepDay();
            await Frames(1);
            if (daily.NoticeText is not null)
            {
                notices++;
                if (Named<Control>(daily, "FaixaDecisao") is null || daily.Running)
                {
                    Fail($"pausa automática sem a faixa de decisão ou com o tempo correndo: {daily.NoticeText}");
                    return;
                }

                FindAll<Button>(daily).First(b => b.Text == "1x").EmitSignal(BaseButton.SignalName.Pressed);
                FindAll<Button>(daily).First(b => b.Name == "Pausa").EmitSignal(BaseButton.SignalName.Pressed);
            }

            if (campaign.Agenda.PendingRequest is not null)
            {
                if (!daily.RequestOpen || Named<Button>(daily, "Resposta_" + answers[requests % answers.Length]) is not { } answer)
                {
                    Fail("pedido de liderança pendente sem o cartão com as respostas.");
                    return;
                }

                answer.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(1);
                Named<Button>(daily, "Continuar")!.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(1);
                requests++;
            }
        }

        if (requests < 2 || notices == 0 || campaign.Agenda.Log.Count < 12)
        {
            Fail($"em seis semanas dia a dia: {requests} pedidos respondidos, {notices} pausas com aviso, {campaign.Agenda.Log.Count} compromissos concluídos.");
            return;
        }

        GD.Print($"  dia a dia: {campaign.Agenda.Log.Count} compromissos concluídos, {requests} pedidos respondidos, {notices} pausas com aviso; Livro: {game.State.Ledger.Promises.Count} promessas, {game.State.Ledger.Debts.Count} dívidas, {game.State.Ledger.Skeletons.Count} esqueletos");
        RemoveChild(daily);
        daily.QueueFree();
        campaign.Agenda.Scheduled.Clear();
        while (game.State.Time.Day != 0)
        {
            campaign.Events.Pending.Clear();
            game.AdvanceDay();
        }

        campaign.Events.Pending.Clear();
        Node? hub = null;
        int week = 0;
        while (campaign.Phase != Core.Campaign.CampaignPhase.Votacao)
        {
            if (week++ % 6 == 0)
            {
                hub?.QueueFree();
                hub = GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate();
                AddChild(hub);
                await Frames(2);
                if (shot is not null && campaign.Phase == Core.Campaign.CampaignPhase.Campanha && campaign.Events.Pending.Count == 0)
                {
                    GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", "_hub.png"));
                }
            }

            while (campaign.Events.Pending.Count > 0)
            {
                var pending = campaign.Events.Pending[0];
                var definition = game.Events.Find(pending.EventId)!;
                int choice = Enumerable.Range(0, definition.Choices.Count).First(i => Core.Events.EventEngine.Blocked(engine.Context, definition.Choices[i]) is null);
                Core.Events.EventEngine.Resolve(engine.Context, pending, choice, game.State.Rng);
            }

            // Uma caminhada por dia útil numa zona sorteada e uma entrevista na quarta à tarde.
            DateOnly monday = game.State.Calendar.WeekStart;
            for (int day = 0; day < 5; day++)
            {
                engine.Schedule(new Core.Campaign.CommitmentRequest("caminhada", policy.Pick(campaign.Territory.Zones), monday.AddDays(day), Turno.Manha));
            }

            engine.Schedule(new Core.Campaign.CommitmentRequest("entrevista", null, monday.AddDays(2), Turno.Tarde));
            engine.AdvanceWeek();
        }

        hub?.QueueFree();
        var urna = GD.Load<PackedScene>(Game.Scenes.Urna).Instantiate();
        AddChild(urna);
        await Frames(3);
        foreach (char digit in campaign.Player.Number)
        {
            Press((Key)((int)Key.Key0 + (digit - '0')));
        }

        await Frames(2);
        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", "_urna.png"));
        }

        Press(Key.Enter);
        foreach (char digit in campaign.ForOffice(Core.Elections.Office.Prefeito).First().Number)
        {
            Press((Key)((int)Key.Key0 + (digit - '0')));
        }

        Press(Key.Enter);
        await Frames(4);
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
        if (campaign.Outcome is null || game.LastNavigation != Game.Scenes.ElectionNight || campaign.PlayerVotes.Count != 2)
        {
            GD.PushError("Smoke: a urna não registrou os dois votos e a apuração.");
            GetTree().Quit(1);
            return;
        }

        urna.QueueFree();
        var night = GD.Load<PackedScene>(Game.Scenes.ElectionNight).Instantiate();
        AddChild(night);
        await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot.Replace(".png", "_apuracao.png"));
        }

        SaveManager.Instance.Save("smoke");
        string? error = SaveManager.Instance.Load("smoke");
        if (error is not null || game.State!.Campaign?.Outcome is null)
        {
            GD.PushError("Smoke: save/load falhou: " + error);
            GetTree().Quit(1);
            return;
        }

        GD.Print($"Smoke campaign: ok ({week} semanas, eleito: {game.State.Campaign.Outcome.PlayerElected})");
        GetTree().Quit(0);
    }

    private static void Press(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
    }

    private async Task Frames(int n)
    {
        for (int i = 0; i < n; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }
}
