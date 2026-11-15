using Planalto.Core.Data;
using Planalto.Core.Electorate;
using Planalto.Core.Ideology;
using Planalto.Core.Municipios;
using Planalto.Core.Scenarios;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Electorate;

public class ElectorateTests
{
    private static readonly Lazy<Fixture> Data = new(() => new Fixture());

    private static Fixture F => Data.Value;

    [Fact]
    public void Fatias_NormalizamPelaSomaDasCategoriasSemAsExcluidas()
    {
        SegmentDimension idade = F.Electorate.Segments.Dimensions.Single(d => d.Id == "idade");
        var block = new Dictionary<string, long>
        {
            ["0_15"] = 1000, ["16_17"] = 10, ["18_24"] = 20, ["25_34"] = 20, ["35_44"] = 20, ["45_59"] = 10, ["60_69"] = 10, ["70_mais"] = 10,
        };

        Dictionary<string, double> shares = ElectorateBuilder.GroupShares(idade, block);

        Assert.Equal(1, shares.Values.Sum(), 9);
        Assert.Equal(0.1, shares["idade_16_17"], 9);
        Assert.Equal(0.4, shares["idade_18_34"], 9);
    }

    [Fact]
    public void Segmentos_SomamOEleitoradoDoTse()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);

        Assert.Equal(240, electorate.Segments.Count);
        Assert.Equal(F.SaoPaulo.Eleitorado, electorate.Voters, 3);
        Assert.Equal(electorate.Segments.Count, electorate.Segments.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Afinidade_ReproduzAForcaLocalDe2024()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);
        MunicipalScenario local = F.Scenario.Municipio(F.SaoPaulo.CodigoIbge)!;
        double voters = electorate.Voters;

        foreach (ElectoralParty party in F.Parties)
        {
            double mean = electorate.Segments.Sum(s => s.Voters * s.Affinity[party.Id]) / voters;
            double expected = local.ForcaVereador.GetValueOrDefault(party.Id) / (double)MunicipalScenario.BasisPoints;
            Assert.True(Math.Abs(mean - expected) < 1e-3, $"{party.Id}: {mean:0.0000} vs {expected:0.0000}");
        }

        Assert.All(electorate.Segments, s => Assert.Equal(1, s.Affinity.Values.Sum(), 6));
    }

    [Fact]
    public void Afinidade_VariaEntreSegmentosPelaIdeologia()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);
        string right = F.Parties.Where(p => electorate.PartyStrength[p.Id] > 0.02).MaxBy(p => p.Position.Economic)!.Id;
        Segment richest = electorate.Segments.Where(s => s.Has("alta")).MaxBy(s => s.Ideology.Economic)!;
        Segment poorest = electorate.Segments.Where(s => s.Has("baixa")).MinBy(s => s.Ideology.Economic)!;

        Assert.True(richest.Affinity[right] > poorest.Affinity[right]);
    }

    [Fact]
    public void MundoSorteado_GeraForcaPeloTamanhoEProximidade()
    {
        MunicipalElectorate a = ElectorateBuilder.Build(F.SaoPaulo, F.Electorate.Segments, F.Electorate.VoteModel, F.Themes, F.Parties, null, new Rng(5));
        MunicipalElectorate b = ElectorateBuilder.Build(F.SaoPaulo, F.Electorate.Segments, F.Electorate.VoteModel, F.Themes, F.Parties, null, new Rng(5));

        Assert.Equal(1, a.PartyStrength.Values.Sum(), 9);
        Assert.Equal(a.PartyStrength, b.PartyStrength);
        Assert.All(a.Segments, s => Assert.Equal(1, s.Affinity.Values.Sum(), 6));
    }

    [Fact]
    public void VotoFacultativo_ComparecemMenos()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);
        double facultative = electorate.Segments.Where(s => s.Facultative).Average(s => s.Turnout);
        double compulsory = electorate.Segments.Where(s => !s.Facultative).Average(s => s.Turnout);

        Assert.True(facultative < compulsory * 0.7);
        Assert.All(electorate.Segments, s => Assert.InRange(s.Turnout, 0, 1));
    }

    [Fact]
    public void Projecao_FechaComOEleitoradoESharesSomamUm()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);
        VoteProjection projection = VoteModel.Project(electorate, F.Candidates(), F.Electorate.VoteModel, F.Themes);

        Assert.Equal(electorate.Voters, projection.Turnout + projection.Abstention, 3);
        Assert.All(projection.Segments, s => Assert.Equal(1, s.Shares.Values.Sum(), 9));
        Assert.All(projection.Segments, s => Assert.InRange(s.BlankNull, 0, F.Electorate.VoteModel.BlankNullMax));
        double turnout = projection.Turnout / electorate.Voters;
        Assert.InRange(turnout, 0.6, 0.9);
    }

    [Fact]
    public void Projecao_ReconhecimentoEImagemAumentamOsVotos()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);
        List<VoteCandidate> baseline = F.Candidates();
        VoteCandidate target = baseline[0];
        var boosted = baseline.Select(c => c == target
            ? new VoteCandidate { Id = c.Id, PartyId = c.PartyId, Position = c.Position, Recognition = 1, Image = 0.8 }
            : c).ToList();

        double before = VoteModel.Project(electorate, baseline, F.Electorate.VoteModel, F.Themes).ValidShares[target.Id];
        double after = VoteModel.Project(electorate, boosted, F.Electorate.VoteModel, F.Themes).ValidShares[target.Id];

        Assert.True(after > before + 0.02, $"{before:0.000} → {after:0.000}");
    }

    [Fact]
    public void Projecao_CandidatoIdeologicamenteProximoLeva()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);
        string party = F.Parties[0].Id;
        var near = new VoteCandidate { Id = "perto", PartyId = party, Position = Round(electorate.Center), Recognition = 0.5 };
        var far = new VoteCandidate { Id = "longe", PartyId = party, Position = new IdeologicalPosition(-100, 100), Recognition = 0.5 };

        VoteProjection projection = VoteModel.Project(electorate, [near, far], F.Electorate.VoteModel, F.Themes);

        Assert.True(projection.ValidShares["perto"] > 0.6);
    }

    [Fact]
    public void Projecao_ComRuidoEDeterministicaPelaSemente()
    {
        MunicipalElectorate electorate = F.Build(F.SaoPaulo);
        var a = VoteModel.Project(electorate, F.Candidates(), F.Electorate.VoteModel, F.Themes, new Rng(9)).ValidShares;
        var b = VoteModel.Project(electorate, F.Candidates(), F.Electorate.VoteModel, F.Themes, new Rng(9)).ValidShares;
        var c = VoteModel.Project(electorate, F.Candidates(), F.Electorate.VoteModel, F.Themes, new Rng(10)).ValidShares;

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    internal static IdeologicalPosition Round(AxisPoint p) => new((int)Math.Round(p.Economic), (int)Math.Round(p.Social));

    internal sealed class Fixture
    {
        public Fixture()
        {
            PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(TestContent.DataDirectory, "portes.json")).GetValueOrThrow();
            Municipios = MunicipiosDataSet.Load(TestContent.DataDirectory, portes).DataSet!;
            Themes = TestContent.Parties.Themes;
            IReadOnlyList<DataFileReport> reports = ElectorateContent.Load(TestContent.DataDirectory, Themes, Municipios, out ElectorateContent? electorate);
            Electorate = electorate ?? throw new InvalidOperationException(string.Join("\n", reports.SelectMany(r => r.Errors)));
            Scenario = Brasil2026Scenario.Load(TestContent.DataDirectory, Municipios, TestContent.Parties).Scenario!;
            var weights = TestContent.Parties.Generator.SizeShareWeights;
            Parties = Scenario.National.Parties.Select(p => new ElectoralParty(p.Id, p.Position, weights[p.Size])).ToList();
            SaoPaulo = Municipios.All.Single(m => m.CodigoIbge == 3550308);
        }

        public MunicipiosDataSet Municipios { get; }

        public ThemeCatalog Themes { get; }

        public ElectorateContent Electorate { get; }

        public Brasil2026Scenario Scenario { get; }

        public List<ElectoralParty> Parties { get; }

        public Municipio SaoPaulo { get; }

        public MunicipalElectorate Build(Municipio m) =>
            ElectorateBuilder.Build(m, Electorate.Segments, Electorate.VoteModel, Themes, Parties, Scenario.Municipio(m.CodigoIbge)!.ForcaVereador, new Rng(1));

        /// <summary>Um candidato de cada um dos quatro partidos mais fortes de São Paulo.</summary>
        public List<VoteCandidate> Candidates()
        {
            MunicipalScenario local = Scenario.Municipio(SaoPaulo.CodigoIbge)!;
            return local.ForcaVereador.OrderByDescending(f => f.Value).Take(4)
                .Select(f => Parties.Single(p => p.Id == f.Key))
                .Select(p => new VoteCandidate { Id = $"c_{p.Id}", PartyId = p.Id, Position = p.Position, Recognition = 0.4 })
                .ToList();
        }
    }
}
