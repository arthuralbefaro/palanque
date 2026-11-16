using Planalto.Core.Data;
using Planalto.Core.Ideology;

namespace Planalto.Core.Campaign;

public sealed class PlatformRules
{
    public required int BandeirasMax { get; init; }

    /// <summary>Quanto a mais um tema de bandeira fica conhecido a cada fala.</summary>
    public required double BandeiraBonus { get; init; }

    /// <summary>Ganho semanal de posição conhecida nos temas de bandeira, mesmo sem fala.</summary>
    public required double BandeiraGanhoSemanal { get; init; }

    /// <summary>Uma mudança de posição é notícia: torna a posição mais conhecida.</summary>
    public required double ConhecidaPorMudanca { get; init; }

    public required int JanelaMudancasSemanas { get; init; }

    /// <summary>Imagem perdida por ponto de incoerência.</summary>
    public required double ImagemPorIncoerencia { get; init; }

    /// <summary>Pontos de relação, por ponto de incoerência, nas lideranças com interesse no tema.</summary>
    public required double PontosLiderancaPorIncoerencia { get; init; }
}

/// <summary>data/campaign/platform.json.</summary>
public sealed class PlatformCatalog : IValidatable
{
    public const string File = "campaign/platform.json";

    public required string Note { get; init; }

    public required PlatformRules Regras { get; init; }

    /// <summary>Compromissos em que o candidato fala de um tema → quanto a posição fica mais conhecida.</summary>
    public required Dictionary<string, double> Falas { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "anuncio", "fala", "fala_zona", "mudou_posicao", "feed_posicao", "previsao_salto", "previsao_incoerencia", "previsao_sem_custo",
        "previsao_cidade", "previsao_grupo", "previsao_zona", "previsao_liderancas", "previsao_livro", "previsao_bandeiras",
    ];

    public IEnumerable<string> Validate()
    {
        PlatformRules r = Regras;
        if (r.BandeirasMax < 1 || r.BandeiraBonus < 1 || r.BandeiraGanhoSemanal is < 0 or > 1 || r.ConhecidaPorMudanca is < 0 or > 1
            || r.JanelaMudancasSemanas < 1 || r.ImagemPorIncoerencia < 0 || r.PontosLiderancaPorIncoerencia > 0)
        {
            yield return "regras: valores fora do intervalo.";
        }

        if (Falas.Count == 0 || Falas.Values.Any(v => v is <= 0 or > 1))
        {
            yield return "falas: pelo menos um compromisso, com ganho em (0, 1].";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

/// <summary>Promessa local do catálogo (GDD 17.9), ligada a um tipo de obra ou de serviço.</summary>
public sealed class PromiseDefinition
{
    public const string TipoObra = "obra";
    public const string TipoServico = "servico";

    public required string Id { get; init; }

    /// <summary>Rótulo da escolha (até 28 caracteres).</summary>
    public required string Nome { get; init; }

    /// <summary>Texto por extenso para o Livro; aceita {alvo}.</summary>
    public required string Texto { get; init; }

    /// <summary>"obra" ou "servico".</summary>
    public required string Tipo { get; init; }

    /// <summary>Tipo de obra ou serviço que a prefeitura executa (GDD 17.15), para a cobrança no mandato.</summary>
    public required string Obra { get; init; }

    /// <summary>A quem pode ser feita: "zona" e/ou "grupo".</summary>
    public required List<string> Alvos { get; init; }

    /// <summary>Para zona: "urbana", "rural" ou "qualquer".</summary>
    public string Situacao { get; init; } = "qualquer";

    /// <summary>Para grupo: os grupos de eleitores a quem a promessa faz sentido.</summary>
    public List<string> Grupos { get; init; } = [];

    /// <summary>Apoio imediato do alvo (0 a 1), antes da credibilidade.</summary>
    public required double Apoio { get; init; }

    /// <summary>Semanas depois da posse para cumprir.</summary>
    public required int PrazoSemanas { get; init; }
}

public sealed class PromiseRules
{
    /// <summary>Promessas abertas que a credibilidade aguenta sem cair.</summary>
    public required int PromessasSemCusto { get; init; }

    public required double CredibilidadePerda { get; init; }

    public required double CredibilidadePerdaQuebrada { get; init; }

    public required double CredibilidadeMinima { get; init; }

    public required Parties.DoubleRange Sorte { get; init; }
}

/// <summary>data/campaign/promises.json.</summary>
public sealed class PromiseCatalog : IValidatable
{
    public const string File = "campaign/promises.json";

    public required string Note { get; init; }

    public required PromiseRules Regras { get; init; }

    public required List<PromiseDefinition> Promessas { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public PromiseDefinition? Find(string id) => Promessas.FirstOrDefault(p => p.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public static readonly IReadOnlyList<string> TextKeys = ["previsao_apoio", "previsao_credibilidade", "previsao_livro", "feed", "livro"];

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Promessas.Select(p => p.Id), "Promessa"))
        {
            yield return error;
        }

        PromiseRules r = Regras;
        if (r.PromessasSemCusto < 0 || r.CredibilidadePerda is <= 0 or > 1 || r.CredibilidadePerdaQuebrada is < 0 or > 1 || r.CredibilidadeMinima is <= 0 or > 1
            || r.Sorte.Min <= 0 || r.Sorte.Max < r.Sorte.Min)
        {
            yield return "regras: valores fora do intervalo.";
        }

        foreach (PromiseDefinition p in Promessas)
        {
            if (p.Nome.Length > 28)
            {
                yield return $"promessa '{p.Id}': nome com mais de 28 caracteres.";
            }

            if (p.Tipo is not (PromiseDefinition.TipoObra or PromiseDefinition.TipoServico) || string.IsNullOrWhiteSpace(p.Obra) || p.Apoio is <= 0 or > 1 || p.PrazoSemanas < 1)
            {
                yield return $"promessa '{p.Id}': tipo 'obra' ou 'servico', tipo de obra, apoio em (0, 1] e prazo positivo.";
            }

            if (p.Alvos.Count == 0 || p.Alvos.Any(a => a is not (People.Promise.ParaZona or People.Promise.ParaGrupo))
                || (p.Alvos.Contains(People.Promise.ParaGrupo) && p.Grupos.Count == 0)
                || p.Situacao is not ("urbana" or "rural" or "qualquer"))
            {
                yield return $"promessa '{p.Id}': alvos 'zona' e/ou 'grupo' (com a lista de grupos) e situação válida.";
            }

            if (!p.Texto.Contains("{alvo}", StringComparison.Ordinal))
            {
                yield return $"promessa '{p.Id}': o texto precisa de {{alvo}}.";
            }
        }

        if (Promessas.Count == 0)
        {
            yield return "nenhuma promessa no catálogo.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

/// <summary>Mudança de posição registrada (para contar as das últimas semanas).</summary>
public sealed record PositionChange(string Theme, DateOnly Date, int From, int To, double Incoherence);

/// <summary>Plataforma da campanha em curso (GDD 17.9).</summary>
public sealed class PlatformState
{
    /// <summary>Posições que o jogador assumiu (−2 a 2); nos demais temas vale a do partido.</summary>
    public Dictionary<string, int> Positions { get; init; } = [];

    /// <summary>Temas que a campanha enfatiza.</summary>
    public List<string> Flags { get; init; } = [];

    /// <summary>Posição conhecida por tema (0 a 1).</summary>
    public Dictionary<string, double> Known { get; init; } = [];

    public List<PositionChange> Changes { get; init; } = [];
}

/// <summary>O que uma mudança de posição custou.</summary>
public sealed record PositionResult(PositionChange Change, double ImageDelta, IReadOnlyList<string> LeadersWhoRemember, People.StatedPosition Stated);

/// <summary>O que uma promessa rendeu.</summary>
public sealed record PromiseResult(People.Promise Promise, IReadOnlyDictionary<string, double> Support, double Credibility);

/// <summary>Célula da matriz "plataforma × públicos": o que o público prefere, quanto o tema pesa para ele e o quanto concorda com a posição do jogador (−1 a 1).</summary>
public sealed record PlatformCell(string TargetKind, string TargetId, double Preference, double Priority, double Agreement);

/// <summary>Linha da matriz: um tema, a posição do jogador e a do partido, o quanto é conhecida e cada público.</summary>
public sealed record PlatformRow(ThemeDefinition Theme, int Position, bool Explicit, double PartyPosition, double Known, bool Flag, IReadOnlyList<PlatformCell> Cells);

/// <summary>Promessa do catálogo que cabe a um alvo agora, para a tela.</summary>
public sealed record PromiseOption(PromiseDefinition Definition, string ToKind, string ToId, string Text, CommandFailure? Blocked);
