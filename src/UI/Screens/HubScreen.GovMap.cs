using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Governance;
using Planalto.Core.People;
using Planalto.Core.Renewal;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.UI.Map;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// O mapa e a coluna direita fora de campanha (GDD 17.3): modos Obras (fase e atraso por zona, com marcadores), Base
/// (presença e cobrança), Aprovação e perfil; a ficha da zona e a da pessoa; as notícias do mandato, com o cartão de
/// luto sóbrio; os objetivos que vêm do Livro de compromissos; e a Câmara ou a próxima eleição no pé da coluna.
/// </summary>
public partial class HubScreen
{
    private static readonly (string Id, string Name, string Tip)[] GovMapModes =
    [
        ("obras", "Obras", "A etapa da obra mais adiantada de cada zona, da cidade inteira; os marcadores mostram cada obra em andamento, as prontas e as atrasadas."),
        ("base", "Base", "A sua presença em cada zona, na escala de 0 a 100 da legenda; os marcadores mostram o assessor de base, as promessas com dono e as zonas que cobram."),
        ("aprovacao", "Aprovação", "A aprovação do governo da cidade em cada zona, em 5 faixas da própria cidade."),
        ("renda", "Renda", "Rendimento médio do responsável pelo domicílio (Censo 2022), em 5 faixas da própria cidade."),
        ("situacao", "Urbana/rural", "Zonas urbanas e rurais pelo Censo 2022."),
    ];

    private IEnumerable<(string Id, string Name, string Tip)> GovModesNow => Kind switch
    {
        GovKind.Prefeito => GovMapModes.Where(m => m.Id != "base"),
        GovKind.Vereador => GovMapModes,
        _ => GovMapModes.Where(m => m.Id is "base" or "renda" or "situacao"),
    };

    private static int StageRank(WorkStage stage) => stage switch
    {
        WorkStage.Liberacao or WorkStage.Projeto => 1,
        WorkStage.Modalidade or WorkStage.Licitacao => 2,
        WorkStage.Execucao => 3,
        WorkStage.Pronta => 4,
        _ => 0,
    };

    private void BuildGovMap()
    {
        Ui.Clear(_modes);
        if (!GovModesNow.Any(m => m.Id == _mapMode))
        {
            _mapMode = GovModesNow.First().Id;
        }

        foreach (var (id, name, tip) in GovModesNow)
        {
            var b = Ui.Button(_modes, name, () => { _mapMode = id; Refresh(); }, Palette.Tip(Tr(tip), Tr("muda as cores do mapa e os marcadores."), Tr("nada.")));
            b.Name = "Modo_" + id;
            b.ToggleMode = true;
            b.ButtonPressed = _mapMode == id;
        }

        AddFramingButton();
        _map.HoverText = GovZoneTooltip;

        Ui.Clear(_legend);
        if (_zonas is null)
        {
            return;
        }

        var colors = new Dictionary<string, Color>();
        var markers = new List<ZoneMapView.Marker>();
        var pulsing = new List<string>();
        var chips = new List<(Color Color, string Text, Texture2D? Icon)>();
        GovernanceEngine? gov = Gov;
        switch (_mapMode)
        {
            case "obras" when gov is not null:
                LegendTitle(Tr("Etapa da obra mais adiantada da zona"));
                var works = GovernanceQueries.Works(gov).Where(w => w.Work.Open).ToList();
                string[] stageNames = [Tr("sem obra em andamento"), Tr("projeto"), Tr("licitação"), Tr("execução"), Tr("pronta para entregar")];
                var seen = new SortedSet<int>();
                foreach (Zona z in _zonas.Zonas)
                {
                    int rank = works.Where(w => w.Work.ZoneId == z.Id).Select(w => StageRank(w.Work.Stage)).DefaultIfEmpty(0).Max();
                    colors[z.Id] = Palette.Forca(rank / 4.0);
                    seen.Add(rank);
                }

                foreach (int rank in seen.Reverse())
                {
                    LegendRow(Palette.Forca(rank / 4.0), stageNames[rank]);
                }

                foreach (WorkView w in works)
                {
                    if (w.DelayWeeks > 0 && w.Work.Stage != WorkStage.Pronta)
                    {
                        markers.Add(new ZoneMapView.Marker(w.Work.ZoneId, Palette.Negativo, null, string.Format(Tr("+{0} sem"), w.DelayWeeks)));
                    }
                    else
                    {
                        markers.Add(new ZoneMapView.Marker(w.Work.ZoneId, w.Work.Stage == WorkStage.Pronta ? Palette.Positivo : w.ByPlayer ? Palette.Acao : Palette.Texto2, Palette.Icon("obra"), null));
                    }
                }

                chips.Add((Palette.Texto2, Tr("obra em andamento"), Palette.Icon("obra")));
                chips.Add((Palette.Acao, Tr("obra sua"), Palette.Icon("obra")));
                chips.Add((Palette.Positivo, Tr("pronta para entregar"), Palette.Icon("obra")));
                chips.Add((Palette.Negativo, Tr("etiqueta: semanas de atraso"), Palette.Icon("marcador")));
                pulsing.AddRange(gov.G.Crises.Where(c => c.ZoneId is not null && !c.Resolved).Select(c => c.ZoneId!));
                break;
            case "aprovacao" when gov is not null:
                LegendTitle(Tr("Aprovação do governo na zona"));
                Bands(GovernanceQueries.Zones(gov).Select(z => (z.ZoneId, (double?)z.GovernmentApproval)), Palette.Forca, v => v.ToString("0", PtBr) + "%", colors);
                pulsing.AddRange(gov.G.Crises.Where(c => c.ZoneId is not null && !c.Resolved).Select(c => c.ZoneId!));
                break;
            case "base" when gov is not null:
                var standing = GovernanceQueries.Zones(gov);
                PresenceLegend(standing.Select(z => (z.ZoneId, z.Presence)), colors);
                foreach (ZoneStanding z in standing)
                {
                    if (z.BaseAide is not null)
                    {
                        markers.Add(new ZoneMapView.Marker(z.ZoneId, Palette.Acao, Palette.Icon("pessoas"), null));
                    }

                    if (z.OpenPromises > 0)
                    {
                        markers.Add(new ZoneMapView.Marker(z.ZoneId, Palette.Texto2, Palette.Icon("alfinete"), null));
                    }

                    if (z.Base)
                    {
                        markers.Add(new ZoneMapView.Marker(z.ZoneId, Palette.Positivo, null, Tr("base")));
                    }
                }

                pulsing.AddRange(gov.G.Demands.Where(d => d.Kind == Demand.Presenca && d.Met is null && d.ZoneId is not null).Select(d => d.ZoneId!));
                chips.Add((Palette.Positivo, Tr("etiqueta: zona da sua base"), Palette.Icon("marcador")));
                chips.Add((Palette.Acao, Tr("assessor de base"), Palette.Icon("pessoas")));
                chips.Add((Palette.Texto2, Tr("promessa aberta"), Palette.Icon("alfinete")));
                if (pulsing.Count > 0)
                {
                    chips.Add((Palette.Atencao, Tr("pulsando: cobra a sua presença"), Palette.Icon("marcador")));
                }

                break;
            case "base" when Off is { } off:
                PresenceLegend(off.S.Zones.Select((id, i) => (id, off.S.Presence[i])), colors);
                foreach (Leader leader in off.Leaders)
                {
                    markers.Add(new ZoneMapView.Marker(leader.Zones[0], Palette.Texto2, Palette.Icon("lider_" + leader.Type), null));
                }

                chips.Add((Palette.Texto2, Tr("liderança que você conhece"), Palette.Icon("pessoas")));
                break;
            case "situacao":
                LegendTitle(Tr("Situação (Censo 2022)"));
                foreach (Zona z in _zonas.Zonas)
                {
                    colors[z.Id] = z.IsRural ? Palette.Perfil(0.2) : Palette.Perfil(0.9);
                }

                LegendRow(Palette.Perfil(0.9), Tr("urbana"));
                LegendRow(Palette.Perfil(0.2), Tr("rural"));
                break;
            default:
                LegendTitle(Tr("Renda do responsável"));
                Bands(_zonas.Zonas.Select(z => (z.Id, z.Perfil.RendaResponsavel)), Palette.Perfil, v => string.Create(PtBr, $"R$ {v:N0}"), colors);
                break;
        }

        _map.SetColors(colors);
        _map.SetMarkers(markers, pulsing);
        if (chips.Count > 0)
        {
            var flow = _legend.Add(new HFlowContainer { CustomMinimumSize = new Vector2(300, 0) });
            flow.AddThemeConstantOverride("h_separation", 14);
            flow.AddThemeConstantOverride("v_separation", 4);
            foreach (var (color, text, icon) in chips)
            {
                Chip(flow, color, text, icon);
            }
        }
    }

    private string GovZoneTooltip(string id)
    {
        if (!_map.Zonas.TryGetValue(id, out Zona? z))
        {
            return "";
        }

        var here = new List<string>();
        string does = _govPick is not null ? Tr("clique para escolher esta zona para o comando aberto no painel.") : Tr("clique para abrir a ficha da zona.");
        if (Gov is { } gov)
        {
            ZoneStanding? s = GovernanceQueries.Zones(gov).FirstOrDefault(x => x.ZoneId == id);
            if (s is not null)
            {
                does += " " + (Kind == GovKind.Vereador
                    ? string.Format(PtBr, Tr("Sua presença aqui: {0:0.0} de 100. Aprovação do governo: {1:0}%."), s.Presence, s.GovernmentApproval)
                    : string.Format(PtBr, Tr("Aprovação do governo aqui: {0:0}%."), s.GovernmentApproval));
            }

            here.AddRange(GovernanceQueries.Works(gov, id).Where(w => w.Work.Open).Select(w => $"{w.Name}: {LowerFirst(StageText(w))}"));
            here.AddRange(State.Ledger.Open.Where(p => p.ZoneId == id).Select(p => string.Format(Tr("promessa: {0}"), p.Text.TrimEnd('.'))));
        }
        else if (Off is { } off && off.S.IndexOf(id) is >= 0 and var index)
        {
            does += " " + string.Format(PtBr, Tr("Sua presença aqui: {0:0.0} de 100."), off.S.Presence[index]);
        }

        return Palette.Tip(
            string.Format(PtBr, Tr("{0}: {1:N0} moradores ({2})."), z.Nome, z.Populacao, Tr(z.IsRural ? "zona rural" : "zona urbana")),
            does,
            Tr("nada. Fonte: IBGE, Censo 2022.")) + (here.Count == 0 ? "" : "\n\n" + Tr("Nesta zona:") + "\n· " + string.Join("\n· ", here.Take(8)));
    }

    /// <summary>Texto do tooltip de uma zona fora de campanha (testes de fumaça).</summary>
    public string GovZoneTooltipForTest(string id) => GovZoneTooltip(id);

    // ------------------------------------------------------------------ ficha da zona

    private void BuildGovZoneSheet(Zona z)
    {
        _govPick = null;
        GovBackButton();
        NameTitle(_left, z.Nome);
        Glossary.Rich(_left, string.Format(PtBr, Tr("{0}. {1:N0} moradores, em {2:N1} km²."), Tr(z.IsRural ? "Zona rural" : "Zona urbana"), z.Populacao, z.AreaKm2));
        if (Gov is { } gov)
        {
            ZoneStanding? s = GovernanceQueries.Zones(gov).FirstOrDefault(x => x.ZoneId == z.Id);
            if (s is not null)
            {
                if (Kind == GovKind.Vereador)
                {
                    ValueRow(_left, Tr("Sua presença (0 a 100)"), s.Presence.ToString("0.0", PtBr), highlight: true);
                    ValueRow(_left, Tr("Na posse"), s.PresenceAtStart.ToString("0.0", PtBr));
                    Glossary.Rich(_left, s.Base ? Tr("É zona da sua base: cobra presença quando ela cai.") : Tr("Não é zona da sua base."));
                    if (s.BaseAide is not null)
                    {
                        Glossary.Rich(_left, string.Format(Tr("Assessor de base: {0}. Mantém a sua presença aqui e traz as demandas da zona."), s.BaseAide));
                    }
                }

                ValueRow(_left, Tr("Aprovação do governo aqui"), string.Format(PtBr, "{0:0}%", s.GovernmentApproval), highlight: Kind == GovKind.Prefeito);
            }

            string zone = z.Id;
            if (Kind == GovKind.Vereador)
            {
                HBoxContainer row = Ui.Row(_left, 6);
                Ui.Button(row, "Indicação aqui", () => { OpenGovArea("instrumentos"); OpenInstrument(InstrumentDefinition.Indicacao, zone); },
                    Palette.Tip(string.Format(Tr("Abre a Indicação com {0} já escolhida."), z.Nome), Tr("você escolhe o serviço ou a pequena obra e vê a chance de o prefeito atender."), Tr("nada para abrir; a indicação gasta turno."))).Name = "IndicacaoAqui";
                Ui.Button(row, "Emenda aqui", () => { OpenGovArea("instrumentos"); OpenInstrument(InstrumentDefinition.Emenda, zone); },
                    Palette.Tip(string.Format(Tr("Abre a Emenda com {0} já escolhida."), z.Nome), Tr("você escolhe a obra e vê o prazo e a previsão; ela entra no mapa."), Tr("nada para abrir; a emenda gasta turno, capital político e uma emenda do ano."))).Name = "EmendaAqui";
            }
            else if (Kind == GovKind.Prefeito)
            {
                var start = Ui.Button(_left, "Iniciar uma obra aqui", () => { OpenGovArea("obras"); _govType = null; OpenGov("nova_obra", null, zone); },
                    Palette.Tip(string.Format(Tr("Abre o início de obra com {0} já escolhida."), z.Nome), Tr("você escolhe o tipo e vê custo, prazo e previsão antes de confirmar."), Tr("nada para abrir; a obra desconta do investimento do ano.")));
                start.Name = "ObraAqui";
                Palette.Primary(start);
            }

            Heading(_left, Tr("Obras aqui"), 20);
            var works = GovernanceQueries.Works(gov, z.Id);
            if (works.Count == 0)
            {
                Glossary.Rich(_left, Tr("Nenhuma obra nesta zona."));
            }

            foreach (WorkView w in works.Take(8))
            {
                WorkRow(w);
            }

            var promises = GovernanceQueries.Promises(gov).Where(p => p.Promise.ZoneId == z.Id || (p.Promise.ToKind == Promise.ParaZona && p.Promise.ToId == z.Id)).ToList();
            if (promises.Count > 0)
            {
                Heading(_left, Tr("Promessas ligadas a esta zona"), 20);
                foreach (PromiseView v in promises)
                {
                    PromiseCard(_left, v);
                }
            }
        }
        else if (Off is { } off && off.S.IndexOf(z.Id) is >= 0 and var index)
        {
            ValueRow(_left, Tr("Sua presença (0 a 100)"), off.S.Presence[index].ToString("0.0", PtBr), highlight: true);
            string zone = z.Id;
            var walk = Ui.Button(_left, "Caminhar aqui", () => { OpenGovArea("entressafra"); OpenGov("caminhada", null, zone); }, GovTip(string.Format(Tr("Abre a caminhada com {0} já escolhida."), z.Nome), off.ForecastWalk(zone)));
            walk.Name = "CaminharAqui";
            Palette.Primary(walk);
        }

        var leaders = (State.City?.Leaders ?? []).Where(l => l.Known && l.Zones.Contains(z.Id) && State.Renewal?.Status(l.PersonId)?.Saida is null).ToList();
        if (leaders.Count > 0)
        {
            Heading(_left, Tr("Lideranças que você conhece aqui"), 20);
            foreach (Leader leader in leaders)
            {
                GovLeaderRow(leader);
            }
        }

        Heading(_left, Tr("Perfil"), 20);
        if (z.Perfil.RendaResponsavel is { } renda)
        {
            Glossary.Rich(_left, string.Format(PtBr, Tr("Rendimento médio do responsável pelo domicílio: R$ {0:N0} ({1})."), renda, z.Perfil.RendaFonte == PerfilZona.FonteZona ? Tr("dado da zona") : Tr("dado do município: setores pequenos têm sigilo")));
        }

        Glossary.Rich(_left, Tr("Fonte: IBGE, Censo 2022 (setores censitários). A presença e a aprovação são contas do jogo, não pesquisa."), 18, Palette.Texto3);
    }

    // ------------------------------------------------------------------ pessoas

    private string GovLeaderLabel(Leader leader) => $"{GovName(leader.PersonId)} ({_game.Campaign.Leaders.Type(leader.Type).Papel})";

    private void GovLeaderRow(Leader leader)
    {
        string person = leader.PersonId;
        DateOnly today = Clock.Today(State);
        double relation = State.City?.Person(person)?.Relation(today, _game.Campaign.People) ?? 0;
        PanelContainer card = ClickCard(_left, () => OpenGov(Off is null ? "pessoa" : "visita", person),
            Palette.Tip(GovLeaderLabel(leader) + ".", Tr("abre a ficha: relação, lembranças e o que dá para fazer com ela."), Tr("nada para abrir.")), "Lideranca_" + person);
        HBoxContainer row = Ui.Row(card, 10);
        Portrait(row, person, 56);
        var col = Ui.Column(row, 2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Glossary.Rich(col, GovLeaderLabel(leader), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, string.Format(Tr("{0} · relação {1}"), GZone(leader.Zones[0]), Signed(relation, "0")));
    }

    private void GovPersonHeader(string personId)
    {
        HBoxContainer top = Ui.Row(_left, 12);
        Portrait(top, personId, 120);
        var head = Ui.Column(top, 4);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        NameTitle(head, GovName(personId), 22);
        string what = State.City?.Leader(personId) is { } leader ? string.Format(Tr("{0}, em {1}"), _game.Campaign.Leaders.Type(leader.Type).Papel, GZone(leader.Zones[0]))
            : Gov?.Member(personId) is { } member ? string.Format(Tr("vereador, {0}"), GovPartyOf(member.PartyId))
            : Tr("pessoa da política da cidade");
        Glossary.Rich(head, string.Format(Tr("{0}. Pessoa fictícia."), char.ToUpper(what[0], PtBr) + what[1..]));
        if (State.Npcs.FirstOrDefault(n => n.Id == personId) is { } npc)
        {
            Glossary.Rich(head, string.Format(Tr("{0} anos."), npc.AgeOn(Clock.Today(State))));
        }

        if (State.Renewal is { } renewal && RenewalQueries.ExitOf(renewal, personId) is { } exit)
        {
            Glossary.Rich(_left, exit == ExitKind.Morte ? Tr("Morreu.") : Tr("Deixou a política da cidade."), 18, Palette.Texto, Palette.Serif);
        }
    }

    private void BuildGovPersonSheet(string personId)
    {
        _govPick = null;
        GovBackButton();
        GovPersonHeader(personId);
        if (State.City?.Person(personId) is { } person)
        {
            Glossary.Rich(_left, string.Format(Tr("Temperamento {0}."), TemperamentName(person.Temperament)));
        }

        GovMemories(personId);
    }

    // ------------------------------------------------------------------ coluna direita

    private static string GovFeedSource(string kind) => kind switch
    {
        "obra" => "Obras",
        "folha" or "secretario" or "prefeito" or "convenio" => "Prefeitura",
        "camara" or "mesa" or "cpi" => "Câmara",
        "cobranca" => "Livro de compromissos",
        "gabinete" => "Seu mandato",
        "crise" or "evento" => "Cidade",
        "mundo" => "Política da cidade",
        OffSeasonKinds.Caminhada or OffSeasonKinds.Visita or OffSeasonKinds.Entrevista or OffSeasonKinds.Posicao => "Sua agenda",
        _ => "Na cidade",
    };

    private static class OffSeasonKinds
    {
        public const string Caminhada = Core.Career.OffSeasonEngine.Caminhada;
        public const string Visita = Core.Career.OffSeasonEngine.Visita;
        public const string Entrevista = Core.Career.OffSeasonEngine.Entrevista;
        public const string Posicao = Core.Career.OffSeasonEngine.Posicao;
    }

    /// <summary>Notícia de morte: vira o cartão de luto, sóbrio, sem cor de destaque (GDD 17.16).</summary>
    private bool IsMourning(FeedItem f) =>
        f.Kind == "mundo" && State.Renewal is { } renewal
        && renewal.Historico.Any(e => e.Tipo == RenewalEventKind.Morte && e.Texto == f.Text);

    private List<(FeedItem Item, string Source)> GovFeed()
    {
        var items = new List<(FeedItem, string)>();
        if (State.Governance is { } g && Kind != GovKind.Entressafra)
        {
            items.AddRange(g.Feed.Select(f => (f, Tr(GovFeedSource(f.Kind)))));
        }
        else
        {
            if (State.OffSeason is { } off)
            {
                items.AddRange(off.Feed.Select(f => (f, Tr(GovFeedSource(f.Kind)))));
            }

            // Sem cargo, as notícias da política da cidade vêm direto da renovação.
            if (State.Renewal is { } renewal)
            {
                DateOnly since = State.OffSeason?.Since ?? State.Calendar.WeekStart.AddDays(-7 * 26);
                items.AddRange(renewal.Noticias.Where(n => n.Date >= since).Select(n => (new FeedItem(n.Date, "mundo", n.Text, n.ZoneId, n.PersonId), Tr("Política da cidade"))));
            }
        }

        if (State.Accounts is { } accounts)
        {
            items.AddRange(accounts.Log.Select(h => (new FeedItem(h.Week, "contas", h.Text), Tr("Justiça Eleitoral"))));
        }

        return items.Select((x, i) => (x, i)).OrderByDescending(x => x.x.Item1.Date).ThenByDescending(x => x.i).Select(x => x.x).ToList();
    }

    private void GovFeedCard(Node parent, FeedItem item, string source, int order)
    {
        bool mourning = IsMourning(item);
        string? zone = item.ZoneId is not null && _map.Zonas.ContainsKey(item.ZoneId) ? item.ZoneId : null;
        PanelContainer card;
        if (zone is not null)
        {
            card = ClickCard(parent, () => { _map.CenterOn(zone); _map.Select(zone); },
                Palette.Tip(string.Format(Tr("{0}, {1}. Aconteceu em {2}."), source, item.Date.ToString("dd/MM", PtBr), GZone(zone)), Tr("centraliza o mapa nessa zona e a destaca; clique na zona para abrir a ficha."), Tr("nada.")));
        }
        else
        {
            card = Card(parent, mourning ? Palette.Painel : null, mourning ? Palette.Texto3 : null);
        }

        card.Name = (mourning ? "Luto_" : "Feed_") + order;
        var col = Ui.Column(card, 4);
        Glossary.Rich(col, $"{(mourning ? Tr("Luto") : source)} · {item.Date.ToString("dd/MM", PtBr)}", 18, Palette.Texto3);
        Glossary.Rich(col, item.Text.TrimEnd('.') + ".", 18, Palette.Texto, mourning ? Palette.Serif : Palette.Weight(Palette.Inter, 500));
    }

    private void BuildGovRight()
    {
        Ui.Clear(_feed);
        Heading(_feed, Tr("Notícias"));
        var items = GovFeed();
        if (items.Count == 0)
        {
            Glossary.Rich(_feed, Tr("Nada ainda. O que a Câmara, a prefeitura e a cidade fizerem aparece aqui, com o resultado de cada ato seu."));
        }

        int order = 0;
        foreach (var (item, source) in items.Take(16))
        {
            GovFeedCard(_feed, item, source, order++);
        }

        // Objetivos (princípio h): fora de campanha eles nascem do Livro de compromissos, com prazo e origem.
        Ui.Clear(_objectives);
        _objectives.Visible = true;
        Heading(_objectives, Tr("Objetivos"));
        var goals = new List<(string Text, string Detail, string Name, Action? Open)>();
        if (Gov is { } gov)
        {
            foreach (PromiseView v in GovernanceQueries.Promises(gov).DistinctBy(v => (v.Promise.Text, v.Holder)).Take(3))
            {
                string? zone = v.Promise.ZoneId;
                goals.Add((v.Promise.Text.TrimEnd('.'), string.Format(Tr("até {0} · origem: promessa a {1}"), v.Deadline.ToString("MM/yyyy", PtBr), v.Holder), "Objetivo_" + v.Promise.Id,
                    () => { if (zone is not null && _map.Zonas.ContainsKey(zone)) { _map.CenterOn(zone); _map.Select(zone); } OpenGovArea(Kind == GovKind.Prefeito ? "governo" : "base"); if (Kind == GovKind.Prefeito) { OpenGov("livro", null); } }));
            }

            if (goals.Count == 0)
            {
                GovernanceSummary s = GovernanceQueries.Summary(gov);
                goals.Add((Kind == GovKind.Prefeito ? Tr("Manter a aprovação acima de 50% até o fim do mandato") : Tr("Chegar à reeleição com mais presença do que na posse"),
                    string.Format(PtBr, Tr("hoje: {0:0} · origem: o próprio mandato"), Kind == GovKind.Prefeito ? s.Approval : s.CityPresence), "Objetivo_mandato", null));
            }
        }
        else if (Off is { } off)
        {
            int year = Core.Career.CareerFlow.NextEligibleElectionYear(State);
            goals.Add((string.Format(Tr("Chegar à pré-campanha de {0} com mais presença do que hoje"), year), string.Format(PtBr, Tr("hoje: {0:0.0} · origem: a próxima eleição"), off.S.CityPresence()), "Objetivo_presenca", null));
            if (!Core.Career.PartyAffiliation.CanRun(State, year))
            {
                goals.Add((Tr("Filiar-se a um partido registrado antes do prazo"), string.Format(Tr("até {0} · origem: a lei eleitoral"), Core.Career.CareerFlow.CampaignOpens(year).ToString("dd/MM/yyyy", PtBr)), "Objetivo_filiacao", () => OpenGovArea("partido")));
            }
        }

        foreach (var (text, detail, name, open) in goals)
        {
            PanelContainer card = open is null ? Card(_objectives, margin: 8)
                : ClickCard(_objectives, open, Palette.Tip(text + ".", Tr("abre a área em que isto se resolve."), Tr("nada.")), name);
            card.Name = name;
            var col = Ui.Column(card, 2);
            Glossary.Rich(col, text, 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
            Glossary.Rich(col, detail);
        }

        Ui.Clear(_poll);
        switch (Kind)
        {
            case GovKind.Entressafra:
                int next = Core.Career.CareerFlow.NextEligibleElectionYear(State);
                Heading(_poll, Tr("Próxima eleição"));
                Glossary.Rich(_poll, string.Format(Tr("Eleição municipal em {0}. A pré-campanha abre em {1}, no prazo de filiação."),
                    Calendar.ElectionFor(next).FirstRound.ToString("dd/MM/yyyy", PtBr), Core.Career.CareerFlow.CampaignOpens(next).ToString("dd/MM/yyyy", PtBr)));
                break;
            default:
                Heading(_poll, Tr("Câmara Municipal"));
                BuildSeatsSummary(_poll);
                break;
        }
    }
}
