using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Governance;
using Planalto.Core.Ideology;
using Planalto.Core.State;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Entressafra na tela central: sem cargo e sem eleição aberta, o jogador usa os turnos da semana para caminhar nas
/// zonas, visitar lideranças, dar entrevista e assumir posições, e pode ir direto para a pré-campanha seguinte. Não há
/// verba de canal, equipe paga nem pedido de voto. Só lê o Core e chama os comandos dele.
/// </summary>
public partial class HubScreen
{
    private int? _govValue;
    private string? _govTheme;

    private void BuildOffAgenda()
    {
        if (Off is not { } off)
        {
            return;
        }

        int year = CareerFlow.NextEligibleElectionYear(State);
        DateOnly opens = CareerFlow.CampaignOpens(year);
        Glossary.Rich(_left, string.Format(Tr("Você está sem cargo e sem eleição aberta. Turnos desta semana: {0} de {1}. A pré-campanha de {2} abre em {3}."),
            off.S.TurnsLeft, _game.OffSeasonConfig.TurnosPorSemana, year, opens.ToString("dd/MM/yyyy", PtBr)), 18, Palette.Texto);
        if (year != CareerFlow.NextMunicipalElectionYear(State))
        {
            Glossary.Rich(_left, string.Format(Tr("Você ainda não terá 18 anos no prazo do registro da eleição de {0}; a primeira que pode disputar é a de {1}."), CareerFlow.NextMunicipalElectionYear(State), year), 18, Palette.Texto);
        }

        if (!PartyAffiliation.CanRun(State, year))
        {
            PanelContainer warn = Card(_left, Palette.CartaoAlto, Palette.Linha, 10);
            warn.Name = "SemPartido";
            Glossary.Rich(warn, Tr("Do jeito que está, você não pode disputar: falta filiação a um partido registrado dentro do prazo. Veja a área Partido."), 18, Palette.Texto);
        }

        Heading(_left, Tr("Agenda fora de campanha"), 20);
        var walk = Ui.Button(_left, "Caminhar numa zona", () => OpenGov("caminhada", null, _govZone),
            Palette.Tip(Tr("Abre a caminhada: você escolhe a zona no mapa."), Tr("a caminhada aumenta a sua presença na zona e um pouco nas vizinhas, e pode revelar uma liderança de lá; a previsão aparece antes de confirmar."), Tr("nada para abrir; a caminhada gasta um turno.")));
        walk.Name = "AbrirCaminhada";
        Palette.Primary(walk);
        var interview = Ui.Button(_left, "Dar uma entrevista", () => { _govTheme = null; OpenGov("entrevista", null); },
            Palette.Tip(Tr("Abre a entrevista na imprensa local."), Tr("rende um pouco de presença na cidade inteira e, com um tema, divulga a posição que você já assumiu nele."), Tr("nada para abrir; a entrevista gasta um turno.")));
        interview.Name = "AbrirEntrevista";
        Ui.Button(_left, "Visitar uma liderança", () => OpenGovArea("liderancas"),
            Palette.Tip(Tr("Abre as lideranças que você conhece."), Tr("a visita melhora a relação e fica na memória dela."), Tr("nada para abrir; a visita gasta um turno."))).Name = "AbrirLiderancas";
        Ui.Button(_left, "Assumir uma posição", () => OpenGovArea("posicoes"),
            Palette.Tip(Tr("Abre os temas da plataforma."), Tr("a posição assumida vai para o Livro de compromissos e para a sua próxima campanha."), Tr("nada para abrir; assumir posição gasta um turno."))).Name = "AbrirPosicoes";

        Heading(_left, Tr("Sua presença por zona"), 20);
        foreach (var (id, index) in off.S.Zones.Select((id, i) => (id, i)).OrderByDescending(x => off.S.Presence[x.i]))
        {
            string zone = id;
            GovRow(_left, string.Format(PtBr, "{0}\n{1} {2:0.0}", GZone(id), Tr("presença"), off.S.Presence[index]), () => { _map.Select(zone); OpenGov("zona", zone, zone); },
                Palette.Tip(string.Format(Tr("Zona {0}."), GZone(id)), Tr("abre a ficha da zona e a destaca no mapa."), Tr("nada.")), "Zona_" + id);
        }

        Glossary.Rich(_left, string.Format(Tr("Sem agenda, a presença cai {0} por semana em todas as zonas."), Pct(_game.OffSeasonConfig.DecaimentoSemanal, "0.#")), 18, Palette.Texto3);

        Heading(_left, Tr("Pular a entressafra"), 20);
        string skipTip = Palette.Tip(
            string.Format(Tr("Avança o tempo até a abertura da pré-campanha de {0}."), year),
            Tr("as semanas passam sem a sua agenda: a presença cai, a cidade muda e a fundação de partido em curso segue andando."),
            Tr("a presença que você deixa de manter até lá."));
        var skip = Ui.Button(_left, string.Format(Tr("Ir para a pré-campanha de {0}"), year), () =>
        {
            Game.WeekNote note = _game.SkipToNextElection();
            SaveManager.Instance.Autosave();
            _log.Insert(0, note switch
            {
                Game.WeekNote.CampanhaAberta => Tr("Abriu a pré-campanha da próxima eleição."),
                Game.WeekNote.SemPartidoNoPrazo => Tr("Passou o prazo de filiação sem partido válido: você não disputa esta eleição."),
                Game.WeekNote.SemIdade => Tr("Você ainda não tem idade para disputar esta eleição."),
                _ => Tr("O tempo avançou."),
            });
            if (State.Campaign is not null)
            {
                _game.GoToCareer();
            }

            Refresh();
        }, skipTip);
        skip.Name = "PularParaCampanha";
        skip.ClipText = true;
    }

    private void BuildOffWalkSheet()
    {
        OffSeasonEngine off = Off!;
        GovBackButton();
        NameTitle(_left, Tr("Caminhada"), 22);
        Glossary.Rich(_left, string.Format(Tr("Andar pela zona, conversar com quem mora lá. Turnos desta semana: {0}."), off.S.TurnsLeft), 18, Palette.Texto);
        GovZonePicker(OffSeasonEngine.Caminhada);
        GovForecast forecast = _govZone is null ? GovForecast.Block(GovFailure.AlvoInvalido, Tr("escolha a zona clicando nela no mapa")) : off.ForecastWalk(_govZone);
        GovForecastCard(_left, forecast);
        string? zone = _govZone;
        GovCommand(_left, "Caminhar", Tr("Faz a caminhada na zona escolhida."), forecast, () => off.Walk(zone!), "Confirmar", primary: true);
    }

    private void BuildOffInterviewSheet()
    {
        OffSeasonEngine off = Off!;
        _govPick = null;
        GovBackButton();
        NameTitle(_left, Tr("Entrevista"), 22);
        Glossary.Rich(_left, string.Format(Tr("Uma conversa na imprensa local, fora de época de eleição. Turnos desta semana: {0}."), off.S.TurnsLeft), 18, Palette.Texto);
        var themes = new List<(string Id, string Text)> { ("", Tr("Sem tema: só aparecer")) };
        themes.AddRange(off.Context.Themes.Where(t => off.S.Positions.ContainsKey(t.Key)).OrderBy(t => t.Value, StringComparer.Ordinal).Select(t => (t.Key, string.Format(Tr("Falar de {0}"), LowerFirst(t.Value)))));
        FieldLabel("Assunto");
        OptionButton choice = Ui.Options(_left, themes, _govTheme ?? "", id => { _govTheme = id.Length == 0 ? null : id; Refresh(); }, translate: false);
        choice.Name = "Tema";
        choice.ClipText = true;
        choice.TooltipText = Palette.Tip(Tr("Assunto da entrevista."), Tr("com um tema, mais gente passa a conhecer a posição que você assumiu nele; só entram na lista os temas em que você já tem posição."), Tr("nada para escolher."));
        if (themes.Count == 1)
        {
            Glossary.Rich(_left, Tr("Você ainda não assumiu posição em nenhum tema: a entrevista só rende presença. Assuma posições na área Posições."));
        }

        GovForecast forecast = off.ForecastInterview(_govTheme);
        GovForecastCard(_left, forecast);
        string? theme = _govTheme;
        GovCommand(_left, "Dar a entrevista", Tr("Dá a entrevista com o assunto escolhido."), forecast, () => off.Interview(theme), "Confirmar", primary: true);
    }

    private void BuildOffLeaders()
    {
        if (Off is not { } off)
        {
            return;
        }

        var leaders = off.Leaders.OrderByDescending(l => off.RelationOf(l.PersonId)).ToList();
        int unknown = (State.City?.Leaders.Count(l => !l.Known) ?? 0);
        Glossary.Rich(_left, string.Format(Tr("Turnos desta semana: {0}. Cada visita gasta um turno; a mesma liderança só rende de novo depois de {1} semanas."), off.S.TurnsLeft, _game.OffSeasonConfig.VisitaIntervaloSemanas), 18, Palette.Texto);
        if (leaders.Count == 0)
        {
            Glossary.Rich(_left, Tr("Você ainda não conhece nenhuma liderança. Uma caminhada numa zona pode revelar quem atua ali."));
        }

        foreach (Leader leader in leaders)
        {
            GovLeaderRow(leader);
        }

        if (unknown > 0)
        {
            Glossary.Rich(_left, string.Format(Tr(unknown == 1 ? "? · {0} liderança da cidade que você ainda não conhece." : "? · {0} lideranças da cidade que você ainda não conhece."), unknown), 18, Palette.Texto3);
        }
    }

    private void BuildOffVisitSheet(string personId)
    {
        OffSeasonEngine off = Off!;
        _govPick = null;
        GovBackButton();
        GovPersonHeader(personId);
        if (State.City?.Leader(personId) is { } leader)
        {
            string theme = off.ThemeName(leader.Theme);
            Glossary.Rich(_left, string.Format(Tr("Tema que mais importa para ela: {0}."), LowerFirst(theme)));
            _map.Select(leader.Zones[0]);
        }

        GovMemories(personId);
        GovForecast forecast = off.ForecastVisit(personId);
        GovForecastCard(_left, forecast, "PrevisaoVisita", Tr("Previsão de visitar"));
        GovCommand(_left, "Visitar", string.Format(Tr("Visita {0} fora de época de eleição."), GovName(personId)), forecast, () => off.Visit(personId), "Confirmar", primary: true);
    }

    private static string OffPosition(int value) => value > 0 ? "+" + value.ToString(PtBr) : value < 0 ? "−" + Math.Abs(value).ToString(PtBr) : "0";

    private void BuildOffPositions()
    {
        if (Off is not { } off)
        {
            return;
        }

        Glossary.Rich(_left, string.Format(Tr("Turnos desta semana: {0}. Em cada tema a posição vai de −2 a +2. Sem posição assumida, o eleitor presume a do seu partido."), off.S.TurnsLeft), 18, Palette.Texto);
        foreach (ThemeDefinition theme in _game.World.Parties.Themes.Themes)
        {
            string id = theme.Id;
            string status = off.S.Positions.TryGetValue(theme.Id, out int value)
                ? string.Format(PtBr, Tr("posição {0} · conhecida por {1:P0} da cidade"), OffPosition(value), off.S.Known.GetValueOrDefault(theme.Id))
                : Tr("sem posição assumida");
            GovRow(_left, $"{theme.Name}\n{status}", () => { _govValue = null; OpenGov("posicao", id); },
                Palette.Tip(string.Format(Tr("Tema {0}."), theme.Name), Tr("abre o tema: você escolhe a posição e vê o que fica registrado antes de confirmar."), Tr("nada para abrir; assumir posição gasta um turno.")), "Tema_" + theme.Id);
        }

        var stated = State.Ledger.Positions.AsEnumerable().Reverse().Take(4).ToList();
        if (stated.Count > 0)
        {
            Heading(_left, Tr("No Livro de compromissos"), 20);
            foreach (var p in stated)
            {
                Glossary.Rich(_left, string.Format(Tr("· {0}, {1}: {2} ({3})."), off.ThemeName(p.Theme), p.Date.ToString("MM/yyyy", PtBr), OffPosition(p.Value), p.Where));
            }
        }
    }

    private void BuildOffPositionSheet(string themeId)
    {
        OffSeasonEngine off = Off!;
        _govPick = null;
        ThemeDefinition? theme = _game.World.Parties.Themes.Themes.FirstOrDefault(t => t.Id == themeId);
        GovBackButton();
        if (theme is null)
        {
            return;
        }

        NameTitle(_left, theme.Name, 22);
        bool has = off.S.Positions.TryGetValue(themeId, out int current);
        Glossary.Rich(_left, has ? string.Format(PtBr, Tr("Sua posição hoje: {0}, conhecida por {1:P0} da cidade."), OffPosition(current), off.S.Known.GetValueOrDefault(themeId)) : Tr("Você ainda não assumiu posição neste tema."), 18, Palette.Texto);
        FieldLabel("Posição (de −2 a +2)");
        HBoxContainer row = Ui.Row(_left, 6);
        _govValue ??= has ? current : 0;
        for (int v = -2; v <= 2; v++)
        {
            int value = v;
            var b = Ui.Button(row, OffPosition(v), () => { _govValue = value; Refresh(); },
                Palette.Tip(string.Format(Tr("Posição {0} em {1}."), OffPosition(v), LowerFirst(theme.Name)), Tr("escolhe o valor; só vale depois de confirmar. −2 e +2 são as posições mais firmes de cada lado do tema; 0 é o meio."), Tr("nada para escolher.")));
            b.Name = "Valor_" + (v + 2);
            b.CustomMinimumSize = new Vector2(56, 44);
            b.ToggleMode = true;
            b.ButtonPressed = _govValue == v;
            if (_govValue == v)
            {
                Palette.Selected(b);
            }
        }

        GovForecast forecast = off.ForecastPosition(themeId, _govValue.Value);
        GovForecastCard(_left, forecast);
        int chosen = _govValue.Value;
        GovCommand(_left, "Assumir a posição", string.Format(Tr("Declara em público a sua posição sobre {0}."), LowerFirst(theme.Name)), forecast, () => off.TakePosition(themeId, chosen), "Confirmar", primary: true);
    }
}
