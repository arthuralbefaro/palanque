using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Media;
using Planalto.Core.State;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Mídia local (GDD 17.13): a área Mídia (ataques com prazo, tema em alta, veículos, horário gratuito e o noticiário
/// com o fato de origem e a marca de boato) e a ficha do veículo (linha percebida, alcance, entrevista, sugerir pauta
/// e anúncio com o limite legal). Só lê o Core e chama os comandos dele.
/// </summary>
public partial class HubScreen
{
    private string? _storyFact;

    private PropagandaLawConfig PropagandaLaw => _engine.Context.PropagandaLaw;

    private string StanceText(string stance) => stance switch
    {
        "critico" => Tr("crítico a você"),
        "amigo" => Tr("amigo"),
        _ => Tr("neutro com você"),
    };

    private string ToneText(string tone) => tone switch
    {
        Tone.Favoravel => Tr("tom favorável"),
        Tone.Critico => Tr("tom crítico"),
        _ => Tr("tom neutro"),
    };

    /// <summary>Tema em alta que vale para a zona nessa data (sem zona: em alguma zona da cidade); null se não há.</summary>
    private HotThemeView? HotIn(string? zoneId) =>
        _engine.HotThemes().FirstOrDefault(h => zoneId is null || h.Zones.Contains(zoneId));

    private string HotZonesText(HotThemeView hot) =>
        hot.Zones.Count >= Territory.Zones.Count ? Tr("na cidade inteira")
        : string.Format(Tr("em {0}"), string.Join(", ", hot.Zones.Take(4).Select(ZoneName)) + (hot.Zones.Count > 4 ? " " + string.Format(Tr("e mais {0}"), hot.Zones.Count - 4) : ""));

    // ------------------------------------------------------------------ área Mídia

    private void BuildMedia()
    {
        var pending = _engine.PendingAttacks();
        if (pending.Count > 0)
        {
            Heading(_left, Tr("Ataques contra você"), 20);
            foreach (PendingAttackView view in pending)
            {
                PendingAttackCard(_left, view);
            }
        }

        Heading(_left, Tr("Tema em alta"), 20);
        var hot = _engine.HotThemes();
        if (hot.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhum tema em alta nesta semana. Uma semana de notícias sobre o mesmo assunto põe o tema em alta nas zonas que o noticiário alcança."));
        }

        foreach (HotThemeView h in hot)
        {
            PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Linha, 10);
            card.Name = "TemaEmAlta_" + h.Theme;
            Glossary.Rich(card, string.Format(PtBr, Tr("{0}: em alta {1} até {2:dd/MM}, depois de {3} notícias. Compromisso em que você fala desse tema rende {4} nessas zonas."),
                h.ThemeName, HotZonesText(h), h.Until, h.News, Percent(1 + h.Bonus)), 18, Palette.Texto);
        }

        Heading(_left, Tr("Veículos da cidade"), 20);
        var outlets = _engine.Outlets();
        if (outlets.Count == 0)
        {
            Glossary.Rich(_left, Tr("Esta cidade não tem veículo de notícias próprio."));
        }

        foreach (OutletView outlet in outlets)
        {
            OutletRow(_left, outlet);
        }

        BuildAirtime();

        Heading(_left, Tr("Notícias"), 20);
        var news = _engine.NewsFeed(30);
        if (news.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nada saiu ainda. Os veículos cobrem o que acontece na cidade: nenhuma notícia existe sem um fato."));
        }

        foreach (NewsView item in news)
        {
            NewsCard(_left, item);
        }
    }

    /// <summary>Linha de veículo: nome com o que é, alcance e relação com o jogador; abre a ficha.</summary>
    private void OutletRow(Node parent, OutletView o)
    {
        PanelContainer card = ClickCard(parent, () => OpenAreaSheet(Sheet.Veiculo, o.Outlet.Id),
            Palette.Tip(o.Label + ".", Tr("abre a ficha do veículo: linha, alcance por zona, dono, jornalista e o que dá para fazer nele (entrevista, pauta, anúncio)."), Tr("nada.")),
            "Veiculo_" + o.Outlet.Id);
        var col = Ui.Column(card, 2);
        Glossary.Plain(col, o.Label, 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, string.Format(PtBr, Tr("alcança {0:P0} dos eleitores · {1} · jornalista: {2} (relação {3})"), o.ReachShare, StanceText(o.Stance), o.JournalistName, Signed(o.JournalistRelation, "0")));
    }

    /// <summary>Horário eleitoral gratuito (art. 47): quando começa, o tempo do partido e o que rende.</summary>
    private void BuildAirtime()
    {
        FreeAirtimeView air = _engine.FreeAirtime();
        Heading(_left, Tr("Horário eleitoral gratuito"), 20);
        PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
        card.Name = "HorarioGratuito";
        var col = Ui.Column(card, 4);
        if (!air.HasBroadcaster)
        {
            Glossary.Rich(col, air.Text + ".", 18, Palette.Texto);
            Glossary.Rich(col, string.Format(Tr("Base legal: {0}."), PropagandaLaw.ArtigoEmissora), 18, Palette.Texto3);
            return;
        }

        Glossary.Rich(col, string.Format(PtBr, Tr(air.OnAir ? "No ar até {1:dd/MM}." : "Começa em {0:dd/MM} e vai até {1:dd/MM}."), air.Starts, air.Ends), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
        string office = Campaign.Player.Office == Office.Prefeito
            ? string.Format(Tr("blocos de {0} minutos e inserções"), PropagandaLaw.BlocoPrefeitoMinutos)
            : Tr("só inserções: vereador não tem bloco");
        Glossary.Rich(col, string.Format(PtBr, Tr("Seu partido tem {0:P1} do tempo: cerca de {1:0.0} minutos por dia ({2}). Rende a você cerca de {3} de presença por semana em todas as zonas."),
            air.PartyShare, air.MinutesDaily, office, Signed(air.WeeklyPresence, "0.00")), 18, Palette.Texto);
        Glossary.Rich(col, air.SeatsKnown
            ? string.Format(PtBr, Tr("Divisão: {0:P0} pela bancada de cada partido na Câmara dos Deputados e {1:P0} por igual."), PropagandaLaw.TempoProporcionalABancada, PropagandaLaw.TempoIgualitario)
            : Tr("Neste mundo não há bancada federal registrada: o tempo é repartido por igual entre os partidos da disputa."));
        Glossary.Rich(col, string.Format(Tr("Rádio e TV só no horário gratuito: propaganda paga é proibida. Base legal: {0}; {1}."), PropagandaLaw.ArtigoHorarioGratuito, PropagandaLaw.ArtigoRadioETv), 18, Palette.Texto3);
    }

    /// <summary>Notícia de um veículo: o tom, a manchete, a marca de boato e o fato de origem. Clicar centraliza o mapa na zona do fato.</summary>
    private void NewsCard(Node parent, NewsView item)
    {
        NewsItem n = item.News;
        string? zone = n.ZoneId is not null && _map.Zonas.ContainsKey(n.ZoneId) ? n.ZoneId : null;
        PanelContainer card = zone is not null
            ? ClickCard(parent, () => { _map.CenterOn(zone); _map.Select(zone); },
                Palette.Tip(string.Format(Tr("Notícia de {0}, {1}. O fato aconteceu em {2}."), item.OutletLabel, n.Date.ToString("dd/MM", PtBr), ZoneName(zone)), Tr("centraliza o mapa na zona do fato e a destaca."), Tr("nada.")))
            : Card(parent);
        card.Name = "Noticia_" + n.Id;
        var col = Ui.Column(card, 4);
        Glossary.Plain(col, $"{item.OutletLabel} · {n.Date.ToString("dd/MM", PtBr)} · {ToneText(n.Tone)}", 18, Palette.Texto3);
        Glossary.Rich(col, n.Headline, 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        if (n.Boato)
        {
            Glossary.Rich(col, Tr("BOATO de rede: pode ser falso."), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600)).Name = "MarcaBoato";
        }

        if (item.Fact is { } fact)
        {
            Glossary.Rich(col, string.Format(Tr("Fato de origem ({0}): {1}."), fact.Date.ToString("dd/MM", PtBr), fact.Text.TrimEnd('.')), 18, Palette.Texto2).Name = "FatoDeOrigem";
        }
    }

    // ------------------------------------------------------------------ ficha do veículo

    private string AdFailureText(CommandFailure failure, OutletView o) => failure switch
    {
        CommandFailure.GastoVedado => string.Format(Tr("rádio e TV só no horário eleitoral gratuito: a lei proíbe propaganda paga neles ({0})."), PropagandaLaw.ArtigoRadioETv),
        CommandFailure.CanalFechado when !o.Written => Tr("anúncio pago só na imprensa escrita. Na internet, a lei só permite impulsionamento, que fica em Campanha, Verba, canal Redes (Lei 9.504/1997, art. 57-C)."),
        CommandFailure.CanalFechado => string.Format(Tr("propaganda só a partir de {0} ({1})."), PropagandaLaw.PrimeiroDiaDePropaganda(Campaign.ElectionDate.Year).ToString("d 'de' MMMM", PtBr), PropagandaLaw.ArtigoInicio),
        CommandFailure.LimiteLegal => string.Format(Tr("limite legal: até {0} anúncios por veículo, em datas diversas, até a antevéspera da eleição ({1}). Restam {2} neste veículo; hoje não dá."), PropagandaLaw.AnunciosPorVeiculo, PropagandaLaw.ArtigoImprensaEscrita, o.AdsLeft),
        CommandFailure.SemDinheiro => string.Format(Tr("faltam {0} em caixa."), Money(Math.Max(0, o.AdCost - State.Player.Resources.Money))),
        CommandFailure.ForaDaCampanha => Tr("só dá para anunciar durante a campanha."),
        _ => Tr("não dá para anunciar neste veículo."),
    };

    private string StoryFailureText(CommandFailure failure, OutletView o) => failure switch
    {
        CommandFailure.JaFeito => string.Format(Tr("{0} já recebeu uma pauta sua nesta semana, ou o veículo já publicou esse fato."), o.JournalistName),
        CommandFailure.PrazoVencido => Tr("o fato é velho demais para virar pauta."),
        CommandFailure.NaoEncontrado => Tr("falta um fato recente, seu ou de um rival, para sugerir."),
        CommandFailure.ForaDaCampanha => Tr("só dá para sugerir pauta durante a pré-campanha e a campanha."),
        _ => Tr("não dá para sugerir pauta a este veículo."),
    };

    private void BuildOutletSheet(OutletView o)
    {
        Outlet outlet = o.Outlet;
        BackButton();
        NameTitle(_left, outlet.Name);
        Glossary.Rich(_left, string.Format(Tr("{0}, {1}. Veículo fictício."), UpperFirst(o.TypeName), o.LineName));

        // Linha editorial percebida.
        Heading(_left, Tr("Linha editorial"), 20);
        string government = _engine.GovernmentPartyId is { } gov ? PartySigla(gov) : Tr("ninguém");
        string line = outlet.Line switch
        {
            EditorialLine.Governista => Tr("governista: tende a tratar bem quem está com a prefeitura e mal quem está contra"),
            EditorialLine.Critica => Tr("crítica: tende a tratar mal quem está com a prefeitura e bem quem está contra"),
            _ => Tr("independente: não tem lado fixo em relação à prefeitura"),
        };
        Glossary.Rich(_left, string.Format(Tr("{0}. A prefeitura hoje é do {1}. A linha decide o tom da notícia, nunca o fato."), UpperFirst(line), government), 18, Palette.Texto);
        ValueRow(_left, Tr("Com você"), StanceText(o.Stance), highlight: true);
        Glossary.Rich(_left, string.Format(Tr("Temas de interesse: {0}."), string.Join(", ", outlet.Themes.Select(ThemeName))));
        string sensational = outlet.Sensationalism >= 0.66 ? Tr("alto") : outlet.Sensationalism >= 0.33 ? Tr("médio") : Tr("baixo");
        ValueRow(_left, Tr("Carrega no tom"), sensational);

        Heading(_left, Tr("Alcance"), 20);
        ValueRow(_left, Tr("Eleitores da cidade"), o.ReachShare.ToString("P0", PtBr), highlight: true);
        foreach (var (zone, reach) in o.ReachByZone.OrderByDescending(r => r.Value).Take(3))
        {
            ValueRow(_left, ZoneName(zone), reach.ToString("P0", PtBr));
        }

        if (outlet.Publico != CommitmentType.PublicoGeral)
        {
            Glossary.Rich(_left, string.Format(Tr("Mais forte no grupo {0}."), EffectSummary.GroupName(outlet.Publico)));
        }

        Heading(_left, Tr("Quem manda e quem escreve"), 20);
        PersonLink(_left, outlet.OwnerId, string.Format(Tr("Quem manda: {0}"), o.OwnerName));
        PersonLink(_left, outlet.JournalistId, string.Format(Tr("Jornalista: {0} (relação {1})"), o.JournalistName, Signed(o.JournalistRelation, "0")));

        // Entrevista.
        Heading(_left, Tr("O que dá para fazer"), 20);
        CommitmentType interview = Catalog.Get(CommitmentCatalog.Entrevista);
        var schedule = Ui.Button(_left, "Marcar entrevista aqui", () =>
        {
            var (date, turno) = _engine.NextFreeSlot();
            StartSchedule(date, turno, outlet: outlet.Id);
        }, Palette.Tip(
            string.Format(Tr("Abre a marcação de uma entrevista em {0}."), o.Label),
            Tr("veículo crítico pergunta mais duro e convence mais; veículo amigo é seguro e convence pouco. A previsão mostra a diferença antes de confirmar."),
            CostText(interview)));
        schedule.Name = "MarcarEntrevistaAqui";
        Palette.Primary(schedule);
        if (BlockReason() is { } block)
        {
            schedule.Disabled = true;
            schedule.TooltipText = Palette.Blocked(string.Format(Tr("{0}; resolva antes de marcar."), block), schedule.TooltipText);
        }

        StoryControls(o);
        AdControls(o);

        Heading(_left, Tr("O que este veículo publicou"), 20);
        var news = _engine.NewsFeed(200).Where(n => n.News.OutletId == outlet.Id).Take(5).ToList();
        if (news.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nada ainda."));
        }

        foreach (NewsView item in news)
        {
            NewsCard(_left, item);
        }
    }

    /// <summary>Nome de pessoa como link para a ficha (princípio j: nome fictício clicável).</summary>
    private void PersonLink(Node parent, string personId, string text)
    {
        if (!HasPersonSheet(personId))
        {
            Glossary.Plain(parent, text, 18, Palette.Texto);
            return;
        }

        var b = Ui.Button(parent, text, () => OpenPerson(personId),
            Palette.Tip(PersonLabel(personId) + ".", Tr("abre a ficha: relação, interesses e lembranças."), Tr("nada.")));
        b.Name = "Pessoa_" + personId;
        b.Alignment = HorizontalAlignment.Left;
        b.ClipText = true;
        b.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        Palette.ListRow(b);
    }

    private void StoryControls(OutletView o)
    {
        var candidates = _engine.StoryCandidates();
        Heading(_left, Tr("Sugerir pauta"), 20);
        if (candidates.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhum fato recente, seu ou de um rival, para sugerir. Compromissos, promessas e atos dos rivais viram fatos."));
            return;
        }

        if (_storyFact is null || candidates.All(f => f.Id != _storyFact))
        {
            // De saída, um ato seu que ainda caiba como pauta; senão, o fato mais novo que couber.
            var fits = candidates.Where(f => _engine.CanSuggestStory(o.Outlet.Id, f.Id) is null).ToList();
            _storyFact = (fits.FirstOrDefault(f => f.ActorId == RaceSetup.PlayerId) ?? fits.FirstOrDefault() ?? candidates[0]).Id;
        }

        FieldLabel("Fato");
        var shown = candidates.Take(12).ToList();
        if (shown.All(f => f.Id != _storyFact))
        {
            shown.Insert(0, candidates.First(f => f.Id == _storyFact));
        }

        var facts = Ui.Options(_left, shown.Select(f => (f.Id, $"{f.Date.ToString("dd/MM", PtBr)} · {f.Text}")), _storyFact, id => { _storyFact = id; Refresh(); }, translate: false);
        facts.Name = "FatoDaPauta";
        facts.ClipText = true;
        facts.TooltipText = Palette.Tip(Tr("O fato que você sugere ao jornalista."), Tr("pode ser um ato seu ou de um rival; o veículo publica o fato no tom da linha dele, se aceitar."), Tr("nada em dinheiro; pauta fraca desgasta a relação com o jornalista."));

        string factId = _storyFact;
        CommandForecast forecast = _engine.ForecastStory(o.Outlet.Id, factId);
        var explanation = forecast.Explanation.Take(1).ToList();
        var tones = forecast.Explanation.Skip(1).Select(l => l.Replace(Tone.Favoravel, Tr("tom favorável"), StringComparison.Ordinal).Replace(Tone.Critico, Tr("tom crítico"), StringComparison.Ordinal).Replace(Tone.Neutro, Tr("tom neutro"), StringComparison.Ordinal)).ToList();
        if (tones.Count > 0)
        {
            explanation.Add(Tr("Se entrar, o tom provável:") + " " + string.Join("; ", tones) + ".");
        }

        string who = string.Format(Tr("Quem vai lembrar: {0}."), o.JournalistName);
        VBoxContainer col = CommandForecastCard(_left, "PrevisaoPauta", Tr("Previsão da pauta"), forecast, explanation, [who]);
        string tip = Palette.Tip(
            string.Format(Tr("Sugere a pauta a {0}, de {1}."), o.JournalistName, o.Label),
            string.Join(" ", explanation) + " " + who,
            Tr("nada em dinheiro; uma pauta por jornalista por semana."));
        var button = Ui.Button(_left, "Sugerir a pauta", () =>
        {
            var (failure, result) = _engine.SuggestStory(o.Outlet.Id, factId);
            if (failure is not null || result is null)
            {
                Refresh();
                return;
            }

            var lines = new List<string> { result.Text };
            if (result.News is { } published)
            {
                lines.Add(string.Format(Tr("Saiu em {0}, em {1}: \"{2}\"."), o.Label, ToneText(published.Tone), published.Headline));
            }

            if (Math.Abs(result.ImageDelta) >= 0.0005)
            {
                lines.Add(string.Format(Tr("Imagem de quem fez o fato: {0} pontos."), Signed(result.ImageDelta * 100, "0.0")));
            }

            lines.Add(string.Format(Tr("{0} vai lembrar: \"{1}\" ({2} de relação)."), o.JournalistName, result.Memory.Text, Signed(result.Memory.Pontos, "0")));
            _log.Insert(0, string.Format(Tr("Pauta sugerida a {0}: {1}"), o.Label, result.Text));
            _changes = (Tr("Pauta sugerida"), lines);
            Refresh();
        }, tip);
        button.Name = "SugerirPauta";
        if (forecast.Blocked is { } blocked)
        {
            BlockCard(col, Tr("Não dá para sugerir:") + " " + StoryFailureText(blocked, o));
            button.Disabled = true;
            button.TooltipText = Palette.Blocked(StoryFailureText(blocked, o), tip);
        }
    }

    private void AdControls(OutletView o)
    {
        Heading(_left, Tr("Anunciar"), 20);
        CommandForecast forecast = _engine.ForecastAd(o.Outlet.Id);
        CommandFailure? blocked = forecast.Blocked;
        bool allowedHere = o.Written;
        string what = string.Format(Tr("Paga um anúncio de campanha em {0}."), o.Label);
        string tip;
        if (allowedHere)
        {
            string who = string.Format(Tr("Quem vai lembrar: {0}, que manda no veículo."), o.OwnerName);
            VBoxContainer col = CommandForecastCard(_left, "PrevisaoAnuncio", Tr("Previsão do anúncio"), forecast, extra: [who]);
            tip = Palette.Tip(what, string.Join(" ", forecast.Explanation) + " " + string.Join("; ", CommandLines(forecast)) + ".", Money(o.AdCost) + ".");
            if (blocked is { } reason)
            {
                BlockCard(col, Tr("Não dá para anunciar:") + " " + AdFailureText(reason, o));
            }
        }
        else
        {
            tip = Palette.Tip(what, Tr("neste tipo de veículo a lei não permite anúncio pago de campanha."), Tr("nada: o anúncio não existe aqui."));
            if (blocked is { } reason)
            {
                PanelContainer stop = Card(_left, Palette.CartaoAlto, Palette.Linha, 10);
                stop.Name = "AnuncioVedado";
                Glossary.Rich(stop, UpperFirst(AdFailureText(reason, o)), 18, Palette.Texto);
            }
        }

        var button = Ui.Button(_left, allowedHere ? $"{Tr("Anunciar")} · {Money(o.AdCost)}" : Tr("Anunciar"), () =>
        {
            var (failure, result) = _engine.Advertise(o.Outlet.Id);
            if (failure is not null || result is null)
            {
                Refresh();
                return;
            }

            var lines = new List<string> { result.Text + ".", string.Format(Tr("Caixa: {0}."), SignedMoney(result.Money)) };
            lines.AddRange(result.Presence.Where(p => Math.Abs(p.Value) >= 0.005).OrderByDescending(p => p.Value).Take(4)
                .Select(p => string.Format(Tr("{0} de presença em {1}."), Signed(p.Value, "0.00"), ZoneName(p.Key))));
            lines.Add(string.Format(Tr("Restam {0} anúncios neste veículo ({1})."), result.AdsLeft, PropagandaLaw.ArtigoImprensaEscrita));
            _log.Insert(0, result.Text + ".");
            _changes = (Tr("Anúncio publicado"), lines);
            Refresh();
        }, tip);
        button.Name = "Anunciar";
        button.ClipText = true;
        if (blocked is { } why)
        {
            button.Disabled = true;
            button.TooltipText = Palette.Blocked(AdFailureText(why, o), tip);
        }
    }
}
