using Godot;
using Planalto.Core.Executive;
using Planalto.Core.Governance;
using Planalto.Core.People;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Decisões do mandato no cartão de 720 px sobre o mapa (GDD 17.3): evento de mandato com a previsão de cada opção,
/// as pessoas e a zona envolvidas; crise da prefeitura com a causa; e a cobrança do Livro de compromissos, com quem
/// cobra, o que foi prometido e as formas de cumprir. Depois da escolha, o cartão vira a lista "O que mudou".
/// </summary>
public partial class HubScreen
{
    private PanelContainer? _govCard;
    private bool _govCardHidden;
    private string? _govDemand;
    private string? _cardResultTitle;
    private List<string>? _cardResult;

    /// <summary>Cartão de decisão do mandato (ou do resultado dela) aberto sobre o mapa.</summary>
    public bool GovCardOpen => _govCard is not null && IsInstanceValid(_govCard);

    /// <summary>Há algo para o cartão mostrar (crise, evento de mandato, cobrança ou o resultado de uma escolha).</summary>
    private bool GovCardWanted =>
        State.Executive?.Crisis is not null || Gov?.G.Decision is not null || _cardResult is not null
        || (_govDemand is not null && Gov?.G.Demands.Any(d => d.Id == _govDemand && d.Met is null) == true);

    /// <summary>Teste de fumaça: abre o cartão de cobrança de uma cobrança do Livro.</summary>
    public void ShowDemandForTest(string demandId)
    {
        _govDemand = demandId;
        _govCardHidden = false;
        Refresh();
    }

    private void ShowGovCard()
    {
        if (_eventShade is not null && IsInstanceValid(_eventShade))
        {
            _eventShade.QueueFree();
        }

        _eventShade = null;
        _govCard = null;
        GovernanceEngine? gov = Gov;
        Demand? demand = _govDemand is null ? null : gov?.G.Demands.FirstOrDefault(d => d.Id == _govDemand && d.Met is null);
        if (demand is null)
        {
            _govDemand = null;
        }

        bool crisis = State.Executive?.Crisis is not null;
        bool decision = gov?.G.Decision is not null;
        if (!crisis && !decision && demand is null && _cardResult is null)
        {
            _govCardHidden = false;
            return;
        }

        if (_govCardHidden)
        {
            // Recolhido para o jogador consultar o mapa: a faixa de decisão traz o cartão de volta.
            return;
        }

        _running = false;
        var shade = _mapArea.Add(new ColorRect { Color = new Color(Palette.Fundo, 0.6f), MouseFilter = MouseFilterEnum.Stop });
        _eventShade = shade;
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = shade.Add(new CenterContainer());
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var card = center.Add(new PanelContainer { CustomMinimumSize = new Vector2(720, 0), Name = "CartaoMandato" });
        card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 12, 20, Palette.Atencao, 2));
        _govCard = card;
        var column = Ui.Column(card, 6);
        if (_cardResult is not null)
        {
            Title(column, _cardResultTitle ?? "");
            Heading(column, Tr("O que mudou"), 20);
            foreach (string line in _cardResult)
            {
                Glossary.Rich(column, "· " + line.TrimEnd('.') + ".", 18, Palette.Texto);
            }

            var proceed = Ui.Button(column, "Continuar", () => { _cardResult = null; _cardResultTitle = null; Refresh(); },
                Palette.Tip(Tr("Fecha o cartão da decisão."), Tr("volta ao mapa; o jogo continua pausado até você soltar o tempo."), Tr("nada.")));
            proceed.Name = "Continuar";
            Palette.Primary(proceed);
        }
        else if (crisis)
        {
            BuildCrisisCard(column);
        }
        else if (decision)
        {
            BuildDecisionCard(column, gov!);
        }
        else
        {
            BuildDemandCard(column, gov!, demand!);
        }

        if (_banner is not null)
        {
            _mapArea.MoveChild(_banner, -1);
        }
    }

    private void CardResult(string title, GovResult result)
    {
        _cardResultTitle = title;
        _cardResult = [result.Text, .. result.Changes];
        if (result.Factors.Count > 0)
        {
            _cardResult.Add(Tr("Fatores:") + " " + string.Join(", ", result.Factors.Select(x => $"{x.Text} ({Percent(x.Multiplier)})")));
        }

        _log.Insert(0, result.Text.TrimEnd('.') + ".");
    }

    // ------------------------------------------------------------------ evento de mandato

    private void BuildDecisionCard(Node column, GovernanceEngine gov)
    {
        DecisionView view = GovernanceQueries.Decision(gov)!;
        TermDecision d = view.Decision;
        HBoxContainer top = Ui.Row(column, 16);
        if (d.PersonId is not null)
        {
            Portrait(top, d.PersonId, 112);
        }

        var head = Ui.Column(top, 6);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Title(head, d.Title);
        var who = new List<string>();
        if (d.PersonId is not null)
        {
            who.Add(string.Format(Tr("Envolve {0}"), Gov?.Member(d.PersonId) is not null ? MemberLabel(d.PersonId) : GovName(d.PersonId)));
        }

        if (d.ZoneId is not null && GZone(d.ZoneId).Length > 0)
        {
            who.Add(string.Format(Tr("em {0}"), GZone(d.ZoneId)));
            _map.Select(d.ZoneId);
        }

        who.Add(string.Format(Tr("responda até {0}"), d.Deadline.ToString("d 'de' MMMM", PtBr)));
        Glossary.Rich(head, char.ToUpper(string.Join(", ", who)[0], PtBr) + string.Join(", ", who)[1..] + ".", 18, Palette.Texto3);
        Glossary.Rich(column, d.Text, 20, Palette.Texto, Palette.Serif);
        foreach (var (option, forecast) in view.Options)
        {
            string id = option.Id;
            string label = option.Rotulo + (option.Padrao ? " · " + Tr("vale se você não responder") : "");
            var button = GovCommandCard(column, label, option.Rotulo.TrimEnd('.') + ".", forecast, () =>
            {
                GovResult result = gov.Decide(id);
                if (result.Ok)
                {
                    CardResult(d.Title, result);
                }
                else
                {
                    GovShow(option.Rotulo, result);
                }

                Refresh();
            });
            button.Name = "Opcao_" + option.Id;
            string line = forecast.Blocked is not null ? Tr("Não dá:") + " " + GovReason(forecast)
                : GovDoes(forecast) + " " + Tr("Custo:") + " " + GovCost(forecast) + (forecast.Remembered.Count > 0 ? " " + string.Format(Tr("Quem vai lembrar: {0}."), string.Join(", ", forecast.Remembered)) : "");
            Glossary.Rich(column, OneDot(char.ToUpper(line[0], PtBr) + line[1..]));
        }
    }

    /// <summary>Botão de opção de cartão: o tooltip vem da previsão; bloqueado, diz o motivo.</summary>
    private Button GovCommandCard(Node parent, string label, string what, GovForecast forecast, Action run)
    {
        string tip = GovTip(what, forecast);
        var button = Ui.Button(parent, label, run, tip);
        button.Alignment = HorizontalAlignment.Left;
        button.ClipText = true;
        button.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        if (forecast.Blocked is not null)
        {
            button.Disabled = true;
            button.TooltipText = Palette.Blocked(GovReason(forecast), tip);
        }

        return button;
    }

    // ------------------------------------------------------------------ crise da prefeitura

    private void BuildCrisisCard(Node column)
    {
        ExecutiveEngine mayor = Mayor!;
        PendingCrisis pending = State.Executive!.Crisis!;
        CrisisDefinition crisis = _game.ExecutiveContent.Crises.Find(pending.CrisisId)!;
        CrisisRecord? record = Gov?.G.Crises.LastOrDefault(c => c.CrisisId == pending.CrisisId && !c.Resolved);
        Title(column, crisis.Name);
        if (record?.ZoneId is not null && GZone(record.ZoneId).Length > 0)
        {
            Glossary.Rich(column, string.Format(Tr("Em {0}."), GZone(record.ZoneId)), 18, Palette.Texto3);
            _map.Select(record.ZoneId);
            _map.CenterOn(record.ZoneId);
        }

        Glossary.Rich(column, crisis.Text, 20, Palette.Texto, Palette.Serif);
        if (record is not null)
        {
            PanelContainer cause = Card(column, Palette.Cartao, Palette.Linha, 10);
            cause.Name = "CausaDaCrise";
            Glossary.Rich(cause, string.Format(Tr("Causa: {0}"), record.CauseText.TrimEnd('.') + "."), 18, Palette.Texto);
        }

        for (int i = 0; i < crisis.Choices.Count; i++)
        {
            CrisisChoice choice = crisis.Choices[i];
            int index = i;
            // A previsão vem do Core (mesmas regras da resposta); a resposta é certa, então ela também é o que muda.
            GovForecast forecast = mayor.ForecastCrisis(i);
            List<string> changes = forecast.Lines.Where(Matters).Select(l => GovLine(l)).ToList();
            var button = GovCommandCard(column, choice.Label, choice.Label.TrimEnd('.') + ".", forecast, () =>
            {
                string text = mayor.ResolveCrisis(index);
                _cardResultTitle = crisis.Name;
                _cardResult = [text, .. changes.Select(c => char.ToUpper(c[0], PtBr) + c[1..])];
                _log.Insert(0, $"{crisis.Name}: {text.TrimEnd('.')}.");
                Refresh();
            });
            button.Name = "Crise_Opcao_" + i;
            string line = GovDoes(forecast) + " " + Tr("Custo:") + " " + GovCost(forecast) + " " + string.Join(" ", forecast.Explanation.Select(x => x.TrimEnd('.') + "."));
            Glossary.Rich(column, OneDot(char.ToUpper(line[0], PtBr) + line[1..]));
        }
    }

    // ------------------------------------------------------------------ cobrança do Livro

    private void BuildDemandCard(Node column, GovernanceEngine gov, Demand demand)
    {
        Promise? promise = demand.Kind == Demand.Promessa ? State.Ledger.Promises.FirstOrDefault(p => p.Id == demand.RefId) : null;
        Debt? debt = demand.Kind == Demand.Divida ? State.Ledger.Debts.FirstOrDefault(d => d.Id == demand.RefId) : null;
        PromiseView? view = promise is null ? null : GovernanceQueries.Promises(gov).FirstOrDefault(v => v.Promise.Id == promise.Id);
        HBoxContainer top = Ui.Row(column, 16);
        if (demand.PersonId is not null)
        {
            Portrait(top, demand.PersonId, 112);
        }

        var head = Ui.Column(top, 6);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        string who = demand.PersonId is not null ? GovName(demand.PersonId) : GZone(demand.ZoneId);
        Title(head, string.Format(Tr("{0} cobra você"), who));
        if (demand.PersonId is not null && State.City?.Leader(demand.PersonId) is { } leader)
        {
            Glossary.Rich(head, string.Format(Tr("{0}, em {1}. Relação com você: {2}."), char.ToUpper(_game.Campaign.Leaders.Type(leader.Type).Papel[0], PtBr) + _game.Campaign.Leaders.Type(leader.Type).Papel[1..], GZone(leader.Zones[0]), Signed(gov.RelationOf(demand.PersonId), "0")), 18, Palette.Texto3);
        }

        if (demand.ZoneId is not null && GZone(demand.ZoneId).Length > 0)
        {
            _map.Select(demand.ZoneId);
        }

        Glossary.Rich(column, demand.Text.TrimEnd('.') + ".", 20, Palette.Texto, Palette.Serif);
        if (promise is not null)
        {
            PanelContainer what = Card(column, Palette.Cartao, Palette.Linha, 10);
            what.Name = "OQueFoiPrometido";
            var col = Ui.Column(what, 4);
            Glossary.Rich(col, string.Format(Tr("O que você prometeu, em {0}: {1}"), promise.Made.ToString("MM/yyyy", PtBr), promise.Text.TrimEnd('.') + "."), 18, Palette.Texto);
            if (view is not null)
            {
                Glossary.Rich(col, string.Format(Tr("Prazo: {0} ({1}). Vencida, a promessa derruba a relação e quem cobra passa a apoiar outro nome."), view.Deadline.ToString("dd/MM/yyyy", PtBr),
                    view.WeeksLeft < 0 ? Tr("vencido") : string.Format(Tr(view.WeeksLeft == 1 ? "falta {0} semana" : "faltam {0} semanas"), view.WeeksLeft)));
                Glossary.Rich(col, string.Format(Tr("Como cumprir: {0}"), view.HowToFulfil.TrimEnd('.') + "."), 18, Palette.Texto);
            }
        }
        else if (debt is not null)
        {
            Glossary.Rich(column, string.Format(Tr("A dívida, desde {0}: {1}"), debt.Made.ToString("MM/yyyy", PtBr), debt.Text.TrimEnd('.') + "."), 18, Palette.Texto);
        }

        if (view is not null)
        {
            PromiseActions(column, view, "cobranca");
        }

        var later = Ui.Button(column, "Depois", () => { _govDemand = null; Refresh(); },
            Palette.Tip(Tr("Fecha a cobrança sem agir agora."), Tr("a promessa continua no Livro de compromissos, com o mesmo prazo; a área Base lista todas."), Tr("nada agora; o prazo segue correndo.")));
        later.Name = "CobrancaDepois";
    }
}
