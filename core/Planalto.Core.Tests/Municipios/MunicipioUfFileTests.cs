using Planalto.Core.Municipios;

namespace Planalto.Core.Tests.Municipios;

/// <summary>Regras de consistência de um arquivo de UF, com municípios fictícios montados no teste.</summary>
public class MunicipioUfFileTests
{
    private static PerfilCenso Perfil(long total = 1000) => new()
    {
        PopulacaoCenso = total,
        Situacao = new() { ["urbana"] = total - 200, ["rural"] = 200 },
        Idade = PerfilCategorias.Idade.Select((k, i) => (k, v: i == 0 ? total - 700 : 100L)).ToDictionary(x => x.k, x => x.v),
        Renda = PerfilCategorias.Renda.ToDictionary(k => k, _ => 10L),
        Religiao = PerfilCategorias.Religiao.ToDictionary(k => k, _ => 10L),
        Escolaridade = PerfilCategorias.Escolaridade.ToDictionary(k => k, _ => 10L),
    };

    private static Municipio Valid(int codigo = 9900001, long populacao = 15_000, long eleitorado = 12_000) => new()
    {
        CodigoIbge = codigo,
        Nome = "Município de Teste",
        Uf = "ZZ",
        Regiao = "NE",
        Microrregiao = null,
        RegiaoImediata = new RegiaoRef { Id = 990001, Nome = "Teste" },
        Lat = -10,
        Lon = -40,
        CoordenadaMetodo = Municipio.CoordenadaCentroide,
        AreaKm2 = 350.5,
        Populacao = populacao,
        Eleitorado = eleitorado,
        UrbanaPct = 80.0,
        PerfilFonte = Municipio.PerfilFonteMunicipio,
        Perfil = Perfil(),
        EleicoesMunicipais = true,
        VagasVereador = 9,
        VagasVereadorMax = 9,
        VagasFonte = Municipio.VagasFonteTse2024,
        SegundoTurnoPrefeito = false,
        Porte = "pequeno",
    };

    private static MunicipioUfFile File(params Municipio[] municipios) => new() { Uf = "ZZ", Municipios = [.. municipios] };

    [Fact]
    public void MunicipioConsistente_NaoTemErros()
    {
        Assert.Empty(File(Valid()).Validate());
    }

    [Fact]
    public void SemEleicaoMunicipal_ExigeVagasZeradas()
    {
        Municipio distrito = Valid();
        distrito = new Municipio
        {
            CodigoIbge = distrito.CodigoIbge, Nome = distrito.Nome, Uf = distrito.Uf, Regiao = distrito.Regiao,
            Microrregiao = null, RegiaoImediata = distrito.RegiaoImediata, Lat = distrito.Lat, Lon = distrito.Lon,
            CoordenadaMetodo = distrito.CoordenadaMetodo, AreaKm2 = distrito.AreaKm2,
            Populacao = distrito.Populacao, Eleitorado = distrito.Eleitorado, UrbanaPct = distrito.UrbanaPct,
            PerfilFonte = distrito.PerfilFonte, Perfil = distrito.Perfil, Porte = distrito.Porte,
            EleicoesMunicipais = false, VagasVereador = 0, VagasVereadorMax = 0,
            VagasFonte = Municipio.VagasFonteSemEleicao, SegundoTurnoPrefeito = false,
        };

        Assert.Empty(File(distrito).Validate());
    }

    [Fact]
    public void CodigoDuplicado_EReportado()
    {
        Assert.Contains(File(Valid(), Valid()).Validate(), e => e.Contains("duplicado"));
    }

    [Theory]
    [InlineData(15_001, 12_000)] // teto passaria a 11 e o arquivo diz 9
    [InlineData(15_000, 200_001)] // eleitorado exige 2º turno
    [InlineData(15_000, 0)] // sem eleitorado
    public void RegrasDerivadasInconsistentes_SaoReportadas(long populacao, long eleitorado)
    {
        Assert.NotEmpty(File(Valid(populacao: populacao, eleitorado: eleitorado)).Validate());
    }

    [Fact]
    public void PerfilQueNaoSomaAPopulacaoDoCenso_EReportado()
    {
        Municipio m = Valid();
        m.Perfil.Idade["0_15"] += 1;

        Assert.Contains(File(m).Validate(), e => e.Contains("perfil.idade"));
    }

    [Fact]
    public void UrbanaPct_ArredondaComUmaCasa()
    {
        Assert.Equal(66.7, PerfilCenso.UrbanaPct(2, 3));
        Assert.Equal(0.1, PerfilCenso.UrbanaPct(1, 2000));
        Assert.Equal(0.0, PerfilCenso.UrbanaPct(0, 0));
    }

    [Fact]
    public void ValidatePortes_DetectaPorteDesatualizado()
    {
        var portes = new PorteCatalog
        {
            Portes = [new() { Id = "pequeno", Name = "P", MaxPopulation = 10_000 }, new() { Id = "medio", Name = "M" }],
        };

        Assert.Single(File(Valid()).ValidatePortes(portes));
    }
}
