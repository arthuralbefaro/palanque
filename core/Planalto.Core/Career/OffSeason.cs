using System.Globalization;
using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Governance;
using Planalto.Core.Municipios;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Career;

/// <summary>Uma ação da agenda fora de campanha: quantos turnos gasta e quanta presença rende.</summary>
public sealed class OffSeasonAction
{
    public required int Turnos { get; init; }

    public required double Presenca { get; init; }
}

/// <summary>data/career/offseason.json: números de jogo da entressafra (fora de mandato e fora de campanha).</summary>
public sealed class OffSeasonConfig : IValidatable
{
    public const string File = "career/offseason.json";

    public static readonly IReadOnlyList<string> TextKeys =
    [
        "sem_turno", "zona_invalida", "pessoa_invalida", "visita_recente", "tema_invalido", "sem_posicao", "mesma_posicao", "valor_invalido",
        "previsao_caminhada", "previsao_vizinhas", "previsao_visita", "previsao_entrevista", "previsao_entrevista_tema", "previsao_posicao", "previsao_posicao_mudou",
        "previsao_decaimento", "fator_cansaco", "fator_teto", "resultado_caminhada", "resultado_visita", "resultado_entrevista", "resultado_entrevista_tema",
        "resultado_posicao", "mudou_presenca", "mudou_presenca_cidade", "mudou_relacao", "mudou_conhecida", "mudou_livro", "lembranca_visita", "onde_declaracao",
        "onde_entrevista", "descoberta", "semana_parada",
    ];

    public required string Note { get; init; }

    public required int TurnosPorSemana { get; init; }

    public required OffSeasonAction Caminhada { get; init; }

    public required OffSeasonAction Visita { get; init; }

    public required OffSeasonAction Entrevista { get; init; }

    public required OffSeasonAction Posicao { get; init; }

    public required double VizinhaFracao { get; init; }

    public required double DecaimentoSemanal { get; init; }

    public required double CansacoPorUso { get; init; }

    public required double CansacoRecuperacao { get; init; }

    public required double Incerteza { get; init; }

    public required int RelacaoVisita { get; init; }

    public required int PesoVisita { get; init; }

    public required int VisitaIntervaloSemanas { get; init; }

    public required double ConhecidaPorEntrevista { get; init; }

    public required double ConhecidaPorPosicao { get; init; }

    /// <summary>Chance de a caminhada revelar uma liderança da zona que o jogador ainda não conhece.</summary>
    public required double ChanceDescoberta { get; init; }

    public required int NoticiasMaximo { get; init; }

    public required Dictionary<string, string> Textos { get; init; }

    public string Texto(string key) => Textos.TryGetValue(key, out string? text) ? text : key;

    public IEnumerable<string> Validate()
    {
        if (TurnosPorSemana is < 1 or > 21)
        {
            yield return "turnos_por_semana: de 1 a 21.";
        }

        foreach (var (name, action) in new[] { ("caminhada", Caminhada), ("visita", Visita), ("entrevista", Entrevista), ("posicao", Posicao) })
        {
            if (action.Turnos < 1 || action.Turnos > TurnosPorSemana || action.Presenca is < 0 or > 20)
            {
                yield return $"{name}: turnos de 1 ao total da semana e presença de 0 a 20.";
            }
        }

        if (VizinhaFracao is < 0 or > 1 || DecaimentoSemanal is < 0 or >= 1 || CansacoPorUso < 0 || CansacoRecuperacao is < 0 or > 1 || Incerteza is < 0 or >= 1)
        {
            yield return "vizinha_fracao, decaimento_semanal, cansaco e incerteza fora da faixa.";
        }

        if (PesoVisita is < 1 or > 3 || RelacaoVisita < 0 || VisitaIntervaloSemanas < 1 || NoticiasMaximo < 10)
        {
            yield return "visita: peso de 1 a 3, relação não negativa, intervalo de pelo menos 1 semana; noticias_maximo de pelo menos 10.";
        }

        if (ConhecidaPorEntrevista is < 0 or > 1 || ConhecidaPorPosicao is < 0 or > 1 || ChanceDescoberta is < 0 or > 1)
        {
            yield return "conhecida_por_entrevista, conhecida_por_posicao e chance_descoberta: de 0 a 1.";
        }

        foreach (string key in TextKeys.Where(k => !Textos.TryGetValue(k, out string? t) || string.IsNullOrWhiteSpace(t)))
        {
            yield return $"textos: falta '{key}'.";
        }
    }
}

/// <summary>
/// Entressafra no save: a presença do jogador por zona enquanto ele está sem cargo e sem eleição aberta, os turnos da
/// semana, o cansaço e as posições assumidas. Ao abrir a campanha, vai para o território e a plataforma dela.
/// </summary>
public sealed class OffSeasonState
{
    public required int Municipio { get; init; }

    public required DateOnly Since { get; init; }

    public required List<string> Zones { get; init; }

    public required List<long> ZonePopulation { get; init; }

    /// <summary>Presença do jogador em cada zona (0 a 100).</summary>
    public required List<double> Presence { get; init; }

    public int TurnsLeft { get; set; }

    /// <summary>Quantas ações o jogador fez nesta semana (semana parada vira notícia).</summary>
    public int ActionsThisWeek { get; set; }

    /// <summary>Cansaço por "ação|alvo".</summary>
    public Dictionary<string, double> Fatigue { get; init; } = [];

    /// <summary>Posições assumidas fora de campanha (−2 a 2).</summary>
    public Dictionary<string, int> Positions { get; init; } = [];

    /// <summary>Quanto da cidade conhece cada posição (0 a 1).</summary>
    public Dictionary<string, double> Known { get; init; } = [];

    public List<FeedItem> Feed { get; init; } = [];

    public int IndexOf(string zoneId) => Zones.IndexOf(zoneId);

    /// <summary>Presença na cidade: média das zonas pela população.</summary>
    public double CityPresence()
    {
        double total = ZonePopulation.Sum();
        return total <= 0 ? Presence.DefaultIfEmpty(0).Average() : Presence.Select((p, z) => p * ZonePopulation[z]).Sum() / total;
    }
}

public sealed class OffSeasonContext
{
    public required GameState State { get; init; }

    public required Municipio Municipio { get; init; }

    public required ZonasMunicipio Zonas { get; init; }

    public required OffSeasonConfig Config { get; init; }

    public required PeopleConfig Memory { get; init; }

    /// <summary>Temas da plataforma (id → nome).</summary>
    public required IReadOnlyDictionary<string, string> Themes { get; init; }
}

/// <summary>
/// Agenda fora de campanha (GDD 17.3 e 17.6, pendência da entressafra): sem cargo e sem eleição aberta, o jogador
/// mantém a presença nas zonas (caminhada), visita lideranças, dá entrevistas e assume posições. Não há verba de canal,
/// equipe paga nem pedido de voto. Todo comando tem previsão com faixa e resultado explicado; repetir cansa.
/// </summary>
public sealed class OffSeasonEngine(OffSeasonContext ctx)
{
    public const string Caminhada = "caminhada";
    public const string Visita = "visita";
    public const string Entrevista = "entrevista";
    public const string Posicao = "posicao";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public OffSeasonContext Context { get; } = ctx;

    private GameState State => Context.State;

    private OffSeasonConfig Config => Context.Config;

    public OffSeasonState S => State.OffSeason ?? throw new InvalidOperationException("Fora da entressafra.");

    private DateOnly Today => Clock.Today(State);

    /// <summary>Sem mandato, sem prefeitura e sem campanha aberta.</summary>
    public static bool Applies(GameState state) => state.Campaign is null && state.Mandate is null && state.Executive is null;

    /// <summary>
    /// Garante o estado da entressafra quando ela vale e o retira quando deixa de valer. A presença de partida é a do
    /// mandato de vereador que acabou, zona a zona; sem isso, o reconhecimento na cidade, igual em todas as zonas.
    /// </summary>
    public static OffSeasonState? Ensure(OffSeasonContext ctx)
    {
        GameState state = ctx.State;
        if (!Applies(state))
        {
            return state.OffSeason;
        }

        if (state.OffSeason is { } current && current.Municipio == ctx.Municipio.CodigoIbge && current.Zones.Count == ctx.Zonas.Zonas.Count)
        {
            return current;
        }

        List<string> zones = ctx.Zonas.Zonas.Select(z => z.Id).ToList();
        double recognition = state.Player.Resources.GetRecognition(ctx.Municipio.CodigoIbge.ToString(CultureInfo.InvariantCulture));
        List<double> presence = state.Governance is { } g && g.Municipio == ctx.Municipio.CodigoIbge && g.Zones.SequenceEqual(zones)
            ? g.Presence.Select(p => Math.Clamp(p, 0, 100)).ToList()
            : zones.Select(_ => Math.Clamp(recognition, 0, 100)).ToList();
        state.OffSeason = new OffSeasonState
        {
            Municipio = ctx.Municipio.CodigoIbge,
            Since = state.Calendar.WeekStart,
            Zones = zones,
            ZonePopulation = ctx.Zonas.Zonas.Select(z => z.Populacao).ToList(),
            Presence = presence,
            TurnsLeft = ctx.Config.TurnosPorSemana,
        };
        return state.OffSeason;
    }

    /// <summary>
    /// A campanha abriu: a presença por zona e as posições assumidas na entressafra passam ao território e à plataforma
    /// dela, e o estado da entressafra sai do save.
    /// </summary>
    public static void CarryToCampaign(GameState state)
    {
        if (state.OffSeason is not { } s || state.Campaign is not { } campaign)
        {
            return;
        }

        if (campaign.Municipio == s.Municipio && campaign.Territory.Presence.TryGetValue(RaceSetup.PlayerId, out double[]? presence) && presence.Length == s.Presence.Count)
        {
            for (int z = 0; z < presence.Length; z++)
            {
                presence[z] = Math.Clamp(s.Presence[z], 0, 100);
            }

            campaign.Player.Recognition = campaign.Territory.Recognition(RaceSetup.PlayerId);
            foreach (var (theme, value) in s.Positions)
            {
                campaign.Platform.Positions[theme] = value;
                campaign.Platform.Known[theme] = Math.Max(campaign.Platform.Known.GetValueOrDefault(theme), s.Known.GetValueOrDefault(theme));
            }
        }

        state.OffSeason = null;
    }

    // ---------------------------------------------------------------- leitura

    public string ZoneName(string? zoneId) => Context.Zonas.Zonas.FirstOrDefault(z => z.Id == zoneId)?.Nome ?? "";

    public string NameOf(string personId) => State.Npcs.FirstOrDefault(n => n.Id == personId)?.BallotName ?? personId;

    public string ThemeName(string theme) => Context.Themes.GetValueOrDefault(theme, theme);

    public double RelationOf(string personId) => State.City?.Person(personId)?.Relation(Today, Context.Memory) ?? 0;

    /// <summary>Lideranças conhecidas que seguem na cidade (quem morreu ou se mudou não recebe visita).</summary>
    public IEnumerable<Leader> Leaders =>
        (State.City?.Leaders ?? []).Where(l => l.Known && State.Renewal?.Status(l.PersonId)?.Saida is null);

    /// <summary>Quanto rende a próxima ação igual no mesmo alvo: 1 ÷ (1 + cansaço).</summary>
    public double FatigueYield(string action, string? target) => 1 / (1 + S.Fatigue.GetValueOrDefault(Key(action, target)));

    private static string Key(string action, string? target) => $"{action}|{target}";

    private OffSeasonAction ActionOf(string id) => id switch
    {
        Caminhada => Config.Caminhada,
        Visita => Config.Visita,
        Entrevista => Config.Entrevista,
        _ => Config.Posicao,
    };

    private string T(string key, params (string Key, string Value)[] values) => Texts.Fill(Config.Texto(key), values);

    private static string Num(double value, string format = "0.0") => value.ToString(format, PtBr);

    private static string Signed(double value, string format = "0.0") => (value < 0 ? "−" : "+") + Math.Abs(value).ToString(format, PtBr);

    private static string PositionText(int value) => value > 0 ? "+" + value.ToString(PtBr) : value < 0 ? "−" + Math.Abs(value).ToString(PtBr) : "0";

    private GovForecast? NoTurn(string action) =>
        S.TurnsLeft < ActionOf(action).Turnos
            ? GovForecast.Block(GovFailure.SemTurno, T("sem_turno", ("turnos", Config.TurnosPorSemana.ToString(PtBr))))
            : null;

    private ForecastLine TurnLine(string action) => new(GovForecast.Turnos, null, -ActionOf(action).Turnos, -ActionOf(action).Turnos, -ActionOf(action).Turnos);

    private (double Expected, double Min, double Max) Spread(double expected) =>
        (expected, expected * (1 - Config.Incerteza), expected * (1 + Config.Incerteza));

    private double Draw(double expected) => expected * (1 + (((State.Rng.NextDouble() * 2) - 1) * Config.Incerteza));

    private List<YieldFactor> Factors(string action, string? target, double? presence)
    {
        var factors = new List<YieldFactor>();
        double fatigue = FatigueYield(action, target);
        if (fatigue < 0.999)
        {
            factors.Add(new YieldFactor("cansaco", T("fator_cansaco"), fatigue));
        }

        if (presence is { } p && p > 0.5)
        {
            factors.Add(new YieldFactor("teto", T("fator_teto"), 1 - (p / 100)));
        }

        return factors;
    }

    private void Spend(string action, string? target)
    {
        OffSeasonState s = S;
        s.TurnsLeft -= ActionOf(action).Turnos;
        s.ActionsThisWeek++;
        string key = Key(action, target);
        s.Fatigue[key] = s.Fatigue.GetValueOrDefault(key) + Config.CansacoPorUso;
    }

    private void News(string kind, string text, string? zoneId = null, string? personId = null)
    {
        OffSeasonState s = S;
        s.Feed.Add(new FeedItem(Today, kind, text, zoneId, personId));
        if (s.Feed.Count > Config.NoticiasMaximo)
        {
            s.Feed.RemoveRange(0, s.Feed.Count - Config.NoticiasMaximo);
        }
    }

    private void SyncRecognition() =>
        State.Player.Resources.SetRecognition(Context.Municipio.CodigoIbge.ToString(CultureInfo.InvariantCulture), (int)Math.Round(S.CityPresence()));

    // ---------------------------------------------------------------- caminhada

    private double WalkGain(int zone) =>
        Config.Caminhada.Presenca * FatigueYield(Caminhada, S.Zones[zone]) * (1 - (S.Presence[zone] / 100));

    public GovForecast ForecastWalk(string zoneId)
    {
        OffSeasonState s = S;
        int zone = s.IndexOf(zoneId);
        if (zone < 0)
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, T("zona_invalida"));
        }

        if (NoTurn(Caminhada) is { } blocked)
        {
            return blocked;
        }

        var (expected, min, max) = Spread(WalkGain(zone));
        return new GovForecast(null, null, [new ForecastLine(GovForecast.Presenca, zoneId, min, max, expected), TurnLine(Caminhada)], [],
        [
            T("previsao_caminhada", ("zona", ZoneName(zoneId)), ("antes", Num(s.Presence[zone])), ("depois", Num(Math.Min(100, s.Presence[zone] + expected)))),
            T("previsao_vizinhas", ("fracao", Texts.Percent(Config.VizinhaFracao))),
            T("previsao_decaimento", ("queda", Texts.Percent(Config.DecaimentoSemanal))),
        ])
        { Factors = Factors(Caminhada, zoneId, s.Presence[zone]) };
    }

    public GovResult Walk(string zoneId)
    {
        GovForecast forecast = ForecastWalk(zoneId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        OffSeasonState s = S;
        int zone = s.IndexOf(zoneId);
        double gain = Draw(WalkGain(zone));
        var changes = new List<string>();
        s.Presence[zone] = Math.Min(100, s.Presence[zone] + gain);
        changes.Add(T("mudou_presenca", ("delta", Signed(gain)), ("zona", ZoneName(zoneId)), ("valor", Num(s.Presence[zone]))));
        foreach (string neighbour in Context.Zonas.Zonas[zone].Vizinhas.Where(v => v != zoneId && s.IndexOf(v) >= 0))
        {
            int n = s.IndexOf(neighbour);
            double spill = gain * Config.VizinhaFracao;
            s.Presence[n] = Math.Min(100, s.Presence[n] + spill);
            if (spill >= 0.05)
            {
                changes.Add(T("mudou_presenca", ("delta", Signed(spill)), ("zona", ZoneName(neighbour)), ("valor", Num(s.Presence[n]))));
            }
        }

        // Andar pela zona pode revelar uma liderança dela que o jogador ainda não conhecia.
        if (State.City?.Leaders.FirstOrDefault(l => !l.Known && l.Zones.Contains(zoneId) && State.Renewal?.Status(l.PersonId)?.Saida is null) is { } unknown
            && State.Rng.NextDouble() < Config.ChanceDescoberta)
        {
            unknown.Known = true;
            changes.Add(T("descoberta", ("zona", ZoneName(zoneId)), ("nome", NameOf(unknown.PersonId))));
        }

        Spend(Caminhada, zoneId);
        SyncRecognition();
        string title = T("resultado_caminhada", ("zona", ZoneName(zoneId)));
        News(Caminhada, $"{title}: {string.Join("; ", changes)}", zoneId);
        return new GovResult(null, title, changes, forecast.Factors);
    }

    // ---------------------------------------------------------------- visita

    public GovForecast ForecastVisit(string personId)
    {
        OffSeasonState s = S;
        if (Leaders.FirstOrDefault(l => l.PersonId == personId) is not { } leader || State.City?.Person(personId) is not { } person)
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, T("pessoa_invalida"));
        }

        if (person.LastContact is { } last && (Today.DayNumber - last.DayNumber) / 7 < Config.VisitaIntervaloSemanas)
        {
            return GovForecast.Block(GovFailure.JaFeito, T("visita_recente", ("nome", NameOf(personId)), ("semanas", ((Today.DayNumber - last.DayNumber) / 7).ToString(PtBr)),
                ("intervalo", Config.VisitaIntervaloSemanas.ToString(PtBr))));
        }

        if (NoTurn(Visita) is { } blocked)
        {
            return blocked;
        }

        double relation = person.Relation(Today, Context.Memory);
        int zone = s.IndexOf(leader.Zones[0]);
        var lines = new List<ForecastLine> { new(GovForecast.Relacao, personId, Config.RelacaoVisita, Config.RelacaoVisita, Config.RelacaoVisita) };
        if (zone >= 0 && Config.Visita.Presenca > 0)
        {
            var (expected, min, max) = Spread(Config.Visita.Presenca * (1 - (s.Presence[zone] / 100)));
            lines.Add(new ForecastLine(GovForecast.Presenca, leader.Zones[0], min, max, expected));
        }

        lines.Add(TurnLine(Visita));
        return new GovForecast(null, null, lines, [],
            [T("previsao_visita", ("nome", NameOf(personId)), ("antes", Num(relation, "0")), ("depois", Num(Math.Min(Person.MaxRelation, relation + Config.RelacaoVisita), "0")))])
        { Remembered = [NameOf(personId)] };
    }

    public GovResult Visit(string personId)
    {
        GovForecast forecast = ForecastVisit(personId);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        OffSeasonState s = S;
        Leader leader = Leaders.First(l => l.PersonId == personId);
        Person person = State.City!.Person(personId)!;
        person.Remember(Today, "visita_entressafra", Config.PesoVisita, Config.RelacaoVisita, T("lembranca_visita"), Context.Memory);
        person.LastContact = Today;
        var changes = new List<string> { T("mudou_relacao", ("delta", Signed(Config.RelacaoVisita, "0")), ("nome", NameOf(personId)), ("valor", Num(person.Relation(Today, Context.Memory), "0"))) };
        int zone = s.IndexOf(leader.Zones[0]);
        if (zone >= 0 && Config.Visita.Presenca > 0)
        {
            double gain = Draw(Config.Visita.Presenca * (1 - (s.Presence[zone] / 100)));
            s.Presence[zone] = Math.Min(100, s.Presence[zone] + gain);
            changes.Add(T("mudou_presenca", ("delta", Signed(gain)), ("zona", ZoneName(leader.Zones[0])), ("valor", Num(s.Presence[zone]))));
        }

        Spend(Visita, personId);
        SyncRecognition();
        string title = T("resultado_visita", ("nome", NameOf(personId)));
        News(Visita, $"{title}: {string.Join("; ", changes)}", leader.Zones[0], personId);
        return new GovResult(null, title, changes, []);
    }

    // ---------------------------------------------------------------- entrevista

    private double InterviewGain(int zone) => Config.Entrevista.Presenca * FatigueYield(Entrevista, null) * (1 - (S.Presence[zone] / 100));

    /// <summary>Entrevista na imprensa local; com tema, divulga a posição já assumida nele.</summary>
    public GovForecast ForecastInterview(string? theme = null)
    {
        OffSeasonState s = S;
        if (theme is not null && !Context.Themes.ContainsKey(theme))
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, T("tema_invalido"));
        }

        if (theme is not null && !s.Positions.ContainsKey(theme))
        {
            return GovForecast.Block(GovFailure.Bloqueado, T("sem_posicao", ("tema", ThemeName(theme))));
        }

        if (NoTurn(Entrevista) is { } blocked)
        {
            return blocked;
        }

        double average = Enumerable.Range(0, s.Zones.Count).Average(InterviewGain);
        var (expected, min, max) = Spread(average);
        var explanation = new List<string> { T("previsao_entrevista") };
        if (theme is not null)
        {
            double known = s.Known.GetValueOrDefault(theme);
            explanation.Add(T("previsao_entrevista_tema", ("tema", ThemeName(theme)), ("antes", Texts.Percent(known)), ("depois", Texts.Percent(Math.Min(1, known + Config.ConhecidaPorEntrevista)))));
        }

        return new GovForecast(null, null, [new ForecastLine(GovForecast.Presenca, null, min, max, expected), TurnLine(Entrevista)], [], explanation)
        { Factors = Factors(Entrevista, null, null) };
    }

    public GovResult Interview(string? theme = null)
    {
        GovForecast forecast = ForecastInterview(theme);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        OffSeasonState s = S;
        double factor = Draw(1);
        double total = 0;
        for (int z = 0; z < s.Zones.Count; z++)
        {
            double gain = InterviewGain(z) * factor;
            s.Presence[z] = Math.Min(100, s.Presence[z] + gain);
            total += gain;
        }

        var changes = new List<string> { T("mudou_presenca_cidade", ("delta", Signed(total / Math.Max(1, s.Zones.Count)))) };
        if (theme is not null)
        {
            double before = s.Known.GetValueOrDefault(theme);
            s.Known[theme] = Math.Min(1, before + Config.ConhecidaPorEntrevista);
            State.Ledger.Positions.Add(new StatedPosition(theme, s.Positions[theme], Today, T("onde_entrevista")));
            changes.Add(T("mudou_conhecida", ("delta", Signed((s.Known[theme] - before) * 100, "0")), ("tema", ThemeName(theme)), ("valor", Texts.Percent(s.Known[theme]))));
        }

        Spend(Entrevista, null);
        SyncRecognition();
        string title = theme is null ? T("resultado_entrevista") : T("resultado_entrevista_tema", ("tema", ThemeName(theme)));
        News(Entrevista, $"{title}: {string.Join("; ", changes)}");
        return new GovResult(null, title, changes, forecast.Factors);
    }

    // ---------------------------------------------------------------- posição

    public GovForecast ForecastPosition(string theme, int value)
    {
        OffSeasonState s = S;
        if (!Context.Themes.ContainsKey(theme))
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, T("tema_invalido"));
        }

        if (value is < -2 or > 2)
        {
            return GovForecast.Block(GovFailure.AlvoInvalido, T("valor_invalido"));
        }

        if (s.Positions.TryGetValue(theme, out int current) && current == value)
        {
            return GovForecast.Block(GovFailure.JaFeito, T("mesma_posicao", ("tema", ThemeName(theme))));
        }

        if (NoTurn(Posicao) is { } blocked)
        {
            return blocked;
        }

        var explanation = new List<string> { T("previsao_posicao", ("tema", ThemeName(theme)), ("valor", PositionText(value))) };
        if (s.Positions.TryGetValue(theme, out int before))
        {
            explanation.Add(T("previsao_posicao_mudou", ("antes", PositionText(before)), ("tema", ThemeName(theme))));
        }

        return new GovForecast(null, null, [TurnLine(Posicao)], [], explanation);
    }

    public GovResult TakePosition(string theme, int value)
    {
        GovForecast forecast = ForecastPosition(theme, value);
        if (forecast.Blocked is { } failure)
        {
            return GovResult.Fail(failure, forecast.BlockedReason ?? "");
        }

        OffSeasonState s = S;
        s.Positions[theme] = value;
        double before = s.Known.GetValueOrDefault(theme);
        s.Known[theme] = Math.Min(1, before + Config.ConhecidaPorPosicao);
        State.Ledger.Positions.Add(new StatedPosition(theme, value, Today, T("onde_declaracao")));
        var changes = new List<string>
        {
            T("mudou_livro", ("tema", ThemeName(theme)), ("valor", PositionText(value))),
            T("mudou_conhecida", ("delta", Signed((s.Known[theme] - before) * 100, "0")), ("tema", ThemeName(theme)), ("valor", Texts.Percent(s.Known[theme]))),
        };
        Spend(Posicao, theme);
        string title = T("resultado_posicao", ("tema", ThemeName(theme)), ("valor", PositionText(value)));
        News(Posicao, title);
        return new GovResult(null, title, changes, []);
    }

    // ---------------------------------------------------------------- semana

    /// <summary>
    /// Fim da semana: a presença cai um pouco em toda zona, o cansaço passa e os turnos voltam. Roda antes de o
    /// calendário andar.
    /// </summary>
    public void ResolveWeek()
    {
        if (State.OffSeason is not { } s)
        {
            return;
        }

        double before = s.CityPresence();
        for (int z = 0; z < s.Presence.Count; z++)
        {
            s.Presence[z] *= 1 - Config.DecaimentoSemanal;
        }

        foreach (string key in s.Fatigue.Keys.ToList())
        {
            s.Fatigue[key] *= Config.CansacoRecuperacao;
            if (s.Fatigue[key] < 0.01)
            {
                s.Fatigue.Remove(key);
            }
        }

        double drop = before - s.CityPresence();
        if (s.ActionsThisWeek == 0 && drop >= 0.05)
        {
            News("semana", T("semana_parada", ("queda", Num(drop))));
        }

        s.ActionsThisWeek = 0;
        s.TurnsLeft = Config.TurnosPorSemana;
        SyncRecognition();
    }
}
