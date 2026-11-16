using Planalto.Core.Elections;
using Planalto.Core.Media;
using Planalto.Core.People;
using Planalto.Core.State;

namespace Planalto.Core.Campaign;

/// <summary>Veículo como a tela mostra: nome com o que é, linha, alcance, dono, jornalista e o que dá para fazer nele.</summary>
public sealed record OutletView(
    Outlet Outlet,
    string Label,
    string TypeName,
    string LineName,
    double ReachShare,
    IReadOnlyDictionary<string, double> ReachByZone,
    string OwnerName,
    string JournalistName,
    double JournalistRelation,
    string Stance,
    bool Broadcaster,
    bool Written,
    int AdsLeft,
    decimal AdCost);

/// <summary>Notícia com o fato de origem e o veículo, para o feed de notícias.</summary>
public sealed record NewsView(NewsItem News, Fact? Fact, string OutletLabel);

public sealed record HotThemeView(string Theme, string ThemeName, IReadOnlyList<string> Zones, DateOnly Until, double Bonus, int News);

public sealed record StoryResult(bool Published, NewsItem? News, double ImageDelta, Memory Memory, string Text);

public sealed record AdResult(IReadOnlyDictionary<string, double> Presence, decimal Money, int AdsLeft, string Text);

/// <summary>Horário eleitoral gratuito (art. 47): quando vai ao ar, o tempo do partido e o que rende por semana.</summary>
public sealed record FreeAirtimeView(bool HasBroadcaster, DateOnly Starts, DateOnly Ends, bool OnAir, double PartyShare, double MinutesDaily, double WeeklyPresence, bool SeatsKnown, string Text);

/// <summary>
/// Mídia local (GDD 17.13): os veículos cobrem os fatos de visibilidade 1 ou mais conforme o interesse; a linha
/// editorial decide o tom, nunca o fato. Uma semana de notícias sobre um tema o põe em alta nas zonas atingidas.
/// Entrevista num veículo, sugerir pauta, anunciar na imprensa escrita (art. 43) e horário gratuito (art. 47).
/// </summary>
public sealed partial class CampaignEngine
{
    private OutletCatalog OutletContent => MediaContent.Outlets;

    private MediaRules MediaRules => OutletContent.Regras;

    private PropagandaLawConfig PropagandaLaw => Context.PropagandaLaw;

    public MediaState Media => Campaign.Media;

    /// <summary>Partido da prefeitura hoje (a linha editorial é em relação a ela).</summary>
    public string? GovernmentPartyId => WorldBuilder.GovernmentParty(Context, Campaign);

    /// <summary>De que lado da prefeitura está quem fez o fato: candidato pelo partido; liderança pelo candidato que apoia.</summary>
    public string SideOf(string? actorId)
    {
        string? government = GovernmentPartyId;
        if (actorId is null || government is null)
        {
            return Side.Nenhum;
        }

        Candidate? candidate = Campaign.Find(actorId) ?? (City.Leader(actorId)?.AlignedWith is { } aligned ? Campaign.Find(aligned) : null);
        return candidate is null ? Side.Nenhum : candidate.PartyId == government ? Side.Situacao : Side.Oposicao;
    }

    /// <summary>Nome do veículo com o que é: "Rádio Vale (rádio local, linha governista)".</summary>
    public string OutletLabel(Outlet outlet) => $"{outlet.Name} ({OutletContent.Type(outlet.Type).Nome}, {OutletContent.Line(outlet.Line).Nome})";

    /// <summary>Fração dos eleitores da cidade que o veículo alcança.</summary>
    public double ReachShare(Outlet outlet)
    {
        double pop = Territory.Population.Sum(x => (double)x);
        return pop <= 0 ? outlet.Reach.DefaultIfEmpty(0).Average() : outlet.Reach.Select((r, z) => r * Territory.Population[z]).Sum() / pop;
    }

    /// <summary>Como o veículo trata o candidato: "critico", "amigo" ou "neutro", pela linha e pelo lado dele.</summary>
    public string StanceToward(Outlet outlet, string candidateId)
    {
        string side = SideOf(candidateId);
        return (outlet.Line, side) switch
        {
            (EditorialLine.Governista, Side.Oposicao) or (EditorialLine.Critica, Side.Situacao) => "critico",
            (EditorialLine.Governista, Side.Situacao) or (EditorialLine.Critica, Side.Oposicao) => "amigo",
            _ => "neutro",
        };
    }

    private string ThemeName(string? theme) => theme is null ? "" : Theme(theme)?.Name ?? theme;

    // ------------------------------------------------------------------ o dia e a semana da mídia

    private void MediaDay(DateOnly day)
    {
        if (City.Outlets.Count == 0)
        {
            return;
        }

        TemplateCatalog templates = MediaContent.Templates;
        if (Rng.Chance(MediaRules.FatoDaCidadeChanceDiaria))
        {
            List<CityFactDefinition> possible = templates.FatosDaCidade
                .Where(f => f.Situacao == "qualquer" || Context.Zonas.Zonas.Any(z => z.Situacao == f.Situacao)).ToList();
            if (possible.Count > 0)
            {
                CityFactDefinition def = Rng.PickWeighted(possible, f => f.Weight);
                var zones = Context.Zonas.Zonas.Where(z => def.Situacao == "qualquer" || z.Situacao == def.Situacao).ToList();
                var zone = Rng.PickWeighted(zones, z => Math.Max(1, z.Populacao));
                Emit(FactKind.Cidade, Texts.Fill(def.Texto, ("zona", zone.Nome)), zone: zone.Id, theme: def.Tema, sub: def.Id, date: day);
            }
        }

        if (Media.LastEconomyFact is not { } last || day.DayNumber - last.DayNumber >= 7 * MediaRules.EconomiaACadaSemanas)
        {
            EconomyFact(day);
        }

        foreach (Fact fact in Facts.Items.Where(f => f.Seq > Media.CoveredUpTo && f.Visibility >= 1).ToList())
        {
            foreach (Outlet outlet in City.Outlets)
            {
                OutletType type = OutletContent.Type(outlet.Type);
                if ((fact.Rumor && !type.Rede) || Media.News.Any(n => n.FactId == fact.Id && n.OutletId == outlet.Id))
                {
                    continue;
                }

                double chance = fact.Rumor ? 1 : Math.Min(1, MediaRules.CoberturaPorVisibilidade[fact.Visibility] * (fact.Theme is not null && outlet.Themes.Contains(fact.Theme) ? MediaRules.InteresseFator : 1));
                if (Rng.Chance(chance))
                {
                    Publish(fact, outlet);
                }
            }
        }

        Media.CoveredUpTo = Facts.NextSeq - 1;
    }

    /// <summary>Fato econômico nacional: o indicador anda um pouco a partir do número de partida (sem série histórica).</summary>
    private void EconomyFact(DateOnly day)
    {
        EconomyFactDefinition def = Rng.Pick(MediaContent.Templates.FatosEconomicos);
        decimal before = Media.Economy.GetValueOrDefault(def.Indicador);
        decimal after = Math.Max(0.01m, Math.Round(before * (decimal)(1 + Rng.NextDouble(-MediaRules.EconomiaPasso, MediaRules.EconomiaPasso)), 2));
        Media.Economy[def.Indicador] = after;
        Media.LastEconomyFact = day;
        string template = after > before ? def.Sobe : after < before ? def.Cai : def.Estavel;
        Emit(FactKind.Economia, Texts.Fill(template, ("valor", Texts.Number((double)after, "0.00"))), theme: def.Tema, sub: def.Indicador, date: day);
    }

    /// <summary>Tons possíveis de um veículo para um lado, com o peso de cada um (os coerentes com a linha).</summary>
    private Dictionary<string, double> TonesFor(Outlet outlet, string side) => OutletContent.Line(outlet.Line).Tons[side];

    /// <summary>Imagem que uma notícia do veículo, nesse tom, rende a quem fez o fato.</summary>
    private double NewsImage(Outlet outlet, string tone) =>
        Tone.Sign(tone) * MediaRules.ImagemPorNoticia * ReachShare(outlet) * (1 + outlet.Sensationalism);

    /// <summary>
    /// O veículo publica o fato: a linha decide o tom (com um desvio raro), a manchete sai do tipo do fato, e quem
    /// fez o fato ganha ou perde um pouco de imagem, conforme o alcance do veículo.
    /// </summary>
    private (NewsItem News, double ImageDelta) Publish(Fact fact, Outlet outlet)
    {
        string side = SideOf(fact.ActorId);
        Dictionary<string, double> tones = TonesFor(outlet, side);
        // Morte é tratada com seriedade (GDD 17.16): tom neutro em todos os veículos, qualquer que seja a linha.
        string tone = fact.Kind == FactKind.Morte ? Tone.Neutro
            : Rng.Chance(MediaRules.DesvioDeTom) ? Rng.Pick(Tone.All) : Rng.PickWeighted(tones.Keys.Order(StringComparer.Ordinal).ToList(), t => tones[t]);
        TemplateCatalog templates = MediaContent.Templates;
        string template = templates.Find(fact.Kind, fact.Sub)?.Manchetes.GetValueOrDefault(tone) is { Count: > 0 } list ? Rng.Pick(list) : templates.Padrao[tone];
        string headline = Texts.Fill(template,
            ("ator", fact.ActorId is null ? "" : NameOf(fact.ActorId)),
            ("alvo", fact.TargetId is null ? "" : NameOf(fact.TargetId)),
            ("zona", fact.ZoneId is null ? Context.Municipio.Nome : ZoneName(fact.ZoneId)),
            ("tema", ThemeName(fact.Theme)),
            ("texto", fact.Text));
        if (fact.Rumor)
        {
            headline = templates.BoatoPrefixo + headline;
        }

        if (headline.Length > TemplateCatalog.MaxHeadline)
        {
            headline = headline[..(TemplateCatalog.MaxHeadline - 1)].TrimEnd() + "…";
        }

        var news = new NewsItem
        {
            Id = $"n{Media.NextId++}",
            FactId = fact.Id,
            OutletId = outlet.Id,
            Date = fact.Date > Today ? fact.Date : Today,
            Tone = tone,
            Headline = headline,
            Boato = fact.Rumor,
            Theme = fact.Theme,
            ZoneId = fact.ZoneId,
            ActorId = fact.ActorId,
            CitaJogador = fact.ActorId == RaceSetup.PlayerId || fact.TargetId == RaceSetup.PlayerId,
            Lado = side,
        };
        Media.News.Add(news);
        if (Media.News.Count > MediaRules.MaxNoticias)
        {
            Media.News.RemoveRange(0, Media.News.Count - MediaRules.MaxNoticias);
        }

        // Ataque e boato já têm o próprio dano; a notícia sobre eles não soma imagem a quem atacou.
        double image = 0;
        if (fact.ActorId is not null && Campaign.Find(fact.ActorId) is { } actor && fact.Kind is not (FactKind.Ataque or FactKind.Boato))
        {
            image = Math.Clamp(actor.Image + NewsImage(outlet, tone), -1, 1) - actor.Image;
            actor.Image += image;
        }

        return (news, image);
    }

    /// <summary>
    /// Virada da semana: os temas com notícias bastantes na semana ficam em alta nas zonas atingidas, e o horário
    /// eleitoral gratuito rende presença a cada candidato pelo tempo do partido.
    /// </summary>
    private void MediaWeek(DateOnly nextWeekStart)
    {
        Media.Hot.RemoveAll(h => h.Until < nextWeekStart);
        DateOnly from = nextWeekStart.AddDays(-7);
        // Conta o noticiário sobre o que acontece na cidade e no país (fatos sem autor), não a propaganda dos
        // candidatos: senão quem fala de um tema o poria em alta sozinho.
        foreach (var group in Media.News.Where(n => n.Theme is not null && n.ActorId is null && n.Date >= from && n.Date < nextWeekStart && !n.Boato).GroupBy(n => n.Theme!).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            if (group.Count() < MediaRules.TemaEmAltaNoticias || group.Select(n => n.FactId).Distinct().Count() < MediaRules.TemaEmAltaFatos
                || Media.Hot.Any(h => h.Theme == group.Key && h.Until >= nextWeekStart))
            {
                continue;
            }

            var zones = new List<string>();
            for (int z = 0; z < Territory.Zones.Count; z++)
            {
                bool reached = group.Any(n => n.ZoneId == Territory.Zones[z] || (City.Outlet(n.OutletId) is { } o && z < o.Reach.Count && o.Reach[z] >= MediaRules.TemaEmAltaAlcanceMinimo));
                if (reached)
                {
                    zones.Add(Territory.Zones[z]);
                }
            }

            if (zones.Count == 0)
            {
                continue;
            }

            Media.Hot.Add(new HotTheme { Theme = group.Key, Zones = zones, From = nextWeekStart, Until = nextWeekStart.AddDays((7 * MediaRules.TemaEmAltaSemanas) - 1), News = group.Count() });
            Emit(FactKind.TemaEmAlta, Texts.Fill(OutletContent.Texto("tema_em_alta"), ("tema", ThemeName(group.Key))), theme: group.Key, visibility: 0);
        }

        FreeAirtimeWeek(nextWeekStart);
    }

    /// <summary>O tema está em alta na zona nessa data (sem zona: em alguma zona da cidade).</summary>
    public HotTheme? HotThemeOn(string theme, string? zoneId, DateOnly date) =>
        Media.Hot.FirstOrDefault(h => h.Theme == theme && date >= h.From && date <= h.Until && (zoneId is null || h.Zones.Contains(zoneId)));

    // ------------------------------------------------------------------ entrevista num veículo

    private void InterviewFactors(CommitmentRequest r, List<YieldFactor> factors)
    {
        if (r.OutletId is null || City.Outlet(r.OutletId) is not { } outlet)
        {
            return;
        }

        double average = City.Outlets.Average(ReachShare);
        if (average > 0)
        {
            factors.Add(Factor("veiculo_alcance", Texts.Fill(OutletContent.Texto("fator_alcance"), ("veiculo", OutletLabel(outlet))), ReachShare(outlet) / average));
        }

        string stance = StanceToward(outlet, RaceSetup.PlayerId);
        if (stance == "critico")
        {
            factors.Add(Factor("veiculo_critico", OutletContent.Texto("fator_veiculo_critico"), MediaRules.EntrevistaCriticoCredito));
        }
        else if (stance == "amigo")
        {
            factors.Add(Factor("veiculo_amigo", OutletContent.Texto("fator_veiculo_amigo"), MediaRules.EntrevistaAmigoCredito));
        }
    }

    /// <summary>Quanto a chance do teste muda pelo veículo: o crítico pergunta mais duro; a relação com o jornalista pesa.</summary>
    private double InterviewChanceShift(CommitmentRequest r)
    {
        if (r.OutletId is null || City.Outlet(r.OutletId) is not { } outlet)
        {
            return 0;
        }

        string stance = StanceToward(outlet, RaceSetup.PlayerId);
        return (stance == "critico" ? MediaRules.EntrevistaCriticoChance : stance == "amigo" ? MediaRules.EntrevistaAmigoChance : 0)
            + (RelationOf(outlet.JournalistId) * MediaRules.EntrevistaChancePorRelacao);
    }

    /// <summary>Depois da entrevista: o veículo publica, e o jornalista lembra.</summary>
    private void AfterInterview(Commitment c, Fact fact)
    {
        if (c.OutletId is null || City.Outlet(c.OutletId) is not { } outlet)
        {
            return;
        }

        Publish(fact, outlet);
        MemoryDefinition memory = OutletContent.Lembrancas["entrevista"];
        Person journalist = City.Person(outlet.JournalistId)!;
        journalist.LastContact = Today;
        journalist.Remember(Today, "entrevista", memory.Peso, memory.Pontos, Texts.Fill(memory.Texto, ("veiculo", outlet.Name)), PeopleConfig);
    }

    // ------------------------------------------------------------------ sugerir pauta

    public CommandFailure? CanSuggestStory(string outletId, string factId)
    {
        if (!CampaignRunning)
        {
            return CommandFailure.ForaDaCampanha;
        }

        if (City.Outlet(outletId) is not { } outlet)
        {
            return CommandFailure.VeiculoInvalido;
        }

        if (Facts.Find(factId) is not { } fact || fact.ActorId is null || Campaign.Find(fact.ActorId) is null || fact.Rumor)
        {
            return CommandFailure.NaoEncontrado;
        }

        if (Today.DayNumber - fact.Date.DayNumber > MediaRules.PautaDias)
        {
            return CommandFailure.PrazoVencido;
        }

        bool sameWeek = Media.LastPitch.TryGetValue(outlet.JournalistId, out DateOnly last) && last >= State.Calendar.WeekStart;
        return sameWeek || Media.News.Any(n => n.FactId == factId && n.OutletId == outletId) ? CommandFailure.JaFeito : null;
    }

    private double StoryChance(Outlet outlet, Fact fact) => Math.Clamp(
        MediaRules.PautaChanceBase + (RelationOf(outlet.JournalistId) * MediaRules.PautaChancePorRelacao) + (fact.Theme is not null && outlet.Themes.Contains(fact.Theme) ? MediaRules.PautaInteresse : 0),
        0.05, 0.95);

    /// <summary>Fatos recentes que servem de pauta: atos do jogador e de rivais (o mais novo primeiro).</summary>
    public IReadOnlyList<Fact> StoryCandidates() =>
        Facts.Items.Where(f => f.ActorId is not null && !f.Rumor && Campaign.Find(f.ActorId) is not null && Today.DayNumber - f.Date.DayNumber <= MediaRules.PautaDias)
            .OrderByDescending(f => f.Seq).ToList();

    /// <summary>
    /// Previsão de sugerir a pauta: a chance de o jornalista aceitar (pela relação e pelo interesse do veículo) e a
    /// faixa de imagem de quem fez o fato, pelo tom que a linha do veículo pode dar.
    /// </summary>
    public CommandForecast ForecastStory(string outletId, string factId)
    {
        CommandFailure? blocked = CanSuggestStory(outletId, factId);
        if (City.Outlet(outletId) is not { } outlet || Facts.Find(factId) is not { } fact || fact.ActorId is null || Campaign.Find(fact.ActorId) is not { } actor)
        {
            return new CommandForecast(blocked ?? CommandFailure.NaoEncontrado, [], [], [OutletContent.Texto("pauta_explica")]);
        }

        double chance = StoryChance(outlet, fact);
        string side = SideOf(fact.ActorId);
        Dictionary<string, double> tones = TonesFor(outlet, side);
        double total = tones.Values.Sum();
        var deltas = Tone.All.Select(t => Math.Clamp(actor.Image + NewsImage(outlet, t), -1, 1) - actor.Image).ToList();
        double expected = chance * Tone.All.Sum(t => tones.GetValueOrDefault(t) / total * (Math.Clamp(actor.Image + NewsImage(outlet, t), -1, 1) - actor.Image));
        string kind = actor.IsPlayer ? "image" : "rival_image";
        var lines = new List<ForecastLine> { new(kind, actor.IsPlayer ? null : actor.Id, Math.Min(0, deltas.Min()), Math.Max(0, deltas.Max()), expected) };
        MemoryDefinition good = OutletContent.Lembrancas["pauta_boa"];
        MemoryDefinition bad = OutletContent.Lembrancas["pauta_fraca"];
        lines.Add(new ForecastLine("relation", outlet.JournalistId, Math.Min(good.Pontos, bad.Pontos), Math.Max(good.Pontos, bad.Pontos), (chance * good.Pontos) + ((1 - chance) * bad.Pontos)));
        var outcomes = new List<ForecastOutcome>
        {
            new(chance, OutletContent.Texto("pauta_sucesso")),
            new(1 - chance, OutletContent.Texto("pauta_falha")),
        };
        var explanation = new List<string>
        {
            Texts.Fill(OutletContent.Texto("pauta_explica"), ("jornalista", NameOf(outlet.JournalistId)), ("veiculo", OutletLabel(outlet)), ("chance", Texts.Percent(chance)), ("fato", fact.Text)),
        };
        explanation.AddRange(tones.Where(t => t.Value > 0).OrderByDescending(t => t.Value).Select(t => $"{Texts.Percent(t.Value / total)}: {t.Key}"));
        return new CommandForecast(blocked, lines, outcomes, explanation);
    }

    /// <summary>Sugere a pauta ao jornalista principal do veículo: se ele aceitar, o veículo publica o fato no tom da linha dele.</summary>
    public (CommandFailure? Failure, StoryResult? Result) SuggestStory(string outletId, string factId)
    {
        if (CanSuggestStory(outletId, factId) is { } failure)
        {
            return (failure, null);
        }

        Outlet outlet = City.Outlet(outletId)!;
        Fact fact = Facts.Find(factId)!;
        Person journalist = City.Person(outlet.JournalistId)!;
        Media.LastPitch[outlet.JournalistId] = Today;
        journalist.LastContact = Today;
        if (Rng.Chance(StoryChance(outlet, fact)))
        {
            var (news, image) = Publish(fact, outlet);
            MemoryDefinition good = OutletContent.Lembrancas["pauta_boa"];
            Memory memory = journalist.Remember(Today, "pauta_boa", good.Peso, good.Pontos, good.Texto, PeopleConfig);
            return (null, new StoryResult(true, news, image, memory, OutletContent.Texto("pauta_sucesso")));
        }

        MemoryDefinition bad = OutletContent.Lembrancas["pauta_fraca"];
        Memory weak = journalist.Remember(Today, "pauta_fraca", bad.Peso, bad.Pontos, bad.Texto, PeopleConfig);
        return (null, new StoryResult(false, null, 0, weak, OutletContent.Texto("pauta_falha")));
    }

    // ------------------------------------------------------------------ anunciar na imprensa escrita (art. 43)

    public decimal AdCost(Outlet outlet) => ScaledTens(OutletContent.Type(outlet.Type).AnuncioCusto);

    public int AdsLeft(Outlet outlet) =>
        OutletContent.Type(outlet.Type).Escrita ? Math.Max(0, PropagandaLaw.AnunciosPorVeiculo - Media.AdDates.GetValueOrDefault(outlet.Id, []).Count) : 0;

    public CommandFailure? CanAdvertise(string outletId)
    {
        if (!CampaignRunning)
        {
            return CommandFailure.ForaDaCampanha;
        }

        if (City.Outlet(outletId) is not { } outlet)
        {
            return CommandFailure.VeiculoInvalido;
        }

        OutletType type = OutletContent.Type(outlet.Type);
        if (!type.Escrita)
        {
            // Rádio e TV só no horário gratuito (art. 44); propaganda paga na internet só por impulsionamento (art. 57-C).
            return type.Emissora ? CommandFailure.GastoVedado : CommandFailure.CanalFechado;
        }

        if (Today < PropagandaLaw.PrimeiroDiaDePropaganda(Campaign.ElectionDate.Year))
        {
            return CommandFailure.CanalFechado;
        }

        if (!PropagandaLaw.PodeAnunciar(Media.AdDates.GetValueOrDefault(outlet.Id, []), Today, Campaign.ElectionDate))
        {
            return CommandFailure.LimiteLegal;
        }

        return State.Player.Resources.Money < AdCost(outlet) ? CommandFailure.SemDinheiro : null;
    }

    private Dictionary<string, double> AdGains(Outlet outlet, double luck)
    {
        var gains = new Dictionary<string, double>(StringComparer.Ordinal);
        double[] own = Territory.Presence[RaceSetup.PlayerId];
        for (int z = 0; z < own.Length && z < outlet.Reach.Count; z++)
        {
            gains[Territory.Zones[z]] = EffectApplier.Gain(own[z], MediaRules.AnuncioPresenca * outlet.Reach[z] * RecognitionScale * luck) - own[z];
        }

        return gains;
    }

    /// <summary>Previsão de um anúncio pago na imprensa escrita: presença por zona alcançada (em faixa) e o custo.</summary>
    public CommandForecast ForecastAd(string outletId)
    {
        CommandFailure? blocked = CanAdvertise(outletId);
        if (City.Outlet(outletId) is not { } outlet)
        {
            return new CommandForecast(blocked ?? CommandFailure.VeiculoInvalido, [], [], [OutletContent.Texto("anuncio_explica")]);
        }

        var luck = LuckRange(MediaRules.Sorte);
        Dictionary<string, double> low = AdGains(outlet, luck.Min);
        Dictionary<string, double> high = AdGains(outlet, luck.Max);
        Dictionary<string, double> mid = AdGains(outlet, 1);
        decimal cost = AdCost(outlet);
        var lines = low.Keys.Select(z => new ForecastLine("presence", z, low[z], high[z], mid[z])).ToList();
        lines.Add(new ForecastLine("money", null, -(double)cost, -(double)cost, -(double)cost));
        var explanation = new List<string>
        {
            Texts.Fill(OutletContent.Texto("anuncio_explica"), ("veiculo", OutletLabel(outlet)), ("custo", Texts.Money(cost)), ("alcance", Texts.Percent(ReachShare(outlet)))),
            Texts.Fill(OutletContent.Texto("anuncio_limite"), ("artigo", PropagandaLaw.ArtigoImprensaEscrita), ("limite", Texts.Number(PropagandaLaw.AnunciosPorVeiculo)), ("restam", Texts.Number(AdsLeft(outlet))),
                ("ultimo", Texts.Date(PropagandaLaw.UltimoDiaDeAnuncio(Campaign.ElectionDate)))),
        };
        return new CommandForecast(blocked, lines, [], explanation);
    }

    /// <summary>Paga um anúncio no veículo (imprensa escrita): no máximo 10 por veículo, em datas diversas, até a antevéspera.</summary>
    public (CommandFailure? Failure, AdResult? Result) Advertise(string outletId)
    {
        if (CanAdvertise(outletId) is { } failure)
        {
            return (failure, null);
        }

        Outlet outlet = City.Outlet(outletId)!;
        var range = LuckRange(MediaRules.Sorte);
        Dictionary<string, double> gains = AdGains(outlet, Rng.NextDouble(range.Min, range.Max));
        double[] own = Territory.Presence[RaceSetup.PlayerId];
        foreach (var (zone, delta) in gains)
        {
            int z = Territory.IndexOf(zone);
            own[z] = Math.Clamp(own[z] + delta, 0, 100);
        }

        Campaign.Player.Recognition = Territory.Recognition(RaceSetup.PlayerId);
        decimal cost = AdCost(outlet);
        Pay(cost);
        if (!Media.AdDates.TryGetValue(outlet.Id, out List<DateOnly>? dates))
        {
            Media.AdDates[outlet.Id] = dates = [];
        }

        dates.Add(Today);
        MemoryDefinition memory = OutletContent.Lembrancas["anuncio"];
        City.Person(outlet.OwnerId)?.Remember(Today, "anuncio", memory.Peso, memory.Pontos, memory.Texto, PeopleConfig);
        string text = Texts.Fill(OutletContent.Texto("anuncio_feito"), ("veiculo", OutletLabel(outlet)), ("valor", Texts.Money(cost)));
        Emit(FactKind.Anuncio, text, RaceSetup.PlayerId, sub: outlet.Id);
        return (null, new AdResult(gains, -cost, AdsLeft(outlet), text));
    }

    // ------------------------------------------------------------------ horário eleitoral gratuito (art. 47)

    private bool HasBroadcaster => City.Outlets.Any(o => OutletContent.Type(o.Type).Emissora);

    /// <summary>Fração do tempo de cada partido da disputa do cargo: 90% pela bancada na Câmara resultante da eleição, 10% igual.</summary>
    private Dictionary<string, double> AirtimeShares(Office office)
    {
        List<string> parties = Campaign.ForOffice(office).Select(c => c.PartyId).Distinct().Order(StringComparer.Ordinal).ToList();
        var rules = PartyFundingRulesConfig.Default with { TvCamaraSeatsShare = PropagandaLaw.TempoProporcionalABancada, TvEqualShare = PropagandaLaw.TempoIgualitario };
        return PartyFunding.TvTimeShares(parties, Context.CamaraSeats ?? new Dictionary<string, int>(), rules);
    }

    private double AirtimePresence(Candidate c, Dictionary<string, double> shares)
    {
        // O tempo do partido é repartido entre os candidatos dele ao cargo (o coletivo conta como um bloco).
        int inParty = Campaign.ForOffice(c.Office).Count(x => x.PartyId == c.PartyId);
        double share = shares.GetValueOrDefault(c.PartyId) * shares.Count;
        double office = PropagandaLaw.MinutosDiarios(c.Office) / PropagandaLaw.InsercoesMinutosDiarios;
        double staff = c.IsPlayer ? StaffEffect(StaffRole.Marqueteiro, StaffRole.Rendimento) : 1;
        return MediaRules.HorarioGratuitoPresenca * RecognitionScale * share * office * staff / Math.Max(1, inParty);
    }

    private void FreeAirtimeWeek(DateOnly nextWeekStart)
    {
        DateOnly starts = PropagandaLaw.HorarioGratuitoComeca(Campaign.ElectionDate);
        DateOnly week = nextWeekStart.AddDays(-7);
        if (week.AddDays(6) < starts || week > PropagandaLaw.HorarioGratuitoTermina(Campaign.ElectionDate))
        {
            return;
        }

        bool first = week <= starts;
        if (!HasBroadcaster && PropagandaLaw.SoOndeHaEmissora)
        {
            if (first)
            {
                Emit(FactKind.HorarioGratuito, OutletContent.Texto("horario_sem_emissora"), visibility: 0);
            }

            return;
        }

        if (first)
        {
            Emit(FactKind.HorarioGratuito, OutletContent.Texto("horario_gratuito"));
        }

        foreach (Office office in new[] { Office.Vereador, Office.Prefeito })
        {
            Dictionary<string, double> shares = AirtimeShares(office);
            foreach (Candidate c in Campaign.ForOffice(office))
            {
                Territory.GainEverywhere(c.Id, AirtimePresence(c, shares));
                c.Recognition = Territory.Recognition(c.Id);
            }
        }
    }

    /// <summary>Horário gratuito para a tela: quando vai ao ar, a fatia do partido do jogador e o que rende por semana.</summary>
    public FreeAirtimeView FreeAirtime()
    {
        Candidate player = Campaign.Player;
        Dictionary<string, double> shares = AirtimeShares(player.Office);
        double share = shares.GetValueOrDefault(player.PartyId);
        bool has = HasBroadcaster || !PropagandaLaw.SoOndeHaEmissora;
        DateOnly starts = PropagandaLaw.HorarioGratuitoComeca(Campaign.ElectionDate);
        DateOnly ends = PropagandaLaw.HorarioGratuitoTermina(Campaign.ElectionDate);
        return new FreeAirtimeView(has, starts, ends, has && PropagandaLaw.NoHorarioGratuito(Today, Campaign.ElectionDate), share, share * PropagandaLaw.MinutosDiarios(player.Office),
            has ? AirtimePresence(player, shares) : 0, Context.CamaraSeats is not null, OutletContent.Texto(has ? "horario_gratuito" : "horario_sem_emissora"));
    }

    // ------------------------------------------------------------------ consultas

    /// <summary>Veículos da cidade, com linha, alcance, dono, jornalista e o que dá para fazer em cada um.</summary>
    public IReadOnlyList<OutletView> Outlets() => City.Outlets.Select(o =>
    {
        OutletType type = OutletContent.Type(o.Type);
        return new OutletView(
            o, OutletLabel(o), type.Nome, OutletContent.Line(o.Line).Nome, ReachShare(o),
            Territory.Zones.Select((zone, z) => (zone, Reach: z < o.Reach.Count ? o.Reach[z] : 0)).ToDictionary(x => x.zone, x => x.Reach, StringComparer.Ordinal),
            NameOf(o.OwnerId), NameOf(o.JournalistId), RelationOf(o.JournalistId), StanceToward(o, RaceSetup.PlayerId), type.Emissora, type.Escrita, AdsLeft(o), type.Escrita ? AdCost(o) : 0);
    }).ToList();

    /// <summary>Feed de notícias, da mais nova para a mais velha, cada uma com o fato de origem.</summary>
    public IReadOnlyList<NewsView> NewsFeed(int max = 50) =>
        Media.News.AsEnumerable().Reverse().Take(max)
            .Select(n => new NewsView(n, Facts.Find(n.FactId), City.Outlet(n.OutletId) is { } o ? OutletLabel(o) : n.OutletId)).ToList();

    /// <summary>Temas em alta hoje, com as zonas e o bônus do compromisso que trata do tema.</summary>
    public IReadOnlyList<HotThemeView> HotThemes() =>
        Media.Hot.Where(h => Today >= h.From && Today <= h.Until)
            .Select(h => new HotThemeView(h.Theme, ThemeName(h.Theme), h.Zones, h.Until, MediaRules.TemaEmAltaBonus, h.News)).ToList();
}
