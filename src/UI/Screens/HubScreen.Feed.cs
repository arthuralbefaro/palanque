using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.State;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Coluna direita (GDD 17.3): o feed com o resultado explicado de cada compromisso, o que rivais e lideranças fizeram
/// sozinhos, eventos e pesquisas; e a intenção de voto. Clicar num item com zona centraliza o mapa nela.
/// </summary>
public partial class HubScreen
{
    /// <param name="Who">De quem o cartão fala (você, um candidato ou outra pessoa): o retrato dele vai ao lado (RL).</param>
    private sealed record FeedEntry(DateOnly Date, int Order, string Source, string? Title, string Text, string? ZoneId, FeedItem? Item = null, string? Who = null);

    /// <summary>
    /// Resultado explicado de um compromisso (princípio f): o que mudou, onde, para quem e por quê.
    /// "+2,1 de presença em Centro; +0,4 em Vila Nova (vizinha). Fatores: …".
    /// </summary>
    private string ResultText(CommitmentResult r)
    {
        Commitment c = r.Commitment;
        CommitmentType type = Catalog.Get(c.Type);
        var parts = new List<string>();
        if (c.ZoneId is not null && r.Presence.TryGetValue(c.ZoneId, out double here))
        {
            parts.Add(string.Format(Tr("{0} de presença em {1}"), Signed(here, "0.0"), ZoneName(c.ZoneId)));
            parts.AddRange(r.Presence.Where(p => p.Key != c.ZoneId && Math.Abs(p.Value) >= 0.05).OrderByDescending(p => p.Value)
                .Select(p => string.Format(Tr("{0} em {1} (vizinha)"), Signed(p.Value, "0.0"), ZoneName(p.Key))));
        }
        else if (r.Presence.Count > 0)
        {
            parts.Add(string.Format(Tr("{0} de presença em todas as zonas (em média)"), Signed(r.Presence.Values.Average(), "0.0")));
        }

        if (r.Support > 0)
        {
            string? group = c.Publico != CommitmentType.PublicoGeral ? c.Publico : type.PublicoFixo;
            parts.Add(group is null
                ? string.Format(Tr("{0} pontos de apoio no público do evento"), Signed(r.Support * 100, "0.0"))
                : string.Format(Tr("{0} pontos de apoio no grupo {1}"), Signed(r.Support * 100, "0.0"), EffectSummary.GroupName(group)));
        }

        if (Math.Abs(r.Image) >= 0.005)
        {
            parts.Add(string.Format(Tr("{0} de imagem"), Signed(r.Image, "0.00")));
        }

        if (r.RelationPoints != 0 && c.PersonId is not null)
        {
            parts.Add(string.Format(Tr("{0} de relação com {1}"), Signed(r.RelationPoints, "0"), _engine.NameOf(c.PersonId)));
        }

        if (r.Money != 0)
        {
            parts.Add(SignedMoney(r.Money));
        }

        if (r.Stress != 0)
        {
            parts.Add(string.Format(Tr("{0} de estresse"), Signed(r.Stress, "0")));
        }

        if (c.Tema is not null && r.ThemeKnown >= 0.0005)
        {
            parts.Add(string.Format(Tr("{0} pontos de posição conhecida em {1}"), Signed(r.ThemeKnown * 100, "0"), ThemeName(c.Tema)));
        }

        string text = (r.Text.Length > 0 ? r.Text + " " : "") + (parts.Count == 0 ? Tr("Sem efeito nos números desta vez") : string.Join("; ", parts)) + ".";
        if (HasYield(type) && r.Factors.Count > 0)
        {
            text += " " + Tr("Fatores:") + " " + string.Join(", ", r.Factors.Select(f => $"{(f.Id == "lideranca" ? f.Text : LowerFirst(f.Text))} ({Percent(f.Multiplier)})")) + ".";
        }

        return text;
    }

    /// <summary>Tudo o que aconteceu, do mais recente ao mais antigo.</summary>
    private IEnumerable<FeedEntry> FeedEntries()
    {
        var items = new List<FeedEntry>();
        int order = 0;
        foreach (CommitmentResult r in Agenda.Log)
        {
            Commitment c = r.Commitment;
            items.Add(new FeedEntry(c.Date, order++, Tr("Sua agenda"), $"{CommitmentTitle(c)} ({DayShort(c.Date)}, {TurnoName(c.Turno)})", ResultText(r), c.ZoneId, Who: RaceSetup.PlayerId));
        }

        // Rivais vistos na mesma zona na mesma semana viram um cartão só (ART_GUIDE, densidade das notícias).
        static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));
        foreach (var group in Agenda.Feed.Where(f => f.Kind == "rival_zona" && f.ZoneId is not null).GroupBy(f => (Week: Monday(f.Date), f.ZoneId)))
        {
            var rivals = group.Select(f => f.CandidateId is null ? null : Campaign.Find(f.CandidateId)).OfType<Candidate>().Distinct().ToList();
            if (rivals.Count == 0)
            {
                continue;
            }

            string zone = ZoneName(group.Key.ZoneId);
            string text = rivals.Count == 1
                ? string.Format(Tr("{0} fez campanha em {1}."), CandidateLabel(rivals[0]), zone)
                : string.Format(Tr("Fizeram campanha em {0}: {1}{2}."), zone, string.Join("; ", rivals.Take(3).Select(CandidateLabel)),
                    rivals.Count > 3 ? " " + string.Format(Tr("e mais {0}"), rivals.Count - 3) : "");
            items.Add(new FeedEntry(group.Max(f => f.Date), order++, Tr("Na rua"), null, text, group.Key.ZoneId, Who: rivals[0].Id));
        }

        foreach (FeedItem f in Agenda.Feed.Where(f => f.Kind is not ("compromisso" or "rival_zona")))
        {
            string text = f.Text;
            if (f.CandidateId is not null && f.CandidateId != RaceSetup.PlayerId && Campaign.Find(f.CandidateId) is { } rival && text.Contains(rival.Name, StringComparison.Ordinal))
            {
                // Nome fictício sempre com o que é (princípio j).
                int at = text.IndexOf(rival.Name, StringComparison.Ordinal);
                text = text[..at] + CandidateLabel(rival) + text[(at + rival.Name.Length)..];
            }

            string source = f.Kind switch
            {
                "descoberta" => Tr("Sua agenda"),
                "objetivo" => Tr("Objetivos"),
                "equipe" or "conselho" or "vazamento" or Clock.CaixaBaixo => Tr("Sua equipe"),
                FactKind.Contas or FactKind.Doacao or FactKind.Fundo or FactKind.Esqueleto => Tr("Suas contas"),
                FactKind.Ataque or FactKind.Resposta or FactKind.Boato or FactKind.Justica or FactKind.Pacto or FactKind.Investigacao => Tr("Disputa"),
                FactKind.Plano or FactKind.RivalLideranca => Tr("Rivais"),
                FactKind.Cidade or FactKind.Morte => Tr("Cidade"),
                FactKind.Economia => Tr("Economia"),
                FactKind.TemaEmAlta or FactKind.Anuncio or FactKind.HorarioGratuito => Tr("Mídia"),
                "verba" or "cabos" => Tr("Sua verba"),
                "posicao" or "promessa" => Tr("Sua plataforma"),
                _ => Tr("Nos bastidores"),
            };
            items.Add(new FeedEntry(f.Date, order++, source, null, text.EndsWith('.') ? text : text + ".", f.ZoneId, f, f.PersonId ?? f.CandidateId));
        }

        foreach (var h in Campaign.Events.History)
        {
            items.Add(new FeedEntry(h.Week, order++, Tr("Cidade"), null, $"{_game.Events.Find(h.EventId)?.Title ?? h.EventId}: {h.Text}", null));
        }

        foreach (PollRecord poll in Campaign.Polls.Where(p => !p.Internal))
        {
            var lead = poll.Candidates.MaxBy(c => c.Value);
            items.Add(new FeedEntry(poll.Week, order++, _game.Electorate.Pollsters.Get(poll.PollsterId).Name, null, string.Format(PtBr, Tr("{0} lidera com {1:0.0}%."), Campaign.Find(lead.Key)?.Name, lead.Value), null, Who: lead.Key));
        }

        return items.OrderByDescending(i => i.Date).ThenByDescending(i => i.Order);
    }

    /// <summary>O que o clique num item do feed abre: o ataque com prazo, a oferta de doação ou a ficha do rival; null se nada.</summary>
    private (Action Open, string Tip)? FeedTarget(FeedEntry entry)
    {
        if (entry.Item is not { } item)
        {
            return null;
        }

        Fact? fact = item.FactId is null ? null : _engine.Facts.Find(item.FactId);
        if (fact is { Kind: FactKind.Ataque or FactKind.Boato, TargetId: RaceSetup.PlayerId } && _engine.Rivals.Attacks.FirstOrDefault(a => a.FactId == fact.Id) is { } attack)
        {
            return (() => OpenAreaSheet(Sheet.Ataque, attack.Id),
                Palette.Tip(Tr("Ataque contra você."), Tr("abre o ataque, com o prazo e as respostas possíveis, cada uma com a previsão."), Tr("nada para abrir.")));
        }

        if (item.Kind == FactKind.Doacao && item.PersonId is { } donorId && _engine.Finance.Offers.FirstOrDefault(o => o.DonorId == donorId) is { } offer)
        {
            return (() => OpenAreaSheet(Sheet.Oferta, offer.Id),
                Palette.Tip(Tr("Oferta de doação à espera de resposta."), Tr("abre a decisão, com a previsão de cada resposta e quem fica sabendo."), Tr("nada para abrir.")));
        }

        string? rivalId = item.CandidateId is { } actor && actor != RaceSetup.PlayerId ? actor : fact?.TargetId is { } target && target != RaceSetup.PlayerId ? target : null;
        if (rivalId is not null && entry.ZoneId is null && _engine.RivalSheetOf(rivalId) is not null)
        {
            return (() => OpenAreaSheet(Sheet.Rival, rivalId),
                Palette.Tip(string.Format(Tr("{0}, {1}. Sobre {2}."), entry.Source, entry.Date.ToString("dd/MM", PtBr), PersonLabel(rivalId)), Tr("abre a ficha: plano percebido, redutos, ataques trocados e o que dá para fazer."), Tr("nada.")));
        }

        return null;
    }

    private void FeedCard(Node parent, FeedEntry entry)
    {
        PanelContainer card;
        if (FeedTarget(entry) is { } target)
        {
            card = ClickCard(parent, target.Open, target.Tip);
        }
        else if (entry.ZoneId is { } zone && _map.Zonas.ContainsKey(zone))
        {
            card = ClickCard(parent, () => { _map.CenterOn(zone); _map.Select(zone); },
                Palette.Tip(string.Format(Tr("{0}, {1}. Aconteceu em {2}."), entry.Source, entry.Date.ToString("dd/MM", PtBr), ZoneName(zone)), Tr("centraliza o mapa nessa zona e a destaca; clique na zona para abrir a ficha."), Tr("nada.")));
        }
        else
        {
            card = Card(parent);
        }

        card.Name = "Feed_" + entry.Order;
        // Retrato de quem o cartão fala, pelo cache (RL); sem pessoa, o texto ocupa a largura toda.
        HBoxContainer line = Ui.Row(card, 10);
        line.MouseFilter = MouseFilterEnum.Pass;
        CachedPortrait(line, entry.Who, 48);
        var col = Ui.Column(line, 4);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        col.MouseFilter = MouseFilterEnum.Pass;
        Glossary.Rich(col, $"{entry.Source} · {entry.Date.ToString("dd/MM", PtBr)}", 18, Palette.Texto3);
        if (entry.Title is not null)
        {
            Glossary.Plain(col, entry.Title, 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            Glossary.Rich(col, entry.Text);
        }
        else
        {
            Glossary.Rich(col, entry.Text, 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        }

        // Fato coberto pela mídia local: em que veículos saiu e, se for o caso, a marca de boato (GDD 17.13).
        if (entry.Item?.FactId is { } factId)
        {
            Fact? fact = _engine.Facts.Find(factId);
            var covered = _engine.Media.News.Where(n => n.FactId == factId).Select(n => _engine.City.Outlet(n.OutletId)).OfType<Planalto.Core.Media.Outlet>().Distinct().ToList();
            if (fact is { Rumor: true })
            {
                Glossary.Rich(col, Tr("BOATO de rede: pode ser falso."), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            }

            if (covered.Count > 0)
            {
                Glossary.Plain(col, string.Format(Tr("Saiu em: {0}{1}."), _engine.OutletLabel(covered[0]), covered.Count > 1 ? " " + string.Format(Tr("e mais {0}"), covered.Count - 1) : ""), 18, Palette.Texto3);
            }
        }
    }

    private void BuildRight()
    {
        Ui.Clear(_feed);
        Heading(_feed, Tr("Notícias"));
        var items = FeedEntries().Take(14).ToList();
        if (items.Count == 0)
        {
            Glossary.Rich(_feed, Tr("Nada ainda. O resultado de cada compromisso e o que os rivais e as lideranças fizerem aparecem aqui."));
        }

        foreach (FeedEntry entry in items)
        {
            FeedCard(_feed, entry);
        }

        Ui.Clear(_poll);
        PollRecord? poll = Campaign.Polls.LastOrDefault();
        Heading(_poll, poll is null ? Tr("Intenção (estimativa da equipe)") : string.Format(PtBr, Tr("Intenção ({0}, {1:dd/MM})"), _game.Electorate.Pollsters.Get(poll.PollsterId).Name, poll.Week));
        if (poll is null)
        {
            TeamEstimateRows();
            return;
        }

        var ordered = poll.Candidates.OrderByDescending(c => c.Value).ToList();
        int me = ordered.FindIndex(c => c.Key == RaceSetup.PlayerId);
        int from = Math.Max(0, me < 0 ? 0 : me - 2);
        foreach (var (id, value) in ordered.Skip(from).Take(5))
        {
            Candidate? c = Campaign.Find(id);
            HBoxContainer row = Ui.Row(_poll, 8);
            Label name = Ui.Value(row, $"{(c?.IsPlayer == true ? Tr("Você") : c?.Name)} ({PartySigla(c?.PartyId ?? "")})", 18);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.TooltipText = c is null ? "" : CandidateLabel(c);
            name.MouseFilter = MouseFilterEnum.Stop;
            name.AddThemeColorOverride("font_color", Palette.Texto);
            name.AddThemeFontOverride("font", Palette.Weight(Palette.Inter, c?.IsPlayer == true ? 600 : 400));
            Ui.Value(row, string.Format(PtBr, "{0:0.0}%", value), 18).AddThemeColorOverride("font_color", Palette.Texto);
        }

        Glossary.Rich(_poll, string.Format(PtBr, Tr("margem de erro de {0:0.0} pontos"), poll.MarginOfError), 18, Palette.Texto3);
    }

    /// <summary>
    /// Sem pesquisa: a estimativa da equipe (Core), marcada como estimativa, com a faixa de cada candidato em vez de
    /// um número só; as mesmas cinco linhas em volta do jogador que a pesquisa mostra.
    /// </summary>
    private void TeamEstimateRows()
    {
        IntentionEstimate estimate = _engine.TeamEstimate();
        string tip = Palette.Tip(
            Tr("Estimativa da sua equipe, não é pesquisa: ninguém foi entrevistado."),
            string.Format(PtBr, Tr("cada faixa é onde a equipe acha que o candidato está hoje, em % de quem iria votar, com {0:0}% de indecisos; a primeira pesquisa publicada ou encomendada substitui a estimativa."), estimate.Undecided),
            Tr("nada; uma pesquisa em Finanças custa dinheiro e dá um número com margem de erro."));

        int me = estimate.Lines.ToList().FindIndex(l => l.CandidateId == RaceSetup.PlayerId);
        foreach (EstimateLine line in estimate.Lines.Skip(Math.Max(0, me - 2)).Take(5))
        {
            Candidate? c = Campaign.Find(line.CandidateId);
            HBoxContainer row = Ui.Row(_poll, 8);
            Label name = Ui.Value(row, $"{(c?.IsPlayer == true ? Tr("Você") : c?.Name)} ({PartySigla(c?.PartyId ?? "")})", 18);
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.TooltipText = c is null ? "" : CandidateLabel(c);
            name.MouseFilter = MouseFilterEnum.Stop;
            name.AddThemeColorOverride("font_color", Palette.Texto2);
            name.AddThemeFontOverride("font", Palette.Weight(Palette.Inter, c?.IsPlayer == true ? 600 : 400));
            Label range = Ui.Value(row, string.Format(PtBr, Tr("{0:0.0} a {1:0.0}%"), line.Low, line.High), 18);
            range.Name = "Estimativa_" + line.CandidateId;
            range.AddThemeColorOverride("font_color", Palette.Texto2);
            range.MouseFilter = MouseFilterEnum.Stop;
            range.TooltipText = tip;
        }

        Glossary.Rich(_poll, Tr("Sem entrevista; uma pesquisa em Finanças dá o número com margem de erro."), 18, Palette.Texto3).Name = "EstimativaEquipe";
    }
}
