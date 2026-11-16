using System.Globalization;
using Godot;
using Planalto.Core.Career;
using Planalto.Core.Characters;
using Planalto.Core.Legislature;

namespace Planalto.UI.Screens;

/// <summary>Textos do mandato e da carreira montados a partir dos dados do Core (sempre por Tr, com o gênero do jogador).</summary>
public static class MandateTexts
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static string Tr(string text) => TranslationServer.Translate(text);

    /// <summary>Terminação de gênero ("o", "a" ou "e").</summary>
    public static string Ending(Gender gender) => gender switch { Gender.Masculino => "o", Gender.Feminino => "a", _ => "e" };

    public static string News(MandateNews n, Gender gender) => n.Kind switch
    {
        MandateNewsKind.ProjetoProtocolado => string.Format(Tr("Você protocolou: {0}."), n.Subject),
        MandateNewsKind.ProjetoAprovado => string.Format(Tr("Aprovado por {1} a {2}: {0}."), n.Subject, n.Yes, n.No) + (n.PlayerAuthor ? " " + Tr("(seu projeto)") : ""),
        MandateNewsKind.ProjetoRejeitado => string.Format(Tr("Rejeitado ({1} a favor, {2} contra): {0}."), n.Subject, n.Yes, n.No) + (n.PlayerAuthor ? " " + Tr("(seu projeto)") : ""),
        MandateNewsKind.ProjetoArquivado => string.Format(Tr("Arquivado na comissão: {0}."), n.Subject) + (n.PlayerAuthor ? " " + Tr("(seu projeto)") : ""),
        MandateNewsKind.Urgencia => string.Format(Tr("Urgência para {0}: vai ao plenário na semana que vem."), n.Subject),
        MandateNewsKind.Advertencia => string.Format(Tr("O {0} advertiu você por votar contra a orientação da bancada."), n.Subject),
        MandateNewsKind.Sancao => string.Format(Tr("O {0} tirou você da comissão e da liderança."), n.Subject),
        MandateNewsKind.Expulsao => string.Format(Tr("Você foi expuls{1} do {0}. O mandato continua; filie-se a outro partido para disputar a reeleição."), n.Subject, Ending(gender)),
        MandateNewsKind.Ausencia => string.Format(Tr("Falta na votação: {0}."), n.Subject),
        MandateNewsKind.Emenda => string.Format(Tr("Emenda indicada: {0}."), n.Subject),
        MandateNewsKind.Articulacao => string.Format(Tr("Conversa com {0}: a relação melhorou."), n.Subject),
        MandateNewsKind.MudancaDePartido => string.Format(Tr("Você se filiou ao {0}."), n.Subject),
        _ => n.Subject,
    };

    public static string Stage(BillStage stage) => Tr(stage switch
    {
        BillStage.Comissao => "na comissão",
        BillStage.Plenario => "no plenário",
        BillStage.Aprovado => "aprovado",
        BillStage.Rejeitado => "rejeitado",
        _ => "arquivado",
    });

    public static string Quorum(Quorum quorum) => Tr(quorum switch
    {
        Core.Legislature.Quorum.Absoluta => "maioria absoluta",
        Core.Legislature.Quorum.DoisTercos => "dois terços",
        _ => "maioria simples",
    });

    public static string Orientation(int orientation) => Tr(orientation switch { 1 => "SIM", -1 => "NÃO", _ => "liberada" });

    public static string Failure(MandateActionFailure failure) => Tr(failure switch
    {
        MandateActionFailure.SemEnergia => "Sem energia nesta semana.",
        MandateActionFailure.SemCapital => "Capital político insuficiente.",
        MandateActionFailure.SemEmendas => "As emendas deste ano acabaram.",
        _ => "Escolha um alvo válido.",
    });

    public static string Founding(FoundingFailure failure) => Tr(failure switch
    {
        FoundingFailure.SemCapital => "Capital político insuficiente.",
        FoundingFailure.SemDinheiro => "Dinheiro insuficiente.",
        FoundingFailure.SemEnergia => "Sem energia nesta semana.",
        FoundingFailure.JaEmCurso => "Já há uma fundação em curso.",
        FoundingFailure.NomeInvalido => "Nome em uso, bloqueado ou vazio.",
        FoundingFailure.SiglaInvalida => "Sigla em uso, bloqueada ou com tamanho inválido.",
        FoundingFailure.NumeroInvalido => "Número fora da faixa ou já usado.",
        FoundingFailure.JaColetouNestaSemana => "A coleta desta semana já foi feita.",
        FoundingFailure.EstadosInvalidos => "Escolha de 1 a 3 estados.",
        FoundingFailure.RequisitosIncompletos => "Ainda faltam assinaturas ou estados.",
        _ => "Não disponível nesta etapa.",
    });

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", PtBr);
}
