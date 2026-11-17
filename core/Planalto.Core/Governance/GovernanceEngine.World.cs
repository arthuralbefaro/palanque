using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Executive;
using Planalto.Core.Indicators;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.Renewal;
using Planalto.Core.State;

namespace Planalto.Core.Governance;

/// <summary>
/// Ligação do governo da cidade com o mundo que anda sozinho: os indicadores em unidade real (GDD 17.17) e a renovação
/// do mundo político (GDD 17.16). Com <see cref="GovernanceContext.IndicatorEvolution"/>, obra entregue, serviço em
/// vigor e crise empurram o indicador real, e as crises "com causa" leem o indicador real também com prefeito NPC. Com
/// <see cref="GovernanceContext.Renewal"/>, a cassação segue a regra de sucessão, e a morte ou a saída de vereador,
/// secretário, assessor ou liderança chega às cadeiras, à Mesa, às pastas, ao gabinete e ao Livro.
/// </summary>
public sealed partial class GovernanceEngine
{
    /// <summary>Origem das pressões de obra e serviço entregues ("obra:{id da obra}").</summary>
    public const string WorkOrigin = "obra:";

    /// <summary>Origem dos choques de crise do governo ("crise:{id do registro}").</summary>
    public const string CrisisOrigin = "crise:";

    private const string VoteDealPrefix = "voto_";

    /// <summary>Indicadores reais do município, quando o contexto traz o modelo de evolução e o save os tem.</summary>
    private MunicipalIndicatorsState? Real =>
        Context.IndicatorEvolution is not null && State.Indicators is { } real && real.Municipio == G.Municipio ? real : null;

    /// <summary>True quando obras, crises e causas de crise passam pelos indicadores em unidade real.</summary>
    public bool UsesRealIndicators => Real is not null;

    /// <summary>Renovação do mundo no município, quando o contexto traz o motor e o save tem o estado.</summary>
    private RenewalState? Renewed => Context.Renewal is not null && State.Renewal is { } r && r.Municipio == G.Municipio ? r : null;

    /// <summary>True quando sucessão e saídas seguem a renovação do mundo.</summary>
    public bool UsesRenewal => Renewed is not null;

    // ================================================================ indicadores

    /// <summary>
    /// Indicador do município na escala de jogo (0 a 100): o da prefeitura do jogador; senão o do gancho do contexto;
    /// senão a nota derivada dos indicadores reais (variação no mandato e comparação com os semelhantes), que é a mesma
    /// régua com prefeito NPC.
    /// </summary>
    public double? IndicatorOf(string id)
    {
        if (Exec is { } e && e.Indicators.TryGetValue(id, out double own))
        {
            return own;
        }

        if (Context.Indicator?.Invoke(id) is { } hooked)
        {
            return hooked;
        }

        if (Real is { } real && Context.IndicatorEvolution is { } evolution
            && IndicatorQueries.GameScore(real, evolution.Content.Definitions, evolution.Content.Model, id) is { } score)
        {
            BoundsConfig bounds = Context.Executive.Config.IndicatorBounds;
            return Math.Clamp(score, bounds.Min, bounds.Max);
        }

        return null;
    }

    /// <summary>
    /// Crescimento nominal da receita no ano que começa. Com os indicadores reais, o centro da faixa de
    /// data/executive/personnel.json é o crescimento de referência e o desvio do ano é o que a receita simulada (receita
    /// corrente por habitante vezes a população) variou no ano que fechou, sem sair da faixa. Sem eles, é sorteado na faixa.
    /// </summary>
    private double RevenueGrowthOf(int year)
    {
        Range range = Content.Personnel.Regras.RclCrescimentoAnual;
        if (Context.RevenueGrowth is { } hook)
        {
            return hook(year);
        }

        if (Real?.Find(IndicatorDefinitions.ReceitaPerCapita) is { VariacaoAnoAnterior: { Antes: > 0 } last })
        {
            double perCapita = last.Depois / last.Antes;
            double population = 1;
            if (Context.IndicatorEvolution?.Projection is { } projection && projection.Population(Context.Municipio, year - 1) is > 0 and var before)
            {
                population = projection.Population(Context.Municipio, year) / (double)before;
            }

            return Math.Clamp(((range.Min + range.Max) / 2) + ((perCapita * population) - 1), range.Min, range.Max);
        }

        return Rng.NextDouble(range.Min, range.Max);
    }

    /// <summary>
    /// Obra entregue ou serviço em vigor empurra o indicador real da área pelo prazo declarado em data/works/types.json,
    /// com origem e texto para a explicação da variação. Devolve false quando não há indicadores reais ou o indicador
    /// da prefeitura não tem mapa para eles.
    /// </summary>
    private bool PushIndicator(Work work, WorkType type, bool staffed)
    {
        if (type.Indicador is not { } indicator || Real is not { } real || Context.IndicatorEvolution is not { } evolution || !evolution.Components(indicator).Any())
        {
            return false;
        }

        int weeks = type.Servico ? WorkRules.IndicadorSemanasServico : WorkRules.IndicadorSemanasObra;
        evolution.ApplyGamePressure(real, indicator, type.IndicadorPontos * (staffed ? 1 : 0.5), weeks, Week, WorkOrigin + work.Id, T("ind_obra", ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId))));
        return true;
    }

    /// <summary>O choque de uma crise do governo nos indicadores reais (com prefeito jogador ou NPC).</summary>
    private void ShockIndicators(CrisisRecord record, CrisisDefinition crisis)
    {
        if (Real is not { } real || Context.IndicatorEvolution is not { } evolution)
        {
            return;
        }

        foreach (var (indicator, delta) in crisis.Shock.Where(s => evolution.Components(s.Key).Any()))
        {
            evolution.ApplyGameShock(real, indicator, delta * record.Severity, Week, CrisisOrigin + record.Id, crisis.Name);
        }
    }

    /// <summary>A competência do secretário da pasta acelera ou atrasa os indicadores reais da área (GDD 17.15).</summary>
    private void PushSpeedFactors()
    {
        if (Real is not { } real || Context.IndicatorEvolution is not { } evolution)
        {
            return;
        }

        foreach (IndicatorDefinition indicator in Context.Executive.Config.Indicators)
        {
            evolution.SetGameSpeedFactor(real, indicator.Id, G.AreaSpeed.GetValueOrDefault(indicator.Id, 1));
        }
    }

    // ================================================================ vacância

    /// <summary>
    /// Entrega a vaga a quem cuida de sucessão: o gancho do contexto, se houver; senão a renovação do mundo, que aplica
    /// a regra de <see cref="SuccessionRulesConfig"/> (vice, suplente ou nova eleição). Devolve false quando ninguém
    /// resolveu e vale o padrão provisório do governo.
    /// </summary>
    public bool ResolveVacancy(OfficeVacancy vacancy)
    {
        if (Context.OnVacancy is { } hook)
        {
            return hook(vacancy);
        }

        if (Context.Renewal is not { } renewal || Renewed is not { } r)
        {
            return false;
        }

        RenewalEvent? fact = vacancy.Office switch
        {
            Office.Prefeito when r.PrefeitoId == vacancy.HolderId => renewal.VacateExecutive(VacancyCause.Cassacao, vacancy.Date),
            Office.Vereador when r.SeatOf(vacancy.HolderId) is not null => renewal.VacateSeat(vacancy.HolderId, VacancyCause.Cassacao, vacancy.Date),
            _ => null,
        };
        if (fact is null)
        {
            return false;
        }

        vacancy.Outcome = fact.Desfecho;
        vacancy.SuccessorId = fact.SucessorId;
        vacancy.Rule = fact.Regra;
        vacancy.ElectionOn = fact.EleicaoEm;
        return true;
    }

    /// <summary>
    /// Depois de a renovação resolver a cassação do prefeito NPC: a relação do mandato recomeça com quem assumiu (o
    /// vice que deve o cargo à CPI do jogador lembra disso) e as notícias da sucessão chegam ao feed.
    /// </summary>
    private void AfterMayorVacancy(OfficeVacancy vacancy, bool byPlayer)
    {
        GovernanceState g = G;
        if (Mandate is { } mandate)
        {
            bool vice = vacancy.Outcome == SuccessionOutcome.ViceAssumiu && vacancy.SuccessorId is not null;
            mandate.MayorRelation = byPlayer && vice ? Content.Chamber.Lembrancas["vice_grato"].Pontos : 0;
            if (byPlayer && vice)
            {
                Remember(vacancy.SuccessorId, PersonRole.Dirigente, "vice_grato", Content.Chamber.Lembrancas["vice_grato"]);
            }

            g.MayorSeenId = mandate.MayorId;
        }

        RelayWorldNews();
    }

    // ================================================================ o que a renovação muda no governo

    /// <summary>Morreu ou deixou a cidade: não há mais a quem cumprir, cobrar ou nomear.</summary>
    private static bool Gone(RenewalState r, string personId) => r.Status(personId)?.Saida is ExitKind.Morte or ExitKind.Mudanca;

    /// <summary>O suplente que tomou posse na cadeira de quem saiu, se já está na Câmara.</summary>
    private string? SeatSuccessor(RenewalState r, string goneId)
    {
        string? successor = r.Historico.LastOrDefault(e => e.Tipo == RenewalEventKind.PosseSuplente && e.PessoaId == goneId)?.SucessorId;
        return successor is not null && Member(successor) is not null ? successor : null;
    }

    /// <summary>
    /// Traz para o governo o que a renovação do mundo mudou desde a última semana. Regra (GDD 17.16, "dívidas e
    /// promessas feitas a quem saiu passam ao sucessor ou se extinguem"):
    /// vereador que deixa a cadeira (morte, renúncia, cassação, investidura) — o suplente herda o reduto e os acordos de
    /// voto, com a promessa do Livro (ele vota como combinado e é ele quem cobra); sem suplente, o acordo e a promessa
    /// se extinguem, com registro. Presidente da Câmara que sai — assume quem a renovação aponta, pelo resto do mandato
    /// da Mesa. Prefeito NPC que sai — a relação do mandato com o governo recomeça do zero. Secretário ou assessor que
    /// morre ou vai embora — a pasta ou a vaga fica aberta. Promessa ou dívida com quem morreu ou foi embora, sem
    /// sucessor no momento da saída — extingue-se, com registro (não conta como cumprida nem como quebrada).
    /// Chamar duas vezes na mesma semana não muda nada.
    /// </summary>
    public void SyncWithWorld()
    {
        if (Renewed is not { } r || State.Governance is not { PlayerRemoved: false } g)
        {
            return;
        }

        // 1. Câmara: reduto e acordos de quem deixou a cadeira.
        if (Members.Count > 0)
        {
            foreach (string gone in g.MemberZones.Keys.Where(id => Member(id) is null).Order(StringComparer.Ordinal).ToList())
            {
                string? successor = SeatSuccessor(r, gone);
                string zone = g.MemberZones[gone];
                g.MemberZones.Remove(gone);
                g.NoDealUntil.Remove(gone);
                if (successor is not null && !g.MemberZones.ContainsKey(successor))
                {
                    g.MemberZones[successor] = zone;
                    PersonOf(successor, PersonRole.Vereador);
                }

                foreach (VoteDeal deal in g.Deals.Where(d => d.MemberId == gone && !d.Extinct))
                {
                    if (State.Ledger.Promises.FirstOrDefault(p => p.Id == deal.PromiseId) is not { State: PromiseState.Aberta } promise)
                    {
                        // Promessa já acertada: só o voto ainda por dar muda de dono ou se perde.
                        if (successor is not null)
                        {
                            (deal.OriginalMemberId, deal.MemberId) = (deal.OriginalMemberId ?? gone, successor);
                        }
                        else
                        {
                            deal.Extinct = true;
                        }

                        continue;
                    }

                    if (successor is not null)
                    {
                        (deal.OriginalMemberId, deal.MemberId) = (deal.OriginalMemberId ?? gone, successor);
                        Inherit(promise, successor, "mundo_acordo_herdado");
                    }
                    else
                    {
                        deal.Extinct = true;
                        Extinguish(promise);
                    }
                }
            }

            if (Members.Any(m => !m.IsPlayer && !g.MemberZones.ContainsKey(m.Id)))
            {
                // Quem chegou por eleição da cadeira escolhe o próprio reduto.
                AssignRedutos();
            }

            // 2. Mesa: a presidência não fica com quem saiu da Câmara.
            if (g.Mesa.PresidentId is { } president && president != RaceSetup.PlayerId && Member(president) is null)
            {
                ChamberMember? next = (r.PresidenteCamaraId is { } pointed ? Member(pointed) : null)
                    ?? Members.Where(m => !m.IsPlayer).OrderByDescending(m => m.Votes).ThenBy(m => m.Id, StringComparer.Ordinal).FirstOrDefault();
                g.Mesa.PresidentId = next?.Id;
                if (Mandate is { } withMesa)
                {
                    withMesa.PresidentId = next?.Id;
                }

                if (next is not null)
                {
                    News("mesa", T("mundo_mesa", ("nome", next.Name)), null, next.Id);
                }
            }
        }

        // 3. Prefeito NPC: quem assumiu não herda a relação do antecessor com o mandato.
        if (Role == GovRole.Vereador && Mandate is { } mandate)
        {
            if (g.MayorSeenId is null)
            {
                g.MayorSeenId = mandate.MayorId;
            }
            else if (g.MayorSeenId != mandate.MayorId)
            {
                g.MayorSeenId = mandate.MayorId;
                mandate.MayorRelation = 0;
                News("prefeito", T("mundo_novo_prefeito", ("nome", mandate.MayorName)), null, mandate.MayorId);
            }
        }

        // 4. Secretários e assessores.
        bool offers = false;
        foreach (Secretary secretary in g.Secretaries.Where(s => !s.Left && Gone(r, s.PersonId)).ToList())
        {
            if (secretary.SecretariaId is { } pasta)
            {
                // A pasta fica vaga sem demissão: o partido não perde o lugar na base por isso.
                g.VacantSince[pasta] = Week;
                News("secretario", T("mundo_secretaria_vaga", ("secretaria", Content.Secretaries.Secretaria(pasta).Nome), ("nome", NameOf(secretary.PersonId))), null, secretary.PersonId);
            }
            else
            {
                offers = true;
            }

            secretary.Left = true;
            secretary.SecretariaId = null;
        }

        if (offers && Role == GovRole.Prefeito)
        {
            EnsureOffers();
        }

        foreach (Aide aide in g.Aides.Where(a => !a.Left && Gone(r, a.PersonId)).ToList())
        {
            aide.Left = true;
            if (aide.Appointed)
            {
                News("gabinete", T("mundo_assessor_saiu", ("nome", NameOf(aide.PersonId))), aide.ZoneId, aide.PersonId);
            }
        }

        // 5. Livro: promessas e dívidas com quem morreu ou foi embora.
        foreach (Promise promise in State.Ledger.Open.Where(p => p.ToKind == Promise.ParaPessoa && Gone(r, p.ToId)).ToList())
        {
            if (promise.Object.StartsWith(VoteDealPrefix, StringComparison.Ordinal) && SeatSuccessor(r, promise.ToId) is { } heir)
            {
                Inherit(promise, heir, "mundo_promessa_herdada");
            }
            else
            {
                Extinguish(promise);
            }
        }

        foreach (Debt debt in State.Ledger.Debts.Where(d => !d.Settled && Gone(r, d.ToPersonId)).ToList())
        {
            debt.Settled = true;
            debt.Extinct = true;
            News("cobranca", T("mundo_divida_extinta", ("nome", NameOf(debt.ToPersonId)), ("divida", debt.Text)), null, debt.ToPersonId);
        }

        RelayWorldNews();
        RefreshDerived();
    }

    private void Inherit(Promise promise, string successorId, string textKey)
    {
        string old = promise.ToId;
        promise.OriginalToId ??= old;
        promise.ToId = successorId;
        News("cobranca", T(textKey, ("nome", NameOf(successorId)), ("antecessor", NameOf(old)), ("promessa", promise.Text)), promise.ZoneId, successorId);
    }

    private void Extinguish(Promise promise)
    {
        promise.State = PromiseState.Extinta;
        News("cobranca", T("mundo_promessa_extinta", ("nome", NameOf(promise.ToId)), ("promessa", promise.Text)), promise.ZoneId, promise.ToId);
    }

    /// <summary>Os fatos da renovação (morte, posse de suplente, vice que assume, nova eleição) viram notícia no feed do governo.</summary>
    private void RelayWorldNews()
    {
        if (Renewed is not { } r || State.Governance is not { } g)
        {
            return;
        }

        foreach (RenewalEvent e in r.Historico.Where(e => e.Id > g.RenewalSeen && e.Data >= g.Start))
        {
            News("mundo", e.Texto, null, e.SucessorId ?? e.PessoaId);
        }

        g.RenewalSeen = Math.Max(g.RenewalSeen, r.NextEventId - 1);
    }
}
