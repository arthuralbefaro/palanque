namespace Planalto.Core.People;

public enum PromiseState
{
    Aberta,
    Cumprida,
    Quebrada,
    Vencida,

    /// <summary>
    /// Extinta sem culpa de ninguém: a pessoa a quem foi feita morreu ou deixou a cidade e não há sucessor que a cobre
    /// (GDD 17.16). Não conta como cumprida nem como quebrada.
    /// </summary>
    Extinta,
}

/// <summary>Promessa do jogador: a quem, o quê, onde e até quando (GDD 17.2, peça 4).</summary>
public sealed class Promise
{
    public const string ParaPessoa = "pessoa";
    public const string ParaZona = "zona";
    public const string ParaGrupo = "grupo";

    public required string Id { get; init; }

    /// <summary>"pessoa", "zona" ou "grupo".</summary>
    public required string ToKind { get; init; }

    /// <summary>Id da pessoa, da zona ou do grupo de eleitores. Muda quando a cobrança passa ao sucessor de quem saiu (GDD 17.16).</summary>
    public required string ToId { get; set; }

    /// <summary>A quem a promessa foi feita, quando a cobrança passou a um sucessor.</summary>
    public string? OriginalToId { get; set; }

    /// <summary>Tipo do que foi prometido (id do catálogo de pedidos: obra, vaga, posição, presença…).</summary>
    public required string Object { get; init; }

    /// <summary>Texto por extenso, como aparece no Livro ("Cascalhar a estrada da zona Rural Leste").</summary>
    public required string Text { get; init; }

    public string? ZoneId { get; init; }

    public required DateOnly Made { get; init; }

    public required DateOnly Deadline { get; init; }

    public PromiseState State { get; set; } = PromiseState.Aberta;

    /// <summary>Quantas vezes já foi cobrada (GDD 17.14).</summary>
    public int Cobrancas { get; set; }
}

/// <summary>Dívida política: o jogador deve algo a alguém (apoio recebido, doação, favor).</summary>
public sealed class Debt
{
    public required string Id { get; init; }

    public required string ToPersonId { get; init; }

    public required string Text { get; init; }

    public required DateOnly Made { get; init; }

    public bool Settled { get; set; }

    /// <summary>Encerrada sem pagamento porque o credor morreu ou deixou a cidade (GDD 17.16); fica também como <see cref="Settled"/>.</summary>
    public bool Extinct { get; set; }
}

/// <summary>Posição assumida em público: tema, valor, quando e onde foi dito.</summary>
public sealed record StatedPosition(string Theme, int Value, DateOnly Date, string Where);

/// <summary>Esqueleto: irregularidade com gravidade e a lista de quem sabe (ids de pessoas).</summary>
public sealed class LedgerSkeleton
{
    public required string Id { get; init; }

    public required string Type { get; init; }

    public required string Text { get; init; }

    public required int Severity { get; init; }

    public required DateOnly Created { get; init; }

    public List<string> KnownBy { get; init; } = [];

    public bool Exposed { get; set; }
}

/// <summary>
/// Livro de compromissos (GDD 17.2): o que fica. É lido pela cobrança no mandato, pelos ataques dos rivais, pela
/// mídia e pela prestação de contas, e atravessa eleições (fica no estado do jogo, não no da campanha).
/// </summary>
public sealed class Ledger
{
    public List<Promise> Promises { get; init; } = [];

    public List<Debt> Debts { get; init; } = [];

    public List<StatedPosition> Positions { get; init; } = [];

    public List<LedgerSkeleton> Skeletons { get; init; } = [];

    public int NextId { get; set; } = 1;

    public string NewId(string prefix) => $"{prefix}{NextId++}";

    public Promise AddPromise(string toKind, string toId, string obj, string text, string? zoneId, DateOnly made, DateOnly deadline)
    {
        var promise = new Promise { Id = NewId("p"), ToKind = toKind, ToId = toId, Object = obj, Text = text, ZoneId = zoneId, Made = made, Deadline = deadline };
        Promises.Add(promise);
        return promise;
    }

    public Debt AddDebt(string toPersonId, string text, DateOnly made)
    {
        var debt = new Debt { Id = NewId("d"), ToPersonId = toPersonId, Text = text, Made = made };
        Debts.Add(debt);
        return debt;
    }

    public LedgerSkeleton AddSkeleton(string type, string text, int severity, DateOnly created, IEnumerable<string> knownBy)
    {
        var skeleton = new LedgerSkeleton { Id = NewId("e"), Type = type, Text = text, Severity = severity, Created = created, KnownBy = knownBy.Distinct().ToList() };
        Skeletons.Add(skeleton);
        return skeleton;
    }

    public IEnumerable<Promise> Open => Promises.Where(p => p.State == PromiseState.Aberta);

    /// <summary>Promessas abertas cujo prazo passou viram vencidas; devolve as que venceram agora.</summary>
    public List<Promise> Expire(DateOnly today)
    {
        List<Promise> expired = Promises.Where(p => p.State == PromiseState.Aberta && p.Deadline < today).ToList();
        expired.ForEach(p => p.State = PromiseState.Vencida);
        return expired;
    }
}
