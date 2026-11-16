using System.Globalization;
using Planalto.Core.Electorate;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Campaign;

/// <summary>
/// Motor da campanha municipal (GDD 17.4 a 17.6 e 17.10): o dia é a unidade, com compromissos marcados na agenda;
/// na virada da semana agem os rivais e as lideranças, a presença decai, saem pesquisas e eventos. A apuração segue
/// as regras de lei. Toda aleatoriedade vem do Rng do estado.
/// </summary>
public sealed partial class CampaignEngine(CampaignContext ctx)
{
    public CampaignContext Context { get; } = ctx;

    private GameState State => Context.State;

    private CampaignState Campaign => State.Campaign!;

    private Rng Rng => State.Rng;

    private double CostScale => Context.Config.PorteCostMultiplier.GetValueOrDefault(Context.Municipio.Porte, 1);

    private double RecognitionScale => Context.Config.PorteRecognitionMultiplier.GetValueOrDefault(Context.Municipio.Porte, 1);

    /// <summary>Começa a campanha no município em foco, se ainda não houver uma.</summary>
    public static CampaignEngine Start(CampaignContext ctx)
    {
        ctx.State.Campaign ??= RaceSetup.Create(ctx, ctx.State.Rng);
        var engine = new CampaignEngine(ctx);
        engine.UpdatePhase();
        if (ctx.State.Campaign.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha)
        {
            engine.PlanWeek(ctx.State.Calendar.WeekStart);
        }

        engine.RefreshSupport();
        return engine;
    }

    /// <summary>Encomenda uma pesquisa (pública ou interna) paga pela campanha.</summary>
    public PollRecord? CommissionPoll(string pollsterId)
    {
        PollsterDefinition pollster = Context.ElectorateContent.Pollsters.Get(pollsterId);
        decimal cost = Math.Round(pollster.Cost * (decimal)CostScale / 100) * 100;
        if (State.Player.Resources.Money < cost)
        {
            return null;
        }

        EffectApplier.Apply(Context, new Effects { Money = -cost });
        return RunPoll(pollster);
    }

    private PollRecord RunPoll(PollsterDefinition pollster)
    {
        // A pesquisa é da disputa do jogador (vereador ou prefeito).
        Office office = Campaign.Player.Office;
        var candidates = Campaign.ForOffice(office).Where(c => !c.IsBulk).Select(ToVote).ToList();
        VoteProjection truth = Project(office, noise: null);
        var byId = Campaign.ForOffice(office).Select(ToVote).ToDictionary(c => c.Id);
        PollResult poll = Polling.Run(truth, byId, pollster, WeeksToElection, Context.ElectorateContent.VoteModel, Rng);
        var record = new PollRecord(
            State.Calendar.WeekStart,
            pollster.Id,
            poll.SampleSize,
            poll.MarginOfError,
            poll.Candidates.Where(c => candidates.Any(x => x.Id == c.Key)).ToDictionary(c => c.Key, c => Math.Round(c.Value, 1)),
            Math.Round(poll.BlankNull, 1),
            Math.Round(poll.Undecided, 1),
            pollster.Internal);
        Campaign.Polls.Add(record);
        return record;
    }

    public int WeeksToElection => Math.Max(0, (Campaign.ElectionDate.DayNumber - State.Calendar.WeekStart.DayNumber) / 7);

    /// <summary>Há uma decisão esperando o jogador (evento, debate ou pedido de liderança): o tempo não anda.</summary>
    public bool DecisionPending => Campaign.Events.Pending.Count > 0 || Campaign.Debate is not null || Agenda.PendingRequest is not null;

    /// <summary>
    /// Avança um dia: acontecem os compromissos de hoje e, no domingo, a virada da semana. Devolve os resultados
    /// explicados do dia. Com decisão pendente, nada anda.
    /// </summary>
    public List<CommitmentResult> AdvanceDay()
    {
        if (DecisionPending)
        {
            throw new InvalidOperationException("Há uma decisão esperando o jogador.");
        }

        List<CommitmentResult> results = RunToday();
        if (!DecisionPending)
        {
            Clock.AdvanceDay(State, AdvanceWeek);
        }

        return results;
    }

    /// <summary>
    /// Fecha a semana: acontecem os compromissos que restam nela, rivais e lideranças agem, a presença decai nas
    /// zonas sem compromisso, o calendário anda, e a semana que entra ganha chuva prevista, eventos da cidade,
    /// convites, pesquisa pública e eventos.
    /// </summary>
    public void AdvanceWeek()
    {
        if (Campaign.Events.Pending.Count > 0)
        {
            throw new InvalidOperationException("Há eventos esperando decisão.");
        }

        if (Campaign.Debate is not null)
        {
            throw new InvalidOperationException("Há um debate marcado para esta semana.");
        }

        if (Campaign.Phase is CampaignPhase.Votacao or CampaignPhase.Encerrada)
        {
            throw new InvalidOperationException("A semana da eleição se resolve na votação.");
        }

        RunUntil(State.Calendar.WeekEnd);
        if (Agenda.PendingRequest is not null)
        {
            throw new InvalidOperationException("Há um pedido de liderança esperando resposta.");
        }

        // A virada acontece no domingo, em qualquer dia que tenha sido pedida: convites vencidos, deriva das
        // lideranças e as datas do feed usam o dia de hoje, e sete dias seguidos têm de dar o mesmo que uma semana.
        State.Time.Day = 6;

        CampaignConfig config = Context.Config;
        TerritorySimulation sim = Context.Campaign.Territory.Simulacao;
        NpcTurn();
        foreach (Candidate c in Campaign.Candidates)
        {
            Territory.Decay(c.Id, sim.DecaimentoSemanal, c.IsBulk ? config.BulkRecognition * RecognitionScale : 0);
            c.Recognition = Territory.Recognition(c.Id);
            c.Image -= Math.Sign(c.Image) * Math.Min(Math.Abs(c.Image), config.ImageDecayPerWeek);
            foreach (string group in c.Support.Keys.ToList())
            {
                c.Support[group] *= 1 - config.SupportDecayPerWeek;
            }
        }

        foreach (Dictionary<string, double> meeting in Territory.MeetingSupport)
        {
            foreach (string group in meeting.Keys.ToList())
            {
                meeting[group] *= 1 - config.SupportDecayPerWeek;
            }
        }

        Territory.ActiveThisWeek.Clear();
        State.Player.Resources.Stress += Campaign.Phase == CampaignPhase.Campanha ? 2 : 0;
        ExpireInvites();
        DateOnly next = State.Calendar.WeekStart.AddDays(7);
        LeadersWeek(next);
        State.Ledger.Expire(next);
        State.Calendar.AdvanceWeek();
        State.Time.Day = 0;
        UpdatePhase();
        if (Campaign.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha)
        {
            AgendaWeek(State.Calendar.WeekStart);
        }

        if (Campaign.Phase == CampaignPhase.Campanha && WeeksToElection % config.PublicPollEveryWeeks == 0)
        {
            var publicPollsters = Context.ElectorateContent.Pollsters.Pollsters.Where(p => !p.Internal).ToList();
            PollRecord poll = RunPoll(Rng.Pick(publicPollsters));
            Campaign.Headlines.Add(new Headline(State.Calendar.WeekStart, $"poll:{poll.PollsterId}", "pesquisa"));
        }

        if (Campaign.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha)
        {
            EventEngine.Roll(Context, Rng);
        }

        // Debate de prefeito na TV (GDD 5.7), nas semanas marcadas.
        if (Context.Debate is { } debate && Debate.DebateEngine.ShouldSchedule(Context, debate))
        {
            Campaign.Debate = Debate.DebateEngine.Start(Context, debate, Rng);
        }
    }

    /// <summary>Avança até a próxima semana com evento, até o início da campanha ou até a eleição.</summary>
    public void AdvanceUntilSomethingHappens()
    {
        do
        {
            AdvanceWeek();
        }
        while (Campaign.Phase == CampaignPhase.PreCampanha && Campaign.Events.Pending.Count == 0 && Campaign.Debate is null);
    }

    private void UpdatePhase()
    {
        DateOnly week = State.Calendar.WeekStart;
        if (Campaign.Outcome is not null)
        {
            Campaign.Phase = CampaignPhase.Encerrada;
        }
        else if (State.Calendar.WeekEnd >= Campaign.ElectionDate)
        {
            Campaign.Phase = CampaignPhase.Votacao;
        }
        else
        {
            Campaign.Phase = week >= Campaign.CampaignStart || State.Calendar.Contains(Campaign.CampaignStart) ? CampaignPhase.Campanha : CampaignPhase.PreCampanha;
        }
    }

    /// <summary>
    /// Turno dos rivais (até a R3 dar plano a cada um): o número de saídas da semana vem do empenho e do porte, e
    /// cada saída rende presença numa zona. Rival nomeado a vereador vai mais aos seus redutos; candidatos a prefeito
    /// e o candidato coletivo de cada partido se espalham pela cidade. A primeira saída de cada rival vira fato no feed.
    /// </summary>
    private void NpcTurn()
    {
        CampaignConfig config = Context.Config;
        if (Campaign.Phase is not (CampaignPhase.PreCampanha or CampaignPhase.Campanha))
        {
            return;
        }

        TerritoryState t = Territory;
        TerritorySimulation sim = Context.Campaign.Territory.Simulacao;
        double basePresence = Commitments.Get("caminhada").Presenca * config.NpcActionEfficiency * RecognitionScale;
        double effort = config.NpcEffortByPorte.GetValueOrDefault(Context.Municipio.Porte, 1);
        var zones = Enumerable.Range(0, t.Zones.Count).ToList();
        foreach (Candidate npc in Campaign.Candidates.Where(c => !c.IsPlayer))
        {
            // Arredondamento estocástico: o empenho fracionário vira uma saída a mais com a probabilidade da fração.
            double expected = Rng.NextInt(config.NpcActionsPerWeek.Min, config.NpcActionsPerWeek.Max + 1) * npc.Drive * effort;
            int count = (int)Math.Floor(expected) + (Rng.NextDouble() < expected - Math.Floor(expected) ? 1 : 0);
            List<int>? redutos = t.Redutos.GetValueOrDefault(npc.Id);
            for (int i = 0; i < count; i++)
            {
                if (redutos is null)
                {
                    // Campanha espalhada: um pouco em todas as zonas.
                    t.GainEverywhere(npc.Id, basePresence * config.NpcSpreadFactor);
                    continue;
                }

                int zone = Rng.PickWeighted(zones, z => Math.Max(1, t.Population[z]) * (redutos.Contains(z) ? config.NpcRedutoWeight : 1));
                List<int> neighbors = Context.Zonas.Zonas[zone].Vizinhas.Select(t.IndexOf).Where(z => z >= 0).ToList();
                t.Gain(npc.Id, zone, basePresence * Reach(zone), sim.Transbordo, neighbors);
                if (i == 0)
                {
                    DateOnly day = State.Calendar.WeekStart.AddDays(Rng.NextInt(0, 7));
                    Agenda.AddFeed(new FeedItem(day, "rival_zona", $"{npc.Name} fez campanha em {ZoneName(t.Zones[zone])}", t.Zones[zone], null, npc.Id));
                }
            }

            npc.Image = Math.Clamp(npc.Image + (count * config.NpcImagePerAction), -1, 1);
            npc.Recognition = t.Recognition(npc.Id);
        }
    }

    public VoteCandidate ToVote(Candidate c) => new()
    {
        Id = c.Id,
        PartyId = c.PartyId,
        Position = c.Position,
        Recognition = c.Recognition / 100,
        Image = c.Image,
        Support = c.Support,
        Multiplicity = c.Multiplicity,
    };

    /// <summary>Projeção da cidade: a soma das projeções das zonas (GDD 17.5).</summary>
    public VoteProjection Project(Office office, Rng? noise) => ProjectByZone(office, noise).City;

    /// <summary>Projeção de cada zona e da cidade, com a presença e o apoio de cada candidato em cada zona.</summary>
    public (VoteProjection City, IReadOnlyList<VoteProjection> Zones) ProjectByZone(Office office, Rng? noise) =>
        ProjectByZone(Campaign.ForOffice(office).Select(ToVote).ToList(), noise);

    private (VoteProjection City, IReadOnlyList<VoteProjection> Zones) ProjectByZone(List<VoteCandidate> candidates, Rng? noise) =>
        TerritoryVote.Project(Context.ZoneElectorates, Territory, candidates, Context.ElectorateContent.VoteModel, Context.Themes, noise);

    /// <summary>Registra o voto do jogador na urna (GDD 16.3). O voto conta como um voto real.</summary>
    public void CastPlayerVote(UrnaVote vote)
    {
        Campaign.PlayerVotes.RemoveAll(v => v.Office == vote.Office);
        Campaign.PlayerVotes.Add(vote);
    }

    /// <summary>Apuração: votos pelo modelo com o ruído da eleição, regras de lei e parciais da noite da apuração.</summary>
    public ElectionOutcome ResolveElection()
    {
        if (Campaign.Phase != CampaignPhase.Votacao)
        {
            throw new InvalidOperationException("Ainda não é a semana da eleição.");
        }

        var bulkVotes = new Dictionary<string, List<long>>(StringComparer.Ordinal);
        OfficeOutcome vereador = ResolveVereador(bulkVotes);
        OfficeOutcome prefeito = ResolvePrefeito();
        var outcome = new ElectionOutcome
        {
            Date = Campaign.ElectionDate,
            Vereador = vereador,
            Prefeito = prefeito,
            PlayerElected = (Campaign.Player.Office == Office.Prefeito ? prefeito : vereador).Elected.Contains(RaceSetup.PlayerId),
            BulkVotes = bulkVotes,
        };
        Campaign.Outcome = outcome;
        Campaign.Phase = CampaignPhase.Encerrada;
        return outcome;
    }

    private OfficeOutcome ResolveVereador(Dictionary<string, List<long>> bulkVotes)
    {
        CampaignConfig config = Context.Config;
        VoteProjection projection = Project(Office.Vereador, Rng);
        var votes = projection.Votes.ToDictionary(v => v.Key, v => (long)Math.Round(v.Value), StringComparer.Ordinal);
        long blank = (long)Math.Round(projection.BlankNull * 0.5);
        long nul = (long)Math.Round(projection.BlankNull) - blank;
        var legend = new Dictionary<string, long>(StringComparer.Ordinal);
        ApplyPlayerVote(Office.Vereador, votes, legend, ref blank, ref nul);

        var lists = new List<PartyList>();
        foreach (var group in Campaign.ForOffice(Office.Vereador).GroupBy(c => c.PartyId))
        {
            var candidates = new List<CandidateVotes>();
            long partyTotal = group.Sum(c => votes[c.Id]);
            long legendVotes = (long)Math.Round(partyTotal * config.LegendVoteShare) + legend.GetValueOrDefault(group.Key);
            foreach (Candidate c in group)
            {
                if (c.IsBulk)
                {
                    // O coletivo vira candidatos anônimos com votos decrescentes; parte vai para a legenda.
                    long pool = Math.Max(0, votes[c.Id] - (long)Math.Round(partyTotal * config.LegendVoteShare));
                    List<long> split = Split(pool, config.BulkCandidatesPerParty);
                    bulkVotes[c.PartyId] = split;
                    for (int i = 0; i < split.Count; i++)
                    {
                        candidates.Add(new CandidateVotes($"{c.Id}_{i + 1}", split[i], new DateOnly(1980, 1, 1).AddDays(i)));
                    }
                }
                else
                {
                    candidates.Add(new CandidateVotes(c.Id, votes[c.Id], c.BirthDate));
                }
            }

            lists.Add(new PartyList(group.Key, legendVotes, candidates));
        }

        ProportionalResult result = ElectionRules.Proportional(Context.SeatsIn(Campaign.ElectionDate.Year), lists);
        var nominal = lists.SelectMany(l => l.Candidates).ToDictionary(c => c.CandidateId, c => c.Votes, StringComparer.Ordinal);
        return new OfficeOutcome
        {
            Office = Office.Vereador,
            Votes = Campaign.ForOffice(Office.Vereador).ToDictionary(c => c.Id, c => votes[c.Id], StringComparer.Ordinal),
            Blank = blank,
            Null = nul,
            Abstention = (long)Math.Round(projection.Abstention),
            Elected = result.Elected.Select(e => e.CandidateId).ToList(),
            SeatsByParty = lists.ToDictionary(l => l.PartyId, l => result.SeatsOf(l.PartyId), StringComparer.Ordinal),
            ElectoralQuotient = result.ElectoralQuotient,
            Snapshots = Snapshots(projection, votes),
        };
    }

    private OfficeOutcome ResolvePrefeito()
    {
        VoteProjection projection = Project(Office.Prefeito, Rng);
        var votes = projection.Votes.ToDictionary(v => v.Key, v => (long)Math.Round(v.Value), StringComparer.Ordinal);
        long blank = (long)Math.Round(projection.BlankNull * 0.5);
        long nul = (long)Math.Round(projection.BlankNull) - blank;
        var legend = new Dictionary<string, long>();
        ApplyPlayerVote(Office.Prefeito, votes, legend, ref blank, ref nul);

        var mayors = Campaign.ForOffice(Office.Prefeito).ToList();
        MajoritarianResult first = ElectionRules.Majoritarian(
            mayors.Select(c => new CandidateVotes(c.Id, votes[c.Id], c.BirthDate)).ToList(),
            ElectionRules.TemSegundoTurno(Context.Municipio.Eleitorado));
        string? winner = first.WinnerId;
        if (first.Outcome == MajoritarianOutcome.Runoff)
        {
            // 2º turno: nova projeção só com os dois finalistas.
            var finalists = mayors.Where(c => first.RunoffCandidateIds.Contains(c.Id)).Select(ToVote).ToList();
            VoteProjection runoff = ProjectByZone(finalists, Rng).City;
            winner = ElectionRules.Runoff(mayors.Where(c => first.RunoffCandidateIds.Contains(c.Id))
                .Select(c => new CandidateVotes(c.Id, (long)Math.Round(runoff.Votes[c.Id]), c.BirthDate)).ToList()).WinnerId;
        }

        return new OfficeOutcome
        {
            Office = Office.Prefeito,
            Votes = votes,
            Blank = blank,
            Null = nul,
            Abstention = (long)Math.Round(projection.Abstention),
            Elected = winner is null ? [] : [winner],
            Snapshots = Snapshots(projection, votes),
            HadRunoff = first.Outcome == MajoritarianOutcome.Runoff,
            RunoffCandidates = [.. first.RunoffCandidateIds],
        };
    }

    private void ApplyPlayerVote(Office office, Dictionary<string, long> votes, Dictionary<string, long> legend, ref long blank, ref long nul)
    {
        if (Campaign.PlayerVotes.FirstOrDefault(v => v.Office == office) is not { } vote)
        {
            return;
        }

        if (vote.Blank)
        {
            blank++;
        }
        else if (Campaign.ForOffice(office).FirstOrDefault(c => !c.IsBulk && c.Number == vote.Number) is { } chosen)
        {
            votes[chosen.Id]++;
        }
        else if (office == Office.Vereador && vote.Number is { Length: 2 } partyNumber
            && State.Parties.FirstOrDefault(p => p.Number.ToString(CultureInfo.InvariantCulture) == partyNumber) is { } party
            && Campaign.ForOffice(office).Any(c => c.PartyId == party.Id))
        {
            legend[party.Id] = legend.GetValueOrDefault(party.Id) + 1;
        }
        else
        {
            nul++;
        }
    }

    /// <summary>Parciais da apuração: segmentos apurados em ordem sorteada, acumulando os votos.</summary>
    private List<CountSnapshot> Snapshots(VoteProjection projection, Dictionary<string, long> final)
    {
        int steps = Context.Config.CountSnapshots;
        var order = Rng.Shuffled(projection.Segments);
        double totalVoters = order.Sum(s => s.Segment.Voters * s.Turnout);
        var snapshots = new List<CountSnapshot>();
        var running = final.Keys.ToDictionary(k => k, _ => 0.0, StringComparer.Ordinal);
        double counted = 0;
        int index = 0;
        for (int step = 1; step <= steps; step++)
        {
            double target = totalVoters * step / steps;
            while (index < order.Count && counted < target)
            {
                SegmentIntention s = order[index++];
                double voters = s.Segment.Voters * s.Turnout * (1 - s.BlankNull);
                foreach (var (id, share) in s.Shares)
                {
                    running[id] += voters * share;
                }

                counted += s.Segment.Voters * s.Turnout;
            }

            // A última parcial é exatamente o resultado final.
            snapshots.Add(new CountSnapshot(
                Math.Round(100.0 * step / steps, 1),
                step == steps ? new Dictionary<string, long>(final) : running.ToDictionary(r => r.Key, r => (long)Math.Round(r.Value), StringComparer.Ordinal)));
        }

        return snapshots;
    }

    /// <summary>Reparte votos em n partes decrescentes (1/i), como a cauda de candidatos de um partido.</summary>
    public static List<long> Split(long total, int n)
    {
        double sum = Enumerable.Range(1, n).Sum(i => 1.0 / i);
        var parts = Enumerable.Range(1, n).Select(i => (long)Math.Floor(total / sum / i)).ToList();
        parts[0] += total - parts.Sum();
        return parts;
    }
}
