using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Executive;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Prefeitura na tela central (GDD 17.15): Governo (resumo, aprovação, crises com causa), Secretariado, Obras (lista,
/// licitação, inauguração e período vedado), Pessoal e fiscal (folha contra os limites da lei, mesa de negociação,
/// mínimos e repasse à Câmara), Convênios, Câmara (contagem e acordo) e Orçamento e políticas. As obras também servem
/// ao vereador. Só lê o Core e chama os comandos dele.
/// </summary>
public partial class HubScreen
{
    private readonly Dictionary<string, string> _actionTargets = new(StringComparer.Ordinal);
    private Dictionary<string, double>? _budgetDraft;
    private string? _govPerson;

    private ExecutiveEngine? Mayor => State.Executive is null ? null : _game.Prefeitura;

    private string ShareMoney(double share) => Mayor is null ? "" : GovMoney(Mayor.AnnualRevenue * (decimal)share);

    private static string Pct(double fraction, string format = "0.0") => (fraction * 100).ToString(format, PtBr) + "%";

    private string ExecFailure(ExecutiveFailure failure) => failure == ExecutiveFailure.CamaraEmLuto
        ? Tr("a Câmara está em luto oficial e não faz sessão nesta semana.")
        : LowerFirst(ExecutiveTexts.Failure(failure));

    // ------------------------------------------------------------------ Governo

    private void BuildCityHallArea()
    {
        if (Gov is not { } gov || Mayor is not { } mayor || State.Executive is not { } e)
        {
            Glossary.Rich(_left, Tr("O mandato terminou."));
            return;
        }

        GovernanceState g = gov.G;
        GovernanceSummary s = GovernanceQueries.Summary(gov);
        int baseSeats = e.Chamber.Count(m => e.InBase(m.PartyId));
        Glossary.Rich(_left, string.Format(Tr("{0}º mandato, até {1}. Partido: {2}. Base na Câmara: {3} de {4} vereadores."), e.Term, e.End.ToString("dd/MM/yyyy", PtBr), GovPartyOf(e.PartyId), baseSeats, e.Chamber.Count), 18, Palette.Texto);

        Heading(_left, Tr("Aprovação"), 20);
        ValueRow(_left, Tr("Na cidade"), string.Format(PtBr, "{0:0}%", s.Approval), highlight: true);
        foreach (ZoneStanding z in GovernanceQueries.Zones(gov).OrderBy(z => z.GovernmentApproval).Take(3))
        {
            ValueRow(_left, z.Name, string.Format(PtBr, "{0:0}%", z.GovernmentApproval));
        }

        Glossary.Rich(_left, Tr("As três zonas em que o governo vai pior. O modo Aprovação do mapa mostra todas."), 18, Palette.Texto3);

        var attention = new List<(string Text, Action Open, string Name)>();
        if (gov.PendingNegotiation is { } negotiation)
        {
            attention.Add((Tr("Mesa de negociação aberta"), () => { OpenGovArea("fiscal"); OpenGov("negociacao", negotiation.Id); }, "Atencao_mesa"));
        }

        foreach (WorkView w in GovernanceQueries.Works(gov).Where(w => w.ByPlayer && w.Work.Stage is WorkStage.Modalidade or WorkStage.Pronta).Take(4))
        {
            string id = w.Work.Id;
            attention.Add((string.Format(Tr(w.Work.Stage == WorkStage.Pronta ? "Obra pronta: {0}, {1}" : "Escolher a licitação: {0}, {1}"), w.Name, w.ZoneName), () => { OpenGovArea("obras"); OpenGov("obra", id); }, "Atencao_obra_" + id));
        }

        int vacant = _game.GovernanceContent.Secretaries.Secretarias.Count(p => g.SecretaryOf(p.Id) is null);
        if (vacant > 0)
        {
            attention.Add((string.Format(Tr("Pastas sem secretário: {0}"), vacant), () => OpenGovArea("secretariado"), "Atencao_pastas"));
        }

        int offers = g.Agreements.Count(a => a.State == AgreementState.Oferta);
        if (offers > 0)
        {
            attention.Add((string.Format(Tr("Convênios oferecidos: {0}"), offers), () => OpenGovArea("convenios"), "Atencao_convenios"));
        }

        if (s.PromisesDueSoon > 0)
        {
            attention.Add((string.Format(Tr("Promessas perto do prazo: {0}"), s.PromisesDueSoon), () => { OpenGovArea("governo"); OpenGov("livro", null); }, "Atencao_promessas"));
        }

        if (attention.Count > 0)
        {
            Heading(_left, Tr("Pede atenção"), 20);
            foreach (var (text, open, name) in attention)
            {
                GovRow(_left, text, open, Palette.Tip(text + ".", Tr("abre a área que resolve isto."), Tr("nada.")), name);
            }
        }

        Heading(_left, Tr("Crises"), 20);
        var crises = GovernanceQueries.Crises(gov);
        if (crises.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma crise neste mandato. Elas não são sorteio: vêm de negociação que falha, zona sem drenagem, secretário, licitação, indicador ruim ou queda de receita."));
        }

        foreach (CrisisView c in crises.Take(4))
        {
            PanelContainer card = c.ZoneName is null || c.Record.ZoneId is null ? Card(_left, Palette.Cartao, Palette.Linha, 10)
                : ClickCard(_left, () => { _map.CenterOn(c.Record.ZoneId); _map.Select(c.Record.ZoneId); },
                    Palette.Tip(string.Format(Tr("{0}, em {1}."), c.Name, c.ZoneName), Tr("centraliza o mapa na zona atingida."), Tr("nada.")));
            card.Name = "Crise_" + c.Record.Id;
            var col = Ui.Column(card, 4);
            Glossary.Plain(col, $"{c.Name} · {c.Record.Date.ToString("dd/MM/yyyy", PtBr)}" + (c.ZoneName is null ? "" : " · " + c.ZoneName), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            Glossary.Rich(col, string.Format(Tr("Causa: {0}"), c.Cause.TrimEnd('.') + "."), 18, Palette.Texto);
            Glossary.Rich(col, c.Record.Resolved ? Tr("Respondida.") : c.Record.Presented ? Tr("Esperando a sua resposta.") : Tr("A caminho da sua mesa."));
        }

        Heading(_left, Tr("Atos do gabinete"), 20);
        foreach (MandateActionDefinition action in _game.ExecutiveContent.Actions.Actions)
        {
            List<(string Id, string Text)> targets = action.Kind == MandateActionKind.Articular
                ? e.Chamber.OrderByDescending(m => m.Relation).Select(m => (m.Id, string.Format(PtBr, "{0} ({1}) · {2}", m.Name, GovPartyOf(m.PartyId), Signed(m.Relation, "0")))).ToList()
                : action.Targets.Select(t => (t.Id, t.Name)).ToList();
            string? target = targets.Count == 0 ? null : _actionTargets.TryGetValue(action.Id, out string? chosen) && targets.Any(t => t.Id == chosen) ? chosen : targets[0].Id;
            string tip = Palette.Tip(action.Name + ".", LowerFirst(action.Description).TrimEnd('.') + ".", string.Format(Tr("{0} de energia da semana."), action.Energy));
            var button = Ui.Button(_left, $"{action.Name} · {action.Energy} {Tr("de energia")}", () =>
            {
                EffectSnapshot before = ExecSnapshot(e);
                string text = mayor.Execute(action.Id, target);
                string changes = EffectSummary.Describe(before, ExecSnapshot(e), id => e.Chamber.FirstOrDefault(m => m.Id == id)?.Name ?? EffectSummary.GroupName(id));
                GovShow(action.Name, new GovResult(null, text, changes.Length == 0 ? [] : [changes], []));
                Refresh();
            }, tip);
            button.Name = "Ato_" + action.Id;
            button.Alignment = HorizontalAlignment.Left;
            button.ClipText = true;
            if (mayor.CanExecute(action, target) is { } failure)
            {
                button.Disabled = true;
                button.TooltipText = Palette.Blocked(ExecFailure(failure), tip);
            }

            if (targets.Count > 0)
            {
                OptionButton options = Ui.Options(_left, targets, target, id => { _actionTargets[action.Id] = id; Refresh(); }, translate: false);
                options.ClipText = true;
                options.TooltipText = Palette.Tip(string.Format(Tr("Alvo de {0}."), LowerFirst(action.Name)), Tr("escolhe com quem ou onde o ato acontece."), Tr("nada."));
            }
        }
    }

    private static EffectSnapshot ExecSnapshot(ExecutiveState e) => new(
        e.Recognition, e.Image, 0, 0, new Dictionary<string, double>(e.Support), e.Chamber.ToDictionary(m => m.Id, m => m.Relation));

    // ------------------------------------------------------------------ Secretariado

    private string CompetenceText(SecretaryView v) => string.Format(Tr("competência {0}"), Span(v.CompetenceMin, v.CompetenceMax, "0"));

    private string SecretaryLabel(SecretaryView v) =>
        v.Pasta is null ? string.Format(Tr("{0} (nome oferecido, {1})"), v.Name, v.PartySigla ?? Tr("técnico, sem partido")) : string.Format(Tr("{0} (secretaria de {1}, {2})"), v.Name, v.Pasta, v.PartySigla ?? Tr("sem partido"));

    private void BuildSecretariesArea()
    {
        if (Gov is not { } gov)
        {
            return;
        }

        var seated = GovernanceQueries.Secretaries(gov);
        var offers = GovernanceQueries.Secretaries(gov, offers: true);
        Glossary.Rich(_left, string.Format(Tr("{0} pastas, {1} com titular. Nomes disponíveis: {2}. A competência (1 a 5) dá a velocidade do indicador da área e o atraso das obras da pasta."),
            _game.GovernanceContent.Secretaries.Secretarias.Count, seated.Count, offers.Count), 18, Palette.Texto);
        foreach (SecretariaDefinition pasta in _game.GovernanceContent.Secretaries.Secretarias)
        {
            SecretaryView? holder = seated.FirstOrDefault(v => v.Secretary.SecretariaId == pasta.Id);
            string id = pasta.Id;
            string text = holder is null ? $"{pasta.Nome}\n{Tr("sem titular")}" : $"{pasta.Nome}: {holder.Name} ({holder.PartySigla ?? Tr("sem partido")})\n{CompetenceText(holder)} · {Tr("lealdade")} {holder.Secretary.Loyalty}";
            GovRow(_left, text, () => { _govPerson = null; OpenGov("pasta", id); },
                Palette.Tip(string.Format(Tr("Secretaria de {0}."), pasta.Nome), Tr("abre a pasta: o titular, a agenda dele, os nomes oferecidos e a previsão de nomear ou demitir."), Tr("nada para abrir.")), "Pasta_" + pasta.Id);
        }
    }

    private void SecretaryCard(Node parent, SecretaryView v, bool withAgenda)
    {
        HBoxContainer row = Ui.Row(parent, 10);
        Portrait(row, v.Secretary.PersonId, 64);
        var col = Ui.Column(row, 2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Glossary.Plain(col, SecretaryLabel(v), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, string.Format(Tr("{0} (1 a 5) · lealdade {1}"), char.ToUpper(CompetenceText(v)[0], PtBr) + CompetenceText(v)[1..], v.Secretary.Loyalty));
        if (withAgenda)
        {
            Glossary.Rich(col, string.Format(Tr("Agenda própria: {0}"), v.Agenda.TrimEnd('.') + "."));
        }
    }

    private void BuildSecretariaSheet(string secretariaId)
    {
        GovernanceEngine gov = Gov!;
        SecretariaDefinition? pasta = _game.GovernanceContent.Secretaries.Secretarias.FirstOrDefault(p => p.Id == secretariaId);
        GovBackButton();
        if (pasta is null)
        {
            return;
        }

        NameTitle(_left, string.Format(Tr("Secretaria de {0}"), pasta.Nome), 22);
        SecretaryView? holder = GovernanceQueries.Secretaries(gov).FirstOrDefault(v => v.Secretary.SecretariaId == secretariaId);
        if (holder is not null)
        {
            PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
            card.Name = "Titular";
            SecretaryCard(card, holder, withAgenda: true);
            Glossary.Rich(_left, string.Format(PtBr, Tr("Com esta competência, o indicador da área anda a {0:0.00} vez(es) a velocidade normal. A competência exata só aparece depois de algumas semanas no cargo."), holder.IndicatorSpeed));
            GovMemories(holder.Secretary.PersonId);
            GovForecast dismiss = gov.ForecastDismissSecretary(secretariaId);
            GovForecastCard(_left, dismiss, "PrevisaoDemitir", Tr("Previsão de demitir"));
            GovCommand(_left, "Demitir", string.Format(Tr("Demite {0} da pasta."), holder.Name), dismiss, () => gov.DismissSecretary(secretariaId), "DemitirSecretario", danger: true);
            return;
        }

        Glossary.Rich(_left, Tr("Pasta sem titular: o indicador da área anda mais devagar e as obras dela atrasam. Escolha um dos nomes abaixo."), 18, Palette.Texto);
        var offers = GovernanceQueries.Secretaries(gov, offers: true);
        _govPerson ??= offers.FirstOrDefault()?.Secretary.PersonId;
        foreach (SecretaryView v in offers.OrderByDescending(o => o.PartySigla is null).ThenBy(o => o.PartySigla, StringComparer.Ordinal))
        {
            bool selected = _govPerson == v.Secretary.PersonId;
            string person = v.Secretary.PersonId;
            PanelContainer card = ClickCard(_left, () => { _govPerson = person; Refresh(); },
                GovTip(SecretaryLabel(v) + ".", gov.ForecastAppointSecretary(secretariaId, person)), "Oferta_" + person);
            if (selected)
            {
                card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 8, 12, Palette.Acao));
            }

            SecretaryCard(card, v, withAgenda: selected);
        }

        if (_govPerson is not null && offers.FirstOrDefault(o => o.Secretary.PersonId == _govPerson) is { } chosen)
        {
            GovForecast forecast = gov.ForecastAppointSecretary(secretariaId, chosen.Secretary.PersonId);
            GovForecastCard(_left, forecast, "PrevisaoNomear", Tr("Previsão de nomear"));
            string person = chosen.Secretary.PersonId;
            GovCommand(_left, "Nomear", string.Format(Tr("Nomeia {0} para a pasta."), chosen.Name), forecast, () => gov.AppointSecretary(secretariaId, person), "NomearSecretario", primary: true);
        }
    }

    // ------------------------------------------------------------------ Obras

    private string StageText(WorkView w)
    {
        string stage = w.Work.Stage is WorkStage.Entregue or WorkStage.Cancelada or WorkStage.Pronta or WorkStage.Modalidade ? w.StageName : string.Format(PtBr, "{0} ({1:P0})", w.StageName, w.StageProgress);
        return w.DelayWeeks > 0 && w.Work.Open ? stage + " · " + string.Format(Tr(w.DelayWeeks == 1 ? "atraso de {0} semana" : "atraso de {0} semanas"), w.DelayWeeks) : stage;
    }

    private void WorkRow(WorkView w)
    {
        string id = w.Work.Id;
        string zone = w.Work.ZoneId;
        GovRow(_left, $"{w.Name} · {w.ZoneName}\n{StageText(w)}", () => { _map.Select(zone); _map.CenterOn(zone); OpenGov("obra", id); },
            Palette.Tip(string.Format(Tr("{0} em {1}."), w.Name, w.ZoneName), Tr("abre a obra e a mostra no mapa: etapa, prazo, atraso, empresa e o que dá para fazer."), Tr("nada para abrir.")), "Obra_" + w.Work.Id);
    }

    private void BuildWorksArea()
    {
        if (Gov is not { } gov)
        {
            Glossary.Rich(_left, Tr("Sem mandato em curso."));
            return;
        }

        var works = GovernanceQueries.Works(gov);
        bool mayor = Kind == GovKind.Prefeito;
        if (gov.PlayerBarredFromInauguration)
        {
            PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Linha, 10);
            card.Name = "PeriodoVedado";
            Glossary.Rich(card, string.Format(Tr("Período vedado: nos três meses antes da eleição, candidato não comparece a inauguração ({0}). Obra pronta pode ser entregue sem cerimônia."), gov.Context.InaugurationLaw.Artigo), 18, Palette.Texto);
        }

        if (mayor && State.Campaign is null)
        {
            FiscalView fiscal = GovernanceQueries.Fiscal(gov);
            Glossary.Rich(_left, string.Format(Tr("Ainda cabem {0} de obra nova neste ano."), GovMoney(fiscal.InvestmentLeft)), 18, Palette.Texto);
            var start = Ui.Button(_left, "Iniciar uma obra", () => { _govType = null; OpenGov("nova_obra", null, _govZone); },
                Palette.Tip(Tr("Abre o início de obra."), Tr("você escolhe o tipo e a zona no mapa e vê custo, prazo e previsão antes de confirmar."), Tr("nada para abrir; a obra desconta do investimento do ano.")));
            start.Name = "IniciarObra";
            Palette.Primary(start);
        }

        var mine = works.Where(w => w.ByPlayer && w.Work.Stage is WorkStage.Modalidade or WorkStage.Pronta).ToList();
        if (mine.Count > 0)
        {
            Heading(_left, Tr("Esperando você"), 20);
            foreach (WorkView w in mine)
            {
                WorkRow(w);
            }
        }

        var open = works.Where(w => w.Work.Open && !mine.Contains(w)).ToList();
        Heading(_left, string.Format(Tr("Em andamento ({0})"), open.Count), 20);
        if (open.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma obra em andamento na cidade."));
        }

        foreach (WorkView w in open.Take(14))
        {
            WorkRow(w);
        }

        var delivered = works.Where(w => w.Work.Stage == WorkStage.Entregue).Take(5).ToList();
        if (delivered.Count > 0)
        {
            Heading(_left, Tr("Entregues há pouco"), 20);
            foreach (WorkView w in delivered)
            {
                WorkRow(w);
            }
        }
    }

    private void BuildWorkSheet(GovernanceEngine gov, Work work)
    {
        _govPick = null;
        WorkView w = GovernanceQueries.Works(gov).First(v => v.Work.Id == work.Id);
        WorkType? type = _game.GovernanceContent.Works.Find(work.TypeId);
        GovBackButton();
        NameTitle(_left, $"{w.Name} · {w.ZoneName}", 22);
        string origin = work.Origin switch
        {
            Work.DeEmenda => Tr("emenda de vereador"),
            Work.DeIndicacao => Tr("indicação de vereador atendida"),
            Work.DeConvenio => Tr("convênio"),
            Work.DeAcordo => Tr("acordo de voto na Câmara"),
            _ => Tr("decisão da prefeitura"),
        };
        Glossary.Rich(_left, string.Format(Tr("Custo: {0}. Origem: {1}. Responsável político: {2}."), GovMoney(work.Cost), origin, w.ByPlayer ? Tr("você") : w.Sponsor), 18, Palette.Texto);
        ValueRow(_left, Tr("Etapa"), w.StageName, highlight: true);
        if (work.Open && work.Stage is not (WorkStage.Pronta or WorkStage.Modalidade))
        {
            Bar(_left, w.StageProgress, Palette.Acao).CustomMinimumSize = new Vector2(48, 8);
            if (work.StageEnd is { } end)
            {
                ValueRow(_left, Tr("Fim previsto da etapa"), end.ToString("dd/MM/yyyy", PtBr));
            }
        }

        if (work.DelayWeeks > 0)
        {
            ValueRow(_left, Tr("Atraso acumulado"), string.Format(Tr(work.DelayWeeks == 1 ? "{0} semana" : "{0} semanas"), work.DelayWeeks));
        }

        if (work.Mode is { } mode)
        {
            ProcurementOption? option = _game.GovernanceContent.Procurement.Modalidades.FirstOrDefault(m => m.Modo == mode);
            Glossary.Rich(_left, string.Format(Tr("Licitação: {0} ({1}).{2}"), LowerFirst(option?.Nome ?? mode.ToString()), gov.Context.ProcurementLaw.ArtigoDe(mode), work.Impugned ? " " + Tr("Foi impugnada por uma empresa perdedora, o que atrasou a etapa.") : ""));
        }

        if (w.Company is not null)
        {
            Glossary.Rich(_left, string.Format(Tr("Empresa contratada: {0} (empresa fictícia)."), w.Company));
        }

        if (w.KnownIrregular)
        {
            Glossary.Rich(_left, string.Format(PtBr, Tr("Irregularidade conhecida: sobrepreço de {0:P0} no contrato."), work.Overprice), 18, Palette.Texto);
        }

        if (type?.Indicador is { } indicator && State.Indicators?.Find(indicator) is not null && _game.IndicatorModel.Definitions.Find(indicator) is { } definition)
        {
            Glossary.Rich(_left, string.Format(Tr("Entregue, melhora o indicador {0}."), definition.Nome));
        }

        bool mayor = Kind == GovKind.Prefeito;
        if (mayor && w.ByPlayer && work.Stage == WorkStage.Modalidade)
        {
            Heading(_left, Tr("Modalidade de licitação"), 20);
            Glossary.Rich(_left, Tr("A escolha troca rapidez por segurança: a modalidade rápida só é legal em emergência reconhecida; fora dela, vira irregularidade que a fiscalização e a imprensa podem achar."));
            foreach (var (option, blocked, reason, irregular) in gov.ModesFor(work))
            {
                GovForecast forecast = gov.ForecastMode(work.Id, option.Modo);
                PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
                card.Name = "Modalidade_" + option.Modo;
                var col = Ui.Column(card, 4);
                Glossary.Plain(col, option.Nome, 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
                Glossary.Rich(col, string.Format(Tr("{0} De {1} a {2} semanas. Base legal: {3}."), option.Descricao.TrimEnd('.') + ".", option.Semanas.Min, option.Semanas.Max, gov.Context.ProcurementLaw.ArtigoDe(option.Modo)));
                if (blocked is not null)
                {
                    PanelContainer stop = Card(col, Palette.CartaoAlto, Palette.Linha, 8);
                    stop.Name = "Bloqueio_" + option.Modo;
                    string why = option.SoPequenasEServicos && blocked == GovFailure.AlvoInvalido
                        ? Tr("esta modalidade só serve para serviços e obras pequenas, e esta obra não é uma delas.")
                        : string.IsNullOrWhiteSpace(reason) ? GovFailureText(blocked.Value) : reason!.TrimEnd('.') + ".";
                    Glossary.Rich(stop, Tr("Não pode:") + " " + why, 18, Palette.Texto);
                }
                else
                {
                    if (irregular)
                    {
                        Glossary.Rich(col, Tr("Sem emergência reconhecida nesta zona, esta contratação é irregular: fica registrada e pode ser descoberta."), 18, Palette.Texto);
                    }

                    foreach (string line in forecast.Explanation)
                    {
                        Glossary.Rich(col, "· " + line.TrimEnd('.') + ".");
                    }

                    foreach (ForecastOutcome o in forecast.Outcomes)
                    {
                        Glossary.Rich(col, string.Format(Tr("· Chance de {0}: {1}"), o.Probability.ToString("P0", PtBr), o.Text.TrimEnd('.') + "."));
                    }
                }

                string workId = work.Id;
                ProcurementMode chosen = option.Modo;
                GovCommand(col, string.Format(Tr("Licitar por {0}"), LowerFirst(option.Nome)), option.Descricao.TrimEnd('.') + ".", forecast, () => gov.ChooseMode(workId, chosen), "Escolher_" + option.Modo);
            }
        }

        if (w.ByPlayer && work.Stage == WorkStage.Pronta)
        {
            GovForecast forecast = gov.ForecastInaugurate(work.Id);
            GovForecastCard(_left, forecast, "PrevisaoInaugurar", Tr("Previsão de inaugurar"));
            string workId = work.Id;
            GovCommand(_left, "Inaugurar", Tr("Entrega a obra com cerimônia, com você presente."), forecast, () => gov.Inaugurate(workId), "Inaugurar", primary: true);
            string quietTip = Palette.Tip(Tr("Entrega a obra pronta sem cerimônia."), Tr("a obra passa a funcionar e a promessa ligada a ela é cumprida; rende menos presença do que a inauguração e é o caminho no período vedado."), Tr("nada; você só deixa de ganhar o palanque."));
            var quiet = Ui.Button(_left, "Entregar sem cerimônia", () => { GovShow(Tr("Entregar sem cerimônia"), gov.DeliverQuietly(workId)); Refresh(); }, quietTip);
            quiet.Name = "EntregarSemCerimonia";
        }

        if (Kind == GovKind.Vereador && work.Open && State.Campaign is null)
        {
            HBoxContainer row = Ui.Row(_left, 6);
            Ui.Button(row, "Fiscalizar", () => { OpenGovArea("instrumentos"); OpenInstrument(InstrumentDefinition.Fiscalizacao, null); _govTarget = $"||{work.Id}"; Refresh(); },
                Palette.Tip(Tr("Abre a Fiscalização com esta obra escolhida."), Tr("você vê a previsão antes de confirmar; pode encontrar normalidade, atraso ou irregularidade."), Tr("nada para abrir; fiscalizar gasta turno."))).Name = "FiscalizarObra";
            Ui.Button(row, "Requerimento", () => { OpenGovArea("instrumentos"); OpenInstrument(InstrumentDefinition.Requerimento, null); _govTarget = $"{InfoRequest.SobreObra}||{work.Id}"; Refresh(); },
                Palette.Tip(Tr("Abre o Requerimento de informação com esta obra escolhida."), Tr("em algumas semanas a prefeitura responde e pode revelar atraso, sobrepreço ou quem venceu a licitação."), Tr("nada para abrir; o requerimento gasta turno e esfria a relação com o prefeito."))).Name = "RequererObra";
        }
    }

    private void BuildNewWorkSheet()
    {
        GovernanceEngine gov = Gov!;
        GovBackButton();
        NameTitle(_left, Tr("Iniciar uma obra"), 22);
        FiscalView fiscal = GovernanceQueries.Fiscal(gov);
        Glossary.Rich(_left, string.Format(Tr("Ainda cabem {0} de obra nova neste ano. Toda obra passa por projeto, licitação, execução e entrega."), GovMoney(fiscal.InvestmentLeft)), 18, Palette.Texto);
        var types = _game.GovernanceContent.Works.Tipos;
        _govType ??= (_govZone is null ? types.FirstOrDefault() : types.FirstOrDefault(t => gov.Fits(t, _govZone)) ?? types.FirstOrDefault())?.Id;
        FieldLabel("Tipo de obra");
        OptionButton choice = Ui.Options(_left, types.Select(t => (t.Id, string.Format(Tr("{0} · {1}"), t.Nome, GovMoney(gov.CostOf(t))))), _govType, t => { _govType = t; Refresh(); }, translate: false);
        choice.Name = "Tipo";
        choice.ClipText = true;
        choice.TooltipText = Palette.Tip(Tr("Tipo de obra."), Tr("define o custo, a pasta responsável, o prazo e qual promessa do Livro a entrega cumpre."), Tr("nada para escolher; o custo aparece ao lado do nome."));
        WorkType? type = types.FirstOrDefault(t => t.Id == _govType);
        GovZonePicker("nova_obra", type is null ? null : z => gov.Fits(type, z));
        if (type is not null)
        {
            string pasta = _game.GovernanceContent.Secretaries.Secretarias.FirstOrDefault(p => p.Id == type.Secretaria)?.Nome ?? type.Secretaria;
            Glossary.Rich(_left, string.Format(Tr("Pasta responsável: {0}. Projeto: de {1} a {2} semanas. Execução: de {3} a {4} semanas."), pasta, type.ProjetoSemanas.Min, type.ProjetoSemanas.Max, type.ExecucaoSemanas.Min, type.ExecucaoSemanas.Max));
        }

        GovForecast forecast = _govType is null || _govZone is null ? GovForecast.Block(GovFailure.AlvoInvalido, Tr("escolha a zona clicando nela no mapa")) : gov.ForecastStartWork(_govType, _govZone);
        GovForecastCard(_left, forecast);
        string? typeId = _govType;
        string? zone = _govZone;
        GovCommand(_left, "Iniciar a obra", Tr("Manda fazer o projeto desta obra na zona escolhida."), forecast, () => gov.StartWork(typeId!, zone!), "Confirmar", primary: true, after: result =>
        {
            if (result.Ok && result.CreatedId is { } created)
            {
                _govSheet = "obra";
                _govId = created;
                _govPick = null;
            }
        });
    }

    // ------------------------------------------------------------------ Pessoal e fiscal

    /// <summary>Barra da folha sobre a RCL com as três marcas da lei: alerta, prudencial e limite.</summary>
    private void PayrollBar(FiscalView f)
    {
        double max = f.LimitAt * 1.15;
        var frame = _left.Add(new Control { CustomMinimumSize = new Vector2(0, 28), Name = "BarraFolha", MouseFilter = MouseFilterEnum.Stop });
        frame.TooltipText = string.Format(PtBr, Tr("Folha de {0:0.0}% da receita corrente líquida. Alerta em {1:0.0}%, limite prudencial em {2:0.0}% e limite máximo em {3:0.0}%."), f.PayrollShare * 100, f.AlertAt * 100, f.PrudentialAt * 100, f.LimitAt * 100);
        var back = frame.Add(new ColorRect { Color = Palette.Cartao, MouseFilter = MouseFilterEnum.Ignore });
        back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var fill = frame.Add(new ColorRect { Color = f.Band >= PayrollBand.Prudencial ? Palette.Negativo : Palette.Acao, MouseFilter = MouseFilterEnum.Ignore, Name = "Preenchimento" });
        fill.AnchorRight = (float)Math.Clamp(f.PayrollShare / max, 0, 1);
        fill.AnchorBottom = 1;
        foreach (var (at, color, name) in new[] { (f.AlertAt, Palette.Texto2, "MarcaAlerta"), (f.PrudentialAt, Palette.Atencao, "MarcaPrudencial"), (f.LimitAt, Palette.Texto, "MarcaLimite") })
        {
            var mark = frame.Add(new ColorRect { Color = color, MouseFilter = MouseFilterEnum.Ignore, Name = name });
            mark.AnchorLeft = mark.AnchorRight = (float)(at / max);
            mark.AnchorBottom = 1;
            mark.OffsetLeft = -1.5f;
            mark.OffsetRight = 1.5f;
            mark.OffsetTop = -3;
            mark.OffsetBottom = 3;
        }

        HFlowContainer legend = _left.Add(new HFlowContainer());
        legend.AddThemeConstantOverride("h_separation", 12);
        Chip(legend, Palette.Texto2, string.Format(PtBr, Tr("alerta {0:0.0}%"), f.AlertAt * 100));
        Chip(legend, Palette.Atencao, string.Format(PtBr, Tr("prudencial {0:0.0}%"), f.PrudentialAt * 100));
        Chip(legend, Palette.Texto, string.Format(PtBr, Tr("limite {0:0.0}%"), f.LimitAt * 100));
    }

    private void BuildFiscalArea()
    {
        if (Gov is not { } gov || State.Executive is not { } e)
        {
            return;
        }

        FiscalView f = GovernanceQueries.Fiscal(gov);
        FiscalLawConfig law = gov.Context.FiscalLaw;
        Glossary.Rich(_left, string.Format(Tr("Receita corrente líquida: {0} por ano ({1})."), GovMoney(f.Rcl), f.RclDeclared ? Tr("declarada pelo município ao Tesouro; daí em diante, simulada") : Tr("estimativa do jogo pelo porte da cidade")), 18, Palette.Texto);

        Heading(_left, Tr("Folha de pagamento"), 20);
        ValueRow(_left, string.Format(Tr("Folha sobre a receita ({0})"), LowerFirst(gov.BandName(f.Band))), Pct(f.PayrollShare), highlight: true);
        PayrollBar(f);
        Glossary.Rich(_left, f.PayrollFromRgf ? Tr("Ponto de partida: o Relatório de Gestão Fiscal do município; daí em diante, simulado.") : Tr("Ponto de partida estimado pelo porte da cidade."), 18, Palette.Texto3);
        Glossary.Rich(_left, string.Format(Tr("· Alerta ({0}): o Tribunal de Contas avisa; nada fica vedado."), law.ArtigoAlerta));
        Glossary.Rich(_left, string.Format(Tr("· Prudencial ({0}): ficam vedados {1}."), law.ArtigoPrudencial, string.Join("; ", law.VedacoesPrudencial.Select(LowerFirst))), 18, f.RaisesBarred ? Palette.Texto : Palette.Texto2);
        Glossary.Rich(_left, string.Format(Tr("· Limite ({0}): o excesso tem de ser eliminado em {1} quadrimestres; sem isso, {2}."), law.ArtigoExcesso, law.QuadrimestresParaEliminar, string.Join("; ", law.ConsequenciasExcesso.Select(LowerFirst))), 18, f.Band == PayrollBand.Excedido ? Palette.Texto : Palette.Texto2);
        if (f.ExcessDeadline is { } deadline)
        {
            Glossary.Rich(_left, string.Format(Tr("Você passou do limite em {0}: o prazo para voltar é {1}."), f.ExcessSince?.ToString("dd/MM/yyyy", PtBr), deadline.ToString("dd/MM/yyyy", PtBr)) + (f.Restricted ? " " + Tr("As restrições já valem.") : ""), 18, Palette.Texto);
        }

        GovForecast cut = gov.ForecastCutPayroll();
        GovCommand(_left, "Cortar despesa de pessoal", Tr("Corta cargos em comissão e horas extras para baixar a folha."), cut, gov.CutPayroll, "CortarFolha");

        Heading(_left, Tr("Mesa de negociação"), 20);
        if (gov.PendingNegotiation is { } pending)
        {
            NegotiationTable table = _game.GovernanceContent.Personnel.Mesas.First(m => m.Id == pending.TableId);
            GovRow(_left, string.Format(PtBr, "{0}\n{1} {2:0.0}% · {3} {4}", char.ToUpper(table.Nome[0], PtBr) + table.Nome[1..], Tr("pedem"), pending.Asked * 100, Tr("resposta até"), pending.Deadline.ToString("dd/MM", PtBr)), () => OpenGov("negociacao", pending.Id),
                Palette.Tip(string.Format(Tr("Mesa de negociação: {0}."), table.Nome), Tr("abre a mesa com o pedido, a folha contra os limites e as respostas possíveis, cada uma com a previsão."), Tr("nada para abrir.")), "Mesa_" + pending.Id);
        }
        else
        {
            Glossary.Rich(_left, Tr("Nenhuma mesa aberta. Elas abrem no mês da data-base de cada categoria; sem resposta no prazo, vale a revisão geral."));
        }

        foreach (Negotiation n in gov.G.Negotiations.Where(n => n.Outcome != NegotiationOutcome.Pendente).Reverse().Take(3))
        {
            NegotiationTable table = _game.GovernanceContent.Personnel.Mesas.First(m => m.Id == n.TableId);
            Glossary.Rich(_left, string.Format(PtBr, Tr("· {0}, {1}: {2} (reajuste de {3:0.0}%)."), table.Nome, n.Date.ToString("MM/yyyy", PtBr), n.Outcome == NegotiationOutcome.Fracassou ? Tr("sem acordo") : Tr("acordo"), n.Granted * 100));
        }

        Heading(_left, Tr("Mínimos de saúde e educação"), 20);
        Glossary.Rich(_left, string.Format(Tr("Os mínimos incidem sobre a base de impostos e transferências ({0} por ano), não sobre o orçamento todo."), GovMoney(f.TaxBase)));
        foreach (var (area, minimum, reais, article) in new[] { (law.AreaSaude, f.HealthMinimumOfBudget, f.HealthMinimum, law.ArtigoSaude), (law.AreaEducacao, f.EducationMinimumOfBudget, f.EducationMinimum, law.ArtigoEducacao) })
        {
            double share = e.Allocation.GetValueOrDefault(area);
            string name = _game.ExecutiveContent.Config.Areas.FirstOrDefault(a => a.Id == area)?.Name ?? area;
            bool below = share + 1e-9 < minimum;
            ValueRow(_left, string.Format(Tr("{0}: mínimo de {1} do orçamento ({2}; {3})"), name, Pct(minimum), GovMoney(reais), article), Pct(share) + (below ? " ▼" : ""), highlight: below);
        }

        if (e.MinimumViolatedThisYear)
        {
            Glossary.Rich(_left, Tr("Neste ano o gasto ficou abaixo de um mínimo: o Tribunal de Contas pode rejeitar as contas."), 18, Palette.Texto);
        }

        Heading(_left, Tr("Repasse à Câmara"), 20);
        Glossary.Rich(_left, string.Format(Tr("Hoje: {0} da base de impostos ({1} por ano). Teto para esta cidade: {2} ({3})."), Pct(f.ChamberShare, "0.00"), GovMoney(f.ChamberBudget), Pct(f.ChamberCap, "0.00"), law.ArtigoCamara));
        HBoxContainer row = Ui.Row(_left, 6);
        foreach (var (label, delta, name) in new[] { ("Reduzir 0,25 ponto", -0.0025, "RepasseMenos"), ("Aumentar 0,25 ponto", 0.0025, "RepasseMais") })
        {
            double target = Math.Round(f.ChamberShare + delta, 4);
            string tip = Palette.Tip(
                string.Format(Tr("Muda o repasse à Câmara para {0} da base de impostos."), Pct(Math.Max(0, target), "0.00")),
                Tr(delta > 0 ? "a Câmara recebe mais; os vereadores lembram do gesto." : "sobra mais no caixa da prefeitura; os vereadores lembram do corte."),
                Tr(delta > 0 ? "dinheiro do orçamento." : "a relação com os vereadores."));
            var b = Ui.Button(row, label, () => { GovShow(Tr(label), gov.SetChamberTransfer(target)); Refresh(); }, tip);
            b.Name = name;
            b.ClipText = true;
            b.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (target < 0 || target > f.ChamberCap + 1e-9)
            {
                b.Disabled = true;
                b.TooltipText = Palette.Blocked(target < 0 ? Tr("o repasse já está em zero.") : string.Format(Tr("passaria do teto de {0} ({1})."), Pct(f.ChamberCap, "0.00"), law.ArtigoCamara), tip);
            }
        }
    }

    private void BuildNegotiationSheet(GovernanceEngine gov, Negotiation n)
    {
        _govPick = null;
        NegotiationTable table = _game.GovernanceContent.Personnel.Mesas.First(m => m.Id == n.TableId);
        FiscalView f = GovernanceQueries.Fiscal(gov);
        GovBackButton();
        NameTitle(_left, char.ToUpper(table.Nome[0], PtBr) + table.Nome[1..], 22);
        Glossary.Rich(_left, string.Format(PtBr, Tr("Categoria: {0}. Pedem {1:0.0}% de reajuste. A revisão geral do ano (reposição da inflação) é de {2:0.0}%."), LowerFirst(table.Categoria), n.Asked * 100, n.GeneralRevision * 100), 18, Palette.Texto);
        ValueRow(_left, string.Format(Tr("Folha hoje ({0})"), LowerFirst(gov.BandName(f.Band))), Pct(f.PayrollShare), highlight: true);
        PayrollBar(f);
        if (n.Outcome != NegotiationOutcome.Pendente)
        {
            Glossary.Rich(_left, string.Format(PtBr, Tr("Mesa encerrada: {0}, com reajuste de {1:0.0}%."), n.Outcome == NegotiationOutcome.Fracassou ? Tr("sem acordo") : Tr("acordo"), n.Granted * 100), 18, Palette.Texto);
            return;
        }

        Glossary.Rich(_left, string.Format(Tr("Responda até {0}. Sem resposta, vale a revisão geral. Mesa que fracassa vira greve: a crise chega depois, com esta negociação como causa."), n.Deadline.ToString("d 'de' MMMM", PtBr)));
        foreach (NegotiationAnswer answer in _game.GovernanceContent.Personnel.Respostas)
        {
            GovForecast forecast = gov.ForecastNegotiation(n.Id, answer.Id);
            PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
            card.Name = "Resposta_" + answer.Id;
            var col = Ui.Column(card, 4);
            Glossary.Plain(col, answer.Nome, 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            Glossary.Rich(col, answer.Descricao.TrimEnd('.') + ".");
            if (forecast.Blocked is null)
            {
                Glossary.Rich(col, string.Format(PtBr, Tr("Chance de a categoria aceitar: {0:P0}."), gov.AcceptChance(n, answer.Id)), 18, Palette.Texto);
                foreach (string line in forecast.Explanation)
                {
                    Glossary.Rich(col, "· " + line.TrimEnd('.') + ".");
                }
            }
            else
            {
                PanelContainer stop = Card(col, Palette.CartaoAlto, Palette.Linha, 8);
                stop.Name = "Bloqueio_" + answer.Id;
                Glossary.Rich(stop, Tr("Não pode:") + " " + GovReason(forecast), 18, Palette.Texto);
            }

            string negotiationId = n.Id;
            string answerId = answer.Id;
            GovCommand(col, answer.Nome, answer.Descricao.TrimEnd('.') + ".", forecast, () => gov.AnswerNegotiation(negotiationId, answerId), "Responder_" + answer.Id);
        }
    }

    // ------------------------------------------------------------------ Convênios

    private string AgreementOrigin(Agreement a) => _game.GovernanceContent.Agreements.Origens.FirstOrDefault(o => o.Id == a.OriginId)?.Nome ?? a.OriginId;

    private string AgreementObject(Agreement a) => _game.GovernanceContent.Works.Find(a.TypeId)?.Nome ?? a.TypeId;

    private void BuildAgreementsArea()
    {
        if (Gov is not { } gov)
        {
            return;
        }

        GovernanceState g = gov.G;
        Glossary.Rich(_left, string.Format(PtBr, Tr("As ofertas dependem da sua relação com o governador ({0}) e chegam sozinhas. Cada convênio paga parte de uma obra; o município entra com a contrapartida."), Signed(g.GovernorRelation, "0")), 18, Palette.Texto);
        if (g.AgreementsBlockedUntil is { } blocked && blocked > State.Calendar.WeekStart)
        {
            Glossary.Rich(_left, string.Format(Tr("Novos convênios bloqueados até {0}: um convênio venceu sem a obra e o dinheiro foi devolvido."), blocked.ToString("dd/MM/yyyy", PtBr)), 18, Palette.Texto);
        }

        var offers = g.Agreements.Where(a => a.State == AgreementState.Oferta).ToList();
        Heading(_left, string.Format(Tr("Ofertas ({0})"), offers.Count), 20);
        if (offers.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma oferta agora."));
        }

        foreach (Agreement a in offers)
        {
            string id = a.Id;
            GovRow(_left, string.Format(PtBr, "{0}: {1}\n{2} · {3} {4:P0} · {5} {6}", AgreementOrigin(a), LowerFirst(AgreementObject(a)), GovMoney(a.Value), Tr("contrapartida"), a.Counterpart, Tr("vale até"), a.OfferExpires.ToString("dd/MM", PtBr)),
                () => OpenGov("convenio", id), Palette.Tip(string.Format(Tr("Convênio oferecido por {0}."), AgreementOrigin(a)), Tr("abre a oferta: valor, objeto, contrapartida, prazo e a previsão de assinar."), Tr("nada para abrir.")), "Convenio_" + a.Id);
        }

        var signed = g.Agreements.Where(a => a.State == AgreementState.Assinado).ToList();
        if (signed.Count > 0)
        {
            Heading(_left, Tr("Em execução"), 20);
            foreach (Agreement a in signed)
            {
                string? workId = a.WorkId;
                GovRow(_left, string.Format("{0}: {1}\n{2} {3}", AgreementOrigin(a), LowerFirst(AgreementObject(a)), Tr("entregar a obra até"), a.Deadline?.ToString("dd/MM/yyyy", PtBr)), () => { if (workId is not null) { OpenGov("obra", workId); } },
                    Palette.Tip(Tr("Convênio em execução."), Tr("abre a obra do convênio; se ela não for entregue no prazo, o dinheiro volta e novos convênios ficam bloqueados."), Tr("nada para abrir.")), "ConvenioObra_" + a.Id);
            }
        }

        var past = g.Agreements.Where(a => a.State is AgreementState.Cumprido or AgreementState.Devolvido).Reverse().Take(4).ToList();
        if (past.Count > 0)
        {
            Heading(_left, Tr("Encerrados"), 20);
            foreach (Agreement a in past)
            {
                Glossary.Rich(_left, string.Format(Tr("· {0}, {1}: {2}."), AgreementOrigin(a), LowerFirst(AgreementObject(a)), a.State == AgreementState.Cumprido ? Tr("cumprido") : Tr("dinheiro devolvido")));
            }
        }
    }

    private void BuildAgreementSheet(GovernanceEngine gov, Agreement a)
    {
        GovBackButton();
        NameTitle(_left, string.Format(Tr("Convênio: {0}"), LowerFirst(AgreementObject(a))), 22);
        WorkType? type = _game.GovernanceContent.Works.Find(a.TypeId);
        Glossary.Rich(_left, string.Format(PtBr, Tr("Quem oferece: {0}. Valor: {1}. Contrapartida do município: {2:P0} do custo da obra. Prazo para entregar: {3} semanas depois de assinar. A oferta vale até {4}."),
            AgreementOrigin(a), GovMoney(a.Value), a.Counterpart, a.Weeks, a.OfferExpires.ToString("dd/MM/yyyy", PtBr)), 18, Palette.Texto);
        if (a.State != AgreementState.Oferta)
        {
            _govPick = null;
            Glossary.Rich(_left, Tr("Esta oferta já foi respondida ou venceu."));
            return;
        }

        GovZonePicker("convenio", type is null ? null : z => gov.Fits(type, z));
        GovForecast forecast = _govZone is null ? GovForecast.Block(GovFailure.AlvoInvalido, Tr("escolha a zona da obra clicando nela no mapa")) : gov.ForecastSignAgreement(a.Id, _govZone);
        GovForecastCard(_left, forecast);
        string id = a.Id;
        string? zone = _govZone;
        GovCommand(_left, "Assinar o convênio", Tr("Assina o convênio e manda fazer a obra na zona escolhida."), forecast, () => gov.SignAgreement(id, zone!), "AssinarConvenio", primary: true);
        string declineTip = Palette.Tip(Tr("Recusa a oferta."), Tr("o dinheiro não vem e a contrapartida fica livre para outra obra; quem ofereceu toma nota."), Tr("nada em dinheiro."));
        Ui.Button(_left, "Recusar", () => { GovShow(Tr("Recusar o convênio"), gov.DeclineAgreement(id)); Refresh(); }, declineTip).Name = "RecusarConvenio";
    }

    // ------------------------------------------------------------------ Câmara (prefeito)

    private void BuildChamberArea()
    {
        if (Gov is not { } gov || Mayor is not { } mayor || State.Executive is not { } e)
        {
            return;
        }

        int baseSeats = e.Chamber.Count(m => e.InBase(m.PartyId));
        Glossary.Rich(_left, string.Format(Tr("Base do governo: {0} de {1} vereadores. Política que precisa de lei vai a voto; cada indeciso tem um pedido."), baseSeats, e.Chamber.Count), 18, Palette.Texto);
        if (mayor.ChamberInMourning)
        {
            PanelContainer luto = Card(_left, Palette.Cartao, Palette.Linha, 10);
            luto.Name = "Luto";
            Glossary.Rich(luto, string.Format(Tr("Luto oficial até {0}. A Câmara não faz sessão nesta semana: projeto que precisa de lei espera a seguinte."), State.Renewal?.LutoAte?.ToString("d 'de' MMMM", PtBr)), 18, Palette.Texto, Palette.Serif);
        }

        Heading(_left, Tr("Políticas que precisam de lei"), 20);
        foreach (PolicyDefinition policy in _game.ExecutiveContent.Policies.Policies.Where(p => p.RequiresLaw && !e.Policies.Any(x => x.PolicyId == p.Id && x.Counts)))
        {
            NominalCount? count = gov.CountPolicy(policy.Id);
            if (count is null)
            {
                continue;
            }

            string id = policy.Id;
            GovRow(_left, $"{policy.Name}\n{GovernanceQueries.CountText(gov, count)}", () => { _govMember = null; OpenGov("contagem", id); },
                Palette.Tip(string.Format(Tr("Contagem de votos de {0}."), policy.Name), Tr("abre a contagem nominal: quem vota como, por quê, e o pedido de cada indeciso, com a opção de fechar acordo."), Tr("nada para abrir.")), "Contagem_" + policy.Id);
        }

        if (gov.G.Cpi is { } cpi)
        {
            Heading(_left, Tr("CPI contra o governo"), 20);
            Glossary.Rich(_left, string.Format(PtBr, Tr("{0}. Termina em {1}. Prova reunida: {2:P0}."), cpi.Subject.TrimEnd('.'), cpi.Ends.ToString("dd/MM/yyyy", PtBr), cpi.Evidence), 18, Palette.Texto);
            Glossary.Rich(_left, GovernanceQueries.CassationText(gov).TrimEnd('.') + ".");
        }

        Heading(_left, Tr("Bancadas"), 20);
        foreach (var group in e.Chamber.Where(m => m.PartyId is not null).GroupBy(m => m.PartyId!).OrderByDescending(x => x.Count()))
        {
            ValueRow(_left, string.Format(Tr("{0}{1}: {2} cadeira(s)"), GovPartyOf(group.Key), e.InBase(group.Key) ? " · " + Tr("base") : "", group.Count()), Signed(group.Average(m => m.Relation), "0"));
        }

        Glossary.Rich(_left, Tr("À direita, a relação média da bancada com você (−100 a 100)."), 18, Palette.Texto3);
        BuildSeatsBlock();
    }

    // ------------------------------------------------------------------ Orçamento e políticas

    private void BuildBudgetPoliciesArea()
    {
        if (Mayor is not { } mayor || State.Executive is not { } e)
        {
            return;
        }

        _budgetDraft ??= new Dictionary<string, double>(e.Allocation);
        Glossary.Rich(_left, string.Format(Tr("Orçamento anual: {0}. Mudanças valem aos poucos: os indicadores seguem o gasto com atraso."), GovMoney(mayor.AnnualRevenue)), 18, Palette.Texto);
        foreach (BudgetArea area in _game.ExecutiveContent.Config.Areas)
        {
            double share = _budgetDraft.GetValueOrDefault(area.Id);
            double minimum = mayor.MinimumShare(area);
            string id = area.Id;
            ValueRow(_left, $"{area.Name} · {ShareMoney(share)}" + (minimum > 0 ? string.Format(Tr(" (mínimo {0})"), Pct(minimum)) : ""), Pct(share) + (share + 1e-9 < minimum ? " ▼" : ""), highlight: share + 1e-9 < minimum);
            HSlider slider = Ui.Slider(_left, share, v => { _budgetDraft[id] = v; Refresh(); }, 0, 0.5, 0.005);
            slider.Name = "Orcamento_" + area.Id;
            slider.CustomMinimumSize = new Vector2(120, 32);
            slider.TooltipText = Palette.Tip(string.Format(Tr("Fatia do orçamento para {0}."), LowerFirst(area.Name)), Tr("muda o rascunho; só vale depois de Aplicar o orçamento."), Tr("o que sair daqui falta em outra área."));
        }

        double sum = _budgetDraft.Values.Sum();
        ValueRow(_left, Tr("Total do rascunho"), Pct(sum), highlight: Math.Abs(sum - 1) > 0.001);
        VBoxContainer actions = Ui.Column(_left, 6);
        var fit = Ui.Button(actions, "Ajustar para 100%", () =>
        {
            double s = Math.Max(1e-9, _budgetDraft.Values.Sum());
            _budgetDraft = _budgetDraft.ToDictionary(d => d.Key, d => d.Value / s);
            Refresh();
        }, Palette.Tip(Tr("Acerta o rascunho para somar 100%."), Tr("cada área mantém a proporção e o total volta a 100%."), Tr("nada.")));
        fit.Name = "AjustarOrcamento";
        string applyTip = Palette.Tip(Tr("Aplica o rascunho do orçamento."), Tr("as fatias novas passam a valer; os indicadores de cada área seguem o gasto ao longo das semanas."), Tr("nada para aplicar; gasto abaixo do mínimo de saúde ou educação pode levar à rejeição das contas."));
        var apply = Ui.Button(actions, "Aplicar o orçamento", () =>
        {
            ExecutiveFailure? failure = mayor.SetAllocation(_budgetDraft);
            GovShow(Tr("Orçamento"), failure is null ? new GovResult(null, Tr("Orçamento atualizado"), [], []) : GovResult.Fail(GovFailure.Bloqueado, ExecFailure(failure.Value)));
            _budgetDraft = failure is null ? null : _budgetDraft;
            Refresh();
        }, applyTip);
        apply.Name = "AplicarOrcamento";
        if (Math.Abs(sum - 1) > 0.001)
        {
            apply.Disabled = true;
            apply.TooltipText = Palette.Blocked(string.Format(Tr("o rascunho soma {0}; precisa somar 100%."), Pct(sum)), applyTip);
        }

        ValueRow(_left, string.Format(Tr("Reserva livre para programas ({0})"), ShareMoney(Math.Max(0, mayor.FreeReserve))), Pct(mayor.FreeReserve));

        Heading(_left, Tr("Programas e políticas"), 20);
        foreach (PolicyDefinition policy in _game.ExecutiveContent.Policies.Policies)
        {
            EnactedPolicy? enacted = e.Policies.LastOrDefault(p => p.PolicyId == policy.Id);
            string status = enacted is { Counts: true } ? ExecutiveTexts.Status(enacted.Status)
                : enacted?.Status == PolicyStatus.Rejeitada ? string.Format(Tr("rejeitada por {0} a {1}"), enacted.No, enacted.Yes)
                : policy.RequiresLaw ? Tr("precisa de lei") : Tr("por decreto");
            string id = policy.Id;
            GovRow(_left, $"{policy.Name}\n{status} · {(policy.Cost >= 0 ? Tr("custa") : Tr("libera"))} {Pct(Math.Abs(policy.Cost))}", () => OpenGov("politica", id),
                Palette.Tip(policy.Name + ": " + LowerFirst(policy.Description).TrimEnd('.') + ".", Tr("abre a política: custo, prazo, o que melhora e, se precisa de lei, a contagem de votos."), Tr("nada para abrir.")), "Politica_" + policy.Id);
        }
    }

    private void BuildPolicySheet(PolicyDefinition policy)
    {
        _govPick = null;
        ExecutiveEngine mayor = Mayor!;
        ExecutiveState e = State.Executive!;
        GovernanceEngine gov = Gov!;
        GovBackButton();
        NameTitle(_left, policy.Name, 22);
        Glossary.Rich(_left, policy.Description.TrimEnd('.') + ".", 18, Palette.Texto);
        string cost = policy.Cost >= 0 ? string.Format(Tr("custa {0} do orçamento ao ano ({1})"), Pct(policy.Cost), ShareMoney(policy.Cost)) : string.Format(Tr("libera {0} do orçamento ao ano ({1})"), Pct(-policy.Cost), ShareMoney(-policy.Cost));
        string boosts = string.Join(", ", policy.Boosts.Select(b => string.Format(PtBr, "{0} {1}", LowerFirst(_game.ExecutiveContent.Config.Indicators.FirstOrDefault(i => i.Id == b.Key)?.Name ?? b.Key), Signed(b.Value, "0.#"))));
        string does = string.Format(Tr("em {0} semanas passa a funcionar e empurra os indicadores: {1} (pontos na escala de 0 a 100)."), policy.ImplementationWeeks, boosts.Length == 0 ? Tr("nenhum diretamente") : boosts);
        Glossary.Rich(_left, char.ToUpper(does[0], PtBr) + does[1..]);
        Glossary.Rich(_left, char.ToUpper(cost[0], PtBr) + cost[1..] + ". " + (policy.RequiresLaw ? string.Format(Tr("Precisa de lei ({0})."), MandateTexts.Quorum(policy.Quorum)) : Tr("Sai por decreto.")));
        EnactedPolicy? enacted = e.Policies.LastOrDefault(p => p.PolicyId == policy.Id);
        if (enacted is { Counts: true })
        {
            Glossary.Rich(_left, string.Format(Tr("Situação: {0}."), ExecutiveTexts.Status(enacted.Status)), 18, Palette.Texto);
            var revoke = Ui.Button(_left, "Revogar", () =>
            {
                mayor.Revoke(policy.Id);
                GovShow(Tr("Revogar"), new GovResult(null, string.Format(Tr("{0} foi revogada"), policy.Name), [], []));
                Refresh();
            }, Palette.Tip(string.Format(Tr("Revoga {0}."), policy.Name), Tr("a política deixa de valer, o custo volta para a reserva e os indicadores perdem o empurrão aos poucos."), Tr("quem gostava dela nota.")));
            revoke.Name = "Revogar";
            Palette.Danger(revoke);
            return;
        }

        if (enacted?.Status == PolicyStatus.Rejeitada)
        {
            Glossary.Rich(_left, string.Format(Tr("A Câmara rejeitou da última vez: {0} a favor, {1} contra."), enacted.Yes, enacted.No), 18, Palette.Texto);
        }

        if (policy.RequiresLaw && gov.CountPolicy(policy.Id) is { } count)
        {
            Glossary.Rich(_left, string.Format(Tr("Contagem hoje: {0}. {1}"), GovernanceQueries.CountText(gov, count), count.MissingToApprove > 0 ? string.Format(Tr(count.MissingToApprove == 1 ? "Falta {0} voto." : "Faltam {0} votos."), count.MissingToApprove) : Tr("Aprovaria.")), 18, Palette.Texto);
            Ui.Button(_left, "Ver a contagem nominal", () => { _govMember = null; OpenGov("contagem", policy.Id); },
                Palette.Tip(Tr("Abre a contagem de votos desta política."), Tr("mostra quem vota como, por quê, e o pedido de cada indeciso, com a opção de fechar acordo antes de mandar o projeto."), Tr("nada."))).Name = "VerContagem";
        }

        string tip = Palette.Tip(policy.Name + ".", does, char.ToUpper(cost[0], PtBr) + cost[1..] + "; " + string.Format(Tr("{0} de energia da semana."), _game.ExecutiveContent.Config.EnactEnergy));
        var send = Ui.Button(_left, policy.RequiresLaw ? "Enviar à Câmara" : "Decretar", () =>
        {
            EnactResult result = mayor.Enact(policy.Id);
            string text = !result.Ok ? ExecFailure(result.Failure!.Value)
                : policy.RequiresLaw ? string.Format(Tr(result.Approved ? "{0}: aprovada por {1} a {2}" : "{0}: rejeitada, com {1} a favor e {2} contra"), policy.Name, result.Yes, result.No)
                : string.Format(Tr("{0}: decreto assinado"), policy.Name);
            GovShow(policy.Name, result.Ok ? new GovResult(null, text, [], []) : GovResult.Fail(GovFailure.Bloqueado, text));
            Refresh();
        }, tip);
        send.Name = "Adotar";
        Palette.Primary(send);
        if (mayor.CanEnact(policy) is { } failure)
        {
            send.Disabled = true;
            send.TooltipText = Palette.Blocked(ExecFailure(failure), tip);
        }
    }
}
