using Planalto.Core.Data;

namespace Planalto.Core.Campaign;

/// <summary>Tipo de objetivo que o gerador sabe propor (GDD 17.2, peça 6).</summary>
public sealed class ObjectiveType
{
    public const string PresencaZona = "presenca_zona";
    public const string PassarRival = "passar_rival";
    public const string TrazerLideranca = "trazer_lideranca";
    public const string JuntarCaixa = "juntar_caixa";
    public const string PosicaoConhecida = "posicao_conhecida";
    public const string CumprirPromessa = "cumprir_promessa";
    public const string MontarEquipe = "montar_equipe";

    public static readonly IReadOnlyList<string> Ids = [PresencaZona, PassarRival, TrazerLideranca, JuntarCaixa, PosicaoConhecida, CumprirPromessa, MontarEquipe];

    public required string Id { get; init; }

    /// <summary>Nome do tipo (até 28 caracteres).</summary>
    public required string Nome { get; init; }

    public required double Weight { get; init; }

    /// <summary>Prazo em semanas (0 = o prazo vem do próprio alvo, como a promessa).</summary>
    public required int PrazoSemanas { get; init; }

    /// <summary>Quanto a meta pede além do valor de hoje (pontos de presença, fração conhecida, reais no porte médio).</summary>
    public required double Passo { get; init; }

    /// <summary>Texto da meta; aceita {valor}, {alvo} e {prazo}.</summary>
    public required string Texto { get; init; }

    /// <summary>De onde a meta veio; aceita {alvo}, {atual}, {nome}, {papel}, {zona}, {eleitores}, {semanas}.</summary>
    public required string Origem { get; init; }

    /// <summary>Origem quando a meta nasce do conselho do coordenador.</summary>
    public string? OrigemConselho { get; init; }
}

public sealed class ObjectiveRules
{
    /// <summary>Objetivos ativos ao mesmo tempo.</summary>
    public required int Maximo { get; init; }

    /// <summary>Semanas que uma proposta não fixada fica no painel.</summary>
    public required int ValidadePropostaSemanas { get; init; }

    /// <summary>Lealdade que cada membro da equipe ganha quando um objetivo é cumprido.</summary>
    public required int LealdadeEquipe { get; init; }

    public required int Historico { get; init; }

    public required double PresencaTeto { get; init; }

    public required double ConhecidaTeto { get; init; }
}

/// <summary>data/campaign/objectives.json.</summary>
public sealed class ObjectiveCatalog : IValidatable
{
    public const string File = "campaign/objectives.json";

    public required string Note { get; init; }

    public required ObjectiveRules Regras { get; init; }

    /// <summary>Lembrança positiva em quem se beneficiou de um objetivo cumprido.</summary>
    public required MemoryDefinition Lembranca { get; init; }

    public required List<ObjectiveType> Tipos { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public ObjectiveType Type(string id) => Tipos.First(t => t.Id == id);

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Tipos.Select(t => t.Id), "Tipo de objetivo"))
        {
            yield return error;
        }

        foreach (string id in ObjectiveType.Ids.Where(id => Tipos.All(t => t.Id != id)))
        {
            yield return $"falta o tipo de objetivo '{id}'.";
        }

        foreach (ObjectiveType t in Tipos)
        {
            if (t.Nome.Length > 28 || t.Weight <= 0 || t.PrazoSemanas < 0 || t.Passo < 0 || string.IsNullOrWhiteSpace(t.Texto) || string.IsNullOrWhiteSpace(t.Origem))
            {
                yield return $"tipo '{t.Id}': nome com até 28 caracteres, peso positivo, prazo e passo não negativos, texto e origem.";
            }

            if (!t.Texto.Contains("{prazo}", StringComparison.Ordinal))
            {
                yield return $"tipo '{t.Id}': o texto precisa de {{prazo}}.";
            }
        }

        ObjectiveRules r = Regras;
        if (r.Maximo is < 1 or > 3 || r.ValidadePropostaSemanas < 1 || r.Historico < 1 || r.PresencaTeto is <= 0 or > 100 || r.ConhecidaTeto is <= 0 or > 1)
        {
            yield return "regras: de 1 a 3 objetivos ativos e limites válidos.";
        }

        if (Lembranca.Peso is < 1 or > 3 || Lembranca.Pontos <= 0)
        {
            yield return "lembranca: peso e pontos positivos.";
        }

        foreach (string key in new[] { "cumprido", "vencido" }.Where(k => !Textos.ContainsKey(k)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

public enum ObjectiveStatus
{
    Ativo,
    Cumprido,
    Vencido,
    Dispensado,
    Retirado,
}

/// <summary>Meta secundária com condição mensurável, prazo e origem (GDD 17.1, princípio h).</summary>
public sealed class Objective
{
    public required string Id { get; init; }

    /// <summary>Tipo (id de data/campaign/objectives.json).</summary>
    public required string Kind { get; init; }

    public required string Text { get; init; }

    /// <summary>De onde a meta veio, por extenso.</summary>
    public required string Origin { get; init; }

    /// <summary>Alvo da condição: zona, rival, liderança, tema, promessa ou função, conforme o tipo.</summary>
    public required string TargetId { get; init; }

    /// <summary>Valor que a medida precisa alcançar.</summary>
    public required double Goal { get; init; }

    /// <summary>Valor da medida quando a meta foi proposta (para o progresso).</summary>
    public required double Start { get; init; }

    public required DateOnly Created { get; init; }

    public required DateOnly Deadline { get; init; }

    /// <summary>Quem se beneficia quando a meta é cumprida (ganha a lembrança).</summary>
    public string? BeneficiaryId { get; set; }

    public bool Pinned { get; set; }

    public ObjectiveStatus Status { get; set; }

    public DateOnly? Closed { get; set; }
}

public sealed class ObjectiveState
{
    public List<Objective> Active { get; init; } = [];

    public List<Objective> History { get; init; } = [];

    public int NextId { get; set; } = 1;
}

/// <summary>Um objetivo ativo como o painel mostra: a medida de hoje, a meta e o progresso (0 a 1).</summary>
public sealed record ObjectiveView(Objective Objective, string TypeName, double Current, double Goal, double Progress, bool Done, int DaysLeft);
