using Planalto.Core.Elections;
using Planalto.Core.Municipios;
using Planalto.Core.Tests.Campaign;
using Xunit;

namespace Planalto.Core.Tests.Municipios;

public class PopulationProjectionTests
{
    private static PopulationProjection Projection => CampaignKit.C.Projection;

    [Fact]
    public void Projecao_parte_da_estimativa_e_segue_a_tendencia_do_censo()
    {
        PopulationProjection p = Projection;
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
}
