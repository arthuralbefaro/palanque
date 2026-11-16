using Godot;
using Planalto.Core.Executive;

namespace Planalto.UI.Screens;

/// <summary>Textos da prefeitura montados a partir dos dados do Core (sempre por Tr).</summary>
public static class ExecutiveTexts
{
    private static string Tr(string text) => TranslationServer.Translate(text);

    public static string News(ExecutiveNews n) => n.Kind switch
    {
        ExecutiveNewsKind.PoliticaAprovada => string.Format(Tr("A Câmara aprovou por {1} a {2}: {0}."), n.Subject, n.Yes, n.No),
        ExecutiveNewsKind.PoliticaRejeitada => string.Format(Tr("A Câmara rejeitou ({1} a favor, {2} contra): {0}."), n.Subject, n.Yes, n.No),
        ExecutiveNewsKind.PoliticaDecretada => string.Format(Tr("Decreto assinado: {0}."), n.Subject),
        ExecutiveNewsKind.PoliticaEmVigor => string.Format(Tr("Em funcionamento: {0}."), n.Subject),
        ExecutiveNewsKind.PoliticaRevogada => string.Format(Tr("Revogado: {0}."), n.Subject),
        ExecutiveNewsKind.Crise => string.Format(Tr("Crise: {0}."), n.Subject),
        ExecutiveNewsKind.CriseResolvida => string.Format(Tr("Resposta à crise: {0}."), n.Subject),
        ExecutiveNewsKind.MinimoDescumprido => string.Format(Tr("O orçamento está abaixo do mínimo legal em: {0}. O Tribunal de Contas vai olhar as contas do ano."), n.Subject),
        ExecutiveNewsKind.ContasRejeitadas => string.Format(Tr("O Tribunal de Contas rejeitou as contas de {0} por descumprir os mínimos de saúde ou educação."), n.Subject),
        ExecutiveNewsKind.SecretariaEntregue => string.Format(Tr("O {0} ganhou uma secretaria e entrou na base do governo."), n.Subject),
        ExecutiveNewsKind.SecretariaRetirada => string.Format(Tr("O {0} perdeu uma secretaria."), n.Subject),
        ExecutiveNewsKind.Articulacao => string.Format(Tr("Reunião com {0}: a relação melhorou."), n.Subject),
        _ => n.Subject,
    };

    public static string Failure(ExecutiveFailure failure) => Tr(failure switch
    {
        ExecutiveFailure.SemEnergia => "Sem energia nesta semana.",
        ExecutiveFailure.SemReserva => "A reserva livre do orçamento não cobre o custo.",
        ExecutiveFailure.JaAdotada => "Já adotada.",
        ExecutiveFailure.CrisePendente => "Resolva a crise primeiro.",
        ExecutiveFailure.SemSecretarias => "Não há secretaria para entregar.",
        ExecutiveFailure.OrcamentoInvalido => "As parcelas precisam somar 100%.",
        _ => "Escolha um alvo válido.",
    });

    public static string Status(PolicyStatus status) => Tr(status switch
    {
        PolicyStatus.Implantando => "em implantação",
        PolicyStatus.Ativa => "em funcionamento",
        PolicyStatus.Rejeitada => "rejeitada na Câmara",
        _ => "revogada",
    });
}
