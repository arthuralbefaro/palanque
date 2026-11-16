using System.Globalization;
using Planalto.Core.Campaign;
using Planalto.Core.Legislature;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.Core.Governance;

/// <summary>Uma linha da contagem nominal: como o vereador tende a votar e por quê.</summary>
public sealed record CountLine(string MemberId, string Name, string? PartyId, string Lean, double YesProbability, string Reason, string? RequestText);

/// <summary>Contagem nominal antes da votação (GDD 17.14): "5 sim, 4 não, 2 indecisos: Marta (quer a emenda da creche)".</summary>
public sealed record NominalCount(string SubjectId, string Title, Quorum Quorum, int Yes, int No, int Undecided, int Members, IReadOnlyList<CountLine> Lines)
{
    public const string Sim = "sim";
    public const string Nao = "nao";
    public const string Indeciso = "indeciso";

    /// <summary>Votos SIM que faltam para aprovar contando só os certos (0 = já passa).</summary>
    public int MissingToApprove => Quorum switch
    {
        Quorum.Absoluta => Math.Max(0, (Members / 2) + 1 - Yes),
        Quorum.DoisTercos => Math.Max(0, (int)Math.Ceiling(Members * 2 / 3.0) - Yes),
        _ => Math.Max(0, No + 1 - Yes),
    };
}

/// <summary>Uma opção de instrumento com o alvo já escolhido, para a tela listar e para o robô comparar.</summary>
public sealed record InstrumentOption(string InstrumentId, string Label, string? ZoneId, string? TargetId, string? TypeId, GovForecast Forecast);

/// <summary>Mandato de vereador (GDD 17.14): gabinete, instrumentos, acordos de voto, contagem nominal e a base que cobra.</summary>
public sealed partial class GovernanceEngine
{
    private InstrumentRules CouncilRules => Content.Instruments.Regras;

    // ================================================================ rendimento e cansaço

    private static string FatigueKey(string instrument, string? zoneId) => zoneId is null ? instrument : $"{instrument}@{zoneId}";

    /// <summary>
    /// Fatores que multiplicam o que um instrumento rende agora: cansaço do instrumento repetido, o mesmo na zona,
    /// assessor de comunicação e demanda trazida pelo assessor de base (GDD 17.1, princípio g).
    /// </summary>
    public IReadOnlyList<YieldFactor> YieldFactors(string instrumentId, string? zoneId)
    {
        GovernanceState g = G;
        var factors = new List<YieldFactor>();
        double tired = g.Fatigue.GetValueOrDefault(instrumentId);
        if (tired > 1e-9)
        {
            factors.Add(new YieldFactor("cansaco", Fill(Content.Instruments.Texto("fator_cansaco"), ("vezes", Num(tired))), Math.Pow(CouncilRules.CansacoFator, tired)));
        }

        if (zoneId is not null)
        {
            double tiredZone = g.Fatigue.GetValueOrDefault(FatigueKey(instrumentId, zoneId));
            if (tiredZone > 1e-9)
            {
                factors.Add(new YieldFactor("cansaco_zona", Fill(Content.Instruments.Texto("fator_cansaco_zona"), ("zona", ZoneName(zoneId))), Math.Pow(CouncilRules.CansacoZonaFator, tiredZone)));
            }

            if (g.ZoneLeads.Contains(zoneId) && instrumentId is InstrumentDefinition.Indicacao or InstrumentDefinition.Emenda)
            {
                factors.Add(new YieldFactor("demanda", Fill(Content.Instruments.Texto("fator_demanda"), ("zona", ZoneName(zoneId))), 1 + CouncilRules.BonusDemandaDeBase));
            }
        }

        if (g.AideIn(AideRole.Comunicacao) is { } aide)
        {
            factors.Add(new YieldFactor("comunicacao", Fill(Content.Instruments.Texto("fator_comunicacao"), ("nome", NameOf(aide.PersonId))), Content.Instruments.Role(AideRole.Comunicacao).At(aide.Quality)));
        }

        return factors;
    }

    private static double Product(IEnumerable<YieldFactor> factors) => factors.Aggregate(1.0, (acc, f) => acc * f.Multiplier);

    private double ChiefBonus => G.AideIn(AideRole.Chefe) is { } chief ? Content.Instruments.Role(AideRole.Chefe).At(chief.Quality) : 0;

    private GovFailure? CanUse(InstrumentDefinition def, out string reason)
    {
        reason = "";
        if (Role != GovRole.Vereador || Mandate is null)
        {
            return GovFailure.PapelErrado;
        }

        if (G.TurnsLeft < def.Turnos)
        {
            reason = Content.Instruments.Texto("sem_turno");
            return GovFailure.SemTurno;
        }

        if (Capital < def.Capital)
        {
            reason = Fill(Content.Instruments.Texto("sem_capital"), ("capital", def.Capital.ToString(CultureInfo.InvariantCulture)));
            return GovFailure.SemCapital;
        }

        return null;
    }

    /// <summary>Paga o instrumento (turno, capital, relação com o prefeito) e soma o cansaço.</summary>
    private void Spend(InstrumentDefinition def, string? zoneId)
    {
        GovernanceState g = G;
        g.TurnsLeft -= def.Turnos;
        Capital -= def.Capital;
        ShiftMayorRelation(def.RelacaoPrefeito);
        g.Fatigue[def.Id] = g.Fatigue.GetValueOrDefault(def.Id) + 1;
        if (zoneId is not null)
        {
            string key = FatigueKey(def.Id, zoneId);
            g.Fatigue[key] = g.Fatigue.GetValueOrDefault(key) + 1;
            g.ZoneLeads.Remove(zoneId);
            TouchZone(zoneId);
        }
    }

    private List<ForecastLine> BaseLines(InstrumentDefinition def, string? zoneId, double yield)
    {
        var lines = new List<ForecastLine>();
        // Levar o mandato à zona cumpre as promessas de presença feitas ali: a previsão conta com elas.
        int kept = zoneId is null ? 0 : PresencePromises(zoneId).Count;
        if (zoneId is not null && (def.Presenca > 0 || kept > 0))
        {
            double gain = ChainPresence(G.IndexOf(zoneId), Enumerable.Repeat(CollectionRules.PresencaPromessaCumprida, kept).Prepend(def.Presenca * yield));
            lines.Add(new ForecastLine(GovForecast.Presenca, zoneId, gain, gain, gain));
        }

        double image = (def.Imagem * yield) + (kept * CollectionRules.ImagemPromessaCumprida);
        if (Math.Abs(image) > 1e-12)
        {
            lines.Add(new ForecastLine(GovForecast.Imagem, null, image, image, image));
        }

        if (kept > 0)
        {
            lines.Add(new ForecastLine(GovForecast.PromessaCumprida, null, kept, kept, kept));
        }

        if (def.Capital != 0)
        {
            lines.Add(new ForecastLine(GovForecast.Capital, null, -def.Capital, -def.Capital, -def.Capital));
        }

        if (def.RelacaoPrefeito != 0)
        {
            lines.Add(new ForecastLine(GovForecast.RelacaoPrefeito, null, def.RelacaoPrefeito, def.RelacaoPrefeito, def.RelacaoPrefeito));
        }

        lines.Add(new ForecastLine(GovForecast.Turnos, null, -def.Turnos, -def.Turnos, -def.Turnos));
        return lines;
    }

    private List<string> ApplyBase(InstrumentDefinition def, string? zoneId, double yield)
    {
        var changes = new List<string>();
        if (zoneId is not null && def.Presenca > 0)
        {
            changes.Add(ChPresence(zoneId, GainPresence(G.IndexOf(zoneId), def.Presenca * yield)));
        }

        if (Math.Abs(def.Imagem) > 1e-12)
        {
            ShiftImage(def.Imagem * yield);
            changes.Add(ChImage(def.Imagem * yield));
        }

        if (def.RelacaoPrefeito != 0)
        {
            changes.Add(ChRelation(MayorName, def.RelacaoPrefeito));
        }

        if (def.Capital != 0)
        {
            changes.Add(ChCapital(-def.Capital));
        }

        return changes;
    }

    /// <summary>Um instrumento foi usado na zona: a cobrança de presença dali fica atendida e o assessor de base, amparado.</summary>
    private void TouchZone(string zoneId)
    {
        GovernanceState g = G;
        foreach (Aide aide in g.Cabinet.Where(a => a.Role == AideRole.Base && a.ZoneId == zoneId))
        {
            aide.LastSupported = Week;
            aide.Neglected = false;
        }

        FulfilByPresence(zoneId);
        foreach (Demand demand in g.Demands.Where(d => d.Kind == Demand.Presenca && d.Met is null && d.RefId == zoneId).ToList())
        {
            demand.Met = true;
            Remember(demand.PersonId, PersonRole.Lideranca, "base_cobra_atendida", Content.Instruments.Lembrancas["base_cobra_atendida"], ("zona", ZoneName(zoneId)));
            News("cobranca", Fill(Content.Instruments.Texto("base_cobra_atendida"), ("zona", ZoneName(zoneId)), ("nome", NameOf(demand.PersonId))), zoneId, demand.PersonId);
        }
    }

    // ================================================================ requerimento

    /// <summary>Estimativa pública de haver problema numa obra: o que qualquer um vê (atraso, contratação direta).</summary>
    public double Suspicion(Work work)
    {
        double p = CouncilRules.ChanceIrregularidadeBase;
        if (work.Mode is { } mode)
        {
            p += Content.Procurement.Option(mode).ChanceSobrepreco;
        }

        if (work.DelayWeeks >= WorkRules.AtrasoQueViraNoticia)
        {
            p += CouncilRules.ChanceIrregularidadeBase;
        }

        return Math.Clamp(p, 0.02, 0.9);
    }

    private bool ValidRequestTarget(string kind, string targetId) => kind switch
    {
        InfoRequest.SobreObra => G.Work(targetId) is { Open: true, Revealed: false, CompanyId: not null },
        InfoRequest.SobreSecretaria => G.SecretaryOf(targetId) is not null,
        _ => false,
    } && !G.Requests.Any(r => !r.Answered && r.TargetKind == kind && r.TargetId == targetId);

    public GovForecast ForecastRequerimento(string kind, string targetId)
    {
        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Requerimento);
        if (CanUse(def, out string reason) is { } failure)
        {
            return GovForecast.Block(failure, reason);
        }

        if (!ValidRequestTarget(kind, targetId))
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, def.Descricao);
        }

        Work? work = kind == InfoRequest.SobreObra ? G.Work(targetId) : null;
        var factors = YieldFactors(def.Id, work?.ZoneId);
        double yield = Product(factors);
        List<ForecastLine> lines = BaseLines(def, work?.ZoneId, yield);
        var outcomes = new List<ForecastOutcome>();
        if (work is not null)
        {
            double p = Suspicion(work);
            double bonus = CouncilRules.ImagemRequerimentoRevelador * yield;
            lines.Add(new ForecastLine(GovForecast.Imagem + "_later", null, 0, bonus, p * bonus));
            outcomes.Add(new ForecastOutcome(p, Content.Instruments.Texto("requerimento_resposta_irregular")));
            outcomes.Add(new ForecastOutcome(1 - p, Content.Instruments.Texto("requerimento_resposta_normal")));
        }

        IntRange prazo = def.PrazoSemanas ?? new IntRange { Min = 2, Max = 4 };
        string alvo = work is not null ? T("alvo_obra", ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId))) : Content.Secretaries.Secretaria(targetId).Nome;
        return new GovForecast(null, null, lines, outcomes,
        [
            Fill(Content.Instruments.Texto("previsao_requerimento"), ("alvo", alvo), ("min", prazo.Min.ToString(CultureInfo.InvariantCulture)), ("max", prazo.Max.ToString(CultureInfo.InvariantCulture))),
            Fill(Content.Instruments.Texto("previsao_prefeito"), ("prefeito", MayorName), ("pontos", def.RelacaoPrefeito.ToString(CultureInfo.InvariantCulture))),
        ])
        { Factors = factors, Remembered = [MayorName] };
    }

    /// <summary>Requerimento de informação: em algumas semanas revela o dado escondido de uma obra ou de uma secretaria; esfria a relação com o prefeito.</summary>
    public GovResult Requerimento(string kind, string targetId)
    {
        GovForecast forecast = ForecastRequerimento(kind, targetId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Requerimento);
        Work? work = kind == InfoRequest.SobreObra ? G.Work(targetId) : null;
        double yield = Product(forecast.Factors);
        IntRange prazo = def.PrazoSemanas ?? new IntRange { Min = 2, Max = 4 };
        var request = new InfoRequest
        {
            Id = G.NewId("req"),
            TargetKind = kind,
            TargetId = targetId,
            Filed = Week,
            Due = Week.AddDays(7 * Rng.NextInt(prazo.Min, prazo.Max + 1)),
            Yield = yield,
        };
        G.Requests.Add(request);
        List<string> changes = ApplyBase(def, work?.ZoneId, yield);
        Spend(def, work?.ZoneId);
        Remember(Mandate!.MayorId, PersonRole.Dirigente, "requerimento", Content.Instruments.Lembrancas["requerimento"]);
        string alvo = work is not null ? T("alvo_obra", ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId))) : Content.Secretaries.Secretaria(targetId).Nome;
        string text = Fill(Content.Instruments.Texto("requerimento_protocolado"), ("alvo", alvo), ("data", Texts.Date(request.Due)));
        News("gabinete", text, work?.ZoneId);
        return new GovResult(null, text, changes, forecast.Factors) { CreatedId = request.Id };
    }

    private void AnswerRequests()
    {
        GovernanceState g = G;
        foreach (InfoRequest request in g.Requests.Where(r => !r.Answered && r.Due <= Week).ToList())
        {
            request.Answered = true;
            if (request.TargetKind == InfoRequest.SobreSecretaria)
            {
                request.Finding = InfoRequest.SobreSecretaria;
                Secretary? s = g.SecretaryOf(request.TargetId);
                News("gabinete", Fill(Content.Instruments.Texto("requerimento_secretaria"), ("secretaria", Content.Secretaries.Secretaria(request.TargetId).Nome), ("nome", NameOf(s?.PersonId)),
                    ("competencia", (s?.Competence ?? 0).ToString(CultureInfo.InvariantCulture)), ("folha", Pct(g.Payroll.Share))), null, s?.PersonId);
                if (s is not null)
                {
                    g.KnownSecretaries.Add(s.PersonId);
                }

                continue;
            }

            if (g.Work(request.TargetId) is not { } work)
            {
                continue;
            }

            work.Revealed = true;
            request.Finding = work.Irregular ? Inspection.Irregular : work.DelayWeeks > 0 ? Inspection.Atraso : Inspection.Normal;
            string key = work.Irregular ? "requerimento_resposta_irregular" : work.DelayWeeks > 0 ? "requerimento_resposta_atraso" : "requerimento_resposta_normal";
            if (work.Irregular)
            {
                ShiftImage(CouncilRules.ImagemRequerimentoRevelador * request.Yield);
            }

            News("gabinete", Fill(Content.Instruments.Texto(key), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("empresa", CompanyName(work)),
                ("semanas", work.DelayWeeks.ToString(CultureInfo.InvariantCulture)), ("sobrepreco", Texts.Percent(work.Overprice))), work.ZoneId);
        }
    }

    // ================================================================ indicação

    public double IndicationChance(InstrumentDefinition def) =>
        Math.Clamp(def.ChanceBase + (def.ChancePorRelacao * (Mandate?.MayorRelation ?? 0) / 100) + ChiefBonus, 0.05, 0.95);

    private GovFailure? CanIndicate(InstrumentDefinition def, WorkType? type, string zoneId, out string reason)
    {
        if (CanUse(def, out reason) is { } failure)
        {
            return failure;
        }

        return type is null || !(type.Pequena || type.Servico) || !Fits(type, zoneId) ? GovFailure.AlvoInvalido : null;
    }

    public GovForecast ForecastIndicacao(string typeId, string zoneId)
    {
        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Indicacao);
        WorkType? type = Content.Works.Find(typeId);
        if (CanIndicate(def, type, zoneId, out string reason) is { } failure)
        {
            return GovForecast.Block(failure, reason.Length > 0 ? reason : def.Descricao);
        }

        var factors = YieldFactors(def.Id, zoneId);
        double yield = Product(factors);
        double chance = IndicationChance(def);
        List<ForecastLine> lines = BaseLines(def, zoneId, yield);
        double later = PresenceGain(G.IndexOf(zoneId), type!.Prestigio * yield);
        lines.Add(new ForecastLine(GovForecast.PresencaDepois, zoneId, 0, later, chance * later));
        var probe = new Work { Id = "", TypeId = type.Id, ZoneId = zoneId, Origin = Work.DeIndicacao, SponsorId = RaceSetup.PlayerId, Cost = 0, Created = Week };
        List<Promise> promises = PromisesFulfilledBy(probe);
        if (promises.Count > 0)
        {
            lines.Add(new ForecastLine(GovForecast.PromessaCumprida, zoneId, 0, promises.Count, chance * promises.Count));
        }

        var explanation = new List<string>
        {
            Fill(Content.Instruments.Texto("previsao_indicacao"), ("obra", type.Nome), ("zona", ZoneName(zoneId)), ("prefeito", MayorName), ("chance", Texts.Percent(chance)),
                ("relacao", Num(Mandate?.MayorRelation ?? 0))),
        };
        explanation.AddRange(promises.Select(p => Fill(Content.Works.Texto("previsao_promessa"), ("promessa", p.Text))));
        return new GovForecast(null, null, lines,
            [new ForecastOutcome(chance, Content.Instruments.Texto("indicacao_atendida")), new ForecastOutcome(1 - chance, Content.Instruments.Texto("indicacao_negada"))], explanation)
        {
            Factors = factors,
            Remembered = promises.Select(PromiseHolder).Append(ZoneLeaderName(zoneId)).Where(n => n.Length > 0).Distinct().ToList(),
        };
    }

    /// <summary>Indicação: pede ao prefeito um serviço ou pequena obra numa zona. Atendida, entra no mapa "a pedido do vereador".</summary>
    public GovResult Indicacao(string typeId, string zoneId)
    {
        GovForecast forecast = ForecastIndicacao(typeId, zoneId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Indicacao);
        WorkType type = Content.Works.Type(typeId);
        double yield = Product(forecast.Factors);
        IntRange prazo = def.PrazoSemanas ?? new IntRange { Min = 1, Max = 3 };
        var indication = new Indication
        {
            Id = G.NewId("ind"),
            TypeId = typeId,
            ZoneId = zoneId,
            Filed = Week,
            DecideOn = Week.AddDays(7 * Rng.NextInt(prazo.Min, prazo.Max + 1)),
            Chance = IndicationChance(def),
            Yield = yield,
        };
        G.Indications.Add(indication);
        List<string> changes = ApplyBase(def, zoneId, yield);
        Spend(def, zoneId);
        RememberInZone(zoneId, "indicacao", Content.Instruments.Lembrancas["indicacao"], ("obra", type.Nome), ("zona", ZoneName(zoneId)));
        string text = Fill(Content.Instruments.Texto("indicacao_protocolada"), ("obra", type.Nome), ("zona", ZoneName(zoneId)), ("prefeito", MayorName));
        News("gabinete", text, zoneId);
        return new GovResult(null, text, changes, forecast.Factors) { CreatedId = indication.Id };
    }

    private void DecideIndications()
    {
        foreach (Indication indication in G.Indications.Where(i => i.State == IndicationState.Pendente && i.DecideOn <= Week).ToList())
        {
            WorkType type = Content.Works.Type(indication.TypeId);
            if (Rng.Chance(indication.Chance))
            {
                indication.State = IndicationState.Atendida;
                Work work = CreateWork(type, indication.ZoneId, Work.DeIndicacao, RaceSetup.PlayerId, WorkStage.Projeto, indication.Yield);
                indication.WorkId = work.Id;
                News("gabinete", Fill(Content.Instruments.Texto("indicacao_atendida"), ("obra", type.Nome), ("zona", ZoneName(indication.ZoneId)), ("prefeito", MayorName)), indication.ZoneId);
                CheckZoneWorkPromises(work);
            }
            else
            {
                indication.State = IndicationState.Negada;
                RememberInZone(indication.ZoneId, "indicacao_negada", Content.Instruments.Lembrancas["indicacao_negada"], ("obra", type.Nome), ("zona", ZoneName(indication.ZoneId)));
                News("gabinete", Fill(Content.Instruments.Texto("indicacao_negada"), ("obra", type.Nome), ("zona", ZoneName(indication.ZoneId)), ("prefeito", MayorName)), indication.ZoneId);
            }
        }
    }

    // ================================================================ fiscalização

    /// <summary>Obra contratada ou em execução, sem fiscalização aberta nem recente (a mesma obra não rende visita toda semana).</summary>
    private bool Inspectable(Work? work) =>
        work is { Stage: WorkStage.Licitacao or WorkStage.Execucao or WorkStage.Pronta }
        && !G.Inspections.Any(i => i.WorkId == work.Id && (!i.Closed || Week.DayNumber - i.Date.DayNumber < 7 * CouncilRules.SemanasEntreFiscalizacoes));

    public double DetectionChance(InstrumentDefinition def, Work work) => work.Revealed ? 1 : Math.Clamp(def.ChanceBase + ChiefBonus, 0.05, 1);

    public GovForecast ForecastFiscalizacao(string workId)
    {
        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Fiscalizacao);
        if (CanUse(def, out string reason) is { } failure)
        {
            return GovForecast.Block(failure, reason);
        }

        Work? work = G.Work(workId);
        if (!Inspectable(work))
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, def.Descricao);
        }

        var factors = YieldFactors(def.Id, work!.ZoneId);
        double yield = Product(factors);
        List<ForecastLine> lines = BaseLines(def, work.ZoneId, yield);
        double detect = DetectionChance(def, work);
        // Com a obra já revelada por requerimento, o que há para achar é conhecido; sem isso, vale a suspeita pública.
        double irregular = work.Revealed ? (work.Irregular ? 1 : 0) : Suspicion(work) * detect;
        double delay = work.DelayWeeks > 0 ? 1 - irregular : 0;
        FindingOption denounce = Content.Instruments.Finding(FindingOption.Denunciar);
        double best = PresenceGain(G.IndexOf(work.ZoneId), (def.Presenca + denounce.Presenca) * yield) - PresenceGain(G.IndexOf(work.ZoneId), def.Presenca * yield);
        double expected = (irregular * best) + (delay * best * CouncilRules.AchadoAtrasoFator);
        if (best > 0)
        {
            lines.Add(new ForecastLine(GovForecast.PresencaDepois, work.ZoneId, 0, best, expected));
            lines.Add(new ForecastLine(GovForecast.Imagem + "_later", null, 0, denounce.Imagem * yield, ((irregular * denounce.Imagem) + (delay * denounce.Imagem * CouncilRules.AchadoAtrasoFator)) * yield));
        }

        var outcomes = new List<ForecastOutcome>
        {
            new(irregular, Content.Instruments.Texto("fiscalizacao_irregular")),
            new(delay, Content.Instruments.Texto("fiscalizacao_atraso")),
            new(Math.Max(0, 1 - irregular - delay), Content.Instruments.Texto("fiscalizacao_normal")),
        };
        return new GovForecast(null, null, lines, outcomes.Where(o => o.Probability > 1e-9).ToList(),
        [
            Fill(Content.Instruments.Texto("previsao_fiscalizacao"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("empresa", CompanyName(work)), ("chance", Texts.Percent(detect))),
        ])
        { Factors = factors };
    }

    /// <summary>Fiscalização de uma obra no mapa: encontra normalidade, atraso ou irregularidade; o que fazer com o achado é outra decisão.</summary>
    public GovResult Fiscalizacao(string workId)
    {
        GovForecast forecast = ForecastFiscalizacao(workId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Fiscalizacao);
        Work work = G.Work(workId)!;
        double yield = Product(forecast.Factors);
        bool found = work.Irregular && Rng.Chance(DetectionChance(def, work));
        string finding = found ? Inspection.Irregular : work.DelayWeeks > 0 ? Inspection.Atraso : Inspection.Normal;
        if (found)
        {
            work.Revealed = true;
        }

        var inspection = new Inspection
        {
            Id = G.NewId("fisc"),
            WorkId = work.Id,
            Date = Week,
            Finding = finding,
            DecisionDeadline = finding == Inspection.Normal ? null : Week.AddDays(7 * CouncilRules.PrazoDoAchadoSemanas),
            Closed = finding == Inspection.Normal,
            Yield = yield,
        };
        G.Inspections.Add(inspection);
        List<string> changes = ApplyBase(def, work.ZoneId, yield);
        Spend(def, work.ZoneId);
        string text = Fill(Content.Instruments.Texto($"fiscalizacao_{finding}"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("empresa", CompanyName(work)),
            ("semanas", work.DelayWeeks.ToString(CultureInfo.InvariantCulture)), ("sobrepreco", Texts.Percent(work.Overprice)));
        News("gabinete", text, work.ZoneId);
        return new GovResult(null, text, changes, forecast.Factors) { CreatedId = inspection.Id };
    }

    private double FindingScale(Inspection inspection) => (inspection.Finding == Inspection.Irregular ? 1 : CouncilRules.AchadoAtrasoFator) * inspection.Yield;

    public GovForecast ForecastFinding(string inspectionId, string optionId)
    {
        Inspection? inspection = G.Inspections.FirstOrDefault(i => i.Id == inspectionId);
        FindingOption? option = Content.Instruments.Achados.FirstOrDefault(a => a.Id == optionId);
        if (inspection is not { Pending: true } || option is null || G.Work(inspection.WorkId) is not { } work)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        if (option.GanhaObra && inspection.Finding != Inspection.Irregular)
        {
            // Atraso não é segredo de ninguém: só a irregularidade dá o que negociar em silêncio.
            return GovForecast.Block(GovFailure.AlvoInvalido, Content.Instruments.Texto("achado_so_irregularidade"));
        }

        double scale = FindingScale(inspection);
        var lines = new List<ForecastLine>();
        if (option.Esqueleto > 0)
        {
            lines.Add(new ForecastLine(GovForecast.Esqueleto, null, option.Esqueleto, option.Esqueleto, option.Esqueleto));
        }

        if (option.Presenca != 0)
        {
            double gain = PresenceGain(G.IndexOf(work.ZoneId), option.Presenca * scale);
            lines.Add(new ForecastLine(GovForecast.Presenca, work.ZoneId, gain, gain, gain));
        }

        if (option.Imagem != 0)
        {
            lines.Add(new ForecastLine(GovForecast.Imagem, null, option.Imagem * scale, option.Imagem * scale, option.Imagem * scale));
        }

        if (option.RelacaoPrefeito != 0)
        {
            lines.Add(new ForecastLine(GovForecast.RelacaoPrefeito, null, option.RelacaoPrefeito, option.RelacaoPrefeito, option.RelacaoPrefeito));
        }

        var remembered = new List<string> { NameOf(G.Company(work.CompanyId)?.OwnerId) };
        if (option.Id != FindingOption.Tribunal)
        {
            remembered.Add(MayorName);
        }

        var explanation = new List<string> { option.Descricao };
        if (option.Esqueleto > 0)
        {
            explanation.Add(Fill(Content.Instruments.Texto("previsao_livro"), ("quem", Join(remembered))));
        }

        return new GovForecast(null, null, lines, [], explanation) { Remembered = remembered.Where(n => n.Length > 0).ToList() };
    }

    /// <summary>O que fazer com o achado: denunciar, negociar em silêncio ou encaminhar ao Tribunal de Contas.</summary>
    public GovResult DecideFinding(string inspectionId, string optionId)
    {
        GovForecast forecast = ForecastFinding(inspectionId, optionId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Inspection inspection = G.Inspections.First(i => i.Id == inspectionId);
        FindingOption option = Content.Instruments.Finding(optionId);
        Work work = G.Work(inspection.WorkId)!;
        string? owner = G.Company(work.CompanyId)?.OwnerId;
        double scale = FindingScale(inspection);
        var changes = new List<string>();
        inspection.Decision = option.Id;
        switch (option.Id)
        {
            case FindingOption.Tribunal:
                IntRange weeks = option.Semanas ?? new IntRange { Min = 12, Max = 20 };
                inspection.TribunalDue = Week.AddDays(7 * Rng.NextInt(weeks.Min, weeks.Max + 1));
                changes.Add(T("mud_tribunal", ("data", Texts.Date(inspection.TribunalDue.Value))));
                Remember(owner, PersonRole.Doador, "achado_tribunal", option.Lembranca, ("obra", WorkName(work)));
                break;
            case FindingOption.Negociar:
                inspection.Closed = true;
                ShiftMayorRelation(option.RelacaoPrefeito);
                Remember(Mandate?.MayorId, PersonRole.Dirigente, "achado_negociar", option.Lembranca, ("obra", WorkName(work)));
                Remember(owner, PersonRole.Doador, "achado_negociar", option.Lembranca, ("obra", WorkName(work)));
                State.Ledger.AddSkeleton("acordo_fiscalizacao", Fill(Content.Instruments.Texto("esqueleto_acordo"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId))), option.Esqueleto, Week,
                    new[] { Mandate?.MayorId, owner }.OfType<string>());
                changes.Add(T("mud_esqueleto", ("gravidade", option.Esqueleto.ToString(CultureInfo.InvariantCulture)), ("quem", Join(forecast.Remembered))));
                if (option.GanhaObra && Content.Works.Tipos.FirstOrDefault(t => t.Pequena && Fits(t, work.ZoneId)) is { } small)
                {
                    Work gift = CreateWork(small, work.ZoneId, Work.DeAcordo, RaceSetup.PlayerId, WorkStage.Projeto, inspection.Yield);
                    changes.Add(T("mud_obra_a_pedido", ("obra", small.Nome), ("zona", ZoneName(work.ZoneId))));
                    changes.AddRange(CheckZoneWorkPromises(gift));
                }

                break;
            default:
                inspection.Closed = true;
                work.Denounced = true;
                ShiftMayorRelation(option.RelacaoPrefeito);
                ShiftImage(option.Imagem * scale);
                changes.Add(ChPresence(work.ZoneId, GainPresence(G.IndexOf(work.ZoneId), option.Presenca * scale)));
                changes.Add(ChImage(option.Imagem * scale));
                changes.Add(ChRelation(MayorName, option.RelacaoPrefeito));
                Remember(Mandate?.MayorId, PersonRole.Dirigente, "achado_denunciar", option.Lembranca, ("obra", WorkName(work)));
                Remember(owner, PersonRole.Doador, "achado_denunciar", option.Lembranca, ("obra", WorkName(work)));
                ShiftApproval(-Content.Chamber.Regras.CpiAprovacaoPorSemana * scale);
                if (inspection.Finding == Inspection.Irregular)
                {
                    // A denúncia de irregularidade numa contratação é a causa de um escândalo no governo.
                    RaiseCrisisFromWork(work);
                }

                break;
        }

        string text = Fill(Content.Instruments.Texto($"achado_{option.Id}"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("empresa", CompanyName(work)), ("prefeito", MayorName));
        News("gabinete", text, work.ZoneId);
        return new GovResult(null, text, changes, []);
    }

    private void InspectionsWeek()
    {
        foreach (Inspection inspection in G.Inspections.Where(i => !i.Closed).ToList())
        {
            if (inspection.Pending && inspection.DecisionDeadline <= Week)
            {
                // Sem decisão no prazo, o achado segue para o Tribunal de Contas.
                DecideFinding(inspection.Id, FindingOption.Tribunal);
            }
            else if (inspection.TribunalDue is { } due && due <= Week && G.Work(inspection.WorkId) is { } work)
            {
                FindingOption option = Content.Instruments.Finding(FindingOption.Tribunal);
                double scale = FindingScale(inspection);
                inspection.Closed = true;
                ShiftImage(option.Imagem * scale);
                GainPresence(G.IndexOf(work.ZoneId), option.Presenca * scale);
                News("gabinete", Fill(Content.Instruments.Texto("tribunal_resultado"), ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)), ("empresa", CompanyName(work))), work.ZoneId);
            }
        }
    }

    // ================================================================ emenda

    private GovFailure? CanAmend(InstrumentDefinition def, WorkType? type, string zoneId, out string reason)
    {
        if (CanUse(def, out reason) is { } failure)
        {
            return failure;
        }

        if (Mandate!.EmendasLeft <= 0)
        {
            return GovFailure.SemEmenda;
        }

        return type is null || !type.Emenda || !Fits(type, zoneId) ? GovFailure.AlvoInvalido : null;
    }

    public GovForecast ForecastEmenda(string typeId, string zoneId)
    {
        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Emenda);
        WorkType? type = Content.Works.Find(typeId);
        if (CanAmend(def, type, zoneId, out string reason) is { } failure)
        {
            return GovForecast.Block(failure, reason.Length > 0 ? reason : def.Descricao);
        }

        var factors = YieldFactors(def.Id, zoneId);
        double yield = Product(factors);
        List<ForecastLine> lines = BaseLines(def, zoneId, yield);
        double later = PresenceGain(G.IndexOf(zoneId), type!.Prestigio * yield);
        lines.Add(new ForecastLine(GovForecast.PresencaDepois, zoneId, later * WorkRules.PrestigioSemCerimonia, later, later));
        var probe = new Work { Id = "", TypeId = type.Id, ZoneId = zoneId, Origin = Work.DeEmenda, SponsorId = RaceSetup.PlayerId, Cost = 0, Created = Week };
        List<Promise> promises = PromisesFulfilledBy(probe);
        int deals = G.Deals.Count(d => d.ZoneId == zoneId && State.Ledger.Promises.Any(p => p.Id == d.PromiseId && p.State == PromiseState.Aberta));
        if (promises.Count + deals > 0)
        {
            lines.Add(new ForecastLine(GovForecast.PromessaCumprida, zoneId, deals, promises.Count + deals, promises.Count + deals));
        }

        double release = Math.Clamp(WorkRules.ChanceLiberacaoEmenda + (WorkRules.LiberacaoPorRelacao * Mandate!.MayorRelation / 100), 0.01, 1);
        var explanation = new List<string>
        {
            Fill(Content.Instruments.Texto("previsao_emenda"), ("obra", type.Nome), ("zona", ZoneName(zoneId)), ("restam", (Mandate.EmendasLeft - 1).ToString(CultureInfo.InvariantCulture)),
                ("chance", Texts.Percent(release)), ("prefeito", MayorName), ("limite", WorkRules.SemanasLiberacaoObrigatoria.ToString(CultureInfo.InvariantCulture))),
        };
        explanation.AddRange(promises.Select(p => Fill(Content.Works.Texto("previsao_promessa"), ("promessa", p.Text))));
        return new GovForecast(null, null, lines, [], explanation)
        {
            Factors = factors,
            Remembered = promises.Select(PromiseHolder).Append(ZoneLeaderName(zoneId)).Where(n => n.Length > 0).Distinct().ToList(),
        };
    }

    /// <summary>Emenda: destina uma obra a uma zona. Entra no mapa com prazo; a liberação depende do Executivo; a entrega é uma inauguração.</summary>
    public GovResult Emenda(string typeId, string zoneId)
    {
        GovForecast forecast = ForecastEmenda(typeId, zoneId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Emenda);
        WorkType type = Content.Works.Type(typeId);
        double yield = Product(forecast.Factors);
        Mandate!.EmendasLeft--;
        List<string> changes = ApplyBase(def, zoneId, yield);
        Spend(def, zoneId);
        Work work = CreateWork(type, zoneId, Work.DeEmenda, RaceSetup.PlayerId, WorkStage.Liberacao, yield);
        RememberInZone(zoneId, "emenda", Content.Instruments.Lembrancas["emenda"], ("obra", type.Nome), ("zona", ZoneName(zoneId)));
        changes.AddRange(CheckZoneWorkPromises(work));
        string text = Fill(Content.Works.Texto("emenda_destinada"), ("obra", type.Nome), ("zona", ZoneName(zoneId)));
        News("gabinete", text, zoneId);
        return new GovResult(null, text, changes, forecast.Factors) { CreatedId = work.Id };
    }

    // ================================================================ projeto de lei

    public IEnumerable<BillTemplate> ProposableBills() => Mandate is null ? [] :
        Context.Legislature.MunicipalBills.Bills.Where(t => t.Author == BillAuthorKind.Vereador && !Mandate.Bills.Any(b => b.TemplateId == t.Id && b.Stage is BillStage.Comissao or BillStage.Plenario));

    public GovForecast ForecastProjeto(string templateId)
    {
        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Projeto);
        if (CanUse(def, out string reason) is { } failure)
        {
            return GovForecast.Block(failure, reason);
        }

        BillTemplate? template = ProposableBills().FirstOrDefault(t => t.Id == templateId);
        if (template is null)
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, def.Descricao);
        }

        LegislatureConfig config = Context.Legislature.Config;
        var factors = YieldFactors(def.Id, null);
        double yield = Product(factors);
        List<ForecastLine> lines = BaseLines(def, null, yield);
        double committee = config.CommitteePassChance + (config.CommitteePassPerRelation * Members.Where(m => !m.IsPlayer).Select(m => m.Relation).DefaultIfEmpty().Average()) + G.CommitteeBonus;
        if (template.Theme is not null && config.Committees.FirstOrDefault(c => c.Id == Mandate!.Committee)?.Themes.Contains(template.Theme) == true)
        {
            committee += config.CommitteeMemberBonus;
        }

        committee = Math.Clamp(committee, 0, 1);
        lines.Add(new ForecastLine(GovForecast.Capital + "_later", null, 0, config.Capital.OwnBillApproved, committee * 0.5 * config.Capital.OwnBillApproved));
        return new GovForecast(null, null, lines, [new ForecastOutcome(committee, T("comissao_passa")), new ForecastOutcome(1 - committee, T("comissao_arquiva"))],
        [
            Fill(Content.Instruments.Texto("previsao_projeto"), ("projeto", template.Title), ("chance", Texts.Percent(committee)), ("min", config.CommitteeWeeks.Min.ToString(CultureInfo.InvariantCulture)),
                ("max", config.CommitteeWeeks.Max.ToString(CultureInfo.InvariantCulture))),
        ])
        { Factors = factors };
    }

    /// <summary>Projeto de lei: protocola um dos modelos; a tramitação é a do mandato (comissão e plenário).</summary>
    public GovResult Projeto(string templateId)
    {
        GovForecast forecast = ForecastProjeto(templateId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.Projeto);
        BillTemplate template = Context.Legislature.MunicipalBills.Find(templateId)!;
        double yield = Product(forecast.Factors);
        List<string> changes = ApplyBase(def, null, yield);
        Spend(def, null);
        Bill bill = LegislatureEngine.FileBill(Mandate!, Context.Legislature.Config, template, RaceSetup.PlayerId, Week, Rng);
        Mandate!.News.Add(new MandateNews(Week, MandateNewsKind.ProjetoProtocolado, bill.Title, PlayerAuthor: true));
        FulfilByTheme(template.Theme);
        string text = Fill(Content.Instruments.Texto("projeto_protocolado"), ("projeto", template.Title), ("data", Texts.Date(bill.CommitteeUntil)));
        News("camara", text);
        return new GovResult(null, text, changes, forecast.Factors) { CreatedId = bill.Id };
    }

    // ================================================================ contagem nominal e instrução de voto

    /// <summary>Contagem nominal de um projeto em tramitação, com o que cada indeciso quer.</summary>
    public NominalCount? CountBill(string billId)
    {
        if (Role != GovRole.Vereador || Mandate is not { } mandate || mandate.Bills.FirstOrDefault(b => b.Id == billId) is not { } bill)
        {
            return null;
        }

        BillTemplate template = Context.Legislature.MunicipalBills.Find(bill.TemplateId)!;
        LegislatureConfig config = Context.Legislature.Config;
        ChamberMember player = mandate.Player;
        int playerSign = PlannedVote(bill) switch { VoteChoice.Sim => 1, VoteChoice.Nao => -1, _ => 0 };
        var lines = new List<CountLine>();
        foreach (ChamberMember m in mandate.Members.Where(m => !m.IsPlayer))
        {
            int side = bill.AuthorKind != BillAuthorKind.Executivo || m.PartyId is null ? 0 : mandate.InBase(m.PartyId) ? 1 : -1;
            int orientation = player.Leader && m.PartyId == player.PartyId && player.PartyId is not null
                ? playerSign
                : ChamberVote.PartyOrientation(config, State, m.PartyId, template.Position, side);
            double relation = bill.AuthorId == RaceSetup.PlayerId ? m.Relation : 0;
            lines.Add(Line(m, bill.Id, template.Position, orientation, side, relation, GovernanceContent.PapelVereador));
        }

        int yes = lines.Count(l => l.Lean == NominalCount.Sim) + (playerSign > 0 ? 1 : 0);
        int no = lines.Count(l => l.Lean == NominalCount.Nao) + (playerSign < 0 ? 1 : 0);
        return new NominalCount(bill.Id, bill.Title, template.Quorum, yes, no, lines.Count(l => l.Lean == NominalCount.Indeciso), mandate.Members.Count, lines);
    }

    private CountLine Line(ChamberMember m, string subjectId, Ideology.IdeologicalPosition position, int orientation, int side, double relation, string papel)
    {
        LegislatureConfig config = Context.Legislature.Config;
        LegislatureVoteConfig w = config.Vote;
        double ideology = w.IdeologyWeight * ChamberVote.Alignment(w, m.Position, position);
        double party = w.OrientationWeight * orientation * ChamberVote.Discipline(config, State, m.PartyId);
        double government = w.GovernmentWeight * side;
        double personal = w.RelationWeight * relation / 100;
        double deal = G.DealLogit(subjectId, m.Id);
        double logit = ideology + party + government + personal + deal;
        double p = w.NoiseSd <= 1e-9 ? (logit > 0 ? 1 : 0) : Phi(logit / w.NoiseSd);
        string lean = p >= CouncilRules.ContagemCerteza ? NominalCount.Sim : p <= 1 - CouncilRules.ContagemCerteza ? NominalCount.Nao : NominalCount.Indeciso;
        string? request = null;
        string reason;
        if (Math.Abs(deal) > 1e-9)
        {
            reason = Content.Instruments.Texto("motivo_acordo");
        }
        else if (lean == NominalCount.Indeciso && RequestOf(subjectId, m.Id, papel) is { } wanted)
        {
            request = RequestText(wanted.Request, wanted.ZoneId, wanted.BillId);
            reason = Fill(Content.Instruments.Texto("motivo_pedido"), ("pedido", request));
        }
        else
        {
            // O motivo é o termo que mais pesa no voto dele.
            var terms = new (double Value, string Key)[]
            {
                (Math.Abs(party), "motivo_lider"), (Math.Abs(government), government >= 0 ? "motivo_governo" : "motivo_oposicao"), (Math.Abs(ideology), "motivo_ideologia"), (Math.Abs(personal), "motivo_relacao"),
            };
            reason = Content.Instruments.Texto(terms.MaxBy(t => t.Value).Key);
        }

        return new CountLine(m.Id, m.Name, m.PartyId, lean, p, reason, request);
    }

    /// <summary>O voto que o jogador dará se nada mudar: o marcado, ou o da instrução permanente.</summary>
    public VoteChoice PlannedVote(Bill bill)
    {
        if (bill.PlayerVote is { } marked)
        {
            return marked;
        }

        return InstructionVote(State, Context.Legislature, bill) ?? VoteChoice.Ausente;
    }

    /// <summary>
    /// Voto pela instrução permanente (GDD 17.14): seguir a orientação do partido ou seguir a própria plataforma. Null
    /// sem instrução. Usado pelo motor do mandato quando o jogador não marcou o voto.
    /// </summary>
    public static VoteChoice? InstructionVote(State.GameState state, LegislatureContent content, Bill bill) =>
        state.Governance is { } governance ? InstructionVote(state, content, bill, governance.Instruction) : null;

    /// <summary>O voto que a instrução <paramref name="instruction"/> daria (a previsão de trocar de instrução usa a mesma regra).</summary>
    private static VoteChoice? InstructionVote(State.GameState state, LegislatureContent content, Bill bill, VoteInstruction instruction)
    {
        if (instruction == VoteInstruction.Nenhuma || state.Governance is not { } g || state.Mandate is not { } mandate || content.MunicipalBills.Find(bill.TemplateId) is not { } template)
        {
            return null;
        }

        // Acordo de voto fechado sobre este projeto do colega prevalece: a palavra dada está no Livro.
        if (g.Deals.Any(d => d.SupportBillId == bill.Id && state.Ledger.Promises.Any(p => p.Id == d.PromiseId && p.State == PromiseState.Aberta)))
        {
            return VoteChoice.Sim;
        }

        ChamberMember player = mandate.Player;
        VoteChoice platform = ChamberVote.Alignment(content.Config.Vote, player.Position, template.Position) >= 0 ? VoteChoice.Sim : VoteChoice.Nao;
        if (instruction == VoteInstruction.SeguirPlataforma || player.Leader || player.PartyId is null)
        {
            return platform;
        }

        int side = bill.AuthorKind != BillAuthorKind.Executivo ? 0 : mandate.InBase(player.PartyId) ? 1 : -1;
        return ChamberVote.PartyOrientation(content.Config, state, player.PartyId, template.Position, side) switch { 1 => VoteChoice.Sim, -1 => VoteChoice.Nao, _ => platform };
    }

    /// <summary>
    /// Previsão de trocar a instrução permanente de voto: o que ela faz e quantos votos da pauta desta semana (sem voto
    /// marcado) mudam com ela. Nada muda nos números agora; o custo é o de cada voto dado.
    /// </summary>
    public GovForecast ForecastInstruction(VoteInstruction instruction)
    {
        if (Role != GovRole.Vereador || Mandate is not { } mandate)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        var explanation = new List<string>
        {
            Content.Instruments.Texto(instruction switch
            {
                VoteInstruction.SeguirOrientacao => "previsao_instrucao_orientacao",
                VoteInstruction.SeguirPlataforma => "previsao_instrucao_plataforma",
                _ => "previsao_instrucao_nenhuma",
            }),
        };
        List<Bill> changed = mandate.Bills.Where(b => b.Stage == BillStage.Plenario && b.CommitteeUntil <= Week && b.PlayerVote is null)
            .Where(b => (InstructionVote(State, Context.Legislature, b, G.Instruction) ?? VoteChoice.Ausente) != (InstructionVote(State, Context.Legislature, b, instruction) ?? VoteChoice.Ausente))
            .ToList();
        explanation.Add(changed.Count == 0 ? Content.Instruments.Texto("previsao_instrucao_igual")
            : Fill(Content.Instruments.Texto("previsao_instrucao_pauta"), ("n", changed.Count.ToString(CultureInfo.InvariantCulture)), ("projetos", string.Join("; ", changed.Select(b => b.Title)))));
        return new GovForecast(null, null, [], [], explanation);
    }

    public GovResult SetInstruction(VoteInstruction instruction)
    {
        GovForecast forecast = ForecastInstruction(instruction);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        G.Instruction = instruction;
        string text = Fill(Content.Instruments.Texto("instrucao"), ("instrucao", T(instruction switch
        {
            VoteInstruction.SeguirOrientacao => "instrucao_orientacao",
            VoteInstruction.SeguirPlataforma => "instrucao_plataforma",
            _ => "instrucao_nenhuma",
        })));
        News("camara", text);
        return new GovResult(null, text, [], []);
    }

    /// <summary>
    /// Votações da pauta em que orientação, plataforma, acordo ou base entram em conflito (GDD 17.14): é só nelas que
    /// o jogo pausa quando há instrução permanente.
    /// </summary>
    public IReadOnlyList<(Bill Bill, IReadOnlyList<string> Reasons)> VoteConflicts()
    {
        var list = new List<(Bill, IReadOnlyList<string>)>();
        if (Role != GovRole.Vereador || Mandate is not { } mandate)
        {
            return list;
        }

        LegislatureConfig config = Context.Legislature.Config;
        ChamberMember player = mandate.Player;
        foreach (Bill bill in mandate.Bills.Where(b => b.Stage == BillStage.Plenario && b.CommitteeUntil <= Week && b.PlayerVote is null))
        {
            BillTemplate template = Context.Legislature.MunicipalBills.Find(bill.TemplateId)!;
            var reasons = new List<string>();
            int platform = ChamberVote.Alignment(config.Vote, player.Position, template.Position) >= 0 ? 1 : -1;
            int side = bill.AuthorKind != BillAuthorKind.Executivo ? 0 : mandate.InBase(player.PartyId) ? 1 : -1;
            int orientation = player.Leader || player.PartyId is null ? 0 : ChamberVote.PartyOrientation(config, State, player.PartyId, template.Position, side);
            if (orientation != 0 && orientation != platform)
            {
                reasons.Add(Content.Instruments.Texto("conflito_orientacao"));
            }

            // A base: os grupos em que o jogador tem apoio reagem ao voto.
            double baseReaction = template.Popularity.Sum(p => p.Value * mandate.Support.GetValueOrDefault(p.Key));
            int planned = PlannedVote(bill) switch { VoteChoice.Sim => 1, VoteChoice.Nao => -1, _ => 0 };
            if (planned != 0 && baseReaction * planned < -1e-9)
            {
                reasons.Add(Content.Instruments.Texto("conflito_base"));
            }

            if (G.Deals.Any(d => d.SupportBillId == bill.Id && !d.Voted) && planned < 0)
            {
                reasons.Add(Content.Instruments.Texto("conflito_acordo"));
            }

            if (reasons.Count > 0)
            {
                list.Add((bill, reasons));
            }
        }

        return list;
    }

    // ================================================================ negociar voto

    /// <summary>
    /// O que o vereador quer em troca do voto neste assunto. A escolha é estável (não gasta o Rng): o mesmo vereador
    /// pede a mesma coisa enquanto o assunto estiver em pauta.
    /// </summary>
    public (VoteRequest Request, string? ZoneId, string? BillId)? RequestOf(string subjectId, string memberId, string papel)
    {
        GovernanceState g = G;
        if (Member(memberId) is not { IsPlayer: false } member)
        {
            return null;
        }

        Bill? own = Mandate?.Bills.FirstOrDefault(b => b.AuthorId == memberId && b.Id != subjectId && b.Stage is BillStage.Comissao or BillStage.Plenario);
        bool payrollBlocked = Role == GovRole.Prefeito && Context.FiscalLaw.VedadoAumentar(g.Payroll.Share);
        List<VoteRequest> options = Content.Instruments.Pedidos.Where(p => p.Papeis.Contains(papel) && p.Weight > 0)
            .Where(p => p.Id != VoteRequest.ApoioProjeto || own is not null)
            .Where(p => p.Id != VoteRequest.Cargo || !payrollBlocked)
            .ToList();
        if (options.Count == 0)
        {
            return null;
        }

        double total = options.Sum(o => o.Weight);
        double target = (StableHash(memberId + "|" + subjectId) % 10_000) / 10_000.0 * total;
        VoteRequest chosen = options[^1];
        foreach (VoteRequest option in options)
        {
            if (target < option.Weight)
            {
                chosen = option;
                break;
            }

            target -= option.Weight;
        }

        string? zone = chosen.Id is VoteRequest.EmendaReduto or VoteRequest.ObraReduto or VoteRequest.LiberarEmenda ? g.MemberZones.GetValueOrDefault(member.Id) : null;
        return (chosen, zone, chosen.Id == VoteRequest.ApoioProjeto ? own!.Id : null);
    }

    private string RequestText(VoteRequest request, string? zoneId, string? billId) =>
        Fill(request.Texto, ("zona", ZoneName(zoneId)), ("projeto", Mandate?.Bills.FirstOrDefault(b => b.Id == billId)?.Title ?? ""));

    private GovFailure? CanNegotiate(InstrumentDefinition def, NominalCount? count, string memberId, out string reason)
    {
        reason = "";
        if (Role == GovRole.Vereador)
        {
            if (CanUse(def, out reason) is { } failure)
            {
                return failure;
            }
        }
        else if (Capital < def.Capital)
        {
            reason = Fill(Content.Instruments.Texto("sem_capital"), ("capital", def.Capital.ToString(CultureInfo.InvariantCulture)));
            return GovFailure.SemCapital;
        }

        if (count?.Lines.FirstOrDefault(l => l.MemberId == memberId) is not { Lean: NominalCount.Indeciso, RequestText: not null })
        {
            return GovFailure.AlvoInvalido;
        }

        if (G.NoDealUntil.TryGetValue(memberId, out DateOnly until) && until > Week)
        {
            reason = Fill(Content.Instruments.Texto("acordo_quebrado"), ("nome", NameOf(memberId)));
            return GovFailure.Bloqueado;
        }

        return null;
    }

    private GovForecast ForecastDeal(NominalCount? count, string subjectId, string memberId, int side, string papel)
    {
        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.NegociarVoto);
        if (CanNegotiate(def, count, memberId, out string reason) is { } failure)
        {
            return GovForecast.Block(failure, reason.Length > 0 ? reason : def.Descricao);
        }

        var (request, zoneId, billId) = RequestOf(subjectId, memberId, papel)!.Value;
        MemoryDefinition memory = Content.Instruments.Lembrancas["acordo"];
        var lines = new List<ForecastLine>
        {
            new(GovForecast.Capital, null, -def.Capital, -def.Capital, -def.Capital),
            new(GovForecast.Relacao, memberId, memory.Pontos, memory.Pontos, memory.Pontos),
        };
        if (Role == GovRole.Vereador)
        {
            lines.Add(new ForecastLine(GovForecast.Turnos, null, -def.Turnos, -def.Turnos, -def.Turnos));
        }

        if (request.Id == VoteRequest.Cargo && Role == GovRole.Prefeito)
        {
            lines.Add(new ForecastLine(GovForecast.Folha, null, CouncilRules.CargoFolha, CouncilRules.CargoFolha, CouncilRules.CargoFolha));
        }

        if (Role == GovRole.Vereador && count is not null && Mandate?.Bills.FirstOrDefault(b => b.Id == subjectId) is { AuthorId: RaceSetup.PlayerId })
        {
            // Projeto próprio: o voto que falta aproxima o capital político que a aprovação rende.
            int own = Context.Legislature.Config.Capital.OwnBillApproved;
            lines.Add(new ForecastLine(GovForecast.Capital + "_later", null, 0, own, own / (double)Math.Max(1, count.MissingToApprove)));
        }

        string name = NameOf(memberId);
        return new GovForecast(null, null, lines, [],
        [
            Fill(Content.Instruments.Texto("previsao_negociar"), ("nome", name), ("pedido", RequestText(request, zoneId, billId)), ("voto", Vote(side)),
                ("semanas", request.PrazoSemanas.ToString(CultureInfo.InvariantCulture)), ("faltam", (count?.MissingToApprove ?? 0).ToString(CultureInfo.InvariantCulture))),
            Fill(Content.Instruments.Texto("previsao_quem_lembra"), ("nome", name)),
        ])
        { Remembered = [name] };
    }

    public GovForecast ForecastNegociarVoto(string billId, string memberId, int side) =>
        ForecastDeal(CountBill(billId), billId, memberId, side, GovernanceContent.PapelVereador);

    /// <summary>Negociar voto com um vereador indeciso: ele tem um pedido; aceitar põe a promessa no Livro e garante o voto.</summary>
    public GovResult NegociarVoto(string billId, string memberId, int side) =>
        CloseDeal(ForecastNegociarVoto(billId, memberId, side), billId, memberId, side, GovernanceContent.PapelVereador);

    private GovResult CloseDeal(GovForecast forecast, string subjectId, string memberId, int side, string papel)
    {
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        GovernanceState g = G;
        InstrumentDefinition def = Content.Instruments.Instrument(InstrumentDefinition.NegociarVoto);
        var (request, zoneId, billId) = RequestOf(subjectId, memberId, papel)!.Value;
        string name = NameOf(memberId);
        string pedido = RequestText(request, zoneId, billId);
        if (Role == GovRole.Vereador)
        {
            Spend(def, null);
        }
        else
        {
            Capital -= def.Capital;
        }

        Promise promise = State.Ledger.AddPromise(Promise.ParaPessoa, memberId, "voto_" + request.Id, Fill(Content.Instruments.Texto("acordo_livro"), ("nome", name), ("pedido", pedido)), zoneId, Week,
            Week.AddDays(7 * request.PrazoSemanas));
        var deal = new VoteDeal
        {
            Id = g.NewId("acordo"),
            SubjectId = subjectId,
            MemberId = memberId,
            Side = side >= 0 ? 1 : -1,
            Logit = (side >= 0 ? 1 : -1) * CouncilRules.AcordoLogito,
            RequestId = request.Id,
            ZoneId = zoneId,
            SupportBillId = billId,
            PromiseId = promise.Id,
            Date = Week,
        };
        g.Deals.Add(deal);
        Remember(memberId, PersonRole.Vereador, "acordo", Content.Instruments.Lembrancas["acordo"], ("pedido", pedido));
        var changes = new List<string>
        {
            T("mud_voto", ("nome", name), ("voto", Vote(deal.Side))), T("mud_promessa", ("data", Texts.Date(promise.Deadline)), ("promessa", pedido)), ChCapital(-def.Capital),
        };
        if (request.Id == VoteRequest.Cargo && Role == GovRole.Prefeito)
        {
            // Cargo para o indicado: sai da folha na hora e a promessa fica cumprida.
            g.Payroll.Share += CouncilRules.CargoFolha;
            promise.State = PromiseState.Cumprida;
            changes.Add(Fill(Content.Works.Texto("previsao_folha"), ("folha", Pct(CouncilRules.CargoFolha))));
        }
        else if (request.Id == VoteRequest.Cargo)
        {
            // O indicado dele entra na lista de candidatos ao gabinete.
            Person nominee = NewPerson(PersonRole.Assessor, 22, 55, Member(memberId)?.PartyId);
            g.Aides.Add(new Aide { PersonId = nominee.Id, Origin = Aide.DeVereador, NomineeOf = memberId, Quality = Rng.NextInt(1, 4) });
        }

        string text = Fill(Content.Instruments.Texto("acordo_fechado"), ("nome", name), ("pedido", pedido));
        News("camara", text, zoneId, memberId);
        return new GovResult(null, text, changes, []) { CreatedId = deal.Id };
    }

    /// <summary>Confere os acordos: voto dado, pedido cumprido ou prazo vencido. Cumprir ou não fica na memória dele.</summary>
    private void DealsWeek()
    {
        GovernanceState g = G;
        foreach (VoteDeal deal in g.Deals)
        {
            if (!deal.Voted && Role == GovRole.Vereador && Mandate?.Bills.FirstOrDefault(b => b.Id == deal.SubjectId) is { Decided: not null })
            {
                deal.Voted = true;
            }

            if (State.Ledger.Promises.FirstOrDefault(p => p.Id == deal.PromiseId) is not { State: PromiseState.Aberta } promise)
            {
                continue;
            }

            if (deal.SupportBillId is not null && Mandate?.Bills.FirstOrDefault(b => b.Id == deal.SupportBillId) is { Decided: not null } supported)
            {
                // O projeto dele foi votado (ou arquivado): valeu o voto que o jogador deu.
                bool kept = supported.Stage == BillStage.Arquivado || supported.PlayerVote == VoteChoice.Sim;
                SettleDeal(deal, promise, kept);
            }
            else if (promise.Deadline < Week)
            {
                SettleDeal(deal, promise, kept: false);
            }
        }
    }

    private void SettleDeal(VoteDeal deal, Promise promise, bool kept)
    {
        promise.State = kept ? PromiseState.Cumprida : PromiseState.Quebrada;
        string key = kept ? "acordo_cumprido" : "acordo_quebrado";
        Remember(deal.MemberId, PersonRole.Vereador, key, Content.Instruments.Lembrancas[key]);
        if (!kept)
        {
            G.NoDealUntil[deal.MemberId] = Week.AddDays(7 * CouncilRules.SemanasSemNegociarDepoisDeEnganado);
        }

        News("camara", Fill(Content.Instruments.Texto(key), ("nome", NameOf(deal.MemberId))), deal.ZoneId, deal.MemberId);
    }

    /// <summary>Obra nova numa zona cumpre os acordos de emenda ou obra no reduto fechados com vereadores dali.</summary>
    private List<string> CheckZoneWorkPromises(Work work)
    {
        var changes = new List<string>();
        bool mine = work.SponsorId == RaceSetup.PlayerId || (Role == GovRole.Prefeito && work.SponsorId == Work.Prefeito);
        if (!mine)
        {
            return changes;
        }

        foreach (VoteDeal deal in G.Deals.Where(d => d.ZoneId == work.ZoneId && d.Date <= work.Created))
        {
            if (State.Ledger.Promises.FirstOrDefault(p => p.Id == deal.PromiseId) is { State: PromiseState.Aberta } promise)
            {
                SettleDeal(deal, promise, kept: true);
                changes.Add(T("mud_acordo_cumprido", ("nome", NameOf(deal.MemberId))));
            }
        }

        return changes;
    }

    // ================================================================ gabinete

    public int CabinetSlots => CouncilRules.GabinetePorPorte[Porte];

    /// <summary>
    /// Candidatos ao gabinete na posse: quem trabalhou na campanha, os indicados das lideranças a quem se prometeu
    /// vaga, um parente e nomes de mercado. Nomear paga dívidas; quem fica de fora lembra.
    /// </summary>
    private void BuildAidePool()
    {
        GovernanceState g = G;
        foreach (StaffMember staff in City.Staff.Where(s => s.Hired && !s.Left))
        {
            g.Aides.Add(new Aide { PersonId = staff.PersonId, Origin = Aide.DaCampanha, Quality = staff.Quality });
            PersonOf(staff.PersonId, PersonRole.Assessor);
        }

        foreach (Promise promise in State.Ledger.Open.Where(p => p.ToKind == Promise.ParaPessoa && Content.Collection.PromessaDeCargo.Contains(p.Object)))
        {
            if (City.Leader(promise.ToId) is { } leader && !g.Aides.Any(a => a.NomineeOf == leader.PersonId))
            {
                Person nominee = NewPerson(PersonRole.Assessor, 22, 55, null, leader.Zones[0]);
                g.Aides.Add(new Aide { PersonId = nominee.Id, Origin = Aide.DeLideranca, NomineeOf = leader.PersonId, Quality = Rng.NextInt(1, 5) });
            }
        }

        Person relative = NewPerson(PersonRole.Assessor, 20, 60, null);
        g.Aides.Add(new Aide { PersonId = relative.Id, Origin = Aide.Parente, Quality = Rng.NextInt(1, 4) });
        for (int i = 0; i < CabinetSlots * CouncilRules.CandidatosPorVaga; i++)
        {
            Person person = NewPerson(PersonRole.Assessor, 22, 60, null);
            g.Aides.Add(new Aide { PersonId = person.Id, Origin = Aide.DeMercado, Quality = Rng.PickWeighted([1, 2, 3, 4, 5], q => q is 1 or 5 ? 1.0 : q == 3 ? 3.0 : 2.0) });
        }
    }

    private GovFailure? CanAppoint(Aide? aide, AideRole? role, string? zoneId, out string reason)
    {
        reason = "";
        GovernanceState g = G;
        if (Role != GovRole.Vereador)
        {
            return GovFailure.PapelErrado;
        }

        if (aide is null || role is null || aide.Appointed || aide.Left)
        {
            return GovFailure.NaoEncontrado;
        }

        if (g.Cabinet.Count() >= CabinetSlots || g.Cabinet.Count(a => a.Role == role.Id) >= role.Maximo)
        {
            return GovFailure.SemVaga;
        }

        if (role.Id == AideRole.Base && (zoneId is null || g.IndexOf(zoneId) < 0 || g.Cabinet.Any(a => a.Role == AideRole.Base && a.ZoneId == zoneId)))
        {
            return GovFailure.AlvoInvalido;
        }

        return null;
    }

    public GovForecast ForecastAppointAide(string personId, string roleId, string? zoneId = null)
    {
        GovernanceState g = G;
        Aide? aide = g.Aides.FirstOrDefault(a => a.PersonId == personId);
        AideRole? role = Content.Instruments.Funcoes.FirstOrDefault(f => f.Id == roleId);
        if (CanAppoint(aide, role, zoneId, out string reason) is { } failure)
        {
            return GovForecast.Block(failure, reason);
        }

        var explanation = new List<string> { role!.Descricao };
        var remembered = new List<string> { NameOf(personId) };
        var lines = new List<ForecastLine>();
        MemoryDefinition memory = Content.Instruments.Lembrancas["nomeado"];
        lines.Add(new ForecastLine(GovForecast.Relacao, personId, memory.Pontos, memory.Pontos, memory.Pontos));
        int paid = AidePromises(aide!).Count;
        if (paid > 0)
        {
            lines.Add(new ForecastLine(GovForecast.PromessaCumprida, null, paid, paid, paid));
            explanation.AddRange(AidePromises(aide!).Select(p => Fill(Content.Works.Texto("previsao_promessa"), ("promessa", p.Text))));
        }

        if (aide!.NomineeOf is { } patron)
        {
            remembered.Add(NameOf(patron));
        }

        if (aide.Origin == Aide.Parente)
        {
            explanation.Add(Fill(Content.Instruments.Texto("esqueleto_nepotismo"), ("nome", NameOf(personId))));
        }

        if (g.Cabinet.Count() + 1 >= CabinetSlots)
        {
            List<string> left = g.Aides.Where(a => !a.Appointed && !a.Left && !a.PassedOver && a.PersonId != personId && a.Origin is Aide.DaCampanha or Aide.DeLideranca or Aide.DeVereador)
                .Select(a => NameOf(a.PersonId)).ToList();
            if (left.Count > 0)
            {
                explanation.Add(Fill(Content.Instruments.Texto("assessor_preterido"), ("nomes", string.Join(", ", left))));
                remembered.AddRange(left);
            }
        }

        return new GovForecast(null, null, lines, [], explanation) { Remembered = remembered.Where(n => n.Length > 0).Distinct().ToList() };
    }

    private List<Promise> AidePromises(Aide aide) => State.Ledger.Open
        .Where(p => p.ToKind == Promise.ParaPessoa && (p.ToId == aide.PersonId || p.ToId == aide.NomineeOf)
            && (Content.Collection.PromessaDeCargo.Contains(p.Object) || p.Object == "voto_" + VoteRequest.Cargo))
        .ToList();

    /// <summary>Nomeia um assessor: paga a promessa de vaga, se havia; parente vira esqueleto; com o gabinete cheio, quem ficou de fora lembra.</summary>
    public GovResult AppointAide(string personId, string roleId, string? zoneId = null)
    {
        GovForecast forecast = ForecastAppointAide(personId, roleId, zoneId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        GovernanceState g = G;
        Aide aide = g.Aides.First(a => a.PersonId == personId);
        AideRole role = Content.Instruments.Role(roleId);
        aide.Appointed = true;
        aide.Role = roleId;
        aide.ZoneId = roleId == AideRole.Base ? zoneId : null;
        aide.Since = Week;
        aide.LastSupported = Week;
        var changes = new List<string>();
        Remember(personId, PersonRole.Assessor, "nomeado", Content.Instruments.Lembrancas["nomeado"], ("funcao", role.Nome));
        foreach (Promise promise in AidePromises(aide))
        {
            changes.AddRange(Fulfil(promise));
            if (g.Deals.FirstOrDefault(d => d.PromiseId == promise.Id) is { } deal)
            {
                Remember(deal.MemberId, PersonRole.Vereador, "acordo_cumprido", Content.Instruments.Lembrancas["acordo_cumprido"]);
            }
        }

        changes.AddRange(SettleDebts(personId));
        if (aide.NomineeOf is { } patron)
        {
            changes.AddRange(SettleDebts(patron));
        }

        if (aide.Origin == Aide.Parente)
        {
            string skeleton = Fill(Content.Instruments.Texto("esqueleto_nepotismo"), ("nome", NameOf(personId)));
            State.Ledger.AddSkeleton("nepotismo", skeleton, CouncilRules.GravidadeNepotismo, Week, g.Cabinet.Where(a => a.PersonId != personId).Select(a => a.PersonId));
            changes.Add(skeleton);
        }

        string text = Fill(Content.Instruments.Texto("assessor_nomeado"), ("nome", NameOf(personId)), ("funcao", aide.ZoneId is null ? role.Nome : T("funcao_na_zona", ("funcao", role.Nome), ("zona", ZoneName(aide.ZoneId)))));
        News("gabinete", text, aide.ZoneId, personId);
        if (g.Cabinet.Count() >= CabinetSlots)
        {
            PassOver();
        }

        RefreshDerived();
        return new GovResult(null, text, changes, []);
    }

    /// <summary>Gabinete fechado: quem era da campanha ou indicado e ficou de fora guarda a lembrança.</summary>
    private void PassOver()
    {
        foreach (Aide aide in G.Aides.Where(a => !a.Appointed && !a.Left && !a.PassedOver && a.Origin is Aide.DaCampanha or Aide.DeLideranca or Aide.DeVereador))
        {
            aide.PassedOver = true;
            MemoryDefinition memory = Content.Instruments.Lembrancas["preterido"];
            Remember(aide.PersonId, PersonRole.Assessor, "preterido", memory);
            Remember(aide.NomineeOf, aide.Origin == Aide.DeVereador ? PersonRole.Vereador : PersonRole.Lideranca, "preterido", memory);
        }
    }

    /// <summary>
    /// Previsão de demitir um assessor: a lembrança dele e de quem o indicou (com a mudança na relação), a função que
    /// para de render e, no assessor de base, a presença da zona que volta a cair.
    /// </summary>
    public GovForecast ForecastDismissAide(string personId)
    {
        if (Role != GovRole.Vereador)
        {
            return GovForecast.Block(GovFailure.PapelErrado, "");
        }

        if (G.Cabinet.FirstOrDefault(a => a.PersonId == personId) is not { } aide)
        {
            return GovForecast.Block(GovFailure.NaoEncontrado, "");
        }

        MemoryDefinition memory = Content.Instruments.Lembrancas["exonerado"];
        string role = aide.Role is { } roleId ? Content.Instruments.Role(roleId).Nome : "";
        double own = RememberDelta(personId, memory);
        var lines = new List<ForecastLine> { new(GovForecast.Relacao, personId, own, own, own) };
        var explanation = new List<string> { Fill(Content.Instruments.Texto("previsao_exonerar"), ("nome", NameOf(personId)), ("funcao", role)) };
        var remembered = new List<string> { NameOf(personId) };
        if (aide.Role == AideRole.Base && aide.ZoneId is { } zone)
        {
            explanation.Add(Fill(Content.Instruments.Texto("previsao_exonerar_base"), ("zona", ZoneName(zone)), ("decaimento", Num(CouncilRules.PresencaDecaimentoSemanal))));
        }

        if (aide.NomineeOf is { } patron && patron != RaceSetup.PlayerId && patron != Work.Prefeito)
        {
            double delta = RememberDelta(patron, memory);
            lines.Add(new ForecastLine(GovForecast.Relacao, patron, delta, delta, delta));
            explanation.Add(Fill(Content.Instruments.Texto("previsao_exonerar_padrinho"), ("padrinho", NameOf(patron)), ("nome", NameOf(personId))));
            remembered.Add(NameOf(patron));
        }

        return new GovForecast(null, null, lines, [], explanation) { Remembered = remembered.Where(n => n.Length > 0).Distinct().ToList() };
    }

    public GovResult DismissAide(string personId)
    {
        GovForecast forecast = ForecastDismissAide(personId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        Aide aide = G.Cabinet.First(a => a.PersonId == personId);
        aide.Appointed = false;
        aide.Left = true;
        Remember(personId, PersonRole.Assessor, "exonerado", Content.Instruments.Lembrancas["exonerado"]);
        Remember(aide.NomineeOf, PersonRole.Lideranca, "exonerado", Content.Instruments.Lembrancas["exonerado"]);
        string text = Fill(Content.Instruments.Texto("assessor_exonerado"), ("nome", NameOf(personId)));
        News("gabinete", text, aide.ZoneId, personId);
        RefreshDerived();
        return new GovResult(null, text, [ChRelation(NameOf(personId), Content.Instruments.Lembrancas["exonerado"].Pontos)], []);
    }

    // ================================================================ a base cobra

    private void CouncilWeek(bool automatic)
    {
        AnswerRequests();
        DecideIndications();
        InspectionsWeek();
        DealsWeek();
        if (Role != GovRole.Vereador)
        {
            return;
        }

        GovernanceState g = G;
        AideRole baseRole = Content.Instruments.Role(AideRole.Base);
        for (int z = 0; z < g.Zones.Count; z++)
        {
            string zoneId = g.Zones[z];
            Aide? aide = g.Cabinet.FirstOrDefault(a => a.Role == AideRole.Base && a.ZoneId == zoneId);
            double hold = aide is null ? 0 : baseRole.At(aide.Quality);
            double floor = g.PresenceAtStart[z] * CouncilRules.PresencaPiso;
            if (g.Presence[z] > floor)
            {
                g.Presence[z] = Math.Max(floor, g.Presence[z] - (CouncilRules.PresencaDecaimentoSemanal * (1 - Math.Clamp(hold, 0, 1))));
            }

            if (aide is null)
            {
                continue;
            }

            if (!g.ZoneLeads.Contains(zoneId) && Rng.Chance(CouncilRules.ChanceDemandaDeBase))
            {
                g.ZoneLeads.Add(zoneId);
                News("gabinete", Fill(Content.Instruments.Texto("demanda_de_base"), ("nome", NameOf(aide.PersonId)), ("zona", ZoneName(zoneId))), zoneId, aide.PersonId);
            }

            if (!aide.Neglected && aide.LastSupported is { } last && Week.DayNumber - last.DayNumber >= 7 * CouncilRules.SemanasDeNegligencia)
            {
                aide.Neglected = true;
                News("gabinete", Fill(Content.Instruments.Texto("assessor_negligenciado"), ("nome", NameOf(aide.PersonId)), ("zona", ZoneName(zoneId))), zoneId, aide.PersonId);
            }
        }

        // Zonas da base: quem apoiou cobra presença quando ela cai muito abaixo da que havia na posse.
        foreach (int z in g.BaseZones)
        {
            string zoneId = g.Zones[z];
            bool recent = g.Demands.Any(d => d.Kind == Demand.Presenca && d.RefId == zoneId && (d.Met is null || Week.DayNumber - d.Date.DayNumber < 7 * CouncilRules.BaseCobraPrazoSemanas * 3));
            if (!recent && g.Presence[z] < g.PresenceAtStart[z] * CouncilRules.BaseCobraAbaixo)
            {
                Leader? leader = City.Leaders.Where(l => l.Zones.Contains(zoneId)).OrderByDescending(l => l.AlignedWith == RaceSetup.PlayerId).ThenByDescending(l => l.Influence).FirstOrDefault();
                string text = Fill(Content.Instruments.Texto("base_cobra"), ("zona", ZoneName(zoneId)), ("nome", leader is null ? Fill(Content.Collection.Texto("quem_moradores"), ("zona", ZoneName(zoneId))) : NameOf(leader.PersonId)),
                    ("semanas", CouncilRules.BaseCobraPrazoSemanas.ToString(CultureInfo.InvariantCulture)));
                g.Demands.Add(new Demand { Id = g.NewId("cob"), Kind = Demand.Presenca, RefId = zoneId, PersonId = leader?.PersonId, ZoneId = zoneId, Date = Week, Deadline = Week.AddDays(7 * CouncilRules.BaseCobraPrazoSemanas), Text = text });
                News("cobranca", text, zoneId, leader?.PersonId);
            }
        }

        foreach (Demand demand in g.Demands.Where(d => d.Kind == Demand.Presenca && d.Met is null && d.Deadline < Week).ToList())
        {
            demand.Met = false;
            MemoryDefinition memory = Content.Instruments.Lembrancas["base_cobra_vencida"];
            Remember(demand.PersonId, PersonRole.Lideranca, "base_cobra_vencida", memory, ("zona", ZoneName(demand.ZoneId)));
            if (demand.PersonId is not null && City.Leader(demand.PersonId) is { } leader && leader.AlignedWith == RaceSetup.PlayerId)
            {
                leader.AlignedWith = null;
            }

            News("cobranca", Fill(Content.Instruments.Texto("base_cobra_vencida"), ("zona", ZoneName(demand.ZoneId)), ("nome", NameOf(demand.PersonId))), demand.ZoneId, demand.PersonId);
        }

        _ = automatic;
    }

    private string ZoneLeaderName(string zoneId) =>
        NameOf(City.Leaders.Where(l => l.Zones.Contains(zoneId)).OrderByDescending(l => l.AlignedWith == RaceSetup.PlayerId).ThenByDescending(l => l.Influence).FirstOrDefault()?.PersonId);

    // ================================================================ opções para a tela e para o robô

    /// <summary>
    /// Todas as opções de instrumento que o vereador pode usar agora, cada uma com a previsão. A tela lista; o robô
    /// do BalanceSim compara o ganho previsto.
    /// </summary>
    public IReadOnlyList<InstrumentOption> CouncilOptions()
    {
        var list = new List<InstrumentOption>();
        if (Role != GovRole.Vereador || Mandate is not { } mandate)
        {
            return list;
        }

        GovernanceState g = G;
        void Add(string instrument, string label, string? zone, string? target, string? type, GovForecast forecast)
        {
            if (forecast.Blocked is null)
            {
                list.Add(new InstrumentOption(instrument, label, zone, target, type, forecast));
            }
        }

        foreach (Work work in g.Works.Where(w => w.Open))
        {
            string label = T("alvo_obra", ("obra", WorkName(work)), ("zona", ZoneName(work.ZoneId)));
            Add(InstrumentDefinition.Requerimento, label, work.ZoneId, work.Id, InfoRequest.SobreObra, ForecastRequerimento(InfoRequest.SobreObra, work.Id));
            Add(InstrumentDefinition.Fiscalizacao, label, work.ZoneId, work.Id, null, ForecastFiscalizacao(work.Id));
        }

        foreach (SecretariaDefinition pasta in Content.Secretaries.Secretarias.Where(s => g.SecretaryOf(s.Id) is { } t && !g.KnownSecretaries.Contains(t.PersonId)))
        {
            Add(InstrumentDefinition.Requerimento, pasta.Nome, null, pasta.Id, InfoRequest.SobreSecretaria, ForecastRequerimento(InfoRequest.SobreSecretaria, pasta.Id));
        }

        foreach (string zoneId in g.Zones)
        {
            foreach (WorkType type in Content.Works.Tipos.Where(t => Fits(t, zoneId)))
            {
                if (type.Pequena || type.Servico)
                {
                    Add(InstrumentDefinition.Indicacao, T("alvo_tipo", ("obra", type.Nome), ("zona", ZoneName(zoneId))), zoneId, null, type.Id, ForecastIndicacao(type.Id, zoneId));
                }

                if (type.Emenda)
                {
                    Add(InstrumentDefinition.Emenda, T("alvo_tipo", ("obra", type.Nome), ("zona", ZoneName(zoneId))), zoneId, null, type.Id, ForecastEmenda(type.Id, zoneId));
                }
            }
        }

        foreach (BillTemplate template in ProposableBills())
        {
            Add(InstrumentDefinition.Projeto, template.Title, null, template.Id, null, ForecastProjeto(template.Id));
        }

        foreach (Bill bill in mandate.Bills.Where(b => b.Stage == BillStage.Plenario))
        {
            NominalCount? count = CountBill(bill.Id);
            if (count is null)
            {
                continue;
            }

            int side = PlannedVote(bill) == VoteChoice.Nao ? -1 : 1;
            foreach (CountLine line in count.Lines.Where(l => l.Lean == NominalCount.Indeciso && l.RequestText is not null))
            {
                Add(InstrumentDefinition.NegociarVoto, T("alvo_voto", ("nome", line.Name), ("projeto", bill.Title)), g.MemberZones.GetValueOrDefault(line.MemberId), line.MemberId, bill.Id, ForecastDeal(count, bill.Id, line.MemberId, side, GovernanceContent.PapelVereador));
            }
        }

        return list;
    }

    /// <summary>Executa uma opção de <see cref="CouncilOptions"/>.</summary>
    public GovResult Use(InstrumentOption option) => option.InstrumentId switch
    {
        InstrumentDefinition.Requerimento => Requerimento(option.TypeId!, option.TargetId!),
        InstrumentDefinition.Fiscalizacao => Fiscalizacao(option.TargetId!),
        InstrumentDefinition.Indicacao => Indicacao(option.TypeId!, option.ZoneId!),
        InstrumentDefinition.Emenda => Emenda(option.TypeId!, option.ZoneId!),
        InstrumentDefinition.Projeto => Projeto(option.TargetId!),
        InstrumentDefinition.NegociarVoto => NegociarVoto(option.TypeId!, option.TargetId!, Mandate?.Bills.FirstOrDefault(b => b.Id == option.TypeId) is { } bill && PlannedVote(bill) == VoteChoice.Nao ? -1 : 1),
        _ => GovResult.Fail(GovFailure.NaoEncontrado, ""),
    };
}
