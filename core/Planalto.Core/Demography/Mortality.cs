using Planalto.Core.Characters;
using Planalto.Core.State;

namespace Planalto.Core.Demography;

/// <summary>Chance de morte de uma pessoa e a tábua de onde ela saiu.</summary>
/// <param name="Anual">Q(tábua, sexo, idade) × fator pessoal, limitada a 1.</param>
/// <param name="PorSorteio">Chance de cada sorteio (mensal no padrão) que, repetida, recompõe a anual.</param>
/// <param name="Tabua">Chave da tábua usada: a UF pedida ou "BR" quando a da UF faltou. Vai para o save.</param>
/// <param name="QTabua">Probabilidade anual da tábua, antes do fator pessoal.</param>
/// <param name="FatorPessoal">Fator aplicado, já dentro dos limites da configuração.</param>
public readonly record struct MortalityChance(double Anual, double PorSorteio, string Tabua, double QTabua, double FatorPessoal);

/// <summary>Chance de morte por pessoa (GDD 17.16). Funções puras; o sorteio usa o Rng do Core.</summary>
public static class Mortality
{
    /// <summary>
    /// Probabilidade anual da tábua para a idade. Até a última idade simples, é o qx gravado; depois dela, a curva é
    /// prolongada pelo parâmetro de jogo: qx da última idade × (1 + crescimento) a cada ano a mais, até o teto.
    /// </summary>
    public static double Q(TabuaSexo tabua, int idade, CurvaAposTabuaConfig curva)
    {
        int last = tabua.Qx.Count - 1;
        if (idade <= last)
        {
            return tabua.Qx[Math.Max(0, idade)];
        }

        // A curva só sobe: o teto não derruba uma tábua cuja última idade já esteja acima dele.
        double extended = tabua.Qx[last] * Math.Pow(1 + curva.CrescimentoAnual, idade - last);
        return Math.Min(1, Math.Max(tabua.Qx[last], Math.Min(curva.TetoAnual, extended)));
    }

    /// <summary>Chance de morte da pessoa: tábua da UF (ou do Brasil, se faltar), do sexo e da idade, vezes o fator pessoal.</summary>
    public static MortalityChance Chance(MortalityTable table, DemographyConfig config, string? uf, Gender gender, int idade, double fatorPessoal)
    {
        var (local, used) = table.Para(uf);
        double q = Q(local.Para(gender), idade, config.CurvaAposTabua);
        double factor = Math.Clamp(fatorPessoal, config.FatorPessoal.Minimo, config.FatorPessoal.Maximo);
        double annual = Math.Min(1, q * factor);
        return new MortalityChance(annual, PorSorteio(annual, config.SorteiosPorAno), used, q, factor);
    }

    /// <summary>O mesmo, com o fator pessoal calculado pela saúde e pelo estresse (0 a 100).</summary>
    public static MortalityChance Chance(MortalityTable table, DemographyConfig config, string? uf, Gender gender, int idade, int saude, int estresse) =>
        Chance(table, config, uf, gender, idade, config.Fator(saude, estresse));

    /// <summary>Chance mensal equivalente à anual: 1 − (1 − anual)^(1/12).</summary>
    public static double Mensal(double anual) => PorSorteio(anual, 12);

    /// <summary>Chance de cada um dos sorteios do ano que, juntos, dão a chance anual.</summary>
    public static double PorSorteio(double anual, int sorteiosPorAno) =>
        1 - Math.Pow(1 - Math.Clamp(anual, 0, 1), 1.0 / sorteiosPorAno);

    /// <summary>Sorteio de um período (um mês, no padrão) pelo Rng do Core.</summary>
    public static bool Sorteia(Rng rng, MortalityChance chance) => rng.Chance(chance.PorSorteio);
}
