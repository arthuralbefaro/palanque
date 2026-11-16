using Planalto.Core.Career;
using Planalto.Core.Elections;
using Planalto.Core.Legislature;
using Planalto.Core.State;
using Xunit;

namespace Planalto.Core.Tests.Legislature;

public class PartyAffiliationTests
{
    [Fact]
    [Trait("Categoria", "Lento")]
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
    [Trait("Categoria", "Lento")]
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

    /// <summary>Janela de um ano, 14 dias antes do prazo de filiação.</summary>
    private static void JumpToWindow(GameState state, int year) =>
        state.Calendar.JumpTo(Calendar.ElectionFor(year).PartyAffiliationDeadline.AddDays(-14));

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Vereador_eleito_em_2028_perde_o_mandato_ao_trocar_na_janela_de_2030()
    {
        var (state, _) = LegislatureTests.ElectedCouncillor();
        Assert.Equal(2029, state.Mandate!.Start.Year);
        JumpToWindow(state, 2030);
        Assert.False(PartyAffiliation.InWindow(state), "A janela de 2030 é da eleição geral, não do mandato de vereador.");

        PartyAffiliation.Join(state, state.Parties.First(p => p.Id != state.Player.PartyId).Id);
        Assert.Null(state.Mandate);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Vereador_eleito_em_2028_mantem_o_mandato_ao_trocar_na_janela_de_2032()
    {
        var (state, _) = LegislatureTests.ElectedCouncillor();
        Assert.Equal(2032, state.Mandate!.End.Year);
        JumpToWindow(state, 2032);
        Assert.True(PartyAffiliation.InWindow(state));

        string other = state.Parties.First(p => p.Id != state.Player.PartyId).Id;
        PartyAffiliation.Join(state, other);
        Assert.NotNull(state.Mandate);
        Assert.Equal(other, state.Mandate!.Player.PartyId);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Deputado_federal_mantem_o_mandato_so_na_janela_do_ultimo_ano()
    {
        var (state, _) = LegislatureTests.ElectedCouncillor();
        MandateState councillor = state.Mandate!;

        // Mandato de deputado federal eleito em 2026 (posse em 2027, término em 2030) sobre a mesma bancada de teste.
        MandateState Deputy(int electionYear) => new()
        {
            Office = Office.DeputadoFederal,
            Municipio = councillor.Municipio,
            Start = new DateOnly(electionYear + 1, 2, 1),
            End = new DateOnly(electionYear + 4, 12, 31),
            Members = councillor.Members,
            MayorId = councillor.MayorId,
            MayorName = councillor.MayorName,
            MayorPartyId = councillor.MayorPartyId,
            BaseParties = councillor.BaseParties,
        };

        state.Mandate = Deputy(2026);
        JumpToWindow(state, 2030);
        Assert.True(PartyAffiliation.InWindow(state));
        string other = state.Parties.First(p => p.Id != state.Player.PartyId).Id;
        PartyAffiliation.Join(state, other);
        Assert.NotNull(state.Mandate);

        // Eleito em 2030, o mandato vai até 2034: a janela de 2030 não serve para ele, nem a municipal de 2032.
        state.Mandate = Deputy(2030);
        Assert.False(PartyAffiliation.InWindow(state));
        JumpToWindow(state, 2032);
        Assert.False(PartyAffiliation.InWindow(state));
        PartyAffiliation.Join(state, state.Parties.First(p => p.Id != state.Player.PartyId).Id);
        Assert.Null(state.Mandate);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
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
