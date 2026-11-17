using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Fase R, parte final: as telas fora da tela central no teste de explicação e no de layout (P-02).
/// </summary>
public partial class UiShot
{
    /// <summary>Telas fora da tela central que o `explicacao` precisa percorrer, no mínimo.</summary>
    private const int OutsideScreensMinimum = 20;

    /// <summary>
    /// Princípio j num controle e nos filhos: todo controle interativo (botão, caixa de marcar, lista, campo de texto,
    /// número, barra deslizante, cartão clicável e cada aba) tem tooltip em três partes; todo controle bloqueado diz o
    /// motivo; nenhum termo do glossário fica em Label sem link; nenhum texto cita documento interno.
    /// </summary>
    private List<string> ExplainProblems(Node root, string where)
    {
        var problems = new List<string>();
        foreach (Control c in FindAll<Control>(root).Where(c => c.IsVisibleInTree()))
        {
            if (InternalReference(c) is { } reference)
            {
                problems.Add($"{where}: {reference}");
            }

            if (c is BaseButton or LineEdit or SpinBox or Slider or TextEdit or ItemList or PanelContainer { MouseDefaultCursorShape: Control.CursorShape.PointingHand })
            {
                string tip = c.TooltipText;
                string name = c is Button { Text.Length: > 0 } b ? b.Text : c.Name;
                if (!ThreeParts(tip))
                {
                    problems.Add($"{where}: controle '{name}' ({c.GetType().Name}) sem tooltip em três partes");
                }

                if (c is BaseButton { Disabled: true } or LineEdit { Editable: false } && !tip.StartsWith(Tr("Bloqueado:"), StringComparison.Ordinal))
                {
                    problems.Add($"{where}: controle bloqueado '{name}' sem motivo");
                }
            }
            else if (c is Label label && !label.Name.ToString().StartsWith("TituloVerbete", StringComparison.Ordinal) && Widgets.Glossary.TermsIn(label.Text).FirstOrDefault() is { } term)
            {
                problems.Add($"{where}: termo '{term}' sem link em \"{label.Text}\"");
            }

            if (c is TabContainer tabs)
            {
                for (int i = 0; i < tabs.GetTabCount(); i++)
                {
                    if (!ThreeParts(tabs.GetTabBar().GetTabTooltip(i)))
                    {
                        problems.Add($"{where}: aba '{tabs.GetTabTitle(i)}' sem tooltip em três partes");
                    }
                }
            }
        }

        return problems;
    }

    private bool ThreeParts(string tip) => tip.Contains(Tr("O que faz:"), StringComparison.Ordinal) && tip.Contains(Tr("Quanto custa:"), StringComparison.Ordinal);

    /// <summary>
    /// Percorre as telas fora da tela central, chamando <paramref name="check"/> em cada estado: menu principal e
    /// Opções, Novo Mundo (cenário espelhado e predefinição comum), criação (as cinco abas, partido próprio e o seletor
    /// de cidade antes e depois de escolher), mapa do estado, debate (mão e resultado), urna (número, candidato e
    /// legenda) e apuração (parcial e final). Devolve quantos estados passaram.
    /// </summary>
    private async Task<int> VisitOutsideScreens(Action<Node, string, string> check)
    {
        Game game = Game.Instance;
        int visited = 0;
        void Visit(Node root, string where, string slug)
        {
            visited++;
            check(root, where, slug);
        }

        // Menu principal e as Opções abertas por ele.
        UI.Settings.ResetForTest();
        var menu = GD.Load<PackedScene>(Game.Scenes.MainMenu).Instantiate<Control>();
        AddChild(menu);
        await Frames(3);
        Visit(menu, "menu principal", "menu");
        await Click(menu.GetNode<Button>("%OptionsButton"), () => Widgets.OptionsDialog.IsOpen(GetTree().Root));
        Visit(GetTree().Root.FindChild("Opcoes", recursive: true, owned: false)!, "Opções pelo menu", "opcoes_menu");
        await Click(Named<Button>(GetTree().Root, "FecharOpcoes")!, () => !Widgets.OptionsDialog.IsOpen(GetTree().Root));
        RemoveChild(menu);
        menu.QueueFree();

        // Novo Mundo: o cenário espelhado (sem adicionar nem remover partidos) e a predefinição comum.
        foreach (var (preset, label) in new[] { ("brasil_2026", "cenário espelhado"), (game.World.Presets.DefaultPreset, "predefinição padrão") })
        {
            game.PendingWorld = WorldSetup.Create(game.World, preset, new Rng(SmokeSeed)).Setup;
            var world = GD.Load<PackedScene>(Game.Scenes.NewWorld).Instantiate<Control>();
            AddChild(world);
            await Frames(4);
            Visit(world, $"Novo Mundo, {label}", preset == "brasil_2026" ? "novo_mundo_cenario" : "novo_mundo_padrao");
            RemoveChild(world);
            world.QueueFree();
        }

        // Criação: as cinco abas, o partido próprio e o seletor de cidade.
        game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(SmokeSeed)).Setup;
        game.PendingSeed = SmokeSeed;
        var creator = GD.Load<PackedScene>(Game.Scenes.CharacterCreator).Instantiate<CharacterCreatorScreen>();
        AddChild(creator);
        await Frames(4);
        var tabs = FindAll<TabContainer>(creator).First();
        for (int tab = 0; tab < tabs.GetTabCount(); tab++)
        {
            tabs.CurrentTab = tab;
            await Frames(2);
            Visit(creator, $"criação, aba {tabs.GetTabTitle(tab)}", $"criacao_aba{tab}");
        }

        FindAll<CheckBox>(creator).First(b => b.Text == Tr("Criar meu partido")).EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        tabs = FindAll<TabContainer>(creator).First();
        tabs.CurrentTab = tabs.GetTabCount() - 1;
        await Frames(2);
        Visit(creator, "criação, aba Partido com partido próprio", "criacao_partido_proprio");

        tabs.CurrentTab = 2;
        await Frames(2);
        FindAll<Button>(creator).First(b => b.Text == Tr("Escolher cidade")).EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        var picker = FindAll<Widgets.MunicipioPicker>(creator).Single();
        Visit(picker, "seletor de cidade", "seletor");
        picker.SelectFirstForTest("Salvador");
        await Frames(2);
        Visit(picker, "seletor de cidade com a cidade escolhida", "seletor_escolhida");
        picker.QueueFree();
        RemoveChild(creator);
        creator.QueueFree();
        await Frames(2);

        // Uma carreira de prefeito numa cidade média, até a urna: mapa do estado, debate, urna e apuração.
        game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(SmokeSeed)).Setup;
        var setup = game.PendingWorld!;
        var city = game.Municipios.All.First(m => m.CodigoIbge == MediumCity);
        var player = Core.Characters.PersonGenerator.Generate(new Rng(9), game.People, new Core.Characters.PersonRequest(RaceSetup.PlayerId, city.Uf, city.CodigoIbge, new DateOnly(setup.Options.StartYear, 1, 1), Age: 40, PartyId: setup.Parties[0].Id));
        game.PendingSeed = SmokeSeed;
        game.StartCareer(NewGame.Create(setup, player, game.PendingSeed));
        if (game.SwitchCandidacy(Core.Elections.Office.Prefeito).Issue is { } issue)
        {
            Fail($"candidatura a prefeito recusada para o teste do debate ({issue}).");
            return visited;
        }

        var map = (UI.Map.MapScreen)GD.Load<PackedScene>(Game.Scenes.Map).Instantiate();
        map.Uf = "AC";
        AddChild(map);
        await Frames(4);
        Visit(map, "mapa do estado", "mapa_estado");
        RemoveChild(map);
        map.QueueFree();

        var engine = game.Engine!;
        var campaign = game.State!.Campaign!;
        bool debated = false;
        for (int guard = 0; guard < 80 && campaign.Phase != CampaignPhase.Votacao; guard++)
        {
            while (campaign.Events.Pending.Count > 0)
            {
                var pending = campaign.Events.Pending[0];
                var definition = game.Events.Find(pending.EventId)!;
                Core.Events.EventEngine.Resolve(engine.Context, pending, Enumerable.Range(0, definition.Choices.Count).First(i => Core.Events.EventEngine.Blocked(engine.Context, definition.Choices[i]) is null), game.State.Rng);
            }

            campaign.Team.Pending.Clear();
            campaign.Agenda.PendingRequest = null;
            if (campaign.Debate is not null)
            {
                if (!debated)
                {
                    debated = true;
                    var debate = GD.Load<PackedScene>(Game.Scenes.Debate).Instantiate<Control>();
                    AddChild(debate);
                    await Frames(3);
                    Visit(debate, "debate, mão de cartas", "debate");
                    for (int round = 0; round < 10 && !campaign.Debate.Finished; round++)
                    {
                        FindAll<Button>(debate).First(b => b.Text == Tr("Jogar")).EmitSignal(BaseButton.SignalName.Pressed);
                        await Frames(2);
                    }

                    Visit(debate, "debate, resultado", "debate_resultado");
                    FindAll<Button>(debate).First(b => b.Text == Tr("Voltar à campanha")).EmitSignal(BaseButton.SignalName.Pressed);
                    await Frames(2);
                    RemoveChild(debate);
                    debate.QueueFree();
                }
                else
                {
                    Core.Debate.DebateEngine.Decline(engine.Context, game.Debate);
                }
            }

            game.AdvanceWeek();
        }

        if (!debated || campaign.Phase != CampaignPhase.Votacao)
        {
            Fail($"a carreira de prefeito em {city.Nome} não chegou ao debate e à urna (debate: {debated}; fase {campaign.Phase}).");
            return visited;
        }

        var urna = GD.Load<PackedScene>(Game.Scenes.Urna).Instantiate<Control>();
        AddChild(urna);
        await Frames(3);
        Visit(urna, "urna, tela inicial", "urna");
        string mine = campaign.ForOffice(Core.Elections.Office.Vereador).First(c => !c.IsBulk && !c.IsPlayer).Number;
        foreach (char digit in mine)
        {
            Named<Button>(urna, "Tecla_" + digit)!.EmitSignal(BaseButton.SignalName.Pressed);
        }

        await Frames(2);
        Visit(urna, "urna, candidato na tela", "urna_candidato");
        Named<Button>(urna, "Tecla_CORRIGE")!.EmitSignal(BaseButton.SignalName.Pressed);
        foreach (char digit in mine[..2])
        {
            Named<Button>(urna, "Tecla_" + digit)!.EmitSignal(BaseButton.SignalName.Pressed);
        }

        await Frames(2);
        Visit(urna, "urna, voto de legenda", "urna_legenda");
        Named<Button>(urna, "Tecla_CONFIRMA")!.EmitSignal(BaseButton.SignalName.Pressed);
        foreach (char digit in campaign.Player.Number)
        {
            Named<Button>(urna, "Tecla_" + digit)!.EmitSignal(BaseButton.SignalName.Pressed);
        }

        Named<Button>(urna, "Tecla_CONFIRMA")!.EmitSignal(BaseButton.SignalName.Pressed);
        await WaitUntil(() => campaign.Outcome is not null, "a urna fechar a votação e a apuração sair");
        RemoveChild(urna);
        urna.QueueFree();

        var night = GD.Load<PackedScene>(Game.Scenes.ElectionNight).Instantiate<Control>();
        AddChild(night);
        await Frames(3);
        Visit(night, "apuração parcial", "apuracao_parcial");
        FindAll<Button>(night).First(b => b.Text == Tr("Pular para o resultado")).EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(2);
        Visit(night, "apuração final", "apuracao_final");
        RemoveChild(night);
        night.QueueFree();
        await Frames(2);
        return visited;
    }
}
