using Planalto.Core.Characters;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Campaign;

/// <summary>Gera os candidatos a cada função da equipe quando o município entra em foco (GDD 17.7).</summary>
public static class StaffBuilder
{
    public static void Ensure(CampaignContext ctx, CampaignState campaign)
    {
        GameState state = ctx.State;
        CityState? city = state.City;
        if (city is null || city.Staff.Count > 0)
        {
            return;
        }

        // Sorteio próprio, derivado da semente do save e do município: gerar a equipe não desloca o sorteio da campanha.
        var rng = new Rng(state.Rng.Seed ^ ((ulong)ctx.Municipio.CodigoIbge * 2654435761UL) ^ 0x45515549UL);
        StaffCatalog catalog = ctx.Campaign.Staff;
        StaffRules rules = catalog.Regras;
        double costScale = ctx.Config.PorteCostMultiplier.GetValueOrDefault(ctx.Municipio.Porte, 1);
        List<Candidate> rivals = campaign.Candidates.Where(c => !c.IsPlayer && !c.IsBulk).ToList();
        List<string> themes = ctx.Themes.Themes.Select(t => t.Id).ToList();
        int serial = state.Npcs.Count;

        foreach (StaffRole role in catalog.Funcoes)
        {
            int count = rng.NextInt(rules.CandidatosPorFuncao.Min, rules.CandidatosPorFuncao.Max + 1);
            bool family = false;
            for (int i = 0; i < count; i++)
            {
                List<StaffBondType> bonds = catalog.Vinculos
                    .Where(b => b.Id switch
                    {
                        StaffBondType.Rival => rivals.Count > 0,
                        StaffBondType.Lideranca => city.Leaders.Count > 0,
                        StaffBondType.Familiar => !family,
                        _ => true,
                    }).ToList();
                StaffBondType bond = rng.PickWeighted(bonds, b => b.Weight);
                family |= bond.Id == StaffBondType.Familiar;

                string? bondPerson = null;
                string bondText = bond.Texto;
                if (bond.Id == StaffBondType.Rival)
                {
                    Candidate rival = rng.Pick(rivals);
                    bondPerson = rival.Id;
                    bondText = Texts.Fill(bond.Texto, ("nome", rival.Name));
                }
                else if (bond.Id == StaffBondType.Lideranca)
                {
                    Leader leader = rng.Pick(city.Leaders);
                    bondPerson = leader.PersonId;
                    bondText = Texts.Fill(bond.Texto,
                        ("nome", state.Npcs.FirstOrDefault(n => n.Id == leader.PersonId)?.BallotName ?? leader.PersonId),
                        ("papel", ctx.Campaign.Leaders.Type(leader.Type).Papel));
                }

                int quality = Math.Clamp(rng.PickWeighted([1, 2, 3, 4, 5], v => v is 1 or 5 ? 1.0 : v == 3 ? 3.0 : 2.0) + bond.QualidadeDelta, 1, 5);
                int shownMin = Math.Clamp(quality - (rng.Chance(0.5) ? 1 : 0), 1, 4);

                // O salário pedido segue a fama (a faixa que o jogador vê), não a qualidade real.
                decimal asked = (rules.SalarioPorQualidade[shownMin - 1] + rules.SalarioPorQualidade[shownMin]) / 2;
                decimal salary = Math.Max(10, Math.Round(asked * (decimal)costScale * (decimal)rng.NextDouble(rules.SalarioVariacao.Min, rules.SalarioVariacao.Max) / 10) * 10);

                Character npc = PersonGenerator.Generate(rng, ctx.People, new PersonRequest(
                    $"npc_{ctx.Municipio.CodigoIbge}_{serial++}", ctx.Municipio.Uf, ctx.Municipio.CodigoIbge, state.Calendar.WeekStart, Age: rng.NextInt(22, 61)));
                state.Npcs.Add(npc);
                var interests = new List<Interest> { new() { Kind = Interest.Tema, Target = rng.Pick(themes) } };
                if (rng.Chance(0.5))
                {
                    interests.Add(new Interest { Kind = Interest.ZonaKind, Target = rng.Pick(campaign.Territory.Zones) });
                }

                city.People.Add(new Person
                {
                    Id = npc.Id,
                    Roles = [PersonRole.Equipe],
                    Interests = interests,
                    Temperament = (Temperament)rng.NextInt(5),
                    Affinity = bond.Id == StaffBondType.Familiar ? rng.NextInt(15, 31) : rng.NextInt(-5, 11),
                });
                city.Staff.Add(new StaffMember
                {
                    PersonId = npc.Id,
                    Role = role.Id,
                    Quality = quality,
                    QualityShownMin = shownMin,
                    QualityShownMax = shownMin + 1,
                    Salary = salary,
                    Loyalty = Math.Max(rules.LealdadeMinimaInicial, rng.NextInt(bond.Lealdade.Min, bond.Lealdade.Max + 1)),
                    BondKind = bond.Id,
                    BondPersonId = bondPerson,
                    BondText = bondText,
                });
            }
        }
    }
}

/// <summary>Equipe na campanha (GDD 17.7): contratar, demitir, folha, conselho da semana, eventos próprios e os efeitos de cada função.</summary>
public sealed partial class CampaignEngine
{
    private StaffCatalog StaffContent => Context.Campaign.Staff;

    private StaffRules StaffRules => StaffContent.Regras;

    public TeamState Team => Campaign.Team;

    public IEnumerable<StaffMember> HiredStaff => City.Staff.Where(s => s.Hired);

    /// <summary>Quem ocupa a função hoje, ou null.</summary>
    public StaffMember? StaffIn(string roleId) => City.Staff.FirstOrDefault(s => s.Hired && s.Role == roleId);

    public StaffMember? Staff(string personId) => City.Staff.FirstOrDefault(s => s.PersonId == personId);

    /// <summary>Valor de um efeito de função hoje: pela qualidade de quem ocupa a função, ou o valor sem ninguém nela.</summary>
    public double StaffEffect(string roleId, string effect)
    {
        StaffScale scale = StaffContent.Role(roleId).Efeitos[effect];
        return StaffIn(roleId) is { } member ? scale.At(member.Quality) : scale.Sem;
    }

    /// <summary>Chance de êxito do advogado em impugnação e direito de resposta (a R3 usa).</summary>
    public double LawyerSuccessChance => StaffEffect(StaffRole.Advogado, StaffRole.Exito);

    /// <summary>O coordenador é bom o bastante para saber a agenda dos rivais na véspera (a R3 usa).</summary>
    public bool SeesRivalAgenda => StaffEffect(StaffRole.Coordenador, StaffRole.VeAgendaRival) > 0;

    /// <summary>Vagas da equipe no porte da cidade, contando a rede de cabos.</summary>
    public int TeamSlots => StaffRules.VagasPorPorte.GetValueOrDefault(Context.Municipio.Porte, 3);

    private bool CabosNetworkActive => false;

    public int TeamSlotsUsed => HiredStaff.Count() + (CabosNetworkActive ? 1 : 0);

    /// <summary>Faixa da sorte de compromissos e canais: o marqueteiro a estreita (e a previsão junto).</summary>
    private (double Min, double Max) LuckRange(Parties.DoubleRange luck)
    {
        double narrow = StaffEffect(StaffRole.Marqueteiro, StaffRole.Faixa);
        return (1 - ((1 - luck.Min) * narrow), 1 + ((luck.Max - 1) * narrow));
    }

    private string StaffText(string key, StaffMember member, params (string Key, string Value)[] more) =>
        FillStaff(member, StaffContent.Texto(key), more);

    private Memory RememberStaff(StaffMember member, string key)
    {
        MemoryDefinition def = StaffContent.Memory(key);
        Person person = City.Person(member.PersonId)!;
        person.LastContact = Today;
        return person.Remember(Today, key, def.Peso, def.Pontos, def.Texto, PeopleConfig);
    }

    private static void ChangeLoyalty(StaffMember member, int delta) => member.Loyalty = Math.Clamp(member.Loyalty + delta, 0, 100);

    private bool CampaignRunning => Campaign.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha;

    // ------------------------------------------------------------------ consultas

    /// <summary>Candidatos e ocupante de uma função, para a tela: a qualidade aparece em faixa até ser conhecida.</summary>
    public IReadOnlyList<StaffView> StaffCandidates(string roleId) =>
        City.Staff.Where(s => s.Role == roleId && !s.Left).Select(View).ToList();

    /// <summary>A equipe contratada.</summary>
    public IReadOnlyList<StaffView> TeamView() => HiredStaff.Select(View).ToList();

    private StaffView View(StaffMember s) => new(
        s, NameOf(s.PersonId), StaffContent.Role(s.Role).Nome, s.QualityShownMin, s.QualityShownMax, s.QualityShownMin == s.QualityShownMax,
        s.Salary, s.Loyalty, s.BondText, s.Hired ? null : CanHire(s.PersonId));

    /// <summary>Folha semanal e quantas semanas o caixa de hoje paga.</summary>
    public PayrollView Payroll() => PayrollWith(HiredStaff.ToList());

    private PayrollView PayrollWith(List<StaffMember> hired)
    {
        decimal weekly = hired.Sum(s => s.Salary);
        double weeks = weekly <= 0 ? double.PositiveInfinity : (double)(Math.Max(0, State.Player.Resources.Money) / weekly);
        return new PayrollView(weekly, weeks, weekly > 0 && weeks < StaffRules.SemanasDeFolhaAlerta, hired.Select(s => (s.PersonId, s.Salary)).ToList());
    }

    private List<LedgerSkeleton> SkeletonsKnownBy(string personId) =>
        State.Ledger.Skeletons.Where(s => !s.Exposed && s.KnownBy.Contains(personId)).ToList();

    // ------------------------------------------------------------------ contratar e demitir

    public CommandFailure? CanHire(string personId)
    {
        if (Staff(personId) is not { Left: false } member)
        {
            return CommandFailure.NaoEncontrado;
        }

        if (!CampaignRunning)
        {
            return CommandFailure.ForaDaCampanha;
        }

        if (member.Hired || StaffIn(member.Role) is not null)
        {
            return CommandFailure.FuncaoOcupada;
        }

        if (TeamSlotsUsed >= TeamSlots)
        {
            return CommandFailure.SemVaga;
        }

        return State.Player.Resources.Money < member.Salary ? CommandFailure.SemDinheiro : null;
    }

    /// <summary>Previsão de contratar: qualidade em faixa, salário, semanas de caixa, o efeito da função e quem vai lembrar.</summary>
    public CommandForecast ForecastHire(string personId)
    {
        if (Staff(personId) is not { } member)
        {
            return new CommandForecast(CommandFailure.NaoEncontrado, [], [], [StaffContent.Texto("previsao_contratar")]);
        }

        StaffRole role = StaffContent.Role(member.Role);
        PayrollView after = PayrollWith([.. HiredStaff, member]);
        var lines = new List<ForecastLine>
        {
            new("quality", null, member.QualityShownMin, member.QualityShownMax, (member.QualityShownMin + member.QualityShownMax) / 2.0),
            new("salary", null, (double)member.Salary, (double)member.Salary, (double)member.Salary),
            new("payroll", null, (double)after.Weekly, (double)after.Weekly, (double)after.Weekly),
        };
        foreach (var (effect, scale) in role.Efeitos)
        {
            double a = scale.At(member.QualityShownMin);
            double b = scale.At(member.QualityShownMax);
            lines.Add(new ForecastLine(effect, role.Id, Math.Min(a, b), Math.Max(a, b), (a + b) / 2));
        }

        var explanation = new List<string>
        {
            StaffText("previsao_contratar", member, ("min", Texts.Number(member.QualityShownMin)), ("max", Texts.Number(member.QualityShownMax)),
                ("semanas", Texts.Number(StaffRules.SemanasParaConhecer)), ("salario", Texts.Money(member.Salary))),
            StaffText("previsao_vinculo", member, ("vinculo", member.BondText)),
            StaffText("previsao_folha", member, ("folha", Texts.Money(after.Weekly)), ("semanas_caixa", double.IsInfinity(after.WeeksOfCash) ? "∞" : Texts.Number(Math.Floor(after.WeeksOfCash)))),
        };
        explanation.AddRange(PassedOver(member).Select(other => StaffText("previsao_preterido", other)));
        return new CommandForecast(CanHire(personId), lines, [], explanation);
    }

    /// <summary>Candidatos da mesma função que são da família do jogador: lembram quando outro é escolhido.</summary>
    private IEnumerable<StaffMember> PassedOver(StaffMember chosen) =>
        City.Staff.Where(s => s.Role == chosen.Role && s != chosen && !s.Left && !s.Hired && s.BondKind == StaffBondType.Familiar);

    /// <summary>Contrata; quem entra lembra, e o parente preterido também.</summary>
    public (CommandFailure? Failure, Memory? Memory) Hire(string personId)
    {
        if (CanHire(personId) is { } failure)
        {
            return (failure, null);
        }

        StaffMember member = Staff(personId)!;
        foreach (StaffMember other in PassedOver(member).ToList())
        {
            RememberStaff(other, "preterido");
        }

        member.Hired = true;
        member.HiredOn = Today;
        Memory memory = RememberStaff(member, "contratacao");
        Agenda.AddFeed(new FeedItem(Today, "equipe", StaffText("contratado", member), null, member.PersonId, RaceSetup.PlayerId));
        UpdatePayrollAlert();
        return (null, memory);
    }

    /// <summary>Previsão de demitir: a folha que sobra e a chance de a pessoa contar a um rival o que sabe.</summary>
    public CommandForecast ForecastFire(string personId)
    {
        if (Staff(personId) is not { Hired: true } member)
        {
            return new CommandForecast(CommandFailure.NaoEncontrado, [], [], [StaffContent.Texto("previsao_demitir")]);
        }

        PayrollView after = PayrollWith(HiredStaff.Where(s => s != member).ToList());
        var lines = new List<ForecastLine> { new("payroll", null, (double)after.Weekly, (double)after.Weekly, (double)after.Weekly) };
        var outcomes = new List<ForecastOutcome>();
        var explanation = new List<string> { StaffText("previsao_demitir", member, ("folha", Texts.Money(after.Weekly))) };
        List<LedgerSkeleton> known = SkeletonsKnownBy(member.PersonId);
        if (known.Count > 0 && member.Loyalty < StaffRules.LealdadeVazamento)
        {
            double image = known.Sum(s => s.Severity) * StaffRules.VazamentoImagemPorGravidade;
            lines.Add(new ForecastLine("image", null, Math.Min(image, 0), Math.Max(image, 0), image * StaffRules.ChanceVazamento));
            string text = StaffText("previsao_vazamento", member, ("esqueletos", Texts.Number(known.Count)));
            outcomes.Add(new ForecastOutcome(StaffRules.ChanceVazamento, text));
            explanation.Add(text);
        }
        else
        {
            explanation.Add(StaffText("previsao_sem_vazamento", member));
        }

        return new CommandForecast(null, lines, outcomes, explanation);
    }

    /// <summary>Demite. Quem sai lembra; com lealdade baixa e sabendo de irregularidade, pode contar a um rival.</summary>
    public StaffDeparture? Fire(string personId) =>
        Staff(personId) is { Hired: true } member ? Depart(member, "demissao", null) : null;

    private StaffDeparture Depart(StaffMember member, string memoryKey, string? toRival)
    {
        member.Hired = false;
        member.Left = true;
        Memory memory = RememberStaff(member, memoryKey);
        Team.Pending.RemoveAll(d => d.PersonId == member.PersonId || d.OtherPersonId == member.PersonId);
        Agenda.AddFeed(new FeedItem(Today, "equipe", StaffText("demitido", member), null, member.PersonId, RaceSetup.PlayerId));

        var exposed = new List<string>();
        string? rivalId = null;
        List<LedgerSkeleton> known = SkeletonsKnownBy(member.PersonId);
        if (known.Count > 0 && member.Loyalty < StaffRules.LealdadeVazamento && Rng.Chance(StaffRules.ChanceVazamento))
        {
            List<Candidate> rivals = Campaign.ForOffice(Campaign.Player.Office).Where(c => !c.IsPlayer && !c.IsBulk).ToList();
            rivalId = toRival ?? (member.BondKind == StaffBondType.Rival && Campaign.Find(member.BondPersonId ?? "") is not null ? member.BondPersonId : null)
                ?? (rivals.Count > 0 ? Rng.Pick(rivals).Id : null);
            foreach (LedgerSkeleton skeleton in known)
            {
                // O rival passa a saber (a R3 decide quando ele usa); o boato já pesa na imagem.
                if (rivalId is not null && !skeleton.KnownBy.Contains(rivalId))
                {
                    skeleton.KnownBy.Add(rivalId);
                }

                Campaign.Player.Image = Math.Clamp(Campaign.Player.Image + (skeleton.Severity * StaffRules.VazamentoImagemPorGravidade), -1, 1);
                exposed.Add(skeleton.Id);
            }

            RememberStaff(member, "vazamento");
            Agenda.AddFeed(new FeedItem(Today, "vazamento", StaffText("vazou", member, ("rival", rivalId is null ? "" : NameOf(rivalId))), null, member.PersonId, rivalId));
        }

        UpdatePayrollAlert();
        return new StaffDeparture(memory, exposed.Count > 0, rivalId, exposed);
    }

    private void UpdatePayrollAlert()
    {
        bool alert = Payroll().Alert;
        if (alert && !Team.PayrollAlert)
        {
            Agenda.AddFeed(new FeedItem(Today, Clock.CaixaBaixo, Texts.Fill(StaffContent.Texto("caixa_baixo"), ("semanas", Texts.Number(StaffRules.SemanasDeFolhaAlerta))), null, null, RaceSetup.PlayerId));
        }

        Team.PayrollAlert = alert;
    }

    // ------------------------------------------------------------------ conselho da semana

    /// <summary>O jogador dispensa o conselho da semana na hora: conta como ignorado.</summary>
    public bool IgnoreAdvice()
    {
        if (Team.Advice is not { State: AdviceState.Aberto } advice)
        {
            return false;
        }

        CloseAdvice(advice, followed: false);
        return true;
    }

    /// <summary>Quantos compromissos o jogador já fez nesta semana na zona aconselhada, e quantos bastam para seguir o conselho.</summary>
    public (int Done, int Needed) AdviceProgress()
    {
        if (Team.Advice is not { } advice)
        {
            return (0, 0);
        }

        DateOnly start = State.Calendar.WeekStart;
        int done = Agenda.Log.Count(x => x.Commitment.ZoneId == advice.ZoneId && x.Commitment.Date >= start && x.Commitment.Date <= start.AddDays(6));
        return (done, StaffRules.ConselhoCompromissosMinimos);
    }

    private void CloseAdvice(Advice advice, bool followed)
    {
        advice.State = followed ? AdviceState.Seguido : AdviceState.Ignorado;
        if (Staff(advice.FromPersonId) is { Hired: true } coordinator)
        {
            ChangeLoyalty(coordinator, followed ? StaffRules.LealdadeConselho : -StaffRules.LealdadeConselho);
            RememberStaff(coordinator, followed ? "conselho_seguido" : "conselho_ignorado");
            Agenda.AddFeed(new FeedItem(Today, "conselho", StaffText(followed ? "conselho_seguido" : "conselho_ignorado", coordinator), advice.ZoneId, coordinator.PersonId, RaceSetup.PlayerId));
        }

        Team.AdviceLog.Add(advice);
        if (Team.AdviceLog.Count > TeamState.MaxAdviceLog)
        {
            Team.AdviceLog.RemoveRange(0, Team.AdviceLog.Count - TeamState.MaxAdviceLog);
        }

        Team.Advice = null;
    }

    /// <summary>
    /// Conselho de segunda-feira: a zona em que uma caminhada renderia mais votos hoje (sensibilidade da projeção real
    /// × alcance × o que ainda há a ganhar × cansaço), vista com erro maior quanto pior o coordenador.
    /// </summary>
    private void NewAdvice()
    {
        if (StaffIn(StaffRole.Coordenador) is not { } coordinator || Territory.Zones.Count < 2)
        {
            return;
        }

        double error = StaffEffect(StaffRole.Coordenador, StaffRole.ErroConselho);
        VoteSensitivity sens = Sensitivity([]);
        var seen = new List<(int Zone, double Value)>();
        for (int z = 0; z < Territory.Zones.Count; z++)
        {
            double fatigue = FatigueYield(AgendaState.FatigueKey("caminhada", Territory.Zones[z], CommitmentType.PublicoGeral));
            double value = sens.PerPresencePoint[z] * Reach(z) * (1 - (Territory.PresenceOf(RaceSetup.PlayerId, z) / 100)) * fatigue;
            seen.Add((z, value * (1 + (error * Rng.NextDouble(-1, 1)))));
        }

        int best = seen.MaxBy(x => x.Value).Zone;
        int worst = seen.MinBy(x => x.Value).Zone;
        string zone = Territory.Zones[best];
        string? avoid = Territory.Zones.Count >= 3 && worst != best ? Territory.Zones[worst] : null;
        Team.Advice = new Advice
        {
            FromPersonId = coordinator.PersonId,
            Week = State.Calendar.WeekStart,
            ZoneId = zone,
            AvoidZoneId = avoid,
            Text = StaffText(avoid is null ? "conselho_unico" : "conselho", coordinator, ("zona", ZoneName(zone)), ("evitar", avoid is null ? "" : ZoneName(avoid))),
        };
        Agenda.AddFeed(new FeedItem(Today, "conselho", Team.Advice.Text, zone, coordinator.PersonId, RaceSetup.PlayerId));
    }

    // ------------------------------------------------------------------ virada da semana

    /// <summary>Fim da semana: fecha o conselho, paga a folha, conta o tempo de casa, e o tesoureiro pode errar nas contas.</summary>
    private void StaffWeekEnd()
    {
        if (Team.Advice is { State: AdviceState.Aberto } advice)
        {
            var (done, needed) = AdviceProgress();
            CloseAdvice(advice, done >= needed);
        }

        List<StaffMember> hired = HiredStaff.ToList();
        bool late = false;
        foreach (StaffMember member in hired)
        {
            if (State.Player.Resources.Money >= member.Salary)
            {
                State.Player.Resources.Money -= member.Salary;
                Team.RegisteredSpending += member.Salary;
            }
            else
            {
                late = true;
                ChangeLoyalty(member, StaffRules.LealdadeSalarioAtrasado);
                RememberStaff(member, "salario_atrasado");
            }

            member.WeeksWorked++;
            if (member.WeeksWorked >= StaffRules.SemanasParaConhecer)
            {
                member.QualityShownMin = member.QualityShownMax = member.Quality;
            }
        }

        Campaign.Player.Money = State.Player.Resources.Money;
        if (late)
        {
            Agenda.AddFeed(new FeedItem(Today, "equipe", StaffContent.Texto("folha_atrasada"), null, null, RaceSetup.PlayerId));
        }

        // Erro contábil: só há o que registrar errado em semana com gasto de campanha (folha ou canais).
        if (Team.RegisteredSpending > 0 && Rng.Chance(StaffEffect(StaffRole.Tesoureiro, StaffRole.ErroContabil)))
        {
            State.Ledger.AddSkeleton("erro_contabil", StaffContent.Texto("erro_contabil_livro"), StaffRules.GravidadeErroContabil, Today, []);
            Agenda.AddFeed(new FeedItem(Today, "contas", StaffContent.Texto("erro_contabil"), null, StaffIn(StaffRole.Tesoureiro)?.PersonId, RaceSetup.PlayerId));
        }

        Team.RegisteredSpending = 0;

        // Quem coordena e quem cuida das contas fica sabendo das irregularidades do tempo em que trabalha na campanha.
        foreach (StaffMember member in hired.Where(m => StaffRules.FuncoesQueSabem.Contains(m.Role)))
        {
            foreach (LedgerSkeleton skeleton in State.Ledger.Skeletons.Where(s => s.Created >= member.HiredOn && !s.KnownBy.Contains(member.PersonId)))
            {
                skeleton.KnownBy.Add(member.PersonId);
            }
        }

        UpdatePayrollAlert();
    }

    /// <summary>Segunda-feira: conselho da semana e, às vezes, um evento da equipe que pede decisão.</summary>
    private void StaffWeekStart()
    {
        if (!CampaignRunning || !HiredStaff.Any())
        {
            return;
        }

        NewAdvice();
        if (Team.Pending.Count == 0 && Rng.Chance(StaffRules.ChanceEventoSemanal))
        {
            TriggerStaffEvent();
        }
    }

    /// <summary>
    /// Abre um evento da equipe como decisão pendente: o pedido (ou um sorteado entre os que cabem agora). Devolve
    /// null se nenhum membro contratado está em situação de vivê-lo.
    /// </summary>
    public StaffDecision? TriggerStaffEvent(string? eventId = null)
    {
        List<StaffMember> hired = HiredStaff.ToList();
        List<Candidate> rivals = Campaign.ForOffice(Campaign.Player.Office).Where(c => !c.IsPlayer && !c.IsBulk).ToList();
        bool Recent(StaffMember m, params string[] facts) =>
            City.Person(m.PersonId)!.Memories.Any(x => facts.Contains(x.Fact) && (Today.DayNumber - x.Date.DayNumber) / 7 < StaffRules.SemanasParaPedirAumento * 2);

        var eligible = new Dictionary<string, List<StaffMember>>(StringComparer.Ordinal)
        {
            [StaffEventDefinition.Aumento] = hired.Where(m => m.WeeksWorked >= StaffRules.SemanasParaPedirAumento && !Recent(m, "aumento_concedido", "aumento_negado", "cargo_prometido")).ToList(),
            [StaffEventDefinition.PropostaRival] = rivals.Count == 0 ? [] : hired.Where(m => (m.BondKind == StaffBondType.Rival || m.Quality >= StaffRules.QualidadeAssediada) && !Recent(m, "proposta_coberta", "apelo")).ToList(),
            [StaffEventDefinition.Briga] = hired.Count >= 2 ? hired : [],
            [StaffEventDefinition.TrazLideranca] = hired.Where(m => m.BondKind == StaffBondType.Lideranca && City.Leader(m.BondPersonId ?? "") is { AlignedWith: not RaceSetup.PlayerId }
                && !City.Person(m.PersonId)!.Memories.Any(x => x.Fact is "apresentou" or "ajuda_recusada")).ToList(),
            [StaffEventDefinition.TrazDoador] = hired.Where(m => m.BondKind is StaffBondType.Familiar or StaffBondType.Nenhum && !Recent(m, "trouxe_doador", "ajuda_recusada")).ToList(),
        };
        List<StaffEventDefinition> events = StaffContent.Eventos.Where(e => (eventId is null || e.Id == eventId) && eligible.GetValueOrDefault(e.Id) is { Count: > 0 }).ToList();
        if (events.Count == 0)
        {
            return null;
        }

        StaffEventDefinition definition = Rng.PickWeighted(events, e => e.Weight);
        StaffMember member = Rng.Pick(eligible[definition.Id]);
        StaffMember? other = definition.Id == StaffEventDefinition.Briga ? Rng.Pick(hired.Where(m => m != member).ToList()) : null;
        string? rivalId = definition.Id != StaffEventDefinition.PropostaRival ? null
            : member.BondKind == StaffBondType.Rival && rivals.Any(r => r.Id == member.BondPersonId) ? member.BondPersonId : Rng.Pick(rivals).Id;
        string? leaderId = definition.Id == StaffEventDefinition.TrazLideranca ? member.BondPersonId : null;
        var decision = new StaffDecision
        {
            Id = $"eq{Team.NextId++}",
            EventId = definition.Id,
            PersonId = member.PersonId,
            OtherPersonId = other?.PersonId,
            RivalId = rivalId,
            LeaderId = leaderId,
            Date = Today,
        };
        decision.Text = DecisionText(decision, definition.Texto, null);
        Team.Pending.Add(decision);
        return decision;
    }

    /// <summary>Texto de um evento da equipe ou de uma opção dele, com os nomes do save.</summary>
    public string DecisionText(StaffDecision decision, string template, StaffEventOption? option)
    {
        StaffMember member = Staff(decision.PersonId)!;
        StaffMember? other = decision.OtherPersonId is null ? null : Staff(decision.OtherPersonId);
        decimal money = option is null
            ? StaffContent.Event(decision.EventId).Opcoes.Select(o => ScaledMoney(o.Efeitos.Dinheiro)).FirstOrDefault(v => v != 0)
            : ScaledMoney(option.Efeitos.Dinheiro);
        if (money == 0)
        {
            money = StaffContent.Event(decision.EventId).Opcoes.Select(o => ScaledMoney(o.Efeitos.Dinheiro)).FirstOrDefault(v => v != 0);
        }

        return FillStaff(member, template,
            ("outro", other is null ? "" : NameOf(other.PersonId)),
            ("papel_outro", other is null ? "" : StaffContent.Role(other.Role).Papel),
            ("rival", decision.RivalId is null ? "" : NameOf(decision.RivalId)),
            ("lideranca", decision.LeaderId is null ? "" : NameOf(decision.LeaderId)),
            ("valor", Texts.Money(Math.Abs(money))),
            ("salario", Texts.Money(option is null ? member.Salary : NewSalary(member, option.Efeitos))));
    }

    private string FillStaff(StaffMember member, string template, params (string Key, string Value)[] more)
    {
        StaffRole role = StaffContent.Role(member.Role);
        return Texts.Fill(Texts.Fill(template, ("nome", NameOf(member.PersonId)), ("papel", role.Papel), ("funcao", role.Nome)), more);
    }

    private decimal ScaledMoney(decimal amount) => Math.Round(amount * (decimal)CostScale / 100) * 100;

    private static decimal NewSalary(StaffMember member, StaffOptionEffects fx) =>
        Math.Abs(fx.SalarioFator - 1) < 1e-9 ? member.Salary : Math.Max(10, Math.Round(member.Salary * (decimal)fx.SalarioFator / 10) * 10);

    private (StaffDecision Decision, StaffEventDefinition Definition, StaffEventOption Option) FindDecision(string decisionId, string optionId)
    {
        StaffDecision decision = Team.Pending.FirstOrDefault(d => d.Id == decisionId) ?? throw new InvalidOperationException("Decisão da equipe não encontrada.");
        StaffEventDefinition definition = StaffContent.Event(decision.EventId);
        return (decision, definition, definition.Option(optionId));
    }

    /// <summary>Previsão de uma opção de evento da equipe: lealdade, salário, caixa, chance de alguém sair e o que entra no Livro.</summary>
    public CommandForecast ForecastStaffDecision(string decisionId, string optionId)
    {
        var (decision, _, option) = FindDecision(decisionId, optionId);
        StaffMember member = Staff(decision.PersonId)!;
        StaffMember? other = decision.OtherPersonId is null ? null : Staff(decision.OtherPersonId);
        StaffOptionEffects fx = option.Efeitos;
        var lines = new List<ForecastLine>();
        var outcomes = new List<ForecastOutcome>();
        string Signed(int v) => v > 0 ? $"+{v}" : v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        decimal salary = NewSalary(member, fx);
        var explanation = new List<string>
        {
            FillStaff(member, StaffContent.Texto("previsao_opcao"), ("rotulo", DecisionText(decision, option.Rotulo, option)), ("lealdade", Signed(fx.Lealdade)), ("salario", Texts.Money(salary))),
        };
        if (fx.Lealdade != 0)
        {
            lines.Add(new ForecastLine("loyalty", member.PersonId, fx.Lealdade, fx.Lealdade, fx.Lealdade));
        }

        if (salary != member.Salary)
        {
            lines.Add(new ForecastLine("salary", member.PersonId, (double)salary, (double)salary, (double)salary));
        }

        if (other is not null && fx.LealdadeOutro != 0)
        {
            lines.Add(new ForecastLine("loyalty", other.PersonId, fx.LealdadeOutro, fx.LealdadeOutro, fx.LealdadeOutro));
            explanation.Add(Texts.Fill(StaffContent.Texto("previsao_outro"), ("outro", NameOf(other.PersonId)), ("lealdade", Signed(fx.LealdadeOutro))));
        }

        decimal money = ScaledMoney(fx.Dinheiro);
        if (money != 0)
        {
            lines.Add(new ForecastLine("money", null, (double)money, (double)money, (double)money));
            explanation.Add(Texts.Fill(StaffContent.Texto("previsao_dinheiro"), ("valor", (money > 0 ? "+" : "−") + Texts.Money(Math.Abs(money)))));
        }

        double leave = fx.FicaPelaLealdade ? 1 - (Math.Clamp(member.Loyalty + fx.Lealdade, 0, 100) / 100.0) : fx.SaiChance;
        if (leave > 0)
        {
            string text = FillStaff(member, StaffContent.Texto("previsao_saida"), ("chance", Texts.Percent(leave)));
            outcomes.Add(new ForecastOutcome(leave, text));
            explanation.Add(text);
        }

        if (other is not null && fx.OutroSaiChance > 0)
        {
            string text = Texts.Fill(StaffContent.Texto("previsao_saida"), ("nome", NameOf(other.PersonId)), ("chance", Texts.Percent(fx.OutroSaiChance)));
            outcomes.Add(new ForecastOutcome(fx.OutroSaiChance, text));
            explanation.Add(text);
        }

        if (fx.Promessa || fx.Divida)
        {
            explanation.Add(StaffContent.Texto("previsao_livro"));
        }

        return new CommandForecast(null, lines, outcomes, explanation);
    }

    /// <summary>Responde a um evento da equipe. Toda opção deixa lembrança em quem estava envolvido.</summary>
    public StaffDecisionResult AnswerStaffDecision(string decisionId, string optionId)
    {
        var (decision, _, option) = FindDecision(decisionId, optionId);
        StaffMember member = Staff(decision.PersonId)!;
        StaffMember? other = decision.OtherPersonId is null ? null : Staff(decision.OtherPersonId);
        StaffOptionEffects fx = option.Efeitos;
        Team.Pending.Remove(decision);

        member.Salary = NewSalary(member, fx);
        string text = DecisionText(decision, option.Resultado, option);
        ChangeLoyalty(member, fx.Lealdade);
        if (fx.Lembranca is not null)
        {
            RememberStaff(member, fx.Lembranca);
        }

        if (other is not null)
        {
            ChangeLoyalty(other, fx.LealdadeOutro);
            if (fx.LembrancaOutro is not null)
            {
                RememberStaff(other, fx.LembrancaOutro);
            }
        }

        decimal money = ScaledMoney(fx.Dinheiro);
        if (money != 0 || fx.Estresse != 0)
        {
            EffectApplier.Apply(Context, new Effects { Money = money, Stress = fx.Estresse });
            State.Player.Resources.Stress = Math.Max(0, State.Player.Resources.Stress);
        }

        bool ledger = false;
        if (fx.Promessa)
        {
            State.Ledger.AddPromise(Promise.ParaPessoa, member.PersonId, "cargo_equipe", StaffText("cargo_livro", member), null, Today, new DateOnly(Campaign.ElectionDate.Year + 1, 1, 1).AddDays(56));
            ledger = true;
        }

        if (fx.Divida)
        {
            State.Ledger.AddDebt(member.PersonId, StaffText("divida_livro", member), Today);
            ledger = true;
        }

        if (fx.ApresentaLideranca && decision.LeaderId is not null && City.Leader(decision.LeaderId) is { } leader)
        {
            MemoryDefinition def = StaffContent.Memory("apresentado");
            leader.Known = true;
            RememberLeader(leader, "apresentado", def.Peso, def.Pontos, def.Texto);
            Realign(leader);
            RefreshSupport();
        }

        bool left = false;
        bool otherLeft = false;
        if ((fx.FicaPelaLealdade && !Rng.Chance(member.Loyalty / 100.0)) || (fx.SaiChance > 0 && Rng.Chance(fx.SaiChance)))
        {
            Depart(member, "saiu", decision.RivalId);
            left = true;
            if (fx.ReforcaRival && decision.RivalId is not null && Territory.Redutos.TryGetValue(decision.RivalId, out List<int>? redutos))
            {
                // Quem saiu leva o que sabe: o rival rende uma saída a mais em cada reduto.
                double amount = Commitments.Get("caminhada").Presenca * RecognitionScale;
                foreach (int z in redutos)
                {
                    Territory.Gain(decision.RivalId, z, amount * Reach(z), 0, []);
                }

                Campaign.Find(decision.RivalId)!.Recognition = Territory.Recognition(decision.RivalId);
                Agenda.AddFeed(new FeedItem(Today, "rival_zona", Texts.Fill(StaffContent.Texto("reforco_rival"), ("rival", NameOf(decision.RivalId))), Territory.Zones[redutos[0]], null, decision.RivalId));
            }
        }

        if (other is not null && fx.OutroSaiChance > 0 && Rng.Chance(fx.OutroSaiChance))
        {
            Depart(other, "saiu", null);
            otherLeft = true;
        }

        UpdatePayrollAlert();
        return new StaffDecisionResult(text, fx.Lealdade, fx.LealdadeOutro, money, left, otherLeft, ledger);
    }
}
