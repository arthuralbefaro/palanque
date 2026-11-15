using Godot;
using Planalto.Core.Parties;
using Planalto.Core.World;

namespace Planalto.UI.Screens;

/// <summary>Rótulos da tela "Novo Mundo" e mensagens das validações do Core (traduzidas com Tr()).</summary>
public static class WorldTexts
{
    public static string Size(PartySize size) => TranslationServer.Translate(size switch
    {
        PartySize.Nanico => "Nanico",
        PartySize.Pequeno => "Pequeno",
        PartySize.Medio => "Médio",
        PartySize.Grande => "Grande",
        _ => "Dominante",
    });

    public static string Leadership(LeadershipStyle style) => TranslationServer.Translate(style switch
    {
        LeadershipStyle.Ideologica => "Ideológica",
        LeadershipStyle.Pragmatica => "Pragmática",
        LeadershipStyle.Fisiologica => "Fisiológica",
        _ => "Personalista",
    });

    public static string Frequency(EventFrequency f) => TranslationServer.Translate(f switch
    {
        EventFrequency.Baixa => "Baixa",
        EventFrequency.Normal => "Normal",
        EventFrequency.Alta => "Alta",
        _ => "Caótica",
    });

    public static string Realism(LegalRealism r) => TranslationServer.Translate(r == LegalRealism.Relaxado ? "Relaxado" : "Rigoroso");

    public static string Issue(WorldIssue issue, Func<string?, string> party)
    {
        string p = party(issue.PartyId);
        string o = party(issue.OtherPartyId);
        string d = issue.Detail ?? "";
        string text = issue.Code switch
        {
            WorldIssueCode.PresetDesconhecida => "Predefinição desconhecida.",
            WorldIssueCode.PresetIndisponivel => "Predefinição indisponível: {d}",
            WorldIssueCode.QuantidadeDePartidosForaDaFaixa => "O mundo precisa ter entre 2 e 35 partidos.",
            WorldIssueCode.NomeVazio => "{p}: nome vazio.",
            WorldIssueCode.NomeBloqueado => "{p}: o nome contém o de um partido real ({d}).",
            WorldIssueCode.NomeRepetido => "{p} e {o} têm o mesmo nome.",
            WorldIssueCode.SiglaComTamanhoInvalido => "{p}: a sigla precisa ter de 2 a 8 letras.",
            WorldIssueCode.SiglaComCaracteresInvalidos => "{p}: a sigla só pode ter letras.",
            WorldIssueCode.SiglaBloqueada => "{p}: sigla de partido real.",
            WorldIssueCode.SiglaBloqueadaPorOrdemDasLetras => "{p}: a sigla tem as mesmas letras de uma sigla real.",
            WorldIssueCode.SiglaRepetida => "{p} e {o} têm a mesma sigla.",
            WorldIssueCode.NumeroForaDaFaixa => "{p}: o número precisa estar entre 10 e 99.",
            WorldIssueCode.NumeroRepetido => "{p} e {o} têm o mesmo número.",
            WorldIssueCode.FamiliaDesconhecida => "{p}: família ideológica desconhecida.",
            WorldIssueCode.PosicaoForaDosEixos => "{p}: posição fora dos eixos.",
            WorldIssueCode.CorInvalida => "{p}: cor inválida.",
            WorldIssueCode.LogotipoDesconhecido => "{p}: logotipo desconhecido.",
            WorldIssueCode.AnoInicialInvalido => "O ano inicial precisa ser de eleição, a partir de 2028.",
            WorldIssueCode.DificuldadeDesconhecida => "Dificuldade desconhecida.",
            WorldIssueCode.EspectroSemEsquerdaEconomica => "Nenhum partido à esquerda no eixo econômico.",
            WorldIssueCode.EspectroSemDireitaEconomica => "Nenhum partido à direita no eixo econômico.",
            WorldIssueCode.PosicaoForaDaFaixaDaFamilia => "{p}: posição fora da faixa da família.",
            WorldIssueCode.CoresParecidas => "{p} e {o} têm cores muito parecidas.",
            WorldIssueCode.NenhumPartidoPrincipal => "Nenhum partido médio ou maior.",
            WorldIssueCode.DominantesDemais => "Partidos dominantes demais ({d}).",
            _ => "{p}: nanico com peso grande demais num mundo com poucos partidos.",
        };
        return TranslationServer.Translate(text).ToString().Replace("{p}", p).Replace("{o}", o).Replace("{d}", d);
    }
}
