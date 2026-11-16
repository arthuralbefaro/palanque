using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Governance;
using Planalto.Core.Legislature;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// Testes de fumaça da R4, com cliques de mouse, todos na tela central: mandato de vereador, prefeitura, gabinete e
/// instrumentos, obras com licitação e período vedado, folha e mesa de negociação, indicadores em unidade real e a
/// entressafra. Também o que o `layout` e o `explicacao` percorrem das áreas novas.
/// </summary>
public partial class UiShot
{
    // ------------------------------------------------------------------ preparação

    /// <summary>
    /// Leva uma carreira nova até a posse (vereador ou prefeito) na cidade pedida: a campanha anda sozinha, com
    /// promessas a zonas para haver cobrança no mandato, e a eleição é garantida só para o teste chegar ao mandato.
    /// </summary>
    private async Task<bool> TakeOffice(int codigoIbge, bool mayor, ulong seed = 7)
    {
        Game game = Game.Instance;
        game.PendingWorld = WorldSetup.Create(game.World, "brasil_2026", new Rng(2028)).Setup;
        var world = game.PendingWorld!;
        var city = game.Municipios.All.First(m => m.CodigoIbge == codigoIbge);
        var parties = CampaignContext.ElectoralParties(world.Parties.Select(GameParty.From), game.World.Parties.Generator.SizeShareWeights);
        var local = world.Preset.Scenario == "brasil_2026" ? game.Brasil2026?.Municipio(city.CodigoIbge) : null;
        string party = CampaignContext.PartyStrength(game.PendingSeed, city.CodigoIbge, game.Electorate, parties, local).MaxBy(x => x.Value).Key;
        var player = Core.Characters.PersonGenerator.Generate(new Rng(seed), game.People, new Core.Characters.PersonRequest(RaceSetup.PlayerId, city.Uf, city.CodigoIbge, new DateOnly(world.Options.StartYear, 1, 1), Age: 40, PartyId: party));
        foreach (var attribute in Enum.GetValues<Core.Characters.AttributeId>())
        {
            player.Attributes.Set(attribute, 70);
        }

        game.StartCareer(NewGame.Create(world, player, game.PendingSeed));
        if (mayor && game.SwitchCandidacy(Core.Elections.Office.Prefeito).Issue is { } issue)
        {
            Fail($"candidatura a prefeito recusada ({issue}).");
            return false;
        }

        var engine = game.Engine!;
        var campaign = game.State!.Campaign!;
        foreach (string zone in campaign.Territory.Zones.Take(3))
        {
            if (engine.PromiseOptions(Core.People.Promise.ParaZona, zone).FirstOrDefault(o => o.Blocked is null) is { } option)
            {
                engine.MakePromise(option.Definition.Id, option.ToKind, option.ToId);
            }
        }

        foreach (var leader in engine.City.Leaders.Take(4))
        {
            leader.Known = true;
        }

        int guard = 0;
        while (campaign.Phase != CampaignPhase.Votacao && guard++ < 80)
        {
            while (campaign.Events.Pending.Count > 0)
            {
                var pending = campaign.Events.Pending[0];
                var definition = game.Events.Find(pending.EventId)!;
                Core.Events.EventEngine.Resolve(engine.Context, pending, Enumerable.Range(0, definition.Choices.Count).First(i => Core.Events.EventEngine.Blocked(engine.Context, definition.Choices[i]) is null), game.State.Rng);
            }

            if (campaign.Debate is not null)
            {
                Core.Debate.DebateEngine.Decline(engine.Context, game.Debate);
            }

            campaign.Team.Pending.Clear();
            campaign.Agenda.PendingRequest = null;
            game.AdvanceWeek();
        }

        Array.Fill(campaign.Territory.Presence[RaceSetup.PlayerId], 100);
        campaign.Player.Recognition = 100;
        campaign.Player.Image = 1;
        engine.CastPlayerVote(new UrnaVote(mayor ? Core.Elections.Office.Prefeito : Core.Elections.Office.Vereador, campaign.Player.Number, false));
        engine.ResolveElection();
        if (campaign.Outcome is not { PlayerElected: true })
        {
            Fail("o jogador não se elegeu nem com reconhecimento máximo.");
            return false;
        }

        game.ConcludeElection();
        game.ResetNavigationForTest();
        game.GoToCareer();
        if (game.LastNavigation != Game.Scenes.Hub || game.Governo is null || (mayor ? game.Prefeitura is null : game.Mandate is null))
        {
            Fail($"a posse não levou à tela central com o governo montado (tela {game.LastNavigation}).");
            return false;
        }

        await Frames(1);
        return true;
    }

    private async Task<HubScreen> OpenGovHub()
    {
        UI.Settings.ResetForTest();
        var hub = (HubScreen)GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate();
        AddChild(hub);
        await Frames(5);
        return hub;
    }

    /// <summary>Fecha o que estiver aberto sobre o mapa escolhendo a primeira opção possível, como um jogador apressado.</summary>
    private async Task SettleGovCards(HubScreen hub)
    {
        for (int guard = 0; guard < 6 && hub.GovCardOpen; guard++)
        {
            Node card = Named<Control>(hub, "CartaoMandato")!;
            Button? choice = FindAll<Button>(card).FirstOrDefault(b => !b.Disabled && (b.Name.ToString().StartsWith("Opcao_", StringComparison.Ordinal)
                || b.Name.ToString().StartsWith("Crise_Opcao_", StringComparison.Ordinal) || b.Name == "Continuar" || b.Name == "CobrancaDepois"));
            if (choice is null)
            {
                break;
            }

            choice.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
        }
    }

    /// <summary>Uma semana pela tela (sete dias), resolvendo cartões e fechando avisos e resultados.</summary>
    private async Task GovWeek(HubScreen hub, int weeks = 1)
    {
        for (int day = 0; day < 7 * weeks; day++)
        {
            await SettleGovCards(hub);
            hub.StepDay();
            await Frames(1);
        }

        await SettleGovCards(hub);
        await Press(hub, "FecharAviso");
        await Press(hub, "FecharResultado");
    }

    /// <summary>Clica de verdade numa zona do mapa que esteja visível e livre dos controles do mapa.</summary>
    private async Task<string?> ClickZone(HubScreen hub, Func<string, bool>? accept = null)
    {
        var map = hub.ZoneMap;
        Rect2 safe = new Rect2(map.GlobalPosition, map.Size).Grow(-120);
        foreach (var zona in map.Zonas.Values.OrderByDescending(z => z.Populacao))
        {
            Vector2 at = map.ScreenCenterOf(zona.Id);
            if (map.Hit(at) == zona.Id && safe.HasPoint(map.GlobalPosition + at) && at.Y < map.Size.Y - 420 && (accept is null || accept(zona.Id)))
            {
                await ClickAt(map.GlobalPosition + at);
                return zona.Id;
            }
        }

        return null;
    }

    private static bool BlockedWithReason(Control? c) => c is BaseButton { Disabled: true } && c.TooltipText.StartsWith(TranslationServer.Translate("Bloqueado:"), StringComparison.Ordinal);

    // ------------------------------------------------------------------ mandato

    /// <summary>
    /// Mandato de vereador na tela central: posse, as áreas do trilho, a pauta com voto clicado, a instrução de voto,
    /// semanas correndo pelo relógio, save e load, troca de partido fora da janela (perde o mandato), entressafra na
    /// mesma tela e a abertura da próxima campanha.
    /// </summary>
    private async Task PlayMandate(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("mandate") || !await TakeOffice(SmallCity, mayor: false))
        {
            return;
        }

        HubScreen hub = await OpenGovHub();
        if (!hub.GovMode || hub.AreaName != "mandato" || Named<Button>(hub, "Area_instrumentos") is null)
        {
            Fail($"a tela central não abriu no mandato de vereador (área {hub.AreaName}).");
            return;
        }

        foreach (string area in new[] { "gabinete", "instrumentos", "mesa", "base", "indicadores", "partido", "mandato" })
        {
            await Click(Named<Button>(hub, "Area_" + area)!);
            if (hub.AreaName != area || TextOf(hub).Length < 200)
            {
                Fail($"a área {area} do mandato não abriu.");
                return;
            }
        }

        if (!GovImageBand(hub, game.State!.Mandate!.Image, "mandato"))
        {
            return;
        }

        Save(shot, "_mandato");
        foreach (string mode in new[] { "obras", "aprovacao", "base" })
        {
            await Click(Named<Button>(hub, "Modo_" + mode)!);
        }

        // Semanas até haver projeto no plenário; o voto é marcado com clique.
        GovernanceEngine gov = game.Governo!;
        int voted = 0;
        for (int week = 0; week < 30 && voted == 0; week++)
        {
            await GovWeek(hub);
            await Click(Named<Button>(hub, "Area_mandato")!);
            if (GovernanceQueries.Agenda(gov).FirstOrDefault().Bill is { } bill && Named<Button>(hub, $"Voto_{bill.Id}_{VoteChoice.Sim}") is { } yes)
            {
                await Click(yes);
                if (bill.PlayerVote != VoteChoice.Sim)
                {
                    Fail("o clique em Sim não marcou o voto na pauta.");
                    return;
                }

                voted++;
            }
        }

        if (voted == 0)
        {
            Fail("nenhum projeto chegou ao plenário em 30 semanas de mandato.");
            return;
        }

        await Click(Named<Button>(hub, "Instrucao_" + VoteInstruction.SeguirOrientacao)!);
        if (gov.G.Instruction != VoteInstruction.SeguirOrientacao || Named<Control>(hub, "OQueMudou") is null)
        {
            Fail("a instrução permanente de voto não foi gravada ou não mostrou o que mudou.");
            return;
        }

        await Press(hub, "FecharResultado");
        DateOnly before = game.State!.Calendar.WeekStart;
        await GovWeek(hub, 12);
        if (game.State.Calendar.WeekStart <= before || game.State.Mandate!.Bills.Count(b => b.Decided is not null) == 0 || gov.G.Feed.Count == 0)
        {
            Fail("doze semanas pelo relógio não andaram o mandato (calendário, votações ou notícias).");
            return;
        }

        if (!FeedTexts(hub).Any())
        {
            Fail("a coluna direita do mandato está sem notícias.");
            return;
        }

        // Renovação: morre um vereador. Luto sóbrio na pauta e no feed, e a posse do suplente com a regra na composição.
        string colleague = game.State.Renewal!.Titulares.First(id => id != RaceSetup.PlayerId && game.State.Mandate.Members.Any(m => m.Id == id));
        string colleagueName = gov.NameOf(colleague);
        game.Mundo!.Renewal!.Die(colleague, game.State.Calendar.WeekStart);
        hub.Refresh();
        await Frames(2);
        await Click(Named<Button>(hub, "Area_mandato")!);
        if (!game.Mandate!.InMourning || Named<Control>(hub, "Luto") is not { } mourning || !TextOf(mourning).Contains(Tr("Luto oficial"), StringComparison.Ordinal))
        {
            Fail("a morte de um vereador não pôs o luto oficial na área Mandato.");
            return;
        }

        await GovWeek(hub);
        Control? mourningCard = FindAll<PanelContainer>(hub).FirstOrDefault(p => p.Name.ToString().StartsWith("Luto_", StringComparison.Ordinal) && !p.IsQueuedForDeletion());
        if (mourningCard is null || !TextOf(mourningCard).Contains(colleagueName, StringComparison.Ordinal) || TextOf(mourningCard).Contains('!'))
        {
            Fail("a morte não virou cartão de luto sóbrio nas notícias.");
            return;
        }

        await Click(Named<Button>(hub, "Area_mesa")!);
        await Click(Named<Button>(hub, "VerCadeiras")!);
        string seatsText = TextOf(hub);
        if (hub.GovSheetName != "cadeiras" || !seatsText.Contains(Tr("Posses e saídas recentes"), StringComparison.Ordinal) || !seatsText.Contains(colleagueName, StringComparison.Ordinal) || !seatsText.Contains(Tr("Regra:"), StringComparison.Ordinal))
        {
            Fail("a composição da Câmara não registra a morte e a substituição com a regra.");
            return;
        }

        Save(shot, "_luto");
        await Press(hub, "Voltar");

        SaveManager.Instance.Save("smoke_mandato");
        if (SaveManager.Instance.Load("smoke_mandato") is not null || game.Mandate is null || game.Governo is null)
        {
            Fail("save/load do mandato falhou.");
            return;
        }

        hub.Refresh();
        await Frames(3);

        // Fundação de partido pelo diálogo, a partir da área Partido.
        game.State!.Player.Resources.PoliticalCapital = 50;
        game.State.Player.Resources.Money = 100_000;
        await Click(Named<Button>(hub, "Area_partido")!);
        if (Named<Button>(hub, "Fundar") is null || Named<Button>(hub, "Filiar") is null)
        {
            Fail("a área Partido do mandato está sem Fundar ou sem Filiar-se.");
            return;
        }

        var draft = game.FoundingEngine!.Propose(new Rng(5), game.State.Parties[0].FamilyId);
        if (game.FoundingEngine.Start(draft) is { } failure)
        {
            Fail($"a fundação não começou ({failure}).");
            return;
        }

        // Troca de partido fora da janela: pede confirmação, o mandato acaba e a mesma tela vira a entressafra.
        await Click(Named<Button>(hub, "Filiar")!);
        if (game.State.Mandate is null)
        {
            Fail("trocar de partido fora da janela não pediu confirmação.");
            return;
        }

        await Click(Named<Button>(hub, "Filiar")!);
        await Frames(3);
        if (game.State.Mandate is not null || !hub.GovMode || hub.AreaName != "entressafra" || game.Entressafra is null)
        {
            Fail($"perder o mandato não levou à entressafra na tela central (área {hub.AreaName}).");
            return;
        }

        Save(shot, "_entressafra");
        await Click(Named<Button>(hub, "PularParaCampanha")!);
        await Frames(3);
        if (game.Engine is null || game.State.Campaign is null || hub.GovMode)
        {
            Fail("a próxima campanha não abriu a partir da entressafra.");
            return;
        }

        GD.Print($"Smoke mandate: ok ({voted} voto marcado com clique, {game.State.Calendar.WeekStart:yyyy-MM-dd}, fundação {game.State.Founding?.Stage}, campanha aberta na mesma tela)");
        GetTree().Quit(0);
    }

    /// <summary>"Como te veem" na barra do mandato e da prefeitura: a faixa nomeada e a régua, com o número só no tooltip.</summary>
    private bool GovImageBand(HubScreen hub, double image, string where)
    {
        var config = Game.Instance.Campaign.Config;
        string expected = config.ImageBandOf(image).Name;
        var box = Named<Control>(hub, "Imagem");
        string shown = box is null ? "" : FindAll<Label>(box).FirstOrDefault(l => l.Name == "Faixa")?.Text ?? "";
        int ticks = box is null ? 0 : FindAll<HBoxContainer>(box).FirstOrDefault(r => r.Name == "Regua")?.GetChildCount() ?? 0;
        if (!shown.EndsWith(expected, StringComparison.Ordinal) || shown.Any(char.IsDigit) || ticks != config.ImageScale.Count || !ThreePartTip(box))
        {
            Fail($"{where}: a barra de cima deveria mostrar a imagem na faixa \"{expected}\", sem número, com a régua e o tooltip em três partes (mostra \"{shown}\", {ticks} casas).");
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------ prefeitura

    /// <summary>
    /// Prefeitura na tela central: posse, as áreas do trilho, secretário nomeado com previsão, orçamento aplicado,
    /// política por decreto e com contagem de votos, crise com a causa no cartão e save e load.
    /// </summary>
    private async Task PlayMayor(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("prefeitura") || !await TakeOffice(MediumCity, mayor: true))
        {
            return;
        }

        HubScreen hub = await OpenGovHub();
        if (!hub.GovMode || hub.AreaName != "governo")
        {
            Fail($"a tela central não abriu na prefeitura (área {hub.AreaName}).");
            return;
        }

        foreach (string area in new[] { "secretariado", "obras", "fiscal", "convenios", "camara", "orcamento", "indicadores", "partido", "governo" })
        {
            await Click(Named<Button>(hub, "Area_" + area)!);
            if (hub.AreaName != area || TextOf(hub).Length < 200)
            {
                Fail($"a área {area} da prefeitura não abriu.");
                return;
            }
        }

        if (!GovImageBand(hub, game.State!.Executive!.Image, "prefeitura"))
        {
            return;
        }

        Save(shot, "_prefeitura");
        GovernanceEngine gov = game.Governo!;
        var engine = game.Prefeitura!;

        // Secretariado: demite um titular (com previsão e confirmação) e nomeia um nome oferecido.
        await Click(Named<Button>(hub, "Area_secretariado")!);
        string pasta = game.GovernanceContent.Secretaries.Secretarias.First(p => gov.G.SecretaryOf(p.Id) is not null).Id;
        await Click(Named<Button>(hub, "Pasta_" + pasta)!);
        if (hub.GovSheetName != "pasta" || Named<Control>(hub, "PrevisaoDemitir") is null || !ThreePartTip(Named<Button>(hub, "DemitirSecretario")))
        {
            Fail("a pasta não abriu com o titular, a previsão de demitir e o tooltip em três partes.");
            return;
        }

        await Click(Named<Button>(hub, "DemitirSecretario")!);
        await Click(Named<Button>(hub, "DemitirSecretario")!);
        if (gov.G.SecretaryOf(pasta) is not null)
        {
            Fail("demitir com confirmação não deixou a pasta vaga.");
            return;
        }

        await Press(hub, "FecharResultado");
        if (Named<Control>(hub, "PrevisaoNomear") is null || Named<Button>(hub, "NomearSecretario") is not { Disabled: false } appoint)
        {
            Fail("pasta vaga sem nomes oferecidos, sem previsão de nomear ou com Nomear bloqueado.");
            return;
        }

        await Click(appoint);
        if (gov.G.SecretaryOf(pasta) is null || Named<Control>(hub, "OQueMudou") is null)
        {
            Fail("nomear o secretário não preencheu a pasta ou não mostrou o que mudou.");
            return;
        }

        await Press(hub, "FecharResultado");

        // Orçamento e políticas: aplica o orçamento, decreta um programa e abre a contagem de um que precisa de lei.
        await Click(Named<Button>(hub, "Area_orcamento")!);
        if (Named<Button>(hub, "AplicarOrcamento") is not { Disabled: false } apply)
        {
            Fail("Aplicar o orçamento está bloqueado com o orçamento em 100%.");
            return;
        }

        await Click(apply);
        await Press(hub, "FecharResultado");
        var decree = game.ExecutiveContent.Policies.Policies.First(p => !p.RequiresLaw && p.Cost > 0 && p.Cost <= engine.FreeReserve);
        await Click(Named<Button>(hub, "Politica_" + decree.Id)!);
        if (hub.GovSheetName != "politica" || Named<Button>(hub, "Adotar") is not { Disabled: false } adopt || !ThreePartTip(adopt))
        {
            Fail("a política por decreto não abriu com Decretar liberado e tooltip em três partes.");
            return;
        }

        await Click(adopt);
        if (!game.State!.Executive!.Policies.Any(p => p.PolicyId == decree.Id && p.Counts))
        {
            Fail("o decreto não foi assinado.");
            return;
        }

        await Press(hub, "FecharResultado");
        await Click(Named<Button>(hub, "Area_camara")!);
        var law = game.ExecutiveContent.Policies.Policies.First(p => p.RequiresLaw && gov.CountPolicy(p.Id) is not null);
        await Click(Named<Button>(hub, "Contagem_" + law.Id)!);
        if (hub.GovSheetName != "contagem" || !TextOf(hub).Contains(Tr("Contagem nominal hoje"), StringComparison.Ordinal))
        {
            Fail("a contagem nominal da política que precisa de lei não abriu.");
            return;
        }

        // Semanas pelo relógio, com as crises respondidas no cartão.
        await GovWeek(hub, 16);

        // Crise na mesa: o cartão mostra a causa e as opções com efeito e custo; o tempo fica travado até a resposta.
        gov.G.Crises.Add(new CrisisRecord
        {
            Id = "crise_teste", CrisisId = "enchente", Date = game.State.Calendar.WeekStart, ZoneId = gov.G.Zones[0], CauseKind = CrisisRecord.CausaZonaSemDrenagem, CauseRef = gov.G.Zones[0],
            CauseText = "A zona não tem drenagem entregue", Presented = true,
        });
        game.State.Executive.Crisis = new Core.Executive.PendingCrisis("enchente", game.State.Calendar.WeekStart);
        hub.Refresh();
        await Frames(3);
        if (!hub.GovCardOpen || Named<Control>(hub, "CausaDaCrise") is null || hub.BlockReason() is null || Named<Button>(hub, "Velocidade_1") is not { Disabled: true })
        {
            Fail("a crise não abriu o cartão com a causa ou não travou o tempo.");
            return;
        }

        Save(shot, "_crise");
        await Click(Named<Button>(hub, "Crise_Opcao_0")!);
        if (game.State.Executive.Crisis is not null || !TextOf(Named<Control>(hub, "CartaoMandato")!).Contains(Tr("O que mudou"), StringComparison.Ordinal))
        {
            Fail("responder à crise não mostrou o que mudou.");
            return;
        }

        await Click(Named<Button>(hub, "Continuar")!);
        if (hub.GovCardOpen || hub.EventOpen)
        {
            Fail("o cartão da crise não fechou.");
            return;
        }

        SaveManager.Instance.Save("smoke_prefeitura");
        if (SaveManager.Instance.Load("smoke_prefeitura") is not null || game.Prefeitura is null || game.State!.Executive is null || game.Governo is null)
        {
            Fail("save/load da prefeitura falhou.");
            return;
        }

        GD.Print($"Smoke prefeitura: ok (aprovação {game.State.Executive.Approval:0}%, {game.State.Executive.Policies.Count} políticas, secretário nomeado e crise respondida no cartão)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ gabinete e instrumentos

    /// <summary>
    /// Gabinete e instrumentos: nomeia um assessor com a previsão antes, faz uma indicação com previsão e resultado,
    /// destina uma emenda escolhendo a zona no mapa, abre a contagem nominal e fecha um acordo de voto.
    /// </summary>
    private async Task PlayCabinet(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("gabinete") || !await TakeOffice(SmallCity, mayor: false))
        {
            return;
        }

        HubScreen hub = await OpenGovHub();
        GovernanceEngine gov = game.Governo!;
        GovernanceState g = gov.G;
        game.State!.Player.Resources.PoliticalCapital = 60;

        // Gabinete: ficha do candidato, função, previsão com quem vai lembrar e Nomear.
        await Click(Named<Button>(hub, "Area_gabinete")!);
        AideView candidate = GovernanceQueries.Cabinet(gov, candidates: true).OrderByDescending(c => c.PaysPromise).ThenByDescending(c => c.Aide.Quality).First();
        await ClickCard(Named<Control>(hub, "Assessor_" + candidate.Aide.PersonId)!);
        if (hub.GovSheetName != "assessor")
        {
            Fail("o clique no nome não abriu a ficha do assessor.");
            return;
        }

        await Click(Named<Button>(hub, "Funcao_" + AideRole.Chefe)!);
        Control? forecast = Named<Control>(hub, "PrevisaoNomear");
        if (forecast is null || !TextOf(forecast).Contains(Tr("Quem vai lembrar"), StringComparison.Ordinal) || Named<Button>(hub, "Nomear") is not { Disabled: false } appoint || !ThreePartTip(appoint))
        {
            Fail("a ficha do assessor está sem a previsão de nomear com quem vai lembrar, ou com Nomear bloqueado.");
            return;
        }

        Save(shot, "_assessor");
        await Click(appoint);
        if (!g.Cabinet.Any(a => a.PersonId == candidate.Aide.PersonId && a.Role == AideRole.Chefe) || Named<Control>(hub, "OQueMudou") is null)
        {
            Fail("nomear o assessor não o pôs no gabinete ou não mostrou o que mudou.");
            return;
        }

        await Press(hub, "FecharResultado");
        if (Named<Button>(hub, "Demitir") is null)
        {
            Fail("o assessor nomeado ficou sem a opção de demitir.");
            return;
        }

        // Indicação: tipo, zona escolhida no mapa, previsão e resultado.
        await Click(Named<Button>(hub, "Area_instrumentos")!);
        await Click(Named<Button>(hub, "Instrumento_" + InstrumentDefinition.Indicacao)!);
        if (hub.GovSheetName != "instrumento" || Named<Button>(hub, "Confirmar") is not { } blocked || !BlockedWithReason(blocked))
        {
            Fail("a indicação sem zona escolhida deveria estar bloqueada com o motivo.");
            return;
        }

        var small = game.GovernanceContent.Works.Tipos.First(t => t.Pequena || t.Servico);
        SelectItem(hub, "Tipo", small.Nome);
        await Frames(2);
        string? zone = await ClickZone(hub, z => gov.Fits(small, z));
        if (zone is null || hub.GovZone != zone)
        {
            Fail($"o clique no mapa não escolheu a zona da indicação (escolhida: {hub.GovZone}).");
            return;
        }

        if (Named<Control>(hub, "Previsao") is null || Named<Button>(hub, "Confirmar") is not { Disabled: false } indicate)
        {
            Fail("a indicação com zona escolhida está sem previsão ou bloqueada.");
            return;
        }

        int turns = g.TurnsLeft;
        await Click(indicate);
        if (g.Indications.Count != 1 || g.TurnsLeft >= turns || hub.GovResultText is null || !FeedTexts(hub).Any())
        {
            Fail("a indicação não foi registrada, não gastou turno ou não mostrou o resultado.");
            return;
        }

        await Press(hub, "FecharResultado");

        // Emenda: a obra entra no mapa na zona escolhida com clique.
        await Click(Named<Button>(hub, "Area_instrumentos")!);
        await Click(Named<Button>(hub, "Instrumento_" + InstrumentDefinition.Emenda)!);
        var amendable = game.GovernanceContent.Works.Tipos.First(t => t.Emenda);
        SelectItem(hub, "Tipo", amendable.Nome);
        await Frames(2);
        string? emendaZone = await ClickZone(hub, z => gov.Fits(amendable, z));
        if (emendaZone is null || hub.GovZone != emendaZone || Named<Button>(hub, "Confirmar") is not { Disabled: false } amend)
        {
            Fail($"a emenda não aceitou a zona escolhida no mapa ({hub.GovZone}) ou ficou bloqueada: {TextOf(Named<Control>(hub, "Previsao") ?? hub)[..80]}");
            return;
        }

        Save(shot, "_emenda");
        await Click(amend);
        Work? work = g.Works.FirstOrDefault(w => w.Origin == Work.DeEmenda && w.SponsorId == RaceSetup.PlayerId);
        if (work is null || work.ZoneId != emendaZone)
        {
            Fail("a emenda não criou a obra na zona escolhida.");
            return;
        }

        await Press(hub, "FecharResultado");
        await Click(Named<Button>(hub, "Modo_obras")!);
        if (!hub.ZoneMap.Markers.Any(m => m.ZoneId == emendaZone) || !hub.GovZoneTooltipForTest(emendaZone).Contains(gov.WorkName(work), StringComparison.Ordinal))
        {
            Fail("a obra da emenda não apareceu no mapa, no modo Obras.");
            return;
        }

        Save(shot, "_mapa_obras");

        // Contagem nominal e negociação de voto: semanas até haver indeciso com pedido.
        bool dealt = false;
        for (int week = 0; week < 60 && !dealt; week++)
        {
            await GovWeek(hub);
            foreach (var (bill, count, _, _) in GovernanceQueries.Agenda(gov))
            {
                CountLine? line = count.Lines.FirstOrDefault(l => l.Lean == NominalCount.Indeciso && l.RequestText is not null
                    && gov.ForecastNegociarVoto(bill.Id, l.MemberId, gov.PlannedVote(bill) == VoteChoice.Nao ? -1 : 1).Blocked is null);
                if (line is null)
                {
                    continue;
                }

                await Click(Named<Button>(hub, "Area_mandato")!);
                await Click(Named<Button>(hub, "Contagem_" + bill.Id)!);
                if (hub.GovSheetName != "contagem" || !TextOf(hub).Contains(GovernanceQueries.CountText(gov, count), StringComparison.Ordinal) || Named<Control>(hub, "Indeciso_" + line.MemberId) is null)
                {
                    Fail("a contagem nominal não abriu com a linha da contagem e o indeciso.");
                    return;
                }

                Save(shot, "_contagem");
                await Click(Named<Button>(hub, "Negociar_" + line.MemberId)!);
                if (Named<Control>(hub, "PrevisaoAcordo") is null || Named<Button>(hub, "FecharAcordo") is not { Disabled: false } close)
                {
                    Fail("a negociação de voto não mostrou a previsão do acordo.");
                    return;
                }

                int deals = g.Deals.Count;
                await Click(close);
                if (g.Deals.Count != deals + 1 || Named<Control>(hub, "OQueMudou") is null)
                {
                    Fail("fechar o acordo de voto não o registrou ou não mostrou o que mudou.");
                    return;
                }

                dealt = true;
                break;
            }
        }

        if (!dealt)
        {
            Fail("em 60 semanas de mandato não houve votação com indeciso para negociar.");
            return;
        }

        GD.Print($"Smoke gabinete: ok (assessor nomeado com previsão, indicação em {zone}, emenda em {emendaZone} no mapa, contagem nominal e acordo de voto)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ obras

    /// <summary>
    /// Obras do prefeito: inicia a obra escolhendo a zona no mapa, escolhe a modalidade de licitação com o bloqueio
    /// explicado, acompanha no mapa, inaugura a obra pronta e, na campanha de reeleição, esbarra no período vedado.
    /// </summary>
    private async Task PlayWorks(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("obras") || !await TakeOffice(MediumCity, mayor: true))
        {
            return;
        }

        HubScreen hub = await OpenGovHub();
        GovernanceEngine gov = game.Governo!;
        GovernanceState g = gov.G;
        await Click(Named<Button>(hub, "Area_obras")!);
        await Click(Named<Button>(hub, "IniciarObra")!);
        if (hub.GovSheetName != "nova_obra" || !BlockedWithReason(Named<Button>(hub, "Confirmar")))
        {
            Fail("o início de obra sem zona deveria estar bloqueado com o motivo.");
            return;
        }

        var affordable = game.GovernanceContent.Works.Tipos.Where(t => t.Engenharia && !t.Pequena && gov.CostOf(t) <= gov.InvestmentLeft).OrderByDescending(t => gov.CostOf(t)).FirstOrDefault()
            ?? game.GovernanceContent.Works.Tipos.Where(t => gov.CostOf(t) <= gov.InvestmentLeft).OrderByDescending(t => gov.CostOf(t)).First();
        SelectItem(hub, "Tipo", affordable.Nome);
        await Frames(2);
        string? zone = await ClickZone(hub, z => gov.Fits(affordable, z));
        if (zone is null || hub.GovZone != zone || Named<Control>(hub, "Previsao") is null || Named<Button>(hub, "Confirmar") is not { Disabled: false } start)
        {
            Fail($"o início de obra não aceitou a zona do mapa ou ficou bloqueado: {TextOf(Named<Control>(hub, "Previsao") ?? hub)}");
            return;
        }

        Save(shot, "_nova_obra");
        await Click(start);
        Work? work = g.Works.FirstOrDefault(w => w.ZoneId == zone && w.TypeId == affordable.Id && w.SponsorId == Work.Prefeito && w.Stage == WorkStage.Projeto);
        if (work is null || hub.GovSheetName != "obra")
        {
            Fail("iniciar a obra não a criou na zona escolhida nem abriu a ficha dela.");
            return;
        }

        await Press(hub, "FecharResultado");

        // O projeto anda pelo relógio até a escolha da modalidade.
        for (int week = 0; week < 20 && work.Stage == WorkStage.Projeto; week++)
        {
            await GovWeek(hub);
        }

        if (work.Stage != WorkStage.Modalidade)
        {
            Fail($"o projeto não chegou à escolha da modalidade em 20 semanas (etapa {work.Stage}).");
            return;
        }

        await Click(Named<Button>(hub, "Area_obras")!);
        await Click(Named<Button>(hub, "Obra_" + work.Id)!);
        var modes = gov.ModesFor(work);
        var barred = modes.FirstOrDefault(m => m.Blocked is not null);
        if (barred.Option is null || Named<Control>(hub, "Bloqueio_" + barred.Option.Modo) is not { } reason || TextOf(reason).Length < 30 || !BlockedWithReason(Named<Button>(hub, "Escolher_" + barred.Option.Modo)))
        {
            Fail("nenhuma modalidade bloqueada com o motivo explicado na ficha da obra.");
            return;
        }

        if (!TextOf(hub).Contains("Lei 14.133/2021", StringComparison.Ordinal))
        {
            Fail("a escolha da modalidade não cita a base legal.");
            return;
        }

        Save(shot, "_modalidade");
        var allowed = modes.First(m => m.Blocked is null && !m.Irregular);
        await Click(Named<Button>(hub, "Escolher_" + allowed.Option.Modo)!);
        if (work.Mode != allowed.Option.Modo || work.Stage != WorkStage.Licitacao)
        {
            Fail("escolher a modalidade não mandou a obra para a licitação.");
            return;
        }

        await Press(hub, "FecharResultado");
        await Click(Named<Button>(hub, "Modo_obras")!);
        if (!hub.ZoneMap.Markers.Any(m => m.ZoneId == zone) || !hub.GovZoneTooltipForTest(zone).Contains(gov.WorkName(work), StringComparison.Ordinal))
        {
            Fail("a obra não aparece no mapa, no modo Obras.");
            return;
        }

        Save(shot, "_mapa_obras");

        // Até a obra ficar pronta (semanas sem tela, com as crises respondidas), e a inauguração pela ficha.
        for (int week = 0; week < 220 && work.Stage is not (WorkStage.Pronta or WorkStage.Entregue or WorkStage.Cancelada) && game.State!.Campaign is null; week++)
        {
            if (game.State.Executive?.Crisis is not null)
            {
                game.Prefeitura!.ResolveCrisis(0);
            }

            if (g.Decision is not null && GovernanceQueries.Decision(gov) is { } pending)
            {
                gov.Decide(pending.Default.Id);
            }

            game.AdvanceWeek();
        }

        if (work.Stage != WorkStage.Pronta)
        {
            Fail($"a obra não ficou pronta antes da campanha de reeleição (etapa {work.Stage}, {game.State!.Calendar.WeekStart:yyyy-MM-dd}).");
            return;
        }

        hub.Refresh();
        await Frames(3);
        await SettleGovCards(hub);
        await Press(hub, "FecharAviso");
        await Click(Named<Button>(hub, "Area_obras")!);
        await Click(Named<Button>(hub, "Obra_" + work.Id)!);
        if (Named<Control>(hub, "PrevisaoInaugurar") is null || Named<Button>(hub, "Inaugurar") is not { Disabled: false } inaugurate || Named<Button>(hub, "EntregarSemCerimonia") is null)
        {
            Fail("a obra pronta está sem a previsão de inaugurar, sem Inaugurar liberado ou sem a entrega sem cerimônia.");
            return;
        }

        Save(shot, "_inaugurar");
        await Click(inaugurate);
        if (work.Stage != WorkStage.Entregue || !work.Ceremony || Named<Control>(hub, "OQueMudou") is null)
        {
            Fail("inaugurar não entregou a obra com cerimônia ou não mostrou o que mudou.");
            return;
        }

        // Período vedado: na campanha de reeleição, nos três meses antes do pleito, o prefeito não inaugura.
        int guard = 0;
        while ((game.State!.Campaign is null || !gov.PlayerBarredFromInauguration) && guard++ < 260 && game.State.Executive is not null)
        {
            if (game.State.Executive.Crisis is not null)
            {
                game.Prefeitura!.ResolveCrisis(0);
            }

            if (g.Decision is not null && GovernanceQueries.Decision(gov) is { } pending)
            {
                gov.Decide(pending.Default.Id);
            }

            if (game.State.Campaign is { } running)
            {
                running.Events.Pending.Clear();
                running.Team.Pending.Clear();
                running.Agenda.PendingRequest = null;
                running.Debate = null;
            }

            game.AdvanceWeek();
        }

        if (game.State.Campaign is null || game.State.Executive is null || !gov.PlayerBarredFromInauguration)
        {
            Fail($"a campanha de reeleição não chegou ao período vedado ({game.State.Calendar.WeekStart:yyyy-MM-dd}).");
            return;
        }

        ClearInterruptions();
        Work ready = g.Works.FirstOrDefault(w => w.Stage == WorkStage.Pronta && w.SponsorId == Work.Prefeito)
            ?? g.Works.First(w => w.Open && w.SponsorId == Work.Prefeito);
        ready.Stage = WorkStage.Pronta;
        hub.Refresh();
        await Frames(3);
        if (hub.GovMode || Named<Button>(hub, "Area_gov_obras") is not { } worksArea)
        {
            Fail("na campanha de reeleição, o trilho não ganhou a área das obras do mandato.");
            return;
        }

        await Click(worksArea);
        if (Named<Control>(hub, "PeriodoVedado") is not { } notice || !TextOf(notice).Contains("art. 77", StringComparison.Ordinal))
        {
            Fail("as obras do mandato não avisam do período vedado com o artigo.");
            return;
        }

        await Click(Named<Button>(hub, "Obra_" + ready.Id)!);
        Button? barredInauguration = Named<Button>(hub, "Inaugurar");
        if (!BlockedWithReason(barredInauguration) || !barredInauguration!.TooltipText.Contains("art. 77", StringComparison.Ordinal))
        {
            Fail("no período vedado, Inaugurar deveria estar bloqueado com o artigo no motivo.");
            return;
        }

        Save(shot, "_periodo_vedado");
        await Click(Named<Button>(hub, "EntregarSemCerimonia")!);
        if (ready.Stage != WorkStage.Entregue || ready.Ceremony)
        {
            Fail("a entrega sem cerimônia não entregou a obra no período vedado.");
            return;
        }

        GD.Print($"Smoke obras: ok ({affordable.Nome} em {zone}: {allowed.Option.Nome}, {barred.Option.Nome} bloqueada com motivo, inaugurada; período vedado em {game.State.Calendar.WeekStart:yyyy-MM-dd} com entrega sem cerimônia)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ fiscal

    /// <summary>
    /// Pessoal e fiscal: a folha na faixa prudencial com as três marcas na barra e os bloqueios do art. 22, a mesa de
    /// negociação com o reajuste vedado e a revisão geral possível, os mínimos de saúde e educação e o repasse à Câmara.
    /// </summary>
    private async Task PlayFiscal(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("fiscal") || !await TakeOffice(MediumCity, mayor: true))
        {
            return;
        }

        HubScreen hub = await OpenGovHub();
        GovernanceEngine gov = game.Governo!;
        GovernanceState g = gov.G;
        FiscalLawConfig law = gov.Context.FiscalLaw;
        g.Payroll.Share = (law.PrudencialExecutivo + law.LimitePessoalExecutivo) / 2;
        g.Payroll.Band = law.Faixa(g.Payroll.Share);
        g.Fiscal.GeneralRevision = 0.03;
        await Click(Named<Button>(hub, "Area_fiscal")!);
        Control? bar = Named<Control>(hub, "BarraFolha");
        if (bar is null || Named<Control>(bar, "MarcaAlerta") is null || Named<Control>(bar, "MarcaPrudencial") is not { } prudential || Named<Control>(bar, "MarcaLimite") is not { } limit
            || Named<Control>(bar, "Preenchimento") is not { } fill)
        {
            Fail("a barra da folha está sem as marcas de alerta, prudencial e limite.");
            return;
        }

        await Frames(2);
        if (fill.GetGlobalRect().End.X < prudential.GetGlobalRect().Position.X || fill.GetGlobalRect().End.X > limit.GetGlobalRect().End.X)
        {
            Fail("a barra da folha não desenha a folha entre o prudencial e o limite.");
            return;
        }

        string text = TextOf(hub);
        if (!text.Contains("art. 22", StringComparison.Ordinal) || !text.Contains("art. 23", StringComparison.Ordinal) || !law.VedacoesPrudencial.All(v => text.Contains(v[1..], StringComparison.Ordinal)))
        {
            Fail("a área fiscal não diz o que cada faixa bloqueia, com os artigos.");
            return;
        }

        if (!text.Contains("CF, art. 212", StringComparison.Ordinal) || !text.Contains(Tr("mínimo de"), StringComparison.Ordinal) || !text.Contains("CF, art. 29-A", StringComparison.Ordinal))
        {
            Fail("a área fiscal está sem os mínimos de saúde e educação ou sem o repasse à Câmara.");
            return;
        }

        Save(shot, "_fiscal");
        if (Named<Button>(hub, "RepasseMenos") is not { Disabled: false } less || !ThreePartTip(less))
        {
            Fail("o repasse à Câmara está sem o controle de reduzir.");
            return;
        }

        double share = g.Fiscal.ChamberShare;
        await Click(less);
        if (g.Fiscal.ChamberShare >= share)
        {
            Fail("reduzir o repasse à Câmara não mudou o repasse.");
            return;
        }

        await Press(hub, "FecharResultado");

        // Mesa de negociação: abre na data-base; com a folha no prudencial, conceder o pedido fica vedado.
        NegotiationTable table = game.GovernanceContent.Personnel.Mesas.First(m => m.AfetaFolha);
        int year = game.State!.Calendar.WeekStart.Month >= table.Mes ? game.State.Calendar.WeekStart.Year + 1 : game.State.Calendar.WeekStart.Year;
        game.State.Calendar.JumpTo(new DateOnly(year, table.Mes, 10));
        Clock.ResetDay(game.State);
        g.Payroll.Share = (law.PrudencialExecutivo + law.LimitePessoalExecutivo) / 2;
        await GovWeek(hub);
        g.Payroll.Share = (law.PrudencialExecutivo + law.LimitePessoalExecutivo) / 2;
        g.Payroll.Band = law.Faixa(g.Payroll.Share);
        if (gov.PendingNegotiation is not { } negotiation)
        {
            Fail("a mesa de negociação não abriu no mês da data-base.");
            return;
        }

        await Click(Named<Button>(hub, "Area_fiscal")!);
        await Click(Named<Button>(hub, "Mesa_" + negotiation.Id)!);
        if (hub.GovSheetName != "negociacao")
        {
            Fail("a mesa de negociação não abriu no painel.");
            return;
        }

        Button? concede = Named<Button>(hub, "Responder_" + NegotiationAnswer.Conceder);
        if (!BlockedWithReason(concede) || !concede!.TooltipText.Contains("art. 22", StringComparison.Ordinal) || Named<Control>(hub, "Bloqueio_" + NegotiationAnswer.Conceder) is null)
        {
            Fail("com a folha acima do prudencial, conceder o reajuste deveria estar bloqueado com o art. 22 no motivo.");
            return;
        }

        Save(shot, "_mesa");
        if (Named<Button>(hub, "Responder_" + NegotiationAnswer.RevisaoGeral) is not { Disabled: false } revision || !TextOf(hub).Contains(Tr("Chance de a categoria aceitar"), StringComparison.Ordinal))
        {
            Fail("a revisão geral deveria seguir possível, com a chance de aceite.");
            return;
        }

        await Click(revision);
        if (negotiation.Outcome == NegotiationOutcome.Pendente || Named<Control>(hub, "OQueMudou") is null)
        {
            Fail("responder à mesa não a encerrou ou não mostrou o que mudou.");
            return;
        }

        GD.Print($"Smoke fiscal: ok (folha em {g.Payroll.Share:P1} na faixa {g.Payroll.Band}, marcas na barra, art. 22 no bloqueio, mesa {negotiation.Outcome}, mínimos e repasse)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ indicadores

    /// <summary>
    /// Indicadores em unidade real: o painel no mandato, com valor, unidade, fonte e ano, a variação no mandato, a
    /// comparação com o estado e "por que mudou"; a composição da Câmara; e o mesmo painel na prefeitura e na entressafra.
    /// </summary>
    private async Task PlayIndicators(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("indicadores") || !await TakeOffice(SmallCity, mayor: false))
        {
            return;
        }

        HubScreen hub = await OpenGovHub();
        var real = game.State!.Indicators;
        if (real is null)
        {
            Fail("a cidade do teste não tem indicadores reais no save.");
            return;
        }

        await GovWeek(hub, 30);
        await Click(Named<Button>(hub, "Area_indicadores")!);
        var rows = FindAll<PanelContainer>(hub).Where(b => b.Name.ToString().StartsWith("Indicador_", StringComparison.Ordinal) && !b.IsQueuedForDeletion()).ToList();
        if (rows.Count != real.Indicadores.Count || rows.Count < 8 || rows.Any(r => !ThreePartTip(r) || !r.TooltipText.Contains(Tr("Fonte:"), StringComparison.Ordinal)))
        {
            Fail($"o painel mostra {rows.Count} indicadores de {real.Indicadores.Count}, ou algum sem fonte no tooltip.");
            return;
        }

        Save(shot, "_indicadores");
        var defs = game.IndicatorModel.Definitions;
        string? explained = real.Indicadores.Select(i => i.Id).FirstOrDefault(id => Core.Indicators.IndicatorQueries.YearChange(real, id) is { Fatores.Count: > 0 } || Core.Indicators.IndicatorQueries.PreviousYearChange(real, id) is { Fatores.Count: > 0 });
        if (explained is null)
        {
            Fail("em 30 semanas nenhum indicador teve variação com fatores.");
            return;
        }

        await ClickCard(Named<Control>(hub, "Indicador_" + explained)!);
        var reading = Core.Indicators.IndicatorQueries.Current(real, defs, explained);
        string sheet = TextOf(hub);
        string unit = reading.Unidade.StartsWith("R$", StringComparison.Ordinal) ? reading.Unidade[2..].Trim() : reading.Unidade;
        if (hub.GovSheetName != "indicador" || Named<Control>(hub, "ValorIndicador") is null || !sheet.Contains(unit[1..], StringComparison.Ordinal) || !sheet.Contains(reading.Fonte, StringComparison.Ordinal)
            || !sheet.Contains(Tr("Neste mandato"), StringComparison.Ordinal) || !sheet.Contains(Tr("Comparação com o estado"), StringComparison.Ordinal))
        {
            Fail($"a ficha do indicador {explained} está sem valor, unidade, fonte, variação no mandato ou comparação.");
            return;
        }

        Control? why = Named<Control>(hub, "PorQueMudou") ?? Named<Control>(hub, "PorQueMudouAntes");
        if (why is null || TextOf(why).Split('\n').Count(l => l.StartsWith('·')) == 0)
        {
            Fail("a ficha do indicador não explica por que ele mudou (fatores).");
            return;
        }

        Save(shot, "_indicador");
        if (real.Indicadores.FirstOrDefault(i => i.Estimativa) is { } estimated)
        {
            await Press(hub, "Voltar");
            await ClickCard(Named<Control>(hub, "Indicador_" + estimated.Id)!);
            if (Named<Control>(hub, "Estimativa") is null)
            {
                Fail($"o indicador {estimated.Id} é estimativa e a ficha não marca isso.");
                return;
            }
        }

        // Composição da Câmara: cadeiras, suplentes e idade.
        await Press(hub, "Voltar");
        await Click(Named<Button>(hub, "VerCadeiras")!);
        string seats = TextOf(hub);
        if (hub.GovSheetName != "cadeiras" || !seats.Contains(Tr("Idade média"), StringComparison.Ordinal) || !seats.Contains(Tr("Suplentes"), StringComparison.Ordinal) && !seats.Contains(Tr("Sem suplentes"), StringComparison.Ordinal))
        {
            Fail("a composição da Câmara está sem cadeiras, suplentes ou idade média.");
            return;
        }

        Save(shot, "_cadeiras");

        // Sem mandato, o mesmo painel aparece na Ficha da cidade (área Cidade da entressafra).
        game.JoinParty(game.State.Parties.First(p => p.Id != game.State.Player.PartyId).Id);
        hub.Refresh();
        await Frames(3);
        await Click(Named<Button>(hub, "Area_indicadores")!);
        if (hub.AreaName != "indicadores" || FindAll<PanelContainer>(hub).Count(b => b.Name.ToString().StartsWith("Indicador_", StringComparison.Ordinal) && !b.IsQueuedForDeletion()) != real.Indicadores.Count)
        {
            Fail("fora de mandato, a Ficha da cidade não mostra os indicadores.");
            return;
        }

        GD.Print($"Smoke indicadores: ok ({rows.Count} indicadores em unidade real, {explained} com os fatores da variação, composição da Câmara e Ficha da cidade na entressafra)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ entressafra

    /// <summary>
    /// Entressafra na tela central: caminhada com a zona escolhida no mapa, previsão e resultado no feed; cansaço na
    /// repetição; turnos que acabam com o motivo; visita a liderança; posição no Livro; entrevista com tema; a semana
    /// que devolve os turnos; e a ida à pré-campanha levando a presença.
    /// </summary>
    private async Task PlayOffSeason(string? shot)
    {
        Game game = Game.Instance;
        if (NeedsWindow("entressafra") || !await TakeOffice(SmallCity, mayor: false))
        {
            return;
        }

        game.JoinParty(game.State!.Parties.First(p => p.Id != game.State.Player.PartyId).Id);
        game.ResetNavigationForTest();
        game.GoToCareer();
        if (game.State.Mandate is not null || game.LastNavigation != Game.Scenes.Hub)
        {
            Fail("fora de mandato, a carreira deveria ir para a tela central.");
            return;
        }

        // Só neste teste: a eleição foi garantida com presença máxima; a entressafra começa de um nível comum.
        if (game.State.OffSeason is { } seeded)
        {
            for (int z = 0; z < seeded.Presence.Count; z++)
            {
                seeded.Presence[z] = 18 + (2 * z);
            }
        }

        HubScreen hub = await OpenGovHub();
        var off = game.Entressafra;
        if (off is null || !hub.GovMode || hub.AreaName != "entressafra")
        {
            Fail($"a tela central não abriu na entressafra (área {hub.AreaName}).");
            return;
        }

        foreach (string area in new[] { "liderancas", "posicoes", "indicadores", "partido", "entressafra" })
        {
            await Click(Named<Button>(hub, "Area_" + area)!);
            if (hub.AreaName != area)
            {
                Fail($"a área {area} da entressafra não abriu.");
                return;
            }
        }

        Save(shot, "_entressafra");

        // Caminhada: zona escolhida no mapa, previsão, resultado e o cansaço na terceira igual.
        await Click(Named<Button>(hub, "AbrirCaminhada")!);
        if (!BlockedWithReason(Named<Button>(hub, "Confirmar")))
        {
            Fail("a caminhada sem zona deveria estar bloqueada com o motivo.");
            return;
        }

        string? zone = await ClickZone(hub);
        if (zone is null || hub.GovZone != zone || Named<Control>(hub, "Previsao") is null)
        {
            Fail("o clique no mapa não escolheu a zona da caminhada.");
            return;
        }

        int index = off.S.IndexOf(zone);
        double before = off.S.Presence[index];
        await Click(Named<Button>(hub, "Confirmar")!);
        if (off.S.Presence[index] <= before || hub.GovResultText is null || !FeedTexts(hub).Any(t => t.Contains(Tr("Sua agenda"), StringComparison.Ordinal)))
        {
            Fail("a caminhada não aumentou a presença, não mostrou o que mudou ou não virou cartão no feed.");
            return;
        }

        await Press(hub, "FecharResultado");
        await Click(Named<Button>(hub, "Confirmar")!);
        await Press(hub, "FecharResultado");
        if (!TextOf(Named<Control>(hub, "Previsao")!).Contains(Tr("Fatores:"), StringComparison.Ordinal))
        {
            Fail("a previsão da terceira caminhada igual não mostra o cansaço.");
            return;
        }

        Save(shot, "_caminhada");
        await Click(Named<Button>(hub, "Confirmar")!);
        await Press(hub, "FecharResultado");
        if (off.S.TurnsLeft != 0 || !BlockedWithReason(Named<Button>(hub, "Confirmar")))
        {
            Fail("com os turnos da semana gastos, a caminhada deveria estar bloqueada com o motivo.");
            return;
        }

        // A semana vira pelo relógio: os turnos voltam e a presença cai um pouco.
        double city = off.S.CityPresence();
        await GovWeek(hub);
        if (off.S.TurnsLeft != game.OffSeasonConfig.TurnosPorSemana || off.S.CityPresence() >= city)
        {
            Fail("a virada da semana não devolveu os turnos ou não fez a presença cair.");
            return;
        }

        // Visita a uma liderança conhecida.
        await Click(Named<Button>(hub, "Area_liderancas")!);
        var leader = off.Leaders.FirstOrDefault(l => off.ForecastVisit(l.PersonId).Blocked is null);
        if (leader is null)
        {
            Fail("nenhuma liderança conhecida para visitar na entressafra.");
            return;
        }

        await ClickCard(Named<Control>(hub, "Lideranca_" + leader.PersonId)!);
        Control? visit = Named<Control>(hub, "PrevisaoVisita");
        if (hub.GovSheetName != "visita" || visit is null || !TextOf(visit).Contains(Tr("Quem vai lembrar"), StringComparison.Ordinal))
        {
            Fail("a ficha da liderança não abriu com a previsão da visita e quem vai lembrar.");
            return;
        }

        await Click(Named<Button>(hub, "Confirmar")!);
        if (!game.State.City!.Person(leader.PersonId)!.Memories.Any(m => m.Fact == "visita_entressafra") || !BlockedWithReason(Named<Button>(hub, "Confirmar")))
        {
            Fail("a visita não gravou a lembrança, ou repetir a visita não ficou bloqueado com o motivo.");
            return;
        }

        await Press(hub, "FecharResultado");

        // Posição no Livro e entrevista com o tema.
        await Click(Named<Button>(hub, "Area_posicoes")!);
        string theme = game.World.Parties.Themes.Themes[0].Id;
        int stated = game.State.Ledger.Positions.Count;
        await Click(Named<Button>(hub, "Tema_" + theme)!);
        await Click(Named<Button>(hub, "Valor_4")!);
        await Click(Named<Button>(hub, "Confirmar")!);
        if (off.S.Positions.GetValueOrDefault(theme) != 2 || game.State.Ledger.Positions.Count != stated + 1)
        {
            Fail("assumir a posição não a gravou na entressafra e no Livro de compromissos.");
            return;
        }

        await Press(hub, "FecharResultado");
        await Click(Named<Button>(hub, "Area_entressafra")!);
        await Click(Named<Button>(hub, "AbrirEntrevista")!);
        if (!SelectItem(hub, "Tema", Tr("Falar de")))
        {
            Fail("a entrevista não oferece o tema com posição assumida.");
            return;
        }

        await Frames(2);
        await Click(Named<Button>(hub, "Confirmar")!);
        if (off.S.TurnsLeft != 0 || off.S.Known.GetValueOrDefault(theme) <= game.OffSeasonConfig.ConhecidaPorPosicao)
        {
            Fail("a entrevista com tema não divulgou a posição ou não gastou o turno.");
            return;
        }

        await Press(hub, "FecharResultado");
        Save(shot, "_entrevista");

        // Ir para a pré-campanha: a presença e a posição vão junto.
        await Click(Named<Button>(hub, "Area_entressafra")!);
        await Click(Named<Button>(hub, "PularParaCampanha")!);
        await Frames(3);
        if (game.State.Campaign is not { } campaign || game.State.OffSeason is not null || hub.GovMode)
        {
            Fail("a pré-campanha não abriu a partir da entressafra.");
            return;
        }

        if (campaign.Platform.Positions.GetValueOrDefault(theme) != 2 || campaign.Territory.Presence[RaceSetup.PlayerId].Max() <= 0)
        {
            Fail("a campanha não recebeu a posição e a presença da entressafra.");
            return;
        }

        GD.Print($"Smoke entressafra: ok (caminhada em {zone} com cansaço, turnos, visita, posição no Livro, entrevista e pré-campanha de {campaign.ElectionDate.Year} com a presença levada)");
        GetTree().Quit(0);
    }

    // ------------------------------------------------------------------ o que o layout e o explicacao percorrem

    /// <summary>
    /// Passa por todas as áreas e fichas do mandato de vereador, chamando <paramref name="check"/> em cada uma
    /// (o segundo argumento é o sufixo da captura, quando a tela merece uma).
    /// </summary>
    private async Task VisitCouncilPanels(HubScreen hub, Func<string, string?, Task> check)
    {
        Game game = Game.Instance;
        GovernanceEngine gov = game.Governo!;
        GovernanceState g = gov.G;
        game.State!.Player.Resources.PoliticalCapital = 60;
        foreach (var (area, suffix) in new[] { ("mandato", "mandato"), ("gabinete", "gabinete"), ("instrumentos", "instrumentos"), ("mesa", "mesa"), ("base", "base"), ("indicadores", "indicadores"), ("partido", null as string) })
        {
            await Press(hub, "Area_" + area);
            await check("mandato, área " + area, suffix);
        }

        foreach (string mode in new[] { "obras", "aprovacao", "renda", "situacao", "base" })
        {
            await Press(hub, "Modo_" + mode);
            await check("mandato, mapa " + mode, mode is "obras" or "base" ? "mapa_" + mode : null);
        }

        await Press(hub, "Area_gabinete");
        AideView candidate = GovernanceQueries.Cabinet(gov, candidates: true)[0];
        Named<Control>(hub, "Assessor_" + candidate.Aide.PersonId)!.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        await Frames(3);
        await check("ficha de candidato ao gabinete", "assessor");
        await Press(hub, "Funcao_" + AideRole.Base);
        await check("assessor de base sem zona", null);
        await Press(hub, "Funcao_" + AideRole.Chefe);
        await Press(hub, "Nomear");
        await check("assessor nomeado, com o que mudou", null);
        await Press(hub, "FecharResultado");
        await Press(hub, "Demitir");
        await check("confirmação de demissão do assessor", null);

        string zone = g.Zones[g.BaseZones[0]];
        foreach (InstrumentDefinition def in game.GovernanceContent.Instruments.Instrumentos)
        {
            await Press(hub, "Area_instrumentos");
            await Press(hub, "Instrumento_" + def.Id);
            await check("instrumento " + def.Id, def.Id == InstrumentDefinition.Fiscalizacao ? "fiscalizacao" : null);
            if (def.Id is InstrumentDefinition.Indicacao or InstrumentDefinition.Emenda)
            {
                var type = game.GovernanceContent.Works.Tipos.First(t => (def.Id == InstrumentDefinition.Emenda ? t.Emenda : t.Pequena || t.Servico) && gov.Fits(t, zone));
                SelectItem(hub, "Tipo", type.Nome);
                await Frames(2);
                hub.ZoneMap.Select(zone);
                hub.ZoneMap.EmitZoneClickForTest(zone);
                await Frames(3);
                await check($"instrumento {def.Id} com zona", def.Id == InstrumentDefinition.Emenda ? "emenda" : null);
                if (def.Id == InstrumentDefinition.Emenda)
                {
                    await Press(hub, "Confirmar");
                    await check("emenda destinada, com o que mudou", null);
                    await Press(hub, "FecharResultado");
                }
            }
        }

        // Achado de fiscalização com as três saídas.
        Work target = g.Works.First(w => w.Open);
        g.Inspections.Add(new Inspection { Id = "fisc_teste", WorkId = target.Id, Date = game.State.Calendar.WeekStart, Finding = Inspection.Irregular, DecisionDeadline = game.State.Calendar.WeekStart.AddDays(28) });
        target.Revealed = true;
        target.Irregular = true;
        target.Overprice = 0.18;
        hub.OpenGovArea("instrumentos");
        await Frames(2);
        await Press(hub, "Achado_fisc_teste");
        await check("achado de fiscalização com as três saídas", "achado");
        await Press(hub, "Area_mesa");
        await check("Mesa e CPI com irregularidade conhecida", "cpi");
        await Press(hub, "VerCadeiras");
        await check("composição da Câmara", "cadeiras");

        await Press(hub, "Area_instrumentos");
        await Press(hub, "Obra_" + target.Id);
        hub.OpenGovArea("base");
        await Frames(2);
        await Press(hub, "Zona_" + zone);
        await check("ficha da zona no mandato", "zona_mandato");
        if (g.Works.FirstOrDefault(w => w.ZoneId == zone) is { } here)
        {
            await Press(hub, "Obra_" + here.Id);
            await check("ficha de obra vista pelo vereador", "obra_vereador");
        }

        await Press(hub, "Area_indicadores");
        Named<Control>(hub, "Indicador_" + Core.Indicators.IndicatorDefinitions.MortalidadeInfantil)?.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
        await Frames(3);
        await check("ficha de indicador", "indicador");

        // Contagem nominal, se houver projeto no plenário.
        if (GovernanceQueries.Agenda(gov).FirstOrDefault().Bill is { } bill)
        {
            await Press(hub, "Area_mandato");
            await Press(hub, "Contagem_" + bill.Id);
            await check("contagem nominal", "contagem");
            if (FindAll<Button>(hub).FirstOrDefault(b => b.Name.ToString().StartsWith("Negociar_", StringComparison.Ordinal) && !b.Disabled) is { } negotiate)
            {
                negotiate.EmitSignal(BaseButton.SignalName.Pressed);
                await Frames(3);
                await check("negociação de voto com a previsão", "acordo");
            }
        }

        // Evento de mandato e cobrança do Livro no cartão sobre o mapa.
        TermEventDefinition definition = game.GovernanceContent.Events.Eventos.First(e => e.Papeis.Contains(GovernanceContent.PapelVereador));
        g.Decision = new TermDecision
        {
            Id = "dec_teste", EventId = definition.Id, Date = game.State.Calendar.WeekStart, Deadline = game.State.Calendar.WeekStart.AddDays(14), ZoneId = zone,
            PersonId = State(game).City!.Leaders.FirstOrDefault()?.PersonId, Title = FillForTest(definition.Titulo, gov, zone), Text = FillForTest(definition.Texto, gov, zone),
        };
        hub.Refresh();
        await Frames(3);
        await check("evento de mandato no cartão", "evento_mandato");
        g.Decision = null;
        if (game.State.Ledger.Open.FirstOrDefault() is { } promise)
        {
            g.Demands.Add(new Demand { Id = "cob_teste", Kind = Demand.Promessa, RefId = promise.Id, PersonId = promise.ToKind == Core.People.Promise.ParaPessoa ? promise.ToId : null, ZoneId = promise.ZoneId, Date = game.State.Calendar.WeekStart, Deadline = gov.DeadlineOf(promise), Text = $"Cobrança: {promise.Text}" });
            hub.ShowDemandForTest("cob_teste");
            await Frames(3);
            await check("cobrança do Livro no cartão", "cobranca");
            await Press(hub, "CobrancaDepois");
        }

        hub.Refresh();
        await Frames(2);
    }

    private static GameState State(Game game) => game.State!;

    /// <summary>Preenche as variáveis do texto de um evento de mandato montado à mão pelo teste (no jogo, o motor preenche).</summary>
    private static string FillForTest(string text, GovernanceEngine gov, string zone)
    {
        string person = gov.NameOf(gov.Context.State.City!.Leaders.FirstOrDefault()?.PersonId);
        Work? work = gov.G.Works.FirstOrDefault(w => w.Open);
        text = text.Replace("{pessoa}", person, StringComparison.Ordinal).Replace("{zona}", gov.ZoneName(zone), StringComparison.Ordinal)
            .Replace("{obra}", work is null ? "a obra" : gov.WorkName(work), StringComparison.Ordinal).Replace("{papel}", "liderança da zona", StringComparison.Ordinal)
            .Replace("{prefeito}", gov.MayorName, StringComparison.Ordinal);
        return System.Text.RegularExpressions.Regex.Replace(text, @"\{[a-z_]+\}", "isso");
    }

    /// <summary>Passa por todas as áreas e fichas da prefeitura, chamando <paramref name="check"/> em cada uma.</summary>
    private async Task VisitMayorPanels(HubScreen hub, Func<string, string?, Task> check)
    {
        Game game = Game.Instance;
        GovernanceEngine gov = game.Governo!;
        GovernanceState g = gov.G;
        FiscalLawConfig law = gov.Context.FiscalLaw;
        g.Payroll.Share = (law.PrudencialExecutivo + law.LimitePessoalExecutivo) / 2;
        g.Payroll.Band = law.Faixa(g.Payroll.Share);
        NegotiationTable table = game.GovernanceContent.Personnel.Mesas.First(m => m.AfetaFolha);
        if (gov.PendingNegotiation is null)
        {
            g.Negotiations.Add(new Negotiation { Id = "neg_teste", TableId = table.Id, Date = game.State!.Calendar.WeekStart, Deadline = game.State.Calendar.WeekStart.AddDays(21), Asked = 0.08, GeneralRevision = 0.03 });
        }

        var workType = game.GovernanceContent.Works.Tipos.First(t => t.Emenda);
        if (!g.Agreements.Any(a => a.State == AgreementState.Oferta))
        {
            g.Agreements.Add(new Agreement
            {
                Id = "conv_teste", OriginId = game.GovernanceContent.Agreements.Origens[0].Id, TypeId = workType.Id, Value = gov.CostOf(workType) * 0.9m, Counterpart = 0.1,
                Offered = game.State!.Calendar.WeekStart, OfferExpires = game.State.Calendar.WeekStart.AddDays(56), Weeks = 60,
            });
        }

        g.Crises.Add(new CrisisRecord
        {
            Id = "crise_layout", CrisisId = "enchente", Date = game.State!.Calendar.WeekStart, ZoneId = g.Zones[0], CauseKind = CrisisRecord.CausaZonaSemDrenagem, CauseRef = g.Zones[0],
            CauseText = "A zona não tem drenagem entregue", Presented = true, Resolved = true,
        });
        hub.Refresh();
        await Frames(2);
        foreach (var (area, suffix) in new[] { ("governo", "governo"), ("secretariado", "secretariado"), ("obras", "obras"), ("fiscal", "fiscal"), ("convenios", "convenios"), ("camara", "camara"), ("orcamento", "orcamento"), ("indicadores", null as string), ("partido", null) })
        {
            await Press(hub, "Area_" + area);
            await check("prefeitura, área " + area, suffix);
        }

        foreach (string mode in new[] { "aprovacao", "renda", "situacao", "obras" })
        {
            await Press(hub, "Modo_" + mode);
            await check("prefeitura, mapa " + mode, mode == "obras" ? "mapa_obras_prefeitura" : null);
        }

        await Press(hub, "Area_secretariado");
        var pastas = game.GovernanceContent.Secretaries.Secretarias;
        await Press(hub, "Pasta_" + pastas.First(p => g.SecretaryOf(p.Id) is not null).Id);
        await check("pasta com titular", "pasta");
        await Press(hub, "DemitirSecretario");
        await check("confirmação de demissão do secretário", null);
        await Press(hub, "DemitirSecretario");
        await Press(hub, "FecharResultado");
        await check("pasta vaga com os nomes oferecidos", "pasta_vaga");

        string zone = g.Zones[0];
        await Press(hub, "Area_obras");
        await Press(hub, "IniciarObra");
        await check("início de obra sem zona", null);
        var type = game.GovernanceContent.Works.Tipos.Where(t => gov.Fits(t, zone) && gov.CostOf(t) <= gov.InvestmentLeft).OrderByDescending(t => gov.CostOf(t)).First();
        SelectItem(hub, "Tipo", type.Nome);
        await Frames(2);
        hub.ZoneMap.Select(zone);
        hub.ZoneMap.EmitZoneClickForTest(zone);
        await Frames(3);
        await check("início de obra com zona", "nova_obra");
        await Press(hub, "Confirmar");
        await Press(hub, "FecharResultado");
        Work? work = g.Works.LastOrDefault(w => w.SponsorId == Work.Prefeito && w.ZoneId == zone);
        if (work is not null)
        {
            await check("ficha de obra em projeto", null);
            work.Stage = WorkStage.Modalidade;
            hub.Refresh();
            await Frames(3);
            await check("escolha da modalidade de licitação", "modalidade");
            work.Stage = WorkStage.Pronta;
            hub.Refresh();
            await Frames(3);
            await check("obra pronta para inaugurar", "inaugurar");
        }

        await Press(hub, "Area_fiscal");
        if (gov.PendingNegotiation is { } negotiation)
        {
            await Press(hub, "Mesa_" + negotiation.Id);
            await check("mesa de negociação", "mesa_negociacao");
        }

        await Press(hub, "Area_convenios");
        if (g.Agreements.FirstOrDefault(a => a.State == AgreementState.Oferta) is { } offer)
        {
            await Press(hub, "Convenio_" + offer.Id);
            await check("convênio oferecido", "convenio");
            hub.ZoneMap.Select(zone);
            hub.ZoneMap.EmitZoneClickForTest(zone);
            await Frames(3);
            await check("convênio com zona", null);
        }

        await Press(hub, "Area_camara");
        var law2 = game.ExecutiveContent.Policies.Policies.First(p => p.RequiresLaw && gov.CountPolicy(p.Id) is not null);
        await Press(hub, "Contagem_" + law2.Id);
        await check("contagem nominal de política", "contagem_politica");
        if (FindAll<Button>(hub).FirstOrDefault(b => b.Name.ToString().StartsWith("Negociar_", StringComparison.Ordinal) && !b.Disabled) is { } negotiate)
        {
            negotiate.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(3);
            await check("acordo de voto do prefeito", null);
        }

        await Press(hub, "Area_orcamento");
        await Press(hub, "Politica_" + law2.Id);
        await check("ficha de política que precisa de lei", "politica");
        await Press(hub, "Area_governo");
        hub.OpenGovArea("governo");
        await Frames(2);
        await Press(hub, "Atencao_promessas");
        await check("Livro de compromissos da prefeitura", null);

        game.State.Executive!.Crisis = new Core.Executive.PendingCrisis("enchente", game.State.Calendar.WeekStart);
        g.Crises.Add(new CrisisRecord
        {
            Id = "crise_cartao", CrisisId = "enchente", Date = game.State.Calendar.WeekStart, ZoneId = zone, CauseKind = CrisisRecord.CausaZonaSemDrenagem, CauseRef = zone,
            CauseText = "A zona não tem drenagem entregue", Presented = true,
        });
        hub.Refresh();
        await Frames(3);
        await check("crise no cartão, com a causa", "crise");
        game.Prefeitura!.ResolveCrisis(0);
        hub.Refresh();
        await Frames(2);
    }

    /// <summary>Passa pelas áreas e fichas da entressafra.</summary>
    private async Task VisitOffSeasonPanels(HubScreen hub, Func<string, string?, Task> check)
    {
        Game game = Game.Instance;
        var off = game.Entressafra!;
        foreach (var (area, suffix) in new[] { ("entressafra", "entressafra"), ("liderancas", "liderancas"), ("posicoes", "posicoes"), ("indicadores", "cidade"), ("partido", null as string) })
        {
            await Press(hub, "Area_" + area);
            await check("entressafra, área " + area, suffix);
        }

        string zone = off.S.Zones[0];
        await Press(hub, "Area_entressafra");
        await Press(hub, "AbrirCaminhada");
        await check("caminhada sem zona", null);
        hub.ZoneMap.Select(zone);
        hub.ZoneMap.EmitZoneClickForTest(zone);
        await Frames(3);
        await check("caminhada com zona", "caminhada");
        await Press(hub, "Confirmar");
        await check("caminhada feita, com o que mudou", null);
        await Press(hub, "FecharResultado");
        await Press(hub, "Voltar");
        await Press(hub, "AbrirEntrevista");
        await check("entrevista", null);
        await Press(hub, "Area_posicoes");
        await Press(hub, "Tema_" + game.World.Parties.Themes.Themes[0].Id);
        await check("tema da entressafra", "posicao");
        await Press(hub, "Area_liderancas");
        if (off.Leaders.FirstOrDefault() is { } leader)
        {
            Named<Control>(hub, "Lideranca_" + leader.PersonId)!.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false });
            await Frames(3);
            await check("visita a liderança", "visita");
        }

        await Press(hub, "Area_entressafra");
        await Press(hub, "Zona_" + zone);
        await check("ficha da zona na entressafra", null);
    }

    /// <summary>Apaga os saves de teste da R4, para cada execução rejogar a campanha com o código de agora.</summary>
    private static void ClearGovSaves()
    {
        foreach (int city in new[] { SmallCity, MediumCity, Metropolis })
        {
            foreach (string role in new[] { "prefeito", "vereador" })
            {
                string path = ProjectSettings.GlobalizePath(SaveManager.PathOf($"smoke_r4_{role}_{city}"));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    /// <summary>
    /// Para o `layout` e o `explicacao`: prepara vereador, prefeito e entressafra numa cidade e percorre tudo. Os saves
    /// de teste evitam rejogar a campanha em cada resolução.
    /// </summary>
    private async Task<bool> VisitGovScreens(int city, string label, Func<HubScreen, string, string?, Task> check)
    {
        Game game = Game.Instance;
        foreach (bool mayor in new[] { false, true })
        {
            string slot = $"smoke_r4_{(mayor ? "prefeito" : "vereador")}_{city}";
            if (!SaveManager.Instance.Exists(slot) || SaveManager.Instance.Load(slot) is not null || game.Governo is null)
            {
                if (!await TakeOffice(city, mayor))
                {
                    return false;
                }

                // Algumas semanas de governo, para haver obras, notícias e pauta.
                for (int week = 0; week < 10; week++)
                {
                    if (game.State!.Executive?.Crisis is not null)
                    {
                        game.Prefeitura!.ResolveCrisis(0);
                    }

                    if (game.Governo!.G.Decision is not null && GovernanceQueries.Decision(game.Governo) is { } pending)
                    {
                        game.Governo.Decide(pending.Default.Id);
                    }

                    game.AdvanceWeek();
                }

                SaveManager.Instance.Save(slot);
            }

            game.State!.Time.Day = 0;

            // Só para as capturas: a eleição foi garantida com presença máxima; o mandato começa de níveis comuns por
            // zona. E nada de decisão pendente cobrindo o mapa logo na abertura.
            GovernanceState started = game.Governo!.G;
            for (int z = 0; z < started.Presence.Count; z++)
            {
                started.Presence[z] = 12 + ((z * 37) % 55);
            }

            if (started.Decision is not null && GovernanceQueries.Decision(game.Governo) is { } waiting)
            {
                game.Governo.Decide(waiting.Default.Id);
            }

            if (game.State.Executive?.Crisis is not null)
            {
                game.Prefeitura!.ResolveCrisis(0);
            }

            HubScreen hub = await OpenGovHub();
            if (mayor)
            {
                await VisitMayorPanels(hub, (where, suffix) => check(hub, $"{where}, {label}", suffix));
            }
            else
            {
                await VisitCouncilPanels(hub, (where, suffix) => check(hub, $"{where}, {label}", suffix));
                game.JoinParty(game.State.Parties.First(p => p.Id != game.State.Player.PartyId).Id);
                hub.Refresh();
                await Frames(3);
                if (game.Entressafra is null)
                {
                    Fail("perder o mandato não abriu a entressafra.");
                    return false;
                }

                await VisitOffSeasonPanels(hub, (where, suffix) => check(hub, $"{where}, {label}", suffix));
            }

            RemoveChild(hub);
            hub.QueueFree();
            await Frames(2);
        }

        return true;
    }
}
