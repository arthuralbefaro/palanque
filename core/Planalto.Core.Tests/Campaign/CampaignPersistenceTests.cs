using Planalto.Core.Campaign;
using Planalto.Core.Save;
using Planalto.Core.State;
using Xunit;

namespace Planalto.Core.Tests.Campaign;

/// <summary>NPCs do município gerados quando ele entra em foco e persistidos no save (Fase 3).</summary>
public class CampaignPersistenceTests
{
    [Fact]
    [Trait("Categoria", "Lento")]
    public void Rivais_nomeados_viram_NPCs_persistidos_do_municipio_em_foco()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3304557, 7);
        GameState state = engine.Context.State;
        var named = state.Campaign!.Candidates.Where(c => !c.IsPlayer && !c.IsBulk).ToList();

        Assert.True(named.Count >= engine.Context.Config.NamedRivals, "Rivais nomeados a menos.");
        Assert.All(named, c =>
        {
            Assert.NotNull(c.CharacterId);
            Assert.Contains(state.Npcs, n => n.Id == c.CharacterId);
        });
        Assert.All(state.Npcs, n => Assert.Equal(state.FocusMunicipio, n.HomeMunicipio));
        Assert.Equal(state.Npcs.Count, state.Npcs.Select(n => n.Id).Distinct().Count());
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Carregar_o_save_retoma_a_mesma_disputa_sem_gerar_NPCs_novos()
    {
        CampaignEngine engine = CampaignKit.NewCareer(2927408, 21);
        CampaignState campaign = engine.Context.State.Campaign!;
        for (int i = 0; i < 5; i++)
        {
            // Resolve os eventos da semana pela primeira escolha disponível; um evento pendente pode ficar para o save.
            while (campaign.Events.Pending.Count > 0)
            {
                var pending = campaign.Events.Pending[0];
                var choices = engine.Context.Events!.Find(pending.EventId)!.Choices;
                int choice = Enumerable.Range(0, choices.Count).First(c => Planalto.Core.Events.EventEngine.Blocked(engine.Context, choices[c]) is null);
                Planalto.Core.Events.EventEngine.Resolve(engine.Context, pending, choice, engine.Context.State.Rng);
            }

            engine.AdvanceWeek();
        }

        string json = GameStateSerializer.Serialize(engine.Context.State);
        GameState loaded = GameStateSerializer.Deserialize(json);
        CampaignEngine resumed = CampaignEngine.Start(CampaignKit.Context(loaded));

        Assert.Equal(engine.Context.State.Npcs.Select(n => n.Id), resumed.Context.State.Npcs.Select(n => n.Id));
        Assert.Equal(engine.Context.State.Campaign!.Candidates.Select(c => c.Id), resumed.Context.State.Campaign!.Candidates.Select(c => c.Id));
        Assert.Equal(json, GameStateSerializer.Serialize(resumed.Context.State));
    }
}
