using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Como o jogador automático dos testes preenche a agenda e responde às decisões.</summary>
internal sealed record RobotOptions
{
    /// <summary>Turnos preenchidos por dia (2 = sem custo de estresse).</summary>
    public int TurnsPerDay { get; init; } = 2;

    /// <summary>Tipos de compromisso, na ordem em que a agenda roda por eles.</summary>
    public IReadOnlyList<string> Cycle { get; init; } =
        ["caminhada", "reuniao", "visita", "caminhada", "entrevista", "reuniao", "gravacao", "comicio", "visita", "jantar"];

    /// <summary>Resposta aos pedidos de liderança; null sorteia entre as quatro.</summary>
    public LeaderAnswer? Answer { get; init; }

    /// <summary>Chance de aceitar cada convite (o resto é recusado na hora).</summary>
    public double AcceptInvite { get; init; } = 0.8;

    /// <summary>Vai aos eventos da cidade da semana.</summary>
    public bool CityEvents { get; init; } = true;

    /// <summary>Estresse a partir do qual entra descanso na agenda.</summary>
    public int RestAbove { get; init; } = 60;

    /// <summary>Chance semanal de encomendar a pesquisa interna.</summary>
    public double PollChance { get; init; } = 0.1;
}

/// <summary>
/// Jogador automático dos testes (a mesma ideia serve ao BalanceSim): a cada semana aceita ou recusa convites, vai
/// aos eventos da cidade e preenche os turnos livres rodando pelos tipos de compromisso, pelas zonas, pelos públicos
/// e pelas lideranças conhecidas; responde aos pedidos de liderança, aos eventos e ao debate; e avança dia a dia.
/// </summary>
internal sealed class CampaignRobot(CampaignEngine engine, ulong seed, RobotOptions? options = null)
{
    private readonly Rng _policy = new(seed * 7919);
    private readonly RobotOptions _options = options ?? new RobotOptions();
    private int _type;
    private int _zone;
    private int _group;
    private int _leader;

    /// <summary>Motor em uso; trocável para retomar a mesma política num estado carregado do save.</summary>
    public CampaignEngine Engine { get; set; } = engine;

    private GameState State => Engine.Context.State;

    private CampaignState Campaign => State.Campaign!;

    /// <summary>Compromissos executados desde o começo, para os testes conferirem.</summary>
    public List<CommitmentResult> Results { get; } = [];

    /// <summary>
    /// Joga até a semana da votação. <paramref name="eachWeek"/> roda no começo de cada semana (com o número dela);
    /// <paramref name="advanceWeek"/> troca a virada da semana (ex.: a da carreira, que também anda o mandato).
    /// </summary>
    public void PlayToElection(Action<int>? eachWeek = null, Action? advanceWeek = null, int maxWeeks = 80)
    {
        int weeks = 0;
        ResolveDecisions();
        while (Campaign.Phase != CampaignPhase.Votacao)
        {
            Assert.True(++weeks < maxWeeks, "A campanha não chegou à eleição.");
            eachWeek?.Invoke(weeks);
            PlayWeek(advanceWeek);
        }
    }

    /// <summary>Marca a semana e avança os dias até a segunda-feira seguinte.</summary>
    public void PlayWeek(Action? advanceWeek = null)
    {
        ResolveDecisions();
        PlanWeek();
        if (_policy.Chance(_options.PollChance))
        {
            Engine.CommissionPoll("interna");
        }

        DateOnly week = State.Calendar.WeekStart;
        int guard = 0;
        while (State.Calendar.WeekStart == week)
        {
            Assert.True(++guard < 60, "O dia não avança.");
            PlayDay(advanceWeek);
        }
    }

    /// <summary>Um dia: acontecem os compromissos (respondendo ao que pedir decisão) e o relógio anda.</summary>
    public void PlayDay(Action? advanceWeek = null)
    {
        ResolveDecisions();
        if (advanceWeek is null)
        {
            DateOnly today = Engine.Today;
            Results.AddRange(Engine.AdvanceDay());
            while (Engine.Today == today && Engine.DecisionPending)
            {
                // Uma visita deixou um pedido esperando: responde e retoma o dia.
                ResolveDecisions();
                Results.AddRange(Engine.AdvanceDay());
            }
        }
        else
        {
            Results.AddRange(Engine.RunToday());
            while (Engine.DecisionPending)
            {
                ResolveDecisions();
                Results.AddRange(Engine.RunToday());
            }

            Clock.AdvanceDay(State, advanceWeek);
        }

        ResolveDecisions();
    }

    /// <summary>Preenche a agenda de hoje até domingo; devolve quantos compromissos marcou.</summary>
    public int PlanWeek()
    {
        if (Campaign.Phase is not (CampaignPhase.PreCampanha or CampaignPhase.Campanha))
        {
            return 0;
        }

        AgendaState agenda = Engine.Agenda;
        DateOnly today = Engine.Today;
        DateOnly end = State.Calendar.WeekEnd;
        int marked = 0;

        foreach (Invite invite in agenda.Invites.Where(i => !i.Answered && i.Date >= today && i.Date <= end).ToList())
        {
            if (_policy.Chance(_options.AcceptInvite) && TurnsOn(invite.Date) < _options.TurnsPerDay && Engine.AcceptInvite(invite.Id).Failure is null)
            {
                marked++;
            }
            else
            {
                Engine.DeclineInvite(invite.Id);
            }
        }

        if (_options.CityEvents)
        {
            foreach (CityEventInstance e in agenda.CityEvents.Where(e => e.Date >= today && e.Date <= end).ToList())
            {
                if (TurnsOn(e.Date) < _options.TurnsPerDay && Affordable(CommitmentCatalog.EventoCidade)
                    && Engine.Schedule(new CommitmentRequest(CommitmentCatalog.EventoCidade, e.ZoneId, e.Date, e.Turno, CityEventId: e.Id)).Failure is null)
                {
                    marked++;
                }
            }
        }

        // Estresse alto: um descanso por dia até voltar ao limite.
        int rests = Math.Clamp(((State.Player.Resources.Stress - _options.RestAbove) / 5) + 1, 0, 7);
        for (DateOnly day = today; day <= end; day = day.AddDays(1))
        {
            foreach (Turno turno in new[] { Turno.Manha, Turno.Tarde, Turno.Noite })
            {
                if (TurnsOn(day) >= _options.TurnsPerDay)
                {
                    break;
                }

                if (rests > 0 && State.Player.Resources.Stress >= _options.RestAbove)
                {
                    if (Engine.Schedule(new CommitmentRequest(CommitmentCatalog.Descanso, null, day, turno)).Failure is null)
                    {
                        rests--;
                        marked++;
                    }

                    continue;
                }

                marked += FillTurn(day, turno) ? 1 : 0;
            }
        }

        return marked;
    }

    private int TurnsOn(DateOnly day) =>
        Engine.Agenda.Scheduled.Where(c => c.Date == day).Sum(c => Engine.Context.Campaign.Commitments.Get(c.Type).Turnos);

    /// <summary>Há caixa para o compromisso, contando o que já está marcado e ainda não foi pago.</summary>
    private bool Affordable(string typeId)
    {
        CommitmentCatalog catalog = Engine.Context.Campaign.Commitments;
        decimal cost = Engine.CostOf(catalog.Get(typeId));
        decimal committed = Engine.Agenda.Scheduled.Sum(c => Math.Max(0, Engine.CostOf(catalog.Get(c.Type))));
        return cost <= 0 || State.Player.Resources.Money - committed >= cost;
    }

    /// <summary>Tenta os tipos do ciclo, a partir do cursor, até um caber no turno.</summary>
    private bool FillTurn(DateOnly day, Turno turno)
    {
        for (int tries = 0; tries < _options.Cycle.Count; tries++)
        {
            string typeId = _options.Cycle[_type++ % _options.Cycle.Count];
            if (Affordable(typeId) && Request(typeId, day, turno) is { } request && Engine.Schedule(request).Failure is null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Monta o pedido do tipo no turno, rodando zona, público e liderança; null se o tipo não tem alvo agora.</summary>
    private CommitmentRequest? Request(string typeId, DateOnly day, Turno turno)
    {
        CommitmentType type = Engine.Context.Campaign.Commitments.Get(typeId);
        List<string> zones = Engine.Territory.Zones;
        if (type.SoEvento)
        {
            return null;
        }

        if (type.Visita)
        {
            List<Leader> known = Engine.City.Leaders.Where(l => l.Known).ToList();
            return known.Count == 0 ? null : new CommitmentRequest(typeId, null, day, turno, PersonId: known[_leader++ % known.Count].PersonId);
        }

        if (!type.ExigeZona)
        {
            return new CommitmentRequest(typeId, null, day, turno);
        }

        if (typeId == CommitmentCatalog.Comicio)
        {
            // Comício onde o candidato já é mais conhecido (senão esvazia).
            string best = zones.MaxBy(z => Engine.Territory.PresenceOf(RaceSetup.PlayerId, Engine.Territory.IndexOf(z)))!;
            return new CommitmentRequest(typeId, best, day, turno);
        }

        string zone = zones[_zone++ % zones.Count];
        if (type.EscolhePublico)
        {
            List<string> groups = Engine.Context.ElectorateContent.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id)
                .Where(g => Engine.Context.ZoneElectorates[Engine.Territory.IndexOf(zone)].VotersIn(g) > 0).ToList();
            return groups.Count == 0 ? null : new CommitmentRequest(typeId, zone, day, turno, groups[_group++ % groups.Count]);
        }

        // Compromisso de rua: leva junto uma liderança aliada da zona, se houver.
        string? companion = type.Rua ? Engine.City.Leaders.FirstOrDefault(l => l.AlignedWith == RaceSetup.PlayerId && l.Zones.Contains(zone))?.PersonId : null;
        return new CommitmentRequest(typeId, zone, day, turno, PersonId: companion);
    }

    /// <summary>Responde a tudo o que trava o relógio: debate, eventos e pedido de liderança.</summary>
    public void ResolveDecisions()
    {
        CampaignAutoplayTests.PlayDebate(Engine, _policy);
        while (Campaign.Events.Pending.Count > 0)
        {
            PendingEvent pending = Campaign.Events.Pending[0];
            EventDefinition definition = Engine.Context.Events!.Find(pending.EventId)!;
            var open = Enumerable.Range(0, definition.Choices.Count).Where(i => EventEngine.Blocked(Engine.Context, definition.Choices[i]) is null).ToList();
            Assert.NotEmpty(open);
            EventEngine.Resolve(Engine.Context, pending, _policy.Pick(open), State.Rng);
        }

        if (Engine.Agenda.PendingRequest is not null)
        {
            Engine.AnswerLeader(_options.Answer ?? (LeaderAnswer)_policy.NextInt(4));
        }
    }

    /// <summary>Na semana da votação: vota em si, branco para prefeito (ou em si, se disputa a prefeitura), e apura.</summary>
    public ElectionOutcome Vote()
    {
        ResolveDecisions();
        Candidate player = Campaign.Player;
        Engine.CastPlayerVote(new UrnaVote(player.Office, player.Number, false));
        Engine.CastPlayerVote(new UrnaVote(player.Office == Office.Vereador ? Office.Prefeito : Office.Vereador, null, true));
        return Engine.ResolveElection();
    }
}
