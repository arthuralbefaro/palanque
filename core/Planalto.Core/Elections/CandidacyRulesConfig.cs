namespace Planalto.Core.Elections;

/// <summary>
/// Condições de elegibilidade por cargo. Padrão: idade mínima da CF, art. 14, § 3º, VI (18 anos para vereador;
/// 21 para prefeito e deputados; 30 para governador; 35 para senador e presidente), aferida na data da posse,
/// salvo quando o mínimo é 18 anos: aí vale a data-limite do pedido de registro (Lei 9.504, art. 11, § 2º),
/// que é 15 de agosto do ano da eleição (art. 11, caput).
/// </summary>
public sealed class CandidacyRulesConfig
{
    public static CandidacyRulesConfig Default { get; } = new();

    public IReadOnlyDictionary<Office, int> MinimumAge { get; init; } = new Dictionary<Office, int>
    {
        [Office.Vereador] = 18,
        [Office.Prefeito] = 21,
        [Office.DeputadoEstadual] = 21,
        [Office.DeputadoDistrital] = 21,
        [Office.DeputadoFederal] = 21,
        [Office.Governador] = 30,
        [Office.Senador] = 35,
        [Office.Presidente] = 35,
    };

    public int MinimumAgeFor(Office office) => MinimumAge.GetValueOrDefault(office, 18);

    /// <summary>Idade mínima aferida no registro, e não na posse (art. 11, § 2º).</summary>
    public int AgeCheckedAtRegistration { get; init; } = 18;

    public int RegistrationDeadlineMonth { get; init; } = 8;

    public int RegistrationDeadlineDay { get; init; } = 15;

    /// <summary>Data em que a idade mínima do cargo é aferida numa eleição com posse em <paramref name="posse"/>.</summary>
    public DateOnly AgeReferenceDate(Office office, int electionYear, DateOnly posse) =>
        MinimumAgeFor(office) == AgeCheckedAtRegistration ? new DateOnly(electionYear, RegistrationDeadlineMonth, RegistrationDeadlineDay) : posse;
}
