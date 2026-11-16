using Godot;
using Planalto.Autoload;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Teste de fumaça e captura das telas: "-- --target=newworld|creator|creator17|hub|picker|campaign|map|mandate|prefeitura|eventos|layout [--screenshot=arquivo.png]".
/// Prepara o estado necessário, abre a tela, exercita os botões principais e fecha com código de saída.
/// </summary>
public partial class UiShot : Node
{
    public override async void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        string target = args.FirstOrDefault(a => a.StartsWith("--target=", StringComparison.Ordinal))?.Split('=', 2)[1] ?? "creator";
        string? shot = args.FirstOrDefault(a => a.StartsWith("--screenshot=", StringComparison.Ordinal))?.Split('=', 2)[1];
        Game game = Game.Instance;
        game.SuppressNavigation = true;
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

                // Cada ação da agenda mostra o efeito real no registro, nunca só "feito".
                game.State.Player.Resources.Money = 100_000;
                foreach (var action in game.Campaign.Actions.Actions)
                {
                    var button = FindAll<Button>(hub).FirstOrDefault(b => b.Text.StartsWith(action.Name, StringComparison.Ordinal) && !b.Disabled);
                    if (button is null)
                    {
                        continue;
                    }

                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    await Frames(2);
                    string line = hub.Log[0];
                    if (!line.StartsWith(action.Name, StringComparison.Ordinal) || line.Contains(Tr("feito."), StringComparison.Ordinal) || !line.Any(char.IsDigit) && !line.Contains(Tr("sem efeito"), StringComparison.Ordinal))
                    {
                        Fail($"resultado sem efeito mensurável: \"{line}\".");
                        return;
                    }

                    GD.Print("  " + line);
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
            var hub = GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate<Control>();
            AddChild(hub);
            await Frames(5);
            problems.AddRange(Overflow(hub, $"Hub {tag}"));
            Save(shot, $"_hub_{tag}");
            RemoveChild(hub);
            hub.QueueFree();
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

        GD.Print("Smoke layout: ok (1920x1080 e 1366x768, criação em todas as abas, Novo Mundo e hub)");
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

                var buttons = FindAll<Button>(dialog).ToList();
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
                    Fail($"{definition.Id}, opção {choice + 1}: o hub não aceitou o clique depois do evento.");
                    return;
                }

                clicked++;
            }
        }

        GD.Print($"Smoke eventos: ok ({game.Events.Events.Count} eventos, {clicked} opções clicadas, {blocked} bloqueadas por requisito)");
        GetTree().Quit(0);
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
        Vector2 center = control.GetGlobalRect().GetCenter();
        Viewport viewport = GetViewport();
        viewport.PushInput(new InputEventMouseMotion { Position = center, GlobalPosition = center });
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = center, GlobalPosition = center });
        await Frames(1);
        viewport.PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = center, GlobalPosition = center });
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

            foreach (var action in game.Campaign.Actions.Actions)
            {
                engine.Execute(action.Id, action.Targets.Count > 0 ? policy.Pick(action.Targets).Id : null);
            }

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
