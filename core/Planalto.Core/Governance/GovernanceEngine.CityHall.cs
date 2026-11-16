using System.Globalization;
using Planalto.Core.Campaign;
using Planalto.Core.Executive;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Governance;

/// <summary>Prefeitura (GDD 17.15): secretários, folha com os limites fiscais, convênios, Câmara que negocia e crises com causa.</summary>
public sealed partial class GovernanceEngine
{
    private SecretaryRules SecretaryRules => Content.Secretaries.Regras;

    private PersonnelRules PersonnelRules => Content.Personnel.Regras;

    private string MayorPartyId => Role == GovRole.Prefeito ? Exec?.PartyId ?? "" : Mandate?.MayorPartyId ?? "";

    // ================================================================ secretários

    private Secretary NewSecretaryName(string? partyId)
    {
        GovernanceState g = G;
        Person person = NewPerson(PersonRole.Secretario, 30, 68, partyId);
        string agenda = Rng.PickWeighted(Content.Secretaries.Agendas, a => a.Weight).Id;
        var secretary = new Secretary
        {
            PersonId = person.Id,
            PartyId = partyId,
            Competence = Rng.PickWeighted([1, 2, 3, 4, 5], q => q is 1 or 5 ? 1.0 : q == 3 ? 3.0 : 2.0),
            Loyalty = Math.Clamp(Rng.NextInt(SecretaryRules.LealdadeInicial.Min, SecretaryRules.LealdadeInicial.Max + 1) + (partyId == MayorPartyId ? SecretaryRules.LealdadeDoProprioPartido : 0), 0, 100),
            Integrity = Rng.PickWeighted([1, 2, 3, 4, 5], q => q is 1 or 5 ? 1.0 : 2.0),
            Agenda = agenda,
            AgendaZone = agenda == SecretaryAgenda.Zona ? Rng.Pick(g.Zones) : null,
            AgendaWork = agenda == SecretaryAgenda.Projeto ? Rng.Pick(Content.Works.Tipos.Where(t => !t.Servico).ToList()).Id : null,
        };
        g.Secretaries.Add(secretary);
        return secretary;
    }

    /// <summary>Posse: cada pasta ganha um titular, repartidas entre o partido do prefeito e os da base.</summary>
    private void SeatSecretaries()
    {
        GovernanceState g = G;
        var pastas = Content.Secretaries.Secretarias.Select(s => s.Id).ToList();
        var parties = new List<string>();
        if (Role == GovRole.Prefeito && Exec is { } e)
        {
            foreach (var (party, count) in e.Secretarias.OrderByDescending(s => s.Key == e.PartyId).ThenBy(s => s.Key, StringComparer.Ordinal))
            {
                parties.AddRange(Enumerable.Repeat(party, count));
            }
        }
        else if (Mandate is { } mandate)
        {
            List<string> allies = mandate.BaseParties.Where(p => p != mandate.MayorPartyId).ToList();
            parties.AddRange(Enumerable.Repeat(mandate.MayorPartyId, Math.Max(1, pastas.Count - allies.Count)));
            parties.AddRange(allies);
        }

        for (int i = 0; i < pastas.Count; i++)
        {
            Secretary secretary = NewSecretaryName(i < parties.Count ? parties[i] : MayorPartyId);
            secretary.SecretariaId = pastas[i];
            secretary.Since = Week;
        }

        if (Role == GovRole.Vereador)
        {
            News("secretario", Fill(Content.Secretaries.Texto("posse_npc"), ("prefeito", MayorName), ("quantidade", pastas.Count.ToString(CultureInfo.InvariantCulture))));
        }
    }

    /// <summary>Cada partido com cadeira (e o do prefeito) mantém nomes a oferecer; há também nomes técnicos, sem partido.</summary>
    private void EnsureOffers()
    {
        GovernanceState g = G;
        var parties = Members.Select(m => m.PartyId).Append(MayorPartyId).Where(p => !string.IsNullOrEmpty(p)).Distinct().Cast<string?>().Append(null).ToList();
        foreach (string? party in parties)
        {
            int have = g.Secretaries.Count(s => s.SecretariaId is null && !s.Left && s.PartyId == party);
            for (int i = have; i < SecretaryRules.OfertasPorPartido; i++)
            {
                NewSecretaryName(party);
            }
        }
    }

    /// <summary>Nomes que podem ser nomeados agora: os que cada partido oferece e os técnicos.</summary>
    public IEnumerable<Secretary> SecretaryOffers(string? partyId = null) =>
        G.Secretaries.Where(s => s.SecretariaId is null && !s.Left && (partyId is null || s.PartyId == partyId));

    /// <summary>A competência exata só aparece depois de algumas semanas no cargo; antes, uma faixa.</summary>
    public (int Min, int Max) CompetenceShown(Secretary s)
    {
        bool known = G.KnownSecretaries.Contains(s.PersonId) || (s.Since is { } since && Week.DayNumber - since.DayNumber >= 7 * SecretaryRules.SemanasParaConhecer);
        return known ? (s.Competence, s.Competence) : (Math.Max(1, s.Competence - 1), Math.Min(5, s.Competence + 1));
    }

    private void SyncSecretariaCounts()
    {
        if (Exec is not { } e)
        {
            return;
        }

        foreach (string party in e.Secretarias.Keys.ToList())
        {
            e.Secretarias[party] = 0;
        }

        foreach (Secretary s in G.Secretaries.Where(s => s.SecretariaId is not null && !s.Left))
        {
            string party = s.PartyId ?? e.PartyId;
            e.Secretarias[party] = e.Secretarias.GetValueOrDefault(party) + 1;
        }
    }

    public GovForecast ForecastAppointSecretary(string secretariaId, string personId)
    {
        GovernanceState g = G;
        Secretary? candidate = g.Secretaries.FirstOrDefault(s => s.PersonId == personId && s.SecretariaId is null && !s.Left);
        if (Role != GovRole.Prefeito)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        if (candidate is null || Content.Secretaries.Secretarias.All(s => s.Id != secretariaId))
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        var (min, max) = CompetenceShown(candidate);
        Secretary? current = g.SecretaryOf(secretariaId);
        var lines = new List<ForecastLine>();
        var explanation = new List<string>
        {
            Fill(Content.Secretaries.Texto("previsao_nomear"), ("nome", NameOf(personId)), ("secretaria", Content.Secretaries.Secretaria(secretariaId).Nome),
                ("vinculo", candidate.PartyId is null ? Content.Secretaries.Texto("vinculo_sem_partido") : Fill(Content.Secretaries.Texto("vinculo_partido"), ("partido", State.Party(candidate.PartyId)?.Sigla ?? candidate.PartyId)))),
            Fill(Content.Secretaries.Texto("previsao_competencia"), ("min", min.ToString(CultureInfo.InvariantCulture)), ("max", max.ToString(CultureInfo.InvariantCulture)),
                ("vmin", Texts.Percent(SecretaryRules.VelocidadePorCompetencia[min - 1])), ("vmax", Texts.Percent(SecretaryRules.VelocidadePorCompetencia[max - 1]))),
            Fill(Content.Secretaries.Texto("previsao_agenda"), ("agenda", AgendaText(candidate))),
        };
        var remembered = new List<string> { NameOf(personId) };
        if (candidate.PartyId is not null && candidate.PartyId != MayorPartyId)
        {
            int seats = Members.Count(m => m.PartyId == candidate.PartyId);
            lines.Add(new ForecastLine(GovForecast.Relacao, candidate.PartyId, SecretaryRules.RelacaoBancadaAoNomear, SecretaryRules.RelacaoBancadaAoNomear, SecretaryRules.RelacaoBancadaAoNomear));
            explanation.Add(T("bancada_melhora", ("cadeiras", seats.ToString(CultureInfo.InvariantCulture))));
        }

        if (current is not null)
        {
            explanation.AddRange(ForecastDismissSecretary(secretariaId).Explanation);
            remembered.Add(NameOf(current.PersonId));
        }

        remembered.AddRange(SecretaryOffers(candidate.PartyId).Where(s => s.PersonId != personId && candidate.PartyId is not null).Select(s => NameOf(s.PersonId)));
        return new GovForecast(null, null, lines, [], explanation) { Remembered = remembered.Where(n => n.Length > 0).Distinct().ToList() };
    }

    private string AgendaText(Secretary s) =>
        Fill(Content.Secretaries.Agenda(s.Agenda).Texto, ("zona", ZoneName(s.AgendaZone)), ("obra", s.AgendaWork is null ? "" : Content.Works.Find(s.AgendaWork)?.Nome ?? ""));

    /// <summary>Texto da agenda própria do secretário, para a ficha.</summary>
    public string AgendaOf(Secretary s) => AgendaText(s);

    /// <summary>Nomeia um dos nomes oferecidos para a pasta (o titular atual, se houver, é demitido). A bancada do partido melhora a relação; o outro nome oferecido lembra.</summary>
    public GovResult AppointSecretary(string secretariaId, string personId)
    {
        GovForecast forecast = ForecastAppointSecretary(secretariaId, personId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        GovernanceState g = G;
        var changes = new List<string>();
        if (g.SecretaryOf(secretariaId) is not null)
        {
            changes.AddRange(DismissSecretary(secretariaId).Changes);
        }

        Secretary secretary = g.Secretaries.First(s => s.PersonId == personId);
        Seat(secretary, secretariaId);
        foreach (Secretary other in SecretaryOffers(secretary.PartyId).Where(s => secretary.PartyId is not null).ToList())
        {
            Remember(other.PersonId, PersonRole.Secretario, "preterido", Content.Secretaries.Lembrancas["preterido"], ("secretaria", Content.Secretaries.Secretaria(secretariaId).Nome));
        }

        changes.Add(T("mud_assume", ("nome", NameOf(personId)), ("secretaria", Content.Secretaries.Secretaria(secretariaId).Nome)));
        changes.AddRange(SettleDebts(personId));
        EnsureOffers();
        RefreshDerived();
        return new GovResult(null, Fill(Content.Secretaries.Texto("nomeado"), ("nome", NameOf(personId)), ("secretaria", Content.Secretaries.Secretaria(secretariaId).Nome)), changes, []);
    }

    private void Seat(Secretary secretary, string secretariaId)
    {
        secretary.SecretariaId = secretariaId;
        secretary.Since = Week;
        G.VacantSince.Remove(secretariaId);
        Remember(secretary.PersonId, PersonRole.Secretario, "nomeado", Content.Secretaries.Lembrancas["nomeado"], ("secretaria", Content.Secretaries.Secretaria(secretariaId).Nome));
        if (secretary.PartyId is not null && secretary.PartyId != MayorPartyId)
        {
            foreach (ChamberMember m in Members.Where(m => m.PartyId == secretary.PartyId))
            {
                m.Relation = Math.Clamp(m.Relation + SecretaryRules.RelacaoBancadaAoNomear, -100, 100);
            }
        }

        SyncSecretariaCounts();
        News("secretario", Fill(Content.Secretaries.Texto("nomeado"), ("nome", NameOf(secretary.PersonId)), ("secretaria", Content.Secretaries.Secretaria(secretariaId).Nome)), null, secretary.PersonId);
    }

    public GovForecast ForecastDismissSecretary(string secretariaId)
    {
        Secretary? secretary = G.SecretaryOf(secretariaId);
        if (Role != GovRole.Prefeito)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        if (secretary is null)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        MemoryDefinition memory = Content.Secretaries.Lembrancas["demitido"];
        var lines = new List<ForecastLine> { new(GovForecast.Relacao, secretary.PersonId, memory.Pontos, memory.Pontos, memory.Pontos) };
        var explanation = new List<string> { Fill(Content.Secretaries.Texto("previsao_demitir"), ("nome", NameOf(secretary.PersonId)), ("secretaria", Content.Secretaries.Secretaria(secretariaId).Nome)) };
        if (LeavesBase(secretary))
        {
            int seats = Members.Count(m => m.PartyId == secretary.PartyId);
            lines.Add(new ForecastLine(GovForecast.Relacao, secretary.PartyId, SecretaryRules.RelacaoBancadaAoDemitir, SecretaryRules.RelacaoBancadaAoDemitir, SecretaryRules.RelacaoBancadaAoDemitir));
            explanation.Add(Fill(Content.Secretaries.Texto("previsao_demitir_base"), ("partido", State.Party(secretary.PartyId)?.Sigla ?? secretary.PartyId ?? ""), ("cadeiras", seats.ToString(CultureInfo.InvariantCulture))));
        }

        return new GovForecast(null, null, lines, [], explanation) { Remembered = [NameOf(secretary.PersonId)] };
    }

    private bool LeavesBase(Secretary secretary) =>
        secretary.PartyId is not null && secretary.PartyId != MayorPartyId && G.Secretaries.Count(s => s.SecretariaId is not null && !s.Left && s.PartyId == secretary.PartyId) == 1;

    /// <summary>Demite o secretário. Se era a única pasta do partido dele, o partido sai da base.</summary>
    public GovResult DismissSecretary(string secretariaId)
    {
        GovForecast forecast = ForecastDismissSecretary(secretariaId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Secretary secretary = G.SecretaryOf(secretariaId)!;
        var changes = Unseat(secretary);
        RefreshDerived();
        return new GovResult(null, Fill(Content.Secretaries.Texto("demitido"), ("nome", NameOf(secretary.PersonId)), ("secretaria", Content.Secretaries.Secretaria(secretariaId).Nome)), changes, []);
    }

    private List<string> Unseat(Secretary secretary)
    {
        var changes = new List<string>();
        string pasta = Content.Secretaries.Secretaria(secretary.SecretariaId!).Nome;
        bool leaves = LeavesBase(secretary);
        Remember(secretary.PersonId, PersonRole.Secretario, "demitido", Content.Secretaries.Lembrancas["demitido"], ("secretaria", pasta));
        changes.Add(ChRelation(NameOf(secretary.PersonId), Content.Secretaries.Lembrancas["demitido"].Pontos));
        G.VacantSince[secretary.SecretariaId!] = Week;
        secretary.Left = true;
        secretary.SecretariaId = null;
        if (leaves)
        {
            foreach (ChamberMember m in Members.Where(m => m.PartyId == secretary.PartyId))
            {
                m.Relation = Math.Clamp(m.Relation + SecretaryRules.RelacaoBancadaAoDemitir, -100, 100);
            }

            string sigla = State.Party(secretary.PartyId)?.Sigla ?? secretary.PartyId ?? "";
            changes.Add(Fill(Content.Secretaries.Texto("saiu_da_base"), ("partido", sigla)));
            News("secretario", Fill(Content.Secretaries.Texto("saiu_da_base"), ("partido", sigla)));
        }

        SyncSecretariaCounts();
        News("secretario", Fill(Content.Secretaries.Texto("demitido"), ("nome", NameOf(secretary.PersonId)), ("secretaria", pasta)), null, secretary.PersonId);
        return changes;
    }

    private void SecretariesWeek()
    {
        GovernanceState g = G;
        // Pasta vaga não fica vaga para sempre: o partido do prefeito indica um nome.
        foreach (SecretariaDefinition pasta in Content.Secretaries.Secretarias.Where(s => g.SecretaryOf(s.Id) is null))
        {
            DateOnly vacantSince = g.VacantSince.GetValueOrDefault(pasta.Id, g.Start);
            if (Role == GovRole.Vereador || Week.DayNumber - vacantSince.DayNumber >= 7 * SecretaryRules.SemanasPastaVaga)
            {
                Secretary pick = SecretaryOffers(MayorPartyId).FirstOrDefault() ?? NewSecretaryName(MayorPartyId);
                Seat(pick, pasta.Id);
            }
        }

        foreach (Secretary s in g.Secretaries.Where(s => s.SecretariaId is not null && !s.Left).ToList())
        {
            if (s.Agenda != SecretaryAgenda.Candidato || !Rng.Chance(SecretaryRules.ChanceAparicao))
            {
                continue;
            }

            // Quer ser candidato e usa a pasta para aparecer; com pouca lealdade, aparece contra o prefeito.
            string pasta = Content.Secretaries.Secretaria(s.SecretariaId!).Nome;
            if (s.Loyalty < SecretaryRules.LealdadeDeCritica)
            {
                ShiftApproval(SecretaryRules.ImagemCritica);
                News("secretario", Fill(Content.Secretaries.Texto("critica"), ("nome", NameOf(s.PersonId)), ("secretaria", pasta), ("prefeito", MayorName)), null, s.PersonId);
            }
            else
            {
                News("secretario", Fill(Content.Secretaries.Texto("aparicao"), ("nome", NameOf(s.PersonId)), ("secretaria", pasta)), null, s.PersonId);
            }
        }

        if (Role == GovRole.Prefeito)
        {
            ReconcileWithCounts();
        }
        else
        {
            g.MayorApproval += (50 - g.MayorApproval) * SecretaryRules.AprovacaoNpcRetornoSemanal;
        }

        for (int z = 0; z < g.ZoneApproval.Count; z++)
        {
            g.ZoneApproval[z] *= 1 - SecretaryRules.AprovacaoNpcRetornoSemanal;
        }
    }

    /// <summary>
    /// A tela antiga da prefeitura entrega e retira secretarias por partido (contagem). Quando a contagem diverge dos
    /// titulares, o governo se ajusta: o partido que ganhou a pasta indica o primeiro nome oferecido.
    /// </summary>
    private void ReconcileWithCounts()
    {
        if (Exec is not { } e)
        {
            return;
        }

        GovernanceState g = G;
        Dictionary<string, int> held = g.Secretaries.Where(s => s.SecretariaId is not null && !s.Left).GroupBy(s => s.PartyId ?? e.PartyId).ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
        foreach (var (party, wanted) in e.Secretarias.ToList())
        {
            int have = held.GetValueOrDefault(party);
            while (have < wanted)
            {
                string? donor = held.Where(h => h.Value > e.Secretarias.GetValueOrDefault(h.Key)).OrderByDescending(h => h.Key == e.PartyId).Select(h => h.Key).FirstOrDefault();
                Secretary? outgoing = donor is null ? null : g.Secretaries.LastOrDefault(s => s.SecretariaId is not null && !s.Left && (s.PartyId ?? e.PartyId) == donor);
                if (outgoing is null)
                {
                    break;
                }

                string pasta = outgoing.SecretariaId!;
                outgoing.Left = true;
                outgoing.SecretariaId = null;
                held[donor!]--;
                Secretary incoming = SecretaryOffers(party).FirstOrDefault() ?? NewSecretaryName(party);
                incoming.SecretariaId = pasta;
                incoming.Since = Week;
                have++;
                held[party] = have;
            }
        }

        EnsureOffers();
    }

    // ================================================================ folha e limites fiscais

    /// <summary>Base dos mínimos de saúde e educação e do teto da Câmara, em reais: impostos e transferências de impostos.</summary>
    public decimal TaxBase => G.Fiscal.Rcl * (decimal)G.Fiscal.TaxBaseShare;

    /// <summary>Teto do repasse à Câmara (CF, art. 29-A), em fração da base de impostos.</summary>
    public double ChamberCap => Context.FiscalLaw.TetoCamara(Context.Municipio.Populacao);

    private Leader? UnionLeader => City.Leaders.FirstOrDefault(l => l.Type == PersonnelRules.LiderancaSindical);

    private void PayrollWeek(bool automatic)
    {
        GovernanceState g = G;
        FiscalLawConfig law = Context.FiscalLaw;
        bool npc = Role == GovRole.Vereador || automatic;

        // Mesas de negociação: abrem no mês da data-base; a resposta que não vem no prazo é a padrão.
        foreach (NegotiationTable table in Content.Personnel.Mesas)
        {
            if (Week.Month == table.Mes && !g.Negotiations.Any(n => n.TableId == table.Id && n.Date.Year == Week.Year))
            {
                double asked = Rng.NextDouble(table.Pedido.Min, table.Pedido.Max) + (table.AfetaFolha ? g.Payroll.Loss : 0);
                var negotiation = new Negotiation
                {
                    Id = g.NewId("neg"),
                    TableId = table.Id,
                    Date = Week,
                    Deadline = Week.AddDays(7 * PersonnelRules.PrazoSemanas),
                    Asked = asked,
                    GeneralRevision = Math.Min(asked, g.Fiscal.GeneralRevision),
                };
                g.Negotiations.Add(negotiation);
                News("folha", Fill(Content.Personnel.Texto("mesa_aberta"), ("mesa", table.Nome), ("categoria", table.Categoria), ("pedido", Pct(asked)), ("data", Texts.Date(negotiation.Deadline))), null, UnionLeader?.PersonId);
            }
        }

        foreach (Negotiation negotiation in g.Negotiations.Where(n => n.Outcome == NegotiationOutcome.Pendente).ToList())
        {
            if (npc && negotiation.Date < Week)
            {
                // O prefeito NPC oferece o que a folha permite.
                bool tight = g.Payroll.Share >= PersonnelRules.NpcFolhaConfortavel || law.VedadoAumentar(g.Payroll.Share);
                Settle(negotiation, tight ? NegotiationAnswer.RevisaoGeral : NegotiationAnswer.Contraproposta);
            }
            else if (negotiation.Deadline <= Week)
            {
                News("folha", Fill(Content.Personnel.Texto("sem_resposta"), ("mesa", Content.Personnel.Mesa(negotiation.TableId).Nome)));
                Settle(negotiation, NegotiationAnswer.RevisaoGeral);
            }
        }

        // Faixas da LRF.
        PayrollBand before = g.Payroll.Band;
        PayrollBand band = law.Faixa(g.Payroll.Share);
        g.Payroll.Band = band;
        if (band != before)
        {
            string key = band switch { PayrollBand.Alerta => "alerta", PayrollBand.Prudencial => "prudencial", PayrollBand.Excedido => "excedido", _ => "normalizou" };
            if (band > before || band == PayrollBand.Normal)
            {
                News("folha", Fill(Content.Personnel.Texto(key), ("folha", Pct(g.Payroll.Share)), ("alerta", Pct(law.AlertaExecutivo)), ("prudencial", Pct(law.PrudencialExecutivo)),
                    ("limite", Pct(law.LimitePessoalExecutivo)), ("artigo", band == PayrollBand.Alerta ? law.ArtigoAlerta : band == PayrollBand.Prudencial ? law.ArtigoPrudencial : law.ArtigoExcesso)));
            }
        }

        if (band == PayrollBand.Excedido)
        {
            if (g.Payroll.ExcessSince is null)
            {
                g.Payroll.ExcessSince = Week;
                g.Payroll.ExcessAtStart = g.Payroll.Share - law.LimitePessoalExecutivo;
                g.Payroll.OneThirdChecked = false;
            }

            if (npc)
            {
                // O prefeito NPC corta para voltar ao limite.
                g.Payroll.Share *= 1 - PersonnelRules.CorteFracao;
                ShiftApproval(PersonnelRules.CorteAprovacao);
                News("folha", Fill(Content.Personnel.Texto("corte"), ("prefeito", MayorName), ("folha", Pct(g.Payroll.Share))));
            }
        }
        else
        {
            g.Payroll.ExcessSince = null;
            g.Payroll.Restricted = false;
        }

        if (g.Payroll.ExcessSince is { } since)
        {
            double excess = g.Payroll.Share - law.LimitePessoalExecutivo;
            if (!g.Payroll.OneThirdChecked && Week >= law.PrazoDoPrimeiroTerco(since))
            {
                g.Payroll.OneThirdChecked = true;
                if (excess > g.Payroll.ExcessAtStart * (1 - law.FracaoNoPrimeiroQuadrimestre))
                {
                    News("folha", Fill(Content.Personnel.Texto("excedido"), ("folha", Pct(g.Payroll.Share)), ("alerta", Pct(law.AlertaExecutivo)), ("prudencial", Pct(law.PrudencialExecutivo)),
                        ("limite", Pct(law.LimitePessoalExecutivo)), ("artigo", law.ArtigoExcesso)));
                }
            }

            bool restricted = law.RestricoesAtivas(g.Payroll.Share, since, Week, g.End.Year);
            if (restricted && !g.Payroll.Restricted)
            {
                News("folha", Fill(Content.Personnel.Texto("restricoes"), ("artigo", law.ArtigoExcesso), ("consequencias", string.Join("; ", law.ConsequenciasExcesso).ToLowerInvariant())));
            }

            g.Payroll.Restricted = restricted;
            if (restricted)
            {
                ShiftApproval(PersonnelRules.RestricaoAprovacaoSemanal);
            }
        }
    }

    public Negotiation? PendingNegotiation => G.Negotiations.FirstOrDefault(n => n.Outcome == NegotiationOutcome.Pendente);

    private double OfferOf(Negotiation negotiation, string answerId) => answerId switch
    {
        NegotiationAnswer.Conceder => negotiation.Asked,
        NegotiationAnswer.Contraproposta => Math.Max(negotiation.GeneralRevision, negotiation.Asked * PersonnelRules.ContrapropostaFracao),
        NegotiationAnswer.RevisaoGeral => negotiation.GeneralRevision,
        _ => 0,
    };

    /// <summary>Chance de a categoria aceitar a oferta: quanto do pedido ela cobre e a relação com quem negocia.</summary>
    public double AcceptChance(Negotiation negotiation, string answerId)
    {
        if (answerId == NegotiationAnswer.Conceder)
        {
            return 1;
        }

        double relation = UnionLeader is { } leader ? RelationOf(leader.PersonId) : 0;
        return Math.Clamp(PersonnelRules.AceiteBase + (PersonnelRules.AceitePorOferta * OfferOf(negotiation, answerId) / Math.Max(1e-9, negotiation.Asked)) + (PersonnelRules.AceitePorRelacao * relation / 100), 0, 0.97);
    }

    /// <summary>O reajuste está vedado pelo art. 22 (acima do limite prudencial só cabe a revisão geral anual)?</summary>
    private bool RaiseBarred(Negotiation negotiation, string answerId) =>
        Content.Personnel.Mesa(negotiation.TableId).AfetaFolha && Context.FiscalLaw.VedadoAumentar(G.Payroll.Share) && OfferOf(negotiation, answerId) > negotiation.GeneralRevision + 1e-9;

    public GovForecast ForecastNegotiation(string negotiationId, string answerId)
    {
        GovernanceState g = G;
        Negotiation? negotiation = g.Negotiations.FirstOrDefault(n => n.Id == negotiationId && n.Outcome == NegotiationOutcome.Pendente);
        if (Role != GovRole.Prefeito)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        if (negotiation is null || !NegotiationAnswer.Ids.Contains(answerId))
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        FiscalLawConfig law = Context.FiscalLaw;
        if (RaiseBarred(negotiation, answerId))
        {
            return GovForecast.Block(GovFailure.VedadoArt22, Fill(Content.Personnel.Texto("vedado_art22"), ("folha", Pct(g.Payroll.Share)), ("prudencial", Pct(law.PrudencialExecutivo)), ("artigo", law.ArtigoPrudencial)));
        }

        NegotiationTable table = Content.Personnel.Mesa(negotiation.TableId);
        double offer = OfferOf(negotiation, answerId);
        double chance = AcceptChance(negotiation, answerId);
        var lines = new List<ForecastLine>();
        var explanation = new List<string>
        {
            Fill(Content.Personnel.Texto($"previsao_{answerId}"), ("mesa", table.Nome), ("categoria", table.Categoria), ("pedido", Pct(negotiation.Asked)), ("oferta", Pct(offer)), ("chance", Texts.Percent(chance))),
        };
        if (table.AfetaFolha)
        {
            double after = g.Payroll.Share * (1 + offer);
            lines.Add(new ForecastLine(GovForecast.Folha, null, 0, after - g.Payroll.Share, chance * (after - g.Payroll.Share)));
            explanation.Add(Fill(Content.Personnel.Texto("previsao_folha"), ("antes", Pct(g.Payroll.Share)), ("depois", Pct(after)), ("faixa", BandName(law.Faixa(after))), ("prudencial", Pct(law.PrudencialExecutivo)),
                ("limite", Pct(law.LimitePessoalExecutivo))));
        }

        CrisisDefinition crisis = Context.Executive.Crises.Find(table.Crise)!;
        return new GovForecast(null, null, lines,
            chance >= 1 ? [] : [new ForecastOutcome(chance, Content.Personnel.Texto("acordo")), new ForecastOutcome(1 - chance, Fill(Content.Personnel.Texto("greve"), ("crise", crisis.Name), ("categoria", table.Categoria)))], explanation)
        { Remembered = UnionLeader is { } leader ? [NameOf(leader.PersonId)] : [] };
    }

    /// <summary>Responde à mesa de negociação. Recusada a oferta, a categoria entra em greve: a greve é consequência, não sorteio.</summary>
    public GovResult AnswerNegotiation(string negotiationId, string answerId)
    {
        GovForecast forecast = ForecastNegotiation(negotiationId, answerId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Negotiation negotiation = G.Negotiations.First(n => n.Id == negotiationId);
        List<string> changes = Settle(negotiation, answerId);
        string text = negotiation.Outcome == NegotiationOutcome.Acordo
            ? Fill(Content.Personnel.Texto("acordo"), ("categoria", Content.Personnel.Mesa(negotiation.TableId).Categoria), ("reajuste", Pct(negotiation.Granted)))
            : Fill(Content.Personnel.Texto("fracasso"), ("categoria", Content.Personnel.Mesa(negotiation.TableId).Categoria));
        return new GovResult(null, text, changes, []);
    }

    private List<string> Settle(Negotiation negotiation, string answerId)
    {
        GovernanceState g = G;
        NegotiationTable table = Content.Personnel.Mesa(negotiation.TableId);
        if (RaiseBarred(negotiation, answerId))
        {
            answerId = NegotiationAnswer.RevisaoGeral;
        }

        var changes = new List<string>();
        double offer = OfferOf(negotiation, answerId);
        bool accepted = Rng.Chance(AcceptChance(negotiation, answerId));
        negotiation.Answer = answerId;
        negotiation.Closed = Week;
        string? union = UnionLeader?.PersonId;
        if (accepted)
        {
            negotiation.Outcome = NegotiationOutcome.Acordo;
            negotiation.Granted = offer;
            if (table.AfetaFolha)
            {
                double before = g.Payroll.Share;
                g.Payroll.Share *= 1 + offer;
                g.Payroll.Loss = Math.Max(0, (negotiation.Asked - offer) * PersonnelRules.PerdaAcumulada);
                changes.Add(ChPayroll(before, g.Payroll.Share));
            }
            else if (offer > 0)
            {
                double cost = table.CustoPorPonto * offer * 100;
                ShiftAccounts(-cost);
                changes.Add(T("mud_subsidio", ("valor", Pct(cost))));
            }

            Remember(union, PersonRole.Lideranca, "acordo", Content.Personnel.Lembrancas["acordo"], ("reajuste", Pct(offer)));
            News("folha", Fill(Content.Personnel.Texto("acordo"), ("categoria", table.Categoria), ("reajuste", Pct(offer))), null, union);
            return changes;
        }

        negotiation.Outcome = NegotiationOutcome.Fracassou;
        if (table.AfetaFolha)
        {
            g.Payroll.Loss = Math.Max(0, negotiation.Asked * PersonnelRules.PerdaAcumulada);
        }

        Remember(union, PersonRole.Lideranca, "fracasso", Content.Personnel.Lembrancas["fracasso"], ("oferta", Pct(offer)));
        News("folha", Fill(Content.Personnel.Texto("fracasso"), ("categoria", table.Categoria)), null, union);
        CrisisDefinition crisis = Context.Executive.Crises.Find(table.Crise)!;
        CrisisRecord record = RaiseCrisis(crisis, null, CrisisRecord.CausaNegociacao, negotiation.Id,
            Fill(crisis.Cause?.Text ?? Content.Personnel.Texto("greve"), ("mesa", table.Nome), ("categoria", table.Categoria), ("crise", crisis.Name), ("oferta", Pct(offer)), ("pedido", Pct(negotiation.Asked))), 1);
        negotiation.StrikeRecordId = record.Id;
        changes.Add(Fill(Content.Personnel.Texto("greve"), ("crise", crisis.Name), ("categoria", table.Categoria)));
        return changes;
    }

    /// <summary>Gasto único que pesa nas contas (com o jogador prefeito) ou na aprovação (prefeito NPC).</summary>
    private void ShiftAccounts(double budgetFraction)
    {
        if (Exec is { } e && e.Indicators.ContainsKey("contas"))
        {
            BoundsConfig bounds = Context.Executive.Config.IndicatorBounds;
            e.Indicators["contas"] = Math.Clamp(e.Indicators["contas"] + (budgetFraction * Context.Executive.Config.UnusedReserveToAccounts), bounds.Min, bounds.Max);
        }
        else
        {
            G.MayorApproval = Math.Clamp(G.MayorApproval + (budgetFraction * 100), 0, 100);
        }
    }

    public GovForecast ForecastCutPayroll()
    {
        if (Role != GovRole.Prefeito)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        GovernanceState g = G;
        double after = g.Payroll.Share * (1 - PersonnelRules.CorteFracao);
        return new GovForecast(null, null,
        [
            new ForecastLine(GovForecast.Folha, null, after - g.Payroll.Share, after - g.Payroll.Share, after - g.Payroll.Share),
            new ForecastLine(GovForecast.Aprovacao, null, PersonnelRules.CorteAprovacao, PersonnelRules.CorteAprovacao, PersonnelRules.CorteAprovacao),
        ], [],
        [
            Fill(Content.Personnel.Texto("previsao_corte"), ("antes", Pct(g.Payroll.Share)), ("depois", Pct(after)), ("faixa", BandName(Context.FiscalLaw.Faixa(after)))),
        ])
        { Remembered = UnionLeader is { } leader ? [NameOf(leader.PersonId)] : [] };
    }

    /// <summary>Corta pessoal (comissionados, horas extras) para baixar a folha: custa aprovação e a relação com o sindicato.</summary>
    public GovResult CutPayroll()
    {
        GovForecast forecast = ForecastCutPayroll();
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, "");
        }

        GovernanceState g = G;
        double before = g.Payroll.Share;
        g.Payroll.Share *= 1 - PersonnelRules.CorteFracao;
        ShiftApproval(PersonnelRules.CorteAprovacao);
        Remember(UnionLeader?.PersonId, PersonRole.Lideranca, "corte", Content.Personnel.Lembrancas["corte"]);
        g.Payroll.Band = Context.FiscalLaw.Faixa(g.Payroll.Share);
        string text = Fill(Content.Personnel.Texto("corte"), ("prefeito", MayorName), ("folha", Pct(g.Payroll.Share)));
        News("folha", text);
        return new GovResult(null, text, [ChPayroll(before, g.Payroll.Share), ChApproval(PersonnelRules.CorteAprovacao)], []);
    }

    /// <summary>
    /// Previsão de mudar o repasse à Câmara: a relação com cada vereador (com o corte de cada um), a lembrança de quem
    /// preside a Câmara quando o repasse cai e o peso nas contas quando sobe.
    /// </summary>
    public GovForecast ForecastChamberTransfer(double share)
    {
        if (Role != GovRole.Prefeito)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        GovernanceState g = G;
        FiscalLawConfig law = Context.FiscalLaw;
        if (share < 0 || share > ChamberCap + 1e-9)
        {
            return GovForecast.Block(GovFailure.LimiteLegal, Fill(Content.Personnel.Texto("repasse_acima_do_teto"), ("teto", Pct(ChamberCap)), ("artigo", law.ArtigoCamara)));
        }

        double before = g.Fiscal.ChamberShare;
        var lines = new List<ForecastLine>();
        var explanation = new List<string> { Fill(Content.Personnel.Texto("previsao_repasse"), ("antes", Pct(before)), ("depois", Pct(share)), ("teto", Pct(ChamberCap)), ("artigo", law.ArtigoCamara)) };
        var remembered = new List<string>();
        bool cut = share < before - 1e-9, raise = share > before + 1e-9;
        if (cut || raise)
        {
            double step = cut ? PersonnelRules.RelacaoCamaraPorCorteDeRepasse : -PersonnelRules.RelacaoCamaraPorCorteDeRepasse;
            ChamberMember? president = cut && g.Mesa.PresidentId is { } presidentId && Member(presidentId) is { IsPlayer: false } p ? p : null;
            List<double> deltas = Members.Where(m => !m.IsPlayer && m != president).Select(m => Math.Clamp(m.Relation + step, -100, 100) - m.Relation).ToList();
            if (deltas.Count > 0)
            {
                lines.Add(new ForecastLine(GovForecast.RelacaoCamara, null, deltas.Min(), deltas.Max(), deltas.Average()));
            }

            if (president is not null)
            {
                double shifted = Math.Clamp(president.Relation + step, -100, 100);
                double total = Math.Clamp(shifted + Content.Personnel.Lembrancas["repasse"].Pontos, -100, 100) - president.Relation;
                lines.Add(new ForecastLine(GovForecast.Relacao, president.Id, total, total, total));
                remembered.Add(NameOf(president.Id));
            }

            if (raise && Exec is { } e && e.Indicators.TryGetValue("contas", out double accounts))
            {
                BoundsConfig bounds = Context.Executive.Config.IndicatorBounds;
                double delta = Math.Clamp(accounts - ((share - before) * g.Fiscal.TaxBaseShare * Context.Executive.Config.UnusedReserveToAccounts), bounds.Min, bounds.Max) - accounts;
                lines.Add(new ForecastLine(GovForecast.Indicador, "contas", delta, delta, delta));
            }

            explanation.Add(Content.Personnel.Texto(cut ? "previsao_repasse_corte" : "previsao_repasse_aumento"));
        }

        return new GovForecast(null, null, lines, [], explanation) { Remembered = remembered.Where(n => n.Length > 0).ToList() };
    }

    /// <summary>Define o repasse à Câmara, em fração da base de impostos. Acima do teto do art. 29-A, não passa.</summary>
    public GovResult SetChamberTransfer(double share)
    {
        GovForecast forecast = ForecastChamberTransfer(share);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        GovernanceState g = G;
        FiscalLawConfig law = Context.FiscalLaw;
        double before = g.Fiscal.ChamberShare;
        g.Fiscal.ChamberShare = share;
        var changes = new List<string> { T("mud_repasse", ("antes", Pct(before)), ("depois", Pct(share)), ("teto", Pct(ChamberCap))) };
        if (share < before - 1e-9)
        {
            foreach (ChamberMember m in Members)
            {
                m.Relation = Math.Clamp(m.Relation + PersonnelRules.RelacaoCamaraPorCorteDeRepasse, -100, 100);
            }

            Remember(g.Mesa.PresidentId, PersonRole.Vereador, "repasse_cortado", Content.Personnel.Lembrancas["repasse"]);
            changes.Add(T("mud_relacao_bancada", ("valor", Signed(PersonnelRules.RelacaoCamaraPorCorteDeRepasse, "0"))));
        }
        else if (share > before + 1e-9)
        {
            foreach (ChamberMember m in Members)
            {
                m.Relation = Math.Clamp(m.Relation - PersonnelRules.RelacaoCamaraPorCorteDeRepasse, -100, 100);
            }

            ShiftAccounts(-(share - before) * g.Fiscal.TaxBaseShare);
            changes.Add(T("mud_relacao_bancada", ("valor", Signed(-PersonnelRules.RelacaoCamaraPorCorteDeRepasse, "0"))));
        }

        string text = Fill(Content.Personnel.Texto("repasse_camara"), ("repasse", Pct(share)), ("teto", Pct(ChamberCap)), ("artigo", law.ArtigoCamara));
        News("camara", text);
        return new GovResult(null, text, changes, []);
    }

    // ================================================================ convênios

    private AgreementRules AgreementRules => Content.Agreements.Regras;

    private void AgreementsWeek(bool automatic)
    {
        GovernanceState g = G;
        bool npc = Role == GovRole.Vereador || automatic;
        foreach (Agreement a in g.Agreements)
        {
            switch (a.State)
            {
                case AgreementState.Oferta when npc && InvestmentLeft > 0 && !Context.ElectionConduct.NosTresMeses(Week):
                    SignAgreementCore(a, PickZone(Content.Works.Type(a.TypeId)));
                    break;
                case AgreementState.Oferta when a.OfferExpires < Week:
                    a.State = AgreementState.Expirado;
                    News("convenio", Fill(Content.Agreements.Texto("expirou"), ("obra", Content.Works.Type(a.TypeId).Nome), ("origem", OriginName(a))));
                    break;
                case AgreementState.Assinado when g.Work(a.WorkId ?? "") is { Stage: WorkStage.Entregue }:
                    a.State = AgreementState.Cumprido;
                    g.GovernorRelation = Math.Clamp(g.GovernorRelation + AgreementRules.RelacaoPorConvenioCumprido, -100, 100);
                    News("convenio", Fill(Content.Agreements.Texto("cumprido"), ("obra", Content.Works.Type(a.TypeId).Nome), ("origem", OriginName(a))));
                    break;
                case AgreementState.Assinado when a.Deadline < Week:
                    // Convênio não executado devolve o dinheiro e bloqueia novos.
                    a.State = AgreementState.Devolvido;
                    g.AgreementsBlockedUntil = Week.AddDays(7 * AgreementRules.BloqueioPorDevolucao);
                    g.GovernorRelation = Math.Clamp(g.GovernorRelation + AgreementRules.RelacaoPorDevolucao, -100, 100);
                    g.Fiscal.InvestmentUsed += a.Value;
                    ShiftApproval(AgreementRules.ImagemDevolucao);
                    News("convenio", Fill(Content.Agreements.Texto("devolvido"), ("obra", Content.Works.Type(a.TypeId).Nome), ("origem", OriginName(a)), ("valor", Texts.Money(a.Value)),
                        ("semanas", AgreementRules.BloqueioPorDevolucao.ToString(CultureInfo.InvariantCulture))));
                    break;
            }
        }

        if (g.Agreements.Count(a => a.State is AgreementState.Oferta or AgreementState.Assinado) >= AgreementRules.MaximoAbertos)
        {
            return;
        }

        double chance = AgreementRules.ChanceOfertaSemanal + (AgreementRules.OfertaPorRelacao * g.GovernorRelation / 100);
        if (!Rng.Chance(Math.Clamp(chance, 0, 1)))
        {
            return;
        }

        if (Context.ElectionConduct.NosTresMeses(Week))
        {
            // Nos três meses antes do pleito não há transferência voluntária (Lei 9.504/1997, art. 73, VI).
            News("convenio", Fill(Content.Agreements.Texto("bloqueado_eleicao"), ("artigo", Context.ElectionConduct.ArtigoTransferencias)));
            return;
        }

        if (g.Payroll.Restricted)
        {
            // Sem transferências voluntárias enquanto durar o excesso da folha (LC 101/2000, art. 23, § 3º).
            News("convenio", Fill(Content.Agreements.Texto("bloqueado_lrf"), ("artigo", Context.FiscalLaw.ArtigoExcesso)));
            return;
        }

        if (g.AgreementsBlockedUntil is { } until && until > Week)
        {
            News("convenio", Fill(Content.Agreements.Texto("bloqueado_devolucao"), ("data", Texts.Date(until))));
            return;
        }

        WorkType type = Rng.Pick(Content.Works.Tipos.Where(t => !t.Servico && !t.Pequena).ToList());
        double counterpart = Rng.NextDouble(AgreementRules.Contrapartida.Min, AgreementRules.Contrapartida.Max);
        decimal value = Math.Round(CostOf(type) * (decimal)(1 - counterpart), 2);
        int normal = type.ProjetoSemanas.Max + Content.Procurement.Option(ProcurementMode.Concorrencia).Semanas.Max + type.ExecucaoSemanas.Max;
        var offer = new Agreement
        {
            Id = g.NewId("conv"),
            OriginId = Rng.PickWeighted(Content.Agreements.Origens, o => o.Weight).Id,
            TypeId = type.Id,
            Value = value,
            Counterpart = counterpart,
            Offered = Week,
            OfferExpires = Week.AddDays(7 * AgreementRules.OfertaValeSemanas),
            Weeks = normal + Rng.NextInt(AgreementRules.FolgaSemanas.Min, AgreementRules.FolgaSemanas.Max + 1),
        };
        g.Agreements.Add(offer);
        News("convenio", Fill(Content.Agreements.Texto("oferta"), ("origem", OriginName(offer)), ("obra", type.Nome), ("valor", Texts.Money(value)), ("contrapartida", Texts.Percent(offer.Counterpart)),
            ("semanas", offer.Weeks.ToString(CultureInfo.InvariantCulture))));
    }

    private string OriginName(Agreement a) => Content.Agreements.Origens.FirstOrDefault(o => o.Id == a.OriginId)?.Nome ?? a.OriginId;

    private string PickZone(WorkType type)
    {
        GovernanceState g = G;
        List<int> zones = Enumerable.Range(0, g.Zones.Count).Where(i => Fits(type, g.Zones[i])).ToList();
        return zones.Count == 0 ? g.Zones[0] : g.Zones[Rng.PickWeighted(zones, i => Math.Max(1.0, g.ZonePopulation[i]))];
    }

    /// <summary>O que o município paga da obra do convênio: o custo menos o que o convênio cobre.</summary>
    private decimal CounterpartOf(Agreement a) => Math.Max(0, CostOf(Content.Works.Type(a.TypeId)) - a.Value);

    public GovForecast ForecastSignAgreement(string agreementId, string zoneId)
    {
        Agreement? a = G.Agreements.FirstOrDefault(x => x.Id == agreementId && x.State == AgreementState.Oferta);
        if (Role != GovRole.Prefeito)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        if (a is null)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        WorkType type = Content.Works.Type(a.TypeId);
        if (!Fits(type, zoneId))
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, "");
        }

        if (Context.ElectionConduct.NosTresMeses(Week))
        {
            return GovForecast.Block(GovFailure.PeriodoVedado, Fill(Content.Agreements.Texto("bloqueado_eleicao"), ("artigo", Context.ElectionConduct.ArtigoTransferencias)));
        }

        decimal own = CounterpartOf(a);
        if (own > InvestmentLeft)
        {
            return GovForecast.Block(GovFailure.SemInvestimento, Fill(Content.Works.Texto("sem_investimento"), ("custo", Texts.Money(own)), ("resta", Texts.Money(Math.Max(0, InvestmentLeft)))));
        }

        int normalMin = (int)Math.Ceiling(type.ProjetoSemanas.Min * CompetenceDelay(type)) + Content.Procurement.Option(ProcurementMode.Concorrencia).Semanas.Min + type.ExecucaoSemanas.Min;
        int normalMax = (int)Math.Ceiling(type.ProjetoSemanas.Max * CompetenceDelay(type)) + Content.Procurement.Option(ProcurementMode.Concorrencia).Semanas.Max + type.ExecucaoSemanas.Max;
        double approval = type.Prestigio * WorkRules.AprovacaoPorPrestigio;
        return new GovForecast(null, null,
        [
            new ForecastLine(GovForecast.Investimento, null, -(double)own, -(double)own, -(double)own),
            new ForecastLine(GovForecast.AprovacaoZona, zoneId, approval * WorkRules.PrestigioSemCerimonia, approval, approval),
        ], [],
        [
            Fill(Content.Agreements.Texto("previsao_assinar"), ("origem", OriginName(a)), ("obra", type.Nome), ("zona", ZoneName(zoneId)), ("valor", Texts.Money(a.Value))),
            Fill(Content.Agreements.Texto("previsao_contrapartida"), ("contrapartida", Texts.Money(own)), ("resta", Texts.Money(InvestmentLeft - own))),
            Fill(Content.Agreements.Texto("previsao_prazo"), ("semanas", a.Weeks.ToString(CultureInfo.InvariantCulture)), ("min", normalMin.ToString(CultureInfo.InvariantCulture)), ("max", normalMax.ToString(CultureInfo.InvariantCulture))),
            Fill(Content.Agreements.Texto("previsao_risco"), ("bloqueio", AgreementRules.BloqueioPorDevolucao.ToString(CultureInfo.InvariantCulture))),
        ]);
    }

    /// <summary>Assina o convênio: a obra entra no mapa paga em parte por ele, com contrapartida e prazo.</summary>
    public GovResult SignAgreement(string agreementId, string zoneId)
    {
        GovForecast forecast = ForecastSignAgreement(agreementId, zoneId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Agreement a = G.Agreements.First(x => x.Id == agreementId);
        Work work = SignAgreementCore(a, zoneId);
        var changes = new List<string> { ChInvestment(-CounterpartOf(a)), T("mud_convenio_prazo", ("data", Texts.Date(a.Deadline!.Value))) };
        changes.AddRange(CheckZoneWorkPromises(work));
        return new GovResult(null, Fill(Content.Agreements.Texto("assinado"), ("origem", OriginName(a)), ("obra", WorkName(work)), ("zona", ZoneName(zoneId))), changes, []) { CreatedId = work.Id };
    }

    private Work SignAgreementCore(Agreement a, string zoneId)
    {
        GovernanceState g = G;
        WorkType type = Content.Works.Type(a.TypeId);
        g.Fiscal.InvestmentUsed += CounterpartOf(a);
        Work work = CreateWork(type, zoneId, Work.DeConvenio, Work.Prefeito, WorkStage.Projeto);
        work.AgreementId = a.Id;
        a.State = AgreementState.Assinado;
        a.WorkId = work.Id;
        a.Deadline = Week.AddDays(7 * a.Weeks);
        News("convenio", Fill(Content.Agreements.Texto("assinado"), ("origem", OriginName(a)), ("obra", type.Nome), ("zona", ZoneName(zoneId))), zoneId);
        return work;
    }

    /// <summary>Previsão de recusar o convênio: o dinheiro não vem e a contrapartida fica livre; ninguém guarda lembrança.</summary>
    public GovForecast ForecastDeclineAgreement(string agreementId)
    {
        if (Role != GovRole.Prefeito)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        if (G.Agreements.FirstOrDefault(x => x.Id == agreementId && x.State == AgreementState.Oferta) is not { } a)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        return new GovForecast(null, null, [], [],
            [Fill(Content.Agreements.Texto("previsao_recusar"), ("origem", OriginName(a)), ("obra", Content.Works.Type(a.TypeId).Nome), ("contrapartida", Texts.Money(CounterpartOf(a))))]);
    }

    public GovResult DeclineAgreement(string agreementId)
    {
        GovForecast forecast = ForecastDeclineAgreement(agreementId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Agreement a = G.Agreements.First(x => x.Id == agreementId && x.State == AgreementState.Oferta);
        a.State = AgreementState.Recusado;
        string text = Fill(Content.Agreements.Texto("recusado"), ("origem", OriginName(a)), ("obra", Content.Works.Type(a.TypeId).Nome));
        News("convenio", text);
        return new GovResult(null, text, [], []);
    }

    // ================================================================ Câmara que negocia (prefeito)

    /// <summary>Contagem nominal de uma política que precisa de lei, como a Câmara votaria hoje.</summary>
    public NominalCount? CountPolicy(string policyId)
    {
        if (Role != GovRole.Prefeito || Exec is not { } e || Context.Executive.Policies.Find(policyId) is not { RequiresLaw: true } policy)
        {
            return null;
        }

        LegislatureConfig config = Context.Legislature.Config;
        var lines = new List<CountLine>();
        foreach (ChamberMember m in e.Chamber)
        {
            int side = e.InBase(m.PartyId) ? 1 : -1;
            int orientation = m.PartyId == e.PartyId ? 1 : ChamberVote.PartyOrientation(config, State, m.PartyId, policy.Position, side);
            lines.Add(Line(m, policy.Id, policy.Position, orientation, side, m.Relation, GovernanceContent.PapelPrefeito));
        }

        return new NominalCount(policy.Id, policy.Name, policy.Quorum, lines.Count(l => l.Lean == NominalCount.Sim), lines.Count(l => l.Lean == NominalCount.Nao), lines.Count(l => l.Lean == NominalCount.Indeciso),
            e.Chamber.Count, lines);
    }

    public GovForecast ForecastPolicyDeal(string policyId, string memberId) =>
        ForecastDeal(CountPolicy(policyId), policyId, memberId, 1, GovernanceContent.PapelPrefeito);

    /// <summary>O prefeito atende o pedido de um vereador indeciso em troca do voto numa política. Atendido, ele lembra; enganado, também.</summary>
    public GovResult NegotiatePolicyVote(string policyId, string memberId) =>
        CloseDeal(ForecastPolicyDeal(policyId, memberId), policyId, memberId, 1, GovernanceContent.PapelPrefeito);

    // ================================================================ crises com causa

    private CrisisRecord RaiseCrisis(CrisisDefinition crisis, string? zoneId, string causeKind, string causeRef, string causeText, double severity)
    {
        GovernanceState g = G;
        var record = new CrisisRecord { Id = g.NewId("crise"), CrisisId = crisis.Id, Date = Week, ZoneId = zoneId, CauseKind = causeKind, CauseRef = causeRef, CauseText = causeText, Severity = severity };
        g.Crises.Add(record);
        return record;
    }

    /// <summary>A denúncia de uma contratação irregular é a causa de um escândalo no governo.</summary>
    private void RaiseCrisisFromWork(Work work)
    {
        CrisisDefinition? crisis = Context.Executive.Crises.Crises.FirstOrDefault(c => c.Cause?.Kind == CrisisRecord.CausaSecretario);
        if (crisis is null || G.Crises.Any(c => c.CauseKind == CrisisRecord.CausaLicitacao && c.CauseRef == work.Id))
        {
            return;
        }

        RaiseCrisis(crisis, work.ZoneId, CrisisRecord.CausaLicitacao, work.Id,
            Fill(Content.Secretaries.Texto("escandalo_causa"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("empresa", CompanyName(work))), 1);
    }

    private double? IndicatorValue(string id) => IndicatorOf(id);

    private void CrisesWeek(bool automatic)
    {
        GovernanceState g = G;
        bool raised = false;
        foreach (CrisisDefinition crisis in Context.Executive.Crises.Crises)
        {
            if (raised || crisis.Cause is not { } cause || cause.Kind == CrisisRecord.CausaNegociacao)
            {
                continue;
            }

            if (crisis.Months.Count > 0 && !crisis.Months.Contains(Week.Month))
            {
                continue;
            }

            if (g.Crises.Any(c => c.CrisisId == crisis.Id && Week.DayNumber - c.Date.DayNumber < 7 * cause.CooldownWeeks))
            {
                continue;
            }

            switch (cause.Kind)
            {
                case CrisisRecord.CausaZonaSemDrenagem:
                    // A chuva vira enchente onde não há drenagem entregue; onde há, a obra segura.
                    // Só entram as zonas em que a obra de proteção caberia (a enchente é a falta dela).
                    List<int> exposed = Enumerable.Range(0, g.Zones.Count)
                        .Where(z => Content.Works.Tipos.Any(t => t.ProtegeDeEnchente && Fits(t, g.Zones[z])))
                        .Where(z => !g.Works.Any(w => w.Stage == WorkStage.Entregue && w.ZoneId == g.Zones[z] && Content.Works.Find(w.TypeId)?.ProtegeDeEnchente == true))
                        .ToList();
                    if (exposed.Count > 0 && Rng.Chance(cause.Chance))
                    {
                        int zone = Rng.PickWeighted(exposed, z => Math.Max(1.0, g.ZonePopulation[z]) * (Zone(g.Zones[z])?.Situacao == Zona.Urbana ? 2 : 1));
                        RaiseCrisis(crisis, g.Zones[zone], cause.Kind, g.Zones[zone], Fill(cause.Text, ("zona", ZoneName(g.Zones[zone]))), 1);
                        raised = true;
                    }

                    break;
                case CrisisRecord.CausaSecretario:
                    Secretary? weak = g.Secretaries.Where(s => s.SecretariaId is not null && !s.Left && s.Integrity <= SecretaryRules.IntegridadeDeRisco).OrderBy(s => s.Integrity).ThenBy(s => s.PersonId, StringComparer.Ordinal).FirstOrDefault();
                    if (weak is not null && Rng.Chance(cause.Chance))
                    {
                        RaiseCrisis(crisis, null, cause.Kind, weak.PersonId, Fill(cause.Text, ("pessoa", NameOf(weak.PersonId)), ("secretaria", Content.Secretaries.Secretaria(weak.SecretariaId!).Nome)), 1);
                        raised = true;
                    }

                    break;
                case CrisisRecord.CausaIndicador:
                    if (cause.Indicator is { } indicator && IndicatorValue(indicator) is { } value && value < cause.Below && Rng.Chance(cause.Chance))
                    {
                        string name = Context.Executive.Config.Indicators.FirstOrDefault(i => i.Id == indicator)?.Name ?? indicator;
                        RaiseCrisis(crisis, null, cause.Kind, indicator, Fill(cause.Text, ("indicador", name), ("valor", Num(value))), 1);
                        raised = true;
                    }

                    break;
                case CrisisRecord.CausaReceita:
                    if (g.Fiscal.Growth < 0 && !g.Crises.Any(c => c.CrisisId == crisis.Id && c.Date.Year == Week.Year) && Rng.Chance(cause.Chance))
                    {
                        RaiseCrisis(crisis, null, cause.Kind, g.Fiscal.Year.ToString(CultureInfo.InvariantCulture), Fill(cause.Text, ("queda", Texts.Percent(-g.Fiscal.Growth))), 1);
                        raised = true;
                    }

                    break;
            }
        }

        // Uma crise por vez chega à mesa do prefeito; as outras esperam.
        bool npc = Role == GovRole.Vereador || automatic;
        foreach (CrisisRecord record in g.Crises.Where(c => !c.Resolved).ToList())
        {
            CrisisDefinition crisis = Context.Executive.Crises.Find(record.CrisisId)!;
            if (!record.Presented)
            {
                if (!npc && Exec is { Crisis: not null })
                {
                    continue;
                }

                record.Presented = true;
                int zone = record.ZoneId is null ? -1 : g.IndexOf(record.ZoneId);
                ShiftZoneApproval(zone, -SecretaryRules.CriseZonaDano * record.Severity);

                // Com os indicadores reais, o choque fica neles (e a nota da prefeitura é relida deles na semana seguinte).
                ShockIndicators(record, crisis);
                if (Exec is { } e)
                {
                    BoundsConfig bounds = Context.Executive.Config.IndicatorBounds;
                    foreach (var (indicator, delta) in crisis.Shock.Where(s => e.Indicators.ContainsKey(s.Key)))
                    {
                        e.Indicators[indicator] = Math.Clamp(e.Indicators[indicator] + (delta * record.Severity), bounds.Min, bounds.Max);
                    }

                    e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.Crise, crisis.Name));
                    if (!npc)
                    {
                        e.Crisis = new PendingCrisis(crisis.Id, Week);
                    }
                }

                News("crise", T("noticia_crise", ("crise", crisis.Name), ("texto", crisis.Text), ("causa", record.CauseText)), record.ZoneId, record.CauseKind == CrisisRecord.CausaSecretario ? record.CauseRef : null);
            }

            if (npc)
            {
                // O prefeito NPC (ou o mandato em segundo plano) responde sozinho: quase sempre a melhor saída.
                int choice = Rng.Chance(SecretaryRules.NpcBoaEscolha)
                    ? Enumerable.Range(0, crisis.Choices.Count).MaxBy(i => crisis.Choices[i].Image)
                    : Rng.NextInt(crisis.Choices.Count);
                record.Choice = choice;
                record.Resolved = true;
                CrisisChoice chosen = crisis.Choices[choice];
                if (Exec is { } e)
                {
                    e.Crisis = null;
                    e.Image = Math.Clamp(e.Image + chosen.Image, -1, 1);
                    e.News.Add(new ExecutiveNews(Week, ExecutiveNewsKind.CriseResolvida, crisis.Name));
                }
                else
                {
                    g.MayorApproval = Math.Clamp(g.MayorApproval - (SecretaryRules.CriseNpcDano * record.Severity) + (chosen.Image * Context.Executive.Config.Approval.ImageWeight), 0, 100);
                }

                News("crise", T("noticia_crise_resposta", ("crise", crisis.Name), ("resposta", chosen.Label), ("resultado", chosen.Result)), record.ZoneId);
                if (record.CauseKind == CrisisRecord.CausaZonaSemDrenagem && record.ZoneId is { } flooded && Rng.Chance(WorkRules.NpcChanceObraAposCrise)
                    && !g.Works.Any(w => w.Open && w.ZoneId == flooded && Content.Works.Find(w.TypeId)?.ProtegeDeEnchente == true)
                    && Content.Works.Tipos.FirstOrDefault(t => t.ProtegeDeEnchente && Fits(t, flooded)) is { } protection)
                {
                    // A prefeitura responde à causa: abre a obra que faltava na zona atingida.
                    Work work = CreateWork(protection, flooded, Work.DaPrefeitura, Work.Prefeito, WorkStage.Projeto);
                    News("obra", Fill(Content.Works.Texto("anunciada"), ("obra", WorkName(work)), ("zona", ZoneName(flooded)), ("prefeito", MayorName)), flooded);
                }
            }
        }
    }
}
