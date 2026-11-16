using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Demography;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;
using Xunit;

namespace Planalto.Core.Tests.Demography;

public class MortalityTests
{
    private static readonly Lazy<MortalityTable> LazyTable = new(() =>
        JsonData.LoadFile<MortalityTable>(Path.Combine(TestContent.DataDirectory, MortalityTable.File)).GetValueOrThrow());

    private static readonly Lazy<DemographyConfig> LazyConfig = new(() =>
        JsonData.LoadFile<DemographyConfig>(Path.Combine(TestContent.DataDirectory, DemographyConfig.File)).GetValueOrThrow());

    private static MortalityTable Table => LazyTable.Value;

    private static DemographyConfig Config => LazyConfig.Value;

    // Conferência de leitura do GDD 17.16: tábua completa do Brasil de 2024, Q(X) por mil.
    [Theory]
    [InlineData(Gender.Masculino, 40, 3.265)]
    [InlineData(Gender.Feminino, 40, 1.535)]
    [InlineData(Gender.Masculino, 60, 12.532)]
    [InlineData(Gender.Feminino, 60, 6.888)]
    [InlineData(Gender.Masculino, 80, 62.284)]
    [InlineData(Gender.Feminino, 80, 46.531)]
    public void Tabua_do_brasil_tem_os_valores_de_conferencia(Gender gender, int idade, double porMil)
    {
        Assert.Equal(2024, Table.Ano);
        Assert.Equal(porMil, Math.Round(Table.Tabuas[MortalityTable.Brasil].Para(gender).Qx[idade] * 1000, 3));
    }

    [Fact]
    public void Ha_tabua_do_brasil_e_das_27_ufs_dos_municipios()
    {
        var ufs = CampaignKit.C.Municipios.All.Select(m => m.Uf).Distinct().Order(StringComparer.Ordinal).ToList();
        Assert.Equal(27, ufs.Count);
        Assert.Equal(ufs, Table.Tabuas.Keys.Where(k => k != MortalityTable.Brasil).Order(StringComparer.Ordinal));
        Assert.Contains(MortalityTable.Brasil, Table.Tabuas.Keys);
    }

    [Fact]
    public void Todo_qx_fica_entre_zero_e_um_nas_idades_de_0_a_89()
    {
        foreach (var (local, tabua) in Table.Tabuas)
        {
            foreach (var (sexo, t) in tabua.PorSexo())
            {
                Assert.Equal(90, t.Qx.Count);
                Assert.Equal(90, t.GrupoAberto.Idade);
                Assert.All(t.Qx, q => Assert.True(q > 0 && q <= 1, $"{local}/{sexo}: qx {q}"));
            }
        }
    }

    [Fact]
    public void A_faixa_recomposta_de_cada_uf_e_sexo_e_igual_a_publicada()
    {
        int faixas = 0;
        foreach (var (local, tabua) in Table.Tabuas.Where(t => t.Key != MortalityTable.Brasil))
        {
            foreach (var (sexo, t) in tabua.PorSexo())
            {
                // 0, 1 a 4 e as faixas de cinco anos de 5 a 89: 19 faixas, cobrindo todas as idades simples.
                Assert.Equal(19, t.Faixas.Count);
                Assert.Equal(90, t.Faixas.Sum(f => f.Amplitude));
                foreach (FaixaPublicada faixa in t.Faixas)
                {
                    // Tolerância declarada: o arquivo guarda todas as casas do double; sobra só o arredondamento das contas.
                    Assert.True(
                        Math.Abs(t.Recomposta(faixa) - faixa.Nqx) <= MortalityTable.ToleranciaRecomposicao,
                        $"{local}/{sexo}, faixa de {faixa.Idade} anos: recomposta {t.Recomposta(faixa):R}, publicada {faixa.Nqx:R}.");
                    faixas++;
                }
            }
        }

        Assert.Equal(27 * 3 * 19, faixas);
    }

    [Fact]
    public void A_tabua_completa_do_brasil_fica_perto_da_abreviada_das_projecoes()
    {
        // Duas publicações do IBGE para o mesmo ano; a completa é suavizada, então a igualdade não é exata (ver _fontes.json).
        foreach (var (sexo, t) in Table.Tabuas[MortalityTable.Brasil].PorSexo())
        {
            Assert.All(t.Faixas, f => Assert.True(
                Math.Abs(t.Recomposta(f) - f.Nqx) / f.Nqx <= MortalityTable.ToleranciaBrasil,
                $"BR/{sexo}, faixa de {f.Idade} anos."));
        }
    }

    [Fact]
    public void O_perfil_por_idade_dentro_da_faixa_segue_o_do_brasil()
    {
        // Dentro de cada faixa, a ordem das idades pela mortalidade é a mesma da tábua do Brasil.
        TabuaSexo brasil = Table.Tabuas[MortalityTable.Brasil].Masculino;
        TabuaSexo uf = Table.Tabuas["BA"].Masculino;
        foreach (FaixaPublicada faixa in uf.Faixas.Where(f => f.Amplitude > 1))
        {
            for (int age = faixa.Idade; age < faixa.Idade + faixa.Amplitude - 1; age++)
            {
                Assert.Equal(Math.Sign(brasil.Qx[age + 1] - brasil.Qx[age]), Math.Sign(uf.Qx[age + 1] - uf.Qx[age]));
            }
        }
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.0005)]
    [InlineData(0.0125)]
    [InlineData(0.3)]
    [InlineData(1.0)]
    public void A_chance_mensal_recompoe_a_anual(double anual)
    {
        double mensal = Mortality.Mensal(anual);
        Assert.InRange(mensal, 0, 1);
        Assert.Equal(anual, 1 - Math.Pow(1 - mensal, 12), 12);
    }

    [Fact]
    public void A_chance_da_pessoa_e_a_da_tabua_vezes_o_fator_e_o_sorteio_e_mensal()
    {
        Assert.Equal(12, Config.SorteiosPorAno);
        MortalityChance chance = Mortality.Chance(Table, Config, "SP", Gender.Feminino, 60, fatorPessoal: 1.5);
        Assert.Equal("SP", chance.Tabua);
        Assert.Equal(Table.Tabuas["SP"].Feminino.Qx[60], chance.QTabua);
        Assert.Equal(chance.QTabua * 1.5, chance.Anual, 15);
        Assert.Equal(Mortality.Mensal(chance.Anual), chance.PorSorteio);
        Assert.Equal(chance.Anual, 1 - Math.Pow(1 - chance.PorSorteio, 12), 12);
    }

    [Fact]
    public void O_fator_pessoal_vai_de_0_6_a_2_5_e_fica_dentro_dos_limites()
    {
        Assert.Equal(0.6, Config.FatorPessoal.Minimo);
        Assert.Equal(2.5, Config.FatorPessoal.Maximo);

        // Saúde plena sem estresse no mínimo; saúde zerada com estresse máximo no máximo; fora da escala, os mesmos limites.
        Assert.Equal(0.6, Config.Fator(saude: 100, estresse: 0), 12);
        Assert.Equal(2.5, Config.Fator(saude: 0, estresse: 100), 12);
        Assert.Equal(0.6, Config.Fator(saude: 500, estresse: -20), 12);
        Assert.Equal(2.5, Config.Fator(saude: -5, estresse: 900), 12);

        // Nunca sai da faixa e piora (ou fica igual) com menos saúde e com mais estresse.
        for (int saude = 0; saude <= 100; saude += 5)
        {
            for (int estresse = 0; estresse <= 100; estresse += 5)
            {
                double fator = Config.Fator(saude, estresse);
                Assert.InRange(fator, 0.6, 2.5);
                Assert.True(Config.Fator(Math.Max(0, saude - 5), estresse) >= fator);
                Assert.True(Config.Fator(saude, Math.Min(100, estresse + 5)) >= fator);
            }
        }

        // Fator informado fora dos limites é trazido para dentro; a chance anual nunca passa de 1.
        Assert.Equal(2.5, Mortality.Chance(Table, Config, "RJ", Gender.Masculino, 50, fatorPessoal: 40).FatorPessoal);
        Assert.Equal(0.6, Mortality.Chance(Table, Config, "RJ", Gender.Masculino, 50, fatorPessoal: 0).FatorPessoal);
        Assert.InRange(Mortality.Chance(Table, Config, "RJ", Gender.Masculino, 130, fatorPessoal: 2.5).Anual, 0, 1);
        Assert.Equal(
            Mortality.Chance(Table, Config, "RJ", Gender.Masculino, 50, Config.Fator(40, 70)),
            Mortality.Chance(Table, Config, "RJ", Gender.Masculino, 50, saude: 40, estresse: 70));
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("")]
    [InlineData(null)]
    public void Uf_inexistente_usa_a_tabua_do_brasil_e_diz_qual_usou(string? uf)
    {
        MortalityChance chance = Mortality.Chance(Table, Config, uf, Gender.Masculino, 40, fatorPessoal: 1);
        Assert.Equal(MortalityTable.Brasil, chance.Tabua);
        Assert.Equal(Table.Tabuas[MortalityTable.Brasil].Masculino.Qx[40], chance.Anual);
    }

    [Fact]
    public void Pessoa_nao_binaria_usa_a_tabua_de_ambos_os_sexos()
    {
        MortalityChance chance = Mortality.Chance(Table, Config, "MG", Gender.NaoBinario, 70, fatorPessoal: 1);
        Assert.Equal(Table.Tabuas["MG"].Ambos.Qx[70], chance.Anual);
        Assert.NotEqual(Table.Tabuas["MG"].Masculino.Qx[70], chance.Anual);
        Assert.NotEqual(Table.Tabuas["MG"].Feminino.Qx[70], chance.Anual);
    }

    [Fact]
    public void Depois_dos_89_anos_a_curva_segue_a_regra_da_configuracao()
    {
        TabuaSexo tabua = Table.Tabuas["PE"].Feminino;
        CurvaAposTabuaConfig curva = Config.CurvaAposTabua;
        Assert.Equal(tabua.Qx[89], Mortality.Q(tabua, 89, curva));
        Assert.Equal(tabua.Qx[89] * (1 + curva.CrescimentoAnual), Mortality.Q(tabua, 90, curva), 12);

        double previous = tabua.Qx[89];
        for (int idade = 90; idade <= 125; idade++)
        {
            double q = Mortality.Q(tabua, idade, curva);
            Assert.True(q >= previous);
            Assert.InRange(q, 0, Math.Max(curva.TetoAnual, tabua.Qx[89]));
            previous = q;
        }

        Assert.Equal(Math.Max(curva.TetoAnual, tabua.Qx[89]), Mortality.Q(tabua, 125, curva));

        // Idade negativa não existe: vale a do recém-nascido.
        Assert.Equal(tabua.Qx[0], Mortality.Q(tabua, -3, curva));
    }

    [Fact]
    public void O_sorteio_usa_o_rng_do_core_e_e_reproduzivel()
    {
        MortalityChance chance = Mortality.Chance(Table, Config, "RS", Gender.Masculino, 85, fatorPessoal: 2.5);
        bool[] Run(ulong seed)
        {
            var rng = new Rng(seed);
            return Enumerable.Range(0, 2000).Select(_ => Mortality.Sorteia(rng, chance)).ToArray();
        }

        bool[] first = Run(7);
        Assert.Equal(first, Run(7));

        // 2000 sorteios mensais: a frequência fica perto da chance de cada sorteio.
        Assert.InRange(first.Count(d => d) / 2000.0, chance.PorSorteio * 0.5, chance.PorSorteio * 1.5);
        Assert.False(Mortality.Sorteia(new Rng(1), chance with { PorSorteio = 0 }));
        Assert.True(Mortality.Sorteia(new Rng(1), chance with { PorSorteio = 1 }));
    }

    [Fact]
    public void Tabua_que_nao_recompoe_a_faixa_e_reprovada_na_validacao()
    {
        string json = File.ReadAllText(Path.Combine(TestContent.DataDirectory, MortalityTable.File));
        MortalityTable table = JsonData.Parse<MortalityTable>(json, "mortality.json").GetValueOrThrow();
        table.Tabuas["AC"].Masculino.Qx[42] *= 1.001;
        Assert.Contains(table.Validate(), e => e.Contains("tabuas.AC.masculino") && e.Contains("40 anos"));

        table.Tabuas.Remove(MortalityTable.Brasil);
        Assert.Contains(table.Validate(), e => e.Contains("Brasil"));
    }
}
