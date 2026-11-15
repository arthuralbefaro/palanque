namespace Planalto.Core.Elections;

/// <summary>
/// Parâmetros da apuração proporcional que vêm de lei recente (Lei 14.211/2021 e decisão do STF
/// nas ADIs 7228, 7263 e 7325). O padrão reproduz a regra vigente; mudar aqui não exige mexer no algoritmo.
/// </summary>
public sealed record ProportionalRulesConfig
{
    public static ProportionalRulesConfig Default { get; } = new();

    /// <summary>Votação nominal mínima para ocupar vaga do quociente partidário (Código Eleitoral, art. 108).</summary>
    public int CandidateMinimumPercent { get; init; } = 10;

    /// <summary>Votação mínima do partido para disputar as sobras (art. 109, §2º).</summary>
    public int PartyRemainderMinimumPercent { get; init; } = 80;

    /// <summary>Votação nominal mínima do candidato para ocupar uma sobra (art. 109, §2º).</summary>
    public int CandidateRemainderMinimumPercent { get; init; } = 20;

    /// <summary>
    /// 3ª etapa (art. 109, III). True: todos os partidos disputam pelas maiores médias, sem cláusula
    /// de desempenho, como decidiu o STF. False: só disputam os partidos que atingiram
    /// <see cref="PartyRemainderMinimumPercent"/>. Em ambos os casos não há piso individual.
    /// </summary>
    public bool FinalStageOpenToAllParties { get; init; } = true;

    internal void Validate()
    {
        CheckPercent(CandidateMinimumPercent, nameof(CandidateMinimumPercent));
        CheckPercent(PartyRemainderMinimumPercent, nameof(PartyRemainderMinimumPercent));
        CheckPercent(CandidateRemainderMinimumPercent, nameof(CandidateRemainderMinimumPercent));
    }

    private static void CheckPercent(int value, string name)
    {
        if (value is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(name, value, "Percentual deve estar entre 0 e 100.");
        }
    }
}
