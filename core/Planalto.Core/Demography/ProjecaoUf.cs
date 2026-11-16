using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.Core.Demography;

/// <summary>
/// População total projetada do Brasil e de cada UF, ano a ano (data/demography/projecao_uf.json; IBGE, Projeções da
/// População, revisão 2024). É o alvo a que a soma dos municípios de cada UF é reescalada (GDD 17.17, "População").
/// </summary>
public sealed class ProjecaoUf : IValidatable
{
    public const string File = "demography/projecao_uf.json";

    public const string Brasil = "BR";

    public required string Note { get; init; }

    public required int AnoInicial { get; init; }

    public required int AnoFinal { get; init; }

    /// <summary>População em 1º de julho de cada ano, de ano_inicial a ano_final, por sigla ("BR" e as UFs).</summary>
    public required Dictionary<string, List<long>> Populacao { get; init; }

    /// <summary>População publicada para o local e o ano; null fora da série ou sem o local.</summary>
    public long? Publicada(string local, int ano) =>
        ano >= AnoInicial && ano <= AnoFinal && Populacao.TryGetValue(local, out List<long>? serie) ? serie[ano - AnoInicial] : null;

    /// <summary>
    /// Alvo para qualquer ano: o valor publicado dentro da série; antes dela, o do primeiro ano; depois dela, o do
    /// último ano levado adiante pela taxa do último ano publicado. Null se o local não está no arquivo.
    /// </summary>
    public double? Alvo(string local, int ano)
    {
        if (!Populacao.TryGetValue(local, out List<long>? serie))
        {
            return null;
        }

        if (ano <= AnoFinal)
        {
            return serie[Math.Max(ano, AnoInicial) - AnoInicial];
        }

        double lastGrowth = (double)serie[^1] / serie[^2];
        return serie[^1] * Math.Pow(lastGrowth, ano - AnoFinal);
    }

    public IEnumerable<string> Validate()
    {
        int years = AnoFinal - AnoInicial + 1;
        if (years < 2 || Populacao is null || !Populacao.ContainsKey(Brasil))
        {
            yield return $"série de pelo menos dois anos e com o total do Brasil ('{Brasil}').";
            yield break;
        }

        foreach (var (local, serie) in Populacao)
        {
            if (serie is null || serie.Count != years || serie.Any(p => p <= 0))
            {
                yield return $"populacao.{local}: um valor positivo por ano, de {AnoInicial} a {AnoFinal}.";
                yield break;
            }
        }

        for (int i = 0; i < years; i++)
        {
            long sum = Populacao.Where(p => p.Key != Brasil).Sum(p => p.Value[i]);
            if (sum != Populacao[Brasil][i])
            {
                yield return $"populacao: em {AnoInicial + i}, a soma das UFs ({sum}) difere do total do Brasil ({Populacao[Brasil][i]}).";
            }
        }
    }
}

/// <summary>Procedência dos arquivos de data/demography/ (data/demography/_fontes.json).</summary>
public sealed class FontesDemografia : IValidatable
{
    public const string File = "demography/_fontes.json";

    public required DateTimeOffset GeradoEm { get; init; }

    public required List<FonteCampo> Fontes { get; init; }

    /// <summary>Como as tábuas das UFs chegam à idade simples.</summary>
    public required string Metodo { get; init; }

    /// <summary>Valores de conferência da leitura da tábua completa do Brasil (Q(X) por mil), como lidos.</summary>
    public required Dictionary<string, double> Conferencia { get; init; }

    public required List<string> Observacoes { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Fontes is null || Fontes.Count == 0)
        {
            yield return "a lista 'fontes' está vazia.";
            yield break;
        }

        foreach (FonteCampo fonte in Fontes)
        {
            string where = $"fontes '{fonte?.Campo}'";
            if (fonte is null)
            {
                yield return "fontes: item null.";
                continue;
            }

            foreach (string error in DataChecks.Text(fonte.Campo, "campo", where)
                .Concat(DataChecks.Text(fonte.Fonte, "fonte", where))
                .Concat(DataChecks.Text(fonte.Url, "url", where)))
            {
                yield return error;
            }
        }

        foreach (string error in DataChecks.Text(Metodo, "metodo", "_fontes.json"))
        {
            yield return error;
        }
    }
}
