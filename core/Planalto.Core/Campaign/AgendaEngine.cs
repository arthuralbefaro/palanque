using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.Electorate;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.Core.World;

namespace Planalto.Core.Campaign;

/// <summary>
/// Agenda com lugar e dia (GDD 17.6): marcar, prever e executar compromissos. O rendimento é
/// base × alcance da zona × liderança junto × ocasião × 1 ÷ (1 + fator × cansaço) × sorte, e a previsão mostra cada
/// fator antes de marcar (princípios f e g).
/// </summary>
public sealed partial class CampaignEngine
{
    private CommitmentCatalog Commitments => Context.Campaign.Commitments;

    private AgendaRules Rules => Commitments.Regras;

    public AgendaState Agenda => Campaign.Agenda;

    public TerritoryState Territory => Campaign.Territory;

    /// <summary>Custo do compromisso em reais (negativo = arrecada), no porte da cidade.</summary>
    public decimal CostOf(CommitmentType type)
    {
        decimal money = Math.Round(type.Dinheiro * (decimal)CostScale / 100) * 100;
        if (type.Arrecada is { } per)
        {
            money -= per.Amount * State.Player.Attributes.Get(per.Attribute) * (decimal)CostScale;
        }

        return money;
    }

    /// <summary>Texto de um fator do rendimento, para a previsão e para a explicação.</summary>
    private static YieldFactor Factor(string id, string text, double multiplier) => new(id, text, multiplier);

    /// <summary>
    /// Alcance da zona: quantas vezes a cidade é maior que ela, em eleitores. As bases de presença dos compromissos são
    /// medidas em "pontos na cidade inteira"; um turno gasto numa zona que é 1/10 da cidade rende 10 vezes isso em
    /// presença ali. Zona pequena rende mais presença por turno e vale menos votos; zona grande, o contrário
    /// (GDD 17.5, "a troca").
    /// </summary>
    public double Reach(int zone)
    {
        IReadOnlyList<ZoneElectorate> zones = Context.ZoneElectorates;
        double total = zones.Sum(z => z.Voters);
        double own = zones[zone].Voters;
        return Math.Clamp(own <= 0 ? Rules.Alcance.Max : total / own, Rules.Alcance.Min, Rules.Alcance.Max);
    }

    /// <summary>Cansaço do trio na data do compromisso: o de hoje mais os iguais já marcados antes dele.</summary>
    private double FatigueAt(CommitmentRequest r)
    {
        string key = KeyOf(r.Type, r.ZoneId, r.Publico, r.PersonId);
        double now = Agenda.Fatigue.GetValueOrDefault(key);
        int before = Agenda.Scheduled.Count(c => KeyOf(c.Type, c.ZoneId, c.Publico, c.PersonId) == key
            && (c.Date < r.Date || (c.Date == r.Date && c.Turno < r.Turno)));
        return now + before;
    }

    /// <summary>
    /// Chave do cansaço (princípio g). Ir a um evento da cidade é estar na rua naquela zona: divide o cansaço com a
    /// caminhada ali. A visita cansa por pessoa visitada.
    /// </summary>
    private string KeyOf(string typeId, string? zoneId, string publico, string? personId)
    {
        CommitmentType type = Commitments.Get(typeId);
        return AgendaState.FatigueKey(type.SoEvento ? "caminhada" : typeId, zoneId, type.SoEvento ? CommitmentType.PublicoGeral : publico, type.Visita ? personId : null);
    }

    private IEnumerable<Turno> TurnsOf(CommitmentType type, Turno start) =>
        Enumerable.Range((int)start, type.Turnos).Select(t => (Turno)t);

    public ScheduleFailure? CanSchedule(CommitmentRequest r)
    {
        CommitmentType type = Commitments.Get(r.Type);
        if (Agenda.PendingRequest is not null || Campaign.Events.Pending.Count > 0 || Team.Pending.Count > 0)
        {
            return ScheduleFailure.DecisaoPendente;
        }

        if (!type.AllowedIn(Campaign.Phase) || (r.Date >= Campaign.CampaignStart ? !type.Fases.Contains(CampaignActionDefinition.Campanha) : !type.Fases.Contains(CampaignActionDefinition.PreCampanha)))
        {
            return ScheduleFailure.FaseNaoPermite;
        }

        if (r.Date < Today || r.Date > Today.AddDays(Rules.AntecedenciaDias) || r.Date >= Campaign.ElectionDate)
        {
            return ScheduleFailure.ForaDoPrazo;
        }

        // Relógio do dia: o turno de hoje que já terminou não volta (o que está em curso ainda aceita).
        if (TurnoPassed(r.Date, r.Turno))
        {
            return ScheduleFailure.TurnoPassou;
        }

        if (r.OutletId is not null && (type.Id != CommitmentCatalog.Entrevista || City.Outlet(r.OutletId) is null || r.Delegado))
        {
            return ScheduleFailure.VeiculoInvalido;
        }

        // Tema tratado: só nos compromissos em que o candidato fala (entrevista, gravação, comício), e em pessoa.
        if (r.Tema is not null && (r.Delegado || ThemeGainBase(r.Type, r.Tema) <= 0))
        {
            return ScheduleFailure.TemaInvalido;
        }

        if (r.Delegado)
        {
            // Quem vai é um representante: não ocupa o turno do candidato, mas gasta uma delegação do coordenador.
            if (!type.Delegavel)
            {
                return ScheduleFailure.NaoDelegavel;
            }

            if (StaffIn(StaffRole.Coordenador) is null)
            {
                return ScheduleFailure.SemCoordenador;
            }

            if ((int)r.Turno + type.Turnos > 3 || DelegationsIn(r.Date) >= DelegationsPerWeek)
            {
                return ScheduleFailure.SemDelegacao;
            }
        }

        // Turno já usado hoje (o compromisso aconteceu e saiu da lista de marcados) continua ocupado.
        else if ((int)r.Turno + type.Turnos > 3 || TurnsOf(type, r.Turno).Any(t => Agenda.At(r.Date, t) is not null)
            || Agenda.Scheduled.Any(c => !c.Delegado && c.Date == r.Date && TurnsOf(Commitments.Get(c.Type), c.Turno).Intersect(TurnsOf(type, r.Turno)).Any())
            || Agenda.Log.Any(x => !x.Commitment.Delegado && x.Commitment.Date == r.Date && TurnsOf(Commitments.Get(x.Commitment.Type), x.Commitment.Turno).Intersect(TurnsOf(type, r.Turno)).Any()))
        {
            return ScheduleFailure.TurnoOcupado;
        }

        int zone = r.ZoneId is null ? -1 : Territory.IndexOf(r.ZoneId);
        if (type.ExigeZona && zone < 0)
        {
            return ScheduleFailure.ZonaInvalida;
        }

        if (type.SoEvento)
        {
            CityEventInstance? e = Agenda.CityEvents.FirstOrDefault(x => x.Id == r.CityEventId);
            if (e is null || e.Date != r.Date || e.Turno != r.Turno || e.ZoneId != r.ZoneId)
            {
                return ScheduleFailure.EventoInvalido;
            }
        }

        if (type.EscolhePublico && (r.Publico == CommitmentType.PublicoGeral || Context.ZoneElectorates[zone].VotersIn(r.Publico) <= 0))
        {
            return ScheduleFailure.PublicoInvalido;
        }

        if (type.Visita && (r.PersonId is null || City.Leader(r.PersonId) is not { Known: true }))
        {
            return ScheduleFailure.SemLideranca;
        }

        // Reunião em casa de apoiador (GDD 17.6): exige uma liderança conhecida e não hostil daquele público na zona.
        if (type.Id == CommitmentCatalog.Reuniao
            && !City.Leaders.Any(l => l.Known && l.Publico == r.Publico && l.Zones.Contains(r.ZoneId!) && RelationOf(l.PersonId) >= 0))
        {
            return ScheduleFailure.SemApoiadorNaZona;
        }

        // O comício depende do canal Rua (GDD 17.8): sem verba de Rua na zona não há estrutura para montar o palanque.
        if (type.Id == CommitmentCatalog.Comicio && RuaBudgetOn(r.Date, r.ZoneId!) < ComicioRuaMinimum)
        {
            return ScheduleFailure.SemVerbaDeRua;
        }

        decimal cost = CostOf(type);
        return cost > 0 && State.Player.Resources.Money < cost ? ScheduleFailure.SemDinheiro : null;
    }

    /// <summary>Parte certa do rendimento: os fatores (sem a sorte) e as bases já no porte da cidade.</summary>
    /// <param name="hot">Tema em alta já travado quando o compromisso foi marcado; null = conferir agora.</param>
    private (List<YieldFactor> Factors, double Yield) Plan(CommitmentRequest r, bool? hot = null)
    {
        CommitmentType type = Commitments.Get(r.Type);
        var factors = new List<YieldFactor>();
        InterviewFactors(r, factors);

        // Ocasião (GDD 17.6 e 17.13): tratar do tema que está em alta no noticiário rende mais.
        if (r.Tema is not null && (hot ?? HotThemeOn(r.Tema, r.ZoneId, r.Date) is not null))
        {
            factors.Add(Factor("tema_em_alta", Texts.Fill(OutletContent.Texto("fator_tema_em_alta"), ("tema", ThemeName(r.Tema))), 1 + MediaRules.TemaEmAltaBonus));
        }

        int zone = r.ZoneId is null ? -1 : Territory.IndexOf(r.ZoneId);
        if (type.ExigeZona && zone >= 0 && (type.Presenca > 0 || type.Apoio > 0))
        {
            factors.Add(Factor("alcance", "Tamanho da zona", Reach(zone)));
        }

        if (type.Rua && r.PersonId is { } companion && City.Leader(companion) is { AlignedWith: RaceSetup.PlayerId } leader && leader.Zones.Contains(r.ZoneId ?? ""))
        {
            factors.Add(Factor("lideranca", $"{NameOf(companion)} foi junto", 1 + Rules.LiderancaBonus));
        }

        if (r.CityEventId is not null && Agenda.CityEvents.FirstOrDefault(e => e.Id == r.CityEventId) is { } cityEvent)
        {
            factors.Add(Factor("evento", cityEvent.Nome, 1 + Rules.EventoBonus));
        }

        if (type.EquipeFuncao is { } roleId && StaffIn(roleId) is { } staff)
        {
            factors.Add(Factor("equipe", $"{NameOf(staff.PersonId)} ({StaffContent.Role(roleId).Papel})", StaffEffect(roleId, type.EquipeEfeito!)));
        }

        if (r.Delegado)
        {
            factors.Add(Factor("delegado", StaffContent.Texto("delegado"), StaffRules.DelegacaoRendimento));
        }

        if (zone >= 0 && ChannelRules.RuaMultiplica.Contains(type.Id))
        {
            decimal rua = RuaBudgetOn(r.Date, r.ZoneId!);
            if (rua > 0)
            {
                factors.Add(Factor("verba_rua", Channels.Texto("fator_verba_rua"), 1 + (ChannelRules.RuaBonusAgenda * Math.Min(1, Math.Sqrt((double)(rua / ChannelScale))))));
            }

            if (type.Id == CommitmentCatalog.Comicio && rua < ComicioRuaMinimum)
            {
                factors.Add(Factor("sem_estrutura", Channels.Texto("sem_estrutura"), Rules.ComicioVazioFator));
            }
        }

        if (type.Rua && Agenda.Rain.TryGetValue(r.Date, out double rain) && rain > 0)
        {
            factors.Add(Factor("chuva", "Chuva", 1 - rain));
        }

        double fatigue = FatigueAt(r);
        if (fatigue > 0 && type.Id != CommitmentCatalog.Descanso)
        {
            int ordinal = (int)Math.Round(fatigue) + 1;
            string where = r.ZoneId is null ? "" : $" em {ZoneName(r.ZoneId)}";
            factors.Add(Factor("cansaco", $"{ordinal}º compromisso igual{where} em pouco tempo", 1 / (1 + (Rules.CansacoFator * fatigue))));
        }

        if (!r.Delegado && type.ExigeZona && zone >= 0 && Rules.PortesComDeslocamento.Contains(Context.Municipio.Porte))
        {
            Zona here = Context.Zonas.Zonas[zone];
            // Conta também o que já aconteceu hoje: na execução o compromisso anterior já saiu da lista de marcados.
            bool far = Agenda.Scheduled.Concat(Agenda.Log.Select(x => x.Commitment))
                .Any(c => !c.Delegado && c.Date == r.Date && c.Turno < r.Turno && c.ZoneId is not null && c.ZoneId != r.ZoneId && !here.Vizinhas.Contains(c.ZoneId));
            if (far)
            {
                factors.Add(Factor("deslocamento", "Deslocamento entre zonas distantes no mesmo dia", 1 - Rules.DeslocamentoPerda));
            }
        }

        if (type.Id == CommitmentCatalog.Comicio && zone >= 0 && Territory.PresenceOf(RaceSetup.PlayerId, zone) < Rules.ComicioPresencaMinima)
        {
            factors.Add(Factor("comicio_vazio", "Você é pouco conhecido na zona: o comício esvazia", Rules.ComicioVazioFator));
        }

        return (factors, factors.Aggregate(1.0, (acc, f) => acc * f.Multiplier));
    }

    /// <summary>
    /// Estresse de um compromisso pessoal: o do tipo mais o dos turnos além dos que não custam no dia. Na previsão
    /// (<paramref name="running"/> falso) conta o que ele acrescenta ao dia, com o que já foi feito e o que está
    /// marcado; na execução, só os turnos que vieram antes dele no dia, para o total do dia ser o mesmo em qualquer
    /// ordem de execução (o dia de uma vez ou turno a turno pelo relógio).
    /// </summary>
    private int StressOf(CommitmentType type, CommitmentRequest r, bool running = false)
    {
        if (r.Delegado)
        {
            return 0;
        }

        int Turns(Commitment c) => Commitments.Get(c.Type).Turnos;
        IEnumerable<Commitment> done = Agenda.Log.Select(x => x.Commitment).Where(c => c.Date == r.Date && !c.Delegado);
        IEnumerable<Commitment> marked = Agenda.Scheduled.Where(c => c.Date == r.Date && !c.Delegado);
        int sameDay = running
            ? done.Sum(Turns) + marked.Where(c => c.Turno < r.Turno).Sum(Turns)
            : done.Sum(Turns) + marked.Sum(Turns);
        int extra = Math.Max(0, sameDay + type.Turnos - Rules.TurnosSemCusto) - Math.Max(0, sameDay - Rules.TurnosSemCusto);
        return type.Estresse + (extra * Rules.EstresseTurnoExtra);
    }

    private double TestChance(ActionTest test, CommitmentRequest r)
    {
        double chance = (State.Player.Attributes.Get(test.Attribute) / 100.0) + InterviewChanceShift(r);
        if (State.Player.Resources.Stress > Rules.EstresseLimite)
        {
            chance -= Rules.EstressePenalidade;
        }

        return Math.Clamp(chance, 0.05, 0.95);
    }

    /// <summary>Público que de fato recebe o apoio: o do evento, o escolhido ou o fixo do tipo (gravação).</summary>
    private string? SupportGroup(CommitmentType type, CommitmentRequest r)
    {
        if (type.Apoio <= 0)
        {
            return null;
        }

        if (r.CityEventId is not null && Agenda.CityEvents.FirstOrDefault(e => e.Id == r.CityEventId) is { } e)
        {
            return e.Publico == CommitmentType.PublicoGeral ? null : e.Publico;
        }

        return type.PublicoFixo ?? (r.Publico == CommitmentType.PublicoGeral ? null : r.Publico);
    }

    /// <summary>
    /// Retorno decrescente do apoio conquistado em conversa: quanto mais apoio o jogador já tem daquele público (na
    /// zona, ou na cidade quando o compromisso não tem zona), menos a próxima conversa acrescenta, até o teto.
    /// </summary>
    private double SupportHeadroom(int zone, string group)
    {
        double current = zone >= 0 ? Territory.MeetingSupport[zone].GetValueOrDefault(group) : Campaign.Player.Support.GetValueOrDefault(group);
        return Math.Clamp(1 - (current / Rules.ApoioTeto), 0, 1);
    }

    /// <summary>Previsão antes de marcar (princípio f): efeito esperado, faixa pela sorte e pelos desfechos, e os fatores.</summary>
    public CommitmentForecast Forecast(CommitmentRequest r)
    {
        CommitmentType type = Commitments.Get(r.Type);
        ScheduleFailure? blocked = CanSchedule(r);
        var (factors, yield) = Plan(r);
        int zone = r.ZoneId is null ? -1 : Territory.IndexOf(r.ZoneId);
        double current = zone >= 0 ? Territory.PresenceOf(RaceSetup.PlayerId, zone) : Campaign.Player.Recognition;
        double baseAmount = (zone >= 0 ? type.Presenca : type.PresencaCidade) * RecognitionScale * (zone >= 0 ? PresenceYield(factors, r) : YieldWithout(factors, "alcance"));
        double Delta(double luck) => EffectApplier.Gain(current, baseAmount * luck) - current;
        double support = SupportGroup(type, r) is { } supportGroup ? type.Apoio * yield * SupportHeadroom(zone, supportGroup) : 0;
        _ = yield;

        var luck = LuckRange(Rules.Sorte);
        double imageMin = type.Imagem * yield * luck.Min;
        double imageMax = type.Imagem * yield * luck.Max;
        var outcomes = new List<ForecastOutcome>();
        if (type.Teste is { } test)
        {
            double chance = TestChance(test, r);
            outcomes.Add(new ForecastOutcome(chance, test.SuccessText));
            outcomes.Add(new ForecastOutcome(1 - chance, test.FailureText));
            imageMin += Math.Min(test.Success.Image, test.Failure.Image);
            imageMax += Math.Max(test.Success.Image, test.Failure.Image);
        }

        if (type.RiscoViral)
        {
            outcomes.Add(new ForecastOutcome(StaffEffect(StaffRole.SocialMedia, StaffRole.Viral), StaffContent.Texto("viral")));
            imageMin += Math.Min(0, StaffRules.ViralImagem);
            imageMax += Math.Max(0, StaffRules.ViralImagem);
        }

        if (type.Id == "caminhada" && !r.Delegado && zone >= 0 && City.Leaders.Any(l => !l.Known && l.Zones.Contains(r.ZoneId!)))
        {
            outcomes.Add(new ForecastOutcome(LeaderRules.ChanceDescoberta, "Você pode conhecer uma liderança da zona."));
        }

        double fatigueFactor = factors.FirstOrDefault(f => f.Id == "cansaco")?.Multiplier ?? 1;
        decimal money = MoneyOf(type, fatigueFactor);
        int relation = type.Visita ? (r.Delegado ? StaffRules.DelegacaoPontosVisita : VisitPoints(fatigueFactor))
            : r.InviteId is not null ? (r.Delegado ? StaffRules.DelegacaoPontosVisita : LeaderRules.PontosConviteAceito) : 0;
        return new CommitmentForecast(
            blocked,
            Delta(luck.Min), Delta(luck.Max), Delta(1),
            support * luck.Min, support * luck.Max,
            imageMin, imageMax,
            money, money,
            StressOf(type, r),
            relation,
            fatigueFactor,
            factors,
            outcomes)
        {
            ThemeKnownGain = r.Tema is null ? 0 : ThemeGain(r.Type, r.Tema),
        };
    }

    /// <summary>
    /// Rendimento que vale para a presença. O bônus do evento da cidade é "com o público dele" (GDD 17.6): num evento
    /// de público geral ele vale para a presença na zona; num evento de um grupo, só para o apoio desse grupo.
    /// </summary>
    private double PresenceYield(List<YieldFactor> factors, CommitmentRequest r)
    {
        bool groupEvent = r.CityEventId is not null
            && Agenda.CityEvents.FirstOrDefault(e => e.Id == r.CityEventId) is { } e && e.Publico != CommitmentType.PublicoGeral;
        double yield = factors.Where(f => f.Id != "sorte" && !(groupEvent && f.Id == "evento")).Aggregate(1.0, (acc, f) => acc * f.Multiplier);
        if (groupEvent && r.ZoneId is not null && Territory.IndexOf(r.ZoneId) is var z and >= 0)
        {
            // Quem está no evento é o público dele: a presença na zona cresce na proporção desse grupo entre os eleitores dali.
            ZoneElectorate zone = Context.ZoneElectorates[z];
            string publico = Agenda.CityEvents.First(e => e.Id == r.CityEventId).Publico;
            yield *= zone.Voters <= 0 ? 0 : Math.Clamp(zone.VotersIn(publico) / zone.Voters, 0, 1);
        }

        return yield;
    }

    /// <summary>Dinheiro do compromisso: o custo sai inteiro; o que se arrecada cai com o cansaço (jantar repetido rende menos).</summary>
    private decimal MoneyOf(CommitmentType type, double fatigueFactor)
    {
        decimal money = -CostOf(type);
        return money > 0 ? Math.Round(money * (decimal)fatigueFactor) : money;
    }

    /// <summary>Pontos de relação de uma visita: a visita repetida à mesma pessoa em pouco tempo rende menos (princípio g).</summary>
    private int VisitPoints(double fatigueFactor) => (int)Math.Round(LeaderRules.PontosVisita * fatigueFactor);

    private static double YieldWithout(List<YieldFactor> factors, string id) =>
        factors.Where(f => f.Id != id).Aggregate(1.0, (acc, f) => acc * f.Multiplier);

    /// <summary>Marca o compromisso; o custo sai quando ele acontece.</summary>
    public (ScheduleFailure? Failure, Commitment? Commitment) Schedule(CommitmentRequest r)
    {
        if (CanSchedule(r) is { } failure)
        {
            return (failure, null);
        }

        var commitment = new Commitment
        {
            Id = $"c{Agenda.NextId++}",
            Type = r.Type,
            ZoneId = r.ZoneId,
            Date = r.Date,
            Turno = r.Turno,
            Publico = r.Publico,
            PersonId = r.PersonId,
            CityEventId = r.CityEventId,
            InviteId = r.InviteId,
            Delegado = r.Delegado,
            Tema = r.Tema,
            OutletId = r.OutletId,
            TemaEmAlta = r.Tema is not null && HotThemeOn(r.Tema, r.ZoneId, r.Date) is not null,
        };
        Agenda.Scheduled.Add(commitment);
        if (r.InviteId is not null && Agenda.Invites.FirstOrDefault(i => i.Id == r.InviteId) is { } invite)
        {
            invite.Answered = true;
        }

        NotifyChanged();
        return (null, commitment);
    }

    /// <summary>Aceita um convite: marca o compromisso no dia e turno dele (pode falhar se o turno estiver ocupado).</summary>
    public (ScheduleFailure? Failure, Commitment? Commitment) AcceptInvite(string inviteId)
    {
        Invite invite = Agenda.Invites.First(i => i.Id == inviteId);
        return Schedule(new CommitmentRequest("caminhada", invite.ZoneId, invite.Date, invite.Turno, CommitmentType.PublicoGeral, invite.FromPersonId, InviteId: invite.Id));
    }

    public bool Cancel(string commitmentId)
    {
        Commitment? c = Agenda.Scheduled.FirstOrDefault(x => x.Id == commitmentId);
        if (c is null)
        {
            return false;
        }

        Agenda.Scheduled.Remove(c);
        if (c.InviteId is not null && Agenda.Invites.FirstOrDefault(i => i.Id == c.InviteId) is { } invite)
        {
            // Desmarcar o que veio de convite é faltar ao convite.
            invite.Answered = false;
            DeclineInvite(invite.Id);
        }

        NotifyChanged();
        return true;
    }

    /// <summary>Executa os compromissos de hoje, na ordem dos turnos, e devolve os resultados explicados.</summary>
    public List<CommitmentResult> RunToday()
    {
        List<CommitmentResult> results = RunUntil(Today);
        if (results.Count > 0)
        {
            NotifyChanged();
        }

        return results;
    }

    private List<CommitmentResult> RunUntil(DateOnly lastDay)
    {
        var results = new List<CommitmentResult>();
        foreach (Commitment c in Agenda.Scheduled.Where(c => c.Date <= lastDay).OrderBy(c => c.Date).ThenBy(c => c.Turno).ToList())
        {
            Agenda.Scheduled.Remove(c);
            if (Agenda.PendingRequest is not null)
            {
                // Uma decisão ficou esperando: o resto do dia não acontece sem o jogador (volta para a agenda).
                Agenda.Scheduled.Add(c);
                continue;
            }

            // Cada compromisso acontece na data dele (a virada da semana executa os que restam): lembranças e feed
            // gravam esse dia.
            State.Time.Day = Math.Clamp(c.Date.DayNumber - State.Calendar.WeekStart.DayNumber, State.Time.Day, 6);
            results.Add(Run(c));
        }

        return results;
    }

    private CommitmentResult Run(Commitment c)
    {
        CommitmentType type = Commitments.Get(c.Type);
        CommitmentRequest request = RequestOf(c);
        var (factors, yield) = Plan(request, c.TemaEmAlta);
        int stress = StressOf(type, request, running: true);
        var luckRange = LuckRange(Rules.Sorte);
        double luck = Rng.NextDouble(luckRange.Min, luckRange.Max);
        factors.Add(Factor("sorte", "Sorte do dia", luck));
        int zone = c.ZoneId is null ? -1 : Territory.IndexOf(c.ZoneId);
        var presence = new Dictionary<string, double>(StringComparer.Ordinal);
        TerritorySimulation sim = Context.Campaign.Territory.Simulacao;

        if (zone >= 0 && type.Presenca > 0)
        {
            List<int> neighbors = Context.Zonas.Zonas[zone].Vizinhas.Select(Territory.IndexOf).Where(i => i >= 0).ToList();
            foreach (var (z, delta) in Territory.Gain(RaceSetup.PlayerId, zone, type.Presenca * RecognitionScale * PresenceYield(factors, request) * luck, sim.Transbordo, neighbors))
            {
                presence[Territory.Zones[z]] = delta;
            }
        }
        else if (type.PresencaCidade > 0)
        {
            double amount = type.PresencaCidade * RecognitionScale * YieldWithout(factors, "alcance");
            double[] before = [.. Territory.Presence[RaceSetup.PlayerId]];
            Territory.GainEverywhere(RaceSetup.PlayerId, amount);
            for (int z = 0; z < before.Length; z++)
            {
                presence[Territory.Zones[z]] = Territory.Presence[RaceSetup.PlayerId][z] - before[z];
            }
        }

        if (zone >= 0)
        {
            Territory.MarkActive(RaceSetup.PlayerId, zone);
            if (!c.Delegado)
            {
                Agenda.LastVisit[c.ZoneId!] = c.Date;
            }
        }

        double support = 0;
        if (SupportGroup(type, request) is { } group)
        {
            support = type.Apoio * yield * luck * SupportHeadroom(zone, group);
            if (zone >= 0)
            {
                Dictionary<string, double> meeting = Territory.MeetingSupport[zone];
                meeting[group] = Math.Min(1, meeting.GetValueOrDefault(group) + support);
            }
            else
            {
                Campaign.Player.Support[group] = Math.Clamp(Campaign.Player.Support.GetValueOrDefault(group) + support, 0, 1);
            }
        }

        double image = type.Imagem * yield * luck;
        string text = "";
        if (type.Teste is { } test)
        {
            bool passed = Rng.Chance(TestChance(test, request));
            image += passed ? test.Success.Image : test.Failure.Image;
            text = passed ? test.SuccessText : test.FailureText;
        }

        if (type.RiscoViral && Rng.Chance(StaffEffect(StaffRole.SocialMedia, StaffRole.Viral)))
        {
            image += StaffRules.ViralImagem;
            text = StaffContent.Texto("viral");
        }

        double themeKnown = c.Tema is null ? 0 : SpeakOf(c.Tema, type, c.ZoneId);
        decimal money = MoneyOf(type, factors.FirstOrDefault(f => f.Id == "cansaco")?.Multiplier ?? 1);
        Character player = State.Player;
        player.Resources.Money += money;
        Campaign.Player.Money = player.Resources.Money;
        RegisterSpending(-money);
        RegisterReceipt(FinanceCatalog.Jantar, money);
        player.Resources.Stress = Math.Max(0, player.Resources.Stress + stress);
        Campaign.Player.Image = Math.Clamp(Campaign.Player.Image + image, -1, 1);
        Campaign.Player.Recognition = Territory.Recognition(RaceSetup.PlayerId);
        player.Resources.SetRecognition(Campaign.Municipio.ToString(System.Globalization.CultureInfo.InvariantCulture), (int)Math.Round(Campaign.Player.Recognition));

        double visitFatigue = factors.FirstOrDefault(f => f.Id == "cansaco")?.Multiplier ?? 1;
        string key = KeyOf(c.Type, c.ZoneId, c.Publico, c.PersonId);
        if (type.Id != CommitmentCatalog.Descanso)
        {
            Agenda.Fatigue[key] = Agenda.Fatigue.GetValueOrDefault(key) + 1;
        }

        int relation = 0;
        if (c.Delegado && c.PersonId is not null && City.Leader(c.PersonId) is { } received && (type.Visita || c.InviteId is not null))
        {
            // Quem recebeu lembra que o candidato não foi (GDD 17.6): rende menos relação e o pedido não é ouvido.
            MemoryDefinition delegated = StaffContent.Memory("visita_delegada");
            relation = StaffRules.DelegacaoPontosVisita;
            RememberLeader(received, "visita_delegada", delegated.Peso, relation, delegated.Texto, contact: false);
            Realign(received);
        }
        else if (c.Delegado)
        {
            // Representante em compromisso de rua: nada a registrar com a liderança que acompanha.
        }
        else if (type.Visita && c.PersonId is not null && City.Leader(c.PersonId) is { } visited)
        {
            relation = VisitPoints(visitFatigue);
            RememberLeader(visited, "visita", 1, relation);
            visited.RequestKnown = true;
            if (!visited.RequestMet)
            {
                Agenda.PendingRequest = visited.PersonId;
            }

            Realign(visited);
        }
        else if (c.PersonId is not null && City.Leader(c.PersonId) is { } companion)
        {
            City.Person(companion.PersonId)!.LastContact = Today;
            FulfilPresence(companion);
            if (c.InviteId is not null)
            {
                relation = LeaderRules.PontosConviteAceito;
                RememberLeader(companion, "convite_aceito", 2, relation);
                Realign(companion);
            }
        }

        // Caminhar pela zona pode revelar uma liderança que o jogador ainda não conhecia.
        if (type.Id == "caminhada" && zone >= 0 && !c.Delegado)
        {
            List<Leader> unknown = City.Leaders.Where(l => !l.Known && l.Zones.Contains(c.ZoneId!)).ToList();
            if (unknown.Count > 0 && Rng.Chance(LeaderRules.ChanceDescoberta))
            {
                Leader found = Rng.Pick(unknown);
                found.Known = true;
                EmitFeed(c.Date, "descoberta", LeaderText("descoberta", found), c.ZoneId, found.PersonId);
            }
        }

        RefreshSupport();
        var result = new CommitmentResult
        {
            Commitment = c,
            Presence = presence,
            Support = support,
            Image = image,
            Money = money,
            Stress = stress,
            RelationPoints = relation,
            Factors = factors,
            Text = text,
            ThemeKnown = themeKnown,
        };
        Agenda.Log.Add(result);
        if (Agenda.Log.Count > AgendaState.MaxLog)
        {
            Agenda.Log.RemoveRange(0, Agenda.Log.Count - AgendaState.MaxLog);
        }

        Fact fact = Emit(FactKind.Compromisso, type.Nome, RaceSetup.PlayerId, c.ZoneId, c.PersonId, c.Tema, sub: type.Id, date: c.Date);
        AfterInterview(c, fact);
        if (PactBrokenBy(request) is { } brokenWith)
        {
            // Ato de rua numa zona que o pacto fechou: o pacto está quebrado.
            BreakPact(brokenWith);
        }

        return result;
    }

    /// <summary>Virada da semana para a agenda: cansaço cai, e a semana que entra ganha chuva prevista e eventos da cidade.</summary>
    private void AgendaWeek(DateOnly nextWeekStart)
    {
        foreach (string key in Agenda.Fatigue.Keys.ToList())
        {
            double value = Agenda.Fatigue[key] - Rules.CansacoDecaimentoSemanal;
            if (value <= 0)
            {
                Agenda.Fatigue.Remove(key);
            }
            else
            {
                Agenda.Fatigue[key] = value;
            }
        }

        foreach (DateOnly old in Agenda.Rain.Keys.Where(d => d < nextWeekStart).ToList())
        {
            Agenda.Rain.Remove(old);
        }

        Agenda.CityEvents.RemoveAll(e => e.Date < nextWeekStart);
        PlanWeek(nextWeekStart);
    }

    /// <summary>Sorteia a chuva e os eventos da cidade de uma semana (se ainda não foram sorteados).</summary>
    public void PlanWeek(DateOnly weekStart)
    {
        if (Agenda.Rain.Keys.Any(d => d >= weekStart && d < weekStart.AddDays(7)) || Agenda.CityEvents.Any(e => e.Date >= weekStart && e.Date < weekStart.AddDays(7)))
        {
            return;
        }

        for (int d = 0; d < 7; d++)
        {
            Agenda.Rain[weekStart.AddDays(d)] = Rng.Chance(Rules.ChuvaChance) ? Rng.NextDouble(Rules.ChuvaPerda.Min, Rules.ChuvaPerda.Max) : 0;
        }

        CityEventCatalog catalog = Context.Campaign.CityEvents;
        double total = Context.Electorate.Voters;
        foreach (CityEventType type in catalog.Tipos.Where(t => t.Portes.Contains(Context.Municipio.Porte)))
        {
            DateOnly date = weekStart.AddDays(type.DiaSemana);
            if (type.Meses.Count > 0 && !type.Meses.Contains(date.Month))
            {
                continue;
            }

            // Evento religioso: a chance segue a fatia daquela religião entre os eleitores do município (Censo).
            double chance = type.ChanceSemanal;
            if (type.Religiao is { } religion)
            {
                double share = total <= 0 ? 0 : Context.Electorate.Segments.Where(s => s.Has(religion)).Sum(s => s.Voters) / total;
                chance = Math.Min(1, chance * share * catalog.ReligiosoEscala);
            }

            List<ZoneElectorate> zones = Context.ZoneElectorates
                .Where(z => (type.Situacao == "qualquer" || z.Zona.Situacao == type.Situacao) && (type.Publico == CommitmentType.PublicoGeral || z.VotersIn(type.Publico) > 0))
                .ToList();
            if (zones.Count == 0 || !Rng.Chance(chance))
            {
                continue;
            }

            ZoneElectorate zone = Rng.PickWeighted(zones, z => type.Publico == CommitmentType.PublicoGeral ? Math.Max(1, z.Voters) : z.VotersIn(type.Publico));
            Agenda.CityEvents.Add(new CityEventInstance
            {
                Id = $"ev{Agenda.NextId++}",
                TypeId = type.Id,
                Nome = type.Nome,
                ZoneId = zone.Zona.Id,
                Date = date,
                Turno = type.Turno,
                Publico = type.Publico,
            });
        }
    }

    private static CommitmentRequest RequestOf(Commitment c) =>
        new(c.Type, c.ZoneId, c.Date, c.Turno, c.Publico, c.PersonId, c.CityEventId, c.InviteId, c.Delegado, c.Tema, c.OutletId);

    /// <summary>Delegações que o coordenador faz por semana (pela qualidade); 0 sem coordenador.</summary>
    public int DelegationsPerWeek => (int)Math.Round(StaffEffect(StaffRole.Coordenador, StaffRole.Delegacoes));

    /// <summary>Delegações já usadas (marcadas ou feitas) na semana da data.</summary>
    public int DelegationsIn(DateOnly date)
    {
        DateOnly monday = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
        DateOnly sunday = monday.AddDays(6);
        return Agenda.Scheduled.Count(c => c.Delegado && c.Date >= monday && c.Date <= sunday)
            + Agenda.Log.Count(x => x.Commitment.Delegado && x.Commitment.Date >= monday && x.Commitment.Date <= sunday);
    }

    /// <summary>Compromissos marcados de hoje até o domingo desta semana (para a pausa "semana sem compromisso").</summary>
    public int ScheduledIn(DateOnly from, DateOnly to) => Agenda.Scheduled.Count(c => c.Date >= from && c.Date <= to);

    /// <summary>
    /// Sensibilidade dos votos do jogador à presença em cada zona e ao apoio de cada grupo em cada zona (derivadas
    /// por diferença finita na projeção da zona). Serve para estimar o ganho de votos de um compromisso sem
    /// reprojetar a cidade inteira a cada opção (o robô do BalanceSim e os conselhos da equipe usam).
    /// </summary>
    public sealed record VoteSensitivity(double[] PerPresencePoint, Dictionary<string, double>[] PerSupportUnit);

    public VoteSensitivity Sensitivity(IReadOnlyCollection<string> groups)
    {
        Office office = Campaign.Player.Office;
        List<VoteCandidate> candidates = Campaign.ForOffice(office).Select(ToVote).ToList();
        VoteModelConfig config = Context.ElectorateContent.VoteModel;
        Dictionary<string, double> noise = VoteModel.Noise(candidates, config, null);
        TerritoryState t = Territory;
        double[] presence = t.Presence[RaceSetup.PlayerId];
        var perPoint = new double[t.Zones.Count];
        var perSupport = new Dictionary<string, double>[t.Zones.Count];
        if (!t.Support.TryGetValue(RaceSetup.PlayerId, out List<Dictionary<string, double>>? support))
        {
            RefreshSupport();
            support = t.Support[RaceSetup.PlayerId];
        }

        foreach (ZoneElectorate zone in Context.ZoneElectorates)
        {
            int z = zone.Index;
            double Votes() => TerritoryVote.ProjectZone(zone, t, candidates, config, Context.Themes, noise).Votes[RaceSetup.PlayerId];
            double baseline = Votes();
            double saved = presence[z];
            const double Step = 2;
            presence[z] = Math.Min(100, saved + Step);
            perPoint[z] = presence[z] > saved ? (Votes() - baseline) / (presence[z] - saved) : 0;
            presence[z] = saved;

            perSupport[z] = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string group in groups)
            {
                if (zone.VotersIn(group) <= 0)
                {
                    continue;
                }

                const double Unit = 0.05;
                bool had = support[z].TryGetValue(group, out double before);
                support[z][group] = Math.Min(1, before + Unit);
                perSupport[z][group] = (Votes() - baseline) / Unit;
                if (had)
                {
                    support[z][group] = before;
                }
                else
                {
                    support[z].Remove(group);
                }
            }
        }

        return new VoteSensitivity(perPoint, perSupport);
    }
}
