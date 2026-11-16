using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Campaign;

/// <summary>
/// Verba por canal (GDD 17.8): o jogador reparte a verba semanal em quatro canais pagos, cada um com alvo. O efeito é
/// √(verba ÷ escala do porte) × (verba própria ÷ verba total do canal no alvo)^expoente: dobrar rende cerca de 41% a
/// mais, e a verba de rival no mesmo canal e alvo reduz o efeito. A previsão mostra as duas contas.
/// </summary>
public sealed partial class CampaignEngine
{
    private ChannelCatalog Channels => Context.Campaign.Channels;

    private ChannelRules ChannelRules => Channels.Regras;

    private ChannelLawConfig Law => Context.ChannelLaw;

    public BudgetState Budget => Campaign.Budget;

    /// <summary>Verba semanal em que o retorno vale 1, no porte da cidade.</summary>
    public decimal ChannelScale => ChannelRules.Escala * (decimal)CostScale;

    private decimal ScaledTens(decimal amount) => Math.Max(10, Math.Round(amount * (decimal)CostScale / 10) * 10);

    /// <summary>Múltiplo de verba que a tela oferece, no porte da cidade.</summary>
    public decimal ChannelStep => ScaledTens(ChannelRules.Passo);

    /// <summary>Custo semanal de um grupo de cabos, no porte da cidade.</summary>
    public decimal CabosGroupCost => ScaledTens(ChannelRules.CabosCustoGrupo);

    /// <summary>Verba de Rua na zona que o comício exige, no porte da cidade.</summary>
    public decimal ComicioRuaMinimum => ScaledTens(ChannelRules.ComicioVerbaRua);

    /// <summary>A rede de cabos está montada (ocupa uma vaga da equipe).</summary>
    public bool CabosNetworkActive => Budget.Plan.Concat(Budget.Active).Any(a => a.ChannelId == ChannelDefinition.Cabos && a.Amount > 0);

    /// <summary>Limite legal de pessoas contratadas para rua pela candidatura do jogador (art. 100-A).</summary>
    public int CabosLimit => Law.LimiteCabos(Campaign.Player.Office, Context.Municipio.Eleitorado, Context.MaiorEleitoradoUf);

    private int CabosPeople(decimal amount) => (int)Math.Floor(amount / CabosGroupCost) * ChannelRules.CabosPorGrupo;

    /// <summary>Pessoas contratadas como cabos no plano semanal.</summary>
    public int CabosPlanned => Budget.Plan.Where(a => a.ChannelId == ChannelDefinition.Cabos).Sum(a => CabosPeople(a.Amount));

    /// <summary>Alvos que o canal aceita: as zonas da cidade ou as faixas de idade.</summary>
    public IReadOnlyList<ChannelTarget> ChannelTargets(string channelId) =>
        Channels.Find(channelId) is not { } c ? []
        : c.Alvo == ChannelDefinition.AlvoZona ? Territory.Zones.Select(z => new ChannelTarget(z, ZoneName(z))).ToList()
        : Channels.FaixasIdade.Select(b => new ChannelTarget(b.Id, b.Nome)).ToList();

    private string TargetName(ChannelDefinition c, string targetId) =>
        c.Alvo == ChannelDefinition.AlvoZona ? ZoneName(targetId) : Channels.Band(targetId)?.Nome ?? targetId;

    private bool ValidTarget(ChannelDefinition c, string targetId) =>
        c.Alvo == ChannelDefinition.AlvoZona ? Territory.IndexOf(targetId) >= 0 : Channels.Band(targetId) is not null;

    /// <summary>Antes de 16 de agosto (início oficial da propaganda): valem as regras da pré-campanha.</summary>
    public bool BeforeOfficialCampaign => Today < Campaign.CampaignStart;

    /// <summary>Verba de Rua que vale para a data: a paga, nesta semana; a do plano, nas seguintes.</summary>
    private decimal RuaBudgetOn(DateOnly date, string zoneId) =>
        date <= State.Calendar.WeekEnd ? Budget.ActiveOf(ChannelDefinition.Rua, zoneId) : Budget.PlanOf(ChannelDefinition.Rua, zoneId);

    /// <summary>
    /// Verba semanal dos rivais no canal e alvo (estimativa que o jogador vê): cada rival nomeado da mesma disputa
    /// reparte a verba pelos canais e a concentra nos seus redutos; nas redes, pelas faixas de idade. Antes da
    /// campanha oficial só gastam, e pouco, nos canais que a pré-campanha permite.
    /// </summary>
    public decimal RivalBudget(string channelId, string targetId)
    {
        if (Channels.Find(channelId) is not { } c)
        {
            return 0;
        }

        double phase = !BeforeOfficialCampaign ? 1 : c.PreCampanha is null ? 0 : ChannelRules.RivalPreCampanha;
        double total = Budget.RivalExtra.GetValueOrDefault(BudgetState.Key(channelId, targetId));
        if (phase > 0)
        {
            int zone = c.Alvo == ChannelDefinition.AlvoZona ? Territory.IndexOf(targetId) : -1;
            double bandShare = Channels.Band(targetId) is { } band ? band.Peso / Channels.FaixasIdade.Sum(b => b.Peso) : 0;
            foreach (Candidate rival in Campaign.ForOffice(Campaign.Player.Office).Where(x => !x.IsPlayer && !x.IsBulk))
            {
                double budget = (double)ChannelRules.RivalVerba * CostScale * rival.Drive * ChannelRules.RivalReparte.GetValueOrDefault(c.Id) * phase;
                if (c.Alvo == ChannelDefinition.AlvoIdade)
                {
                    total += budget * bandShare;
                }
                else if (!Territory.Redutos.TryGetValue(rival.Id, out List<int>? redutos))
                {
                    total += budget / Territory.Zones.Count;
                }
                else if (redutos.Contains(zone))
                {
                    total += budget / redutos.Count;
                }
            }
        }

        return Math.Round((decimal)total);
    }

    /// <summary>A verba como o canal a aceita: cabos são contratados em grupos inteiros.</summary>
    public decimal NormalizeAllocation(string channelId, decimal amount) =>
        Channels.Find(channelId) is { EmGrupos: true } ? Math.Floor(Math.Max(0, amount) / CabosGroupCost) * CabosGroupCost : Math.Max(0, Math.Round(amount));

    public CommandFailure? CanAllocate(string channelId, string targetId, decimal amount)
    {
        if (!Law.GastoPermitido(channelId))
        {
            return CommandFailure.GastoVedado;
        }

        if (Channels.Find(channelId) is not { } c)
        {
            return CommandFailure.NaoEncontrado;
        }

        if (!CampaignRunning)
        {
            return CommandFailure.ForaDaCampanha;
        }

        if (!ValidTarget(c, targetId))
        {
            return CommandFailure.AlvoInvalido;
        }

        if (amount < 0)
        {
            return CommandFailure.ValorInvalido;
        }

        amount = NormalizeAllocation(channelId, amount);
        if (amount > 0 && PhaseBlocks(c, amount) is { } phase)
        {
            return phase;
        }

        if (c.EmGrupos && amount > 0)
        {
            int others = Budget.Plan.Where(a => a.ChannelId == c.Id && a.TargetId != targetId).Sum(a => CabosPeople(a.Amount));
            if (others + CabosPeople(amount) > CabosLimit)
            {
                return CommandFailure.LimiteLegal;
            }

            if (!CabosNetworkActive && TeamSlotsUsed >= TeamSlots)
            {
                return CommandFailure.SemVaga;
            }
        }

        decimal increase = amount - Budget.ActiveOf(channelId, targetId);
        return increase > 0 && State.Player.Resources.Money < increase ? CommandFailure.SemDinheiro : null;
    }

    /// <summary>
    /// Regras de fase: na pré-campanha só os canais que a permitem, até o teto (sem impulsionamento, material
    /// institucional). Acima do teto, o canal de impulsionamento só se a regra de lei em vigor deixar.
    /// </summary>
    private CommandFailure? PhaseBlocks(ChannelDefinition c, decimal amount)
    {
        if (!BeforeOfficialCampaign)
        {
            return null;
        }

        if (c.PreCampanha is not { } pre)
        {
            return CommandFailure.CanalFechado;
        }

        if (amount <= ScaledTens(pre.Teto))
        {
            return null;
        }

        return !c.Impulsionamento ? CommandFailure.CanalFechado : Law.ImpulsionamentoSoNaCampanha ? CommandFailure.LimiteLegal : null;
    }

    /// <summary>Teto de verba do canal na pré-campanha, no porte da cidade; null se o canal não abre antes da campanha.</summary>
    public decimal? PreCampaignCap(string channelId) => Channels.Find(channelId)?.PreCampanha is { } pre ? ScaledTens(pre.Teto) : null;

    /// <summary>
    /// Define a verba semanal do canal no alvo. O aumento sobre o que já está pago nesta semana sai do caixa agora;
    /// a redução só vale a partir de segunda-feira. Toda segunda o plano inteiro é cobrado de novo.
    /// </summary>
    public CommandFailure? SetAllocation(string channelId, string targetId, decimal amount)
    {
        if (CanAllocate(channelId, targetId, amount) is { } failure)
        {
            return failure;
        }

        amount = NormalizeAllocation(channelId, amount);
        Budget.Plan.RemoveAll(a => a.ChannelId == channelId && a.TargetId == targetId);
        if (amount > 0)
        {
            Budget.Plan.Add(new ChannelAllocation { ChannelId = channelId, TargetId = targetId, Amount = amount });
        }

        decimal active = Budget.ActiveOf(channelId, targetId);
        if (amount > active)
        {
            PayChannels(amount - active);
            Budget.Active.RemoveAll(a => a.ChannelId == channelId && a.TargetId == targetId);
            Budget.Active.Add(new ChannelAllocation { ChannelId = channelId, TargetId = targetId, Amount = amount });
        }

        UpdatePayrollAlert();
        return null;
    }

    private void PayChannels(decimal amount)
    {
        State.Player.Resources.Money -= amount;
        Campaign.Player.Money = State.Player.Resources.Money;
        Budget.Spent += amount;
        Team.RegisteredSpending += amount;
    }

    /// <summary>As duas contas do efeito e os fatores do canal no alvo, para uma verba semanal.</summary>
    private (double Return, double Share, decimal Rival, List<YieldFactor> Factors) PlanChannel(ChannelDefinition c, string targetId, decimal amount)
    {
        decimal rival = RivalBudget(c.Id, targetId);
        double ret = amount <= 0 ? 0 : Math.Sqrt((double)(amount / ChannelScale));
        double share = amount <= 0 ? 0 : Math.Pow((double)(amount / (amount + rival)), ChannelRules.ExpoenteDisputa);
        var factors = new List<YieldFactor>();
        if (c.EquipeFuncao is { } roleId && StaffIn(roleId) is { } staff)
        {
            factors.Add(Factor("equipe", $"{NameOf(staff.PersonId)} ({StaffContent.Role(roleId).Papel})", StaffEffect(roleId, c.EquipeEfeito!)));
        }

        int zone = c.Alvo == ChannelDefinition.AlvoZona ? Territory.IndexOf(targetId) : -1;
        if (c.Id == ChannelDefinition.Rua && zone >= 0 && Context.Zonas.Zonas[zone].IsRural)
        {
            factors.Add(Factor("rural", Channels.Texto("fator_rural"), ChannelRules.RuaFatorRural));
        }

        if (Channels.Band(targetId) is { } band && c.Alvo == ChannelDefinition.AlvoIdade)
        {
            factors.Add(Factor("faixa", Channels.Texto("fator_faixa"), band.Peso));
        }

        if (BeforeOfficialCampaign && c.PreCampanha is { } pre)
        {
            factors.Add(Factor("pre_campanha", Channels.Texto("fator_pre_campanha"), pre.Fator));
        }

        if (c.Id == ChannelDefinition.Cabos && zone >= 0)
        {
            if (Budget.ActiveOf(ChannelDefinition.Material, targetId) <= 0)
            {
                factors.Add(Factor("sem_material", Channels.Texto("fator_sem_material"), ChannelRules.CabosSemMaterial));
            }

            if (City.Leaders.Where(l => l.AlignedWith is not null and not RaceSetup.PlayerId && l.Influence >= ChannelRules.LiderancaContrariaInfluencia && l.Zones.Contains(targetId))
                    .OrderByDescending(l => l.Influence).FirstOrDefault() is { } against)
            {
                string text = Texts.Fill(Channels.Texto("fator_lideranca_contraria"), ("nome", NameOf(against.PersonId)), ("papel", LeaderContent.Type(against.Type).Papel));
                factors.Add(Factor("lideranca_contraria", text, ChannelRules.LiderancaContrariaFator));
            }
        }

        return (ret, share, rival, factors);
    }

    private double ChannelHeadroom(double current) => Math.Clamp(1 - (current / ChannelRules.ApoioTeto), 0, 1);

    /// <summary>
    /// O que o canal rende com o efeito e a sorte dados, a partir do estado de agora: presença por zona e apoio por
    /// grupo. Só calcula; a virada da semana grava (<see cref="ChannelsWeekEnd"/>).
    /// </summary>
    private (Dictionary<string, double> Presence, Dictionary<string, double> Support) ChannelGains(ChannelDefinition c, string targetId, double effect, double luck)
    {
        var presence = new Dictionary<string, double>(StringComparer.Ordinal);
        var support = new Dictionary<string, double>(StringComparer.Ordinal);
        TerritoryState t = Territory;
        double[] own = t.Presence[RaceSetup.PlayerId];
        int zone = c.Alvo == ChannelDefinition.AlvoZona ? t.IndexOf(targetId) : -1;
        AgeBand? band = c.Alvo == ChannelDefinition.AlvoIdade ? Channels.Band(targetId) : null;

        if (c.KPresenca > 0 && zone >= 0)
        {
            double amount = c.KPresenca * effect * luck * RecognitionScale * Reach(zone);
            presence[targetId] = EffectApplier.Gain(own[zone], amount) - own[zone];
        }
        else if (c.KPresenca > 0 && band is not null)
        {
            // Nas redes a presença vale para a cidade inteira, na proporção dos eleitores da faixa.
            double voters = Context.Electorate.Segments.Sum(s => s.Voters);
            double inBand = Context.Electorate.Segments.Where(s => band.Grupos.Any(s.Has)).Sum(s => s.Voters);
            double amount = c.KPresenca * effect * luck * RecognitionScale * (voters <= 0 ? 0 : inBand / voters);
            for (int z = 0; z < own.Length; z++)
            {
                presence[t.Zones[z]] = EffectApplier.Gain(own[z], amount) - own[z];
            }
        }

        if (c.KApoio > 0 && zone >= 0)
        {
            // Apoio de todos os eleitores da zona: um grupo de idade por eleitor, para ninguém contar duas vezes.
            Dictionary<string, double> meeting = t.MeetingSupport[zone];
            foreach (string group in Channels.FaixasIdade.SelectMany(b => b.Grupos).Where(g => Context.ZoneElectorates[zone].VotersIn(g) > 0))
            {
                support[group] = c.KApoio * effect * luck * ChannelHeadroom(meeting.GetValueOrDefault(group));
            }
        }
        else if (c.KApoio > 0 && band is not null)
        {
            Dictionary<string, double> city = Campaign.Player.Support;
            foreach (string group in band.Grupos)
            {
                support[group] = c.KApoio * effect * luck * ChannelHeadroom(city.GetValueOrDefault(group));
            }
        }

        return (presence, support);
    }

    private double CityAverage(Dictionary<string, double> byZone)
    {
        double pop = Territory.Population.Sum(x => (double)x);
        return pop <= 0 || byZone.Count == 0 ? 0 : byZone.Sum(p => p.Value * Territory.Population[Territory.IndexOf(p.Key)]) / pop;
    }

    /// <summary>
    /// Previsão da verba semanal de um canal num alvo: as duas contas (retorno e disputa), o efeito, a presença e o
    /// apoio em faixa (pela sorte da semana), os fatores e a explicação.
    /// </summary>
    public CommandForecast ForecastChannel(string channelId, string targetId, decimal amount)
    {
        if (Channels.Find(channelId) is not { } c || !ValidTarget(c, targetId))
        {
            CommandFailure failure = !Law.GastoPermitido(channelId) ? CommandFailure.GastoVedado : Channels.Find(channelId) is null ? CommandFailure.NaoEncontrado : CommandFailure.AlvoInvalido;
            string why = Law.GastosVedados.TryGetValue(channelId, out string? article) ? article : channelId;
            return new CommandForecast(failure, [], [], [why]);
        }

        amount = NormalizeAllocation(channelId, amount);
        var (ret, share, rival, factors) = PlanChannel(c, targetId, amount);
        double yield = factors.Aggregate(1.0, (acc, f) => acc * f.Multiplier);
        double effect = ret * share;
        var luck = LuckRange(ChannelRules.Sorte);
        var low = ChannelGains(c, targetId, effect * yield, luck.Min);
        var high = ChannelGains(c, targetId, effect * yield, luck.Max);
        var mid = ChannelGains(c, targetId, effect * yield, 1);
        string target = TargetName(c, targetId);

        var lines = new List<ForecastLine>
        {
            new("return", null, ret, ret, ret),
            new("share", null, share, share, share),
            new("effect", null, effect, effect, effect),
            new("money", null, -(double)amount, -(double)amount, -(double)amount),
        };
        var explanation = new List<string>
        {
            Texts.Fill(Channels.Texto("conta_retorno"), ("verba", Texts.Money(amount)), ("escala", Texts.Money(ChannelScale)), ("retorno", Texts.Number(ret, "0.00"))),
            Texts.Fill(Channels.Texto("conta_disputa"), ("verba", Texts.Money(amount)), ("rival", Texts.Money(rival)), ("disputa", Texts.Percent(share))),
            Texts.Fill(Channels.Texto("efeito"), ("canal", c.Nome), ("alvo", target), ("efeito", Texts.Number(effect, "0.00"))),
        };
        explanation.AddRange(factors.Select(f => $"× {Texts.Number(f.Multiplier, "0.00")}: {f.Text}"));

        if (c.Alvo == ChannelDefinition.AlvoZona && low.Presence.Count > 0)
        {
            lines.Add(new ForecastLine("presence", targetId, low.Presence[targetId], high.Presence[targetId], mid.Presence[targetId]));
            explanation.Add(Texts.Fill(Channels.Texto("presenca"), ("alvo", target), ("min", Texts.Number(low.Presence[targetId], "0.0")), ("max", Texts.Number(high.Presence[targetId], "0.0"))));
        }
        else if (low.Presence.Count > 0)
        {
            lines.Add(new ForecastLine("presence", null, CityAverage(low.Presence), CityAverage(high.Presence), CityAverage(mid.Presence)));
            explanation.Add(Texts.Fill(Channels.Texto("presenca_cidade"), ("min", Texts.Number(CityAverage(low.Presence), "0.0")), ("max", Texts.Number(CityAverage(high.Presence), "0.0"))));
        }

        foreach (string group in low.Support.Keys)
        {
            lines.Add(new ForecastLine("support", group, low.Support[group], high.Support[group], mid.Support[group]));
        }

        if (low.Support.Count > 0)
        {
            explanation.Add(Texts.Fill(Channels.Texto(c.Alvo == ChannelDefinition.AlvoZona ? "apoio_zona" : "apoio"), ("alvo", target),
                ("min", Texts.Number(low.Support.Values.Min(), "0.000")), ("max", Texts.Number(high.Support.Values.Max(), "0.000"))));
        }

        if (c.EmGrupos)
        {
            lines.Add(new ForecastLine("people", null, CabosPeople(amount), CabosPeople(amount), CabosPeople(amount)));
            explanation.Add(Texts.Fill(Channels.Texto("cabos_seguram"), ("alvo", target)));
            explanation.Add(Texts.Fill(Channels.Texto("cabos_limite"), ("limite", Texts.Number(CabosLimit)), ("atual", Texts.Number(CabosPlanned))));
        }

        decimal increase = Math.Max(0, amount - Budget.ActiveOf(channelId, targetId));
        explanation.Add(Texts.Fill(Channels.Texto("cobranca"), ("valor", Texts.Money(increase))));
        return new CommandForecast(CanAllocate(channelId, targetId, amount), lines, [], explanation) { Factors = factors };
    }

    /// <summary>Resumo da verba para a tela: plano, o que está pago, folha e semanas de caixa.</summary>
    public BudgetView BudgetSummary()
    {
        decimal plan = Budget.Plan.Sum(a => a.Amount);
        decimal payroll = Payroll().Weekly;
        decimal weekly = plan + payroll;
        return new BudgetView(plan, Budget.Active.Sum(a => a.Amount), payroll,
            weekly <= 0 ? double.PositiveInfinity : (double)(Math.Max(0, State.Player.Resources.Money) / weekly), CabosPlanned, CabosLimit);
    }

    /// <summary>
    /// Fim da semana: a verba paga rende (presença e apoio), com a sorte da semana, e fica o resultado explicado.
    /// Todos os canais são calculados sobre o estado do fim da semana e só depois gravados: o que um rende não
    /// depende da ordem em que os outros entram, e cada resultado bate com a previsão do próprio canal.
    /// </summary>
    private void ChannelsWeekEnd()
    {
        Budget.LastResults.Clear();
        if (!CampaignRunning)
        {
            return;
        }

        var definitions = new List<ChannelDefinition>();
        foreach (ChannelAllocation a in Budget.Active.Where(a => a.Amount > 0).ToList())
        {
            if (Channels.Find(a.ChannelId) is not { } c || !ValidTarget(c, a.TargetId))
            {
                continue;
            }

            var (ret, share, rival, factors) = PlanChannel(c, a.TargetId, a.Amount);
            var range = LuckRange(ChannelRules.Sorte);
            double luck = Rng.NextDouble(range.Min, range.Max);
            double yield = factors.Aggregate(1.0, (acc, f) => acc * f.Multiplier);
            var (presence, support) = ChannelGains(c, a.TargetId, ret * share * yield, luck);
            factors.Add(Factor("sorte", "Sorte da semana", luck));
            definitions.Add(c);
            Budget.LastResults.Add(new ChannelResult
            {
                Week = State.Calendar.WeekStart,
                ChannelId = c.Id,
                TargetId = a.TargetId,
                Spent = a.Amount,
                RivalBudget = rival,
                Return = ret,
                Share = share,
                Presence = presence,
                Support = support,
                Factors = factors,
            });
        }

        double[] own = Territory.Presence[RaceSetup.PlayerId];
        for (int i = 0; i < Budget.LastResults.Count; i++)
        {
            ChannelResult result = Budget.LastResults[i];
            int zone = definitions[i].Alvo == ChannelDefinition.AlvoZona ? Territory.IndexOf(result.TargetId) : -1;
            foreach (var (zoneId, delta) in result.Presence)
            {
                int z = Territory.IndexOf(zoneId);
                own[z] = Math.Clamp(own[z] + delta, 0, 100);
            }

            if (zone >= 0 && result.Presence.Count > 0)
            {
                // Material e som na rua contam como presença da semana: a zona não decai nesta virada.
                Territory.MarkActive(RaceSetup.PlayerId, zone);
            }

            Dictionary<string, double> store = zone >= 0 ? Territory.MeetingSupport[zone] : Campaign.Player.Support;
            foreach (var (group, gain) in result.Support)
            {
                store[group] = Math.Clamp(store.GetValueOrDefault(group) + gain, 0, 1);
            }
        }

        if (Budget.LastResults.Count > 0)
        {
            Campaign.Player.Recognition = Territory.Recognition(RaceSetup.PlayerId);
            RefreshSupport();
        }
    }

    /// <summary>Com cabos pagos na zona, a presença do jogador ali cai só uma fração do que cairia.</summary>
    private void HoldPresenceWithCabos(double[] before)
    {
        double[] now = Territory.Presence[RaceSetup.PlayerId];
        foreach (ChannelAllocation a in Budget.Active.Where(a => a.ChannelId == ChannelDefinition.Cabos && a.Amount > 0))
        {
            int z = Territory.IndexOf(a.TargetId);
            if (z >= 0 && now[z] < before[z])
            {
                now[z] = before[z] - ((before[z] - now[z]) * ChannelRules.CabosFatorDecaimento);
            }
        }
    }

    /// <summary>
    /// Segunda-feira: o plano da semana é cobrado (o que o caixa não paga fica de fora), a verba a mais dos rivais
    /// esfria, e cabos que deixaram de ser pagos numa zona vão trabalhar para um rival dali.
    /// </summary>
    private void ChannelsWeekStart()
    {
        List<ChannelAllocation> previous = Budget.Active.ToList();
        Budget.Active.Clear();
        foreach (string key in Budget.RivalExtra.Keys.ToList())
        {
            double value = Budget.RivalExtra[key] * ChannelRules.RivalExtraDecaimento;
            if (value < 1)
            {
                Budget.RivalExtra.Remove(key);
            }
            else
            {
                Budget.RivalExtra[key] = value;
            }
        }

        if (CampaignRunning)
        {
            foreach (ChannelAllocation a in Budget.Plan.Where(a => a.Amount > 0).ToList())
            {
                if (Channels.Find(a.ChannelId) is not { } c || !ValidTarget(c, a.TargetId) || PhaseBlocks(c, a.Amount) is not null)
                {
                    continue;
                }

                if (State.Player.Resources.Money < a.Amount)
                {
                    Agenda.AddFeed(new FeedItem(Today, "verba", Texts.Fill(Channels.Texto("sem_caixa"), ("canal", c.Nome), ("alvo", TargetName(c, a.TargetId))), c.Alvo == ChannelDefinition.AlvoZona ? a.TargetId : null, null, RaceSetup.PlayerId));
                    continue;
                }

                PayChannels(a.Amount);
                Budget.Active.Add(new ChannelAllocation { ChannelId = a.ChannelId, TargetId = a.TargetId, Amount = a.Amount });
            }
        }

        foreach (ChannelAllocation old in previous.Where(a => a.ChannelId == ChannelDefinition.Cabos))
        {
            decimal cut = old.Amount - Budget.ActiveOf(old.ChannelId, old.TargetId);
            int zone = Territory.IndexOf(old.TargetId);
            if (cut <= 0 || zone < 0 || !CampaignRunning)
            {
                continue;
            }

            // Quem foi dispensado vai para o rival mais presente na zona, e a verba dele ali cresce por um tempo.
            string key = BudgetState.Key(old.ChannelId, old.TargetId);
            Budget.RivalExtra[key] = Budget.RivalExtra.GetValueOrDefault(key) + (double)cut;
            Candidate? rival = Campaign.ForOffice(Campaign.Player.Office).Where(x => !x.IsPlayer && !x.IsBulk).MaxBy(x => Territory.PresenceOf(x.Id, zone));
            if (rival is not null)
            {
                Agenda.AddFeed(new FeedItem(Today, "cabos", Texts.Fill(Channels.Texto("cabos_dispensados"), ("alvo", ZoneName(old.TargetId)), ("rival", rival.Name)), old.TargetId, null, rival.Id));
            }
        }
    }
}
