using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.Media;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Testes de fumaça da R3, com cliques de mouse: rivais (ficha, ataque com previsão, resposta a ataque no prazo, pacto,
/// painel da chapa), mídia (veículos, entrevista com veículo, pauta, anúncio bloqueado com o artigo, notícia com fato
/// de origem, tema em alta) e contas (oferta de doação, limite, risco, esqueleto com quem sabe, julgamento depois da
/// eleição). Também o que o `layout` e o `explicacao` percorrem das telas novas.
/// </summary>
public partial class UiShot
{
    private static bool Has(string text, params string[] parts) => parts.All(p => text.Contains(p, StringComparison.Ordinal));

    private static List<Candidate> RivalsOf(CampaignState campaign) =>
        campaign.ForOffice(campaign.Player.Office).Where(c => !c.IsPlayer && !c.IsBulk).OrderByDescending(c => c.Recognition).ToList();

    private async Task ClickNamedCard(HubScreen hub, string name)
    {
        if (Named<Control>(hub, name) is { } card)
        {
            await ClickCard(card);
        }
    }

    /// <summary>Aciona um cartão clicável pelo nome sem mouse (serve com e sem janela).</summary>
    private async Task PressCard(Node root, string name)
    {
        if (Named<Control>(root, name) is { } card)
        {
            card.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            await Frames(3);
        }
    }

    private static Button? BannerButton(HubScreen hub, string text) =>
        Named<Control>(hub, "FaixaDecisao") is { } banner ? FindAll<Button>(banner).FirstOrDefault(b => b.Text == TranslationServer.Translate(text)) : null;

    private static bool BlockedWith(Button? b, string part) =>
        b is { Disabled: true } && b.TooltipText.StartsWith(TranslationServer.Translate("Bloqueado:"), StringComparison.Ordinal) && b.TooltipText.Contains(part, StringComparison.Ordinal);

    private static void HireFor(CampaignEngine engine, params string[] roles)
    {
        foreach (string role in roles)
        {
            if (engine.StaffIn(role) is null && engine.StaffCandidates(role).FirstOrDefault(v => v.HireBlocked is null) is { } candidate)
            {
                engine.Hire(candidate.Member.PersonId);
            }
        }
    }

    // ------------------------------------------------------------------ rivais

    /// <summary>
    /// Rivais (GDD 17.11): abre a ficha pela área Pessoas, pelo marcador do mapa e pelo feed; ataca com a previsão
    /// antes; confere o bloqueio do segundo ataque; propõe pacto e vê o aviso de quebra na marcação; recebe um ataque
    /// com prazo, responde pela faixa, deixa outro vencer; e abre o painel da chapa.
    /// </summary>
    private async Task PlayRivals(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("dos rivais"))
        {
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(MediumCity, 11);
        var campaign = game.State!.Campaign!;
        var engine = game.Engine!;
        game.State.Player.Resources.Money = 60_000;
        HireFor(engine, StaffRole.Coordenador);
        hub.Refresh();
        await Frames(2);

        // Área Pessoas: a chapa em resumo e os rivais clicáveis.
        await Click(Named<Button>(hub, "Area_pessoas")!);
        List<Candidate> rivals = RivalsOf(campaign);
        if (rivals.Count < 2)
        {
            Fail("a disputa não tem dois rivais nomeados.");
            return;
        }

        Candidate rival = rivals[0];
        SlateView slate = engine.SlatePanel();
        if (Named<Control>(hub, "ResumoChapa") is not { } summary || !TextOf(summary).Contains(slate.Text, StringComparison.Ordinal) || Named<Control>(hub, "Rival_" + rival.Id) is null)
        {
            Fail("a área Pessoas não mostra o resumo da chapa e os rivais clicáveis.");
            return;
        }

        Save(shot, "_pessoas");
        await ClickNamedCard(hub, "Rival_" + rival.Id);
        string sheet = TextOf(hub);
        if (hub.SheetName != "Rival" || !Has(sheet, Tr("Plano percebido"), Tr("Redutos"), Tr("Verba vista"), Tr("Ataques trocados"), Tr("Pacto"), Tr("Lembranças de você"), Tr("Confiança da leitura"))
            || Named<Control>(hub, "Dica_rivais") is null)
        {
            Fail($"o clique no rival não abriu a ficha com plano percebido, confiança da leitura, redutos, verba vista, ataques, pacto e lembranças: {sheet}");
            return;
        }

        Save(shot, "_ficha");
        GD.Print("  " + TextOf(Named<Control>(hub, "ConfiancaLeitura")!));

        // Pelo marcador do mapa: rival visto numa zona sem outros marcadores.
        await Click(Named<Button>(hub, "Voltar")!);
        string free = campaign.Territory.Zones.First(z => engine.City.Leaders.All(l => !l.Known || l.Zones[0] != z) && campaign.Agenda.Scheduled.All(c => c.ZoneId != z));
        campaign.Agenda.AddFeed(new FeedItem(engine.Today, "rival_zona", $"{rival.Name} fez campanha em {engine.ZoneName(free)}", free, null, rival.Id));
        hub.Refresh();
        await Frames(3);
        hub.ZoneMap.CenterOn(free);
        await Frames(2);
        if (hub.ZoneMap.MarkerCenter(rival.Id) is not { } marker)
        {
            Fail("o rival visto na zona não virou marcador clicável no mapa.");
            return;
        }

        await ClickAt(hub.ZoneMap, hub.ZoneMap.GlobalPosition + marker);
        if (hub.SheetName != "Rival")
        {
            Fail("o clique no marcador do rival no mapa não abriu a ficha dele.");
            return;
        }

        // Pelo feed: um fato do rival sem zona abre a ficha.
        await Click(Named<Button>(hub, "Area_campanha")!);
        string plan = $"{rival.Name} partiu para o ataque";
        engine.Emit(FactKind.Plano, plan, rival.Id);
        hub.Refresh();
        await Frames(3);
        PanelContainer? feedCard = FindAll<PanelContainer>(hub).FirstOrDefault(p => p.Name.ToString().StartsWith("Feed_", StringComparison.Ordinal) && !p.IsQueuedForDeletion() && TextOf(p).Contains(Tr("partiu para o ataque"), StringComparison.Ordinal));
        if (feedCard is null)
        {
            Fail("o fato do rival não apareceu no feed.");
            return;
        }

        await ClickCard(feedCard);
        if (hub.SheetName != "Rival")
        {
            Fail("o clique na notícia do rival no feed não abriu a ficha dele.");
            return;
        }

        // Atacar: a previsão antes, com o risco de voltar contra você e quem vai lembrar.
        await Click(Named<Button>(hub, "AbrirAtaque")!);
        if (hub.SheetName != "Atacar" || !SelectItem(hub, "TipoAtaque", engine.Context.Campaign.Attacks.Type(AttackType.Esqueleto)!.Nome))
        {
            Fail("\"Atacar\" não abriu a tela do ataque com a escolha do tipo.");
            return;
        }

        await Frames(3);
        Control? forecast = Named<Control>(hub, "PrevisaoAtaque");
        string forecastText = forecast is null ? "" : TextOf(forecast);
        if (!Has(forecastText, Tr("Quem vai lembrar:"), rival.Name, "%") || !forecastText.Contains(Tr("Sua imagem"), StringComparison.OrdinalIgnoreCase)
            || !ThreePartTip(Named<Button>(hub, "ConfirmarAtaque")) || !ThreePartTip(Named<OptionButton>(hub, "CanalAtaque")))
        {
            Fail($"a previsão do ataque não mostra a imagem dos dois, o risco de processo e quem vai lembrar: {forecastText}");
            return;
        }

        GD.Print("  " + forecastText.Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_atacar");
        int memories = engine.City.Person(rival.Id)!.Memories.Count;
        await Click(Named<Button>(hub, "ConfirmarAtaque")!);
        if (hub.SheetName != "Rival" || !engine.Rivals.Attacks.Any(a => a.AttackerId == RaceSetup.PlayerId && a.TargetId == rival.Id) || Named<Control>(hub, "OQueMudou") is null
            || engine.City.Person(rival.Id)!.Memories.Count <= memories)
        {
            Fail("o ataque confirmado não ficou registrado, não mostrou \"O que mudou\" ou o rival não lembrou.");
            return;
        }

        GD.Print("  " + TextOf(Named<Control>(hub, "OQueMudou")!).Replace("\n", " | ", StringComparison.Ordinal));
        await Click(Named<Button>(hub, "AbrirAtaque")!);
        if (!BlockedWith(Named<Button>(hub, "ConfirmarAtaque"), Tr("espere")) || Named<Control>(hub, "Bloqueio") is null)
        {
            Fail("o segundo ataque ao mesmo rival, em seguida, deveria estar bloqueado com o motivo.");
            return;
        }

        await Click(Named<Button>(hub, "CancelarAtaque")!);

        // Investigar e propor pacto a outro rival, com a previsão.
        Candidate other = rivals[1];
        await Click(Named<Button>(hub, "Area_pessoas")!);
        await ClickNamedCard(hub, "Rival_" + other.Id);
        Control? pactForecast = Named<Control>(hub, "PrevisaoPacto");
        if (hub.SheetName != "Rival" || pactForecast is null || !Has(TextOf(pactForecast), "%", Tr("Quem vai lembrar:")) || !ThreePartTip(Named<Button>(hub, "Investigar")) || !ThreePartTip(Named<Button>(hub, "ProporPacto")))
        {
            Fail("a ficha do segundo rival não mostra a previsão do pacto (chance de aceitar, quem vai lembrar) nem os botões explicados.");
            return;
        }

        GD.Print("  " + TextOf(pactForecast).Replace("\n", " | ", StringComparison.Ordinal));
        decimal cash = game.State.Player.Resources.Money;
        await Click(Named<Button>(hub, "Investigar")!);
        if (game.State.Player.Resources.Money != cash - engine.InvestigationCost || Named<Control>(hub, "OQueMudou") is null)
        {
            Fail("investigar não cobrou o custo ou não mostrou o resultado.");
            return;
        }

        memories = engine.City.Person(other.Id)!.Memories.Count;
        await Click(Named<Button>(hub, "ProporPacto")!);
        if (!engine.Rivals.LastProposal.ContainsKey(other.Id) || engine.City.Person(other.Id)!.Memories.Count <= memories || Named<Control>(hub, "OQueMudou") is null)
        {
            Fail("a proposta de pacto não ficou registrada, o rival não lembrou ou faltou \"O que mudou\".");
            return;
        }

        GD.Print("  " + TextOf(Named<Control>(hub, "OQueMudou")!).Replace("\n", " | ", StringComparison.Ordinal));

        // Só neste teste: garante um pacto em vigor para conferir o aviso de quebra na marcação.
        string closed = campaign.Territory.Zones.First(z => z != campaign.Territory.Zones[campaign.Territory.HomeZone]);
        if (engine.Rivals.PactWith(other.Id) is null)
        {
            engine.Rivals.Pacts.Add(new Pact { Id = "pc_teste", RivalId = other.Id, Kind = PactKind.NaoAgressao, Since = engine.Today, ClosedZones = [closed] });
        }
        else
        {
            closed = engine.Rivals.PactWith(other.Id)!.ClosedZones.FirstOrDefault() ?? closed;
            if (!engine.Rivals.PactWith(other.Id)!.ClosedZones.Contains(closed))
            {
                engine.Rivals.PactWith(other.Id)!.ClosedZones.Add(closed);
            }
        }

        hub.Refresh();
        await Frames(2);
        if (Named<Control>(hub, "PactoEmVigor") is null)
        {
            Fail("a ficha do rival não mostra o pacto em vigor.");
            return;
        }

        Save(shot, "_pacto");
        await Click(Named<Button>(hub, "Area_territorio")!);
        Named<Button>(hub, "Zona_" + closed)!.EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        await Click(Named<Button>(hub, "MarcarAqui")!);
        SelectOption(hub, "Tipo", "caminhada");
        await Frames(3);
        if (Named<Control>(hub, "QuebraPacto") is not { } warn || !TextOf(warn).Contains(other.Name, StringComparison.Ordinal))
        {
            Fail("marcar ato de rua numa zona fechada pelo pacto deveria avisar que o pacto será quebrado.");
            return;
        }

        GD.Print("  " + TextOf(warn));
        await Click(Named<Button>(hub, "Cancelar")!);

        // Ataque contra você, com prazo: cartão no feed, faixa com a contagem, tempo livre, respostas com previsão.
        ClearInterruptions();
        Attack? incoming = engine.TriggerRivalAttack(rival.Id);
        if (incoming is null)
        {
            Fail("não foi possível disparar um ataque do rival.");
            return;
        }

        await Click(Named<Button>(hub, "Area_campanha")!);
        Control? banner = Named<Control>(hub, "FaixaDecisao");
        if (banner is null || !Has(TextOf(banner), rival.Name, Tr("contra você"), Tr("faltam 2 dias"), Tr("vale o silêncio")) || hub.BlockReason() is not null
            || !FeedTexts(hub).Any(t => t.Contains(incoming.Text[(incoming.Text.IndexOf(rival.Name, StringComparison.Ordinal) + rival.Name.Length)..].Trim(), StringComparison.Ordinal)))
        {
            Fail($"o ataque contra você deveria aparecer no feed e na faixa, com a contagem do prazo, sem bloquear o tempo. Faixa: {(banner is null ? "nenhuma" : TextOf(banner))}. Ataque: {incoming.Text}. Feed: {string.Join(" || ", FeedTexts(hub).Take(4))}");
            return;
        }

        GD.Print("  " + TextOf(banner).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_faixa");
        DateOnly before = engine.Today;
        hub.StepDay();
        await Frames(3);
        if (engine.Today == before || hub.Running || hub.NoticeText?.Contains(Tr("contra você"), StringComparison.Ordinal) != true)
        {
            Fail($"o ataque com prazo deveria deixar o dia virar e pausar com o aviso (aviso: {hub.NoticeText ?? "nenhum"}).");
            return;
        }

        Save(shot, "_pausa");
        ClearInterruptions();
        hub.Refresh();
        await Frames(2);

        if (BannerButton(hub, "Responder") is not { } respond)
        {
            Fail("a faixa do ataque não tem o botão Responder.");
            return;
        }

        await Click(respond);
        if (hub.SheetName != "Ataque" || Named<Control>(hub, "PrazoResposta") is null)
        {
            Fail("\"Responder\" não abriu o ataque com o prazo.");
            return;
        }

        foreach (string response in AttackResponse.Required)
        {
            if (Named<Control>(hub, "Resposta_" + response) is not { } option || TextOf(option).Length < 40 || !ThreePartTip(Named<Button>(hub, "Responder_" + response)))
            {
                Fail($"a resposta '{response}' não aparece com a previsão e o botão explicado.");
                return;
            }
        }

        if (engine.StaffIn(StaffRole.Advogado) is null && !BlockedWith(Named<Button>(hub, "Responder_" + AttackResponse.DireitoDeResposta), Tr("Jurídico")))
        {
            Fail("sem advogado na equipe, o direito de resposta deveria estar bloqueado com o motivo.");
            return;
        }

        Save(shot, "_ataque");
        HireFor(engine, StaffRole.Advogado);
        hub.Refresh();
        await Frames(2);
        if (engine.StaffIn(StaffRole.Advogado) is not null && Named<Button>(hub, "Responder_" + AttackResponse.DireitoDeResposta) is not { Disabled: false })
        {
            Fail("com advogado na equipe, o direito de resposta deveria estar liberado.");
            return;
        }

        GD.Print("  " + TextOf(Named<Control>(hub, "Resposta_" + AttackResponse.DireitoDeResposta)!).Replace("\n", " | ", StringComparison.Ordinal));
        await Click(Named<Button>(hub, "Responder_" + AttackResponse.MesmoVeiculo)!);
        if (incoming.Response != AttackResponse.MesmoVeiculo || Named<Control>(hub, "OQueMudou") is null || Named<Control>(hub, "RespostaDada") is null)
        {
            Fail("a resposta ao ataque não ficou registrada ou não mostrou o resultado.");
            return;
        }

        GD.Print("  " + TextOf(Named<Control>(hub, "OQueMudou")!).Replace("\n", " | ", StringComparison.Ordinal));

        // Outro ataque: o prazo vence sem resposta e vale o silêncio.
        Attack? ignored = engine.TriggerRivalAttack(rivals[^1].Id);
        if (ignored is null)
        {
            Fail("não foi possível disparar o segundo ataque.");
            return;
        }

        for (int day = 0; day < 4 && engine.PendingAttacks().Any(a => a.Attack.Id == ignored.Id); day++)
        {
            ClearInterruptions();
            hub.StepDay();
            await Frames(2);
        }

        ClearInterruptions();
        hub.Refresh();
        if (engine.PendingAttacks().Any(a => a.Attack.Id == ignored.Id) || ignored.Response is not null and not AttackResponse.Silencio)
        {
            Fail("o prazo do segundo ataque deveria ter vencido sem resposta.");
            return;
        }

        // Painel da chapa.
        await Click(Named<Button>(hub, "Area_pessoas")!);
        await Click(Named<Button>(hub, "VerChapa")!);
        string slateText = TextOf(hub);
        if (hub.SheetName != "Chapa" || !Has(slateText, Tr("Quem puxa quem"), Tr("Você"), Tr("quociente eleitoral"), Tr("votos da chapa")))
        {
            Fail($"o painel da chapa não mostra as cadeiras, a posição, os votos projetados e quem puxa quem: {slateText}");
            return;
        }

        GD.Print("  " + TextOf(Named<Control>(hub, "TextoChapa")!));
        Save(shot, "_chapa");
        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot);
        }

        GD.Print("Smoke rivais: ok (ficha pela área Pessoas, pelo marcador do mapa e pelo feed; ataque com previsão e bloqueio do segundo; investigar; pacto com previsão e aviso de quebra na marcação; ataque com prazo respondido pela faixa sem travar o tempo; prazo vencido; painel da chapa)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ mídia

    /// <summary>
    /// Mídia (GDD 17.13): veículos com linha e alcance, horário gratuito, entrevista com veículo escolhido e tema em
    /// alta na previsão, anúncio bloqueado em rádio ou TV com o artigo, notícia com fato de origem, boato marcado,
    /// sugerir pauta, anúncio na imprensa escrita com o limite legal e cartão de evento com pessoas e veículo.
    /// </summary>
    private async Task PlayMedia(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("da mídia"))
        {
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(Metropolis, 11);
        var campaign = game.State!.Campaign!;
        var engine = game.Engine!;
        game.State.Player.Resources.Money = 200_000;
        hub.Refresh();
        await Frames(2);

        await Click(Named<Button>(hub, "Area_midia")!);
        var outlets = engine.Outlets();
        OutletView? broadcaster = outlets.FirstOrDefault(o => o.Broadcaster);
        OutletView? written = outlets.FirstOrDefault(o => o.Written);
        if (broadcaster is null || written is null)
        {
            Fail("a metrópole deveria ter pelo menos uma emissora e um veículo de imprensa escrita.");
            return;
        }

        string area = TextOf(hub);
        if (Named<Control>(hub, "Dica_midia") is null || Named<Control>(hub, "HorarioGratuito") is null || outlets.Any(o => Named<Control>(hub, "Veiculo_" + o.Outlet.Id) is null || !area.Contains(o.Label, StringComparison.Ordinal))
            || !Has(area, Tr("Tema em alta"), Tr("alcança"), Tr("Horário eleitoral gratuito"), "art. 47"))
        {
            Fail($"a área Mídia não mostra a dica, os veículos (tipo, linha, alcance, relação), o tema em alta e o horário gratuito com o artigo: {area}");
            return;
        }

        GD.Print("  " + TextOf(Named<Control>(hub, "HorarioGratuito")!).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_area");

        // Emissora: anúncio pago bloqueado, com o artigo.
        await ClickNamedCard(hub, "Veiculo_" + broadcaster.Outlet.Id);
        if (hub.SheetName != "Veiculo" || !BlockedWith(Named<Button>(hub, "Anunciar"), "art. 44") || Named<Control>(hub, "AnuncioVedado") is not { } vetoed || !TextOf(vetoed).Contains("art. 44", StringComparison.Ordinal)
            || !Has(TextOf(hub), Tr("Linha editorial"), Tr("Alcance"), Tr("Com você"), Tr("Jornalista:")))
        {
            Fail("a ficha da emissora deveria mostrar linha, alcance e relação, e bloquear o anúncio pago citando o art. 44.");
            return;
        }

        GD.Print("  " + TextOf(vetoed));
        Save(shot, "_emissora");

        // Tema em alta (só neste teste: posto direto no estado) e entrevista com o veículo escolhido.
        var theme = engine.Context.Themes.Themes[0];
        engine.Media.Hot.Add(new HotTheme { Theme = theme.Id, Zones = [.. campaign.Territory.Zones], From = engine.Today, Until = engine.Today.AddDays(13), News = 6 });
        await Click(Named<Button>(hub, "MarcarEntrevistaAqui")!);
        if (hub.SheetName != "Marcar" || Named<OptionButton>(hub, "Veiculo") is not { } chooser || !chooser.GetItemText(chooser.Selected).StartsWith(broadcaster.Outlet.Name, StringComparison.Ordinal))
        {
            Fail("\"Marcar entrevista aqui\" não abriu a marcação com o veículo já escolhido.");
            return;
        }

        if (!SelectItem(hub, "Tema", theme.Name))
        {
            Fail("a marcação da entrevista não oferece o tema.");
            return;
        }

        await Frames(3);
        Control? interview = Named<Control>(hub, "Previsao");
        if (interview is null || Named<Control>(hub, "TemaEmAlta") is not { } hot || !TextOf(hot).Contains(theme.Name, StringComparison.Ordinal) || !TextOf(interview).Contains(broadcaster.Outlet.Name, StringComparison.Ordinal)
            || !ThreePartTip(Named<OptionButton>(hub, "Veiculo")))
        {
            Fail($"a previsão da entrevista deveria mostrar o veículo escolhido e o bônus do tema em alta: {(interview is null ? "" : TextOf(interview))}");
            return;
        }

        GD.Print("  " + TextOf(interview).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_entrevista");
        await Click(Named<Button>(hub, "Confirmar")!);
        if (!campaign.Agenda.Scheduled.Any(c => c.Type == CommitmentCatalog.Entrevista && c.OutletId == broadcaster.Outlet.Id && c.Tema == theme.Id))
        {
            Fail("a entrevista marcada não guardou o veículo e o tema escolhidos.");
            return;
        }

        // Tema em alta na área Mídia e no modo Disputa do mapa.
        await Click(Named<Button>(hub, "Area_midia")!);
        await Click(Named<Button>(hub, "Modo_disputa")!);
        if (Named<Control>(hub, "TemaEmAlta_" + theme.Id) is null || !hub.ZoneTooltipForTest(campaign.Territory.Zones[0]).Contains(theme.Name, StringComparison.Ordinal)
            || hub.ZoneMap.Markers.Count(m => m.Icon is not null) < campaign.Territory.Zones.Count)
        {
            Fail("o tema em alta deveria aparecer na área Mídia e, no modo Disputa do mapa, em cada zona atingida.");
            return;
        }

        Save(shot, "_disputa");
        await Click(Named<Button>(hub, "Modo_forca")!);

        // Um boato e alguns dias de noticiário: toda notícia tem fato de origem; boato vem marcado.
        Candidate rival = RivalsOf(campaign)[0];
        engine.Emit(FactKind.Boato, "uma mensagem sem autor circula nas redes sobre a campanha", rival.Id, target: RaceSetup.PlayerId, visibility: 2, rumor: true, falso: true);
        for (int day = 0; day < 7; day++)
        {
            ClearInterruptions();
            hub.StepDay();
            await Frames(1);
        }

        ClearInterruptions();
        await Click(Named<Button>(hub, "Area_midia")!);
        var cards = FindAll<PanelContainer>(hub).Where(p => p.Name.ToString().StartsWith("Noticia_", StringComparison.Ordinal) && !p.IsQueuedForDeletion()).ToList();
        if (cards.Count < 3 || cards.Any(c => Named<Control>(c, "FatoDeOrigem") is null) || engine.Media.News.Any(n => engine.Facts.Find(n.FactId) is null && n.Date >= engine.Today.AddDays(-7)))
        {
            Fail($"depois de uma semana deveria haver notícias, todas com o fato de origem ({cards.Count} cartões).");
            return;
        }

        NewsItem? rumor = engine.Media.News.LastOrDefault(n => n.Boato);
        if (rumor is null || (Named<Control>(hub, "Noticia_" + rumor.Id) is { } rumorCard && Named<Control>(rumorCard, "MarcaBoato") is null))
        {
            Fail("o boato de rede deveria ter virado notícia marcada como boato.");
            return;
        }

        GD.Print("  " + TextOf(cards[0]).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_noticias");

        // Sugerir pauta a um veículo escrito, com a previsão; a segunda na mesma semana fica bloqueada.
        await ClickNamedCard(hub, "Veiculo_" + written.Outlet.Id);
        Control? story = Named<Control>(hub, "PrevisaoPauta");
        if (hub.SheetName != "Veiculo" || story is null || !Has(TextOf(story), "%", Tr("Quem vai lembrar:")) || Named<Button>(hub, "SugerirPauta") is not { Disabled: false } suggest || !ThreePartTip(suggest)
            || !ThreePartTip(Named<OptionButton>(hub, "FatoDaPauta")))
        {
            Fail($"a ficha do veículo escrito deveria oferecer \"Sugerir pauta\" com a previsão: {(story is null ? "" : TextOf(story))}");
            return;
        }

        GD.Print("  " + TextOf(story).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_pauta");
        await Click(suggest);
        if (!engine.Media.LastPitch.ContainsKey(written.Outlet.JournalistId) || Named<Control>(hub, "OQueMudou") is null || Named<Button>(hub, "SugerirPauta") is not { Disabled: true } again
            || !again.TooltipText.StartsWith(Tr("Bloqueado:"), StringComparison.Ordinal))
        {
            Fail("a pauta sugerida não ficou registrada, não mostrou o resultado ou a segunda da semana não ficou bloqueada com o motivo.");
            return;
        }

        GD.Print("  " + TextOf(Named<Control>(hub, "OQueMudou")!).Replace("\n", " | ", StringComparison.Ordinal));

        // Anúncio na imprensa escrita: fechado antes de 16 de agosto; na campanha, vale e o segundo no mesmo dia bate no limite legal.
        if (!BlockedWith(Named<Button>(hub, "Anunciar"), Tr("propaganda só a partir de")))
        {
            Fail("antes de 16 de agosto o anúncio deveria estar bloqueado com a data.");
            return;
        }

        while (engine.BeforeOfficialCampaign)
        {
            ClearInterruptions();
            game.AdvanceWeek();
        }

        ClearInterruptions();
        game.State.Player.Resources.Money = 200_000;
        hub.Refresh();
        await Frames(3);
        Control? ad = Named<Control>(hub, "PrevisaoAnuncio");
        if (ad is null || !Has(TextOf(ad), "art. 43", Tr("Quem vai lembrar:")) || Named<Button>(hub, "Anunciar") is not { Disabled: false } advertise)
        {
            Fail($"na campanha oficial o anúncio na imprensa escrita deveria estar liberado, com a previsão e o limite legal: {(ad is null ? "" : TextOf(ad))}");
            return;
        }

        GD.Print("  " + TextOf(ad).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_anuncio");
        decimal money = game.State.Player.Resources.Money;
        await Click(advertise);
        if (engine.Media.AdDates.GetValueOrDefault(written.Outlet.Id, []).Count != 1 || game.State.Player.Resources.Money != money - written.AdCost || !BlockedWith(Named<Button>(hub, "Anunciar"), "art. 43"))
        {
            Fail("o anúncio não foi cobrado e registrado, ou o segundo no mesmo dia não ficou bloqueado com o art. 43.");
            return;
        }

        // Cartão de evento com as pessoas, a zona e o veículo do save, com nomes clicáveis.
        EventDefinition? definition = game.Events.Events.FirstOrDefault(e => e.Id == "campanha_radio_local")
            ?? game.Events.Events.FirstOrDefault(e => EventValidation.Needs(e).Contains("veiculo"));
        if (definition is null)
        {
            Fail("nenhum evento envolve um veículo.");
            return;
        }

        PendingEvent pending = EventEngine.Trigger(engine.Context, definition.Id, game.State.Rng);
        hub.Refresh();
        await Frames(3);
        Control? involved = Named<Control>(hub, "Envolvidos");
        Button? outletLink = Named<Button>(hub, "VeiculoDoEvento");
        if (!hub.EventOpen || involved is null || pending.OutletId is null || outletLink is null || !ThreePartTip(outletLink)
            || pending.People.Values.Any(id => !TextOf(involved).Contains(engine.NameOf(id), StringComparison.Ordinal)))
        {
            Fail("o cartão do evento deveria mostrar as pessoas e o veículo envolvidos, com os nomes clicáveis.");
            return;
        }

        GD.Print("  " + TextOf(involved).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_evento");
        await Click(outletLink);
        if (hub.SheetName != "Veiculo" || !hub.EventOpen)
        {
            Fail("o clique no veículo do cartão deveria abrir a ficha no painel, com o cartão ainda aberto.");
            return;
        }

        if (pending.People.Values.FirstOrDefault() is { } person && Named<Button>(hub, "Envolvido_" + person) is { } personLink)
        {
            await Click(personLink);
            if (hub.SheetName is not ("Gente" or "Pessoa" or "Rival" or "Membro") || !hub.EventOpen)
            {
                Fail("o clique no nome da pessoa do cartão deveria abrir a ficha dela no painel.");
                return;
            }
        }

        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot);
        }

        GD.Print("Smoke midia: ok (veículos com linha e alcance, horário gratuito com o art. 47, anúncio vedado em emissora com o art. 44, entrevista com veículo e tema em alta na previsão, modo Disputa do mapa, notícias com fato de origem, boato marcado, pauta com previsão, anúncio escrito com o art. 43 e cartão de evento com pessoas e veículo clicáveis)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ contas

    private static DonorOffer AddOffer(CampaignEngine engine, DonorView donor, decimal amount, bool offBooks)
    {
        // Só nos testes: a oferta entra direto no estado, com o prazo de uma semana.
        var offer = new DonorOffer { Id = $"of_teste{engine.Finance.NextId++}", DonorId = donor.Donor.PersonId, Amount = amount, PorFora = offBooks, Deadline = engine.Today.AddDays(7) };
        engine.Finance.Offers.Add(offer);
        return offer;
    }

    /// <summary>
    /// Contas (GDD 17.12): barra do limite e risco em faixa na área Finanças, oferta de doação respondida com
    /// previsão (com recibo e "por fora"), esqueleto com quem sabe e chance de vir à tona, fontes e prazos; depois
    /// da eleição, o julgamento das contas e a representação na tela do mandato ou da entressafra.
    /// </summary>
    private async Task PlayAccounts(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("das contas"))
        {
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(MediumCity, 11);
        var campaign = game.State!.Campaign!;
        var engine = game.Engine!;
        game.State.Player.Resources.Money = 20_000;
        HireFor(engine, StaffRole.Tesoureiro);
        engine.Finance.Offers.Clear();
        hub.Refresh();
        await Frames(2);

        await Click(Named<Button>(hub, "Area_financas")!);
        if (Named<Control>(hub, "BarraLimite") is not { } bar || Named<Control>(hub, "RiscoDasContas") is not { } risk || Named<Button>(hub, "VerContas") is null
            || !TextOf(bar).Contains(Money(engine.SpendingLimit), StringComparison.Ordinal) || !TextOf(risk).Contains(Tr("Faixa:"), StringComparison.Ordinal))
        {
            Fail("a área Finanças não mostra o bloco Contas com a barra do limite e o risco em faixa.");
            return;
        }

        // Oferta com recibo: decisão com prazo na faixa, previsão por resposta, dívida no Livro.
        DonorView donor = engine.Donors().OrderByDescending(d => d.CapLeft).First();
        DonorOffer official = AddOffer(engine, donor, Math.Floor(donor.CapLeft / 2 / 10) * 10, offBooks: false);
        hub.Refresh();
        await Frames(3);
        Control? banner = Named<Control>(hub, "FaixaDecisao");
        if (banner is null || !TextOf(banner).Contains(Tr("oferta de doação"), StringComparison.Ordinal) || hub.BlockReason() is not null || Named<Control>(hub, "Oferta_" + official.Id) is null)
        {
            Fail("a oferta de doação deveria aparecer na faixa e no bloco Contas, sem travar o tempo.");
            return;
        }

        Save(shot, "_financas");
        hub.StepDay();
        await Frames(3);
        if (hub.Running || hub.NoticeText is not { } notice || !(notice.Contains(Tr("oferta de doação"), StringComparison.Ordinal) || notice.Contains(Tr("contra você"), StringComparison.Ordinal)))
        {
            Fail($"a oferta de doação nova deveria pausar o jogo com o aviso (aviso: {hub.NoticeText ?? "nenhum"}).");
            return;
        }

        ClearInterruptions();
        hub.Refresh();
        await Frames(2);
        await ClickNamedCard(hub, "Oferta_" + official.Id);
        Control? accept = Named<Control>(hub, "Doacao_" + DonationAnswer.Oficial);
        if (hub.SheetName != "Oferta" || accept is null || Named<Control>(hub, "Doacao_" + DonationAnswer.Recusar) is null || Named<Control>(hub, "Doacao_" + DonationAnswer.PorFora) is not null
            || !Has(TextOf(accept), Tr("Risco das contas:"), Tr("Quem vai lembrar:"), "art. 23", Money(official.Amount)) || !ThreePartTip(Named<Button>(hub, "Responder_" + DonationAnswer.Oficial)))
        {
            Fail($"a oferta com recibo deveria mostrar aceitar e recusar, cada uma com a previsão (dinheiro, risco, teto legal, quem vai lembrar): {(accept is null ? "" : TextOf(accept))}");
            return;
        }

        GD.Print("  " + TextOf(accept).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_oferta");
        decimal money = game.State.Player.Resources.Money;
        int debts = game.State.Ledger.Debts.Count;
        await Click(Named<Button>(hub, "Responder_" + DonationAnswer.Oficial)!);
        if (game.State.Player.Resources.Money != money + official.Amount || game.State.Ledger.Debts.Count != debts + 1 || Named<Control>(hub, "OQueMudou") is null || engine.Finance.Offers.Contains(official))
        {
            Fail("aceitar a doação com recibo deveria pôr o dinheiro no caixa, gravar a dívida no Livro e mostrar \"O que mudou\".");
            return;
        }

        GD.Print("  " + TextOf(Named<Control>(hub, "OQueMudou")!).Replace("\n", " | ", StringComparison.Ordinal));

        // Oferta "por fora": o risco e quem fica sabendo aparecem antes; aceitar vira esqueleto.
        DonorView second = engine.Donors().First(d => d.Donor.PersonId != donor.Donor.PersonId);
        DonorOffer dirty = AddOffer(engine, second, Math.Round(engine.SpendingLimit * 0.35m / 10) * 10, offBooks: true);
        hub.Refresh();
        await Frames(3);
        if (BannerButton(hub, "Decidir") is not { } decide)
        {
            Fail("a faixa da oferta de doação não tem o botão Decidir.");
            return;
        }

        await Click(decide);
        Control? offBooks = Named<Control>(hub, "Doacao_" + DonationAnswer.PorFora);
        if (hub.SheetName != "Oferta" || offBooks is null || !Has(TextOf(offBooks), Tr("Quem fica sabendo:"), Tr("Risco das contas:"), second.Name))
        {
            Fail($"a oferta \"por fora\" deveria mostrar o risco e quem fica sabendo antes do clique: {(offBooks is null ? "" : TextOf(offBooks))}");
            return;
        }

        GD.Print("  " + TextOf(offBooks).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_por_fora");
        int riskBefore = engine.AccountsRisk;
        await Click(Named<Button>(hub, "Responder_" + DonationAnswer.PorFora)!);
        LedgerSkeleton? skeleton = game.State.Ledger.Skeletons.LastOrDefault(s => s.Type == FinanceCatalog.CaixaDois);
        if (skeleton is null || engine.AccountsRisk <= riskBefore || !skeleton.KnownBy.Contains(second.Donor.PersonId))
        {
            Fail("aceitar \"por fora\" deveria gravar um esqueleto com quem sabe e subir o risco das contas.");
            return;
        }

        // Painel Contas: limite, risco em faixa com os fatores, esqueleto com quem sabe, fontes e prazos.
        engine.City.Person(second.Donor.PersonId)!.Affinity = -90;
        await Click(Named<Button>(hub, "Area_financas")!);
        await Click(Named<Button>(hub, "VerContas")!);
        string sheet = TextOf(hub);
        Control? irregularity = Named<Control>(hub, "Irregularidade_" + skeleton.Id);
        if (hub.SheetName != "Contas" || Named<Control>(hub, "Dica_contas") is null || irregularity is null || Named<Control>(irregularity, "QuemSabe") is null
            || !Has(TextOf(irregularity), Tr("Quem sabe:"), second.Name, Tr("Chance de vir à tona nesta semana:"), "%")
            || Named<Control>(hub, "RecursosProprios") is null || Named<Control>(hub, "FundoDoPartido") is null || Named<Control>(hub, "FinanciamentoColetivo") is not { } crowd
            || !TextOf(crowd).Contains(Tr("15 de maio"), StringComparison.Ordinal)
            || !Has(sheet, Tr("Faixa:"), Tr("O que pesa no risco"), "72", Tr("15 de setembro"), "30", "art. 23", "art. 30-A", Tr("Doadores")))
        {
            Fail($"o painel Contas não mostra limite, risco em faixa com os fatores, esqueleto com quem sabe e a chance de vir à tona, fontes e prazos: {sheet}");
            return;
        }

        GD.Print("  " + TextOf(Named<Control>(hub, "RiscoDasContas")!).Replace("\n", " | ", StringComparison.Ordinal));
        GD.Print("  " + TextOf(irregularity).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_contas");

        // Depois da eleição: prestação final, julgamento e representação andam na tela do mandato (ou da entressafra).
        while (campaign.Phase != CampaignPhase.Votacao)
        {
            ClearInterruptions();
            game.AdvanceWeek();
        }

        Array.Fill(campaign.Territory.Presence[RaceSetup.PlayerId], 100);
        campaign.Player.Recognition = 100;
        campaign.Player.Image = 1;
        engine.CastPlayerVote(new UrnaVote(Office.Vereador, campaign.Player.Number, false));
        engine.ResolveElection();
        RemoveChild(hub);
        hub.QueueFree();
        game.ConcludeElection();
        if (game.State.Accounts is not { } accounts || accounts.OffBooks <= 0)
        {
            Fail("depois da eleição deveria existir o processo de contas, com o dinheiro \"por fora\" registrado.");
            return;
        }

        var seen = new List<string>();
        bool shown = false;
        for (int week = 0; week < 130 && (week == 0 || accounts.Stage != AccountsStage.Encerrado); week++)
        {
            // A primeira volta mostra o que saiu entre a eleição e a posse (prestação final; representação, se houver).
            if (week > 0)
            {
                game.AdvanceWeek();
            }

            if (game.AccountsNews.Count == 0)
            {
                continue;
            }

            seen.AddRange(game.AccountsNews.Select(h => h.Text));
            game.ResetNavigationForTest();
            game.GoToCareer();
            if (game.LastNavigation is not { } scene)
            {
                Fail("a carreira não soube para que tela ir depois da novidade das contas.");
                return;
            }

            var screen = GD.Load<PackedScene>(scene).Instantiate<Control>();
            AddChild(screen);
            await Frames(4);
            Control? panel = Named<Control>(screen, "ContasDaEleicao");
            if (panel is null || Named<Control>(panel, "NovidadeContas") is null || !TextOf(panel).Contains(game.AccountsNews[0].Text, StringComparison.Ordinal)
                || !Has(TextOf(panel), Tr("Prestação de contas final"), Tr("Julgamento das contas"), Tr("Representação por captação ou gasto ilícito"), "art. 30-A"))
            {
                Fail($"a tela depois da eleição ({scene}) deveria mostrar o andamento das contas com a novidade da semana: {(panel is null ? "sem painel" : TextOf(panel))}");
                return;
            }

            GD.Print("  " + TextOf(panel).Replace("\n", " | ", StringComparison.Ordinal));
            if (!shown)
            {
                Save(shot, "_julgamento");
                shown = true;
            }

            if (accounts.MandateLost && (game.State.Mandate is not null || game.State.Executive is not null || scene != Game.Scenes.Hub || game.Entressafra is null))
            {
                Fail("com o diploma cassado, o mandato deveria ter acabado e a carreira ido para a entressafra.");
                return;
            }

            RemoveChild(screen);
            screen.QueueFree();
            await Frames(1);
        }

        if (accounts.Verdict is null || seen.Count < 2 || accounts.Stage != AccountsStage.Encerrado)
        {
            Fail($"o processo de contas não chegou ao fim: etapa {accounts.Stage}, {seen.Count} novidades.");
            return;
        }

        if (shot is not null)
        {
            GetViewport().GetTexture().GetImage().SavePng(shot);
        }

        GD.Print($"Smoke contas: ok (bloco Contas com a barra do limite e o risco em faixa, oferta com recibo e \"por fora\" com previsão, esqueleto com quem sabe e chance de vir à tona, fontes e prazos; depois da eleição: {string.Join("; ", seen)}{(accounts.MandateLost ? "; mandato cassado" : "")})");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ telas novas no layout e no explicacao

    /// <summary>Estado para as telas da R3 aparecerem cheias: ataque com prazo, oferta de doação, esqueleto, tema em alta, notícias e pacto.</summary>
    private static void PrepareR3Panels(HubScreen hub)
    {
        Game game = Game.Instance;
        var engine = game.Engine!;
        var campaign = game.State!.Campaign!;
        game.State.Player.Resources.Money = 50_000;
        HireFor(engine, StaffRole.Coordenador);
        var theme = engine.Context.Themes.Themes[0];
        if (engine.Media.Hot.Count == 0)
        {
            engine.Media.Hot.Add(new HotTheme { Theme = theme.Id, Zones = [.. campaign.Territory.Zones.Take(Math.Max(1, campaign.Territory.Zones.Count / 2))], From = engine.Today, Until = engine.Today.AddDays(13), News = 6 });
        }

        var donors = engine.Donors();
        engine.Finance.Offers.Clear();
        if (donors.Count > 0)
        {
            DonorOffer dirty = AddOffer(engine, donors[0], Math.Round(engine.SpendingLimit * 0.2m / 10) * 10, offBooks: true);
            engine.AnswerDonation(dirty.Id, DonationAnswer.PorFora);
            engine.City.Person(donors[0].Donor.PersonId)!.Affinity = -90;
            DonorView open = engine.Donors()[^1];
            AddOffer(engine, open, Math.Max(100, Math.Floor(open.CapLeft / 2 / 10) * 10), offBooks: false);
        }

        List<Candidate> rivals = RivalsOf(campaign);
        if (rivals.Count > 1 && engine.Rivals.PactWith(rivals[1].Id) is null)
        {
            engine.Rivals.Pacts.Add(new Pact { Id = "pc_teste", RivalId = rivals[1].Id, Kind = PactKind.NaoAgressao, Since = engine.Today, ClosedZones = [campaign.Territory.Zones[^1]] });
        }

        if (rivals.Count > 0)
        {
            engine.AttackRival(new AttackRequest(rivals[0].Id, AttackType.Tema, AttackChannel.Redes, Theme: theme.Id));
            engine.Emit(FactKind.Boato, "uma mensagem sem autor circula nas redes sobre a campanha", rivals[0].Id, target: RaceSetup.PlayerId, visibility: 2, rumor: true, falso: true);
            campaign.Agenda.AddFeed(new FeedItem(engine.Today, "rival_zona", $"{rivals[0].Name} fez campanha em {engine.ZoneName(campaign.Territory.Zones[0])}", campaign.Territory.Zones[0], null, rivals[0].Id));
        }

        // Dois dias de noticiário, e então um ataque com o prazo correndo.
        for (int day = 0; day < 2; day++)
        {
            ClearInterruptions();
            game.AdvanceDay();
        }

        ClearInterruptions();
        if (engine.PendingAttacks().Count == 0)
        {
            engine.TriggerRivalAttack();
        }

        game.State.Player.Resources.Money = 50_000;
        hub.Refresh();
    }

    /// <summary>Percorre as telas da R3 chamando <paramref name="check"/> em cada uma (o layout mede; o explicacao confere tooltips e links).</summary>
    private async Task VisitR3Panels(HubScreen hub, Func<string, string?, Task> check)
    {
        Game game = Game.Instance;
        var engine = game.Engine!;
        var campaign = game.State!.Campaign!;
        PrepareR3Panels(hub);
        await Frames(3);
        List<Candidate> rivals = RivalsOf(campaign);

        await Press(hub, "Area_pessoas");
        await check("área Pessoas com a chapa e os rivais", "pessoas_r3");
        if (Named<Button>(hub, "VerChapa") is not null)
        {
            await Press(hub, "VerChapa");
            await check("painel da chapa", "chapa");
            await Press(hub, "Voltar");
        }

        if (rivals.Count > 0)
        {
            await PressCard(hub, "Rival_" + rivals[0].Id);
            await check("ficha do rival", "rival");
            await Press(hub, "AbrirAtaque");
            await check("atacar um rival (bloqueado pelo intervalo)", null);
            await Press(hub, "CancelarAtaque");
            await Press(hub, "Voltar");
        }

        if (rivals.Count > 1)
        {
            await PressCard(hub, "Rival_" + rivals[1].Id);
            await check("ficha do rival com pacto", null);
            await Press(hub, "AbrirAtaque");
            SelectItem(hub, "TipoAtaque", engine.Context.Campaign.Attacks.Type(AttackType.Esqueleto)!.Nome);
            await Frames(3);
            await check("atacar um rival com a previsão", "atacar");
            SelectItem(hub, "CanalAtaque", engine.Context.Campaign.Attacks.Channel(AttackChannel.Imprensa)!.Nome);
            await Frames(3);
            await check("atacar um rival por um veículo", null);
            await Press(hub, "CancelarAtaque");
        }

        // Marcação que quebra o pacto.
        await Press(hub, "Area_territorio");
        await Press(hub, "Zona_" + campaign.Territory.Zones[^1]);
        await Press(hub, "MarcarAqui");
        SelectOption(hub, "Tipo", "caminhada");
        await Frames(3);
        await check("marcação que quebra um pacto", null);
        await Press(hub, "Cancelar");

        // Mídia: a área, um veículo de cada tipo, a entrevista com veículo e tema em alta.
        await Press(hub, "Area_midia");
        await check("área Mídia", "midia");
        var outlets = engine.Outlets();
        foreach (OutletView outlet in outlets.GroupBy(o => o.Outlet.Type).Select(g => g.First()))
        {
            await PressCard(hub, "Veiculo_" + outlet.Outlet.Id);
            await check($"ficha do veículo ({outlet.TypeName})", outlet == outlets[0] ? "veiculo" : null);
            await Press(hub, "Voltar");
        }

        if (outlets.Count > 0)
        {
            await PressCard(hub, "Veiculo_" + outlets[0].Outlet.Id);
            await Press(hub, "MarcarEntrevistaAqui");
            SelectItem(hub, "Tema", engine.Context.Themes.Themes[0].Name);
            await Frames(3);
            await check("entrevista com veículo e tema em alta", "entrevista");
            await Press(hub, "Cancelar");
            if (engine.City.Person(outlets[0].Outlet.JournalistId) is not null)
            {
                await Press(hub, "Pessoa_" + outlets[0].Outlet.JournalistId);
                await check("ficha de jornalista", null);
            }
        }

        if (engine.PendingAttacks().FirstOrDefault() is { } pending)
        {
            await Press(hub, "Area_midia");
            await check("faixa do ataque com prazo", null);
            await PressCard(hub, "AtaquePendente_" + pending.Attack.Id);
            await check("ataque contra você com as respostas", "ataque");
        }

        // Finanças: o bloco, o painel Contas, a oferta e a ficha do doador.
        await Press(hub, "Area_financas");
        await check("área Finanças com o bloco Contas", "financas");
        await Press(hub, "VerContas");
        await check("painel Contas", "contas");
        if (engine.Finance.Offers.FirstOrDefault() is { } offer)
        {
            await PressCard(hub, "Oferta_" + offer.Id);
            await check("oferta de doação", "oferta");
            await Press(hub, "Voltar");
            await PressCard(hub, "Doador_" + offer.DonorId);
            await check("ficha de doador", null);
        }

        await Press(hub, "Area_partido");
        await check("área Partido com a chapa e a direção", null);

        // Mapa no modo Disputa.
        await Press(hub, "Area_campanha");
        await Press(hub, "Modo_disputa");
        await check("mapa no modo Disputa", "mapa_disputa");
        await Press(hub, "Modo_forca");

        // Cartão de evento com pessoas e veículo.
        if (game.Events.Events.FirstOrDefault(e => e.Id == "campanha_radio_local") is { } definition && engine.City.Outlets.Count > 0)
        {
            ClearInterruptions();
            EventEngine.Trigger(engine.Context, definition.Id, game.State.Rng);
            hub.Refresh();
            await Frames(3);
            await check("cartão de evento com pessoas e veículo", "evento");
            ClearInterruptions();
            hub.Refresh();
            await Frames(2);
        }
    }
}
