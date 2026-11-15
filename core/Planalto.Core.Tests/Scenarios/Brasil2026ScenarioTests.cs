using System.Text.Json;
using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.Municipios;
using Planalto.Core.Parties;
using Planalto.Core.Scenarios;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Scenarios;

public class Brasil2026ScenarioTests
{
    private static readonly Lazy<(Brasil2026Scenario Scenario, MunicipiosDataSet Municipios)> Loaded = new(() =>
    {
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(TestContent.DataDirectory, "portes.json")).GetValueOrThrow();
        MunicipiosDataSet municipios = MunicipiosDataSet.Load(TestContent.DataDirectory, portes).DataSet!;
        var (scenario, errors) = Brasil2026Scenario.Load(TestContent.DataDirectory, municipios, TestContent.Parties);
        Assert.True(scenario is not null, string.Join("\n", errors));
        return (scenario!, municipios);
    });

    private static Brasil2026Scenario Scenario => Loaded.Value.Scenario;

    private static MunicipiosDataSet Municipios => Loaded.Value.Municipios;

    [Fact]
    public void Nacional_TemOsTrintaPartidosECasasCompletas()
    {
        NationalScenarioFile national = Scenario.National;

        Assert.Equal(30, national.Parties.Count);
        Assert.Equal(513, national.Camara.Seats);
        Assert.Equal(513, national.Camara.ByParty.Values.Sum());
        Assert.Equal(81, national.Senado.ByParty.Values.Sum());
        Assert.Equal(5, national.Federations.Count);
        Assert.Contains(national.Presidente.Party, national.BaseGovernista);
        Assert.Empty(national.BaseGovernista.Intersect(national.Oposicao));
    }

    [Fact]
    public void Partidos_NenhumNomeOuSiglaReal()
    {
        PartyBlocklist blocklist = TestContent.Parties.Blocklist;

        Assert.All(Scenario.National.Parties, p =>
        {
            Assert.Null(blocklist.CheckName(p.Name));
            Assert.Null(blocklist.CheckSigla(p.Sigla));
        });
    }

    [Fact]
    public void Arquivos_NaoContemNenhumNomeDePartidoReal()
    {
        PartyBlocklist blocklist = TestContent.Parties.Blocklist;
        string directory = Path.Combine(TestContent.DataDirectory, ScenarioPaths.Brasil2026Directory);

        foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            foreach (string text in Strings(document.RootElement).Where(s => s.Contains(' ')))
            {
                Assert.True(blocklist.CheckName(text) is null, $"{Path.GetFileName(file)}: \"{text}\"");
            }
        }
    }

    [Fact]
    public void Partidos_ClassificadosSeguemARegraDiagonal()
    {
        int max = TestContent.Parties.Generator.Scenario.SocialVariationMax;
        foreach (ScenarioParty party in Scenario.National.Parties.Where(p => p.Classified))
        {
            int economic = (int)Math.Round((party.SourceScore!.Value - 5) * 20, MidpointRounding.AwayFromZero);
            Assert.Equal(economic, party.Position.Economic);
            Assert.InRange(party.Position.Social - party.Position.Economic, -max, max);
            Assert.Equal(ScenarioParty.SocialVariationNote, party.SocialVariationOrigin);
        }

        Assert.Single(Scenario.National.Parties, p => !p.Classified);
    }

    [Fact]
    public void Municipios_CamaraIgualAsVagasEPrefeitoComPartido()
    {
        var gaps = Scenario.National.Gaps;
        int checkedCount = 0;
        foreach (Municipio municipio in Municipios.All.Where(m => m.EleicoesMunicipais))
        {
            MunicipalScenario m = Scenario.Municipio(municipio.CodigoIbge)!;
            Assert.NotNull(m);
            bool semFonte = gaps.Any(g => g.Kind == Brasil2026Scenario.GapCamaraSemFonte && g.Ibge == m.Ibge);
            Assert.True(m.Camara.Values.Sum() == municipio.VagasVereador || semFonte, $"{municipio.Nome}");
            Assert.True(m.Prefeito is not null || gaps.Any(g => g.Kind == Brasil2026Scenario.GapPrefeito && g.Ibge == m.Ibge), $"{municipio.Nome}");
            Assert.Equal(MunicipalScenario.BasisPoints, m.ForcaVereador.Values.Sum());
            checkedCount++;
        }

        Assert.Equal(5569, checkedCount);
        Assert.True(gaps.Count(g => g.Ibge is not null) < 40, "Lacunas municipais acima do esperado; conferir a fonte.");
    }

    [Fact]
    public void Municipios_SemEleicaoMunicipalNaoEntram()
    {
        foreach (Municipio municipio in Municipios.All.Where(m => !m.EleicoesMunicipais))
        {
            Assert.Null(Scenario.Municipio(municipio.CodigoIbge));
        }
    }

    [Fact]
    public void Estados_TemGovernadorTresSenadoresEAssembleiaFechada()
    {
        Assert.Equal(27, Scenario.States.Count);
        Assert.All(Scenario.States.Values, s =>
        {
            Assert.Equal(3, s.Senadores.Count);
            Assert.Equal(s.Assembleia.Seats, s.Assembleia.ByParty.Values.Sum());
        });
        Assert.Equal(1059, Scenario.States.Values.Sum(s => s.Assembleia.Seats));
    }

    [Fact]
    public void Arquivos_CabemNaMetaDeCincoMegabytes()
    {
        string directory = Path.Combine(TestContent.DataDirectory, ScenarioPaths.Brasil2026Directory);
        long bytes = Directory.EnumerateFiles(directory, "*.json").Sum(f => new FileInfo(f).Length);

        Assert.True(bytes <= 5 * 1024 * 1024, $"{bytes} bytes");
    }

    [Fact]
    public void FundoETempoDeTv_SaemDasBancadas()
    {
        NationalScenarioFile national = Scenario.National;
        var ids = national.Parties.Select(p => p.Id).ToList();

        Dictionary<string, double> fund = PartyFunding.ElectoralFundShares(ids, national.CamaraVotes, national.Camara.ByParty, national.Senado.ByParty);
        Dictionary<string, double> tv = PartyFunding.TvTimeShares(ids, national.Camara.ByParty);

        Assert.Equal(1, fund.Values.Sum(), 9);
        Assert.Equal(1, tv.Values.Sum(), 9);
        string largest = national.Camara.ByParty.MaxBy(p => p.Value).Key;
        Assert.Equal(largest, tv.MaxBy(p => p.Value).Key);
    }



    private static IEnumerable<string> Strings(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => [element.GetString()!],
        JsonValueKind.Array => element.EnumerateArray().SelectMany(Strings),
        JsonValueKind.Object => element.EnumerateObject().SelectMany(p => Strings(p.Value)),
        _ => [],
    };
}
