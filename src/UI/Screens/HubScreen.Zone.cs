using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Electorate;
using Planalto.Core.Elections;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.UI.Map;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Território na tela central (GDD 17.3 e 17.5): a lista de zonas, a Ficha da zona no painel e o mapa com os modos
/// (Minha força, Partido líder, perfil) e os marcadores (compromissos, rivais vistos, lideranças, eventos de hoje).
/// </summary>
public partial class HubScreen
{
    private void BuildTerritory()
    {
        if (_zonas is null)
        {
            return;
        }

        Glossary.Rich(_left, string.Format(Tr("{0} zonas, montadas com setores do Censo 2022 do IBGE. À direita de cada nome, a sua presença (0 a 100). Clique numa zona do mapa ou da lista."), _zonas.Zonas.Count));
        foreach (Zona z in _zonas.Zonas.OrderByDescending(z => Territory.PresenceOf(RaceSetup.PlayerId, Territory.IndexOf(z.Id))))
        {
            int index = Territory.IndexOf(z.Id);
            string home = index == Territory.HomeZone ? " · " + Tr("onde você mora") : "";
            var b = Ui.Button(_left, string.Format(PtBr, "{0}{1}\n{2} {3:0.0} · {4:N0} {5}", z.Nome, home, Tr("presença"), Territory.PresenceOf(RaceSetup.PlayerId, index), _engine.Context.ZoneElectorates[index].Voters, Tr("eleitores")),
                () => { _map.Select(z.Id); OpenSheet(Sheet.Zona, z.Id); },
                Palette.Tip(string.Format(Tr("Zona {0}."), z.Nome), Tr("abre a ficha da zona e a destaca no mapa."), Tr("nada.")));
            b.Name = "Zona_" + z.Id;
            b.Alignment = HorizontalAlignment.Left;
            b.ClipText = true;
            Palette.ListRow(b);
        }
    }

    /// <summary>Texto da demanda da zona na unidade real, comparada com a cidade (GDD 17.5).</summary>
    private string DemandText(ZoneDemand demand)
    {
        string Format(double v) => demand.Rule.Regra switch
        {
            ZoneDemandRule.RendaBaixa => string.Create(PtBr, $"R$ {v:N0}"),
            ZoneDemandRule.Idosos or ZoneDemandRule.Jovens => string.Create(PtBr, $"{v:0.0}%"),
            _ => string.Create(PtBr, $"{v:N0}"),
        };
        return demand.Rule.Texto.Replace("{valor}", Format(demand.Value), StringComparison.Ordinal).Replace("{cidade}", Format(demand.CityValue), StringComparison.Ordinal);
    }

    private void BuildZoneSheet(Zona z)
    {
        int index = Territory.IndexOf(z.Id);
        ZoneElectorate electorate = _engine.Context.ZoneElectorates[index];
        BackButton();
        NameTitle(_left, z.Nome);
        FirstTimeHint("ficha_zona");
        string origem = z.NomeFonte switch
        {
            Zona.NomeBairro => Tr("nome do bairro mais populoso da zona (malha de bairros do IBGE)"),
            Zona.NomeDistrito => Tr("nome do distrito do IBGE"),
            _ => Tr("nome dado pelo jogo, pela posição na cidade: aqui não há bairro oficial"),
        };
        string home = index == Territory.HomeZone ? " " + Tr("É a sua zona natal: você mora aqui.") : "";
        Glossary.Rich(_left, string.Format(PtBr, Tr("{0}. {1:N0} moradores e {2:N0} eleitores, em {3:N1} km². Origem do nome: {4}.{5}"),
            Tr(z.IsRural ? "Zona rural" : "Zona urbana"), z.Populacao, electorate.Voters, z.AreaKm2, origem, home));

        var marcar = Ui.Button(_left, "Marcar compromisso aqui", () =>
        {
            var (date, turno) = _engine.NextFreeSlot();
            StartSchedule(date, turno, zoneId: z.Id);
        }, Palette.Tip(
            string.Format(Tr("Abre a marcação com {0} já escolhida."), z.Nome),
            Tr("começa no primeiro turno livre a partir de hoje; você escolhe o tipo e vê a previsão antes de confirmar."),
            Tr("nada para abrir; o custo de cada compromisso aparece na previsão.")));
        marcar.Name = "MarcarAqui";
        Palette.Primary(marcar);
        if (BlockReason() is { } block)
        {
            marcar.Disabled = true;
            marcar.TooltipText = Palette.Blocked(string.Format(Tr("{0}; resolva antes de marcar."), block), marcar.TooltipText);
        }

        // Demanda da zona: a maior carência medida, em unidade real.
        Heading(_left, Tr("Demanda da zona"), 20);
        string theme = _engine.Context.Themes.Themes.FirstOrDefault(t => t.Id == electorate.Demand.Rule.Tema)?.Name ?? electorate.Demand.Rule.Tema;
        Glossary.Rich(_left, $"{electorate.Demand.Rule.Nome}. {DemandText(electorate.Demand)}.", 18, Palette.Texto);
        Glossary.Rich(_left, string.Format(Tr("Pesa no tema {0} entre os eleitores daqui e é o assunto que as lideranças da zona levantam."), theme));

        // Presença: você e os três primeiros.
        Heading(_left, Tr("Presença aqui (0 a 100)"), 20);
        double mine = Territory.PresenceOf(RaceSetup.PlayerId, index);
        ValueRow(_left, Tr("Você"), mine.ToString("0.0", PtBr), highlight: true);
        foreach (var (candidate, presence) in _engine.TopPresence(index, 4).Where(x => !x.Candidate.IsPlayer).Take(3))
        {
            ValueRow(_left, CandidateLabel(candidate), presence.ToString("0.0", PtBr));
        }

        if (_zoneParties.TryGetValue(z.Id, out var parties) && parties.Count > 0)
        {
            Glossary.Rich(_left, Tr("Voto por partido na zona hoje:") + " " + string.Join(", ", parties.OrderByDescending(p => p.Value).Take(3).Select(p => string.Format(PtBr, "{0} {1:P0}", PartySigla(p.Key), p.Value))) + ".");
        }

        // Lideranças.
        var leaders = _engine.City.Leaders.Where(l => l.Zones.Contains(z.Id)).ToList();
        Heading(_left, Tr("Lideranças"), 20);
        foreach (Leader leader in leaders.Where(l => l.Known))
        {
            LeaderRow(_left, leader);
        }

        int unknown = leaders.Count(l => !l.Known);
        if (unknown > 0)
        {
            Glossary.Rich(_left, string.Format(Tr(unknown == 1 ? "? · {0} liderança daqui que você ainda não conhece. Uma caminhada na zona pode revelar." : "? · {0} lideranças daqui que você ainda não conhece. Uma caminhada na zona pode revelar."), unknown));
        }
        else if (leaders.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma liderança atua nesta zona."));
        }

        // O que você já fez aqui.
        Heading(_left, Tr("O que você já fez aqui"), 20);
        var done = Agenda.Log.Where(r => r.Commitment.ZoneId == z.Id).Reverse().Take(5).ToList();
        if (done.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nada ainda."));
        }

        foreach (CommitmentResult r in done)
        {
            double delta = r.Presence.GetValueOrDefault(z.Id);
            Glossary.Rich(_left, string.Format(Tr("· {0}, {1}: {2} de presença"), Catalog.Get(r.Commitment.Type).Nome, r.Commitment.Date.ToString("d/MM", PtBr), Signed(delta, "0.0")));
        }

        // Cansaço por tipo e público.
        var fatigue = Agenda.Fatigue.Where(f => f.Key.Split('|') is [_, var zone, _] && zone == z.Id).OrderByDescending(f => f.Value).ToList();
        Heading(_left, Tr("Cansaço"), 20);
        if (fatigue.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhum: o próximo compromisso aqui rende inteiro."));
        }

        foreach (var (key, value) in fatigue)
        {
            string[] parts = key.Split('|');
            string publico = parts[2] == CommitmentType.PublicoGeral ? Tr("público geral") : EffectSummary.GroupName(parts[2]);
            Glossary.Rich(_left, string.Format(PtBr, Tr("· {0} ({1}): o próximo igual rende {2:P0}"), Catalog.Tipos.FirstOrDefault(t => t.Id == parts[0])?.Nome ?? parts[0], publico, _engine.FatigueYield(key)));
        }

        // Perfil.
        Heading(_left, Tr("Perfil"), 20);
        if (z.Perfil.RendaResponsavel is { } renda)
        {
            Glossary.Rich(_left, string.Format(PtBr, Tr("Rendimento médio do responsável pelo domicílio: R$ {0:N0} ({1})."), renda, z.Perfil.RendaFonte == PerfilZona.FonteZona ? Tr("dado da zona") : Tr("dado do município: setores pequenos têm sigilo")));
        }

        var ages = ZoneProjection.AgeShares(z);
        if (ages.Values.Sum() > 0)
        {
            Glossary.Rich(_left, string.Format(PtBr, Tr("Eleitores por idade: 16 a 34 anos {0:P0}, 35 a 59 {1:P0}, 60 ou mais {2:P0}."),
                ages["idade_16_17"] + ages["idade_18_34"], ages["idade_35_59"], ages["idade_60_69"] + ages["idade_70_mais"]));
        }

        Glossary.Rich(_left, Tr("Fonte: IBGE, Censo 2022 (setores censitários); eleitores pelo TSE. A presença e o voto por partido são contas do jogo, não pesquisa."), 18, Palette.Texto3);
    }

    /// <summary>Linha de lista: nome à esquerda (cede e quebra), um valor à direita (nunca cortado).</summary>
    private static void ValueRow(Node parent, string name, string value, bool highlight = false, bool link = false)
    {
        HBoxContainer row = Ui.Row(parent, 8);
        if (link)
        {
            Glossary.Rich(row, name, 18, highlight ? Palette.Texto : Palette.Texto2);
        }
        else
        {
            Glossary.Plain(row, name, 18, highlight ? Palette.Texto : Palette.Texto2);
        }

        Label v = Ui.Value(row, value, 18);
        v.AddThemeColorOverride("font_color", Palette.Texto);
        v.SizeFlagsVertical = SizeFlags.ShrinkBegin;
    }

    // ------------------------------------------------------------------ mapa

    private static readonly (string Id, string Name, string Tip)[] MapModes =
    [
        ("forca", "Minha força", "Sua presença em cada zona, em 5 faixas da própria cidade: quanto mais clara, mais você é conhecido ali."),
        ("lider", "Partido líder", "Cor do partido com mais voto na zona hoje, pela projeção de voto zona a zona; mais forte quanto maior a vantagem."),
        ("renda", "Renda", "Rendimento médio do responsável pelo domicílio (Censo 2022), em 5 faixas da própria cidade."),
        ("idade", "Idosos", "Parte dos eleitores com 60 anos ou mais (Censo 2022), em 5 faixas da própria cidade."),
        ("situacao", "Urbana/rural", "Zonas urbanas e rurais pelo Censo 2022."),
        ("verba", "Verba", "Onde está a sua verba semanal nos canais de zona (Rua, Material gráfico e Cabos eleitorais), em 5 faixas; com um canal aberto no painel, só a verba dele. O tooltip de cada zona compara com a verba dos rivais."),
    ];

    /// <summary>Canais de verba cujo alvo é zona (os que o mapa mostra).</summary>
    private IEnumerable<ChannelDefinition> ZoneChannels => Channels.Canais.Where(c => c.Alvo == ChannelDefinition.AlvoZona);

    /// <summary>Canal que o modo Verba mostra sozinho: o que está aberto no painel, se o alvo dele é zona.</summary>
    private ChannelDefinition? MapChannel =>
        _sheet == Sheet.Canal && _sheetId is not null && Channels.Find(_sheetId) is { Alvo: ChannelDefinition.AlvoZona } channel ? channel : null;

    private void BuildMap()
    {
        Ui.Clear(_modes);
        foreach (var (id, name, tip) in MapModes)
        {
            var b = Ui.Button(_modes, name, () => { _mapMode = id; Refresh(); }, Palette.Tip(Tr(tip), Tr("muda as cores do mapa; os marcadores continuam."), Tr("nada.")));
            b.Name = "Modo_" + id;
            b.ToggleMode = true;
            b.ButtonPressed = _mapMode == id;
        }

        Ui.Button(_modes, "Cidade inteira", _map.ShowWhole, Palette.Tip(Tr("Enquadra o município inteiro."), Tr("mostra todas as zonas; a roda do mouse aproxima, o botão direito arrasta e o duplo clique volta ao enquadramento."), Tr("nada.")));

        Ui.Clear(_legend);
        if (_zonas is null)
        {
            return;
        }

        ComputeZoneParties();
        var colors = new Dictionary<string, Color>();
        switch (_mapMode)
        {
            case "forca":
                LegendTitle(Tr("Sua presença (0 a 100)"));
                Bands(_zonas.Zonas.Select(z => (z.Id, (double?)Territory.PresenceOf(RaceSetup.PlayerId, Territory.IndexOf(z.Id)))), Palette.Forca, v => v.ToString("0.0", PtBr), colors);
                break;
            case "lider":
                LegendTitle(Tr("Partido com mais voto"));
                foreach (Zona z in _zonas.Zonas)
                {
                    if (!_zoneParties.TryGetValue(z.Id, out var shares) || shares.Count == 0)
                    {
                        continue;
                    }

                    var ordered = shares.OrderByDescending(s => s.Value).ToList();
                    double margin = ordered[0].Value - (ordered.Count > 1 ? ordered[1].Value : 0);
                    colors[z.Id] = new Color(PartyColor(ordered[0].Key), (float)Math.Clamp(0.45 + (margin * 3), 0.45, 1));
                }

                foreach (string leader in colors.Keys.Select(id => _zoneParties[id].MaxBy(s => s.Value).Key).Distinct())
                {
                    LegendRow(PartyColor(leader), PartySigla(leader));
                }

                break;
            case "verba":
                ChannelDefinition? only = MapChannel;
                LegendTitle(only is null ? Tr("Sua verba por semana (canais de zona)") : string.Format(Tr("Sua verba por semana: {0}"), only.Nome));
                Bands(_zonas.Zonas.Select(z => (z.Id, (double?)(double)(only is null ? ZoneChannels.Sum(c => _engine.Budget.PlanOf(c.Id, z.Id)) : _engine.Budget.PlanOf(only.Id, z.Id)))), Palette.Verba, v => string.Create(PtBr, $"R$ {v:N0}"), colors);
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
            case "renda":
                LegendTitle(Tr("Renda do responsável"));
                Bands(_zonas.Zonas.Select(z => (z.Id, z.Perfil.RendaResponsavel)), Palette.Perfil, v => string.Create(PtBr, $"R$ {v:N0}"), colors);
                break;
            default:
                LegendTitle(Tr("Eleitores de 60 anos ou mais"));
                Bands(_zonas.Zonas.Select(z =>
                {
                    var a = ZoneProjection.AgeShares(z);
                    return (z.Id, a.Values.Sum() > 0 ? a["idade_60_69"] + a["idade_70_mais"] : (double?)null);
                }), Palette.Perfil, v => v.ToString("P0", PtBr), colors);
                break;
        }

        _map.SetColors(colors);
        BuildMarkers();
    }

    private Color PartyColor(string partyId) => State.Party(partyId) is { } party ? new Color(party.Colors.Primary) : Palette.NeutroDado;

    /// <summary>Cinco faixas por quintil da própria cidade (ART_GUIDE): cor por posição, legenda com os valores de cada faixa.</summary>
    private void Bands(IEnumerable<(string Id, double? Value)> values, Func<double, Color> scale, Func<double, string> format, Dictionary<string, Color> colors)
    {
        var ordered = values.Where(v => v.Value is not null).Select(v => (v.Id, Value: v.Value!.Value)).OrderBy(v => v.Value).ToList();
        var bands = new List<(double Min, double Max)?>(new (double, double)?[5]);
        int first = 0;
        for (int i = 0; i < ordered.Count; i++)
        {
            // Valores iguais (na precisão que a legenda mostra) ficam na mesma faixa: a do primeiro deles.
            if (format(ordered[i].Value) != format(ordered[first].Value))
            {
                first = i;
            }

            // Com menos de cinco zonas distintas, a menor fica na faixa mais escura e a maior na mais clara.
            int band = ordered.Count == 1 ? 2 : (int)Math.Round(4.0 * first / (ordered.Count - 1));
            colors[ordered[i].Id] = scale(band / 4.0);
            bands[band] = bands[band] is { } range ? (range.Min, ordered[i].Value) : (ordered[i].Value, ordered[i].Value);
        }

        for (int band = 4; band >= 0; band--)
        {
            if (bands[band] is { } range)
            {
                string low = format(range.Min), high = format(range.Max);
                LegendRow(scale(band / 4.0), low == high ? low : $"{low} {Tr("a")} {high}");
            }
        }
    }

    /// <summary>Texto da legenda numa linha só (a largura é a do texto), com os termos do glossário como link.</summary>
    private static RichTextLabel LegendText(Node parent, string text, Color color)
    {
        RichTextLabel l = Glossary.Rich(parent, text, 18, color);
        l.AutowrapMode = TextServer.AutowrapMode.Off;
        l.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        return l;
    }

    private void LegendTitle(string text) => LegendText(_legend, text, Palette.Texto2);

    private void LegendRow(Color color, string text, Texture2D? icon = null)
    {
        HBoxContainer row = Ui.Row(_legend, 8);
        if (icon is not null)
        {
            row.Add(new TextureRect { Texture = icon, Modulate = color, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(20, 20), MouseFilter = MouseFilterEnum.Ignore });
        }
        else
        {
            // Contorno de 1 px: a faixa mais escura da escala quase some sobre o painel.
            var swatch = row.Add(new Panel { CustomMinimumSize = new Vector2(18, 18), MouseFilter = MouseFilterEnum.Ignore });
            swatch.AddThemeStyleboxOverride("panel", Palette.Box(color, 2, 0, Palette.Texto3));
            swatch.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }

        LegendText(row, text, Palette.Texto);
    }

    /// <summary>Cor do alinhamento de uma liderança: com você, com um rival (cor do partido dele) ou neutra.</summary>
    private Color SideColor(Leader leader) =>
        leader.AlignedWith == RaceSetup.PlayerId ? Palette.Positivo
        : leader.AlignedWith is not null && Campaign.Find(leader.AlignedWith) is { } rival ? PartyColor(rival.PartyId)
        : Palette.NeutroDado;

    /// <summary>Itens do feed com rival visto numa zona nos últimos sete dias.</summary>
    private IEnumerable<FeedItem> RivalSightings() =>
        Agenda.Feed.Where(f => f.Kind is "rival_zona" or "rival_lideranca" && f.ZoneId is not null && f.CandidateId is not null && f.Date >= _engine.Today.AddDays(-7));

    private void BuildMarkers()
    {
        DateOnly today = _engine.Today;
        var markers = new List<ZoneMapView.Marker>();

        // Compromissos seus até o fim desta semana.
        foreach (Commitment c in Agenda.Scheduled.Where(c => c.ZoneId is not null && c.Date >= today && c.Date <= State.Calendar.WeekEnd))
        {
            markers.Add(new ZoneMapView.Marker(c.ZoneId!, Palette.Acao, Palette.Icon("calendario"), null));
        }

        // Rivais vistos na semana: etiqueta com a sigla, na cor do partido.
        foreach (var sighting in RivalSightings().GroupBy(f => (f.ZoneId, f.CandidateId)))
        {
            if (Campaign.Find(sighting.Key.CandidateId!) is { } rival)
            {
                markers.Add(new ZoneMapView.Marker(sighting.Key.ZoneId!, PartyColor(rival.PartyId), null, PartySigla(rival.PartyId)));
            }
        }

        // Lideranças conhecidas: ícone do tipo, cor do alinhamento.
        foreach (Leader leader in _engine.City.Leaders.Where(l => l.Known))
        {
            markers.Add(new ZoneMapView.Marker(leader.Zones[0], SideColor(leader), Palette.Icon("lider_" + leader.Type), null));
        }

        // Verba: no modo Verba, uma etiqueta por canal com verba na zona (no lugar dos outros marcadores); nos demais
        // modos, só onde há cabos contratados.
        bool budgetMode = _mapMode == "verba";
        if (budgetMode)
        {
            markers.Clear();
        }

        foreach (ChannelAllocation a in _engine.Budget.Plan.Where(a => a.Amount > 0 && Channels.Find(a.ChannelId) is { Alvo: ChannelDefinition.AlvoZona }))
        {
            if (a.ChannelId == ChannelDefinition.Cabos)
            {
                markers.Add(new ZoneMapView.Marker(a.TargetId, Palette.Acao, Palette.Icon("pessoas"), null));
            }
            else if (budgetMode)
            {
                markers.Add(new ZoneMapView.Marker(a.TargetId, Palette.Verba(0.55), null, Channels.Get(a.ChannelId).Nome.Split(' ')[0]));
            }
        }

        var pulsing = Agenda.CityEvents.Where(e => e.Date == today).Select(e => e.ZoneId).ToList();
        _map.SetMarkers(markers, pulsing);

        // Marcadores: uma fila de etiquetas curtas que quebra de linha, para a legenda não cobrir o mapa.
        var chips = _legend.Add(new HFlowContainer { CustomMinimumSize = new Vector2(300, 0) });
        chips.AddThemeConstantOverride("h_separation", 14);
        chips.AddThemeConstantOverride("v_separation", 4);
        if (budgetMode)
        {
            Chip(chips, Palette.Verba(0.55), Tr("etiqueta: canal com verba sua na zona"), Palette.Icon("marcador"));
            Chip(chips, Palette.Acao, Tr("cabos contratados"), Palette.Icon("pessoas"));
            return;
        }

        Chip(chips, Palette.Acao, Tr("compromisso seu"), Palette.Icon("calendario"));
        foreach (string type in _engine.City.Leaders.Where(l => l.Known).Select(l => l.Type).Distinct())
        {
            Chip(chips, Palette.Texto2, _engine.Context.Campaign.Leaders.Type(type).Nome, Palette.Icon("lider_" + type));
        }

        Chip(chips, Palette.Positivo, Tr("apoia você"));
        Chip(chips, Palette.NeutroDado, Tr("neutra"));
        Chip(chips, Palette.Texto2, Tr("sigla: rival visto na semana"), Palette.Icon("marcador"));
        if (_engine.CabosNetworkActive)
        {
            Chip(chips, Palette.Acao, Tr("cabos contratados"), Palette.Icon("pessoas"));
        }

        if (pulsing.Count > 0)
        {
            Chip(chips, Palette.Atencao, Tr("pulsando: evento da cidade hoje"), Palette.Icon("marcador"));
        }
    }

    private static void Chip(Node parent, Color color, string text, Texture2D? icon = null)
    {
        HBoxContainer row = Ui.Row(parent, 6);
        if (icon is not null)
        {
            row.Add(new TextureRect { Texture = icon, Modulate = color, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(20, 20), MouseFilter = MouseFilterEnum.Ignore });
        }
        else
        {
            var swatch = row.Add(new ColorRect { Color = color, CustomMinimumSize = new Vector2(14, 14), MouseFilter = MouseFilterEnum.Ignore });
            swatch.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        }

        LegendText(row, text, Palette.Texto2);
    }

    /// <summary>Voto por partido em cada zona (a disputa do jogador), pela projeção por zona, sem ruído.</summary>
    private void ComputeZoneParties()
    {
        _zoneParties.Clear();
        if (_zonas is null)
        {
            return;
        }

        Office office = Campaign.Player.Office;
        var partyOf = Campaign.ForOffice(office).ToDictionary(c => c.Id, c => c.PartyId);
        IReadOnlyList<VoteProjection> zones = _engine.ProjectByZone(office, noise: null).Zones;
        for (int i = 0; i < zones.Count && i < _zonas.Zonas.Count; i++)
        {
            double total = zones[i].Votes.Values.Sum();
            _zoneParties[_zonas.Zonas[i].Id] = zones[i].Votes
                .GroupBy(v => partyOf.GetValueOrDefault(v.Key, ""))
                .ToDictionary(g => g.Key, g => total <= 0 ? 0 : g.Sum(v => v.Value) / total);
        }
    }

    /// <summary>Texto do tooltip de uma zona (testes de fumaça conferem a verba própria e a dos rivais).</summary>
    public string ZoneTooltipForTest(string id) => ZoneTooltip(id);

    private string ZoneTooltip(string id)
    {
        if (!_map.Zonas.TryGetValue(id, out Zona? z))
        {
            return "";
        }

        int index = Territory.IndexOf(id);
        string leader = _zoneParties.TryGetValue(id, out var shares) && shares.Count > 0
            ? " " + string.Format(PtBr, Tr("Partido com mais voto hoje: {0}, com {1:P0}."), PartySigla(shares.MaxBy(s => s.Value).Key), shares.Values.Max())
            : "";
        var here = new List<string>();
        here.AddRange(Agenda.Scheduled.Where(c => c.ZoneId == id && c.Date >= _engine.Today && c.Date <= State.Calendar.WeekEnd)
            .Select(c => string.Format(Tr("você: {0} ({1}, {2})"), Catalog.Get(c.Type).Nome, DayShort(c.Date), TurnoName(c.Turno))));
        here.AddRange(Agenda.CityEvents.Where(e => e.ZoneId == id && e.Date == _engine.Today).Select(e => string.Format(Tr("hoje: {0} ({1})"), e.Nome, TurnoName(e.Turno))));
        here.AddRange(RivalSightings().Where(f => f.ZoneId == id).Select(f => f.Text));
        here.AddRange(_engine.City.Leaders.Where(l => l.Known && l.Zones[0] == id).Select(l => $"{LeaderLabel(l)}: {SideText(l)}"));
        // Verba própria e dos rivais em cada canal de zona: sempre no modo Verba; nos outros, só onde você gasta.
        var budget = ZoneChannels
            .Select(c => (c.Nome, Own: _engine.Budget.PlanOf(c.Id, id), Rival: _engine.RivalBudget(c.Id, id)))
            .Where(x => _mapMode == "verba" || x.Own > 0)
            .Select(x => string.Format(Tr("{0}: você {1}, rivais cerca de {2}"), x.Nome, Money(x.Own), Money(x.Rival)))
            .ToList();
        string marks = (budget.Count == 0 ? "" : "\n\n" + Tr("Verba por semana nesta zona:") + "\n· " + string.Join("\n· ", budget))
            + (here.Count == 0 ? "" : "\n\n" + Tr("Nesta zona:") + "\n· " + string.Join("\n· ", here.Take(8)));
        return Palette.Tip(
            string.Format(PtBr, Tr("{0}: {1:N0} moradores ({2})."), z.Nome, z.Populacao, Tr(z.IsRural ? "zona rural" : "zona urbana")),
            string.Format(PtBr, Tr("clique para abrir a ficha da zona. Sua presença aqui: {0:0.0} de 100."), Territory.PresenceOf(RaceSetup.PlayerId, index)) + leader,
            Tr("nada. Fonte: IBGE, Censo 2022.")) + marks;
    }
}
