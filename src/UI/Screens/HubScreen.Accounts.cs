using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.People;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Dinheiro e contas (GDD 17.12): o bloco Contas da área Finanças, o painel inteiro (limite de gastos, fontes,
/// doadores, risco em faixa com os fatores, esqueletos com "quem sabe" e a chance de vir à tona, prazos de
/// prestação) e a oferta de doação como decisão com previsão. Só lê o Core e chama os comandos dele.
/// </summary>
public partial class HubScreen
{
    private FinanceCatalog FinanceContent => _engine.Context.Campaign.Finance;

    private CampaignFinanceLawConfig FinanceLaw => _engine.Context.FinanceLaw;

    private string SourceName(string source) => source switch
    {
        FinanceCatalog.Fundo => Tr("Fundo eleitoral, pela direção do partido"),
        FinanceCatalog.PessoaFisica => Tr("Doações de pessoa física"),
        FinanceCatalog.Proprios => Tr("Recursos próprios"),
        FinanceCatalog.Coletivo => Tr("Financiamento coletivo"),
        FinanceCatalog.PorFora => Tr("Dinheiro \"por fora\" (sem registro)"),
        FinanceCatalog.Jantar => Tr("Jantares de arrecadação"),
        _ => Tr("Outras entradas"),
    };

    /// <summary>A barra usa a cor de ação; passou do limite, a de risco (ART_GUIDE, paleta).</summary>
    private static Color LimitColor(double used) => used > 1 ? Palette.Negativo : Palette.Acao;

    /// <summary>Barra do limite de gastos, com o quanto já foi e o que resta.</summary>
    private void LimitBar(Node parent, AccountsView a)
    {
        var col = Ui.Column(parent, 4);
        col.Name = "BarraLimite";
        HBoxContainer row = Ui.Row(col, 8);
        Glossary.Rich(row, Tr("Limite de gastos"), 18, Palette.Texto);
        Label value = Ui.Value(row, string.Format(PtBr, "{0:P0}", a.LimitUsed), 18);
        value.AddThemeColorOverride("font_color", LimitColor(a.LimitUsed));
        Bar(col, a.LimitUsed, LimitColor(a.LimitUsed));
        Glossary.Rich(col, string.Format(Tr("Gasto {0} de {1}. {2}"), Money(a.Spent), Money(a.Limit),
            a.Spent > a.Limit ? string.Format(Tr("Passou do limite em {0}."), Money(a.Spent - a.Limit)) : string.Format(Tr("Restam {0}."), Money(a.Limit - a.Spent))));
    }

    /// <summary>Risco das contas em faixa (0 a 100), com a cor e o que a faixa causa no julgamento.</summary>
    private void RiskMeter(Node parent, AccountsView a)
    {
        FinanceRules rules = FinanceContent.Regras;
        var col = Ui.Column(parent, 4);
        col.Name = "RiscoDasContas";
        HBoxContainer row = Ui.Row(col, 8);
        Glossary.Rich(row, Tr("Risco das contas (0 a 100)"), 18, Palette.Texto);
        Label value = Ui.Value(row, a.Risk.ToString(PtBr), 18);
        value.AddThemeColorOverride("font_color", AccountsCasePanel.RiskColor(a.Risk, rules));
        Bar(col, a.Risk / 100.0, AccountsCasePanel.RiskColor(a.Risk, rules));
        Glossary.Rich(col, string.Format(Tr("Faixa: {0}. A partir de {1}, ressalvas; a partir de {2}, desaprovação."), AccountsCasePanel.RiskBand(a.Risk, rules), rules.RiscoRessalvas, rules.RiscoDesaprovacao));
    }

    private void OfferRow(Node parent, DonorView donor, DonorOffer offer)
    {
        PanelContainer card = ClickCard(parent, () => OpenAreaSheet(Sheet.Oferta, offer.Id),
            Palette.Tip(string.Format(Tr("Oferta de doação de {0} (doa como pessoa física)."), donor.Name), Tr("abre a decisão: aceitar, recusar ou, se for o caso, aceitar \"por fora\", cada resposta com a previsão e quem fica sabendo."), Tr("nada para abrir.")),
            "Oferta_" + offer.Id);
        var col = Ui.Column(card, 2);
        Glossary.Plain(col, string.Format(Tr("{0} oferece {1}{2}"), donor.Name, Money(offer.Amount), offer.PorFora ? " " + Tr("\"por fora\"") : ""), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
        Glossary.Rich(col, string.Format(Tr("Responda até {0}. Sem resposta, a oferta cai e a pessoa lembra."), offer.Deadline.ToString("dddd, d/MM", PtBr)));
    }

    // ------------------------------------------------------------------ bloco da área Finanças

    private void BuildAccountsBlock()
    {
        AccountsView a = _engine.Accounts();
        BlockHeader(Tr("Contas"), "VerContas", () => OpenAreaSheet(Sheet.Contas, null),
            Palette.Tip(Tr("Abre o painel Contas."), Tr("mostra o limite de gastos, de onde veio o dinheiro, os doadores, o risco das contas com os fatores, os esqueletos e os prazos de prestação."), Tr("nada.")));
        LimitBar(_left, a);
        RiskMeter(_left, a);
        foreach (DonorView donor in _engine.Donors().Where(d => d.Offer is not null))
        {
            OfferRow(_left, donor, donor.Offer!);
        }
    }

    private void BuildFinances()
    {
        Glossary.Rich(_left, string.Format(Tr("Caixa: {0}."), Money(State.Player.Resources.Money)), 18, Palette.Texto);
        BuildAccountsBlock();
        BuildPollsBlock();
    }

    // ------------------------------------------------------------------ painel Contas

    private void BuildAccountsSheet()
    {
        AccountsView a = _engine.Accounts();
        int year = Campaign.ElectionDate.Year;
        BackButton();
        Heading(_left, Tr("Contas"));
        FirstTimeHint("contas");
        LimitBar(_left, a);
        Glossary.Rich(_left, string.Format(Tr("O limite é por cargo e por município ({0}); gasto declarado acima dele vira irregularidade.{1}"), FinanceLaw.ArtigoLimiteDeGastos,
            a.LimitIsGameParameter ? " " + Tr("O valor desta cidade é uma conta do jogo pelo número de eleitores: a tabela oficial ainda não foi incorporada.") : ""), 18, Palette.Texto3);

        // Risco e fatores.
        RiskMeter(_left, a);
        Heading(_left, Tr("O que pesa no risco"), 20);
        if (a.Irregularities.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nada: as contas estão limpas. Dinheiro \"por fora\", doação acima do teto, gasto acima do limite, recursos próprios além do permitido e erro contábil somam risco."));
        }

        foreach (IrregularityView i in a.Irregularities)
        {
            IrregularityCard(i);
        }

        // De onde veio o dinheiro.
        Heading(_left, Tr("De onde veio o dinheiro"), 20);
        ValueRow(_left, Tr("Arrecadado"), Money(a.Raised), highlight: true);
        foreach (var (source, amount) in a.BySource.Where(s => s.Value > 0).OrderByDescending(s => s.Value))
        {
            ValueRow(_left, SourceName(source), Money(amount), link: true);
        }

        PanelContainer own = Card(_left, Palette.Cartao, a.OwnResources > a.OwnResourcesLimit ? Palette.Negativo : Palette.Linha, 10);
        own.Name = "RecursosProprios";
        Glossary.Rich(own, string.Format(PtBr, Tr("Recursos próprios: {0} de um teto de {1}, que é {2:P0} do limite de gastos ({3}). É o que você gastou além do que arrecadou."),
            Money(a.OwnResources), Money(a.OwnResourcesLimit), FinanceLaw.RecursosPropriosFracaoDoLimite, FinanceLaw.ArtigoRecursosProprios), 18, Palette.Texto);

        FundView fund = _engine.FundQuota();
        PanelContainer fundCard = Card(_left, Palette.Cartao, Palette.Linha, 10);
        fundCard.Name = "FundoDoPartido";
        var fundCol = Ui.Column(fundCard, 4);
        Glossary.Rich(fundCol, fund.Paid
            ? Tr("Fundo eleitoral: a cota desta campanha já foi paga pela direção do partido.")
            : string.Format(Tr("Fundo eleitoral: a direção do partido paga a cota no começo da campanha oficial ({0})."), fund.PaidOn.ToString("dd/MM", PtBr)), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
        foreach (string line in fund.Explanation)
        {
            Glossary.Rich(fundCol, line, 18, Palette.Texto2);
        }

        Glossary.Rich(fundCol, Tr("Quem recebe a cota fica devendo fidelidade à direção: a dívida entra no Livro de compromissos."), 18, Palette.Texto3);
        if (fund.BossId is { } boss)
        {
            PersonLink(fundCol, boss, string.Format(Tr("Quem decide: {0}"), fund.BossName));
        }

        DateOnly crowdOpens = FinanceLaw.FinanciamentoColetivoAbre(year);
        PanelContainer crowd = Card(_left, Palette.Cartao, Palette.Linha, 10);
        crowd.Name = "FinanciamentoColetivo";
        string crowdState = _engine.Today < crowdOpens
            ? string.Format(Tr("abre em {0}"), crowdOpens.ToString("d 'de' MMMM", PtBr))
            : _engine.Finance.CrowdReleased
                ? Tr("aberto; o que entra vai direto para o caixa")
                : string.Format(Tr("aberto desde {0}; {1} retidos até o registro da candidatura ({2})"), crowdOpens.ToString("d 'de' MMMM", PtBr), Money(_engine.Finance.CrowdHeld), FinanceLaw.Registro(year).ToString("dd/MM", PtBr));
        Glossary.Rich(crowd, string.Format(Tr("Financiamento coletivo: {0}. Rende pouco e cresce com a sua presença nas redes ({1})."), crowdState, FinanceLaw.ArtigoFinanciamentoColetivo), 18, Palette.Texto);

        // Doadores.
        var donors = _engine.Donors();
        Heading(_left, string.Format(Tr("Doadores ({0})"), donors.Count), 20);
        Glossary.Rich(_left, string.Format(PtBr, Tr("Pessoa física doa até {0:P0} dos rendimentos brutos do ano anterior ({1}). Empresa não doa: oferta de empresário é doação pessoal dentro do teto ou é \"por fora\"."),
            FinanceLaw.DoacaoPessoaFisicaFracaoDosRendimentos, FinanceLaw.ArtigoDoacaoPessoaFisica), 18, Palette.Texto3);
        foreach (DonorView donor in donors.OrderByDescending(d => d.Offer is not null).ThenByDescending(d => d.Relation))
        {
            if (donor.Offer is { } offer)
            {
                OfferRow(_left, donor, offer);
            }

            DonorRow(donor);
        }

        // Esqueletos que não são das contas.
        var listed = a.Irregularities.Select(i => i.SkeletonId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var others = State.Ledger.Skeletons.Where(s => !listed.Contains(s.Id)).ToList();
        if (others.Count > 0)
        {
            Heading(_left, Tr("Outros esqueletos"), 20);
            foreach (LedgerSkeleton s in others)
            {
                PanelContainer card = Card(_left, Palette.Cartao, s.Exposed ? Palette.Linha : Palette.Negativo, 10);
                card.Name = "Esqueleto_" + s.Id;
                var col = Ui.Column(card, 4);
                Glossary.Rich(col, string.Format(Tr("{0} (gravidade {1} de 3)"), s.Text, s.Severity), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
                SkeletonLines(col, s);
            }
        }

        // Prazos.
        Heading(_left, Tr("Prazos da prestação de contas"), 20);
        Glossary.Rich(_left, string.Format(Tr("· Toda doação em dinheiro é divulgada em até {0} horas do recebimento."), FinanceLaw.DivulgacaoDeDoacaoHoras), 18, Palette.Texto);
        Glossary.Rich(_left, string.Format(Tr("· Relatório parcial em {0}: {1}."), a.PartialReport.ToString("d 'de' MMMM", PtBr), _engine.Finance.PartialDone ? Tr("entregue") : Tr("a entregar; o tesoureiro cuida disso")), 18, Palette.Texto);
        Glossary.Rich(_left, string.Format(Tr("· Prestação final até {0}: {1} dias depois da eleição. Depois vem o julgamento: aprovadas, aprovadas com ressalvas ou desaprovadas."), a.FinalReport.ToString("dd/MM/yyyy", PtBr), FinanceLaw.PrestacaoFinalDiasDepoisDaEleicao), 18, Palette.Texto);
        Glossary.Rich(_left, string.Format(Tr("· Com irregularidade grave e denúncia, partido ou coligação pode pedir a cassação do diploma em até {0} dias da diplomação."), FinanceLaw.RepresentacaoDiasDaDiplomacao), 18, Palette.Texto);
        Glossary.Rich(_left, string.Format(Tr("Base legal: {0}; {1}."), FinanceLaw.ArtigoPrestacaoDeContas, FinanceLaw.ArtigoRepresentacao), 18, Palette.Texto3);

        if (State.Accounts is { } previous)
        {
            Heading(_left, Tr("Eleição anterior"), 20);
            AccountsCasePanel.Build(_left, previous, FinanceContent, FinanceLaw);
        }
    }

    /// <summary>Quem sabe do esqueleto, quem entre eles pode falar e a chance de vir à tona nesta semana.</summary>
    private void SkeletonLines(Node col, LedgerSkeleton s)
    {
        var hostile = _engine.HostileKnowers(s);
        Glossary.Rich(col, s.KnownBy.Count == 0
            ? Tr("Quem sabe: ninguém além de você.")
            : string.Format(Tr("Quem sabe: {0}."), string.Join(", ", s.KnownBy.Select(PersonLabel))), 18, Palette.Texto).Name = "QuemSabe";
        if (s.Exposed)
        {
            Glossary.Rich(col, Tr("Já veio à tona: a cidade sabe."), 18, Palette.Texto2);
            return;
        }

        Glossary.Rich(col, hostile.Count == 0
            ? Tr("Ninguém que sabe está contra você hoje: não deve vir à tona. Relação ou lealdade baixa muda isso.")
            : string.Format(PtBr, Tr("Podem falar (relação ou lealdade baixa): {0}. Chance de vir à tona nesta semana: {1:P1}."), string.Join(", ", hostile.Select(_engine.NameOf)), _engine.SurfaceChance(s)), 18, hostile.Count == 0 ? Palette.Texto2 : Palette.Texto).Name = "ChanceDeVirATona";
    }

    private void IrregularityCard(IrregularityView i)
    {
        PanelContainer card = Card(_left, Palette.Cartao, Palette.Negativo, 10);
        card.Name = "Irregularidade_" + (i.SkeletonId ?? i.Type);
        var col = Ui.Column(card, 4);
        string kind = FinanceContent.Texto("irregularidade_" + i.Type);
        Glossary.Rich(col, string.Format(PtBr, Tr("{0}{1} · {2} no risco"), kind, i.Amount > 0 ? ": " + Money(i.Amount) : "", Signed(i.Risk, "0")), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
        if (i.Text.Length > 0 && i.Text != kind)
        {
            Glossary.Rich(col, i.Text + ".");
        }

        if (i.SkeletonId is not null && State.Ledger.Skeletons.FirstOrDefault(s => s.Id == i.SkeletonId) is { } skeleton)
        {
            SkeletonLines(col, skeleton);
        }
        else
        {
            Glossary.Rich(col, Tr("Está na própria conta: quem julga as contas vê."), 18, Palette.Texto2);
        }
    }

    private void DonorRow(DonorView d)
    {
        PanelContainer card = ClickCard(_left, () => OpenAreaSheet(Sheet.Gente, d.Donor.PersonId),
            Palette.Tip(string.Format(Tr("{0} (doa como pessoa física)."), d.Name), Tr("abre a ficha: relação, o que pede em troca, o teto legal e as lembranças."), Tr("nada.")),
            "Doador_" + d.Donor.PersonId);
        HBoxContainer row = Ui.Row(card, 10);
        PortraitOf(row, d.Donor.PersonId, 56);
        var col = Ui.Column(row, 2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Glossary.Plain(col, string.Format(Tr("{0} (doa como pessoa física)"), d.Name), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, string.Format(Tr("relação {0} · teto legal {1} · já doou {2}"), Signed(d.Relation, "0"), Money(d.LegalCap), Money(d.Donor.Donated + d.Donor.OffBooks)));
    }

    /// <summary>Bloco de doador na ficha da pessoa: teto legal, o que já doou, o que pede e a oferta em aberto.</summary>
    private void DonorBlock(DonorView d)
    {
        Heading(_left, Tr("Como doa"), 20);
        ValueRow(_left, Tr("Teto legal de doação neste ano"), Money(d.LegalCap), highlight: true);
        ValueRow(_left, Tr("Já doou com recibo"), Money(d.Donor.Donated));
        if (d.Donor.OffBooks > 0)
        {
            ValueRow(_left, Tr("Já deu \"por fora\""), Money(d.Donor.OffBooks));
        }

        ValueRow(_left, Tr("Ainda pode doar com recibo"), Money(d.CapLeft));
        Glossary.Rich(_left, string.Format(PtBr, Tr("O teto é {0:P0} dos rendimentos brutos do ano anterior ({1})."), FinanceLaw.DoacaoPessoaFisicaFracaoDosRendimentos, FinanceLaw.ArtigoDoacaoPessoaFisica), 18, Palette.Texto3);
        Glossary.Rich(_left, string.Format(Tr("Em troca: {0}. Aceitar a doação grava isso como dívida no Livro de compromissos."), d.RequestText), 18, Palette.Texto);
        if (d.Offer is { } offer)
        {
            OfferRow(_left, d, offer);
        }
    }

    // ------------------------------------------------------------------ oferta de doação

    private string AnswerLabel(DonationAnswer answer, DonorOffer offer) => answer switch
    {
        DonationAnswer.Oficial => offer.PorFora ? Tr("Pedir que doe com recibo, dentro do teto") : Tr("Aceitar com recibo, até o teto legal"),
        DonationAnswer.OficialIntegral => Tr("Aceitar o valor inteiro com recibo, acima do teto"),
        DonationAnswer.PorFora => Tr("Aceitar \"por fora\""),
        _ => Tr("Recusar"),
    };

    private string DonationFailureText(CommandFailure failure) => failure switch
    {
        CommandFailure.LimiteLegal => Tr("esta pessoa já chegou ao teto legal de doação deste ano; com recibo não cabe mais nada."),
        CommandFailure.PrazoVencido => Tr("o prazo da oferta venceu."),
        _ => Tr("esta resposta não cabe nesta oferta."),
    };

    private void BuildOfferSheet(string offerId)
    {
        BackButton();
        Heading(_left, Tr("Oferta de doação"));
        DonorView? donor = _engine.Donors().FirstOrDefault(d => d.Offer?.Id == offerId);
        if (donor?.Offer is not { } offer)
        {
            Glossary.Rich(_left, Tr("Esta oferta não está mais de pé."));
            return;
        }

        HBoxContainer top = Ui.Row(_left, 12);
        PortraitOf(top, donor.Donor.PersonId, 96);
        var head = Ui.Column(top, 4);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        NameTitle(head, donor.Name, 22);
        Glossary.Rich(head, string.Format(Tr("Doa como pessoa física. Relação com você: {0}. Pessoa fictícia."), Signed(donor.Relation, "0")));
        PersonLink(_left, donor.Donor.PersonId, string.Format(Tr("Ficha de {0}"), donor.Name));

        PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Linha, 10);
        card.Name = "TextoOferta";
        var col = Ui.Column(card, 4);
        Glossary.Rich(col, string.Format(Tr(offer.PorFora ? "Oferece {0} \"por fora\", sem recibo." : "Oferece {0} para a campanha, com recibo."), Money(offer.Amount)), 20, Palette.Texto, Palette.Serif);
        Glossary.Rich(col, string.Format(Tr("Em troca: {0}."), donor.RequestText), 18, Palette.Texto);
        Glossary.Rich(col, string.Format(Tr("Responda até {0}; o tempo não para por causa disso. Sem resposta, a oferta cai e a pessoa lembra."), offer.Deadline.ToString("dddd, d/MM", PtBr)));
        Glossary.Rich(col, string.Format(Tr("Teto legal de doação de quem oferece: {0}; ainda livre: {1}."), Money(donor.LegalCap), Money(donor.CapLeft)), 18, Palette.Texto2);

        AccountsView accounts = _engine.Accounts();
        foreach (DonationAnswer answer in new[] { DonationAnswer.Oficial, DonationAnswer.OficialIntegral, DonationAnswer.PorFora, DonationAnswer.Recusar })
        {
            CommandForecast forecast = _engine.ForecastDonation(offerId, answer);
            if (forecast.Blocked == CommandFailure.ValorInvalido)
            {
                // A resposta não existe nesta oferta (não há "por fora" numa oferta com recibo, nem valor acima do teto).
                continue;
            }

            string label = AnswerLabel(answer, offer);
            DecisionTrade trade = _engine.DonationTrade(answer);
            double riskDelta = forecast.Line("risk")?.Max ?? 0;
            var extra = new List<string>
            {
                string.Format(Tr("Risco das contas: de {0} para {1} ({2})."), accounts.Risk, accounts.Risk + (int)Math.Round(riskDelta), AccountsCasePanel.RiskBand(accounts.Risk + (int)Math.Round(riskDelta), FinanceContent.Regras)),
                TradeText(trade),
                string.Format(Tr("Quem vai lembrar: {0}."), donor.Name),
            };
            VBoxContainer body = CommandForecastCard(_left, "Doacao_" + answer, label, forecast, extra: extra);
            string tip = Palette.Tip(label + ".", string.Join(" ", forecast.Explanation) + " " + extra[0], TradeText(trade));
            var button = Ui.Button(body, label, () => AnswerOffer(offer, donor, answer, label), tip);
            button.Name = "Responder_" + answer;
            button.ClipText = true;
            button.Alignment = HorizontalAlignment.Left;
            if (answer == DonationAnswer.PorFora || answer == DonationAnswer.OficialIntegral)
            {
                Palette.Danger(button);
            }

            if (forecast.Blocked is { } blocked)
            {
                button.Disabled = true;
                button.TooltipText = Palette.Blocked(DonationFailureText(blocked), tip);
                BlockCard(body, Tr("Não dá:") + " " + DonationFailureText(blocked));
            }
        }
    }

    private void AnswerOffer(DonorOffer offer, DonorView donor, DonationAnswer answer, string label)
    {
        var (failure, result) = _engine.AnswerDonation(offer.Id, answer);
        if (failure is not null || result is null)
        {
            Refresh();
            return;
        }

        var lines = new List<string> { result.Text };
        if (result.Money != 0)
        {
            lines.Add(string.Format(Tr("Caixa: {0}."), SignedMoney(result.Money)));
        }

        lines.Add(string.Format(Tr("Risco das contas: de {0} para {1} ({2})."), result.RiskBefore, result.RiskAfter, AccountsCasePanel.RiskBand(result.RiskAfter, FinanceContent.Regras)));
        if (result.Debt is { } debt)
        {
            lines.Add(string.Format(Tr("Dívida no Livro de compromissos: {0}."), debt.Text));
        }

        if (result.Skeleton is { } skeleton)
        {
            lines.Add(string.Format(Tr("Esqueleto no Livro de compromissos: {0} (gravidade {1} de 3). Quem sabe: {2}."), skeleton.Text, skeleton.Severity, string.Join(", ", skeleton.KnownBy.Select(_engine.NameOf))));
        }

        lines.Add(string.Format(Tr("{0} vai lembrar: \"{1}\" ({2} de relação)."), donor.Name, result.Memory.Text, Signed(result.Memory.Pontos, "0")));
        _log.Insert(0, string.Format(Tr("Oferta de {0}: {1}."), donor.Name, LowerFirst(label)));
        _notice = null;
        Done(string.Format(Tr("Oferta de {0}: {1}"), donor.Name, LowerFirst(label)), lines);
    }
}
