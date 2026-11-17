using Godot;
using Planalto.Core.Campaign;
using Planalto.UI.Theme;

namespace Planalto.UI.Screens;

/// <summary>
/// Painel Você (RL), sempre visível no alto do painel da esquerda: turnos livres hoje e na semana, estresse com o
/// limite que derruba os testes, dinheiro e semanas até a eleição. Os números vêm do Core; a tela só escreve.
/// </summary>
public partial class HubScreen
{
    /// <summary>Painel Você (RL): turnos de hoje e da semana, estresse com o limite, dinheiro e a eleição.</summary>
    private void BuildYou()
    {
        if (_gov)
        {
            _you.Visible = false;
            return;
        }

        _you.Visible = true;
        Ui.Clear(_you);
        YouStatus you = _engine.You();
        PanelContainer card = Card(_you, Palette.Cartao, null, 12);
        card.Name = "Voce";
        VBoxContainer col = Ui.Column(card, 2);
        HBoxContainer title = Ui.Row(col, 8);
        NameTitle(title, Tr("Você"), 22).SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Label hint = Ui.Label(title, "o que limita você agora", 18);
        hint.AddThemeColorOverride("font_color", Palette.Texto3);

        // Turnos livres hoje: uma bolinha por turno, na cor do que ele é.
        string turnsWhat = string.Format(PtBr, Tr("Turnos livres hoje: {0} de {1} (manhã, tarde e noite)."), you.FreeToday, you.Today.Count);
        CommitmentType longest = Catalog.Tipos.MaxBy(t => t.Turnos)!;
        string turnsDoes = string.Format(PtBr, Tr("cada compromisso seu ocupa um turno ({0}, {1}) e acontece no fim dele. {2} turnos por dia não custam nada; os seguintes somam +{3} de estresse cada. Hoje ainda dá para usar {4} sem estresse a mais. Bolinhas: verde, livre; azul, marcado; cinza, feito ou passado."), longest.Nome.ToLower(PtBr), longest.Turnos, you.TurnsWithoutCost, you.ExtraTurnStress, you.FreeTodayWithoutStress);
        HBoxContainer turns = YouRow(col, "Voce_TurnosHoje", Tr("Turnos livres hoje"), Palette.Tip(turnsWhat, turnsDoes, Tr("o turno que passa vazio não volta; um representante, com coordenação, não ocupa turno seu.")));
        var dots = turns.Add(new HBoxContainer { MouseFilter = MouseFilterEnum.Pass, SizeFlagsVertical = SizeFlags.ShrinkCenter });
        dots.AddThemeConstantOverride("separation", 4);
        foreach (TurnoSlot slot in you.Today)
        {
            Color color = slot.Situacao switch
            {
                TurnoSituacao.Livre => Palette.Positivo,
                TurnoSituacao.Marcado or TurnoSituacao.EmAndamento => Palette.Acao,
                TurnoSituacao.Feito => Palette.Texto3,
                _ => Palette.Linha,
            };
            var dot = dots.Add(new Panel { CustomMinimumSize = new Vector2(16, 16), MouseFilter = MouseFilterEnum.Pass, SizeFlagsVertical = SizeFlags.ShrinkCenter });
            dot.AddThemeStyleboxOverride("panel", Palette.Box(color, 8, 0));
        }

        YouValue(turns, string.Format(PtBr, "{0} de {1}", you.FreeToday, you.Today.Count));

        HBoxContainer week = YouRow(col, "Voce_TurnosSemana", Tr("Turnos livres na semana"), Palette.Tip(
            string.Format(PtBr, Tr("Turnos livres de agora até domingo: {0} de {1}."), you.FreeWeek, you.WeekTurns),
            Tr("é o tempo que você tem para aparecer nas zonas antes da virada da semana, quando a presença cai onde você não esteve."),
            Tr("nada; cada turno usado sai daqui.")));
        YouValue(week, string.Format(PtBr, "{0} de {1}", you.FreeWeek, you.WeekTurns));

        bool stressed = you.Stress > you.StressLimit;
        HBoxContainer stress = YouRow(col, "Voce_Estresse", Tr("Estresse"), Palette.Tip(
            string.Format(PtBr, Tr("Estresse: {0} de 100; o limite é {1}."), you.Stress, you.StressLimit),
            string.Format(PtBr, Tr("acima de {0}, a chance de você se sair bem cai {1:0} pontos nos compromissos com teste ({2}). Sobe com os compromissos, mais no terceiro turno do dia; Descanso e família baixa."), you.StressLimit, you.StressPenalty * 100, string.Join(", ", you.TestedTypes)),
            Tr("nada; é um limite, não um gasto.")));
        YouValue(stress, string.Format(PtBr, stressed ? "▲ {0} · {1} {2}" : "{0} · {1} {2}", you.Stress, Tr("limite"), you.StressLimit), stressed ? Palette.Negativo : null);
        StressBar(col, you);

        string cash = double.IsInfinity(you.WeeksOfCash) ? Tr("sem gasto fixo por semana") : string.Format(PtBr, Tr("o caixa cobre {0:0.#} semanas disso"), you.WeeksOfCash);
        HBoxContainer money = YouRow(col, "Voce_Dinheiro", Tr("Dinheiro"), Palette.Tip(
            string.Format(Tr("Dinheiro em caixa: {0}."), Money(you.Money)),
            string.Format(Tr("paga compromissos (o mais barato custa {0}), pesquisas, a folha da equipe ({1} por semana) e a verba dos canais ({2} por semana); folha e verba saem toda segunda-feira e {3}."), Money(you.CheapestCommitment), Money(you.Payroll), Money(you.BudgetPlan), cash),
            Tr("abaixo do custo de um compromisso, ele fica bloqueado; o jantar de arrecadação traz mais.")));
        YouValue(money, Money(you.Money), you.Money < you.CheapestCommitment ? Palette.Negativo : null);

        // Reconhecimento e imagem saíram da barra de cima (resposta do autor à RL): ficam aqui, sempre à vista.
        string zones = string.Join(", ", Enumerable.Range(0, Territory.Zones.Count)
            .OrderByDescending(z => Territory.Population[z]).Take(4)
            .Select(z => string.Format(PtBr, "{0} {1:0}", ZoneName(Territory.Zones[z]), Territory.PresenceOf(RaceSetup.PlayerId, z))));
        HBoxContainer known = YouRow(col, "Voce_Reconhecimento", Tr("Conhecem você"), Palette.Tip(
            Tr("Reconhecimento: quanto da cidade sabe quem você é."),
            string.Format(Tr("é a média da sua presença nas zonas, pesada pela população (nas maiores: {0}); o eleitor só vota em quem conhece."), zones),
            Tr("é um resultado, não um custo.")));
        YouValue(known, string.Format(PtBr, "{0:0}%", Campaign.Player.Recognition));
        YouImageRow(col, Campaign.Player.Image);

        string start = you.Phase == CampaignPhase.PreCampanha
            ? string.Format(Tr("a campanha oficial começa em {0} e libera o comício; até lá é pré-campanha."), you.CampaignStart.ToString("d 'de' MMMM", PtBr))
            : Tr("a campanha oficial já começou: comício liberado.");
        HBoxContainer election = YouRow(col, "Voce_Eleicao", Tr("Eleição"), Palette.Tip(
            string.Format(Tr("Eleição em {0} dias, no domingo {1}."), you.DaysToElection, you.ElectionDate.ToString("d 'de' MMMM", PtBr)),
            start,
            Tr("nada; o tempo corre sozinho.")));
        YouValue(election, string.Format(PtBr, Tr(you.WeeksToElection == 1 ? "em {0} semana" : "em {0} semanas"), you.WeeksToElection));
    }

    /// <summary>
    /// Imagem no painel Você: a faixa nomeada (data/campaign/config.json) e a régua com uma casa por faixa; o número cru
    /// fica só no tooltip, com os limites da faixa.
    /// </summary>
    private void YouImageRow(Node parent, double image)
    {
        CampaignConfig config = _game.Campaign.Config;
        int index = config.ImageBandIndex(image);
        int side = config.ImageBandSide(index);
        var (from, to) = config.ImageBandRange(index);
        Color color = side > 0 ? Palette.Positivo : side < 0 ? Palette.Negativo : Palette.Texto;
        string tip = Palette.Tip(
            Tr("Imagem: como a cidade vê você, numa régua de −1 a +1."),
            string.Format(Tr("hoje {0}, na faixa \"{1}\" (de {2} a {3}); pesa no voto e volta aos poucos para zero sem novidades."), Signed(image, "0.00"), config.ImageScale[index].Name, Signed(from, "0.00"), Signed(to, "0.00")),
            Tr("é um resultado, não um custo."));
        HBoxContainer row = YouRow(parent, "Imagem", Tr("Como te veem"), tip);
        var ruler = row.Add(new HBoxContainer { Name = "Regua", MouseFilter = MouseFilterEnum.Pass, SizeFlagsVertical = SizeFlags.ShrinkCenter });
        ruler.AddThemeConstantOverride("separation", 3);
        for (int i = 0; i < config.ImageScale.Count; i++)
        {
            ruler.Add(new ColorRect { Color = i == index ? color : new Color(Palette.Texto3, 0.5f), CustomMinimumSize = new Vector2(8, i == index ? 18 : 10), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        }

        // Cor de significado vem sempre com sinal (ART_GUIDE): seta para cima ou para baixo antes do nome da faixa.
        string sign = side > 0 ? "▲ " : side < 0 ? "▼ " : "";
        Label value = Ui.Value(row, sign + config.ImageScale[index].Name, 20);
        value.Name = "Faixa";
        value.AddThemeFontOverride("font", Palette.Weight(Palette.Inter, 600));
        value.AddThemeColorOverride("font_color", color);
        value.VerticalAlignment = VerticalAlignment.Center;
        value.MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>Linha do painel Você: rótulo à esquerda (cede), valor à direita (nunca cortado), tooltip em três partes.</summary>
    private static HBoxContainer YouRow(Node parent, string name, string caption, string tip)
    {
        var row = parent.Add(new HBoxContainer { Name = name, MouseFilter = MouseFilterEnum.Stop, TooltipText = tip, CustomMinimumSize = new Vector2(0, 30) });
        row.AddThemeConstantOverride("separation", 8);
        Label label = Ui.Value(row, caption, 18);
        label.AddThemeColorOverride("font_color", Palette.Texto2);
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.TooltipText = tip;
        label.MouseFilter = MouseFilterEnum.Pass;
        return row;
    }

    private static void YouValue(HBoxContainer row, string text, Color? color = null)
    {
        Label value = Ui.Value(row, text, 20);
        value.Name = "Valor";
        value.AddThemeFontOverride("font", Palette.Weight(Palette.Inter, 600));
        value.AddThemeColorOverride("font_color", color ?? Palette.Texto);
        value.VerticalAlignment = VerticalAlignment.Center;
        value.MouseFilter = MouseFilterEnum.Pass;
    }

    /// <summary>Barra do estresse de 0 a 100 com a marca do limite que derruba os testes.</summary>
    private static void StressBar(Node parent, YouStatus you)
    {
        var bar = parent.Add(new Control { Name = "BarraEstresse", CustomMinimumSize = new Vector2(0, 10), MouseFilter = MouseFilterEnum.Ignore });
        var back = bar.Add(new ColorRect { Color = Palette.Linha, MouseFilter = MouseFilterEnum.Ignore });
        back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var fill = bar.Add(new ColorRect { Color = you.Stress > you.StressLimit ? Palette.Negativo : Palette.Texto2, MouseFilter = MouseFilterEnum.Ignore });
        fill.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
        fill.AnchorRight = Math.Clamp(you.Stress / 100f, 0, 1);
        fill.OffsetRight = 0;
        var limit = bar.Add(new ColorRect { Name = "Limite", Color = Palette.Negativo, MouseFilter = MouseFilterEnum.Ignore });
        limit.AnchorTop = 0;
        limit.AnchorBottom = 1;
        limit.AnchorLeft = limit.AnchorRight = Math.Clamp(you.StressLimit / 100f, 0, 1);
        limit.OffsetLeft = -1;
        limit.OffsetRight = 1;
        limit.OffsetTop = -3;
        limit.OffsetBottom = 3;
    }
}
