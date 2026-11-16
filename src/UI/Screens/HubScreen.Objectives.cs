using Godot;
using Planalto.Core.Campaign;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Objetivos (GDD 17.2, peça 6, e princípio h): na coluna direita, entre as Notícias e a Intenção de voto, até três
/// metas que nasceram do jogo, cada uma com a condição, a medida de hoje e a meta, o prazo e a origem; Fixar e Dispensar.
/// </summary>
public partial class HubScreen
{
    /// <summary>A medida de hoje e a meta, na unidade do tipo de objetivo.</summary>
    private string ObjectiveMeasure(ObjectiveView v)
    {
        Objective o = v.Objective;
        switch (o.Kind)
        {
            case ObjectiveType.PresencaZona:
                return string.Format(PtBr, Tr("Hoje: {0:0.0} · meta: {1:0}"), v.Current, v.Goal);
            case ObjectiveType.PassarRival:
                return v.Done ? Tr("Hoje: você está à frente") : string.Format(PtBr, Tr("Hoje: faltam {0:N0} votos"), Math.Ceiling(Math.Abs(v.Current)));
            case ObjectiveType.TrazerLideranca:
                return v.Done ? Tr("Hoje: apoia você") : string.Format(Tr("Hoje: relação {0}"), Signed(v.Current, "0"));
            case ObjectiveType.JuntarCaixa:
                return string.Format(Tr("Hoje: {0} de {1}"), Money((decimal)v.Current), Money((decimal)v.Goal));
            case ObjectiveType.PosicaoConhecida:
                return string.Format(PtBr, Tr("Hoje: {0:P0} · meta: {1:P0}"), v.Current, v.Goal);
            case ObjectiveType.CumprirPromessa:
                return v.Done ? Tr("Hoje: cumprida") : Tr("Hoje: em aberto");
            default:
                return v.Done ? Tr("Hoje: função ocupada") : Tr("Hoje: função vaga");
        }
    }

    private Button SmallIcon(Node parent, string icon, string tip, Action onPressed, bool selected = false)
    {
        var b = parent.Add(new Button
        {
            Icon = Palette.Icon(icon),
            ExpandIcon = true,
            IconAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(44, 44),
            TooltipText = tip,
        });
        b.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        b.AddThemeStyleboxOverride("normal", Palette.Box(selected ? new Color(Palette.Acao, 0.2f) : Palette.Cartao, 8, 8, selected ? Palette.Acao : Palette.Linha));
        b.AddThemeStyleboxOverride("hover", Palette.Box(Palette.CartaoAlto, 8, 8, selected ? Palette.Acao : Palette.Linha));
        b.AddThemeStyleboxOverride("pressed", Palette.Box(Palette.CartaoAlto, 8, 8, Palette.Acao));
        b.Pressed += onPressed;
        return b;
    }

    /// <summary>Uma linha só: o que não cabe é cortado com reticências (o texto inteiro está no tooltip do cartão).</summary>
    private static void OneLine(Node parent, string text, Color color)
    {
        const int Max = 31;
        string shown = text.Length <= Max ? text : text[..(Max - 1)].TrimEnd() + "…";
        // Sem quebra automática o rótulo alargaria a coluna: fica com quebra, altura de uma linha e o resto recortado.
        RichTextLabel label = Glossary.Rich(parent, shown, 18, color);
        label.FitContent = false;
        label.CustomMinimumSize = new Vector2(0, 24);
        label.ClipContents = true;
    }

    private void BuildObjectives()
    {
        Ui.Clear(_objectives);
        if (Campaign.Phase is not (CampaignPhase.PreCampanha or CampaignPhase.Campanha))
        {
            _objectives.Visible = false;
            return;
        }

        _objectives.Visible = true;
        ObjectiveRules rules = _engine.Context.Campaign.Objectives.Regras;
        Heading(_objectives, Tr("Objetivos"));
        var views = _engine.ActiveObjectives();
        if (views.Count == 0)
        {
            Glossary.Rich(_objectives, Tr("Nenhum objetivo agora. Toda segunda-feira o jogo propõe metas a partir do que está acontecendo."));
        }

        foreach (ObjectiveView view in views.Take(3))
        {
            Objective o = view.Objective;
            PanelContainer card = Card(_objectives, o.Pinned ? Palette.CartaoAlto : Palette.Cartao, margin: 8);
            card.Name = "Objetivo_" + o.Id;
            card.MouseFilter = MouseFilterEnum.Stop;
            card.TooltipText = string.Format(Tr("{0}.\n{1}.\nDe onde veio: {2}.\nPrazo: {3} ({4}).\nSe cumprir: lealdade {5} em cada pessoa da equipe e uma lembrança boa em quem se interessa pelo assunto."),
                o.Text, ObjectiveMeasure(view), o.Origin, o.Deadline.ToString("dd/MM/yyyy", PtBr), string.Format(Tr(view.DaysLeft == 1 ? "falta {0} dia" : "faltam {0} dias"), view.DaysLeft), Signed(rules.LealdadeEquipe, "0"));
            // Condição em cima, na largura toda; embaixo, medida, prazo e origem ao lado dos dois botões. A origem
            // inteira fica no tooltip do cartão (ART_GUIDE: reticências com o texto inteiro no tooltip).
            HBoxContainer line = Ui.Row(card, 6);
            var col = Ui.Column(line, 2);
            col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            Glossary.Rich(col, o.Text + (o.Pinned ? " · " + Tr("fixado") : ""), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
            Bar(col, view.Progress, view.Done ? Palette.Positivo : Palette.Acao).CustomMinimumSize = new Vector2(48, 6);
            Glossary.Rich(col, ObjectiveMeasure(view) + " · " + string.Format(Tr("até {0}"), o.Deadline.ToString("d/MM", PtBr)));
            OneLine(col, string.Format(Tr("Origem: {0}"), o.Origin), Palette.Texto3);
            var row = Ui.Column(line, 4);

            var pin = SmallIcon(row, "alfinete", Palette.Tip(
                Tr(o.Pinned ? "Soltar: o objetivo volta a ser uma proposta." : "Fixar: mantém este objetivo no painel até o prazo."),
                string.Format(Tr(o.Pinned ? "sem fixar, a proposta sai sozinha depois de {0} semanas se não for cumprida." : "sem fixar, a proposta sai sozinha depois de {0} semanas; fixado, fica até {1}."), rules.ValidadePropostaSemanas, o.Deadline.ToString("dd/MM", PtBr)),
                Tr("nada; fixar não muda a recompensa nem o prazo.")), () =>
            {
                _engine.PinObjective(o.Id, !o.Pinned);
                _log.Insert(0, string.Format(Tr(o.Pinned ? "Objetivo fixado: {0}." : "Objetivo solto: {0}."), o.Text));
                Refresh();
            }, selected: o.Pinned);
            pin.Name = "Fixar_" + o.Id;
            var dismiss = SmallIcon(row, "fechar", Palette.Tip(
                Tr("Dispensar: tira este objetivo do painel."),
                Tr("ele sai sem punição; na segunda-feira o jogo propõe outro (se o painel ficar vazio, na hora)."),
                Tr("nada; você só deixa de ganhar a recompensa dele.")), () =>
            {
                string text = o.Text;
                _engine.DismissObjective(o.Id);
                _log.Insert(0, string.Format(Tr("Objetivo dispensado: {0}."), text));
                Refresh();
            });
            dismiss.Name = "Dispensar_" + o.Id;
        }
    }
}
