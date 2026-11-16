using Planalto.Core.Campaign;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.BalanceSim;

/// <summary>
/// O que o robô faz com os sistemas da R2b: contrata, reparte a verba pelos canais, assume posições, faz promessas,
/// delega e responde aos eventos da equipe. Como na agenda, decide pelo ganho previsto de votos (a previsão do jogo
/// vezes a sensibilidade dos votos) e não enxerga nada que o jogador não veja.
/// </summary>
public sealed partial class CampaignRobot
{
    /// <summary>Ordem em que o robô preenche as funções.</summary>
    private static readonly string[] HireOrder = [StaffRole.Coordenador, StaffRole.Marqueteiro, StaffRole.SocialMedia, StaffRole.Tesoureiro, StaffRole.Advogado];

    /// <summary>Semanas de folha que o robô considera ao responder aos eventos da equipe.</summary>
    private const int CashWeeks = 12;

    /// <summary>Fração do caixa que o robô põe na verba a cada semana, e o máximo de parcelas.</summary>
    private const decimal BudgetShare = 0.06m;

    private const int MaxParcels = 6;

    /// <summary>Caminhadas que o caixa precisa pagar para o robô delegar ou contratar sem arriscar a própria agenda.</summary>
    private const int AgendaCushion = 20;

    private int _weeksPlayed;

    private void Decision(string kind)
    {
        Trace.TradeDecisions[kind] = Trace.TradeDecisions.GetValueOrDefault(kind) + 1;
    }

    /// <summary>Folha mais plano de verba: o que sai do caixa toda semana.</summary>
    private decimal WeeklyOutflow => _engine.Payroll().Weekly + _engine.Budget.Plan.Sum(a => a.Amount);

    /// <summary>Eventos da equipe: uma regra simples por evento, olhando o caixa e a qualidade de quem está envolvido.</summary>
    private void ResolveStaffDecisions()
    {
        while (_engine.Team.Pending.Count > 0)
        {
            StaffDecision decision = _engine.Team.Pending[0];
            StaffMember member = _engine.Staff(decision.PersonId)!;
            bool rich = State.Player.Resources.Money > WeeklyOutflow * CashWeeks;
            string option = decision.EventId switch
            {
                StaffEventDefinition.Aumento => rich && member.QualityShownMax >= 3 ? "conceder" : member.QualityShownMax >= 3 ? "prometer" : "negar",
                StaffEventDefinition.PropostaRival => rich && member.QualityShownMax >= 4 ? "cobrir" : member.Loyalty >= 50 ? "apelar" : "liberar",
                StaffEventDefinition.Briga => member.QualityShownMax >= (_engine.Staff(decision.OtherPersonId ?? "")?.QualityShownMax ?? 0) ? "lado_primeiro" : "lado_segundo",
                _ => "aceitar",
            };
            _engine.AnswerStaffDecision(decision.Id, option);
            Trace.StaffEvents++;
            Decision("evento da equipe");
        }
    }

    /// <summary>Começo da semana: equipe, plataforma e promessas.</summary>
    private void ManageWeek()
    {
        _weeksPlayed++;
        if (_engine.Team.Advice is not null)
        {
            // Seguir o conselho ou a própria conta: o robô segue a conta, e a semana diz se coincidiu.
            Decision("conselho da semana");
        }

        Hire();
        if (_weeksPlayed % 4 == 1)
        {
            Position();
        }

        if (_weeksPlayed % 2 == 0)
        {
            Promise();
        }

        foreach (Objective o in _engine.Objectives.Active.Where(o => !o.Pinned))
        {
            _engine.PinObjective(o.Id);
        }
    }

    /// <summary>Contrata, uma função por semana, o candidato de melhor qualidade esperada que o caixa sustenta.</summary>
    private void Hire()
    {
        decimal money = State.Player.Resources.Money;

        // Na campanha oficial o robô guarda uma vaga para a rede de cabos, se a lei deixa contratar algum grupo.
        bool keepSlot = !_engine.CabosNetworkActive && _engine.CabosLimit >= _engine.Context.Campaign.Channels.Regras.CabosPorGrupo;
        if (_engine.TeamSlotsUsed + (keepSlot ? 1 : 0) >= _engine.TeamSlots)
        {
            return;
        }

        foreach (string role in HireOrder)
        {
            if (_engine.StaffIn(role) is not null)
            {
                continue;
            }

            // Só contrata quem o caixa de hoje paga por boa parte do que falta de campanha, sem tirar da agenda.
            decimal walks = _engine.CostOf(_engine.Context.Campaign.Commitments.Get("caminhada")) * AgendaCushion;
            decimal horizon = Math.Max(4, _engine.WeeksToElection * 0.6m);
            StaffView? best = _engine.StaffCandidates(role)
                .Where(v => v.HireBlocked is null && money >= ((_engine.Payroll().Weekly + v.Salary) * horizon) + walks)
                .OrderByDescending(v => (v.QualityMin + v.QualityMax) / 2.0)
                .ThenBy(v => v.Salary)
                .FirstOrDefault();
            if (best is not null && best.QualityMax >= 3 && _engine.Hire(best.Member.PersonId).Failure is null)
            {
                Trace.Hires++;
                Decision("contratação");
                return;
            }
        }
    }

    /// <summary>
    /// Plataforma: as bandeiras são os três temas que mais pesam para a cidade; em cada uma, o robô assume a posição
    /// de maior ganho previsto de votos (a previsão já desconta a incoerência).
    /// </summary>
    private void Position()
    {
        IReadOnlyList<PlatformRow> matrix = _engine.PlatformMatrix();
        if (_engine.Platform.Flags.Count == 0)
        {
            var flags = matrix
                .OrderByDescending(r => r.Cells.Where(c => c.TargetKind == Planalto.Core.People.Promise.ParaZona).Sum(c => c.Priority))
                .Take(3).Select(r => r.Theme.Id).ToList();
            _engine.SetFlags(flags);
        }

        foreach (string theme in _engine.Platform.Flags.ToList())
        {
            int current = _engine.PositionOf(theme);
            (int Value, double Gain) best = (current, 0);
            for (int value = -2; value <= 2; value++)
            {
                if (value == current)
                {
                    continue;
                }

                CommandForecast forecast = _engine.ForecastPosition(theme, value);
                double gain = forecast.Blocked is null ? forecast.Line("votes_pct")!.Expected : 0;
                if (gain > best.Gain)
                {
                    best = (value, gain);
                }
            }

            // Só muda por ganho que valha a pena: meio por cento dos próprios votos.
            if (best.Gain > 0.5 && _engine.SetPosition(theme, best.Value).Failure is null)
            {
                Trace.PositionChanges++;
                Decision("mudança de posição");
            }
        }
    }

    /// <summary>Promessa local: enquanto a credibilidade está inteira, a de maior ganho previsto de votos.</summary>
    private void Promise()
    {
        if (_engine.Credibility < 1 || _sens is null)
        {
            return;
        }

        int open = State.Ledger.Promises.Count(p => p.State == PromiseState.Aberta);
        if (open >= _engine.Context.Campaign.Promises.Regras.PromessasSemCusto)
        {
            return;
        }

        TerritoryState t = _engine.Territory;
        (PromiseOption? Option, double Votes) best = (null, 0);
        foreach (string zone in t.Zones)
        {
            int z = t.IndexOf(zone);
            foreach (PromiseOption option in _engine.PromiseOptions(Planalto.Core.People.Promise.ParaZona, zone).Where(o => o.Blocked is null))
            {
                double votes = _engine.ForecastPromise(option.Definition.Id, option.ToKind, option.ToId).Lines
                    .Where(l => l.Kind == "support")
                    .Sum(l => l.Expected * _sens.PerSupportUnit[z].GetValueOrDefault(l.Target!));
                if (votes > best.Votes)
                {
                    best = (option, votes);
                }
            }
        }

        if (best.Option is { } chosen && _engine.MakePromise(chosen.Definition.Id, chosen.ToKind, chosen.ToId).Failure is null)
        {
            Trace.PromisesMade++;
            Decision("promessa local");
        }
    }

    /// <summary>Ganho previsto de votos de uma verba semanal num canal e alvo: presença e apoio da previsão vezes a sensibilidade.</summary>
    /// <param name="ignoreCash">Mede o destino mesmo sem caixa para pagá-lo (a estatística do melhor destino não depende do caixa do robô).</param>
    private double ChannelVotes(ChannelDefinition channel, string target, decimal amount, bool ignoreCash = false)
    {
        CommandFailure? blocked = _engine.CanAllocate(channel.Id, target, amount);
        if (_sens is null || (blocked is not null && !(ignoreCash && blocked == CommandFailure.SemDinheiro)))
        {
            return double.NegativeInfinity;
        }

        CommandForecast forecast = _engine.ForecastChannel(channel.Id, target, amount);

        TerritoryState t = _engine.Territory;
        int zone = channel.Alvo == ChannelDefinition.AlvoZona ? t.IndexOf(target) : -1;
        double votes = 0;
        foreach (ForecastLine line in forecast.Lines)
        {
            if (line.Kind == "presence" && zone >= 0)
            {
                votes += line.Expected * _sens.PerPresencePoint[zone];
            }
            else if (line.Kind == "presence")
            {
                votes += line.Expected * _sens.PerPresencePoint.Sum();
            }
            else if (line.Kind == "support" && zone >= 0)
            {
                votes += line.Expected * _sens.PerSupportUnit[zone].GetValueOrDefault(line.Target!);
            }
            else if (line.Kind == "support")
            {
                votes += line.Expected * _sens.PerSupportUnit.Sum(z => z.GetValueOrDefault(line.Target!));
            }
        }

        return votes;
    }

    /// <summary>
    /// Domingo: o plano de verba da semana que entra. O robô gasta o que sobra do caixa depois da reserva, em
    /// parcelas, sempre no destino (canal, alvo) em que a próxima parcela rende mais votos; como o retorno é
    /// decrescente, a verba se espalha. O melhor destino da semana é o da primeira parcela.
    /// </summary>
    private void PlanBudget()
    {
        if (_sens is null || Campaign.Phase is not (CampaignPhase.PreCampanha or CampaignPhase.Campanha))
        {
            return;
        }

        ChannelCatalog catalog = _engine.Context.Campaign.Channels;
        var plan = new Dictionary<(string Channel, string Target), decimal>();
        decimal Current(string channel, string target) => plan.GetValueOrDefault((channel, target));
        decimal Step(ChannelDefinition c) => c.EmGrupos ? _engine.CabosGroupCost : _engine.ChannelStep;

        // Zera o plano antigo (a redução só vale na segunda) e monta o novo a partir do nada.
        foreach (ChannelAllocation a in _engine.Budget.Plan.ToList())
        {
            _engine.SetAllocation(a.ChannelId, a.TargetId, 0);
        }

        decimal money = State.Player.Resources.Money;
        decimal payroll = _engine.Payroll().Weekly;
        int weeksLeft = Math.Max(1, _engine.WeeksToElection);
        // Uma fração fixa do caixa vai para a verba toda semana; com pouco caixa, ainda assim uma parcela.
        decimal free = Math.Max(0, money - payroll);
        decimal budget = Math.Max(free * BudgetShare, free >= _engine.ChannelStep ? _engine.ChannelStep : 0);

        // O plano é feito no domingo, antes de a semana fechar: o que tem verba nesta semana vai estar mais saturado
        // na segunda-feira, e o robô já conta com isso (o jogador vê o mesmo fator na previsão de segunda).
        ChannelRules rules = catalog.Regras;
        double Saturating(string channel, string target)
        {
            if (_engine.Budget.ActiveOf(channel, target) <= 0)
            {
                return 1;
            }

            double now = _engine.ChannelSaturationYield(channel, target);
            double level = ((1 / now) - 1) / Math.Max(1e-9, rules.SaturacaoFator);
            double next = Math.Max(0, level - rules.SaturacaoDecaimentoSemanal) + 1;
            return (1 / (1 + (rules.SaturacaoFator * next))) / now;
        }
        bool open = !_engine.BeforeOfficialCampaign;
        var options = catalog.Canais.SelectMany(c => _engine.ChannelTargets(c.Id).Select(t => (Channel: c, Target: t.Id))).ToList();

        // O melhor destino da semana: a primeira parcela, com o plano vazio, entre os canais que a fase permite.
        var first = options
            .Select(o => (o.Channel, o.Target, Votes: ChannelVotes(o.Channel, o.Target, Step(o.Channel), ignoreCash: true) * Saturating(o.Channel.Id, o.Target) / (double)Step(o.Channel)))
            .Where(o => !double.IsNegativeInfinity(o.Votes))
            .OrderByDescending(o => o.Votes)
            .ToList();
        if (first.Count > 0)
        {
            (open ? Trace.BestChannelByWeek : Trace.BestChannelByWeekPre).Add(first[0].Channel.Id);
            foreach (var group in first.GroupBy(o => o.Channel.Id))
            {
                Dictionary<string, double> sums = open ? Trace.ChannelValue : Trace.ChannelValuePre;
                sums[group.Key] = sums.GetValueOrDefault(group.Key) + group.Max(o => o.Votes);
            }

            if (open)
            {
                Trace.ChannelWeeks++;
            }
            else
            {
                Trace.ChannelWeeksPre++;
            }
        }

        // Comício: com caixa para duas semanas de estrutura, o robô garante a verba de Rua na zona em que é mais conhecido.
        if (open && free >= _engine.ComicioRuaMinimum * 2)
        {
            string home = _engine.Territory.Zones.MaxBy(z => _engine.Territory.PresenceOf(RaceSetup.PlayerId, _engine.Territory.IndexOf(z)))!;
            if (_engine.SetAllocation(ChannelDefinition.Rua, home, _engine.ComicioRuaMinimum) is null)
            {
                plan[(ChannelDefinition.Rua, home)] = _engine.ComicioRuaMinimum;
                budget = Math.Max(0, budget - _engine.ComicioRuaMinimum);
                Trace.Spent[ChannelDefinition.Rua] = Trace.Spent.GetValueOrDefault(ChannelDefinition.Rua) + _engine.ComicioRuaMinimum;
            }
        }

        // Votos da verba já posta em cada destino: só muda no destino escolhido a cada parcela.
        var votesNow = new Dictionary<(string Channel, string Target), double>();
        for (int parcel = 0; parcel < MaxParcels && budget > 0; parcel++)
        {
            (ChannelDefinition? Channel, string Target, double Votes, decimal Amount) best = (null, "", 0, 0);
            foreach (var (channel, target) in options)
            {
                decimal step = Step(channel);
                decimal now = Current(channel.Id, target);
                if (step > budget)
                {
                    continue;
                }

                if (!votesNow.TryGetValue((channel.Id, target), out double before))
                {
                    votesNow[(channel.Id, target)] = before = now > 0 ? ChannelVotes(channel, target, now) : 0;
                }

                double after = ChannelVotes(channel, target, now + step);
                double perReal = double.IsNegativeInfinity(after) || double.IsNegativeInfinity(before) ? double.NegativeInfinity : (after - before) * Saturating(channel.Id, target) / (double)step;
                if (perReal > best.Votes)
                {
                    best = (channel, target, perReal, now + step);
                }
            }

            if (best.Channel is null || _engine.SetAllocation(best.Channel.Id, best.Target, best.Amount) is not null)
            {
                break;
            }

            decimal spent = best.Amount - Current(best.Channel.Id, best.Target);
            plan[(best.Channel.Id, best.Target)] = best.Amount;
            votesNow.Remove((best.Channel.Id, best.Target));
            budget -= spent;
            Trace.Spent[best.Channel.Id] = Trace.Spent.GetValueOrDefault(best.Channel.Id) + spent;
        }

        if (plan.Count > 0)
        {
            Trace.BudgetWeeks++;
        }
    }

    /// <summary>Delegações da semana: o coordenador manda representante aos compromissos de rua e de conversa de maior ganho previsto.</summary>
    private void Delegate(DateOnly today, List<RobotOption> options)
    {
        CommitmentCatalog catalog = _engine.Context.Campaign.Commitments;
        if (State.Player.Resources.Money < _engine.CostOf(catalog.Get("caminhada")) * AgendaCushion)
        {
            return;
        }

        int left = _engine.DelegationsPerWeek - _engine.DelegationsIn(today);
        foreach (RobotOption option in options
            .Where(o => catalog.Get(o.Request.Type) is { Delegavel: true, Visita: false } && o.VotesPerTurn > 0 && o.VotesPerTurn < Forced)
            .OrderByDescending(o => o.VotesPerTurn).Take(Math.Max(0, Math.Min(1, left))))
        {
            if (_engine.Schedule(option.Request with { Delegado = true, PersonId = null }).Failure is null)
            {
                Trace.Delegations++;
            }
        }
    }

    /// <summary>Tema de que o candidato trata numa fala: a bandeira cuja posição é menos conhecida.</summary>
    private string? TalkTheme(string typeId) =>
        _engine.SpeaksOfThemes(typeId) && _engine.Platform.Flags.Count > 0 ? _engine.Platform.Flags.MinBy(_engine.KnownOf) : null;
}
