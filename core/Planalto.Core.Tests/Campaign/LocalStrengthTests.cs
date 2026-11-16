using Planalto.Core.Campaign;
using Planalto.Core.State;
using Xunit;

namespace Planalto.Core.Tests.Campaign;

public class LocalStrengthTests
{
    [Theory]
    [InlineData(3550308, 11ul, "brasil_2026")]
    [InlineData(5200852, 22ul, "brasil_2026")]
    [InlineData(2927408, 33ul, "multipartidario_moderado")]
    public void Forca_mostrada_na_criacao_e_a_mesma_que_a_campanha_usa(int ibge, ulong seed, string preset)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed, preset);
        GameState state = engine.Context.State;
        var parties = CampaignContext.ElectoralParties(state.Parties, CampaignKit.C.World.Parties.Generator.SizeShareWeights);

        // Antes da carreira só existem a semente do mundo e os partidos; o resultado tem de bater com o da campanha.
        var before = CampaignContext.PartyStrength(state.World!.Seed, ibge, CampaignKit.C.Electorate, parties, engine.Context.Local);

        Assert.Equal(engine.Context.Electorate.PartyStrength.Count, before.Count);
        foreach (var (party, share) in engine.Context.Electorate.PartyStrength)
        {
            Assert.Equal(share, before[party], 12);
        }
    }
}
