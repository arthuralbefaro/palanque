using Godot;
using Planalto.Autoload;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Teste de fumaça e captura das telas: "-- --target=newworld|creator|hub|picker|campaign|map|mandate [--screenshot=arquivo.png]".
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
                AddChild(GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate());
                await Frames(5);
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
