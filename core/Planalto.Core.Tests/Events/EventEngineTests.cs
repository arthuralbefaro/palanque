using System.Text.RegularExpressions;
using Planalto.Core.Campaign;
using Planalto.Core.Events;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;

namespace Planalto.Core.Tests.Events;

public partial class EventEngineTests
{
    private static EventContent Events => CampaignKit.C.Events;

    [Fact]
    public void Conteudo_TemAoMenos30EventosComVariaveisConhecidas()
    {
        Assert.True(Events.Events.Count >= 30);
        foreach (EventDefinition e in Events.Events)
        {
            var texts = e.Variants.Append(e.Title)
                .Concat(e.Choices.SelectMany(c => new[] { c.Label, c.Result, c.Test?.SuccessText, c.Test?.FailureText }))
                .Where(t => t is not null)!;
            foreach (Match m in texts.SelectMany(t => Variable().Matches(t!)))
            {
                Assert.Contains(m.Groups[1].Value, EventValidation.Variables);
            }
        }
    }

    [Fact]
    public void Cadeia_DisparaDepoisDoPrazo()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3550308, 1);
        CampaignState c = engine.Context.State.Campaign!;
        c.Events.Pending.Clear();
        c.Events.Scheduled.Add(new DueEvent("campanha_cobranca_lider", engine.Context.State.Calendar.WeekStart.AddDays(14)));

        engine.AdvanceWeek();
        Assert.DoesNotContain(c.Events.Pending, p => p.EventId == "campanha_cobranca_lider");
        ResolveAll(engine);
        engine.AdvanceWeek();
        Assert.Contains(c.Events.Pending, p => p.EventId == "campanha_cobranca_lider");
    }

    [Fact]
    public void Unico_NaoSeRepeteECooldownRespeitado()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3550308, 2);
        CampaignContext ctx = engine.Context;
        EventDefinition unique = Events.Find("campanha_meme_simpatico")!;
        ctx.State.Campaign!.Events.History.Add(new EventLogEntry(ctx.State.Calendar.WeekStart, unique.Id, null, ""));
        Assert.False(EventEngine.IsEligible(ctx, unique, ctx.State.Calendar.WeekStart));

        EventDefinition cooled = Events.Find("campanha_feira_livre")!;
        ctx.State.Campaign.Events.LastFired[cooled.Id] = ctx.State.Calendar.WeekStart;
        Assert.False(EventEngine.IsEligible(ctx, cooled, ctx.State.Calendar.WeekStart.AddDays(7)));
        Assert.True(EventEngine.IsEligible(ctx, cooled, ctx.State.Calendar.WeekStart.AddDays(7 * cooled.CooldownWeeks)));
    }

    [Fact]
    public void Escolha_ComRequisitoMostraOMotivo()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3550308, 3);
        var choice = new EventChoice { Label = "x", Requires = new ChoiceRequirement { MoneyMin = 1_000_000_000 } };

        Assert.StartsWith("Requer R$", EventEngine.Blocked(engine.Context, choice));
    }

    [Fact]
    public void Resolver_AplicaEfeitosEAgendaContinuacao()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3550308, 4);
        CampaignContext ctx = engine.Context;
        CampaignState c = ctx.State.Campaign!;
        decimal money = ctx.State.Player.Resources.Money;
        var pending = new PendingEvent { EventId = "campanha_doacao_por_fora", Title = "t", Text = "x", Variables = [] };
        c.Events.Pending.Add(pending);

        EventEngine.Resolve(ctx, pending, 0, ctx.State.Rng);

        Assert.Equal(money + 8000, ctx.State.Player.Resources.Money);
        Assert.Single(c.Events.Skeletons);
        Assert.Contains(c.Events.Scheduled, s => s.EventId == "campanha_investigacao_doacao");
        Assert.Empty(c.Events.Pending);
        Assert.Single(c.Events.History);
    }

    [Fact]
    public void Sorteio_GeraEventosNaCampanhaComFrequenciaNormal()
    {
        int fired = 0;
        for (ulong seed = 1; seed <= 3; seed++)
        {
            CampaignEngine engine = CampaignKit.NewCareer(2504009, seed);
            CampaignAutoplayTests.Play(engine, seed);
            fired += engine.Context.State.Campaign!.Events.History.Count;
        }

        Assert.InRange(fired / 3.0, 5, 40);
    }

    private static void ResolveAll(CampaignEngine engine)
    {
        CampaignState c = engine.Context.State.Campaign!;
        while (c.Events.Pending.Count > 0)
        {
            PendingEvent p = c.Events.Pending[0];
            EventDefinition d = Events.Find(p.EventId)!;
            int choice = Enumerable.Range(0, d.Choices.Count).First(i => EventEngine.Blocked(engine.Context, d.Choices[i]) is null);
            EventEngine.Resolve(engine.Context, p, choice, engine.Context.State.Rng);
        }
    }

    [GeneratedRegex(@"\{([a-z_]+\.[a-z_]+|[a-z_]+)\}")]
    private static partial Regex Variable();
}
