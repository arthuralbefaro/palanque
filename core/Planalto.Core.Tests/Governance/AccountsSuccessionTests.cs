using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Data;
using Planalto.Core.Events;
using Planalto.Core.Executive;
using Planalto.Core.Legislature;
using Planalto.Core.Renewal;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;
using Xunit;

namespace Planalto.Core.Tests.Governance;

/// <summary>Diploma cassado pela Justiça Eleitoral por causa das contas de campanha (GDD 17.12): o cargo segue a sucessão real (GDD 17.16).</summary>
public class AccountsSuccessionTests
{
    private static void OpenRepresentation(GameState state)
    {
        AccountsCase accounts = state.Accounts!;
        state.Accounts = new AccountsCase
        {
            Municipio = accounts.Municipio, ElectionDate = accounts.ElectionDate, Elected = true, Limit = 100_000, Spent = 90_000, OffBooks = 80_000, Risk = 100,
            HostileKnowers = 2, Exposed = true, Denuncias = 1, Stage = AccountsStage.Julgadas, Verdict = AccountsVerdict.Desaprovadas, RepresentationChecked = true,
            RepresentationOpen = true, RulingOn = state.Calendar.WeekStart,
        };
    }

    private static void RuleUntilLost(Linked l)
    {
        FinanceCatalog content = CampaignKit.C.Campaign.Finance;
        var law = Planalto.Core.Elections.CampaignFinanceLawConfig.Default;
        for (ulong seed = 1; seed < 80 && l.InOffice; seed++)
        {
            l.State.Accounts!.RepresentationOpen = true;
            l.State.Accounts.RulingOn = l.State.Calendar.WeekStart;
            l.State.Accounts.Stage = AccountsStage.Julgadas;
            CareerFlow.AccountsWeek(l.State, content, law, new Rng(seed), l.Renewal);
        }
    }

    [Fact]
    public void Vereador_cassado_pela_Justica_Eleitoral_da_a_cadeira_ao_suplente()
    {
        Linked l = Linked.Of(GovKit.Councillor().State);
        Assert.NotNull(l.R.SeatOf(RaceSetup.PlayerId));
        int seat = l.R.SeatOf(RaceSetup.PlayerId)!.Numero;
        OpenRepresentation(l.State);
        RuleUntilLost(l);

        Assert.True(l.State.Accounts!.MandateLost);
        Assert.Null(l.State.Mandate);
        Assert.True(l.G.PlayerRemoved);
        Assert.Null(l.R.SeatOf(RaceSetup.PlayerId));
        RenewalEvent fact = RenewalQueries.History(l.R, RenewalEventKind.JusticaEleitoral).First();
        Assert.Equal(RaceSetup.PlayerId, fact.PessoaId);
        Assert.Equal(VacancyCause.JusticaEleitoral, fact.Causa);
        Assert.Equal(seat, fact.Cadeira);
        Assert.Contains("Justiça Eleitoral", fact.Texto);
        Assert.Contains(fact.Desfecho, new[] { SuccessionOutcome.SuplenteAssumiu, SuccessionOutcome.EleicaoParaACadeira, SuccessionOutcome.CadeiraVagaAteOFim });
        Assert.False(string.IsNullOrWhiteSpace(fact.Regra));

        // O mundo segue andando sem o jogador no cargo, e a renúncia automática não é registrada por cima.
        for (int i = 0; i < 8; i++)
        {
            CareerFlow.AdvanceWeek(l.State, null, null, world: l.World);
        }

        Assert.DoesNotContain(RenewalQueries.History(l.R, RenewalEventKind.Renuncia), e => e.PessoaId == RaceSetup.PlayerId);
    }

    [Fact]
    public void Prefeito_cassado_pela_Justica_Eleitoral_leva_a_nova_eleicao()
    {
        Linked l = Linked.Of(GovKit.Mayor().State);
        Assert.Equal(RaceSetup.PlayerId, l.R.PrefeitoId);
        OpenRepresentation(l.State);
        RuleUntilLost(l);

        Assert.Null(l.State.Executive);
        Assert.True(l.G.PlayerRemoved);
        RenewalEvent fact = RenewalQueries.History(l.R, RenewalEventKind.JusticaEleitoral).First();
        Assert.Equal(VacancyCause.JusticaEleitoral, fact.Causa);

        // Decisão da Justiça Eleitoral derruba a chapa: o vice não assume, e a eleição é direta longe do fim do mandato.
        Assert.Equal(SuccessionOutcome.EleicaoDireta, fact.Desfecho);
        Assert.NotEqual(RaceSetup.PlayerId, l.R.PrefeitoId);
        Assert.Contains(l.R.EleicoesPendentes, e => e.Cargo == CivicRole.Prefeito && !e.Indireta);
    }
}
