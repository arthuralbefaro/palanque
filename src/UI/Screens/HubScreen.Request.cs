using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.People;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Pedido de liderança (GDD 17.3 e 17.10): decisão imediata num cartão de 720 px sobre o mapa. Cada resposta mostra a
/// previsão em até duas linhas e quem vai lembrar; depois da escolha, o cartão vira a lista "O que mudou".
/// </summary>
public partial class HubScreen
{
    private bool _requestHidden;
    private PanelContainer? _requestCard;
    private string? _requestResultTitle;
    private List<string>? _requestResult;

    /// <summary>Cartão do pedido de liderança (ou do resultado dele) aberto sobre o mapa.</summary>
    public bool RequestOpen => _requestCard is not null && IsInstanceValid(_requestCard);

    private void BuildRequestCard(Node center)
    {
        var card = center.Add(new PanelContainer { CustomMinimumSize = new Vector2(720, 0), Name = "CartaoPedido" });
        card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 12, 20, Palette.Atencao, 2));
        _requestCard = card;
        var column = Ui.Column(card, 5);
        if (_requestResult is not null)
        {
            Title(column, _requestResultTitle ?? "");
            Heading(column, Tr("O que mudou"), 20);
            foreach (string line in _requestResult)
            {
                Glossary.Rich(column, "· " + line, 18, Palette.Texto);
            }

            var proceed = Ui.Button(column, "Continuar", () =>
            {
                _requestResult = null;
                _requestResultTitle = null;
                Refresh();
            }, Palette.Tip(Tr("Fecha o cartão da decisão."), Tr("volta ao mapa; o jogo continua pausado até você soltar o tempo."), Tr("nada.")));
            proceed.Name = "Continuar";
            Palette.Primary(proceed);
            return;
        }

        Leader leader = _engine.City.Leader(Agenda.PendingRequest!)!;
        LeaderRequestDefinition request = LeaderContent.Request(leader.RequestId);
        LeaderRules rules = LeaderContent.Regras;
        string name = _engine.NameOf(leader.PersonId);
        bool money = request.Kind == LeaderRequestDefinition.Dinheiro;

        HBoxContainer top = Ui.Row(column, 16);
        Portrait(top, leader.PersonId, 120);
        var head = Ui.Column(top, 6);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Title(head, string.Format(Tr("{0} faz um pedido"), name));
        string role = LeaderContent.Type(leader.Type).Papel;
        Glossary.Rich(head, string.Format(Tr("{0}, de {1}, quer isto para ficar do seu lado: {2}."), char.ToUpper(role[0], PtBr) + role[1..], ZoneName(leader.Zones[0]), LowerFirst(leader.RequestText)), 22, Palette.Texto, Palette.Serif);

        // Cada resposta: a previsão numa linha e quem vai lembrar na outra (ART_GUIDE: até 2 linhas); o resto vai no tooltip.
        bool aligns = _engine.WouldAlignWithPlayer(leader, rules.PontosPromessa);
        string side = aligns ? Tr("passa a apoiar você") : Tr("ainda não apoia você");
        string sideDetail = aligns ? "" : " " + string.Format(PtBr, Tr("Para apoiar, ela quer relação de {0} ou mais (ficaria em {1:0}) e nenhum rival com mais favor."), leader.Price, _engine.RelationOf(leader.PersonId) + rules.PontosPromessa);
        string accept = money
            ? string.Format(Tr("{0} sem registro: esqueleto no Livro de compromissos, e ela sabe."), SignedMoney(-_engine.RequestMoneyCost()))
            : string.Format(Tr("Promessa no Livro de compromissos, com prazo até {0}."), _engine.PromiseDeadline(request).ToString("dd/MM/yyyy", PtBr));
        string acceptWho = string.Format(Tr("Quem vai lembrar: {0} (relação {1}); {2}."), name, Signed(rules.PontosPromessa, "0"), side);
        string acceptMore = (money
            ? string.Format(Tr("É gasto irregular: o dinheiro sai do caixa fora da prestação de contas; o esqueleto tem gravidade {0} e pode vir a público."), rules.GravidadeDinheiro)
            : Tr(request.PrazoSemanas <= 0 ? "O prazo vence antes da eleição." : "O prazo corre a partir da posse: a promessa é cobrada no mandato.")) + sideDetail;
        Choice(column, "Aceitar", LeaderAnswer.Aceitar, leader, accept, acceptWho, acceptMore, money ? Money(_engine.RequestMoneyCost()) + Tr(", fora da prestação de contas.") : Tr("nada agora; a promessa é cobrada no prazo."));

        Choice(column, "Negociar", LeaderAnswer.Negociar, leader,
            string.Format(PtBr, Tr("Chance de {0:P0} (Articulação): apoio por uma dívida, sem promessa."), _engine.NegotiationChance),
            string.Format(Tr("Quem vai lembrar: {0} (relação {1} se der certo; {2} se não)."), name, Signed(rules.PontosNegociacao, "0"), Signed(rules.PontosNegociacaoFalha, "0")),
            Tr("A dívida fica no Livro de compromissos: você passa a dever um favor a ela."),
            Tr("nada em dinheiro; o risco é a conversa não andar."));

        Choice(column, "Ouvir sem se comprometer", LeaderAnswer.NaoSeComprometer, leader,
            Tr("Nada entra no Livro de compromissos; o pedido continua de pé."),
            string.Format(Tr("Quem vai lembrar: {0} (relação {1})."), name, Signed(rules.PontosVisitaSemCompromisso, "0")),
            Tr("Numa próxima visita ela pergunta de novo."),
            Tr("nada; ela continua sem lado por sua causa."));

        Choice(column, "Recusar", LeaderAnswer.Recusar, leader,
            Tr("Nada entra no Livro de compromissos; ela fica livre para um rival."),
            string.Format(Tr("Quem vai lembrar: {0} (relação {1})."), name, Signed(rules.PontosRecusa, "0")),
            Tr("O pedido continua de pé: numa próxima visita ela pergunta de novo."),
            Tr("nada em dinheiro; custa relação."));
    }

    private static string LowerFirst(string text) => text.Length == 0 ? text : char.ToLower(text[0], PtBr) + text[1..];

    private static void Title(Node parent, string text)
    {
        RichTextLabel title = Glossary.Plain(parent, text, 32, Palette.Texto, Palette.Weight(Palette.Oswald, 600));
        title.Name = "TituloDecisao";
    }

    private void Choice(Node parent, string label, LeaderAnswer answer, Leader leader, string forecast, string who, string more, string cost)
    {
        var button = Ui.Button(parent, label, () => Answer(answer, leader), Palette.Tip(Tr(label) + ".", $"{forecast} {who} {more}", cost));
        button.Name = "Resposta_" + answer;
        button.Alignment = HorizontalAlignment.Left;
        Glossary.Rich(parent, forecast + "\n" + who);
    }

    /// <summary>Responde ao pedido e monta a lista "O que mudou" comparando o Livro e o feed de antes e de depois.</summary>
    private void Answer(LeaderAnswer answer, Leader leader)
    {
        Ledger ledger = State.Ledger;
        int promises = ledger.Promises.Count, debts = ledger.Debts.Count, skeletons = ledger.Skeletons.Count;
        decimal money = State.Player.Resources.Money;
        string? sideBefore = leader.AlignedWith;
        string name = _engine.NameOf(leader.PersonId);
        Memory memory = _engine.AnswerLeader(answer);

        var lines = new List<string>
        {
            string.Format(Tr("{0} vai lembrar: \"{1}\" ({2} de relação; agora {3})."), name, memory.Text, Signed(memory.Pontos, "0"), Signed(_engine.RelationOf(leader.PersonId), "0")),
        };
        foreach (Promise p in ledger.Promises.Skip(promises))
        {
            lines.Add(string.Format(Tr("Promessa no Livro de compromissos: {0}. Prazo: {1}."), p.Text, p.Deadline.ToString("dd/MM/yyyy", PtBr)));
        }

        foreach (Debt d in ledger.Debts.Skip(debts))
        {
            lines.Add(string.Format(Tr("Dívida no Livro de compromissos: {0}."), d.Text));
        }

        foreach (LedgerSkeleton s in ledger.Skeletons.Skip(skeletons))
        {
            lines.Add(string.Format(Tr("Esqueleto no Livro de compromissos (gravidade {0}): {1}. Quem sabe: {2}."), s.Severity, s.Text, string.Join(", ", s.KnownBy.Select(_engine.NameOf))));
        }

        if (State.Player.Resources.Money != money)
        {
            lines.Add(string.Format(Tr("Caixa: {0}."), SignedMoney(State.Player.Resources.Money - money)));
        }

        if (leader.AlignedWith != sideBefore)
        {
            lines.Add(string.Format(Tr("{0} agora {1}."), name, SideText(leader)));
            if (leader.AlignedWith == RaceSetup.PlayerId)
            {
                foreach (Leader rival in leader.Rivals.Select(id => _engine.City.Leader(id)).OfType<Leader>())
                {
                    lines.Add(string.Format(Tr("{0} disputa o mesmo público e esfriou com você ({1} de relação)."), rival.Known ? LeaderLabel(rival) : Tr("Uma liderança que você ainda não conhece"), Signed(LeaderContent.Regras.PontosRivalidade, "0")));
                }
            }
        }
        else
        {
            lines.Add(string.Format(Tr("{0} continua como estava: {1}."), name, SideText(leader)));
        }

        _requestResultTitle = answer switch
        {
            LeaderAnswer.Aceitar => string.Format(Tr("Você aceitou o pedido de {0}"), name),
            LeaderAnswer.Negociar => memory.Pontos > 0 ? string.Format(Tr("Acordo fechado com {0}"), name) : string.Format(Tr("A conversa com {0} não andou"), name),
            LeaderAnswer.NaoSeComprometer => string.Format(Tr("Você ouviu {0} e não se comprometeu"), name),
            _ => string.Format(Tr("Você recusou o pedido de {0}"), name),
        };
        _requestResult = lines;
        _requestHidden = false;
        _log.Insert(0, _requestResultTitle + ".");
        Refresh();
    }
}
