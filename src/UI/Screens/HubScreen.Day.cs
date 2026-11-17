using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.State;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Leitura mínima (RL): o relógio do dia e a linha do dia na barra de cima, com os três turnos, o compromisso em
/// andamento e os próximos, e a virada do dia visível. A hora vem do Core; a tela só escreve.
/// </summary>
public partial class HubScreen
{
    private Label? _clock;
    private ColorRect? _needle;
    private DateOnly? _shownDay;
    private string _clockText = "";

    /// <summary>Hora do relógio que a barra de cima mostra (testes de fumaça conferem que ela anda).</summary>
    public string ClockText => _clock?.Text ?? "";

    /// <summary>Posição do marcador da hora na linha do dia, de 0 a 1.</summary>
    public float NeedleFraction => _needle?.AnchorLeft ?? 0;

    /// <summary>Texto da última virada de dia mostrada sobre o mapa, se houver.</summary>
    public string? DayTurnText { get; private set; }

    private string HourText()
    {
        var (hour, minute) = Clock.TimeOfDay(_game.Time, State);
        return $"{hour:00}:{minute:00}";
    }

    /// <summary>Anda o relógio e o marcador da linha do dia; chamado a cada quadro só com o tempo correndo.</summary>
    private void UpdateClock()
    {
        if (_clock is null || !IsInstanceValid(_clock))
        {
            return;
        }

        string text = HourText();
        if (text != _clockText)
        {
            _clockText = text;
            _clock.Text = text;
        }

        if (_needle is not null && IsInstanceValid(_needle))
        {
            float fraction = (float)Clock.DayFraction(_game.Time, State);
            _needle.AnchorLeft = fraction;
            _needle.AnchorRight = fraction;
        }
    }

    /// <summary>Um turno terminou sem compromisso: só a linha do dia e o painel Você mudam.</summary>
    private void RefreshDayPanels()
    {
        BuildDayLine();
        BuildYou();
    }

    private string SlotLabel(TurnoSlot slot)
    {
        string turno = TurnoName(slot.Turno);
        turno = char.ToUpper(turno[0], PtBr) + turno[1..];
        return slot.Situacao switch
        {
            TurnoSituacao.Marcado => $"{turno} · {CommitmentTitle(slot.Commitment!)}",
            TurnoSituacao.EmAndamento => $"{turno} · {Tr("agora")}: {CommitmentTitle(slot.Commitment!)}",
            TurnoSituacao.Feito => $"{turno} · {Tr("feito")}: {CommitmentTitle(slot.Commitment!)}",
            TurnoSituacao.Passou => $"{turno} · {Tr("passou")}",
            _ => slot.CityEvent is not null ? $"{turno} · {Tr("livre")} ({Tr("evento")}: {slot.CityEvent.Nome})" : $"{turno} · {Tr("livre")}",
        };
    }

    private string SlotTip(TurnoSlot slot)
    {
        string hours = string.Format(PtBr, Tr("{0}, das {1}h às {2}h"), TurnoName(slot.Turno), slot.HoraInicio, slot.HoraFim);
        return slot.Situacao switch
        {
            TurnoSituacao.Marcado => Palette.Tip(string.Format(Tr("Marcado para a {0}: {1}."), hours, CommitmentTitle(slot.Commitment!)), Tr("o compromisso acontece quando o relógio chega ao fim do turno; o resultado vai para o noticiário e para o mapa. Clique para ver a previsão ou desmarcar."), CostText(Catalog.Get(slot.Commitment!.Type))),
            TurnoSituacao.EmAndamento => Palette.Tip(string.Format(Tr("Em andamento, {0}: {1}."), hours, CommitmentTitle(slot.Commitment!)), Tr("acontece quando o relógio chegar ao fim do turno; clique para ver a previsão."), CostText(Catalog.Get(slot.Commitment!.Type))),
            TurnoSituacao.Feito => Palette.Tip(string.Format(Tr("Feito, {0}: {1}."), hours, CommitmentTitle(slot.Commitment!)), Tr("clique para ver o resultado explicado: o que mudou, onde e por quê."), Tr("nada; já aconteceu.")),
            TurnoSituacao.Passou => Palette.Tip(string.Format(Tr("A {0} passou sem compromisso seu."), hours), Tr("o turno que passa vazio não volta."), Tr("nada.")),
            _ => Palette.Tip(
                string.Format(Tr("Turno livre: {0}."), hours) + (slot.CityEvent is { } e ? " " + string.Format(Tr("Evento da cidade nesse turno: {0}, em {1}."), e.Nome, ZoneName(e.ZoneId)) : ""),
                Tr("abre a marcação nesse turno: você escolhe o compromisso, a zona e o público e vê a previsão antes de confirmar."),
                Tr("nada para abrir; o custo de cada compromisso aparece na previsão.")),
        };
    }

    /// <summary>Linha do dia: relógio, os três turnos (com o marcador da hora) e o próximo compromisso.</summary>
    private void BuildDayLine()
    {
        if (_gov)
        {
            _dayRow.Visible = false;
            return;
        }

        _dayRow.Visible = true;
        Ui.Clear(_dayRow);
        DateOnly today = _engine.Today;

        _clock = Ui.Value(_dayRow, HourText(), 34);
        _clock.Name = "Relogio";
        _clock.AddThemeFontOverride("font", Palette.Weight(Palette.Oswald, 600));
        _clock.AddThemeColorOverride("font_color", Palette.Texto);
        _clock.CustomMinimumSize = new Vector2(104, 44);
        _clock.VerticalAlignment = VerticalAlignment.Center;
        _clock.MouseFilter = MouseFilterEnum.Stop;
        _clock.TooltipText = Palette.Tip(
            string.Format(PtBr, Tr("Relógio do jogo: o dia de campanha vai das {0}h às {1}h."), _game.Time.Dia.HoraInicio, _game.Time.Dia.HoraFim),
            string.Format(PtBr, Tr("anda com o tempo: o dia inteiro leva {0:0.#} s em 1x; cada compromisso acontece no fim do seu turno, e às {1}h o dia vira."), Clock.SecondsPerDay(_game.Time, Clock.ModoCampanha, _game.Time.Velocidades[0]), _game.Time.Dia.HoraFim),
            Tr("nada; pause para planejar com calma."));
        _clockText = _clock.Text;

        var line = _dayRow.Add(new Control { Name = "Turnos", SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 44) });
        var segments = line.Add(new HBoxContainer());
        segments.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        segments.AddThemeConstantOverride("separation", 2);
        foreach (TurnoSlot slot in _engine.DayLine(today))
        {
            DayLineSegment(segments, slot);
        }

        // Marcador da hora: corre por cima dos turnos (cor de texto; a de atenção fica para o que pede decisão).
        _needle = line.Add(new ColorRect { Name = "Agulha", Color = Palette.Texto, MouseFilter = MouseFilterEnum.Ignore });
        _needle.AnchorTop = 0;
        _needle.AnchorBottom = 1;
        _needle.OffsetLeft = -1;
        _needle.OffsetRight = 2;
        _needle.OffsetTop = -3;
        _needle.OffsetBottom = 3;
        float fraction = (float)Clock.DayFraction(_game.Time, State);
        _needle.AnchorLeft = fraction;
        _needle.AnchorRight = fraction;

        // Próximos compromissos depois de hoje (os de hoje já estão na linha).
        var upcoming = _engine.UpcomingCommitments(4).Where(c => c.Date > today).Take(3).ToList();
        string next = upcoming.Count == 0 ? Tr("Depois de hoje: nada marcado") : string.Format(Tr("Depois: {0}, {1} · {2}"), DayShort(upcoming[0].Date), TurnoName(upcoming[0].Turno), CommitmentTitle(upcoming[0]));
        Label after = Ui.Value(_dayRow, next, 18);
        after.Name = "Proximos";
        after.AddThemeColorOverride("font_color", Palette.Texto2);
        after.CustomMinimumSize = new Vector2(384, 44);
        after.VerticalAlignment = VerticalAlignment.Center;
        after.ClipText = true;
        after.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        after.MouseFilter = MouseFilterEnum.Stop;
        after.TooltipText = Palette.Tip(
            upcoming.Count == 0 ? Tr("Nenhum compromisso marcado depois de hoje.") : Tr("Próximos compromissos:") + " " + string.Join("; ", upcoming.Select(c => $"{DayShort(c.Date)}, {TurnoName(c.Turno)}: {CommitmentTitle(c)}")) + ".",
            Tr("cada um acontece no fim do seu turno; a Agenda mostra a semana inteira."),
            Tr("nada."));

        if (_shownDay is { } shown && shown != today)
        {
            ShowDayTurn(today);
        }

        _shownDay = today;
    }

    private void DayLineSegment(Node parent, TurnoSlot slot)
    {
        var b = parent.Add(new Button
        {
            Name = $"Linha_{(int)slot.Turno}",
            Text = SlotLabel(slot),
            TooltipText = SlotTip(slot),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsStretchRatio = Math.Max(1, slot.HoraFim - slot.HoraInicio),
            CustomMinimumSize = new Vector2(0, 44),
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            Alignment = HorizontalAlignment.Left,
        });
        (Color background, Color border, Color text, int width) = slot.Situacao switch
        {
            TurnoSituacao.Marcado => (new Color(Palette.Acao, 0.2f), Palette.Acao, Palette.Texto, 1),
            TurnoSituacao.EmAndamento => (new Color(Palette.Acao, 0.38f), Palette.Texto, Palette.Texto, 2),
            TurnoSituacao.Feito => (Palette.Cartao, Palette.Linha, Palette.Texto3, 1),
            TurnoSituacao.Passou => (new Color(Palette.Cartao, 0.4f), new Color(Palette.Linha, 0.5f), Palette.Texto3, 1),
            _ => (Palette.Cartao, Palette.Texto3, Palette.Texto, 1),
        };
        b.AddThemeStyleboxOverride("normal", Palette.Box(background, 6, 8, border, width));
        b.AddThemeStyleboxOverride("hover", Palette.Box(background.Lightened(0.08f), 6, 8, border, width));
        b.AddThemeStyleboxOverride("pressed", Palette.Box(background.Darkened(0.08f), 6, 8, border, width));
        b.AddThemeStyleboxOverride("disabled", Palette.Box(background, 6, 8, border, width));
        foreach (string color in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_disabled_color" })
        {
            b.AddThemeColorOverride(color, text);
        }

        switch (slot.Situacao)
        {
            case TurnoSituacao.Marcado or TurnoSituacao.EmAndamento:
                string id = slot.Commitment!.Id;
                b.Pressed += () => OpenSheet(Sheet.Compromisso, id);
                break;
            case TurnoSituacao.Feito:
                string done = slot.Commitment!.Id;
                b.Pressed += () => OpenSheet(Sheet.Compromisso, done);
                break;
            case TurnoSituacao.Livre:
                CityEventInstance? cityEvent = slot.CityEvent;
                b.Pressed += () => StartSchedule(slot.Date, slot.Turno, cityEvent: cityEvent);
                if (BlockReason() is { } block)
                {
                    b.Disabled = true;
                    b.TooltipText = Palette.Blocked(string.Format(Tr("{0}; resolva antes de marcar."), block), b.TooltipText);
                }

                break;
            default:
                b.Disabled = true;
                b.TooltipText = Palette.Blocked(Tr("esse turno já passou; o próximo livre está à direita na linha ou na Agenda."), b.TooltipText);
                break;
        }
    }

    /// <summary>Virada do dia visível: uma faixa curta no alto do mapa com o dia que começa.</summary>
    private void ShowDayTurn(DateOnly today)
    {
        DayTurnText = string.Format(Tr("Fim do dia · começa {0}"), DayLong(today));
        var toast = _mapArea.Add(new PanelContainer { Name = "ViradaDoDia", MouseFilter = MouseFilterEnum.Ignore });
        toast.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 8, 12, Palette.Texto2));
        toast.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop);
        toast.GrowHorizontal = GrowDirection.Both;
        toast.OffsetTop = 76;
        Label text = Ui.Value(toast, DayTurnText, 20);
        text.AddThemeFontOverride("font", Palette.Weight(Palette.Oswald, 500));
        text.AddThemeColorOverride("font_color", Palette.Texto);
        Tween tween = toast.CreateTween();
        tween.TweenInterval(1.4);
        tween.TweenProperty(toast, "modulate:a", 0.0, 0.4);
        tween.TweenCallback(Callable.From(toast.QueueFree));
    }
}
