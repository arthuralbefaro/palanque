using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.BalanceSim;

/// <summary>
/// O que o robô anota de um canal de verba nas semanas da campanha oficial: as duas contas da melhor primeira parcela
/// (retorno e disputa), os fatores, a verba dos rivais e o que a lei e a equipe bloquearam. São somas; quem lê divide
/// pelo número de semanas.
/// </summary>
public sealed class ChannelDiag
{
    /// <summary>Semanas da campanha oficial em que o canal foi olhado.</summary>
    public int Weeks { get; set; }

    /// <summary>Semanas em que o canal tinha algum alvo permitido.</summary>
    public int OptionWeeks { get; set; }

    public int Wins { get; set; }

    public double VotesPerReal { get; set; }

    public double Return { get; set; }

    public double Share { get; set; }

    /// <summary>Produto dos fatores do melhor alvo (equipe, rural, faixa, saturação, sem material, liderança contrária).</summary>
    public double Yield { get; set; }

    public double Saturation { get; set; }

    /// <summary>Verba dos rivais no melhor alvo do canal, em reais por semana.</summary>
    public double RivalAtTarget { get; set; }

    /// <summary>Verba de todos os rivais da disputa no canal, na cidade inteira, em reais por semana.</summary>
    public double RivalWeekly { get; set; }

    public double Targets { get; set; }

    public double TargetsWithRival { get; set; }

    /// <summary>Soma, sobre os alvos com verba de rival, da fatia que uma parcela do jogador teria ali.</summary>
    public double ShareWhereRival { get; set; }

    public int SemMaterial { get; set; }

    public int LiderancaContraria { get; set; }

    /// <summary>Alvos-semana bloqueados pelo limite legal (art. 100-A), por falta de vaga na equipe e por outro motivo.</summary>
    public int BlockedLaw { get; set; }

    public int BlockedSlot { get; set; }

    public int BlockedOther { get; set; }

    /// <summary>Em que alvo o canal venceu: a faixa de idade, ou a zona com e sem verba de rival.</summary>
    public Dictionary<string, int> WinTargets { get; } = [];

    public void Add(ChannelDiag o)
    {
        Weeks += o.Weeks;
        OptionWeeks += o.OptionWeeks;
        Wins += o.Wins;
        VotesPerReal += o.VotesPerReal;
        Return += o.Return;
        Share += o.Share;
        Yield += o.Yield;
        Saturation += o.Saturation;
        RivalAtTarget += o.RivalAtTarget;
        RivalWeekly += o.RivalWeekly;
        Targets += o.Targets;
        TargetsWithRival += o.TargetsWithRival;
        ShareWhereRival += o.ShareWhereRival;
        SemMaterial += o.SemMaterial;
        LiderancaContraria += o.LiderancaContraria;
        BlockedLaw += o.BlockedLaw;
        BlockedSlot += o.BlockedSlot;
        BlockedOther += o.BlockedOther;
        foreach (var (key, value) in o.WinTargets)
        {
            WinTargets[key] = WinTargets.GetValueOrDefault(key) + value;
        }
    }
}

/// <summary>Medidas de diagnóstico do robô: só observam (não sorteiam nem mudam o estado), então não alteram a campanha.</summary>
public sealed partial class CampaignRobot
{
    private bool _officialSeen;

    /// <summary>Semana da campanha oficial, antes de o robô montar o plano: o melhor alvo de cada canal e a verba dos rivais.</summary>
    private void DiagnoseChannels(IReadOnlyList<(ChannelDefinition Channel, string Target, double Votes)> ranked)
    {
        ChannelCatalog catalog = _engine.Context.Campaign.Channels;
        Office office = Campaign.Player.Office;
        if (!_officialSeen)
        {
            _officialSeen = true;
            Trace.CashAtOfficialStart = State.Player.Resources.Money;
            Trace.BudgetSpentPre = _engine.Budget.Spent;
            Trace.CabosLimit = _engine.CabosLimit;
            Trace.ChannelStep = _engine.ChannelStep;
        }

        List<ChannelAllocation> rivalBudgets = Campaign.Rivals.Records
            .Where(r => Campaign.Find(r.CandidateId) is { } c && c.Office == office)
            .SelectMany(r => r.Budget).ToList();
        for (int i = 0; i < catalog.Canais.Count; i++)
        {
            ChannelDefinition channel = catalog.Canais[i];
            decimal step = channel.EmGrupos ? _engine.CabosGroupCost : _engine.ChannelStep;
            if (!Trace.ChannelDiag.TryGetValue(channel.Id, out ChannelDiag? d))
            {
                Trace.ChannelDiag[channel.Id] = d = new ChannelDiag();
            }

            d.Weeks++;
            d.RivalWeekly += (double)rivalBudgets.Where(a => a.ChannelId == channel.Id).Sum(a => a.Amount);
            foreach (ChannelTarget target in _engine.ChannelTargets(channel.Id))
            {
                d.Targets++;
                decimal rival = _engine.RivalBudget(channel.Id, target.Id);
                if (rival > 0)
                {
                    d.TargetsWithRival++;
                    d.ShareWhereRival += Math.Pow((double)(step / (step + rival)), catalog.Regras.ExpoenteDisputa);
                }

                switch (_engine.CanAllocate(channel.Id, target.Id, step))
                {
                    case null or CommandFailure.SemDinheiro:
                        break;
                    case CommandFailure.LimiteLegal:
                        d.BlockedLaw++;
                        break;
                    case CommandFailure.SemVaga:
                        d.BlockedSlot++;
                        break;
                    default:
                        d.BlockedOther++;
                        break;
                }
            }

            var best = ranked.FirstOrDefault(o => o.Channel.Id == channel.Id);
            if (best.Channel is null)
            {
                continue;
            }

            CommandForecast forecast = _engine.ForecastChannel(channel.Id, best.Target, step);
            d.OptionWeeks++;
            d.VotesPerReal += best.Votes;
            d.Return += forecast.Line("return")?.Expected ?? 0;
            d.Share += forecast.Line("share")?.Expected ?? 0;
            d.Yield += forecast.Factors.Aggregate(1.0, (acc, f) => acc * f.Multiplier);
            d.Saturation += forecast.Factors.FirstOrDefault(f => f.Id == "saturacao")?.Multiplier ?? 1;
            d.SemMaterial += forecast.Factors.Any(f => f.Id == "sem_material") ? 1 : 0;
            d.LiderancaContraria += forecast.Factors.Any(f => f.Id == "lideranca_contraria") ? 1 : 0;
            decimal rivalAtBest = _engine.RivalBudget(channel.Id, best.Target);
            d.RivalAtTarget += (double)rivalAtBest;
            if (ranked[0].Channel.Id == channel.Id)
            {
                d.Wins++;
                string where = channel.Alvo == ChannelDefinition.AlvoIdade ? best.Target : rivalAtBest > 0 ? "zona com verba de rival" : "zona sem verba de rival";
                d.WinTargets[where] = d.WinTargets.GetValueOrDefault(where) + 1;
            }
        }
    }

    /// <summary>O par (tipo, lugar) mais repetido da campanha, turno a turno, e de que lugar se trata.</summary>
    private void DiagnosePair()
    {
        Trace.BudgetSpentTotal = _engine.Budget.Spent;
        if (Trace.BestPairByTurn.Count == 0)
        {
            return;
        }

        var top = Trace.BestPairByTurn.GroupBy(x => x).OrderByDescending(g => g.Count()).First();
        string[] parts = top.Key.Split('|');
        string type = parts[0];
        string place = parts[1];
        TerritoryState t = _engine.Territory;
        string kind;
        if (_engine.City.Outlet(place) is { } outlet)
        {
            double Weight(Planalto.Core.Media.Outlet o) => Enumerable.Range(0, t.Zones.Count).Sum(z => o.Reach[z] * t.Population[z]);
            bool widest = _engine.City.Outlets.All(o => Weight(o) <= Weight(outlet));
            kind = $"veículo ({_engine.Context.Campaign.Media.Outlets.Type(outlet.Type).Nome}{(widest ? ", o de maior alcance" : "")}; {_engine.City.Outlets.Count} na cidade)";
        }
        else if (t.IndexOf(place) is var z and >= 0)
        {
            kind = _engine.Context.Zonas.Zonas[z].IsRural ? "zona rural" : z == t.HomeZone ? "zona natal" : "zona urbana";
            kind += $" ({t.Zones.Count} zonas na cidade)";
        }
        else
        {
            kind = "sem lugar";
        }

        Trace.TopPair = $"{_engine.Context.Campaign.Commitments.Get(type).Nome}: {kind}";
        Trace.TopPairTurns = top.Count();
    }

    /// <summary>Campanha parada: o robô não marca compromisso, não gasta e não aceita nada; só responde ao que pausa o jogo.</summary>
    public CampaignTrace PlayIdle()
    {
        while (Campaign.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha)
        {
            ResolvePending();
            World?.AdvanceTo(State.Calendar.WeekStart);
            _engine.AdvanceDay();
        }

        ResolvePending();
        ElectionOutcome outcome = _engine.ResolveElection();
        Trace.Elected = outcome.PlayerElected;
        Trace.Position = 1 + outcome.Vereador.Votes.Where(v => !v.Key.StartsWith("demais_", StringComparison.Ordinal)).Count(v => v.Value > outcome.Vereador.Votes[RaceSetup.PlayerId]);
        Trace.FinalRecognition = Campaign.Player.Recognition;
        DiagnoseOutcome(outcome);
        return Trace;
    }

    /// <summary>Dificuldade: quão conhecidos ficaram os rivais, os votos do jogador sobre o quociente e a chapa dele.</summary>
    private void DiagnoseOutcome(ElectionOutcome outcome)
    {
        Candidate player = Campaign.Player;
        List<Candidate> rivals = Campaign.ForOffice(player.Office).Where(c => !c.IsPlayer && !c.IsBulk).ToList();
        Trace.RivalRecognition = rivals.Count == 0 ? 0 : rivals.Average(c => c.Recognition);
        Trace.RivalRecognitionMax = rivals.Count == 0 ? 0 : rivals.Max(c => c.Recognition);
        long own = outcome.Vereador.Votes.GetValueOrDefault(RaceSetup.PlayerId);
        Trace.VotesOverQuotient = outcome.Vereador.ElectoralQuotient <= 0 ? 0 : own / (double)outcome.Vereador.ElectoralQuotient;
        Trace.PartySeats = outcome.Vereador.SeatsByParty.GetValueOrDefault(player.PartyId);
        Trace.RankInParty = 1 + rivals.Count(c => c.PartyId == player.PartyId && outcome.Vereador.Votes.GetValueOrDefault(c.Id) > own);
    }
}
