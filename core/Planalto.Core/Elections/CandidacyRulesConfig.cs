namespace Planalto.Core.Elections;

/// <summary>
/// Condições de elegibilidade por cargo. Padrão: idade mínima da CF, art. 14, § 3º, VI, verificada na data da
/// posse (Lei 9.504, art. 11, § 2º): 18 anos para vereador; 21 para prefeito e deputados; 30 para governador;
/// 35 para senador e presidente.
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
}
