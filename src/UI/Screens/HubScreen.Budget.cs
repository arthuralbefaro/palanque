using Godot;
using Planalto.Core.Campaign;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Verba por canal (GDD 17.8): o bloco da área Campanha, a tela inteira com os quatro canais e o resultado explicado
/// da semana passada, e a tela de um canal (alvo, valor em passos e a previsão com as duas contas: retorno da própria
/// verba e disputa com a verba dos rivais). Rádio, TV, outdoor e brinde não existem como canal: a lei veda o gasto.
/// </summary>
public partial class HubScreen
{
    private readonly Dictionary<string, string> _channelTarget = new(StringComparer.Ordinal);
    private readonly Dictionary<string, decimal> _channelDraft = new(StringComparer.Ordinal);

    private ChannelCatalog Channels => _engine.Context.Campaign.Channels;

    private string TargetName(ChannelDefinition c, string targetId) =>
        c.Alvo == ChannelDefinition.AlvoZona ? ZoneName(targetId) : Channels.Band(targetId)?.Nome ?? targetId;

    private decimal ChannelPlan(string channelId) => _engine.Budget.Plan.Where(a => a.ChannelId == channelId).Sum(a => a.Amount);

    private int WeeksToOfficialCampaign => Math.Max(0, (Campaign.CampaignStart.DayNumber - _engine.Today.DayNumber + 6) / 7);

    private void SetChannelTarget(string channelId, string targetId) => _channelTarget[channelId] = targetId;

    private string ChannelTargetOf(ChannelDefinition c)
    {
        var targets = _engine.ChannelTargets(c.Id);
        if (_channelTarget.TryGetValue(c.Id, out string? chosen) && targets.Any(t => t.Id == chosen))
        {
            return chosen;
        }

        return c.Alvo == ChannelDefinition.AlvoZona ? Territory.Zones[Territory.HomeZone] : targets[0].Id;
    }

    private decimal DraftAmount(ChannelDefinition c, string targetId) =>
        _channelDraft.TryGetValue(BudgetState.Key(c.Id, targetId), out decimal value) ? value : _engine.Budget.PlanOf(c.Id, targetId);

    /// <summary>Barra horizontal (ART_GUIDE: a barra de verba usa a cor de ação).</summary>
    private static Control Bar(Node parent, double fraction, Color color)
    {
        var back = parent.Add(new Panel { CustomMinimumSize = new Vector2(48, 8), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        back.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Linha, 4, 0));
        var fill = back.Add(new Panel { MouseFilter = MouseFilterEnum.Ignore });
        fill.AddThemeStyleboxOverride("panel", Palette.Box(color, 4, 0));
        fill.AnchorRight = (float)Math.Clamp(fraction, 0, 1);
        fill.AnchorBottom = 1;
        return back;
    }

    /// <summary>O que o canal pode antes de 16 de agosto e por quê; null na campanha oficial.</summary>
    private string? PhaseText(ChannelDefinition c)
    {
        if (!_engine.BeforeOfficialCampaign)
        {
            return null;
        }

        string date = Campaign.CampaignStart.ToString("d 'de' MMMM", PtBr);
        if (_engine.PreCampaignCap(c.Id) is not { } cap)
        {
            return string.Format(Tr("Fechado até {0} (faltam {1} semanas): é propaganda eleitoral, que a lei só permite a partir dessa data (Lei 9.504/1997, art. 36)."), date, WeeksToOfficialCampaign);
        }

        return c.Impulsionamento
            ? string.Format(PtBr, Tr("Antes de {0} (faltam {1} semanas): até {2} por semana, só produção de conteúdo, sem impulsionamento pago, que é propaganda eleitoral ({3} e art. 36). Rende {4:P0} do normal."), date, WeeksToOfficialCampaign, Money(cap), _engine.Context.ChannelLaw.ArtigoImpulsionamento, c.PreCampanha!.Fator)
            : string.Format(PtBr, Tr("Antes de {0} (faltam {1} semanas): até {2} por semana, só material institucional do partido, sem pedido de voto (Lei 9.504/1997, art. 36-A). Rende {3:P0} do normal."), date, WeeksToOfficialCampaign, Money(cap), c.PreCampanha!.Fator);
    }

    /// <summary>Por que a verba não pode ser aplicada e o que falta (princípio j).</summary>
    private string BudgetFailureText(CommandFailure failure, ChannelDefinition c, string targetId, decimal amount)
    {
        string date = Campaign.CampaignStart.ToString("d 'de' MMMM", PtBr);
        switch (failure)
        {
            case CommandFailure.CanalFechado:
                return _engine.PreCampaignCap(c.Id) is { } cap
                    ? string.Format(Tr("antes de {0} este canal aceita até {1} por semana (faltam {2} semanas para abrir)."), date, Money(cap), WeeksToOfficialCampaign)
                    : string.Format(Tr("o canal {0} só abre em {1} (faltam {2} semanas): antes disso é propaganda antecipada (Lei 9.504/1997, art. 36)."), c.Nome, date, WeeksToOfficialCampaign);
            case CommandFailure.LimiteLegal when c.EmGrupos:
                return string.Format(Tr("passa do limite legal de {0} contratações para a sua candidatura ({1}); o plano já tem {2}."), _engine.CabosLimit, _engine.Context.ChannelLaw.ArtigoCabos, _engine.CabosPlanned);
            case CommandFailure.LimiteLegal:
                return string.Format(Tr("acima de {0} por semana é impulsionamento pago, que só vale a partir de {1} ({2}); faltam {3} semanas."), Money(_engine.PreCampaignCap(c.Id) ?? 0), date, _engine.Context.ChannelLaw.ArtigoImpulsionamento, WeeksToOfficialCampaign);
            case CommandFailure.SemVaga:
                return string.Format(Tr("a rede de cabos ocupa uma vaga da equipe e as {0} desta cidade estão ocupadas; falta abrir uma vaga."), _engine.TeamSlots);
            case CommandFailure.SemDinheiro:
                decimal increase = amount - _engine.Budget.ActiveOf(c.Id, targetId);
                return string.Format(Tr("faltam {0} em caixa: o aumento desta semana ({1}) sai agora."), Money(increase - State.Player.Resources.Money), Money(increase));
            case CommandFailure.GastoVedado:
                return Tr("a lei veda esse gasto.");
            case CommandFailure.ForaDaCampanha:
                return Tr("a verba só existe durante a pré-campanha e a campanha.");
            default:
                return Tr("falta escolher o alvo e um valor válido.");
        }
    }

    /// <summary>Linhas de efeito da previsão de um canal (presença e apoio em faixa), vindas do Core.</summary>
    private List<string> ChannelEffectLines(ChannelDefinition c, string targetId, CommandForecast f)
    {
        var lines = new List<string>();
        string target = TargetName(c, targetId);
        if (f.Lines.FirstOrDefault(l => l.Kind == "presence") is { } presence && presence.Max >= 0.005)
        {
            lines.Add(c.Alvo == ChannelDefinition.AlvoZona
                ? string.Format(Tr("presença em {0}: {1} por semana"), target, Range(presence.Min, presence.Max, "0.00"))
                : string.Format(Tr("presença em todas as zonas (em média): {0} por semana"), Range(presence.Min, presence.Max, "0.00")));
        }

        var support = f.Lines.Where(l => l.Kind == "support").ToList();
        if (support.Count > 0 && support.Max(l => l.Max) > 0)
        {
            lines.Add(c.Alvo == ChannelDefinition.AlvoZona
                ? string.Format(Tr("pontos de apoio dos eleitores de {0}: {1} por semana"), target, Range(support.Min(l => l.Min) * 100, support.Max(l => l.Max) * 100, "0.00"))
                : string.Format(Tr("pontos de apoio de quem tem {0}: {1} por semana"), target, Range(support.Min(l => l.Min) * 100, support.Max(l => l.Max) * 100, "0.00")));
        }

        return lines;
    }

    /// <summary>Resultado explicado de um canal na semana passada: o que rendeu, as duas contas e os fatores.</summary>
    private string ChannelResultText(ChannelResult r, bool full)
    {
        ChannelDefinition c = Channels.Get(r.ChannelId);
        var parts = new List<string>();
        if (c.Alvo == ChannelDefinition.AlvoZona && r.Presence.TryGetValue(r.TargetId, out double here))
        {
            parts.Add(string.Format(Tr("{0} de presença"), Signed(here, "0.00")));
        }
        else if (r.Presence.Count > 0)
        {
            parts.Add(string.Format(Tr("{0} de presença em todas as zonas (em média)"), Signed(r.Presence.Values.Average(), "0.00")));
        }

        if (r.Support.Count > 0 && r.Support.Values.Max() > 0)
        {
            parts.Add(string.Format(Tr("até {0} pontos de apoio por grupo de idade"), Signed(r.Support.Values.Max() * 100, "0.00")));
        }

        string text = string.Format(Tr("{0} em {1}, {2}: {3}."), c.Nome, TargetName(c, r.TargetId), Money(r.Spent), parts.Count == 0 ? Tr("sem efeito nos números") : string.Join("; ", parts));
        if (!full)
        {
            return text;
        }

        text += " " + string.Format(PtBr, Tr("Contas: retorno {0:0.00} × disputa {1:P0} (rivais com cerca de {2} ali) = efeito {3:0.00}."), r.Return, r.Share, Money(r.RivalBudget), r.Effect);
        if (r.Factors.Count > 0)
        {
            text += " " + Tr("Fatores:") + " " + string.Join(", ", r.Factors.Select(f => $"{LowerFirst(f.Text)} ({Percent(f.Multiplier)})")) + ".";
        }

        return text;
    }

    // ------------------------------------------------------------------ bloco da área Campanha

    private void BuildBudgetBlock()
    {
        BudgetView view = _engine.BudgetSummary();
        BlockHeader(Tr("Verba por canal"), "VerVerba", () => OpenSheet(Sheet.Verba, null),
            Palette.Tip(Tr("Abre a Verba por canal inteira."), Tr("mostra os quatro canais pagos, os alvos, a previsão com as duas contas e o resultado explicado da semana passada."), Tr("nada para abrir; a verba repartida sai do caixa toda segunda-feira.")));
        decimal max = Math.Max(1, Channels.Canais.Max(c => ChannelPlan(c.Id)));
        foreach (ChannelDefinition c in Channels.Canais)
        {
            decimal plan = ChannelPlan(c.Id);
            HBoxContainer row = Ui.Row(_left, 8);
            RichTextLabel name = Glossary.Rich(row, c.Nome, 18, Palette.Texto2);
            name.AutowrapMode = TextServer.AutowrapMode.Off;
            name.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            name.CustomMinimumSize = new Vector2(150, 0);
            Bar(row, (double)(plan / max), Palette.Acao);
            Label value = Ui.Value(row, Money(plan), 18);
            value.AddThemeColorOverride("font_color", Palette.Texto);
            value.CustomMinimumSize = new Vector2(84, 0);
            value.HorizontalAlignment = HorizontalAlignment.Right;
        }

        Glossary.Rich(_left, view.PlanWeekly + view.PayrollWeekly > 0
            ? string.Format(Tr("Gasto da semana: {0} em canais e {1} de folha. O caixa paga {2}."), Money(view.PlanWeekly), Money(view.PayrollWeekly), WeeksOfCash(view.WeeksOfCash))
            : Tr("Gasto da semana: nada em canais nem em folha."));
        var results = _engine.Budget.LastResults;
        if (results.Count > 0)
        {
            Glossary.Rich(_left, Tr("Semana passada:") + " " + string.Join(" ", results.Take(2).Select(r => ChannelResultText(r, full: false))) + (results.Count > 2 ? " " + string.Format(Tr("E mais {0}."), results.Count - 2) : ""), 18, Palette.Texto);
        }
        else if (view.PlanWeekly > 0)
        {
            Glossary.Rich(_left, Tr("O resultado explicado da verba aparece aqui na virada da semana."), 18, Palette.Texto3);
        }
    }

    // ------------------------------------------------------------------ tela inteira

    private void BuildBudgetSheet()
    {
        BackButton();
        Heading(_left, Tr("Verba por canal"));
        FirstTimeHint("verba");
        BudgetView view = _engine.BudgetSummary();
        Glossary.Rich(_left, string.Format(Tr("Por semana: {0} em canais e {1} de folha. O caixa de hoje ({2}) paga {3}. A verba sai toda segunda-feira; o aumento feito no meio da semana sai na hora."),
            Money(view.PlanWeekly), Money(view.PayrollWeekly), Money(State.Player.Resources.Money), WeeksOfCash(view.WeeksOfCash)), 18, Palette.Texto);
        if (_engine.BeforeOfficialCampaign)
        {
            PanelContainer phase = Card(_left, Palette.Cartao, Palette.Linha, 10);
            phase.Name = "BloqueioPreCampanha";
            Glossary.Rich(phase, string.Format(Tr("Antes de {0} (faltam {1} semanas) a lei não permite propaganda eleitoral (Lei 9.504/1997, art. 36): Rua e Cabos eleitorais ficam fechados; Redes e Material gráfico aceitam um valor pequeno, sem impulsionamento e sem pedido de voto."),
                Campaign.CampaignStart.ToString("d 'de' MMMM", PtBr), WeeksToOfficialCampaign), 18, Palette.Texto);
        }

        decimal max = Math.Max(1, Channels.Canais.Max(c => ChannelPlan(c.Id)));
        foreach (ChannelDefinition c in Channels.Canais)
        {
            decimal plan = ChannelPlan(c.Id);
            bool closed = _engine.BeforeOfficialCampaign && _engine.PreCampaignCap(c.Id) is null;
            string where = string.Join(", ", _engine.Budget.Plan.Where(a => a.ChannelId == c.Id && a.Amount > 0).OrderByDescending(a => a.Amount).Take(3).Select(a => $"{TargetName(c, a.TargetId)} {Money(a.Amount)}"));
            string status = closed ? string.Format(Tr("fechado até {0}"), Campaign.CampaignStart.ToString("d/MM", PtBr))
                : where.Length > 0 ? where
                : Tr("sem verba");
            PanelContainer card = ClickCard(_left, () => OpenSheet(Sheet.Canal, c.Id),
                Palette.Tip($"{c.Nome}: {c.Descricao}", Tr("abre o canal: você escolhe o alvo e o valor e vê a previsão com as duas contas antes de aplicar."), string.Format(Tr("nada para abrir; hoje este canal leva {0} por semana."), Money(plan))),
                "Canal_" + c.Id);
            var col = Ui.Column(card, 4);
            HBoxContainer row = Ui.Row(col, 8);
            RichTextLabel name = Glossary.Plain(row, c.Nome, 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
            name.AutowrapMode = TextServer.AutowrapMode.Off;
            name.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            name.CustomMinimumSize = new Vector2(150, 0);
            Bar(row, (double)(plan / max), Palette.Acao);
            Label value = Ui.Value(row, Money(plan), 18);
            value.AddThemeColorOverride("font_color", Palette.Texto);
            value.MouseFilter = MouseFilterEnum.Pass;
            Glossary.Rich(col, string.Format(Tr("Alvo: {0}. {1}."), c.Alvo == ChannelDefinition.AlvoZona ? Tr("zonas") : Tr("faixa de idade"), char.ToUpper(status[0], PtBr) + status[1..]));
        }

        Glossary.Rich(_left, string.Format(Tr("Cabos eleitorais: {0} de {1} contratações permitidas para a sua candidatura ({2})."), view.CabosPeople, view.CabosLimit, _engine.Context.ChannelLaw.ArtigoCabos), 18, Palette.Texto);

        Glossary.Rich(_left, Tr("Rádio, TV, outdoor e brinde não são canal de verba: a lei proíbe esse gasto. No rádio e na TV você aparece por entrevista e pelo horário eleitoral."));
        Ui.Button(_left, "O que a lei permite na propaganda", () => EncyclopediaDialog.OpenArticle(GetTree().Root, "propaganda"),
            Palette.Tip(Tr("Abre a Enciclopédia no artigo sobre propaganda."), Tr("explica o que pode e o que não pode na campanha, com os artigos da lei."), Tr("nada; o jogo fica pausado."))).Name = "LeiPropaganda";

        Heading(_left, Tr("Semana passada"), 20);
        var results = _engine.Budget.LastResults;
        if (results.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma verba rendeu na semana passada. O resultado de cada canal aparece aqui na virada da semana, com as contas e os fatores."));
        }

        foreach (ChannelResult r in results)
        {
            PanelContainer card = Card(_left, margin: 10);
            card.Name = "ResultadoVerba_" + results.IndexOf(r);
            Glossary.Rich(card, ChannelResultText(r, full: true));
        }
    }

    private void BuildChannelSheet(ChannelDefinition c)
    {
        BackButton();
        Heading(_left, c.Nome);
        Glossary.Rich(_left, c.Descricao);
        if (PhaseText(c) is { } phase)
        {
            PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
            card.Name = "FaseCanal";
            Glossary.Rich(card, phase, 18, Palette.Texto);
        }

        if (c.EmGrupos)
        {
            Glossary.Rich(_left, string.Format(Tr("Limite legal: {0} de {1} contratações permitidas ({2}). Cada grupo tem {3} pessoas e custa {4} por semana. A rede de cabos ocupa uma vaga da equipe."),
                _engine.CabosPlanned, _engine.CabosLimit, _engine.Context.ChannelLaw.ArtigoCabos, Channels.Regras.CabosPorGrupo, Money(_engine.CabosGroupCost)), 18, Palette.Texto);
        }

        var targets = _engine.ChannelTargets(c.Id);
        string targetId = ChannelTargetOf(c);
        bool byZone = c.Alvo == ChannelDefinition.AlvoZona;
        FieldLabel(byZone ? "Alvo: zona (ou clique no mapa)" : "Alvo: faixa de idade");
        var options = Ui.Options(_left, targets.Select(t => (t.Id, $"{t.Name} · {Money(_engine.Budget.PlanOf(c.Id, t.Id))}")), targetId, id =>
        {
            SetChannelTarget(c.Id, id);
            if (byZone)
            {
                _map.Select(id);
            }

            Refresh();
        }, translate: false);
        options.Name = "Alvo";
        options.ClipText = true;
        options.TooltipText = Palette.Tip(
            Tr(byZone ? "A zona que recebe a verba deste canal." : "A faixa de idade que recebe a verba deste canal."),
            Tr(byZone ? "a verba rende presença e apoio só nesse alvo; onde os rivais também gastam, a sua fatia rende menos. Ao lado de cada nome, a verba de hoje." : "o conteúdo chega a quem tem essa idade em toda a cidade; rende mais entre os mais jovens. Ao lado de cada nome, a verba de hoje."),
            Tr("nada para escolher; o custo é a verba que você definir."));

        decimal step = c.EmGrupos ? _engine.CabosGroupCost : _engine.ChannelStep;
        decimal active = _engine.Budget.ActiveOf(c.Id, targetId);
        decimal plan = _engine.Budget.PlanOf(c.Id, targetId);
        decimal amount = DraftAmount(c, targetId);
        string key = BudgetState.Key(c.Id, targetId);
        FieldLabel("Verba por semana neste alvo");
        HBoxContainer stepper = Ui.Row(_left, 8);
        string lessTip = Palette.Tip(string.Format(Tr("Tira {0} da verba semanal."), Money(step)), Tr("a previsão abaixo muda na hora; só vale depois de Aplicar. Redução só conta a partir de segunda-feira: o que já foi pago nesta semana rende até domingo."), Tr("nada; diminuir a verba não devolve o que já saiu."));
        var less = Ui.Button(stepper, "− " + Money(step), () => { _channelDraft[key] = Math.Max(0, amount - step); Refresh(); }, lessTip);
        less.Name = "VerbaMenos";
        if (amount <= 0)
        {
            less.Disabled = true;
            less.TooltipText = Palette.Blocked(Tr("a verba neste alvo já é zero."), lessTip);
        }

        Label value = Ui.Value(stepper, Money(amount), 22);
        value.Name = "ValorVerba";
        value.AddThemeColorOverride("font_color", Palette.Texto);
        value.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        value.HorizontalAlignment = HorizontalAlignment.Center;
        var more = Ui.Button(stepper, "+ " + Money(step), () => { _channelDraft[key] = amount + step; Refresh(); },
            Palette.Tip(string.Format(Tr("Põe mais {0} na verba semanal."), Money(step)), Tr("a previsão abaixo muda na hora; só vale depois de Aplicar. Dobrar a verba não dobra o efeito: o retorno é decrescente."), string.Format(Tr("{0} a mais por semana, se você aplicar."), Money(step))));
        more.Name = "VerbaMais";

        CommandForecast forecast = _engine.ForecastChannel(c.Id, targetId, amount);
        PanelContainer forecastCard = Card(_left, Palette.Cartao, Palette.Linha);
        forecastCard.Name = "PrevisaoVerba";
        var col = Ui.Column(forecastCard, 6);
        Heading(col, Tr("Previsão por semana"), 20);
        var effects = ChannelEffectLines(c, targetId, forecast);
        if (amount <= 0)
        {
            Glossary.Rich(col, plan > 0
                ? string.Format(Tr("Sem verba neste alvo a partir de segunda-feira; o que já foi pago nesta semana ({0}) rende até domingo."), Money(active))
                : Tr("Sem verba neste alvo. Use o botão de mais para ver a previsão."), 18, Palette.Texto);
        }
        else
        {
            // As duas contas (GDD 17.8): retorno decrescente da própria verba e a fatia contra a verba dos rivais.
            foreach (string line in forecast.Explanation.Take(3))
            {
                Glossary.Rich(col, "· " + line, 18, Palette.Texto);
            }

            foreach (string line in effects)
            {
                Glossary.Rich(col, "· " + char.ToUpper(line[0], PtBr) + line[1..] + ".", 18, Palette.Texto);
            }

            if (forecast.Factors.FirstOrDefault(f => f.Id == "saturacao") is { } saturation)
            {
                // A saturação em destaque (princípio g): repetir o mesmo canal no mesmo alvo rende cada vez menos.
                PanelContainer warn = Card(col, Palette.CartaoAlto, Palette.Negativo, 10);
                warn.Name = "Saturacao";
                Glossary.Rich(warn, string.Format(PtBr, Tr("Saturação: {0}: rende {1:P0}."), LowerFirst(saturation.Text), saturation.Multiplier), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            }

            foreach (YieldFactor factor in forecast.Factors.Where(f => f.Id != "saturacao"))
            {
                Glossary.Rich(col, string.Format(PtBr, "· {0}: ×{1:0.00} ({2})", factor.Text, factor.Multiplier, Percent(factor.Multiplier)));
            }

            if (c.EmGrupos && forecast.Line("people") is { } people)
            {
                Glossary.Rich(col, string.Format(PtBr, Tr("· {0:0} pessoas nesta zona. {1}"), people.Expected, Channels.Texto("cabos_seguram").Replace("{alvo}", TargetName(c, targetId), StringComparison.Ordinal)));
            }
        }

        if (c.EmGrupos && amount < active)
        {
            Glossary.Rich(col, Tr("Cabos dispensados vão trabalhar para o rival mais presente na zona, e a verba dele ali cresce por um tempo."), 18, Palette.Texto);
        }

        Glossary.Rich(col, forecast.Explanation[^1], 18, Palette.Texto3);
        CommandFailure? blocked = forecast.Blocked;
        if (blocked is { } failure)
        {
            PanelContainer stop = Card(col, Palette.CartaoAlto, Palette.Linha, 10);
            stop.Name = "Bloqueio";
            Glossary.Rich(stop, Tr("Não dá para aplicar:") + " " + BudgetFailureText(failure, c, targetId, amount), 18, Palette.Texto);
        }

        decimal increase = Math.Max(0, amount - active);
        string tip = Palette.Tip(
            string.Format(Tr("Define a verba de {0} em {1}: {2} por semana."), c.Nome, TargetName(c, targetId), Money(amount)),
            effects.Count == 0 ? Tr("sem efeito nos números.") : string.Join("; ", effects) + ".",
            string.Format(Tr("{0} toda segunda-feira; {1} saem do caixa agora."), Money(amount), Money(increase)));
        var apply = Ui.Button(_left, "Aplicar", () =>
        {
            CommandFailure? result = _engine.SetAllocation(c.Id, targetId, amount);
            _channelDraft.Remove(key);
            _log.Insert(0, result is null
                ? string.Format(Tr("Verba de {0} em {1}: {2} por semana."), c.Nome, TargetName(c, targetId), Money(amount))
                : BudgetFailureText(result.Value, c, targetId, amount));
            Refresh();
        }, tip);
        apply.Name = "AplicarVerba";
        if (blocked is { } reason)
        {
            apply.Disabled = true;
            apply.TooltipText = Palette.Blocked(BudgetFailureText(reason, c, targetId, amount), tip);
        }
        else if (amount == plan)
        {
            apply.Disabled = true;
            apply.TooltipText = Palette.Blocked(Tr("o valor é o mesmo do plano de hoje; mude com os botões de menos e de mais."), tip);
        }
        else
        {
            Palette.Primary(apply);
        }

        var here = _engine.Budget.Plan.Where(a => a.ChannelId == c.Id && a.Amount > 0).OrderByDescending(a => a.Amount).ToList();
        if (here.Count > 0)
        {
            Heading(_left, Tr("Onde este canal tem verba"), 20);
            foreach (ChannelAllocation a in here)
            {
                ValueRow(_left, TargetName(c, a.TargetId), Money(a.Amount));
            }
        }

        Glossary.Rich(_left, string.Format(Tr("Verba dos rivais neste canal e alvo: cerca de {0} por semana. É estimativa da sua campanha."), Money(_engine.RivalBudget(c.Id, targetId))), 18, Palette.Texto3);
    }
}
