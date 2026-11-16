using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Mandato de vereador na tela central (GDD 17.14): Mandato (resumo, pauta com a contagem nominal, instrução de voto),
/// Gabinete (assessores), Instrumentos (requerimento, indicação, fiscalização, emenda, projeto e negociação de voto),
/// Mesa e CPI, e Base (zonas que cobram e o Livro de compromissos). Só lê o Core e chama os comandos dele.
/// </summary>
public partial class HubScreen
{
    private string? _govTarget;
    private string? _govType;
    private string? _govRole;
    private string? _govMember;

    private static string OptionKey(InstrumentOption o) => $"{o.TypeId}|{o.ZoneId}|{o.TargetId}";

    private string VoteName(VoteChoice vote) => vote switch
    {
        VoteChoice.Sim => Tr("sim"),
        VoteChoice.Nao => Tr("não"),
        VoteChoice.Abstencao => Tr("abstenção"),
        _ => Tr("falta (sem voto marcado)"),
    };

    private string MemberLabel(string memberId)
    {
        ChamberMember? m = Gov?.Member(memberId);
        return m is null ? GovName(memberId) : string.Format(Tr("{0} (vereador, {1})"), m.Name, GovPartyOf(m.PartyId));
    }

    // ------------------------------------------------------------------ Mandato

    private void BuildCouncilArea()
    {
        if (Gov is not { } gov || _game.Mandate is not { } engine || State.Mandate is not { } mandate)
        {
            Glossary.Rich(_left, Tr("O mandato terminou."));
            return;
        }

        GovernanceState g = gov.G;
        GovernanceSummary s = GovernanceQueries.Summary(gov);
        Glossary.Rich(_left, OneDot(string.Format(Tr("Mandato até {0}. Prefeitura: {1} ({2}); a sua relação com o governo é {3}. Preside a Câmara: {4}."),
            mandate.End.ToString("dd/MM/yyyy", PtBr), mandate.MayorName, GovPartyOf(mandate.MayorPartyId), Signed(mandate.MayorRelation, "0"), s.ChamberPresident ?? Tr("a eleger"))), 18, Palette.Texto);
        if (engine.InMourning)
        {
            PanelContainer luto = Card(_left, Palette.Cartao, Palette.Linha, 10);
            luto.Name = "Luto";
            Glossary.Rich(luto, string.Format(Tr("Luto oficial até {0}. A Câmara não faz sessão nesta semana; a pauta fica para a seguinte."), State.Renewal?.LutoAte?.ToString("d 'de' MMMM", PtBr)), 18, Palette.Texto, Palette.Serif);
        }
        else if (engine.InRecess)
        {
            Glossary.Rich(_left, Tr("Recesso parlamentar: sem sessões neste mês. Os instrumentos do gabinete continuam."));
        }

        // O que pede atenção: cada linha leva à área que resolve.
        var attention = new List<(string Text, Action Open, string Name)>();
        if (s.PendingFindings > 0)
        {
            string id = g.Inspections.First(i => i.Pending).Id;
            attention.Add((Tr("Achado de fiscalização a decidir"), () => OpenGov("achado", id), "Atencao_achado"));
        }

        if (s.WorksToInaugurate > 0)
        {
            attention.Add((string.Format(Tr("Obras suas para entregar: {0}"), s.WorksToInaugurate), () => OpenGovArea("instrumentos"), "Atencao_obras"));
        }

        if (s.PromisesDueSoon > 0)
        {
            attention.Add((string.Format(Tr("Promessas perto do prazo: {0}"), s.PromisesDueSoon), () => OpenGovArea("base"), "Atencao_promessas"));
        }

        if (s.OpenDemands > 0)
        {
            attention.Add((string.Format(Tr("Zonas da base cobrando você: {0}"), s.OpenDemands), () => OpenGovArea("base"), "Atencao_base"));
        }

        if (attention.Count > 0)
        {
            Heading(_left, Tr("Pede atenção"), 20);
            foreach (var (text, open, name) in attention)
            {
                GovRow(_left, text, open, Palette.Tip(text + ".", Tr("abre a área que resolve isto."), Tr("nada.")), name);
            }
        }

        BuildInstruction(gov);

        Heading(_left, Tr("Pauta da Câmara"), 20);
        var agenda = GovernanceQueries.Agenda(gov);
        if (agenda.Count == 0)
        {
            Glossary.Rich(_left, engine.InRecess ? Tr("Recesso: sem votações.") : Tr("Nenhum projeto no plenário nesta semana."));
        }

        foreach (var (bill, count, planned, conflicts) in agenda)
        {
            BillTemplate template = engine.Template(bill);
            PanelContainer card = Card(_left, conflicts.Count > 0 && bill.PlayerVote is null ? Palette.CartaoAlto : Palette.Cartao, Palette.Linha, 10);
            card.Name = "Pauta_" + bill.Id;
            var col = Ui.Column(card, 4);
            Glossary.Plain(col, bill.Title, 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            string author = bill.AuthorId == Bill.MayorAuthor ? Tr("a Prefeitura") : bill.AuthorId == RaceSetup.PlayerId ? Tr("você") : MemberLabel(bill.AuthorId);
            Glossary.Rich(col, string.Format(Tr("Autoria: {0}. Quórum: {1}. {2}"), author, MandateTexts.Quorum(template.Quorum), template.Summary));
            Glossary.Rich(col, string.Format(Tr("Contagem hoje: {0}. {1}"), GovernanceQueries.CountText(gov, count),
                count.MissingToApprove > 0 ? string.Format(Tr(count.MissingToApprove == 1 ? "Falta {0} voto para aprovar." : "Faltam {0} votos para aprovar."), count.MissingToApprove) : Tr("Aprovaria.")), 18, Palette.Texto);
            int orientation = engine.PlayerOrientation(bill);
            Glossary.Rich(col, mandate.Player.PartyId is null ? Tr("Sem partido: você vota livre.")
                : string.Format(Tr("Orientação do {0}: {1}. Seu voto, se nada mudar: {2}."), GovPartyOf(mandate.Player.PartyId), MandateTexts.Orientation(orientation), VoteName(planned)));
            foreach (string reason in conflicts)
            {
                Glossary.Rich(col, "· " + string.Format(Tr("Conflito: {0}"), reason.TrimEnd('.') + "."), 18, Palette.Texto);
            }

            HBoxContainer votes = Ui.Row(col, 6);
            foreach (var (choice, text, what) in new[] { (VoteChoice.Sim, "Sim", "a favor"), (VoteChoice.Nao, "Não", "contra"), (VoteChoice.Abstencao, "Abstenção", "pela abstenção") })
            {
                string tip = GovTip(string.Format(Tr("Marca o seu voto {0} neste projeto."), Tr(what)), engine.ForecastVote(bill.Id, choice));
                var b = Ui.Button(votes, text, () => { engine.CastVote(bill.Id, choice); Refresh(); }, tip);
                b.Name = $"Voto_{bill.Id}_{choice}";
                b.ToggleMode = true;
                b.ButtonPressed = bill.PlayerVote == choice;
                if (bill.PlayerVote == choice)
                {
                    Palette.Selected(b);
                }
            }

            var open = Ui.Button(col, "Ver a contagem nominal", () => { _govMember = null; OpenGov("contagem", bill.Id); },
                Palette.Tip(Tr("Abre a contagem de votos deste projeto."), Tr("mostra quem vota sim, quem vota não, por quê, e o pedido de cada indeciso, com a opção de negociar."), Tr("nada.")));
            open.Name = "Contagem_" + bill.Id;
        }

        var own = mandate.Bills.Where(b => b.AuthorId == RaceSetup.PlayerId).Reverse().Take(5).ToList();
        if (own.Count > 0)
        {
            Heading(_left, Tr("Seus projetos"), 20);
            foreach (Bill bill in own)
            {
                ValueRow(_left, bill.Title, MandateTexts.Stage(bill.Stage));
            }
        }
    }

    private void BuildInstruction(GovernanceEngine gov)
    {
        Heading(_left, Tr("Instrução permanente de voto"), 20);
        Glossary.Rich(_left, Tr("Com uma instrução, o seu voto sai sozinho nas votações comuns, e o jogo só para quando orientação, plataforma, acordo ou base entram em conflito."));
        foreach (var (instruction, label) in new[]
        {
            (VoteInstruction.SeguirOrientacao, "Seguir a orientação do partido"),
            (VoteInstruction.SeguirPlataforma, "Seguir a minha plataforma"),
            (VoteInstruction.Nenhuma, "Sem instrução"),
        })
        {
            bool selected = gov.G.Instruction == instruction;
            var b = Ui.Button(_left, label, () => { GovShow(Tr(label), gov.SetInstruction(instruction)); Refresh(); },
                GovTip(string.Format(Tr("Instrução de voto: {0}."), LowerFirst(Tr(label))), gov.ForecastInstruction(instruction)));
            b.Name = "Instrucao_" + instruction;
            b.Alignment = HorizontalAlignment.Left;
            b.ToggleMode = true;
            b.ButtonPressed = selected;
            if (selected)
            {
                Palette.Selected(b);
            }
        }
    }

    /// <summary>Contagem nominal de um projeto (vereador) ou de uma política que precisa de lei (prefeito), com a negociação de voto.</summary>
    private bool BuildCountSheet(string subjectId)
    {
        GovernanceEngine gov = Gov!;
        bool mayor = Kind == GovKind.Prefeito;
        NominalCount? count = mayor ? gov.CountPolicy(subjectId) : gov.CountBill(subjectId);
        if (count is null)
        {
            return false;
        }

        GovBackButton();
        NameTitle(_left, count.Title, 22);
        Glossary.Rich(_left, string.Format(Tr("Contagem nominal hoje: {0}. Quórum: {1}. {2}"), GovernanceQueries.CountText(gov, count), MandateTexts.Quorum(count.Quorum),
            count.MissingToApprove > 0 ? string.Format(Tr(count.MissingToApprove == 1 ? "Falta {0} voto para aprovar." : "Faltam {0} votos para aprovar."), count.MissingToApprove) : Tr("Com estes votos, aprova.")), 18, Palette.Texto);
        Glossary.Rich(_left, Tr("A contagem é uma estimativa: cada vereador tem uma chance de votar sim, e indeciso é quem está perto do meio."), 18, Palette.Texto3);

        int side = 1;
        if (!mayor && State.Mandate?.Bills.FirstOrDefault(b => b.Id == subjectId) is { } bill)
        {
            side = gov.PlannedVote(bill) == VoteChoice.Nao ? -1 : 1;
        }

        var undecided = count.Lines.Where(l => l.Lean == NominalCount.Indeciso).ToList();
        Heading(_left, string.Format(Tr("Indecisos ({0})"), undecided.Count), 20);
        if (undecided.Count == 0)
        {
            Glossary.Rich(_left, Tr("Ninguém indeciso: não há voto para negociar neste assunto."));
        }

        foreach (CountLine line in undecided)
        {
            PanelContainer card = Card(_left, _govMember == line.MemberId ? Palette.CartaoAlto : Palette.Cartao, Palette.Linha, 10);
            card.Name = "Indeciso_" + line.MemberId;
            var col = Ui.Column(card, 4);
            HBoxContainer top = Ui.Row(col, 8);
            Portrait(top, line.MemberId, 48);
            var text = Ui.Column(top, 2);
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            Glossary.Plain(text, string.Format(Tr("{0} (vereador, {1})"), line.Name, GovPartyOf(line.PartyId)), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
            // O motivo do indeciso costuma ser o próprio pedido, que vem logo abaixo: não se repete.
            bool sameAsRequest = line.RequestText is not null && line.Reason.Contains(line.RequestText.TrimEnd('.'), StringComparison.OrdinalIgnoreCase);
            string why = sameAsRequest || line.Reason.Length == 0 ? "" : " " + char.ToUpper(line.Reason[0], PtBr) + line.Reason[1..].TrimEnd('.') + ".";
            Glossary.Rich(text, string.Format(PtBr, Tr("Chance de votar sim: {0:P0}.{1}"), line.YesProbability, why));
            if (line.RequestText is null)
            {
                Glossary.Rich(col, Tr("Não tem pedido a fazer agora."));
                continue;
            }

            Glossary.Rich(col, string.Format(Tr("Pede: {0}"), line.RequestText.TrimEnd('.') + "."), 18, Palette.Texto);
            GovForecast forecast = mayor ? gov.ForecastPolicyDeal(subjectId, line.MemberId) : gov.ForecastNegociarVoto(subjectId, line.MemberId, side);
            if (_govMember == line.MemberId)
            {
                GovForecastCard(col, forecast, "PrevisaoAcordo");
                string member = line.MemberId;
                GovCommand(col, "Fechar o acordo", string.Format(Tr("Atende o pedido de {0} em troca do voto."), line.Name), forecast,
                    () => mayor ? gov.NegotiatePolicyVote(subjectId, member) : gov.NegociarVoto(subjectId, member, side), "FecharAcordo", primary: true);
            }
            else
            {
                string member = line.MemberId;
                var see = Ui.Button(col, "Negociar o voto", () => { _govMember = member; Refresh(); }, GovTip(string.Format(Tr("Abre a negociação com {0}."), line.Name), forecast));
                see.Name = "Negociar_" + line.MemberId;
                if (forecast.Blocked is not null)
                {
                    see.Disabled = true;
                    see.TooltipText = Palette.Blocked(GovReason(forecast), see.TooltipText);
                }
            }
        }

        foreach (var (lean, title) in new[] { (NominalCount.Sim, "Votam sim ({0})"), (NominalCount.Nao, "Votam não ({0})") })
        {
            var lines = count.Lines.Where(l => l.Lean == lean).ToList();
            Heading(_left, string.Format(Tr(title), lines.Count), 20);
            foreach (CountLine line in lines)
            {
                Glossary.Rich(_left, string.Format(Tr("· {0} ({1}): {2}"), line.Name, GovPartyOf(line.PartyId), line.Reason.TrimEnd('.') + "."));
            }
        }

        return true;
    }

    // ------------------------------------------------------------------ Gabinete

    private string AideOrigin(AideView v) => v.Aide.Origin switch
    {
        Aide.DaCampanha => Tr("trabalhou na sua campanha"),
        Aide.DeLideranca => string.Format(Tr("indicação de {0}"), v.NomineeOf ?? Tr("uma liderança")),
        Aide.DeVereador => string.Format(Tr("indicação de {0}"), v.NomineeOf ?? Tr("um vereador")),
        Aide.Parente => Tr("é seu parente"),
        _ => Tr("nome de mercado"),
    };

    private void AideRow(AideView v)
    {
        string what = v.Aide.Appointed
            ? string.Format(Tr("{0} ({1}{2})"), v.Name, LowerFirst(v.RoleName ?? ""), v.ZoneName is null ? "" : ", " + v.ZoneName)
            : string.Format(Tr("{0} (nome para o gabinete)"), v.Name);
        PanelContainer card = ClickCard(_left, () => { _govRole = null; OpenGov("assessor", v.Aide.PersonId); },
            Palette.Tip(what + ".", Tr("abre a ficha: origem, qualidade, a previsão de nomear ou de demitir e quem vai lembrar."), Tr("nada para abrir.")), "Assessor_" + v.Aide.PersonId);
        HBoxContainer row = Ui.Row(card, 10);
        Portrait(row, v.Aide.PersonId, 56);
        var col = Ui.Column(row, 2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Glossary.Plain(col, what, 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, string.Format(Tr("Qualidade {0} · {1}{2}"), v.Aide.Quality, AideOrigin(v), v.PaysPromise ? " · " + Tr("nomear cumpre promessa") : ""));
    }

    private void BuildCabinetArea()
    {
        if (Gov is not { } gov)
        {
            return;
        }

        var cabinet = GovernanceQueries.Cabinet(gov);
        Glossary.Rich(_left, string.Format(Tr("Vagas nesta cidade: {0}. Ocupadas: {1}. Cada função muda o que os seus instrumentos rendem."), gov.CabinetSlots, cabinet.Count), 18, Palette.Texto);
        Heading(_left, string.Format(Tr("Nomeados ({0}/{1})"), cabinet.Count, gov.CabinetSlots), 20);
        if (cabinet.Count == 0)
        {
            Glossary.Rich(_left, Tr("Ninguém nomeado. Sem assessores, os instrumentos rendem o básico e nenhuma zona tem quem cuide da sua presença."));
        }

        foreach (AideView v in cabinet)
        {
            AideRow(v);
        }

        Heading(_left, Tr("Funções"), 20);
        foreach (AideRole role in _game.GovernanceContent.Instruments.Funcoes)
        {
            int filled = cabinet.Count(a => a.Aide.Role == role.Id);
            Glossary.Rich(_left, string.Format(Tr("· {0} ({1} de até {2}): {3}"), role.Nome, filled, role.Maximo, LowerFirst(role.Descricao).TrimEnd('.') + "."));
        }

        var candidates = GovernanceQueries.Cabinet(gov, candidates: true);
        Heading(_left, string.Format(Tr("Quem pode ser nomeado ({0})"), candidates.Count), 20);
        foreach (AideView v in candidates.OrderByDescending(c => c.PaysPromise).ThenByDescending(c => c.Aide.Quality))
        {
            AideRow(v);
        }
    }

    private void BuildAideSheet(GovernanceEngine gov, Aide aide)
    {
        AideView view = GovernanceQueries.Cabinet(gov, candidates: !aide.Appointed).FirstOrDefault(v => v.Aide.PersonId == aide.PersonId)
            ?? new AideView(aide, GovName(aide.PersonId), null, null, null, false);
        GovBackButton();
        HBoxContainer top = Ui.Row(_left, 12);
        Portrait(top, aide.PersonId, 120);
        var head = Ui.Column(top, 4);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        NameTitle(head, view.Name, 22);
        Glossary.Rich(head, aide.Left ? Tr("Deixou o gabinete. Pessoa fictícia.")
            : aide.Appointed ? string.Format(Tr("{0} do seu gabinete{1}. Pessoa fictícia."), view.RoleName, view.ZoneName is null ? "" : string.Format(Tr(", em {0}"), view.ZoneName))
            : Tr("Nome para o seu gabinete. Pessoa fictícia."));
        ValueRow(_left, Tr("Qualidade (1 a 5)"), aide.Quality.ToString(PtBr), highlight: true);
        Glossary.Rich(_left, string.Format(Tr("Origem: {0}."), AideOrigin(view)), 18, Palette.Texto);
        if (view.PaysPromise)
        {
            Glossary.Rich(_left, Tr("Há uma promessa de vaga no Livro de compromissos: nomear a cumpre; deixar de fora, não."), 18, Palette.Texto);
        }

        GovMemories(aide.PersonId);
        if (aide.Left)
        {
            return;
        }

        if (aide.Appointed)
        {
            string person = aide.PersonId;
            GovForecast dismiss = gov.ForecastDismissAide(person);
            GovForecastCard(_left, dismiss, "PrevisaoDemitir", Tr("Previsão de demitir"));
            GovCommand(_left, "Demitir", string.Format(Tr("Demite {0} do gabinete."), view.Name), dismiss, () => gov.DismissAide(person), "Demitir", danger: true);
            return;
        }

        Heading(_left, Tr("Função"), 20);
        _govRole ??= _game.GovernanceContent.Instruments.Funcoes.FirstOrDefault(f => gov.ForecastAppointAide(aide.PersonId, f.Id, gov.G.Zones[0]).Blocked is null)?.Id
            ?? _game.GovernanceContent.Instruments.Funcoes[0].Id;
        foreach (AideRole role in _game.GovernanceContent.Instruments.Funcoes)
        {
            string id = role.Id;
            var b = Ui.Button(_left, role.Nome, () => { _govRole = id; Refresh(); },
                Palette.Tip(string.Format(Tr("Função {0}."), role.Nome), LowerFirst(role.Descricao).TrimEnd('.') + ".", Tr("nada para escolher; nomear ocupa uma vaga do gabinete.")));
            b.Name = "Funcao_" + role.Id;
            b.Alignment = HorizontalAlignment.Left;
            b.ToggleMode = true;
            b.ButtonPressed = _govRole == role.Id;
            if (_govRole == role.Id)
            {
                Palette.Selected(b);
            }
        }

        bool needsZone = _govRole == AideRole.Base;
        if (needsZone)
        {
            GovZonePicker("assessor");
        }
        else
        {
            _govPick = null;
        }

        GovForecast forecast = gov.ForecastAppointAide(aide.PersonId, _govRole, needsZone ? _govZone : null);
        if (needsZone && _govZone is null)
        {
            forecast = GovForecast.Block(GovFailure.AlvoInvalido, Tr("o assessor de base cuida de uma zona: clique nela no mapa"));
        }

        GovForecastCard(_left, forecast, "PrevisaoNomear", Tr("Previsão de nomear"));
        string personId = aide.PersonId;
        string roleId = _govRole;
        string? zone = needsZone ? _govZone : null;
        GovCommand(_left, "Nomear", string.Format(Tr("Nomeia {0} para a função escolhida."), view.Name), forecast, () => gov.AppointAide(personId, roleId, zone), "Nomear", primary: true);
    }

    /// <summary>Relação e lembranças de uma pessoa da cidade (GDD 17.2, peça 2), como na ficha da campanha.</summary>
    private void GovMemories(string personId)
    {
        if (State.City?.Person(personId) is not { } person)
        {
            return;
        }

        DateOnly today = Core.State.Clock.Today(State);
        ValueRow(_left, Tr("Relação com você (−100 a 100)"), Signed(Gov?.RelationOf(personId) ?? person.Relation(today, _game.Campaign.People), "0"));
        Heading(_left, Tr("Lembranças"), 20);
        if (person.Memories.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma ainda."));
        }

        foreach (Memory memory in person.Memories.OrderByDescending(m => m.Date).Take(6))
        {
            Glossary.Rich(_left, string.Format(Tr("· {0}, {1}: {2}"), memory.Text.TrimEnd('.'), memory.Date.ToString("MMM/yyyy", PtBr), Signed(memory.Pontos, "0")));
        }
    }

    // ------------------------------------------------------------------ Instrumentos

    private void BuildInstrumentsArea()
    {
        if (Gov is not { } gov)
        {
            return;
        }

        GovernanceState g = gov.G;
        Glossary.Rich(_left, string.Format(Tr("Turnos desta semana: {0} de {1}. Emendas deste ano: {2}."), g.TurnsLeft, _game.GovernanceContent.Instruments.Regras.TurnosPorSemana, State.Mandate?.EmendasLeft ?? 0), 18, Palette.Texto);
        var options = gov.CouncilOptions();
        foreach (InstrumentDefinition def in _game.GovernanceContent.Instruments.Instrumentos)
        {
            int available = options.Count(o => o.InstrumentId == def.Id);
            string cost = string.Format(Tr(def.Turnos == 1 ? "{0} turno" : "{0} turnos"), def.Turnos) + (def.Capital > 0 ? string.Format(Tr(", {0} de capital político"), def.Capital) : "");
            string shortCost = string.Format(Tr(def.Turnos == 1 ? "{0} turno" : "{0} turnos"), def.Turnos) + (def.Capital > 0 ? string.Format(Tr(" e {0} de capital"), def.Capital) : "");
            string id = def.Id;
            GovRow(_left, $"{def.Nome}\n{shortCost} · {string.Format(Tr(available == 1 ? "{0} opção" : "{0} opções"), available)}", () => OpenInstrument(id, null),
                Palette.Tip(def.Nome + ": " + LowerFirst(def.Descricao).TrimEnd('.') + ".", Tr("abre o instrumento: você escolhe o alvo e vê a previsão antes de confirmar."), cost + "."), "Instrumento_" + def.Id);
        }

        var ready = GovernanceQueries.Works(gov).Where(w => w.ByPlayer && w.Work.Stage == WorkStage.Pronta).ToList();
        if (ready.Count > 0)
        {
            Heading(_left, Tr("Obras suas prontas para entregar"), 20);
            foreach (WorkView w in ready)
            {
                WorkRow(w);
            }
        }

        var findings = g.Inspections.Where(i => i.Pending).ToList();
        if (findings.Count > 0)
        {
            Heading(_left, Tr("Achados esperando decisão"), 20);
            foreach (Inspection i in findings)
            {
                string id = i.Id;
                Work? work = g.Work(i.WorkId);
                GovRow(_left, string.Format(Tr("{0} em {1}\nresposta até {2}"), work is null ? "" : gov.WorkName(work), GZone(work?.ZoneId), i.DecisionDeadline?.ToString("dd/MM", PtBr)), () => OpenGov("achado", id),
                    Palette.Tip(Tr("Achado de fiscalização."), Tr("abre o achado com as três saídas: denunciar, negociar em silêncio ou encaminhar ao Tribunal de Contas."), Tr("nada para abrir.")), "Achado_" + i.Id);
            }
        }

        var waiting = g.Requests.Where(r => !r.Answered).ToList();
        var indications = g.Indications.Where(i => i.State == IndicationState.Pendente).ToList();
        if (waiting.Count + indications.Count > 0)
        {
            Heading(_left, Tr("Em andamento"), 20);
            foreach (InfoRequest r in waiting)
            {
                string target = r.TargetKind == InfoRequest.SobreObra && g.Work(r.TargetId) is { } w ? $"{gov.WorkName(w)} ({GZone(w.ZoneId)})" : _game.GovernanceContent.Secretaries.Secretaria(r.TargetId).Nome;
                Glossary.Rich(_left, string.Format(Tr("· Requerimento sobre {0}: resposta até {1}."), target, r.Due.ToString("dd/MM", PtBr)));
            }

            foreach (Indication i in indications)
            {
                Glossary.Rich(_left, string.Format(PtBr, Tr("· Indicação de {0} em {1}: o prefeito decide até {2} (chance de atender: {3:P0})."),
                    LowerFirst(_game.GovernanceContent.Works.Find(i.TypeId)?.Nome ?? i.TypeId), GZone(i.ZoneId), i.DecideOn.ToString("dd/MM", PtBr), i.Chance));
            }
        }

        var answered = g.Requests.Where(r => r.Answered && r.Finding is not null).Reverse().Take(3).ToList();
        if (answered.Count > 0)
        {
            Heading(_left, Tr("Respostas recebidas"), 20);
            foreach (InfoRequest r in answered)
            {
                Glossary.Rich(_left, "· " + r.Finding!.TrimEnd('.') + ".");
            }
        }
    }

    private void OpenInstrument(string id, string? zone)
    {
        _govTarget = null;
        _govType = null;
        OpenGov("instrumento", id, zone);
    }

    private void BuildInstrumentSheet(string id)
    {
        GovernanceEngine gov = Gov!;
        InstrumentDefinition? def = _game.GovernanceContent.Instruments.Instrumentos.FirstOrDefault(i => i.Id == id);
        if (def is null)
        {
            GovBackButton();
            return;
        }

        GovBackButton();
        NameTitle(_left, def.Nome, 22);
        Glossary.Rich(_left, def.Descricao.TrimEnd('.') + ".", 18, Palette.Texto);
        Glossary.Rich(_left, string.Format(Tr("Turnos desta semana: {0}."), gov.G.TurnsLeft));
        var options = gov.CouncilOptions().Where(o => o.InstrumentId == id).ToList();
        bool byZone = id is InstrumentDefinition.Indicacao or InstrumentDefinition.Emenda;
        InstrumentOption? selected;
        GovForecast forecast;
        if (byZone)
        {
            var types = _game.GovernanceContent.Works.Tipos.Where(t => id == InstrumentDefinition.Emenda ? t.Emenda : t.Pequena || t.Servico).ToList();
            _govType ??= (_govZone is null ? types.FirstOrDefault() : types.FirstOrDefault(t => gov.Fits(t, _govZone)) ?? types.FirstOrDefault())?.Id;
            FieldLabel("O que pedir");
            OptionButton type = Ui.Options(_left, types.Select(t => (t.Id, string.Format(Tr("{0} · {1}"), t.Nome, GovMoney(gov.CostOf(t))))), _govType, t => { _govType = t; Refresh(); }, translate: false);
            type.Name = "Tipo";
            type.ClipText = true;
            type.TooltipText = Palette.Tip(Tr("Tipo de obra ou de serviço."), Tr("define o que entra no mapa, quem pode ser atendido e qual promessa do Livro a entrega cumpre."), Tr("nada para escolher; o custo da obra aparece ao lado do nome."));
            WorkType? chosen = types.FirstOrDefault(t => t.Id == _govType);
            GovZonePicker(id, chosen is null ? null : z => gov.Fits(chosen, z));
            selected = options.FirstOrDefault(o => o.TypeId == _govType && o.ZoneId == _govZone);
            forecast = _govType is null || _govZone is null ? GovForecast.Block(GovFailure.AlvoInvalido, Tr("escolha a zona clicando nela no mapa"))
                : selected?.Forecast ?? (id == InstrumentDefinition.Emenda ? gov.ForecastEmenda(_govType, _govZone) : gov.ForecastIndicacao(_govType, _govZone));
        }
        else
        {
            _govPick = null;
            if (options.Count == 0)
            {
                Glossary.Rich(_left, id switch
                {
                    InstrumentDefinition.NegociarVoto => Tr("Nenhum voto para negociar agora: não há projeto no plenário com vereador indeciso que tenha um pedido."),
                    InstrumentDefinition.Projeto => Tr("Nenhum projeto para apresentar agora."),
                    _ => gov.G.TurnsLeft < def.Turnos ? Tr("Sem turnos nesta semana: eles voltam na segunda-feira.") : Tr("Nenhum alvo agora: não há obra em andamento na cidade que ainda não tenha sido examinada."),
                }, 18, Palette.Texto);
            }

            selected = options.FirstOrDefault(o => OptionKey(o) == _govTarget) ?? options.FirstOrDefault();
            FieldLabel(id == InstrumentDefinition.Projeto ? "Projeto" : "Alvo");
            foreach (InstrumentOption option in options.Take(12))
            {
                string key = OptionKey(option);
                GovRow(_left, option.Label, () => { _govTarget = key; _map.Select(option.ZoneId); if (option.ZoneId is not null) { _map.CenterOn(option.ZoneId); } Refresh(); },
                    GovTip(option.Label + ".", option.Forecast), "Alvo_" + options.IndexOf(option), selected is not null && OptionKey(selected) == key);
            }

            if (options.Count > 12)
            {
                Glossary.Rich(_left, string.Format(Tr("E mais {0}; para um alvo específico, abra a obra pelo mapa."), options.Count - 12), 18, Palette.Texto3);
            }

            forecast = selected?.Forecast ?? GovForecast.Block(gov.G.TurnsLeft < def.Turnos ? GovFailure.SemTurno : GovFailure.AlvoInvalido, "");
            if (id == InstrumentDefinition.Projeto && selected is not null && _game.Legislature.MunicipalBills.Find(selected.TargetId ?? "") is { } template)
            {
                Glossary.Rich(_left, template.Summary.TrimEnd('.') + ".");
            }
        }

        GovForecastCard(_left, forecast);
        string label = id switch
        {
            InstrumentDefinition.Requerimento => "Protocolar o requerimento",
            InstrumentDefinition.Indicacao => "Fazer a indicação",
            InstrumentDefinition.Fiscalizacao => "Fiscalizar",
            InstrumentDefinition.Emenda => "Destinar a emenda",
            InstrumentDefinition.Projeto => "Apresentar o projeto",
            _ => "Fechar o acordo",
        };
        string? type2 = _govType;
        string? zone2 = _govZone;
        GovCommand(_left, label, string.Format(Tr("Usa o instrumento: {0}."), LowerFirst(def.Nome)), forecast, () => selected is not null ? gov.Use(selected)
            : id == InstrumentDefinition.Emenda ? gov.Emenda(type2!, zone2!) : gov.Indicacao(type2!, zone2!), "Confirmar", primary: true, after: result =>
        {
            if (result.Ok && id == InstrumentDefinition.Fiscalizacao && result.CreatedId is { } inspection && gov.G.Inspections.Any(i => i.Id == inspection && i.Pending))
            {
                _govSeen.Add("achado:" + inspection);
                _govSheet = "achado";
                _govId = inspection;
            }
        });
    }

    private string FindingName(string finding) => finding switch
    {
        Inspection.Irregular => Tr("irregularidade"),
        Inspection.Atraso => Tr("atraso"),
        _ => Tr("normalidade"),
    };

    private void BuildFindingSheet(GovernanceEngine gov, Inspection inspection)
    {
        _govPick = null;
        Work? work = gov.G.Work(inspection.WorkId);
        GovBackButton();
        NameTitle(_left, string.Format(Tr("Achado: {0}"), FindingName(inspection.Finding)), 22);
        if (work is not null)
        {
            WorkView? view = GovernanceQueries.Works(gov).FirstOrDefault(w => w.Work.Id == work.Id);
            Glossary.Rich(_left, string.Format(Tr("Fiscalização de {0} em {1}, em {2}. Etapa: {3}. Empresa: {4}. Atraso: {5} semana(s)."),
                gov.WorkName(work), GZone(work.ZoneId), inspection.Date.ToString("dd/MM/yyyy", PtBr), view?.StageName ?? "", view?.Company ?? Tr("ainda não contratada"), work.DelayWeeks), 18, Palette.Texto);
            if (work.Revealed && work.Irregular)
            {
                Glossary.Rich(_left, string.Format(PtBr, Tr("Irregularidade conhecida: sobrepreço de {0:P0} no contrato."), work.Overprice), 18, Palette.Texto);
            }

            _map.Select(work.ZoneId);
        }

        if (!inspection.Pending)
        {
            Glossary.Rich(_left, inspection.Decision is null ? Tr("Nada a decidir: a fiscalização encontrou a obra em ordem.")
                : string.Format(Tr("Você decidiu: {0}."), LowerFirst(_game.GovernanceContent.Instruments.Achados.FirstOrDefault(a => a.Id == inspection.Decision)?.Nome ?? inspection.Decision)));
            if (inspection.TribunalDue is { } due)
            {
                Glossary.Rich(_left, string.Format(Tr("O Tribunal de Contas responde até {0}."), due.ToString("dd/MM/yyyy", PtBr)));
            }

            return;
        }

        Glossary.Rich(_left, string.Format(Tr("Decida até {0}. Sem decisão, o achado é arquivado."), inspection.DecisionDeadline?.ToString("d 'de' MMMM", PtBr)));
        foreach (FindingOption option in _game.GovernanceContent.Instruments.Achados)
        {
            GovForecast forecast = gov.ForecastFinding(inspection.Id, option.Id);
            PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
            card.Name = "Saida_" + option.Id;
            var col = Ui.Column(card, 4);
            Glossary.Plain(col, option.Nome, 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            Glossary.Rich(col, option.Descricao.TrimEnd('.') + ".");
            if (forecast.Blocked is null)
            {
                foreach (ForecastLine l in forecast.Lines.Where(Matters))
                {
                    string text = GovLine(l);
                    Glossary.Rich(col, OneDot("· " + char.ToUpper(text[0], PtBr) + text[1..] + "."));
                }

                if (forecast.Remembered.Count > 0)
                {
                    Glossary.Rich(col, OneDot(string.Format(Tr("Quem vai lembrar: {0}."), string.Join(", ", forecast.Remembered))));
                }
            }
            else
            {
                Glossary.Rich(col, Tr("Não dá:") + " " + GovReason(forecast), 18, Palette.Texto);
            }

            string optionId = option.Id;
            string inspectionId = inspection.Id;
            GovCommand(col, option.Nome, option.Descricao.TrimEnd('.') + ".", forecast, () => gov.DecideFinding(inspectionId, optionId), "Achado_" + option.Id);
        }
    }

    // ------------------------------------------------------------------ Mesa e CPI

    private void BuildMesaArea()
    {
        if (Gov is not { } gov)
        {
            return;
        }

        GovernanceState g = gov.G;
        Heading(_left, Tr("Mesa da Câmara"), 20);
        Glossary.Rich(_left, string.Format(Tr("Presidente: {0}. Próxima eleição da Mesa: {1}. A Mesa é eleita a cada dois anos e quem preside não é reconduzido ao mesmo cargo na eleição seguinte."),
            g.Mesa.PresidentId is null ? Tr("a eleger") : MemberLabel(g.Mesa.PresidentId), g.Mesa.NextElection.ToString("dd/MM/yyyy", PtBr)), 18, Palette.Texto);
        if (g.Mesa.PlayerRunning)
        {
            Glossary.Rich(_left, Tr("Você é candidato à presidência na próxima eleição da Mesa."), 18, Palette.Texto);
        }

        GovForecast mesa = gov.ForecastRunForMesa();
        if (mesa.Blocked is null)
        {
            GovForecastCard(_left, mesa, "PrevisaoMesa", Tr("Previsão de disputar a presidência"));
        }

        GovCommand(_left, "Candidatar-se à presidência", Tr("Lança o seu nome para presidir a Câmara na próxima eleição da Mesa."), mesa, gov.RunForMesa, "Mesa_Candidatar", primary: mesa.Blocked is null);

        Heading(_left, Tr("CPI"), 20);
        if (g.Cpi is { } cpi)
        {
            PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
            card.Name = "CpiAberta";
            var col = Ui.Column(card, 4);
            Glossary.Rich(col, string.Format(PtBr, Tr("CPI em andamento: {0}. Aberta em {1} com {2} assinaturas; termina em {3}. Prova reunida: {4:P0}."),
                cpi.Subject.TrimEnd('.'), cpi.Opened.ToString("dd/MM/yyyy", PtBr), cpi.Signatures, cpi.Ends.ToString("dd/MM/yyyy", PtBr), cpi.Evidence), 18, Palette.Texto);
            Glossary.Rich(col, GovernanceQueries.CassationText(gov).TrimEnd('.') + ".");
        }
        else
        {
            Glossary.Rich(_left, Tr("Nenhuma CPI em andamento. Para pedir uma é preciso um fato determinado: uma obra com irregularidade já revelada por requerimento ou fiscalização."));
        }

        var known = g.Works.Where(w => w.Revealed && w.Irregular).ToList();
        foreach (Work work in known.Take(4))
        {
            GovForecast forecast = gov.ForecastCpi(work.Id);
            string id = work.Id;
            PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
            var col = Ui.Column(card, 4);
            Glossary.Rich(col, string.Format(Tr("{0} em {1}: irregularidade conhecida."), gov.WorkName(work), GZone(work.ZoneId)), 18, Palette.Texto);
            if (forecast.Blocked is null)
            {
                foreach (string line in forecast.Explanation)
                {
                    Glossary.Rich(col, "· " + line.TrimEnd('.') + ".");
                }
            }

            GovCommand(col, "Pedir CPI", Tr("Colhe as assinaturas para uma comissão parlamentar de inquérito sobre esta obra."), forecast, () => gov.RequestCpi(id), "Cpi_" + work.Id);
        }

        foreach (Cpi past in g.CpiHistory.AsEnumerable().Reverse().Take(3))
        {
            Glossary.Rich(_left, string.Format(Tr("· CPI encerrada ({0}): {1}."), past.Ends.ToString("MM/yyyy", PtBr), past.Subject.TrimEnd('.')));
        }

        BuildSeatsBlock();
    }

    // ------------------------------------------------------------------ Base e Livro de compromissos

    private enum PromiseRoute
    {
        Obra,
        Cargo,
        Presenca,
        Posicao,
        Voto,
    }

    private PromiseRoute RouteOf(Promise p)
    {
        CollectionCatalog collection = _game.GovernanceContent.Collection;
        return collection.PromessaDeCargo.Contains(p.Object) || p.Object == "voto_" + VoteRequest.Cargo ? PromiseRoute.Cargo
            : collection.PromessaDePresenca.Contains(p.Object) ? PromiseRoute.Presenca
            : collection.PromessaDePosicao.Contains(p.Object) ? PromiseRoute.Posicao
            : p.Object == "voto_" + VoteRequest.ApoioProjeto ? PromiseRoute.Voto
            : PromiseRoute.Obra;
    }

    /// <summary>Botões que levam ao instrumento certo para cumprir uma promessa (GDD 17.14, "a base cobra").</summary>
    private void PromiseActions(Node parent, PromiseView v, string suffix)
    {
        GovernanceEngine gov = Gov!;
        Promise p = v.Promise;
        string zone = p.ZoneId ?? (p.ToKind == Promise.ParaZona ? p.ToId : null) ?? "";
        string? zoneId = zone.Length > 0 && _map.Zonas.ContainsKey(zone) ? zone : null;
        bool mayor = Kind == GovKind.Prefeito;
        switch (RouteOf(p))
        {
            case PromiseRoute.Cargo when mayor:
                string promiseId = p.Id;
                GovCommand(parent, "Dar o cargo", Tr("Nomeia o indicado para um cargo na prefeitura e cumpre a promessa."), gov.ForecastGrantPost(promiseId), () => gov.GrantPost(promiseId), "Cumprir_" + suffix);
                break;
            case PromiseRoute.Cargo:
                Ui.Button(parent, "Abrir o gabinete", () => { _govDemand = null; OpenGovArea("gabinete"); },
                    Palette.Tip(Tr("Abre o Gabinete."), Tr("mostra quem pode ser nomeado; o nome ligado a esta promessa vem marcado."), Tr("nada."))).Name = "Cumprir_" + suffix;
                break;
            case PromiseRoute.Presenca:
                Ui.Button(parent, mayor ? "Abrir a zona" : "Fazer uma indicação na zona", () =>
                {
                    _govDemand = null;
                    if (mayor || zoneId is null)
                    {
                        if (zoneId is not null)
                        {
                            _map.Select(zoneId);
                            OpenGov("zona", zoneId, zoneId);
                        }
                    }
                    else
                    {
                        OpenGovArea("instrumentos");
                        OpenInstrument(InstrumentDefinition.Indicacao, zoneId);
                    }
                }, Palette.Tip(Tr("Leva ao que traz presença nesta zona."), Tr("presença na zona vem de indicação atendida, emenda entregue, fiscalização e inauguração ali."), Tr("nada para abrir."))).Name = "Cumprir_" + suffix;
                break;
            case PromiseRoute.Posicao or PromiseRoute.Voto:
                Ui.Button(parent, mayor ? "Abrir a Câmara" : "Abrir a pauta", () => { _govDemand = null; OpenGovArea(mayor ? "camara" : "mandato"); },
                    Palette.Tip(Tr("Abre as votações."), Tr("a promessa se cumpre com o voto ou com a posição defendida quando o assunto for a plenário."), Tr("nada."))).Name = "Cumprir_" + suffix;
                break;
            default:
                if (mayor)
                {
                    Ui.Button(parent, "Iniciar a obra", () => { _govDemand = null; OpenGovArea("obras"); _govType = null; OpenGov("nova_obra", null, zoneId); },
                        Palette.Tip(Tr("Abre o início de obra com a zona da promessa já escolhida."), Tr("você escolhe o tipo e vê o custo, o prazo e a previsão antes de confirmar."), Tr("nada para abrir."))).Name = "Cumprir_" + suffix;
                }
                else
                {
                    HBoxContainer row = Ui.Row(parent, 6);
                    Ui.Button(row, "Indicação", () => { _govDemand = null; OpenGovArea("instrumentos"); OpenInstrument(InstrumentDefinition.Indicacao, zoneId); },
                        Palette.Tip(Tr("Abre a Indicação com a zona da promessa já escolhida."), Tr("o prefeito atende ou não, conforme a relação com você; atendida, a obra entra no mapa a seu pedido."), Tr("nada para abrir; a indicação gasta turno."))).Name = "Cumprir_" + suffix;
                    Ui.Button(row, "Emenda", () => { _govDemand = null; OpenGovArea("instrumentos"); OpenInstrument(InstrumentDefinition.Emenda, zoneId); },
                        Palette.Tip(Tr("Abre a Emenda com a zona da promessa já escolhida."), Tr("a obra entra no mapa com prazo; a liberação depende do Executivo, e a entrega é uma inauguração."), Tr("nada para abrir; a emenda gasta turno, capital político e uma emenda do ano."))).Name = "CumprirEmenda_" + suffix;
                }

                break;
        }
    }

    private void PromiseCard(Node parent, PromiseView v)
    {
        PanelContainer card = Card(parent, v.WeeksLeft <= 26 ? Palette.CartaoAlto : Palette.Cartao, Palette.Linha, 10);
        card.Name = "Promessa_" + v.Promise.Id;
        var col = Ui.Column(card, 4);
        Glossary.Rich(col, v.Promise.Text.TrimEnd('.') + ".", 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, string.Format(Tr("Quem cobra: {0}. Prazo: {1} ({2}). Cobranças até hoje: {3}."), v.Holder, v.Deadline.ToString("dd/MM/yyyy", PtBr),
            v.WeeksLeft < 0 ? Tr("vencido") : string.Format(Tr(v.WeeksLeft == 1 ? "falta {0} semana" : "faltam {0} semanas"), v.WeeksLeft), v.Promise.Cobrancas));
        Glossary.Rich(col, string.Format(Tr("Como cumprir: {0}"), v.HowToFulfil.TrimEnd('.') + "."), 18, Palette.Texto);
        PromiseActions(col, v, v.Promise.Id);
    }

    private void BuildBaseArea()
    {
        if (Gov is not { } gov)
        {
            return;
        }

        GovernanceState g = gov.G;
        InstrumentRules rules = _game.GovernanceContent.Instruments.Regras;
        Glossary.Rich(_left, string.Format(PtBr, Tr("A base é onde você teve mais presença na posse. Abaixo de {0:0} de presença, a zona da base cobra você. A presença cai um pouco toda semana onde você não aparece."), rules.BaseCobraAbaixo), 18, Palette.Texto);
        Heading(_left, Tr("Zonas"), 20);
        foreach (ZoneStanding z in GovernanceQueries.Zones(gov).OrderByDescending(z => z.Base).ThenByDescending(z => z.Presence))
        {
            string marks = (z.Base ? " · " + Tr("base") : "") + (z.BaseAide is null ? "" : " · " + string.Format(Tr("assessor: {0}"), z.BaseAide));
            string id = z.ZoneId;
            GovRow(_left, string.Format(PtBr, "{0}{1}\n{2} {3:0.0} ({4} {5:0.0}) · {6}: {7}", z.Name, marks, Tr("presença"), z.Presence, Tr("na posse"), z.PresenceAtStart, Tr("promessas"), z.OpenPromises),
                () => { _map.Select(id); OpenGov("zona", id, id); }, Palette.Tip(string.Format(Tr("Zona {0}."), z.Name), Tr("abre a ficha da zona e a destaca no mapa."), Tr("nada.")), "Zona_" + z.ZoneId);
        }

        var demands = g.Demands.Where(d => d.Kind == Demand.Presenca && d.Met is null).ToList();
        if (demands.Count > 0)
        {
            Heading(_left, Tr("Cobrança de presença"), 20);
            foreach (Demand d in demands)
            {
                Glossary.Rich(_left, "· " + d.Text.TrimEnd('.') + (d.Deadline is { } deadline ? string.Format(Tr(" (até {0})"), deadline.ToString("dd/MM", PtBr)) : "") + ".", 18, Palette.Texto);
            }
        }

        BuildLedgerBlock(gov);
    }

    /// <summary>Promessas e dívidas abertas do Livro, com prazo, quem cobra e como cumprir.</summary>
    private void BuildLedgerBlock(GovernanceEngine gov)
    {
        var promises = GovernanceQueries.Promises(gov);
        var (total, collected) = gov.CollectionStats();
        Heading(_left, string.Format(Tr("Promessas no Livro ({0})"), promises.Count), 20);
        Glossary.Rich(_left, string.Format(Tr("Na posse havia {0} promessas abertas; {1} já foram cobradas ao menos uma vez. Promessa vencida derruba a relação com quem cobra."), total, collected));
        if (promises.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma promessa aberta."));
        }

        foreach (PromiseView v in promises.Take(8))
        {
            PromiseCard(_left, v);
        }

        var debts = State.Ledger.Debts.Where(d => !d.Settled && !d.Extinct).ToList();
        if (debts.Count > 0)
        {
            Heading(_left, string.Format(Tr("Dívidas ({0})"), debts.Count), 20);
            foreach (Debt d in debts.Take(6))
            {
                Glossary.Rich(_left, string.Format(Tr("· Com {0}, desde {1}: {2}"), GovName(d.ToPersonId), d.Made.ToString("MM/yyyy", PtBr), d.Text.TrimEnd('.') + "."));
            }
        }
    }
}
