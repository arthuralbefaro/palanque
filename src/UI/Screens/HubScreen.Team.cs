using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.People;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Equipe (GDD 17.7): o bloco da área Campanha, a tela inteira com as funções e os candidatos, a ficha de cada pessoa
/// (vínculo, previsão de contratar e de demitir, lembranças), o conselho da semana e as decisões da equipe no cartão
/// de 720 px sobre o mapa. Só lê o Core e chama os comandos dele.
/// </summary>
public partial class HubScreen
{
    private string? _confirmFire;
    private bool _staffHidden;
    private PanelContainer? _staffCard;
    private string? _staffResultTitle;
    private List<string>? _staffResult;

    /// <summary>Cartão de decisão da equipe (ou do resultado dela) aberto sobre o mapa.</summary>
    public bool StaffCardOpen => _staffCard is not null && IsInstanceValid(_staffCard);

    private StaffCatalog StaffContent => _engine.Context.Campaign.Staff;

    /// <summary>Nome fictício com o que é (princípio j): "Lúcia R. (coordena a sua campanha)".</summary>
    private string StaffLabel(StaffMember m) =>
        m.Hired ? $"{_engine.NameOf(m.PersonId)} ({StaffContent.Role(m.Role).Papel})"
        : m.Left ? string.Format(Tr("{0} (deixou a sua campanha)"), _engine.NameOf(m.PersonId))
        : string.Format(Tr("{0} (nome para a função {1})"), _engine.NameOf(m.PersonId), StaffContent.Role(m.Role).Nome);

    /// <summary>Faixa sem sinal: "3" ou "3 a 4".</summary>
    private string Span(double min, double max, string format)
    {
        string a = min.ToString(format, PtBr), b = max.ToString(format, PtBr);
        return a == b ? a : $"{a} {Tr("a")} {b}";
    }

    private string QualityText(StaffMember m) =>
        string.Format(Tr("qualidade {0}"), Span(m.QualityShownMin, m.QualityShownMax, "0"));

    private string WeeksOfCash(double weeks) =>
        double.IsInfinity(weeks) ? Tr("sem gasto semanal") : string.Format(PtBr, Tr(Math.Floor(weeks) == 1 ? "{0:0} semana" : "{0:0} semanas"), Math.Floor(weeks));

    /// <summary>Título de bloco com o botão "Ver tudo" à direita (ART_GUIDE, densidade: bloco com título, até 5 linhas e "ver tudo").</summary>
    private void BlockHeader(string title, string buttonName, Action open, string tip)
    {
        HBoxContainer row = Ui.Row(_left, 8);
        RichTextLabel heading = Heading(row, title, 20);
        heading.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        var all = Ui.Button(row, "Ver tudo", open, tip);
        all.Name = buttonName;
        all.SizeFlagsVertical = SizeFlags.ShrinkCenter;
    }

    // ------------------------------------------------------------------ bloco da área Campanha

    private void BuildTeamBlock()
    {
        PayrollView payroll = _engine.Payroll();
        BlockHeader(string.Format(Tr("Equipe ({0}/{1})"), _engine.TeamSlotsUsed, _engine.TeamSlots), "VerEquipe", () => OpenSheet(Sheet.Equipe, null),
            Palette.Tip(Tr("Abre a Equipe inteira."), Tr("mostra as cinco funções, quem pode ocupar cada uma, o conselho da semana e a folha."), Tr("nada para abrir; cada pessoa contratada tem salário semanal.")));
        var team = _engine.HiredStaff.ToList();
        if (team.Count == 0)
        {
            Glossary.Rich(_left, Tr("Ninguém contratado. Sem coordenação, não há conselho da semana nem representante."));
        }

        foreach (StaffMember member in team.Take(4))
        {
            // No bloco, linha de lista sem retrato (ART_GUIDE, densidade); a tela inteira mostra o retrato.
            var b = Ui.Button(_left, $"{StaffLabel(member)}\n{QualityText(member)} · {Money(member.Salary)}/{Tr("sem.")}", () => OpenSheet(Sheet.Membro, member.PersonId),
                Palette.Tip(string.Format(Tr("{0}; lealdade {1}."), StaffLabel(member), member.Loyalty), Tr("abre a ficha: vínculo, temperamento, lembranças, o que faz por você e a opção de demitir."), string.Format(Tr("nada para abrir; o salário é {0} por semana."), Money(member.Salary))));
            b.Name = "Linha_" + member.PersonId;
            b.Alignment = HorizontalAlignment.Left;
            b.ClipText = true;
            b.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            Palette.ListRow(b);
        }

        if (_engine.CabosNetworkActive)
        {
            ValueRow(_left, Tr("Rede de cabos eleitorais (ocupa uma vaga)"), string.Format(PtBr, Tr("{0} pessoas"), _engine.CabosPlanned));
        }

        if (team.Count > 0)
        {
            Glossary.Rich(_left, string.Format(Tr("Folha: {0} por semana. O caixa paga {1}."), Money(payroll.Weekly), WeeksOfCash(payroll.WeeksOfCash)), 18, payroll.Alert ? Palette.Texto : Palette.Texto2);
        }

        if (_engine.Team.Advice is { } advice)
        {
            var (done, needed) = _engine.AdviceProgress();
            Glossary.Rich(_left, string.Format(Tr("Conselho da semana: concentrar em {0} ({1} de {2} feitos)."), ZoneName(advice.ZoneId), done, needed), 18, Palette.Texto);
        }
    }

    /// <summary>Linha de pessoa da equipe: retrato, nome com o que é e, embaixo, qualidade, salário e lealdade; abre a ficha.</summary>
    private void StaffRow(Node parent, StaffMember member)
    {
        PanelContainer card = ClickCard(parent, () => OpenSheet(Sheet.Membro, member.PersonId),
            Palette.Tip(StaffLabel(member) + ".", Tr("abre a ficha: vínculo, temperamento, lembranças, a previsão e o que dá para fazer."), string.Format(Tr("nada para abrir; o salário é {0} por semana."), Money(member.Salary))),
            "Membro_" + member.PersonId);
        HBoxContainer row = Ui.Row(card, 10);
        Portrait(row, member.PersonId, 56);
        var col = Ui.Column(row, 2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Glossary.Plain(col, StaffLabel(member), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, string.Format(Tr("{0} · {1}/sem. · lealdade {2}"), QualityText(member), Money(member.Salary), member.Loyalty));
    }

    // ------------------------------------------------------------------ tela inteira

    private void BuildTeamSheet()
    {
        BackButton();
        Heading(_left, string.Format(Tr("Equipe ({0}/{1})"), _engine.TeamSlotsUsed, _engine.TeamSlots));
        FirstTimeHint("equipe");
        PayrollView payroll = _engine.Payroll();
        Glossary.Rich(_left, string.Format(Tr("Vagas nesta cidade: {0}, contando a rede de cabos eleitorais. Folha: {1} por semana, paga toda segunda-feira. O caixa de hoje paga {2}."),
            _engine.TeamSlots, Money(payroll.Weekly), WeeksOfCash(payroll.WeeksOfCash)), 18, Palette.Texto);
        if (payroll.Alert)
        {
            PanelContainer alert = Card(_left, Palette.CartaoAlto, Palette.Negativo, 10);
            alert.Name = "AlertaFolha";
            Glossary.Rich(alert, string.Format(Tr("O caixa paga menos de {0} semanas de folha. Quem fica sem receber perde {1} de lealdade por semana e lembra disso."), StaffContent.Regras.SemanasDeFolhaAlerta, Math.Abs(StaffContent.Regras.LealdadeSalarioAtrasado)), 18, Palette.Texto);
        }

        AdviceCard();

        foreach (StaffRole role in StaffContent.Funcoes)
        {
            Heading(_left, role.Nome, 20);
            Glossary.Rich(_left, role.Descricao);
            StaffMember? holder = _engine.StaffIn(role.Id);
            if (holder is not null)
            {
                StaffRow(_left, holder);
                continue;
            }

            var candidates = _engine.StaffCandidates(role.Id).Select(v => v.Member).Where(m => !m.Hired).ToList();
            if (candidates.Count == 0)
            {
                Glossary.Rich(_left, Tr("Ninguém disponível na cidade para esta função."), 18, Palette.Texto3);
            }

            foreach (StaffMember candidate in candidates)
            {
                StaffRow(_left, candidate);
            }
        }

        Heading(_left, Tr("Cabos eleitorais"), 20);
        Glossary.Rich(_left, _engine.CabosNetworkActive
            ? string.Format(Tr("Rede montada: {0} pessoas contratadas, de {1} que a lei permite. Ocupa uma vaga da equipe."), _engine.CabosPlanned, _engine.CabosLimit)
            : Tr("Sem rede de cabos. Ela é montada pondo verba no canal Cabos eleitorais e ocupa uma vaga da equipe."));
        Ui.Button(_left, "Abrir a verba dos cabos", () => OpenSheet(Sheet.Canal, ChannelDefinition.Cabos),
            Palette.Tip(Tr("Abre o canal Cabos eleitorais."), Tr("mostra onde há cabos, a previsão por zona e o limite legal de contratações."), string.Format(Tr("nada para abrir; cada grupo de cabos custa {0} por semana."), Money(_engine.CabosGroupCost))));
    }

    /// <summary>Conselho da semana do coordenador, com o progresso de "Seguir" e o custo de "Ignorar".</summary>
    private void AdviceCard()
    {
        if (_engine.Team.Advice is not { } advice || _engine.Staff(advice.FromPersonId) is not { } coordinator)
        {
            if (_engine.StaffIn(StaffRole.Coordenador) is not null)
            {
                Glossary.Rich(_left, Tr("O conselho da semana chega toda segunda-feira."), 18, Palette.Texto3);
            }

            return;
        }

        int loyalty = StaffContent.Regras.LealdadeConselho;
        var (done, needed) = _engine.AdviceProgress();
        int marked = Agenda.Scheduled.Count(c => c.ZoneId == advice.ZoneId && c.Date >= State.Calendar.WeekStart && c.Date <= State.Calendar.WeekEnd);
        PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha);
        card.Name = "Conselho";
        var col = Ui.Column(card, 6);
        Heading(col, Tr("Conselho da semana"), 20);
        Glossary.Rich(col, advice.Text, 18, Palette.Texto, Palette.Serif);
        Glossary.Rich(col, string.Format(Tr("Para seguir: {0} compromissos em {1} até domingo. Feitos: {2}. Marcados: {3}."), needed, ZoneName(advice.ZoneId), done, marked));
        HBoxContainer row = Ui.Row(col, 8);
        string zone = advice.ZoneId;
        var follow = Ui.Button(row, "Seguir", () =>
        {
            var (date, turno) = _engine.NextFreeSlot();
            StartSchedule(date, turno, zoneId: zone);
        }, Palette.Tip(
            string.Format(Tr("Segue o conselho de {0}."), StaffLabel(coordinator)),
            string.Format(Tr("abre a marcação em {0}, no primeiro turno livre. Com {1} compromissos lá até domingo ({2} feitos, {3} marcados), a lealdade dela sobe {4} e ela lembra."), ZoneName(zone), needed, done, marked, Signed(loyalty, "0")),
            Tr("o custo de cada compromisso; o conselho pode estar errado, mais ainda com coordenação fraca.")));
        follow.Name = "SeguirConselho";
        Palette.Primary(follow);
        if (BlockReason() is { } block)
        {
            follow.Disabled = true;
            follow.TooltipText = Palette.Blocked(string.Format(Tr("{0}; resolva antes de marcar."), block), follow.TooltipText);
        }

        var ignore = Ui.Button(row, "Ignorar", () =>
        {
            string name = _engine.NameOf(coordinator.PersonId);
            if (_engine.IgnoreAdvice())
            {
                _log.Insert(0, string.Format(Tr("Você dispensou o conselho de {0}: lealdade {1} (agora {2})."), name, Signed(-loyalty, "0"), coordinator.Loyalty));
            }

            Refresh();
        }, Palette.Tip(
            Tr("Dispensa o conselho desta semana."),
            string.Format(Tr("o conselho sai do painel e conta como não seguido: lealdade de {0} {1}, e ela lembra."), _engine.NameOf(coordinator.PersonId), Signed(-loyalty, "0")),
            string.Format(Tr("{0} de lealdade; nada em dinheiro."), Signed(-loyalty, "0"))));
        ignore.Name = "IgnorarConselho";
    }

    // ------------------------------------------------------------------ ficha de quem é (ou pode ser) da equipe

    /// <summary>O que a função faz com a qualidade desta pessoa, na faixa que o jogador conhece (vem da previsão do Core).</summary>
    private List<string> RoleEffectLines(CommandForecast forecast, string roleId)
    {
        var lines = new List<string>();
        foreach (ForecastLine line in forecast.Lines.Where(l => l.Target == roleId))
        {
            string? text = line.Kind switch
            {
                StaffRole.Delegacoes => string.Format(Tr("delegações por semana: {0}"), Span(line.Min, line.Max, "0")),
                StaffRole.ErroConselho => string.Format(Tr("erro do conselho da semana na conta de cada zona: até {0}"), Span(line.Min * 100, line.Max * 100, "0") + "%"),
                StaffRole.Rendimento => string.Format(Tr("comício e material gráfico rendem {0} vezes o normal"), Span(line.Min, line.Max, "0.00")),
                StaffRole.Faixa => string.Format(Tr("a faixa das previsões fica com {0} da largura normal"), Span(line.Min * 100, line.Max * 100, "0") + "%"),
                StaffRole.ErroContabil => string.Format(Tr("chance de erro nas contas por semana: {0}"), Span(line.Min * 100, line.Max * 100, "0.#") + "%"),
                StaffRole.Exito => string.Format(Tr("chance de êxito na Justiça Eleitoral: {0} (as ações na Justiça entram numa parte seguinte do jogo)"), Span(line.Min * 100, line.Max * 100, "0") + "%"),
                StaffRole.Alcance => string.Format(Tr("o canal Redes e as gravações alcançam {0} vezes o normal"), Span(line.Min, line.Max, "0.00")),
                StaffRole.Viral => string.Format(Tr("risco de um vídeo pegar mal: {0}"), Span(line.Min * 100, line.Max * 100, "0.#") + "%"),
                _ => null,
            };
            if (text is not null)
            {
                lines.Add(text);
            }
        }

        return lines;
    }

    private string HireFailureText(CommandFailure failure, StaffMember member) => failure switch
    {
        CommandFailure.FuncaoOcupada => _engine.StaffIn(member.Role) is { } holder
            ? string.Format(Tr("a função {0} já é de {1}; para trocar, demita antes."), StaffContent.Role(member.Role).Nome, _engine.NameOf(holder.PersonId))
            : Tr("essa pessoa já está na equipe."),
        CommandFailure.SemVaga => string.Format(Tr("as {0} vagas da equipe nesta cidade estão ocupadas (a rede de cabos conta uma); falta abrir uma vaga."), _engine.TeamSlots),
        CommandFailure.SemDinheiro => string.Format(Tr("faltam {0} em caixa para a primeira semana de salário ({1})."), Money(member.Salary - State.Player.Resources.Money), Money(member.Salary)),
        CommandFailure.ForaDaCampanha => Tr("só dá para contratar durante a pré-campanha e a campanha."),
        _ => Tr("essa pessoa não está mais disponível."),
    };

    private void BuildMemberSheet(StaffMember member)
    {
        Person person = _engine.City.Person(member.PersonId)!;
        StaffRole role = StaffContent.Role(member.Role);
        string name = _engine.NameOf(member.PersonId);
        BackButton();

        HBoxContainer top = Ui.Row(_left, 12);
        Portrait(top, member.PersonId, 120);
        var head = Ui.Column(top, 4);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        NameTitle(head, name, 22);
        string what = member.Hired ? role.Papel : member.Left ? Tr("deixou a sua campanha") : string.Format(Tr("nome para a função {0}"), role.Nome);
        Glossary.Rich(head, string.Format(Tr("{0}. Pessoa fictícia."), char.ToUpper(what[0], PtBr) + what[1..]));
        if (member.Hired)
        {
            Glossary.Rich(head, string.Format(Tr(member.WeeksWorked == 1 ? "Na equipe há {0} semana." : "Na equipe há {0} semanas."), member.WeeksWorked), 18, Palette.Texto);
        }

        Glossary.Rich(_left, role.Descricao);
        ValueRow(_left, Tr("Qualidade (1 a 5)"), Span(member.QualityShownMin, member.QualityShownMax, "0"), highlight: true);
        if (member.QualityShownMin != member.QualityShownMax)
        {
            Glossary.Rich(_left, string.Format(Tr("Você só sabe a qualidade ao certo depois de {0} semanas de trabalho."), StaffContent.Regras.SemanasParaConhecer), 18, Palette.Texto3);
        }

        ValueRow(_left, Tr("Salário por semana"), Money(member.Salary));
        ValueRow(_left, Tr("Lealdade (0 a 100)"), member.Loyalty.ToString(PtBr));
        Glossary.Rich(_left, string.Format(Tr("Vínculo: {0}."), member.BondText), 18, Palette.Texto);
        Glossary.Rich(_left, string.Format(Tr("Temperamento {0}."), TemperamentName(person.Temperament)));
        foreach (Interest interest in person.Interests)
        {
            string text = interest.Kind switch
            {
                Interest.Tema => string.Format(Tr("Interesse: o tema {0}."), _engine.Context.Themes.Themes.FirstOrDefault(t => t.Id == interest.Target)?.Name ?? interest.Target),
                Interest.ZonaKind => string.Format(Tr("Interesse: {0}, onde mora."), ZoneName(interest.Target)),
                _ => string.Format(Tr("Interesse: {0}."), interest.Target),
            };
            Glossary.Rich(_left, text);
        }

        CommandForecast hire = _engine.ForecastHire(member.PersonId);
        if (member.Left)
        {
            Glossary.Rich(_left, Tr("Não volta a trabalhar com você nesta campanha."), 18, Palette.Texto);
        }
        else if (!member.Hired)
        {
            PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha);
            card.Name = "PrevisaoContratar";
            var col = Ui.Column(card, 6);
            Heading(col, Tr("Previsão de contratar"), 20);
            foreach (string line in hire.Explanation)
            {
                Glossary.Rich(col, "· " + line, 18, Palette.Texto);
            }

            foreach (string line in RoleEffectLines(hire, role.Id))
            {
                Glossary.Rich(col, "· " + char.ToUpper(line[0], PtBr) + line[1..] + ".");
            }

            Glossary.Rich(col, string.Format(Tr("Quem vai lembrar: {0} (entrou na sua campanha)."), name));
            if (hire.Blocked is { } blocked)
            {
                PanelContainer stop = Card(col, Palette.CartaoAlto, Palette.Linha, 10);
                stop.Name = "Bloqueio";
                Glossary.Rich(stop, Tr("Não dá para contratar:") + " " + HireFailureText(blocked, member), 18, Palette.Texto);
            }

            string tip = Palette.Tip(
                string.Format(Tr("Contrata {0} para a função {1}."), name, role.Nome),
                string.Join(" ", hire.Explanation),
                string.Format(Tr("{0} por semana, toda segunda-feira, enquanto ficar na equipe; ocupa uma das {1} vagas."), Money(member.Salary), _engine.TeamSlots));
            var button = Ui.Button(_left, "Contratar", () =>
            {
                var (failure, memory) = _engine.Hire(member.PersonId);
                _log.Insert(0, failure is null
                    ? string.Format(Tr("Contratado: {0}. Vai lembrar: \"{1}\"."), StaffLabel(member), memory?.Text)
                    : HireFailureText(failure.Value, member));
                Refresh();
            }, tip);
            button.Name = "Contratar";
            if (hire.Blocked is { } reason)
            {
                button.Disabled = true;
                button.TooltipText = Palette.Blocked(HireFailureText(reason, member), tip);
            }
            else
            {
                Palette.Primary(button);
            }
        }
        else
        {
            Heading(_left, Tr("O que faz por você"), 20);
            foreach (string line in RoleEffectLines(hire, role.Id))
            {
                Glossary.Rich(_left, "· " + char.ToUpper(line[0], PtBr) + line[1..] + ".");
            }

            FireControls(member, name);
        }

        Heading(_left, Tr("Lembranças de você"), 20);
        if (person.Memories.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma lembrança de você ainda."));
        }

        foreach (Memory memory in person.Memories.AsEnumerable().Reverse())
        {
            Glossary.Rich(_left, string.Format(Tr("· {0}: {1}. {2} de relação"), memory.Date.ToString("d MMM yyyy", PtBr), memory.Text, Signed(memory.Pontos, "0")));
        }
    }

    /// <summary>Demitir: estado "perigoso" do ART_GUIDE, com a previsão e a confirmação antes de valer.</summary>
    private void FireControls(StaffMember member, string name)
    {
        CommandForecast fire = _engine.ForecastFire(member.PersonId);
        string does = string.Join(" ", fire.Explanation);
        string cost = fire.Outcomes.Count > 0
            ? string.Format(PtBr, Tr("nada em dinheiro; chance de {0:P0} de contar a um rival o que sabe, com perda de imagem."), fire.Outcomes[0].Probability)
            : Tr("nada em dinheiro; a pessoa lembra da demissão e não volta nesta campanha.");
        if (_confirmFire != member.PersonId)
        {
            var open = Ui.Button(_left, "Demitir", () => { _confirmFire = member.PersonId; Refresh(); },
                Palette.Tip(string.Format(Tr("Demite {0}."), StaffLabel(member)), Tr("abre a confirmação, com a previsão. ") + does, cost));
            open.Name = "Demitir";
            Palette.Danger(open);
            return;
        }

        PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Negativo);
        card.Name = "ConfirmarDemissao";
        var col = Ui.Column(card, 6);
        Heading(col, string.Format(Tr("Demitir {0}?"), name), 20);
        foreach (string line in fire.Explanation)
        {
            Glossary.Rich(col, "· " + line, 18, Palette.Texto);
        }

        foreach (ForecastOutcome outcome in fire.Outcomes)
        {
            Glossary.Rich(col, string.Format(PtBr, Tr("· Chance de {0:P0} de isso acontecer."), outcome.Probability), 18, Palette.Texto);
        }

        Glossary.Rich(col, string.Format(Tr("Quem vai lembrar: {0}. A função {1} fica vaga e ela não volta nesta campanha."), name, StaffContent.Role(member.Role).Nome));
        VBoxContainer row = Ui.Column(col, 8);
        var confirm = Ui.Button(row, "Confirmar a demissão", () =>
        {
            string label = StaffLabel(member);
            StaffDeparture? departure = _engine.Fire(member.PersonId);
            _confirmFire = null;
            if (departure is not null)
            {
                _log.Insert(0, string.Format(Tr("Demitido: {0}. Vai lembrar: \"{1}\".{2}"), label, departure.Memory.Text,
                    departure.Leaked ? " " + string.Format(Tr("Contou o que sabia a {0}."), departure.LeakedTo is null ? Tr("um rival") : _engine.NameOf(departure.LeakedTo)) : ""));
            }

            Refresh();
        }, Palette.Tip(string.Format(Tr("Confirma a demissão de {0}."), name), does, cost));
        confirm.Name = "ConfirmarDemitir";
        Palette.Danger(confirm);
        Ui.Button(row, "Manter na equipe", () => { _confirmFire = null; Refresh(); },
            Palette.Tip(Tr("Desiste da demissão."), Tr("nada muda; a pessoa continua na função."), Tr("nada."))).Name = "ManterNaEquipe";
    }

    // ------------------------------------------------------------------ decisão da equipe sobre o mapa

    private string NatureName(string nature) => nature switch
    {
        DecisionNature.Dinheiro => Tr("dinheiro"),
        DecisionNature.Tempo => Tr("tempo"),
        DecisionNature.Relacao => Tr("relação"),
        DecisionNature.Zona => Tr("presença numa zona"),
        DecisionNature.Risco => Tr("risco"),
        _ => Tr("posição pública"),
    };

    /// <summary>
    /// Decisão da equipe (pede aumento, proposta de rival, briga, traz liderança ou doador): mesmo cartão de 720 px do
    /// pedido de liderança, com a previsão de cada opção, quem vai lembrar e, depois da escolha, "O que mudou".
    /// </summary>
    private void BuildStaffCard(Node center)
    {
        var card = center.Add(new PanelContainer { CustomMinimumSize = new Vector2(720, 0), Name = "CartaoEquipe" });
        card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 12, 20, Palette.Atencao, 2));
        _staffCard = card;
        var column = Ui.Column(card, 5);
        if (_staffResult is not null)
        {
            Title(column, _staffResultTitle ?? "");
            Heading(column, Tr("O que mudou"), 20);
            foreach (string line in _staffResult)
            {
                Glossary.Rich(column, "· " + line, 18, Palette.Texto);
            }

            var proceed = Ui.Button(column, "Continuar", () =>
            {
                _staffResult = null;
                _staffResultTitle = null;
                Refresh();
            }, Palette.Tip(Tr("Fecha o cartão da decisão."), Tr("volta ao mapa; o jogo continua pausado até você soltar o tempo."), Tr("nada.")));
            proceed.Name = "Continuar";
            Palette.Primary(proceed);
            return;
        }

        StaffDecision decision = _engine.Team.Pending[0];
        StaffEventDefinition definition = StaffContent.Event(decision.EventId);
        StaffMember member = _engine.Staff(decision.PersonId)!;
        StaffMember? other = decision.OtherPersonId is null ? null : _engine.Staff(decision.OtherPersonId);
        HBoxContainer top = Ui.Row(column, 16);
        Portrait(top, decision.PersonId, 120);
        var head = Ui.Column(top, 6);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Title(head, definition.Titulo);
        Glossary.Rich(head, decision.Text, 22, Palette.Texto, Palette.Serif);

        foreach (StaffEventOption option in definition.Opcoes)
        {
            CommandForecast forecast = _engine.ForecastStaffDecision(decision.Id, option.Id);
            string label = _engine.DecisionText(decision, option.Rotulo, option);
            // A primeira linha da previsão do Core repete o rótulo da opção, que já está no botão.
            string text = string.Join(" ", forecast.Explanation);
            if (text.StartsWith(label + ": ", StringComparison.Ordinal))
            {
                text = char.ToUpper(text[label.Length + 2], PtBr) + text[(label.Length + 3)..];
            }

            var who = new List<string>();
            if (option.Efeitos.Lembranca is not null)
            {
                who.Add(_engine.NameOf(member.PersonId));
            }

            if (other is not null && option.Efeitos.LembrancaOutro is not null)
            {
                who.Add(_engine.NameOf(other.PersonId));
            }

            string remember = who.Count == 0 ? Tr("Quem vai lembrar: ninguém.") : string.Format(Tr("Quem vai lembrar: {0}."), string.Join(Tr(" e "), who));
            string trade = string.Format(Tr("Você ganha em {0} e paga em {1}."), string.Join(", ", option.Ganho.Select(NatureName)), string.Join(", ", option.Custo.Select(NatureName)));
            var button = Ui.Button(column, label, () => AnswerStaff(decision, option), Palette.Tip(label + ".", $"{text} {remember}", trade));
            button.Name = "OpcaoEquipe_" + option.Id;
            button.Alignment = HorizontalAlignment.Left;
            Glossary.Rich(column, text + "\n" + remember);
        }
    }

    /// <summary>Responde à decisão da equipe e monta a lista "O que mudou" com o que o Core devolveu e o que entrou no Livro.</summary>
    private void AnswerStaff(StaffDecision decision, StaffEventOption option)
    {
        Ledger ledger = State.Ledger;
        int promises = ledger.Promises.Count, debts = ledger.Debts.Count;
        StaffMember member = _engine.Staff(decision.PersonId)!;
        StaffMember? other = decision.OtherPersonId is null ? null : _engine.Staff(decision.OtherPersonId);
        string name = _engine.NameOf(member.PersonId);
        string label = _engine.DecisionText(decision, option.Rotulo, option);
        int memories = _engine.City.Person(member.PersonId)!.Memories.Count;
        StaffDecisionResult result = _engine.AnswerStaffDecision(decision.Id, option.Id);

        var lines = new List<string> { result.Text };
        if (result.Left)
        {
            lines.Add(string.Format(Tr("{0} deixou a sua campanha; a função {1} ficou vaga."), name, StaffContent.Role(member.Role).Nome));
        }
        else if (result.Loyalty != 0)
        {
            lines.Add(string.Format(Tr("Lealdade de {0}: {1} (agora {2})."), name, Signed(result.Loyalty, "0"), member.Loyalty));
        }

        Person person = _engine.City.Person(member.PersonId)!;
        if (person.Memories.Count > memories)
        {
            lines.Add(string.Format(Tr("{0} vai lembrar: \"{1}\"."), name, person.Memories[^1].Text));
        }

        if (other is not null)
        {
            string otherName = _engine.NameOf(other.PersonId);
            lines.Add(result.OtherLeft
                ? string.Format(Tr("{0} deixou a sua campanha; a função {1} ficou vaga."), otherName, StaffContent.Role(other.Role).Nome)
                : string.Format(Tr("Lealdade de {0}: {1} (agora {2})."), otherName, Signed(result.LoyaltyOther, "0"), other.Loyalty));
        }

        if (result.Money != 0)
        {
            lines.Add(string.Format(Tr("Caixa: {0}."), SignedMoney(result.Money)));
        }

        foreach (Promise p in ledger.Promises.Skip(promises))
        {
            lines.Add(string.Format(Tr("Promessa no Livro de compromissos: {0}. Prazo: {1}."), p.Text, p.Deadline.ToString("dd/MM/yyyy", PtBr)));
        }

        foreach (Debt d in ledger.Debts.Skip(debts))
        {
            lines.Add(string.Format(Tr("Dívida no Livro de compromissos: {0}."), d.Text));
        }

        _staffResultTitle = label;
        _staffResult = lines;
        _staffHidden = false;
        _log.Insert(0, string.Format(Tr("Decisão da equipe: {0}."), label));
        Refresh();
    }
}
