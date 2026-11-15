namespace Planalto.Core.Elections;

/// <summary>
/// Divisão do Fundo Especial de Financiamento de Campanha (Lei 9.504/1997, art. 16-D) e do horário
/// eleitoral gratuito (art. 47, § 2º). O padrão reproduz a lei vigente.
/// </summary>
public sealed record PartyFundingRulesConfig
{
    public static PartyFundingRulesConfig Default { get; } = new();

    /// <summary>Art. 16-D, I: dividido igualmente entre todos os partidos registrados.</summary>
    public double FundEqualShare { get; init; } = 0.02;

    /// <summary>Art. 16-D, II: entre os partidos com ao menos um deputado, pelos votos para a Câmara na última eleição geral.</summary>
    public double FundCamaraVotesShare { get; init; } = 0.35;

    /// <summary>Art. 16-D, III: pela bancada na Câmara.</summary>
    public double FundCamaraSeatsShare { get; init; } = 0.48;

    /// <summary>Art. 16-D, IV: pela bancada no Senado.</summary>
    public double FundSenadoSeatsShare { get; init; } = 0.15;

    /// <summary>Art. 47, § 2º, I: pela bancada na Câmara.</summary>
    public double TvCamaraSeatsShare { get; init; } = 0.90;

    /// <summary>Art. 47, § 2º, II: dividido igualmente.</summary>
    public double TvEqualShare { get; init; } = 0.10;

    internal void Validate()
    {
        if (Math.Abs(FundEqualShare + FundCamaraVotesShare + FundCamaraSeatsShare + FundSenadoSeatsShare - 1) > 1e-9
            || Math.Abs(TvCamaraSeatsShare + TvEqualShare - 1) > 1e-9)
        {
            throw new ArgumentException("As parcelas do fundo e do tempo de TV devem somar 100%.");
        }
    }
}

/// <summary>Frações (somando 1) do fundo eleitoral e do tempo de TV de cada partido.</summary>
public static class PartyFunding
{
    public static Dictionary<string, double> ElectoralFundShares(
        IReadOnlyCollection<string> parties,
        IReadOnlyDictionary<string, long> camaraVotes,
        IReadOnlyDictionary<string, int> camaraSeats,
        IReadOnlyDictionary<string, int> senadoSeats,
        PartyFundingRulesConfig? rules = null)
    {
        rules ??= PartyFundingRulesConfig.Default;
        rules.Validate();

        var withDeputy = parties.Where(p => camaraSeats.GetValueOrDefault(p) > 0).ToList();
        var shares = parties.ToDictionary(p => p, _ => rules.FundEqualShare / parties.Count, StringComparer.Ordinal);
        AddProportional(shares, withDeputy, p => camaraVotes.GetValueOrDefault(p), rules.FundCamaraVotesShare);
        AddProportional(shares, parties, p => camaraSeats.GetValueOrDefault(p), rules.FundCamaraSeatsShare);
        AddProportional(shares, parties, p => senadoSeats.GetValueOrDefault(p), rules.FundSenadoSeatsShare);
        return Normalize(shares);
    }

    public static Dictionary<string, double> TvTimeShares(
        IReadOnlyCollection<string> parties,
        IReadOnlyDictionary<string, int> camaraSeats,
        PartyFundingRulesConfig? rules = null)
    {
        rules ??= PartyFundingRulesConfig.Default;
        rules.Validate();

        var shares = parties.ToDictionary(p => p, _ => rules.TvEqualShare / parties.Count, StringComparer.Ordinal);
        AddProportional(shares, parties, p => camaraSeats.GetValueOrDefault(p), rules.TvCamaraSeatsShare);
        return Normalize(shares);
    }

    private static void AddProportional(Dictionary<string, double> shares, IReadOnlyCollection<string> eligible, Func<string, double> weight, double portion)
    {
        double total = eligible.Sum(weight);
        if (total <= 0)
        {
            // Sem base para ratear (ex.: ninguém com cadeira): a parcela vai por igual a todos.
            foreach (string party in shares.Keys.ToList())
            {
                shares[party] += portion / shares.Count;
            }

            return;
        }

        foreach (string party in eligible)
        {
            shares[party] += portion * weight(party) / total;
        }
    }

    private static Dictionary<string, double> Normalize(Dictionary<string, double> shares)
    {
        double sum = shares.Values.Sum();
        return shares.ToDictionary(p => p.Key, p => p.Value / sum, StringComparer.Ordinal);
    }
}
