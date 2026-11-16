using System.Globalization;
using Planalto.Core.Electorate;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.State;

namespace Planalto.Core.Campaign;

public enum ActionFailure
{
    FaseNaoPermite,
    SemEnergia,
    SemDinheiro,
    AlvoInvalido,
    EventoPendente,
}

/// <summary>Resultado de uma ação da agenda.</summary>
public sealed record ActionResult(bool Ok, ActionFailure? Failure, string Text, decimal MoneyDelta);

/// <summary>
/// Motor da campanha municipal (GDD 5.6 e 6.1): agenda semanal com Energia e Dinheiro, IA dos rivais,
/// pesquisas, eventos e apuração. Toda aleatoriedade vem do Rng do estado.
/// </summary>
public sealed class CampaignEngine(CampaignContext ctx)
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
        return engine;
    }

    public decimal CostOf(CampaignActionDefinition action)
    {
        decimal money = Math.Round(action.Money * (decimal)CostScale / 100) * 100;
        if (action.MoneyPerAttribute is { } per)
        {
            money -= per.Amount * State.Player.Attributes.Get(per.Attribute) * (decimal)CostScale;
        }

        return money;
    }

    public ActionFailure? CanExecute(CampaignActionDefinition action, string? targetId)
    {
        if (Campaign.Events.Pending.Count > 0)
        {
            return ActionFailure.EventoPendente;
        }

        if (!action.AllowedIn(Campaign.Phase))
        {
            return ActionFailure.FaseNaoPermite;
        }

        if (Campaign.EnergyLeft < action.Energy)
        {
            return ActionFailure.SemEnergia;
        }

        decimal cost = CostOf(action);
        if (cost > 0 && State.Player.Resources.Money < cost)
        {
            return ActionFailure.SemDinheiro;
        }

        return action.Targets.Count > 0 && action.Targets.All(t => t.Id != targetId) ? ActionFailure.AlvoInvalido : null;
    }

    public ActionResult Execute(string actionId, string? targetId = null)
    {
        CampaignActionDefinition action = Context.Campaign.Actions.Get(actionId);
        if (CanExecute(action, targetId) is ActionFailure failure)
        {
            return new ActionResult(false, failure, "", 0);
        }

        Campaign.EnergyLeft -= action.Energy;
        decimal cost = CostOf(action);
        var texts = new List<string>();
        EffectApplier.Apply(Context, new Effects { Money = -cost }, recognitionScale: RecognitionScale);
        EffectApplier.Apply(Context, action.Effects, recognitionScale: RecognitionScale);

        if (action.Targets.FirstOrDefault(t => t.Id == targetId) is { } target)
        {
            EffectApplier.Apply(Context, new Effects { Support = new() { [target.Group] = target.Support } });
        }

        if (action.Test is { } test)
        {
            bool passed = Rng.Chance(Math.Clamp(State.Player.Attributes.Get(test.Attribute) / 100.0, 0.05, 0.95));
            EffectApplier.Apply(Context, passed ? test.Success : test.Failure, recognitionScale: RecognitionScale);
            texts.Add(passed ? test.SuccessText : test.FailureText);
        }

        foreach (ActionRisk risk in action.Risks)
        {
            if (Rng.Chance(risk.Chance))
            {
                EffectApplier.Apply(Context, risk.Effects, recognitionScale: RecognitionScale);
                texts.Add(risk.Text);
            }
        }

        Campaign.ActionsThisWeek.Add(actionId);
        return new ActionResult(true, null, string.Join(" ", texts), -cost);
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

    /// <summary>Fecha a semana: rivais agem, efeitos decaem, o calendário anda, sai pesquisa pública e novos eventos.</summary>
    public void AdvanceWeek()
    {
        if (Campaign.Events.Pending.Count > 0)
        {
            throw new InvalidOperationException("Há eventos esperando decisão.");
        }

        if (Campaign.Phase is CampaignPhase.Votacao or CampaignPhase.Encerrada)
        {
            throw new InvalidOperationException("A semana da eleição se resolve na votação.");
        }

        CampaignConfig config = Context.Config;
        NpcTurn();
        foreach (Candidate c in Campaign.Candidates)
        {
            c.Recognition = Math.Max(c.IsBulk ? config.BulkRecognition * RecognitionScale : 0, c.Recognition - config.RecognitionDecayPerWeek);
            c.Image -= Math.Sign(c.Image) * Math.Min(Math.Abs(c.Image), config.ImageDecayPerWeek);
            foreach (string group in c.Support.Keys.ToList())
            {
                c.Support[group] *= 1 - config.SupportDecayPerWeek;
            }
        }

        State.Player.Resources.Stress += Campaign.Phase == CampaignPhase.Campanha ? 2 : 0;
        Campaign.ActionsThisWeek.Clear();
        Campaign.EnergyLeft = config.WeeklyEnergy;
        State.Calendar.AdvanceWeek();
        UpdatePhase();

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
    }

    /// <summary>Avança até a próxima semana com evento, até o início da campanha ou até a eleição.</summary>
    public void AdvanceUntilSomethingHappens()
    {
        do
        {
            AdvanceWeek();
        }
        while (Campaign.Phase == CampaignPhase.PreCampanha && Campaign.Events.Pending.Count == 0);
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
    /// Turno dos rivais: ações sorteadas pelo empenho, com eficiência menor que a do jogador. O candidato coletivo
    /// de cada partido também age (só reconhecimento e imagem; os apoios ficam com os nomeados).
    /// </summary>
    private void NpcTurn()
    {
        CampaignConfig config = Context.Config;
        var actions = Context.Campaign.Actions.Actions.Where(a => a.AllowedIn(Campaign.Phase) && a.MoneyPerAttribute is null).ToList();
        if (actions.Count == 0)
        {
            return;
        }

        double effort = config.NpcEffortByPorte.GetValueOrDefault(Context.Municipio.Porte, 1);
        foreach (Candidate npc in Campaign.Candidates.Where(c => !c.IsPlayer))
        {
            // Arredondamento estocástico: o empenho fracionário vira uma ação a mais com a probabilidade da fração.
            double expected = Rng.NextInt(config.NpcActionsPerWeek.Min, config.NpcActionsPerWeek.Max + 1) * npc.Drive * effort;
            int count = (int)Math.Floor(expected) + (Rng.NextDouble() < expected - Math.Floor(expected) ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                CampaignActionDefinition action = Rng.Pick(actions);
                double gain = action.Effects.Recognition * config.NpcActionEfficiency * RecognitionScale;
                npc.Recognition = EffectApplier.Gain(npc.Recognition, gain);
                npc.Image = Math.Clamp(npc.Image + (action.Effects.Image * config.NpcActionEfficiency), -1, 1);
                if (npc.IsBulk)
                {
                    continue;
                }

                ActionTarget? target = action.Targets.Count > 0 ? Rng.Pick(action.Targets) : null;
                foreach (var (group, delta) in action.Effects.Support.Concat(target is null ? [] : [new KeyValuePair<string, double>(target.Group, target.Support)]))
                {
                    npc.Support[group] = Math.Clamp(npc.Support.GetValueOrDefault(group) + (delta * config.NpcActionEfficiency), 0, 1);
                }
            }
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

    public VoteProjection Project(Office office, Rng? noise) =>
        VoteModel.Project(Context.Electorate, Campaign.ForOffice(office).Select(ToVote).ToList(), Context.ElectorateContent.VoteModel, Context.Themes, noise);

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

        ProportionalResult result = ElectionRules.Proportional(Context.Municipio.VagasVereador, lists);
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
            VoteProjection runoff = VoteModel.Project(Context.Electorate, finalists, Context.ElectorateContent.VoteModel, Context.Themes, Rng);
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
