using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.Electorate;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.Core.World;

namespace Planalto.Core.Campaign;

/// <summary>
/// Agenda com lugar e dia (GDD 17.6): marcar, prever e executar compromissos. O rendimento é
/// base × alcance da zona × liderança junto × ocasião × 1 ÷ (1 + fator × cansaço) × sorte, e a previsão mostra cada
/// fator antes de marcar (princípios f e g).
/// </summary>
public sealed partial class CampaignEngine
{
    private CommitmentCatalog Commitments => Context.Campaign.Commitments;

    private AgendaRules Rules => Commitments.Regras;

    public AgendaState Agenda => Campaign.Agenda;

    public TerritoryState Territory => Campaign.Territory;

    /// <summary>Custo do compromisso em reais (negativo = arrecada), no porte da cidade.</summary>
    public decimal CostOf(CommitmentType type)
    {
        decimal money = Math.Round(type.Dinheiro * (decimal)CostScale / 100) * 100;
        if (type.Arrecada is { } per)
        {
            money -= per.Amount * State.Player.Attributes.Get(per.Attribute) * (decimal)CostScale;
        }

        return money;
    }

    /// <summary>Texto de um fator do rendimento, para a previsão e para a explicação.</summary>
    private static YieldFactor Factor(string id, string text, double multiplier) => new(id, text, multiplier);

    /// <summary>Alcance relativo da zona: zona menor que a média rende mais por turno; maior, menos (GDD 17.5, "a troca").</summary>
    public double Reach(int zone)
    {
        IReadOnlyList<ZoneElectorate> zones = Context.ZoneElectorates;
        double mean = zones.Average(z => z.Voters);
        double own = zones[zone].Voters;
        return Math.Clamp(own <= 0 ? Rules.Alcance.Max : Math.Sqrt(mean / own), Rules.Alcance.Min, Rules.Alcance.Max);
    }

    /// <summary>Cansaço do trio na data do compromisso: o de hoje mais os iguais já marcados antes dele.</summary>
    private double FatigueAt(CommitmentRequest r)
    {
        bool visit = Commitments.Get(r.Type).Visita;
        string key = AgendaState.FatigueKey(r.Type, r.ZoneId, r.Publico, visit ? r.PersonId : null);
        double now = Agenda.Fatigue.GetValueOrDefault(key);
        int before = Agenda.Scheduled.Count(c => AgendaState.FatigueKey(c, Commitments.Get(c.Type).Visita) == key
            && (c.Date < r.Date || (c.Date == r.Date && c.Turno < r.Turno)));
        return now + before;
    }

    private IEnumerable<Turno> TurnsOf(CommitmentType type, Turno start) =>
        Enumerable.Range((int)start, type.Turnos).Select(t => (Turno)t);

    public ScheduleFailure? CanSchedule(CommitmentRequest r)
    {
        CommitmentType type = Commitments.Get(r.Type);
        if (Agenda.PendingRequest is not null || Campaign.Events.Pending.Count > 0)
        {
            return ScheduleFailure.DecisaoPendente;
        }

        if (!type.AllowedIn(Campaign.Phase) || (r.Date >= Campaign.CampaignStart ? !type.Fases.Contains(CampaignActionDefinition.Campanha) : !type.Fases.Contains(CampaignActionDefinition.PreCampanha)))
        {
            return ScheduleFailure.FaseNaoPermite;
        }

        if (r.Date < Today || r.Date > Today.AddDays(Rules.AntecedenciaDias) || r.Date >= Campaign.ElectionDate)
        {
            return ScheduleFailure.ForaDoPrazo;
        }

        // Turno já usado hoje (o compromisso aconteceu e saiu da lista de marcados) continua ocupado.
        if ((int)r.Turno + type.Turnos > 3 || TurnsOf(type, r.Turno).Any(t => Agenda.At(r.Date, t) is not null)
            || Agenda.Scheduled.Any(c => c.Date == r.Date && TurnsOf(Commitments.Get(c.Type), c.Turno).Intersect(TurnsOf(type, r.Turno)).Any())
            || Agenda.Log.Any(x => x.Commitment.Date == r.Date && TurnsOf(Commitments.Get(x.Commitment.Type), x.Commitment.Turno).Intersect(TurnsOf(type, r.Turno)).Any()))
        {
            return ScheduleFailure.TurnoOcupado;
        }

        int zone = r.ZoneId is null ? -1 : Territory.IndexOf(r.ZoneId);
        if (type.ExigeZona && zone < 0)
        {
            return ScheduleFailure.ZonaInvalida;
        }

        if (type.SoEvento)
        {
            CityEventInstance? e = Agenda.CityEvents.FirstOrDefault(x => x.Id == r.CityEventId);
            if (e is null || e.Date != r.Date || e.Turno != r.Turno || e.ZoneId != r.ZoneId)
            {
                return ScheduleFailure.EventoInvalido;
            }
        }

        if (type.EscolhePublico && (r.Publico == CommitmentType.PublicoGeral || Context.ZoneElectorates[zone].VotersIn(r.Publico) <= 0))
        {
            return ScheduleFailure.PublicoInvalido;
        }

        if (type.Visita && (r.PersonId is null || City.Leader(r.PersonId) is not { Known: true }))
        {
            return ScheduleFailure.SemLideranca;
        }

        // Reunião em casa de apoiador (GDD 17.6): exige uma liderança conhecida e não hostil daquele público na zona.
        if (type.Id == CommitmentCatalog.Reuniao
            && !City.Leaders.Any(l => l.Known && l.Publico == r.Publico && l.Zones.Contains(r.ZoneId!) && RelationOf(l.PersonId) >= 0))
        {
            return ScheduleFailure.SemApoiadorNaZona;
        }

        decimal cost = CostOf(type);
        return cost > 0 && State.Player.Resources.Money < cost ? ScheduleFailure.SemDinheiro : null;
    }

    /// <summary>Parte certa do rendimento: os fatores (sem a sorte) e as bases já no porte da cidade.</summary>
    private (List<YieldFactor> Factors, double Yield) Plan(CommitmentRequest r)
    {
        CommitmentType type = Commitments.Get(r.Type);
        var factors = new List<YieldFactor>();
        int zone = r.ZoneId is null ? -1 : Territory.IndexOf(r.ZoneId);
        if (type.ExigeZona && zone >= 0 && (type.Presenca > 0 || type.Apoio > 0))
        {
            factors.Add(Factor("alcance", "Tamanho da zona", Reach(zone)));
        }

        if (type.Rua && r.PersonId is { } companion && City.Leader(companion) is { AlignedWith: RaceSetup.PlayerId } leader && leader.Zones.Contains(r.ZoneId ?? ""))
        {
            factors.Add(Factor("lideranca", $"{NameOf(companion)} foi junto", 1 + Rules.LiderancaBonus));
        }

        if (r.CityEventId is not null && Agenda.CityEvents.FirstOrDefault(e => e.Id == r.CityEventId) is { } cityEvent)
        {
            factors.Add(Factor("evento", cityEvent.Nome, 1 + Rules.EventoBonus));
        }

        if (type.Rua && Agenda.Rain.TryGetValue(r.Date, out double rain) && rain > 0)
        {
            factors.Add(Factor("chuva", "Chuva", 1 - rain));
        }

        double fatigue = FatigueAt(r);
        if (fatigue > 0 && type.Id != CommitmentCatalog.Descanso)
        {
            int ordinal = (int)Math.Round(fatigue) + 1;
            string where = r.ZoneId is null ? "" : $" em {ZoneName(r.ZoneId)}";
            factors.Add(Factor("cansaco", $"{ordinal}º compromisso igual{where} em pouco tempo", 1 / (1 + (Rules.CansacoFator * fatigue))));
        }

        if (type.ExigeZona && zone >= 0 && Rules.PortesComDeslocamento.Contains(Context.Municipio.Porte))
        {
            Zona here = Context.Zonas.Zonas[zone];
            // Conta também o que já aconteceu hoje: na execução o compromisso anterior já saiu da lista de marcados.
            bool far = Agenda.Scheduled.Concat(Agenda.Log.Select(x => x.Commitment))
                .Any(c => c.Date == r.Date && c.Turno < r.Turno && c.ZoneId is not null && c.ZoneId != r.ZoneId && !here.Vizinhas.Contains(c.ZoneId));
            if (far)
            {
                factors.Add(Factor("deslocamento", "Deslocamento entre zonas distantes no mesmo dia", 1 - Rules.DeslocamentoPerda));
            }
        }

        if (type.Id == CommitmentCatalog.Comicio && zone >= 0 && Territory.PresenceOf(RaceSetup.PlayerId, zone) < Rules.ComicioPresencaMinima)
        {
            factors.Add(Factor("comicio_vazio", "Você é pouco conhecido na zona: o comício esvazia", Rules.ComicioVazioFator));
        }

        return (factors, factors.Aggregate(1.0, (acc, f) => acc * f.Multiplier));
    }

    private int StressOf(CommitmentType type, CommitmentRequest r)
    {
        int sameDay = Agenda.Scheduled.Where(c => c.Date == r.Date).Sum(c => Commitments.Get(c.Type).Turnos);
        int extra = Math.Max(0, sameDay + type.Turnos - Rules.TurnosSemCusto) - Math.Max(0, sameDay - Rules.TurnosSemCusto);
        return type.Estresse + (extra * Rules.EstresseTurnoExtra);
    }

    private double TestChance(ActionTest test)
    {
        double chance = State.Player.Attributes.Get(test.Attribute) / 100.0;
        if (State.Player.Resources.Stress > Rules.EstresseLimite)
        {
            chance -= Rules.EstressePenalidade;
        }

        return Math.Clamp(chance, 0.05, 0.95);
    }

    /// <summary>Público que de fato recebe o apoio: o do evento, o escolhido ou o fixo do tipo (gravação).</summary>
    private string? SupportGroup(CommitmentType type, CommitmentRequest r)
    {
        if (type.Apoio <= 0)
        {
            return null;
        }

        if (r.CityEventId is not null && Agenda.CityEvents.FirstOrDefault(e => e.Id == r.CityEventId) is { } e)
        {
            return e.Publico == CommitmentType.PublicoGeral ? null : e.Publico;
        }

        return type.PublicoFixo ?? (r.Publico == CommitmentType.PublicoGeral ? null : r.Publico);
    }

    /// <summary>
    /// Retorno decrescente do apoio conquistado em conversa: quanto mais apoio o jogador já tem daquele público (na
    /// zona, ou na cidade quando o compromisso não tem zona), menos a próxima conversa acrescenta, até o teto.
    /// </summary>
    private double SupportHeadroom(int zone, string group)
    {
        double current = zone >= 0 ? Territory.MeetingSupport[zone].GetValueOrDefault(group) : Campaign.Player.Support.GetValueOrDefault(group);
        return Math.Clamp(1 - (current / Rules.ApoioTeto), 0, 1);
    }

    /// <summary>Previsão antes de marcar (princípio f): efeito esperado, faixa pela sorte e pelos desfechos, e os fatores.</summary>
    public CommitmentForecast Forecast(CommitmentRequest r)
    {
        CommitmentType type = Commitments.Get(r.Type);
        ScheduleFailure? blocked = CanSchedule(r);
        var (factors, yield) = Plan(r);
        int zone = r.ZoneId is null ? -1 : Territory.IndexOf(r.ZoneId);
        double current = zone >= 0 ? Territory.PresenceOf(RaceSetup.PlayerId, zone) : Campaign.Player.Recognition;
        double baseAmount = (zone >= 0 ? type.Presenca : type.PresencaCidade) * RecognitionScale * (zone >= 0 ? PresenceYield(factors, r) : YieldWithout(factors, "alcance"));
        double Delta(double luck) => EffectApplier.Gain(current, baseAmount * luck) - current;
        double support = SupportGroup(type, r) is { } supportGroup ? type.Apoio * yield * SupportHeadroom(zone, supportGroup) : 0;
        _ = yield;

        double imageMin = type.Imagem * yield * Rules.Sorte.Min;
        double imageMax = type.Imagem * yield * Rules.Sorte.Max;
        var outcomes = new List<ForecastOutcome>();
        if (type.Teste is { } test)
        {
            double chance = TestChance(test);
            outcomes.Add(new ForecastOutcome(chance, test.SuccessText));
            outcomes.Add(new ForecastOutcome(1 - chance, test.FailureText));
            imageMin += Math.Min(test.Success.Image, test.Failure.Image);
            imageMax += Math.Max(test.Success.Image, test.Failure.Image);
        }

        if (type.Id == "caminhada" && zone >= 0 && City.Leaders.Any(l => !l.Known && l.Zones.Contains(r.ZoneId!)))
        {
            outcomes.Add(new ForecastOutcome(LeaderRules.ChanceDescoberta, "Você pode conhecer uma liderança da zona."));
        }

        decimal money = -CostOf(type);
        double fatigueFactor = factors.FirstOrDefault(f => f.Id == "cansaco")?.Multiplier ?? 1;
        int relation = type.Visita ? VisitPoints(fatigueFactor) : r.InviteId is not null ? LeaderRules.PontosConviteAceito : 0;
        return new CommitmentForecast(
            blocked,
            Delta(Rules.Sorte.Min), Delta(Rules.Sorte.Max), Delta(1),
            support * Rules.Sorte.Min, support * Rules.Sorte.Max,
            imageMin, imageMax,
            money, money,
            StressOf(type, r),
            relation,
            fatigueFactor,
            factors,
            outcomes);
    }

    /// <summary>
    /// Rendimento que vale para a presença. O bônus do evento da cidade é "com o público dele" (GDD 17.6): num evento
    /// de público geral ele vale para a presença na zona; num evento de um grupo, só para o apoio desse grupo.
    /// </summary>
    private double PresenceYield(List<YieldFactor> factors, CommitmentRequest r)
    {
        bool groupEvent = r.CityEventId is not null
            && Agenda.CityEvents.FirstOrDefault(e => e.Id == r.CityEventId) is { } e && e.Publico != CommitmentType.PublicoGeral;
        return factors.Where(f => f.Id != "sorte" && !(groupEvent && f.Id == "evento")).Aggregate(1.0, (acc, f) => acc * f.Multiplier);
    }

    /// <summary>Pontos de relação de uma visita: a visita repetida à mesma pessoa em pouco tempo rende menos (princípio g).</summary>
    private int VisitPoints(double fatigueFactor) => (int)Math.Round(LeaderRules.PontosVisita * fatigueFactor);

    private static double YieldWithout(List<YieldFactor> factors, string id) =>
        factors.Where(f => f.Id != id).Aggregate(1.0, (acc, f) => acc * f.Multiplier);

    /// <summary>Marca o compromisso; o custo sai quando ele acontece.</summary>
    public (ScheduleFailure? Failure, Commitment? Commitment) Schedule(CommitmentRequest r)
    {
        if (CanSchedule(r) is { } failure)
        {
            return (failure, null);
        }

        var commitment = new Commitment
        {
            Id = $"c{Agenda.NextId++}",
            Type = r.Type,
            ZoneId = r.ZoneId,
            Date = r.Date,
            Turno = r.Turno,
            Publico = r.Publico,
            PersonId = r.PersonId,
            CityEventId = r.CityEventId,
            InviteId = r.InviteId,
        };
        Agenda.Scheduled.Add(commitment);
        if (r.InviteId is not null && Agenda.Invites.FirstOrDefault(i => i.Id == r.InviteId) is { } invite)
        {
            invite.Answered = true;
        }

        return (null, commitment);
    }

    /// <summary>Aceita um convite: marca o compromisso no dia e turno dele (pode falhar se o turno estiver ocupado).</summary>
    public (ScheduleFailure? Failure, Commitment? Commitment) AcceptInvite(string inviteId)
    {
        Invite invite = Agenda.Invites.First(i => i.Id == inviteId);
        return Schedule(new CommitmentRequest("caminhada", invite.ZoneId, invite.Date, invite.Turno, CommitmentType.PublicoGeral, invite.FromPersonId, InviteId: invite.Id));
    }

    public bool Cancel(string commitmentId)
    {
        Commitment? c = Agenda.Scheduled.FirstOrDefault(x => x.Id == commitmentId);
        if (c is null)
        {
            return false;
        }

        Agenda.Scheduled.Remove(c);
        if (c.InviteId is not null && Agenda.Invites.FirstOrDefault(i => i.Id == c.InviteId) is { } invite)
        {
            // Desmarcar o que veio de convite é faltar ao convite.
            invite.Answered = false;
            DeclineInvite(invite.Id);
        }

        return true;
    }

    /// <summary>Executa os compromissos de hoje, na ordem dos turnos, e devolve os resultados explicados.</summary>
    public List<CommitmentResult> RunToday() => RunUntil(Today);

    private List<CommitmentResult> RunUntil(DateOnly lastDay)
    {
        var results = new List<CommitmentResult>();
        foreach (Commitment c in Agenda.Scheduled.Where(c => c.Date <= lastDay).OrderBy(c => c.Date).ThenBy(c => c.Turno).ToList())
        {
            Agenda.Scheduled.Remove(c);
            if (Agenda.PendingRequest is not null)
            {
                // Uma decisão ficou esperando: o resto do dia não acontece sem o jogador (volta para a agenda).
                Agenda.Scheduled.Add(c);
                continue;
            }

            // Cada compromisso acontece na data dele (a virada da semana executa os que restam): lembranças e feed
            // gravam esse dia.
            State.Time.Day = Math.Clamp(c.Date.DayNumber - State.Calendar.WeekStart.DayNumber, State.Time.Day, 6);
            results.Add(Run(c));
        }

        return results;
    }

    private CommitmentResult Run(Commitment c)
    {
        CommitmentType type = Commitments.Get(c.Type);
        var request = new CommitmentRequest(c.Type, c.ZoneId, c.Date, c.Turno, c.Publico, c.PersonId, c.CityEventId, c.InviteId);
        var (factors, yield) = Plan(request);
        int stress = StressOf(type, request);
        double luck = Rng.NextDouble(Rules.Sorte.Min, Rules.Sorte.Max);
        factors.Add(Factor("sorte", "Sorte do dia", luck));
        int zone = c.ZoneId is null ? -1 : Territory.IndexOf(c.ZoneId);
        var presence = new Dictionary<string, double>(StringComparer.Ordinal);
        TerritorySimulation sim = Context.Campaign.Territory.Simulacao;

        if (zone >= 0 && type.Presenca > 0)
        {
            List<int> neighbors = Context.Zonas.Zonas[zone].Vizinhas.Select(Territory.IndexOf).Where(i => i >= 0).ToList();
            foreach (var (z, delta) in Territory.Gain(RaceSetup.PlayerId, zone, type.Presenca * RecognitionScale * PresenceYield(factors, request) * luck, sim.Transbordo, neighbors))
            {
                presence[Territory.Zones[z]] = delta;
            }
        }
        else if (type.PresencaCidade > 0)
        {
            double amount = type.PresencaCidade * RecognitionScale * YieldWithout(factors, "alcance");
            double[] before = [.. Territory.Presence[RaceSetup.PlayerId]];
            Territory.GainEverywhere(RaceSetup.PlayerId, amount);
            for (int z = 0; z < before.Length; z++)
            {
                presence[Territory.Zones[z]] = Territory.Presence[RaceSetup.PlayerId][z] - before[z];
            }
        }

        if (zone >= 0)
        {
            Territory.MarkActive(RaceSetup.PlayerId, zone);
        }

        double support = 0;
        if (SupportGroup(type, request) is { } group)
        {
            support = type.Apoio * yield * luck * SupportHeadroom(zone, group);
            if (zone >= 0)
            {
                Dictionary<string, double> meeting = Territory.MeetingSupport[zone];
                meeting[group] = Math.Min(1, meeting.GetValueOrDefault(group) + support);
            }
            else
            {
                Campaign.Player.Support[group] = Math.Clamp(Campaign.Player.Support.GetValueOrDefault(group) + support, 0, 1);
            }
        }

        double image = type.Imagem * yield * luck;
        string text = "";
        if (type.Teste is { } test)
        {
            bool passed = Rng.Chance(TestChance(test));
            image += passed ? test.Success.Image : test.Failure.Image;
            text = passed ? test.SuccessText : test.FailureText;
        }

        decimal money = -CostOf(type);
        Character player = State.Player;
        player.Resources.Money += money;
        Campaign.Player.Money = player.Resources.Money;
        player.Resources.Stress = Math.Max(0, player.Resources.Stress + stress);
        Campaign.Player.Image = Math.Clamp(Campaign.Player.Image + image, -1, 1);
        Campaign.Player.Recognition = Territory.Recognition(RaceSetup.PlayerId);
        player.Resources.SetRecognition(Campaign.Municipio.ToString(System.Globalization.CultureInfo.InvariantCulture), (int)Math.Round(Campaign.Player.Recognition));

        double visitFatigue = factors.FirstOrDefault(f => f.Id == "cansaco")?.Multiplier ?? 1;
        string key = AgendaState.FatigueKey(c, type.Visita);
        if (type.Id != CommitmentCatalog.Descanso)
        {
            Agenda.Fatigue[key] = Agenda.Fatigue.GetValueOrDefault(key) + 1;
        }

        int relation = 0;
        if (type.Visita && c.PersonId is not null && City.Leader(c.PersonId) is { } visited)
        {
            relation = VisitPoints(visitFatigue);
            RememberLeader(visited, "visita", 1, relation);
            visited.RequestKnown = true;
            if (!visited.RequestMet)
            {
                Agenda.PendingRequest = visited.PersonId;
            }

            Realign(visited);
        }
        else if (c.PersonId is not null && City.Leader(c.PersonId) is { } companion)
        {
            City.Person(companion.PersonId)!.LastContact = Today;
            if (c.InviteId is not null)
            {
                relation = LeaderRules.PontosConviteAceito;
                RememberLeader(companion, "convite_aceito", 2, relation);
                Realign(companion);
            }
        }

        // Caminhar pela zona pode revelar uma liderança que o jogador ainda não conhecia.
        if (type.Id == "caminhada" && zone >= 0)
        {
            List<Leader> unknown = City.Leaders.Where(l => !l.Known && l.Zones.Contains(c.ZoneId!)).ToList();
            if (unknown.Count > 0 && Rng.Chance(LeaderRules.ChanceDescoberta))
            {
                Leader found = Rng.Pick(unknown);
                found.Known = true;
                Agenda.AddFeed(new FeedItem(c.Date, "descoberta", LeaderText("descoberta", found), c.ZoneId, found.PersonId));
            }
        }

        RefreshSupport();
        var result = new CommitmentResult
        {
            Commitment = c,
            Presence = presence,
            Support = support,
            Image = image,
            Money = money,
            Stress = stress,
            RelationPoints = relation,
            Factors = factors,
            Text = text,
        };
        Agenda.Log.Add(result);
        if (Agenda.Log.Count > AgendaState.MaxLog)
        {
            Agenda.Log.RemoveRange(0, Agenda.Log.Count - AgendaState.MaxLog);
        }

        Agenda.AddFeed(new FeedItem(c.Date, "compromisso", type.Nome, c.ZoneId, c.PersonId, RaceSetup.PlayerId));
        return result;
    }

    /// <summary>Virada da semana para a agenda: cansaço cai, e a semana que entra ganha chuva prevista e eventos da cidade.</summary>
    private void AgendaWeek(DateOnly nextWeekStart)
    {
        foreach (string key in Agenda.Fatigue.Keys.ToList())
        {
            double value = Agenda.Fatigue[key] - Rules.CansacoDecaimentoSemanal;
            if (value <= 0)
            {
                Agenda.Fatigue.Remove(key);
            }
            else
            {
                Agenda.Fatigue[key] = value;
            }
        }

        foreach (DateOnly old in Agenda.Rain.Keys.Where(d => d < nextWeekStart).ToList())
        {
            Agenda.Rain.Remove(old);
        }

        Agenda.CityEvents.RemoveAll(e => e.Date < nextWeekStart);
        PlanWeek(nextWeekStart);
    }

    /// <summary>Sorteia a chuva e os eventos da cidade de uma semana (se ainda não foram sorteados).</summary>
    public void PlanWeek(DateOnly weekStart)
    {
        if (Agenda.Rain.Keys.Any(d => d >= weekStart && d < weekStart.AddDays(7)) || Agenda.CityEvents.Any(e => e.Date >= weekStart && e.Date < weekStart.AddDays(7)))
        {
            return;
        }

        for (int d = 0; d < 7; d++)
        {
            Agenda.Rain[weekStart.AddDays(d)] = Rng.Chance(Rules.ChuvaChance) ? Rng.NextDouble(Rules.ChuvaPerda.Min, Rules.ChuvaPerda.Max) : 0;
        }

        CityEventCatalog catalog = Context.Campaign.CityEvents;
        double total = Context.Electorate.Voters;
        foreach (CityEventType type in catalog.Tipos.Where(t => t.Portes.Contains(Context.Municipio.Porte)))
        {
            DateOnly date = weekStart.AddDays(type.DiaSemana);
            if (type.Meses.Count > 0 && !type.Meses.Contains(date.Month))
            {
                continue;
            }

            // Evento religioso: a chance segue a fatia daquela religião entre os eleitores do município (Censo).
            double chance = type.ChanceSemanal;
            if (type.Religiao is { } religion)
            {
                double share = total <= 0 ? 0 : Context.Electorate.Segments.Where(s => s.Has(religion)).Sum(s => s.Voters) / total;
                chance = Math.Min(1, chance * share * catalog.ReligiosoEscala);
            }

            List<ZoneElectorate> zones = Context.ZoneElectorates
                .Where(z => (type.Situacao == "qualquer" || z.Zona.Situacao == type.Situacao) && (type.Publico == CommitmentType.PublicoGeral || z.VotersIn(type.Publico) > 0))
                .ToList();
            if (zones.Count == 0 || !Rng.Chance(chance))
            {
                continue;
            }

            ZoneElectorate zone = Rng.PickWeighted(zones, z => type.Publico == CommitmentType.PublicoGeral ? Math.Max(1, z.Voters) : z.VotersIn(type.Publico));
            Agenda.CityEvents.Add(new CityEventInstance
            {
                Id = $"ev{Agenda.NextId++}",
                TypeId = type.Id,
                Nome = type.Nome,
                ZoneId = zone.Zona.Id,
                Date = date,
                Turno = type.Turno,
                Publico = type.Publico,
            });
        }
    }

    /// <summary>Compromissos marcados de hoje até o domingo desta semana (para a pausa "semana sem compromisso").</summary>
    public int ScheduledIn(DateOnly from, DateOnly to) => Agenda.Scheduled.Count(c => c.Date >= from && c.Date <= to);

    /// <summary>
    /// Sensibilidade dos votos do jogador à presença em cada zona e ao apoio de cada grupo em cada zona (derivadas
    /// por diferença finita na projeção da zona). Serve para estimar o ganho de votos de um compromisso sem
    /// reprojetar a cidade inteira a cada opção (o robô do BalanceSim e os conselhos da equipe usam).
    /// </summary>
    public sealed record VoteSensitivity(double[] PerPresencePoint, Dictionary<string, double>[] PerSupportUnit);

    public VoteSensitivity Sensitivity(IReadOnlyCollection<string> groups)
    {
        Office office = Campaign.Player.Office;
        List<VoteCandidate> candidates = Campaign.ForOffice(office).Select(ToVote).ToList();
        VoteModelConfig config = Context.ElectorateContent.VoteModel;
        Dictionary<string, double> noise = VoteModel.Noise(candidates, config, null);
        TerritoryState t = Territory;
        double[] presence = t.Presence[RaceSetup.PlayerId];
        var perPoint = new double[t.Zones.Count];
        var perSupport = new Dictionary<string, double>[t.Zones.Count];
        if (!t.Support.TryGetValue(RaceSetup.PlayerId, out List<Dictionary<string, double>>? support))
        {
            RefreshSupport();
            support = t.Support[RaceSetup.PlayerId];
        }

        foreach (ZoneElectorate zone in Context.ZoneElectorates)
        {
            int z = zone.Index;
            double Votes() => TerritoryVote.ProjectZone(zone, t, candidates, config, Context.Themes, noise).Votes[RaceSetup.PlayerId];
            double baseline = Votes();
            double saved = presence[z];
            const double Step = 2;
            presence[z] = Math.Min(100, saved + Step);
            perPoint[z] = presence[z] > saved ? (Votes() - baseline) / (presence[z] - saved) : 0;
            presence[z] = saved;

            perSupport[z] = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string group in groups)
            {
                if (zone.VotersIn(group) <= 0)
                {
                    continue;
                }

                const double Unit = 0.05;
                bool had = support[z].TryGetValue(group, out double before);
                support[z][group] = Math.Min(1, before + Unit);
                perSupport[z][group] = (Votes() - baseline) / Unit;
                if (had)
                {
                    support[z][group] = before;
                }
                else
                {
                    support[z].Remove(group);
                }
            }
        }

        return new VoteSensitivity(perPoint, perSupport);
    }
}
