using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Debate;
using Planalto.Core.Elections;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Xunit;

namespace Planalto.Core.Tests.Debate;

public class DebateTests
{
    private static int MediumTown() =>
        CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == "medio").OrderBy(m => m.CodigoIbge).Skip(3).First().CodigoIbge;

    /// <summary>Candidato a prefeito numa cidade média, avançado até o primeiro debate de TV.</summary>
    private static CampaignEngine UntilDebate(ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(MediumTown(), seed, strongestParty: true);
        GameState state = engine.Context.State;
        Assert.Null(CareerFlow.SwitchCandidacy(engine.Context, Office.Prefeito).Issue);
        var policy = new Rng(seed);
        while (state.Campaign!.Debate is null)
        {
            Assert.True(state.Campaign.Phase != CampaignPhase.Votacao, "Nenhum debate antes da eleição.");
            while (state.Campaign.Events.Pending.Count > 0)
            {
                var pending = state.Campaign.Events.Pending[0];
                var choices = engine.Context.Events!.Find(pending.EventId)!.Choices;
                Planalto.Core.Events.EventEngine.Resolve(engine.Context, pending, Enumerable.Range(0, choices.Count).First(c => Planalto.Core.Events.EventEngine.Blocked(engine.Context, choices[c]) is null), state.Rng);
            }

            engine.AdvanceWeek();
        }

        return engine;
    }

    [Fact]
    public void Debate_e_marcado_na_campanha_de_prefeito_e_trava_a_semana()
    {
        CampaignEngine engine = UntilDebate(3);
        CampaignState campaign = engine.Context.State.Campaign!;
        DebateState debate = campaign.Debate!;
        DebateContent content = CampaignKit.C.Debate;

        Assert.Equal(RaceSetup.PlayerId, debate.Participants[0]);
        Assert.True(debate.Participants.Count >= 2);
        Assert.Equal(content.Rounds, debate.Topics.Count);
        Assert.Equal(content.HandSize, debate.Hand.Count);
        Assert.Contains(DebateEngine.Dates(campaign, content), d => engine.Context.State.Calendar.Contains(d));
        Assert.Throws<InvalidOperationException>(() => engine.AdvanceWeek());
    }

    [Fact]
    public void Rodadas_terminam_o_debate_e_mexem_na_imagem_dos_participantes()
    {
        CampaignEngine engine = UntilDebate(4);
        CampaignState campaign = engine.Context.State.Campaign!;
        DebateState debate = campaign.Debate!;
        var imageBefore = debate.Participants.ToDictionary(p => p, p => campaign.Find(p)!.Image);
        var recognitionBefore = debate.Participants.ToDictionary(p => p, p => campaign.Find(p)!.Recognition);

        while (!debate.Finished)
        {
            string card = debate.Hand[0];
            string? target = CampaignKit.C.Debate.Card(card).Targeted ? debate.Participants[1] : null;
            var plays = DebateEngine.Play(engine.Context, CampaignKit.C.Debate, card, target, engine.Context.State.Rng);
            Assert.Equal(debate.Participants.Count, plays.Count);
        }

        Assert.Equal(CampaignKit.C.Debate.Rounds * debate.Participants.Count, debate.Log.Count);
        Assert.Contains(debate.Participants, p => Math.Abs(campaign.Find(p)!.Image - imageBefore[p]) > 1e-9);
        Assert.All(debate.Participants, p => Assert.True(campaign.Find(p)!.Recognition >= recognitionBefore[p]));
        Assert.Throws<ArgumentException>(() => DebateEngine.Play(engine.Context, CampaignKit.C.Debate, "proposta", null, engine.Context.State.Rng));

        DebateEngine.Close(campaign);
        Assert.Null(campaign.Debate);
        Assert.Single(campaign.DebatesHeld);
        campaign.Events.Pending.Clear();
        engine.AdvanceWeek();
    }

    [Fact]
    public void Recusar_o_debate_custa_imagem_e_ataque_exige_alvo()
    {
        CampaignEngine engine = UntilDebate(5);
        CampaignState campaign = engine.Context.State.Campaign!;
        DebateState debate = campaign.Debate!;
        if (debate.Hand.Contains("ataque"))
        {
            Assert.Throws<ArgumentException>(() => DebateEngine.Play(engine.Context, CampaignKit.C.Debate, "ataque", null, engine.Context.State.Rng));
        }

        double image = campaign.Player.Image;
        DebateEngine.Decline(engine.Context, CampaignKit.C.Debate);
        Assert.Equal(Math.Clamp(image + CampaignKit.C.Debate.DeclineImage, -1, 1), campaign.Player.Image, 9);
        Assert.Null(campaign.Debate);
        Assert.Single(campaign.DebatesHeld);
    }

    [Theory]
    [InlineData(6UL)]
    [InlineData(7UL)]
    public void Campanha_de_prefeito_com_debates_ate_a_apuracao(ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(MediumTown(), seed, strongestParty: true);
        Assert.Null(CareerFlow.SwitchCandidacy(engine.Context, Office.Prefeito).Issue);
        CampaignAutoplayTests.Play(engine, seed);
        CampaignState campaign = engine.Context.State.Campaign!;
        Assert.Equal(CampaignKit.C.Debate.WeeksBeforeElection.Count, campaign.DebatesHeld.Count);
        Assert.NotNull(campaign.Outcome);
        Assert.Equal(campaign.Outcome!.PlayerElected, campaign.Outcome.Prefeito.Elected.Contains(RaceSetup.PlayerId));
    }
}
