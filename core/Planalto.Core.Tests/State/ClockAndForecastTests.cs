using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Events;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.State;

/// <summary>Tempo corrente (GDD 17.4) e previsão antes do clique (GDD 17.1, f).</summary>
public class ClockAndForecastTests
{
    private static TimeConfig Time => JsonData.LoadFile<TimeConfig>(Path.Combine(TestContent.DataDirectory, TimeConfig.File)).GetValueOrThrow();

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Sete_dias_produzem_o_mesmo_estado_que_uma_semana()
    {
        // Sem compromisso marcado, sete chamadas de AdvanceDay são uma semana do motor: mesma semente, mesmo estado.
        CampaignEngine byWeek = CampaignKit.NewCareer(3106200, 31);
        CampaignEngine byDay = CampaignKit.NewCareer(3106200, 31);
        Assert.Equal(GameStateSerializer.Serialize(byWeek.Context.State), GameStateSerializer.Serialize(byDay.Context.State));
        for (int week = 0; week < 12; week++)
        {
            foreach (CampaignEngine engine in new[] { byWeek, byDay })
            {
                engine.Context.State.Campaign!.Events.Pending.Clear();
                engine.Context.State.Campaign.Debate = null;
            }

            byWeek.AdvanceWeek();
            DateOnly monday = byDay.Context.State.Calendar.WeekStart;
            for (int day = 0; day < 7; day++)
            {
                Assert.Equal(monday.AddDays(day), byDay.Today);
                Assert.Empty(byDay.AdvanceDay());
            }

            Assert.Equal(monday.AddDays(7), byDay.Context.State.Calendar.WeekStart);
            Assert.Equal(0, byDay.Context.State.Time.Day);
            Assert.Equal(GameStateSerializer.Serialize(byWeek.Context.State), GameStateSerializer.Serialize(byDay.Context.State));
        }
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Semana_fechada_no_meio_executa_o_que_resta_na_data_de_cada_compromisso()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3106200, 36);
        GameState state = engine.Context.State;
        state.Campaign!.Events.Pending.Clear();
        string zone = engine.Territory.Zones[0];
        DateOnly monday = state.Calendar.WeekStart;
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, monday.AddDays(1), Turno.Manha)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, monday.AddDays(4), Turno.Tarde)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, monday.AddDays(9), Turno.Tarde)).Failure);

        // Segunda sem compromisso; na terça acontece o primeiro.
        Assert.Empty(engine.AdvanceDay());
        Assert.Single(engine.AdvanceDay());
        engine.AdvanceWeek();

        Assert.Equal(monday.AddDays(7), state.Calendar.WeekStart);
        Assert.Equal(0, state.Time.Day);
        Assert.Equal(2, engine.Agenda.Log.Count);
        Assert.Equal([monday.AddDays(1), monday.AddDays(4)], engine.Agenda.Feed.Where(f => f.Kind == "compromisso").Select(f => f.Date));
        Assert.Equal(monday.AddDays(9), Assert.Single(engine.Agenda.Scheduled).Date);
    }

    [Fact]
    public void Dia_anda_dentro_da_semana_e_hoje_acompanha()
    {
        GameState state = CampaignKit.NewCareer(3106200, 32).Context.State;
        DateOnly monday = state.Calendar.WeekStart;
        Assert.False(Clock.AdvanceDay(state, () => throw new InvalidOperationException("não é domingo")));
        Assert.Equal(monday.AddDays(1), Clock.Today(state));
        Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);
    }

    [Fact]
    public void Pausas_respeitam_o_padrao_e_so_as_desligaveis_aceitam_escolha()
    {
        TimeConfig config = Time;
        Assert.Empty(config.Validate());
        var off = Clock.PauseIds.ToDictionary(id => id, _ => false);
        Assert.True(Clock.ShouldPause(config, Clock.DecisaoImediata, off));
        Assert.True(Clock.ShouldPause(config, Clock.FaseNova, off));
        Assert.False(Clock.ShouldPause(config, Clock.PesquisaPublicada, off));
        Assert.True(Clock.ShouldPause(config, Clock.PesquisaPublicada, null));
        Assert.False(Clock.ShouldPause(config, Clock.NoticiaComum, null));
        Assert.Equal(1.0, Clock.SecondsPerDay(config, Clock.ModoCampanha, 4));
        Assert.Equal(7.0, 7 * Clock.SecondsPerDay(config, Clock.ModoCampanha, 4), 3);
    }

    /// <summary>Leva a campanha, sem compromissos, até a propaganda liberada (todo tipo de compromisso pode ser marcado).</summary>
    internal static CampaignEngine InCampaign(int ibge, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed);
        CampaignState campaign = engine.Context.State.Campaign!;
        while (engine.Today < campaign.CampaignStart)
        {
            campaign.Events.Pending.Clear();
            campaign.Debate = null;
            engine.AdvanceWeek();
        }

        campaign.Events.Pending.Clear();
        campaign.Debate = null;
        Assert.Equal(CampaignPhase.Campanha, campaign.Phase);
        return engine;
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Resultado_de_cada_compromisso_cai_na_faixa_prevista_em_mil_execucoes()
    {
        CampaignEngine engine = InCampaign(2927408, 33);
        CampaignKit.FundRua(engine);
        GameState state = engine.Context.State;
        CampaignState campaign = state.Campaign!;
        var types = engine.Context.Campaign.Commitments.Tipos;
        List<string> zones = engine.Territory.Zones;
        List<string> groups = engine.Context.ElectorateContent.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToList();
        foreach (Leader l in engine.City.Leaders)
        {
            l.Known = true;
            engine.City.Person(l.PersonId)!.Affinity = 60;
        }

        DateOnly today = engine.Today;
        var used = new HashSet<string>(StringComparer.Ordinal);
        int inside = 0;
        for (int i = 0; i < 1_000; i++)
        {
            // Estado de partida variado: presença baixa, média e alta, com e sem chuva, com e sem cansaço acumulado.
            CommitmentType type = types[i % types.Count];
            string zone = zones[i / types.Count % zones.Count];
            int z = engine.Territory.IndexOf(zone);
            double[] presence = engine.Territory.Presence[RaceSetup.PlayerId];
            Array.Fill(presence, new[] { 5.0, 30.0, 70.0 }[i % 3]);
            campaign.Player.Recognition = engine.Territory.Recognition(RaceSetup.PlayerId);
            campaign.Player.Image = 0;
            state.Player.Resources.Money = 1_000_000;
            state.Player.Resources.Stress = i % 5 == 0 ? 90 : 0;
            engine.Agenda.Rain[today] = i % 4 == 0 ? 0.3 : 0;
            engine.Agenda.Log.Clear();
            if (i % 40 == 0)
            {
                engine.Agenda.Fatigue.Clear();
            }

            Turno turno = (Turno)(i % (4 - type.Turnos));
            if (engine.Context.ZoneElectorates[z].Voters <= 0)
            {
                // Zona sem eleitores (sem local de votação): os compromissos com público não cabem; usa a zona natal.
                z = engine.Territory.HomeZone;
                zone = zones[z];
            }

            string publico = CommitmentType.PublicoGeral;
            if (type.EscolhePublico)
            {
                // Reunião exige uma liderança conhecida daquele público na zona: usa a zona e o público de uma delas.
                List<Leader> hosts = engine.City.Leaders.Where(l => engine.Context.ZoneElectorates[engine.Territory.IndexOf(l.Zones[0])].VotersIn(l.Publico) > 0).ToList();
                Leader host = hosts[i / types.Count % hosts.Count];
                zone = host.Zones[0];
                z = engine.Territory.IndexOf(zone);
                publico = host.Publico;
            }

            string? cityEvent = null;
            if (type.SoEvento)
            {
                var instance = new CityEventInstance { Id = $"teste{i}", TypeId = "feira", Nome = "Feira livre", ZoneId = zone, Date = today, Turno = turno, Publico = i % 2 == 0 ? CommitmentType.PublicoGeral : groups[i % groups.Count] };
                engine.Agenda.CityEvents.Add(instance);
                cityEvent = instance.Id;
            }

            var request = new CommitmentRequest(
                type.Id, type.ExigeZona ? zone : null, today, turno, publico,
                type.Visita ? engine.City.Leaders[i % engine.City.Leaders.Count].PersonId : null, cityEvent);

            CommitmentForecast forecast = engine.Forecast(request);
            Assert.True(forecast.Blocked is null, $"{type.Id}: {forecast.Blocked}");
            double before = type.ExigeZona ? presence[z] : campaign.Player.Recognition;
            decimal money = state.Player.Resources.Money;
            int stress = state.Player.Resources.Stress;
            Assert.Null(engine.Schedule(request).Failure);
            CommitmentResult result = Assert.Single(engine.RunToday());
            if (engine.Agenda.PendingRequest is not null)
            {
                engine.AnswerLeader(LeaderAnswer.NaoSeComprometer);
            }

            const double Eps = 1e-9;
            double delta = (type.ExigeZona ? presence[z] : campaign.Player.Recognition) - before;
            bool ok = delta >= forecast.PresenceMin - Eps && delta <= forecast.PresenceMax + Eps
                && result.Support >= forecast.SupportMin - Eps && result.Support <= forecast.SupportMax + Eps
                && campaign.Player.Image >= forecast.ImageMin - Eps && campaign.Player.Image <= forecast.ImageMax + Eps
                && state.Player.Resources.Money - money >= forecast.MoneyMin && state.Player.Resources.Money - money <= forecast.MoneyMax
                && Math.Clamp(stress + forecast.Stress, 0, 100) == state.Player.Resources.Stress
                && result.RelationPoints == forecast.RelationPoints
                && Math.Abs(forecast.Yield - result.Factors.Where(f => f.Id != "sorte").Aggregate(1.0, (acc, f) => acc * f.Multiplier)) < Eps;
            Assert.True(ok, $"{type.Id} (rodada {i}): presença {delta} em [{forecast.PresenceMin}, {forecast.PresenceMax}], apoio {result.Support}, imagem {campaign.Player.Image}, dinheiro {state.Player.Resources.Money - money}, estresse {state.Player.Resources.Stress - stress} (previsto {forecast.Stress})");
            inside += ok ? 1 : 0;
            used.Add(type.Id);

            // Explicação nunca vazia: todo resultado diz os fatores (ao menos a sorte do dia).
            Assert.Contains(result.Factors, f => f.Id == "sorte");
            Assert.All(result.Factors, f => Assert.False(string.IsNullOrWhiteSpace(f.Text)));
        }

        Assert.True(inside >= 800, $"{inside} de 1000 dentro da faixa");
        Assert.Equal(1_000, inside);
        Assert.Equal(types.Select(t => t.Id).Order(), used.Order());
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Previsao_mostra_os_desfechos_sorteados_com_a_chance()
    {
        CampaignEngine engine = InCampaign(2927408, 34);
        GameState state = engine.Context.State;
        state.Player.Resources.Money = 1_000_000;
        state.Player.Resources.Stress = 0;
        DateOnly today = engine.Today;

        // Entrevista: teste de Oratória, com a chance de cada desfecho e a imagem podendo subir ou cair.
        CommitmentForecast entrevista = engine.Forecast(new CommitmentRequest("entrevista", null, today, Turno.Manha));
        double chance = Math.Clamp(state.Player.Attributes.Get(Planalto.Core.Characters.AttributeId.Oratoria) / 100.0, 0.05, 0.95);
        Assert.Equal(2, entrevista.Outcomes.Count);
        Assert.Contains(entrevista.Outcomes, o => Math.Abs(o.Probability - chance) < 1e-9);
        Assert.Equal(1, entrevista.Outcomes.Sum(o => o.Probability), 9);
        Assert.True(entrevista.ImageMin < 0 && entrevista.ImageMax > 0, "a entrevista pode render ou custar imagem");
        Assert.True(entrevista.PresenceMin > 0 && entrevista.PresenceMin < entrevista.PresenceExpected && entrevista.PresenceExpected < entrevista.PresenceMax);

        // Estresse acima do limite derruba a chance do teste, e a previsão mostra.
        state.Player.Resources.Stress = 95;
        CommitmentForecast cansado = engine.Forecast(new CommitmentRequest("entrevista", null, today, Turno.Manha));
        Assert.Contains(cansado.Outcomes, o => Math.Abs(o.Probability - Math.Clamp(chance - engine.Context.Campaign.Commitments.Regras.EstressePenalidade, 0.05, 0.95)) < 1e-9);
        state.Player.Resources.Stress = 0;

        // Gravação custa dinheiro; jantar arrecada; o comício não custa no clique, mas exige verba de Rua na zona.
        string zone = engine.Territory.Zones[engine.Territory.HomeZone];
        Assert.True(engine.Forecast(new CommitmentRequest("gravacao", null, today, Turno.Manha)).MoneyMax < 0);
        Assert.Equal(ScheduleFailure.SemVerbaDeRua, engine.Forecast(new CommitmentRequest("comicio", zone, today, Turno.Manha)).Blocked);
        CampaignKit.FundRua(engine);
        Assert.Null(engine.Forecast(new CommitmentRequest("comicio", zone, today, Turno.Manha)).Blocked);
        Assert.True(engine.Forecast(new CommitmentRequest("jantar", null, today, Turno.Noite)).MoneyMin > 0);

        // Caminhada em zona com liderança desconhecida: a previsão avisa da chance de conhecê-la.
        Leader hidden = engine.City.Leaders.First();
        hidden.Known = false;
        CommitmentForecast caminhada = engine.Forecast(new CommitmentRequest("caminhada", hidden.Zones[0], today, Turno.Tarde));
        Assert.Contains(caminhada.Outcomes, o => Math.Abs(o.Probability - engine.Context.Campaign.Leaders.Regras.ChanceDescoberta) < 1e-9);

        // Bloqueio com motivo: comício não cabe à noite (ocupa dois turnos).
        Assert.Equal(ScheduleFailure.TurnoOcupado, engine.Forecast(new CommitmentRequest("comicio", zone, today, Turno.Noite)).Blocked);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Escolha_de_evento_cai_na_faixa_prevista()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3550308, 35);
        CampaignContext ctx = engine.Context;
        foreach (EventDefinition e in CampaignKit.C.Events.Events)
        {
            for (int choice = 0; choice < e.Choices.Count; choice++)
            {
                ctx.State.Campaign!.Events.Pending.Clear();
                ctx.State.Player.Resources.Money = 1_000_000;
                ctx.State.Player.Resources.PoliticalCapital = 100;
                PendingEvent pending = EventEngine.Trigger(ctx, e.Id, ctx.State.Rng);
                if (EventEngine.Blocked(ctx, e.Choices[choice]) is not null)
                {
                    continue;
                }

                Forecast forecast = Forecaster.ForChoice(ctx, e.Choices[choice]);
                Candidate p = ctx.State.Campaign.Player;

                // Presença desigual entre as zonas (algumas perto do piso): a perda de reconhecimento não passa do
                // que cada zona tem, e a previsão tem de contar com isso.
                double[] presence = ctx.State.Campaign.Territory.Presence[p.Id];
                for (int z = 0; z < presence.Length; z++)
                {
                    presence[z] = z % 3 == 0 ? 60 : z % 3 == 1 ? 1.5 : 0;
                }

                p.Recognition = ctx.State.Campaign.Territory.Recognition(p.Id);
                p.Image = 0;
                forecast = Forecaster.ForChoice(ctx, e.Choices[choice], pending);
                double recognition = p.Recognition, image = p.Image;
                decimal money = ctx.State.Player.Resources.Money;
                EventEngine.Resolve(ctx, pending, choice, ctx.State.Rng);
                Assert.True(forecast.Contains("recognition", null, p.Recognition - recognition), $"{e.Id}/{choice}: reconhecimento");
                Assert.True(forecast.Contains("image", null, p.Image - image), $"{e.Id}/{choice}: imagem");
                Assert.True(forecast.Contains("money", null, (double)(ctx.State.Player.Resources.Money - money)), $"{e.Id}/{choice}: dinheiro");
            }
        }
    }
}
