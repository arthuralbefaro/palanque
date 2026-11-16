using Planalto.Core.Campaign;
using Planalto.Core.Executive;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Governance;

/// <summary>Obras e serviços no mapa: etapas, licitação, atraso, inauguração e entrega (GDD 17.14 e 17.15).</summary>
public sealed partial class GovernanceEngine
{
    private WorkRules WorkRules => Content.Works.Regras;

    /// <summary>Custo de um tipo de obra nesta cidade.</summary>
    public decimal CostOf(WorkType type) => Math.Round(type.Custo * (decimal)WorkRules.CustoPorPorte[Porte], 2);

    /// <summary>O tipo de obra pode ser feito na zona (urbana, rural)?</summary>
    public bool Fits(WorkType type, string zoneId) => Zone(zoneId) is { } zone && (type.Situacao == "qualquer" || type.Situacao == zone.Situacao);

    /// <summary>Quanto ainda cabe de obra nova neste ano (a fatia de "obras" do orçamento que pode virar investimento).</summary>
    public decimal InvestmentLeft
    {
        get
        {
            double share = Exec?.Allocation.GetValueOrDefault("obras") ?? Context.Executive.Config.Areas.FirstOrDefault(a => a.Id == "obras")?.ReferenceShare ?? 0;
            return (G.Fiscal.Rcl * (decimal)(share * WorkRules.FracaoInvestimento)) - G.Fiscal.InvestmentUsed;
        }
    }

    private Secretary? SecretaryFor(WorkType type) => G.SecretaryOf(type.Secretaria);

    private double CompetenceDelay(WorkType type) => Content.Secretaries.Regras.AtrasoPorCompetencia[(SecretaryFor(type)?.Competence ?? 1) - 1];

    /// <summary>Fator do secretário com agenda de zona: as obras da pasta andam mais na zona dele e menos nas outras.</summary>
    private double ZoneAgendaFactor(WorkType type, string zoneId) =>
        SecretaryFor(type) is { Agenda: SecretaryAgenda.Zona, AgendaZone: { } favored }
            ? favored == zoneId ? Content.Secretaries.Regras.ZonaFavorecida : Content.Secretaries.Regras.ZonaPreterida
            : 1;

    private void EnsureCompanies()
    {
        GovernanceState g = G;
        CompanyNames names = Content.Procurement.Empresas;
        int wanted = Content.Procurement.Regras.EmpresasPorPorte[Porte];
        int guard = 0;
        while (g.Companies.Count < wanted && guard++ < wanted * 20)
        {
            string name = $"{Rng.Pick(names.Tipos)} {Rng.Pick(names.Nucleos)} {Rng.Pick(names.Complementos)}";
            if (g.Companies.Any(c => c.Name == name))
            {
                continue;
            }

            Person owner = NewPerson(PersonRole.Doador, 35, 70, null);
            g.Companies.Add(new Company { Id = g.NewId("emp"), Name = name, Quality = Rng.PickWeighted([1, 2, 3, 4, 5], q => q is 1 or 5 ? 1.0 : q == 3 ? 3.0 : 2.0), OwnerId = owner.Id });
        }
    }

    private Work CreateWork(WorkType type, string zoneId, string origin, string sponsorId, WorkStage stage, double yield = 1)
    {
        GovernanceState g = G;
        var work = new Work
        {
            Id = g.NewId("obra"),
            TypeId = type.Id,
            ZoneId = zoneId,
            Origin = origin,
            SponsorId = sponsorId,
            Cost = CostOf(type),
            Created = Week,
            Yield = yield,
        };
        g.Works.Add(work);
        Enter(work, stage);
        return work;
    }

    /// <summary>Entra numa etapa e calcula o fim previsto dela.</summary>
    private void Enter(Work work, WorkStage stage)
    {
        WorkType type = Content.Works.Type(work.TypeId);
        work.Stage = stage;
        work.StageStart = Week;
        switch (stage)
        {
            case WorkStage.Projeto:
                int project = (int)Math.Ceiling(Rng.NextInt(type.ProjetoSemanas.Min, type.ProjetoSemanas.Max + 1) * CompetenceDelay(type) / ZoneAgendaFactor(type, work.ZoneId));
                work.StageEnd = Week.AddDays(7 * Math.Max(1, project));
                break;
            case WorkStage.Modalidade:
                work.StageEnd = Week.AddDays(7 * WorkRules.SemanasParaModalidadePadrao);
                break;
            case WorkStage.Licitacao:
                ProcurementOption option = Content.Procurement.Option(work.Mode ?? ProcurementMode.Concorrencia);
                work.StageEnd = Week.AddDays(7 * Rng.NextInt(option.Semanas.Min, option.Semanas.Max + 1));
                break;
            case WorkStage.Execucao:
                work.StageEnd = Week.AddDays(7 * Rng.NextInt(type.ExecucaoSemanas.Min, type.ExecucaoSemanas.Max + 1));
                break;
            default:
                work.StageEnd = null;
                break;
        }
    }

    /// <summary>Houve crise na zona há pouco: a emergência está reconhecida para contratar sem licitação.</summary>
    public bool EmergencyIn(string zoneId) =>
        G.Crises.Any(c => c.ZoneId == zoneId && Week.DayNumber - c.Date.DayNumber <= 7 * Content.Procurement.Regras.EmergenciaValidadeSemanas);

    /// <summary>Modalidades que a tela oferece para a obra, com o motivo quando a lei ou a regra impedem.</summary>
    public IReadOnlyList<(ProcurementOption Option, GovFailure? Blocked, string? Reason, bool Irregular)> ModesFor(Work work)
    {
        WorkType type = Content.Works.Type(work.TypeId);
        ProcurementLawConfig law = Context.ProcurementLaw;
        var list = new List<(ProcurementOption, GovFailure?, string?, bool)>();
        foreach (ProcurementOption option in Content.Procurement.Modalidades)
        {
            GovFailure? blocked = null;
            string? reason = null;
            bool irregular = false;
            if (option.SoPequenasEServicos && !type.Pequena && !type.Servico)
            {
                blocked = GovFailure.AlvoInvalido;
                reason = option.Descricao;
            }
            else if (option.Modo == ProcurementMode.DispensaPorValor && !law.CabeDispensaPorValor(work.Cost, type.Engenharia))
            {
                blocked = GovFailure.LimiteLegal;
                reason = Fill(Content.Works.Texto("previsao_dispensa_teto"), ("teto", Texts.Money(law.TetoDispensa(type.Engenharia))), ("valor", Texts.Money(work.Cost)), ("artigo", law.ArtigoDispensaValor));
            }
            else if (option.Modo == ProcurementMode.Emergencial)
            {
                irregular = !law.Permitida(ProcurementMode.Emergencial, work.Cost, type.Engenharia, EmergencyIn(work.ZoneId), type.ExecucaoSemanas.Max);
            }

            list.Add((option, blocked, reason, irregular));
        }

        return list;
    }

    private ProcurementMode DefaultMode(Work work)
    {
        var modes = ModesFor(work);
        bool Free(ProcurementMode mode) => modes.First(m => m.Option.Modo == mode) is { Blocked: null, Irregular: false };
        return Free(ProcurementMode.Emergencial) && Content.Works.Type(work.TypeId).ProtegeDeEnchente ? ProcurementMode.Emergencial
            : Free(ProcurementMode.DispensaPorValor) ? ProcurementMode.DispensaPorValor
            : Free(ProcurementMode.Pregao) ? ProcurementMode.Pregao : ProcurementMode.Concorrencia;
    }

    /// <summary>Modalidade que a prefeitura NPC escolhe; secretário de pouca integridade às vezes força a emergencial.</summary>
    private ProcurementMode NpcMode(Work work)
    {
        WorkType type = Content.Works.Type(work.TypeId);
        ProcurementRules rules = Content.Procurement.Regras;
        return SecretaryFor(type) is { } s && s.Integrity <= Content.Secretaries.Regras.IntegridadeDeRisco && !type.Servico && Rng.Chance(rules.NpcChanceEmergencialIrregular)
            ? ProcurementMode.Emergencial
            : DefaultMode(work);
    }

    private void StartProcurement(Work work, ProcurementMode mode)
    {
        WorkType type = Content.Works.Type(work.TypeId);
        work.Mode = mode;
        if (mode == ProcurementMode.Emergencial && !Context.ProcurementLaw.Permitida(mode, work.Cost, type.Engenharia, EmergencyIn(work.ZoneId), type.ExecucaoSemanas.Max))
        {
            // Emergencial sem emergência é irregularidade (GDD 17.15): fica escondida até alguém olhar.
            work.Irregular = true;
        }

        Enter(work, WorkStage.Licitacao);
        News("obra", Fill(Content.Works.Texto("licitacao_aberta"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("modalidade", Content.Procurement.Option(mode).Nome)), work.ZoneId);
    }

    /// <summary>Fim da licitação: sai o vencedor; contratação direta e secretário de pouca integridade podem trazer sobrepreço.</summary>
    private void Contract(Work work)
    {
        GovernanceState g = G;
        WorkType type = Content.Works.Type(work.TypeId);
        ProcurementRules rules = Content.Procurement.Regras;
        ProcurementOption option = Content.Procurement.Option(work.Mode ?? ProcurementMode.Concorrencia);
        Company company = Rng.PickWeighted(g.Companies, c => Math.Pow(c.Quality, rules.PesoQualidade));
        work.CompanyId = company.Id;
        int integrity = SecretaryFor(type)?.Integrity ?? 3;
        double overpriceChance = option.ChanceSobrepreco + (rules.SobreprecoPorIntegridade * Math.Max(0, 3 - integrity));
        if (Rng.Chance(Math.Clamp(overpriceChance, 0, 1)))
        {
            work.Overprice = Rng.NextDouble(rules.Sobrepreco.Min, rules.Sobrepreco.Max);
            work.Cost = Math.Round(work.Cost * (decimal)(1 + work.Overprice), 2);
            work.Irregular = true;
        }

        if (work.Irregular && Role == GovRole.Prefeito && work.Mode == ProcurementMode.Emergencial)
        {
            State.Ledger.AddSkeleton("contratacao_emergencial_irregular", Fill(Content.Works.Texto("emergencial_irregular"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId))),
                rules.GravidadeEmergencialIrregular, Week, new[] { SecretaryFor(type)?.PersonId, company.OwnerId }.OfType<string>());
        }

        News("obra", Fill(Content.Works.Texto("contratada"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("empresa", company.Name)), work.ZoneId, company.OwnerId);
        if (g.DonorCompanies.Contains(company.Id))
        {
            // Empresa de doador vencendo é fato que a imprensa nota (GDD 17.15).
            ShiftApproval(rules.ImagemEmpresaDoadora);
            News("obra", Fill(Content.Works.Texto("empresa_doadora"), ("empresa", company.Name), ("obra", WorkName(work))), work.ZoneId, company.OwnerId);
        }

        Enter(work, WorkStage.Execucao);
    }

    private void WorksWeek(bool automatic)
    {
        GovernanceState g = G;
        bool npcGovernment = Role == GovRole.Vereador || automatic;
        foreach (Work work in g.Works.Where(w => w.Open).ToList())
        {
            WorkType type = Content.Works.Type(work.TypeId);
            switch (work.Stage)
            {
                case WorkStage.Liberacao:
                    ReleaseAmendment(work);
                    break;
                case WorkStage.Projeto when work.StageEnd <= Week:
                    if (type.Servico)
                    {
                        Enter(work, WorkStage.Execucao);
                    }
                    else if (npcGovernment)
                    {
                        StartProcurement(work, NpcMode(work));
                    }
                    else
                    {
                        Enter(work, WorkStage.Modalidade);
                        News("obra", Fill(Content.Works.Texto("projeto_pronto"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId))), work.ZoneId);
                    }

                    break;
                case WorkStage.Modalidade when work.StageEnd <= Week || npcGovernment:
                    StartProcurement(work, DefaultMode(work));
                    break;
                case WorkStage.Licitacao when work.StageEnd <= Week:
                    ProcurementOption option = Content.Procurement.Option(work.Mode ?? ProcurementMode.Concorrencia);
                    if (!work.Impugned && Rng.Chance(option.ChanceImpugnacao))
                    {
                        work.Impugned = true;
                        int weeks = Rng.NextInt(option.ImpugnacaoSemanas.Min, option.ImpugnacaoSemanas.Max + 1);
                        work.DelayWeeks += weeks;
                        work.StageEnd = Week.AddDays(7 * weeks);
                        News("obra", Fill(Content.Works.Texto("impugnada"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("semanas", weeks.ToString(System.Globalization.CultureInfo.InvariantCulture))), work.ZoneId);
                    }
                    else
                    {
                        Contract(work);
                    }

                    break;
                case WorkStage.Execucao:
                    if (work.StageEnd <= Week)
                    {
                        Finish(work, npcGovernment);
                    }
                    else
                    {
                        MaybeDelay(work, type);
                    }

                    break;
                case WorkStage.Pronta when Week.DayNumber - work.StageStart.DayNumber >= 7 * WorkRules.SemanasParaEntregaSemCerimonia:
                    Deliver(work, ceremony: false, playerAttended: false);
                    break;
            }
        }

        if (npcGovernment)
        {
            PlanNpcWork();
        }
    }

    private void MaybeDelay(Work work, WorkType type)
    {
        int quality = G.Company(work.CompanyId)?.Quality ?? 3;
        double chance = WorkRules.ChanceAtrasoSemanal * ((6 - quality) / 3.0) * CompetenceDelay(type) / ZoneAgendaFactor(type, work.ZoneId);
        if (type.Engenharia && WorkRules.MesesDeChuva.Contains(Week.Month))
        {
            chance *= WorkRules.ChuvaFatorAtraso;
        }

        if (!Rng.Chance(Math.Clamp(chance, 0, 1)))
        {
            return;
        }

        int weeks = Rng.NextInt(WorkRules.AtrasoSemanas.Min, WorkRules.AtrasoSemanas.Max + 1);
        bool wasNews = work.DelayWeeks >= WorkRules.AtrasoQueViraNoticia;
        work.DelayWeeks += weeks;
        work.StageEnd = work.StageEnd?.AddDays(7 * weeks);
        if (!wasNews && work.DelayWeeks >= WorkRules.AtrasoQueViraNoticia)
        {
            ShiftZoneApproval(G.IndexOf(work.ZoneId), -WorkRules.AprovacaoPorPrestigio * type.Prestigio / 2);
            News("obra", Fill(Content.Works.Texto("atraso"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("semanas", work.DelayWeeks.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("empresa", CompanyName(work))), work.ZoneId);
        }
    }

    /// <summary>A execução terminou: serviço entra em vigor; obra fica pronta e espera a inauguração.</summary>
    private void Finish(Work work, bool npcGovernment)
    {
        WorkType type = Content.Works.Type(work.TypeId);
        if (type.Servico)
        {
            Deliver(work, ceremony: false, playerAttended: false);
            return;
        }

        Enter(work, WorkStage.Pronta);
        bool players = work.SponsorId == RaceSetup.PlayerId || (Role == GovRole.Prefeito && !npcGovernment);
        if (players)
        {
            // A obra é do jogador: fica pronta e espera ele inaugurar (ou a entrega sem cerimônia).
            News("obra", Fill(Content.Works.Texto("pronta"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId))), work.ZoneId);
            return;
        }

        // Obra do governo NPC: o prefeito inaugura, salvo no período em que candidato não pode comparecer.
        Deliver(work, ceremony: !Context.InaugurationLaw.NoPeriodoVedado(Week), playerAttended: false);
    }

    /// <summary>
    /// Entrega a obra: presença de quem patrocinou, aprovação do governo na zona, indicador, folha e as promessas
    /// que ela cumpre.
    /// </summary>
    private List<string> Deliver(Work work, bool ceremony, bool playerAttended)
    {
        GovernanceState g = G;
        WorkType type = Content.Works.Type(work.TypeId);
        var changes = new List<string>();
        int zone = g.IndexOf(work.ZoneId);
        work.Stage = WorkStage.Entregue;
        work.StageStart = Week;
        work.StageEnd = null;
        work.Delivered = Week;
        work.Ceremony = ceremony;
        work.PlayerAttended = playerAttended;

        double credit = playerAttended || type.Servico ? 1 : WorkRules.PrestigioSemCerimonia;
        if (work.SponsorId == RaceSetup.PlayerId && Role == GovRole.Vereador)
        {
            double gained = GainPresence(zone, type.Prestigio * work.Yield * credit, spill: true);
            changes.Add(ChPresence(work.ZoneId, gained));
        }

        double approval = type.Prestigio * WorkRules.AprovacaoPorPrestigio * (Role == GovRole.Prefeito ? credit : 1);
        ShiftZoneApproval(zone, approval);
        if (Role == GovRole.Prefeito)
        {
            changes.Add(ChZoneApproval(work.ZoneId, approval));
        }

        bool staffed = true;
        if (type.Folha > 0)
        {
            if (Context.FiscalLaw.VedadoAumentar(g.Payroll.Share))
            {
                // Acima do limite prudencial não se admite pessoal novo (LC 101/2000, art. 22): abre sem equipe completa.
                staffed = false;
                changes.Add(Fill(Content.Works.Texto("abre_sem_pessoal"), ("obra", WorkName(work)), ("artigo", Context.FiscalLaw.ArtigoPrudencial)));
            }
            else
            {
                g.Payroll.Share += type.Folha;
                changes.Add(Fill(Content.Works.Texto("previsao_folha"), ("folha", Pct(type.Folha))));
            }
        }

        // Com os indicadores reais, a entrega vira pressão no indicador da área, com prazo; sem eles, vale o salto na nota.
        if (!PushIndicator(work, type, staffed) && type.Indicador is { } indicator && Exec is { } e && e.Indicators.ContainsKey(indicator))
        {
            BoundsConfig bounds = Context.Executive.Config.IndicatorBounds;
            e.Indicators[indicator] = Math.Clamp(e.Indicators[indicator] + (type.IndicadorPontos * (staffed ? 1 : 0.5)), bounds.Min, bounds.Max);
        }

        string key = type.Servico ? "servico_em_vigor" : ceremony ? (playerAttended ? "inaugurada" : "inaugurada_sem_voce") : "entregue_sem_cerimonia";
        News("obra", Fill(Content.Works.Texto(key), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("prefeito", MayorName),
            ("pedido", work.SponsorId == RaceSetup.PlayerId && Role == GovRole.Vereador ? Content.Works.Texto("a_pedido") : "")).Trim(), work.ZoneId);
        changes.AddRange(OnWorkDelivered(work));
        TouchZone(work.ZoneId);
        return changes;
    }

    // ---------------------------------------------------------------- comandos de obra

    private GovFailure? CanInaugurate(Work? work, out string reason)
    {
        reason = "";
        if (work is null || work.Stage != WorkStage.Pronta)
        {
            // Só se inaugura obra que existe e está pronta.
            reason = Content.Works.Texto("sem_obra");
            return GovFailure.SemObraPronta;
        }

        if (Role == GovRole.Vereador && work.SponsorId != RaceSetup.PlayerId)
        {
            reason = Content.Works.Texto("sem_obra");
            return GovFailure.AlvoInvalido;
        }

        if (PlayerBarredFromInauguration)
        {
            InaugurationLawConfig law = Context.InaugurationLaw;
            reason = Fill(Content.Works.Texto("periodo_vedado"), ("meses", law.MesesAntesDoPleito.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("artigo", law.Artigo), ("pena", law.Pena));
            return GovFailure.PeriodoVedado;
        }

        return null;
    }

    /// <summary>
    /// O jogador está impedido de comparecer a inauguração: é pré-candidato ou candidato e a data cai nos meses
    /// anteriores ao pleito (Lei 9.504/1997, art. 77).
    /// </summary>
    public bool PlayerBarredFromInauguration => State.Campaign is not null && Context.InaugurationLaw.NoPeriodoVedado(Clock.Today(State));

    public GovForecast ForecastInaugurate(string workId)
    {
        Work? work = G.Work(workId);
        if (CanInaugurate(work, out string reason) is { } failure)
        {
            return GovForecast.Block(failure, reason);
        }

        WorkType type = Content.Works.Type(work!.TypeId);
        int zone = G.IndexOf(work.ZoneId);
        var lines = new List<ForecastLine>();
        // Cada promessa cumprida com zona soma ao que a obra rende ali.
        List<Promise> kept = PromisesOnDelivery(work);
        int inZone = kept.Count(p => p.ZoneId is not null);
        if (Role == GovRole.Vereador)
        {
            double gain = ChainPresence(zone, Enumerable.Repeat(CollectionRules.PresencaPromessaCumprida, inZone).Prepend(type.Prestigio * work.Yield));
            lines.Add(new ForecastLine(GovForecast.Presenca, work.ZoneId, gain, gain, gain));
        }
        else
        {
            double gain = (type.Prestigio * WorkRules.AprovacaoPorPrestigio) + (inZone * CollectionRules.PresencaPromessaCumprida);
            lines.Add(new ForecastLine(GovForecast.AprovacaoZona, work.ZoneId, gain, gain, gain));
        }

        if (kept.Count > 0)
        {
            lines.Add(new ForecastLine(GovForecast.PromessaCumprida, work.ZoneId, kept.Count, kept.Count, kept.Count));
        }

        var explanation = new List<string>
        {
            Fill(Content.Works.Texto("previsao_inauguracao"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("fracao", Texts.Percent(WorkRules.PrestigioSemCerimonia))),
        };
        explanation.AddRange(kept.Select(p => Fill(Content.Works.Texto("previsao_promessa"), ("promessa", p.Text))));
        return new GovForecast(null, null, lines, [], explanation)
        {
            Factors = work.Yield < 0.999 || work.Yield > 1.001 ? [new YieldFactor("rendimento", Content.Works.Texto("fator_cansaco"), work.Yield)] : [],
            Remembered = kept.Select(PromiseHolder).Where(n => n.Length > 0).Distinct().ToList(),
        };
    }

    /// <summary>Inaugura, com o jogador presente, uma obra pronta. Não existe inauguração sem obra entregue.</summary>
    public GovResult Inaugurate(string workId)
    {
        Work? work = G.Work(workId);
        if (CanInaugurate(work, out string reason) is { } failure)
        {
            return GovResult.Fail(failure, reason);
        }

        List<string> changes = Deliver(work!, ceremony: true, playerAttended: true);
        return new GovResult(null, Fill(Content.Works.Texto("inaugurada"), ("obra", WorkName(work!)), ("zona", ZoneName(work!.ZoneId)), ("prefeito", MayorName), ("pedido", "")).Trim(), changes,
            work.Yield is < 0.999 or > 1.001 ? [new YieldFactor("rendimento", Content.Works.Texto("fator_cansaco"), work.Yield)] : []) { CreatedId = work.Id };
    }

    /// <summary>Entrega a obra pronta sem cerimônia (o caminho no período vedado): cumpre a promessa, rende menos.</summary>
    public GovResult DeliverQuietly(string workId)
    {
        Work? work = G.Work(workId);
        if (work is null || work.Stage != WorkStage.Pronta || (Role == GovRole.Vereador && work.SponsorId != RaceSetup.PlayerId))
        {
            return GovResult.Fail(GovFailure.SemObraPronta, Content.Works.Texto("sem_obra"));
        }

        List<string> changes = Deliver(work, ceremony: false, playerAttended: false);
        return new GovResult(null, Fill(Content.Works.Texto("entregue_sem_cerimonia"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("prefeito", MayorName), ("pedido", "")).Trim(), changes, []);
    }

    // ---------------------------------------------------------------- prefeito: abrir obra e escolher a modalidade

    private GovFailure? CanStartWork(WorkType? type, string zoneId, out string reason)
    {
        reason = "";
        if (Role != GovRole.Prefeito)
        {
            return GovFailure.PapelErrado;
        }

        if (type is null || !Fits(type, zoneId))
        {
            return GovFailure.AlvoInvalido;
        }

        if (G.Works.Count(w => w.Open && w.Origin is Work.DaPrefeitura or Work.DeConvenio) >= WorkRules.CapacidadeDeExecucao[Porte])
        {
            reason = Fill(Content.Works.Texto("sem_capacidade"), ("limite", WorkRules.CapacidadeDeExecucao[Porte].ToString(System.Globalization.CultureInfo.InvariantCulture)));
            return GovFailure.SemVaga;
        }

        if (CostOf(type) > InvestmentLeft)
        {
            reason = Fill(Content.Works.Texto("sem_investimento"), ("custo", Texts.Money(CostOf(type))), ("resta", Texts.Money(Math.Max(0, InvestmentLeft))));
            return GovFailure.SemInvestimento;
        }

        return null;
    }

    public GovForecast ForecastStartWork(string typeId, string zoneId)
    {
        WorkType? type = Content.Works.Find(typeId);
        if (CanStartWork(type, zoneId, out string reason) is { } failure)
        {
            return GovForecast.Block(failure, reason);
        }

        double delay = CompetenceDelay(type!);
        int minWeeks = (int)Math.Ceiling(type!.ProjetoSemanas.Min * delay) + (type.Servico ? 0 : Content.Procurement.Modalidades.Min(m => m.Semanas.Min)) + type.ExecucaoSemanas.Min;
        int maxWeeks = (int)Math.Ceiling(type.ProjetoSemanas.Max * delay) + (type.Servico ? 0 : Content.Procurement.Option(ProcurementMode.Concorrencia).Semanas.Max) + type.ExecucaoSemanas.Max;
        double approval = type.Prestigio * WorkRules.AprovacaoPorPrestigio;
        var lines = new List<ForecastLine>
        {
            new(GovForecast.Investimento, null, -(double)CostOf(type), -(double)CostOf(type), -(double)CostOf(type)),
            new(GovForecast.AprovacaoZona, zoneId, approval * WorkRules.PrestigioSemCerimonia, approval, approval),
        };
        if (type.Folha > 0)
        {
            lines.Add(new ForecastLine(GovForecast.Folha, null, 0, type.Folha, type.Folha));
        }

        var probe = new Work { Id = "", TypeId = type.Id, ZoneId = zoneId, Origin = Work.DaPrefeitura, SponsorId = Work.Prefeito, Cost = CostOf(type), Created = Week };
        int promises = PromisesFulfilledBy(probe).Count;
        if (promises > 0)
        {
            lines.Add(new ForecastLine(GovForecast.PromessaCumprida, zoneId, promises, promises, promises));
        }

        var explanation = new List<string>
        {
            Fill(Content.Works.Texto("previsao_entrega"), ("obra", type.Nome), ("zona", ZoneName(zoneId)), ("min", minWeeks.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("max", maxWeeks.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("custo", Texts.Money(CostOf(type))), ("resta", Texts.Money(InvestmentLeft - CostOf(type)))),
        };
        explanation.AddRange(PromisesFulfilledBy(probe).Select(p => Fill(Content.Works.Texto("previsao_promessa"), ("promessa", p.Text))));
        var factors = new List<YieldFactor>();
        if (SecretaryFor(type) is { } secretary)
        {
            factors.Add(new YieldFactor("secretario", Fill(Content.Works.Texto("fator_secretario"), ("nome", NameOf(secretary.PersonId))), 1 / delay));
        }

        return new GovForecast(null, null, lines, [], explanation) { Factors = factors, Remembered = PromisesFulfilledBy(probe).Select(PromiseHolder).Where(n => n.Length > 0).Distinct().ToList() };
    }

    /// <summary>O prefeito abre uma obra numa zona: entra no mapa na etapa de projeto e consome investimento do ano.</summary>
    public GovResult StartWork(string typeId, string zoneId)
    {
        WorkType? type = Content.Works.Find(typeId);
        if (CanStartWork(type, zoneId, out string reason) is { } failure)
        {
            return GovResult.Fail(failure, reason);
        }

        G.Fiscal.InvestmentUsed += CostOf(type!);
        Work work = CreateWork(type!, zoneId, Work.DaPrefeitura, Work.Prefeito, WorkStage.Projeto);
        string text = Fill(Content.Works.Texto("anunciada"), ("obra", type!.Nome), ("zona", ZoneName(zoneId)), ("prefeito", MayorName));
        News("obra", text, zoneId);
        // A zona lembra: quem mora ali tem uma liderança que registra o anúncio.
        RememberInZone(zoneId, "obra_anunciada", Content.Collection.Lembrancas["obra_anunciada"], ("obra", type.Nome), ("zona", ZoneName(zoneId)));
        var changes = new List<string> { ChInvestment(-work.Cost), T("mud_etapa_ate", ("etapa", StageName(WorkStage.Projeto)), ("data", Texts.Date(work.StageEnd!.Value))) };
        changes.AddRange(CheckZoneWorkPromises(work));
        TouchZone(zoneId);
        return new GovResult(null, text, changes, []) { CreatedId = work.Id };
    }

    public GovForecast ForecastMode(string workId, ProcurementMode mode)
    {
        Work? work = G.Work(workId);
        if (Role != GovRole.Prefeito || work is null || work.Stage != WorkStage.Modalidade)
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, Content.Works.Texto("sem_obra"));
        }

        var (option, blocked, reason, irregular) = ModesFor(work).First(m => m.Option.Modo == mode);
        if (blocked is { } failure)
        {
            return GovForecast.Block(failure, reason ?? option.Descricao);
        }

        var explanation = new List<string>
        {
            Fill(Content.Works.Texto("previsao_modalidade"), ("modalidade", option.Nome), ("min", option.Semanas.Min.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("max", option.Semanas.Max.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("impugnacao", Texts.Percent(option.ChanceImpugnacao)), ("artigo", Context.ProcurementLaw.ArtigoDe(mode))),
        };
        var outcomes = new List<ForecastOutcome>();
        if (option.ChanceImpugnacao > 0)
        {
            outcomes.Add(new ForecastOutcome(option.ChanceImpugnacao, Fill(Content.Works.Texto("impugnada"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)),
                ("semanas", T("faixa_de_semanas", ("min", option.ImpugnacaoSemanas.Min.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    ("max", option.ImpugnacaoSemanas.Max.ToString(System.Globalization.CultureInfo.InvariantCulture)))))));
        }

        var remembered = new List<string>();
        if (irregular)
        {
            explanation.Add(Fill(Content.Works.Texto("previsao_irregular"), ("artigo", Context.ProcurementLaw.ArtigoEmergencia)));
            if (SecretaryFor(Content.Works.Type(work.TypeId)) is { } s)
            {
                remembered.Add(NameOf(s.PersonId));
            }
        }

        return new GovForecast(null, null, [], outcomes, explanation) { Remembered = remembered };
    }

    /// <summary>O prefeito escolhe a modalidade da contratação. Dispensa acima do teto é barrada; emergencial sem emergência passa como irregularidade.</summary>
    public GovResult ChooseMode(string workId, ProcurementMode mode)
    {
        GovForecast forecast = ForecastMode(workId, mode);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Work work = G.Work(workId)!;
        StartProcurement(work, mode);
        var changes = new List<string> { T("mud_etapa_ate", ("etapa", StageName(WorkStage.Licitacao)), ("data", Texts.Date(work.StageEnd!.Value))) };
        if (work.Irregular)
        {
            changes.Add(Fill(Content.Works.Texto("previsao_irregular"), ("artigo", Context.ProcurementLaw.ArtigoEmergencia)));
        }

        return new GovResult(null, Fill(Content.Works.Texto("licitacao_aberta"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("modalidade", Content.Procurement.Option(mode).Nome)), changes, []);
    }

    // ---------------------------------------------------------------- governo NPC

    /// <summary>Na posse de um vereador, a cidade já tem obras do prefeito em andamento.</summary>
    private void SeedNpcWorks()
    {
        int wanted = Math.Max(1, WorkRules.ObrasSimultaneasNpc[Porte] / 2);
        for (int i = G.Works.Count(w => w.Open); i < wanted; i++)
        {
            NewNpcWork();
        }
    }

    private void PlanNpcWork()
    {
        int open = G.Works.Count(w => w.Open && w.Origin == Work.DaPrefeitura);
        if (open < WorkRules.ObrasSimultaneasNpc[Porte] && Rng.Chance(WorkRules.NpcChanceNovaObra))
        {
            Work work = NewNpcWork();
            News("obra", Fill(Content.Works.Texto("anunciada"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("prefeito", MayorName)), work.ZoneId);
        }
    }

    private Work NewNpcWork()
    {
        GovernanceState g = G;
        // O prefeito prefere os redutos dos vereadores da base e as zonas com mais gente.
        var baseZones = Members.Where(m => InBase(m.PartyId) && g.MemberZones.ContainsKey(m.Id)).Select(m => g.MemberZones[m.Id]).ToHashSet(StringComparer.Ordinal);
        int zone = Rng.PickWeighted(Enumerable.Range(0, g.Zones.Count).ToList(), i => Math.Max(1.0, g.ZonePopulation[i]) * (baseZones.Contains(g.Zones[i]) ? 1.5 : 1));
        string zoneId = g.Zones[zone];
        List<WorkType> types = Content.Works.Tipos.Where(t => Fits(t, zoneId)).ToList();
        WorkType type = Rng.PickWeighted(types, t => t.Servico ? 0.5 : 1.0);
        if (SecretaryFor(type) is { Agenda: SecretaryAgenda.Projeto, AgendaWork: { } pet } && Content.Works.Find(pet) is { } petType && Fits(petType, zoneId))
        {
            type = petType;
        }

        return CreateWork(type, zoneId, Work.DaPrefeitura, Work.Prefeito, WorkStage.Projeto);
    }

    /// <summary>Emenda parada: a liberação depende do Executivo (relação com o prefeito); depois do prazo, é obrigatória.</summary>
    private void ReleaseAmendment(Work work)
    {
        int waited = (Week.DayNumber - work.StageStart.DayNumber) / 7;
        double chance = Role == GovRole.Prefeito ? 1
            : WorkRules.ChanceLiberacaoEmenda + (WorkRules.LiberacaoPorRelacao * (Mandate?.MayorRelation ?? 0) / 100);
        if (waited >= WorkRules.SemanasLiberacaoObrigatoria || Rng.Chance(Math.Clamp(chance, 0.01, 1)))
        {
            Enter(work, WorkStage.Projeto);
            News("obra", Fill(Content.Works.Texto("emenda_liberada"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("prefeito", MayorName)), work.ZoneId);
        }
        else if (waited > 0 && waited % 8 == 0)
        {
            News("obra", Fill(Content.Works.Texto("emenda_retida"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("prefeito", MayorName),
                ("semanas", waited.ToString(System.Globalization.CultureInfo.InvariantCulture))), work.ZoneId);
        }
    }

    /// <summary>Lembrança numa liderança da zona (quem representa os moradores dali).</summary>
    private void RememberInZone(string zoneId, string fact, MemoryDefinition memory, params (string Key, string Value)[] fill)
    {
        Leader? leader = City.Leaders.Where(l => l.Zones.Contains(zoneId)).OrderByDescending(l => l.AlignedWith == RaceSetup.PlayerId).ThenByDescending(l => l.Influence).FirstOrDefault();
        if (leader is not null)
        {
            Remember(leader.PersonId, PersonRole.Lideranca, fact, memory, fill);
        }
    }
}
