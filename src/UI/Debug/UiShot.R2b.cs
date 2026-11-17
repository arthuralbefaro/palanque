using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.State;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Testes de fumaça da R2b, com cliques de mouse: equipe (contratar, decisão da equipe, delegar, demitir), verba por
/// canal (repartir, previsão com as duas contas, bloqueios), plataforma (mudar posição, Livro, promessa, bandeiras) e
/// objetivos (coluna direita, fixar, dispensar). Também o que o `layout` e o `explicacao` percorrem das telas novas.
/// </summary>
public partial class UiShot
{
    private static readonly System.Globalization.CultureInfo PtBr = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    private static string Money(decimal value) => string.Create(PtBr, $"R$ {value:N0}");

    /// <summary>Escolhe um item de uma lista de escolha pelo começo do texto.</summary>
    private static bool SelectItem(Node root, string name, string startsWith)
    {
        if (Named<OptionButton>(root, name) is not { } options)
        {
            return false;
        }

        for (int i = 0; i < options.ItemCount; i++)
        {
            if (options.GetItemText(i).StartsWith(startsWith, StringComparison.Ordinal))
            {
                options.Select(i);
                options.EmitSignal(OptionButton.SignalName.ItemSelected, i);
                return true;
            }
        }

        return false;
    }

    /// <summary>Tira da frente o que pausaria o teste: eventos, decisões da equipe e pedidos sorteados na virada da semana.</summary>
    private static void ClearInterruptions()
    {
        Game game = Game.Instance;
        var campaign = game.State!.Campaign!;
        campaign.Events.Pending.Clear();
        campaign.Team.Pending.Clear();
        campaign.Agenda.PendingRequest = null;
        campaign.Debate = null;
    }

    private bool NeedsWindow(string what)
    {
        if (DisplayServer.GetName() != "headless")
        {
            return false;
        }

        Fail($"o teste {what} clica com o mouse e precisa de janela (rode sem --headless).");
        return true;
    }

    private static bool ThreePartTip(Control? c) =>
        c is not null && c.TooltipText.Contains(TranslationServer.Translate("O que faz:"), StringComparison.Ordinal) && c.TooltipText.Contains(TranslationServer.Translate("Quanto custa:"), StringComparison.Ordinal);

    // ------------------------------------------------------------------ equipe

    /// <summary>
    /// Equipe (GDD 17.7): contrata um coordenador clicando (com a previsão antes), delega um compromisso, recebe o
    /// conselho da semana e o ignora, responde a uma decisão da equipe forçada e demite com confirmação.
    /// </summary>
    private async Task PlayTeam(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("da equipe"))
        {
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(MediumCity, 11);
        var campaign = game.State!.Campaign!;
        var engine = game.Engine!;
        game.State.Player.Resources.Money = 30_000;
        hub.Refresh();
        await Frames(2);

        // A área Campanha tem os três blocos com "ver tudo".
        string area = TextOf(hub);
        if (Named<Button>(hub, "VerEquipe") is null || Named<Button>(hub, "VerVerba") is null || Named<Button>(hub, "VerPlataforma") is null
            || !area.Contains($"Equipe (0/{engine.TeamSlots})", StringComparison.Ordinal))
        {
            Fail("a área Campanha não mostra os blocos Equipe (0/N), Verba por canal e Plataforma com \"Ver tudo\".");
            return;
        }

        Save(shot, "_campanha");
        await Click(Named<Button>(hub, "VerEquipe")!);
        if (hub.SheetName != "Equipe")
        {
            Fail("\"Ver tudo\" da Equipe não abriu a tela da equipe.");
            return;
        }

        StaffView? candidate = engine.StaffCandidates(StaffRole.Coordenador).FirstOrDefault(v => v.HireBlocked is null);
        if (candidate is null)
        {
            Fail("nenhum nome disponível para a coordenação nesta cidade.");
            return;
        }

        string id = candidate.Member.PersonId;
        await ClickCard(Named<Control>(hub, "Membro_" + id)!);
        Control? forecast = Named<Control>(hub, "PrevisaoContratar");
        if (hub.SheetName != "Membro" || forecast is null)
        {
            Fail("o clique no nome não abriu a ficha com a previsão de contratar.");
            return;
        }

        string forecastText = TextOf(forecast);
        string sheet = TextOf(hub);
        if (!forecastText.Contains(Money(candidate.Salary), StringComparison.Ordinal) || !forecastText.Contains(Tr("Quem vai lembrar:"), StringComparison.Ordinal)
            || !sheet.Contains(candidate.Bond, StringComparison.Ordinal) || !sheet.Contains(Tr("Lealdade"), StringComparison.Ordinal) || !sheet.Contains(Tr("Temperamento"), StringComparison.Ordinal))
        {
            Fail($"a ficha não mostra salário, quem vai lembrar, vínculo, lealdade e temperamento: {sheet}");
            return;
        }

        GD.Print("  " + forecastText.Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_ficha_equipe");
        await Click(Named<Button>(hub, "Contratar")!);
        if (engine.StaffIn(StaffRole.Coordenador)?.PersonId != id || engine.City.Person(id)!.Memories.All(m => m.Fact != "contratacao"))
        {
            Fail("Contratar não pôs a pessoa na coordenação ou não gravou a lembrança.");
            return;
        }

        if (Named<Button>(hub, "Demitir") is null || Named<Button>(hub, "Contratar") is not null)
        {
            Fail("depois de contratar, a ficha deveria oferecer Demitir no lugar de Contratar.");
            return;
        }

        // Um segundo membro (pelo Core) para a briga ter com quem acontecer.
        StaffView? second = engine.StaffCandidates(StaffRole.Tesoureiro).FirstOrDefault(v => v.HireBlocked is null);
        if (second is null || engine.Hire(second.Member.PersonId).Failure is not null)
        {
            Fail("não foi possível contratar um segundo membro para o teste.");
            return;
        }

        // Delegar: marca uma caminhada com representante; o turno do candidato continua livre.
        DateOnly today = engine.Today;
        await Click(Named<Button>(hub, "Area_agenda")!);
        await Click(Named<Button>(hub, $"Turno_{today.AddDays(1):yyyyMMdd}_0")!);
        if (Named<Button>(hub, "Delegar") is not { Disabled: false } delegar || !ThreePartTip(delegar))
        {
            Fail("com coordenador, a marcação deveria oferecer \"Mandar representante\" com tooltip em três partes.");
            return;
        }

        await Click(delegar);
        Control? sent = Named<Control>(hub, "Representante");
        string yield = game.Campaign.Staff.Regras.DelegacaoRendimento.ToString("P0", PtBr);
        string left = TextOf(hub);
        if (sent is null || !TextOf(sent).Contains(yield, StringComparison.Ordinal) || !TextOf(sent).Contains(Tr("lembra que você não foi"), StringComparison.Ordinal)
            || !left.Contains($"0 de {engine.DelegationsPerWeek}", StringComparison.Ordinal))
        {
            Fail($"a previsão do compromisso delegado não diz que rende {yield}, que quem recebe lembra, ou quantas delegações restam.");
            return;
        }

        GD.Print("  " + TextOf(sent));
        Save(shot, "_delegar");
        await Click(Named<Button>(hub, "Confirmar")!);
        if (campaign.Agenda.Scheduled.SingleOrDefault() is not { Delegado: true } delegated || engine.DelegationsIn(delegated.Date) != 1 || engine.Occupying(delegated.Date, delegated.Turno) is not null)
        {
            Fail("o compromisso delegado não foi marcado, não gastou uma delegação ou ocupou o turno do candidato.");
            return;
        }

        // A semana corre: o representante vai, e na segunda-feira chega o conselho da semana. O caixa fica curto de
        // propósito: paga a folha de segunda e sobra menos de duas semanas dela, o que pausa o jogo com aviso.
        decimal payroll = engine.Payroll().Weekly;
        for (int day = 0; day < 9 && (campaign.Team.Advice is null || campaign.Agenda.Log.Count == 0); day++)
        {
            ClearInterruptions();
            if (campaign.Agenda.Log.Count > 0)
            {
                game.State.Player.Resources.Money = payroll * 2.5m;
            }

            await Press(hub, "FecharAviso");
            hub.StepDay();
            await Frames(2);
        }

        if (!campaign.Team.PayrollAlert || hub.Running || hub.NoticeText is null || !hub.NoticeText.Contains(Tr("folha"), StringComparison.Ordinal) || Named<Control>(hub, "FaixaDecisao") is null)
        {
            Fail($"com o caixa abaixo de duas semanas de folha, o jogo deveria pausar com a faixa de aviso (aviso: {hub.NoticeText}).");
            return;
        }

        GD.Print("  " + hub.NoticeText);
        game.State.Player.Resources.Money = 30_000;
        await Press(hub, "FecharAviso");

        ClearInterruptions();
        hub.Refresh();
        await Frames(2);
        var done = campaign.Agenda.Log.FirstOrDefault(r => r.Commitment.Delegado);
        if (done is null || done.Factors.All(f => f.Id != "delegado"))
        {
            Fail("o compromisso delegado não aconteceu com o fator do representante.");
            return;
        }

        if (campaign.Team.Advice is not { } advice)
        {
            Fail("a segunda-feira não trouxe o conselho da semana do coordenador.");
            return;
        }

        await Click(Named<Button>(hub, "Area_campanha")!);
        await Click(Named<Button>(hub, "VerEquipe")!);
        Control? adviceCard = Named<Control>(hub, "Conselho");
        if (adviceCard is null || !TextOf(adviceCard).Contains(engine.ZoneName(advice.ZoneId), StringComparison.Ordinal)
            || !ThreePartTip(Named<Button>(hub, "SeguirConselho")) || Named<Button>(hub, "IgnorarConselho") is not { } ignore
            || !ignore.TooltipText.Contains(Tr("lealdade"), StringComparison.OrdinalIgnoreCase))
        {
            Fail("a tela da equipe não mostra o conselho da semana com Seguir (progresso) e Ignorar (custo em lealdade).");
            return;
        }

        GD.Print("  " + TextOf(adviceCard).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_equipe");
        StaffMember coordinator = engine.StaffIn(StaffRole.Coordenador)!;
        int loyalty = coordinator.Loyalty;
        await Click(ignore);
        if (campaign.Team.Advice is not null || coordinator.Loyalty != Math.Max(0, loyalty - game.Campaign.Staff.Regras.LealdadeConselho))
        {
            Fail("Ignorar não fechou o conselho ou não custou a lealdade prevista.");
            return;
        }

        // Decisão da equipe forçada: cartão de 720 px, faixa de decisão, tempo bloqueado, previsão e quem vai lembrar.
        StaffDecision? decision = engine.TriggerStaffEvent(StaffEventDefinition.Briga);
        if (decision is null)
        {
            Fail("o Core não abriu a decisão da equipe pedida (briga).");
            return;
        }

        hub.Refresh();
        await Frames(3);
        Control? card = Named<Control>(hub, "CartaoEquipe");
        StaffEventDefinition definition = game.Campaign.Staff.Event(decision.EventId);
        if (card is null || !hub.StaffCardOpen || hub.Running || Named<Control>(hub, "FaixaDecisao") is null
            || FindAll<Button>(hub).First(b => b.Text == "4x") is not { Disabled: true })
        {
            Fail("a decisão da equipe não abriu o cartão com o jogo pausado, a faixa de decisão e o tempo bloqueado.");
            return;
        }

        string cardText = TextOf(card);
        var options = FindAll<Button>(card).Where(b => b.Name.ToString().StartsWith("OpcaoEquipe_", StringComparison.Ordinal)).ToList();
        if (options.Count != definition.Opcoes.Count || options.Any(b => !ThreePartTip(b)) || !cardText.Contains(Tr("Quem vai lembrar:"), StringComparison.Ordinal)
            || !cardText.Contains(Tr("Lealdade"), StringComparison.OrdinalIgnoreCase) || card.Size.X < 719 || card.Size.Y > 641)
        {
            Fail($"o cartão da decisão da equipe não tem as {definition.Opcoes.Count} opções com previsão e quem vai lembrar, ou passa de 720 × 640 ({card.Size}).");
            return;
        }

        GD.Print("  " + cardText.Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_decisao_equipe");
        await Click(options[0]);
        string changed = TextOf(Named<Control>(hub, "CartaoEquipe")!);
        if (campaign.Team.Pending.Count != 0 || !changed.Contains(Tr("O que mudou"), StringComparison.Ordinal))
        {
            Fail("responder à decisão da equipe não a fechou ou o cartão não mostrou \"O que mudou\".");
            return;
        }

        GD.Print("  " + changed.Replace("\n", " | ", StringComparison.Ordinal));
        await Click(Named<Button>(hub, "Continuar")!);
        if (hub.StaffCardOpen || hub.BlockReason() is not null)
        {
            Fail("depois de Continuar, o cartão da decisão da equipe ficou na tela.");
            return;
        }

        // Sobras da R2a: repetir a semana anterior (o compromisso delegado da semana passada volta a ser marcado) e o
        // aviso visível de estresse acima do limite.
        await Click(Named<Button>(hub, "Area_agenda")!);
        int scheduled = campaign.Agenda.Scheduled.Count;
        if (Named<Button>(hub, "RepetirSemana") is not { Disabled: false } repeat || !ThreePartTip(repeat))
        {
            Fail("a Agenda deveria oferecer \"Repetir a semana anterior\" (houve compromisso na semana passada).");
            return;
        }

        await Click(repeat);
        if (campaign.Agenda.Scheduled.Count != scheduled + 1)
        {
            Fail($"Repetir a semana anterior deveria marcar 1 compromisso; marcou {campaign.Agenda.Scheduled.Count - scheduled}.");
            return;
        }

        int limit = game.Campaign.Commitments.Regras.EstresseLimite;
        game.State.Player.Resources.Stress = limit + 10;
        hub.Refresh();
        await Frames(2);
        if (Named<Control>(hub, "AvisoEstresse") is not { } stress || !TextOf(stress).Contains(limit.ToString(PtBr), StringComparison.Ordinal))
        {
            Fail("com o estresse acima do limite, a tela deveria mostrar o aviso com o que ele causa.");
            return;
        }

        GD.Print("  " + TextOf(stress));
        game.State.Player.Resources.Stress = 0;

        // Demitir: perigoso, com confirmação e previsão.
        StaffMember? target = engine.HiredStaff.FirstOrDefault();
        if (target is null)
        {
            Fail("ninguém sobrou na equipe para o teste de demissão.");
            return;
        }

        await Click(Named<Button>(hub, "Area_campanha")!);
        await Click(Named<Button>(hub, "VerEquipe")!);
        await ClickCard(Named<Control>(hub, "Membro_" + target.PersonId)!);
        if (Named<Button>(hub, "Demitir") is not { } fire || !ThreePartTip(fire))
        {
            Fail("a ficha de quem está na equipe não oferece Demitir com tooltip em três partes.");
            return;
        }

        await Click(fire);
        Control? confirmCard = Named<Control>(hub, "ConfirmarDemissao");
        if (!target.Hired || confirmCard is null || !TextOf(confirmCard).Contains(Tr("Quem vai lembrar:"), StringComparison.Ordinal))
        {
            Fail("Demitir deveria pedir confirmação, com a previsão e quem vai lembrar, antes de valer.");
            return;
        }

        Save(shot, "_demitir");
        await Click(Named<Button>(hub, "ConfirmarDemitir")!);
        if (target.Hired || !target.Left || engine.City.Person(target.PersonId)!.Memories.All(m => m.Fact != "demissao"))
        {
            Fail("confirmar a demissão não tirou a pessoa da equipe ou não gravou a lembrança.");
            return;
        }

        GD.Print($"Smoke equipe: ok (coordenador contratado por clique com previsão; compromisso delegado rende {yield} e não ocupa o turno; pausa de caixa abaixo de duas semanas de folha; conselho da semana ignorado custou lealdade; decisão \"{definition.Titulo}\" respondida no cartão; repetir a semana anterior; aviso de estresse; demissão com confirmação)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ verba

    /// <summary>
    /// Verba por canal (GDD 17.8): confere o bloqueio antes de 16 de agosto com o motivo, reparte verba em dois canais
    /// vendo as duas contas, leva o calendário à campanha oficial, confere o resultado explicado, o comício sem verba
    /// de Rua (bloqueado com o que falta) e com ela, o modo Verba do mapa e o limite legal de cabos.
    /// </summary>
    private async Task PlayBudget(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("da verba"))
        {
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(MediumCity, 11);
        var campaign = game.State!.Campaign!;
        var engine = game.Engine!;
        game.State.Player.Resources.Money = 60_000;
        hub.Refresh();
        await Frames(2);
        await Click(Named<Button>(hub, "VerVerba")!);
        string date = campaign.CampaignStart.ToString("d 'de' MMMM", PtBr);
        Control? phase = Named<Control>(hub, "BloqueioPreCampanha");
        if (hub.SheetName != "Verba" || phase is null || !TextOf(phase).Contains(date, StringComparison.Ordinal)
            || FindAll<Control>(hub).Count(c => c.Name.ToString().StartsWith("Canal_", StringComparison.Ordinal) && !c.IsQueuedForDeletion()) != 4)
        {
            Fail("a tela da verba não mostra os quatro canais e o aviso do que está fechado antes de 16 de agosto.");
            return;
        }

        string all = TextOf(hub);
        foreach (string forbidden in new[] { "Rádio", "TV", "outdoor", "brinde" })
        {
            if (FindAll<Control>(hub).Any(c => c.Name.ToString().StartsWith("Canal_", StringComparison.Ordinal) && TextOf(c).Contains(forbidden, StringComparison.OrdinalIgnoreCase)))
            {
                Fail($"há um canal de verba para \"{forbidden}\": a lei veda esse gasto.");
                return;
            }
        }

        if (!all.Contains(Tr("contratações permitidas"), StringComparison.Ordinal) || Named<Button>(hub, "LeiPropaganda") is null)
        {
            Fail("a tela da verba não mostra o limite legal de cabos ou o atalho para o que a lei permite.");
            return;
        }

        // Rua antes de 16 de agosto: fechado, com o motivo e o que falta.
        await ClickCard(Named<Control>(hub, "Canal_rua")!);
        await Click(Named<Button>(hub, "VerbaMais")!);
        Button? apply = Named<Button>(hub, "AplicarVerba");
        Control? stop = Named<Control>(hub, "Bloqueio");
        if (hub.SheetName != "Canal" || apply is not { Disabled: true } || !apply.TooltipText.StartsWith(Tr("Bloqueado:"), StringComparison.Ordinal)
            || stop is null || !TextOf(stop).Contains(date, StringComparison.Ordinal) || !TextOf(stop).Contains(Tr("faltam"), StringComparison.Ordinal))
        {
            Fail("o canal Rua antes de 16 de agosto deveria estar bloqueado dizendo a data e quanto falta.");
            return;
        }

        GD.Print("  " + TextOf(stop));
        Save(shot, "_verba_bloqueio");
        await Click(Named<Button>(hub, "Voltar")!);

        // Dois canais abertos na pré-campanha: Redes e Material gráfico, cada um com as duas contas na previsão.
        decimal step = engine.ChannelStep;
        foreach (string channel in new[] { ChannelDefinition.Redes, ChannelDefinition.Material })
        {
            await ClickCard(Named<Control>(hub, "Canal_" + channel)!);
            await Click(Named<Button>(hub, "VerbaMais")!);
            Control? forecast = Named<Control>(hub, "PrevisaoVerba");
            string text = forecast is null ? "" : TextOf(forecast);
            if (!text.Contains("Retorno", StringComparison.Ordinal) || !text.Contains("Disputa", StringComparison.Ordinal) || !text.Contains("efeito", StringComparison.Ordinal)
                || !text.Contains(" a +", StringComparison.Ordinal))
            {
                Fail($"a previsão do canal {channel} não mostra as duas contas (retorno e disputa) e a faixa do efeito: {text}");
                return;
            }

            GD.Print("  " + text.Replace("\n", " | ", StringComparison.Ordinal));
            if (channel == ChannelDefinition.Material)
            {
                Save(shot, "_verba_previsao");
            }

            apply = Named<Button>(hub, "AplicarVerba");
            if (apply is not { Disabled: false } || !ThreePartTip(apply))
            {
                Fail($"Aplicar deveria estar liberado para {Money(step)} no canal {channel}, com tooltip em três partes.");
                return;
            }

            await Click(apply);
            if (engine.Budget.Plan.Where(a => a.ChannelId == channel).Sum(a => a.Amount) != step)
            {
                Fail($"Aplicar não gravou {Money(step)} por semana no canal {channel}.");
                return;
            }

            await Click(Named<Button>(hub, "Voltar")!);
        }

        if (engine.BudgetSummary().PlanWeekly != 2 * step)
        {
            Fail("o plano semanal não soma a verba dos dois canais.");
            return;
        }

        // Impulsionamento antes da campanha: acima do teto, bloqueado com o artigo.
        await ClickCard(Named<Control>(hub, "Canal_" + ChannelDefinition.Redes)!);
        for (int i = 0; i < 4; i++)
        {
            await Click(Named<Button>(hub, "VerbaMais")!);
        }

        stop = Named<Control>(hub, "Bloqueio");
        if (Named<Button>(hub, "AplicarVerba") is not { Disabled: true } || stop is null || !TextOf(stop).Contains("57-C", StringComparison.Ordinal))
        {
            Fail("impulsionamento acima do teto da pré-campanha deveria estar bloqueado citando o art. 57-C.");
            return;
        }

        GD.Print("  " + TextOf(stop));

        // O calendário anda até a campanha oficial (o plano é cobrado toda segunda-feira).
        while (engine.BeforeOfficialCampaign)
        {
            ClearInterruptions();
            game.AdvanceWeek();
        }

        ClearInterruptions();
        game.State.Player.Resources.Money = 60_000;
        await Click(Named<Button>(hub, "Area_campanha")!);
        await Click(Named<Button>(hub, "VerVerba")!);
        var results = FindAll<Control>(hub).Where(c => c.Name.ToString().StartsWith("ResultadoVerba_", StringComparison.Ordinal) && !c.IsQueuedForDeletion()).Select(TextOf).ToList();
        if (engine.Budget.LastResults.Count < 2 || results.Count != engine.Budget.LastResults.Count || results.Any(r => !r.Contains(Tr("Contas:"), StringComparison.Ordinal)) || Named<Control>(hub, "BloqueioPreCampanha") is not null)
        {
            Fail($"na campanha oficial, a tela da verba deveria mostrar o resultado explicado de cada canal da semana passada ({engine.Budget.LastResults.Count} resultados, {results.Count} cartões) e não o aviso de pré-campanha.");
            return;
        }

        GD.Print("  " + results[0].Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_verba_resultado");

        // Comício sem verba de Rua na zona: bloqueado, dizendo o que falta.
        string home = campaign.Territory.Zones[campaign.Territory.HomeZone];
        DateOnly today = engine.Today;
        await Click(Named<Button>(hub, "Area_agenda")!);
        await Click(Named<Button>(hub, $"Turno_{today:yyyyMMdd}_0")!);
        SelectOption(hub, "Tipo", "comicio");
        await Frames(2);
        Button? confirm = Named<Button>(hub, "Confirmar");
        stop = Named<Control>(hub, "Bloqueio");
        string minimum = Money(engine.ComicioRuaMinimum);
        if (confirm is not { Disabled: true } || !confirm.TooltipText.StartsWith(Tr("Bloqueado:"), StringComparison.Ordinal) || stop is null
            || !TextOf(stop).Contains("Rua", StringComparison.Ordinal) || !TextOf(stop).Contains(minimum, StringComparison.Ordinal))
        {
            Fail($"comício sem verba de Rua deveria estar bloqueado dizendo que falta verba de Rua e quanto ({minimum}): {(stop is null ? "sem cartão de bloqueio" : TextOf(stop))}");
            return;
        }

        GD.Print("  " + TextOf(stop));
        Save(shot, "_comicio_sem_rua");
        await Click(Named<Button>(hub, "Cancelar")!);

        // Com a verba de Rua na zona, o comício fica liberado.
        await Click(Named<Button>(hub, "Area_campanha")!);
        await Click(Named<Button>(hub, "VerVerba")!);
        await ClickCard(Named<Control>(hub, "Canal_rua")!);
        if (!SelectItem(hub, "Alvo", engine.ZoneName(home)))
        {
            Fail("o canal Rua não oferece a zona natal como alvo.");
            return;
        }

        await Frames(2);
        for (int guard = 0; guard < 40 && Named<Label>(hub, "ValorVerba") is { } value && value.Text != minimum && Named<Button>(hub, "VerbaMais") is { } more; guard++)
        {
            await Click(more);
        }

        await Click(Named<Button>(hub, "AplicarVerba")!);
        if (engine.Budget.PlanOf(ChannelDefinition.Rua, home) < engine.ComicioRuaMinimum)
        {
            Fail("não foi possível pôr a verba mínima de Rua na zona natal.");
            return;
        }

        // O mapa mostra onde a verba está.
        await Click(Named<Button>(hub, "Modo_verba")!);
        if (!hub.ZoneMap.Markers.Any(m => m.ZoneId == home && m.Text == "Rua") || !hub.ZoneTooltipForTest(home).Contains(Tr("rivais cerca de"), StringComparison.Ordinal))
        {
            Fail("o modo Verba do mapa não marca o canal Rua na zona ou o tooltip não compara com a verba dos rivais.");
            return;
        }

        Save(shot, "_mapa_verba");
        await Click(Named<Button>(hub, "Area_agenda")!);
        await Click(Named<Button>(hub, $"Turno_{today:yyyyMMdd}_0")!);
        SelectOption(hub, "Tipo", "comicio");
        await Frames(2);
        SelectItem(hub, "Zona", engine.ZoneName(home));
        await Frames(2);
        if (Named<Button>(hub, "Confirmar") is not { Disabled: false })
        {
            Fail($"com {minimum} de Rua na zona, o comício deveria estar liberado: {Named<Button>(hub, "Confirmar")?.TooltipText.Split('\n')[0]}");
            return;
        }

        await Click(Named<Button>(hub, "Cancelar")!);

        // Cabos: limite legal e a vaga da equipe.
        await Click(Named<Button>(hub, "Area_campanha")!);
        await Click(Named<Button>(hub, "VerVerba")!);
        await ClickCard(Named<Control>(hub, "Canal_cabos")!);
        int slots = engine.TeamSlotsUsed;
        await Click(Named<Button>(hub, "VerbaMais")!);
        string cabos = TextOf(hub);
        if (!cabos.Contains($"0 de {engine.CabosLimit} {Tr("contratações permitidas")}", StringComparison.Ordinal))
        {
            Fail($"o canal Cabos não mostra \"0 de {engine.CabosLimit} contratações permitidas\".");
            return;
        }

        await Click(Named<Button>(hub, "AplicarVerba")!);
        if (engine.CabosPlanned != game.Campaign.Channels.Regras.CabosPorGrupo || engine.TeamSlotsUsed != slots + 1
            || !TextOf(hub).Contains($"{engine.CabosPlanned} de {engine.CabosLimit} {Tr("contratações permitidas")}", StringComparison.Ordinal))
        {
            Fail("contratar um grupo de cabos não contou no limite legal nem ocupou uma vaga da equipe.");
            return;
        }

        GD.Print($"Smoke verba: ok (Rua bloqueado antes de {date} com motivo; {Money(step)} em Redes e em Material com as duas contas; impulsionamento barrado pelo art. 57-C; {results.Count} resultados explicados; comício sem Rua bloqueado pedindo {minimum}, liberado com a verba; modo Verba no mapa; cabos {engine.CabosPlanned} de {engine.CabosLimit})");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ plataforma

    /// <summary>
    /// Plataforma (GDD 17.9): abre os dez temas e a matriz, muda uma posição vendo a previsão por grupo e por zona e o
    /// custo de incoerência, confere a posição assumida no Livro (F1), define bandeiras até o limite, faz uma promessa a
    /// uma zona e escolhe o tema de uma entrevista.
    /// </summary>
    private async Task PlayPlatform(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("da plataforma"))
        {
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(MediumCity, 11);
        var campaign = game.State!.Campaign!;
        var engine = game.Engine!;
        var themes = engine.Context.Themes.Themes;
        await Click(Named<Button>(hub, "VerPlataforma")!);
        Control? matrix = Named<Control>(hub, "Matriz");
        if (hub.SheetName != "Plataforma" || matrix is null || themes.Count != 10 || themes.Any(t => Named<Control>(hub, "Tema_" + t.Id) is null || !TextOf(matrix).Contains(t.Name, StringComparison.Ordinal)))
        {
            Fail("a tela da plataforma não mostra os 10 temas e a matriz Plataforma × públicos com todos eles.");
            return;
        }

        string list = TextOf(hub);
        if (!list.Contains(Tr("partido "), StringComparison.Ordinal) || !list.Contains(Tr("conhecida "), StringComparison.Ordinal))
        {
            Fail("a lista de temas não mostra a posição do partido e a conhecida.");
            return;
        }

        // Troca o público da matriz: um grupo no lugar da zona.
        var audience = Named<OptionButton>(hub, "PublicoMatriz")!;
        string before = TextOf(matrix);
        int group = Enumerable.Range(0, audience.ItemCount).First(i => audience.GetItemText(i).StartsWith(Tr("Grupo:"), StringComparison.Ordinal));
        audience.Select(group);
        audience.EmitSignal(OptionButton.SignalName.ItemSelected, group);
        await Frames(2);
        if (TextOf(Named<Control>(hub, "Matriz")!) == before)
        {
            Fail("trocar o público da matriz não mudou os valores.");
            return;
        }

        Save(shot, "_plataforma");

        // Um tema já um pouco conhecido, para a mudança ter custo de incoerência.
        var theme = themes[0];
        engine.Platform.Known[theme.Id] = 0.4;
        await ClickCard(Named<Control>(hub, "Tema_" + theme.Id)!);
        int current = engine.PositionOf(theme.Id);
        int target = current <= 0 ? current + 2 : current - 2;
        if (hub.SheetName != "Tema" || Named<Button>(hub, $"Posicao_{target}") is not { } pick)
        {
            Fail("o clique no tema não abriu a tela do tema com as cinco posições.");
            return;
        }

        await Click(pick);
        Control? forecast = Named<Control>(hub, "PrevisaoPosicao");
        string text = forecast is null ? "" : TextOf(forecast);
        bool hasGroup = engine.Context.ElectorateContent.Segments.Dimensions.SelectMany(d => d.Groups).Any(g => text.Contains(g.Name, StringComparison.Ordinal));
        bool hasZone = campaign.Territory.Zones.Any(z => text.Contains(engine.ZoneName(z), StringComparison.Ordinal));
        if (engine.PositionOf(theme.Id) != current || !text.Contains(Tr("Por grupo"), StringComparison.Ordinal) || !text.Contains(Tr("Por zona"), StringComparison.Ordinal) || !hasGroup || !hasZone
            || !text.Contains("Incoerência", StringComparison.Ordinal) || !text.Contains(Tr("nas últimas"), StringComparison.Ordinal))
        {
            Fail($"a previsão da mudança deveria vir antes de valer, com grupos, zonas e o custo de incoerência: {text}");
            return;
        }

        GD.Print("  " + text.Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_previsao_posicao");
        int positions = game.State.Ledger.Positions.Count;
        await Click(Named<Button>(hub, "ConfirmarPosicao")!);
        var stated = game.State.Ledger.Positions.LastOrDefault();
        if (engine.PositionOf(theme.Id) != target || game.State.Ledger.Positions.Count != positions + 1 || stated is null || stated.Theme != theme.Id || stated.Value != target
            || Named<Control>(hub, "MudancaPosicao") is not { } changed || !TextOf(changed).Contains("Livro de compromissos", StringComparison.Ordinal))
        {
            Fail("confirmar a posição não a gravou no Livro ou a tela não mostrou o que mudou.");
            return;
        }

        // A posição assumida está no Livro, na Enciclopédia.
        await PressF1();
        Node? encyclopedia = GetTree().Root.FindChild("Enciclopedia", recursive: true, owned: false);
        if (encyclopedia is null)
        {
            Fail("F1 não abriu a Enciclopédia.");
            return;
        }

        await Click(FindAll<Button>(encyclopedia).First(b => b.Text == Tr("Sua carreira")));
        string book = TextOf(encyclopedia);
        string pole = target > 0 ? theme.PositivePole : target < 0 ? theme.NegativePole : Tr("no meio");
        if (!book.Contains(string.Format(Tr("Posição assumida · {0}"), theme.Name), StringComparison.Ordinal) || !book.Contains(pole, StringComparison.Ordinal)
            || !book.Contains(stated.Date.ToString("dd/MM/yyyy", PtBr), StringComparison.Ordinal))
        {
            Fail("o Livro de compromissos na Enciclopédia não mostra a posição assumida com o tema, o lado e a data.");
            return;
        }

        Save(shot, "_livro_posicao");
        await Click(FindAll<Button>(encyclopedia).First(b => b.Text == Tr("Fechar")));
        await Frames(3);

        // Bandeiras: até o máximo; a seguinte fica bloqueada com o motivo.
        int max = game.Campaign.Platform.Regras.BandeirasMax;
        for (int i = 0; i <= max; i++)
        {
            await Click(Named<Button>(hub, "Area_campanha")!);
            await Click(Named<Button>(hub, "VerPlataforma")!);
            await ClickCard(Named<Control>(hub, "Tema_" + themes[i].Id)!);
            Button flag = Named<Button>(hub, "Bandeira")!;
            if (i < max)
            {
                await Click(flag);
            }
            else if (!flag.Disabled || !flag.TooltipText.StartsWith(Tr("Bloqueado:"), StringComparison.Ordinal))
            {
                Fail($"com {max} bandeiras, a seguinte deveria estar bloqueada com o motivo.");
                return;
            }
        }

        if (engine.Platform.Flags.Count != max || !engine.Platform.Flags.SequenceEqual(themes.Take(max).Select(t => t.Id)))
        {
            Fail($"as bandeiras não foram definidas ({engine.Platform.Flags.Count} de {max}).");
            return;
        }

        // Promessa a uma zona, com previsão e credibilidade.
        await Click(Named<Button>(hub, "Area_campanha")!);
        if (!TextOf(hub).Contains(themes[0].Name, StringComparison.Ordinal) || !TextOf(hub).Contains(Tr("Bandeiras da campanha"), StringComparison.Ordinal))
        {
            Fail("o bloco Plataforma da área Campanha não mostra as bandeiras.");
            return;
        }

        await Click(Named<Button>(hub, "VerPlataforma")!);
        await Click(Named<Button>(hub, "AbrirPrometer")!);
        var who = Named<OptionButton>(hub, "AlvoPromessa");
        Control? promiseForecast = Named<Control>(hub, "PrevisaoPromessa");
        if (hub.SheetName != "Prometer" || who is null || !who.GetItemText(who.Selected).StartsWith(Tr("Zona:"), StringComparison.Ordinal) || promiseForecast is null
            || !TextOf(promiseForecast).Contains(Tr("credibilidade"), StringComparison.OrdinalIgnoreCase) || !TextOf(promiseForecast).Contains(" a +", StringComparison.Ordinal))
        {
            Fail("a tela de prometer não abriu com uma zona, a previsão de apoio em faixa e a credibilidade.");
            return;
        }

        GD.Print("  " + TextOf(promiseForecast).Replace("\n", " | ", StringComparison.Ordinal));
        Save(shot, "_prometer");
        int promises = game.State.Ledger.Promises.Count;
        await Click(Named<Button>(hub, "ConfirmarPromessa")!);
        var promise = game.State.Ledger.Promises.LastOrDefault();
        if (game.State.Ledger.Promises.Count != promises + 1 || promise is null || promise.ToKind != Core.People.Promise.ParaZona || Named<Control>(hub, "PromessaFeita") is null
            || Named<Button>(hub, "ConfirmarPromessa") is not { Disabled: true })
        {
            Fail("Prometer não gravou a promessa à zona no Livro, não mostrou o que mudou ou deixou repetir a mesma promessa.");
            return;
        }

        // Tema na marcação de uma entrevista.
        DateOnly today = engine.Today;
        await Click(Named<Button>(hub, "Area_agenda")!);
        await Click(Named<Button>(hub, $"Turno_{today:yyyyMMdd}_1")!);
        SelectOption(hub, "Tipo", "entrevista");
        await Frames(2);
        var second = themes[1];
        if (!SelectItem(hub, "Tema", second.Name))
        {
            Fail("a marcação da entrevista não oferece a escolha do tema.");
            return;
        }

        await Frames(2);
        string interview = TextOf(Named<Control>(hub, "Previsao")!);
        if (!interview.Contains(Tr("Posição conhecida em"), StringComparison.OrdinalIgnoreCase) || !interview.Contains(second.Name, StringComparison.Ordinal))
        {
            Fail($"a previsão da entrevista não mostra o ganho de posição conhecida no tema: {interview}");
            return;
        }

        await Click(Named<Button>(hub, "Confirmar")!);
        if (campaign.Agenda.Scheduled.SingleOrDefault(c => c.Type == "entrevista")?.Tema != second.Id)
        {
            Fail("a entrevista não foi marcada com o tema escolhido.");
            return;
        }

        double known = engine.KnownOf(second.Id);
        ClearInterruptions();
        hub.StepDay();
        await Frames(2);
        if (engine.KnownOf(second.Id) <= known || game.State.Ledger.Positions.Last().Theme != second.Id)
        {
            Fail("a entrevista sobre o tema não deixou a posição mais conhecida nem entrou no Livro.");
            return;
        }

        GD.Print($"Smoke plataforma: ok ({theme.Name} de {current:+0;−0;0} para {target:+0;−0;0} com previsão por grupo e zona e incoerência; posição no Livro pela Enciclopédia; {max} bandeiras e a seguinte bloqueada; promessa \"{promise.Text}\" à zona; entrevista sobre {second.Name}: conhecida {known:P0} → {engine.KnownOf(second.Id):P0})");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ objetivos

    /// <summary>
    /// Objetivos (princípio h): de 1 a 3 na coluna direita, entre as Notícias e a Intenção de voto, cada um com medida,
    /// meta, prazo e origem; fixa um, dispensa outro, cumpre um e confere o cartão no feed.
    /// </summary>
    private async Task PlayObjectives(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("dos objetivos"))
        {
            return;
        }

        UI.Settings.ResetForTest();
        HubScreen hub = await OpenHubIn(MediumCity, 11);
        var campaign = game.State!.Campaign!;
        var engine = game.Engine!;
        game.State.Player.Resources.Money = 20_000;
        hub.Refresh();
        await Frames(3);
        var views = engine.ActiveObjectives();
        Control? column = Named<Control>(hub, "Objetivos");
        if (views.Count is < 1 or > 3 || column is null)
        {
            Fail($"esperava de 1 a 3 objetivos ativos na coluna direita; há {views.Count}.");
            return;
        }

        foreach (ObjectiveView view in views)
        {
            Control? card = Named<Control>(hub, "Objetivo_" + view.Objective.Id);
            string text = card is null ? "" : TextOf(card);
            if (card is null || !text.Contains(view.Objective.Text, StringComparison.Ordinal) || !text.Contains(Tr("Hoje:"), StringComparison.Ordinal) || !text.Contains(Tr("Origem:"), StringComparison.Ordinal)
                || !card.TooltipText.Contains(view.Objective.Origin, StringComparison.Ordinal)
                || !text.Contains(view.Objective.Deadline.ToString("d/MM", PtBr), StringComparison.Ordinal)
                || !ThreePartTip(Named<Button>(hub, "Fixar_" + view.Objective.Id)) || !ThreePartTip(Named<Button>(hub, "Dispensar_" + view.Objective.Id)))
            {
                Fail($"o objetivo \"{view.Objective.Text}\" não aparece com medida de hoje, meta, prazo, origem e os botões Fixar e Dispensar com tooltip: {text}");
                return;
            }

            GD.Print("  " + text.Replace("\n", " | ", StringComparison.Ordinal));
        }

        // Entre as Notícias (em cima) e a Intenção de voto (embaixo).
        var feedCard = FindAll<RichTextLabel>(hub).First(l => l.GetParsedText() == Tr("Notícias"));
        var poll = FindAll<RichTextLabel>(hub).First(l => l.GetParsedText().StartsWith(Tr("Intenção"), StringComparison.Ordinal));
        if (!(feedCard.GlobalPosition.Y < column.GlobalPosition.Y && column.GlobalPosition.Y < poll.GlobalPosition.Y) || Math.Abs(column.GlobalPosition.X - poll.GlobalPosition.X) > 2)
        {
            Fail("os Objetivos deveriam ficar na coluna direita, entre as Notícias e a Intenção de voto.");
            return;
        }

        Save(shot, "_objetivos");
        Objective pinned = views[0].Objective;
        await Click(Named<Button>(hub, "Fixar_" + pinned.Id)!);
        if (!pinned.Pinned || !TextOf(Named<Control>(hub, "Objetivo_" + pinned.Id)!).Contains(Tr("fixado"), StringComparison.Ordinal))
        {
            Fail("Fixar não fixou o objetivo ou a tela não mostrou.");
            return;
        }

        Objective dismissed = views[^1].Objective;
        if (dismissed == pinned)
        {
            Fail("só há um objetivo ativo: o teste precisa de dois para fixar um e dispensar outro.");
            return;
        }

        await Click(Named<Button>(hub, "Dispensar_" + dismissed.Id)!);
        if (engine.Objectives.Active.Contains(dismissed) || dismissed.Status != ObjectiveStatus.Dispensado || Named<Control>(hub, "Objetivo_" + dismissed.Id) is not null || engine.Objectives.Active.Count < 1)
        {
            Fail("Dispensar não tirou o objetivo do painel.");
            return;
        }

        // Cumpre um objetivo (pondo o estado na meta) e deixa a semana virar: vira cartão no feed.
        Objective? goal = null;
        foreach (Objective o in engine.Objectives.Active)
        {
            switch (o.Kind)
            {
                case ObjectiveType.PresencaZona:
                    campaign.Territory.Presence[RaceSetup.PlayerId][campaign.Territory.IndexOf(o.TargetId)] = o.Goal + 1;
                    goal = o;
                    break;
                case ObjectiveType.JuntarCaixa:
                    game.State.Player.Resources.Money = (decimal)o.Goal + 50_000;
                    goal = o;
                    break;
                case ObjectiveType.PosicaoConhecida:
                    engine.Platform.Known[o.TargetId] = 1;
                    goal = o;
                    break;
                case ObjectiveType.MontarEquipe:
                    if (engine.StaffCandidates(o.TargetId).FirstOrDefault(v => v.HireBlocked is null) is { } hire)
                    {
                        engine.Hire(hire.Member.PersonId);
                        goal = o;
                    }

                    break;
            }

            if (goal is not null)
            {
                break;
            }
        }

        if (goal is null)
        {
            Fail("nenhum dos objetivos ativos é de um tipo que o teste sabe cumprir.");
            return;
        }

        hub.Refresh();
        await Frames(2);
        if (!engine.ActiveObjectives().First(v => v.Objective == goal).Done)
        {
            Fail("o objetivo levado à meta não aparece como cumprido na medida de hoje.");
            return;
        }

        for (int day = 0; day < 8 && goal.Status == ObjectiveStatus.Ativo; day++)
        {
            ClearInterruptions();
            hub.StepDay();
            await Frames(2);
        }

        ClearInterruptions();
        hub.Refresh();
        await Frames(2);
        string done = string.Format(Tr("Objetivo cumprido: {0}"), goal.Text);
        if (goal.Status != ObjectiveStatus.Cumprido || !FeedTexts(hub).Any(t => t.Contains(done, StringComparison.Ordinal)))
        {
            Fail($"o objetivo cumprido não virou cartão no feed (estado: {goal.Status}).");
            return;
        }

        int now = engine.ActiveObjectives().Count;
        if (now is < 1 or > 3)
        {
            Fail($"depois da virada da semana há {now} objetivos ativos (esperado: de 1 a 3).");
            return;
        }

        Save(shot, "_objetivo_cumprido");
        GD.Print($"Smoke objetivos: ok ({views.Count} objetivos na coluna com medida, meta, prazo e origem; \"{pinned.Text}\" fixado; \"{dismissed.Text}\" dispensado; \"{goal.Text}\" cumprido virou cartão no feed; {now} ativos depois)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ telas novas no layout e no explicacao

    /// <summary>Estado para as telas da R2b aparecerem cheias: dois contratados, verba em dois canais, bandeira, posição conhecida e conselho.</summary>
    private static void PrepareCampaignPanels(HubScreen hub)
    {
        Game game = Game.Instance;
        var engine = game.Engine!;
        game.State!.Player.Resources.Money = 50_000;
        foreach (string role in new[] { StaffRole.Coordenador, StaffRole.Marqueteiro })
        {
            if (engine.StaffIn(role) is null && engine.StaffCandidates(role).FirstOrDefault(v => v.HireBlocked is null) is { } candidate)
            {
                engine.Hire(candidate.Member.PersonId);
            }
        }

        var territory = game.State.Campaign!.Territory;
        string home = territory.Zones[territory.HomeZone];
        engine.SetAllocation(ChannelDefinition.Redes, engine.ChannelTargets(ChannelDefinition.Redes)[0].Id, engine.ChannelStep);
        engine.SetAllocation(ChannelDefinition.Material, home, engine.ChannelStep);
        var themes = engine.Context.Themes.Themes;
        engine.SetFlags([themes[0].Id, themes[3].Id]);
        engine.Platform.Known[themes[0].Id] = 0.35;
        engine.Platform.Known[themes[3].Id] = 0.2;

        // A semana vira: a verba rende (resultado explicado) e o coordenador dá o conselho da semana.
        for (int guard = 0; guard < 2 && (engine.Team.Advice is null || engine.Budget.LastResults.Count == 0); guard++)
        {
            ClearInterruptions();
            game.AdvanceWeek();
        }

        ClearInterruptions();
        game.State.Player.Resources.Money = 50_000;
        hub.Refresh();
    }

    /// <summary>Percorre as telas da R2b chamando <paramref name="check"/> em cada uma (o layout mede; o explicacao confere tooltips e links).</summary>
    private async Task VisitCampaignPanels(HubScreen hub, Func<string, string?, Task> check)
    {
        Game game = Game.Instance;
        var engine = game.Engine!;
        PrepareCampaignPanels(hub);
        await Frames(3);
        await Press(hub, "Area_campanha");
        await check("área Campanha com os três blocos", "campanha");

        await Press(hub, "VerEquipe");
        await check("Equipe", "equipe");
        StaffMember hired = engine.HiredStaff.First();
        Named<Control>(hub, "Membro_" + hired.PersonId)!.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        await Frames(3);
        await check("ficha de membro da equipe", null);
        await Press(hub, "Demitir");
        await check("confirmação de demissão", null);
        await Press(hub, "ManterNaEquipe");
        await Press(hub, "Voltar");
        if (engine.StaffCandidates(StaffRole.Tesoureiro).FirstOrDefault(v => !v.Member.Hired) is { } candidate)
        {
            Named<Control>(hub, "Membro_" + candidate.Member.PersonId)!.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            await Frames(3);
            await check("ficha de candidato à equipe", null);
        }

        // Decisão da equipe no cartão sobre o mapa.
        if (engine.TriggerStaffEvent(StaffEventDefinition.Briga) is not null)
        {
            hub.Refresh();
            await Frames(3);
            await check("cartão de decisão da equipe", "decisao_equipe");
            Button option = FindAll<Button>(hub).First(b => b.Name.ToString().StartsWith("OpcaoEquipe_", StringComparison.Ordinal));
            option.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
            await check("o que mudou da decisão da equipe", null);
            await Press(hub, "Continuar");
        }

        // Marcação com representante e com tema.
        await Press(hub, "Area_agenda");
        await check("agenda com repetir a semana", null);
        DateOnly day = engine.Today.AddDays(2);
        await Press(hub, $"Turno_{day:yyyyMMdd}_2");
        if (Named<Button>(hub, "Delegar") is { Disabled: false })
        {
            await Press(hub, "Delegar");
            await check("marcação com representante", null);
        }

        if (SelectOption(hub, "Tipo", "entrevista"))
        {
            await Frames(2);
            SelectItem(hub, "Tema", engine.Context.Themes.Themes[0].Name);
            await Frames(2);
            await check("marcação de entrevista com tema", null);
        }

        await Press(hub, "Cancelar");

        // Verba: a tela, um canal aberto com previsão, um fechado, e o modo Verba do mapa.
        await Press(hub, "Area_campanha");
        await Press(hub, "VerVerba");
        await check("Verba por canal", null);
        foreach (string channel in new[] { ChannelDefinition.Material, ChannelDefinition.Rua, ChannelDefinition.Cabos, ChannelDefinition.Redes })
        {
            Named<Control>(hub, "Canal_" + channel)!.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            await Frames(3);
            await Press(hub, "VerbaMais");
            await check($"canal {channel} com previsão", channel == ChannelDefinition.Material ? "verba" : null);
            if (channel == ChannelDefinition.Material)
            {
                await Press(hub, "Modo_verba");
                await check("mapa no modo Verba", "mapa_verba");
                await Press(hub, "Modo_forca");
            }

            await Press(hub, "Voltar");
        }

        // Plataforma: temas e matriz, um tema com a previsão de mudança, e prometer.
        await Press(hub, "Area_campanha");
        await Press(hub, "VerPlataforma");
        await check("Plataforma com a matriz", "plataforma");
        var theme = engine.Context.Themes.Themes[0];
        Named<Control>(hub, "Tema_" + theme.Id)!.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        await Frames(3);
        int current = engine.PositionOf(theme.Id);
        await Press(hub, $"Posicao_{(current <= 0 ? current + 2 : current - 2)}");
        await check("tema com a previsão de mudança de posição", "tema");
        await Press(hub, "CancelarPosicao");
        await Press(hub, "Voltar");
        await Press(hub, "AbrirPrometer");
        await check("prometer", null);
        await Press(hub, "ConfirmarPromessa");
        await check("promessa feita", null);
        await Press(hub, "Area_campanha");
        await check("coluna com Objetivos", "objetivos");
    }

    /// <summary>Aciona um botão pelo nome (sem mouse: serve com e sem janela) e espera a tela redesenhar.</summary>
    private async Task Press(Node root, string name)
    {
        if (Named<Button>(root, name) is { Disabled: false } button)
        {
            button.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
        }
    }
}
