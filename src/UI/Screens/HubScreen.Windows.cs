using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.UI.Portrait;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Leitura mínima (RL): a ficha da zona flutua sobre o mapa numa pilha de janelas (só a de cima recebe clique, Esc
/// volta, foco preso na janela modal), com "Fazer agora" como ação principal; o número flutuante sobe da zona depois
/// da ação; retratos pelo cache; e os painéis novos se atualizam pelo evento Changed do Core.
/// </summary>
public partial class HubScreen
{
    private sealed record FloatingWindow(Control Window, bool Modal, Action Close);

    private readonly List<FloatingWindow> _windows = [];
    private Control? _windowLayer;
    private bool _panelsDirty;

    /// <summary>Janelas abertas sobre o mapa, de baixo para cima (testes de fumaça).</summary>
    public IReadOnlyList<string> WindowNames => _windows.Select(w => w.Window.Name.ToString()).ToList();

    /// <summary>Janela de cima, que recebe os cliques e o Esc.</summary>
    public Control? TopWindow => _windows.Count == 0 ? null : _windows[^1].Window;

    /// <summary>Quantas vezes os painéis novos foram refeitos pelo evento do Core (testes de fumaça).</summary>
    public int PanelUpdatesFromCore { get; private set; }

    /// <summary>A marcação abriu a partir da ficha da zona: flutua por cima dela, modal.</summary>
    private bool ScheduleFloats => _sheet == Sheet.Marcar && _draft is not null && _back.Count > 0 && _back.Peek().Sheet == Sheet.Zona;

    private bool ZoneFloats => _sheet == Sheet.Zona && _sheetId is not null && _map.Zonas.ContainsKey(_sheetId);

    // ------------------------------------------------------------------ eventos do Core

    private void OnCampaignChanged() => _panelsDirty = true;

    /// <summary>Refaz só os painéis que o evento do Core marcou (linha do dia, Você e, aberto, o painel de zonas).</summary>
    private void RefreshFromCore()
    {
        _panelsDirty = false;
        PanelUpdatesFromCore++;
        BuildDayLine();
        BuildYou();
        if (_area == "territorio" && _sheet is Sheet.Nenhuma or Sheet.Zona)
        {
            BuildLeft();
        }
    }

    // ------------------------------------------------------------------ pilha de janelas

    /// <summary>Monta as janelas flutuantes do estado atual: a ficha da zona e, por cima dela, a marcação.</summary>
    private void BuildWindows()
    {
        _windows.Clear();
        if (_windowLayer is not null && IsInstanceValid(_windowLayer))
        {
            _windowLayer.GetParent()?.RemoveChild(_windowLayer);
            _windowLayer.QueueFree();
        }

        _windowLayer = null;
        string? zone = ZoneFloats ? _sheetId : ScheduleFloats ? _back.Peek().Id : null;
        if (_gov || zone is null || !_map.Zonas.TryGetValue(zone, out Zona? z))
        {
            return;
        }

        _windowLayer = _mapArea.Add(new Control { Name = "Janelas", MouseFilter = MouseFilterEnum.Ignore });
        _windowLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        PushWindow("FichaZona", false, Back, () => BuildZoneSheet(z));
        if (ScheduleFloats)
        {
            Draft draft = _draft!;
            PushWindow("JanelaAgendar", true, Back, () => BuildScheduleSheet(draft));
        }

        // Só a de cima recebe clique: as de baixo ficam escurecidas atrás de um bloqueio; a modal bloqueia o mapa.
        for (int i = 0; i < _windows.Count - 1; i++)
        {
            var cover = _windows[i].Window.Add(new ColorRect { Name = "Bloqueio", Color = new Color(Palette.Fundo, 0.5f), MouseFilter = MouseFilterEnum.Stop });
            cover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        FloatingWindow top = _windows[^1];
        if (top.Modal)
        {
            var under = _windowLayer.Add(new ColorRect { Name = "FundoModal", Color = new Color(Palette.Fundo, 0.3f), MouseFilter = MouseFilterEnum.Stop });
            under.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _windowLayer.MoveChild(under, top.Window.GetIndex());
            Callable.From(() => FocusFirst(top.Window)).CallDeferred();
        }
    }

    /// <summary>Janela flutuante sobre o mapa, com o conteúdo de uma ficha do painel (montada no lugar de _left).</summary>
    private void PushWindow(string name, bool modal, Action close, Action build)
    {
        int level = _windows.Count;
        var window = _windowLayer!.Add(new PanelContainer { Name = name, MouseFilter = MouseFilterEnum.Stop });
        window.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Painel, 12, 16, modal ? Palette.Texto3 : Palette.Linha));
        window.AnchorLeft = window.AnchorRight = 0;
        window.AnchorTop = 0;
        window.AnchorBottom = 1;
        window.OffsetLeft = 16 + (level * 40);
        window.OffsetRight = window.OffsetLeft + 420;
        window.OffsetTop = 72 + (level * 40);
        window.OffsetBottom = -16;
        var scroll = window.Add(new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        var content = scroll.Add(new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        content.AddThemeConstantOverride("separation", 8);
        VBoxContainer saved = _left;
        _left = content;
        try
        {
            build();
        }
        finally
        {
            _left = saved;
        }

        _windows.Add(new FloatingWindow(window, modal, close));
    }

    private static void FocusFirst(Control root)
    {
        if (IsInstanceValid(root) && root.IsInsideTree() && FirstFocusable(root) is { } first)
        {
            first.GrabFocus();
        }
    }

    private static Control? FirstFocusable(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Control { FocusMode: FocusModeEnum.All } c && c.IsVisibleInTree() && c is not BaseButton { Disabled: true })
            {
                return c;
            }

            if (FirstFocusable(child) is { } inner)
            {
                return inner;
            }
        }

        return null;
    }

    /// <summary>Foco preso na janela modal: o que sair dela volta para o primeiro controle de dentro.</summary>
    private void OnFocusChanged(Control focused)
    {
        if (_windows.Count > 0 && _windows[^1] is { Modal: true } top && IsInstanceValid(top.Window) && top.Window != focused && !top.Window.IsAncestorOf(focused))
        {
            Callable.From(() => FocusFirst(top.Window)).CallDeferred();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape } && _windows.Count > 0 && !EventOpen)
        {
            GetViewport().SetInputAsHandled();
            CloseTopWindow();
        }
    }

    /// <summary>Fecha a janela de cima (o mesmo que Esc): a de baixo volta a receber cliques.</summary>
    public void CloseTopWindow()
    {
        if (_windows.Count > 0)
        {
            _windows[^1].Close();
        }
    }

    /// <summary>Número flutuante (RL): "+2,1 presença" na zona do compromisso e o transbordo nas vizinhas.</summary>
    private void ShowResultFloats(IEnumerable<CommitmentResult> results)
    {
        foreach (CommitmentResult r in results)
        {
            if (r.Commitment.ZoneId is not { } zone || !r.Presence.TryGetValue(zone, out double here))
            {
                continue;
            }

            _map.ShowFloat(zone, $"{Signed(here, "0.0")} {Tr("presença")}", false, 0);
            int i = 0;
            foreach (var (neighbor, delta) in r.Presence.Where(p => p.Key != zone && Math.Abs(p.Value) >= 0.05))
            {
                _map.ShowFloat(neighbor, Signed(delta, "0.0"), true, 0.25 + (0.12 * i++));
            }
        }
    }
}
