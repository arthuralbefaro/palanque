using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Planalto.Core.Indicators;

namespace Planalto.DataImporter.Rd;

/// <summary>Lê do cache as respostas do SICONFI baixadas por <see cref="SiconfiDownload"/> e monta as finanças de um ente.</summary>
public static partial class SiconfiRead
{
    private const string ContaReceitasCorrentes = "RO1.0.0.0.00.0.0";
    private const string ColunaReceitaBruta = "Receitas Brutas Realizadas";
    private const string ColunaEmpenhadas = "Despesas Empenhadas";

    /// <summary>Finanças do ente no exercício mais recente com DCA entregue; null se não há DCA em nenhum dos exercícios.</summary>
    /// <param name="receitaPlausivel">
    /// Confere a receita corrente declarada; a DCA reprovada é descartada (declaração com erro evidente) e vale a do
    /// exercício anterior. Os exercícios descartados e o valor declarado voltam em <paramref name="descartadas"/>.
    /// </param>
    public static FinancasEnte? Read(string cacheDirectory, int ente, Func<decimal, bool>? receitaPlausivel = null, List<(int Exercicio, decimal Receita)>? descartadas = null)
    {
        foreach (int ano in SiconfiDownload.Exercicios)
        {
            string receitas = SiconfiDownload.DcaFile(cacheDirectory, "I-C", ano, ente);
            string despesas = SiconfiDownload.DcaFile(cacheDirectory, "I-E", ano, ente);
            if (!File.Exists(receitas) || !File.Exists(despesas))
            {
                continue;
            }

            decimal? receitaCorrente = ReceitaCorrente(receitas);
            Dictionary<string, decimal> funcoes = DespesaPorFuncao(despesas);
            if (receitaCorrente is not > 0 || funcoes.Count == 0)
            {
                continue;
            }

            if (receitaPlausivel is not null && !receitaPlausivel(receitaCorrente.Value))
            {
                descartadas?.Add((ano, receitaCorrente.Value));
                continue;
            }

            var (rcl, pessoal, rgfAno) = Rgf(cacheDirectory, ente);
            return new FinancasEnte
            {
                Fonte = ano == SiconfiDownload.Exercicios[0] ? FinancasEnte.Declarado : FinancasEnte.ExercicioAnterior,
                Exercicio = ano,
                ReceitaCorrente = receitaCorrente.Value,
                DespesaPorFuncao = funcoes,
                ReceitaCorrenteLiquida = rcl,
                PessoalPctRcl = pessoal,
                RgfExercicio = rgfAno,
            };
        }

        return null;
    }

    /// <summary>True se todos os arquivos esperados do ente já estão no cache (o download terminou para ele).</summary>
    public static bool Downloaded(string cacheDirectory, int ente) =>
        new[] { "I-C", "I-E" }.All(anexo => SiconfiDownload.Exercicios.Any(ano => File.Exists(SiconfiDownload.DcaFile(cacheDirectory, anexo, ano, ente))))
        && SiconfiDownload.Exercicios.Any(ano => File.Exists(SiconfiDownload.RgfFile(cacheDirectory, ano, ente)));

    /// <summary>Receitas correntes brutas menos as deduções (colunas "Deduções - ...") da mesma conta.</summary>
    private static decimal? ReceitaCorrente(string file)
    {
        decimal? bruta = null;
        decimal deducoes = 0;
        foreach (var (coluna, codConta, _, valor) in Items(file))
        {
            if (codConta != ContaReceitasCorrentes)
            {
                continue;
            }

            if (coluna == ColunaReceitaBruta)
            {
                bruta = valor;
            }
            else if (coluna.StartsWith("Deduções", StringComparison.Ordinal))
            {
                deducoes += valor;
            }
        }

        return bruta - deducoes;
    }

    /// <summary>Despesa empenhada por função ("10 - Saúde" → "10"); subfunções ("10.301 - ...") ficam de fora.</summary>
    private static Dictionary<string, decimal> DespesaPorFuncao(string file)
    {
        var result = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (coluna, _, conta, valor) in Items(file))
        {
            if (coluna == ColunaEmpenhadas && Funcao().Match(conta) is { Success: true } m)
            {
                result[m.Groups[1].Value] = result.GetValueOrDefault(m.Groups[1].Value) + valor;
            }
        }

        return new Dictionary<string, decimal>(result, StringComparer.Ordinal);
    }

    private static (decimal? Rcl, decimal? PessoalPct, int? Ano) Rgf(string cacheDirectory, int ente)
    {
        foreach (int ano in SiconfiDownload.Exercicios)
        {
            string file = SiconfiDownload.RgfFile(cacheDirectory, ano, ente);
            if (!File.Exists(file))
            {
                continue;
            }

            decimal? rcl = null;
            decimal? pct = null;
            foreach (var (coluna, codConta, _, valor) in Items(file))
            {
                if (codConta == "ReceitaCorrenteLiquidaLimiteLegal" && coluna == "Valor")
                {
                    rcl = valor;
                }
                else if (codConta == "DespesaComPessoalTotal" && coluna.StartsWith("% sobre a RCL", StringComparison.OrdinalIgnoreCase))
                {
                    pct = valor;
                }
            }

            if (rcl is > 0 && pct is not null)
            {
                return (rcl, pct, ano);
            }
        }

        return (null, null, null);
    }

    private static IEnumerable<(string Coluna, string CodConta, string Conta, decimal Valor)> Items(string file)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file));
        foreach (JsonElement item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            if (item.GetProperty("valor").ValueKind != JsonValueKind.Number)
            {
                continue;
            }

            yield return (
                item.GetProperty("coluna").GetString() ?? string.Empty,
                item.GetProperty("cod_conta").GetString() ?? string.Empty,
                item.GetProperty("conta").GetString() ?? string.Empty,
                item.GetProperty("valor").GetDecimal());
        }
    }

    [GeneratedRegex(@"^(\d\d) - ", RegexOptions.CultureInvariant)]
    private static partial Regex Funcao();
}
