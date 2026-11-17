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
/// Leitura mínima (RL): a ficha da zona flutua encaixada no painel da esquerda, sem cobrir o mapa, numa pilha de
/// janelas (só a de cima recebe clique, Esc
/// volta, foco preso na janela modal), com "Fazer agora" como ação principal; o número flutuante sobe da zona depois
/// da ação; retratos pelo cache; e os painéis novos se atualizam pelo evento Changed do Core.
/// </summary>
public partial class HubScreen
{
    private sealed record FloatingWindow(Control Window, bool Modal, Action Close);

    private readonly List<FloatingWindow> _windows = [];
    private readonly List<Control> _windowLayers = [];
    private bool _panelsDirty;
    private string? _nowZone;
    private string _nowType = "caminhada";
    private string? _nowPublico;
    private string? _nowPerson;

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

    /// <summary>
    /// Monta as janelas flutuantes do estado atual: a ficha da zona, encaixada no painel da esquerda por cima do
    /// conteúdo da área (o mapa fica livre e mostra o resultado do "Fazer agora"), e, por cima dela, a marcação, modal
    /// sobre o mapa.
    /// </summary>
    private void BuildWindows()
    {
        _windows.Clear();
        foreach (Control layer in _windowLayers.Where(IsInstanceValid))
        {
            layer.GetParent()?.RemoveChild(layer);
            layer.QueueFree();
        }

        _windowLayers.Clear();
        string? zone = ZoneFloats ? _sheetId : ScheduleFloats ? _back.Peek().Id : null;
        if (_gov || zone is null || !_map.Zonas.TryGetValue(zone, out Zona? z))
        {
            return;
        }

        PushWindow(Layer(_leftStack, "Janelas"), "FichaZona", false, Back, () => BuildZoneSheet(z));
        Control? mapLayer = null;
        if (ScheduleFloats)
        {
            Draft draft = _draft!;
            mapLayer = Layer(_mapArea, "JanelasMapa");
            PushWindow(mapLayer, "JanelaAgendar", true, Back, () => BuildScheduleSheet(draft));
        }

        // Só a de cima recebe clique: as de baixo ficam escurecidas atrás de um bloqueio; a modal bloqueia o mapa.
        for (int i = 0; i < _windows.Count - 1; i++)
        {
            var cover = _windows[i].Window.Add(new ColorRect { Name = "Bloqueio", Color = new Color(Palette.Fundo, 0.5f), MouseFilter = MouseFilterEnum.Stop });
            cover.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        FloatingWindow top = _windows[^1];
        if (top.Modal && mapLayer is not null)
        {
            var under = mapLayer.Add(new ColorRect { Name = "FundoModal", Color = new Color(Palette.Fundo, 0.3f), MouseFilter = MouseFilterEnum.Stop });
            under.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            mapLayer.MoveChild(under, top.Window.GetIndex());
            Callable.From(() => FocusFirst(top.Window)).CallDeferred();
        }
    }

    private Control Layer(Control parent, string name)
    {
        var layer = parent.Add(new Control { Name = name, MouseFilter = MouseFilterEnum.Ignore });
        layer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _windowLayers.Add(layer);
        return layer;
    }

    /// <summary>
    /// Janela flutuante com o conteúdo de uma ficha do painel (montada no lugar de _left). A primeira ocupa a camada
    /// inteira (o painel da esquerda, sob o Você); a de cima fica sobre o mapa, à esquerda, com 420 de largura.
    /// </summary>
    private void PushWindow(Control layer, string name, bool modal, Action close, Action build)
    {
        int level = _windows.Count;
        var window = layer.Add(new PanelContainer { Name = name, MouseFilter = MouseFilterEnum.Stop });
        window.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Painel, 12, level == 0 ? 12 : 16, modal ? Palette.Texto3 : Palette.Linha));
        if (level == 0)
        {
            // Encaixada no painel: passa 8 px para dentro da margem dele, e o conteúdo fica com a largura da área.
            window.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            window.OffsetLeft = -8;
            window.OffsetRight = 8;
        }
        else
        {
            window.AnchorLeft = window.AnchorRight = 0;
            window.AnchorTop = 0;
            window.AnchorBottom = 1;
            window.OffsetLeft = 16;
            window.OffsetRight = window.OffsetLeft + 420;
            window.OffsetTop = 72;
            window.OffsetBottom = -16;
        }

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

    // ------------------------------------------------------------------ Fazer agora

    private static string TurnoPhrase(Turno turno) => turno switch
    {
        Turno.Manha => TranslationServer.Translate("de manhã"),
        Turno.Tarde => TranslationServer.Translate("à tarde"),
        _ => TranslationServer.Translate("à noite"),
    };

    /// <summary>O pedido do "Fazer agora" numa zona: o tipo escolhido, o público e a liderança que ele pede.</summary>
    private CommitmentRequest NowRequest(string zoneId)
    {
        if (_nowZone != zoneId)
        {
            _nowZone = zoneId;
            _nowPublico = null;
            _nowPerson = null;
        }

        CommitmentType type = Catalog.Get(_nowType);
        string publico = CommitmentType.PublicoGeral;
        if (type.EscolhePublico)
        {
            // Sem escolha ainda: o público de uma liderança conhecida e não hostil da zona (quem abre a casa), senão o maior.
            publico = _nowPublico
                ?? _engine.City.Leaders.FirstOrDefault(l => l.Known && l.Zones.Contains(zoneId) && _engine.RelationOf(l.PersonId) >= 0)?.Publico
                ?? PublicOptions(zoneId).Select(p => p.Id).FirstOrDefault()
                ?? CommitmentType.PublicoGeral;
        }

        string? person = type.Visita ? _nowPerson ?? _engine.City.Leaders.FirstOrDefault(l => l.Known && l.Zones.Contains(zoneId))?.PersonId : null;
        return new CommitmentRequest(type.Id, zoneId, _engine.Today, Turno.Manha, publico, person);
    }

    /// <summary>Texto do botão: "Fazer agora" hoje; "Fazer amanhã de manhã" quando hoje não há turno livre.</summary>
    private static string NowLabel(NowPlan plan) =>
        plan.Tomorrow ? string.Format(TranslationServer.Translate("Fazer amanhã {0}"), TurnoPhrase(plan.Turno)) : TranslationServer.Translate("Fazer agora");

    private string NowWhen(NowPlan plan)
    {
        TurnoHours hours = _game.Time.Dia.Of(plan.Turno);
        return plan.Tomorrow
            ? string.Format(PtBr, Tr("Quando: amanhã {0} ({1}h às {2}h). Hoje não há turno livre; o dia só vira porque você escolheu."), TurnoPhrase(plan.Turno), hours.Inicio, hours.Fim)
            : string.Format(PtBr, Tr("Quando: hoje {0} ({1}h às {2}h); o tempo anda até o fim do turno e o resultado aparece no mapa."), TurnoPhrase(plan.Turno), hours.Inicio, hours.Fim);
    }

    /// <summary>
    /// Bloco de ação da ficha da zona (RL): o tipo, a previsão do Core no turno em que cabe e "Fazer agora" (ou
    /// "Fazer amanhã de manhã"); agendar para outro dia fica como opção secundária logo abaixo.
    /// </summary>
    private void ZoneNowBlock(Zona z)
    {
        if (_gov)
        {
            return;
        }

        PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 12);
        card.Name = "AgirAqui";
        VBoxContainer col = Ui.Column(card, 8);
        Heading(col, Tr("Agir aqui"), 20);
        var types = Catalog.Tipos.Where(t => t.ExigeZona && !t.SoEvento).Select(t => (t.Id, $"{t.Nome} · {ShortCost(t)}")).ToList();
        if (types.All(t => t.Id != _nowType))
        {
            _nowType = types[0].Id;
        }

        OptionButton typeOptions = Ui.Options(col, types, _nowType, id => { _nowType = id; _nowPerson = null; Refresh(); }, translate: false);
        typeOptions.Name = "AgoraTipo";
        typeOptions.ClipText = true;
        typeOptions.CustomMinimumSize = new Vector2(0, 44);
        typeOptions.TooltipText = Palette.Tip(Tr("O que fazer nesta zona."), Tr("muda a previsão abaixo: presença, apoio, relação ou imagem, com a faixa do Core."), Tr("o custo de cada tipo está ao lado do nome."));

        CommitmentRequest request = NowRequest(z.Id);
        CommitmentType type = Catalog.Get(request.Type);
        if (type.EscolhePublico)
        {
            OptionButton publico = Ui.Options(col, PublicOptions(z.Id), request.Publico, id => { _nowPublico = id; Refresh(); }, translate: false);
            publico.Name = "AgoraPublico";
            publico.ClipText = true;
            publico.CustomMinimumSize = new Vector2(0, 44);
            publico.TooltipText = Palette.Tip(Tr("Público da reunião."), Tr("o apoio vai para esse grupo de eleitores da zona; precisa de alguém dele que abra a casa."), Tr("nada."));
        }

        if (type.Visita)
        {
            var people = _engine.City.Leaders.Where(l => l.Known && l.Zones.Contains(z.Id)).Select(l => (l.PersonId, LeaderLabel(l))).ToList();
            if (people.Count > 0)
            {
                OptionButton person = Ui.Options(col, people, request.PersonId, id => { _nowPerson = id; Refresh(); }, translate: false);
                person.Name = "AgoraPessoa";
                person.ClipText = true;
                person.CustomMinimumSize = new Vector2(0, 44);
                person.TooltipText = Palette.Tip(Tr("Quem visitar."), Tr("a visita sobe a relação e revela o pedido da liderança."), Tr("nada."));
            }
        }

        NowPlan plan = _engine.PlanNow(request);
        CommitmentRequest at = request with { Date = plan.Date, Turno = plan.Turno };
        var forecast = col.Add(new PanelContainer { Name = "PrevisaoAgora" });
        forecast.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Fundo, 8, 10, Palette.Linha));
        VBoxContainer lines = Ui.Column(forecast, 4);
        Glossary.Rich(lines, NowWhen(plan), 18, Palette.Texto);
        if (plan.Forecast is { } f)
        {
            Glossary.Rich(lines, ForecastSummary(at, f) + ".", 18, Palette.Texto2);
            int extra = f.Stress - type.Estresse;
            if (extra > 0)
            {
                // O custo do turno além dos que não custam vem do Core (regras da agenda em data/).
                Glossary.Rich(lines, string.Format(PtBr, Tr("Turno além dos {0} sem custo no dia: {1} de estresse."), Catalog.Regras.TurnosSemCusto, Signed(extra, "0")), 18, Palette.Texto).Name = "EstresseTurno";
            }
        }

        string label = NowLabel(plan);
        string tip = Palette.Tip(
            string.Format(Tr("{0}: {1} em {2}."), label, type.Nome, z.Nome),
            NowWhen(plan) + (plan.Forecast is { } pf ? " " + ForecastSummary(at, pf) + "." : ""),
            CostText(type));
        Button now = Ui.Button(col, label, () => DoNow(request), tip);
        now.Name = "FazerAgora";
        now.CustomMinimumSize = new Vector2(0, 52);
        Palette.Primary(now);
        string? why = plan.Failure is { } failure ? FailureText(failure, at)
            : BlockReason() is { } block ? string.Format(Tr("{0}; resolva antes."), block)
            : null;
        if (why is not null)
        {
            now.Disabled = true;
            now.TooltipText = Palette.Blocked(why, tip);
        }
        else if (!plan.Tomorrow && Clock.CurrentTurno(_game.Time, State) is { } current && current != plan.Turno)
        {
            why = string.Format(Tr("O turno de agora está ocupado: a ação será feita {0}."), TurnoPhrase(plan.Turno));
        }

        if (why is not null)
        {
            Glossary.Rich(col, why, 18, Palette.Texto).Name = "PorQueAgora";
        }
    }

    /// <summary>Executa o "Fazer agora" pelo Core e mostra o resultado no mapa.</summary>
    private void DoNow(CommitmentRequest request)
    {
        NowResult result = _game.DoNow(request);
        if (_game.Engine is null || State.Campaign is null)
        {
            _game.GoToCareer();
            return;
        }

        if (result.Results.Count > 0)
        {
            foreach (CommitmentResult r in result.Results)
            {
                _log.Insert(0, string.Format(Tr("Feito: {0}."), CommitmentTitle(r.Commitment)));
            }

            ShowResultFloats(result.Results);
        }
        else if (result.Commitment is { } c)
        {
            _log.Insert(0, string.Format(Tr("Marcado: {0}, {1}, {2}; acontece quando o tempo voltar a correr."), CommitmentTitle(c), DayShort(c.Date), TurnoName(c.Turno)));
        }

        Refresh();
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

    // ------------------------------------------------------------------ retratos pelo cache

    /// <summary>Aparência e roupa de quem aparece num cartão: você, um candidato (pelo personagem) ou outra pessoa.</summary>
    private (Appearance Appearance, PortraitOptions Options)? PortraitSource(string? id)
    {
        if (id is null)
        {
            return null;
        }

        if (id == RaceSetup.PlayerId)
        {
            GameParty? mine = State.Party(State.Player.PartyId ?? "");
            return State.Player.Appearance is { } own
                ? (own, mine is null ? new PortraitOptions() : new PortraitOptions(Planalto.Core.Characters.Expression.Neutra, WardrobeContext.Campanha, mine.Colors.Primary, "background_solido", mine.Colors.Secondary))
                : null;
        }

        Candidate? candidate = _gov ? null : Campaign.Find(id);
        string characterId = candidate?.CharacterId ?? id;
        Appearance? appearance = State.Npcs.FirstOrDefault(n => n.Id == characterId)?.Appearance;
        if (appearance is null)
        {
            return null;
        }

        GameParty? party = candidate is null ? null : State.Party(candidate.PartyId);
        return (appearance, party is null ? new PortraitOptions() : new PortraitOptions(Planalto.Core.Characters.Expression.Neutra, WardrobeContext.Campanha, party.Colors.Primary, "background_solido", party.Colors.Secondary));
    }

    /// <summary>Retrato pelo PortraitCache (96 px, mostrado no tamanho pedido); a textura chega no quadro seguinte.</summary>
    private TextureRect? CachedPortrait(Node parent, string? id, int size)
    {
        if (PortraitSource(id) is not { } source || PortraitCache.Instance is null)
        {
            return null;
        }

        var rect = parent.Add(new TextureRect
        {
            Name = "Retrato",
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
        });
        _ = FillPortrait(rect, source.Appearance, source.Options);
        return rect;
    }

    private static async Task FillPortrait(TextureRect rect, Appearance appearance, PortraitOptions options)
    {
        Texture2D texture = await PortraitCache.Instance.GetAsync(appearance, options, 96);
        if (IsInstanceValid(rect))
        {
            rect.Texture = texture;
        }
    }
}
