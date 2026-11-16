using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.State;
using Xunit;

namespace Planalto.Core.Tests.Campaign;

/// <summary>
/// Guarda de balanceamento da campanha de vereador (DECISOES D-026): sem campanha não se elege; com o partido mais
/// forte da cidade e a energia gasta toda semana, elege-se na maior parte das vezes.
/// </summary>
public class CampaignBalanceTests
{
    private static List<int> Cities()
    {
        var all = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais).OrderBy(m => m.CodigoIbge).ToList();
        return new[] { "pequeno", "medio", "grande", "metropole" }
            .SelectMany(p => all.Where(m => m.Porte == p).Where((m, i) => i % 50 == 7).Take(2))
            .Select(m => m.CodigoIbge)
            .ToList();
    }

    [Fact]
    public void Sem_campanha_nao_se_elege_nem_com_o_partido_mais_forte()
    {
        foreach (int ibge in Cities())
        {
            CampaignEngine engine = CampaignKit.NewCareer(ibge, 3, strongestParty: true);
            PlayIdle(engine);
            Assert.False(engine.Context.State.Campaign!.Outcome!.PlayerElected, $"Eleito sem campanha em {ibge}.");
        }
    }

    [Fact]
    public void Campanha_ativa_no_partido_mais_forte_elege_na_maior_parte_das_vezes()
    {
        var cities = Cities();
        int wins = 0;
        foreach (int ibge in cities)
        {
            CampaignEngine engine = CampaignKit.NewCareer(ibge, 3, strongestParty: true);
            CampaignAutoplayTests.Play(engine, 3);
            wins += engine.Context.State.Campaign!.Outcome!.PlayerElected ? 1 : 0;
        }

        Assert.True(wins >= cities.Count / 2, $"Eleito em {wins} de {cities.Count}.");
    }

    private static void PlayIdle(CampaignEngine engine)
    {
        GameState state = engine.Context.State;
        while (state.Campaign!.Phase != CampaignPhase.Votacao)
        {
            while (state.Campaign.Events.Pending.Count > 0)
            {
                PendingEvent pending = state.Campaign.Events.Pending[0];
                var choices = engine.Context.Events!.Find(pending.EventId)!.Choices;
                EventEngine.Resolve(engine.Context, pending, Enumerable.Range(0, choices.Count).First(c => EventEngine.Blocked(engine.Context, choices[c]) is null), state.Rng);
            }

            engine.AdvanceWeek();
        }

        engine.CastPlayerVote(new UrnaVote(Office.Vereador, state.Campaign.Player.Number, false));
        engine.ResolveElection();
    }
}
