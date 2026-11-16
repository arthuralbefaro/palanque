using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Ideology;
using Planalto.Core.People;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Plataforma (GDD 17.9): o bloco da área Campanha, a tela inteira com os temas (posição do partido, a sua, a
/// conhecida e as bandeiras) e a matriz "Plataforma × públicos", a tela de um tema (mudar de posição com a previsão
/// por grupo e por zona e o custo de incoerência) e a de prometer a uma zona ou a um grupo.
/// </summary>
public partial class HubScreen
{
    private int? _positionDraft;
    private List<string>? _positionResult;
    private string? _matrixTarget;
    private string? _promiseTarget;
    private string? _promiseId;
    private List<string>? _promiseResult;

    private PlatformState Platform => _engine.Platform;

    /// <summary>"+1 (mais policiamento)", "0 (no meio)", "−2 (…)".</summary>
    private string PositionText(ThemeDefinition theme, double value)
    {
        int rounded = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        string number = Math.Abs(value - rounded) < 0.05 ? Signed(rounded, "0") : Signed(value, "0.0");
        if (rounded == 0)
        {
            number = Math.Abs(value) < 0.05 ? "0" : number;
            return string.Format(Tr("{0} (no meio)"), number);
        }

        return $"{number} ({(rounded > 0 ? theme.PositivePole : theme.NegativePole)})";
    }

    private int RecentPositionChanges() =>
        Platform.Changes.Count(c => (_engine.Today.DayNumber - c.Date.DayNumber) / 7.0 < _engine.Context.Campaign.Platform.Regras.JanelaMudancasSemanas);

    private string ThemeName(string id) => _engine.Context.Themes.Themes.FirstOrDefault(t => t.Id == id)?.Name ?? id;

    private string AudienceName(string kind, string id) => kind == Promise.ParaZona ? ZoneName(id) : EffectSummary.GroupName(id);

    /// <summary>Valor com sinal e seta, na cor do efeito para o jogador (ART_GUIDE: positivo e negativo têm sempre sinal).</summary>
    private static Label SignedValue(Node parent, double value, string format, string suffix = "")
    {
        bool zero = Signed(value, format).All(ch => ch is '+' or '−' or '0' or ',' or '.');
        Label label = Ui.Value(parent, zero ? "0" + suffix : $"{Signed(value, format)}{suffix} {(value > 0 ? "▲" : "▼")}", 18);
        label.AddThemeColorOverride("font_color", zero ? Palette.Texto2 : value > 0 ? Palette.Positivo : Palette.Negativo);
        label.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        return label;
    }

    private void SignedRow(Node parent, string name, double value, string format, string? tip = null, string suffix = "")
    {
        HBoxContainer row = Ui.Row(parent, 8);
        RichTextLabel label = Glossary.Plain(row, name, 18, Palette.Texto2);
        if (tip is not null)
        {
            row.MouseFilter = MouseFilterEnum.Stop;
            row.TooltipText = tip;
            label.MouseFilter = MouseFilterEnum.Pass;
        }

        SignedValue(row, value, format, suffix);
    }

    // ------------------------------------------------------------------ bloco da área Campanha

    private void BuildPlatformBlock()
    {
        BlockHeader(Tr("Plataforma"), "VerPlataforma", () => OpenSheet(Sheet.Plataforma, null),
            Palette.Tip(Tr("Abre a Plataforma inteira."), Tr("mostra os dez temas com a posição do partido, a sua e a conhecida, as bandeiras, quem gosta e quem não gosta de cada posição, e as promessas."), Tr("nada para abrir; mudar uma posição já conhecida custa imagem.")));
        int max = _engine.Context.Campaign.Platform.Regras.BandeirasMax;
        Glossary.Rich(_left, Platform.Flags.Count == 0
            ? string.Format(Tr("Bandeiras da campanha: nenhuma (até {0})."), max)
            : string.Format(Tr("Bandeiras da campanha ({0} de {1}): {2}."), Platform.Flags.Count, max, string.Join(", ", Platform.Flags.Select(ThemeName))), 18, Palette.Texto);
        var known = _engine.Context.Themes.Themes.Select(t => (Theme: t, Known: _engine.KnownOf(t.Id))).Where(x => x.Known >= 0.005).OrderByDescending(x => x.Known).Take(3).ToList();
        Glossary.Rich(_left, known.Count == 0
            ? Tr("Posição conhecida: nenhuma; o eleitor presume a do partido.")
            : Tr("Posição conhecida:") + " " + string.Join("; ", known.Select(x => string.Format(PtBr, "{0} {1}, {2:P0}", x.Theme.Name, PositionText(x.Theme, _engine.PositionOf(x.Theme.Id)), x.Known))) + ".");
        int open = State.Ledger.Promises.Count(p => p.State == PromiseState.Aberta);
        Glossary.Rich(_left, string.Format(PtBr, Tr("Promessas em aberto: {0} · credibilidade {1:P0}."), open, _engine.Credibility));
    }

    // ------------------------------------------------------------------ tela inteira

    private void BuildPlatformSheet()
    {
        BackButton();
        Heading(_left, Tr("Plataforma"));
        FirstTimeHint("plataforma");
        PlatformRules rules = _engine.Context.Campaign.Platform.Regras;
        int open = State.Ledger.Promises.Count(p => p.State == PromiseState.Aberta);
        Glossary.Rich(_left, string.Format(PtBr, Tr("Bandeiras da campanha: {0} de {1}. Mudanças de posição nas últimas {2} semanas: {3}. Promessas em aberto: {4} · credibilidade {5:P0}."),
            Platform.Flags.Count, rules.BandeirasMax, rules.JanelaMudancasSemanas, RecentPositionChanges(), open, _engine.Credibility), 18, Palette.Texto);
        var promise = Ui.Button(_left, "Prometer a uma zona ou a um grupo", () => OpenSheet(Sheet.Prometer, null),
            Palette.Tip(Tr("Abre o catálogo de promessas locais."), Tr("você escolhe a quem e o quê, e vê o apoio previsto e a credibilidade antes de prometer; a promessa entra no Livro de compromissos."), Tr("nada para abrir; cada promessa é cobrada no mandato.")));
        promise.Name = "AbrirPrometer";
        BuildMatrix(_engine.PlatformMatrix());
    }

    /// <summary>
    /// Matriz "Plataforma × públicos" num painel de 400 px: escolhe-se o público (zona ou grupo) e cada tema traz, numa
    /// linha, a sua posição, a do partido e a conhecida e, à direita, o quanto esse público concorda com ela. A outra
    /// leitura (um tema, todos os públicos) fica na tela do tema, que a linha abre.
    /// </summary>
    private void BuildMatrix(IReadOnlyList<PlatformRow> rows)
    {
        var col = Ui.Column(_left, 6);
        col.Name = "Matriz";
        Heading(col, Tr("Plataforma × públicos"), 20);
        var audiences = new List<(string Id, string Text)>();
        audiences.AddRange(_engine.Context.Zonas.Zonas.Select(z => ($"{Promise.ParaZona}|{z.Id}", string.Format(Tr("Zona: {0}"), z.Nome))));
        audiences.AddRange(rows[0].Cells.Where(c => c.TargetKind == Promise.ParaGrupo).Select(c => ($"{Promise.ParaGrupo}|{c.TargetId}", string.Format(Tr("Grupo: {0}"), EffectSummary.GroupName(c.TargetId)))));
        if (_matrixTarget is null || audiences.All(a => a.Id != _matrixTarget))
        {
            _matrixTarget = $"{Promise.ParaZona}|{Territory.Zones[Territory.HomeZone]}";
        }

        var options = Ui.Options(col, audiences, _matrixTarget, id => { _matrixTarget = id; Refresh(); }, translate: false);
        options.Name = "PublicoMatriz";
        options.ClipText = true;
        options.TooltipText = Palette.Tip(Tr("O público da matriz: uma zona ou um grupo de eleitores."), Tr("o número à direita de cada tema passa a dizer o quanto esse público concorda com a sua posição de hoje."), Tr("nada."));
        string[] parts = _matrixTarget.Split('|');
        string audience = AudienceName(parts[0], parts[1]);
        var cells = rows.Select(r => (Row: r, Cell: r.Cells.First(c => c.TargetKind == parts[0] && c.TargetId == parts[1]))).ToList();
        var heavy = cells.OrderByDescending(x => x.Cell.Priority).Take(3).Select(x => x.Row.Theme.Id).ToHashSet();
        Glossary.Rich(col, Tr("À direita: o quanto esse público concorda com a sua posição, de −1 a +1. \"Pesa mais\": os três temas que mais contam para ele."), 18, Palette.Texto3);
        foreach (var (row, cell) in cells)
        {
            ThemeDefinition theme = row.Theme;
            string marks = (row.Flag ? " · " + Tr("bandeira") : "") + (heavy.Contains(theme.Id) ? " · " + Tr("pesa mais") : "");
            PanelContainer card = ClickCard(col, () => OpenSheet(Sheet.Tema, theme.Id),
                Palette.Tip(
                    string.Format(PtBr, Tr("{0}: de −2 ({1}) a +2 ({2}). Você: {3}{4}. {5} prefere {6}; o tema tem peso {7:0.00} para esse público (os pesos de todos os temas somam 1)."),
                        theme.Name, theme.NegativePole, theme.PositivePole, PositionText(theme, row.Position), row.Explicit ? "" : " " + Tr("(a do partido, que você ainda não assumiu em público)"), audience, PositionText(theme, cell.Preference), cell.Priority),
                    Tr("abre o tema: quem gosta e quem não gosta, por grupo e por zona; mudar de posição, com a previsão antes; tornar bandeira."),
                    Tr("nada para abrir; mudar uma posição que já é conhecida custa imagem.")),
                "Tema_" + theme.Id);
            var text = Ui.Column(card, 2);
            HBoxContainer line = Ui.Row(text, 8);
            Glossary.Plain(line, theme.Name + marks, 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
            SignedValue(line, cell.Agreement, "0.0").MouseFilter = MouseFilterEnum.Pass;
            Glossary.Plain(text, string.Format(PtBr, Tr("Você {0} · partido {1} · conhecida {2:P0}"), Signed(row.Position, "0").Replace("+0", "0", StringComparison.Ordinal), Signed(row.PartyPosition, "0.0"), row.Known), 18, Palette.Texto2);
        }
    }

    // ------------------------------------------------------------------ um tema

    private void BuildThemeSheet(ThemeDefinition theme)
    {
        PlatformRules rules = _engine.Context.Campaign.Platform.Regras;
        PlatformRow row = _engine.PlatformMatrix().First(r => r.Theme.Id == theme.Id);
        BackButton();
        NameTitle(_left, theme.Name);
        Glossary.Rich(_left, string.Format(Tr("Escala de −2 ({0}) a +2 ({1})."), theme.NegativePole, theme.PositivePole));
        Glossary.Rich(_left, string.Format(Tr("Sua posição: {0}{1}."), PositionText(theme, row.Position), row.Explicit ? "" : " " + Tr("(é a do partido; você ainda não a assumiu em público)")), 18, Palette.Texto);
        ValueRow(_left, Tr("Posição do partido"), Signed(row.PartyPosition, "0.0"));
        ValueRow(_left, Tr("Posição conhecida (eleitores que sabem)"), row.Known.ToString("P0", PtBr), link: true);
        Glossary.Rich(_left, Tr("Quem não conhece a sua posição presume a do partido. Ela fica conhecida quando você fala do tema em entrevista, gravação ou comício."), 18, Palette.Texto3);

        // Bandeira.
        bool flag = Platform.Flags.Contains(theme.Id);
        string flagTip = Palette.Tip(
            Tr(flag ? "Tira este tema das bandeiras da campanha." : "Põe este tema entre as bandeiras da campanha."),
            string.Format(PtBr, Tr("tema de bandeira fica conhecido {0:0.#} vezes mais depressa quando você fala dele e um pouco a cada semana, mesmo sem fala; quem não gosta da posição também fica sabendo."), rules.BandeiraBonus),
            string.Format(Tr("nada em dinheiro; ocupa uma das {0} bandeiras."), rules.BandeirasMax));
        var flagButton = Ui.Button(_left, flag ? "Deixar de ser bandeira" : "Tornar bandeira da campanha", () =>
        {
            List<string> flags = flag ? Platform.Flags.Where(f => f != theme.Id).ToList() : [.. Platform.Flags, theme.Id];
            CommandFailure? failure = _engine.SetFlags(flags);
            _log.Insert(0, failure is null
                ? string.Format(Tr(flag ? "{0} deixou de ser bandeira da campanha." : "{0} virou bandeira da campanha."), theme.Name)
                : string.Format(Tr("A campanha já tem {0} bandeiras."), rules.BandeirasMax));
            Refresh();
        }, flagTip);
        flagButton.Name = "Bandeira";
        if (flag)
        {
            Palette.Selected(flagButton);
        }
        else if (Platform.Flags.Count >= rules.BandeirasMax)
        {
            flagButton.Disabled = true;
            flagButton.TooltipText = Palette.Blocked(string.Format(Tr("a campanha já tem {0} bandeiras ({1}); tire uma antes."), rules.BandeirasMax, string.Join(", ", Platform.Flags.Select(ThemeName))), flagTip);
        }

        // Mudar de posição: a previsão vem antes de confirmar.
        Heading(_left, Tr("Assumir ou mudar de posição"), 20);
        HBoxContainer positions = Ui.Row(_left, 6);
        for (int value = ThemeDefinition.MinPosition; value <= ThemeDefinition.MaxPosition; value++)
        {
            int v = value;
            string tip = Palette.Tip(
                string.Format(Tr("Posição {0} em {1}."), PositionText(theme, v), theme.Name),
                Tr("mostra a previsão por grupo e por zona e o custo de incoerência; só vale depois de confirmar."),
                Tr("nada para ver a previsão."));
            var b = Ui.Button(positions, Signed(v, "0").Replace("+0", "0", StringComparison.Ordinal), () => { _positionDraft = v; _positionResult = null; Refresh(); }, tip);
            b.Name = "Posicao_" + v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            b.CustomMinimumSize = new Vector2(56, 44);
            if (_positionDraft == v)
            {
                Palette.Selected(b);
            }
            else if (row.Position == v)
            {
                Palette.ListRow(b, selected: true);
            }

            if (_engine.CanSetPosition(theme.Id, v) is CommandFailure.MesmaPosicao)
            {
                b.Disabled = true;
                b.TooltipText = Palette.Blocked(Tr("essa já é a posição que você assumiu em público; escolha outra para mudar."), tip);
            }
        }

        if (_positionResult is not null)
        {
            PanelContainer result = Card(_left, Palette.Cartao, Palette.Linha);
            result.Name = "MudancaPosicao";
            var col = Ui.Column(result, 6);
            Heading(col, Tr("O que mudou"), 20);
            foreach (string line in _positionResult)
            {
                Glossary.Rich(col, "· " + line, 18, Palette.Texto);
            }
        }
        else if (_positionDraft is { } draft)
        {
            PositionForecast(theme, draft);
        }

        // A linha da matriz deste tema: quem gosta e quem não gosta, por grupo e por zona.
        Heading(_left, Tr("Quem gosta e quem não gosta"), 20);
        Glossary.Rich(_left, Tr("De −1 (discorda de todo) a +1 (concorda de todo) com a sua posição de hoje."), 18, Palette.Texto3);
        var groups = row.Cells.Where(c => c.TargetKind == Promise.ParaGrupo).OrderByDescending(c => c.Agreement).ToList();
        FieldLabel("Por grupo: os três que mais concordam e os três que menos");
        foreach (PlatformCell cell in groups.Take(3).Concat(groups.Skip(Math.Max(3, groups.Count - 3))))
        {
            SignedRow(_left, EffectSummary.GroupName(cell.TargetId), cell.Agreement, "0.0",
                string.Format(PtBr, Tr("Esse grupo prefere {0}; o tema tem peso {1:0.00} para ele."), PositionText(theme, cell.Preference), cell.Priority));
        }

        var zones = row.Cells.Where(c => c.TargetKind == Promise.ParaZona).OrderByDescending(c => c.Agreement).ToList();
        FieldLabel(zones.Count > 8 ? "Por zona: as quatro que mais concordam e as quatro que menos" : "Por zona");
        foreach (PlatformCell cell in zones.Count > 8 ? zones.Take(4).Concat(zones.Skip(zones.Count - 4)) : zones)
        {
            SignedRow(_left, ZoneName(cell.TargetId), cell.Agreement, "0.0",
                string.Format(PtBr, Tr("Os eleitores dessa zona preferem {0}; o tema tem peso {1:0.00} para eles."), PositionText(theme, cell.Preference), cell.Priority));
        }

        var history = State.Ledger.Positions.Where(p => p.Theme == theme.Id).Reverse().Take(4).ToList();
        if (history.Count > 0)
        {
            Heading(_left, Tr("O que você já disse"), 20);
            foreach (StatedPosition stated in history)
            {
                Glossary.Rich(_left, string.Format(Tr("· {0}: {1}, em {2}"), stated.Date.ToString("d MMM yyyy", PtBr), PositionText(theme, stated.Value), LowerFirst(stated.Where)));
            }
        }
    }

    /// <summary>Previsão de mudar de posição (princípio f): custo de incoerência e votos por grupo e por zona, antes de confirmar.</summary>
    private void PositionForecast(ThemeDefinition theme, int value)
    {
        CommandForecast forecast = _engine.ForecastPosition(theme.Id, value);
        PlatformRules rules = _engine.Context.Campaign.Platform.Regras;
        PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha);
        card.Name = "PrevisaoPosicao";
        var col = Ui.Column(card, 6);
        Heading(col, Tr("Previsão"), 20);

        // As linhas "Ganha"/"Perde" do Core repetem os extremos das listas abaixo.
        string gains = Tr("Ganha") + " ", loses = Tr("Perde") + " ";
        foreach (string line in forecast.Explanation.Where(l => !l.StartsWith(gains, StringComparison.Ordinal) && !l.StartsWith(loses, StringComparison.Ordinal)))
        {
            Glossary.Rich(col, "· " + line, 18, Palette.Texto);
        }

        int recent = RecentPositionChanges();
        Glossary.Rich(col, string.Format(Tr(recent == 1 ? "Você mudou de posição {0} vez nas últimas {1} semanas; cada mudança recente aumenta o custo da próxima." : "Você mudou de posição {0} vezes nas últimas {1} semanas; cada mudança recente aumenta o custo da próxima."), recent, rules.JanelaMudancasSemanas));

        void Section(string title, string kind, Func<string, string> name, int take)
        {
            // Em disputa de vereador a fatia de cada candidato é pequena: o que diz se a mudança é grande é a variação
            // dos próprios votos ali (%); os pontos percentuais dos votos válidos ficam no tooltip.
            var lines = forecast.Lines.Where(l => l.Kind == kind + "_pct" && l.Target is not null).ToList();
            var shown = lines.Where(l => Math.Abs(l.Expected) >= 0.05).OrderByDescending(l => l.Expected).ToList();
            if (shown.Count > 2 * take)
            {
                shown = shown.Take(take).Concat(shown.Skip(shown.Count - take)).ToList();
            }

            Glossary.Rich(col, Tr(title), 18, Palette.Texto3);
            if (shown.Count == 0)
            {
                Glossary.Rich(col, Tr("Ninguém muda de voto por isso hoje (menos de 0,1% dos seus votos)."));
            }

            foreach (ForecastLine line in shown)
            {
                ForecastLine? points = forecast.Line(kind, line.Target);
                SignedRow(col, name(line.Target!), line.Expected, "0.0",
                    string.Format(Tr("Variação dos seus votos ali: de {0}% a {1}%, conforme a posição ficar conhecida. Em pontos percentuais dos votos válidos dali: {2}."), Signed(line.Min, "0.0"), Signed(line.Max, "0.0"), Signed(points?.Expected ?? 0, "0.00")), "%");
            }
        }

        Section("Por grupo: quanto os seus votos entre eles mudam", "votes_group", EffectSummary.GroupName, 3);
        Section("Por zona: quanto os seus votos ali mudam", "votes_zone", ZoneName, 4);

        HBoxContainer row = Ui.Row(col, 8);
        double incoherence = forecast.Line("incoherence", theme.Id)?.Expected ?? 0;
        double image = forecast.Line("image")?.Expected ?? 0;
        ForecastLine? city = forecast.Line("votes");
        string tip = Palette.Tip(
            string.Format(Tr("Assume em público a posição {0} em {1}."), PositionText(theme, value), theme.Name),
            (city is null ? "" : string.Format(Tr("na cidade, de {0} a {1} ponto dos votos válidos; "), Signed(city.Min, "0.00"), Signed(city.Max, "0.00"))) + Tr("grava a posição no Livro de compromissos, com a data."),
            incoherence > 0 ? string.Format(PtBr, Tr("incoerência {0:0.00}: imagem {1}; lideranças com interesse no tema lembram."), incoherence, Signed(image, "0.000")) : Tr("nada agora; a posição assumida passa a custar caro para mudar."));
        var confirm = Ui.Button(row, "Confirmar a posição", () =>
        {
            int before = _engine.PositionOf(theme.Id);
            var (failure, result) = _engine.SetPosition(theme.Id, value);
            if (failure is null && result is not null)
            {
                var lines = new List<string>
                {
                    string.Format(Tr("{0}: de {1} para {2}, anunciada em público."), theme.Name, PositionText(theme, before), PositionText(theme, value)),
                    string.Format(PtBr, Tr("Imagem: {0}. Incoerência: {1:0.00}."), Signed(result.ImageDelta, "0.000"), result.Change.Incoherence),
                    string.Format(PtBr, Tr("Agora {0:P0} dos eleitores conhecem a sua posição no tema."), _engine.KnownOf(theme.Id)),
                };
                if (result.LeadersWhoRemember.Count > 0)
                {
                    lines.Add(string.Format(Tr("Quem vai lembrar: {0}."), string.Join(", ", result.LeadersWhoRemember.Select(id => _engine.City.Leader(id) is { } leader ? LeaderLabel(leader) : _engine.NameOf(id)))));
                }

                lines.Add(string.Format(Tr("Posição assumida no Livro de compromissos, com a data de {0}."), result.Stated.Date.ToString("dd/MM/yyyy", PtBr)));
                _positionResult = lines;
                _log.Insert(0, lines[0]);
            }

            _positionDraft = null;
            Refresh();
        }, tip);
        confirm.Name = "ConfirmarPosicao";
        confirm.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (forecast.Blocked is { } blocked)
        {
            confirm.Disabled = true;
            confirm.TooltipText = Palette.Blocked(blocked == CommandFailure.MesmaPosicao ? Tr("essa já é a posição que você assumiu em público.") : Tr("só dá para mudar de posição durante a pré-campanha e a campanha."), tip);
        }
        else
        {
            Palette.Primary(confirm);
        }

        Ui.Button(row, "Cancelar", () => { _positionDraft = null; Refresh(); }, Palette.Tip(Tr("Fecha a previsão sem mudar nada."), Tr("a sua posição continua a de antes."), Tr("nada."))).Name = "CancelarPosicao";
    }

    // ------------------------------------------------------------------ prometer

    private void BuildPromiseSheet()
    {
        PromiseCatalog catalog = _engine.Context.Campaign.Promises;
        BackButton();
        Heading(_left, Tr("Prometer"));
        Glossary.Rich(_left, string.Format(PtBr, Tr("Promessa local: você se compromete com uma obra ou um serviço para uma zona ou um grupo. Rende apoio na hora e entra no Livro de compromissos; no mandato, é cobrada. Credibilidade hoje: {0:P0}."), _engine.Credibility), 18, Palette.Texto);

        var audiences = new List<(string Id, string Text)>();
        audiences.AddRange(_engine.Context.Zonas.Zonas.Where(z => _engine.PromiseOptions(Promise.ParaZona, z.Id).Count > 0)
            .Select(z => ($"{Promise.ParaZona}|{z.Id}", string.Format(Tr("Zona: {0}"), z.Nome))));
        audiences.AddRange(catalog.Promessas.SelectMany(p => p.Grupos).Distinct().Where(g => _engine.PromiseOptions(Promise.ParaGrupo, g).Count > 0)
            .Select(g => ($"{Promise.ParaGrupo}|{g}", string.Format(Tr("Grupo: {0}"), EffectSummary.GroupName(g)))));
        if (audiences.Count == 0)
        {
            Glossary.Rich(_left, Tr("Não há promessa do catálogo que caiba a alguém desta cidade agora."));
            return;
        }

        if (_promiseTarget is null || audiences.All(a => a.Id != _promiseTarget))
        {
            string home = $"{Promise.ParaZona}|{Territory.Zones[Territory.HomeZone]}";
            _promiseTarget = audiences.Any(a => a.Id == home) ? home : audiences[0].Id;
        }

        FieldLabel("A quem");
        var who = Ui.Options(_left, audiences, _promiseTarget, id => { _promiseTarget = id; _promiseId = null; _promiseResult = null; Refresh(); }, translate: false);
        who.Name = "AlvoPromessa";
        who.ClipText = true;
        who.TooltipText = Palette.Tip(Tr("A zona ou o grupo de eleitores que recebe a promessa."), Tr("o apoio vem de quem recebe; só aparecem as promessas que cabem a esse público (rua para zona urbana, estrada para zona rural)."), Tr("nada para escolher."));
        string[] parts = _promiseTarget.Split('|');
        var options = _engine.PromiseOptions(parts[0], parts[1]);
        if (_promiseId is null || options.All(o => o.Definition.Id != _promiseId))
        {
            _promiseId = (options.FirstOrDefault(o => o.Blocked is null) ?? options[0]).Definition.Id;
        }

        FieldLabel("O quê");
        var what = Ui.Options(_left, options.Select(o => (o.Definition.Id, o.Definition.Nome + (o.Blocked is CommandFailure.JaPrometido ? " · " + Tr("já prometido") : ""))), _promiseId, id => { _promiseId = id; _promiseResult = null; Refresh(); }, translate: false);
        what.Name = "Promessa";
        what.ClipText = true;
        what.TooltipText = Palette.Tip(Tr("A obra ou o serviço prometido."), Tr("cada promessa tem um apoio de base e um prazo depois da posse; a previsão abaixo muda com a escolha."), Tr("nada para escolher; prometer custa credibilidade quando há promessas demais em aberto."));

        PromiseOption option = options.First(o => o.Definition.Id == _promiseId);
        CommandForecast forecast = _engine.ForecastPromise(option.Definition.Id, parts[0], parts[1]);
        string audience = AudienceName(parts[0], parts[1]);
        var supports = forecast.Lines.Where(l => l.Kind == "support").ToList();
        string supportText = supports.Count == 0 ? Tr("sem apoio previsto")
            : string.Format(Tr("pontos de apoio de {0}: {1}"), audience, Range(supports.Min(l => l.Min) * 100, supports.Max(l => l.Max) * 100, "0.0"));
        double after = forecast.Line("credibility")?.Expected ?? _engine.Credibility;
        PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha);
        card.Name = "PrevisaoPromessa";
        var col = Ui.Column(card, 6);
        Heading(col, Tr("Previsão"), 20);
        Glossary.Rich(col, "· " + option.Text + ".", 18, Palette.Texto);
        Glossary.Rich(col, "· " + char.ToUpper(supportText[0], PtBr) + supportText[1..] + ", " + Tr("na hora."), 18, Palette.Texto);
        foreach (string line in forecast.Explanation.Skip(1))
        {
            Glossary.Rich(col, "· " + line, 18, Palette.Texto);
        }

        Glossary.Rich(col, string.Format(Tr("Quem vai lembrar: os eleitores de {0}; no mandato, a promessa é cobrada."), audience));
        if (_promiseResult is not null)
        {
            PanelContainer result = Card(_left, Palette.Cartao, Palette.Linha);
            result.Name = "PromessaFeita";
            var changed = Ui.Column(result, 6);
            Heading(changed, Tr("O que mudou"), 20);
            foreach (string line in _promiseResult)
            {
                Glossary.Rich(changed, "· " + line, 18, Palette.Texto);
            }
        }

        string tip = Palette.Tip(
            string.Format(Tr("Promete em público: {0}."), LowerFirst(option.Text)),
            supportText + "; " + string.Format(PtBr, Tr("a promessa entra no Livro de compromissos e a credibilidade vai a {0:P0}."), after),
            Tr("nada agora; a promessa é cobrada no mandato, e não cumprir derruba a credibilidade."));
        var confirm = Ui.Button(_left, "Prometer", () =>
        {
            var (failure, result) = _engine.MakePromise(option.Definition.Id, parts[0], parts[1]);
            if (failure is null && result is not null)
            {
                _promiseResult =
                [
                    string.Format(Tr("Promessa no Livro de compromissos: {0}. Prazo: {1}."), result.Promise.Text, result.Promise.Deadline.ToString("dd/MM/yyyy", PtBr)),
                    result.Support.Count == 0 ? Tr("Sem apoio novo desta vez.") : string.Format(Tr("Pontos de apoio de {0}: {1}."), audience, Signed(result.Support.Values.Max() * 100, "0.0")),
                    string.Format(PtBr, Tr("Credibilidade agora: {0:P0}."), result.Credibility),
                ];
                _log.Insert(0, string.Format(Tr("Você prometeu: {0}."), LowerFirst(option.Text)));
            }

            Refresh();
        }, tip);
        confirm.Name = "ConfirmarPromessa";
        if (forecast.Blocked is { } blocked)
        {
            confirm.Disabled = true;
            confirm.TooltipText = Palette.Blocked(blocked == CommandFailure.JaPrometido ? Tr("você já fez essa promessa a esse público e ela continua em aberto; falta cumprir ou escolher outra.") : Tr("só dá para prometer durante a pré-campanha e a campanha."), tip);
        }
        else
        {
            Palette.Primary(confirm);
        }
    }
}
