using Planalto.Core.Data;

namespace Planalto.Core.Demography;

/// <summary>Fator pessoal sobre a tábua, conforme saúde e estresse (parâmetro de jogo, GDD 17.16 e 17.20, item 22).</summary>
public sealed class FatorPessoalConfig
{
    /// <summary>Fator de quem tem saúde plena e nenhum estresse.</summary>
    public required double Minimo { get; init; }

    /// <summary>Fator de quem tem saúde zerada e estresse no máximo.</summary>
    public required double Maximo { get; init; }

    /// <summary>Peso da saúde que falta (100 − saúde) no risco; soma 1 com o peso do estresse.</summary>
    public required double PesoSaude { get; init; }

    public required double PesoEstresse { get; init; }

    /// <summary>Risco (0 a 1) em que o fator é 1, ou seja, a pessoa morre como a tábua diz.</summary>
    public required double RiscoNeutro { get; init; }
}

/// <summary>Prolongamento da curva depois da última idade simples da tábua (parâmetro de jogo).</summary>
public sealed class CurvaAposTabuaConfig
{
    /// <summary>Aumento proporcional da probabilidade anual a cada ano de idade além da última idade simples.</summary>
    public required double CrescimentoAnual { get; init; }

    /// <summary>Teto da probabilidade anual da tábua, antes do fator pessoal.</summary>
    public required double TetoAnual { get; init; }
}

/// <summary>Parâmetros de jogo da mortalidade (data/demography/config.json). Não são dados: as tábuas estão em mortality.json.</summary>
public sealed class DemographyConfig : IValidatable
{
    public const string File = "demography/config.json";

    public required string Note { get; init; }

    public required FatorPessoalConfig FatorPessoal { get; init; }

    public required CurvaAposTabuaConfig CurvaAposTabua { get; init; }

    /// <summary>Sorteios de morte por ano para cada pessoa; 12 é o sorteio mensal do GDD 17.16.</summary>
    public required int SorteiosPorAno { get; init; }

    public IEnumerable<string> Validate()
    {
        if (FatorPessoal is null || CurvaAposTabua is null)
        {
            yield return "fator_pessoal e curva_apos_tabua são obrigatórios.";
            yield break;
        }

        FatorPessoalConfig f = FatorPessoal;
        if (!(f.Minimo > 0 && f.Minimo <= 1 && f.Maximo >= 1))
        {
            yield return "fator_pessoal: mínimo entre 0 e 1 e máximo de 1 ou mais.";
        }

        if (f.PesoSaude < 0 || f.PesoEstresse < 0 || Math.Abs(f.PesoSaude + f.PesoEstresse - 1) > 1e-9)
        {
            yield return "fator_pessoal: peso_saude e peso_estresse não negativos, somando 1.";
        }

        if (!(f.RiscoNeutro > 0 && f.RiscoNeutro < 1))
        {
            yield return "fator_pessoal: risco_neutro maior que 0 e menor que 1.";
        }

        if (CurvaAposTabua.CrescimentoAnual < 0 || !(CurvaAposTabua.TetoAnual > 0 && CurvaAposTabua.TetoAnual <= 1))
        {
            yield return "curva_apos_tabua: crescimento_anual não negativo e teto_anual entre 0 e 1.";
        }

        if (SorteiosPorAno < 1)
        {
            yield return "sorteios_por_ano deve ser 1 ou mais.";
        }
    }

    /// <summary>
    /// Fator pessoal para a saúde e o estresse (0 a 100) da pessoa. O risco é a média ponderada da saúde que falta e
    /// do estresse; o fator vai do mínimo (risco 0) a 1 (risco neutro) e de 1 ao máximo (risco 1), em dois trechos retos.
    /// </summary>
    public double Fator(int saude, int estresse)
    {
        FatorPessoalConfig f = FatorPessoal;
        double risk = (f.PesoSaude * (100 - Math.Clamp(saude, 0, 100)) / 100.0) + (f.PesoEstresse * Math.Clamp(estresse, 0, 100) / 100.0);
        return risk <= f.RiscoNeutro
            ? f.Minimo + ((1 - f.Minimo) * risk / f.RiscoNeutro)
            : 1 + ((f.Maximo - 1) * (risk - f.RiscoNeutro) / (1 - f.RiscoNeutro));
    }
}
