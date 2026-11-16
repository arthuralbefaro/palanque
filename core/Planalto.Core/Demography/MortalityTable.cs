using Planalto.Core.Characters;
using Planalto.Core.Data;

namespace Planalto.Core.Demography;

/// <summary>Faixa etária da tábua abreviada publicada: probabilidade de morte entre as idades exatas X e X+N.</summary>
public sealed record FaixaPublicada
{
    public required int Idade { get; init; }

    public required int Amplitude { get; init; }

    public required double Nqx { get; init; }
}

/// <summary>O que a fonte publica para o grupo aberto (90 anos ou mais), onde não há idade simples.</summary>
public sealed record GrupoAberto
{
    public required int Idade { get; init; }

    /// <summary>Taxa central de mortalidade do grupo (óbitos por pessoa-ano vivido).</summary>
    public required double TaxaCentral { get; init; }

    /// <summary>Expectativa de vida à idade inicial do grupo, em anos.</summary>
    public required double ExpectativaDeVida { get; init; }
}

/// <summary>Tábua de um local e de um sexo: qx por idade simples, o grupo aberto e as faixas publicadas.</summary>
public sealed class TabuaSexo
{
    /// <summary>Probabilidade de morte entre as idades exatas X e X+1, em proporção; o índice é a idade.</summary>
    public required List<double> Qx { get; init; }

    public required GrupoAberto GrupoAberto { get; init; }

    public required List<FaixaPublicada> Faixas { get; init; }

    /// <summary>Probabilidade da faixa recomposta pelas idades simples: 1 − Π(1 − qx).</summary>
    public double Recomposta(FaixaPublicada faixa)
    {
        double survival = 1;
        for (int age = faixa.Idade; age < faixa.Idade + faixa.Amplitude; age++)
        {
            survival *= 1 - Qx[age];
        }

        return 1 - survival;
    }
}

public sealed class TabuaLocal
{
    public required TabuaSexo Masculino { get; init; }

    public required TabuaSexo Feminino { get; init; }

    public required TabuaSexo Ambos { get; init; }

    /// <summary>Pessoa não binária usa a tábua de ambos os sexos (GDD 17.16).</summary>
    public TabuaSexo Para(Gender gender) => gender switch
    {
        Gender.Masculino => Masculino,
        Gender.Feminino => Feminino,
        _ => Ambos,
    };

    public IEnumerable<(string Sexo, TabuaSexo Tabua)> PorSexo()
    {
        yield return ("masculino", Masculino);
        yield return ("feminino", Feminino);
        yield return ("ambos", Ambos);
    }
}

/// <summary>
/// Tábuas de mortalidade de data/demography/mortality.json (GDD 17.16): Brasil e as 27 UFs, por sexo, em idade simples
/// de 0 a 89 anos. A do Brasil é a tábua completa publicada pelo IBGE; as das UFs repartem cada faixa da tábua
/// abreviada das Projeções da População pelo perfil da tábua do Brasil, de modo que a faixa recomposta seja a publicada.
/// </summary>
public sealed class MortalityTable : IValidatable
{
    public const string File = "demography/mortality.json";

    /// <summary>Chave da tábua do Brasil, usada quando a da UF falta.</summary>
    public const string Brasil = "BR";

    /// <summary>
    /// Diferença máxima entre a faixa recomposta e a publicada nas tábuas das UFs. O arquivo guarda os números com
    /// todas as casas do tipo double; a folga cobre só o arredondamento das contas em ponto flutuante.
    /// </summary>
    public const double ToleranciaRecomposicao = 1e-12;

    /// <summary>
    /// Diferença relativa máxima, na tábua do Brasil, entre a faixa recomposta pela tábua completa e a faixa da tábua
    /// abreviada das Projeções: são duas publicações do IBGE, e a completa é suavizada por idade simples.
    /// </summary>
    public const double ToleranciaBrasil = 0.01;

    public required string Note { get; init; }

    public required int Ano { get; init; }

    public required Dictionary<string, TabuaLocal> Tabuas { get; init; }

    /// <summary>Tábua da UF; se não houver, a do Brasil. Devolve também a chave da tábua usada, para o save.</summary>
    public (TabuaLocal Tabua, string Usada) Para(string? uf) =>
        uf is not null && Tabuas.TryGetValue(uf, out TabuaLocal? local) ? (local, uf) : (Tabuas[Brasil], Brasil);

    public IEnumerable<string> Validate()
    {
        if (Tabuas is null || !Tabuas.ContainsKey(Brasil))
        {
            yield return $"tabuas: falta a tábua do Brasil ('{Brasil}').";
            yield break;
        }

        foreach (var (local, tabua) in Tabuas)
        {
            if (tabua?.Masculino is null || tabua.Feminino is null || tabua.Ambos is null)
            {
                yield return $"tabuas.{local}: faltam sexos (masculino, feminino, ambos).";
                continue;
            }

            foreach (var (sexo, t) in tabua.PorSexo())
            {
                foreach (string error in Validate(t, $"tabuas.{local}.{sexo}", local == Brasil ? null : ToleranciaRecomposicao))
                {
                    yield return error;
                }
            }
        }
    }

    private static IEnumerable<string> Validate(TabuaSexo t, string where, double? tolerance)
    {
        if (t.Qx is null || t.GrupoAberto is null || t.Faixas is null || t.Qx.Count == 0 || t.Qx.Count != t.GrupoAberto.Idade)
        {
            yield return $"{where}: qx deve ter uma probabilidade por idade, de 0 até a idade anterior ao grupo aberto.";
            yield break;
        }

        if (t.Qx.Any(q => !(q > 0 && q <= 1)))
        {
            yield return $"{where}: todo qx deve ser maior que 0 e menor ou igual a 1.";
        }

        if (!(t.GrupoAberto.TaxaCentral > 0) || !(t.GrupoAberto.ExpectativaDeVida > 0))
        {
            yield return $"{where}: grupo_aberto com taxa central e expectativa de vida positivas.";
        }

        int next = 0;
        foreach (FaixaPublicada faixa in t.Faixas)
        {
            if (faixa.Idade != next || faixa.Amplitude < 1 || !(faixa.Nqx > 0 && faixa.Nqx <= 1))
            {
                yield return $"{where}: faixas contíguas a partir de 0, com amplitude de 1 ou mais e nqx entre 0 e 1 (faixa {faixa.Idade}).";
                yield break;
            }

            next += faixa.Amplitude;
        }

        if (next != t.Qx.Count)
        {
            yield return $"{where}: as faixas devem cobrir de 0 até o grupo aberto ({t.Qx.Count} anos); cobrem {next}.";
            yield break;
        }

        if (tolerance is { } limit)
        {
            foreach (FaixaPublicada faixa in t.Faixas.Where(f => Math.Abs(t.Recomposta(f) - f.Nqx) > limit))
            {
                yield return $"{where}: a faixa de {faixa.Idade} anos recomposta ({t.Recomposta(faixa):R}) difere da publicada ({faixa.Nqx:R}).";
            }
        }
    }
}
