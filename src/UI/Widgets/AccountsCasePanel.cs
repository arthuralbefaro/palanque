using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.UI.Theme;

namespace Planalto.UI.Widgets;

/// <summary>
/// Andamento das contas da última eleição (GDD 17.12), depois da apuração: prestação final, julgamento e a
/// representação por captação ou gasto ilícito, cada etapa com o estado, a data e o artigo de lei, e o que já saiu.
/// Serve ao mandato, à prefeitura, à entressafra e ao painel Contas da campanha seguinte. Só lê o estado.
/// </summary>
public static class AccountsCasePanel
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Tr(string text) => TranslationServer.Translate(text);

    private static string Money(decimal value) => string.Create(PtBr, $"R$ {value:N0}");

    /// <summary>Faixa do risco das contas pelo que ele causa no julgamento (os cortes vêm de data/campaign/finance.json).</summary>
    public static string RiskBand(int risk, FinanceRules rules) =>
        risk >= rules.RiscoDesaprovacao ? Tr("alto: do jeito que está, as contas são desaprovadas")
        : risk >= rules.RiscoRessalvas ? Tr("médio: do jeito que está, as contas são aprovadas com ressalvas")
        : Tr("baixo: do jeito que está, as contas são aprovadas");

    public static Color RiskColor(int risk, FinanceRules rules) =>
        risk >= rules.RiscoDesaprovacao ? Palette.Negativo : risk >= rules.RiscoRessalvas ? Palette.Atencao : Palette.Positivo;

    /// <summary>Uma linha por etapa: o que é, em que pé está e a base legal.</summary>
    public static List<(string Step, string Status, bool Done)> Steps(AccountsCase c, CampaignFinanceLawConfig law)
    {
        var steps = new List<(string, string, bool)>();
        bool delivered = c.Stage != AccountsStage.AguardandoPrestacao;
        steps.Add((
            Tr("Prestação de contas final"),
            delivered
                ? Tr("entregue à Justiça Eleitoral.")
                : string.Format(Tr("vence em {0}: {1} dias depois da eleição."), law.PrestacaoFinal(c.ElectionDate).ToString("dd/MM/yyyy", PtBr), law.PrestacaoFinalDiasDepoisDaEleicao),
            delivered));

        string judgment = c.Verdict switch
        {
            AccountsVerdict.Aprovadas => Tr("contas aprovadas."),
            AccountsVerdict.AprovadasComRessalvas => Tr("contas aprovadas com ressalvas."),
            AccountsVerdict.Desaprovadas => Tr("contas desaprovadas."),
            AccountsVerdict.NaoPrestadas => Tr("contas não prestadas."),
            _ => c.JudgmentOn is { } on
                ? string.Format(Tr("aguardando; a Justiça Eleitoral deve julgar em {0}."), on.ToString("MMMM 'de' yyyy", PtBr))
                : Tr("começa depois da entrega da prestação final."),
        };
        steps.Add((Tr("Julgamento das contas"), judgment, c.Verdict is not null));

        int rulings = c.Log.Count(h => h.Kind == "representacao");
        string representation =
            c.MandateLost ? string.Format(Tr("julgada procedente: diploma cassado em {0}. O mandato terminou."), c.MandateLostOn?.ToString("dd/MM/yyyy", PtBr))
            : c.RepresentationOpen ? string.Format(Tr("aberta; a decisão deve sair em {0}. Se for procedente, o diploma é cassado."), c.RulingOn?.ToString("MMMM 'de' yyyy", PtBr))
            : rulings >= 2 ? Tr("julgada improcedente: o diploma está mantido.")
            : c.RepresentationChecked ? Tr("ninguém apresentou no prazo.")
            : string.Format(Tr("pode ser apresentada por partido ou coligação até {0} dias depois da diplomação, se houver irregularidade grave e quem denuncie."), law.RepresentacaoDiasDaDiplomacao);
        steps.Add((Tr("Representação por captação ou gasto ilícito"), representation, c.RepresentationChecked && !c.RepresentationOpen));
        return steps;
    }

    /// <summary>Monta o painel dentro de <paramref name="parent"/>. <paramref name="news"/> são as manchetes da semana que acabou de virar.</summary>
    public static void Build(Node parent, AccountsCase c, FinanceCatalog content, CampaignFinanceLawConfig law, IReadOnlyList<Headline>? news = null)
    {
        var card = parent.Add(new PanelContainer { Name = "ContasDaEleicao" });
        bool fresh = news is { Count: > 0 };
        card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Cartao, 8, 12, fresh ? Palette.Atencao : Palette.Linha));
        VBoxContainer col = Ui.Column(card, 6);
        Glossary.Rich(col, string.Format(Tr("Contas da eleição de {0}"), c.ElectionDate.Year), 20, Palette.Texto, Palette.Weight(Palette.Inter, 600));
        if (fresh)
        {
            foreach (Headline h in news!)
            {
                Glossary.Rich(col, string.Format(Tr("Novidade desta semana: {0}."), h.Text), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600)).Name = "NovidadeContas";
            }
        }

        Glossary.Rich(col, string.Format(PtBr, Tr("Gasto na campanha: {0}, de um limite de {1} ({2:P0}). Risco das contas na entrega: {3} de 100 ({4})."),
            Money(c.Spent), Money(c.Limit), c.Limit <= 0 ? 0 : (double)(c.Spent / c.Limit), c.Risk, RiskBand(c.Risk, content.Regras)), 18, Palette.Texto2);
        if (c.OffBooks > 0)
        {
            string knowers = c.HostileKnowers == 0 ? Tr("Ninguém que sabe estava contra você")
                : c.HostileKnowers == 1 ? Tr("1 pessoa que sabe estava contra você")
                : string.Format(Tr("{0} pessoas que sabem estavam contra você"), c.HostileKnowers);
            Glossary.Rich(col, string.Format(PtBr, Tr("Dinheiro \"por fora\": {0} ({1:P0} do limite). {2}; denúncias já feitas: {3}."), Money(c.OffBooks), c.OffBooksShare, knowers, c.Denuncias), 18, Palette.Texto2);
        }

        foreach (var (step, status, done) in Steps(c, law))
        {
            HBoxContainer row = Ui.Row(col, 8);
            var mark = row.Add(new ColorRect { Color = done ? Palette.Positivo : Palette.Texto3, CustomMinimumSize = new Vector2(10, 10), MouseFilter = Control.MouseFilterEnum.Ignore });
            mark.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            Glossary.Rich(row, $"{step}: {status}", 18, Palette.Texto);
        }

        Glossary.Rich(col, string.Format(Tr("Base legal: {0}; {1}."), law.ArtigoPrestacaoDeContas, law.ArtigoRepresentacao), 18, Palette.Texto3);
        if (c.Log.Count > 0)
        {
            Glossary.Rich(col, Tr("O que já saiu:") + " " + string.Join(" · ", c.Log.Select(h => $"{h.Week.ToString("dd/MM/yyyy", PtBr)}: {h.Text}")) + ".", 18, Palette.Texto2);
        }
    }

    /// <summary>
    /// Atalho para as telas fora da campanha: lê o estado e as novidades do <see cref="Game"/>. Processo encerrado e
    /// sem novidade não aparece, a não ser que o diploma tenha sido cassado.
    /// </summary>
    public static void BuildFromGame(Node parent, bool withNews = true)
    {
        Game game = Game.Instance;
        if (game.State?.Accounts is { } accounts && (accounts.Stage != AccountsStage.Encerrado || accounts.MandateLost || game.AccountsNews.Count > 0))
        {
            Build(parent, accounts, game.Campaign.Finance, CampaignFinanceLawConfig.Default, withNews ? game.AccountsNews : null);
        }
    }
}
