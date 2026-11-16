using Planalto.Core.Campaign;
using Planalto.Core.Events;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.BalanceSim;

/// <summary>Uma opção de compromisso avaliada pelo robô: o pedido e o ganho previsto de votos por turno.</summary>
public sealed record RobotOption(CommitmentRequest Request, double VotesPerTurn);

/// <summary>O que o robô mediu numa campanha.</summary>
public sealed class CampaignTrace
{
    /// <summary>Tipo do compromisso de maior ganho previsto em cada semana (o melhor no primeiro turno livre da semana).</summary>
    public List<string> BestTypeByWeek { get; } = [];

    /// <summary>Par (tipo, zona) de maior ganho previsto em cada semana.</summary>
    public List<string> BestPairByWeek { get; } = [];

    public int Commitments { get; set; }

    /// <summary>Turnos avaliados e, deles, os de escolha forçada (arrecadar sem caixa, descansar com estresse alto).</summary>
    public int Turns { get; set; }

    public int ForcedTurns { get; set; }

    public int Promises { get; set; }

    public int LeaderDecisions { get; set; }

    public int Events { get; set; }

    public bool Elected { get; set; }

    public int Position { get; set; }

    public double FinalRecognition { get; set; }

    public int AlignedLeaders { get; set; }
}

/// <summary>
/// Robô de campanha (GDD 17.1, princípio a): em cada turno livre escolhe o compromisso de maior ganho previsto de
/// votos. O ganho vem da previsão do próprio jogo (<see cref="CampaignEngine.Forecast(CommitmentRequest)"/>) vezes a
/// sensibilidade dos votos à presença e ao apoio em cada zona. Não enxerga nada que o jogador não veja.
/// </summary>
public sealed class CampaignRobot(CampaignEngine engine)
{
    /// <summary>Turnos por dia que o robô ocupa (os dois sem custo de estresse).</summary>
    private const int TurnsPerDay = 2;

    private readonly CampaignEngine _engine = engine;

    private CampaignEngine.VoteSensitivity? _sens;

    private int _sensWeek;

    private GameState State => _engine.Context.State;

    private CampaignState Campaign => State.Campaign!;

    public CampaignTrace Trace { get; } = new();

    /// <summary>Joga até a semana da eleição e apura.</summary>
    public CampaignTrace Play()
    {
        int lastWeek = -1;
        while (Campaign.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha)
        {
            ResolvePending();
            DateOnly today = _engine.Today;
            int week = State.Calendar.WeekStart.DayNumber;
            bool firstOfWeek = week != lastWeek;
            lastWeek = week;
            PlanDay(today, firstOfWeek);
            _engine.AdvanceDay();
            ResolvePending();
        }

        CloseWeek();
        ElectionOutcome outcome = _engine.ResolveElection();
        Trace.Elected = outcome.PlayerElected;
        Trace.Position = 1 + outcome.Vereador.Votes.Where(v => !v.Key.StartsWith("demais_", StringComparison.Ordinal)).Count(v => v.Value > outcome.Vereador.Votes[RaceSetup.PlayerId]);
        Trace.FinalRecognition = Campaign.Player.Recognition;
        Trace.AlignedLeaders = _engine.City.Leaders.Count(l => l.AlignedWith == RaceSetup.PlayerId);
        Trace.Promises = State.Ledger.Promises.Count;
        return Trace;
    }

    /// <summary>Responde ao que pausa o jogo: eventos (primeira escolha permitida) e pedidos de liderança.</summary>
    private void ResolvePending()
    {
        while (Campaign.Events.Pending.Count > 0)
        {
            PendingEvent pending = Campaign.Events.Pending[0];
            EventDefinition definition = _engine.Context.Events!.Find(pending.EventId)!;
            int choice = Enumerable.Range(0, definition.Choices.Count).First(i => EventEngine.Blocked(_engine.Context, definition.Choices[i]) is null);
            EventEngine.Resolve(_engine.Context, pending, choice, State.Rng);
            Trace.Events++;
        }

        while (_engine.Agenda.PendingRequest is { } personId)
        {
            Leader leader = _engine.City.Leader(personId)!;
            string kind = _engine.Context.Campaign.Leaders.Request(leader.RequestId).Kind;

            // O robô joga limpo: pedido de dinheiro irregular vira negociação; os demais são aceitos.
            _engine.AnswerLeader(kind == LeaderRequestDefinition.Dinheiro ? LeaderAnswer.Negociar : LeaderAnswer.Aceitar);
            Trace.LeaderDecisions++;
        }
    }

    /// <summary>Ganho fictício das escolhas forçadas (arrecadar sem caixa, descansar com estresse alto): ficam fora da estatística da melhor escolha.</summary>
    private const double Forced = 1e100;

    private RobotOption? _weekBest;

    /// <summary>Fecha a estatística da semana: o tipo e o par (tipo, lugar) da melhor opção vista nela.</summary>
    private void CloseWeek()
    {
        if (_weekBest is { } best)
        {
            string place = best.Request.ZoneId
                ?? (best.Request.PersonId is { } person && _engine.City.Leader(person) is { } leader ? leader.Zones[0] : "-");
            Trace.BestTypeByWeek.Add(best.Request.Type);
            Trace.BestPairByWeek.Add($"{best.Request.Type}|{place}");
        }

        _weekBest = null;
    }

    private void PlanDay(DateOnly today, bool firstOfWeek)
    {
        if (firstOfWeek)
        {
            CloseWeek();
        }

        // Convites: aceita os de hoje se o turno estiver livre; os demais ficam para o dia.
        foreach (Invite invite in _engine.Agenda.Invites.Where(i => !i.Answered && i.Date == today).ToList())
        {
            _engine.AcceptInvite(invite.Id);
        }

        IReadOnlyCollection<string> groups = Groups();
        for (int slot = 0; slot < TurnsPerDay; slot++)
        {
            var turno = (Turno)slot;
            if (_engine.Agenda.Scheduled.Any(c => c.Date == today && (c.Turno == turno || (c.Turno < turno && (int)c.Turno + _engine.Context.Campaign.Commitments.Get(c.Type).Turnos > (int)turno))))
            {
                continue;
            }

            List<RobotOption> options = Options(today, turno, groups);
            if (options.Count == 0)
            {
                continue;
            }

            RobotOption best = options.MaxBy(o => o.VotesPerTurn)!;
            bool forced = best.VotesPerTurn >= Forced;
            if (forced)
            {
                Trace.ForcedTurns++;
            }
            else if (firstOfWeek && slot == 0)
            {
                _weekBest = best;
            }
            else if (_weekBest is null || best.VotesPerTurn > _weekBest.VotesPerTurn)
            {
                _weekBest = best;
            }

            Trace.Turns++;

            if (_engine.Schedule(best.Request).Failure is null)
            {
                Trace.Commitments++;
            }
        }
    }

    private IReadOnlyCollection<string> Groups() =>
        _engine.Context.ElectorateContent.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToList();

    /// <summary>Todas as opções permitidas para o turno, com o ganho previsto de votos por turno ocupado.</summary>
    public List<RobotOption> Options(DateOnly date, Turno turno, IReadOnlyCollection<string> groups)
    {
        // A sensibilidade é recalculada uma vez por semana: dentro da semana, o que muda de turno para turno é o
        // cansaço e a presença, que a previsão de cada opção já traz.
        int week = State.Calendar.WeekStart.DayNumber;
        if (_sens is null || _sensWeek != week)
        {
            _sens = _engine.Sensitivity(groups);
            _sensWeek = week;
        }

        CampaignEngine.VoteSensitivity sens = _sens;
        TerritoryState t = _engine.Territory;
        CommitmentCatalog catalog = _engine.Context.Campaign.Commitments;
        TerritorySimulation sim = _engine.Context.Campaign.Territory.Simulacao;
        var zonas = _engine.Context.Zonas.Zonas;
        var result = new List<RobotOption>();

        void Consider(CommitmentRequest r)
        {
            CommitmentForecast f = _engine.Forecast(r);
            if (f.Blocked is not null)
            {
                return;
            }

            CommitmentType type = catalog.Get(r.Type);
            double votes = 0;
            int zone = r.ZoneId is null ? -1 : t.IndexOf(r.ZoneId);
            if (zone >= 0)
            {
                votes += f.PresenceExpected * sens.PerPresencePoint[zone];
                List<int> neighbors = zonas[zone].Vizinhas.Select(t.IndexOf).Where(i => i >= 0).ToList();

                // Transbordo: a previsão dá o ganho na zona; a base antes do retorno decrescente é esse ganho ÷ (1 − presença/100).
                double raw = f.PresenceExpected / Math.Max(0.05, 1 - (t.PresenceOf(RaceSetup.PlayerId, zone) / 100));
                foreach (int n in neighbors)
                {
                    votes += raw * sim.Transbordo / neighbors.Count * (1 - (t.PresenceOf(RaceSetup.PlayerId, n) / 100)) * sens.PerPresencePoint[n];
                }

                string? group = r.CityEventId is not null
                    ? _engine.Agenda.CityEvents.First(e => e.Id == r.CityEventId).Publico
                    : r.Publico;
                if (group is not null && group != CommitmentType.PublicoGeral && sens.PerSupportUnit[zone].TryGetValue(group, out double perUnit))
                {
                    votes += (f.SupportMin + f.SupportMax) / 2 * perUnit;
                }
            }
            else if (type.PresencaCidade > 0)
            {
                double raw = f.PresenceExpected / Math.Max(0.05, 1 - (Campaign.Player.Recognition / 100));
                for (int z = 0; z < t.Zones.Count; z++)
                {
                    votes += raw * (1 - (t.PresenceOf(RaceSetup.PlayerId, z) / 100)) * sens.PerPresencePoint[z];
                    if (type.PublicoFixo is { } fixedGroup && sens.PerSupportUnit[z].TryGetValue(fixedGroup, out double perUnit))
                    {
                        votes += (f.SupportMin + f.SupportMax) / 2 * perUnit;
                    }
                }
            }

            if (type.Visita && r.PersonId is not null && _engine.City.Leader(r.PersonId) is { } leader)
            {
                votes += LeaderValue(leader, sens, f.RelationPoints);
            }

            result.Add(new RobotOption(r, votes / type.Turnos));
        }

        // Sem dinheiro para uma semana de rua, arrecadar passa na frente; com estresse alto, descansar.
        decimal walkCost = _engine.CostOf(catalog.Get("caminhada"));
        if (State.Player.Resources.Money < walkCost * 6)
        {
            var dinner = new CommitmentRequest("jantar", null, date, turno);
            if (_engine.CanSchedule(dinner) is null)
            {
                return [new RobotOption(dinner, Forced * 2)];
            }
        }

        if (State.Player.Resources.Stress >= 60)
        {
            return [new RobotOption(new CommitmentRequest(CommitmentCatalog.Descanso, null, date, turno), Forced)];
        }

        foreach (CommitmentType type in catalog.Tipos.Where(x => x.Id != CommitmentCatalog.Descanso && x.Arrecada is null))
        {
            if (type.SoEvento)
            {
                foreach (CityEventInstance e in _engine.Agenda.CityEvents.Where(e => e.Date == date && e.Turno == turno))
                {
                    Consider(new CommitmentRequest(type.Id, e.ZoneId, date, turno, CommitmentType.PublicoGeral, null, e.Id));
                }
            }
            else if (type.Visita)
            {
                foreach (Leader leader in _engine.City.Leaders.Where(l => l.Known))
                {
                    Consider(new CommitmentRequest(type.Id, null, date, turno, CommitmentType.PublicoGeral, leader.PersonId));
                }
            }
            else if (type.ExigeZona)
            {
                foreach (string zoneId in t.Zones)
                {
                    string? companion = type.Rua
                        ? _engine.City.Leaders.FirstOrDefault(l => l.AlignedWith == RaceSetup.PlayerId && l.Zones.Contains(zoneId))?.PersonId
                        : null;
                    if (type.EscolhePublico)
                    {
                        foreach (string group in groups)
                        {
                            Consider(new CommitmentRequest(type.Id, zoneId, date, turno, group, companion));
                        }
                    }
                    else
                    {
                        Consider(new CommitmentRequest(type.Id, zoneId, date, turno, CommitmentType.PublicoGeral, companion));
                    }
                }
            }
            else
            {
                Consider(new CommitmentRequest(type.Id, null, date, turno));
            }
        }

        return result;
    }

    /// <summary>
    /// Quanto vale visitar uma liderança: o apoio que ela daria alinhada (influência × relação ÷ 100 × fator, no
    /// público e nas zonas dela) vezes a sensibilidade, pesado pelo quanto a visita aproxima do alinhamento.
    /// </summary>
    private double LeaderValue(Leader leader, CampaignEngine.VoteSensitivity sens, int points)
    {
        LeaderRules rules = _engine.Context.Campaign.Leaders.Regras;
        TerritoryState t = _engine.Territory;
        double relation = _engine.RelationOf(leader.PersonId);
        double PerUnit(string zoneId) => t.IndexOf(zoneId) is var z and >= 0 && sens.PerSupportUnit[z].TryGetValue(leader.Publico, out double v) ? v : 0;
        double perUnit = leader.Zones.Sum(PerUnit);
        if (leader.AlignedWith == RaceSetup.PlayerId)
        {
            // Já aliada: a visita só rende o apoio a mais dos pontos de relação.
            return leader.Influence * points / 100.0 * rules.ApoioPorInfluencia * perUnit;
        }

        // Ainda não aliada: o robô aceita o pedido, então a visita traz a promessa e o apoio de quem passa do preço.
        double after = relation + points + (leader.RequestMet ? 0 : rules.PontosPromessa);
        if (after < leader.Price || after < (leader.Favor.Count == 0 ? 0 : leader.Favor.Values.Max()))
        {
            return leader.Influence * points / 100.0 * rules.ApoioPorInfluencia * perUnit * 0.5;
        }

        return leader.Influence * Math.Clamp(after, 0, 100) / 100.0 * rules.ApoioPorInfluencia * perUnit;
    }
}
