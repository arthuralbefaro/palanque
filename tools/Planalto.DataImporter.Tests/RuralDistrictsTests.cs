using Planalto.Core.Territory;
using Planalto.DataImporter.Geo;

namespace Planalto.DataImporter.Tests;

/// <summary>Divisão da zona rural pelos distritos do IBGE (GDD 17.5, regra 2): as contas, sem geometria.</summary>
public class RuralDistrictsTests
{
    private static readonly RuralPorDistrito Regra = new() { PopulacaoMinima = 200, ZonasExtrasMax = 4 };

    private static DistritoRural D(string codigo, long pop, double x = 0, double y = 0) => new(codigo, pop, x, y);

    private static Dictionary<(string, string), int> Divisas(params (string A, string B, int N)[] pares) =>
        pares.ToDictionary(p => RuralDistricts.Key(p.A, p.B), p => p.N);

    [Fact]
    public void Municipio_so_com_a_sede_fica_como_estava()
    {
        RuralPlan plan = RuralDistricts.Plan([D("01", 3000)], Divisas(), Regra, zonasBase: 2);

        Assert.Equal([2], plan.ZonasPorGrupo);
        Assert.Equal(2, plan.Total);
    }

    [Fact]
    public void Sem_zona_rural_nao_ha_grupo()
    {
        RuralPlan plan = RuralDistricts.Plan([], Divisas(), Regra, zonasBase: 0);

        Assert.Empty(plan.ZonasPorGrupo);
        Assert.Equal(0, plan.Total);
    }

    [Fact]
    public void Cada_distrito_acima_do_minimo_ganha_a_propria_zona()
    {
        RuralPlan plan = RuralDistricts.Plan([D("05", 2121), D("10", 1752), D("15", 511)], Divisas(("05", "10", 3), ("05", "15", 2)), Regra, zonasBase: 1);

        Assert.Equal(3, plan.Total);
        Assert.Equal(3, plan.ZonasPorGrupo.Count);
        Assert.All(plan.ZonasPorGrupo, n => Assert.Equal(1, n));
        Assert.Equal(3, plan.GrupoDoDistrito.Values.Distinct().Count());
    }

    [Fact]
    public void Distrito_abaixo_do_minimo_vai_para_o_vizinho_de_maior_divisa()
    {
        // "15" tem 120 moradores rurais: divide mais divisa com "10" do que com "05".
        RuralPlan plan = RuralDistricts.Plan([D("05", 2000), D("10", 900), D("15", 120)], Divisas(("05", "15", 1), ("10", "15", 4)), Regra, zonasBase: 1);

        Assert.Equal(2, plan.Total);
        Assert.Equal(plan.GrupoDoDistrito["10"], plan.GrupoDoDistrito["15"]);
        Assert.NotEqual(plan.GrupoDoDistrito["05"], plan.GrupoDoDistrito["10"]);
    }

    [Fact]
    public void Distrito_pequeno_sem_divisa_vai_para_o_mais_proximo()
    {
        RuralPlan plan = RuralDistricts.Plan([D("05", 2000, 0, 0), D("10", 900, 10, 0), D("15", 50, 9, 1)], Divisas(), Regra, zonasBase: 1);

        Assert.Equal(plan.GrupoDoDistrito["10"], plan.GrupoDoDistrito["15"]);
    }

    [Fact]
    public void Todos_abaixo_do_minimo_viram_um_grupo_so_e_o_total_nao_muda()
    {
        RuralPlan plan = RuralDistricts.Plan([D("05", 150), D("10", 90)], Divisas(("05", "10", 2)), Regra, zonasBase: 1);

        Assert.Single(plan.ZonasPorGrupo);
        Assert.Equal(1, plan.Total);
    }

    [Fact]
    public void Distritos_demais_param_no_maximo_de_zonas_extras()
    {
        var distritos = Enumerable.Range(1, 9).Select(i => D($"{i:00}", 1000 + i)).ToList();

        RuralPlan plan = RuralDistricts.Plan(distritos, Divisas(), Regra, zonasBase: 2);

        Assert.Equal(2 + Regra.ZonasExtrasMax, plan.Total);
        Assert.Equal(9, plan.ZonasPorGrupo.Count);
    }

    [Fact]
    public void Zonas_alem_dos_grupos_vao_para_os_grupos_mais_populosos()
    {
        // Dois distritos e quatro zonas rurais: o mais populoso fica com três (maiores médias).
        RuralPlan plan = RuralDistricts.Plan([D("05", 9000), D("10", 3000)], Divisas(("05", "10", 5)), Regra, zonasBase: 4);

        Assert.Equal(4, plan.Total);
        Assert.Equal(3, plan.ZonasPorGrupo[plan.GrupoDoDistrito["05"]]);
        Assert.Equal(1, plan.ZonasPorGrupo[plan.GrupoDoDistrito["10"]]);
    }

    [Fact]
    public void Ordem_de_entrada_nao_muda_o_plano()
    {
        var a = RuralDistricts.Plan([D("05", 2000), D("10", 900), D("15", 120), D("20", 400)], Divisas(("10", "15", 4), ("15", "20", 4)), Regra, zonasBase: 2);
        var b = RuralDistricts.Plan([D("20", 400), D("15", 120), D("05", 2000), D("10", 900)], Divisas(("15", "20", 4), ("10", "15", 4)), Regra, zonasBase: 2);

        Assert.Equal(a.ZonasPorGrupo, b.ZonasPorGrupo);
        Assert.Equal(a.GrupoDoDistrito.OrderBy(p => p.Key), b.GrupoDoDistrito.OrderBy(p => p.Key));
    }
}
