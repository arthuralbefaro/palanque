using Planalto.Core.Data;
using Planalto.DataImporter.Rd;

namespace Planalto.DataImporter.Tests;

public class SaudeImportTests
{
    [Theory]
    [InlineData("001", true)] // 1 minuto
    [InlineData("123", true)] // 23 horas
    [InlineData("229", true)] // 29 dias
    [InlineData("311", true)] // 11 meses
    [InlineData("400", true)] // zero ano completo
    [InlineData("401", false)]
    [InlineData("505", false)] // 105 anos
    [InlineData("000", false)] // ignorada
    [InlineData("999", false)] // ignorada
    [InlineData("", false)]
    public void MenorDeUmAno_PelaUnidadeDoCampoIdade(string idade, bool esperado) =>
        Assert.Equal(esperado, SaudeImport.MenorDeUmAno(idade));

    [Theory]
    [InlineData("X85", true)]
    [InlineData("X954", true)]
    [InlineData("X99", true)]
    [InlineData("Y00", true)]
    [InlineData("Y09", true)]
    [InlineData("Y090", true)]
    [InlineData("X84", false)] // lesão autoprovocada
    [InlineData("Y10", false)] // intenção indeterminada
    [InlineData("Y350", false)] // intervenção legal
    [InlineData("W85", false)]
    [InlineData("I219", false)]
    [InlineData("", false)]
    public void Agressao_CidDeX85AY09(string causa, bool esperado) =>
        Assert.Equal(esperado, SaudeImport.Agressao(causa));

    [Fact]
    public void Municipio6_AceitaSeisOuSeteDigitos()
    {
        Assert.Equal("120040", SaudeImport.Municipio6("1200401", "teste"));
        Assert.Equal("120040", SaudeImport.Municipio6("120040", "teste"));
        Assert.Throws<ImportException>(() => SaudeImport.Municipio6("12004", "teste"));
        Assert.Throws<ImportException>(() => SaudeImport.Municipio6("", "teste"));
    }

    [Fact]
    public void ArquivosEsperados_SeteArquivosPorUf()
    {
        Assert.Equal(27, SaudeImport.Ufs.Distinct().Count());
        Assert.Equal(27 * 7, SaudeImport.FileNames.Distinct().Count());
        Assert.Contains("rd/DOSP2024.dbc", SaudeImport.FileNames);
        Assert.Contains("rd/DNAC2022.dbc", SaudeImport.FileNames);
        Assert.Contains("rd/LTDF2609.dbc", SaudeImport.FileNames);
    }

    /// <summary>
    /// Todo .dbc do Acre abre e entrega exatamente os registros que o cabeçalho DBF anuncia: confere o
    /// descompressor contra arquivos reais. Precisa do cache (rode o importador antes).
    /// </summary>
    [Fact]
    public void Cache_TodoDbcDoAcreTemOsRegistrosDoCabecalho()
    {
        string cache = CacheDirectory();
        List<string> doAcre = SaudeImport.FileNames
            .Where(f => Path.GetFileName(f).Substring(2, 2) == "AC")
            .Select(f => Path.Combine(cache, f))
            .ToList();
        Assert.Equal(7, doAcre.Count);

        foreach (string path in doAcre)
        {
            Assert.True(File.Exists(path), Falta(path));
            DbfHeader cabecalho = Dbc.ReadHeader(path);
            Assert.True(cabecalho.RecordCount > 0, $"{path}: nenhum registro no cabeçalho.");

            int lidos = Dbc.ReadColumns(path, cabecalho.Fields[0].Name, cabecalho.Fields[^1].Name).Count();

            Assert.Equal(cabecalho.RecordCount, lidos);
        }
    }

    /// <summary>
    /// Totais nacionais de 2024 dentro da ordem de grandeza publicada pelo Ministério da Saúde. As faixas
    /// são largas de propósito: pegam erro de leitura (arquivo pela metade, coluna errada, UF faltando ou
    /// contada em dobro), não conferem o número exato. Precisa do cache (rode o importador antes).
    /// </summary>
    [Fact]
    public async Task Cache_TotaisNacionaisDe2024NaOrdemDeGrandezaPublicada()
    {
        string cache = CacheDirectory();
        foreach (string file in SaudeImport.FileNames)
        {
            Assert.True(File.Exists(Path.Combine(cache, file)), Falta(Path.Combine(cache, file)));
        }

        using var http = new HttpClient();
        SaudeAgregados agregados = await SaudeImport.ReadAsync(new SourceCache(cache, http));

        Assert.Equal(27 * 7, agregados.Arquivos.Count);
        foreach (int ano in SaudeImport.Anos)
        {
            Assert.Equal(27, agregados.TotaisPorUf[ano].Count);
            Assert.All(agregados.TotaisPorUf[ano].Values, t =>
            {
                Assert.True(t.Obitos > 0);
                Assert.True(t.NascidosVivos > 0);
                Assert.True(t.ObitosMenoresDeUmAno > 0);
                Assert.True(t.ObitosPorAgressao > 0);
            });

            // Os dicionários por município somam o mesmo que os totais por UF.
            SaudeTotaisUf total = agregados.TotalNacional(ano);
            Assert.Equal(total.ObitosMenoresDeUmAno, agregados.ObitosMenoresDeUmAno[ano].Values.Sum(v => (long)v));
            Assert.Equal(total.ObitosPorAgressao, agregados.ObitosPorAgressao[ano].Values.Sum(v => (long)v));
            Assert.Equal(total.NascidosVivos, agregados.NascidosVivos[ano].Values.Sum(v => (long)v));
        }

        SaudeTotaisUf brasil = agregados.TotalNacional(2024);

        // O SIM registra cerca de 1,5 milhão de óbitos por ano no Brasil desde 2022 (o pico foi 2021, com a
        // pandemia, perto de 1,8 milhão).
        Assert.InRange(brasil.Obitos, 1_200_000, 1_800_000);

        // O SINASC registra entre 2,4 e 2,6 milhões de nascidos vivos por ano, em queda desde 2019.
        Assert.InRange(brasil.NascidosVivos, 2_000_000, 3_000_000);

        // A taxa de mortalidade infantil publicada para o Brasil fica entre 12 e 13 por mil nascidos vivos.
        double mortalidadeInfantil = 1000.0 * brasil.ObitosMenoresDeUmAno / brasil.NascidosVivos;
        Assert.InRange(mortalidadeInfantil, 9.0, 16.0);

        // O Atlas da Violência conta de 45 a 48 mil homicídios por ano em 2022 e 2023, somando às agressões as
        // intervenções legais, que aqui ficam de fora.
        Assert.InRange(brasil.ObitosPorAgressao, 25_000, 60_000);

        // O CNES tem perto de meio milhão de leitos de internação no país, mais os complementares (UTI).
        SaudeLeitos leitos = agregados.TotalNacionalDeLeitos();
        Assert.Equal(27, agregados.LeitosPorUf.Count);
        Assert.InRange(leitos.Internacao, 350_000, 650_000);
        Assert.InRange(leitos.Complementares, 1, leitos.Internacao);
        Assert.True(leitos.Sus > leitos.NaoSus, "Esperado mais leitos SUS que não SUS.");

        // Todo código tem 6 dígitos.
        Assert.All(agregados.NascidosVivos[2024].Keys, m => Assert.Equal(6, m.Length));
        Assert.All(agregados.Leitos.Keys, m => Assert.Equal(6, m.Length));
    }

    private static string CacheDirectory()
    {
        string dataDirectory = DataPaths.FindDataDirectory(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Pasta data/ não encontrada.");
        return SourceCache.Locate(Directory.GetParent(dataDirectory)!.FullName);
    }

    private static string Falta(string path) =>
        $"Arquivo do DATASUS não encontrado em {path}. Rode o Planalto.DataImporter para preencher o cache.";
}
