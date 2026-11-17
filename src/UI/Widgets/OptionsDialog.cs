using Godot;
using Planalto.Autoload;
using Planalto.Core.State;
using Planalto.UI.Theme;

namespace Planalto.UI.Widgets;

/// <summary>
/// Opções do jogador (GDD 17.4), abertas pelo menu principal e pela tela central. Aba Tempo: liga e desliga cada
/// pausa automática de data/time.json (as que não se desligam aparecem bloqueadas, com o motivo) e religa as dicas de
/// primeira vez. Tudo é gravado por <see cref="Settings"/>, fora do save.
/// </summary>
public partial class OptionsDialog : PanelContainer
{
    private const string NodeName = "Opcoes";

    private Action? _onClosed;
    private VBoxContainer _body = null!;
    private string? _message;

    public static bool IsOpen(Node root) => root.FindChild(NodeName, recursive: true, owned: false) is not null;

    /// <summary>Abre as Opções sobre a tela atual; <paramref name="onClosed"/> roda ao fechar (a tela se redesenha).</summary>
    public static void Open(Node root, Action? onClosed = null)
    {
        if (IsOpen(root))
        {
            return;
        }

        var layer = new CanvasLayer { Layer = 50, Name = NodeName + "Camada" };
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.5f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        layer.AddChild(shade);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shade.AddChild(center);
        center.AddChild(new OptionsDialog { Name = NodeName, _onClosed = onClosed });
        root.AddChild(layer);
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(960, 720);
        Theme = Palette.CentralTheme();
        AddThemeStyleboxOverride("panel", Palette.Box(Palette.Painel, 12, 24, Palette.Linha));
        var root = Ui.Column(this, 12);
        var header = Ui.Row(root, 12);
        var title = Ui.Label(header, "Opções", 32);
        title.AddThemeFontOverride("font", Palette.Weight(Palette.Oswald, 600));
        title.AddThemeColorOverride("font_color", Palette.Texto);
        header.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Ui.Button(header, "Fechar", Close, Palette.Tip(Tr("Fecha as Opções (também com Esc)."), Tr("volta para a tela em que você estava; o que você mudou já está gravado."), Tr("nada."))).Name = "FecharOpcoes";

        var tabs = Ui.Row(root, 8);
        var time = Ui.Button(tabs, "Tempo", () => { }, Palette.Tip(Tr("Aba Tempo: as pausas automáticas e as dicas de primeira vez."), Tr("mostra quando o jogo para sozinho e deixa você ligar ou desligar cada pausa."), Tr("nada.")));
        time.Name = "Aba_tempo";
        time.ToggleMode = true;
        time.SetPressedNoSignal(true);

        var scroll = root.Add(new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        _body = scroll.Add(new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _body.AddThemeConstantOverride("separation", 10);
        Build();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Close()
    {
        GetParent()?.GetParent()?.GetParent()?.QueueFree();
        _onClosed?.Invoke();
    }

    private void Build()
    {
        Ui.Clear(_body);
        TimeConfig config = Game.Instance.Time;
        Heading(Tr("Pausas automáticas"));
        Glossary.Rich(_body, Tr("O jogo para sozinho quando algo pede a sua atenção. Desligue as pausas de que você não precisa: o acontecimento continua à vista na tela, só o tempo não para. As que protegem uma decisão que não pode esperar ficam sempre ligadas."), 18, Palette.Texto2);
        foreach (PauseRule rule in config.Pausas)
        {
            bool on = Settings.PauseOn(config, rule.Id);
            var row = Ui.Row(_body, 12);
            var box = row.Add(new CheckBox
            {
                Name = "Pausa_" + rule.Id,
                Text = rule.Nome,
                ButtonPressed = on,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 44),
            });
            string tip = Palette.Tip(
                rule.Nome + ".",
                rule.Desligavel
                    ? string.Format(Tr("ligada, o jogo para sozinho quando isso acontece; desligada, {0}"), rule.Explicacao)
                    : Tr("o jogo sempre para quando isso acontece."),
                Tr("nada; vale para todas as carreiras neste computador."));
            if (rule.Desligavel)
            {
                box.TooltipText = tip;
                string id = rule.Id;
                box.Toggled += pressed =>
                {
                    Settings.SetPause(id, pressed);
                    _message = string.Format(Tr(pressed ? "Pausa ligada: {0}." : "Pausa desligada: {0}."), rule.Nome);
                    Callable.From(Build).CallDeferred();
                };
            }
            else
            {
                box.Disabled = true;
                box.TooltipText = Palette.Blocked(Tr("esta pausa não se desliga: ") + rule.Explicacao, tip);
            }

            // O estado também em palavras, à direita (a cor da caixa não é o único sinal).
            var state = Ui.Value(row, !rule.Desligavel ? Tr("sempre ligada") : on ? Tr("ligada") : Tr("desligada"), 18);
            state.Name = "Estado_" + rule.Id;
            state.CustomMinimumSize = new Vector2(150, 0);
            state.HorizontalAlignment = HorizontalAlignment.Right;
            state.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            state.AddThemeColorOverride("font_color", on ? Palette.Texto : Palette.Texto3);
        }

        Heading(Tr("Dicas de primeira vez"));
        Glossary.Rich(_body, Tr("Cada área mostra uma dica curta na primeira visita. Dica fechada com Entendi, ou desligada com Sem dicas, não volta sozinha."), 18, Palette.Texto2);
        string hintTip = Palette.Tip(
            Tr("Religa as dicas de primeira vez."),
            Tr("as dicas voltam a aparecer, cada uma na próxima visita à área dela, inclusive as que você já fechou."),
            Tr("nada."));
        var restore = Ui.Button(_body, "Religar as dicas", () =>
        {
            Settings.RestoreHints();
            _message = Tr("Dicas religadas: cada área mostra a dica dela de novo na próxima visita.");
            Build();
        }, hintTip);
        restore.Name = "ReligarDicas";
        restore.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        if (!Settings.HintsToRestore)
        {
            restore.Disabled = true;
            restore.TooltipText = Palette.Blocked(Tr("as dicas já estão ligadas e nenhuma foi fechada ainda."), hintTip);
        }

        if (_message is not null)
        {
            var note = Glossary.Plain(_body, _message, 18, Palette.Texto);
            note.Name = "MensagemOpcoes";
        }

        Glossary.Rich(_body, Tr("As opções ficam gravadas neste computador, fora do jogo salvo, e valem para todas as carreiras."), 18, Palette.Texto3);
    }

    private void Heading(string text)
    {
        var label = Ui.Value(_body, text, 22);
        label.AddThemeFontOverride("font", Palette.Weight(Palette.Inter, 600));
        label.AddThemeColorOverride("font_color", Palette.Texto);
    }
}
