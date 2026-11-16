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

    /// <summary>
    /// Joga também os sistemas da R2b: contrata e demite, reparte verba pelos canais, assume posições e bandeiras,
    /// faz promessas, delega compromissos, trata de temas nas falas e fixa ou dispensa objetivos.
    /// </summary>
    public bool R2b { get; init; }

    /// <summary>
    /// Joga também os sistemas da R3: responde a ofertas de doação e a ataques, ataca, propõe pacto, investiga,
    /// sugere pauta, anuncia e dá entrevista em veículo; confere a previsão de cada comando.
    /// </summary>
    public bool R3 { get; init; }
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
        if (_options.R2b)
        {
            PlayR2b();
        }

        if (_options.R3)
        {
            PlayR3();
        }

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
        Engine.Agenda.Scheduled.Where(c => c.Date == day && !c.Delegado).Sum(c => Engine.Context.Campaign.Commitments.Get(c.Type).Turnos);

    /// <summary>Comandos da R2b dados nesta semana (para os testes conferirem que o robô usou os sistemas).</summary>
    public Dictionary<string, int> R2bCommands { get; } = [];

    private void Count(string command, bool done)
    {
        if (done)
        {
            R2bCommands[command] = R2bCommands.GetValueOrDefault(command) + 1;
        }
    }

    /// <summary>Uma semana de decisões da R2b, sorteadas pela política do robô (nunca pelo Rng do jogo).</summary>
    private void PlayR2b()
    {
        if (Campaign.Phase is not (CampaignPhase.PreCampanha or CampaignPhase.Campanha))
        {
            return;
        }

        CampaignContent content = Engine.Context.Campaign;

        // Equipe: contrata para funções vagas e, raramente, demite.
        foreach (StaffRole role in content.Staff.Funcoes)
        {
            List<StaffView> candidates = Engine.StaffCandidates(role.Id).Where(v => v.HireBlocked is null).ToList();
            if (candidates.Count > 0 && _policy.Chance(0.25))
            {
                StaffView pick = _policy.Pick(candidates);
                Assert.NotEmpty(Engine.ForecastHire(pick.Member.PersonId).Explanation);
                Count("contratar", Engine.Hire(pick.Member.PersonId).Failure is null);
            }
        }

        foreach (StaffMember member in Engine.HiredStaff.ToList())
        {
            if (_policy.Chance(0.02))
            {
                Assert.NotEmpty(Engine.ForecastFire(member.PersonId).Explanation);
                Count("demitir", Engine.Fire(member.PersonId) is not null);
            }
        }

        if (Engine.Team.Advice is not null && _policy.Chance(0.1))
        {
            Count("conselho_dispensado", Engine.IgnoreAdvice());
        }

        // Verba: mexe em um ou dois destinos por semana; às vezes zera um.
        for (int i = 0; i < 2; i++)
        {
            ChannelDefinition channel = _policy.Pick(content.Channels.Canais);
            IReadOnlyList<ChannelTarget> targets = Engine.ChannelTargets(channel.Id);
            string target = _policy.Pick(targets).Id;
            decimal amount = _policy.Chance(0.15) ? 0 : Engine.ChannelStep * _policy.NextInt(1, 12);
            if (Engine.BeforeOfficialCampaign && Engine.PreCampaignCap(channel.Id) is { } cap)
            {
                // Na pré-campanha só cabe o teto (sem impulsionamento, material institucional).
                amount = Math.Min(amount, cap);
            }

            if (State.Player.Resources.Money < amount * 3)
            {
                continue;
            }

            CommandForecast forecast = Engine.ForecastChannel(channel.Id, target, amount);
            Assert.NotEmpty(forecast.Explanation);
            Count($"verba_{channel.Id}", Engine.SetAllocation(channel.Id, target, amount) is null && amount > 0);
        }

        // Plataforma: posição, bandeiras e promessas.
        List<string> themes = Engine.Context.Themes.Themes.Select(t => t.Id).ToList();
        if (_policy.Chance(0.12))
        {
            string theme = _policy.Pick(themes);
            int value = _policy.NextInt(-2, 3);
            Assert.NotEmpty(Engine.ForecastPosition(theme, value).Explanation);
            Count("posicao", Engine.SetPosition(theme, value).Failure is null);
        }

        if (_policy.Chance(0.08))
        {
            Count("bandeiras", Engine.SetFlags(_policy.Shuffled(themes).Take(_policy.NextInt(1, 4)).ToList()) is null);
        }

        if (_policy.Chance(0.15))
        {
            bool toZone = _policy.Chance(0.6);
            string kind = toZone ? Planalto.Core.People.Promise.ParaZona : Planalto.Core.People.Promise.ParaGrupo;
            string to = toZone ? _policy.Pick(Engine.Territory.Zones) : _policy.Pick(content.Promises.Promessas.SelectMany(p => p.Grupos).Distinct().ToList());
            List<PromiseOption> options = Engine.PromiseOptions(kind, to).Where(o => o.Blocked is null).ToList();
            if (options.Count > 0)
            {
                PromiseOption option = _policy.Pick(options);
                Assert.NotEmpty(Engine.ForecastPromise(option.Definition.Id, kind, to).Explanation);
                Count("promessa", Engine.MakePromise(option.Definition.Id, kind, to).Failure is null);
            }
        }

        // Objetivos: fixa ou dispensa de vez em quando.
        if (Engine.Objectives.Active.Count > 0 && _policy.Chance(0.2))
        {
            Count("fixar", Engine.PinObjective(_policy.Pick(Engine.Objectives.Active).Id));
        }

        if (Engine.Objectives.Active.Count > 0 && _policy.Chance(0.08))
        {
            Count("dispensar", Engine.DismissObjective(_policy.Pick(Engine.Objectives.Active).Id));
        }

        // Delegação: o coordenador manda representante a uma caminhada.
        if (Engine.DelegationsPerWeek > 0 && _policy.Chance(0.6))
        {
            DateOnly day = Engine.Today.AddDays(_policy.NextInt(0, 7 - State.Time.Day));
            var request = new CommitmentRequest("caminhada", _policy.Pick(Engine.Territory.Zones), day, (Turno)_policy.NextInt(3), Delegado: true);
            Count("delegar", Affordable("caminhada") && Engine.Schedule(request).Failure is null);
        }
    }

    /// <summary>Uma semana de decisões da R3, sorteadas pela política do robô; cada resultado é conferido contra a previsão.</summary>
    private void PlayR3()
    {
        if (Campaign.Phase is not (CampaignPhase.PreCampanha or CampaignPhase.Campanha))
        {
            return;
        }

        foreach (DonorOffer offer in Engine.Finance.Offers.ToList())
        {
            List<DonationAnswer> answers = Enum.GetValues<DonationAnswer>().Where(a => Engine.CanAnswerDonation(offer.Id, a) is null).ToList();
            DonationAnswer answer = _policy.Pick(answers);
            CommandForecast forecast = Engine.ForecastDonation(offer.Id, answer);
            Assert.NotEmpty(forecast.Explanation);
            DonationResult result = Engine.AnswerDonation(offer.Id, answer).Result!;
            Assert.True(forecast.Contains("money", null, (double)result.Money));
            Assert.True(forecast.Contains("risk", null, result.RiskAfter - result.RiskBefore));
            Count($"doacao_{answer}", true);
        }

        foreach (PendingAttackView pending in Engine.PendingAttacks())
        {
            var options = pending.Options.Where(o => o.Blocked is null).ToList();
            if (options.Count == 0 || _policy.Chance(0.2))
            {
                // Às vezes deixa o prazo de 48 horas vencer.
                continue;
            }

            AttackResponse option = _policy.Pick(options).Response;
            CommandForecast forecast = Engine.ForecastResponse(pending.Attack.Id, option.Id);
            Assert.NotEmpty(forecast.Explanation);
            ResponseResult result = Engine.RespondToAttack(pending.Attack.Id, option.Id).Result!;
            Assert.True(forecast.Contains("attack_damage", pending.Attack.Id, result.DamageAfter), $"{option.Id}: dano {result.DamageAfter}");
            Assert.True(forecast.Contains("money", null, (double)result.Money), $"{option.Id}: dinheiro {result.Money}, previsto {forecast.Line("money")}");
            Count($"resposta_{option.Id}", true);
        }

        List<string> themeIds = Engine.Context.Themes.Themes.Select(t => t.Id).ToList();
        List<Candidate> sameOffice = Campaign.ForOffice(Campaign.Player.Office).Where(c => !c.IsPlayer && !c.IsBulk).ToList();
        List<Candidate> named = Campaign.Candidates.Where(c => !c.IsPlayer && !c.IsBulk).ToList();
        AttackCatalog attacks = Engine.Context.Campaign.Attacks;
        if (sameOffice.Count > 0 && _policy.Chance(0.06))
        {
            Candidate rival = _policy.Pick(sameOffice);
            AttackChannel channel = _policy.Pick(attacks.Canais);
            RivalWeakness? weakness = Campaign.Rivals.Record(rival.Id)?.Weaknesses.FirstOrDefault(w => w.Known);
            var request = new AttackRequest(rival.Id, weakness?.Type ?? _policy.Pick(attacks.Tipos).Id, channel.Id, weakness?.Id, _policy.Pick(themeIds), channel.ExigeVeiculo ? _policy.Pick(Engine.City.Outlets).Id : null);
            CommandForecast forecast = Engine.ForecastAttack(request);
            Assert.NotEmpty(forecast.Explanation);
            if (forecast.Blocked is null)
            {
                AttackResult result = Engine.AttackRival(request).Result!;
                Assert.True(forecast.Contains("rival_image", rival.Id, result.RivalImage));
                Assert.True(forecast.Contains("image", null, result.Image));
                Assert.True(forecast.Contains("money", null, (double)result.Money));
                Count("ataque", true);
            }
        }

        if (named.Count > 0 && _policy.Chance(0.05))
        {
            Candidate rival = _policy.Pick(named);
            PactKind kind = rival.Office == Campaign.Player.Office ? PactKind.NaoAgressao : PactKind.Dobradinha;
            CommandForecast forecast = Engine.ForecastPact(rival.Id, kind);
            Assert.NotEmpty(forecast.Explanation);
            if (forecast.Blocked is null)
            {
                PactResult result = Engine.ProposePact(rival.Id, kind).Result!;
                Assert.True(forecast.Contains("relation", rival.Id, result.Memory.Pontos));
                Count(result.Accepted ? "pacto" : "pacto_recusado", true);
            }
        }

        if (sameOffice.Count > 0 && _policy.Chance(0.05))
        {
            Candidate rival = _policy.Pick(sameOffice);
            CommandForecast forecast = Engine.ForecastInvestigation(rival.Id);
            Assert.NotEmpty(forecast.Explanation);
            if (forecast.Blocked is null)
            {
                InvestigationResult result = Engine.Investigate(rival.Id).Result!;
                Assert.True(forecast.Contains("money", null, (double)result.Money));
                Count("investigar", true);
            }
        }

        IReadOnlyList<Fact> stories = Engine.StoryCandidates();
        if (stories.Count > 0 && _policy.Chance(0.2))
        {
            Fact fact = _policy.Pick(stories);
            string outlet = _policy.Pick(Engine.City.Outlets).Id;
            CommandForecast forecast = Engine.ForecastStory(outlet, fact.Id);
            Assert.NotEmpty(forecast.Explanation);
            if (forecast.Blocked is null)
            {
                StoryResult result = Engine.SuggestStory(outlet, fact.Id).Result!;
                Assert.True(forecast.Contains(fact.ActorId == RaceSetup.PlayerId ? "image" : "rival_image", fact.ActorId == RaceSetup.PlayerId ? null : fact.ActorId, result.ImageDelta));
                Count("pauta", true);
            }
        }

        foreach (Planalto.Core.Media.Outlet outlet in Engine.City.Outlets)
        {
            if (Engine.CanAdvertise(outlet.Id) is null && _policy.Chance(0.3))
            {
                CommandForecast forecast = Engine.ForecastAd(outlet.Id);
                Assert.NotEmpty(forecast.Explanation);
                AdResult result = Engine.Advertise(outlet.Id).Result!;
                Assert.All(result.Presence, p => Assert.True(forecast.Contains("presence", p.Key, p.Value)));
                Count("anuncio", true);
            }
        }

        if (_policy.Chance(0.4))
        {
            var (date, turno) = Engine.NextFreeSlot();
            var request = new CommitmentRequest(CommitmentCatalog.Entrevista, null, date, turno, OutletId: _policy.Pick(Engine.City.Outlets).Id, Tema: _policy.Chance(0.5) ? _policy.Pick(themeIds) : null);
            if (Engine.CanSchedule(request) is null)
            {
                Assert.NotEmpty(Engine.Forecast(request).Factors);
                Count("entrevista_veiculo", Engine.Schedule(request).Failure is null);
            }
        }
    }

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

        // Na R2b o candidato trata de um tema nas falas (entrevista, gravação, comício).
        string? theme = _options.R2b && Engine.SpeaksOfThemes(typeId) ? _policy.Pick(Engine.Context.Themes.Themes).Id : null;
        if (!type.ExigeZona)
        {
            return new CommitmentRequest(typeId, null, day, turno, Tema: theme);
        }

        if (typeId == CommitmentCatalog.Comicio)
        {
            // Comício onde o candidato já é mais conhecido (senão esvazia).
            string best = zones.MaxBy(z => Engine.Territory.PresenceOf(RaceSetup.PlayerId, Engine.Territory.IndexOf(z)))!;

            // O comício exige verba de Rua na zona (GDD 17.8): o robô a põe no plano semanal quando o canal está aberto.
            if (Engine.Budget.PlanOf(ChannelDefinition.Rua, best) < Engine.ComicioRuaMinimum
                && Engine.SetAllocation(ChannelDefinition.Rua, best, Engine.ComicioRuaMinimum) is not null)
            {
                return null;
            }

            return new CommitmentRequest(typeId, best, day, turno, Tema: theme);
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

        // Eventos da equipe: uma opção sorteada, sempre com a previsão explicada.
        while (Engine.Team.Pending.Count > 0)
        {
            StaffDecision decision = Engine.Team.Pending[0];
            StaffEventOption option = _policy.Pick(Engine.Context.Campaign.Staff.Event(decision.EventId).Opcoes);
            Assert.NotEmpty(Engine.ForecastStaffDecision(decision.Id, option.Id).Explanation);
            StaffDecisionResult result = Engine.AnswerStaffDecision(decision.Id, option.Id);
            Assert.DoesNotContain("{", result.Text);
            Count($"equipe_{decision.EventId}", true);
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
