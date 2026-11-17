using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Data;
using Planalto.Core.Governance;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Governance;
using Planalto.Core.Tests.Parties;
using Xunit;

namespace Planalto.Core.Tests.Career;

/// <summary>
/// Entressafra: a agenda fora de campanha de quem está sem cargo e sem eleição aberta (caminhada, visita a liderança,
/// entrevista e posição), com previsão, resultado explicado, cansaço e o que vai para a campanha seguinte.
/// </summary>
public class OffSeasonTests
{
    private static readonly OffSeasonConfig Config =
        JsonData.LoadFile<OffSeasonConfig>(Path.Combine(TestContent.DataDirectory, OffSeasonConfig.File)).GetValueOrThrow();

    private static OffSeasonContext Context(GameState state) => new()
    {
        State = state,
        Municipio = CampaignKit.Municipio(state.FocusMunicipio),
        Zonas = CampaignKit.Zonas(CampaignKit.Municipio(state.FocusMunicipio)),
        Config = Config,
        Memory = CampaignKit.C.Campaign.People,
        Themes = TestContent.World.Parties.Themes.Themes.ToDictionary(t => t.Id, t => t.Name),
    };

    /// <summary>Vereador cujo mandato acabou: sem cargo, sem campanha, com a cidade e o Livro que a carreira deixou.</summary>
    private static (GameState State, OffSeasonEngine Engine) OutOfOffice()
    {
        GameState state = GovKit.Councillor().State;
        state.Mandate = null;
        OffSeasonContext ctx = Context(state);
        Assert.NotNull(OffSeasonEngine.Ensure(ctx));
        return (state, new OffSeasonEngine(ctx));
    }

    [Fact]
    public void So_vale_sem_cargo_e_sem_campanha_e_parte_da_presenca_do_mandato()
    {
        GameState inOffice = GovKit.Councillor().State;
        Assert.False(OffSeasonEngine.Applies(inOffice));
        Assert.Null(OffSeasonEngine.Ensure(Context(inOffice)));

        var (state, engine) = OutOfOffice();
        Assert.True(OffSeasonEngine.Applies(state));
        Assert.Equal(state.Governance!.Zones, engine.S.Zones);
        Assert.Equal(state.Governance.Presence.Select(p => Math.Clamp(p, 0, 100)), engine.S.Presence);
        Assert.Equal(Config.TurnosPorSemana, engine.S.TurnsLeft);

        // Chamar de novo não recria nem zera nada.
        engine.S.Presence[0] = 12.5;
        Assert.Same(engine.S, OffSeasonEngine.Ensure(engine.Context));
        Assert.Equal(12.5, engine.S.Presence[0]);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Caminhada_tem_previsao_com_faixa_resultado_dentro_dela_e_cansaco_na_repeticao()
    {
        int inside = 0, runs = 0;
        for (ulong seed = 1; seed <= 40; seed++)
        {
            var (state, engine) = OutOfOffice();
            for (ulong skip = 0; skip < seed; skip++)
            {
                state.Rng.NextDouble();
            }

            string zone = engine.S.Zones[(int)(seed % (ulong)engine.S.Zones.Count)];
            int index = engine.S.IndexOf(zone);
            GovForecast forecast = engine.ForecastWalk(zone);
            Assert.Null(forecast.Blocked);
            ForecastLine line = forecast.Line(GovForecast.Presenca, zone)!;
            Assert.True(line.Min < line.Max && line.Min > 0);
            Assert.NotEmpty(forecast.Explanation);
            double before = engine.S.Presence[index];
            GovResult result = engine.Walk(zone);
            Assert.True(result.Ok);
            Assert.NotEmpty(result.Changes);
            runs++;
            if (forecast.Contains(GovForecast.Presenca, zone, engine.S.Presence[index] - before))
            {
                inside++;
            }
        }

        Assert.True(inside >= runs * 0.8, $"só {inside} de {runs} resultados dentro da faixa prevista.");

        // A terceira caminhada igual rende no máximo metade da primeira, e a previsão mostra o fator.
        var (_, tired) = OutOfOffice();
        string same = tired.S.Zones[0];
        double first = tired.ForecastWalk(same).Line(GovForecast.Presenca, same)!.Expected;
        Assert.True(tired.Walk(same).Ok);
        Assert.True(tired.Walk(same).Ok);
        GovForecast third = tired.ForecastWalk(same);
        Assert.True(third.Line(GovForecast.Presenca, same)!.Expected <= first * 0.5);
        Assert.Contains(third.Factors, f => f.Id == "cansaco" && f.Multiplier < 0.5);
        Assert.Contains(tired.S.Feed, f => f.Kind == OffSeasonEngine.Caminhada && f.ZoneId == same);
    }

    [Fact]
    public void Turnos_da_semana_acabam_com_motivo_e_voltam_na_segunda_com_a_presenca_caindo()
    {
        var (state, engine) = OutOfOffice();
        for (int i = 0; i < Config.TurnosPorSemana; i++)
        {
            Assert.True(engine.Walk(engine.S.Zones[i % engine.S.Zones.Count]).Ok);
        }

        GovForecast blocked = engine.ForecastWalk(engine.S.Zones[0]);
        Assert.Equal(GovFailure.SemTurno, blocked.Blocked);
        Assert.False(string.IsNullOrWhiteSpace(blocked.BlockedReason));
        Assert.Equal(GovFailure.SemTurno, engine.Walk(engine.S.Zones[0]).Failure);

        double before = engine.S.CityPresence();
        DateOnly week = state.Calendar.WeekStart;
        CareerFlow.AdvanceWeek(state, null, null, offSeason: engine);
        Assert.Equal(week.AddDays(7), state.Calendar.WeekStart);
        Assert.Equal(Config.TurnosPorSemana, engine.S.TurnsLeft);
        Assert.Equal(before * (1 - Config.DecaimentoSemanal), engine.S.CityPresence(), 6);
        Assert.Equal((int)Math.Round(engine.S.CityPresence()), state.Player.Resources.GetRecognition(state.FocusMunicipio.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        // Semana sem nenhuma ação vira notícia.
        CareerFlow.AdvanceWeek(state, null, null, offSeason: engine);
        Assert.Contains(engine.S.Feed, f => f.Kind == "semana");
    }

    [Fact]
    public void Visita_a_lideranca_grava_lembranca_e_nao_se_repete_antes_do_intervalo()
    {
        var (state, engine) = OutOfOffice();
        Leader leader = state.City!.Leaders.First();
        leader.Known = true;
        Person person = state.City.Person(leader.PersonId)!;
        person.LastContact = null;
        double before = engine.RelationOf(leader.PersonId);
        GovForecast forecast = engine.ForecastVisit(leader.PersonId);
        Assert.Null(forecast.Blocked);
        Assert.Contains(engine.NameOf(leader.PersonId), forecast.Remembered);
        GovResult result = engine.Visit(leader.PersonId);
        Assert.True(result.Ok);
        Assert.Contains(person.Memories, m => m.Fact == "visita_entressafra");
        Assert.True(engine.RelationOf(leader.PersonId) > before || before >= Person.MaxRelation);
        GovForecast again = engine.ForecastVisit(leader.PersonId);
        Assert.Equal(GovFailure.JaFeito, again.Blocked);
        Assert.Contains(engine.NameOf(leader.PersonId), again.BlockedReason);

        // Liderança que o jogador não conhece não recebe visita.
        if (state.City.Leaders.FirstOrDefault(l => l.PersonId != leader.PersonId) is { } other)
        {
            other.Known = false;
            Assert.Equal(GovFailure.AlvoInvalido, engine.ForecastVisit(other.PersonId).Blocked);
        }
    }

    [Fact]
    public void Posicao_vai_para_o_Livro_e_a_entrevista_so_fala_de_tema_com_posicao_assumida()
    {
        var (state, engine) = OutOfOffice();
        string theme = engine.Context.Themes.Keys.Order(StringComparer.Ordinal).First();
        int positions = state.Ledger.Positions.Count;
        Assert.Equal(GovFailure.Bloqueado, engine.ForecastInterview(theme).Blocked);
        Assert.Equal(GovFailure.AlvoInvalido, engine.ForecastPosition("tema_que_nao_existe", 1).Blocked);
        Assert.Equal(GovFailure.AlvoInvalido, engine.ForecastPosition(theme, 3).Blocked);

        Assert.True(engine.TakePosition(theme, 2).Ok);
        Assert.Equal(2, engine.S.Positions[theme]);
        Assert.Equal(positions + 1, state.Ledger.Positions.Count);
        Assert.Equal(new DateOnly(state.Calendar.WeekStart.Year, state.Calendar.WeekStart.Month, state.Calendar.WeekStart.Day).AddDays(state.Time.Day), state.Ledger.Positions[^1].Date);
        Assert.Equal(GovFailure.JaFeito, engine.ForecastPosition(theme, 2).Blocked);

        // Mudar o que já foi dito: a previsão avisa que as duas posições ficam no Livro.
        GovForecast change = engine.ForecastPosition(theme, -1);
        Assert.Null(change.Blocked);
        Assert.Equal(2, change.Explanation.Count);

        double known = engine.S.Known[theme];
        GovForecast interview = engine.ForecastInterview(theme);
        Assert.Null(interview.Blocked);
        double city = engine.S.CityPresence();
        Assert.True(engine.Interview(theme).Ok);
        Assert.True(engine.S.Known[theme] > known);
        Assert.True(engine.S.CityPresence() >= city);
        Assert.Equal(positions + 2, state.Ledger.Positions.Count);
    }

    [Fact]
    public void Na_abertura_da_campanha_a_presenca_e_as_posicoes_passam_para_ela()
    {
        var (state, engine) = OutOfOffice();
        string theme = engine.Context.Themes.Keys.Order(StringComparer.Ordinal).First();
        Assert.True(engine.TakePosition(theme, -2).Ok);
        Assert.True(engine.Walk(engine.S.Zones[0]).Ok);
        List<double> presence = engine.S.Presence.ToList();
        double known = engine.S.Known[theme];

        // Salvar e carregar no meio não perde a entressafra.
        GameState loaded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        Assert.Equal(presence, loaded.OffSeason!.Presence);
        Assert.Equal(-2, loaded.OffSeason.Positions[theme]);

        state.Calendar.JumpTo(CareerFlow.CampaignOpens(CareerFlow.NextMunicipalElectionYear(state)));
        CampaignEngine.Start(CampaignKit.Context(state));
        OffSeasonEngine.CarryToCampaign(state);
        Assert.Null(state.OffSeason);
        Assert.Equal(presence, state.Campaign!.Territory.Presence[RaceSetup.PlayerId]);
        Assert.Equal(-2, state.Campaign!.Platform.Positions[theme]);
        Assert.Equal(known, state.Campaign!.Platform.Known[theme]);
        Assert.Equal(state.Campaign!.Territory.Recognition(RaceSetup.PlayerId), state.Campaign!.Player.Recognition);
    }

    [Fact]
    public void Arquivo_de_dados_valida_e_falta_de_texto_reprova()
    {
        Assert.Empty(Config.Validate());
        Assert.All(OffSeasonConfig.TextKeys, k => Assert.False(string.IsNullOrWhiteSpace(Config.Texto(k))));
    }
}
