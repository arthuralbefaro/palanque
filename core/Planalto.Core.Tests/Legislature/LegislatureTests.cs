using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Elections;
using Planalto.Core.Legislature;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Xunit;

namespace Planalto.Core.Tests.Legislature;

public class LegislatureTests
{
    /// <summary>Uma carreira já eleita vereador e empossada, num município pequeno.</summary>
    internal static (GameState State, LegislatureEngine Engine) ElectedCouncillor(ulong firstSeed = 3)
    {
        int ibge = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == "pequeno").OrderBy(m => m.CodigoIbge).Skip(7).First().CodigoIbge;
        for (ulong seed = firstSeed; seed < firstSeed + 20; seed++)
        {
            CampaignEngine campaign = CampaignKit.NewCareer(ibge, seed, strongestParty: true);
            CampaignAutoplayTests.Play(campaign, seed);
            if (!campaign.Context.State.Campaign!.Outcome!.PlayerElected)
            {
                continue;
            }

            GameState state = campaign.Context.State;
            CareerFlow.ConcludeElection(campaign.Context, CampaignKit.LegislatureContext(state));
            return (state, new LegislatureEngine(CampaignKit.LegislatureContext(state)));
        }

        throw new InvalidOperationException("Nenhuma semente elegeu o jogador.");
    }

    [Theory]
    [InlineData(Quorum.Simples, 5, 4, 13, true)]
    [InlineData(Quorum.Simples, 4, 4, 13, false)]
    [InlineData(Quorum.Absoluta, 6, 2, 13, false)]
    [InlineData(Quorum.Absoluta, 7, 6, 13, true)]
    [InlineData(Quorum.DoisTercos, 8, 1, 12, true)]
    [InlineData(Quorum.DoisTercos, 8, 1, 13, false)]
    [InlineData(Quorum.DoisTercos, 9, 4, 13, true)]
    public void Quorum_de_aprovacao(Quorum quorum, int yes, int no, int members, bool approved) =>
        Assert.Equal(approved, LegislatureEngine.Approved(quorum, yes, no, members));

    [Fact]
    public void Posse_monta_a_Camara_com_o_jogador_o_prefeito_a_base_e_os_lideres()
    {
        var (state, engine) = ElectedCouncillor();
        MandateState mandate = state.Mandate!;
        int seats = CampaignKit.Municipio(mandate.Municipio).VagasVereador;

        Assert.Equal(seats, mandate.Members.Count);
        Assert.Single(mandate.Members, m => m.IsPlayer);
        Assert.Equal(mandate.Members.Count, mandate.Members.Select(m => m.Id).Distinct().Count());
        Assert.All(mandate.Members.Where(m => !m.IsPlayer), m => Assert.Contains(state.Npcs, n => n.Id == m.Id));
        Assert.Contains(mandate.MayorPartyId, mandate.BaseParties);
        Assert.All(mandate.Members.Where(m => m.PartyId is not null).GroupBy(m => m.PartyId), g => Assert.Single(g, m => m.Leader));
        Assert.Equal(new DateOnly(mandate.Start.Year, 1, 1), mandate.Start);
        Assert.True(state.Calendar.Contains(mandate.Start) || state.Calendar.WeekStart == mandate.Start);
        Assert.Null(state.Campaign);
        Assert.Equal(CareerMode.Mandato, CareerFlow.Mode(state));
        Assert.Equal(seats, state.Local!.Councillors.Count);
        Assert.Contains(RaceSetup.PlayerId, state.Local.Councillors);
        Assert.True(engine.InRecess, "Janeiro é recesso.");
    }

    [Fact]
    public void Base_do_prefeito_soma_maioria_quando_ha_partidos_proximos()
    {
        var (state, _) = ElectedCouncillor();
        MandateState mandate = state.Mandate!;
        int baseSeats = mandate.Members.Count(m => mandate.InBase(m.PartyId));
        var outsideClose = mandate.Members.Select(m => m.PartyId).Distinct()
            .Where(p => p is not null && !mandate.InBase(p))
            .Where(p => ChamberSetup.Distance(state.Party(p)!.Position, state.Party(mandate.MayorPartyId)!.Position) <= CampaignKit.C.Legislature.Config.Base.MaxDistance);

        // Ou a base é majoritária, ou não sobrou partido próximo para chamar.
        Assert.True(baseSeats > mandate.Members.Count / 2 || !outsideClose.Any());
    }

    [Fact]
    public void Vereador_mais_proximo_do_projeto_tem_logito_maior()
    {
        var (state, engine) = ElectedCouncillor();
        BillTemplate template = CampaignKit.C.Legislature.MunicipalBills.Bills.First(b => b.Position.Social >= 50);
        var bill = new Bill { Id = "t", TemplateId = template.Id, Title = template.Title, AuthorId = "x", AuthorKind = template.Author, Filed = state.Calendar.WeekStart, CommitteeUntil = state.Calendar.WeekStart };
        var near = new ChamberMember { Id = "a", Name = "A", PartyId = null, Position = template.Position };
        var far = new ChamberMember { Id = "b", Name = "B", PartyId = null, Position = new Ideology.IdeologicalPosition(-template.Position.Economic, -template.Position.Social) };

        Assert.True(engine.VoteLogit(near, bill, 0) > 0);
        Assert.True(engine.VoteLogit(far, bill, 0) < 0);
        Assert.True(engine.VoteLogit(far, bill, 1) > engine.VoteLogit(far, bill, 0), "A orientação puxa o voto.");
    }

    [Fact]
    public void Votar_sempre_contra_o_partido_leva_a_advertencia_sancao_e_expulsao()
    {
        var (state, engine) = ElectedCouncillor();
        MandateState mandate = state.Mandate!;
        mandate.Player.Leader = false;
        string party = state.Player.PartyId!;
        var oriented = CampaignKit.C.Legislature.MunicipalBills.Bills
            .Select(t => (Template: t, Orientation: engine.Orientation(party, new Bill { Id = "x", TemplateId = t.Id, Title = t.Title, AuthorId = "x", AuthorKind = t.Author, Filed = state.Calendar.WeekStart, CommitteeUntil = state.Calendar.WeekStart })))
            .Where(x => x.Orientation != 0)
            .ToList();
        Assert.NotEmpty(oriented);

        state.Calendar.JumpTo(new DateOnly(mandate.Start.Year, 3, 1));
        int guard = 0;
        while (!mandate.Expelled && guard++ < 40)
        {
            var (template, orientation) = oriented[guard % oriented.Count];
            var bill = new Bill { Id = $"f{guard}", TemplateId = template.Id, Title = template.Title, AuthorId = Bill.MayorAuthor, AuthorKind = template.Author, Filed = state.Calendar.WeekStart, Stage = BillStage.Plenario, CommitteeUntil = state.Calendar.WeekStart };
            mandate.Bills.Add(bill);
            engine.CastVote(bill.Id, orientation > 0 ? VoteChoice.Nao : VoteChoice.Sim);
            engine.AdvanceWeek();
        }

        Assert.True(mandate.Expelled);
        Assert.True(mandate.Sanctioned);
        Assert.Null(mandate.Committee);
        Assert.Null(state.Player.PartyId);
        Assert.Null(mandate.Player.PartyId);
        Assert.Contains(mandate.News, n => n.Kind == MandateNewsKind.Advertencia);
        Assert.Contains(mandate.News, n => n.Kind == MandateNewsKind.Sancao);
        Assert.Contains(mandate.News, n => n.Kind == MandateNewsKind.Expulsao);
    }

    [Fact]
    public void Projeto_protocolado_tramita_e_tem_desfecho()
    {
        var (state, engine) = ElectedCouncillor();
        MandateState mandate = state.Mandate!;
        state.Calendar.JumpTo(new DateOnly(mandate.Start.Year, 2, 2));
        BillTemplate template = engine.ProposableBills().First();
        MandateActionResult result = engine.Execute("propor_projeto", template.Id);
        Assert.True(result.Ok);
        Bill bill = mandate.Bills.Single(b => b.AuthorId == RaceSetup.PlayerId);
        Assert.Equal(BillStage.Comissao, bill.Stage);
        Assert.False(engine.CanExecute(CampaignKit.C.Legislature.Actions.Find("propor_projeto")!, template.Id) is null, "Não protocola o mesmo projeto duas vezes.");

        for (int week = 0; week < 20 && bill.Stage is BillStage.Comissao or BillStage.Plenario; week++)
        {
            foreach (Bill b in engine.Pauta.ToList())
            {
                engine.CastVote(b.Id, VoteChoice.Sim);
            }

            engine.AdvanceWeek();
        }

        Assert.Contains(bill.Stage, new[] { BillStage.Aprovado, BillStage.Rejeitado, BillStage.Arquivado });
        Assert.NotNull(bill.Decided);
    }

    [Fact]
    public void Acoes_respeitam_energia_capital_e_emendas()
    {
        var (state, engine) = ElectedCouncillor();
        MandateState mandate = state.Mandate!;
        MandateActionDefinition emenda = CampaignKit.C.Legislature.Actions.Find("emenda")!;
        string target = emenda.Targets[0].Id;
        for (int i = 0; i < CampaignKit.C.Legislature.Config.EmendasPerYear; i++)
        {
            Assert.True(engine.Execute("emenda", target).Ok);
        }

        Assert.Equal(MandateActionFailure.SemEmendas, engine.CanExecute(emenda, target));
        state.Player.Resources.PoliticalCapital = 0;
        Assert.Equal(MandateActionFailure.SemCapital, engine.CanExecute(CampaignKit.C.Legislature.Actions.Find("propor_projeto")!, engine.ProposableBills().First().Id));
        mandate.EnergyLeft = 5;
        Assert.Equal(MandateActionFailure.SemEnergia, engine.CanExecute(CampaignKit.C.Legislature.Actions.Find("tribuna")!, null));
    }
}
