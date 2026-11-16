using Planalto.Core.Data;
using Planalto.Core.Municipios;
using Planalto.Core.Territory;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Territory;

/// <summary>Zonas geradas pelo importador (GDD 17.5): cobertura de todos os municípios, população, sigilo e tamanho.</summary>
public class ZonasDataTests
{
    private static readonly Lazy<Dictionary<string, (ZonasUf Zonas, GeoZonasUf Geo)>> Files = new(() =>
        CampaignKit.C.Municipios.All.Select(m => m.Uf).Distinct().ToDictionary(
            uf => uf,
            uf => (ZonasPaths.Load(TestContent.DataDirectory, uf).GetValueOrThrow(), ZonasPaths.LoadGeo(TestContent.DataDirectory, uf).GetValueOrThrow())));

    private static TerritoryConfig Config => JsonData.LoadFile<TerritoryConfig>(Path.Combine(TestContent.DataDirectory, TerritoryConfig.File)).GetValueOrThrow();

    [Fact]
    public void Todo_municipio_tem_zonas_e_a_populacao_bate_com_o_Censo_sem_tolerancia()
    {
        var zonas = Files.Value.Values.SelectMany(f => f.Zonas.Municipios).ToDictionary(m => m.CodigoIbge);
        foreach (Municipio m in CampaignKit.C.Municipios.All)
        {
            Assert.True(zonas.TryGetValue(m.CodigoIbge, out ZonasMunicipio? z), $"{m.CodigoIbge} sem zonas");
            Assert.NotEmpty(z!.Zonas);
            if (m.PerfilFonte == Municipio.PerfilFonteMunicipio)
            {
                Assert.Equal(m.Perfil.PopulacaoCenso, z.Populacao);
            }
        }

        Assert.Equal(CampaignKit.C.Municipios.All.Count(), zonas.Count);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Eleitores_das_zonas_somam_o_eleitorado_do_municipio_sem_tolerancia()
    {
        var zonas = Files.Value.Values.SelectMany(f => f.Zonas.Municipios).ToDictionary(m => m.CodigoIbge);
        foreach (Municipio m in CampaignKit.C.Municipios.All)
        {
            ZonasMunicipio z = zonas[m.CodigoIbge];
            Assert.All(z.Zonas, zona => Assert.True(zona.Eleitores is >= 0 && zona.EleitoresFonte is Zona.EleitoresLocais or Zona.EleitoresPopulacao, $"{zona.Id} sem eleitores"));
            Assert.Equal(m.Eleitorado, z.Zonas.Sum(zona => zona.Eleitores!.Value));
        }
    }

    [Fact]
    public void Religiao_e_escolaridade_por_zona_so_onde_ha_duas_ou_mais_areas_de_ponderacao()
    {
        int comAmostra = 0;
        foreach (ZonasMunicipio m in Files.Value.Values.SelectMany(f => f.Zonas.Municipios))
        {
            foreach (Zona z in m.Zonas.Where(z => z.Perfil.Religiao is not null))
            {
                comAmostra++;
                Assert.NotNull(z.Perfil.Escolaridade);
                Assert.Equal(PerfilCategorias.Religiao.Count, z.Perfil.Religiao!.Count);
                Assert.Equal(PerfilCategorias.Escolaridade.Count, z.Perfil.Escolaridade!.Count);
                Assert.All(z.Perfil.Religiao, v => Assert.True(v >= 0));

                // Religião conta pessoas de 10 anos ou mais: não pode passar da população da zona (com folga do
                // arredondamento da amostra, que o IBGE estima e arredonda por área).
                Assert.True(z.Perfil.Religiao.Sum() <= z.Populacao * 1.25 + 50, $"{z.Id}: religião {z.Perfil.Religiao.Sum()} > população {z.Populacao}");
            }
        }

        Assert.True(comAmostra > 10_000, $"{comAmostra} zonas com religião e escolaridade próprias");
    }

    [Fact]
    public void Numero_de_zonas_respeita_o_porte_e_urbano_nao_se_mistura_com_rural()
    {
        TerritoryConfig config = Config;
        var porte = CampaignKit.C.Municipios.All.ToDictionary(m => m.CodigoIbge, m => m.Porte);
        foreach (ZonasMunicipio m in Files.Value.Values.SelectMany(f => f.Zonas.Municipios))
        {
            var range = config.ZonasPorPorte[porte[m.CodigoIbge]];
            Assert.True(m.Zonas.Count <= range.Max, $"{m.CodigoIbge}: {m.Zonas.Count} zonas, máximo {range.Max}");
            Assert.All(m.Zonas, z => Assert.True(z.Situacao is Zona.Urbana or Zona.Rural));
        }

        // Quase todos chegam ao mínimo; só não chega quem tem menos setores que o mínimo.
        double reached = Files.Value.Values.SelectMany(f => f.Zonas.Municipios).Count(m => m.Zonas.Count >= config.ZonasPorPorte[porte[m.CodigoIbge]].Min);
        Assert.True(reached / porte.Count > 0.97, $"{reached} de {porte.Count} municípios no mínimo de zonas");
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Regra_de_sigilo_cobertura_decide_a_fonte_do_perfil()
    {
        double minimum = Config.CoberturaMinima;
        foreach (Zona z in Files.Value.Values.SelectMany(f => f.Zonas.Municipios).SelectMany(m => m.Zonas))
        {
            Assert.Empty(z.Perfil.Validate(z));
            if (z.Perfil.IdadeFonte == PerfilZona.FonteZona)
            {
                Assert.True(z.Perfil.IdadeCobertura >= minimum, $"{z.Id}: idade própria com cobertura {z.Perfil.IdadeCobertura}");
                Assert.True(z.Perfil.Idade.Values.Sum() <= z.Populacao, $"{z.Id}: faixas etárias acima da população");
            }

            if (z.Perfil.RendaFonte == PerfilZona.FonteZona)
            {
                Assert.True(z.Perfil.RendaCobertura >= minimum, $"{z.Id}: renda própria com cobertura {z.Perfil.RendaCobertura}");
            }
        }
    }

    [Fact]
    public void Toda_zona_tem_contorno_e_os_contornos_cabem_no_teto()
    {
        long bytes = Directory.EnumerateFiles(ZonasPaths.GeoDirectory(TestContent.DataDirectory), "*.json").Sum(f => new FileInfo(f).Length);
        Assert.True(bytes <= ZonasPaths.MaxGeoBytes, $"{bytes} bytes em data/geo/zonas/");
        long attributeBytes = Directory.EnumerateFiles(Path.Combine(TestContent.DataDirectory, ZonasPaths.Directory), "*.json").Sum(f => new FileInfo(f).Length);
        Assert.True(attributeBytes <= ZonasPaths.MaxZonasBytes, $"{attributeBytes} bytes em data/zonas/");
        foreach (var (uf, (zonas, geo)) in Files.Value)
        {
            Assert.Empty(zonas.Validate());
            Assert.Empty(geo.Validate());
            var ids = geo.Zonas.Select(z => z.Id).ToHashSet(StringComparer.Ordinal);
            Assert.All(zonas.Municipios.SelectMany(m => m.Zonas), z => Assert.Contains(z.Id, ids));
        }
    }

    [Fact]
    public void Vizinhanca_e_simetrica()
    {
        foreach (ZonasMunicipio m in Files.Value.Values.SelectMany(f => f.Zonas.Municipios))
        {
            var byId = m.Zonas.ToDictionary(z => z.Id);
            foreach (Zona z in m.Zonas)
            {
                Assert.All(z.Vizinhas, v => Assert.Contains(z.Id, byId[v].Vizinhas));
            }
        }
    }
}
