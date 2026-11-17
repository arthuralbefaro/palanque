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

/// <summary>Luto oficial (GDD 17.16): a Câmara não faz sessão enquanto ele dura; a pauta e os projetos do prefeito esperam.</summary>
public class MourningTests
{
    [Fact]
    public void Luto_oficial_suspende_a_sessao_da_Camara_e_a_pauta_espera()
    {
        Linked l = Linked.Career(3, mayor: false);
        LegislatureEngine legislature = l.Legislature!;
        for (int i = 0; i < 40 && (legislature.InRecess || !legislature.Pauta.Any()); i++)
        {
            l.Week();
        }

        List<Bill> pauta = legislature.Pauta.ToList();
        Assert.NotEmpty(pauta);
        Assert.False(legislature.InMourning);

        // Morre um vereador: luto oficial pelos dias configurados, a partir de hoje.
        string colleague = l.R.Titulares.First(id => id != RaceSetup.PlayerId);
        DateOnly week = l.State.Calendar.WeekStart;
        l.Renewal.Die(colleague, week);
        Assert.Equal(week.AddDays(CampaignKitRenewalDays()), l.R.LutoAte);
        Assert.True(legislature.InMourning);

        legislature.ResolveWeek(automatic: false);
        Assert.All(pauta, b => Assert.Null(b.Decided));
        Assert.Equal(pauta.Select(b => b.Id), legislature.Pauta.Select(b => b.Id));

        // Na semana seguinte o luto acabou e a pauta é votada.
        l.State.Calendar.AdvanceWeek();
        Assert.False(legislature.InMourning);
        if (!legislature.InRecess)
        {
            legislature.ResolveWeek(automatic: false);
            Assert.All(pauta, b => Assert.NotNull(b.Decided));
        }
    }

    private static int CampaignKitRenewalDays() =>
        JsonData.LoadFile<RenewalConfig>(Path.Combine(TestContent.DataDirectory, RenewalConfig.File)).GetValueOrThrow().Luto.DiasOficiais;

    [Fact]
    public void Luto_oficial_adia_o_projeto_do_prefeito_que_precisa_de_lei()
    {
        Linked l = Linked.Career(3, mayor: true);
        ExecutiveEngine executive = l.Executive!;
        PolicyDefinition law = CampaignKit.C.Executive.Policies.Policies.First(p => p.RequiresLaw && executive.CanEnact(p) is null);
        PolicyDefinition decree = CampaignKit.C.Executive.Policies.Policies.First(p => !p.RequiresLaw && executive.CanEnact(p) is null);
        Assert.False(executive.ChamberInMourning);

        l.Renewal.Die(l.R.Titulares.First(id => id != RaceSetup.PlayerId), l.State.Calendar.WeekStart);
        Assert.True(executive.ChamberInMourning);
        Assert.Equal(ExecutiveFailure.CamaraEmLuto, executive.CanEnact(law));
        Assert.Equal(ExecutiveFailure.CamaraEmLuto, executive.Enact(law.Id).Failure);
        Assert.Null(executive.CanEnact(decree));

        l.State.Calendar.AdvanceWeek();
        Assert.Null(executive.CanEnact(law));
    }
}
