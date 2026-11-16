using Planalto.Core.Campaign;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.Core.Governance;

/// <summary>Obra como a tela a mostra: etapa, barra de progresso e o que o jogador sabe dela.</summary>
public sealed record WorkView(
    Work Work, string Name, string ZoneName, string StageName, double StageProgress, string? Company, string Sponsor, bool ByPlayer, bool CanInaugurate, bool KnownIrregular, int DelayWeeks);

/// <summary>Zona no mandato: presença do vereador, aprovação do governo, obras e quem cobra ali.</summary>
public sealed record ZoneStanding(
    string ZoneId, string Name, long Population, double Presence, double PresenceAtStart, bool Base, double GovernmentApproval, int OpenWorks, int DeliveredWorks, string? BaseAide, int OpenPromises, bool HasLead);

/// <summary>Secretário como a tela o mostra: a competência é uma faixa até ser conhecida; a integridade não aparece.</summary>
public sealed record SecretaryView(Secretary Secretary, string Name, string? Pasta, string? PartySigla, int CompetenceMin, int CompetenceMax, string Agenda, double IndicatorSpeed);

public sealed record AideView(Aide Aide, string Name, string? RoleName, string? ZoneName, string? NomineeOf, bool PaysPromise);

/// <summary>Painel fiscal: folha contra os limites da LRF, mínimos de saúde e educação, repasse à Câmara e investimento.</summary>
public sealed record FiscalView(
    decimal Rcl, bool RclDeclared, double PayrollShare, bool PayrollFromRgf, PayrollBand Band, double AlertAt, double PrudentialAt, double LimitAt, bool RaisesBarred,
    IReadOnlyList<string> Art22Bars, DateOnly? ExcessSince, DateOnly? ExcessDeadline, bool Restricted, IReadOnlyList<string> Art23Consequences,
    decimal TaxBase, decimal HealthMinimum, decimal EducationMinimum, double HealthMinimumOfBudget, double EducationMinimumOfBudget,
    double ChamberShare, double ChamberCap, decimal ChamberBudget, decimal ChamberPayrollCap, decimal InvestmentLeft);

/// <summary>Promessa aberta com quem cobra, o prazo que vale no mandato e como se cumpre.</summary>
public sealed record PromiseView(Promise Promise, string Holder, DateOnly Deadline, int WeeksLeft, string HowToFulfil);

public sealed record DecisionView(TermDecision Decision, IReadOnlyList<(TermEventOption Option, GovForecast Forecast)> Options, TermEventOption Default);

public sealed record CrisisView(CrisisRecord Record, string Name, string Text, string Cause, string? ZoneName);

/// <summary>Resumo do topo da área Mandato: o que pede atenção agora.</summary>
public sealed record GovernanceSummary(
    GovRole Role, int TurnsLeft, int Capital, double CityPresence, double Approval, int OpenWorks, int WorksToInaugurate, int PendingFindings, bool PendingDecision, bool PendingNegotiation,
    int OpenPromises, int PromisesDueSoon, int OpenDemands, PayrollBand PayrollBand, bool CpiOpen, string? ChamberPresident, bool InaugurationBarred);

/// <summary>Consultas de leitura do governo para a interface (a tela só lê; os comandos são do <see cref="GovernanceEngine"/>).</summary>
public static class GovernanceQueries
{
    public static GovernanceSummary Summary(GovernanceEngine engine)
    {
        GovernanceState g = engine.G;
        GameState state = engine.Context.State;
        DateOnly week = state.Calendar.WeekStart;
        List<Promise> open = state.Ledger.Open.ToList();
        return new GovernanceSummary(
            g.Role, g.TurnsLeft, state.Player.Resources.PoliticalCapital, g.CityPresence(), engine.Approval,
            g.Works.Count(w => w.Open), Works(engine).Count(w => w.CanInaugurate), g.Inspections.Count(i => i.Pending), g.Decision is not null, engine.PendingNegotiation is not null && g.Role == GovRole.Prefeito,
            open.Count, open.Count(p => engine.DeadlineOf(p).DayNumber - week.DayNumber < 7 * 26), g.Demands.Count(d => d.Kind == Demand.Presenca && d.Met is null), g.Payroll.Band, g.Cpi is not null,
            g.Mesa.PresidentId is null ? null : engine.NameOf(g.Mesa.PresidentId), engine.PlayerBarredFromInauguration);
    }

    /// <summary>Obras da cidade para o mapa e para a lista, das abertas às entregues mais recentes.</summary>
    public static IReadOnlyList<WorkView> Works(GovernanceEngine engine, string? zoneId = null)
    {
        GovernanceState g = engine.G;
        DateOnly week = engine.Context.State.Calendar.WeekStart;
        return g.Works.Where(w => zoneId is null || w.ZoneId == zoneId)
            .OrderBy(w => w.Open ? 0 : 1).ThenByDescending(w => w.Delivered ?? w.Created)
            .Select(w =>
            {
                double progress = w.StageEnd is { } end && end.DayNumber > w.StageStart.DayNumber
                    ? Math.Clamp((week.DayNumber - w.StageStart.DayNumber) / (double)(end.DayNumber - w.StageStart.DayNumber), 0, 1)
                    : w.Stage == WorkStage.Entregue ? 1 : 0;
                bool byPlayer = w.SponsorId == RaceSetup.PlayerId || (g.Role == GovRole.Prefeito && w.SponsorId == Work.Prefeito);
                string sponsor = w.SponsorId == Work.Prefeito ? engine.MayorName : engine.NameOf(w.SponsorId);
                return new WorkView(w, engine.WorkName(w), engine.ZoneName(w.ZoneId), engine.StageName(w.Stage), progress, w.CompanyId is null ? null : engine.CompanyName(w), sponsor, byPlayer,
                    w.Stage == WorkStage.Pronta && byPlayer && !engine.PlayerBarredFromInauguration, w.Revealed && w.Irregular, w.DelayWeeks);
            })
            .ToList();
    }

    /// <summary>As zonas no modo "Meus votos" e "Obras" do mapa.</summary>
    public static IReadOnlyList<ZoneStanding> Zones(GovernanceEngine engine)
    {
        GovernanceState g = engine.G;
        List<Promise> open = engine.Context.State.Ledger.Open.ToList();
        return g.Zones.Select((id, z) => new ZoneStanding(
            id, engine.ZoneName(id), g.ZonePopulation[z], g.Presence[z], g.PresenceAtStart[z], g.BaseZones.Contains(z), Math.Clamp(engine.Approval + g.ZoneApproval[z], 0, 100),
            g.Works.Count(w => w.Open && w.ZoneId == id), g.Works.Count(w => w.Stage == WorkStage.Entregue && w.ZoneId == id),
            g.Cabinet.FirstOrDefault(a => a.Role == AideRole.Base && a.ZoneId == id) is { } aide ? engine.NameOf(aide.PersonId) : null,
            open.Count(p => p.ZoneId == id), g.ZoneLeads.Contains(id))).ToList();
    }

    public static IReadOnlyList<SecretaryView> Secretaries(GovernanceEngine engine, bool offers = false)
    {
        GovernanceState g = engine.G;
        SecretaryCatalog catalog = engine.Context.Content.Secretaries;
        return g.Secretaries.Where(s => !s.Left && (s.SecretariaId is null) == offers).Select(s =>
        {
            var (min, max) = engine.CompetenceShown(s);
            string? pasta = s.SecretariaId is null ? null : catalog.Secretaria(s.SecretariaId).Nome;
            return new SecretaryView(s, engine.NameOf(s.PersonId), pasta, engine.Context.State.Party(s.PartyId)?.Sigla, min, max, engine.AgendaOf(s), catalog.Regras.VelocidadePorCompetencia[s.Competence - 1]);
        }).ToList();
    }

    /// <summary>Gabinete do vereador: nomeados (<paramref name="candidates"/> falso) ou quem pode ser nomeado.</summary>
    public static IReadOnlyList<AideView> Cabinet(GovernanceEngine engine, bool candidates = false)
    {
        GovernanceState g = engine.G;
        GovernanceContent content = engine.Context.Content;
        List<Promise> open = engine.Context.State.Ledger.Open.ToList();
        return g.Aides.Where(a => !a.Left && a.Appointed != candidates).Select(a => new AideView(
            a, engine.NameOf(a.PersonId), a.Role is null ? null : content.Instruments.Role(a.Role).Nome, a.ZoneId is null ? null : engine.ZoneName(a.ZoneId), a.NomineeOf is null ? null : engine.NameOf(a.NomineeOf),
            open.Any(p => p.ToKind == Promise.ParaPessoa && (p.ToId == a.PersonId || p.ToId == a.NomineeOf)
                && (content.Collection.PromessaDeCargo.Contains(p.Object) || p.Object == "voto_" + VoteRequest.Cargo)))).ToList();
    }

    public static FiscalView Fiscal(GovernanceEngine engine)
    {
        GovernanceState g = engine.G;
        FiscalLawConfig law = engine.Context.FiscalLaw;
        decimal taxBase = engine.TaxBase;
        decimal chamber = taxBase * (decimal)g.Fiscal.ChamberShare;
        return new FiscalView(
            g.Fiscal.Rcl, g.Fiscal.Declared, g.Payroll.Share, g.Payroll.FromRgf, g.Payroll.Band, law.AlertaExecutivo, law.PrudencialExecutivo, law.LimitePessoalExecutivo, law.VedadoAumentar(g.Payroll.Share),
            law.VedadoAumentar(g.Payroll.Share) ? law.VedacoesPrudencial : [], g.Payroll.ExcessSince, g.Payroll.ExcessSince is { } since ? law.PrazoParaEliminar(since) : null, g.Payroll.Restricted,
            g.Payroll.Restricted ? law.ConsequenciasExcesso : [],
            taxBase, law.MinimoEmReais(law.AreaSaude, taxBase), law.MinimoEmReais(law.AreaEducacao, taxBase), law.MinimoSobreOrcamento(law.AreaSaude, g.Fiscal.TaxBaseShare),
            law.MinimoSobreOrcamento(law.AreaEducacao, g.Fiscal.TaxBaseShare),
            g.Fiscal.ChamberShare, engine.ChamberCap, chamber, law.FolhaMaximaDaCamaraEmReais(chamber), engine.InvestmentLeft);
    }

    /// <summary>Promessas abertas, da mais urgente para a mais folgada, com quem cobra e como se cumpre.</summary>
    public static IReadOnlyList<PromiseView> Promises(GovernanceEngine engine)
    {
        GovernanceContent content = engine.Context.Content;
        DateOnly week = engine.Context.State.Calendar.WeekStart;
        string How(Promise p)
        {
            if (content.Collection.PromessaDeCargo.Contains(p.Object) || p.Object == "voto_" + VoteRequest.Cargo)
            {
                return engine.T("como_cargo");
            }

            if (content.Collection.PromessaDePresenca.Contains(p.Object))
            {
                return engine.T("como_presenca", ("zona", engine.ZoneName(p.ZoneId)));
            }

            if (content.Collection.PromessaDePosicao.Contains(p.Object))
            {
                return engine.T("como_posicao");
            }

            if (p.Object == "voto_" + VoteRequest.ApoioProjeto)
            {
                return engine.T("como_voto");
            }

            string? key = engine.Context.Promises.Find(p.Object)?.Obra;
            List<string> types = content.Works.Tipos
                .Where(t => (key is not null && t.Cumpre.Contains(key)) || (content.Collection.PromessaCumpre.TryGetValue(p.Object, out List<string>? keys) && keys.Any(t.Cumpre.Contains)))
                .Select(t => t.Nome).ToList();
            string where = p.ZoneId is null ? engine.T("onde_cidade") : engine.T("onde_zona", ("zona", engine.ZoneName(p.ZoneId)));
            return types.Count > 0 ? engine.T("como_obra", ("onde", where), ("obras", string.Join(engine.T("ou"), types))) : engine.T("como_obra_qualquer", ("onde", where));
        }

        return engine.Context.State.Ledger.Open.OrderBy(engine.DeadlineOf).Select(p =>
        {
            DateOnly deadline = engine.DeadlineOf(p);
            string holder = p.ToKind == Promise.ParaPessoa ? engine.NameOf(p.ToId) : p.ToKind == Promise.ParaZona ? engine.ZoneName(p.ToId) : p.ToId;
            return new PromiseView(p, holder, deadline, (deadline.DayNumber - week.DayNumber) / 7, How(p));
        }).ToList();
    }

    public static DecisionView? Decision(GovernanceEngine engine)
    {
        if (engine.G.Decision is not { } decision || engine.DecisionDefinition is not { } def)
        {
            return null;
        }

        return new DecisionView(decision, def.Opcoes.Select(o => (o, engine.ForecastDecision(o.Id))).ToList(), def.Opcoes.First(o => o.Padrao));
    }

    /// <summary>Crises do mandato, cada uma com a causa que a produziu.</summary>
    public static IReadOnlyList<CrisisView> Crises(GovernanceEngine engine) =>
        engine.G.Crises.OrderByDescending(c => c.Date).Select(c =>
        {
            Executive.CrisisDefinition def = engine.Context.Executive.Crises.Find(c.CrisisId)!;
            return new CrisisView(c, def.Name, def.Text, c.CauseText, c.ZoneId is null ? null : engine.ZoneName(c.ZoneId));
        }).ToList();

    /// <summary>Contagem em uma linha: "5 sim, 4 não, 2 indeciso(s)".</summary>
    public static string CountText(GovernanceEngine engine, NominalCount count) =>
        GovernanceEngine.Fill(engine.Context.Content.Instruments.Texto("contagem"), ("sim", count.Yes.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ("nao", count.No.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("indecisos", count.Undecided.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Se a cassação fosse a voto hoje: a previsão por extenso.</summary>
    public static string CassationText(GovernanceEngine engine)
    {
        var (expected, needed) = engine.CassationOutlook();
        return GovernanceEngine.Fill(engine.Context.Content.Chamber.Texto("previsao_cassacao"), ("aprovacao", GovernanceEngine.Num(engine.Context.Content.Chamber.Regras.CassacaoAprovacaoAbaixo)),
            ("votos", GovernanceEngine.Num(expected)), ("precisa", needed.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("artigo", engine.Context.ChamberRules.ArtigoCassacao));
    }

    /// <summary>Notícias do governo, da mais nova para a mais antiga.</summary>
    public static IEnumerable<FeedItem> Feed(GovernanceEngine engine, string? kind = null) =>
        Enumerable.Reverse(engine.G.Feed).Where(f => kind is null || f.Kind == kind);

    /// <summary>Votações da pauta desta semana com a contagem nominal e o voto previsto do jogador.</summary>
    public static IReadOnlyList<(Bill Bill, NominalCount Count, VoteChoice Planned, IReadOnlyList<string> Conflicts)> Agenda(GovernanceEngine engine)
    {
        var list = new List<(Bill, NominalCount, VoteChoice, IReadOnlyList<string>)>();
        if (engine.Context.State.Mandate is not { } mandate || engine.Role != GovRole.Vereador)
        {
            return list;
        }

        var conflicts = engine.VoteConflicts();
        foreach (Bill bill in mandate.Bills.Where(b => b.Stage == BillStage.Plenario))
        {
            if (engine.CountBill(bill.Id) is { } count)
            {
                list.Add((bill, count, engine.PlannedVote(bill), conflicts.FirstOrDefault(c => c.Bill.Id == bill.Id).Reasons ?? []));
            }
        }

        return list;
    }
}
