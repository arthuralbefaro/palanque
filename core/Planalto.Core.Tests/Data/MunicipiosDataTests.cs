using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.Municipios;

namespace Planalto.Core.Tests.Data;

/// <summary>
/// Integridade de data/municipios/ gerado pelo DataImporter. Os totais oficiais vêm de _fontes.json,
/// onde o importador grava o que leu das tabelas de UF e do arquivo do TSE (não da soma dos municípios).
/// </summary>
public class MunicipiosDataTests
{
    private static readonly Lazy<MunicipiosDataSet> Data = new(Load);

    private static MunicipiosDataSet Load()
    {
        string dataDirectory = DataPaths.FindDataDirectory(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("Pasta data/ não encontrada.");
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();

        var (dataSet, errors) = MunicipiosDataSet.Load(dataDirectory, portes);
        Assert.True(errors.Count == 0, string.Join("\n", errors.Take(20)));
        return dataSet!;
    }

    private static List<Municipio> All => Data.Value.All.ToList();

    [Fact]
    public void ContagemTotal_IgualAListaOficialDoIbge()
    {
        Assert.Equal(Data.Value.Fontes.TotaisOficiais.TotalMunicipios, All.Count);
        Assert.Equal(MunicipiosDataSet.UfCount, Data.Value.Files.Count);
    }

    [Fact]
    public void CodigosIbge_SemDuplicatasEComSeteDigitos()
    {
        Assert.Equal(All.Count, All.Select(m => m.CodigoIbge).Distinct().Count());
        Assert.All(All, m => Assert.InRange(m.CodigoIbge, 1_000_000, 9_999_999));
    }

    [Fact]
    public void PopulacaoPorUf_IgualAoTotalOficialSemTolerancia()
    {
        var soma = All.GroupBy(m => m.Uf).ToDictionary(g => g.Key, g => g.Sum(m => m.Populacao));

        Assert.Equal(MunicipiosDataSet.UfCount, Data.Value.Fontes.TotaisOficiais.PopulacaoPorUf.Count);
        foreach (var (uf, oficial) in Data.Value.Fontes.TotaisOficiais.PopulacaoPorUf)
        {
            Assert.True(soma[uf] == oficial, $"{uf}: soma {soma[uf]}, oficial {oficial}");
        }
    }

    [Fact]
    public void Eleitorado_MaiorQueZeroExcetoCasosEspeciais()
    {
        Assert.All(All.Where(m => m.EleicoesMunicipais), m => Assert.True(m.Eleitorado > 0, $"{m.CodigoIbge} {m.Nome}"));
    }

    [Fact]
    public void CasosEspeciais_DistritoFederalEFernandoDeNoronhaSemEleicaoMunicipal()
    {
        List<Municipio> semEleicao = All.Where(m => !m.EleicoesMunicipais).ToList();

        Assert.Equal(2, semEleicao.Count);
        Assert.Contains(semEleicao, m => m.Uf == "DF");
        Assert.Contains(semEleicao, m => m.Uf == "PE" && m.Nome == "Fernando de Noronha");
        Assert.All(semEleicao, m => Assert.Equal(0, m.VagasVereador));
    }

    [Fact]
    public void VagasVereador_SomaIgualAoTotalDoTse2024()
    {
        long soma = All.Where(m => m.VagasFonte == Municipio.VagasFonteTse2024).Sum(m => (long)m.VagasVereador);

        Assert.Equal(Data.Value.Fontes.TotaisOficiais.VagasVereadorTse2024, soma);
    }

    [Fact]
    public void VagasAcimaDoTeto_EstaoTodasListadasEmFontes()
    {
        // Não é erro: a Câmara fixou o número quando a população era maior. Só precisa estar documentado.
        var acimaDoTeto = Data.Value.VagasAcimaDoTeto().Select(m => m.CodigoIbge).Order().ToList();
        var listados = Data.Value.Fontes.ExcecoesVagas.Select(e => e.CodigoIbge).Order().ToList();

        Assert.Equal(acimaDoTeto, listados);
        Assert.All(All.Where(m => m.EleicoesMunicipais && !listados.Contains(m.CodigoIbge)),
            m => Assert.True(m.VagasVereador <= m.VagasVereadorMax, $"{m.CodigoIbge} {m.Nome}"));
    }

    [Fact]
    public void RegrasDerivadas_UsamAsFuncoesDoCore()
    {
        Assert.All(All.Where(m => m.EleicoesMunicipais), m =>
        {
            Assert.Equal(ElectionRules.VagasVereador(m.Populacao), m.VagasVereadorMax);
            Assert.Equal(ElectionRules.TemSegundoTurno(m.Eleitorado), m.SegundoTurnoPrefeito);
        });
    }

    [Fact]
    public void Coordenadas_TodosOsMunicipiosComLatLonEMetodoRegistrado()
    {
        Assert.All(All, m =>
        {
            Assert.InRange(m.Lat, -34.0, 6.0);
            Assert.InRange(m.Lon, -74.0, -28.0);
            Assert.True(m.AreaKm2 > 0, $"{m.CodigoIbge} sem área");
            Assert.Contains(m.CoordenadaMetodo, new[] { Municipio.CoordenadaCentroide, Municipio.CoordenadaPontoInterior });
        });
    }

    [Fact]
    public void MunicipioSemCenso_UsaPerfilDaUfComLacunaRegistrada()
    {
        foreach (Municipio m in All.Where(m => m.PerfilFonte == Municipio.PerfilFonteUf))
        {
            Assert.Contains(Data.Value.Fontes.Lacunas, l => l.CodigoIbge == m.CodigoIbge && l.Campo == "perfil");
        }

        Assert.All(All.Where(m => m.Microrregiao is null),
            m => Assert.Contains(Data.Value.Fontes.Lacunas, l => l.CodigoIbge == m.CodigoIbge && l.Campo == "microrregiao"));
    }
}
