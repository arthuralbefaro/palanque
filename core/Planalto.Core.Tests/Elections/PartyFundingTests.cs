using Planalto.Core.Elections;

namespace Planalto.Core.Tests.Elections;

public class PartyFundingTests
{
    private static readonly string[] Parties = ["a", "b", "c", "d"];

    [Fact]
    public void FundoEleitoral_SegueAsQuatroParcelasDoArt16D()
    {
        // Câmara: a=6, b=4 (c e d sem deputado); votos: a=600, b=300, c=100; Senado: a=1, c=3.
        var votes = new Dictionary<string, long> { ["a"] = 600, ["b"] = 300, ["c"] = 100 };
        var camara = new Dictionary<string, int> { ["a"] = 6, ["b"] = 4 };
        var senado = new Dictionary<string, int> { ["a"] = 1, ["c"] = 3 };

        Dictionary<string, double> shares = PartyFunding.ElectoralFundShares(Parties, votes, camara, senado);

        // a: 0,5% + 35%×600/900 + 48%×6/10 + 15%×1/4 = 0,005 + 0,23333 + 0,288 + 0,0375
        Assert.Equal(0.005 + (0.35 * 600 / 900) + (0.48 * 0.6) + (0.15 * 0.25), shares["a"], 9);
        // b: 0,5% + 35%×300/900 + 48%×4/10
        Assert.Equal(0.005 + (0.35 * 300 / 900) + (0.48 * 0.4), shares["b"], 9);
        // c tem votos mas nenhum deputado: fica fora dos 35%.
        Assert.Equal(0.005 + (0.15 * 0.75), shares["c"], 9);
        Assert.Equal(0.005, shares["d"], 9);
        Assert.Equal(1, shares.Values.Sum(), 9);
    }

    [Fact]
    public void TempoDeTv_NoventaPorCentoPelaBancadaEDezIgual()
    {
        var camara = new Dictionary<string, int> { ["a"] = 3, ["b"] = 1 };

        Dictionary<string, double> shares = PartyFunding.TvTimeShares(Parties, camara);

        Assert.Equal(0.025 + (0.9 * 0.75), shares["a"], 9);
        Assert.Equal(0.025 + (0.9 * 0.25), shares["b"], 9);
        Assert.Equal(0.025, shares["c"], 9);
        Assert.Equal(1, shares.Values.Sum(), 9);
    }

    [Fact]
    public void ParcelasQueNaoSomamCem_SaoRejeitadas()
    {
        var rules = PartyFundingRulesConfig.Default with { FundEqualShare = 0.1 };

        Assert.Throws<ArgumentException>(() => PartyFunding.TvTimeShares(Parties, new Dictionary<string, int>(), rules with { TvEqualShare = 0.2 }));
        Assert.Throws<ArgumentException>(() => PartyFunding.ElectoralFundShares(Parties, new Dictionary<string, long>(), new Dictionary<string, int>(), new Dictionary<string, int>(), rules));
    }
}
