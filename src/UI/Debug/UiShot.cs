using Godot;
using Planalto.Autoload;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Teste de fumaça e captura das telas: "-- --target=perfil|newworld|creator|creator17|hub|explicacao|picker|campaign|map|mandate|prefeitura|eventos|layout|agenda|zona|lideranca|equipe|verba|plataforma|objetivos|rivais|midia|contas|mapa_pequeno|gabinete|obras|fiscal|indicadores|entressafra [--perfil=nome] [--screenshot=arquivo.png]".
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

        // O ponteiro do mouse de verdade pode estar parado sobre a janela do teste: sem isto, o tooltip que ele abre
        // entra na frente dos cliques simulados e nas medidas do layout. Os testes leem o texto do tooltip direto do controle.
        ProjectSettings.SetSetting("gui/timers/tooltip_delay_sec", 100000.0);
        game.PendingSeed = SmokeSeed;
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

        if (target == "gabinete")
        {
            await PlayCabinet(shot);
            return;
        }

        if (target == "obras")
        {
            await PlayWorks(shot);
            return;
        }

        if (target == "fiscal")
        {
            await PlayFiscal(shot);
            return;
        }

        if (target == "indicadores")
        {
            await PlayIndicators(shot);
            return;
        }

        if (target == "entressafra")
        {
            await PlayOffSeason(shot);
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

        if (target == "mapa_pequeno")
        {
            await PlayMapaPequeno(shot);
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
                if (!await CheckCreatorSeventeen(creator, shot))
                {
                    return;
                }
            }

            if (target == "creator" && !(await CheckCreatorColors(creator) && await CheckCreatorGender(creator) && await CheckCreatorBallot(creator, shot) && await CheckCreatorParty(creator)))
            {
                return;
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
                if (!await CheckImageStat(hub))
                {
                    return;
                }

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
    /// Nenhum controle cortado. Primeiro a janela real, como o jogo abre: com a moldura, dentro da área útil do
    /// monitor (a tela menos a barra de tarefas), e com os botões do Novo Mundo e da criação dentro dela. Depois, em
    /// 1920x1080 e 1366x768, abre a criação de personagem (todas as abas), o Novo Mundo e o hub, tira print e falha
    /// se algum controle visível passar dos limites da janela.
    /// </summary>
    private async Task CheckLayout(string? shot)
    {
        Game game = Game.Instance;
        var problems = new List<string>();
        await Frames(6);
        if (UI.WindowFit.HasWindow)
        {
            GD.Print($"  janela ao abrir: modo {DisplayServer.WindowGetMode()}, com moldura {UI.WindowFit.Decorated}, área de desenho {DisplayServer.WindowGetSize()}, área útil do monitor {UI.WindowFit.Usable}");
            if (UI.WindowFit.Problem() is { } misfit)
            {
                problems.Add($"Janela ao abrir: {misfit}");
            }

            await CheckRealWindow(problems, shot);
        }

        int govPanels = 0;
        ClearGovSaves();
        foreach (Vector2I size in new[] { new Vector2I(1920, 1080), new Vector2I(1366, 768) })
        {
            bool framed = UI.WindowFit.ExactForCapture(size);
            GetTree().Root.Size = size;
            await Frames(6);
            string tag = $"{size.X}x{size.Y}";
            if (framed && UI.WindowFit.Problem() is { } outside)
            {
                problems.Add($"Janela de {tag}: {outside}");
            }
            else if (!framed)
            {
                GD.Print($"  janela de {tag}: com a moldura não cabe na área útil deste monitor ({UI.WindowFit.Usable.Size}); conferida sem moldura, só a área de desenho");
            }

            game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(2028)).Setup;
            var world = GD.Load<PackedScene>(Game.Scenes.NewWorld).Instantiate<Control>();
            AddChild(world);
            await Frames(5);
            problems.AddRange(Overflow(world, $"Novo Mundo {tag}"));
            Save(shot, $"_novo_mundo_{tag}");
            RemoveChild(world);
            world.QueueFree();

            game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(2028)).Setup;
            game.PendingSeed = SmokeSeed;
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

            // Opções abertas sobre a tela central (P-03).
            Widgets.OptionsDialog.Open(GetTree().Root);
            await Frames(4);
            problems.AddRange(Overflow(GetTree().Root.FindChild("Opcoes", recursive: true, owned: false)!, $"Opções sobre a tela central, {tag}"));
            Save(shot, $"_rfinal_opcoes_central_{tag}");
            GetTree().Root.FindChild("OpcoesCamada", recursive: true, owned: false)?.QueueFree();
            await Frames(2);
            RemoveChild(first);
            first.QueueFree();

            // Telas fora da tela central (P-02): menu, Opções, Novo Mundo, criação, seletor, mapa do estado, debate, urna e apuração.
            await VisitOutsideScreens((root, where, slug) =>
            {
                problems.AddRange(Overflow(root, $"{where}, {tag}"));
                Save(shot, $"_rfinal_{slug}_{tag}");
            });

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

            // Telas da R4: mandato de vereador, entressafra e prefeitura na tela central, em cidade pequena e metrópole.
            foreach (var (code, label) in new[] { (SmallCity, "pequena"), (Metropolis, "metropole") })
            {
                bool visited = await VisitGovScreens(code, label, (hub, where, suffix) =>
                {
                    problems.AddRange(Overflow(hub, $"{where}, {tag}"));
                    if (hub.LeftPanelWidth > 401)
                    {
                        problems.Add($"{where}, {tag}: o painel da área mede {hub.LeftPanelWidth:0} de largura (deveria ter 400): algum conteúdo não quebra a linha");
                    }

                    if (hub.RightPanelWidth > 401)
                    {
                        problems.Add($"{where}, {tag}: a coluna direita mede {hub.RightPanelWidth:0} de largura (deveria ter 400): algum conteúdo não quebra a linha");
                    }

                    if (Named<Control>(hub, "CartaoMandato") is { } govCard && govCard.Size.Y > 640 + 1)
                    {
                        problems.Add($"{where}, {tag}: o cartão de decisão do mandato mede {govCard.Size.Y:0} de altura (máximo 640)");
                    }

                    if (suffix is not null)
                    {
                        Save(shot, $"_r4_{suffix}_{label}_{tag}");
                    }

                    govPanels++;
                    return Task.CompletedTask;
                });
                if (!visited)
                {
                    return;
                }
            }
        }

        if (govPanels < 240)
        {
            Fail($"o teste de layout só passou por {govPanels} telas da R4 (esperado: 240 ou mais).");
            return;
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

        GD.Print("Smoke layout: ok (janela real dentro da área útil do monitor, com os botões do Novo Mundo e da criação; 1920x1080 e 1366x768: criação em todas as abas, Novo Mundo e a tela central em cidade pequena, média e metrópole, com as 7 áreas, 3 modos do mapa, ficha da zona, marcação, ficha da pessoa, cartão do pedido, as telas da R2b: Campanha com os três blocos, Equipe, decisão da equipe, Verba, canais, modo Verba, Plataforma, tema, prometer e Objetivos, e as da R3: Pessoas com a chapa, painel da chapa, ficha do rival, atacar, Mídia, ficha de veículo, entrevista com veículo, ataque com prazo, Finanças com o bloco Contas, painel Contas, oferta de doação, modo Disputa e cartão de evento com pessoas; e as telas da R4 em cidade pequena e metrópole: mandato de vereador, gabinete, instrumentos, achado, Mesa e CPI, Base, contagem nominal, evento de mandato, cobrança do Livro, indicadores, composição da Câmara, entressafra, e a prefeitura com Governo, Secretariado, Obras, modalidade, inauguração, Pessoal e fiscal, mesa de negociação, Convênios, Câmara, Orçamento e políticas e a crise no cartão; e as telas fora da tela central: menu, Opções, Novo Mundo, criação com partido próprio, seletor de cidade, mapa do estado, debate, urna e apuração)");
        GetTree().Quit(0);
    }

    /// <summary>
    /// Novo Mundo e criação (todas as abas) na janela como o jogo a abre: nada passa da área de desenho e todo botão
    /// fora de lista rolável fica, na tela, dentro da área útil do monitor (não atrás da barra de tarefas).
    /// </summary>
    private async Task CheckRealWindow(List<string> problems, string? shot)
    {
        Game game = Game.Instance;
        game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(2028)).Setup;
        var world = GD.Load<PackedScene>(Game.Scenes.NewWorld).Instantiate<Control>();
        AddChild(world);
        await Frames(5);
        problems.AddRange(Overflow(world, "Novo Mundo, janela real"));
        problems.AddRange(OutsideUsable(world, "Novo Mundo, janela real"));
        Save(shot, "_janela_real_novo_mundo");
        RemoveChild(world);
        world.QueueFree();

        game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(2028)).Setup;
        game.PendingSeed = SmokeSeed;
        var creator = GD.Load<PackedScene>(Game.Scenes.CharacterCreator).Instantiate<CharacterCreatorScreen>();
        AddChild(creator);
        await Frames(5);
        var tabs = FindAll<TabContainer>(creator).First();
        for (int tab = 0; tab < tabs.GetTabCount(); tab++)
        {
            tabs.CurrentTab = tab;
            await Frames(4);
            problems.AddRange(Overflow(creator, $"Criação, aba {tabs.GetTabTitle(tab)}, janela real"));
            problems.AddRange(OutsideUsable(creator, $"Criação, aba {tabs.GetTabTitle(tab)}, janela real"));
            Save(shot, $"_janela_real_criacao_{tab}");
        }

        RemoveChild(creator);
        creator.QueueFree();
        await Frames(2);
    }

    /// <summary>Botões visíveis (fora de lista rolável) cujo retângulo na tela sai da área útil do monitor.</summary>
    private List<string> OutsideUsable(Node root, string where)
    {
        Rect2I usable = UI.WindowFit.Usable;
        var problems = new List<string>();

        // Do desenho (pontos lógicos) para a tela (pixels reais): a escala da janela e a posição da área de desenho.
        Transform2D toWindow = GetViewport().GetFinalTransform();
        Vector2 origin = DisplayServer.WindowGetPosition();
        foreach (Button button in FindAll<Button>(root).Where(b => b.IsVisibleInTree() && b.Size.X > 0 && b.Size.Y > 0))
        {
            bool scrolls = false;
            for (Node? up = button.GetParent(); up is not null && up != root; up = up.GetParent())
            {
                scrolls |= up is ScrollContainer or Window;
            }

            if (scrolls)
            {
                continue;
            }

            Rect2 inWindow = toWindow * button.GetGlobalRect();
            var screen = new Rect2(inWindow.Position + origin, inWindow.Size);
            if (screen.Position.X < usable.Position.X - 1 || screen.Position.Y < usable.Position.Y - 1 || screen.End.X > usable.End.X + 1 || screen.End.Y > usable.End.Y + 1)
            {
                problems.Add($"{where}: Button \"{button.Text}\" ocupa na tela {screen.Position} a {screen.End}, fora da área útil do monitor ({usable.Position} a {usable.End})");
            }
        }

        return problems;
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
        game.PendingSeed = SmokeSeed;
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
                var dialogs = FindAll<Widgets.EventDialog>(hub).ToList();
                var dialog = dialogs.Count == 1 ? dialogs[0] : null;
                if (dialog is null || !hub.EventOpen)
                {
                    EventFailure($"o evento {definition.Id} não abriu (cartões de evento na tela: {dialogs.Count}).", hub, dialogs.FirstOrDefault(), null, definition.Id, choice);
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

                Button option = buttons[choice];
                Vector2 optionPoint = option.GetGlobalRect().GetCenter();
                if (await ClickOrExplain(option) is { } optionMiss)
                {
                    EventFailure($"{definition.Id}, opção {choice + 1}: {optionMiss}", hub, dialog, optionPoint, definition.Id, choice);
                    return;
                }

                var proceed = FindAll<Button>(dialog).FirstOrDefault(b => b.Text == Tr("Continuar"));
                if (proceed is null)
                {
                    EventFailure($"{definition.Id}, opção {choice + 1}: sem o botão Continuar depois de escolher.", hub, dialog, optionPoint, definition.Id, choice);
                    return;
                }

                Vector2 proceedPoint = proceed.GetGlobalRect().GetCenter();
                if (await ClickOrExplain(proceed) is { } proceedMiss)
                {
                    EventFailure($"{definition.Id}, opção {choice + 1}: {proceedMiss}", hub, dialog, proceedPoint, definition.Id, choice);
                    return;
                }

                await Frames(1);
                if (IsInstanceValid(dialog) && dialog.IsInsideTree() && !dialog.IsQueuedForDeletion())
                {
                    EventFailure($"{definition.Id}, opção {choice + 1}: o cartão do evento não fechou com Continuar.", hub, dialog, proceedPoint, definition.Id, choice);
                    return;
                }

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

                // O efeito da escolha também pode deixar outra decisão esperando (pedido de liderança, decisão da
                // equipe): o cartão dela é legítimo e não é o objeto deste teste.
                if (campaign.Agenda.PendingRequest is not null || campaign.Team.Pending.Count > 0)
                {
                    // Só as decisões dos outros sistemas: um evento que continue pendente reprova logo abaixo.
                    campaign.Agenda.PendingRequest = null;
                    campaign.Team.Pending.Clear();
                    hub.Refresh();
                    await Frames(3);
                }

                if (hub.EventOpen || campaign.Events.Pending.Count > 0)
                {
                    EventFailure($"{definition.Id}, opção {choice + 1}: o fundo do evento continuou na tela (cartão aberto: {hub.EventOpen}; pendentes: {string.Join(", ", campaign.Events.Pending.Select(p => p.EventId))}).", hub, null, proceedPoint, definition.Id, choice);
                    return;
                }

                var map = FindAll<Button>(hub).First(b => b.Text == Tr("Mapa"));
                Vector2 mapPoint = map.GetGlobalRect().GetCenter();
                game.ResetNavigationForTest();
                string? mapMiss = await ClickOrExplain(map);
                if (mapMiss is not null || game.LastNavigation != Game.Scenes.Map)
                {
                    EventFailure((mapMiss is null ? "" : mapMiss + " ") + $"{definition.Id}, opção {choice + 1}: o hub não aceitou o clique depois do evento (botão em {map.GetGlobalRect()}, sob o mouse: {GetViewport().GuiGetHoveredControl()?.GetPath()}, janela {GetViewport().GetVisibleRect().Size}, navegação registrada: {game.LastNavigation ?? "nenhuma"}).", hub, null, mapPoint, definition.Id, choice);
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
        game.PendingSeed = SmokeSeed;
        game.StartCareer(NewGame.Create(world, player, game.PendingSeed));
        game.State!.Campaign!.Events.Pending.Clear();
        var hub = (HubScreen)GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate();
        AddChild(hub);
        await Frames(5);

        var problems = new List<string>();
        void Check(Node root, string where) => problems.AddRange(ExplainProblems(root, where));

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

        // Opções abertas pela barra de cima da tela central.
        await Click(Named<Button>(hub, "AbrirOpcoes")!, () => Widgets.OptionsDialog.IsOpen(GetTree().Root));
        Check(GetTree().Root.FindChild("Opcoes", recursive: true, owned: false)!, "Opções pela tela central");
        await Click(Named<Button>(GetTree().Root, "FecharOpcoes")!, () => !Widgets.OptionsDialog.IsOpen(GetTree().Root));

        await PressF1();
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

        // Telas da R4: mandato de vereador, entressafra e prefeitura na tela central.
        // A camada inteira (o fundo escuro também): sem isso o fundo ficaria sobre a tela e pegaria os cliques seguintes.
        GetTree().Root.FindChild("EnciclopediaCamada", recursive: true, owned: false)?.QueueFree();
        RemoveChild(hub);
        hub.QueueFree();
        await Frames(3);
        int govPanels = 0;
        ClearGovSaves();
        if (!await VisitGovScreens(SmallCity, "cidade pequena", (govHub, where, _) =>
        {
            govPanels++;
            Check(govHub, where);
            return Task.CompletedTask;
        }))
        {
            return;
        }

        if (govPanels < 60)
        {
            Fail($"o teste de explicação só passou por {govPanels} telas da R4 (esperado: 60 ou mais).");
            return;
        }

        // Telas fora da tela central (P-02): menu e Opções, Novo Mundo, criação com o seletor de cidade, mapa do
        // estado, debate, urna e apuração.
        int outside = await VisitOutsideScreens((root, where, _) => Check(root, where));
        if (outside < OutsideScreensMinimum)
        {
            Fail($"o teste de explicação só passou por {outside} telas fora da tela central (esperado: {OutsideScreensMinimum} ou mais).");
            return;
        }

        // Nenhum texto mostrado ao jogador cita documentos internos.
        foreach (string text in game.Encyclopedia.Termos.Select(t => t.Texto).Concat(game.Encyclopedia.Artigos.Select(a => a.Texto)))
        {
            if (text.Contains("GDD", StringComparison.Ordinal) || text.Contains("docs/", StringComparison.Ordinal) || text.Contains("ROADMAP", StringComparison.Ordinal))
            {
                problems.Add($"Enciclopédia: texto cita documento interno: {text[..Math.Min(60, text.Length)]}");
            }
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

        GD.Print($"Smoke explicacao: ok ({areas} áreas, {zoneMap.Zonas.Count} zonas, {panels} telas da R2b, {r3} telas da R3, {govPanels} telas da R4, {outside} telas fora da tela central, Opções e Enciclopédia aberta com F1)");
        GetTree().Quit(0);
    }

    /// <summary>Texto ou tooltip do controle que cita documento interno do projeto; null se nenhum.</summary>
    private static string? InternalReference(Control c)
    {
        string text = c switch
        {
            RichTextLabel rich => rich.GetParsedText(),
            Label label => label.Text,
            Button button => button.Text,
            LineEdit line => line.Text + " | " + line.PlaceholderText,
            TextEdit edit => edit.PlaceholderText,
            _ => "",
        };
        if (c is OptionButton options)
        {
            text += " | " + string.Join(" | ", Enumerable.Range(0, options.ItemCount).Select(options.GetItemText));
        }

        string all = text + " | " + c.TooltipText;
        return Core.Data.InternalReferenceLint.Find(all) is { } found
            ? $"texto de tela cita documento interno (\"{found}\") em \"{all.Trim()[..Math.Min(80, all.Trim().Length)]}\""
            : null;
    }

    /// <summary>
    /// Semente de toda carreira dos testes. O Novo Mundo sorteia uma semente nova pelo relógio ao abrir (é o jogo
    /// normal); sem voltar a esta, o que vem depois dele mudaria de uma rodada para outra.
    /// </summary>
    private const ulong SmokeSeed = 2028;

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
        game.PendingSeed = SmokeSeed;
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

        // Espera pelo primeiro dia em tempo real (1 s por dia em 4x), com prazo; quadros contados variam com a carga.
        ulong dayLimit = Time.GetTicksMsec() + ClickWaitMs;
        while (campaign.Agenda.Log.Count == 0 && hub.Running && Time.GetTicksMsec() < dayLimit)
        {
            await Frames(1);
        }

        // Com a máquina carregada passam mais dias na espera e uma pausa automática pode ter parado o relógio:
        // nesse caso o clique em Pausa o religaria. Volta a correr antes, para o clique testar a pausa.
        if (!hub.Running)
        {
            await Click(FindAll<Button>(hub).First(b => b.Text == "4x"));
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

        await ClickAt(map, map.GlobalPosition + map.ScreenCenterOf(target));
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
        await PressF1();
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
    private async Task ClickCard(Control card) => await ClickOn(card, () => card.GetGlobalRect().Position + new Vector2(30, 30), "cartão " + card.Name);

    /// <summary>
    /// Clique de mouse de verdade num ponto de <paramref name="target"/> (o mapa, por exemplo), com as mesmas garantias de
    /// <see cref="Click"/>: o alvo pronto, o ponto sobre ele e a confirmação de que o clique chegou.
    /// </summary>
    private async Task ClickAt(Control target, Vector2 point) => await ClickOn(target, () => point, target.Name);

    /// <summary>Clique com as garantias de <see cref="Click"/>; devolve o motivo, em vez de parar o teste, quando ele não chega.</summary>
    private async Task<string?> ClickOrExplain(Control control)
    {
        try
        {
            await Click(control);
            return null;
        }
        catch (InvalidOperationException e)
        {
            return e.Message;
        }
    }

    /// <summary>Tempo máximo, em tempo real, para um controle ficar pronto para o clique ou para a tela responder.</summary>
    private const ulong ClickWaitMs = 20_000;

    /// <summary>
    /// Clique de mouse de verdade no centro do controle (passa pelo roteamento de entrada da interface). Antes, espera o
    /// controle estar na tela, visível, habilitado, dentro da janela e parado no lugar, e o ponto ficar sobre ele (nada
    /// cobrindo); depois confere que o clique chegou ao controle. Movimento, aperto e soltura entram juntos, no mesmo
    /// quadro: um movimento do mouse de verdade ou a perda de foco da janela (outro programa abrindo janela) não
    /// cabem entre eles. Clique que não chega é falha com o diagnóstico, nunca repetido às cegas.
    /// </summary>
    private async Task Click(Control control, Func<bool>? until = null, string? what = null)
    {
        // O nome vem antes do clique: o controle pode sair da tela com ele (o botão de fechar, por exemplo).
        string label = what ?? (control is Button b && b.Text.Length > 0 ? $"botão \"{b.Text}\"" : control.Name.ToString());
        await ClickOn(control, () => control.GetGlobalRect().GetCenter(), label);
        if (until is not null)
        {
            await WaitUntil(until, $"a tela responder ao clique em {label}");
        }
    }

    private async Task ClickOn(Control control, Func<Vector2> pointOf, string what)
    {
        await Reveal(control);
        await WaitClickable(control, what);
        Viewport viewport = control.GetViewport();
        Vector2 point = pointOf();

        // O ponto precisa estar sobre o controle (ou um filho dele que deixa o clique passar): se outra coisa estiver por
        // cima (um cartão, uma janela), o movimento é refeito no quadro seguinte até o fim do prazo.
        ulong deadline = Time.GetTicksMsec() + ClickWaitMs;
        while (true)
        {
            viewport.PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, inLocalCoords: true);
            Control? hovered = viewport.GuiGetHoveredControl();
            if (hovered is not null && (hovered == control || control.IsAncestorOf(hovered)))
            {
                break;
            }

            if (Time.GetTicksMsec() > deadline)
            {
                throw new InvalidOperationException($"clique em {what}: o ponto {point} está coberto por {hovered?.GetPath().ToString() ?? "nada"} ({UnderPoint(point)}).");
            }

            await Frames(1);
            await WaitClickable(control, what);
            point = pointOf();
        }

        bool received = false;
        Action onPressed = () => received = true;
        Control.GuiInputEventHandler onInput = e => received |= e is InputEventMouseButton { ButtonIndex: MouseButton.Left };
        if (control is BaseButton button)
        {
            button.Pressed += onPressed;
        }
        else
        {
            control.GuiInput += onInput;
        }

        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point, GlobalPosition = point }, inLocalCoords: true);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point, GlobalPosition = point }, inLocalCoords: true);
        if (IsInstanceValid(control))
        {
            if (control is BaseButton pressed)
            {
                pressed.Pressed -= onPressed;
            }
            else
            {
                control.GuiInput -= onInput;
            }
        }

        if (!received)
        {
            throw new InvalidOperationException($"o clique em {what} não chegou ao controle (sob o mouse: {viewport.GuiGetHoveredControl()?.GetPath().ToString() ?? "nada"}; {UnderPoint(point)}).");
        }

        // O que o clique fez já está no estado; um quadro para a tela liberar o que saiu e arrumar o que entrou.
        await Frames(1);
    }

    /// <summary>
    /// Espera o controle poder receber o clique: na tela, visível, habilitado, com tamanho, com o centro dentro da janela
    /// e no mesmo lugar em dois quadros seguidos (o contêiner já arrumou os filhos).
    /// </summary>
    private async Task WaitClickable(Control control, string what)
    {
        ulong deadline = Time.GetTicksMsec() + ClickWaitMs;
        Rect2? last = null;
        while (true)
        {
            string? why = NotClickable(control);
            if (why is null)
            {
                Rect2 rect = control.GetGlobalRect();
                if (last == rect)
                {
                    return;
                }

                last = rect;
            }
            else
            {
                last = null;
            }

            if (Time.GetTicksMsec() > deadline)
            {
                throw new InvalidOperationException($"clique em {what}: {why ?? "o controle não parou no lugar"}.");
            }

            await Frames(1);
        }
    }

    private static string? NotClickable(Control c)
    {
        if (!IsInstanceValid(c) || !c.IsInsideTree() || c.IsQueuedForDeletion())
        {
            return "o controle saiu da tela";
        }

        if (!c.IsVisibleInTree())
        {
            return "o controle não está visível";
        }

        if (c is BaseButton { Disabled: true })
        {
            return "o controle está desativado (" + c.TooltipText.Split('\n')[0] + ")";
        }

        Rect2 rect = c.GetGlobalRect();
        if (rect.Size.X < 1 || rect.Size.Y < 1)
        {
            return "o controle ainda não tem tamanho";
        }

        return c.GetViewport().GetVisibleRect().HasPoint(rect.GetCenter()) ? null : $"o controle está fora da janela ({rect})";
    }

    /// <summary>Espera uma condição da tela, em tempo real, com prazo; sem ela, o teste para com o motivo.</summary>
    private async Task WaitUntil(Func<bool> condition, string what)
    {
        ulong deadline = Time.GetTicksMsec() + ClickWaitMs;
        while (!condition())
        {
            if (Time.GetTicksMsec() > deadline)
            {
                throw new InvalidOperationException($"passou o prazo esperando {what}.");
            }

            await Frames(1);
        }
    }

    /// <summary>Controles visíveis sob um ponto, do fundo para a frente (diagnóstico de clique que não chegou).</summary>
    private string UnderPoint(Vector2 point) =>
        "sob o ponto: " + string.Join(", ", FindAll<Control>(GetTree().Root).Where(c => c.IsVisibleInTree() && c.MouseFilter != Control.MouseFilterEnum.Ignore && c.GetGlobalRect().HasPoint(point)).Select(c => $"{c.Name} [{c.GetType().Name}]").TakeLast(4));

    private void Fail(string message)
    {
        GD.PushError("Smoke: " + message);
        GetTree().Quit(1);
    }

    /// <summary>
    /// Falha do alvo eventos com a prova do que estava na tela: grava uma captura e um log com o evento e a opção, o
    /// cartão aberto, o foco, os controles sob o ponto clicado, as pausas e decisões pendentes e o quadro. Vai para a
    /// pasta de "--saida=", ou para user://smoke/&lt;perfil&gt;/falhas/ (apagada na rodada seguinte do mesmo perfil).
    /// </summary>
    private void EventFailure(string message, HubScreen hub, Node? dialog, Vector2? point, string eventId, int choice)
    {
        try
        {
            string? output = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--saida=", StringComparison.Ordinal))?.Split('=', 2)[1];
            string folder = (output ?? ProjectSettings.GlobalizePath(UserPaths.Of("falhas"))).TrimEnd('/', '\\') + "/";
            DirAccess.MakeDirRecursiveAbsolute(folder);
            string stamp = $"eventos_{DateTime.Now:yyyyMMdd_HHmmss}_{eventId}_{choice + 1}";
            Viewport viewport = GetViewport();
            viewport.GetTexture().GetImage().SavePng(folder + stamp + ".png");

            Game game = Game.Instance;
            var campaign = game.State?.Campaign;
            var lines = new List<string>
            {
                "Falha: " + message,
                $"Evento: {eventId}, opção {choice + 1}",
                $"Quadro: {Engine.GetProcessFrames()} (física {Engine.GetPhysicsFrames()})",
                $"Janela: {viewport.GetVisibleRect().Size}, com foco do sistema: {DisplayServer.WindowIsFocused()}, modo: {DisplayServer.WindowGetMode()}",
                $"Cartão de evento aberto (fundo): {hub.EventOpen}; cartão de mandato aberto: {hub.GovCardOpen}; tempo correndo: {hub.Running}; motivo da pausa: {hub.BlockReason() ?? "nenhum"}",
                $"Cartão da falha: {(dialog is null ? "nenhum" : !IsInstanceValid(dialog) ? "liberado" : $"{dialog.GetPath()} (na árvore: {dialog.IsInsideTree()}, na fila para sair: {dialog.IsQueuedForDeletion()}, visível: {(dialog as CanvasItem)?.IsVisibleInTree()})")}",
                $"Cartões de evento na tela: {FindAll<Widgets.EventDialog>(hub).Count()}",
                $"Eventos pendentes: {(campaign is null ? "sem campanha" : string.Join(", ", campaign.Events.Pending.Select(p => p.EventId)))}",
                $"Pedido de liderança pendente: {campaign?.Agenda.PendingRequest is not null}; decisões da equipe pendentes: {campaign?.Team.Pending.Count ?? 0}",
                $"Foco da interface: {viewport.GuiGetFocusOwner()?.GetPath().ToString() ?? "nenhum"}",
                $"Sob o mouse: {viewport.GuiGetHoveredControl()?.GetPath().ToString() ?? "nenhum"}; mouse em {viewport.GetMousePosition()}",
                $"Navegação registrada: {game.LastNavigation ?? "nenhuma"}",
            };
            if (point is { } p)
            {
                lines.Add($"Controles visíveis sob o ponto clicado {p} (do fundo para a frente):");
                foreach (Control c in FindAll<Control>(GetTree().Root).Where(c => c.IsVisibleInTree() && c.GetGlobalRect().HasPoint(p)))
                {
                    lines.Add($"  {c.GetPath()} [{c.GetType().Name}, mouse {c.MouseFilter}{(c is BaseButton b ? $", desativado {b.Disabled}" : "")}]");
                }
            }

            lines.Add("Janelas e popups visíveis:");
            foreach (Window w in FindAll<Window>(GetTree().Root).Where(w => w.Visible))
            {
                lines.Add($"  {w.GetPath()} ({w.GetType().Name})");
            }

            using var file = Godot.FileAccess.Open(folder + stamp + ".txt", Godot.FileAccess.ModeFlags.Write);
            file?.StoreString(string.Join("\n", lines) + "\n");
            GD.Print($"Smoke eventos: captura e log da falha em {folder}{stamp}.png e .txt");
        }
        catch (Exception e)
        {
            GD.PushError("Smoke: não foi possível gravar a captura da falha: " + e.Message);
        }

        Fail(message);
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

    /// <summary>F1 como tecla, e a espera (com prazo) até a Enciclopédia abrir.</summary>
    private async Task PressF1()
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F1, PhysicalKeycode = Key.F1, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F1, PhysicalKeycode = Key.F1, Pressed = false });
        await WaitUntil(() => Widgets.EncyclopediaDialog.IsOpen(GetTree().Root), "F1 abrir a Enciclopédia");
        await Frames(1);
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
