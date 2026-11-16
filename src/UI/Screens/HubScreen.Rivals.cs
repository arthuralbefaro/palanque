using Godot;
using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.People;
using Planalto.Core.State;
using Planalto.UI.Portrait;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;

namespace Planalto.UI.Screens;

/// <summary>
/// Rivais (GDD 17.11): a ficha do rival (perfil e plano com a confiança da leitura, redutos, verba vista, ataques
/// trocados, pacto, lembranças), o painel da chapa, atacar com a previsão antes, investigar, propor pacto e responder
/// a um ataque dentro do prazo. Só lê o Core e chama os comandos dele.
/// </summary>
public partial class HubScreen
{
    private string _attackType = AttackType.Tema;
    private string _attackChannel = AttackChannel.Redes;
    private string? _attackProof;
    private string? _attackTheme;
    private string? _attackOutlet;
    private (string Title, List<string> Lines)? _changes;

    private RivalCatalog RivalContent => _engine.Context.Campaign.Rivals;

    private AttackCatalog AttackContent => _engine.Context.Campaign.Attacks;

    /// <summary>Área do trilho a que cada ficha pertence (o trilho continua marcando a área).</summary>
    private static string? SheetArea(Sheet sheet) => sheet switch
    {
        Sheet.Equipe or Sheet.Membro or Sheet.Verba or Sheet.Canal or Sheet.Plataforma or Sheet.Tema or Sheet.Prometer => "campanha",
        Sheet.Rival or Sheet.Atacar or Sheet.Chapa or Sheet.Gente or Sheet.Pessoa => "pessoas",
        Sheet.Veiculo or Sheet.Ataque => "midia",
        Sheet.Contas or Sheet.Oferta => "financas",
        _ => null,
    };

    /// <summary>Abre uma ficha marcando no trilho a área a que ela pertence; "Voltar" retorna ao que estava aberto.</summary>
    private void OpenAreaSheet(Sheet sheet, string? id)
    {
        _area = SheetArea(sheet) ?? _area;
        OpenSheet(sheet, id);
    }

    /// <summary>Abre a ficha certa para qualquer pessoa do mundo: liderança, equipe, rival, doador, jornalista, dirigente.</summary>
    private void OpenPerson(string id)
    {
        if (_engine.City.Leader(id) is not null)
        {
            OpenAreaSheet(Sheet.Pessoa, id);
        }
        else if (_engine.Staff(id) is not null)
        {
            OpenAreaSheet(Sheet.Membro, id);
        }
        else if (_engine.RivalSheetOf(id) is not null)
        {
            OpenAreaSheet(Sheet.Rival, id);
        }
        else if (_engine.City.Person(id) is not null)
        {
            OpenAreaSheet(Sheet.Gente, id);
        }
    }

    /// <summary>Alguma ficha existe para essa pessoa (o nome vira link).</summary>
    private bool HasPersonSheet(string id) =>
        _engine.City.Leader(id) is not null || _engine.Staff(id) is not null || _engine.RivalSheetOf(id) is not null || _engine.City.Person(id) is not null;

    /// <summary>Fecha a ficha de um comando e mostra, na que estava antes, a lista "O que mudou".</summary>
    private void Done(string title, List<string> lines)
    {
        Back();
        _changes = (title, lines);
        Refresh();
    }

    /// <summary>Lista "O que mudou" do último comando (princípio f), até o jogador fechar ou trocar de painel.</summary>
    private void ChangesCard()
    {
        if (_changes is not { } changes)
        {
            return;
        }

        PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Acao);
        card.Name = "OQueMudou";
        var col = Ui.Column(card, 6);
        Heading(col, Tr("O que mudou"), 20);
        Glossary.Rich(col, changes.Title, 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
        foreach (string line in changes.Lines.Where(l => l.Length > 0))
        {
            Glossary.Rich(col, "· " + line, 18, Palette.Texto);
        }

        Ui.Button(col, "Fechar", () => { _changes = null; Refresh(); }, Palette.Tip(Tr("Fecha esta lista."), Tr("o painel volta ao normal; o que mudou continua valendo."), Tr("nada."))).Name = "FecharMudancas";
    }

    /// <summary>Retrato de qualquer pessoa: candidato (pelo personagem dele), liderança, equipe, doador ou jornalista.</summary>
    private PortraitView? PortraitOf(Node parent, string id, int size)
    {
        Candidate? candidate = Campaign.Find(id);
        string characterId = candidate?.CharacterId ?? id;
        Appearance? appearance = State.Npcs.FirstOrDefault(n => n.Id == characterId)?.Appearance;
        if (appearance is null)
        {
            return null;
        }

        var view = parent.Add(new PortraitView { CustomMinimumSize = new Vector2(size, size), MouseFilter = MouseFilterEnum.Ignore });
        view.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        GameParty? party = candidate is null ? null : State.Party(candidate.PartyId);
        view.Show(appearance, party is null
            ? new PortraitOptions()
            : new PortraitOptions(Planalto.Core.Characters.Expression.Neutra, WardrobeContext.Campanha, party.Colors.Primary, "background_solido", party.Colors.Secondary), _game.People.Appearance);
        return view;
    }

    private string PostureText(Posture posture) => posture switch
    {
        Posture.DisputarZona => Tr("disputar as zonas em que você está"),
        Posture.Atacar => Tr("atacar você"),
        Posture.Compor => Tr("compor com você"),
        _ => Tr("ignorar você"),
    };

    private string TemperamentShort(Temperament t) => t switch
    {
        Temperament.Conciliador => Tr("conciliador"),
        Temperament.Combativo => Tr("combativo"),
        Temperament.Calculista => Tr("calculista"),
        Temperament.Leal => Tr("leal"),
        _ => Tr("volúvel"),
    };

    /// <summary>Linhas de uma previsão de comando da R3, cada uma com a faixa que o Core calculou.</summary>
    private List<string> CommandLines(CommandForecast f)
    {
        var lines = new List<string>();
        foreach (ForecastLine l in f.Lines)
        {
            string? text = l.Kind switch
            {
                "rival_image" => string.Format(Tr("imagem de {0}: {1} pontos"), _engine.NameOf(l.Target ?? ""), Range(l.Min * 100, l.Max * 100, "0.0")),
                "image" => Math.Abs(l.Min) < 0.0005 && Math.Abs(l.Max) < 0.0005 ? null : string.Format(Tr("sua imagem: {0} pontos"), Range(l.Min * 100, l.Max * 100, "0.0")),
                "money" => Math.Abs(l.Min) < 0.5 && Math.Abs(l.Max) < 0.5 ? Tr("dinheiro: nada")
                    : string.Format(Tr("dinheiro: {0}"), Math.Abs(l.Max - l.Min) < 0.5 ? SignedMoney((decimal)l.Min) : $"{SignedMoney((decimal)l.Min)} {Tr("a")} {SignedMoney((decimal)l.Max)}"),
                "attack_damage" => Math.Abs(l.Max - l.Min) < 0.0005
                    ? string.Format(PtBr, Tr("o ataque continua tirando {0:0.0} pontos da sua imagem"), l.Max * 100)
                    : string.Format(PtBr, Tr("o ataque passa a tirar de {0:0.0} a {1:0.0} pontos da sua imagem"), l.Min * 100, l.Max * 100),
                "attack_weeks" => Math.Abs(l.Max - l.Min) < 0.5
                    ? string.Format(PtBr, Tr("o ataque se apaga em {0:0} semanas"), l.Max)
                    : string.Format(PtBr, Tr("o ataque se apaga em {0:0} a {1:0} semanas"), l.Min, l.Max),
                "relation" => string.Format(Tr("relação com {0}: {1}"), _engine.NameOf(l.Target ?? ""), Range(l.Min, l.Max, "0")),
                "recognition" => string.Format(Tr("presença em todas as zonas, por semana: {0}"), Range(l.Min, l.Max, "0.00")),
                _ => null,
            };
            if (text is not null)
            {
                lines.Add(text);
            }
        }

        // Presença por zona (anúncio): as três zonas em que mais rende.
        foreach (ForecastLine l in f.Lines.Where(l => l.Kind == "presence" && l.Target is not null).OrderByDescending(l => l.Expected).Take(3))
        {
            lines.Add(string.Format(Tr("presença em {0}: {1}"), ZoneName(l.Target), Range(l.Min, l.Max, "0.00")));
        }

        // Faixa que encosta no zero: "0", não "+0,0".
        return lines.Select(l => l.Replace("+0,0 ", "0 ", StringComparison.Ordinal).Replace("−0,0 ", "0 ", StringComparison.Ordinal)).ToList();
    }

    private static string UpperFirst(string text) => text.Length == 0 ? text : char.ToUpper(text[0], PtBr) + text[1..];

    /// <summary>Bloco de previsão de um comando: a explicação do Core, as faixas, as chances e o que acrescentar.</summary>
    private VBoxContainer CommandForecastCard(Node parent, string name, string title, CommandForecast f, IEnumerable<string>? explanation = null, IEnumerable<string>? extra = null)
    {
        PanelContainer card = Card(parent, Palette.Cartao, Palette.Linha);
        card.Name = name;
        var col = Ui.Column(card, 6);
        Heading(col, title, 20);
        foreach (string line in explanation ?? f.Explanation)
        {
            Glossary.Rich(col, line, 18, Palette.Texto2);
        }

        foreach (string line in CommandLines(f))
        {
            Glossary.Rich(col, "· " + UpperFirst(line), 18, Palette.Texto);
        }

        foreach (ForecastOutcome outcome in f.Outcomes)
        {
            Glossary.Rich(col, outcome.Probability >= 0.9995
                ? "· " + outcome.Text
                : string.Format(PtBr, "· {0:P0}: {1}", outcome.Probability, outcome.Text), 18, Palette.Texto);
        }

        foreach (string line in extra ?? [])
        {
            Glossary.Rich(col, line, 18, Palette.Texto2);
        }

        return col;
    }

    private static void BlockCard(Node parent, string text)
    {
        var stop = parent.Add(new PanelContainer { Name = "Bloqueio" });
        stop.AddThemeStyleboxOverride("panel", Palette.Box(Palette.CartaoAlto, 8, 10, Palette.Linha));
        Glossary.Rich(stop, text, 18, Palette.Texto);
    }

    private string TradeText(DecisionTrade trade) =>
        string.Format(Tr("Você ganha em {0} e paga em {1}."), string.Join(", ", trade.Ganho.Select(NatureName)), string.Join(", ", trade.Custo.Select(NatureName)));

    // ------------------------------------------------------------------ área Pessoas: chapa e rivais

    /// <summary>Painel da chapa em resumo: cadeiras que o partido faria hoje e a posição do jogador.</summary>
    private void BuildSlateBlock()
    {
        if (Campaign.Player.Office != Office.Vereador)
        {
            return;
        }

        SlateView slate = _engine.SlatePanel();
        BlockHeader(Tr("Chapa do seu partido"), "VerChapa", () => OpenAreaSheet(Sheet.Chapa, null),
            Palette.Tip(Tr("Abre o painel da chapa."), Tr("mostra os colegas de partido que disputam as mesmas vagas, os votos projetados de cada um e quem entra."), Tr("nada.")));
        PanelContainer card = Card(_left, Palette.Cartao, Palette.Linha, 10);
        card.Name = "ResumoChapa";
        var col = Ui.Column(card, 4);
        Glossary.Rich(col, slate.Text + ".", 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
        Glossary.Rich(col, slate.Rank <= slate.Seats && slate.Seats > 0
            ? string.Format(PtBr, Tr("Pela projeção de hoje você entra, com {0:N0} votos."), slate.Votes)
            : slate.Seats == 0
                ? string.Format(PtBr, Tr("Pela projeção de hoje o partido não faz cadeira; você tem {0:N0} votos."), slate.Votes)
                : string.Format(PtBr, Tr("Faltam {0:N0} votos para você passar quem ocupa a última cadeira do partido."), slate.VotesToSeat));
    }

    private void BuildSlateSheet()
    {
        BackButton();
        Heading(_left, Tr("Chapa do seu partido"));
        SlateView slate = _engine.SlatePanel();
        SlateTotals totals = _engine.SlateTotals();
        GameParty? party = State.Party(slate.PartyId);
        Glossary.Rich(_left, slate.Text + ".", 20, Palette.Texto, Palette.Weight(Palette.Inter, 600)).Name = "TextoChapa";
        Glossary.Rich(_left, Tr("Colegas de chapa são aliados e concorrentes ao mesmo tempo: o voto de cada um soma para o partido fazer cadeiras, e as cadeiras ficam com os mais votados da chapa."));
        Glossary.Rich(_left, string.Format(PtBr, Tr("Projeção de hoje para {0}: {1:N0} votos, contando {2:N0} de legenda. O quociente eleitoral está em {3:N0} votos; a chapa tem {4} nomes."),
            party?.Sigla ?? slate.PartyId, totals.PartyVotes, totals.LegendVotes, totals.Quotient, slate.ListSize), 18, Palette.Texto2);
        if (slate.VotesToSeat > 0)
        {
            Glossary.Rich(_left, string.Format(PtBr, Tr("Faltam {0:N0} votos para você passar quem ocupa a última cadeira do partido."), slate.VotesToSeat), 18, Palette.Texto);
        }

        Heading(_left, Tr("Quem puxa quem"), 20);
        int rank = 0;
        foreach (SlateRow row in slate.Top)
        {
            rank++;
            double ofSlate = totals.PartyVotes <= 0 ? 0 : (double)row.Votes / totals.PartyVotes;
            double ofSeat = totals.Quotient <= 0 ? 0 : (double)row.Votes / totals.Quotient;
            bool listed = Campaign.Find(row.CandidateId) is not null;
            string name = row.IsPlayer ? Tr("Você") : listed ? row.Name : string.Format(Tr("outro nome da lista do {0}"), party?.Sigla ?? slate.PartyId);
            string status = row.Seated ? Tr("entra") : Tr("fica de fora");
            string detail = string.Format(PtBr, Tr("{0:N0} votos · {1:P0} dos votos da chapa · {2:P0} de um quociente · {3}"), row.Votes, ofSlate, ofSeat, status);
            bool named = !row.IsPlayer && _engine.RivalSheetOf(row.CandidateId) is not null;
            PanelContainer card = named
                ? ClickCard(_left, () => OpenAreaSheet(Sheet.Rival, row.CandidateId),
                    Palette.Tip(string.Format(Tr("{0}, colega de chapa."), row.Name), Tr("abre a ficha: perfil, redutos, plano percebido e o que dá para fazer."), Tr("nada.")), "Chapa_" + row.CandidateId)
                : Card(_left, row.IsPlayer ? Palette.CartaoAlto : Palette.Cartao, row.IsPlayer ? Palette.Acao : null, 10);
            var col = Ui.Column(card, 2);
            Glossary.Plain(col, $"{rank}º · {name}", 18, Palette.Texto, Palette.Weight(Palette.Inter, row.IsPlayer ? 600 : 500));
            Glossary.Rich(col, detail, 18, row.Seated ? Palette.Texto : Palette.Texto2);
        }

        Glossary.Rich(_left, Tr("Quem passa de um quociente sozinho puxa os de baixo: os votos que sobram dele ajudam o partido a fazer a cadeira seguinte. A projeção não tem o ruído do dia da eleição."), 18, Palette.Texto3);
    }

    /// <summary>Linha de rival: retrato, nome com o que é e o reconhecimento; o cartão inteiro abre a ficha.</summary>
    private void RivalRow(Candidate c)
    {
        bool sheet = _engine.RivalSheetOf(c.Id) is not null;
        PanelContainer card = sheet
            ? ClickCard(_left, () => OpenAreaSheet(Sheet.Rival, c.Id),
                Palette.Tip(CandidateLabel(c) + ".", Tr("abre a ficha: perfil e plano percebidos, redutos, verba vista, ataques trocados, pacto, lembranças e o que dá para fazer."), Tr("nada.")), "Rival_" + c.Id)
            : Card(_left, margin: 8);
        HBoxContainer row = Ui.Row(card, 10);
        PortraitOf(row, c.Id, 56);
        var col = Ui.Column(row, 2);
        col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Glossary.Plain(col, CandidateLabel(c), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        string pact = _engine.Rivals.PactWith(c.Id) is { } p ? " · " + Tr(p.Kind == PactKind.Dobradinha ? "dobradinha com você" : "pacto com você") : "";
        Glossary.Rich(col, string.Format(PtBr, "{0}{1} {2:0}%{3}", c.Incumbent ? Tr("tem mandato") + " · " : "", Tr("reconhecimento"), c.Recognition, pact));
    }

    // ------------------------------------------------------------------ ficha do rival

    private void BuildRivalSheet(RivalSheet sheet)
    {
        Candidate rival = Campaign.Find(sheet.CandidateId)!;
        BackButton();
        FirstTimeHint("rivais");
        HBoxContainer top = Ui.Row(_left, 12);
        PortraitOf(top, rival.Id, 120);
        var head = Ui.Column(top, 4);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        NameTitle(head, rival.Name, 22);
        string label = CandidateLabel(rival);
        string what = label[(rival.Name.Length + 2)..^1];
        bool colleague = rival.PartyId == Campaign.Player.PartyId && rival.Office == Campaign.Player.Office;
        Glossary.Rich(head, string.Format(Tr("{0}. Pessoa fictícia."), UpperFirst(what)));
        Glossary.Rich(head, string.Format(PtBr, Tr("{0}Reconhecimento {1:0}%. Temperamento {2}."), rival.Incumbent ? Tr("Tem mandato.") + " " : "", rival.Recognition, TemperamentShort(sheet.Temperament)), 18, Palette.Texto);
        if (colleague)
        {
            Glossary.Rich(_left, Tr("É colega de chapa: os votos desta candidatura somam para o seu partido e disputam com você as cadeiras que ele fizer."), 18, Palette.Texto);
        }

        ValueRow(_left, Tr("Relação com você (de −100 a 100)"), Signed(sheet.Relation, "0"), highlight: true);

        // Leitura do plano, com a confiança de quem coordena.
        Heading(_left, Tr("Plano percebido"), 20);
        StaffMember? coordinator = _engine.StaffIn(StaffRole.Coordenador);
        Glossary.Rich(_left, coordinator is null
            ? Tr("Sem ninguém na Coordenação, você só sabe onde este rival é forte. Contrate em Campanha, Equipe, para ler o plano.")
            : string.Format(PtBr, Tr("Leitura de {0}. Confiança da leitura: {1:P0}. Coordenação melhor lê mais: com pouca confiança você só vê os redutos."), StaffLabel(coordinator), sheet.Confidence), 18, Palette.Texto2).Name = "ConfiancaLeitura";
        if (sheet.ProfileName is not null && sheet.ProfileId is not null)
        {
            Glossary.Rich(_left, string.Format(Tr("Perfil: {0}. {1}"), sheet.ProfileName, RivalContent.Profile(sheet.ProfileId).Descricao), 18, Palette.Texto);
        }
        else
        {
            Glossary.Rich(_left, Tr("Perfil de campanha: ainda não dá para dizer."), 18, Palette.Texto3);
        }

        if (sheet.StrategyText is not null)
        {
            Glossary.Rich(_left, sheet.StrategyText + ".", 18, Palette.Texto);
        }

        if (sheet.EstimatedPosture is { } posture)
        {
            Glossary.Rich(_left, string.Format(Tr("Diante de você, a postura é {0}."), PostureText(posture)), 18, Palette.Texto);
        }

        bool zonesKnown = !sheet.EstimatedZones.SequenceEqual(sheet.Redutos);
        // Listas de zonas em texto corrido: numa linha de valor elas não quebram e alargariam o painel.
        Glossary.Rich(_left, string.Format(Tr("Redutos: {0}."), sheet.Redutos.Count == 0 ? Tr("nenhum") : string.Join(", ", sheet.Redutos.Select(ZoneName))), 18, Palette.Texto);
        if (zonesKnown)
        {
            Glossary.Rich(_left, string.Format(Tr("Zonas-alvo desta semana: {0}."), string.Join(", ", sheet.EstimatedZones.Select(ZoneName))), 18, Palette.Texto);
        }

        foreach (RivalAction action in sheet.Tomorrow)
        {
            string act = Catalog.Tipos.FirstOrDefault(t => t.Id == action.Kind)?.Nome ?? Tr("procura uma liderança");
            Glossary.Rich(_left, string.Format(Tr("Amanhã: {0}{1}."), act, action.Zone >= 0 ? " " + string.Format(Tr("em {0}"), ZoneName(Territory.Zones[action.Zone])) : ""), 18, Palette.Texto);
        }

        var zones = sheet.EstimatedZones.Where(z => _map.Zonas.ContainsKey(z)).ToList();
        if (zones.Count > 0)
        {
            string first = zones[0];
            Ui.Button(_left, "Ver no mapa", () => { _mapMode = "disputa"; _map.CenterOn(first); _map.Select(first); Refresh(); },
                Palette.Tip(Tr("Mostra no mapa onde este rival concentra a campanha."), Tr("troca o mapa para o modo Disputa e centraliza na primeira zona da lista."), Tr("nada."))).Name = "VerRivalNoMapa";
        }

        // Verba vista, por canal.
        Heading(_left, Tr("Verba vista"), 20);
        var budget = _engine.RivalBudgetSeen(rival.Id);
        if (budget.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma verba de canal vista nesta semana."));
        }

        foreach (RivalBudgetLine line in budget)
        {
            ChannelDefinition? channel = Channels.Find(line.ChannelId);
            string where = channel is { Alvo: ChannelDefinition.AlvoZona } ? " · " + string.Join(", ", line.Targets.Take(2).Select(ZoneName)) : "";
            ValueRow(_left, (channel?.Nome ?? line.ChannelId) + where, string.Format(Tr("cerca de {0}/sem."), Money(line.Weekly)));
        }

        // Pacto.
        Heading(_left, Tr("Pacto"), 20);
        if (sheet.Pact is { } pact)
        {
            PanelContainer card = Card(_left, Palette.CartaoAlto, Palette.Positivo, 10);
            card.Name = "PactoEmVigor";
            Glossary.Rich(card, pact.Kind == PactKind.Dobradinha
                ? string.Format(Tr("Dobradinha desde {0}: o seu nome vai no material da campanha a prefeito, e você deve apoio a essa campanha."), pact.Since.ToString("dd/MM", PtBr))
                : string.Format(Tr("Pacto de não agressão desde {0}. Zonas fechadas para os seus atos de rua: {1}. Marcar ato de rua lá ou atacar quebra o pacto."), pact.Since.ToString("dd/MM", PtBr),
                    pact.ClosedZones.Count == 0 ? Tr("nenhuma") : string.Join(", ", pact.ClosedZones.Select(ZoneName))), 18, Palette.Texto);
        }
        else
        {
            PactControls(rival);
        }

        // Ataques trocados.
        Heading(_left, Tr("Ataques trocados"), 20);
        var exchanged = _engine.Rivals.Attacks.Where(a => (a.AttackerId == rival.Id && a.TargetId == RaceSetup.PlayerId) || (a.AttackerId == RaceSetup.PlayerId && a.TargetId == rival.Id))
            .OrderByDescending(a => a.Date).Take(6).ToList();
        if (exchanged.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhum ataque entre vocês até agora."));
        }

        foreach (Attack attack in exchanged)
        {
            bool mine = attack.AttackerId == RaceSetup.PlayerId;
            string kind = AttackContent.Type(attack.Type)?.Nome ?? attack.Type;
            string channel = AttackContent.Channel(attack.Channel)?.Nome ?? attack.Channel;
            Glossary.Rich(_left, string.Format(PtBr, Tr(mine ? "· {0}: você atacou ({1}, {2}). Ainda tira {3:0.0} pontos da imagem de {4}." : "· {0}: {4} atacou você ({1}, {2}). Ainda tira {3:0.0} pontos da sua imagem."),
                attack.Date.ToString("dd/MM", PtBr), LowerFirst(kind), LowerFirst(channel), _engine.DamageNow(attack) * 100, rival.Name));
        }

        // Pontos fracos conhecidos.
        Heading(_left, Tr("Pontos fracos que você conhece"), 20);
        if (sheet.KnownWeaknesses.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhum. Investigar pode revelar um, que serve de prova num ataque."));
        }

        foreach (RivalWeakness weakness in sheet.KnownWeaknesses)
        {
            Glossary.Rich(_left, string.Format(Tr("· {0} (gravidade {1} de 3): {2}"), AttackContent.Type(weakness.Type)?.Nome ?? weakness.Type, weakness.Severity, weakness.Text), 18, Palette.Texto);
        }

        // O que dá para fazer.
        Heading(_left, Tr("O que dá para fazer"), 20);
        var attack0 = Ui.Button(_left, "Atacar", () =>
        {
            _attackProof = sheet.KnownWeaknesses.OrderByDescending(w => w.Severity).FirstOrDefault()?.Id;
            _attackType = sheet.KnownWeaknesses.FirstOrDefault(w => w.Id == _attackProof)?.Type ?? AttackType.Tema;
            OpenSheet(Sheet.Atacar, rival.Id);
        }, Palette.Tip(
            string.Format(Tr("Abre o ataque a {0}."), rival.Name),
            Tr("você escolhe o tipo, o canal e a prova e vê a previsão antes de confirmar: a imagem que o rival perde, a que você perde e a reação esperada."),
            Tr("nada para abrir; o ataque custa o canal escolhido e imagem entre quem rejeita campanha negativa.")));
        attack0.Name = "AbrirAtaque";
        Palette.Danger(attack0);
        InvestigateControls(rival);

        Heading(_left, Tr("Lembranças de você"), 20);
        if (sheet.Memories.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma lembrança de você ainda."));
        }

        foreach (Memory memory in sheet.Memories.AsEnumerable().Reverse())
        {
            Glossary.Rich(_left, string.Format(Tr("· {0}: {1}. {2} de relação"), memory.Date.ToString("d MMM yyyy", PtBr), memory.Text, Signed(memory.Pontos, "0")));
        }
    }

    private string PactFailureText(CommandFailure failure, Candidate rival, PactKind kind) => failure switch
    {
        CommandFailure.ForaDaCampanha => Tr("só dá para propor durante a pré-campanha e a campanha."),
        CommandFailure.AlvoInvalido => kind == PactKind.Dobradinha
            ? Tr("dobradinha é de quem disputa a Câmara com quem disputa a prefeitura.")
            : Tr("pacto de não agressão é entre quem disputa o mesmo cargo."),
        CommandFailure.PactoEmVigor => kind == PactKind.Dobradinha ? Tr("você já tem uma dobradinha ou um pacto em vigor com esta pessoa.") : Tr("já há um pacto em vigor com esta pessoa."),
        CommandFailure.JaFeito => string.Format(Tr("você já fez uma proposta a {0} há menos de {1} semanas; espere."), rival.Name, RivalContent.Regras.PactoSemanasEntrePropostas),
        _ => Tr("esta pessoa não aceita propostas."),
    };

    /// <summary>Propor pacto: a previsão (chance de aceitar, zonas que se fecham) e o botão, com o motivo quando bloqueado.</summary>
    private void PactControls(Candidate rival)
    {
        PactKind kind = rival.Office == Campaign.Player.Office ? PactKind.NaoAgressao : PactKind.Dobradinha;
        CommandForecast forecast = _engine.ForecastPact(rival.Id, kind);
        DecisionTrade trade = RivalContent.Pactos[kind == PactKind.Dobradinha ? CampaignEngine.PactoDobradinha : CampaignEngine.PactoNaoAgressao];
        string title = Tr(kind == PactKind.Dobradinha ? "Previsão da dobradinha" : "Previsão do pacto de não agressão");
        string who = string.Format(Tr("Quem vai lembrar: {0}."), rival.Name);
        VBoxContainer col = CommandForecastCard(_left, "PrevisaoPacto", title, forecast, forecast.Explanation.Where(l => !l.StartsWith("+ ", StringComparison.Ordinal) && !l.StartsWith("− ", StringComparison.Ordinal)), [TradeText(trade), who]);
        string label = kind == PactKind.Dobradinha ? "Propor dobradinha" : "Propor pacto de não agressão";
        string tip = Palette.Tip(
            string.Format(Tr(kind == PactKind.Dobradinha ? "Propõe dobradinha a {0}." : "Propõe pacto de não agressão a {0}."), rival.Name),
            string.Join(" ", forecast.Explanation.Take(1)) + " " + who,
            Tr("nada em dinheiro; ") + TradeText(trade));
        var button = Ui.Button(_left, label, () =>
        {
            var (failure, result) = _engine.ProposePact(rival.Id, kind);
            if (failure is { } f)
            {
                _log.Insert(0, PactFailureText(f, rival, kind));
                Refresh();
                return;
            }

            var lines = new List<string> { result!.Text, string.Format(Tr("{0} vai lembrar: \"{1}\" ({2} de relação)."), rival.Name, result.Memory.Text, Signed(result.Memory.Pontos, "0")) };
            if (result.Pact is { ClosedZones.Count: > 0 } pact)
            {
                lines.Add(string.Format(Tr("Zonas fechadas para os seus atos de rua: {0}."), string.Join(", ", pact.ClosedZones.Select(ZoneName))));
            }

            if (result is { Accepted: true } && kind == PactKind.Dobradinha)
            {
                lines.Add(Tr("Dívida no Livro de compromissos: apoio à campanha a prefeito."));
            }

            _log.Insert(0, result.Text);
            _changes = (Tr(label), lines);
            Refresh();
        }, tip);
        button.Name = "ProporPacto";
        if (forecast.Blocked is { } blocked)
        {
            BlockCard(col, Tr("Não dá para propor:") + " " + PactFailureText(blocked, rival, kind));
            button.Disabled = true;
            button.TooltipText = Palette.Blocked(PactFailureText(blocked, rival, kind), tip);
        }
    }

    private void InvestigateControls(Candidate rival)
    {
        CommandForecast forecast = _engine.ForecastInvestigation(rival.Id);
        string tip = Palette.Tip(
            string.Format(Tr("Manda levantar a vida de {0}."), rival.Name),
            string.Join(" ", forecast.Explanation) + " " + string.Join(" ", forecast.Outcomes.Select(o => string.Format(PtBr, "{0:P0}: {1}", o.Probability, o.Text))),
            string.Format(Tr("{0}; se não achar nada, {1} pode ficar sabendo e lembrar."), Money(_engine.InvestigationCost), rival.Name));
        var button = Ui.Button(_left, $"{Tr("Investigar")} · {Money(_engine.InvestigationCost)}", () =>
        {
            var (failure, result) = _engine.Investigate(rival.Id);
            if (failure is not null || result is null)
            {
                Refresh();
                return;
            }

            var lines = new List<string> { result.Text, string.Format(Tr("Caixa: {0}."), SignedMoney(result.Money)) };
            if (result.Found is { } found)
            {
                lines.Add(string.Format(Tr("Ponto fraco: {0} (gravidade {1} de 3). Serve de prova num ataque."), found.Text, found.Severity));
            }

            _log.Insert(0, string.Format(Tr("Investigação sobre {0}: {1}"), rival.Name, result.Text));
            _changes = (string.Format(Tr("Investigação sobre {0}"), rival.Name), lines);
            Refresh();
        }, tip);
        button.Name = "Investigar";
        button.ClipText = true;
        if (forecast.Blocked is { } blocked)
        {
            button.Disabled = true;
            button.TooltipText = Palette.Blocked(blocked == CommandFailure.SemDinheiro
                ? string.Format(Tr("faltam {0}."), Money(_engine.InvestigationCost - State.Player.Resources.Money))
                : Tr("só dá para investigar durante a pré-campanha e a campanha."), tip);
        }
    }

    // ------------------------------------------------------------------ atacar

    private AttackRequest AttackDraft(string rivalId)
    {
        AttackType type = AttackContent.Type(_attackType) ?? AttackContent.Tipos[0];
        AttackChannel channel = AttackContent.Channel(_attackChannel) ?? AttackContent.Canais[0];
        var known = _engine.RivalSheetOf(rivalId)?.KnownWeaknesses ?? [];
        string? proof = type.UsaProva && known.Any(w => w.Id == _attackProof && w.Type == type.Id) ? _attackProof : null;
        string? theme = type.Id == AttackType.Tema ? _attackTheme ?? Platform.Flags.FirstOrDefault() ?? _engine.Context.Themes.Themes[0].Id : null;
        string? outlet = channel.ExigeVeiculo ? _attackOutlet ?? _engine.City.Outlets.FirstOrDefault()?.Id : null;
        return new AttackRequest(rivalId, type.Id, channel.Id, proof, theme, outlet);
    }

    private string AttackFailureText(CommandFailure failure, AttackRequest r) => failure switch
    {
        CommandFailure.ForaDaCampanha => Tr("só dá para atacar durante a pré-campanha e a campanha."),
        CommandFailure.CanalFechado => string.Format(Tr("o palanque só existe na campanha oficial, a partir de 16 de agosto (faltam {0} semanas)."), WeeksToOfficialCampaign),
        CommandFailure.VeiculoInvalido => Tr("falta escolher o veículo em que a pauta sai; esta cidade pode não ter nenhum."),
        CommandFailure.AlvoInvalido => Tr("falta escolher o tema."),
        CommandFailure.SemProva => Tr("esse ponto fraco não serve de prova para este tipo de ataque."),
        CommandFailure.JaFeito => string.Format(Tr("você atacou esta pessoa há menos de {0} dias; espere."), RivalContent.Regras.AtaqueIntervaloDias),
        CommandFailure.SemDinheiro => string.Format(Tr("faltam {0} em caixa."), Money(_engine.AttackCost(r.Channel) - State.Player.Resources.Money)),
        _ => Tr("este alvo não pode ser atacado."),
    };

    private void BuildAttackSheet(string rivalId)
    {
        Candidate rival = Campaign.Find(rivalId)!;
        var known = _engine.RivalSheetOf(rivalId)?.KnownWeaknesses ?? [];
        BackButton();
        Heading(_left, Tr("Atacar"));
        Glossary.Plain(_left, CandidateLabel(rival), 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        AttackRequest request = AttackDraft(rivalId);
        AttackType type = AttackContent.Type(request.Type)!;
        AttackChannel channel = AttackContent.Channel(request.Channel)!;

        FieldLabel("Por quê");
        var typeOptions = Ui.Options(_left, AttackContent.Tipos.Select(t => (t.Id, t.Nome)), type.Id, id => { _attackType = id; Refresh(); }, translate: false);
        typeOptions.Name = "TipoAtaque";
        typeOptions.ClipText = true;
        typeOptions.TooltipText = Palette.Tip(Tr("O motivo do ataque."), Tr("incoerência e esqueleto afirmam um fato: com prova tiram mais imagem; sem prova, há risco de processo. Tema é crítica à posição do rival e não precisa de prova."), Tr("nada; o custo é o do canal."));
        Glossary.Rich(_left, type.Descricao);

        if (type.UsaProva)
        {
            FieldLabel("Prova");
            var items = new List<(string Id, string Text)> { ("", Tr("Sem prova")) };
            items.AddRange(known.Where(w => w.Type == type.Id).Select(w => (w.Id, string.Format(Tr("Gravidade {0}: {1}"), w.Severity, w.Text))));
            var proof = Ui.Options(_left, items, request.WeaknessId ?? "", id => { _attackProof = id.Length == 0 ? null : id; Refresh(); }, translate: false);
            proof.Name = "ProvaAtaque";
            proof.ClipText = true;
            proof.TooltipText = Palette.Tip(Tr("O ponto fraco que serve de prova."), Tr("com prova o ataque tira mais imagem, mais ainda com gravidade maior; sem prova, pode vir processo. Investigar o rival revela pontos fracos."), Tr("nada."));
        }
        else
        {
            FieldLabel("Tema");
            var themes = Ui.Options(_left, _engine.Context.Themes.Themes.Select(t => (t.Id, t.Name)), request.Theme, id => { _attackTheme = id; Refresh(); }, translate: false);
            themes.Name = "TemaAtaque";
            themes.ClipText = true;
            themes.TooltipText = Palette.Tip(Tr("O tema da crítica."), Tr("o ataque fala da posição do rival nesse tema; quem se importa com o tema presta mais atenção."), Tr("nada."));
        }

        FieldLabel("Por onde");
        var channelOptions = Ui.Options(_left, AttackContent.Canais.Select(c => (c.Id, $"{c.Nome} · {(_engine.AttackCost(c.Id) > 0 ? Money(_engine.AttackCost(c.Id)) : Tr("sem custo"))}")), channel.Id, id => { _attackChannel = id; Refresh(); }, translate: false);
        channelOptions.Name = "CanalAtaque";
        channelOptions.ClipText = true;
        channelOptions.TooltipText = Palette.Tip(Tr("O canal por onde o ataque sai."), Tr("cada canal alcança de um jeito: redes são baratas e rápidas, a imprensa dá crédito, o palanque só existe na campanha oficial."), Tr("o custo de cada canal está ao lado do nome."));
        if (channel.ExigeVeiculo)
        {
            FieldLabel("Veículo");
            var outlets = Ui.Options(_left, _engine.Outlets().Select(o => (o.Outlet.Id, o.Label)), request.OutletId, id => { _attackOutlet = id; Refresh(); }, translate: false);
            outlets.Name = "VeiculoAtaque";
            outlets.ClipText = true;
            outlets.TooltipText = Palette.Tip(Tr("O veículo que recebe a pauta do ataque."), Tr("o ataque sai como notícia nesse veículo, no tom da linha dele."), Tr("nada além do custo do canal."));
        }

        CommandForecast forecast = _engine.ForecastAttack(request);
        MemoryDefinition memory = RivalContent.Memory("ataque");
        string who = string.Format(Tr("Quem vai lembrar: {0} (\"{1}\", {2} de relação; essa lembrança não se apaga)."), rival.Name, memory.Texto, Signed(memory.Pontos, "0"));
        VBoxContainer col = CommandForecastCard(_left, "PrevisaoAtaque", Tr("Previsão"), forecast, extra: [who]);
        if (forecast.Blocked is { } blocked)
        {
            BlockCard(col, Tr("Não dá para atacar:") + " " + AttackFailureText(blocked, request));
        }

        HBoxContainer row = Ui.Row(_left, 8);
        string tip = Palette.Tip(
            string.Format(Tr("Ataca {0}: {1}, por {2}."), rival.Name, LowerFirst(type.Nome), LowerFirst(channel.Nome)),
            string.Join("; ", CommandLines(forecast)) + ". " + who,
            string.Format(Tr("{0} e imagem entre quem rejeita campanha negativa."), _engine.AttackCost(channel.Id) > 0 ? Money(_engine.AttackCost(channel.Id)) : Tr("nada em dinheiro")));
        var confirm = Ui.Button(row, "Confirmar o ataque", () =>
        {
            var (failure, result) = _engine.AttackRival(request);
            if (failure is not null || result is null)
            {
                Refresh();
                return;
            }

            var lines = new List<string>(result.Explanation)
            {
                string.Format(PtBr, Tr("Imagem de {0}: {1} pontos. Sua imagem: {2} pontos. Caixa: {3}."), rival.Name, Signed(result.RivalImage * 100, "0.0"), Signed(result.Image * 100, "0.0"), SignedMoney(result.Money)),
            };
            if (result.Sued)
            {
                lines.Add(string.Format(Tr("{0} processou você: sem prova, a Justiça Eleitoral aplicou multa."), rival.Name));
            }

            if (result.ExpectedReaction is { } reaction)
            {
                lines.Add(reaction);
            }

            if (result.PactBroken is { } broken)
            {
                lines.Add(string.Format(Tr("O pacto com {0} foi quebrado: virou notícia, e essa lembrança não se apaga."), _engine.NameOf(broken)));
            }

            lines.Add(who);
            _log.Insert(0, string.Format(Tr("Você atacou {0}."), CandidateLabel(rival)));
            Done(string.Format(Tr("Ataque a {0}"), rival.Name), lines);
        }, tip);
        confirm.Name = "ConfirmarAtaque";
        confirm.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        if (forecast.Blocked is { } reason)
        {
            confirm.Disabled = true;
            confirm.TooltipText = Palette.Blocked(AttackFailureText(reason, request), tip);
        }
        else
        {
            Palette.Danger(confirm);
        }

        Ui.Button(row, "Cancelar", Back, Palette.Tip(Tr("Fecha sem atacar."), Tr("nada muda."), Tr("nada."))).Name = "CancelarAtaque";
    }

    // ------------------------------------------------------------------ ataque contra você, com prazo

    private string AttackSourceText(Attack attack)
    {
        string who = Campaign.Find(attack.AttackerId) is { } c ? CandidateLabel(c) : _engine.NameOf(attack.AttackerId);
        return attack.OutletId is not null && _engine.City.Outlet(attack.OutletId) is { } outlet
            ? string.Format(Tr("{0}, em {1}"), who, _engine.OutletLabel(outlet))
            : $"{who}, {LowerFirst(AttackContent.Channel(attack.Channel)?.Nome ?? attack.Channel)}";
    }

    /// <summary>Prazo de resposta com a contagem em dias de jogo (o dia é a unidade do tempo).</summary>
    private string DeadlineText(PendingAttackView view)
    {
        int days = view.Deadline.DayNumber - _engine.Today.DayNumber;
        string left = days <= 0 ? Tr("vence hoje") : days == 1 ? Tr("falta 1 dia") : string.Format(Tr("faltam {0} dias"), days);
        return string.Format(Tr("responda até {0} ({1})"), view.Deadline.ToString("dddd, d/MM", PtBr), left);
    }

    /// <summary>Texto da faixa de um ataque com prazo: quem, até quando e o que vale sem resposta (GDD 17.4).</summary>
    private string AttackBannerText(PendingAttackView view) =>
        string.Format(Tr("Ataque de {0} contra você: {1}. Sem resposta, vale o silêncio."), view.AttackerName, DeadlineText(view));

    private string ResponseFailureText(CommandFailure failure, decimal cost) => failure switch
    {
        CommandFailure.SemAdvogado => Tr("falta alguém no Jurídico da equipe: é quem pede o direito de resposta. Contrate em Campanha, Equipe."),
        CommandFailure.SemDinheiro => string.Format(Tr("faltam {0} em caixa."), Money(Math.Max(0, cost - State.Player.Resources.Money))),
        CommandFailure.PrazoVencido => Tr("o prazo de resposta venceu: valeu o silêncio."),
        CommandFailure.JaFeito => Tr("você já respondeu a este ataque."),
        CommandFailure.CanalFechado => Tr("o canal do contra-ataque só abre na campanha oficial, a partir de 16 de agosto."),
        CommandFailure.NaoEncontrado => Tr("o autor não é um candidato nomeado: não há a quem contra-atacar."),
        _ => Tr("esta resposta não está disponível agora."),
    };

    /// <summary>Cartão de um ataque contra o jogador com o prazo correndo (área Mídia e ficha da resposta).</summary>
    private void PendingAttackCard(Node parent, PendingAttackView view)
    {
        PanelContainer card = ClickCard(parent, () => OpenAreaSheet(Sheet.Ataque, view.Attack.Id),
            Palette.Tip(Tr("Ataque contra você, com prazo de resposta."), Tr("abre as respostas possíveis, cada uma com a previsão; sem resposta até o prazo, vale o silêncio."), Tr("nada para abrir.")),
            "AtaquePendente_" + view.Attack.Id);
        card.AddThemeStyleboxOverride("panel", Palette.Box(Palette.Cartao, 8, 12, Palette.Negativo));
        var col = Ui.Column(card, 4);
        Glossary.Rich(col, UpperFirst(AttackSourceText(view.Attack)) + (view.Attack.Rumor ? " · " + Tr("BOATO") : ""), 18, Palette.Texto3);
        Glossary.Rich(col, view.Attack.Text + ".", 18, Palette.Texto, Palette.Weight(Palette.Inter, 500));
        Glossary.Rich(col, UpperFirst(DeadlineText(view)) + ".", 18, Palette.Texto);
    }

    private void BuildAttackResponseSheet(string attackId)
    {
        BackButton();
        Heading(_left, Tr("Ataque contra você"));
        Attack? attack = _engine.Rivals.Attacks.FirstOrDefault(a => a.Id == attackId);
        if (attack is null)
        {
            Glossary.Rich(_left, Tr("Este ataque já se apagou."));
            return;
        }

        PanelContainer card = Card(_left, Palette.Cartao, Palette.Negativo);
        card.Name = "AtaqueTexto";
        var col = Ui.Column(card, 4);
        Glossary.Rich(col, UpperFirst(AttackSourceText(attack)) + " · " + attack.Date.ToString("dd/MM", PtBr), 18, Palette.Texto3);
        Glossary.Rich(col, attack.Text + ".", 20, Palette.Texto, Palette.Serif);
        if (attack.Rumor)
        {
            Glossary.Rich(col, Tr("Circula como boato de rede: pode ser falso. Boato falso é mais fácil de desmentir."), 18, Palette.Texto, Palette.Weight(Palette.Inter, 600)).Name = "MarcaBoato";
        }

        Glossary.Rich(col, string.Format(PtBr, Tr("Hoje tira {0:0.0} pontos da sua imagem. Cada ponto vale 0,01 na escala de −1 a +1 da barra de cima."), _engine.DamageNow(attack) * 100), 18, Palette.Texto);
        if (Campaign.Find(attack.AttackerId) is { IsPlayer: false, IsBulk: false } author && _engine.RivalSheetOf(author.Id) is not null)
        {
            Ui.Button(_left, string.Format(Tr("Ficha de {0}"), author.Name), () => OpenAreaSheet(Sheet.Rival, author.Id),
                Palette.Tip(string.Format(Tr("Abre a ficha de {0}."), CandidateLabel(author)), Tr("mostra o plano percebido, os pontos fracos que você conhece e os ataques trocados."), Tr("nada."))).Name = "FichaDoAutor";
        }

        PendingAttackView? view = _engine.PendingAttacks().FirstOrDefault(v => v.Attack.Id == attackId);
        AttackRules rules = AttackContent.Regras;
        if (view is null)
        {
            string answered = attack.Response is null
                ? Tr("O prazo venceu sem resposta: valeu o silêncio.")
                : string.Format(Tr("Você respondeu: {0}."), LowerFirst(AttackContent.Response(attack.Response)?.Nome ?? attack.Response));
            if (attack.RulingOn is { } ruling && ruling >= _engine.Today)
            {
                answered += " " + string.Format(Tr("O pedido de direito de resposta sai em {0}."), ruling.ToString("dddd, d/MM", PtBr));
            }

            Glossary.Rich(_left, answered, 18, Palette.Texto).Name = "RespostaDada";
            Glossary.Rich(_left, string.Format(Tr("O ataque se apaga em {0}."), attack.Ends.ToString("dd/MM/yyyy", PtBr)));
            return;
        }

        PanelContainer deadline = Card(_left, Palette.CartaoAlto, Palette.Linha, 10);
        deadline.Name = "PrazoResposta";
        Glossary.Rich(deadline, string.Format(Tr("{0}. O tempo não para por causa disso. Sem resposta até lá, vale o silêncio: o ataque fica sem contestação e leva {1} semanas para se apagar; bem respondido, {2}."),
            UpperFirst(DeadlineText(view)), rules.SemanasSemResposta, rules.SemanasRespondido), 18, Palette.Texto);

        Heading(_left, Tr("Respostas"), 20);
        foreach (var (option, label, blocked) in view.Options)
        {
            CommandForecast forecast = _engine.ForecastResponse(attackId, option.Id);
            PanelContainer optionCard = Card(_left, Palette.Cartao, Palette.Linha, 10);
            optionCard.Name = "Resposta_" + option.Id;
            var body = Ui.Column(optionCard, 4);
            Glossary.Rich(body, label, 18, Palette.Texto, Palette.Weight(Palette.Inter, 600));
            Glossary.Rich(body, option.Descricao);
            var lines = CommandLines(forecast);
            foreach (string line in lines)
            {
                Glossary.Rich(body, "· " + UpperFirst(line), 18, Palette.Texto);
            }

            foreach (ForecastOutcome outcome in forecast.Outcomes.Where(o => o.Probability < 0.9995))
            {
                Glossary.Rich(body, string.Format(PtBr, "· {0:P0}: {1}", outcome.Probability, outcome.Text), 18, Palette.Texto);
            }

            if (option.Dias is { } days)
            {
                Glossary.Rich(body, string.Format(Tr("O resultado sai em {0} a {1} dias."), days.Min, days.Max));
            }

            string trade = TradeText(option.Troca);
            Glossary.Rich(body, trade, 18, Palette.Texto3);
            string tip = Palette.Tip(label + ".", option.Descricao + " " + string.Join("; ", lines) + ".", trade);
            var button = Ui.Button(body, "Escolher esta resposta", () => AnswerAttack(attack, option, label), tip);
            button.Name = "Responder_" + option.Id;
            button.ClipText = true;
            button.Alignment = HorizontalAlignment.Left;
            if (blocked is { } reason)
            {
                button.Disabled = true;
                decimal cost = (decimal)-(forecast.Line("money")?.Min ?? 0);
                button.TooltipText = Palette.Blocked(ResponseFailureText(reason, cost), tip);
                BlockCard(body, Tr("Não dá:") + " " + ResponseFailureText(reason, cost));
            }
        }
    }

    private void AnswerAttack(Attack attack, AttackResponse option, string label)
    {
        var (failure, result) = _engine.RespondToAttack(attack.Id, option.Id);
        if (failure is not null || result is null)
        {
            Refresh();
            return;
        }

        var lines = new List<string>
        {
            result.Text.Replace("{rival}", _engine.NameOf(attack.AttackerId), StringComparison.Ordinal),
            string.Format(PtBr, Tr("O ataque tirava {0:0.0} pontos da sua imagem; agora tira {1:0.0}. Apaga-se em {2} semanas."), result.DamageBefore * 100, result.DamageAfter * 100, result.WeeksLeft),
        };
        if (result.Money != 0)
        {
            lines.Add(string.Format(Tr("Caixa: {0}."), SignedMoney(result.Money)));
        }

        if (attack.RulingOn is { } ruling)
        {
            lines.Add(string.Format(Tr("A Justiça Eleitoral decide o pedido em {0}; o resultado aparece nas Notícias."), ruling.ToString("dddd, d/MM", PtBr)));
        }

        _log.Insert(0, string.Format(Tr("Resposta ao ataque de {0}: {1}."), _engine.NameOf(attack.AttackerId), LowerFirst(label)));
        _changes = (string.Format(Tr("Resposta: {0}"), label), lines);
        _notice = null;
        Refresh();
    }

    // ------------------------------------------------------------------ ficha de uma pessoa qualquer do mundo

    private string RoleText(Person person)
    {
        string id = person.Id;
        if (_engine.City.Donor(id) is not null)
        {
            return Tr("doa como pessoa física");
        }

        if (_engine.City.Outlets.FirstOrDefault(o => o.JournalistId == id) is { } asJournalist)
        {
            return string.Format(Tr("jornalista de {0}"), asJournalist.Name);
        }

        if (_engine.City.Outlets.FirstOrDefault(o => o.OwnerId == id) is { } asOwner)
        {
            return string.Format(Tr("manda em {0}"), asOwner.Name);
        }

        if (_engine.City.Bosses.FirstOrDefault(b => b.Value == id) is { Key: not null } boss)
        {
            return string.Format(Tr("dirigente do {0} na cidade"), PartySigla(boss.Key));
        }

        return Tr("pessoa da política local");
    }

    /// <summary>Nome fictício com o que é (princípio j), para qualquer pessoa.</summary>
    private string PersonLabel(string id)
    {
        if (id == RaceSetup.PlayerId)
        {
            return Tr("Você");
        }

        if (_engine.City.Leader(id) is { } leader)
        {
            return LeaderLabel(leader);
        }

        if (Campaign.Find(id) is { } candidate)
        {
            return CandidateLabel(candidate);
        }

        if (_engine.Staff(id) is { } member)
        {
            return StaffLabel(member);
        }

        return _engine.City.Person(id) is { } person ? $"{_engine.NameOf(id)} ({RoleText(person)})" : _engine.NameOf(id);
    }

    private void BuildGenericPersonSheet(Person person)
    {
        string name = _engine.NameOf(person.Id);
        BackButton();
        HBoxContainer top = Ui.Row(_left, 12);
        PortraitOf(top, person.Id, 120);
        var head = Ui.Column(top, 4);
        head.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        NameTitle(head, name, 22);
        Glossary.Rich(head, string.Format(Tr("{0}. Pessoa fictícia."), UpperFirst(RoleText(person))));
        Glossary.Rich(head, string.Format(Tr("Temperamento {0}."), TemperamentName(person.Temperament)));

        ValueRow(_left, Tr("Relação com você (de −100 a 100)"), Signed(person.Relation(_engine.Today, _engine.Context.Campaign.People), "0"), highlight: true);
        foreach (Interest interest in person.Interests)
        {
            string text = interest.Kind switch
            {
                Interest.Tema => string.Format(Tr("Interesse: o tema {0}."), ThemeName(interest.Target)),
                Interest.ZonaKind => string.Format(Tr("Interesse: a zona {0}."), ZoneName(interest.Target)),
                _ => string.Format(Tr("Interesse: {0}."), interest.Target),
            };
            Glossary.Rich(_left, text);
        }

        if (_engine.Donors().FirstOrDefault(d => d.Donor.PersonId == person.Id) is { } donor)
        {
            DonorBlock(donor);
        }

        foreach (var outlet in _engine.Outlets().Where(o => o.Outlet.JournalistId == person.Id || o.Outlet.OwnerId == person.Id))
        {
            OutletRow(_left, outlet);
        }

        if (_engine.City.Bosses.Any(b => b.Value == person.Id && b.Key == Campaign.Player.PartyId))
        {
            FundView fund = _engine.FundQuota();
            Heading(_left, Tr("Fundo do partido"), 20);
            foreach (string line in fund.Explanation)
            {
                Glossary.Rich(_left, line, 18, Palette.Texto);
            }
        }

        Heading(_left, Tr("Lembranças de você"), 20);
        if (person.Memories.Count == 0)
        {
            Glossary.Rich(_left, Tr("Nenhuma lembrança de você ainda."));
        }

        foreach (Memory memory in person.Memories.AsEnumerable().Reverse())
        {
            Glossary.Rich(_left, string.Format(Tr("· {0}: {1}. {2} de relação"), memory.Date.ToString("d MMM yyyy", PtBr), memory.Text, Signed(memory.Pontos, "0")));
        }
    }
}
