using System.Globalization;
using Planalto.Core.Campaign;
using Planalto.Core.Electorate;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.Core.Governance;

/// <summary>Mesa Diretora, CPI e cassação (pendências das D-027 e D-031).</summary>
public sealed partial class GovernanceEngine
{
    private ChamberRules ChamberGameRules => Content.Chamber.Regras;

    // ================================================================ Mesa Diretora

    /// <summary>Quem disputa a presidência: o mais votado da base do governo contra o mais votado de fora dela.</summary>
    private (ChamberMember? Government, ChamberMember? Opposition) MesaCandidates()
    {
        GovernanceState g = G;

        // Quem preside não é reconduzido ao mesmo cargo na eleição seguinte (CF, art. 57, § 4º, por simetria). A Mesa
        // de uma legislatura nova começa sem essa trava: o governo recomeça a cada posse.
        string? barred = Context.ChamberRules.VedadaReconducaoAoMesmoCargo ? g.Mesa.PresidentId : null;
        ChamberMember? government = Members.Where(m => InBase(m.PartyId) && !m.IsPlayer && m.Id != barred).OrderByDescending(m => m.Votes).ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault();
        ChamberMember? opposition = Members.Where(m => !InBase(m.PartyId) && !m.IsPlayer && m.Id != barred).OrderByDescending(m => m.Votes).ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault();
        if (g.Mesa.PlayerRunning && barred != RaceSetup.PlayerId && Role == GovRole.Vereador && Mandate is { } mandate)
        {
            if (mandate.InBase(mandate.Player.PartyId))
            {
                government = mandate.Player;
            }
            else
            {
                opposition = mandate.Player;
            }
        }

        return (government, opposition);
    }

    /// <summary>Tendência (sem ruído) de um vereador votar no candidato do governo para a Mesa.</summary>
    private double MesaLogit(ChamberMember voter, ChamberMember? government, ChamberMember? opposition)
    {
        double logit = ChamberGameRules.MesaPesoBase * (InBase(voter.PartyId) ? 1 : -1);
        if (government?.IsPlayer == true)
        {
            logit += ChamberGameRules.MesaPesoRelacao * voter.Relation / 100;
        }
        else if (opposition?.IsPlayer == true)
        {
            logit -= ChamberGameRules.MesaPesoRelacao * voter.Relation / 100;
        }

        return logit;
    }

    private void MesaWeek()
    {
        GovernanceState g = G;
        if (g.Mesa.NextElection > Week || Members.Count == 0)
        {
            if (g.Mesa.PresidentId == RaceSetup.PlayerId && Week.Day <= 7)
            {
                Capital += ChamberGameRules.MesaCapitalMensal;
            }

            return;
        }

        var (government, opposition) = MesaCandidates();
        ChamberMember? winner = government ?? opposition;
        int yes = 0, no = 0;
        if (government is not null && opposition is not null)
        {
            foreach (ChamberMember voter in Members)
            {
                bool forGovernment = voter.Id == government.Id || (voter.Id != opposition.Id
                    && (voter.IsPlayer ? InBase(voter.PartyId) : MesaLogit(voter, government, opposition) + (ElectorateBuilder.Gaussian(Rng) * Context.Legislature.Config.Vote.NoiseSd) > 0));
                if (forGovernment)
                {
                    yes++;
                }
                else
                {
                    no++;
                }
            }

            winner = yes >= no ? government : opposition;
        }

        g.Mesa.PresidentId = winner?.Id;
        g.Mesa.Elected = Week;
        g.Mesa.NextElection = new DateOnly(Week.Year + Context.ChamberRules.MandatoDaMesaAnos, 1, 1);
        if (Mandate is { } mandate)
        {
            mandate.PresidentId = winner?.Id;
        }

        if (g.Mesa.PlayerRunning)
        {
            ChamberMember? rival = government?.IsPlayer == true ? opposition : government;
            Remember(rival?.Id, PersonRole.Vereador, "mesa_contra", Content.Chamber.Lembrancas["mesa_contra"]);
            bool won = winner?.IsPlayer == true;
            if (won)
            {
                foreach (ChamberMember ally in Members.Where(m => !m.IsPlayer && InBase(m.PartyId) == InBase(winner!.PartyId)))
                {
                    ally.Relation = Math.Clamp(ally.Relation + ChamberGameRules.MesaRelacaoDosEleitores, -100, 100);
                }
            }

            News("mesa", Fill(Content.Chamber.Texto(won ? "mesa_eleita_jogador" : "mesa_derrota_jogador"), ("nome", winner?.Name ?? ""), ("sim", (won == (government?.IsPlayer == true) ? yes : no).ToString(CultureInfo.InvariantCulture)),
                ("nao", (won == (government?.IsPlayer == true) ? no : yes).ToString(CultureInfo.InvariantCulture))), null, winner?.Id);
            g.Mesa.PlayerRunning = false;
        }
        else if (winner is not null)
        {
            News("mesa", Fill(Content.Chamber.Texto("mesa_eleita"), ("nome", winner.Name), ("partido", State.Party(winner.PartyId)?.Sigla ?? ""), ("sim", Math.Max(yes, no).ToString(CultureInfo.InvariantCulture)),
                ("nao", Math.Min(yes, no).ToString(CultureInfo.InvariantCulture))), null, winner.Id);
        }
    }

    public GovForecast ForecastRunForMesa()
    {
        GovernanceState g = G;
        if (Role != GovRole.Vereador || Mandate is not { } mandate)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        if (g.Mesa.PlayerRunning)
        {
            return GovForecast.Block(GovFailure.JaFeito, "");
        }

        if (Context.ChamberRules.VedadaReconducaoAoMesmoCargo && g.Mesa.PresidentId == RaceSetup.PlayerId)
        {
            return GovForecast.Block(GovFailure.LimiteLegal, Fill(Content.Chamber.Texto("mesa_sem_reconducao"), ("artigo", Context.ChamberRules.ArtigoMesa)));
        }

        ChamberRules rules = ChamberGameRules;
        if (Capital < rules.MesaCapitalParaConcorrer)
        {
            return GovForecast.Block(GovFailure.SemCapital, Fill(Content.Instruments.Texto("sem_capital"), ("capital", rules.MesaCapitalParaConcorrer.ToString(CultureInfo.InvariantCulture))));
        }

        bool inBase = mandate.InBase(mandate.Player.PartyId);
        var (government, opposition) = MesaCandidates();
        ChamberMember? rival = inBase ? opposition : government;
        double sd = Math.Max(1e-9, Context.Legislature.Config.Vote.NoiseSd);
        double expected = 1 + Members.Where(m => !m.IsPlayer && m.Id != rival?.Id).Sum(m =>
        {
            double logit = MesaLogit(m, inBase ? mandate.Player : government, inBase ? opposition : mandate.Player);
            return Phi((inBase ? logit : -logit) / sd);
        });
        MemoryDefinition memory = Content.Chamber.Lembrancas["mesa_contra"];
        var lines = new List<ForecastLine> { new(GovForecast.Capital, null, -rules.MesaCapitalParaConcorrer, -rules.MesaCapitalParaConcorrer, -rules.MesaCapitalParaConcorrer) };
        if (rival is not null)
        {
            lines.Add(new ForecastLine(GovForecast.Relacao, rival.Id, memory.Pontos, memory.Pontos, memory.Pontos));
        }

        return new GovForecast(null, null, lines, [],
        [
            Fill(Content.Chamber.Texto("previsao_mesa"), ("data", Texts.Date(g.Mesa.NextElection)), ("votos", Num(expected)), ("membros", Members.Count.ToString(CultureInfo.InvariantCulture)),
                ("rival", rival?.Name ?? ""), ("capital", rules.MesaCapitalMensal.ToString(CultureInfo.InvariantCulture)), ("artigo", Context.ChamberRules.ArtigoMesa)),
        ])
        { Remembered = rival is null ? [] : [rival.Name] };
    }

    /// <summary>O vereador se lança à presidência da Câmara na próxima eleição da Mesa.</summary>
    public GovResult RunForMesa()
    {
        GovForecast forecast = ForecastRunForMesa();
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Capital -= ChamberGameRules.MesaCapitalParaConcorrer;
        G.Mesa.PlayerRunning = true;
        return new GovResult(null, forecast.Explanation[0], [ChCapital(-ChamberGameRules.MesaCapitalParaConcorrer)], []);
    }

    // ================================================================ CPI

    /// <summary>Quem assinaria uma CPI contra o governo: a oposição e, se o pedido é do jogador, quem tem boa relação com ele.</summary>
    private int CpiSignatures(bool byPlayer)
    {
        ChamberRules rules = ChamberGameRules;
        int signatures = byPlayer ? 1 : 0;
        foreach (ChamberMember m in Members.Where(m => !m.IsPlayer))
        {
            if (byPlayer ? (!InBase(m.PartyId) && m.Relation > -rules.CpiAssinaComRelacao) || m.Relation >= rules.CpiAssinaComRelacao : !InBase(m.PartyId) || m.Relation <= -rules.CpiAssinaComRelacao)
            {
                signatures++;
            }
        }

        return signatures;
    }

    private bool CpiAllowed => G.Cpi is null && !G.CpiHistory.Any(c => Week.DayNumber - c.Ends.DayNumber < 7 * ChamberGameRules.SemanasEntreCpis);

    private double CpiStartingEvidence(Work? work)
    {
        ChamberRules rules = ChamberGameRules;
        double evidence = rules.CpiProvaPorGravidade;
        if (work is not null && G.Requests.Any(r => r.Answered && r.TargetKind == InfoRequest.SobreObra && r.TargetId == work.Id))
        {
            evidence += rules.CpiProvaPorRequerimento;
        }

        return Math.Clamp(evidence, 0, 1);
    }

    public GovForecast ForecastCpi(string workId)
    {
        Work? work = G.Work(workId);
        if (Role != GovRole.Vereador)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        if (work is not { Irregular: true, Revealed: true })
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, "");
        }

        if (!CpiAllowed)
        {
            return GovForecast.Block(GovFailure.Bloqueado, "");
        }

        ChamberRules rules = ChamberGameRules;
        int needed = Context.ChamberRules.AssinaturasNecessarias(Members.Count);
        int have = CpiSignatures(byPlayer: true);
        double evidence = CpiStartingEvidence(work);
        FindingOption denounce = Content.Instruments.Finding(FindingOption.Denunciar);
        var lines = new List<ForecastLine> { new(GovForecast.RelacaoPrefeito, null, denounce.RelacaoPrefeito, denounce.RelacaoPrefeito, denounce.RelacaoPrefeito) };
        if (have >= needed)
        {
            lines.Add(new ForecastLine(GovForecast.Imagem + "_later", null, 0, rules.CpiImagemDoAutor, evidence >= rules.CpiIndiciaAcima ? rules.CpiImagemDoAutor : rules.CpiImagemDoAutor / 2));
        }

        return new GovForecast(null, null, lines, [],
        [
            Fill(Content.Chamber.Texto("previsao_cpi_assinaturas"), ("tem", have.ToString(CultureInfo.InvariantCulture)), ("precisa", needed.ToString(CultureInfo.InvariantCulture)),
                ("membros", Members.Count.ToString(CultureInfo.InvariantCulture)), ("artigo", Context.ChamberRules.ArtigoCpi)),
            Fill(Content.Chamber.Texto("previsao_cpi"), ("prova", Texts.Percent(evidence)), ("indicia", Texts.Percent(rules.CpiIndiciaAcima)), ("min", rules.CpiSemanas.Min.ToString(CultureInfo.InvariantCulture)),
                ("max", rules.CpiSemanas.Max.ToString(CultureInfo.InvariantCulture))),
        ])
        { Remembered = [MayorName] };
    }

    /// <summary>O vereador pede uma CPI sobre uma obra com irregularidade conhecida. Precisa das assinaturas de um terço da Câmara.</summary>
    public GovResult RequestCpi(string workId)
    {
        GovForecast forecast = ForecastCpi(workId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Work work = G.Work(workId)!;
        FindingOption denounce = Content.Instruments.Finding(FindingOption.Denunciar);
        ShiftMayorRelation(denounce.RelacaoPrefeito);
        Remember(Mandate?.MayorId, PersonRole.Dirigente, "cpi_autor", Content.Chamber.Lembrancas["cpi_autor"], ("obra", WorkName(work)));
        int needed = Context.ChamberRules.AssinaturasNecessarias(Members.Count);
        int have = CpiSignatures(byPlayer: true);
        if (have < needed)
        {
            string failed = Fill(Content.Chamber.Texto("cpi_sem_assinaturas"), ("tem", have.ToString(CultureInfo.InvariantCulture)), ("precisa", needed.ToString(CultureInfo.InvariantCulture)));
            News("cpi", failed, work.ZoneId);
            return new GovResult(null, failed, [ChRelation(MayorName, denounce.RelacaoPrefeito)], []);
        }

        string subject = Fill(Content.Chamber.Texto("cpi_motivo_obra"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("empresa", CompanyName(work)));
        Cpi cpi = OpenCpi(subject, have, byPlayer: true, work);
        return new GovResult(null, Fill(Content.Chamber.Texto("cpi_aberta"), ("assunto", subject), ("assinaturas", have.ToString(CultureInfo.InvariantCulture)), ("data", Texts.Date(cpi.Ends))),
            [ChRelation(MayorName, denounce.RelacaoPrefeito), T("mud_prova", ("prova", Texts.Percent(cpi.Evidence)))], []) { CreatedId = cpi.Id };
    }

    private Cpi OpenCpi(string subject, int signatures, bool byPlayer, Work? work)
    {
        GovernanceState g = G;
        ChamberRules rules = ChamberGameRules;
        var cpi = new Cpi
        {
            Id = g.NewId("cpi"),
            Subject = subject,
            Opened = Week,
            Ends = Week.AddDays(7 * Rng.NextInt(rules.CpiSemanas.Min, rules.CpiSemanas.Max + 1)),
            Signatures = signatures,
            ByPlayer = byPlayer,
            WorkId = work?.Id,
            Evidence = CpiStartingEvidence(work),
        };
        g.Cpi = cpi;
        News("cpi", Fill(Content.Chamber.Texto("cpi_aberta"), ("assunto", subject), ("assinaturas", signatures.ToString(CultureInfo.InvariantCulture)), ("data", Texts.Date(cpi.Ends))), work?.ZoneId);
        return cpi;
    }

    /// <summary>Há escândalo recente que sustente uma CPI contra o prefeito jogador? Devolve o motivo.</summary>
    private string? RecentScandal()
    {
        GovernanceState g = G;
        int window = 7 * ChamberGameRules.CpiEscandaloValeSemanas;
        CrisisRecord? crisis = g.Crises.LastOrDefault(c => c.CauseKind is CrisisRecord.CausaSecretario or CrisisRecord.CausaLicitacao && Week.DayNumber - c.Date.DayNumber <= window);
        if (crisis is not null)
        {
            return crisis.CauseText;
        }

        LedgerSkeleton? skeleton = State.Ledger.Skeletons.LastOrDefault(s => s.Exposed);
        return skeleton?.Text;
    }

    private void CpiWeek()
    {
        GovernanceState g = G;
        ChamberRules rules = ChamberGameRules;
        if (g.Cpi is null)
        {
            // Contra o prefeito jogador: aprovação baixa somada a escândalo abre CPI (pendência da D-031).
            if (Role == GovRole.Prefeito && CpiAllowed && Approval < rules.CpiAprovacaoAbaixo && RecentScandal() is { } scandal)
            {
                int needed = Context.ChamberRules.AssinaturasNecessarias(Members.Count);
                int have = CpiSignatures(byPlayer: false);
                if (have >= needed)
                {
                    OpenCpi(Fill(Content.Chamber.Texto("cpi_motivo_escandalo"), ("motivo", scandal)), have, byPlayer: false, null);
                }
            }

            return;
        }

        Cpi cpi = g.Cpi;
        if (cpi.Ends > Week)
        {
            ShiftApproval(-rules.CpiAprovacaoPorSemana);
            if ((Week.DayNumber - cpi.Opened.DayNumber) / 7 % 4 == 0)
            {
                News("cpi", Fill(Content.Chamber.Texto("cpi_semana"), ("assunto", cpi.Subject), ("prova", Texts.Percent(cpi.Evidence))));
            }

            return;
        }

        bool indicted = cpi.Evidence >= rules.CpiIndiciaAcima;
        cpi.Outcome = indicted ? CpiOutcome.Indiciou : CpiOutcome.Arquivada;
        g.CpiHistory.Add(cpi);
        g.Cpi = null;
        News("cpi", Fill(Content.Chamber.Texto(indicted ? "cpi_indiciou" : "cpi_arquivada"), ("assunto", cpi.Subject)));
        if (cpi.ByPlayer)
        {
            double share = indicted ? 1 : 0.5;
            ShiftImage(rules.CpiImagemDoAutor * share);
            for (int z = 0; z < g.Presence.Count; z++)
            {
                GainPresence(z, rules.CpiPresencaDoAutor * share);
            }
        }

        if (!indicted)
        {
            return;
        }

        ShiftApproval(-rules.CpiAprovacaoSeIndicia);
        if (Approval < rules.CassacaoAprovacaoAbaixo && !g.MayorRemoved)
        {
            VoteCassation(cpi);
        }
    }

    /// <summary>Tendência (sem ruído) de um vereador votar pela cassação do prefeito.</summary>
    private double CassationLogit(ChamberMember m)
    {
        ChamberRules rules = ChamberGameRules;
        double logit = (rules.CassacaoPesoBase * (InBase(m.PartyId) ? -1 : 1)) + (rules.CassacaoPesoAprovacao * (50 - Approval) / 50);
        if (Role == GovRole.Prefeito)
        {
            logit -= rules.CassacaoPesoRelacao * m.Relation / 100;
        }

        return logit;
    }

    /// <summary>Votos esperados pela cassação se ela fosse a plenário hoje, e quantos são precisos.</summary>
    public (double Expected, int Needed) CassationOutlook()
    {
        double sd = Math.Max(1e-9, Context.Legislature.Config.Vote.NoiseSd);
        return (Members.Where(m => !m.IsPlayer).Sum(m => Phi(CassationLogit(m) / sd)), Context.ChamberRules.VotosParaCassar(Members.Count));
    }

    private void VoteCassation(Cpi cpi)
    {
        GovernanceState g = G;
        int needed = Context.ChamberRules.VotosParaCassar(Members.Count);
        News("cpi", Fill(Content.Chamber.Texto("cassacao_pedido"), ("prefeito", MayorName), ("precisa", needed.ToString(CultureInfo.InvariantCulture)), ("membros", Members.Count.ToString(CultureInfo.InvariantCulture)),
            ("artigo", Context.ChamberRules.ArtigoCassacao)));
        int yes = 0, no = 0;
        foreach (ChamberMember m in Members)
        {
            bool vote = m.IsPlayer ? cpi.ByPlayer || !InBase(m.PartyId) : CassationLogit(m) + (ElectorateBuilder.Gaussian(Rng) * Context.Legislature.Config.Vote.NoiseSd) > 0;
            if (vote)
            {
                yes++;
            }
            else
            {
                no++;
            }

        }

        bool passed = yes >= needed;
        g.Cassations.Add(new CassationVote(Week, yes, no, needed, passed));
        string count = T("placar", ("sim", yes.ToString(CultureInfo.InvariantCulture)), ("nao", no.ToString(CultureInfo.InvariantCulture)));
        if (!passed)
        {
            News("cpi", Fill(Content.Chamber.Texto("cassacao_rejeitada"), ("prefeito", MayorName), ("placar", count), ("precisa", needed.ToString(CultureInfo.InvariantCulture))));
            return;
        }

        // A vaga fica registrada e é oferecida a quem cuida de sucessão e vacância (gancho do contexto).
        var vacancy = new OfficeVacancy { Id = g.NewId("vaga"), Date = Week, Office = Elections.Office.Prefeito, HolderId = MayorId, Cause = OfficeVacancy.Cassacao, CpiId = cpi.Id };
        g.Vacancies.Add(vacancy);
        if (Role == GovRole.Prefeito)
        {
            // O jogador perde o mandato: o governo fica no estado só para a tela contar o que houve.
            News("cpi", Fill(Content.Chamber.Texto("cassado_jogador"), ("placar", count)));
            g.PlayerRemoved = true;
            State.Executive = null;
            vacancy.Handled = ResolveVacancy(vacancy);
            RelayWorldNews();
            return;
        }

        string old = MayorName;
        g.MayorRemoved = true;
        g.MayorApproval = 50;
        News("cpi", Fill(Content.Chamber.Texto("cassacao_aprovada"), ("prefeito", old), ("placar", count)));
        vacancy.Handled = ResolveVacancy(vacancy);
        if (vacancy.Handled)
        {
            AfterMayorVacancy(vacancy, cpi.ByPlayer);
            return;
        }

        if (g.ViceId.Length == 0)
        {
            // Sem vice e sem ninguém que resolva a sucessão: o governo segue sem a troca, só com o registro da vaga.
            return;
        }

        // Padrão provisório, sem o sistema de vacância ligado: o vice assume.
        if (Mandate is { } mandate)
        {
            mandate.MayorId = g.ViceId;
            mandate.MayorName = NameOf(g.ViceId);
            mandate.MayorRelation = cpi.ByPlayer ? Content.Chamber.Lembrancas["vice_grato"].Pontos : 0;
        }

        if (cpi.ByPlayer)
        {
            Remember(g.ViceId, PersonRole.Dirigente, "vice_grato", Content.Chamber.Lembrancas["vice_grato"]);
        }

        News("cpi", Fill(Content.Chamber.Texto("vice_assume"), ("nome", NameOf(g.ViceId))), null, g.ViceId);
    }
}
