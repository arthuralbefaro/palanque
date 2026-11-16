using Planalto.Core.Data;

namespace Planalto.Core.Campaign;

/// <summary>Um pedido possível de liderança (GDD 17.10): o que ela quer em troca do apoio.</summary>
public sealed class LeaderRequestDefinition
{
    public const string Obra = "obra";
    public const string Vaga = "vaga";
    public const string Posicao = "posicao";
    public const string Presenca = "presenca";
    public const string Dinheiro = "dinheiro";

    public required string Id { get; init; }

    /// <summary>obra, vaga, posicao, presenca ou dinheiro (este é irregular e vira esqueleto).</summary>
    public required string Kind { get; init; }

    /// <summary>Texto do pedido; aceita {zona} e {tema}.</summary>
    public required string Text { get; init; }

    /// <summary>Semanas depois da posse para cumprir (0 = antes da eleição).</summary>
    public required int PrazoSemanas { get; init; }

    public double Weight { get; init; } = 1;
}

/// <summary>Tipo de liderança: o papel social, não uma doutrina (GDD 17.10, neutralidade).</summary>
public sealed class LeaderType
{
    public const string Religiosa = "religiosa";

    public required string Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>O que a pessoa é, para aparecer depois do nome ("presidente de associação de moradores").</summary>
    public required string Papel { get; init; }

    public required double Weight { get; init; }

    /// <summary>"urbana", "rural" ou "qualquer": onde o tipo costuma atuar.</summary>
    public required string Situacao { get; init; }

    /// <summary>
    /// Públicos possíveis (grupo de eleitores → peso). Na liderança religiosa os pesos não valem: o público é
    /// sorteado na proporção da composição religiosa do município no Censo.
    /// </summary>
    public required Dictionary<string, double> Publicos { get; init; }

    /// <summary>Temas de interesse (tema → peso); entre o maior e o menor peso não pode haver mais de 2 para 1.</summary>
    public required Dictionary<string, double> Interesses { get; init; }

    /// <summary>Ids dos pedidos que o tipo costuma fazer.</summary>
    public required List<string> Pedidos { get; init; }
}

public sealed class LeaderRules
{
    public required Dictionary<string, IntRange> QuantidadePorPorte { get; init; }

    /// <summary>Semanas sem contato a partir das quais a relação cai 1 ponto por semana.</summary>
    public required int SemanasSemContato { get; init; }

    /// <summary>Apoio do público na zona = influência × relação ÷ 100 × este fator.</summary>
    public required double ApoioPorInfluencia { get; init; }

    /// <summary>Relação que uma visita rende, conforme a resposta ao pedido.</summary>
    public required int PontosVisita { get; init; }

    public required int PontosVisitaSemCompromisso { get; init; }

    public required int PontosPromessa { get; init; }

    public required int PontosRecusa { get; init; }

    public required int PontosNegociacao { get; init; }

    public required int PontosNegociacaoFalha { get; init; }

    public required int PontosConviteAceito { get; init; }

    /// <summary>Relação que rende cumprir uma promessa feita à liderança.</summary>
    public required int PontosPromessaCumprida { get; init; }

    public required int PontosConviteRecusado { get; init; }

    /// <summary>Quanto esfria a liderança rival de quem fechou com o jogador.</summary>
    public required int PontosRivalidade { get; init; }

    /// <summary>Chance semanal de um rival procurar uma liderança dos seus redutos, e o favor que ganha.</summary>
    public required double ChanceAssedio { get; init; }

    public required IntRange FavorAssedio { get; init; }

    /// <summary>Favor ou relação mínima para a liderança se alinhar a alguém.</summary>
    public required int LimiarAlinhamento { get; init; }

    /// <summary>Favor que os rivais perdem por semana.</summary>
    public required double FavorDecaimento { get; init; }

    /// <summary>Chance semanal de uma liderança conhecida e não hostil convidar o jogador para algo.</summary>
    public required double ChanceConvite { get; init; }

    /// <summary>Fração das lideranças que o jogador já conhece no começo (as da zona natal).</summary>
    public required double ConhecidasNoInicio { get; init; }

    /// <summary>Chance de uma caminhada revelar uma liderança desconhecida da zona.</summary>
    public required double ChanceDescoberta { get; init; }

    /// <summary>Gravidade do esqueleto de quem paga evento de liderança (pedido de dinheiro).</summary>
    public required int GravidadeDinheiro { get; init; }
}

/// <summary>data/campaign/leaders.json.</summary>
public sealed class LeaderCatalog : IValidatable
{
    public const string File = "campaign/leaders.json";

    /// <summary>Razão máxima entre o maior e o menor peso de interesse de um tipo (neutralidade, GDD 17.10).</summary>
    public const double MaxInterestRatio = 2;

    public required string Note { get; init; }

    public required LeaderRules Regras { get; init; }

    public required List<LeaderType> Tipos { get; init; }

    public required List<LeaderRequestDefinition> Pedidos { get; init; }

    /// <summary>Textos do feed e das lembranças, com {nome}, {rival}, {zona}.</summary>
    public required Dictionary<string, string> Textos { get; init; }

    public LeaderType Type(string id) => Tipos.First(t => t.Id == id);

    public LeaderRequestDefinition Request(string id) => Pedidos.First(p => p.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "visita", "visita_sem_compromisso", "promessa", "recusa", "negociacao", "negociacao_falha", "convite_aceito", "convite_recusado",
        "rivalidade", "assedio", "promessa_cumprida", "mudou_para_rival", "mudou_para_jogador", "ficou_neutra", "descoberta", "convite", "promessa_livro", "divida_livro", "esqueleto_dinheiro",
    ];

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Tipos.Select(t => t.Id), "Tipo de liderança").Concat(DataChecks.UniqueIds(Pedidos.Select(p => p.Id), "Pedido")))
        {
            yield return error;
        }

        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            if (!Regras.QuantidadePorPorte.TryGetValue(porte, out IntRange? range) || range.Min < 1 || range.Max < range.Min)
            {
                yield return $"regras.quantidade_por_porte.{porte}: mínimo de 1 e máximo maior ou igual ao mínimo.";
            }
        }

        foreach (LeaderType t in Tipos)
        {
            if (t.Interesses.Count == 0 || t.Publicos.Count == 0 || t.Pedidos.Count == 0 || t.Weight <= 0)
            {
                yield return $"tipo '{t.Id}': interesses, públicos, pedidos e peso positivo.";
                continue;
            }

            if (t.Interesses.Values.Min() <= 0 || t.Interesses.Values.Max() / t.Interesses.Values.Min() > MaxInterestRatio + 1e-9)
            {
                yield return $"tipo '{t.Id}': pesos de interesse fora de {MaxInterestRatio} para 1.";
            }

            if (t.Situacao is not ("urbana" or "rural" or "qualquer"))
            {
                yield return $"tipo '{t.Id}': situação '{t.Situacao}' desconhecida.";
            }

            foreach (string pedido in t.Pedidos.Where(p => Pedidos.All(x => x.Id != p)))
            {
                yield return $"tipo '{t.Id}': pedido '{pedido}' não existe.";
            }
        }

        foreach (LeaderRequestDefinition p in Pedidos.Where(p => p.Kind is not (LeaderRequestDefinition.Obra or LeaderRequestDefinition.Vaga or LeaderRequestDefinition.Posicao or LeaderRequestDefinition.Presenca or LeaderRequestDefinition.Dinheiro)))
        {
            yield return $"pedido '{p.Id}': tipo '{p.Kind}' desconhecido.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

/// <summary>Resposta do jogador ao pedido de uma liderança visitada.</summary>
public enum LeaderAnswer
{
    Aceitar,
    Negociar,
    NaoSeComprometer,
    Recusar,
}

/// <summary>
/// Liderança local (GDD 17.10): pessoa nomeada que move votos de um público numa zona. Os dados de pessoa (interesses,
/// temperamento, lembranças) ficam em <see cref="People.Person"/>, com o mesmo id.
/// </summary>
public sealed class Leader
{
    public required string PersonId { get; init; }

    public required string Type { get; init; }

    /// <summary>Uma ou duas zonas (ids).</summary>
    public required List<string> Zones { get; init; }

    /// <summary>Grupo de eleitores que ela move.</summary>
    public required string Publico { get; init; }

    /// <summary>1 a 5.</summary>
    public required int Influence { get; init; }

    /// <summary>Tema de interesse.</summary>
    public required string Theme { get; init; }

    public required string RequestId { get; init; }

    /// <summary>Texto do pedido já com a zona e o tema.</summary>
    public required string RequestText { get; init; }

    /// <summary>Relação mínima para fechar apoio (o "preço" além do pedido).</summary>
    public required int Price { get; init; }

    /// <summary>Candidato com quem está (id), ou null se neutra.</summary>
    public string? AlignedWith { get; set; }

    /// <summary>O jogador já sabe que ela existe.</summary>
    public bool Known { get; set; }

    /// <summary>O jogador já ouviu o pedido.</summary>
    public bool RequestKnown { get; set; }

    /// <summary>O pedido foi atendido (promessa aceita ou negociação bem-sucedida).</summary>
    public bool RequestMet { get; set; }

    public string? PromiseId { get; set; }

    /// <summary>Favor de cada rival com ela (0 a 100).</summary>
    public Dictionary<string, double> Favor { get; init; } = [];

    /// <summary>Lideranças com quem disputa o mesmo público na mesma zona.</summary>
    public List<string> Rivals { get; init; } = [];
}

/// <summary>
/// A cidade em foco como lugar com gente (GDD 17.2): pessoas com memória e lideranças. Fica no estado do jogo, não
/// no da campanha, porque atravessa eleições e mandatos.
/// </summary>
public sealed class CityState
{
    public required int Municipio { get; init; }

    public List<People.Person> People { get; init; } = [];

    public List<Leader> Leaders { get; init; } = [];

    public People.Person? Person(string id) => People.FirstOrDefault(p => p.Id == id);

    public Leader? Leader(string personId) => Leaders.FirstOrDefault(l => l.PersonId == personId);
}
