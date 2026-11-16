using Planalto.Core.Data;
using Planalto.Core.Economy;
using Planalto.Core.Executive;
using Planalto.Core.Indicators;
using Planalto.Core.Municipios;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Indicators;

/// <summary>Dados reais de partida (GDD 17.17, parte RD): cobertura, lacunas, totais por UF, faixas plausíveis e fontes.</summary>
public class IndicadoresReaisTests
{
    private static readonly Lazy<Dictionary<int, IndicadoresMunicipio>> All = new(() =>
        CampaignKit.C.Municipios.All.Select(m => m.Uf).Distinct()
            .SelectMany(uf => IndicadoresPaths.Load(TestContent.DataDirectory, uf).GetValueOrThrow().Municipios)
            .ToDictionary(i => i.CodigoIbge));

    private static FontesIndicadores Fontes =>
        JsonData.LoadFile<FontesIndicadores>(Path.Combine(TestContent.DataDirectory, IndicadoresPaths.Directory, IndicadoresPaths.FontesFileName)).GetValueOrThrow();

    private static IndicatorDefinitions Definitions =>
        JsonData.LoadFile<IndicatorDefinitions>(Path.Combine(TestContent.DataDirectory, IndicatorDefinitions.File)).GetValueOrThrow();

    [Fact]
    public void Validacao_dos_indicadores_reais_nao_acha_erro()
    {
        var reports = IndicadoresContent.Validate(TestContent.DataDirectory, CampaignKit.C.Municipios).ToList();
        Assert.Empty(reports.SelectMany(r => r.Errors.Select(e => $"{r.File}: {e}")).Take(20));
        Assert.Equal(MunicipiosDataSet.UfCount + 4, reports.Count);
    }

    [Fact]
    public void Todo_municipio_tem_valor_ou_lacuna_registrada_para_cada_indicador()
    {
        var lacunas = Fontes.Lacunas.Select(l => (l.CodigoIbge, l.Indicador)).ToHashSet();
        foreach (Municipio m in CampaignKit.C.Municipios.All)
        {
            Assert.True(All.Value.TryGetValue(m.CodigoIbge, out IndicadoresMunicipio? i), $"{m.CodigoIbge} sem indicadores");
            Assert.True(i!.Pib is not null || lacunas.Contains((m.CodigoIbge, "pib")), $"{m.CodigoIbge}: PIB");
            Assert.True(i.Saneamento is not null || lacunas.Contains((m.CodigoIbge, "saneamento")), $"{m.CodigoIbge}: saneamento");
            Assert.True(i.Financas.Fonte == FinancasEnte.Declarado || lacunas.Contains((m.CodigoIbge, "financas")), $"{m.CodigoIbge}: finanças");
            Assert.True(i.Ideb.Fonte == IdebMunicipal.DoMunicipio || lacunas.Contains((m.CodigoIbge, "ideb")), $"{m.CodigoIbge}: IDEB");
            Assert.True(i.Financas.ReceitaCorrente > 0);
            Assert.NotEmpty(i.Financas.DespesaPorFuncao);
        }

        Assert.Equal(CampaignKit.C.Municipios.All.Count(), All.Value.Count);
    }

    [Fact]
    public void Nenhum_valor_fora_da_faixa_plausivel_declarada()
    {
        IndicatorDefinitions definitions = Definitions;
        foreach (Municipio m in CampaignKit.C.Municipios.All)
        {
            foreach (var (id, value) in IndicatorDefinitions.RealValues(All.Value[m.CodigoIbge], m))
            {
                RealIndicatorDefinition d = definitions.Get(id);
                Assert.True(value >= d.Min && value <= d.Max, $"{m.CodigoIbge} {m.Nome}: {id} = {value}");
            }
        }
    }

    [Fact]
    public void Totais_por_uf_iguais_aos_oficiais_onde_a_fonte_publica_total()
    {
        FontesIndicadores fontes = Fontes;
        foreach (var group in CampaignKit.C.Municipios.All.GroupBy(m => m.Uf))
        {
            TotalUf total = fontes.TotaisUf[group.Key];
            long pib = group.Sum(m => All.Value[m.CodigoIbge].Pib?.ValorMil ?? 0);

            // O PIB é publicado em mil reais arredondados por município: a soma difere do total só por esse arredondamento.
            Assert.True(Math.Abs(pib - total.PibMil) <= group.Count(), $"{group.Key}: PIB {pib} x {total.PibMil}");
            Assert.Equal(total.Domicilios, group.Sum(m => All.Value[m.CodigoIbge].Saneamento?.Domicilios ?? 0));
        }
    }

    [Fact]
    public void Todo_campo_tem_fonte_com_orgao_endereco_e_data()
    {
        FontesIndicadores fontes = Fontes;
        Assert.Empty(fontes.Validate());
        Assert.All(fontes.Fontes, f => Assert.True(f.BaixadoEm.Year >= 2026 && f.Url.Contains("://", StringComparison.Ordinal), f.Campo));
        Assert.All(fontes.Lacunas, l => Assert.False(string.IsNullOrWhiteSpace(l.Regra)));
    }

    [Fact]
    public void Substituicoes_seguem_a_regra_de_cada_indicador()
    {
        IndicatorDefinitions definitions = Definitions;
        foreach (IndicadoresMunicipio i in All.Value.Values)
        {
            // Mortalidade infantil: taxa própria só com o mínimo de nascidos vivos; senão, a da região imediata.
            bool proprio = i.Saude.NascidosVivos >= definitions.MinNascidosMortalidadeInfantil;
            Assert.Equal(proprio ? SaudeMunicipal.DoMunicipio : SaudeMunicipal.DaRegiaoImediata, i.Saude.MortalidadeInfantilFonte);
            if (proprio)
            {
                Assert.Equal(Math.Round(i.Saude.ObitosInfantis * 1000m / i.Saude.NascidosVivos, 2), i.Saude.MortalidadeInfantil);
            }

            // IDEB: a nota usada está na série publicada, salvo quando é a média da UF.
            if (i.Ideb.Fonte != IdebMunicipal.DaUf)
            {
                Assert.Equal(i.Ideb.Serie[i.Ideb.Ano], i.Ideb.Valor);
            }

            // Finanças estimadas não trazem RGF (não são dado do ente).
            if (i.Financas.Fonte == FinancasEnte.Estimativa)
            {
                Assert.Null(i.Financas.PessoalPctRcl);
            }
        }
    }

    [Fact]
    public void Economia_nacional_de_partida_e_contexto_das_ufs()
    {
        EconomyStart start = JsonData.LoadFile<EconomyStart>(Path.Combine(TestContent.DataDirectory, EconomyStart.File)).GetValueOrThrow();
        Assert.True(start.PibMilhoes > 1_000_000 && start.Selic is > 0 and < 60 && start.Cambio is > 1 and < 20 && start.Desocupacao is > 0 and < 40);
        IndicadoresUfsFile ufs = IndicadoresPaths.LoadUfs(TestContent.DataDirectory).GetValueOrThrow();
        Assert.Equal(MunicipiosDataSet.UfCount, ufs.Ufs.Count);
        Assert.All(ufs.Ufs, u => Assert.True(u.Desocupacao is > 0 and < 40 && u.IdebMedioRedeMunicipal is > 0 and <= 10, u.Uf));
    }

    [Fact]
    public void Receita_da_prefeitura_e_a_receita_real_do_municipio()
    {
        Municipio m = CampaignKit.C.Municipios.All.First(x => x.Porte == "medio" && All.Value[x.CodigoIbge].Financas.Fonte == FinancasEnte.Declarado);
        IndicadoresMunicipio i = All.Value[m.CodigoIbge];
        var state = CampaignKit.NewCareer(m.CodigoIbge, seed: 11).Context.State;
        ExecutiveContext real = CampaignKit.ExecutiveContext(state, i);
        ExecutiveContext semDado = CampaignKit.ExecutiveContext(state, null);
        Assert.Equal(i.Financas.ReceitaCorrente, new ExecutiveEngine(real).AnnualRevenue);
        Assert.True(new ExecutiveEngine(real).RevenueIsDeclared);

        // Sem o dado importado, vale a estimativa por habitante e por porte da D-031.
        Assert.Equal(m.Populacao * semDado.Config.RevenuePerCapita[m.Porte], new ExecutiveEngine(semDado).AnnualRevenue);
        Assert.False(new ExecutiveEngine(semDado).RevenueIsDeclared);
    }
}
