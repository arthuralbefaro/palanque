using System.Globalization;
using Planalto.Core.Campaign;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.Core.Governance;

/// <summary>
/// Eventos de mandato (GDD 17.14 e 17.15): cada um nasce de um fato do save e cita a pessoa, a zona ou a obra desse
/// fato. Pedem uma decisão com troca; sem resposta no prazo, vale a opção padrão.
/// </summary>
public sealed partial class GovernanceEngine
{
    /// <summary>O fato que dispara o evento, se ele vale agora: a pessoa, a zona e a obra envolvidas.</summary>
    private (string? PersonId, string? ZoneId, string? WorkId)? Binding(TermEventDefinition def)
    {
        GovernanceState g = G;
        switch (def.Gatilho)
        {
            case TermTrigger.ObraAtrasada:
            {
                Work? work = g.Works.Where(w => w.Open && w.DelayWeeks >= WorkRules.AtrasoQueViraNoticia && (Role == GovRole.Prefeito || g.BaseZones.Contains(g.IndexOf(w.ZoneId))))
                    .OrderByDescending(w => w.DelayWeeks).ThenBy(w => w.Id, StringComparer.Ordinal).FirstOrDefault();
                return work is null ? null : (ZoneLeaderId(work.ZoneId), work.ZoneId, work.Id);
            }

            case TermTrigger.EmpresaComAtraso:
            {
                Work? work = g.Works.Where(w => w.Stage == WorkStage.Execucao && w.DelayWeeks > 0 && w.CompanyId is not null).OrderByDescending(w => w.DelayWeeks).ThenBy(w => w.Id, StringComparer.Ordinal).FirstOrDefault();
                return work is null ? null : (g.Company(work.CompanyId)?.OwnerId, work.ZoneId, work.Id);
            }

            case TermTrigger.AssessorNegligenciado:
            {
                Aide? aide = g.Cabinet.FirstOrDefault(a => a.Neglected);
                return aide is null ? null : (aide.PersonId, aide.ZoneId, null);
            }

            case TermTrigger.VereadorComProjeto:
            {
                Bill? bill = Mandate?.Bills.Where(b => b.Stage is BillStage.Comissao or BillStage.Plenario && Member(b.AuthorId) is { IsPlayer: false }).OrderBy(b => b.Filed).ThenBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
                return bill is null ? null : (bill.AuthorId, g.MemberZones.GetValueOrDefault(bill.AuthorId), null);
            }

            case TermTrigger.LiderancaAliada:
            {
                Leader? leader = City.Leaders.Where(l => l.AlignedWith == RaceSetup.PlayerId).OrderByDescending(l => l.Influence).ThenBy(l => l.PersonId, StringComparer.Ordinal).FirstOrDefault();
                return leader is null ? null : (leader.PersonId, leader.Zones[0], null);
            }

            case TermTrigger.PromessaPertoDoPrazo:
            {
                Promise? promise = State.Ledger.Open.Where(p => p.Cobrancas > 0 && DeadlineOf(p).DayNumber - Week.DayNumber is > 0 and < 7 * 26 && HolderOf(p) is not null)
                    .OrderBy(DeadlineOf).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
                return promise is null ? null : (HolderOf(promise), promise.ZoneId, null);
            }

            case TermTrigger.PrefeitoPedeVoto:
            {
                Bill? bill = Mandate?.Bills.FirstOrDefault(b => b.Stage == BillStage.Plenario && b.AuthorKind == BillAuthorKind.Executivo && b.PlayerVote is null);
                return bill is null || Mandate is null ? null : (Mandate.MayorId, null, null);
            }

            case TermTrigger.SecretarioCandidato:
                return SecretaryWith(s => s.Agenda == SecretaryAgenda.Candidato);
            case TermTrigger.SecretarioProjeto:
                return SecretaryWith(s => s.Agenda == SecretaryAgenda.Projeto && s.AgendaWork is not null);
            case TermTrigger.SecretarioDesleal:
                return SecretaryWith(s => s.Loyalty < SecretaryRules.LealdadeDeCritica);
            case TermTrigger.FolhaNoPrudencial:
                return g.Payroll.Band >= PayrollBand.Prudencial ? (UnionLeader?.PersonId, null, null) : null;
            case TermTrigger.VereadorDaBaseInsatisfeito:
            {
                ChamberMember? member = Members.Where(m => !m.IsPlayer && InBase(m.PartyId) && m.Relation < 0 && g.MemberZones.ContainsKey(m.Id)).OrderBy(m => m.Relation).ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault();
                return member is null ? null : (member.Id, g.MemberZones[member.Id], null);
            }

            case TermTrigger.CpiEmCurso:
                return g.Cpi is { ByPlayer: false } ? (g.Mesa.PresidentId, null, g.Cpi.WorkId) : null;
            case TermTrigger.CamaraPedeRepasse:
                return g.Mesa.PresidentId is { } president && president != RaceSetup.PlayerId && g.Fiscal.ChamberShare < ChamberCap - 1e-9 ? (president, null, null) : null;
            case TermTrigger.ZonaDescontente:
            {
                int zone = Enumerable.Range(0, g.ZoneApproval.Count).OrderBy(z => g.ZoneApproval[z]).ThenBy(z => z).FirstOrDefault(-1);
                return zone < 0 || g.ZoneApproval[zone] > -SecretaryRules.CriseZonaDano / 2 ? null : (ZoneLeaderId(g.Zones[zone]), g.Zones[zone], null);
            }

            default:
                return null;
        }
    }

    private (string? PersonId, string? ZoneId, string? WorkId)? SecretaryWith(Func<Secretary, bool> match)
    {
        Secretary? s = G.Secretaries.Where(x => x.SecretariaId is not null && !x.Left).Where(match).OrderBy(x => x.Loyalty).ThenBy(x => x.PersonId, StringComparer.Ordinal).FirstOrDefault();
        return s is null ? null : (s.PersonId, s.AgendaZone, null);
    }

    private string? ZoneLeaderId(string zoneId) =>
        City.Leaders.Where(l => l.Zones.Contains(zoneId)).OrderByDescending(l => l.AlignedWith == RaceSetup.PlayerId).ThenByDescending(l => l.Influence).FirstOrDefault()?.PersonId;

    /// <summary>O que a pessoa do evento é, para aparecer depois do nome.</summary>
    private string RoleText(string? personId)
    {
        GovernanceState g = G;
        if (personId is null)
        {
            return "";
        }

        if (g.Secretaries.FirstOrDefault(s => s.PersonId == personId && s.SecretariaId is not null) is { } secretary)
        {
            return T("papel_secretario", ("secretaria", Content.Secretaries.Secretaria(secretary.SecretariaId!).Nome));
        }

        if (Member(personId) is { } member)
        {
            return State.Party(member.PartyId) is { } party ? T("papel_vereador", ("partido", party.Sigla)) : T("papel_vereador_sem_partido");
        }

        if (g.Aides.FirstOrDefault(a => a.PersonId == personId && a.Appointed) is { } aide)
        {
            return Content.Instruments.Role(aide.Role!).Nome.ToLowerInvariant();
        }

        if (g.Companies.FirstOrDefault(c => c.OwnerId == personId) is { } company)
        {
            return T("papel_empresa", ("empresa", company.Name));
        }

        if (City.Leader(personId) is { } leader)
        {
            return Context.Campaign.Leaders.Tipos.FirstOrDefault(t => t.Id == leader.Type)?.Papel ?? "";
        }

        return "";
    }

    private void EventsWeek(bool automatic)
    {
        GovernanceState g = G;
        if (g.Decision is { } pending)
        {
            if (automatic || pending.Deadline <= Week)
            {
                TermEventDefinition def = Content.Events.Find(pending.EventId)!;
                Decide(def.Opcoes.First(o => o.Padrao).Id);
            }

            return;
        }

        if (automatic)
        {
            return;
        }

        string papel = Role == GovRole.Vereador ? GovernanceContent.PapelVereador : GovernanceContent.PapelPrefeito;
        foreach (TermEventDefinition def in Rng.Shuffled(Content.Events.Eventos.Where(e => e.Papeis.Contains(papel))))
        {
            if (g.EventCooldown.TryGetValue(def.Id, out DateOnly until) && until > Week)
            {
                continue;
            }

            if (Binding(def) is not { } bound || !Rng.Chance(def.Chance))
            {
                continue;
            }

            Work? work = bound.WorkId is null ? null : g.Work(bound.WorkId);
            (string, string)[] fill =
            [
                ("pessoa", NameOf(bound.PersonId)), ("papel", RoleText(bound.PersonId)), ("zona", ZoneName(bound.ZoneId ?? work?.ZoneId)), ("obra", work is null ? "" : WorkName(work)),
                ("prefeito", MayorName), ("empresa", work is null ? "" : CompanyName(work)),
            ];
            g.Decision = new TermDecision
            {
                Id = g.NewId("dec"),
                EventId = def.Id,
                Date = Week,
                Deadline = Week.AddDays(7 * def.PrazoSemanas),
                PersonId = bound.PersonId,
                ZoneId = bound.ZoneId ?? work?.ZoneId,
                WorkId = bound.WorkId,
                Title = Fill(def.Titulo, fill),
                Text = Fill(def.Texto, fill),
            };
            g.EventCooldown[def.Id] = Week.AddDays(7 * Math.Max(1, def.CooldownSemanas));
            News("evento", T("noticia_decisao", ("titulo", g.Decision.Title), ("texto", g.Decision.Text)), g.Decision.ZoneId, bound.PersonId);
            return;
        }
    }

    /// <summary>Definição do evento da decisão pendente (as opções vêm dela).</summary>
    public TermEventDefinition? DecisionDefinition => G.Decision is { } d ? Content.Events.Find(d.EventId) : null;

    public GovForecast ForecastDecision(string optionId)
    {
        GovernanceState g = G;
        if (g.Decision is not { } decision || DecisionDefinition?.Opcoes.FirstOrDefault(o => o.Id == optionId) is not { } option)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        if (Capital < option.CapitalMinimo || Capital + option.Efeitos.Capital < 0)
        {
            return GovForecast.Block(GovFailure.SemCapital, Fill(Content.Instruments.Texto("sem_capital"), ("capital", Math.Max(option.CapitalMinimo, -option.Efeitos.Capital).ToString(CultureInfo.InvariantCulture))));
        }

        TermEffects fx = option.Efeitos;
        if (fx.Folha > 0 && Context.FiscalLaw.VedadoAumentar(g.Payroll.Share))
        {
            FiscalLawConfig law = Context.FiscalLaw;
            return GovForecast.Block(GovFailure.VedadoArt22, Fill(Content.Personnel.Texto("vedado_art22"), ("folha", Pct(g.Payroll.Share)), ("prudencial", Pct(law.PrudencialExecutivo)), ("artigo", law.ArtigoPrudencial)));
        }

        var lines = new List<ForecastLine>();
        void Add(string kind, string? target, double value)
        {
            if (Math.Abs(value) > 1e-12)
            {
                lines.Add(new ForecastLine(kind, target, value, value, value));
            }
        }

        int zone = decision.ZoneId is null ? -1 : g.IndexOf(decision.ZoneId);
        Add(GovForecast.Imagem, null, fx.Imagem);
        Add(GovForecast.Capital, null, fx.Capital);
        Add(GovForecast.Relacao, decision.PersonId, (decision.PersonId is null ? 0 : fx.Relacao) + (fx.Lembranca?.Pontos ?? 0));
        Add(GovForecast.RelacaoPrefeito, null, Role == GovRole.Vereador ? fx.RelacaoPrefeito : 0);
        Add(GovForecast.Presenca, decision.ZoneId, Role == GovRole.Vereador && zone >= 0 ? (fx.Presenca > 0 ? PresenceGain(zone, fx.Presenca) : fx.Presenca) : 0);
        Add(GovForecast.AprovacaoZona, decision.ZoneId, zone >= 0 ? fx.AprovacaoZona : 0);
        Add(GovForecast.Aprovacao, null, fx.Aprovacao);
        Add(GovForecast.Dinheiro, null, (double)fx.Dinheiro);
        Add(GovForecast.Folha, null, fx.Folha);
        Add(GovForecast.Investimento, null, fx.Investimento * (double)g.Fiscal.Rcl);
        Add(GovForecast.Esqueleto, null, fx.Esqueleto);
        var explanation = new List<string> { option.Resultado };
        var remembered = new List<string>();
        if (decision.PersonId is not null && (fx.Lembranca is not null || fx.Esqueleto > 0 || fx.PromessaSemanas > 0 || fx.Divida))
        {
            remembered.Add(NameOf(decision.PersonId));
            explanation.Add(Fill(Content.Instruments.Texto("previsao_quem_lembra"), ("nome", NameOf(decision.PersonId))));
        }

        if (fx.Esqueleto > 0 || fx.PromessaSemanas > 0 || fx.Divida)
        {
            explanation.Add(Fill(Content.Instruments.Texto("previsao_livro"), ("quem", NameOf(decision.PersonId))));
        }

        return new GovForecast(null, null, lines, [], explanation) { Remembered = remembered };
    }

    /// <summary>Responde à decisão pendente. O que mudou volta linha por linha; quem estava envolvido guarda a lembrança.</summary>
    public GovResult Decide(string optionId)
    {
        GovernanceState g = G;
        GovForecast forecast = ForecastDecision(optionId);
        if (g.Decision is not { } decision || DecisionDefinition is not { } def)
        {
            return GovResult.Fail(GovFailure.NaoEncontrado, "");
        }

        TermEventOption option = def.Opcoes.FirstOrDefault(o => o.Id == optionId) ?? def.Opcoes.First(o => o.Padrao);
        if (forecast.Blocked is { } failure)
        {
            if (option.Padrao)
            {
                // A opção padrão vale mesmo quando o jogador não a poderia pagar: o que não cabe é ignorado.
                forecast = new GovForecast(null, null, [], [], [option.Resultado]);
            }
            else
            {
                return GovResult.Fail(failure, forecast.BlockedReason ?? "");
            }
        }

        TermEffects fx = option.Efeitos;
        string? person = decision.PersonId;
        int zone = decision.ZoneId is null ? -1 : g.IndexOf(decision.ZoneId);
        var changes = new List<string>();
        void Note(bool changed, string line)
        {
            if (changed)
            {
                changes.Add(line);
            }
        }

        ShiftImage(fx.Imagem);
        Note(Math.Abs(fx.Imagem) > 1e-12, ChImage(fx.Imagem));
        Capital = Math.Max(0, Capital + fx.Capital);
        Note(fx.Capital != 0, ChCapital(fx.Capital));
        if (person is not null && fx.Relacao != 0)
        {
            Remember(person, PersonRole.Lideranca, "evento_" + def.Id, new MemoryDefinition { Peso = Math.Sign(fx.Relacao), Pontos = fx.Relacao, Texto = decision.Title });
            changes.Add(ChRelation(NameOf(person), fx.Relacao));
        }

        if (Role == GovRole.Vereador)
        {
            ShiftMayorRelation(fx.RelacaoPrefeito);
            Note(fx.RelacaoPrefeito != 0, ChRelation(MayorName, fx.RelacaoPrefeito));
            if (zone >= 0 && Math.Abs(fx.Presenca) > 1e-12)
            {
                changes.Add(ChPresence(decision.ZoneId, GainPresence(zone, fx.Presenca)));
            }
        }

        if (zone >= 0 && Math.Abs(fx.AprovacaoZona) > 1e-12)
        {
            ShiftZoneApproval(zone, fx.AprovacaoZona);
            changes.Add(ChZoneApproval(decision.ZoneId, fx.AprovacaoZona));
        }

        if (Math.Abs(fx.Aprovacao) > 1e-12)
        {
            ShiftApproval(fx.Aprovacao);
            changes.Add(ChApproval(fx.Aprovacao));
        }

        State.Player.Resources.Money += fx.Dinheiro;
        Note(fx.Dinheiro != 0, T("mud_dinheiro", ("valor", (fx.Dinheiro < 0 ? "−" : "+") + Texts.Money(Math.Abs(fx.Dinheiro)))));
        State.Player.Resources.Stress += fx.Estresse;
        Note(fx.Estresse != 0, T("mud_estresse", ("valor", Signed(fx.Estresse, "0"))));
        if (Math.Abs(fx.Folha) > 1e-12 && !(fx.Folha > 0 && Context.FiscalLaw.VedadoAumentar(g.Payroll.Share)))
        {
            g.Payroll.Share = Math.Max(0, g.Payroll.Share + fx.Folha);
            changes.Add(T("mud_folha_pontos", ("valor", Signed(fx.Folha * 100, "0.00"))));
        }

        if (Math.Abs(fx.Investimento) > 1e-12)
        {
            decimal amount = g.Fiscal.Rcl * (decimal)fx.Investimento;
            g.Fiscal.InvestmentUsed -= amount;
            changes.Add(ChInvestment(amount));
        }

        if (fx.ObraSemanas != 0 && decision.WorkId is not null && g.Work(decision.WorkId) is { Open: true, StageEnd: not null } work)
        {
            work.StageEnd = work.StageEnd.Value.AddDays(7 * fx.ObraSemanas);
            work.DelayWeeks = Math.Max(0, work.DelayWeeks + fx.ObraSemanas);
            changes.Add(T("mud_obra_prazo", ("valor", Signed(fx.ObraSemanas, "0")), ("obra", WorkName(work))));
        }

        Secretary? secretary = person is null ? null : g.Secretaries.FirstOrDefault(s => s.PersonId == person && s.SecretariaId is not null && !s.Left);
        Aide? aide = person is null ? null : g.Cabinet.FirstOrDefault(a => a.PersonId == person);
        if (fx.Lealdade != 0 && secretary is not null)
        {
            secretary.Loyalty = Math.Clamp(secretary.Loyalty + fx.Lealdade, 0, 100);
            changes.Add(T("mud_lealdade", ("valor", Signed(fx.Lealdade, "0")), ("nome", NameOf(person))));
        }

        if (aide is not null && fx.Lealdade > 0)
        {
            aide.Neglected = false;
            aide.LastSupported = Week;
        }

        if (fx.Esqueleto > 0)
        {
            State.Ledger.AddSkeleton(fx.EsqueletoTipo ?? def.Id, decision.Title, fx.Esqueleto, Week, person is null ? [] : [person]);
            string gravity = fx.Esqueleto.ToString(CultureInfo.InvariantCulture);
            changes.Add(person is null ? T("mud_esqueleto_sem_testemunha", ("gravidade", gravity)) : T("mud_esqueleto", ("gravidade", gravity), ("quem", NameOf(person))));
        }

        if (fx.PromessaSemanas > 0 && person is not null)
        {
            string promised = T("promessa_de_evento", ("opcao", Fill(option.Rotulo, ("zona", ZoneName(decision.ZoneId)))), ("nome", NameOf(person)));
            Promise promise = State.Ledger.AddPromise(Promise.ParaPessoa, person, fx.PromessaObjeto ?? "evento_" + def.Id, promised, decision.ZoneId, Week, Week.AddDays(7 * fx.PromessaSemanas));
            changes.Add(T("mud_promessa", ("data", Texts.Date(promise.Deadline)), ("promessa", promised)));
        }

        if (fx.Divida && person is not null)
        {
            State.Ledger.AddDebt(person, decision.Title, Week);
            changes.Add(T("mud_divida", ("nome", NameOf(person))));
        }

        if (Math.Abs(fx.CpiProva) > 1e-12 && g.Cpi is { } cpi)
        {
            cpi.Evidence = Math.Clamp(cpi.Evidence + fx.CpiProva, 0, 1);
            changes.Add(T("mud_cpi", ("valor", Signed(fx.CpiProva * 100, "0"))));
        }

        if (fx.Lembranca is { } memory && person is not null)
        {
            Remember(person, PersonRole.Lideranca, "evento_" + def.Id + "_" + option.Id, memory, ("zona", ZoneName(decision.ZoneId)));
            changes.Add(T("mud_lembranca", ("nome", NameOf(person)), ("texto", Fill(memory.Texto, ("zona", ZoneName(decision.ZoneId))))));
        }

        if (fx.Sai)
        {
            if (secretary is not null)
            {
                changes.AddRange(Unseat(secretary));
            }
            else if (aide is not null)
            {
                aide.Appointed = false;
                aide.Left = true;
                changes.Add(T("mud_sai", ("nome", NameOf(person))));
            }
        }

        if (decision.ZoneId is not null && (fx.Presenca > 0 || fx.AprovacaoZona > 0))
        {
            TouchZone(decision.ZoneId);
        }

        g.Decision = null;
        News("evento", T("noticia_decisao_resposta", ("titulo", decision.Title), ("opcao", Fill(option.Rotulo, ("zona", ZoneName(decision.ZoneId)))), ("resultado", option.Resultado)), decision.ZoneId, person);
        RefreshDerived();
        return new GovResult(null, option.Resultado, changes, []);
    }
}
