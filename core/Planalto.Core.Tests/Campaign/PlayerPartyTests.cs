using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.State;
using Planalto.Core.World;

namespace Planalto.Core.Tests.Campaign;

public class PlayerPartyTests
{
    /// <summary>
    /// O partido escolhido na criação é o do estado do jogo e o da candidatura, com a sigla e o número dele, seja o
    /// partido forte ou sem força na cidade (quando ele entra na disputa no lugar do mais fraco da lista).
    /// </summary>
    [Fact]
    public void O_partido_escolhido_na_criacao_e_o_da_candidatura_em_todo_partido_do_cenario()
    {
        const int ibge = 4202008;
        var m = CampaignKit.Municipio(ibge);
        WorldSetup world = WorldSetup.Create(CampaignKit.C.World, "brasil_2026", new Rng(2028)).Setup!;
        Assert.Contains(world.Parties, p => p.Sigla == "FRB");

        foreach (PartyDraft chosen in world.Parties)
        {
            var rng = new Rng(7);
            Character player = PersonGenerator.Generate(rng, CampaignKit.C.People, new PersonRequest(RaceSetup.PlayerId, m.Uf, m.CodigoIbge, new DateOnly(world.Options.StartYear, 1, 1), Age: 35, PartyId: chosen.Id));
            player.Campaign = new CampaignIdentity
            {
                BallotNumber = $"{chosen.Number}007",
                Slogan = "Teste",
                LogoIcon = "sol",
                LogoFont = "sans_forte",
                LogoPrimaryColor = chosen.Colors.Primary,
                LogoSecondaryColor = chosen.Colors.Secondary,
                Jingle = "forro",
                PhotoPose = "frente",
                PhotoBackground = "background_solido",
            };

            GameState state = NewGame.Create(world, player, 2028);
            CampaignState campaign = RaceSetup.Create(CampaignKit.Context(state), new Rng(2028));

            Assert.Equal(chosen.Id, state.Player.PartyId);
            Assert.Equal(chosen.Sigla, state.Party(state.Player.PartyId)!.Sigla);
            Assert.Equal(chosen.Id, campaign.Player.PartyId);
            Assert.Equal($"{chosen.Number}007", campaign.Player.Number);
            Assert.Contains(campaign.Candidates, c => c.IsBulk && c.PartyId == chosen.Id && c.Name == chosen.Sigla);
        }
    }
}
