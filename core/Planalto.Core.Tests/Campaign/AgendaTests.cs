using Planalto.Core.Campaign;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.Core.Tests.State;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Agenda com lugar e dia (GDD 17.6): cansaço (princípio g), turnos, comício, convites, presença e transbordo.</summary>
public class AgendaTests
{
    private static CampaignEngine Fresh(int ibge, ulong seed, bool campaign = false)
    {
        CampaignEngine engine = campaign ? ClockAndForecastTests.InCampaign(ibge, seed) : CampaignKit.NewCareer(ibge, seed);
        GameState state = engine.Context.State;
        state.Campaign!.Events.Pending.Clear();
        state.Player.Resources.Money = 1_000_000;
        state.Player.Resources.Stress = 0;
        return engine;
    }

    /// <summary>Zona com eleitores que não é a natal nem vizinha dela.</summary>
    private static string OtherZone(CampaignEngine engine)
    {
        TerritoryState t = engine.Territory;
        Zona home = engine.Context.Zonas.Zonas[t.HomeZone];
        return engine.Context.Zonas.Zonas.Where((z, i) => i != t.HomeZone && engine.Context.ZoneElectorates[i].Voters > 0)
            .OrderBy(z => home.Vizinhas.Contains(z.Id)).First().Id;
    }

    /// <summary>Marca e executa hoje, sem chuva; devolve a previsão feita antes e o resultado.</summary>
    private static (CommitmentForecast Forecast, CommitmentResult Result) RunNow(CampaignEngine engine, CommitmentRequest request)
    {
        engine.Agenda.Rain[request.Date] = 0;
        CommitmentForecast forecast = engine.Forecast(request);
        Assert.Null(forecast.Blocked);
        Assert.Null(engine.Schedule(request).Failure);
        return (forecast, Assert.Single(engine.RunToday()));
    }

    private static double Yield(CommitmentResult result) => result.Factors.Where(f => f.Id != "sorte").Aggregate(1.0, (acc, f) => acc * f.Multiplier);

    private static double Luck(CommitmentResult result) => result.Factors.Single(f => f.Id == "sorte").Multiplier;

    private static void NextWeek(CampaignEngine engine)
    {
        engine.AdvanceWeek();
        engine.Context.State.Campaign!.Events.Pending.Clear();
        engine.Context.State.Campaign.Debate = null;
    }

    [Fact]
    public void Terceiro_compromisso_igual_em_duas_semanas_rende_no_maximo_metade_e_a_previsao_informa()
    {
        CampaignEngine engine = Fresh(3106200, 61);
        AgendaRules rules = engine.Context.Campaign.Commitments.Regras;
        string zone = OtherZone(engine);
        int z = engine.Territory.IndexOf(zone);
        double[] presence = engine.Territory.Presence[RaceSetup.PlayerId];
        double start = presence[z];

        // Primeiro: sem cansaço, a previsão não mostra o fator.
        var first = RunNow(engine, new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha));
        Assert.Equal(1, first.Forecast.FatigueFactor);
        Assert.DoesNotContain(first.Forecast.Factors, f => f.Id == "cansaco");
        double firstGain = first.Result.Presence[zone] / Luck(first.Result);

        // Segundo igual na mesma semana: cerca de 62%, e a previsão já avisa antes de marcar.
        presence[z] = start;
        var second = RunNow(engine, new CommitmentRequest("caminhada", zone, engine.Today, Turno.Tarde));
        Assert.Equal(1 / (1 + rules.CansacoFator), second.Forecast.FatigueFactor, 9);
        Assert.InRange(second.Forecast.FatigueFactor, 0.60, 0.65);
        YieldFactor shown = Assert.Single(second.Forecast.Factors, f => f.Id == "cansaco");
        Assert.Equal(second.Forecast.FatigueFactor, shown.Multiplier, 9);
        Assert.Contains("2º", shown.Text);
        Assert.Contains(engine.ZoneName(zone), shown.Text);
        Assert.Equal(second.Forecast.FatigueFactor, Yield(second.Result) / Yield(first.Result), 9);
        Assert.Equal(second.Forecast.FatigueFactor, second.Result.Presence[zone] / Luck(second.Result) / firstGain, 6);

        // Terceiro igual, na semana seguinte (duas semanas ao todo): no máximo 50% do primeiro.
        NextWeek(engine);
        presence[z] = start;
        var third = RunNow(engine, new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha));
        Assert.True(third.Forecast.FatigueFactor <= 0.5, $"fator {third.Forecast.FatigueFactor}");
        Assert.Equal(third.Forecast.FatigueFactor, Assert.Single(third.Forecast.Factors, f => f.Id == "cansaco").Multiplier, 9);
        Assert.True(Yield(third.Result) <= 0.5 * Yield(first.Result));
        Assert.True(third.Result.Presence[zone] / Luck(third.Result) <= 0.5 * firstGain + 1e-9);
        Assert.Contains(third.Result.Factors, f => f.Id == "cansaco");

        // O cansaço é do trio (tipo, zona, público): outra zona e outro tipo não pagam por ele.
        string home = engine.Territory.Zones[engine.Territory.HomeZone];
        Assert.Equal(1, engine.Forecast(new CommitmentRequest("caminhada", home, engine.Today, Turno.Tarde)).FatigueFactor);
        string group = engine.Context.ElectorateContent.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).First(g => engine.Context.ZoneElectorates[z].VotersIn(g) > 0);
        Assert.Equal(1, engine.Forecast(new CommitmentRequest("reuniao", zone, engine.Today, Turno.Tarde, group)).FatigueFactor);
    }

    [Fact]
    public void Previsao_conta_os_iguais_ja_marcados_antes_na_agenda()
    {
        CampaignEngine engine = Fresh(3106200, 62);
        string zone = OtherZone(engine);
        DateOnly today = engine.Today;
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, today, Turno.Manha)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, today.AddDays(1), Turno.Manha)).Failure);

        double factor = engine.Context.Campaign.Commitments.Regras.CansacoFator;
        Assert.Equal(1 / (1 + factor), engine.Forecast(new CommitmentRequest("caminhada", zone, today, Turno.Tarde)).FatigueFactor, 9);
        Assert.Equal(1 / (1 + (2 * factor)), engine.Forecast(new CommitmentRequest("caminhada", zone, today.AddDays(2), Turno.Manha)).FatigueFactor, 9);
    }

    [Fact]
    public void Dois_compromissos_nao_ocupam_o_mesmo_turno()
    {
        CampaignEngine engine = Fresh(3106200, 63, campaign: true);
        DateOnly today = engine.Today;
        string home = engine.Territory.Zones[engine.Territory.HomeZone];
        string other = OtherZone(engine);

        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", home, today, Turno.Tarde)).Failure);
        Assert.Equal(ScheduleFailure.TurnoOcupado, engine.Schedule(new CommitmentRequest("caminhada", other, today, Turno.Tarde)).Failure);
        Assert.Equal(ScheduleFailure.TurnoOcupado, engine.Schedule(new CommitmentRequest("entrevista", null, today, Turno.Tarde)).Failure);

        // O comício ocupa dois turnos: não cabe à noite, nem onde o segundo turno já está tomado, e toma os dois.
        Assert.Equal(2, engine.Context.Campaign.Commitments.Get(CommitmentCatalog.Comicio).Turnos);
        Assert.Equal(ScheduleFailure.TurnoOcupado, engine.Schedule(new CommitmentRequest("comicio", home, today, Turno.Noite)).Failure);
        Assert.Equal(ScheduleFailure.TurnoOcupado, engine.Schedule(new CommitmentRequest("comicio", home, today, Turno.Manha)).Failure);
        DateOnly tomorrow = today.AddDays(1);
        Assert.Null(engine.Schedule(new CommitmentRequest("comicio", home, tomorrow, Turno.Manha)).Failure);
        Assert.Equal(ScheduleFailure.TurnoOcupado, engine.Schedule(new CommitmentRequest("caminhada", home, tomorrow, Turno.Manha)).Failure);
        Assert.Equal(ScheduleFailure.TurnoOcupado, engine.Schedule(new CommitmentRequest("caminhada", home, tomorrow, Turno.Tarde)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", home, tomorrow, Turno.Noite)).Failure);
        Assert.Equal(3, engine.Agenda.Scheduled.Count);

        // Desmarcar libera o turno.
        Assert.True(engine.Cancel(engine.Agenda.At(today, Turno.Tarde)!.Id));
        Assert.Null(engine.Schedule(new CommitmentRequest("entrevista", null, today, Turno.Tarde)).Failure);

        // Fora do prazo: passado, além da antecedência e do dia da eleição em diante.
        int ahead = engine.Context.Campaign.Commitments.Regras.AntecedenciaDias;
        Assert.Equal(ScheduleFailure.ForaDoPrazo, engine.Schedule(new CommitmentRequest("caminhada", home, today.AddDays(-1), Turno.Manha)).Failure);
        Assert.Equal(ScheduleFailure.ForaDoPrazo, engine.Schedule(new CommitmentRequest("caminhada", home, today.AddDays(ahead + 1), Turno.Manha)).Failure);
    }

    [Fact]
    public void Comicio_so_na_campanha()
    {
        CampaignEngine pre = Fresh(3106200, 64);
        CampaignState campaign = pre.Context.State.Campaign!;
        string home = pre.Territory.Zones[pre.Territory.HomeZone];
        Assert.Equal(CampaignPhase.PreCampanha, campaign.Phase);
        Assert.Equal(ScheduleFailure.FaseNaoPermite, pre.Schedule(new CommitmentRequest("comicio", home, pre.Today, Turno.Manha)).Failure);
        Assert.Equal(ScheduleFailure.FaseNaoPermite, pre.Forecast(new CommitmentRequest("comicio", home, pre.Today, Turno.Manha)).Blocked);
        Assert.Null(pre.Schedule(new CommitmentRequest("caminhada", home, pre.Today, Turno.Manha)).Failure);

        // Na semana em que a propaganda começa, o comício só cabe do dia 16 de agosto em diante.
        while (!pre.Context.State.Calendar.Contains(campaign.CampaignStart))
        {
            NextWeek(pre);
        }

        Assert.Equal(CampaignPhase.Campanha, campaign.Phase);
        if (pre.Today < campaign.CampaignStart)
        {
            Assert.Equal(ScheduleFailure.FaseNaoPermite, pre.Schedule(new CommitmentRequest("comicio", home, campaign.CampaignStart.AddDays(-1), Turno.Manha)).Failure);
        }

        pre.Context.State.Player.Resources.Money = 1_000_000;
        Assert.Null(pre.Schedule(new CommitmentRequest("comicio", home, campaign.CampaignStart, Turno.Manha)).Failure);
    }

    [Fact]
    public void Comicio_com_presenca_abaixo_de_vinte_esvazia()
    {
        CampaignEngine engine = Fresh(3106200, 65, campaign: true);
        AgendaRules rules = engine.Context.Campaign.Commitments.Regras;
        string zone = OtherZone(engine);
        int z = engine.Territory.IndexOf(zone);
        double[] presence = engine.Territory.Presence[RaceSetup.PlayerId];

        presence[z] = rules.ComicioPresencaMinima - 1;
        var empty = RunNow(engine, new CommitmentRequest("comicio", zone, engine.Today, Turno.Manha));
        Assert.Equal(rules.ComicioVazioFator, Assert.Single(empty.Forecast.Factors, f => f.Id == "comicio_vazio").Multiplier);
        Assert.Contains(empty.Result.Factors, f => f.Id == "comicio_vazio");

        // Com a presença no mínimo o comício enche; a diferença é o fator do comício vazio (o cansaço à parte).
        engine.Agenda.Fatigue.Clear();
        presence[z] = rules.ComicioPresencaMinima;
        NextWeek(engine);
        engine.Agenda.Fatigue.Clear();
        engine.Context.State.Player.Resources.Money = 1_000_000;
        presence[z] = rules.ComicioPresencaMinima;
        var full = RunNow(engine, new CommitmentRequest("comicio", zone, engine.Today, Turno.Manha));
        Assert.DoesNotContain(full.Forecast.Factors, f => f.Id == "comicio_vazio");
        Assert.Equal(rules.ComicioVazioFator, Yield(empty.Result) / Yield(full.Result), 9);
        Assert.True(full.Forecast.PresenceExpected > empty.Forecast.PresenceExpected * 2);
    }

    [Theory]
    [InlineData("metropole", true)]
    [InlineData("grande", true)]
    [InlineData("medio", false)]
    public void Deslocamento_entre_zonas_distantes_no_mesmo_dia_so_em_cidade_grande(string porte, bool applies)
    {
        // O primeiro município do porte que tem duas zonas com eleitores que não são vizinhas.
        int ibge = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == porte).OrderBy(m => m.CodigoIbge)
            .First(m => CampaignKit.Zonas(m).Zonas.Where(z => z.Eleitores is null or > 0).ToList() is { Count: >= 2 } zs
                && zs.Skip(1).Any(z => !zs[0].Vizinhas.Contains(z.Id) && !z.Vizinhas.Contains(zs[0].Id)))
            .CodigoIbge;
        CampaignEngine engine = Fresh(ibge, 74);
        AgendaRules rules = engine.Context.Campaign.Commitments.Regras;
        Assert.Equal(applies, rules.PortesComDeslocamento.Contains(engine.Context.Municipio.Porte));
        var zonas = engine.Context.Zonas.Zonas.Where((z, i) => engine.Context.ZoneElectorates[i].Voters > 0).ToList();
        Zona first = zonas[0];
        Zona far = zonas.First(z => z.Id != first.Id && !first.Vizinhas.Contains(z.Id) && !z.Vizinhas.Contains(first.Id));
        DateOnly today = engine.Today;
        engine.Agenda.Rain[today] = 0;

        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", first.Id, today, Turno.Manha)).Failure);
        CommitmentForecast forecast = engine.Forecast(new CommitmentRequest("caminhada", far.Id, today, Turno.Tarde));
        Assert.Equal(applies, forecast.Factors.Any(f => f.Id == "deslocamento"));
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", far.Id, today, Turno.Tarde)).Failure);

        // Na execução o fator é o mesmo da previsão, embora o primeiro compromisso já tenha acontecido.
        List<CommitmentResult> results = engine.RunToday();
        Assert.DoesNotContain(results[0].Factors, f => f.Id == "deslocamento");
        Assert.Equal(applies, results[1].Factors.Any(f => f.Id == "deslocamento"));
        Assert.Equal(forecast.Yield, Yield(results[1]), 9);
        if (applies)
        {
            Assert.Equal(1 - rules.DeslocamentoPerda, results[1].Factors.Single(f => f.Id == "deslocamento").Multiplier, 9);

            // Zona vizinha, ou a mesma zona, não perde.
            if (first.Vizinhas.FirstOrDefault(v => engine.Territory.IndexOf(v) >= 0) is { } near)
            {
                Assert.DoesNotContain(engine.Forecast(new CommitmentRequest("caminhada", near, today.AddDays(1), Turno.Tarde)).Factors, f => f.Id == "deslocamento");
            }
        }
    }

    [Fact]
    public void Presenca_decai_so_nas_zonas_sem_compromisso()
    {
        CampaignEngine engine = Fresh(4106902, 66);
        TerritoryState t = engine.Territory;
        TerritorySimulation sim = engine.Context.Campaign.Territory.Simulacao;
        double[] presence = t.Presence[RaceSetup.PlayerId];
        Array.Fill(presence, 30);
        string zone = OtherZone(engine);
        int z = t.IndexOf(zone);
        RunNow(engine, new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha));
        double[] before = [.. presence];

        engine.AdvanceWeek();

        Assert.Equal(0.6, sim.DecaimentoSemanal);
        Assert.Equal(before[z], presence[z], 9);
        for (int i = 0; i < presence.Length; i++)
        {
            if (i != z)
            {
                Assert.Equal(before[i] - sim.DecaimentoSemanal, presence[i], 9);
            }
        }

        // Na semana seguinte, sem compromisso, a zona visitada também decai; e a presença nunca fica negativa.
        engine.Context.State.Campaign!.Events.Pending.Clear();
        presence[(z + 1) % presence.Length] = 0.2;
        engine.AdvanceWeek();
        Assert.Equal(before[z] - sim.DecaimentoSemanal, presence[z], 9);
        Assert.Equal(0, presence[(z + 1) % presence.Length]);
        Assert.Equal(t.Recognition(RaceSetup.PlayerId), engine.Context.State.Campaign.Player.Recognition, 9);
    }

    [Fact]
    public void Quinze_por_cento_do_ganho_transborda_para_as_vizinhas()
    {
        CampaignEngine engine = Fresh(4106902, 67);
        TerritoryState t = engine.Territory;
        TerritorySimulation sim = engine.Context.Campaign.Territory.Simulacao;
        double[] presence = t.Presence[RaceSetup.PlayerId];
        Array.Fill(presence, 0);
        Zona zona = engine.Context.Zonas.Zonas.First(x => x.Vizinhas.Count >= 2 && engine.Context.ZoneElectorates[t.IndexOf(x.Id)].Voters > 0);

        var (_, result) = RunNow(engine, new CommitmentRequest("caminhada", zona.Id, engine.Today, Turno.Manha));

        Assert.Equal(0.15, sim.Transbordo);
        double gain = result.Presence[zona.Id];
        Assert.True(gain > 0);
        Assert.Equal(zona.Vizinhas.Order(), result.Presence.Keys.Where(k => k != zona.Id).Order());
        Assert.All(zona.Vizinhas, v => Assert.Equal(gain * sim.Transbordo / zona.Vizinhas.Count, result.Presence[v], 9));
        Assert.Equal(gain * sim.Transbordo, zona.Vizinhas.Sum(v => result.Presence[v]), 9);

        // Só a zona do compromisso e as vizinhas mudam, e a explicação bate com o território.
        for (int i = 0; i < presence.Length; i++)
        {
            Assert.Equal(result.Presence.GetValueOrDefault(t.Zones[i]), presence[i], 9);
        }
    }

    private static Invite AddInvite(CampaignEngine engine, Leader leader, DateOnly date, Turno turno)
    {
        var invite = new Invite
        {
            Id = $"cv{engine.Agenda.NextId++}",
            FromPersonId = leader.PersonId,
            Text = "Convite de teste",
            ZoneId = leader.Zones[0],
            Date = date,
            Turno = turno,
            Publico = leader.Publico,
        };
        engine.Agenda.Invites.Add(invite);
        return invite;
    }

    [Fact]
    public void Aceitar_convite_marca_no_dia_e_turno_dele_e_convites_no_mesmo_turno_conflitam()
    {
        CampaignEngine engine = Fresh(3106200, 68);
        PeopleConfig people = engine.Context.Campaign.People;
        LeaderRules rules = engine.Context.Campaign.Leaders.Regras;
        Leader a = engine.City.Leaders[0];
        Leader b = engine.City.Leaders[1];
        DateOnly day = engine.Today.AddDays(2);
        Invite first = AddInvite(engine, a, day, Turno.Noite);
        Invite second = AddInvite(engine, b, day, Turno.Noite);

        var (failure, commitment) = engine.AcceptInvite(first.Id);
        Assert.Null(failure);
        Assert.Equal(day, commitment!.Date);
        Assert.Equal(Turno.Noite, commitment.Turno);
        Assert.Equal(a.Zones[0], commitment.ZoneId);
        Assert.Equal(a.PersonId, commitment.PersonId);
        Assert.Equal(first.Id, commitment.InviteId);
        Assert.True(first.Answered);
        Assert.Same(commitment, engine.Agenda.At(day, Turno.Noite));
        Assert.Equal(rules.PontosConviteAceito, engine.Forecast(new CommitmentRequest("caminhada", a.Zones[0], day.AddDays(1), Turno.Noite, PersonId: a.PersonId, InviteId: first.Id)).RelationPoints);

        // O segundo convite é na mesma hora: aceitar um é recusar o outro.
        Assert.Equal(ScheduleFailure.TurnoOcupado, engine.AcceptInvite(second.Id).Failure);
        Assert.False(second.Answered);
        double relationB = engine.RelationOf(b.PersonId);
        engine.DeclineInvite(second.Id);
        Memory refused = Assert.Single(engine.City.Person(b.PersonId)!.Memories, m => m.Fact == "convite_recusado");
        Assert.True(refused.Pontos < 0 && refused.Peso < 0);
        Assert.False(string.IsNullOrWhiteSpace(refused.Text));
        Assert.Equal(relationB + rules.PontosConviteRecusado, engine.RelationOf(b.PersonId), 6);

        // No dia, o compromisso acontece e quem convidou lembra que o candidato foi.
        double relationA = engine.RelationOf(a.PersonId);
        var results = new List<CommitmentResult>();
        while (engine.Today <= day)
        {
            results.AddRange(engine.AdvanceDay());
        }

        CommitmentResult result = Assert.Single(results);
        Assert.Equal(rules.PontosConviteAceito, result.RelationPoints);
        Memory came = Assert.Single(engine.City.Person(a.PersonId)!.Memories, m => m.Fact == "convite_aceito");
        Assert.Equal(day, came.Date);
        Assert.True(came.Pontos > 0);
        Assert.True(engine.City.Person(a.PersonId)!.Relation(engine.Today, people) > relationA);
        Assert.Equal(day, engine.City.Person(a.PersonId)!.LastContact);
    }

    [Fact]
    public void Convite_que_vence_sem_resposta_e_convite_desmarcado_gravam_lembranca_negativa()
    {
        CampaignEngine engine = Fresh(3106200, 69);
        Leader a = engine.City.Leaders[0];
        Leader b = engine.City.Leaders[1];
        Invite ignored = AddInvite(engine, a, engine.Today.AddDays(2), Turno.Tarde);
        Invite dropped = AddInvite(engine, b, engine.Today.AddDays(3), Turno.Tarde);

        // Aceitar e depois desmarcar é faltar ao convite.
        string id = engine.AcceptInvite(dropped.Id).Commitment!.Id;
        Assert.True(engine.Cancel(id));
        Assert.Single(engine.City.Person(b.PersonId)!.Memories, m => m.Fact == "convite_recusado");
        Assert.True(dropped.Answered);
        Assert.Empty(engine.Agenda.Scheduled);

        // Deixar vencer: na virada da semana quem convidou lembra, uma vez só.
        Assert.Empty(engine.City.Person(a.PersonId)!.Memories);
        NextWeek(engine);
        Memory memory = Assert.Single(engine.City.Person(a.PersonId)!.Memories, m => m.Fact == "convite_recusado");
        Assert.True(memory.Pontos < 0);
        Assert.DoesNotContain(engine.Agenda.Invites, i => i.Id == ignored.Id || i.Id == dropped.Id);
        NextWeek(engine);
        Assert.Single(engine.City.Person(a.PersonId)!.Memories, m => m.Fact == "convite_recusado");
    }

    [Fact]
    public void Convites_chegam_sozinhos_com_dia_e_turno_da_semana_que_entra()
    {
        CampaignEngine engine = Fresh(3550308, 70);
        foreach (Leader l in engine.City.Leaders)
        {
            l.Known = true;
        }

        var seen = new List<Invite>();
        for (int week = 0; week < 12; week++)
        {
            NextWeek(engine);
            DateOnly monday = engine.Context.State.Calendar.WeekStart;
            foreach (Invite invite in engine.Agenda.Invites.Where(i => !seen.Contains(i)).ToList())
            {
                Assert.InRange(invite.Date, monday, monday.AddDays(6));
                Assert.Contains(engine.City.Leaders, l => l.PersonId == invite.FromPersonId && l.Zones[0] == invite.ZoneId);
                Assert.False(string.IsNullOrWhiteSpace(invite.Text));
                Assert.DoesNotContain("{", invite.Text);
                seen.Add(invite);
            }
        }

        Assert.NotEmpty(seen);
    }

    [Fact]
    public void Evento_da_cidade_so_existe_no_dia_e_na_zona_dele_e_rende_mais_com_o_publico()
    {
        CampaignEngine engine = Fresh(3106200, 71);
        AgendaRules rules = engine.Context.Campaign.Commitments.Regras;
        string zone = OtherZone(engine);
        string group = engine.Context.ElectorateContent.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).First(g => engine.Context.ZoneElectorates[engine.Territory.IndexOf(zone)].VotersIn(g) > 0);
        DateOnly today = engine.Today;
        engine.Agenda.CityEvents.Clear();
        engine.Agenda.CityEvents.Add(new CityEventInstance { Id = "ev_teste", TypeId = "feira", Nome = "Feira livre", ZoneId = zone, Date = today, Turno = Turno.Manha, Publico = group });

        string home = engine.Territory.Zones[engine.Territory.HomeZone];
        Assert.Equal(ScheduleFailure.EventoInvalido, engine.Schedule(new CommitmentRequest("evento_cidade", zone, today, Turno.Tarde, CityEventId: "ev_teste")).Failure);
        Assert.Equal(ScheduleFailure.EventoInvalido, engine.Schedule(new CommitmentRequest("evento_cidade", zone, today.AddDays(1), Turno.Manha, CityEventId: "ev_teste")).Failure);
        Assert.Equal(ScheduleFailure.EventoInvalido, engine.Schedule(new CommitmentRequest("evento_cidade", home, today, Turno.Manha, CityEventId: "ev_teste")).Failure);
        Assert.Equal(ScheduleFailure.EventoInvalido, engine.Schedule(new CommitmentRequest("evento_cidade", zone, today, Turno.Manha)).Failure);

        var (forecast, result) = RunNow(engine, new CommitmentRequest("evento_cidade", zone, today, Turno.Manha, CityEventId: "ev_teste"));
        Assert.Equal(1 + rules.EventoBonus, Assert.Single(forecast.Factors, f => f.Id == "evento").Multiplier, 9);
        Assert.True(result.Support > 0);
        Assert.Equal(result.Support, engine.Territory.Support[RaceSetup.PlayerId][engine.Territory.IndexOf(zone)][group], 9);

        // Passada a semana, o evento some da agenda.
        NextWeek(engine);
        Assert.DoesNotContain(engine.Agenda.CityEvents, e => e.Id == "ev_teste");
    }

    [Fact]
    public void Terceiro_turno_do_dia_custa_estresse_e_descanso_alivia()
    {
        CampaignEngine engine = Fresh(3106200, 72);
        AgendaRules rules = engine.Context.Campaign.Commitments.Regras;
        CommitmentCatalog catalog = engine.Context.Campaign.Commitments;
        DateOnly today = engine.Today;
        string home = engine.Territory.Zones[engine.Territory.HomeZone];
        string other = OtherZone(engine);
        engine.Context.State.Player.Resources.Stress = 20;

        Assert.Equal(catalog.Get("caminhada").Estresse, engine.Forecast(new CommitmentRequest("caminhada", home, today, Turno.Manha)).Stress);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", home, today, Turno.Manha)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", other, today, Turno.Tarde)).Failure);
        Assert.Equal(catalog.Get("entrevista").Estresse + rules.EstresseTurnoExtra, engine.Forecast(new CommitmentRequest("entrevista", null, today, Turno.Noite)).Stress);
        Assert.Null(engine.Schedule(new CommitmentRequest("entrevista", null, today, Turno.Noite)).Failure);

        List<CommitmentResult> results = engine.RunToday();
        Assert.Equal(3, results.Count);
        int expected = (2 * catalog.Get("caminhada").Estresse) + catalog.Get("entrevista").Estresse + rules.EstresseTurnoExtra;
        Assert.Equal(expected, results.Sum(r => r.Stress));
        Assert.Equal(20 + expected, engine.Context.State.Player.Resources.Stress);

        engine.AdvanceDay();
        int before = engine.Context.State.Player.Resources.Stress;
        var (forecast, _) = RunNow(engine, new CommitmentRequest(CommitmentCatalog.Descanso, null, engine.Today, Turno.Manha));
        Assert.True(forecast.Stress < 0);
        Assert.Equal(before + catalog.Get(CommitmentCatalog.Descanso).Estresse, engine.Context.State.Player.Resources.Stress);
    }

    [Fact]
    public void Semana_planejada_tem_chuva_por_dia_e_o_que_passou_sai_da_agenda()
    {
        CampaignEngine engine = Fresh(3106200, 73);
        AgendaRules rules = engine.Context.Campaign.Commitments.Regras;
        for (int week = 0; week < 10; week++)
        {
            DateOnly monday = engine.Context.State.Calendar.WeekStart;
            Assert.Equal(Enumerable.Range(0, 7).Select(monday.AddDays), engine.Agenda.Rain.Keys.Order());
            Assert.All(engine.Agenda.Rain.Values, v => Assert.True(v == 0 || (v >= rules.ChuvaPerda.Min && v <= rules.ChuvaPerda.Max)));
            Assert.All(engine.Agenda.CityEvents, e =>
            {
                Assert.InRange(e.Date, monday, monday.AddDays(6));
                Assert.Contains(e.ZoneId, engine.Territory.Zones);
            });
            Assert.Equal(engine.Agenda.CityEvents.Count, engine.Agenda.CityEvents.Select(e => e.Id).Distinct().Count());
            NextWeek(engine);
        }

        // Chuva em compromisso de rua aparece na previsão e não pesa nos outros.
        DateOnly today = engine.Today;
        engine.Agenda.Rain[today] = 0.25;
        string home = engine.Territory.Zones[engine.Territory.HomeZone];
        Assert.Equal(0.75, Assert.Single(engine.Forecast(new CommitmentRequest("caminhada", home, today, Turno.Manha)).Factors, f => f.Id == "chuva").Multiplier, 9);
        Assert.DoesNotContain(engine.Forecast(new CommitmentRequest("entrevista", null, today, Turno.Manha)).Factors, f => f.Id == "chuva");
    }
}
