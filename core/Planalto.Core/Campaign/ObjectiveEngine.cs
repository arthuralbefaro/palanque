using Planalto.Core.Electorate;
using Planalto.Core.People;

namespace Planalto.Core.Campaign;

/// <summary>
/// Objetivos que nascem do jogo (GDD 17.2, peça 6): toda segunda-feira o gerador lê o estado e propõe metas com
/// condição mensurável, prazo e origem, até o máximo de ativas. O jogador pode fixar ou dispensar; cumprir rende
/// lealdade à equipe e uma lembrança positiva em quem se beneficiou. Nada aqui é sorteado.
/// </summary>
public sealed partial class CampaignEngine
{
    private ObjectiveCatalog ObjectiveContent => Context.Campaign.Objectives;

    private ObjectiveRules ObjectiveRules => ObjectiveContent.Regras;

    public ObjectiveState Objectives => Campaign.Objectives;

    private sealed record Measurement(double Current, bool Done, bool Failed);

    /// <summary>A medida de hoje da condição do objetivo. A projeção só é calculada se algum objetivo precisar.</summary>
    private Measurement Measure(Objective o, ref VoteProjection? projection)
    {
        switch (o.Kind)
        {
            case ObjectiveType.PresencaZona:
                int zone = Territory.IndexOf(o.TargetId);
                double presence = zone < 0 ? 0 : Territory.PresenceOf(RaceSetup.PlayerId, zone);
                return new Measurement(presence, presence >= o.Goal - 1e-9, zone < 0);
            case ObjectiveType.PassarRival:
                projection ??= Project(Campaign.Player.Office, null);
                if (!projection.Votes.TryGetValue(o.TargetId, out double rival))
                {
                    return new Measurement(0, false, true);
                }

                double gap = projection.Votes[RaceSetup.PlayerId] - rival;
                return new Measurement(gap, gap > 0, false);
            case ObjectiveType.TrazerLideranca:
                Leader? leader = City.Leader(o.TargetId);
                return new Measurement(leader is null ? 0 : RelationOf(leader.PersonId), leader?.AlignedWith == RaceSetup.PlayerId, leader is null);
            case ObjectiveType.JuntarCaixa:
                double money = (double)State.Player.Resources.Money;
                return new Measurement(money, money >= o.Goal, false);
            case ObjectiveType.PosicaoConhecida:
                double known = KnownOf(o.TargetId);
                return new Measurement(known, known >= o.Goal - 1e-9, false);
            case ObjectiveType.CumprirPromessa:
                Promise? promise = State.Ledger.Promises.FirstOrDefault(p => p.Id == o.TargetId);
                return new Measurement(promise?.State == PromiseState.Cumprida ? 1 : 0, promise?.State == PromiseState.Cumprida, promise is null or { State: PromiseState.Quebrada or PromiseState.Vencida });
            case ObjectiveType.MontarEquipe:
                bool filled = StaffIn(o.TargetId) is not null;
                return new Measurement(filled ? 1 : 0, filled, false);
            default:
                return new Measurement(0, false, true);
        }
    }

    /// <summary>Objetivos ativos com a medida de hoje, a meta e o progresso, para o painel.</summary>
    public IReadOnlyList<ObjectiveView> ActiveObjectives()
    {
        VoteProjection? projection = null;
        var views = new List<ObjectiveView>();
        foreach (Objective o in Objectives.Active)
        {
            Measurement m = Measure(o, ref projection);
            double span = o.Goal - o.Start;
            double progress = m.Done ? 1 : Math.Abs(span) < 1e-9 ? 0 : Math.Clamp((m.Current - o.Start) / span, 0, 1);
            views.Add(new ObjectiveView(o, ObjectiveContent.Type(o.Kind).Nome, m.Current, o.Goal, progress, m.Done, Math.Max(0, o.Deadline.DayNumber - Today.DayNumber)));
        }

        return views;
    }

    /// <summary>Fixa (ou solta) um objetivo: o fixado fica no painel até o prazo, em vez de ser trocado por uma proposta nova.</summary>
    public bool PinObjective(string id, bool pinned = true)
    {
        if (Objectives.Active.FirstOrDefault(o => o.Id == id) is not { } objective)
        {
            return false;
        }

        objective.Pinned = pinned;
        return true;
    }

    /// <summary>Dispensa um objetivo. Se o painel ficar vazio, o gerador propõe outro na hora (nunca o mesmo).</summary>
    public bool DismissObjective(string id)
    {
        if (Objectives.Active.FirstOrDefault(o => o.Id == id) is not { } objective)
        {
            return false;
        }

        CloseObjective(objective, ObjectiveStatus.Dispensado);
        if (Objectives.Active.Count == 0 && CampaignRunning)
        {
            GenerateObjectives(minimum: 1, excluded: objective);
        }

        return true;
    }

    private void CloseObjective(Objective o, ObjectiveStatus status)
    {
        o.Status = status;
        o.Closed = Today;
        Objectives.Active.Remove(o);
        Objectives.History.Add(o);
        if (Objectives.History.Count > ObjectiveRules.Historico)
        {
            Objectives.History.RemoveRange(0, Objectives.History.Count - ObjectiveRules.Historico);
        }

        if (status is ObjectiveStatus.Cumprido or ObjectiveStatus.Vencido)
        {
            string key = status == ObjectiveStatus.Cumprido ? "cumprido" : "vencido";
            Agenda.AddFeed(new FeedItem(Today, "objetivo", Texts.Fill(ObjectiveContent.Texto(key), ("texto", o.Text)), o.Kind == ObjectiveType.PresencaZona ? o.TargetId : null, o.BeneficiaryId, RaceSetup.PlayerId));
        }
    }

    /// <summary>Objetivo cumprido: +lealdade em toda a equipe e lembrança positiva em quem se beneficiou.</summary>
    private void RewardObjective(Objective o)
    {
        foreach (StaffMember member in HiredStaff)
        {
            ChangeLoyalty(member, ObjectiveRules.LealdadeEquipe);
        }

        o.BeneficiaryId = Beneficiary(o) ?? o.BeneficiaryId;
        MemoryDefinition memory = ObjectiveContent.Lembranca;
        if (o.BeneficiaryId is { } id && City.Leader(id) is { } leader)
        {
            RememberLeader(leader, "objetivo_cumprido", memory.Peso, memory.Pontos, memory.Texto, contact: false);
            Realign(leader);
            RefreshSupport();
        }
        else if (o.BeneficiaryId is { } personId && City.Person(personId) is { } person)
        {
            person.Remember(Today, "objetivo_cumprido", memory.Peso, memory.Pontos, memory.Texto, PeopleConfig);
        }
    }

    /// <summary>Quem se beneficia do objetivo cumprido: a pessoa do alvo ou quem cuida do assunto na cidade ou na equipe.</summary>
    private string? Beneficiary(Objective o)
    {
        string? Closest(Func<Leader, bool> filter) =>
            City.Leaders.Where(l => l.Known && filter(l)).OrderByDescending(l => RelationOf(l.PersonId)).ThenBy(l => l.PersonId, StringComparer.Ordinal).FirstOrDefault()?.PersonId;

        return o.Kind switch
        {
            ObjectiveType.PresencaZona => Closest(l => l.Zones.Contains(o.TargetId)) ?? StaffIn(StaffRole.Coordenador)?.PersonId,
            ObjectiveType.PassarRival => StaffIn(StaffRole.Coordenador)?.PersonId ?? StaffIn(StaffRole.Marqueteiro)?.PersonId,
            ObjectiveType.TrazerLideranca => o.TargetId,
            ObjectiveType.JuntarCaixa => StaffIn(StaffRole.Tesoureiro)?.PersonId,
            ObjectiveType.PosicaoConhecida => Closest(l => l.Theme == o.TargetId) ?? StaffIn(StaffRole.Marqueteiro)?.PersonId,
            ObjectiveType.CumprirPromessa => State.Ledger.Promises.FirstOrDefault(p => p.Id == o.TargetId) is { ToKind: Promise.ParaPessoa } promise ? promise.ToId : null,
            ObjectiveType.MontarEquipe => StaffIn(o.TargetId)?.PersonId,
            _ => null,
        };
    }

    /// <summary>Garante o painel no começo da campanha (e em saves anteriores ao gerador).</summary>
    private void EnsureObjectives()
    {
        if (CampaignRunning && Objectives.Active.Count == 0)
        {
            GenerateObjectives(ObjectiveRules.Maximo, null);
        }
    }

    /// <summary>
    /// Segunda-feira: fecha os objetivos cumpridos (com a recompensa) e os vencidos, retira as propostas não fixadas
    /// que passaram da validade e propõe novas até completar o painel.
    /// </summary>
    private void ObjectivesWeek()
    {
        if (!CampaignRunning)
        {
            return;
        }

        VoteProjection? projection = null;
        foreach (Objective o in Objectives.Active.ToList())
        {
            Measurement m = Measure(o, ref projection);
            if (m.Done)
            {
                RewardObjective(o);
                CloseObjective(o, ObjectiveStatus.Cumprido);
            }
            else if (m.Failed || o.Deadline < Today)
            {
                CloseObjective(o, ObjectiveStatus.Vencido);
            }
            else if (!o.Pinned && (Today.DayNumber - o.Created.DayNumber) / 7 >= ObjectiveRules.ValidadePropostaSemanas)
            {
                CloseObjective(o, ObjectiveStatus.Retirado);
            }
        }

        GenerateObjectives(ObjectiveRules.Maximo, null);
    }

    /// <summary>
    /// Propõe objetivos até o painel ter <paramref name="minimum"/>: primeiro os tipos que ainda não apareceram nesta
    /// campanha, depois os usados há mais tempo. Nunca dois do mesmo tipo ao mesmo tempo.
    /// </summary>
    private void GenerateObjectives(int minimum, Objective? excluded)
    {
        if (Objectives.Active.Count >= minimum)
        {
            return;
        }

        VoteProjection? projection = null;
        List<Objective> all = [.. Objectives.History, .. Objectives.Active];
        var proposals = new List<(Objective Objective, ObjectiveType Type)>();
        foreach (ObjectiveType type in ObjectiveContent.Tipos)
        {
            if (Objectives.Active.Any(a => a.Kind == type.Id))
            {
                continue;
            }

            Objective? proposal = Propose(type, ref projection);
            if (proposal is not null && !(excluded is not null && excluded.Kind == proposal.Kind && excluded.TargetId == proposal.TargetId))
            {
                proposals.Add((proposal, type));
            }
        }

        DateOnly LastUse(string kind) => all.Where(o => o.Kind == kind).Select(o => o.Created).DefaultIfEmpty(DateOnly.MinValue).Max();
        foreach (var (objective, _) in proposals
            .OrderBy(p => LastUse(p.Type.Id))
            .ThenByDescending(p => p.Type.Weight)
            .ThenBy(p => p.Type.Id, StringComparer.Ordinal))
        {
            if (Objectives.Active.Count >= minimum)
            {
                break;
            }

            Objectives.Active.Add(objective);
        }

        // O painel nunca fica vazio: se só sobrou a meta que o jogador acabou de dispensar, ela volta.
        if (Objectives.Active.Count == 0 && excluded is not null)
        {
            GenerateObjectives(1, null);
        }
    }

    private DateOnly ObjectiveDeadline(ObjectiveType type)
    {
        DateOnly last = Campaign.ElectionDate.AddDays(-1);
        DateOnly deadline = Today.AddDays(7 * Math.Max(1, type.PrazoSemanas));
        return deadline > last ? last : deadline;
    }

    private Objective NewObjective(ObjectiveType type, string targetId, double goal, double start, DateOnly deadline, string text, string origin, string? beneficiary = null) => new()
    {
        Id = $"ob{Objectives.NextId++}",
        Kind = type.Id,
        Text = text,
        Origin = origin,
        TargetId = targetId,
        Goal = goal,
        Start = start,
        Created = Today,
        Deadline = deadline,
        BeneficiaryId = beneficiary,
    };

    /// <summary>A meta que o tipo proporia hoje, lida do estado; null se não há o que propor.</summary>
    private Objective? Propose(ObjectiveType type, ref VoteProjection? projection)
    {
        DateOnly deadline = ObjectiveDeadline(type);
        if (deadline < Today)
        {
            return null;
        }

        string Text(string target, string value = "") => Texts.Fill(type.Texto, ("alvo", target), ("valor", value), ("prazo", Texts.Date(deadline)));
        switch (type.Id)
        {
            case ObjectiveType.PresencaZona:
            {
                double ceiling = ObjectiveRules.PresencaTeto;
                double Presence(int z) => Territory.PresenceOf(RaceSetup.PlayerId, z);
                int advised = Team.Advice is { State: AdviceState.Aberto } advice ? Territory.IndexOf(advice.ZoneId) : -1;
                bool fromAdvice = advised >= 0 && Presence(advised) + type.Passo <= ceiling && type.OrigemConselho is not null && StaffIn(StaffRole.Coordenador) is not null;
                int zone = fromAdvice ? advised : Context.ZoneElectorates
                    .Where(z => Presence(z.Index) + type.Passo <= ceiling && z.Voters > 0)
                    .OrderByDescending(z => z.Voters * (100 - Presence(z.Index)))
                    .ThenBy(z => z.Index)
                    .Select(z => z.Index)
                    .DefaultIfEmpty(-1)
                    .First();
                if (zone < 0)
                {
                    return null;
                }

                string zoneId = Territory.Zones[zone];
                double now = Presence(zone);
                double goal = Math.Min(ceiling, Math.Ceiling(now + type.Passo));
                StaffMember? coordinator = StaffIn(StaffRole.Coordenador);
                string origin = fromAdvice
                    ? Texts.Fill(type.OrigemConselho!, ("nome", NameOf(coordinator!.PersonId)), ("papel", StaffContent.Role(StaffRole.Coordenador).Papel), ("alvo", ZoneName(zoneId)))
                    : Texts.Fill(type.Origem, ("alvo", ZoneName(zoneId)), ("eleitores", Texts.Number(Math.Round(Context.ZoneElectorates[zone].Voters), "N0")), ("atual", Texts.Number(now, "0")));
                return NewObjective(type, zoneId, goal, now, deadline, Text(ZoneName(zoneId), Texts.Number(goal, "0")), origin, fromAdvice ? coordinator!.PersonId : null);
            }

            case ObjectiveType.PassarRival:
            {
                projection ??= Project(Campaign.Player.Office, null);
                VoteProjection p = projection;
                double own = p.Votes[RaceSetup.PlayerId];
                Candidate? ahead = Campaign.ForOffice(Campaign.Player.Office)
                    .Where(c => !c.IsPlayer && !c.IsBulk && p.Votes[c.Id] > own)
                    .OrderBy(c => p.Votes[c.Id])
                    .ThenBy(c => c.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (ahead is null)
                {
                    return null;
                }

                double gap = own - p.Votes[ahead.Id];
                string origin = Texts.Fill(type.Origem, ("alvo", ahead.Name), ("atual", Texts.Number(Math.Ceiling(-gap), "N0")));
                return NewObjective(type, ahead.Id, 0, gap, deadline, Text(ahead.Name), origin);
            }

            case ObjectiveType.TrazerLideranca:
            {
                Leader? leader = City.Leaders
                    .Where(l => l.Known && l.AlignedWith != RaceSetup.PlayerId)
                    .OrderByDescending(l => l.RequestKnown)
                    .ThenByDescending(l => l.Influence)
                    .ThenBy(l => l.PersonId, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (leader is null)
                {
                    return null;
                }

                string name = NameOf(leader.PersonId);
                string origin = Texts.Fill(type.Origem, ("alvo", name), ("papel", LeaderContent.Type(leader.Type).Papel), ("zona", ZoneName(leader.Zones[0])));
                return NewObjective(type, leader.PersonId, leader.Price, RelationOf(leader.PersonId), deadline, Text(name), origin, leader.PersonId);
            }

            case ObjectiveType.JuntarCaixa:
            {
                decimal money = Math.Max(0, State.Player.Resources.Money);
                decimal weekly = Payroll().Weekly + Budget.Plan.Sum(a => a.Amount);
                decimal goal = Math.Ceiling((money + ScaledMoney((decimal)type.Passo)) / 100) * 100;
                string origin = Texts.Fill(type.Origem, ("atual", Texts.Money(money)), ("semanal", Texts.Money(weekly)));
                return NewObjective(type, "caixa", (double)goal, (double)money, deadline, Text("", Texts.Money(goal)), origin);
            }

            case ObjectiveType.PosicaoConhecida:
            {
                double ceiling = ObjectiveRules.ConhecidaTeto;
                double Priority(string theme) => Context.Electorate.Segments.Sum(s => s.Voters * s.ThemePriority.GetValueOrDefault(theme));
                string? themeId = Platform.Flags
                    .Concat(Platform.Positions.Keys.Order(StringComparer.Ordinal))
                    .Concat(Context.Themes.Themes.OrderByDescending(t => Priority(t.Id)).ThenBy(t => t.Id, StringComparer.Ordinal).Select(t => t.Id))
                    .FirstOrDefault(t => KnownOf(t) + (type.Passo / 2) <= ceiling);
                if (themeId is null)
                {
                    return null;
                }

                double now = KnownOf(themeId);
                double goal = Math.Min(ceiling, now + type.Passo);
                string name = Theme(themeId)!.Name;
                string origin = Texts.Fill(type.Origem, ("alvo", name), ("atual", Texts.Number(now * 100, "0")));
                return NewObjective(type, themeId, goal, now, deadline, Text(name, Texts.Number(goal * 100, "0")), origin);
            }

            case ObjectiveType.CumprirPromessa:
            {
                foreach (Promise promise in State.Ledger.Open.Where(p => p.ToKind == Promise.ParaPessoa && p.Deadline >= Today).OrderBy(p => p.Deadline).ThenBy(p => p.Id, StringComparer.Ordinal))
                {
                    // Só as que se cumprem ainda na campanha: comparecer ao evento e defender o tema em público.
                    if (LeaderContent.Pedidos.FirstOrDefault(r => r.Id == promise.Object)?.Kind is not (LeaderRequestDefinition.Presenca or LeaderRequestDefinition.Posicao))
                    {
                        continue;
                    }

                    DateOnly due = promise.Deadline >= Campaign.ElectionDate ? Campaign.ElectionDate.AddDays(-1) : promise.Deadline;
                    if (due < Today)
                    {
                        continue;
                    }

                    string text = Texts.Fill(type.Texto, ("alvo", promise.Text), ("valor", ""), ("prazo", Texts.Date(due)));
                    return NewObjective(type, promise.Id, 1, 0, due, text, Texts.Fill(type.Origem, ("nome", NameOf(promise.ToId))), promise.ToId);
                }

                return null;
            }

            case ObjectiveType.MontarEquipe:
            {
                if (TeamSlotsUsed >= TeamSlots)
                {
                    return null;
                }

                StaffRole? role = StaffContent.Funcoes.FirstOrDefault(r => StaffIn(r.Id) is null && City.Staff.Any(s => s.Role == r.Id && !s.Left && State.Player.Resources.Money >= s.Salary));
                return role is null ? null : NewObjective(type, role.Id, 1, 0, deadline, Text(role.Nome), Texts.Fill(type.Origem, ("alvo", role.Nome)));
            }

            default:
                return null;
        }
    }
}
