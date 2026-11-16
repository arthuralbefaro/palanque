using Planalto.Core.Data;
using Planalto.Core.Demography;
using Planalto.Core.Elections;
using Planalto.Core.Municipios;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;
using Xunit;

namespace Planalto.Core.Tests.Municipios;

public class PopulationProjectionTests
{
    private static PopulationProjection Projection => CampaignKit.C.Projection;

    private static PopulationProjectionConfig Config =>
        JsonData.LoadFile<PopulationProjectionConfig>(Path.Combine(TestContent.DataDirectory, PopulationProjectionConfig.File)).GetValueOrThrow();

    /// <summary>Projeção só pela taxa do município, como era antes da reescala por UF (sem data/demography/projecao_uf.json).</summary>
    private static PopulationProjection Unscaled => PopulationProjection.From(CampaignKit.C.Municipios, Config);

    // Sem a projeção por UF, cada município segue a própria tendência; com ela, a tendência é só relativa
    // (um município que cresce pode encolher se a UF encolher mais), então este teste vale para a projeção sem reescala.
    [Fact]
    public void Sem_a_projecao_por_uf_parte_da_estimativa_e_segue_a_tendencia_do_censo()
    {
        PopulationProjection p = Unscaled;
        Assert.False(p.RescaledByUf);
        Assert.True(p.EstimateYear > PopulationProjection.CensusYear);
        foreach (Municipio m in CampaignKit.C.Municipios.All.Take(500))
        {
            Assert.Equal(m.Populacao, p.Population(m, p.EstimateYear));
            double growth = p.AnnualGrowth(m);
            Assert.InRange(growth, -0.03, 0.05);
            if (m.Populacao > m.Perfil.PopulacaoCenso)
            {
                Assert.True(p.Population(m, p.EstimateYear + 10) >= m.Populacao);
            }
            else if (m.Populacao < m.Perfil.PopulacaoCenso)
            {
                Assert.True(p.Population(m, p.EstimateYear + 10) <= m.Populacao);
            }
        }
    }

    [Fact]
    public void Vagas_nunca_sobem_e_caem_quando_a_populacao_sai_da_faixa()
    {
        PopulationProjection p = Projection;
        var all = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais).ToList();
        Assert.All(all, m => Assert.InRange(p.Seats(m, 2040), 1, m.VagasVereador));

        // Na eleição seguinte à estimativa, quase todos mantêm as vagas; décadas depois, alguns que encolhem perdem cadeiras.
        Assert.True(all.Count(m => p.Seats(m, 2028) == m.VagasVereador) > all.Count * 0.95);
        Municipio shrinking = all.First(m => p.AnnualGrowth(m) < -0.005 && m.VagasVereador == ElectionRules.VagasVereador(m.Populacao) && m.VagasVereador > 9);
        Assert.True(p.Seats(shrinking, 2080) < shrinking.VagasVereador);
    }

    [Fact]
    public void A_eleicao_usa_as_vagas_projetadas()
    {
        Planalto.Core.Campaign.CampaignEngine engine = CampaignKit.NewCareer(3550308, 11);
        int year = engine.Context.State.Campaign!.ElectionDate.Year;
        Assert.Equal(Projection.Seats(engine.Context.Municipio, year), engine.Context.SeatsIn(year));
    }

    [Theory]
    [InlineData(2030)]
    [InlineData(2040)]
    [InlineData(2060)]
    public void A_soma_dos_municipios_de_cada_uf_acompanha_a_projecao_da_uf(int year)
    {
        PopulationProjection p = Projection;
        Assert.True(p.RescaledByUf);
        ProjecaoUf uf = JsonData.LoadFile<ProjecaoUf>(Path.Combine(TestContent.DataDirectory, ProjecaoUf.File)).GetValueOrThrow();
        var groups = CampaignKit.C.Municipios.All.GroupBy(m => m.Uf).ToList();
        Assert.Equal(27, groups.Count);
        foreach (IGrouping<string, Municipio> group in groups)
        {
            long target = uf.Publicada(group.Key, year) ?? throw new InvalidOperationException($"Sem projeção para {group.Key} em {year}.");
            long sum = group.Sum(m => p.Population(m, year));

            // Tolerância declarada: cada município é arredondado para inteiro, então a soma difere do alvo em até meia pessoa por município.
            double tolerance = group.Count() * 0.5;
            Assert.True(Math.Abs(sum - target) <= tolerance, $"{group.Key} em {year}: soma {sum}, projeção {target}, tolerância {tolerance}.");
        }
    }

    [Fact]
    public void Com_a_projecao_por_uf_o_ano_da_estimativa_fica_igual_ao_dado_e_a_ordem_entre_municipios_se_mantem()
    {
        PopulationProjection p = Projection;
        PopulationProjection unscaled = Unscaled;

        // Na UF em que a estimativa municipal do IBGE soma exatamente a projeção da UF no mesmo ano, a reescala não mexe
        // no ponto de partida. Onde a soma difere (em 2026, PR e SC, por 68 habitantes: a estimativa municipal segue a
        // divisa atualizada), a diferença é repartida e não passa de 1 habitante a cada 100 mil.
        ProjecaoUf uf = JsonData.LoadFile<ProjecaoUf>(Path.Combine(TestContent.DataDirectory, ProjecaoUf.File)).GetValueOrThrow();
        int exact = 0;
        foreach (IGrouping<string, Municipio> group in CampaignKit.C.Municipios.All.GroupBy(m => m.Uf))
        {
            if (group.Sum(m => m.Populacao) == uf.Publicada(group.Key, p.EstimateYear))
            {
                exact++;
                Assert.All(group, m => Assert.Equal(m.Populacao, p.Population(m, p.EstimateYear)));
            }
            else
            {
                Assert.All(group, m => Assert.InRange(Math.Abs(p.Population(m, p.EstimateYear) - m.Populacao), 0, 1 + (m.Populacao / 100_000)));
            }
        }

        Assert.InRange(exact, 25, 27);

        // A taxa municipal continua como distribuição relativa: dentro da UF, todos levam o mesmo fator no mesmo ano.
        foreach (IGrouping<string, Municipio> group in CampaignKit.C.Municipios.All.Where(m => m.Populacao > 50_000).GroupBy(m => m.Uf))
        {
            var ratios = group.Select(m => (double)p.Population(m, 2050) / unscaled.Population(m, 2050)).ToList();
            Assert.InRange(ratios.Max() - ratios.Min(), 0, 1e-4);
        }
    }

    [Fact]
    public void Depois_do_fim_da_serie_a_uf_segue_a_taxa_do_ultimo_ano_publicado()
    {
        PopulationProjection p = Projection;
        ProjecaoUf uf = JsonData.LoadFile<ProjecaoUf>(Path.Combine(TestContent.DataDirectory, ProjecaoUf.File)).GetValueOrThrow();
        int year = uf.AnoFinal + 10;
        foreach (IGrouping<string, Municipio> group in CampaignKit.C.Municipios.All.GroupBy(m => m.Uf))
        {
            double last = uf.Publicada(group.Key, uf.AnoFinal)!.Value;
            double expected = last * Math.Pow(last / uf.Publicada(group.Key, uf.AnoFinal - 1)!.Value, 10);
            Assert.Equal(expected, p.UfTarget(group.Key, year)!.Value, 6);
            Assert.InRange(group.Sum(m => p.Population(m, year)) - expected, -group.Count() * 0.5, group.Count() * 0.5);
        }
    }

    [Fact]
    public void Sem_o_arquivo_de_projecao_por_uf_vale_o_comportamento_antigo()
    {
        // Pasta de dados só com a configuração, sem demography/projecao_uf.json.
        string dir = Path.Combine(Path.GetTempPath(), $"planalto_proj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.Copy(Path.Combine(TestContent.DataDirectory, PopulationProjectionConfig.File), Path.Combine(dir, PopulationProjectionConfig.File));
            PopulationProjection loaded = PopulationProjection.Load(dir, CampaignKit.C.Municipios);
            Assert.False(loaded.RescaledByUf);
            Assert.Null(loaded.UfTarget("SP", 2040));

            PopulationProjectionConfig config = Config;
            foreach (Municipio m in CampaignKit.C.Municipios.All.Take(300))
            {
                // Fórmula da D-033: estimativa × (1 + taxa limitada)^(anos desde a estimativa).
                double growth = Math.Clamp(
                    Math.Pow((double)m.Populacao / m.Perfil.PopulacaoCenso, 1.0 / (loaded.EstimateYear - PopulationProjection.CensusYear)) - 1,
                    config.MinAnnualGrowth,
                    config.MaxAnnualGrowth);
                Assert.Equal((long)Math.Round(m.Populacao * Math.Pow(1 + growth, 2040 - loaded.EstimateYear)), loaded.Population(m, 2040));
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
