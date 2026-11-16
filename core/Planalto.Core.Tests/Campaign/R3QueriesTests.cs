using Planalto.Core.Campaign;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;
using Xunit;

namespace Planalto.Core.Tests.Campaign;

/// <summary>
/// Consultas de leitura que as telas da R3 usam (verba vista do rival, contas da chapa, disputa por zona), o gatilho
/// de depuração do ataque e as pausas novas do tempo. Nenhuma consulta pode mudar o estado.
/// </summary>
public class R3QueriesTests
{
    private static CampaignEngine Engine(ulong seed, int idleWeeks = 2)
    {
        CampaignEngine engine = CampaignKit.NewCareer(R3WorldTests.City("medio"), seed);
        for (int i = 0; i < idleWeeks; i++)
        {
            R3WorldTests.IdleWeek(engine);
        }

        return engine;
    }

    [Fact]
    public void Consultas_das_telas_nao_mudam_o_estado()
    {
        CampaignEngine engine = Engine(4101);
        string before = CampaignKit.Canonical(engine.Context.State);
        foreach (RivalRecord record in engine.Rivals.Records)
        {
            engine.RivalBudgetSeen(record.CandidateId);
        }

        engine.SlateTotals();
        engine.ZoneContests();
        Assert.Equal(before, CampaignKit.Canonical(engine.Context.State));
    }

    [Fact]
    public void Verba_vista_do_rival_e_o_plano_dele_arredondado_ao_passo()
    {
        CampaignEngine engine = Engine(4102, idleWeeks: 4);
        decimal step = engine.ChannelStep;
        Assert.Empty(engine.RivalBudgetSeen("ninguem"));
        foreach (RivalRecord record in engine.Rivals.Records)
        {
            var seen = engine.RivalBudgetSeen(record.CandidateId);
            Assert.Equal(seen.Count, seen.Select(l => l.ChannelId).Distinct().Count());
            foreach (RivalBudgetLine line in seen)
            {
                decimal real = record.Budget.Where(a => a.ChannelId == line.ChannelId).Sum(a => a.Amount);
                Assert.True(line.Weekly > 0 && line.Weekly % step == 0, $"{line.ChannelId}: {line.Weekly} não é múltiplo de {step}.");
                Assert.True(Math.Abs(line.Weekly - real) <= step / 2, $"{line.ChannelId}: visto {line.Weekly}, plano {real}.");
                Assert.NotEmpty(line.Targets);
                Assert.All(line.Targets, t => Assert.Contains(record.Budget, a => a.ChannelId == line.ChannelId && a.TargetId == t && a.Amount > 0));
            }
        }
    }

    [Fact]
    public void Contas_da_chapa_batem_com_o_painel()
    {
        CampaignEngine engine = Engine(4103);
        SlateView slate = engine.SlatePanel();
        SlateTotals totals = engine.SlateTotals();
        Assert.True(totals.ValidVotes > 0 && totals.Quotient > 0);
        Assert.True(totals.PartyVotes >= totals.LegendVotes + slate.Top.Sum(r => r.Votes) - 1, "os votos do partido incluem a legenda e os nomes do painel.");
        Assert.True(totals.PartyVotes <= totals.ValidVotes);

        // Com cadeira feita, o partido passou do quociente ou entrou nas sobras: nunca faz cadeira sem voto.
        Assert.True(slate.Seats == 0 || totals.PartyVotes > 0);
    }

    [Fact]
    public void Disputa_por_zona_traz_o_lider_o_segundo_e_a_diferenca()
    {
        CampaignEngine engine = Engine(4104);
        var territory = engine.Context.State.Campaign!.Territory;
        var contests = engine.ZoneContests();
        Assert.Equal(territory.Zones, contests.Select(c => c.ZoneId).ToList());
        for (int z = 0; z < contests.Count; z++)
        {
            ZoneContest c = contests[z];
            var top = engine.TopPresence(z, 2);
            Assert.Equal(top[0].Candidate.Id, c.LeaderId);
            Assert.Equal(top[1].Candidate.Id, c.RunnerUpId);
            Assert.Equal(top[0].Presence, c.LeaderPresence, 9);
            Assert.Equal(top[0].Presence - top[1].Presence, c.Margin, 9);
            Assert.True(c.Margin >= 0);
        }
    }

    [Fact]
    public void Ataque_disparado_para_teste_segue_as_regras_e_abre_o_prazo_de_resposta()
    {
        CampaignEngine engine = Engine(4105);
        Attack? attack = engine.TriggerRivalAttack();
        Assert.NotNull(attack);
        Assert.Equal(RaceSetup.PlayerId, attack.TargetId);
        Assert.NotNull(engine.Facts.Find(attack.FactId));
        PendingAttackView pending = Assert.Single(engine.PendingAttacks(), p => p.Attack.Id == attack.Id);
        Assert.Equal(engine.Today.AddDays(engine.Context.Campaign.Attacks.Regras.PrazoRespostaHoras / 24), pending.Deadline);
        Assert.Equal(AttackResponse.Required.Count, pending.Options.Count);
        Assert.Null(engine.TriggerRivalAttack("ninguem"));
    }

    [Fact]
    public void Pausas_novas_ataque_desligavel_e_decisao_com_prazo_fixa()
    {
        TimeConfig time = Planalto.Core.Data.JsonData.LoadFile<TimeConfig>(Path.Combine(TestContent.DataDirectory, TimeConfig.File)).GetValueOrThrow();
        Assert.Empty(time.Validate());
        Assert.True(Clock.ShouldPause(time, Clock.AtaqueContraVoce, null));
        Assert.False(Clock.ShouldPause(time, Clock.AtaqueContraVoce, new Dictionary<string, bool> { [Clock.AtaqueContraVoce] = false }));
        Assert.True(Clock.ShouldPause(time, Clock.DecisaoComPrazo, new Dictionary<string, bool> { [Clock.DecisaoComPrazo] = false }));
    }
}
