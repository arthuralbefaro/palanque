using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.State;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Área Agenda (GDD 17.6): a semana em sete dias e três turnos, com compromissos marcados, eventos da cidade,
/// chuva prevista e convites; a marcação com a previsão do Core antes de confirmar; o detalhe de um compromisso.
/// </summary>
public partial class HubScreen
{
    /// <summary>Compromisso em preparo na ficha de marcação.</summary>
    private sealed class Draft
    {
        public DateOnly Date { get; set; }

        public Turno Turno { get; set; }

        public string Type { get; set; } = "caminhada";

        public string? ZoneId { get; set; }

        public string Publico { get; set; } = CommitmentType.PublicoGeral;

        public string? PersonId { get; set; }

        public string? CityEventId { get; set; }

        /// <summary>Um representante vai no lugar do candidato (GDD 17.6, "Delegar").</summary>
        public bool Delegado { get; set; }

        /// <summary>Tema de que o candidato vai falar (entrevista, gravação, comício).</summary>
        public string? Tema { get; set; }

        /// <summary>Veículo escolhido para a entrevista (GDD 17.13); null deixa a produção escolher.</summary>
        public string? OutletId { get; set; }
    }

    private int _weekOffset;
    private Draft? _draft;

    private CommitmentCatalog Catalog => _engine.Context.Campaign.Commitments;

    private CommitmentType TypeOf(Draft d) => Catalog.Get(d.Type);

    private string LeaderLabel(Leader leader) => $"{_engine.NameOf(leader.PersonId)} ({_engine.Context.Campaign.Leaders.Type(leader.Type).Papel})";

    /// <summary>O pedido ao Core, só com os campos que o tipo usa.</summary>
    private CommitmentRequest RequestOf(Draft d)
    {
        CommitmentType type = TypeOf(d);
        string? person = type.Visita || type.Rua ? d.PersonId : null;
        string? zone = type.ExigeZona ? d.ZoneId
            : type.Visita && person is not null && _engine.City.Leader(person) is { } visited ? visited.Zones[0]
            : null;
        bool delegated = d.Delegado && type.Delegavel;
        string? theme = !delegated && _engine.SpeaksOfThemes(type.Id) ? d.Tema : null;
        string? outlet = !delegated && type.Id == CommitmentCatalog.Entrevista && d.OutletId is not null && _engine.City.Outlet(d.OutletId) is not null ? d.OutletId : null;
        return new CommitmentRequest(d.Type, zone, d.Date, d.Turno, type.EscolhePublico ? d.Publico : CommitmentType.PublicoGeral, person, type.SoEvento ? d.CityEventId : null, Delegado: delegated, Tema: theme, OutletId: outlet);
    }

    /// <summary>"Caminhada em Centro", "Visita a Fulana (presidente de associação rural)".</summary>
    private string CommitmentTitle(Commitment c)
    {
        CommitmentType type = Catalog.Get(c.Type);
        if (type.Visita && c.PersonId is not null && _engine.City.Leader(c.PersonId) is { } leader)
        {
            return string.Format(Tr("Visita a {0}"), LeaderLabel(leader));
        }

        string title = c.ZoneId is null ? type.Nome : string.Format(Tr("{0} em {1}"), type.Nome, ZoneName(c.ZoneId));
        return c.Delegado ? string.Format(Tr("{0} (com representante)"), title) : title;
    }

    /// <summary>Quanto custa um tipo de compromisso, para a terceira parte do tooltip.</summary>
    private string CostText(CommitmentType type)
    {
        decimal cost = _engine.CostOf(type);
        string money = cost > 0 ? Money(cost) : cost < 0 ? string.Format(Tr("rende cerca de {0}"), Money(-cost)) : Tr("nada em dinheiro");
        return string.Format(Tr(type.Turnos > 1 ? "{0}; ocupa {1} turnos seguidos do dia." : "{0}; ocupa {1} turno do dia."), money, type.Turnos);
    }

    private string ShortCost(CommitmentType type)
    {
        decimal cost = _engine.CostOf(type);
        return cost > 0 ? Money(cost) : cost < 0 ? "+" + Money(-cost) : Tr("sem custo");
    }

    // ------------------------------------------------------------------ a semana

    private void BuildAgendaArea()
    {
        DateOnly today = _engine.Today;
        int maxOffset = Catalog.Regras.AntecedenciaDias / 7;
        _weekOffset = Math.Clamp(_weekOffset, 0, maxOffset);
        DateOnly start = State.Calendar.WeekStart.AddDays(7 * _weekOffset);
        DateOnly end = start.AddDays(6);
        string title = _weekOffset switch
        {
            0 => Tr("Esta semana"),
            1 => Tr("Semana que vem"),
            _ => string.Format(Tr("Daqui a {0} semanas"), _weekOffset),
        };
        Glossary.Rich(_left, $"{title} · {start.ToString("d/MM", PtBr)} a {end.ToString("d/MM", PtBr)}. " + string.Format(Tr("Hoje é {0}."), DayLong(today)), 18, Palette.Texto);

        HBoxContainer nav = Ui.Row(_left, 8);
        string previousTip = Palette.Tip(Tr("Mostra a semana anterior da agenda."), Tr("muda só o que o painel mostra; o tempo não anda."), Tr("nada."));
        var previous = Ui.Button(nav, "Semana anterior", () => { _weekOffset--; Refresh(); }, previousTip);
        previous.Name = "SemanaAnterior";
        previous.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (_weekOffset == 0)
        {
            previous.Disabled = true;
            previous.TooltipText = Palette.Blocked(Tr("esta já é a semana em curso; o que passou fica na coluna da direita."), previousTip);
        }

        string nextTip = Palette.Tip(Tr("Mostra a semana seguinte da agenda."), string.Format(Tr("dá para marcar com até {0} dias de antecedência."), Catalog.Regras.AntecedenciaDias), Tr("nada."));
        var next = Ui.Button(nav, "Semana seguinte", () => { _weekOffset++; Refresh(); }, nextTip);
        next.Name = "SemanaSeguinte";
        next.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (_weekOffset >= maxOffset)
        {
            next.Disabled = true;
            next.TooltipText = Palette.Blocked(string.Format(Tr("só dá para marcar com até {0} dias de antecedência."), Catalog.Regras.AntecedenciaDias), nextTip);
        }

        RepeatWeekButton(start);

        HBoxContainer header = Ui.Row(_left, 0);
        header.Add(new Control { CustomMinimumSize = new Vector2(DayColumn, 0) });
        foreach (Turno turno in Enum.GetValues<Turno>())
        {
            Label l = Ui.Value(header, TurnoName(turno), 18);
            l.AddThemeColorOverride("font_color", Palette.Texto3);
            l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            l.HorizontalAlignment = HorizontalAlignment.Center;
        }

        for (int d = 0; d < 7; d++)
        {
            DateOnly date = start.AddDays(d);
            HBoxContainer row = Ui.Row(_left, 0);
            row.Name = "Dia_" + date.ToString("yyyyMMdd", PtBr);
            double rain = Agenda.Rain.GetValueOrDefault(date);
            var day = row.Add(new PanelContainer
            {
                CustomMinimumSize = new Vector2(DayColumn, 60),
                MouseFilter = MouseFilterEnum.Stop,
                TooltipText = DayLong(date) + ". " + (rain > 0
                    ? string.Format(PtBr, Tr("Chuva prevista: compromissos de rua rendem {0:P0} menos nesse dia."), rain)
                    : Agenda.Rain.ContainsKey(date) ? Tr("Sem chuva prevista.") : Tr("A previsão de chuva da semana sai na segunda-feira.")),
            });
            var box = Palette.Box(date == today ? Palette.Cartao : new Color(Palette.Cartao, 0), 8, 2);
            if (date == today)
            {
                // Hoje: barra de 3 px em acao à esquerda, como a linha selecionada do ART_GUIDE.
                box.BorderColor = Palette.Acao;
                box.BorderWidthLeft = 3;
            }

            day.AddThemeStyleboxOverride("panel", box);
            var col = Ui.Column(day, 0);
            col.Alignment = BoxContainer.AlignmentMode.Center;
            Label name = Ui.Value(col, DayShort(date), 18);
            name.AddThemeColorOverride("font_color", date < today ? Palette.Texto3 : Palette.Texto);
            name.MouseFilter = MouseFilterEnum.Pass;
            if (rain > 0)
            {
                Label wet = Ui.Label(col, "chuva", 18);
                wet.AddThemeColorOverride("font_color", Palette.Texto3);
                wet.MouseFilter = MouseFilterEnum.Pass;
            }
            else if (date == today)
            {
                Label now = Ui.Label(col, "hoje", 18);
                now.AddThemeColorOverride("font_color", Palette.Texto2);
                now.MouseFilter = MouseFilterEnum.Pass;
            }

            foreach (Turno turno in Enum.GetValues<Turno>())
            {
                AgendaCell(row, date, turno, today);
            }
        }

        if (!Agenda.Rain.ContainsKey(start))
        {
            Glossary.Rich(_left, Tr("A previsão de chuva e os eventos da cidade de cada semana saem na segunda-feira dela."), 18, Palette.Texto3);
        }

        var scheduled = Agenda.Scheduled.Where(c => c.Date >= start && c.Date <= end).OrderBy(c => c.Date).ThenBy(c => c.Turno).ToList();
        if (scheduled.Count > 0)
        {
            Heading(_left, Tr("Marcados nesta semana"), 20);
            foreach (Commitment c in scheduled)
            {
                CommitmentRow(c);
            }
        }

        var events = Agenda.CityEvents.Where(e => e.Date >= start && e.Date <= end).OrderBy(e => e.Date).ThenBy(e => e.Turno).ToList();
        if (events.Count > 0)
        {
            Heading(_left, Tr("Eventos da cidade"), 20);
            foreach (CityEventInstance e in events)
            {
                CityEventRow(e, today);
            }
        }

        var invites = Agenda.Invites.Where(i => !i.Answered && i.Date >= today).OrderBy(i => i.Date).ToList();
        if (invites.Count > 0)
        {
            Heading(_left, Tr("Convites"), 20);
            foreach (Invite invite in invites)
            {
                InviteCard(invite);
            }
        }

        Heading(_left, Tr("Datas que vêm"), 20);
        var dates = new List<(DateOnly Date, string Text)>
        {
            (Campaign.CampaignStart, Tr("início da campanha oficial (o comício fica liberado)")),
            (Campaign.ElectionDate, Tr("dia da eleição")),
        };
        if (Campaign.Player.Office == Office.Prefeito)
        {
            dates.AddRange(_game.Debate.WeeksBeforeElection.Select(w => (Campaign.ElectionDate.AddDays(-7 * w), Tr("semana de debate na TV"))));
        }

        foreach (var (date, text) in dates.Where(d => d.Date >= today).OrderBy(d => d.Date))
        {
            Glossary.Rich(_left, $"{date.ToString("dd/MM/yyyy", PtBr)} · {text}");
        }
    }

    /// <summary>Compromisso concluído que ocupou o turno (para a semana em curso mostrar o que já foi feito).</summary>
    private CommitmentResult? DoneAt(DateOnly date, Turno turno) =>
        Agenda.Log.LastOrDefault(r => !r.Commitment.Delegado && r.Commitment.Date == date && (int)turno >= (int)r.Commitment.Turno && (int)turno < (int)r.Commitment.Turno + Catalog.Get(r.Commitment.Type).Turnos);

    /// <summary>Primeira palavra do nome do tipo: é o que cabe numa célula; o nome inteiro vai no tooltip.</summary>
    private static string ShortName(CommitmentType type) => type.SoEvento ? TranslationServer.Translate("Evento") : type.Nome.Split(' ')[0];

    private void AgendaCell(Node row, DateOnly date, Turno turno, DateOnly today)
    {
        string when = $"{DayShort(date)}, {TurnoName(turno)}";
        Button b;
        if (_engine.Occupying(date, turno) is { } c)
        {
            CommitmentType type = Catalog.Get(c.Type);
            b = Ui.Button(row, ShortName(type), () => OpenSheet(Sheet.Compromisso, c.Id),
                Palette.Tip(string.Format(Tr("Marcado: {0}, {1}."), CommitmentTitle(c), when), Tr("abre o compromisso: a previsão de hoje e a opção de desmarcar."), CostText(type)));
            b.AddThemeStyleboxOverride("normal", Palette.Box(new Color(Palette.Acao, 0.2f), 8, 2, Palette.Acao));
        }
        else if (DoneAt(date, turno) is { } done)
        {
            CommitmentType type = Catalog.Get(done.Commitment.Type);
            b = Ui.Button(row, ShortName(type), () => OpenSheet(Sheet.Compromisso, done.Commitment.Id),
                Palette.Tip(string.Format(Tr("Concluído: {0}, {1}."), CommitmentTitle(done.Commitment), when), Tr("abre o resultado explicado: o que mudou, onde e por quê."), Tr("nada; já aconteceu.")));
            b.AddThemeColorOverride("font_color", Palette.Texto3);
            b.AddThemeStyleboxOverride("normal", Palette.Box(Palette.Cartao, 8, 2));
        }
        else
        {
            CityEventInstance? cityEvent = Agenda.CityEvents.FirstOrDefault(e => e.Date == date && e.Turno == turno);
            Invite? invite = Agenda.Invites.FirstOrDefault(i => !i.Answered && i.Date == date && i.Turno == turno);
            string extra = cityEvent is not null ? " " + string.Format(Tr("Evento da cidade nesse turno: {0}, em {1}."), cityEvent.Nome, ZoneName(cityEvent.ZoneId))
                : invite is not null ? " " + string.Format(Tr("Há um convite para esse turno: {0}."), invite.Text) : "";
            string tip = Palette.Tip(
                string.Format(Tr("Turno livre: {0}."), when) + extra,
                Tr("abre a marcação: você escolhe o tipo, a zona e o público e vê a previsão antes de confirmar."),
                Tr("nada para abrir; o custo de cada compromisso aparece na previsão."));
            b = Ui.Button(row, cityEvent is not null ? "Evento" : invite is not null ? "Convite" : "+", () => StartSchedule(date, turno, cityEvent: cityEvent), tip);
            b.AddThemeStyleboxOverride("normal", Palette.Box(Palette.Cartao, 8, 2, cityEvent is not null || invite is not null ? Palette.Texto2 : Palette.Linha));
            if (date < today)
            {
                b.Text = "";
                b.Disabled = true;
                b.TooltipText = Palette.Blocked(Tr("esse dia já passou; marque de hoje em diante."), tip);
            }
            else if (_engine.TurnoPassed(date, turno))
            {
                b.Text = "";
                b.Disabled = true;
                b.TooltipText = Palette.Blocked(string.Format(Tr("a {0} de hoje já passou no relógio; marque num turno que ainda não terminou."), TurnoName(turno)), tip);
            }
            else if (BlockReason() is { } block)
            {
                b.Disabled = true;
                b.TooltipText = Palette.Blocked(string.Format(Tr("{0}; resolva antes de marcar."), block), tip);
            }
        }

        b.Name = $"Turno_{date.ToString("yyyyMMdd", PtBr)}_{(int)turno}";
        b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        b.CustomMinimumSize = new Vector2(0, 60);
        b.ClipText = true;
        b.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;

        // A célula tem cerca de 100 px: sem margem lateral, "Caminhada" (o nome mais usado) cabe inteiro em 18 px.
        foreach (string state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
        {
            if (b.GetThemeStylebox(state)?.Duplicate() is StyleBoxFlat style)
            {
                style.ContentMarginLeft = style.ContentMarginRight = 0;
                style.SetExpandMargin(Side.Left, -1);
                style.SetExpandMargin(Side.Right, -1);
                b.AddThemeStyleboxOverride(state, style);
            }
        }
    }

    private void CityEventRow(CityEventInstance e, DateOnly today)
    {
        string when = $"{DayShort(e.Date)}, {TurnoName(e.Turno)}";
        string publico = e.Publico == CommitmentType.PublicoGeral ? Tr("público geral") : EffectSummary.GroupName(e.Publico);
        CommitmentType type = Catalog.Get(CommitmentCatalog.EventoCidade);
        var request = new CommitmentRequest(type.Id, e.ZoneId, e.Date, e.Turno, CommitmentType.PublicoGeral, null, e.Id);
        ScheduleFailure? failure = e.Date < today ? ScheduleFailure.ForaDoPrazo : _engine.CanSchedule(request);
        string tip = Palette.Tip(
            string.Format(Tr("{0} em {1}, {2} ({3}). Só existe nesse dia, turno e zona."), e.Nome, ZoneName(e.ZoneId), when, publico),
            Tr("abre a marcação para ir ao evento: ") + ForecastSummary(request, _engine.Forecast(request)),
            CostText(type));
        var b = Ui.Button(_left, $"{e.Nome} · {ZoneName(e.ZoneId)}\n{when}", () => StartSchedule(e.Date, e.Turno, cityEvent: e), tip);
        b.Name = "Evento_" + e.Id;
        b.Alignment = HorizontalAlignment.Left;
        b.ClipText = true;
        Palette.ListRow(b);
        if (failure is not null)
        {
            b.Disabled = true;
            b.TooltipText = Palette.Blocked(FailureText(failure.Value, request), tip);
        }
    }

    private void InviteCard(Invite invite)
    {
        Leader? leader = _engine.City.Leader(invite.FromPersonId);
        string who = leader is null ? _engine.NameOf(invite.FromPersonId) : LeaderLabel(leader);
        string when = $"{DayShort(invite.Date)}, {TurnoName(invite.Turno)}";
        LeaderRules rules = _engine.Context.Campaign.Leaders.Regras;
        PanelContainer card = Card(_left);
        card.Name = "Convite_" + invite.Id;
        var col = Ui.Column(card, 8);
        Glossary.Rich(col, string.Format(Tr("{0}. Quando: {1}. Quem convida: {2}."), invite.Text, when, who), 18, Palette.Texto);

        var request = new CommitmentRequest("caminhada", invite.ZoneId, invite.Date, invite.Turno, CommitmentType.PublicoGeral, invite.FromPersonId, InviteId: invite.Id);
        CommitmentForecast forecast = _engine.Forecast(request);
        Glossary.Rich(col, string.Format(Tr("Se aceitar: uma caminhada em {0} nesse turno; {1}. Se recusar ou não responder até o dia: {2} de relação com quem convidou."),
            ZoneName(invite.ZoneId), ForecastSummary(request, forecast), Signed(rules.PontosConviteRecusado, "0")));

        HBoxContainer row = Ui.Row(col, 8);
        string acceptTip = Palette.Tip(
            string.Format(Tr("Aceita o convite de {0}."), who),
            string.Format(Tr("marca uma caminhada em {0}, {1}, com ela: {2}."), ZoneName(invite.ZoneId), when, ForecastSummary(request, forecast)),
            CostText(Catalog.Get("caminhada")));
        var accept = Ui.Button(row, "Aceitar", () =>
        {
            var (failure, _) = _engine.AcceptInvite(invite.Id);
            _log.Insert(0, failure is null ? string.Format(Tr("Convite aceito: caminhada em {0}, {1}."), ZoneName(invite.ZoneId), when) : FailureText(failure.Value, request));
            Refresh();
        }, acceptTip);
        accept.Name = "Aceitar";
        if (forecast.Blocked is { } blocked)
        {
            accept.Disabled = true;
            accept.TooltipText = Palette.Blocked(FailureText(blocked, request), acceptTip);
        }

        var decline = Ui.Button(row, "Recusar", () =>
        {
            _engine.DeclineInvite(invite.Id);
            _log.Insert(0, string.Format(Tr("Convite recusado: {0} vai lembrar."), who));
            Refresh();
        }, Palette.Tip(
            string.Format(Tr("Recusa o convite de {0}."), who),
            string.Format(Tr("o turno fica livre; ela vai lembrar: {0} de relação."), Signed(rules.PontosConviteRecusado, "0")),
            Tr("nada em dinheiro.")));
        decline.Name = "Recusar";
    }

    // ------------------------------------------------------------------ marcação

    /// <summary>Abre a marcação num dia e turno, já com o que se sabe (zona, evento da cidade, liderança a visitar).</summary>
    private void StartSchedule(DateOnly date, Turno turno, string? zoneId = null, CityEventInstance? cityEvent = null, string? visit = null, string? outlet = null)
    {
        _draft = new Draft { Date = date, Turno = turno };
        if (outlet is not null)
        {
            _draft.Type = CommitmentCatalog.Entrevista;
            _draft.OutletId = outlet;
        }
        else if (cityEvent is not null)
        {
            _draft.Type = CommitmentCatalog.EventoCidade;
            _draft.CityEventId = cityEvent.Id;
            _draft.ZoneId = cityEvent.ZoneId;
        }
        else if (visit is not null)
        {
            _draft.Type = CommitmentCatalog.Visita;
            _draft.PersonId = visit;
        }
        else
        {
            _draft.ZoneId = zoneId ?? Territory.Zones[Territory.HomeZone];
        }

        _map.Select(_draft.ZoneId);
        OpenSheet(Sheet.Marcar, null);
    }

    private void SetDraftZone(string zoneId)
    {
        if (_draft is null)
        {
            return;
        }

        _draft.ZoneId = zoneId;
        if (!TypeOf(_draft).Visita)
        {
            // A liderança que acompanha é da zona: trocou a zona, a escolha cai.
            _draft.PersonId = null;
        }
    }

    private void SetDraftType(string typeId)
    {
        if (_draft is null)
        {
            return;
        }

        CommitmentType before = TypeOf(_draft);
        _draft.Type = typeId;
        CommitmentType type = TypeOf(_draft);
        if (type.Visita != before.Visita)
        {
            _draft.PersonId = null;
        }

        if (type.SoEvento)
        {
            CityEventInstance? e = SlotEvents(_draft).FirstOrDefault(x => x.Id == _draft.CityEventId) ?? SlotEvents(_draft).FirstOrDefault();
            _draft.CityEventId = e?.Id;
            _draft.ZoneId = e?.ZoneId ?? _draft.ZoneId;
        }
        else
        {
            _draft.CityEventId = null;
            _draft.ZoneId ??= Territory.Zones[Territory.HomeZone];
        }
    }

    private List<CityEventInstance> SlotEvents(Draft d) => Agenda.CityEvents.Where(e => e.Date == d.Date && e.Turno == d.Turno).ToList();

    /// <summary>Grupos de eleitores que moram na zona, para o público de uma reunião.</summary>
    private List<(string Id, string Text)> PublicOptions(string? zoneId)
    {
        int zone = zoneId is null ? -1 : Territory.IndexOf(zoneId);
        if (zone < 0)
        {
            return [];
        }

        var electorate = _engine.Context.ZoneElectorates[zone];
        return _game.Electorate.Segments.Dimensions.SelectMany(d => d.Groups)
            .Select(g => (g.Id, g.Name, Voters: electorate.VotersIn(g.Id)))
            .Where(g => g.Voters >= 1)
            .Select(g => (g.Id, string.Format(PtBr, Tr("{0} · {1:N0} eleitores"), g.Name, g.Voters)))
            .ToList();
    }

    private void BuildScheduleSheet(Draft d)
    {
        BackButton();
        Heading(_left, Tr("Marcar compromisso"));
        Glossary.Rich(_left, $"{DayLong(d.Date)}, {TurnoName(d.Turno)}.", 18, Palette.Texto);
        CommitmentType type = TypeOf(d);
        List<CityEventInstance> slotEvents = SlotEvents(d);

        // Tipo.
        FieldLabel("Tipo");
        var types = Catalog.Tipos.Where(t => !t.SoEvento || slotEvents.Count > 0).Select(t => (t.Id, $"{t.Nome} · {ShortCost(t)}")).ToList();
        var typeOptions = Ui.Options(_left, types, d.Type, id => { SetDraftType(id); Refresh(); }, translate: false);
        typeOptions.Name = "Tipo";
        typeOptions.ClipText = true;
        typeOptions.TooltipText = Palette.Tip(Tr("O tipo de compromisso."), Tr("cada tipo rende uma coisa: presença numa zona, apoio de um grupo, relação com uma liderança, dinheiro ou descanso. A previsão abaixo muda com a escolha."), Tr("o custo de cada tipo está ao lado do nome."));
        Glossary.Rich(_left, type.Descricao);

        // Zona (ou o evento da cidade daquele turno).
        if (type.SoEvento)
        {
            FieldLabel("Evento");
            var eventOptions = Ui.Options(_left, slotEvents.Select(e => (e.Id, $"{e.Nome} · {ZoneName(e.ZoneId)}")), d.CityEventId, id =>
            {
                d.CityEventId = id;
                d.ZoneId = slotEvents.First(e => e.Id == id).ZoneId;
                d.PersonId = null;
                _map.Select(d.ZoneId);
                Refresh();
            }, translate: false);
            eventOptions.Name = "Evento";
            eventOptions.ClipText = true;
            eventOptions.TooltipText = Palette.Tip(Tr("O evento da cidade a que você vai."), Tr("o evento só existe nesse dia, turno e zona; quem está lá vê você."), Tr("nada; o custo é o do compromisso."));
        }
        else if (type.ExigeZona)
        {
            FieldLabel("Zona (ou clique no mapa)");
            var zones = _engine.Context.Zonas.Zonas.OrderBy(z => z.Nome, StringComparer.Create(PtBr, true))
                .Select(z => (z.Id, string.Format(PtBr, Tr("{0} · presença {1:0.0}"), z.Nome, Territory.PresenceOf(RaceSetup.PlayerId, Territory.IndexOf(z.Id)))));
            var zoneOptions = Ui.Options(_left, zones, d.ZoneId, id => { SetDraftZone(id); _map.Select(id); Refresh(); }, translate: false);
            zoneOptions.Name = "Zona";
            zoneOptions.ClipText = true;
            zoneOptions.TooltipText = Palette.Tip(Tr("A zona onde o compromisso acontece."), Tr("zona pequena rende mais por turno, zona grande vale mais votos; parte do ganho transborda para as vizinhas. Também dá para escolher clicando no mapa."), Tr("nada; em cidade grande, ir a zonas distantes no mesmo dia tira rendimento do segundo compromisso."));
        }

        // Público (reunião).
        if (type.EscolhePublico)
        {
            var groups = PublicOptions(d.ZoneId);
            if (groups.All(g => g.Id != d.Publico))
            {
                d.Publico = groups.FirstOrDefault().Id ?? CommitmentType.PublicoGeral;
            }

            FieldLabel("Público");
            var publicOptions = Ui.Options(_left, groups, d.Publico, id => { d.Publico = id; Refresh(); }, translate: false);
            publicOptions.Name = "Publico";
            publicOptions.ClipText = true;
            publicOptions.TooltipText = Palette.Tip(Tr("O grupo de eleitores com quem você conversa."), Tr("o apoio ganho vale para esse grupo, nessa zona; só aparecem grupos que moram nela."), Tr("nada; o custo é o do compromisso."));
        }

        // Liderança: a visitada, ou a aliada da zona que vai junto.
        if (type.Visita)
        {
            var known = _engine.City.Leaders.Where(l => l.Known).OrderBy(l => _engine.NameOf(l.PersonId), StringComparer.Create(PtBr, true)).ToList();
            if (d.PersonId is null || known.All(l => l.PersonId != d.PersonId))
            {
                d.PersonId = known.FirstOrDefault()?.PersonId;
            }

            FieldLabel("Quem visitar");
            var personOptions = Ui.Options(_left, known.Select(l => (l.PersonId, $"{LeaderLabel(l)} · {ZoneName(l.Zones[0])}")), d.PersonId, id => { d.PersonId = id; Refresh(); }, translate: false);
            personOptions.Name = "Pessoa";
            personOptions.ClipText = true;
            personOptions.TooltipText = Palette.Tip(Tr("A liderança que você vai visitar."), Tr("a visita melhora a relação e ela faz o pedido dela; a resposta é uma decisão sua, na hora."), Tr("nada em dinheiro; ocupa o turno."));
        }
        else if (type.Rua && d.ZoneId is not null)
        {
            var allies = _engine.City.Leaders.Where(l => l.Known && l.AlignedWith == RaceSetup.PlayerId && l.Zones.Contains(d.ZoneId)).ToList();
            if (allies.Count > 0)
            {
                FieldLabel("Quem vai junto");
                var items = new List<(string Id, string Text)> { ("", Tr("Ninguém")) };
                items.AddRange(allies.Select(l => (l.PersonId, LeaderLabel(l))));
                var companion = Ui.Options(_left, items, d.PersonId ?? "", id => { d.PersonId = id.Length == 0 ? null : id; Refresh(); }, translate: false);
                companion.Name = "Acompanhante";
                companion.ClipText = true;
                companion.TooltipText = Palette.Tip(Tr("Liderança aliada da zona que acompanha você."), string.Format(PtBr, Tr("com ela junto, o compromisso de rua rende {0}."), Percent(1 + Catalog.Regras.LiderancaBonus)), Tr("nada."));
            }
            else
            {
                d.PersonId = null;
            }
        }

        DelegateControls(d, type);
        OutletControls(d, type);
        ThemeControls(d, type);

        // Previsão antes de confirmar (princípio f).
        CommitmentRequest request = RequestOf(d);
        CommitmentForecast forecast = _engine.Forecast(request);
        ForecastBlock(_left, request, forecast);
        string? brokenPact = _engine.PactBrokenBy(request);
        if (brokenPact is not null)
        {
            // Marcar ato de rua numa zona fechada quebra o pacto: o jogo avisa antes (GDD 17.11).
            MemoryDefinition broken = _engine.Context.Campaign.Rivals.Memory("pacto_quebrado");
            PanelContainer warn = Card(_left, Palette.CartaoAlto, Palette.Negativo, 10);
            warn.Name = "QuebraPacto";
            Glossary.Rich(warn, string.Format(Tr("Este compromisso quebra o seu pacto com {0}: {1} é zona fechada pelo acordo. A quebra vira notícia e fica na memória (\"{2}\", {3} de relação), sem se apagar."),
                PersonLabel(brokenPact), ZoneName(request.ZoneId), broken.Texto, Signed(broken.Pontos, "0")), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
        }

        HBoxContainer row = Ui.Row(_left, 8);
        string tip = Palette.Tip(
            string.Format(Tr("Marca {0} para {1}, {2}."), type.Nome, DayShort(d.Date), TurnoName(d.Turno)),
            ForecastSummary(request, forecast) + ". " + Tr("Acontece sozinho quando o dia chegar; dá para desmarcar antes."),
            CostText(type));
        var confirm = Ui.Button(row, "Confirmar", () =>
        {
            var (failure, commitment) = _engine.Schedule(request);
            if (failure is null && commitment is not null)
            {
                _log.Insert(0, string.Format(Tr("Marcado: {0}, {1}, {2}."), CommitmentTitle(commitment), DayShort(commitment.Date), TurnoName(commitment.Turno)));
                Back();
            }
            else
            {
                Refresh();
            }
        }, tip);
        confirm.Name = "Confirmar";
        confirm.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (forecast.Blocked is { } blocked)
        {
            confirm.Disabled = true;
            confirm.TooltipText = Palette.Blocked(FailureText(blocked, request), tip);
        }
        else if (brokenPact is not null)
        {
            Palette.Danger(confirm);
            confirm.TooltipText = Palette.Tip(
                string.Format(Tr("Marca {0} para {1}, {2}, e quebra o pacto com {3}."), type.Nome, DayShort(d.Date), TurnoName(d.Turno), _engine.NameOf(brokenPact)),
                ForecastSummary(request, forecast) + ". " + Tr("O pacto acaba quando o compromisso acontecer; a quebra vira notícia."),
                CostText(type));
        }
        else
        {
            Palette.Primary(confirm);
        }

        Ui.Button(row, "Cancelar", Back, Palette.Tip(Tr("Fecha a marcação sem marcar nada."), Tr("o turno continua livre."), Tr("nada."))).Name = "Cancelar";
    }

    /// <summary>Veículo da entrevista (GDD 17.13): o crítico pergunta mais duro e convence mais; o amigo é seguro e convence pouco.</summary>
    private void OutletControls(Draft d, CommitmentType type)
    {
        if (type.Id != CommitmentCatalog.Entrevista || (d.Delegado && type.Delegavel))
        {
            return;
        }

        var outlets = _engine.Outlets();
        if (outlets.Count == 0)
        {
            d.OutletId = null;
            return;
        }

        if (d.OutletId is not null && outlets.All(o => o.Outlet.Id != d.OutletId))
        {
            d.OutletId = null;
        }

        FieldLabel("Veículo");
        var items = new List<(string Id, string Text)> { ("", Tr("Sem escolher veículo")) };
        items.AddRange(outlets.Select(o => (o.Outlet.Id, string.Format(PtBr, Tr("{0} · {1} · alcança {2:P0}"), o.Outlet.Name, StanceText(o.Stance), o.ReachShare))));
        var options = Ui.Options(_left, items, d.OutletId ?? "", id => { d.OutletId = id.Length == 0 ? null : id; Refresh(); }, translate: false);
        options.Name = "Veiculo";
        options.ClipText = true;
        options.TooltipText = Palette.Tip(
            Tr("O veículo que faz a entrevista."),
            Tr("veículo crítico a você pergunta mais duro (chance menor de se sair bem) e quem ouve dá mais crédito; veículo amigo é seguro e convence pouco. O alcance do veículo e a relação com o jornalista entram na previsão abaixo."),
            Tr("nada a mais; o jornalista lembra da entrevista."));
        if (d.OutletId is not null && outlets.FirstOrDefault(o => o.Outlet.Id == d.OutletId) is { } chosen)
        {
            Glossary.Rich(_left, string.Format(Tr("{0}. Com você: {1}. Jornalista: {2} (relação {3})."), chosen.Label, StanceText(chosen.Stance), chosen.JournalistName, Signed(chosen.JournalistRelation, "0")));
        }
    }

    private void FieldLabel(string text) => Glossary.Rich(_left, Tr(text), 18, Palette.Texto3);

    private const int DayColumn = 54;

    /// <summary>"Mandar representante" (GDD 17.6): só com alguém na coordenação, e até o número de delegações da semana.</summary>
    private void DelegateControls(Draft d, CommitmentType type)
    {
        if (!type.Delegavel)
        {
            d.Delegado = false;
            return;
        }

        StaffRules rules = StaffContent.Regras;
        StaffMember? coordinator = _engine.StaffIn(StaffRole.Coordenador);
        int perWeek = _engine.DelegationsPerWeek;
        int used = _engine.DelegationsIn(d.Date);
        string memory = StaffContent.Memory("visita_delegada").Texto;
        string tip = Palette.Tip(
            Tr("Um representante da campanha vai no seu lugar."),
            string.Format(PtBr, Tr("o compromisso rende {0:P0} do normal, não ocupa o seu turno e não dá estresse. Quem recebe lembra que você não foi (\"{1}\"); numa visita, a relação sobe só {2} e o pedido não é ouvido."), rules.DelegacaoRendimento, memory, Signed(rules.DelegacaoPontosVisita, "0")),
            string.Format(Tr("o custo do compromisso e uma das delegações da semana ({0} de {1} usadas)."), used, perWeek));
        var toggle = Ui.Button(_left, "Mandar representante", () => { d.Delegado = !d.Delegado; Refresh(); }, tip);
        toggle.Name = "Delegar";
        toggle.ToggleMode = true;
        toggle.ButtonPressed = d.Delegado;
        if (coordinator is null)
        {
            d.Delegado = false;
            toggle.ButtonPressed = false;
            toggle.Disabled = true;
            toggle.TooltipText = Palette.Blocked(Tr("falta alguém na Coordenação da equipe: é quem manda representante. Contrate em Campanha, Equipe."), tip);
            return;
        }

        if (!d.Delegado && used >= perWeek)
        {
            toggle.Disabled = true;
            toggle.TooltipText = Palette.Blocked(string.Format(Tr("{0} já usou as {1} delegações desta semana; coordenação melhor delega mais."), StaffLabel(coordinator), perWeek), tip);
        }

        Glossary.Rich(_left, string.Format(Tr("Delegações de {0} na semana desse dia: {1} de {2} usadas."), StaffLabel(coordinator), used, perWeek), 18, Palette.Texto3);
    }

    /// <summary>Tema de que o candidato fala (GDD 17.9): entrevista, gravação e comício tornam a posição mais conhecida.</summary>
    private void ThemeControls(Draft d, CommitmentType type)
    {
        if (!_engine.SpeaksOfThemes(type.Id) || (d.Delegado && type.Delegavel))
        {
            return;
        }

        var themes = _engine.Context.Themes.Themes;
        if (d.Tema is not null && themes.All(t => t.Id != d.Tema))
        {
            d.Tema = null;
        }

        FieldLabel("Tema de que você vai falar");
        var items = new List<(string Id, string Text)> { ("", Tr("Nenhum tema em especial")) };
        string? zoneOfTalk = type.ExigeZona ? d.ZoneId : null;
        items.AddRange(themes.Select(t => (t.Id, string.Format(PtBr, Tr("{0}{3} · conhecida {1:P0}{2}"), t.Name, _engine.KnownOf(t.Id), Platform.Flags.Contains(t.Id) ? " · " + Tr("bandeira") : "",
            _engine.HotThemeOn(t.Id, zoneOfTalk, d.Date) is not null ? " · " + Tr("em alta") : ""))));
        var options = Ui.Options(_left, items, d.Tema ?? "", id => { d.Tema = id.Length == 0 ? null : id; Refresh(); }, translate: false);
        options.Name = "Tema";
        options.ClipText = true;
        options.TooltipText = Palette.Tip(
            Tr("O tema de que você fala neste compromisso."),
            Tr("a sua posição nesse tema fica mais conhecida (mais ainda se for bandeira) e entra no Livro de compromissos como posição assumida, com a data e o lugar. Quem não gosta dela também fica sabendo."),
            Tr("nada em dinheiro; posição conhecida custa mais para mudar depois."));
    }

    /// <summary>
    /// Atalho "Repetir a semana anterior": marca de novo, nos mesmos dias e turnos da semana mostrada, o que foi feito
    /// ou está marcado na semana anterior a ela. O que não couber (turno ocupado, sem dinheiro, fora de época) fica de fora.
    /// </summary>
    private void RepeatWeekButton(DateOnly start)
    {
        DateOnly from = start.AddDays(-7), to = start.AddDays(-1);
        var source = Agenda.Log.Select(r => r.Commitment).Concat(Agenda.Scheduled)
            .Where(c => c.Date >= from && c.Date <= to && c.CityEventId is null && c.InviteId is null)
            .OrderBy(c => c.Date).ThenBy(c => c.Turno).ToList();
        var requests = source.Select(c => new CommitmentRequest(c.Type, c.ZoneId, c.Date.AddDays(7), c.Turno, c.Publico, c.PersonId, null, null, c.Delegado, c.Tema)).ToList();
        var possible = requests.Where(r => _engine.CanSchedule(r) is null).ToList();
        decimal cost = possible.Sum(r => Math.Max(0, _engine.CostOf(Catalog.Get(r.Type))));
        string tip = Palette.Tip(
            Tr("Repete a semana anterior nesta semana."),
            string.Format(Tr("marca de novo, nos mesmos dias e turnos, os compromissos da semana anterior a esta: {0} de {1} cabem hoje. Evento da cidade e convite não se repetem; o cansaço de repetir continua valendo e aparece na previsão de cada um."), possible.Count, requests.Count),
            string.Format(Tr("{0} ao todo, cobrados quando cada compromisso acontece."), Money(cost)));
        var repeat = Ui.Button(_left, "Repetir a semana anterior", () =>
        {
            int done = 0;
            foreach (CommitmentRequest r in requests)
            {
                if (_engine.Schedule(r).Failure is null)
                {
                    done++;
                }
            }

            _log.Insert(0, string.Format(Tr("Semana repetida: {0} de {1} compromissos marcados."), done, requests.Count));
            Refresh();
        }, tip);
        repeat.Name = "RepetirSemana";
        if (BlockReason() is { } block)
        {
            repeat.Disabled = true;
            repeat.TooltipText = Palette.Blocked(string.Format(Tr("{0}; resolva antes de marcar."), block), tip);
        }
        else if (possible.Count == 0)
        {
            repeat.Disabled = true;
            repeat.TooltipText = Palette.Blocked(requests.Count == 0
                ? Tr("a semana anterior a esta não teve compromisso para repetir; falta marcar ao menos um.")
                : Tr("nenhum compromisso da semana anterior cabe nesta: os turnos estão ocupados, já passaram ou falta dinheiro."), tip);
        }
    }

    /// <summary>Por que não dá para marcar e o que falta (princípio j).</summary>
    private string FailureText(ScheduleFailure failure, CommitmentRequest r)
    {
        AgendaRules rules = Catalog.Regras;
        CommitmentType type = Catalog.Get(r.Type);
        switch (failure)
        {
            case ScheduleFailure.FaseNaoPermite:
                int weeks = Math.Max(0, (Campaign.CampaignStart.DayNumber - _engine.Today.DayNumber + 6) / 7);
                return string.Format(Tr("{0} só na campanha oficial, a partir de {1} (faltam {2} semanas)."), type.Nome, Campaign.CampaignStart.ToString("d 'de' MMMM", PtBr), weeks);
            case ScheduleFailure.ForaDoPrazo:
                return string.Format(Tr("só dá para marcar de hoje até {0} dias à frente, e antes do dia da eleição."), rules.AntecedenciaDias);
            case ScheduleFailure.TurnoOcupado:
                return type.Turnos > 1
                    ? string.Format(Tr("{0} ocupa {1} turnos seguidos e um deles já tem compromisso (ou o dia acaba antes); escolha outro turno ou desmarque o que está lá."), type.Nome, type.Turnos)
                    : Tr("esse turno já tem compromisso; escolha outro turno ou desmarque o que está lá.");
            case ScheduleFailure.SemDinheiro:
                return string.Format(Tr("faltam {0} em caixa (custa {1})."), Money(_engine.CostOf(type) - State.Player.Resources.Money), Money(_engine.CostOf(type)));
            case ScheduleFailure.ZonaInvalida:
                return Tr("falta escolher a zona.");
            case ScheduleFailure.PublicoInvalido:
                return Tr("falta escolher um grupo de eleitores que more na zona.");
            case ScheduleFailure.SemLideranca:
                return Tr("falta escolher uma liderança que você já conheça; uma caminhada numa zona pode revelar as que você não conhece.");
            case ScheduleFailure.SemApoiadorNaZona:
                int zone = r.ZoneId is null ? -1 : Territory.IndexOf(r.ZoneId);
                double presence = zone < 0 ? 0 : Territory.PresenceOf(RaceSetup.PlayerId, zone);
                return string.Format(PtBr, Tr("você ainda não tem quem abra a casa nessa zona: faltam {0:0.0} pontos de presença (tem {1:0.0}, precisa de {2:0}) ou uma liderança conhecida ali que não esteja contra você."), Math.Max(0, rules.ReuniaoPresencaMinima - presence), presence, rules.ReuniaoPresencaMinima);
            case ScheduleFailure.EventoInvalido:
                return Tr("o evento da cidade só existe no dia, no turno e na zona dele.");
            case ScheduleFailure.NaoDelegavel:
                return string.Format(Tr("{0} exige a sua presença: não dá para mandar representante. Desmarque a opção."), type.Nome);
            case ScheduleFailure.SemCoordenador:
                return Tr("falta alguém na Coordenação da equipe: é quem manda representante. Contrate em Campanha, Equipe.");
            case ScheduleFailure.SemDelegacao:
                return _engine.DelegationsIn(r.Date) >= _engine.DelegationsPerWeek
                    ? string.Format(Tr("as {0} delegações da semana desse dia já foram usadas; falta esperar a semana seguinte ou ter coordenação melhor."), _engine.DelegationsPerWeek)
                    : string.Format(Tr("{0} ocupa {1} turnos seguidos e o dia acaba antes; escolha um turno mais cedo."), type.Nome, type.Turnos);
            case ScheduleFailure.SemVerbaDeRua:
                decimal has = r.ZoneId is null ? 0 : _engine.RuaBudgetFor(r.Date, r.ZoneId);
                return string.Format(Tr("comício precisa de palanque e som: falta verba no canal Rua em {0} na semana do comício. Tem {1} por semana, precisa de {2} (faltam {3}). Ponha em Campanha, Verba por canal, Rua."), ZoneName(r.ZoneId), Money(has), Money(_engine.ComicioRuaMinimum), Money(Math.Max(0, _engine.ComicioRuaMinimum - has)));
            case ScheduleFailure.TemaInvalido:
                return Tr("só você fala pela sua posição: com representante, ou neste tipo de compromisso, não há tema. Escolha Nenhum tema em especial.");
            case ScheduleFailure.TurnoPassou:
                return string.Format(Tr("a {0} de hoje já passou no relógio; escolha um turno que ainda não terminou."), TurnoName(r.Turno));
            default:
                return Tr("há uma decisão aberta; resolva antes de marcar.");
        }
    }

    // ------------------------------------------------------------------ previsão

    /// <summary>O tipo tem algum efeito que o rendimento multiplica (presença, apoio ou imagem); senão os fatores não dizem nada.</summary>
    private static bool HasYield(CommitmentType type) => type.Presenca > 0 || type.PresencaCidade > 0 || type.Apoio > 0 || type.Imagem > 0;

    /// <summary>Linhas de efeito da previsão, cada uma com a faixa vinda do Core.</summary>
    private List<string> ForecastLines(CommitmentRequest r, CommitmentForecast f)
    {
        CommitmentType type = Catalog.Get(r.Type);
        var lines = new List<string>();
        if (f.PresenceMax >= 0.05)
        {
            lines.Add(type.ExigeZona && r.ZoneId is not null
                ? string.Format(Tr("presença em {0}: {1}"), ZoneName(r.ZoneId), Range(f.PresenceMin, f.PresenceMax, "0.0"))
                : string.Format(Tr("presença em todas as zonas: {0}"), Range(f.PresenceMin, f.PresenceMax, "0.0")));
        }

        if (f.SupportMax > 0 && _engine.SupportGroupOf(r) is { } group)
        {
            lines.Add(string.Format(Tr("apoio no grupo {0}: {1} pontos"), EffectSummary.GroupName(group), Range(f.SupportMin * 100, f.SupportMax * 100, "0.0")));
        }

        if (Math.Abs(f.ImageMin) >= 0.005 || Math.Abs(f.ImageMax) >= 0.005)
        {
            lines.Add(string.Format(Tr("imagem: {0}"), Range(f.ImageMin, f.ImageMax, "0.00")));
        }

        if (f.RelationPoints != 0 && r.PersonId is not null)
        {
            lines.Add(string.Format(Tr("relação com {0}: {1}"), _engine.NameOf(r.PersonId), Signed(f.RelationPoints, "0")));
        }

        if (f.MoneyMin != 0 || f.MoneyMax != 0)
        {
            lines.Add(string.Format(Tr("dinheiro: {0}"), f.MoneyMin == f.MoneyMax ? SignedMoney(f.MoneyMin) : $"{SignedMoney(f.MoneyMin)} {Tr("a")} {SignedMoney(f.MoneyMax)}"));
        }

        if (f.Stress != 0)
        {
            lines.Add(string.Format(Tr("estresse: {0}"), Signed(f.Stress, "0")));
        }

        if (r.Tema is not null && f.ThemeKnownGain >= 0.0005 && _engine.Context.Themes.Themes.FirstOrDefault(t => t.Id == r.Tema) is { } theme)
        {
            lines.Add(string.Format(PtBr, Tr("posição conhecida em {0}: {1} pontos (você dirá {2})"), theme.Name, Signed(f.ThemeKnownGain * 100, "0"), PositionText(theme, _engine.PositionOf(theme.Id))));
        }

        return lines;
    }

    /// <summary>
    /// Quanto o próximo compromisso igual ainda rende, pelo cansaço ("rende 62%"): a mesma frase na previsão e no
    /// painel de zonas, para o número nunca ser lido como o tamanho do cansaço.
    /// </summary>
    private string YieldText(double factor) => string.Format(PtBr, Tr("rende {0:P0}"), factor);

    /// <summary>A previsão numa frase, para tooltips ("o que faz", com a faixa do Core).</summary>
    private string ForecastSummary(CommitmentRequest r, CommitmentForecast f)
    {
        var lines = ForecastLines(r, f);
        string text = lines.Count == 0 ? Tr("sem efeito nos números") : string.Join("; ", lines);
        if (HasYield(Catalog.Get(r.Type)) && f.FatigueFactor < 0.999)
        {
            text += "; " + string.Format(Tr("o cansaço já está na conta ({0})"), YieldText(f.FatigueFactor));
        }

        return text;
    }

    /// <summary>
    /// Bloco da previsão (princípios f e g): faixa de cada efeito, o cansaço em destaque, cada fator do rendimento com
    /// nome e multiplicador, os desfechos sorteados e, se estiver bloqueado, o motivo e o que falta.
    /// </summary>
    private void ForecastBlock(Node parent, CommitmentRequest r, CommitmentForecast f, bool showBlocked = true)
    {
        CommitmentType type = Catalog.Get(r.Type);
        PanelContainer card = Card(parent, Palette.Cartao, Palette.Linha);
        card.Name = "Previsao";
        var col = Ui.Column(card, 6);
        Heading(col, Tr("Previsão"), 20);
        var lines = ForecastLines(r, f);
        if (lines.Count == 0)
        {
            Glossary.Rich(col, Tr("Sem efeito nos números."));
        }

        foreach (string line in lines)
        {
            Glossary.Rich(col, "· " + char.ToUpper(line[0], PtBr) + line[1..], 18, Palette.Texto);
        }

        if (r.Delegado && f.Factors.FirstOrDefault(x => x.Id == "delegado") is { } delegated)
        {
            PanelContainer sent = Card(col, Palette.CartaoAlto, Palette.Linha, 10);
            sent.Name = "Representante";
            Glossary.Rich(sent, string.Format(PtBr, Tr("Com representante: rende {0:P0} do normal e não ocupa o seu turno. Quem recebe lembra que você não foi."), delegated.Multiplier), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
        }
        else if (type.Visita)
        {
            Glossary.Rich(col, Tr("Na visita ela faz o pedido dela; a resposta é uma decisão sua, com o jogo pausado."));
        }

        if (HasYield(type))
        {
            YieldFactor? fatigue = f.Factors.FirstOrDefault(x => x.Id == "cansaco");
            if (fatigue is not null)
            {
                // O cansaço em destaque (princípio g): repetir rende cada vez menos, e o jogo diz antes.
                PanelContainer warn = Card(col, Palette.CartaoAlto, Palette.Negativo, 10);
                warn.Name = "Cansaco";
                Glossary.Rich(warn, string.Format(PtBr, Tr("Cansaço: {0}: rende {1:P0}."), fatigue.Text, fatigue.Multiplier), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            }

            if (f.Factors.FirstOrDefault(x => x.Id == "tema_em_alta") is { } hotTheme)
            {
                // Tema em alta (GDD 17.13): o melhor assunto da semana depende do noticiário.
                PanelContainer hot = Card(col, Palette.CartaoAlto, Palette.Positivo, 10);
                hot.Name = "TemaEmAlta";
                Glossary.Rich(hot, string.Format(Tr("{0}: rende {1} nesta semana."), hotTheme.Text, Percent(hotTheme.Multiplier)), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            }

            var others = f.Factors.Where(x => x.Id is not ("cansaco" or "delegado" or "tema_em_alta")).ToList();
            if (f.Factors.Count > 0)
            {
                Glossary.Rich(col, string.Format(PtBr, Tr("Rendimento: {0:P0} do valor de base, antes da sorte do dia (de {1:P0} a {2:P0})."), f.Yield, Catalog.Regras.Sorte.Min, Catalog.Regras.Sorte.Max));
            }

            foreach (YieldFactor factor in others)
            {
                Glossary.Rich(col, string.Format(PtBr, "· {0}: ×{1:0.00} ({2})", factor.Text, factor.Multiplier, Percent(factor.Multiplier)));
            }
        }

        foreach (ForecastOutcome outcome in f.Outcomes)
        {
            Glossary.Rich(col, string.Format(PtBr, "· {0:P0}: {1}", outcome.Probability, outcome.Text));
        }

        if (showBlocked && f.Blocked is { } blocked)
        {
            PanelContainer stop = Card(col, Palette.CartaoAlto, Palette.Linha, 10);
            stop.Name = "Bloqueio";
            Glossary.Rich(stop, Tr("Não dá para marcar:") + " " + FailureText(blocked, r), 18, Palette.Texto);
        }
    }

    // ------------------------------------------------------------------ detalhe do compromisso

    /// <summary>Ficha de um compromisso marcado (previsão e desmarcar) ou concluído (resultado explicado).</summary>
    private bool BuildCommitmentSheet(string id)
    {
        Commitment? scheduled = Agenda.Scheduled.FirstOrDefault(c => c.Id == id);
        CommitmentResult? done = scheduled is null ? Agenda.Log.LastOrDefault(r => r.Commitment.Id == id) : null;
        Commitment? c = scheduled ?? done?.Commitment;
        if (c is null)
        {
            return false;
        }

        BackButton();
        CommitmentType type = Catalog.Get(c.Type);
        NameTitle(_left, CommitmentTitle(c), 22);
        Glossary.Rich(_left, $"{DayLong(c.Date)}, {TurnoName(c.Turno)}.", 18, Palette.Texto);
        Glossary.Rich(_left, type.Descricao);
        if (c.Publico != CommitmentType.PublicoGeral)
        {
            Glossary.Rich(_left, string.Format(Tr("Público: {0}."), EffectSummary.GroupName(c.Publico)));
        }

        if (c.Tema is not null)
        {
            Glossary.Rich(_left, string.Format(Tr("Tema de que você fala: {0}."), ThemeName(c.Tema)));
        }

        if (!type.Visita && c.PersonId is not null && _engine.City.Leader(c.PersonId) is { } companion)
        {
            Glossary.Rich(_left, string.Format(Tr("Com você: {0}."), LeaderLabel(companion)));
        }

        if (done is not null)
        {
            Heading(_left, Tr("O que mudou"), 20);
            Glossary.Rich(_left, ResultText(done), 18, Palette.Texto);
            if (c.ZoneId is not null)
            {
                string zone = c.ZoneId;
                Ui.Button(_left, "Abrir a ficha da zona", () => { _map.CenterOn(zone); _map.Select(zone); OpenSheet(Sheet.Zona, zone); },
                    Palette.Tip(string.Format(Tr("Ficha de {0}."), ZoneName(zone)), Tr("mostra a sua presença ali agora, o cansaço e o que você já fez na zona."), Tr("nada.")));
            }

            return true;
        }

        var request = new CommitmentRequest(c.Type, c.ZoneId, c.Date, c.Turno, c.Publico, c.PersonId, c.CityEventId, c.InviteId, c.Delegado, c.Tema);
        ForecastBlock(_left, request, _engine.ForecastOf(c), showBlocked: false);
        LeaderRules rules = _engine.Context.Campaign.Leaders.Regras;
        string invite = c.InviteId is null ? "" : " " + string.Format(Tr("Veio de um convite: desmarcar conta como recusa ({0} de relação com quem convidou)."), Signed(rules.PontosConviteRecusado, "0"));
        var cancel = Ui.Button(_left, "Desmarcar", () =>
        {
            string title = CommitmentTitle(c);
            _engine.Cancel(c.Id);
            _log.Insert(0, string.Format(Tr("Desmarcado: {0}, {1}."), title, DayShort(c.Date)));
            Back();
        }, Palette.Tip(Tr("Desmarca este compromisso."), Tr("o turno fica livre e nada é cobrado: o custo só sai quando o compromisso acontece.") + invite, Tr("nada em dinheiro.")));
        cancel.Name = "Desmarcar";
        return true;
    }
}
