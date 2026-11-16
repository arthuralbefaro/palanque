using System.Globalization;
using Planalto.Core.Demography;
using Planalto.DataImporter.Rd;

namespace Planalto.DataImporter.Tests;

public class MortalityImportTests
{
    private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    [Fact]
    public void Repartir_segue_o_perfil_e_recompoe_exatamente_a_faixa_publicada()
    {
        double[] perfil = [0.012, 0.0008, 0.0006, 0.0005, 0.0004, 0.0003, 0.00028, 0.00024, 0.00022, 0.00021];
        List<FaixaPublicada> faixas =
        [
            new() { Idade = 0, Amplitude = 1, Nqx = 0.02 },
            new() { Idade = 1, Amplitude = 4, Nqx = 0.004 },
            new() { Idade = 5, Amplitude = 5, Nqx = 0.0009 },
        ];

        double[] qx = MortalityImport.Repartir(faixas, perfil);

        Assert.Equal(0.02, qx[0]);
        foreach (FaixaPublicada faixa in faixas)
        {
            double survival = 1;
            for (int age = faixa.Idade; age < faixa.Idade + faixa.Amplitude; age++)
            {
                survival *= 1 - qx[age];
            }

            Assert.Equal(faixa.Nqx, 1 - survival, 14);
        }

        // Mesmo perfil: a idade mais arriscada da faixa no perfil continua a mais arriscada.
        Assert.True(qx[1] > qx[2] && qx[2] > qx[3] && qx[3] > qx[4]);

        // Faixa igual à do perfil devolve o próprio perfil.
        double recomposto = 1 - perfil[5..10].Aggregate(1.0, (s, q) => s * (1 - q));
        double[] igual = MortalityImport.Repartir([new() { Idade = 5, Amplitude = 5, Nqx = recomposto }], perfil);
        for (int age = 5; age < 10; age++)
        {
            Assert.Equal(perfil[age], igual[age], 14);
        }
    }

    [Theory]
    [InlineData("90")]
    [InlineData("90 ou mais")]
    public void Tabua_completa_le_os_dois_blocos_e_o_grupo_aberto(string rotuloAberto)
    {
        string?[] header = ["(X)", "Q (X, N)   (Por Mil)", null, null, null, null, "E(X)"];
        var rows = new List<string?[]> { new string?[] { "BRASIL: Tábua Completa de Mortalidade" }, header };
        for (int age = 0; age < 90; age++)
        {
            if (age == 45)
            {
                // Segundo bloco: notas e cabeçalho repetido no meio da planilha.
                rows.Add(["Notas:"]);
                rows.Add(["N = 1"]);
                rows.Add(header);
            }

            rows.Add([age.ToString(CultureInfo.InvariantCulture), N(age + 1), "5", "1000", "990", "5000", "70"]);
        }

        rows.Add([rotuloAberto, "1000", "200", "200", "800", "800", "4"]);
        rows.Add(["Fonte: IBGE"]);

        MortalityImport.TabuaCompleta tabua = MortalityImport.ReadCompleta(rows, "teste");

        Assert.Equal(90, tabua.Qx.Length);
        Assert.Equal(0.001, tabua.Qx[0], 15);
        Assert.Equal(0.09, tabua.Qx[89], 15);
        Assert.Equal(90, tabua.GrupoAberto.Idade);
        Assert.Equal(0.25, tabua.GrupoAberto.TaxaCentral);
        Assert.Equal(4, tabua.GrupoAberto.ExpectativaDeVida);
    }

    [Fact]
    public void Tabua_completa_com_idade_faltando_interrompe_a_importacao()
    {
        var rows = new List<string?[]> { new string?[] { "(X)", "Q (X, N)   (Por Mil)" } };
        rows.AddRange(Enumerable.Range(0, 90).Where(a => a != 37).Select(a => new string?[] { a.ToString(CultureInfo.InvariantCulture), "1", "1", "1", "1", "1", "1" }));
        rows.Add(["90", "1000", "200", "200", "800", "800", "4"]);

        ImportException ex = Assert.Throws<ImportException>(() => MortalityImport.ReadCompleta(rows, "teste"));
        Assert.Contains("37", ex.Message);
    }

    private static readonly string?[] AbreviadaHeader =
        ["IDADE", "INTERVALO DE IDADE", "SEXO", "ANO", "CÓD.", "SIGLA", "LOCAL", "nMx", "nAx", "nqx", "lx", "ndx", "nLx", "Sx", "Tx", "ex"];

    private static IEnumerable<string?[]> Abreviada(string sexo, string ano, string codigo, string sigla, double nqx)
    {
        int[] ages = [0, 1, .. Enumerable.Range(1, 17).Select(i => i * 5)];
        foreach (int age in ages)
        {
            string width = age == 0 ? "1" : age == 1 ? "4" : "5";
            yield return [age.ToString(CultureInfo.InvariantCulture), width, sexo, ano, codigo, sigla, "Local", "0.01", "2.5", N(nqx + (age / 1000.0)), "1", "1", "1", "1", "1", "30"];
        }

        yield return ["90", "+", sexo, ano, codigo, sigla, "Local", "0.2", "5", "1", "1", "1", "1", "0", "1", "5"];
    }

    [Fact]
    public void Tabuas_abreviadas_ficam_so_com_o_ano_o_brasil_e_as_ufs()
    {
        var rows = new List<string?[]> { new string?[] { "Projeções das Populações, Revisão 2024" }, Array.Empty<string?>(), AbreviadaHeader };
        foreach (string sexo in new[] { "Mulheres", "Homens", "Ambos" })
        {
            rows.AddRange(Abreviada(sexo, "2023", "0", "BR", 0.5));
            rows.AddRange(Abreviada(sexo, "2024", "0", "BR", 0.01));
            rows.AddRange(Abreviada(sexo, "2024", "1", "NO", 0.02));
            rows.AddRange(Abreviada(sexo, "2024", "35", "SP", 0.03));
        }

        var tabuas = MortalityImport.ReadAbreviadas(rows, 2024);

        Assert.Equal(6, tabuas.Count);
        Assert.DoesNotContain(tabuas.Keys, k => k.Local == "NO");
        MortalityImport.TabuaAbreviada sp = tabuas[("SP", "feminino")];
        Assert.Equal(19, sp.Faixas.Count);
        Assert.Equal((0, 1, 0.03), (sp.Faixas[0].Idade, sp.Faixas[0].Amplitude, sp.Faixas[0].Nqx));
        Assert.Equal((1, 4), (sp.Faixas[1].Idade, sp.Faixas[1].Amplitude));
        Assert.Equal((85, 5), (sp.Faixas[^1].Idade, sp.Faixas[^1].Amplitude));
        Assert.Equal(0.01, tabuas[("BR", "masculino")].Faixas[0].Nqx);
        Assert.Equal(new GrupoAberto { Idade = 90, TaxaCentral = 0.2, ExpectativaDeVida = 5 }, tabuas[("BR", "ambos")].GrupoAberto);
    }

    [Fact]
    public void Populacao_le_pop_t_do_brasil_e_das_ufs_nos_anos_pedidos()
    {
        string[] ufs =
        [
            "AC", "AL", "AM", "AP", "BA", "CE", "DF", "ES", "GO", "MA", "MG", "MS", "MT", "PA",
            "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO",
        ];
        var rows = new List<string?[]>
        {
            new string?[] { null, null, null, null, "POPULAÇÃO" },
            new string?[] { "ANO", "CÓD.", "SIGLA", "LOCAL", "POP_T", "POP_H" },
        };
        foreach (int year in new[] { 2021, 2022, 2023 })
        {
            string y = year.ToString(CultureInfo.InvariantCulture);
            rows.Add([y, "0", "BR", "Brasil", (27 * year).ToString(CultureInfo.InvariantCulture), "1"]);
            rows.Add([y, "3", "SD", "Sudeste", "999", "1"]);
            rows.AddRange(ufs.Select((uf, i) => new string?[] { y, (11 + i).ToString(CultureInfo.InvariantCulture), uf, uf, y, "1" }));
        }

        ProjecaoUf projecao = MortalityImport.ReadPopulacao(rows, 2022, 2023);

        Assert.Empty(projecao.Validate());
        Assert.Equal(28, projecao.Populacao.Count);
        Assert.Equal("BR", projecao.Populacao.Keys.First());
        Assert.Equal([27L * 2022, 27L * 2023], projecao.Populacao["BR"]);
        Assert.Equal([2022L, 2023L], projecao.Populacao["SP"]);
        Assert.Equal(2023, projecao.Publicada("AC", 2023));
        Assert.Null(projecao.Publicada("AC", 2021));

        // Faltando um ano de uma UF, a importação para.
        rows.RemoveAt(rows.Count - 1);
        Assert.Throws<ImportException>(() => MortalityImport.ReadPopulacao(rows, 2022, 2023));
    }

    /// <summary>
    /// Confere os arquivos gerados contra o cache: as tábuas e a população gravadas em data/demography/ são as que a
    /// leitura das planilhas do IBGE dá hoje. Precisa do cache (rode o importador antes).
    /// </summary>
    [Fact]
    public void DadosGerados_SaoOsQueAsPlanilhasDoCacheDao()
    {
        string dataDirectory = Planalto.Core.Data.DataPaths.FindDataDirectory(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Pasta data/ não encontrada.");
        string cache = Path.Combine(SourceCache.Locate(Directory.GetParent(dataDirectory)!.FullName), "rd");
        MortalityTable gravada = Planalto.Core.Data.JsonData.LoadFile<MortalityTable>(Path.Combine(dataDirectory, MortalityTable.File)).GetValueOrThrow();
        ProjecaoUf projecao = Planalto.Core.Data.JsonData.LoadFile<ProjecaoUf>(Path.Combine(dataDirectory, ProjecaoUf.File)).GetValueOrThrow();

        foreach (var (sexo, arquivo) in new[] { ("masculino", "homens"), ("feminino", "mulheres"), ("ambos", "ambos_os_sexos") })
        {
            string path = Path.Combine(cache, $"tabua_completa_{MortalityImport.Ano}_{arquivo}.xlsx");
            MortalityImport.TabuaCompleta lida = MortalityImport.ReadCompleta(Xlsx.ReadRows(path, Xlsx.SheetNames(path)[0]), arquivo);
            TabuaSexo brasil = gravada.Tabuas[MortalityTable.Brasil].PorSexo().Single(s => s.Sexo == sexo).Tabua;
            Assert.Equal(lida.Qx, brasil.Qx);
            Assert.Equal(lida.GrupoAberto, brasil.GrupoAberto);
        }

        string indicadores = Path.Combine(cache, MortalityImport.IndicadoresFile);
        ProjecaoUf lidaPop = MortalityImport.ReadPopulacao(
            Xlsx.ReadRows(indicadores, Xlsx.SheetNames(indicadores)[0]), MortalityImport.PrimeiroAnoPopulacao, MortalityImport.UltimoAnoPopulacao);
        Assert.Equal(lidaPop.Populacao.Keys.Order(), projecao.Populacao.Keys.Order());
        Assert.All(lidaPop.Populacao, p => Assert.Equal(p.Value, projecao.Populacao[p.Key]));

        string tabuas = Path.Combine(cache, MortalityImport.TabuasFile);
        var abreviadas = MortalityImport.ReadAbreviadas(Xlsx.ReadRows(tabuas, Xlsx.SheetNames(tabuas)[0]), MortalityImport.Ano);
        Assert.Equal(28 * 3, abreviadas.Count);
        foreach (var ((local, sexo), abreviada) in abreviadas)
        {
            TabuaSexo t = gravada.Tabuas[local].PorSexo().Single(s => s.Sexo == sexo).Tabua;
            Assert.Equal(abreviada.Faixas, t.Faixas);
            if (local != MortalityTable.Brasil)
            {
                Assert.Equal(abreviada.GrupoAberto, t.GrupoAberto);
            }
        }
    }
}
