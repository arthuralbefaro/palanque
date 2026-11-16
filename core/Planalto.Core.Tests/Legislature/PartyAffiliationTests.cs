using Planalto.Core.Career;
using Planalto.Core.Legislature;
using Planalto.Core.State;
using Xunit;

namespace Planalto.Core.Tests.Legislature;

public class PartyAffiliationTests
{
    [Fact]
    public void Trocar_de_partido_fora_da_janela_custa_o_mandato()
    {
        var (state, _) = LegislatureTests.ElectedCouncillor();
        state.Calendar.JumpTo(new DateOnly(state.Mandate!.Start.Year + 1, 6, 1));
        Assert.False(PartyAffiliation.InWindow(state));
        Assert.True(PartyAffiliation.LosesMandate(state));

        string other = state.Parties.First(p => p.Id != state.Player.PartyId).Id;
        PartyAffiliation.Join(state, other);

        Assert.Null(state.Mandate);
        Assert.Equal(other, state.Player.PartyId);
        Assert.Equal(CareerMode.ForaDeMandato, CareerFlow.Mode(state));
    }

    [Fact]
    public void Na_janela_partidaria_a_troca_preserva_o_mandato()
    {
        var (state, _) = LegislatureTests.ElectedCouncillor();
        int electionYear = state.Mandate!.Start.Year + 3;
        DateOnly deadline = Calendar.ElectionFor(electionYear).PartyAffiliationDeadline;
        state.Calendar.JumpTo(deadline.AddDays(-14));
        Assert.True(PartyAffiliation.InWindow(state));

        string other = state.Parties.First(p => p.Id != state.Player.PartyId).Id;
        PartyAffiliation.Join(state, other);

        Assert.NotNull(state.Mandate);
        Assert.Equal(other, state.Mandate!.Player.PartyId);
        Assert.Contains(state.Mandate.News, n => n.Kind == MandateNewsKind.MudancaDePartido);
        Assert.True(PartyAffiliation.CanRun(state, electionYear));
    }

    [Fact]
    public void Expulso_se_filia_sem_perder_o_mandato_e_quem_se_filia_tarde_nao_disputa()
    {
        var (state, _) = LegislatureTests.ElectedCouncillor();
        MandateState mandate = state.Mandate!;
        state.Calendar.JumpTo(new DateOnly(mandate.Start.Year + 1, 6, 1));
        mandate.Expelled = true;
        state.Player.PartyId = null;
        mandate.Player.PartyId = null;
        Assert.False(PartyAffiliation.LosesMandate(state));

        string other = state.Parties.First().Id;
        PartyAffiliation.Join(state, other);
        Assert.NotNull(state.Mandate);
        Assert.False(state.Mandate!.Expelled);

        // Filiação depois do prazo de abril do ano da eleição: não disputa naquele ano.
        int electionYear = mandate.Start.Year + 3;
        state.Player.AffiliatedOn = Calendar.ElectionFor(electionYear).PartyAffiliationDeadline.AddDays(1);
        Assert.False(PartyAffiliation.CanRun(state, electionYear));
    }
}
