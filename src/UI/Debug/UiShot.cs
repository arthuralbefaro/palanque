using Godot;
using Planalto.Autoload;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Teste de fumaça e captura das telas: "-- --target=newworld|creator|hub|picker|campaign|map [--screenshot=arquivo.png]".
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
