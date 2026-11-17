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

    /// <summary>A melhor escolha de cada turno livre (o que o robô marcou), sem as escolhas forçadas.</summary>
    public List<string> BestTypeByTurn { get; } = [];

    public List<string> BestPairByTurn { get; } = [];

    /// <summary>Tipo mais marcado em cada semana.</summary>
    public List<string> PluralityTypeByWeek { get; } = [];

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

    // ---- R2b: verba, equipe, plataforma, objetivos e decisões com troca.

    public int Weeks { get; set; }

    /// <summary>Canal da primeira parcela de verba (o melhor destino) em cada semana da campanha oficial, com os quatro canais abertos.</summary>
    public List<string> BestChannelByWeek { get; } = [];

    /// <summary>O mesmo na pré-campanha, em que só Redes e Material estão abertos.</summary>
    public List<string> BestChannelByWeekPre { get; } = [];

    /// <summary>Soma, por canal, dos votos por real da melhor primeira parcela de cada semana (para calibrar).</summary>
    public Dictionary<string, double> ChannelValue { get; } = [];

    public Dictionary<string, double> ChannelValuePre { get; } = [];

    public int ChannelWeeks { get; set; }

    public int ChannelWeeksPre { get; set; }

    /// <summary>Verba planejada por canal, em reais.</summary>
    public Dictionary<string, decimal> Spent { get; } = [];

    public int BudgetWeeks { get; set; }

    /// <summary>Decisões em que se ganha uma coisa e se perde outra, por tipo.</summary>
    public Dictionary<string, int> TradeDecisions { get; } = [];

    public int Hires { get; set; }

    public int StaffEvents { get; set; }

    public int PositionChanges { get; set; }

    public int PromisesMade { get; set; }

    public int Delegations { get; set; }

    public int ObjectiveKinds { get; set; }

    public int ObjectivesDone { get; set; }

    public int TeamAtEnd { get; set; }

    // ---- R3: doadores, ataques, mídia, rivais e contas

    /// <summary>Respostas dadas às ofertas de doação, por tipo.</summary>
    public Dictionary<string, int> DonationAnswers { get; } = [];

    public int AttacksReceived { get; set; }

    public int RumorsReceived { get; set; }

    public int AttacksMade { get; set; }

    /// <summary>Respostas a ataques, por tipo.</summary>
    public Dictionary<string, int> Responses { get; } = [];

    /// <summary>Notícias que não citam o jogador, semana a semana.</summary>
    public List<int> NewsByWeek { get; } = [];

    public int HotThemeWeeks { get; set; }

    /// <summary>Trocas de plano dos rivais, por gatilho.</summary>
    public Dictionary<string, int> PlanChanges { get; } = [];

    public decimal Limit { get; set; }

    public decimal CampaignSpent { get; set; }

    public double OffBooksShare { get; set; }

    public int Risk { get; set; }

    /// <summary>Arrecadado por fonte.</summary>
    public Dictionary<string, decimal> Raised { get; } = [];

    public int Replicas { get; set; }

    public int MandatesLost { get; set; }

    public int Representations { get; set; }

    public Dictionary<string, int> Verdicts { get; } = [];

    /// <summary>Tempo da simulação semanal (a virada de domingo), em milissegundos.</summary>
    public List<double> WeekMs { get; } = [];

    public bool Dirty { get; set; }

    /// <summary>Entrevistas marcadas num veículo, e compromissos que trataram de tema em alta.</summary>
    public int OutletInterviews { get; set; }

    public int HotThemeCommitments { get; set; }

    // ---- Diagnóstico do canal de verba, do par mais repetido e da dificuldade

    public bool Strongest { get; set; }

    /// <summary>Campanha sem campanha: o robô ficou parado.</summary>
    public bool Idle { get; set; }

    /// <summary>Por canal, as contas da melhor primeira parcela nas semanas da campanha oficial.</summary>
    public Dictionary<string, ChannelDiag> ChannelDiag { get; } = [];

    public decimal CashAtOfficialStart { get; set; }

    /// <summary>Verba de canais paga até o começo da campanha oficial, e na campanha inteira.</summary>
    public decimal BudgetSpentPre { get; set; }

    public decimal BudgetSpentTotal { get; set; }

    public int CabosLimit { get; set; }

    public decimal ChannelStep { get; set; }

    /// <summary>O par (tipo, lugar) mais repetido da campanha, descrito, e em quantos turnos foi a melhor escolha.</summary>
    public string TopPair { get; set; } = "";

    public int TopPairTurns { get; set; }

    /// <summary>Reconhecimento final dos rivais nomeados do mesmo cargo: média e o maior.</summary>
    public double RivalRecognition { get; set; }

    public double RivalRecognitionMax { get; set; }

    /// <summary>Votos do jogador sobre o quociente eleitoral, cadeiras do partido dele e a posição dele na chapa.</summary>
    public double VotesOverQuotient { get; set; }

    public int PartySeats { get; set; }

    public int RankInParty { get; set; }
}

/// <summary>
/// Robô de campanha (GDD 17.1, princípio a): em cada turno livre escolhe o compromisso de maior ganho previsto de
/// votos. O ganho vem da previsão do próprio jogo (<see cref="CampaignEngine.Forecast(CommitmentRequest)"/>) vezes a
/// sensibilidade dos votos à presença e ao apoio em cada zona. Não enxerga nada que o jogador não veja.
/// </summary>
public sealed partial class CampaignRobot(CampaignEngine engine)
{
    /// <summary>Turnos por dia que o robô ocupa (os dois sem custo de estresse).</summary>
    private const int TurnsPerDay = 2;

    private readonly CampaignEngine _engine = engine;

    private CampaignEngine.VoteSensitivity? _sens;

    private int _sensWeek;

    private GameState State => _engine.Context.State;

    /// <summary>
    /// Mundo que anda junto com a campanha, como no jogo (opção <c>--mundo</c>): indicadores reais e renovação do mundo
    /// político. Sem ele, a campanha roda sozinha, como na medição da R3.
    /// </summary>
    public Planalto.Core.World.WorldEngine? World { get; init; }

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
            if (firstOfWeek)
            {
                Trace.Weeks++;
                ManageWeek();
                ManageR3Week();
            }

            ManageR3Day();
            PlanDay(today, firstOfWeek);
            bool sunday = State.Time.Day == 6;
            if (sunday)
            {
                PlanBudget();
            }

            // Um comando do próprio dia pode ter emitido um fato que dispara um evento (GDD 15.4): responde antes de o dia andar.
            ResolvePending();
            World?.AdvanceTo(State.Calendar.WeekStart);
            _clock.Restart();
            _engine.AdvanceDay();
            if (sunday && !_engine.DecisionPending)
            {
                Trace.WeekMs.Add(_clock.Elapsed.TotalMilliseconds);
            }

            ResolvePending();
        }

        CloseWeek();
        var objectives = _engine.Objectives.History.Concat(_engine.Objectives.Active).ToList();
        Trace.ObjectiveKinds = objectives.Select(o => o.Kind).Distinct().Count();
        Trace.ObjectivesDone = objectives.Count(o => o.Status == ObjectiveStatus.Cumprido);
        Trace.TeamAtEnd = _engine.HiredStaff.Count();
        DiagnosePair();
        ElectionOutcome outcome = _engine.ResolveElection();
        Trace.Dirty = Dirty;
        Trace.CampaignSpent = _engine.Finance.Spent;
        AccountsAfterElection();
        Trace.Elected = outcome.PlayerElected;
        Trace.Position = 1 + outcome.Vereador.Votes.Where(v => !v.Key.StartsWith("demais_", StringComparison.Ordinal)).Count(v => v.Value > outcome.Vereador.Votes[RaceSetup.PlayerId]);
        Trace.FinalRecognition = Campaign.Player.Recognition;
        DiagnoseOutcome(outcome);
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
            // A primeira escolha permitida; o robô limpo pula a que deixa esqueleto (dinheiro por fora, propaganda irregular).
            List<int> allowed = Enumerable.Range(0, definition.Choices.Count).Where(i => EventEngine.Blocked(_engine.Context, definition.Choices[i]) is null).ToList();
            int choice = Dirty ? allowed[0] : allowed.Where(i => definition.Choices[i].Effects.AddSkeleton is null).DefaultIfEmpty(allowed[0]).First();
            EventEngine.Resolve(_engine.Context, pending, choice, State.Rng);
            Trace.Events++;
            Decision("evento");
        }

        ResolveStaffDecisions();

        while (_engine.Agenda.PendingRequest is { } personId)
        {
            Leader leader = _engine.City.Leader(personId)!;
            string kind = _engine.Context.Campaign.Leaders.Request(leader.RequestId).Kind;

            // O robô joga limpo: pedido de dinheiro irregular vira negociação; os demais são aceitos.
            _engine.AnswerLeader(kind == LeaderRequestDefinition.Dinheiro ? LeaderAnswer.Negociar : LeaderAnswer.Aceitar);
            Trace.LeaderDecisions++;
            Decision("pedido de liderança");
        }
    }

    /// <summary>Ganho fictício das escolhas forçadas (arrecadar sem caixa, descansar com estresse alto): ficam fora da estatística da melhor escolha.</summary>
    private const double Forced = 1e100;

    private RobotOption? _weekBest;

    public bool Diagnose { get; init; }

    /// <summary>Fecha a estatística da semana: o tipo e o par (tipo, lugar) da melhor opção vista nela.</summary>
    private void CloseWeek()
    {
        if (_weekBest is { } best)
        {
            // O lugar do compromisso: a zona; na visita, a zona da liderança; na entrevista, o veículo (GDD 17.13).
            string place = best.Request.ZoneId ?? best.Request.OutletId
                ?? (best.Request.PersonId is { } person && _engine.City.Leader(person) is { } leader ? leader.Zones[0] : "-");
            Trace.BestTypeByWeek.Add(best.Request.Type);
            Trace.BestPairByWeek.Add($"{best.Request.Type}|{place}");
        }

        _weekBest = null;
        if (_weekTypes.Count > 0)
        {
            // Tipo mais marcado na semana (empate: o que apareceu primeiro).
            Trace.PluralityTypeByWeek.Add(_weekTypes.GroupBy(x => x).OrderByDescending(g => g.Count()).First().Key);
            _weekTypes.Clear();
        }
    }

    private readonly List<string> _weekTypes = [];

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
            Decision("convite");
        }

        IReadOnlyCollection<string> groups = Groups();
        List<RobotOption>? seen = null;
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
            seen = forced ? seen : options;
            if (Diagnose && firstOfWeek && slot == 0 && !forced)
            {
                // Diagnóstico (variável de ambiente PLANALTO_SIM_DIAG): a melhor opção de cada tipo no primeiro turno da semana.
                Console.WriteLine($"{today:dd/MM} caixa {State.Player.Resources.Money:0} folha {_engine.Payroll().Weekly:0} verba {_engine.Budget.Plan.Sum(x => x.Amount):0} equipe {_engine.HiredStaff.Count()} estresse {State.Player.Resources.Stress} " + string.Join("  ", options.GroupBy(o => o.Request.Type).Select(g => g.MaxBy(o => o.VotesPerTurn)!).OrderByDescending(o => o.VotesPerTurn).Select(o => $"{o.Request.Type}@{o.Request.ZoneId?[^2..] ?? "-"}={o.VotesPerTurn:0.0}")));
            }
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
            if (!forced)
            {
                string turnPlace = best.Request.ZoneId ?? best.Request.OutletId
                    ?? (best.Request.PersonId is { } visited && _engine.City.Leader(visited) is { } visitedLeader ? visitedLeader.Zones[0] : "-");
                Trace.BestTypeByTurn.Add(best.Request.Type);
                Trace.BestPairByTurn.Add($"{best.Request.Type}|{turnPlace}");
                _weekTypes.Add(best.Request.Type);
            }

            if (_engine.Schedule(best.Request) is { Failure: null, Commitment: { } marked })
            {
                Trace.Commitments++;
                Trace.OutletInterviews += marked.OutletId is not null ? 1 : 0;
                Trace.HotThemeCommitments += marked.TemaEmAlta ? 1 : 0;
            }
        }

        if (seen is not null && _engine.DelegationsPerWeek > 0)
        {
            Delegate(today, seen);
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
            r = r with { Tema = TalkTheme(r.Type) };

            // A previsão traz o mesmo bloqueio de CanSchedule; conferir antes poupa a conta das opções bloqueadas.
            if (_engine.CanSchedule(r) is not null)
            {
                return;
            }

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
                if (type.Id == CommitmentCatalog.Entrevista)
                {
                    // Entrevista num veículo (GDD 17.13): o crítico arrisca mais e convence mais; o amigo, o contrário.
                    foreach (Planalto.Core.Media.Outlet outlet in _engine.City.Outlets)
                    {
                        Consider(new CommitmentRequest(type.Id, null, date, turno, OutletId: outlet.Id));
                    }
                }
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
