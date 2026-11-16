using Planalto.Core.Elections;
using Planalto.Core.Electorate;
using Planalto.Core.Media;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Campaign;

/// <summary>
/// Rivais com estratégia (GDD 17.11): cada rival nomeado tem perfil, plano refeito toda segunda (meta de votos,
/// zonas-alvo, mistura de canais, postura diante dos concorrentes) e memória. Agem em dias, com compromissos
/// públicos que viram fato; gastam verba com estado próprio; disputam lideranças; trocam de plano por gatilho.
/// </summary>
public sealed partial class CampaignEngine
{
    private RivalCatalog RivalContent => Context.Campaign.Rivals;

    private RivalRules RivalRules => RivalContent.Regras;

    public RivalsState Rivals => Campaign.Rivals;

    /// <summary>
    /// Projeção da disputa do jogador feita nesta virada de semana (a dos planos dos rivais). Só vale dentro da própria
    /// virada, para a simulação semanal projetar a cidade uma vez só; fora dela é sempre null.
    /// </summary>
    private VoteProjection? _weekProjection;

    /// <summary>
    /// Votos projetados de cada candidato hoje (sem ruído): uma projeção da disputa do jogador; na disputa do outro
    /// cargo vale o reconhecimento, que basta para ordenar os candidatos. Não fica guardado no motor: o resultado
    /// depende só do estado, para um save carregado seguir igual ao jogo que não parou.
    /// </summary>
    /// <param name="keep">Guarda a projeção para o resto da virada da semana (pesquisa pública e objetivos não projetam de novo).</param>
    private Dictionary<string, double> ProjectedVotes(bool keep = false)
    {
        var votes = new Dictionary<string, double>(StringComparer.Ordinal);
        Office office = Campaign.Player.Office;
        VoteProjection projection = Project(office, null);
        if (keep)
        {
            _weekProjection = projection;
        }

        foreach (var (id, value) in projection.Votes)
        {
            votes[id] = value;
        }

        foreach (Candidate other in Campaign.Candidates.Where(c => c.Office != office))
        {
            votes[other.Id] = other.Recognition;
        }

        return votes;
    }

    // ------------------------------------------------------------------ plano da semana

    /// <summary>
    /// Segunda-feira: cada rival nomeado refaz o plano. Na primeira vez, monta o de partida. Devolve a projeção de
    /// votos usada (uma por semana), para os outros sistemas da virada não projetarem de novo.
    /// </summary>
    private Dictionary<string, double>? RivalsWeekStart()
    {
        if (!CampaignRunning || Rivals.Records.All(r => r.Plan?.Week == State.Calendar.WeekStart))
        {
            return null;
        }

        Dictionary<string, double> votes = ProjectedVotes(keep: true);
        Rivals.PlayerVotes = votes.GetValueOrDefault(RaceSetup.PlayerId);
        foreach (RivalRecord record in Rivals.Records)
        {
            record.Votes = votes.GetValueOrDefault(record.CandidateId);
        }

        foreach (RivalRecord record in Rivals.Records)
        {
            if (Campaign.Find(record.CandidateId) is { } candidate && record.Plan?.Week != State.Calendar.WeekStart)
            {
                PlanRival(record, candidate, votes);
            }
        }

        return votes;
    }

    private void PlanRival(RivalRecord record, Candidate c, Dictionary<string, double> votes)
    {
        RivalRules rules = RivalRules;
        RivalProfile profile = RivalContent.Profile(record.ProfileId);
        Person person = City.Person(c.Id)!;
        TerritoryState t = Territory;
        Candidate player = Campaign.Player;
        List<int> redutos = t.Redutos.GetValueOrDefault(c.Id) ?? [];
        bool sameOffice = c.Office == player.Office;
        RivalPlan? old = record.Plan;

        // Gatilhos de mudança (GDD 17.11): ataque, perda de liderança e pesquisa chegam marcados durante a semana;
        // a entrada do jogador no reduto é conferida aqui.
        string trigger = record.PendingTrigger ?? RivalPlan.Rotina;
        record.PendingTrigger = null;
        int defend = -1;
        if (old is not null && sameOffice)
        {
            foreach (int z in redutos)
            {
                double now = t.PresenceOf(RaceSetup.PlayerId, z);
                bool passed = now > t.PresenceOf(c.Id, z);
                // Quem já defende aquele reduto não troca de plano de novo a cada avanço do jogador ali.
                bool defending = old.Strategy == RivalPlan.Defender && old.DefendZone == z;
                if ((!defending && now - old.PlayerPresenceBase.GetValueOrDefault(z, now) > rules.GatilhoPresencaReduto) || (passed && !old.PlayerAheadAtBase.Contains(z)))
                {
                    trigger = RivalPlan.GatilhoReduto;
                    defend = z;
                    break;
                }
            }
        }

        bool changed = old is null || trigger != RivalPlan.Rotina;
        string? lastAttacker = Rivals.Attacks.LastOrDefault(a => a.TargetId == c.Id)?.AttackerId;
        string strategy = trigger switch
        {
            RivalPlan.GatilhoReduto => RivalPlan.Defender,
            RivalPlan.GatilhoAtaque => person.Temperament is Temperament.Conciliador or Temperament.Leal ? RivalPlan.Recompor : RivalPlan.AtacarEstrategia,
            RivalPlan.GatilhoLideranca => RivalPlan.Recompor,
            RivalPlan.GatilhoPesquisa => profile.Agressividade >= 0.5 ? RivalPlan.AtacarEstrategia : RivalPlan.Recompor,
            _ => old?.Strategy ?? RivalPlan.Expandir,
        };
        var plan = new RivalPlan
        {
            Week = State.Calendar.WeekStart,
            Trigger = changed ? trigger : old!.Trigger,
            Strategy = strategy,
            ChangedOn = changed ? Today : old!.ChangedOn,
            DefendZone = changed ? defend : old!.DefendZone,
            Theme = profile.UsaTema ? person.Interests.FirstOrDefault(i => i.Kind == Interest.Tema)?.Target : null,
            PlayerPresenceBase = changed ? redutos.ToDictionary(z => z, z => t.PresenceOf(RaceSetup.PlayerId, z)) : old!.PlayerPresenceBase,
            PlayerAheadAtBase = changed ? redutos.Where(z => t.PresenceOf(RaceSetup.PlayerId, z) > t.PresenceOf(c.Id, z)).ToList() : old!.PlayerAheadAtBase,
        };

        // Zonas-alvo: os redutos (os maiores primeiro) e, nas vagas que sobram, as zonas em que ele está mais perto de
        // passar quem lidera a presença ali (quem não tem reduto mira as maiores). A zona a defender vem primeiro.
        var zones = Enumerable.Range(0, t.Zones.Count).ToList();
        int count = Math.Min(profile.ZonasAlvo, zones.Count);
        List<Candidate> contenders = Campaign.ForOffice(c.Office).Where(x => !x.IsBulk && x.Id != c.Id).ToList();
        double Gap(int z) => contenders.Count == 0 ? 0 : contenders.Max(x => t.PresenceOf(x.Id, z)) - t.PresenceOf(c.Id, z);
        List<int> targets = redutos.OrderByDescending(z => t.Population[z]).ThenBy(z => z).Take(count).ToList();
        targets.AddRange(zones.Where(z => !targets.Contains(z))
            .OrderBy(z => redutos.Count == 0 ? 0 : Math.Max(0, Gap(z))).ThenByDescending(z => t.Population[z]).ThenBy(z => z).Take(count - targets.Count));
        if (plan.DefendZone >= 0)
        {
            targets.Remove(plan.DefendZone);
            targets.Insert(0, plan.DefendZone);
            targets = targets.Take(count).ToList();
        }

        plan.TargetZones = targets;
        plan.OpportunityZone = targets.Where(z => !redutos.Contains(z) && Gap(z) > 0).DefaultIfEmpty(-1).First();

        // Meta de votos: passar com folga o mais votado da própria chapa (ou os votos de hoje, se já é ele).
        double own = votes.GetValueOrDefault(c.Id);
        var slate = Campaign.ForOffice(c.Office).Where(x => !x.IsBulk && x.PartyId == c.PartyId).Select(x => votes.GetValueOrDefault(x.Id)).OrderDescending().ToList();
        plan.VoteGoal = sameOffice ? Math.Round(rules.MetaFolga * Math.Max(own, slate.Count > 0 ? slate[0] : own)) : 0;

        // Postura diante de cada concorrente: pacto compõe; quem entrou no reduto ou lidera uma zona-alvo é disputado;
        // quem atacou é atacado de volta; quem está logo à frente pode ser atacado, conforme a agressividade.
        Pact? pact = Rivals.PactWith(c.Id);
        if (sameOffice)
        {
            Posture posture = Posture.Ignorar;
            if (pact is not null)
            {
                posture = Posture.Compor;
            }
            else if (plan.DefendZone >= 0 || targets.Any(z => t.PresenceOf(RaceSetup.PlayerId, z) > t.PresenceOf(c.Id, z)))
            {
                posture = Posture.DisputarZona;
            }

            plan.Postures[RaceSetup.PlayerId] = posture;
        }

        Candidate? ahead = Campaign.ForOffice(c.Office).Where(x => !x.IsBulk && x.Id != c.Id && votes.GetValueOrDefault(x.Id) > own)
            .OrderBy(x => votes.GetValueOrDefault(x.Id)).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        double attackChance = Math.Min(1, profile.Agressividade * rules.ChanceAtaqueSemanal * (strategy == RivalPlan.AtacarEstrategia ? 2 : 1));
        if (ahead is not null && !plan.Postures.ContainsKey(ahead.Id))
        {
            plan.Postures[ahead.Id] = Posture.DisputarZona;
        }

        // Quem ele ataca nesta semana, se atacar: quem o atacou (na estratégia de ataque); senão quem está logo à frente
        // nos votos ou quem lidera a presença na zona que ele mais quer. Colega de chapa e parceiro de pacto ficam fora.
        if (Rng.Chance(attackChance))
        {
            var options = new List<string>();
            if (strategy == RivalPlan.AtacarEstrategia && lastAttacker is not null && Campaign.Find(lastAttacker) is not null)
            {
                options.Add(lastAttacker);
            }
            else
            {
                if (ahead is not null && ahead.PartyId != c.PartyId)
                {
                    options.Add(ahead.Id);
                }

                int wanted = plan.DefendZone >= 0 ? plan.DefendZone : targets[0];
                Candidate? leader = Campaign.ForOffice(c.Office).Where(x => !x.IsBulk && x.Id != c.Id && x.PartyId != c.PartyId && t.PresenceOf(x.Id, wanted) > t.PresenceOf(c.Id, wanted))
                    .MaxBy(x => t.PresenceOf(x.Id, wanted));
                if (leader is not null && !options.Contains(leader.Id))
                {
                    options.Add(leader.Id);
                }

                // O jogador que está à frente dele nos votos e disputa o mesmo cargo é sempre um alvo possível.
                if (sameOffice && player.PartyId != c.PartyId && votes.GetValueOrDefault(player.Id) > own && !options.Contains(player.Id))
                {
                    options.Add(player.Id);
                }
            }

            options.RemoveAll(id => id == RaceSetup.PlayerId && pact is not null);
            if (options.Count > 0)
            {
                plan.Postures[Rng.Pick(options)] = Posture.Atacar;
            }
        }

        ScheduleActions(record, c, profile, plan, redutos);
        AllocateRivalBudget(record, c, profile, plan, redutos);
        record.Plan = plan;
        if (changed && old is not null)
        {
            // Mudança de plano é fato de bastidor: só quem tem coordenador bom fica sabendo (ficha do rival).
            Emit(FactKind.Plano, Texts.Fill(RivalContent.Texto($"plano_{strategy}"), ("nome", c.Name)), c.Id, zone: plan.DefendZone >= 0 ? t.Zones[plan.DefendZone] : null,
                sub: plan.Trigger, visibility: 0, feed: false);
        }
    }

    /// <summary>
    /// Atos da semana: o empenho e o porte dão as "saídas" da semana (como antes da R3), repartidas num número de
    /// compromissos públicos que cabe na agenda de uma pessoa; cada um acontece num dia e vira fato.
    /// </summary>
    private void ScheduleActions(RivalRecord record, Candidate c, RivalProfile profile, RivalPlan plan, List<int> redutos)
    {
        CampaignConfig config = Context.Config;
        RivalRules rules = RivalRules;
        TerritoryState t = Territory;
        double effort = config.NpcEffortByPorte.GetValueOrDefault(Context.Municipio.Porte, 1);
        double outings = Rng.NextInt(config.NpcActionsPerWeek.Min, config.NpcActionsPerWeek.Max + 1) * c.Drive * effort;
        double expected = Rng.NextInt(rules.CompromissosPorSemana.Min, rules.CompromissosPorSemana.Max + 1) * c.Drive;
        int count = Math.Max(1, (int)Math.Floor(expected) + (Rng.NextDouble() < expected - Math.Floor(expected) ? 1 : 0));
        double strength = outings / count;
        var zones = Enumerable.Range(0, t.Zones.Count).ToList();
        Pact? pact = Rivals.PactWith(c.Id);
        for (int i = 0; i < count; i++)
        {
            int day = Rng.NextInt(0, 7);
            bool official = State.Calendar.WeekStart.AddDays(day) >= Campaign.CampaignStart;
            List<string> kinds = profile.Compromissos.Keys.Order(StringComparer.Ordinal)
                .Where(k => profile.Compromissos[k] > 0 && Commitments.Tipos.FirstOrDefault(x => x.Id == k) is { } type
                    && type.Fases.Contains(official ? CampaignActionDefinition.Campanha : CampaignActionDefinition.PreCampanha))
                .ToList();
            if (kinds.Count == 0)
            {
                continue;
            }

            string kind = Rng.PickWeighted(kinds, k => profile.Compromissos[k]);
            int zone = -1;
            if (Commitments.Get(kind).ExigeZona)
            {
                // Com pacto de não agressão, o rival deixa a zona natal do jogador fora da agenda.
                zone = Rng.PickWeighted(zones, z => Math.Max(1, t.Population[z])
                    * (plan.TargetZones.Contains(z) ? profile.Foco : 1)
                    * (z == plan.DefendZone || z == plan.OpportunityZone ? rules.FatorConcentracao : 1)
                    * (pact is { Kind: PactKind.NaoAgressao } && z == t.HomeZone && zones.Count > 1 ? 0 : 1));
            }

            // Na zona em que concentra a campanha (a que defende ou a de oportunidade), cada ato leva mais estrutura.
            bool focus = zone >= 0 && (zone == plan.DefendZone || zone == plan.OpportunityZone);
            plan.Actions.Add(new RivalAction { Day = day, Kind = kind, Zone = zone, Strength = strength * (focus ? rules.FatorConcentracao : 1) });
        }

        // Procurar uma liderança das zonas-alvo (só quem tem reduto disputa lideranças, como antes).
        if (redutos.Count > 0)
        {
            double chance = Math.Min(1, LeaderRules.ChanceAssedio * Math.Clamp(c.Drive, 0.2, 1.5) * profile.LiderancaFator * (plan.Strategy == RivalPlan.Recompor ? 2 : 1));
            if (Rng.Chance(chance))
            {
                plan.Actions.Add(new RivalAction { Day = Rng.NextInt(0, 7), Kind = RivalAction.Lideranca, Zone = Rng.Pick(plan.TargetZones.Union(redutos).Order().ToList()) });
            }
        }

        foreach (string targetId in plan.Postures.Where(p => p.Value == Posture.Atacar).Select(p => p.Key).Order(StringComparer.Ordinal).ToList())
        {
            bool recent = Rivals.Attacks.Any(a => a.AttackerId == c.Id && a.TargetId == targetId && Today.DayNumber - a.Date.DayNumber < rules.AtaqueIntervaloDias);
            if (!recent)
            {
                plan.Actions.Add(new RivalAction { Day = Rng.NextInt(0, 7), Kind = RivalAction.Ataque, TargetId = targetId });
                break;
            }
        }

        Person person = City.Person(c.Id)!;
        if (c.Office == Campaign.Player.Office && (person.Temperament == Temperament.Calculista || profile.Id == RivalProfile.Estrutura)
            && State.Ledger.Skeletons.Any(s => !s.KnownBy.Contains(c.Id)) && Rng.Chance(rules.ChanceInvestigar))
        {
            plan.Actions.Add(new RivalAction { Day = Rng.NextInt(0, 7), Kind = RivalAction.Investigar, TargetId = RaceSetup.PlayerId });
        }
    }

    /// <summary>
    /// Verba da semana por canal e alvo, com estado próprio: a mistura de canais vem do perfil, a verba sai do caixa
    /// do rival e vai para as zonas-alvo (redes: pelas faixas de idade). Antes da campanha oficial só gastam, e
    /// pouco, nos canais que a pré-campanha permite.
    /// </summary>
    private void AllocateRivalBudget(RivalRecord record, Candidate c, RivalProfile profile, RivalPlan plan, List<int> redutos)
    {
        RivalRules rules = RivalRules;
        double scale = CostScale * c.Drive * profile.VerbaFator;
        record.Money += Math.Round(rules.ArrecadacaoSemanal * (decimal)scale);
        record.Budget = [];
        bool pre = BeforeOfficialCampaign;
        double weekly = (double)ChannelRules.RivalVerba * scale;
        var wanted = new List<ChannelAllocation>();
        double bandTotal = Channels.FaixasIdade.Sum(b => b.Peso);
        List<int> zones = redutos.Count > 0 ? plan.TargetZones : Enumerable.Range(0, Territory.Zones.Count).ToList();
        foreach (ChannelDefinition channel in Channels.Canais)
        {
            double phase = !pre ? 1 : channel.PreCampanha is null ? 0 : ChannelRules.RivalPreCampanha;
            double amount = weekly * profile.Canais.GetValueOrDefault(channel.Id) * phase;
            if (amount <= 0)
            {
                continue;
            }

            if (channel.Alvo == ChannelDefinition.AlvoIdade)
            {
                wanted.AddRange(Channels.FaixasIdade.Select(b => new ChannelAllocation { ChannelId = channel.Id, TargetId = b.Id, Amount = (decimal)(amount * b.Peso / bandTotal) }));
            }
            else if (zones.Count > 0)
            {
                wanted.AddRange(zones.Select(z => new ChannelAllocation { ChannelId = channel.Id, TargetId = Territory.Zones[z], Amount = (decimal)(amount / zones.Count) }));
            }
        }

        decimal total = wanted.Sum(a => a.Amount);
        if (total <= 0)
        {
            return;
        }

        decimal fraction = Math.Min(1, Math.Max(0, record.Money) / total);
        foreach (ChannelAllocation a in wanted)
        {
            a.Amount = Math.Round(a.Amount * fraction, 2);
        }

        decimal spent = wanted.Sum(a => a.Amount);
        record.Money -= spent;
        record.Spent += spent;
        record.Budget = wanted.Where(a => a.Amount > 0).ToList();
    }

    // ------------------------------------------------------------------ o dia dos rivais

    private void RivalsDay(DateOnly day)
    {
        int index = day.DayNumber - State.Calendar.WeekStart.DayNumber;
        foreach (RivalRecord record in Rivals.Records)
        {
            if (record.Plan is not { } plan || plan.Week != State.Calendar.WeekStart || Campaign.Find(record.CandidateId) is not { } c)
            {
                continue;
            }

            foreach (RivalAction action in plan.Actions.Where(a => !a.Done && a.Day <= index).ToList())
            {
                action.Done = true;
                RunRivalAction(c, plan, action, day);
            }
        }
    }

    private void RunRivalAction(Candidate c, RivalPlan plan, RivalAction action, DateOnly day)
    {
        TerritoryState t = Territory;
        CampaignConfig config = Context.Config;
        switch (action.Kind)
        {
            case RivalAction.Lideranca:
            {
                List<Leader> near = City.Leaders.Where(l => l.Zones.Contains(t.Zones[action.Zone])).ToList();
                if (near.Count == 0)
                {
                    near = City.Leaders.Where(l => plan.TargetZones.Any(z => l.Zones.Contains(t.Zones[z]))).ToList();
                }

                if (near.Count > 0)
                {
                    Leader target = Rng.Pick(near);
                    target.Favor[c.Id] = Math.Min(100, target.Favor.GetValueOrDefault(c.Id) + Rng.NextInt(LeaderRules.FavorAssedio.Min, LeaderRules.FavorAssedio.Max + 1));
                    Emit(FactKind.RivalLideranca, LeaderText("assedio", target, c.Id), c.Id, target.Zones[0], target.PersonId, date: day);
                }

                break;
            }

            case RivalAction.Ataque:
                if (action.TargetId is not null && Campaign.Find(action.TargetId) is { } target2 && !(target2.IsPlayer && Rivals.PactWith(c.Id) is not null))
                {
                    RivalAttack(c, target2);
                }

                break;
            case RivalAction.Investigar:
            {
                List<LedgerSkeleton> unknown = State.Ledger.Skeletons.Where(s => !s.KnownBy.Contains(c.Id)).ToList();
                if (unknown.Count > 0)
                {
                    Rng.Pick(unknown).KnownBy.Add(c.Id);
                }

                break;
            }

            default:
            {
                double amount = Commitments.Get("caminhada").Presenca * config.NpcActionEfficiency * RecognitionScale * action.Strength;
                bool spread = !t.Redutos.ContainsKey(c.Id);
                string? zoneId = action.Zone >= 0 ? t.Zones[action.Zone] : null;
                if (spread || action.Zone < 0)
                {
                    // Campanha espalhada (prefeito) e atos sem zona (entrevista, vídeo): um pouco em todas as zonas.
                    t.GainEverywhere(c.Id, amount * config.NpcSpreadFactor);
                }
                else
                {
                    List<int> neighbors = Context.Zonas.Zonas[action.Zone].Vizinhas.Select(t.IndexOf).Where(z => z >= 0).ToList();
                    t.Gain(c.Id, action.Zone, amount * Reach(action.Zone), Context.Campaign.Territory.Simulacao.Transbordo, neighbors);
                }

                c.Image = Math.Clamp(c.Image + (action.Strength * config.NpcImagePerAction), -1, 1);
                c.Recognition = t.Recognition(c.Id);
                string key = RivalContent.Textos.ContainsKey($"fato_{action.Kind}") ? $"fato_{action.Kind}" : "fato_caminhada";
                Emit(FactKind.RivalZona, Texts.Fill(RivalContent.Texto(key), ("nome", c.Name), ("zona", zoneId is null ? Context.Municipio.Nome : ZoneName(zoneId))),
                    c.Id, zoneId, theme: plan.Theme, sub: action.Kind, date: day);
                break;
            }
        }
    }

    /// <summary>
    /// Verba semanal dos rivais da disputa do jogador no canal e alvo: a soma do que cada um pôs ali no plano desta
    /// semana, mais a verba a mais (cabos que o jogador dispensou).
    /// </summary>
    public decimal RivalBudget(string channelId, string targetId)
    {
        if (Channels.Find(channelId) is null)
        {
            return 0;
        }

        decimal total = (decimal)Budget.RivalExtra.GetValueOrDefault(BudgetState.Key(channelId, targetId));
        Office office = Campaign.Player.Office;
        foreach (RivalRecord record in Rivals.Records)
        {
            if (Campaign.Find(record.CandidateId) is { } c && c.Office == office)
            {
                total += record.Budget.Where(a => a.ChannelId == channelId && a.TargetId == targetId).Sum(a => a.Amount);
            }
        }

        return Math.Round(total);
    }

    /// <summary>Pesquisa pública publicada: quem caiu muito desde a anterior troca de plano na segunda seguinte.</summary>
    private void PollTriggers(PollRecord poll)
    {
        foreach (RivalRecord record in Rivals.Records)
        {
            if (!poll.Candidates.TryGetValue(record.CandidateId, out double share))
            {
                continue;
            }

            if (record.LastPoll is { } before && before - share >= RivalRules.GatilhoQuedaPesquisa)
            {
                record.PendingTrigger ??= RivalPlan.GatilhoPesquisa;
                Emit(FactKind.Pesquisa, Texts.Fill(RivalContent.Texto("fato_queda"), ("nome", NameOf(record.CandidateId)), ("pontos", Texts.Number(before - share, "0.0"))), record.CandidateId, sub: "queda");
            }

            record.LastPoll = share;
        }
    }

    // ------------------------------------------------------------------ ataques

    private AttackCatalog AttackContent => Context.Campaign.Attacks;

    private AttackRules AttackRules => AttackContent.Regras;

    /// <summary>Quanto de imagem o ataque ainda tira hoje: o dano se apaga em linha reta até o fim.</summary>
    public double DamageNow(Attack attack)
    {
        int total = attack.Ends.DayNumber - attack.FadeStart.DayNumber;
        int left = attack.Ends.DayNumber - Today.DayNumber;
        return total <= 0 || left <= 0 ? 0 : attack.Damage * Math.Min(1, (double)left / total);
    }

    /// <summary>Imagem que os ataques em curso tiram do candidato hoje.</summary>
    public double AttackPenalty(string candidateId)
    {
        double total = 0;
        foreach (Attack attack in Rivals.Attacks)
        {
            if (attack.TargetId == candidateId)
            {
                total += DamageNow(attack);
            }
        }

        return total;
    }

    private double AttackDamage(AttackType type, AttackChannel channel, bool proof, int severity, double luck, bool rumor) =>
        type.Dano * channel.Fator * luck * (rumor ? AttackRules.BoatoFator : 1)
        * (!type.UsaProva ? 1 : proof ? AttackRules.ProvaBase + (AttackRules.ProvaPorGravidade * severity) : type.SemProvaFator);

    private Attack LaunchAttack(Candidate attacker, Candidate target, AttackType type, AttackChannel channel, bool proof, string? theme, string? skeletonId, Outlet? outlet, double damage, string subject, bool rumor = false, bool falso = false)
    {
        string text = Texts.Fill(type.Fato, ("ator", attacker.Name), ("alvo", target.Name), ("tema", ThemeName(theme)), ("assunto", subject));
        Fact fact = Emit(rumor ? FactKind.Boato : FactKind.Ataque, text, attacker.Id, theme: theme, target: target.Id, sub: type.Id, visibility: channel.Visibilidade, rumor: rumor, falso: falso);
        if (outlet is not null && !rumor)
        {
            Publish(fact, outlet);
        }

        var attack = new Attack
        {
            Id = $"a{Rivals.NextId++}",
            AttackerId = attacker.Id,
            TargetId = target.Id,
            Type = type.Id,
            Channel = channel.Id,
            OutletId = outlet?.Id,
            Date = Today,
            FadeStart = Today,
            Proof = proof,
            Theme = theme,
            SkeletonId = skeletonId,
            Damage = damage,
            Ends = Today.AddDays(7 * AttackRules.SemanasSemResposta),
            Deadline = target.IsPlayer ? Today.AddDays(AttackRules.PrazoRespostaHoras / 24) : null,
            Rumor = rumor,
            False = falso,
            FactId = fact.Id,
            Text = text,
        };
        Rivals.Attacks.Add(attack);
        attacker.Image = Math.Clamp(attacker.Image - AttackRules.Rejeicao, -1, 1);
        return attack;
    }

    private (double Min, double Max) AttackLuck() => LuckRange(AttackRules.Sorte);

    private Outlet? AttackOutlet(Candidate target) =>
        City.Outlets.Where(o => !OutletContent.Type(o.Type).Rede).OrderByDescending(o => StanceToward(o, target.Id) == "critico").ThenByDescending(ReachShare).ThenBy(o => o.Id, StringComparer.Ordinal).FirstOrDefault();

    /// <summary>Um rival ataca: usa o que tem contra o alvo (esqueleto que conhece, mudança de posição) ou critica um tema; às vezes solta boato.</summary>
    private void RivalAttack(Candidate attacker, Candidate target)
    {
        AttackRules rules = AttackRules;
        var range = AttackLuck();
        double luck = Rng.NextDouble(range.Min, range.Max);
        List<AttackChannel> channels = AttackContent.Canais.Where(ch => !(ch.SoCampanha && BeforeOfficialCampaign) && (!ch.ExigeVeiculo || AttackOutlet(target) is not null)).ToList();
        if (channels.Count == 0)
        {
            return;
        }

        AttackChannel channel = Rng.PickWeighted(channels, ch => ch.Fator);
        string theme = City.Person(attacker.Id)?.Interests.FirstOrDefault(i => i.Kind == Interest.Tema)?.Target ?? Context.Themes.Themes[0].Id;
        Attack attack;
        if (target.IsPlayer && State.Ledger.Skeletons.Where(s => s.KnownBy.Contains(attacker.Id)).OrderByDescending(s => s.Severity).FirstOrDefault() is { } skeleton)
        {
            AttackType type = AttackContent.Type(AttackType.Esqueleto)!;
            ExposeSkeleton(skeleton, attacker.Id, announce: false);
            attack = LaunchAttack(attacker, target, type, channel, true, null, skeleton.Id, channel.ExigeVeiculo ? AttackOutlet(target) : null,
                AttackDamage(type, channel, true, skeleton.Severity, luck, false), skeleton.Text);
        }
        else if (target.IsPlayer && Platform.Changes.Count > 0)
        {
            AttackType type = AttackContent.Type(AttackType.Incoerencia)!;
            string changed = Platform.Changes[^1].Theme;
            attack = LaunchAttack(attacker, target, type, channel, true, changed, null, channel.ExigeVeiculo ? AttackOutlet(target) : null,
                AttackDamage(type, channel, true, 2, luck, false), Texts.Fill(AttackContent.Texto("assunto_incoerencia"), ("tema", ThemeName(changed))));
        }
        else if (target.IsPlayer && Rng.Chance(rules.ChanceBoato) && AttackContent.Channel(AttackChannel.Redes) is { } networks)
        {
            AttackType type = AttackContent.Type(AttackType.Esqueleto)!;
            bool falso = Rng.Chance(rules.BoatoChanceFalso);
            attack = LaunchAttack(attacker, target, type, networks, false, null, null, null, AttackDamage(type, networks, false, 1, luck, true), AttackContent.Texto("boato"), rumor: true, falso: falso);
        }
        else
        {
            AttackType type = AttackContent.Type(AttackType.Tema)!;
            attack = LaunchAttack(attacker, target, type, channel, false, theme, null, channel.ExigeVeiculo ? AttackOutlet(target) : null,
                AttackDamage(type, channel, false, 1, luck, false), Texts.Fill(AttackContent.Texto("assunto_tema"), ("tema", ThemeName(theme))));
        }

        if (!target.IsPlayer && Rivals.Record(target.Id) is { } record)
        {
            record.PendingTrigger = RivalPlan.GatilhoAtaque;
            ScheduleReaction(target, attacker, attack);
        }
    }

    /// <summary>Como o rival deve reagir a um ataque de quem atacou, pelo temperamento; null = ignora (está muito à frente).</summary>
    private string? ReactionKind(Candidate rival, Candidate attacker, bool roll)
    {
        // Pelos votos projetados na segunda-feira (guardados no plano): o dia de um ataque não refaz a projeção da cidade.
        double VotesOf(Candidate c) => c.IsPlayer ? Rivals.PlayerVotes : Rivals.Record(c.Id)?.Votes ?? 0;
        double own = VotesOf(rival);
        bool leads = Campaign.ForOffice(rival.Office).Where(x => !x.IsBulk).All(x => VotesOf(x) <= own);
        if (leads && rival.Office == attacker.Office && own >= RivalRules.MuitoAFrente * Math.Max(1, VotesOf(attacker)))
        {
            return null;
        }

        return City.Person(rival.Id)?.Temperament switch
        {
            Temperament.Combativo => RivalReaction.Revide,
            Temperament.Calculista => RivalReaction.Justica,
            Temperament.Voluvel => roll ? Rng.Pick<string>([RivalReaction.Revide, RivalReaction.Vitima]) : RivalReaction.Vitima,
            _ => RivalReaction.Vitima,
        };
    }

    private string? ScheduleReaction(Candidate rival, Candidate attacker, Attack attack)
    {
        string? kind = ReactionKind(rival, attacker, roll: true);
        if (kind is null)
        {
            return null;
        }

        IntRange days = kind == RivalReaction.Revide ? RivalRules.RevideDias : kind == RivalReaction.Justica ? RivalRules.JusticaDias : RivalRules.VitimaDias;
        Rivals.Reactions.Add(new RivalReaction { RivalId = rival.Id, Kind = kind, Due = Today.AddDays(Rng.NextInt(days.Min, days.Max + 1)), AttackId = attack.Id });
        return kind;
    }

    /// <summary>Prazos do dia: reações marcadas dos rivais, respostas do jogador que venceram e direitos de resposta que saem.</summary>
    private void AttacksDay(DateOnly day)
    {
        foreach (RivalReaction reaction in Rivals.Reactions.Where(r => r.Due <= day).ToList())
        {
            Rivals.Reactions.Remove(reaction);
            Attack? attack = Rivals.Attacks.FirstOrDefault(a => a.Id == reaction.AttackId);
            if (attack is null || Campaign.Find(reaction.RivalId) is not { } rival || Campaign.Find(attack.AttackerId) is not { } attacker)
            {
                continue;
            }

            switch (reaction.Kind)
            {
                case RivalReaction.Revide:
                    RivalAttack(rival, attacker);
                    WellAnswered(attack);
                    break;
                case RivalReaction.Vitima:
                    rival.Image = Math.Clamp(rival.Image + RivalRules.VitimaImagem, -1, 1);
                    WellAnswered(attack);
                    Emit(FactKind.Resposta, Texts.Fill(RivalContent.Texto("fato_vitima"), ("nome", rival.Name), ("alvo", attacker.Name)), rival.Id, target: attacker.Id, sub: RivalReaction.Vitima);
                    break;
                default:
                {
                    // Justiça Eleitoral: ataque que afirma um fato sem prova tende a cair; com prova, o pedido é negado.
                    AttackType type = AttackContent.Type(attack.Type)!;
                    double defense = attacker.IsPlayer ? LawyerSuccessChance : StaffContent.Role(StaffRole.Advogado).Efeitos[StaffRole.Exito].Sem;
                    bool accepted = type.UsaProva && !attack.Proof && !Rng.Chance(defense);
                    if (accepted)
                    {
                        attack.Damage = 0;
                        attack.Ends = Today;
                        attacker.Image = Math.Clamp(attacker.Image - AttackRules.DireitoDeRespostaImagem, -1, 1);
                        if (attacker.IsPlayer)
                        {
                            State.Player.Resources.Money -= ScaledMoney(AttackRules.MultaProcesso);
                            attacker.Money = State.Player.Resources.Money;
                        }
                    }

                    Emit(FactKind.Justica, Texts.Fill(RivalContent.Texto(accepted ? "fato_justica_aceita" : "fato_justica_negada"), ("nome", rival.Name), ("alvo", attacker.Name)),
                        rival.Id, target: attacker.Id, sub: accepted ? "aceita" : "negada");
                    break;
                }
            }
        }

        foreach (Attack attack in Rivals.Attacks.Where(a => a.TargetId == RaceSetup.PlayerId).ToList())
        {
            if (attack.Response is null && attack.Deadline is { } deadline && deadline < day)
            {
                // O prazo de 48 horas venceu: vale o silêncio, e fica a versão de quem atacou.
                attack.Response = AttackResponse.Silencio;
                Emit(FactKind.Resposta, AttackContent.Texto("prazo_vencido"), RaceSetup.PlayerId, target: attack.AttackerId, sub: AttackResponse.Silencio, visibility: 0);
            }

            if (attack.RulingOn is { } ruling && ruling <= day)
            {
                attack.RulingOn = null;
                bool granted = Rng.Chance(LawyerSuccessChance);
                if (granted)
                {
                    WellAnswered(attack);
                    if (Campaign.Find(attack.AttackerId) is { } attacker)
                    {
                        attacker.Image = Math.Clamp(attacker.Image - AttackRules.DireitoDeRespostaImagem, -1, 1);
                    }
                }

                Emit(FactKind.Resposta, Texts.Fill(AttackContent.Texto(granted ? "direito_concedido" : "direito_negado"), ("rival", NameOf(attack.AttackerId))),
                    RaceSetup.PlayerId, target: attack.AttackerId, sub: AttackResponse.DireitoDeResposta, visibility: granted ? 2 : 0);
            }
        }

        Rivals.Attacks.RemoveAll(a => a.Ends.DayNumber < day.DayNumber - 14 && a.RulingOn is null);
    }

    /// <summary>Ataque bem respondido: o dano que resta cai e se apaga em 4 semanas (GDD 17.13).</summary>
    private void WellAnswered(Attack attack)
    {
        double now = DamageNow(attack) * AttackRules.RespondidoFator;
        DateOnly ends = Today.AddDays(7 * AttackRules.SemanasRespondido);
        attack.WellAnswered = true;
        attack.Damage = now;
        attack.FadeStart = Today;
        attack.Ends = ends < attack.Ends ? ends : attack.Ends;
    }

    // ------------------------------------------------------------------ o jogador ataca um rival

    private (AttackType Type, AttackChannel Channel, Candidate Rival, RivalWeakness? Weakness, Outlet? Outlet)? AttackParts(AttackRequest r)
    {
        if (AttackContent.Type(r.Type) is not { } type || AttackContent.Channel(r.Channel) is not { } channel
            || Campaign.Find(r.RivalId) is not { IsPlayer: false, IsBulk: false } rival)
        {
            return null;
        }

        RivalWeakness? weakness = r.WeaknessId is null ? null : Rivals.Record(rival.Id)?.Weaknesses.FirstOrDefault(w => w.Id == r.WeaknessId && w.Known && w.Type == type.Id);
        return (type, channel, rival, weakness, r.OutletId is null ? null : City.Outlet(r.OutletId));
    }

    public decimal AttackCost(string channelId) => AttackContent.Channel(channelId) is { } c && c.Custo > 0 ? ScaledTens(c.Custo) : 0;

    public CommandFailure? CanAttack(AttackRequest r)
    {
        if (!CampaignRunning)
        {
            return CommandFailure.ForaDaCampanha;
        }

        if (AttackParts(r) is not { } parts)
        {
            return CommandFailure.NaoEncontrado;
        }

        if (parts.Channel.SoCampanha && BeforeOfficialCampaign)
        {
            return CommandFailure.CanalFechado;
        }

        if (parts.Channel.ExigeVeiculo && parts.Outlet is null)
        {
            return CommandFailure.VeiculoInvalido;
        }

        if (parts.Type.Id == AttackType.Tema && (r.Theme is null || Theme(r.Theme) is null))
        {
            return CommandFailure.AlvoInvalido;
        }

        if (r.WeaknessId is not null && parts.Weakness is null)
        {
            return CommandFailure.SemProva;
        }

        if (Rivals.Attacks.Any(a => a.AttackerId == RaceSetup.PlayerId && a.TargetId == parts.Rival.Id && Today.DayNumber - a.Date.DayNumber < RivalRules.AtaqueIntervaloDias))
        {
            return CommandFailure.JaFeito;
        }

        return State.Player.Resources.Money < AttackCost(parts.Channel.Id) ? CommandFailure.SemDinheiro : null;
    }

    /// <summary>
    /// Previsão do ataque: imagem que o rival perde (em faixa, pela sorte), o que custa a quem ataca (rejeição à
    /// campanha negativa, dinheiro), o risco de processo quando não há prova e a reação esperada pelo temperamento.
    /// </summary>
    public CommandForecast ForecastAttack(AttackRequest r)
    {
        CommandFailure? blocked = CanAttack(r);
        if (AttackParts(r) is not { } parts)
        {
            return new CommandForecast(blocked ?? CommandFailure.NaoEncontrado, [], [], [AttackContent.Texto("explica_dano")]);
        }

        AttackRules rules = AttackRules;
        bool proof = parts.Weakness is not null;
        int severity = parts.Weakness?.Severity ?? 1;
        var luck = AttackLuck();
        double low = AttackDamage(parts.Type, parts.Channel, proof, severity, luck.Min, false);
        double high = AttackDamage(parts.Type, parts.Channel, proof, severity, luck.Max, false);
        double mid = AttackDamage(parts.Type, parts.Channel, proof, severity, 1, false);
        double sue = parts.Type.UsaProva && !proof ? parts.Type.RiscoProcesso : 0;
        decimal cost = AttackCost(parts.Channel.Id);
        decimal fine = sue > 0 ? ScaledMoney(rules.MultaProcesso) : 0;

        // A imagem de quem ataca fica entre −1 e +1 (o comando limita): perto do piso, a perda é menor que a regra.
        double image = Campaign.Player.Image;
        double rejected = Math.Clamp(image - rules.Rejeicao, -1, 1);
        double afterRejection = rejected - image;
        double afterSuit = Math.Clamp(rejected - rules.ProcessoImagem, -1, 1) - image;
        var lines = new List<ForecastLine>
        {
            new("rival_image", parts.Rival.Id, -high, sue > 0 ? 0 : -low, -(1 - sue) * mid),
            new("image", null, sue > 0 ? afterSuit : afterRejection, afterRejection, ((1 - sue) * afterRejection) + (sue * afterSuit)),
            new("money", null, -(double)(cost + fine), -(double)cost, -(double)cost - (sue * (double)fine)),
        };
        var outcomes = new List<ForecastOutcome>();
        if (sue > 0)
        {
            outcomes.Add(new ForecastOutcome(sue, AttackContent.Texto("processo")));
        }

        outcomes.Add(new ForecastOutcome(1, ReactionText(parts.Rival)));
        var explanation = new List<string>
        {
            Texts.Fill(AttackContent.Texto("explica_dano"), ("rival", parts.Rival.Name), ("tipo", parts.Type.Nome), ("canal", parts.Channel.Nome), ("min", Texts.Number(low * 100, "0.0")), ("max", Texts.Number(high * 100, "0.0")),
                ("semanas", Texts.Number(rules.SemanasSemResposta))),
            Texts.Fill(AttackContent.Texto("explica_rejeicao"), ("valor", Texts.Number(rules.Rejeicao * 100, "0.0"))),
            Texts.Fill(AttackContent.Texto("explica_custo"), ("valor", Texts.Money(cost))),
            parts.Type.UsaProva && !proof
                ? Texts.Fill(AttackContent.Texto("explica_sem_prova"), ("chance", Texts.Percent(sue)), ("multa", Texts.Money(fine)))
                : Texts.Fill(AttackContent.Texto("explica_com_prova"), ("prova", parts.Weakness?.Text ?? parts.Type.Nome)),
        };
        if (Rivals.PactWith(parts.Rival.Id) is not null)
        {
            explanation.Add(Texts.Fill(AttackContent.Texto("explica_pacto"), ("rival", parts.Rival.Name)));
        }

        return new CommandForecast(blocked, lines, outcomes, explanation);
    }

    private string ReactionText(Candidate rival) => Texts.Fill(RivalContent.Texto(ReactionKind(rival, Campaign.Player, roll: false) switch
    {
        RivalReaction.Revide => "resposta_revide",
        RivalReaction.Justica => "resposta_justica",
        RivalReaction.Vitima => "resposta_vitima",
        _ => "resposta_ignora",
    }), ("nome", rival.Name));

    /// <summary>
    /// Ataca um rival por incoerência, esqueleto ou tema, por um canal. Quem ataca perde imagem entre quem rejeita
    /// campanha negativa; sem prova pode vir processo; o rival lembra para sempre e responde pelo temperamento.
    /// </summary>
    public (CommandFailure? Failure, AttackResult? Result) AttackRival(AttackRequest r)
    {
        if (CanAttack(r) is { } failure)
        {
            return (failure, null);
        }

        var parts = AttackParts(r)!.Value;
        AttackRules rules = AttackRules;
        Candidate player = Campaign.Player;
        CommandForecast forecast = ForecastAttack(r);
        bool proof = parts.Weakness is not null;
        var luck = AttackLuck();
        double damage = AttackDamage(parts.Type, parts.Channel, proof, parts.Weakness?.Severity ?? 1, Rng.NextDouble(luck.Min, luck.Max), false);
        bool sued = parts.Type.UsaProva && !proof && Rng.Chance(parts.Type.RiscoProcesso);
        decimal cost = AttackCost(parts.Channel.Id);
        double imageBefore = player.Image;
        string subject = parts.Weakness?.Text ?? Texts.Fill(AttackContent.Texto($"assunto_{parts.Type.Id}"), ("tema", ThemeName(r.Theme)));
        Attack attack = LaunchAttack(player, parts.Rival, parts.Type, parts.Channel, proof, r.Theme, null, parts.Outlet, sued ? 0 : damage, subject);
        Pay(cost);
        decimal money = -cost;
        if (sued)
        {
            // O processo derruba o ataque: a multa não é gasto de campanha, mas sai do mesmo caixa.
            attack.Ends = Today;
            decimal fine = ScaledMoney(rules.MultaProcesso);
            State.Player.Resources.Money -= fine;
            player.Money = State.Player.Resources.Money;
            player.Image = Math.Clamp(player.Image - rules.ProcessoImagem, -1, 1);
            money -= fine;
            Emit(FactKind.Justica, Texts.Fill(AttackContent.Texto("processo_resultado"), ("rival", parts.Rival.Name)), parts.Rival.Id, target: RaceSetup.PlayerId, sub: "processo");
        }

        // A lembrança do ataque fica na pessoa do rival e atravessa a eleição (na Câmara, pesa na relação).
        MemoryDefinition memory = RivalContent.Memory("ataque");
        City.Person(parts.Rival.Id)?.Remember(Today, "ataque", memory.Peso, memory.Pontos, memory.Texto, PeopleConfig);
        Campaign.Events.Relations[parts.Rival.Id] = Math.Clamp(Campaign.Events.Relations.GetValueOrDefault(parts.Rival.Id) + memory.Pontos, -100, 100);
        string? broken = BreakPact(parts.Rival.Id);
        string? reaction = null;
        if (Rivals.Record(parts.Rival.Id) is { } record)
        {
            record.PendingTrigger = RivalPlan.GatilhoAtaque;
            reaction = ScheduleReaction(parts.Rival, player, attack);
        }

        return (null, new AttackResult(attack, sued ? 0 : -damage, player.Image - imageBefore, money, sued, reaction, broken, forecast.Explanation));
    }

    // ------------------------------------------------------------------ o jogador responde a um ataque

    public IReadOnlyList<Attack> AttacksOnPlayer => Rivals.Attacks.Where(a => a.TargetId == RaceSetup.PlayerId && a.Ends >= Today).ToList();

    private string ResponseLabel(AttackResponse option, Attack attack) => attack.Rumor && option.NomeBoato is not null ? option.NomeBoato : option.Nome;

    private AttackRequest? CounterAttack(Attack attack)
    {
        if (Campaign.Find(attack.AttackerId) is not { IsPlayer: false, IsBulk: false } attacker)
        {
            return null;
        }

        RivalWeakness? weakness = Rivals.Record(attacker.Id)?.Weaknesses.Where(w => w.Known).OrderByDescending(w => w.Severity).FirstOrDefault();
        string channel = AttackContent.Channel(attack.Channel) is { ExigeVeiculo: false } same && !(same.SoCampanha && BeforeOfficialCampaign) ? same.Id : AttackChannel.Redes;
        return weakness is not null
            ? new AttackRequest(attacker.Id, weakness.Type, channel, weakness.Id)
            : new AttackRequest(attacker.Id, AttackType.Tema, channel, Theme: attack.Theme ?? Platform.Flags.FirstOrDefault() ?? Context.Themes.Themes[0].Id);
    }

    public CommandFailure? CanRespond(string attackId, string responseId)
    {
        if (Rivals.Attacks.FirstOrDefault(a => a.Id == attackId && a.TargetId == RaceSetup.PlayerId) is not { } attack || AttackContent.Response(responseId) is not { } option)
        {
            return CommandFailure.NaoEncontrado;
        }

        if (attack.Response is not null)
        {
            return CommandFailure.JaFeito;
        }

        if (attack.Deadline is { } deadline && deadline < Today)
        {
            return CommandFailure.PrazoVencido;
        }

        if (option.Advogado && StaffIn(StaffRole.Advogado) is null)
        {
            return CommandFailure.SemAdvogado;
        }

        if (option.Id == AttackResponse.ContraAtaque)
        {
            if (CounterAttack(attack) is not { } counter)
            {
                return CommandFailure.NaoEncontrado;
            }

            CommandFailure? cannot = CanAttack(counter);
            if (cannot is not null and not CommandFailure.JaFeito)
            {
                return cannot;
            }
        }

        return State.Player.Resources.Money < ScaledMoney(option.Custo) ? CommandFailure.SemDinheiro : null;
    }

    private double ResponseChance(AttackResponse option, Attack attack) =>
        option.Advogado ? LawyerSuccessChance
        : option.Teste is { } attribute ? Math.Clamp((State.Player.Attributes.Get(attribute) / 100.0) + option.Bonus + (attack.Rumor && attack.False ? AttackRules.BoatoFalsoBonus : 0), 0.05, 0.95)
        : 1;

    /// <summary>
    /// Previsão de uma resposta: quanto do dano sobra (em faixa), em quantas semanas o ataque se apaga, o custo e a
    /// chance. Responder bem apaga o ataque em 4 semanas; calar o deixa por 26; responder mal dá sobrevida ao assunto.
    /// </summary>
    public CommandForecast ForecastResponse(string attackId, string responseId)
    {
        CommandFailure? blocked = CanRespond(attackId, responseId);
        if (Rivals.Attacks.FirstOrDefault(a => a.Id == attackId) is not { } attack || AttackContent.Response(responseId) is not { } option)
        {
            return new CommandForecast(blocked ?? CommandFailure.NaoEncontrado, [], [], [AttackContent.Texto("resposta_explica")]);
        }

        AttackRules rules = AttackRules;
        double now = DamageNow(attack);
        double chance = ResponseChance(option, attack);
        double good = now * rules.RespondidoFator;
        double bad = now * (1 + option.Sobrevida);
        int weeksLeft = Math.Max(0, (attack.Ends.DayNumber - Today.DayNumber + 6) / 7);
        int weeksGood = Math.Min(weeksLeft, rules.SemanasRespondido);
        decimal cost = ScaledMoney(option.Custo);
        var lines = new List<ForecastLine>();
        var outcomes = new List<ForecastOutcome>();
        bool tested = option.Teste is not null || option.Advogado;
        if (tested)
        {
            lines.Add(new ForecastLine("attack_damage", attack.Id, Math.Min(good, now), Math.Max(bad, now), option.Advogado ? now : (chance * good) + ((1 - chance) * bad)));
            lines.Add(new ForecastLine("attack_weeks", attack.Id, weeksGood, weeksLeft, (chance * weeksGood) + ((1 - chance) * weeksLeft)));
            outcomes.Add(new ForecastOutcome(chance, AttackContent.Texto(option.Advogado ? "direito_concedido" : "resposta_boa")));
            outcomes.Add(new ForecastOutcome(1 - chance, AttackContent.Texto(option.Advogado ? "direito_negado" : "resposta_ruim")));
        }
        else
        {
            lines.Add(new ForecastLine("attack_damage", attack.Id, now, now, now));
            lines.Add(new ForecastLine("attack_weeks", attack.Id, weeksLeft, weeksLeft, weeksLeft));
        }

        lines.Add(new ForecastLine("money", null, -(double)cost, -(double)cost, -(double)cost));
        var explanation = new List<string>
        {
            Texts.Fill(AttackContent.Texto("resposta_explica"), ("resposta", ResponseLabel(option, attack)), ("rival", NameOf(attack.AttackerId)), ("dano", Texts.Number(now * 100, "0.0")),
                ("respondido", Texts.Number(rules.SemanasRespondido)), ("sem_resposta", Texts.Number(rules.SemanasSemResposta))),
            option.Descricao,
        };
        if (attack.Deadline is { } deadline)
        {
            explanation.Add(Texts.Fill(AttackContent.Texto("resposta_prazo"), ("prazo", Texts.Date(deadline))));
        }

        if (option.Id == AttackResponse.ContraAtaque && CounterAttack(attack) is { } counter)
        {
            CommandForecast inner = ForecastAttack(counter);
            lines.AddRange(inner.Lines.Where(l => l.Kind != "money"));
            ForecastLine innerMoney = inner.Line("money")!;
            lines[lines.FindIndex(l => l.Kind == "money")] = new ForecastLine("money", null, innerMoney.Min - (double)cost, innerMoney.Max - (double)cost, innerMoney.Expected - (double)cost);
            outcomes.AddRange(inner.Outcomes);
            explanation.AddRange(inner.Explanation);
        }

        return new CommandForecast(blocked, lines, outcomes, explanation);
    }

    /// <summary>Responde a um ataque dentro do prazo de 48 horas (GDD 17.13).</summary>
    public (CommandFailure? Failure, ResponseResult? Result) RespondToAttack(string attackId, string responseId)
    {
        if (CanRespond(attackId, responseId) is { } failure)
        {
            return (failure, null);
        }

        Attack attack = Rivals.Attacks.First(a => a.Id == attackId);
        AttackResponse option = AttackContent.Response(responseId)!;
        AttackRules rules = AttackRules;
        double before = DamageNow(attack);
        decimal cost = ScaledMoney(option.Custo);
        decimal money = -cost;
        attack.Response = option.Id;
        Pay(cost);
        bool? passed = null;
        string text;
        if (option.Advogado)
        {
            IntRange days = option.Dias!;
            attack.RulingOn = Today.AddDays(Rng.NextInt(days.Min, days.Max + 1));
            text = Texts.Fill(AttackContent.Texto("resposta_pedida"), ("data", Texts.Date(attack.RulingOn.Value)));
        }
        else if (option.Teste is not null)
        {
            passed = Rng.Chance(ResponseChance(option, attack));
            if (passed.Value)
            {
                WellAnswered(attack);
            }
            else
            {
                attack.Damage = before * (1 + option.Sobrevida);
                attack.FadeStart = Today;
            }

            text = AttackContent.Texto(passed.Value ? "resposta_boa" : "resposta_ruim");
        }
        else if (option.Id == AttackResponse.ContraAtaque && CounterAttack(attack) is { } counter)
        {
            // O contra-ataque não espera o intervalo entre ataques ao mesmo rival: é a resposta ao dele.
            Rivals.Attacks.RemoveAll(a => a.AttackerId == RaceSetup.PlayerId && a.TargetId == counter.RivalId && a.Ends < Today);
            var launched = AttackRivalNow(counter);
            money += launched?.Money ?? 0;
            text = AttackContent.Texto("resposta_contra");
        }
        else
        {
            text = AttackContent.Texto("resposta_silencio");
        }

        if (option.Id != AttackResponse.Silencio)
        {
            Emit(FactKind.Resposta, Texts.Fill(text, ("rival", NameOf(attack.AttackerId))), RaceSetup.PlayerId, target: attack.AttackerId, theme: attack.Theme, sub: option.Id,
                visibility: AttackContent.Channel(attack.Channel)?.Visibilidade ?? 1);
        }

        int weeks = Math.Max(0, (attack.Ends.DayNumber - Today.DayNumber + 6) / 7);
        return (null, new ResponseResult(attack, option.Id, passed, before, DamageNow(attack), weeks, money, text));
    }

    /// <summary>Ataque de resposta: como <see cref="AttackRival"/>, sem a regra do intervalo.</summary>
    private AttackResult? AttackRivalNow(AttackRequest r)
    {
        List<Attack> recent = Rivals.Attacks.Where(a => a.AttackerId == RaceSetup.PlayerId && a.TargetId == r.RivalId && Today.DayNumber - a.Date.DayNumber < RivalRules.AtaqueIntervaloDias).ToList();
        recent.ForEach(a => Rivals.Attacks.Remove(a));
        AttackResult? result = AttackRival(r).Result;
        Rivals.Attacks.InsertRange(0, recent);
        return result;
    }

    /// <summary>Ataques contra o jogador com o prazo de resposta correndo, com as respostas possíveis.</summary>
    public IReadOnlyList<PendingAttackView> PendingAttacks() =>
        Rivals.Attacks.Where(a => a.TargetId == RaceSetup.PlayerId && a.Response is null && a.Deadline is { } d && d >= Today)
            .Select(a => new PendingAttackView(
                a, NameOf(a.AttackerId), a.OutletId is not null && City.Outlet(a.OutletId) is { } o ? OutletLabel(o) : null, a.Deadline!.Value,
                Math.Max(0, (a.Deadline.Value.DayNumber - Today.DayNumber + 1) * 24), DamageNow(a),
                AttackContent.Respostas.Select(r => (r, ResponseLabel(r, a), CanRespond(a.Id, r.Id))).ToList()))
            .ToList();

    // ------------------------------------------------------------------ pactos

    public const string PactoNaoAgressao = "nao_agressao";
    public const string PactoDobradinha = "dobradinha";

    private static string PactKey(PactKind kind) => kind == PactKind.Dobradinha ? PactoDobradinha : PactoNaoAgressao;

    public CommandFailure? CanProposePact(string rivalId, PactKind kind)
    {
        if (!CampaignRunning)
        {
            return CommandFailure.ForaDaCampanha;
        }

        if (Campaign.Find(rivalId) is not { IsPlayer: false, IsBulk: false } rival)
        {
            return CommandFailure.NaoEncontrado;
        }

        // Dobradinha é de candidato a vereador com candidato a prefeito; não agressão é entre quem disputa o mesmo cargo.
        if (kind == PactKind.Dobradinha ? rival.Office == Campaign.Player.Office || rival.Office != Office.Prefeito : rival.Office != Campaign.Player.Office)
        {
            return CommandFailure.AlvoInvalido;
        }

        if (Rivals.Pacts.Any(p => !p.Broken && (p.RivalId == rivalId || (kind == PactKind.Dobradinha && p.Kind == PactKind.Dobradinha))))
        {
            return CommandFailure.PactoEmVigor;
        }

        return Rivals.LastProposal.TryGetValue(rivalId, out DateOnly last) && Today.DayNumber - last.DayNumber < 7 * RivalRules.PactoSemanasEntrePropostas ? CommandFailure.JaFeito : null;
    }

    private double PactChance(Candidate rival) => Math.Clamp(
        RivalRules.PactoChanceBase + (RelationOf(rival.Id) * RivalRules.PactoChancePorRelacao) + (rival.PartyId == Campaign.Player.PartyId ? RivalRules.PactoMesmoPartido : 0)
        + (City.Person(rival.Id)?.Temperament == Temperament.Combativo ? RivalRules.PactoCombativo : 0), 0.05, 0.95);

    /// <summary>Previsão da proposta de pacto: a chance de o rival aceitar e o que o pacto ganha e custa.</summary>
    public CommandForecast ForecastPact(string rivalId, PactKind kind)
    {
        CommandFailure? blocked = CanProposePact(rivalId, kind);
        if (Campaign.Find(rivalId) is not { IsPlayer: false, IsBulk: false } rival)
        {
            return new CommandForecast(blocked ?? CommandFailure.NaoEncontrado, [], [], [RivalContent.Texto("pacto_explica")]);
        }

        double chance = PactChance(rival);
        MemoryDefinition yes = RivalContent.Memory(kind == PactKind.Dobradinha ? "dobradinha" : "pacto");
        MemoryDefinition no = RivalContent.Memory("proposta_recusada");
        var lines = new List<ForecastLine> { new("relation", rival.Id, Math.Min(yes.Pontos, no.Pontos), Math.Max(yes.Pontos, no.Pontos), (chance * yes.Pontos) + ((1 - chance) * no.Pontos)) };
        if (kind == PactKind.Dobradinha)
        {
            double weekly = DobradinhaWeekly(rival);
            lines.Add(new ForecastLine("recognition", null, 0, weekly, chance * weekly));
        }

        var outcomes = new List<ForecastOutcome>
        {
            new(chance, Texts.Fill(RivalContent.Texto(kind == PactKind.Dobradinha ? "pacto_dobradinha" : "pacto_nao_agressao"), ("nome", rival.Name))),
            new(1 - chance, Texts.Fill(RivalContent.Texto("pacto_recusado"), ("nome", rival.Name))),
        };
        DecisionTrade trade = RivalContent.Pactos[PactKey(kind)];
        var explanation = new List<string>
        {
            Texts.Fill(RivalContent.Texto("pacto_explica"), ("nome", rival.Name), ("chance", Texts.Percent(chance)),
                ("zonas", string.Join(", ", (Territory.Redutos.GetValueOrDefault(rival.Id) ?? []).Select(z => ZoneName(Territory.Zones[z]))))),
            $"+ {string.Join(", ", trade.Ganho)}",
            $"− {string.Join(", ", trade.Custo)}",
        };
        return new CommandForecast(blocked, lines, outcomes, explanation);
    }

    /// <summary>Presença semanal, na cidade inteira, que a dobradinha com o candidato a prefeito rende.</summary>
    private double DobradinhaWeekly(Candidate partner) => RivalRules.DobradinhaPresenca * partner.Recognition * RecognitionScale;

    /// <summary>
    /// Propõe pacto de não agressão (a quem disputa o mesmo cargo) ou dobradinha (ao candidato a prefeito). Aceito, o
    /// rival deixa de atacar e as zonas dele ficam fechadas ao jogador; quebrá-lo grava −3 e vira notícia.
    /// </summary>
    public (CommandFailure? Failure, PactResult? Result) ProposePact(string rivalId, PactKind kind)
    {
        if (CanProposePact(rivalId, kind) is { } failure)
        {
            return (failure, null);
        }

        Candidate rival = Campaign.Find(rivalId)!;
        Person person = City.Person(rivalId)!;
        Rivals.LastProposal[rivalId] = Today;
        if (!Rng.Chance(PactChance(rival)))
        {
            MemoryDefinition no = RivalContent.Memory("proposta_recusada");
            Memory refused = person.Remember(Today, "proposta_recusada", no.Peso, no.Pontos, no.Texto, PeopleConfig);
            return (null, new PactResult(false, null, refused, Texts.Fill(RivalContent.Texto("pacto_recusado"), ("nome", rival.Name))));
        }

        var pact = new Pact
        {
            Id = $"pc{Rivals.NextId++}",
            RivalId = rivalId,
            Kind = kind,
            Since = Today,
            ClosedZones = kind == PactKind.NaoAgressao ? (Territory.Redutos.GetValueOrDefault(rivalId) ?? []).Select(z => Territory.Zones[z]).ToList() : [],
        };
        Rivals.Pacts.Add(pact);
        string key = kind == PactKind.Dobradinha ? "dobradinha" : "pacto";
        MemoryDefinition yes = RivalContent.Memory(key);
        Memory memory = person.Remember(Today, key, yes.Peso, yes.Pontos, yes.Texto, PeopleConfig);
        string text = Texts.Fill(RivalContent.Texto(kind == PactKind.Dobradinha ? "pacto_dobradinha" : "pacto_nao_agressao"), ("nome", rival.Name));
        if (kind == PactKind.Dobradinha)
        {
            State.Ledger.AddDebt(rivalId, Texts.Fill(RivalContent.Texto("pacto_divida"), ("nome", rival.Name)), Today);
        }

        if (Rivals.Record(rivalId)?.Plan is { } plan)
        {
            plan.Postures[RaceSetup.PlayerId] = Posture.Compor;
            plan.Actions.RemoveAll(a => !a.Done && a.Kind == RivalAction.Ataque && a.TargetId == RaceSetup.PlayerId);
        }

        Emit(FactKind.Pacto, text, RaceSetup.PlayerId, target: rivalId, sub: PactKey(kind), visibility: kind == PactKind.Dobradinha ? 2 : 0);
        return (null, new PactResult(true, pact, memory, text));
    }

    /// <summary>O pacto com o rival seria quebrado por este compromisso (ato de rua numa zona fechada)? Devolve o rival.</summary>
    public string? PactBrokenBy(CommitmentRequest r) =>
        r.ZoneId is not null && Commitments.Get(r.Type).Rua && !r.Delegado
            ? Rivals.Pacts.FirstOrDefault(p => !p.Broken && p.ClosedZones.Contains(r.ZoneId))?.RivalId
            : null;

    private string? BreakPact(string rivalId)
    {
        if (Rivals.PactWith(rivalId) is not { } pact)
        {
            return null;
        }

        pact.Broken = true;
        MemoryDefinition memory = RivalContent.Memory("pacto_quebrado");
        City.Person(rivalId)?.Remember(Today, "pacto_quebrado", memory.Peso, memory.Pontos, memory.Texto, PeopleConfig);
        string text = Texts.Fill(RivalContent.Texto("pacto_quebrado"), ("nome", NameOf(rivalId)));
        Emit(FactKind.Pacto, text, RaceSetup.PlayerId, target: rivalId, sub: "quebrado", visibility: 2);
        if (Rivals.Record(rivalId) is { } record)
        {
            record.PendingTrigger = RivalPlan.GatilhoAtaque;
        }

        return text;
    }

    /// <summary>Virada da semana dos pactos: a dobradinha rende presença pelo material do candidato a prefeito e cola a imagem do jogador à dele.</summary>
    private void PactsWeek()
    {
        foreach (Pact pact in Rivals.Pacts.Where(p => !p.Broken && p.Kind == PactKind.Dobradinha))
        {
            if (Campaign.Find(pact.RivalId) is not { } partner)
            {
                continue;
            }

            Candidate player = Campaign.Player;
            Territory.GainEverywhere(player.Id, DobradinhaWeekly(partner));
            player.Recognition = Territory.Recognition(player.Id);
            player.Image = Math.Clamp(player.Image + ((partner.Image - player.Image) * RivalRules.DobradinhaImagem), -1, 1);
        }
    }

    // ------------------------------------------------------------------ investigar

    public decimal InvestigationCost => ScaledMoney(RivalRules.InvestigarCusto);

    public CommandFailure? CanInvestigate(string rivalId)
    {
        if (!CampaignRunning)
        {
            return CommandFailure.ForaDaCampanha;
        }

        if (Campaign.Find(rivalId) is not { IsPlayer: false, IsBulk: false } || Rivals.Record(rivalId) is null)
        {
            return CommandFailure.NaoEncontrado;
        }

        return State.Player.Resources.Money < InvestigationCost ? CommandFailure.SemDinheiro : null;
    }

    private double InvestigationChance(RivalRecord record) => record.Weaknesses.Any(w => !w.Known) ? RivalRules.InvestigarChance : 0;

    /// <summary>Previsão de investigar: o custo, a chance de achar um ponto fraco (que vira prova) e a de o rival ficar sabendo.</summary>
    public CommandForecast ForecastInvestigation(string rivalId)
    {
        CommandFailure? blocked = CanInvestigate(rivalId);
        if (Rivals.Record(rivalId) is not { } record || Campaign.Find(rivalId) is not { } rival)
        {
            return new CommandForecast(blocked ?? CommandFailure.NaoEncontrado, [], [], [RivalContent.Texto("investigar_explica")]);
        }

        decimal cost = InvestigationCost;
        MemoryDefinition caught = RivalContent.Memory("investigado");
        var lines = new List<ForecastLine>
        {
            new("money", null, -(double)cost, -(double)cost, -(double)cost),
            new("relation", rivalId, Math.Min(0, caught.Pontos), Math.Max(0, caught.Pontos), RivalRules.InvestigarDescoberto * caught.Pontos),
        };

        // A chance mostrada é a da regra: o jogador não sabe de antemão se há o que achar.
        var outcomes = new List<ForecastOutcome>
        {
            new(RivalRules.InvestigarChance, RivalContent.Texto("investigar_achou")),
            new(RivalRules.InvestigarDescoberto, Texts.Fill(RivalContent.Texto("investigar_descoberto"), ("nome", rival.Name))),
        };
        var explanation = new List<string>
        {
            Texts.Fill(RivalContent.Texto("investigar_explica"), ("nome", rival.Name), ("custo", Texts.Money(cost)), ("chance", Texts.Percent(RivalRules.InvestigarChance)),
                ("conhecidos", Texts.Number(record.Weaknesses.Count(w => w.Known)))),
        };
        return new CommandForecast(blocked, lines, outcomes, explanation);
    }

    /// <summary>Manda levantar a vida do rival: pode revelar um ponto fraco, que serve de prova num ataque; se não acha nada, ele pode ficar sabendo.</summary>
    public (CommandFailure? Failure, InvestigationResult? Result) Investigate(string rivalId)
    {
        if (CanInvestigate(rivalId) is { } failure)
        {
            return (failure, null);
        }

        RivalRecord record = Rivals.Record(rivalId)!;
        decimal cost = InvestigationCost;
        Pay(cost);
        RivalWeakness? found = null;
        if (Rng.Chance(InvestigationChance(record)))
        {
            found = Rng.Pick(record.Weaknesses.Where(w => !w.Known).ToList());
            found.Known = true;
        }

        bool discovered = found is null && Rng.Chance(RivalRules.InvestigarDescoberto);
        if (discovered)
        {
            MemoryDefinition memory = RivalContent.Memory("investigado");
            City.Person(rivalId)?.Remember(Today, "investigado", memory.Peso, memory.Pontos, memory.Texto, PeopleConfig);
        }

        string text = found is not null ? $"{RivalContent.Texto("investigar_achou")} {found.Text}"
            : discovered ? Texts.Fill(RivalContent.Texto("investigar_descoberto"), ("nome", NameOf(rivalId))) : RivalContent.Texto("investigar_nada");
        Emit(FactKind.Investigacao, text, RaceSetup.PlayerId, target: rivalId, visibility: 0);
        return (null, new InvestigationResult(found, discovered, -cost, text));
    }

    // ------------------------------------------------------------------ consultas

    /// <summary>Listas de partido com os votos dados, como a apuração as monta (o coletivo vira candidatos anônimos).</summary>
    private List<PartyList> SlateLists(Dictionary<string, long> votes, Dictionary<string, long> legend, Dictionary<string, List<long>>? bulkVotes)
    {
        CampaignConfig config = Context.Config;
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
                    if (bulkVotes is not null)
                    {
                        bulkVotes[c.PartyId] = split;
                    }

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

        return lists;
    }

    /// <summary>
    /// Painel da chapa (GDD 17.11): pela projeção de hoje, quantas cadeiras o partido do jogador faria e em que
    /// posição da chapa ele está. Colegas de chapa são aliados e concorrentes ao mesmo tempo.
    /// </summary>
    public SlateView SlatePanel()
    {
        VoteProjection projection = Project(Office.Vereador, null);
        var votes = projection.Votes.ToDictionary(v => v.Key, v => (long)Math.Round(v.Value), StringComparer.Ordinal);
        List<PartyList> lists = SlateLists(votes, [], null);
        ProportionalResult result = ElectionRules.Proportional(Context.SeatsIn(Campaign.ElectionDate.Year), lists);
        string party = Campaign.Player.PartyId;
        int seats = result.SeatsOf(party);
        List<CandidateVotes> ranked = (lists.FirstOrDefault(l => l.PartyId == party)?.Candidates ?? [])
            .OrderByDescending(c => c.Votes).ThenBy(c => c.BirthDate).ThenBy(c => c.CandidateId, StringComparer.Ordinal).ToList();
        int rank = ranked.FindIndex(c => c.CandidateId == RaceSetup.PlayerId) + 1;
        long own = rank > 0 ? ranked[rank - 1].Votes : 0;
        long toSeat = rank > 0 && seats > 0 && rank > seats ? ranked[seats - 1].Votes - own + 1 : 0;
        var top = ranked.Take(Math.Max(seats + 2, rank)).Select((c, i) => new SlateRow(c.CandidateId, Campaign.Find(c.CandidateId)?.Name ?? State.Party(party)?.Sigla ?? party, c.Votes, i < seats, c.CandidateId == RaceSetup.PlayerId)).ToList();
        string text = Texts.Fill(RivalContent.Texto("chapa"), ("cadeiras", Texts.Number(seats)), ("posicao", Texts.Number(rank)));
        return new SlateView(party, seats, rank, ranked.Count, own, toSeat, text, top);
    }

    /// <summary>Confiança do coordenador na leitura do plano dos rivais (0 sem coordenador).</summary>
    public double RivalReadConfidence => StaffIn(StaffRole.Coordenador) is null ? 0 : Math.Clamp(1 - StaffEffect(StaffRole.Coordenador, StaffRole.ErroConselho), 0, 1);

    /// <summary>
    /// Ficha do rival: reduto, plano estimado com a confiança do coordenador (zonas-alvo e postura só aparecem com
    /// confiança bastante; sem ela, a estimativa são os redutos), lembranças, pontos fracos já descobertos e pacto.
    /// </summary>
    public RivalSheet? RivalSheetOf(string rivalId)
    {
        if (Campaign.Find(rivalId) is not { IsPlayer: false, IsBulk: false } rival || City.Person(rivalId) is not { } person)
        {
            return null;
        }

        RivalRecord? record = Rivals.Record(rivalId);
        RivalPlan? plan = record?.Plan;
        double confidence = RivalReadConfidence;
        List<string> redutos = (Territory.Redutos.GetValueOrDefault(rivalId) ?? []).Select(z => Territory.Zones[z]).ToList();
        bool zonesKnown = plan is not null && confidence >= RivalRules.ConfiancaZonas;
        bool postureKnown = plan is not null && confidence >= RivalRules.ConfiancaPostura;
        RivalProfile? profile = record is null ? null : RivalContent.Profile(record.ProfileId);
        int tomorrow = Today.AddDays(1).DayNumber - State.Calendar.WeekStart.DayNumber;
        return new RivalSheet(
            rivalId, rival.Name, postureKnown ? profile?.Id : null, postureKnown ? profile?.Nome : null, person.Temperament, person.Relation(Today, PeopleConfig), redutos, confidence,
            zonesKnown ? plan!.TargetZones.Select(z => Territory.Zones[z]).ToList() : redutos,
            postureKnown ? plan!.Postures.GetValueOrDefault(RaceSetup.PlayerId) : (Posture?)null,
            postureKnown ? plan!.Strategy : null,
            postureKnown ? Texts.Fill(RivalContent.Texto($"plano_{plan!.Strategy}"), ("nome", rival.Name)) : null,
            person.Memories,
            record?.Weaknesses.Where(w => w.Known).ToList() ?? [],
            Rivals.PactWith(rivalId),
            SeesRivalAgenda && plan is not null ? plan.Actions.Where(a => a.Day == tomorrow && a.Kind is not (RivalAction.Ataque or RivalAction.Investigar)).ToList() : []);
    }
}
