using Planalto.Core.Electorate;
using Planalto.Core.Ideology;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.Core.Territory;

namespace Planalto.Core.Campaign;

/// <summary>
/// Plataforma (GDD 17.9): posições do jogador nos temas, bandeiras, posição conhecida, custo de mudar e promessas
/// locais. O eleitor que não conhece a posição do jogador presume a do partido: o modelo de voto recebe a mistura
/// posição conhecida × posição do jogador + resto × posição do partido.
/// </summary>
public sealed partial class CampaignEngine
{
    private PlatformCatalog PlatformContent => Context.Campaign.Platform;

    private PlatformRules PlatformRules => PlatformContent.Regras;

    private PromiseCatalog PromiseContent => Context.Campaign.Promises;

    public PlatformState Platform => Campaign.Platform;

    private ThemeDefinition? Theme(string id) => Context.Themes.Themes.FirstOrDefault(t => t.Id == id);

    /// <summary>Posição do partido do jogador no tema (−2 a 2), derivada dos eixos: é o que o eleitor presume.</summary>
    public double PartyPosition(string theme) =>
        Theme(theme) is { } t ? ElectorateBuilder.ThemePreference(t, AxisPoint.From(Campaign.Player.Position), Context.ElectorateContent.VoteModel) : 0;

    /// <summary>Posição do jogador no tema: a que assumiu ou, por padrão, a do partido (arredondada).</summary>
    public int PositionOf(string theme) =>
        Platform.Positions.TryGetValue(theme, out int value) ? value : (int)Math.Round(PartyPosition(theme), MidpointRounding.AwayFromZero);

    /// <summary>Fração dos eleitores que conhece a posição do jogador no tema (0 a 1).</summary>
    public double KnownOf(string theme) => Math.Clamp(Platform.Known.GetValueOrDefault(theme), 0, 1);

    /// <summary>Posição que o eleitorado enxerga no tema: a mistura entre a do jogador (quem conhece) e a do partido (o resto).</summary>
    public double PerceivedPosition(string theme) =>
        Platform.Positions.TryGetValue(theme, out int value) ? Mix(value, KnownOf(theme), PartyPosition(theme)) : PartyPosition(theme);

    private static double Mix(double own, double known, double party) => (known * own) + ((1 - known) * party);

    /// <summary>Temas que vão ao modelo de voto: só os que o jogador assumiu (os demais o modelo deriva do partido).</summary>
    private Dictionary<string, double> PerceivedThemes() =>
        Platform.Positions.ToDictionary(p => p.Key, p => Mix(p.Value, KnownOf(p.Key), PartyPosition(p.Key)), StringComparer.Ordinal);

    private int RecentChanges() =>
        Platform.Changes.Count(c => (Today.DayNumber - c.Date.DayNumber) / 7.0 < PlatformRules.JanelaMudancasSemanas);

    private string GroupName(string groupId) =>
        Context.ElectorateContent.Segments.Dimensions.SelectMany(d => d.Groups).FirstOrDefault(g => g.Id == groupId)?.Name ?? groupId;

    private IEnumerable<string> AllGroups() => Context.ElectorateContent.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id);

    private string PositionLabel(ThemeDefinition theme, int value) =>
        value == 0 ? "0" : $"{(value > 0 ? "+" : "−")}{Math.Abs(value)} ({(value > 0 ? theme.PositivePole : theme.NegativePole)})";

    // ------------------------------------------------------------------ bandeiras e fala

    /// <summary>Define as bandeiras (até o máximo): os temas em que a posição fica conhecida mais depressa.</summary>
    public CommandFailure? SetFlags(IReadOnlyList<string> themes)
    {
        List<string> distinct = themes.Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Any(t => Theme(t) is null))
        {
            return CommandFailure.NaoEncontrado;
        }

        if (distinct.Count > PlatformRules.BandeirasMax)
        {
            return CommandFailure.BandeirasDemais;
        }

        Platform.Flags.Clear();
        Platform.Flags.AddRange(distinct);
        return null;
    }

    /// <summary>Ganho de posição conhecida de uma fala sobre o tema, antes do que já é conhecido (0 se o tipo não é de fala).</summary>
    private double ThemeGainBase(string typeId, string theme) =>
        PlatformContent.Falas.TryGetValue(typeId, out double gain) && Theme(theme) is not null
            ? Math.Min(1, gain * (Platform.Flags.Contains(theme) ? PlatformRules.BandeiraBonus : 1))
            : 0;

    /// <summary>Quanto a posição no tema ficaria mais conhecida com um compromisso desse tipo (0 se o tipo não é de fala).</summary>
    public double ThemeGain(string typeId, string theme) => ThemeGainBase(typeId, theme) * (1 - KnownOf(theme));

    /// <summary>Compromissos em que o candidato pode tratar de um tema.</summary>
    public bool SpeaksOfThemes(string typeId) => PlatformContent.Falas.ContainsKey(typeId);

    /// <summary>
    /// O candidato falou do tema em público: a posição fica mais conhecida, entra no Livro como posição assumida
    /// (tema, data, onde foi dito) e cumpre o pedido das lideranças que queriam ouvi-lo defender o tema.
    /// </summary>
    private double SpeakOf(string theme, CommitmentType type, string? zoneId)
    {
        // Enquanto o jogador não escolhe outra, a posição dita é a do partido: ficar conhecida não muda o voto de
        // ninguém (o eleitor já presumia a mesma coisa), mas passa a custar caro mudar depois.
        double gain = ThemeGain(type.Id, theme);
        int position = PositionOf(theme);
        Platform.Known[theme] = Math.Clamp(KnownOf(theme) + gain, 0, 1);
        string where = zoneId is null
            ? Texts.Fill(PlatformContent.Texto("fala"), ("compromisso", type.Nome))
            : Texts.Fill(PlatformContent.Texto("fala_zona"), ("compromisso", type.Nome), ("zona", ZoneName(zoneId)));
        State.Ledger.Positions.Add(new StatedPosition(theme, position, Today, where));
        FulfilPosition(theme);
        return gain;
    }

    /// <summary>Pedido "defender em público a pauta em {tema}" (GDD 17.10): fica cumprido quando o jogador assume a posição em público.</summary>
    private void FulfilPosition(string theme)
    {
        bool any = false;
        foreach (Leader leader in City.Leaders.Where(l => l.Theme == theme && l.PromiseId is not null))
        {
            if (State.Ledger.Promises.FirstOrDefault(p => p.Id == leader.PromiseId) is not { State: PromiseState.Aberta } promise
                || LeaderContent.Pedidos.FirstOrDefault(p => p.Id == promise.Object)?.Kind != LeaderRequestDefinition.Posicao)
            {
                continue;
            }

            promise.State = PromiseState.Cumprida;
            RememberLeader(leader, "promessa_cumprida", 2, LeaderRules.PontosPromessaCumprida, contact: false);
            Realign(leader);
            any = true;
        }

        if (any)
        {
            RefreshSupport();
        }
    }

    /// <summary>Virada da semana: os temas de bandeira ficam um pouco mais conhecidos mesmo sem fala.</summary>
    private void PlatformWeek()
    {
        foreach (string theme in Platform.Flags)
        {
            double known = KnownOf(theme);
            Platform.Known[theme] = Math.Clamp(known + (PlatformRules.BandeiraGanhoSemanal * (1 - known)), 0, 1);
        }
    }

    // ------------------------------------------------------------------ mudar de posição

    public CommandFailure? CanSetPosition(string theme, int value)
    {
        if (Theme(theme) is null)
        {
            return CommandFailure.NaoEncontrado;
        }

        if (!CampaignRunning)
        {
            return CommandFailure.ForaDaCampanha;
        }

        if (value is < ThemeDefinition.MinPosition or > ThemeDefinition.MaxPosition)
        {
            return CommandFailure.ValorInvalido;
        }

        return value == PositionOf(theme) && Platform.Positions.ContainsKey(theme) ? CommandFailure.MesmaPosicao : null;
    }

    /// <summary>Incoerência de mudar o tema para o valor agora: salto × posição conhecida × (1 + mudanças recentes).</summary>
    public double IncoherenceOf(string theme, int value) =>
        Math.Abs(value - PositionOf(theme)) * KnownOf(theme) * (1 + RecentChanges());

    /// <summary>Pontos de relação que uma mudança incoerente custa em cada liderança com interesse no tema.</summary>
    private int PositionLeaderPoints(double incoherence) =>
        Math.Min(-1, (int)Math.Round(incoherence * PlatformRules.PontosLiderancaPorIncoerencia));

    private sealed record ShareView(double City, Dictionary<string, double> ByGroup, Dictionary<string, double> ByZone);

    /// <summary>Fatia do jogador nos votos válidos (cidade, cada grupo, cada zona) com os temas e a imagem dados.</summary>
    private ShareView PlayerShares(IReadOnlyDictionary<string, double> themes, double image)
    {
        Candidate player = Campaign.Player;
        List<VoteCandidate> candidates = Campaign.ForOffice(player.Office).Select(c => !c.IsPlayer ? ToVote(c) : new VoteCandidate
        {
            Id = c.Id,
            PartyId = c.PartyId,
            Position = c.Position,
            Themes = themes,
            Recognition = c.Recognition / 100,
            Image = image,
            Support = c.Support,
            Multiplicity = c.Multiplicity,
        }).ToList();
        var (city, zones) = ProjectByZone(candidates, null);
        var byGroup = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (string group in AllGroups())
        {
            double votes = 0;
            double valid = 0;
            foreach (SegmentIntention s in city.Segments)
            {
                if (!s.Segment.Has(group))
                {
                    continue;
                }

                double weight = s.Segment.Voters * s.Turnout * (1 - s.BlankNull);
                votes += weight * s.Shares[player.Id];
                valid += weight;
            }

            if (valid > 0)
            {
                byGroup[group] = votes / valid;
            }
        }

        var byZone = new Dictionary<string, double>(StringComparer.Ordinal);
        for (int z = 0; z < zones.Count; z++)
        {
            byZone[Territory.Zones[z]] = zones[z].Valid <= 0 ? 0 : zones[z].Votes[player.Id] / zones[z].Valid;
        }

        return new ShareView(city.Valid <= 0 ? 0 : city.Votes[player.Id] / city.Valid, byGroup, byZone);
    }

    /// <summary>Fatia do jogador nos votos válidos hoje: cidade, por grupo e por zona (para medir o efeito de uma mudança).</summary>
    public (double City, IReadOnlyDictionary<string, double> ByGroup, IReadOnlyDictionary<string, double> ByZone) PlayerSharesNow()
    {
        ShareView view = PlayerShares(PerceivedThemes(), Campaign.Player.Image);
        return (view.City, view.ByGroup, view.ByZone);
    }

    /// <summary>
    /// Previsão de mudar a posição num tema: o custo (incoerência, imagem, lideranças que lembram) e o ganho ou perda
    /// de votos, em pontos percentuais dos válidos, por grupo e por zona. A faixa vai do efeito com a posição
    /// conhecida como está hoje ao efeito quando todos a conhecerem.
    /// </summary>
    public CommandForecast ForecastPosition(string theme, int value)
    {
        if (Theme(theme) is not { } definition)
        {
            return new CommandForecast(CommandFailure.NaoEncontrado, [], [], [theme]);
        }

        int old = PositionOf(theme);
        int jump = Math.Abs(value - old);
        double known = KnownOf(theme);
        int recent = RecentChanges();
        double incoherence = IncoherenceOf(theme, value);
        double imageDelta = -incoherence * PlatformRules.ImagemPorIncoerencia;
        double knownAfter = known + (PlatformRules.ConhecidaPorMudanca * (1 - known));
        double party = PartyPosition(theme);
        double image = Math.Clamp(Campaign.Player.Image + imageDelta, -1, 1);

        ShareView baseline = PlayerShares(PerceivedThemes(), Campaign.Player.Image);
        ShareView With(double k)
        {
            Dictionary<string, double> themes = PerceivedThemes();
            themes[theme] = Mix(value, k, party);
            return PlayerShares(themes, image);
        }

        // As lideranças com interesse no tema esfriam com a incoerência, e o apoio que dão cai junto: a previsão
        // conta isso baixando a afinidade delas só durante a conta.
        int leaderPoints = incoherence > 0 ? PositionLeaderPoints(incoherence) : 0;
        List<Person> cooled = leaderPoints == 0 ? [] : City.Leaders.Where(l => l.Theme == theme && l.Known).Select(l => City.Person(l.PersonId)!).ToList();
        ShareView[] scenarios;
        try
        {
            cooled.ForEach(p => p.Affinity += leaderPoints);
            if (cooled.Count > 0)
            {
                RefreshSupport();
            }

            scenarios = [With(known), With(knownAfter), With(1)];
        }
        finally
        {
            cooled.ForEach(p => p.Affinity -= leaderPoints);
            if (cooled.Count > 0)
            {
                RefreshSupport();
            }
        }
        ForecastLine Line(string kind, string? target, Func<ShareView, double> read)
        {
            double before = read(baseline);
            double[] deltas = scenarios.Select(s => (read(s) - before) * 100).ToArray();
            return new ForecastLine(kind, target, deltas.Min(), deltas.Max(), deltas[1]);
        }

        // A mesma mudança como variação dos votos do próprio jogador ali (%): numa disputa de vereador a fatia é
        // pequena, e o ponto percentual sozinho não diz se a mudança é grande para a candidatura.
        ForecastLine Relative(string kind, string? target, Func<ShareView, double> read)
        {
            double before = read(baseline);
            double[] deltas = scenarios.Select(s => before <= 0 ? 0 : ((read(s) / before) - 1) * 100).ToArray();
            return new ForecastLine(kind, target, deltas.Min(), deltas.Max(), deltas[1]);
        }

        var lines = new List<ForecastLine>
        {
            new("incoherence", theme, incoherence, incoherence, incoherence),
            new("image", null, imageDelta, imageDelta, imageDelta),
            new("known", theme, knownAfter, knownAfter, knownAfter),
            Line("votes", null, s => s.City),
            Relative("votes_pct", null, s => s.City),
        };
        List<ForecastLine> groups = baseline.ByGroup.Keys.Select(g => Line("votes_group", g, s => s.ByGroup.GetValueOrDefault(g))).ToList();
        List<ForecastLine> zones = baseline.ByZone.Keys.Select(z => Line("votes_zone", z, s => s.ByZone.GetValueOrDefault(z))).ToList();
        lines.AddRange(groups);
        lines.AddRange(zones);
        lines.AddRange(baseline.ByGroup.Keys.Select(g => Relative("votes_group_pct", g, s => s.ByGroup.GetValueOrDefault(g))));
        lines.AddRange(baseline.ByZone.Keys.Select(z => Relative("votes_zone_pct", z, s => s.ByZone.GetValueOrDefault(z))));

        var explanation = new List<string>
        {
            Texts.Fill(PlatformContent.Texto("previsao_salto"), ("tema", definition.Name), ("de", PositionLabel(definition, old)), ("para", PositionLabel(definition, value)),
                ("salto", Texts.Number(jump)), ("conhecida", Texts.Percent(known)), ("partido", Texts.Number(party, "0.0"))),
            incoherence > 0
                ? Texts.Fill(PlatformContent.Texto("previsao_incoerencia"), ("incoerencia", Texts.Number(incoherence, "0.00")), ("salto", Texts.Number(jump)), ("conhecida", Texts.Percent(known)),
                    ("mudancas", Texts.Number(recent)), ("semanas", Texts.Number(PlatformRules.JanelaMudancasSemanas)), ("imagem", Texts.Number(imageDelta, "0.000")))
                : PlatformContent.Texto("previsao_sem_custo"),
            Texts.Fill(PlatformContent.Texto("previsao_cidade"), ("min", Texts.Number(lines[3].Min, "+0.00;−0.00")), ("max", Texts.Number(lines[3].Max, "+0.00;−0.00")),
                ("min_pct", Texts.Number(lines[4].Min, "+0.0;−0.0")), ("max_pct", Texts.Number(lines[4].Max, "+0.0;−0.0"))),
        };
        string Pct(string kind, string target) => Texts.Number(lines.First(l => l.Kind == kind && l.Target == target).Expected, "+0.0;−0.0");
        void Extremes(List<ForecastLine> list, string key, Func<string, string> name)
        {
            string pctKind = list.Count > 0 ? list[0].Kind + "_pct" : "";
            if (list.Count == 0)
            {
                return;
            }

            ForecastLine best = list.MaxBy(l => l.Expected)!;
            ForecastLine worst = list.MinBy(l => l.Expected)!;
            if (best.Expected > 0)
            {
                explanation.Add(Texts.Fill(PlatformContent.Texto(key), ("sinal", "Ganha"), ("alvo", name(best.Target!)), ("valor", Texts.Number(best.Expected, "+0.00;−0.00")), ("pct", Pct(pctKind, best.Target!))));
            }

            if (worst.Expected < 0)
            {
                explanation.Add(Texts.Fill(PlatformContent.Texto(key), ("sinal", "Perde"), ("alvo", name(worst.Target!)), ("valor", Texts.Number(worst.Expected, "+0.00;−0.00")), ("pct", Pct(pctKind, worst.Target!))));
            }
        }

        Extremes(groups, "previsao_grupo", GroupName);
        Extremes(zones, "previsao_zona", ZoneName);
        int leaders = incoherence > 0 ? City.Leaders.Count(l => l.Theme == theme && l.Known) : 0;
        if (leaders > 0)
        {
            explanation.Add(Texts.Fill(PlatformContent.Texto("previsao_liderancas"), ("quantidade", Texts.Number(leaders))));
        }

        explanation.Add(PlatformContent.Texto("previsao_livro"));
        return new CommandForecast(CanSetPosition(theme, value), lines, [], explanation);
    }

    /// <summary>
    /// Assume (ou muda) a posição num tema, em público. A incoerência pesa na imagem, as lideranças com interesse no
    /// tema lembram, e a posição entra no Livro com a data. Mudar de novo em pouco tempo custa mais.
    /// </summary>
    public (CommandFailure? Failure, PositionResult? Result) SetPosition(string theme, int value)
    {
        if (CanSetPosition(theme, value) is { } failure)
        {
            return (failure, null);
        }

        ThemeDefinition definition = Theme(theme)!;
        int old = PositionOf(theme);
        double known = KnownOf(theme);
        double incoherence = IncoherenceOf(theme, value);
        double imageDelta = -incoherence * PlatformRules.ImagemPorIncoerencia;
        Campaign.Player.Image = Math.Clamp(Campaign.Player.Image + imageDelta, -1, 1);

        var remembered = new List<string>();
        if (incoherence > 0)
        {
            int points = PositionLeaderPoints(incoherence);
            string text = Texts.Fill(PlatformContent.Texto("mudou_posicao"), ("tema", definition.Name));
            foreach (Leader leader in City.Leaders.Where(l => l.Theme == theme && l.Known))
            {
                RememberLeader(leader, "mudou_posicao", incoherence >= 2 ? -2 : -1, points, text, contact: false);
                Realign(leader);
                remembered.Add(leader.PersonId);
            }
        }

        var change = new PositionChange(theme, Today, old, value, incoherence);
        Platform.Positions[theme] = value;
        Platform.Changes.Add(change);
        Platform.Known[theme] = Math.Clamp(known + (PlatformRules.ConhecidaPorMudanca * (1 - known)), 0, 1);
        var stated = new StatedPosition(theme, value, Today, PlatformContent.Texto("anuncio"));
        State.Ledger.Positions.Add(stated);
        EmitFeed(Today, "posicao", Texts.Fill(PlatformContent.Texto("feed_posicao"), ("tema", definition.Name), ("posicao", PositionLabel(definition, value))), null, null, RaceSetup.PlayerId);
        FulfilPosition(theme);
        RefreshSupport();
        return (null, new PositionResult(change, imageDelta, remembered, stated));
    }

    // ------------------------------------------------------------------ painel "plataforma × públicos"

    /// <summary>
    /// Matriz "plataforma × públicos": para cada tema, a posição do jogador e a do partido, o quanto é conhecida e,
    /// para cada grupo de eleitores e cada zona, o que o público prefere, quanto o tema pesa e o quanto concorda.
    /// </summary>
    public IReadOnlyList<PlatformRow> PlatformMatrix()
    {
        var rows = new List<PlatformRow>();
        List<string> groups = AllGroups().ToList();
        foreach (ThemeDefinition theme in Context.Themes.Themes)
        {
            int position = PositionOf(theme.Id);
            PlatformCell Cell(string kind, string id, IEnumerable<Segment> segments)
            {
                double voters = 0;
                double preference = 0;
                double priority = 0;
                foreach (Segment s in segments)
                {
                    voters += s.Voters;
                    preference += s.Voters * s.ThemePreference.GetValueOrDefault(theme.Id);
                    priority += s.Voters * s.ThemePriority.GetValueOrDefault(theme.Id);
                }

                preference = voters <= 0 ? 0 : preference / voters;
                priority = voters <= 0 ? 0 : priority / voters;
                return new PlatformCell(kind, id, preference, priority, 1 - (Math.Abs(position - preference) / 2));
            }

            var cells = new List<PlatformCell>();
            cells.AddRange(groups.Select(g => Cell(Promise.ParaGrupo, g, Context.Electorate.Segments.Where(s => s.Has(g)))));
            cells.AddRange(Context.ZoneElectorates.Select(z => Cell(Promise.ParaZona, z.Zona.Id, z.Segments)));
            rows.Add(new PlatformRow(theme, position, Platform.Positions.ContainsKey(theme.Id), PartyPosition(theme.Id), KnownOf(theme.Id), Platform.Flags.Contains(theme.Id), cells));
        }

        return rows;
    }

    // ------------------------------------------------------------------ promessas locais

    /// <summary>Credibilidade (0 a 1): cai com promessas abertas demais e com as quebradas ou vencidas; multiplica o apoio de cada promessa nova.</summary>
    public double Credibility => CredibilityWith(0);

    private double CredibilityWith(int extraOpen)
    {
        PromiseRules rules = PromiseContent.Regras;
        int open = State.Ledger.Promises.Count(p => p.State == PromiseState.Aberta) + extraOpen;
        int broken = State.Ledger.Promises.Count(p => p.State is PromiseState.Quebrada or PromiseState.Vencida);
        double value = 1 - (rules.CredibilidadePerda * Math.Max(0, open - rules.PromessasSemCusto)) - (rules.CredibilidadePerdaQuebrada * broken);
        return Math.Clamp(value, rules.CredibilidadeMinima, 1);
    }

    private string PromiseTargetName(string toKind, string toId) => toKind == Promise.ParaZona ? ZoneName(toId) : GroupName(toId);

    private string PromiseText(PromiseDefinition definition, string toKind, string toId) =>
        Texts.Fill(definition.Texto, ("alvo", PromiseTargetName(toKind, toId)));

    public CommandFailure? CanPromise(string promiseId, string toKind, string toId)
    {
        if (PromiseContent.Find(promiseId) is not { } definition)
        {
            return CommandFailure.NaoEncontrado;
        }

        if (!CampaignRunning)
        {
            return CommandFailure.ForaDaCampanha;
        }

        if (!definition.Alvos.Contains(toKind))
        {
            return CommandFailure.AlvoInvalido;
        }

        if (toKind == Promise.ParaZona)
        {
            int zone = Territory.IndexOf(toId);
            if (zone < 0 || (definition.Situacao != "qualquer" && Context.Zonas.Zonas[zone].Situacao != definition.Situacao))
            {
                return CommandFailure.AlvoInvalido;
            }
        }
        else if (!definition.Grupos.Contains(toId) || !Context.Electorate.Segments.Any(s => s.Has(toId) && s.Voters > 0))
        {
            return CommandFailure.AlvoInvalido;
        }

        return State.Ledger.Promises.Any(p => p.State == PromiseState.Aberta && p.Object == promiseId && p.ToKind == toKind && p.ToId == toId)
            ? CommandFailure.JaPrometido
            : null;
    }

    /// <summary>Promessas do catálogo que cabem ao alvo (zona ou grupo), com o motivo quando não podem ser feitas agora.</summary>
    public IReadOnlyList<PromiseOption> PromiseOptions(string toKind, string toId) =>
        PromiseContent.Promessas
            .Select(p => new PromiseOption(p, toKind, toId, PromiseText(p, toKind, toId), CanPromise(p.Id, toKind, toId)))
            .Where(o => o.Blocked is not CommandFailure.AlvoInvalido)
            .ToList();

    /// <summary>Grupos que recebem o apoio da promessa: o próprio grupo, ou todos os eleitores da zona (um grupo de idade por eleitor).</summary>
    private List<string> PromiseGroups(string toKind, string toId) =>
        toKind == Promise.ParaGrupo ? [toId]
        : Territory.IndexOf(toId) is var z and >= 0 ? Channels.FaixasIdade.SelectMany(b => b.Grupos).Where(g => Context.ZoneElectorates[z].VotersIn(g) > 0).ToList()
        : [];

    private DateOnly PromiseDeadline(PromiseDefinition definition) =>
        new DateOnly(Campaign.ElectionDate.Year + 1, 1, 1).AddDays(7 * definition.PrazoSemanas);

    /// <summary>Previsão de uma promessa local: o apoio imediato em faixa, a credibilidade antes e depois e o prazo no Livro.</summary>
    public CommandForecast ForecastPromise(string promiseId, string toKind, string toId)
    {
        if (PromiseContent.Find(promiseId) is not { } definition)
        {
            return new CommandForecast(CommandFailure.NaoEncontrado, [], [], [promiseId]);
        }

        CommandFailure? blocked = CanPromise(promiseId, toKind, toId);
        if (blocked is CommandFailure.AlvoInvalido)
        {
            return new CommandForecast(blocked, [], [], [definition.Nome]);
        }

        double credibility = Credibility;
        double after = CredibilityWith(1);
        var luck = LuckRange(PromiseContent.Regras.Sorte);
        double support = definition.Apoio * credibility;
        var lines = PromiseGroups(toKind, toId).Select(g => new ForecastLine("support", g, support * luck.Min, support * luck.Max, support)).ToList();
        lines.Add(new ForecastLine("credibility", null, after, after, after));
        string text = PromiseText(definition, toKind, toId);
        var explanation = new List<string>
        {
            Texts.Fill(PromiseContent.Texto("previsao_apoio"), ("promessa", text), ("min", Texts.Number(support * luck.Min, "0.000")), ("max", Texts.Number(support * luck.Max, "0.000")), ("alvo", PromiseTargetName(toKind, toId))),
            Texts.Fill(PromiseContent.Texto("previsao_credibilidade"), ("antes", Texts.Percent(credibility)), ("depois", Texts.Percent(after))),
            Texts.Fill(PromiseContent.Texto("previsao_livro"), ("prazo", PromiseDeadline(definition).ToString("MM/yyyy", System.Globalization.CultureInfo.InvariantCulture))),
        };
        return new CommandForecast(blocked, lines, [], explanation)
        {
            Factors = [Factor("credibilidade", "Credibilidade", credibility)],
        };
    }

    /// <summary>Faz a promessa: apoio imediato do alvo (vezes a credibilidade) e a entrada no Livro de compromissos.</summary>
    public (CommandFailure? Failure, PromiseResult? Result) MakePromise(string promiseId, string toKind, string toId)
    {
        if (CanPromise(promiseId, toKind, toId) is { } failure)
        {
            return (failure, null);
        }

        PromiseDefinition definition = PromiseContent.Find(promiseId)!;
        var range = LuckRange(PromiseContent.Regras.Sorte);
        double support = definition.Apoio * Credibility * Rng.NextDouble(range.Min, range.Max);
        var gained = new Dictionary<string, double>(StringComparer.Ordinal);
        int zone = toKind == Promise.ParaZona ? Territory.IndexOf(toId) : -1;
        foreach (string group in PromiseGroups(toKind, toId))
        {
            Dictionary<string, double> store = zone >= 0 ? Territory.MeetingSupport[zone] : Campaign.Player.Support;
            double before = store.GetValueOrDefault(group);
            store[group] = Math.Clamp(before + support, 0, 1);
            gained[group] = store[group] - before;
        }

        string text = PromiseText(definition, toKind, toId);
        Promise promise = State.Ledger.AddPromise(toKind, toId, definition.Id, Texts.Fill(PromiseContent.Texto("livro"), ("promessa", text)), zone >= 0 ? toId : null, Today, PromiseDeadline(definition));
        EmitFeed(Today, "promessa", Texts.Fill(PromiseContent.Texto("feed"), ("promessa", text)), zone >= 0 ? toId : null, null, RaceSetup.PlayerId);
        RefreshSupport();
        return (null, new PromiseResult(promise, gained, Credibility));
    }
}
