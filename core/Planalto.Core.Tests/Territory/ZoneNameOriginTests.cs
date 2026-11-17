using Planalto.Core.Territory;

namespace Planalto.Core.Tests.Territory;

/// <summary>Origem do nome da zona (D-058): o que a Ficha da zona diz e quando o mapa mostra o crédito do OpenStreetMap.</summary>
public class ZoneNameOriginTests
{
    private static Zona Z(string fonte, string id = "3139607-01") => new()
    {
        Id = id,
        Nome = "Vila Nova",
        NomeFonte = fonte,
        Situacao = Zona.Urbana,
        Populacao = 1000,
        Domicilios = 300,
        Setores = 2,
        AreaKm2 = 1.5,
        Lat = -18.78,
        Lon = -40.98,
        Vizinhas = [],
        Perfil = new PerfilZona
        {
            Idade = new Dictionary<string, long>(),
            IdadeCobertura = 1,
            IdadeFonte = PerfilZona.FonteZona,
            RendaCobertura = 1,
            RendaFonte = PerfilZona.FonteZona,
        },
    };

    [Theory]
    [InlineData(Zona.NomeBairro, "IBGE", "Nome do bairro: IBGE")]
    [InlineData(Zona.NomeOsm, "OpenStreetMap", "Nome do bairro: OpenStreetMap")]
    [InlineData(Zona.NomeDistrito, "IBGE", "Nome do distrito: IBGE")]
    [InlineData(Zona.NomeGerado, "nome gerado", "Nome gerado pelo jogo")]
    public void Ficha_diz_a_origem_do_nome(string fonte, string curta, string inicio)
    {
        Zona z = Z(fonte);
        Assert.Equal(curta, ZoneNameOrigin.Fonte(z));
        Assert.StartsWith(inicio, ZoneNameOrigin.Frase(z), StringComparison.Ordinal);
        Assert.EndsWith(".", ZoneNameOrigin.Frase(z), StringComparison.Ordinal);
    }

    [Fact]
    public void Nome_do_OpenStreetMap_traz_o_credito_e_a_licenca()
    {
        string frase = ZoneNameOrigin.Frase(Z(Zona.NomeOsm));
        Assert.Contains(ZoneNameOrigin.CreditoOsm, frase, StringComparison.Ordinal);
        Assert.Contains("ODbL", frase, StringComparison.Ordinal);
        Assert.Equal("© colaboradores do OpenStreetMap", ZoneNameOrigin.CreditoOsm);
    }

    [Fact]
    public void Mapa_mostra_o_credito_so_com_zona_do_OpenStreetMap()
    {
        var semOsm = new ZonasMunicipio { CodigoIbge = 3139607, Zonas = [Z(Zona.NomeBairro), Z(Zona.NomeGerado, "3139607-02")] };
        var comOsm = new ZonasMunicipio { CodigoIbge = 3139607, Zonas = [Z(Zona.NomeBairro), Z(Zona.NomeOsm, "3139607-02")] };

        Assert.False(semOsm.UsaOpenStreetMap);
        Assert.Null(ZoneNameOrigin.Credito(semOsm));
        Assert.True(comOsm.UsaOpenStreetMap);
        Assert.Equal(ZoneNameOrigin.CreditoOsm, ZoneNameOrigin.Credito(comOsm));
    }

    [Fact]
    public void Validacao_aceita_bairro_osm_e_recusa_fonte_desconhecida()
    {
        ZonasUf File(string fonte) => new()
        {
            Uf = "MG",
            Fonte = "teste",
            Metodo = "teste",
            Municipios = [new ZonasMunicipio { CodigoIbge = 3139607, Zonas = [Z(fonte)] }],
        };

        Assert.Empty(File(Zona.NomeOsm).Validate());
        Assert.Contains(File("bairro_outro").Validate(), e => e.Contains("fonte do nome", StringComparison.Ordinal));
    }

    [Fact]
    public void Registro_do_OpenStreetMap_em_fontes_confere_licenca_data_e_contagem()
    {
        const string Bom = """{ "openstreetmap": { "licenca": "Open Database License (ODbL) 1.0", "credito": "© colaboradores do OpenStreetMap", "url": "https://www.openstreetmap.org/copyright", "extraido_em": "2026-11-17T09:22:00+00:00", "zonas_com_nome_osm": 3 } }""";

        Assert.Empty(ZonasValidation.CheckOsmRecord(Bom, 3));
        Assert.Empty(ZonasValidation.CheckOsmRecord("{}", 0));
        Assert.NotEmpty(ZonasValidation.CheckOsmRecord("{}", 3));
        Assert.NotEmpty(ZonasValidation.CheckOsmRecord(Bom, 4));
        Assert.NotEmpty(ZonasValidation.CheckOsmRecord(Bom.Replace("ODbL", "CC0", StringComparison.Ordinal), 3));
    }
}
