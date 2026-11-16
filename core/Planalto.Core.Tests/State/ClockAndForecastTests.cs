using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Events;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.State;

/// <summary>Tempo corrente (GDD 17.4) e previsão antes do clique (GDD 17.1, f).</summary>
public class ClockAndForecastTests
{
    private static TimeConfig Time => JsonData.LoadFile<TimeConfig>(Path.Combine(TestContent.DataDirectory, TimeConfig.File)).GetValueOrThrow();

    [Fact]
    public void Sete_dias_produzem_o_mesmo_estado_que_uma_semana()
    {
        CampaignEngine byWeek = CampaignKit.NewCareer(3106200, 31);
        CampaignEngine byDay = CampaignKit.NewCareer(3106200, 31);
        for (int week = 0; week < 6; week++)
        {
            byWeek.Context.State.Campaign!.Events.Pending.Clear();
            byDay.Context.State.Campaign!.Events.Pending.Clear();
            byWeek.AdvanceWeek();
            int turned = 0;
            for (int day = 0; day < 7; day++)
            {
                turned += Clock.AdvanceDay(byDay.Context.State, byDay.AdvanceWeek) ? 1 : 0;
            }

            Assert.Equal(1, turned);
            Assert.Equal(0, byDay.Context.State.Time.Day);
            Assert.Equal(GameStateSerializer.Serialize(byWeek.Context.State), GameStateSerializer.Serialize(byDay.Context.State));
        }
    }

    [Fact]
    public void Dia_anda_dentro_da_semana_e_hoje_acompanha()
    {
        GameState state = CampaignKit.NewCareer(3106200, 32).Context.State;
        DateOnly monday = state.Calendar.WeekStart;
        Assert.False(Clock.AdvanceDay(state, () => throw new InvalidOperationException("não é domingo")));
        Assert.Equal(monday.AddDays(1), Clock.Today(state));
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
    }

    [Fact]
    public void Pausas_respeitam_o_padrao_e_so_as_desligaveis_aceitam_escolha()
    {
        TimeConfig config = Time;
        Assert.Empty(config.Validate());
        var off = Clock.PauseIds.ToDictionary(id => id, _ => false);
        Assert.True(Clock.ShouldPause(config, Clock.DecisaoImediata, off));
        Assert.True(Clock.ShouldPause(config, Clock.FaseNova, off));
        Assert.False(Clock.ShouldPause(config, Clock.PesquisaPublicada, off));
        Assert.True(Clock.ShouldPause(config, Clock.PesquisaPublicada, null));
        Assert.False(Clock.ShouldPause(config, Clock.NoticiaComum, null));
        Assert.Equal(1.0, Clock.SecondsPerDay(config, Clock.ModoCampanha, 4));
        Assert.Equal(7.0, 7 * Clock.SecondsPerDay(config, Clock.ModoCampanha, 4), 3);
    }

    [Fact]
    public void Resultado_de_cada_acao_cai_na_faixa_prevista_em_mil_execucoes()
    {
        CampaignEngine engine = CampaignKit.NewCareer(2927408, 33);
        CampaignState campaign = engine.Context.State.Campaign!;
        campaign.Events.Pending.Clear();
        campaign.Phase = CampaignPhase.Campanha;
        var actions = engine.Context.Campaign.Actions.Actions;
        int inside = 0;
        for (int i = 0; i < 1_000; i++)
        {
            CampaignActionDefinition action = actions[i % actions.Count];
            string? target = action.Targets.Count > 0 ? action.Targets[i % action.Targets.Count].Id : null;
            campaign.EnergyLeft = engine.Context.Config.WeeklyEnergy;
            engine.Context.State.Player.Resources.Money = 1_000_000;
            campaign.Player.Money = engine.Context.State.Player.Resources.Money;

            Forecast forecast = engine.Forecast(action.Id, target);
            Candidate p = campaign.Player;
            (double r, double img, decimal money, Dictionary<string, double> support) before = (p.Recognition, p.Image, engine.Context.State.Player.Resources.Money, new Dictionary<string, double>(p.Support));
            ActionResult result = engine.Execute(action.Id, target);
            Assert.True(result.Ok, $"{action.Id}: {result.Failure}");

            bool ok = forecast.Contains("recognition", null, p.Recognition - before.r)
                && forecast.Contains("image", null, p.Image - before.img)
                && forecast.Contains("money", null, (double)(engine.Context.State.Player.Resources.Money - before.money))
                && p.Support.Keys.Union(before.support.Keys).All(g => forecast.Contains("support", g, p.Support.GetValueOrDefault(g) - before.support.GetValueOrDefault(g)));
            inside += ok ? 1 : 0;

            // Reconhecimento e imagem voltam ao meio para a próxima rodada não saturar no teto.
            p.Recognition = 30;
            p.Image = 0;
            p.Support.Clear();
        }

        Assert.True(inside >= 800, $"{inside} de 1000 dentro da faixa");
        Assert.Equal(1_000, inside);
    }

    [Fact]
    public void Previsao_mostra_os_desfechos_sorteados_com_a_chance()
    {
        CampaignEngine engine = CampaignKit.NewCareer(2927408, 34);
        engine.Context.State.Campaign!.Phase = CampaignPhase.Campanha;
        Forecast comicio = engine.Forecast("comicio");
        Assert.Contains(comicio.Outcomes, o => Math.Abs(o.Probability - 0.12) < 1e-9);
        ForecastLine image = comicio.Lines.Single(l => l.Kind == "image");
        Assert.True(image.Min < 0 && image.Max > 0, "o comício pode render ou custar imagem");
        Assert.True(comicio.Lines.Single(l => l.Kind == "money").Max < 0);
    }

    [Fact]
    public void Escolha_de_evento_cai_na_faixa_prevista()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3550308, 35);
        CampaignContext ctx = engine.Context;
        foreach (EventDefinition e in CampaignKit.C.Events.Events)
        {
            for (int choice = 0; choice < e.Choices.Count; choice++)
            {
                ctx.State.Campaign!.Events.Pending.Clear();
                ctx.State.Player.Resources.Money = 1_000_000;
                ctx.State.Player.Resources.PoliticalCapital = 100;
                PendingEvent pending = EventEngine.Trigger(ctx, e.Id, ctx.State.Rng);
                if (EventEngine.Blocked(ctx, e.Choices[choice]) is not null)
                {
                    continue;
                }

                Forecast forecast = Forecaster.ForChoice(ctx, e.Choices[choice]);
                Candidate p = ctx.State.Campaign.Player;
                double recognition = p.Recognition, image = p.Image;
                decimal money = ctx.State.Player.Resources.Money;
                EventEngine.Resolve(ctx, pending, choice, ctx.State.Rng);
                Assert.True(forecast.Contains("recognition", null, p.Recognition - recognition), $"{e.Id}/{choice}: reconhecimento");
                Assert.True(forecast.Contains("image", null, p.Image - image), $"{e.Id}/{choice}: imagem");
                Assert.True(forecast.Contains("money", null, (double)(ctx.State.Player.Resources.Money - money)), $"{e.Id}/{choice}: dinheiro");
                p.Recognition = 30;
                p.Image = 0;
            }
        }
    }
}
