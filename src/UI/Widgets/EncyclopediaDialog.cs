using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Encyclopedia;
using Planalto.Core.Indicators;
using Planalto.Core.State;
using Planalto.UI.Theme;

namespace Planalto.UI.Widgets;

/// <summary>
/// Enciclopédia do jogo (F1 em qualquer tela, princípio j): Glossário, Como funciona, Este mundo (partidos,
/// institutos e pessoas do save atual) e Sua carreira (histórico de decisões).
/// </summary>
public partial class EncyclopediaDialog : PanelContainer
{
    private const string NodeName = "Enciclopedia";

    private string _section = "glossario";
    private string? _term;
    private string _search = "";
    private VBoxContainer _body = null!;
    private readonly Dictionary<string, Button> _tabs = [];

    public static bool IsOpen(Node root) => root.FindChild(NodeName, recursive: true, owned: false) is not null;

    /// <summary>Abre (ou traz para a frente) a Enciclopédia; com <paramref name="termId"/>, já no verbete.</summary>
    public static void Open(Node root, string? termId = null)
    {
        if (root.FindChild(NodeName, recursive: true, owned: false) is EncyclopediaDialog open)
        {
            open.ShowTerm(termId);
            return;
        }

        var layer = new CanvasLayer { Layer = 50, Name = NodeName + "Camada" };
        var shade = new ColorRect { Color = new Color(0, 0, 0, 0.5f), MouseFilter = MouseFilterEnum.Stop };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var dialog = new EncyclopediaDialog { Name = NodeName, _term = termId, _section = "glossario" };
        layer.AddChild(shade);
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        shade.AddChild(center);
        center.AddChild(dialog);
        root.AddChild(layer);
    }

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(1000, 720);
        Theme = Palette.CentralTheme();
        AddThemeStyleboxOverride("panel", Palette.Box(Palette.Painel, 12, 24, Palette.Linha));
        var root = Ui.Column(this, 12);
        var header = Ui.Row(root, 12);
        var title = Ui.Label(header, "Enciclopédia", 32);
        title.AddThemeFontOverride("font", Palette.Weight(Palette.Oswald, 600));
        header.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var search = header.Add(new LineEdit { PlaceholderText = Tr("Buscar"), CustomMinimumSize = new Vector2(280, 44), TooltipText = Palette.Tip(Tr("Busca na Enciclopédia."), Tr("filtra verbetes e artigos pelo texto digitado."), Tr("nada.")) });
        search.TextChanged += text => { _search = text; Build(); };
        Ui.Button(header, "Fechar", Close, Palette.Tip(Tr("Fecha a Enciclopédia (também com Esc ou F1)."), Tr("volta para a tela em que você estava; o jogo continua pausado como estava."), Tr("nada.")));

        var tabs = Ui.Row(root, 8);
        foreach (var (id, name, tip) in new[]
        {
            ("glossario", "Glossário", "Termos do jogo e das eleições, em linguagem simples."),
            ("como_funciona", "Como funciona", "As regras eleitorais reais e as do jogo, com exemplos e a base legal."),
            ("este_mundo", "Este mundo", "Partidos, institutos e pessoas desta carreira."),
            ("carreira", "Sua carreira", "Seu Livro de compromissos (promessas, dívidas, posições e esqueletos) e o que você decidiu até aqui."),
        })
        {
            var b = Ui.Button(tabs, name, () => { _section = id; _term = null; Build(); }, Palette.Tip(Tr(tip), Tr("mostra esta parte da Enciclopédia."), Tr("nada.")));
            b.ToggleMode = true;
            _tabs[id] = b;
        }

        var scroll = root.Add(new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled });
        _body = scroll.Add(new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _body.AddThemeConstantOverride("separation", 14);
        Build();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true } key && key.Keycode is Key.Escape)
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Abre a Enciclopédia em "Como funciona", com o artigo pedido em primeiro lugar e destacado.</summary>
    public static void OpenArticle(Node root, string articleId)
    {
        Open(root);
        if (root.FindChild(NodeName, recursive: true, owned: false) is EncyclopediaDialog dialog)
        {
            dialog._section = "como_funciona";
            dialog._term = articleId;
            dialog.Build();
        }
    }

    private void ShowTerm(string? termId)
    {
        _section = "glossario";
        _term = termId;
        Build();
    }

    private void Close() => GetParent()?.GetParent()?.GetParent()?.QueueFree();

    private bool Matches(string text) => _search.Length == 0 || text.Contains(_search, StringComparison.OrdinalIgnoreCase);

    private void Build()
    {
        if (_body is null)
        {
            return;
        }

        Ui.Clear(_body);
        foreach (var (id, tab) in _tabs)
        {
            // Só a parte aberta fica marcada (o botão de alternância marcaria as duas).
            tab.SetPressedNoSignal(id == _section);
        }

        Game game = Game.Instance;
        EncyclopediaContent content = game.Encyclopedia;
        switch (_section)
        {
            case "como_funciona":
                foreach (EncyclopediaArticle a in content.Artigos.Where(a => Matches(a.Titulo + a.Texto)).OrderBy(a => a.Id == _term ? 0 : 1))
                {
                    Entry(a.Titulo, Marked(a.Texto, a.AConferir), highlight: a.Id == _term);
                }

                break;
            case "este_mundo":
                BuildWorld(game);
                break;
            case "carreira":
                BuildCareer(game);
                break;
            default:
                var terms = content.Termos.Where(t => Matches(t.Nome + t.Texto)).OrderBy(t => t.Id == _term ? 0 : 1).ThenBy(t => t.Nome, StringComparer.Create(CultureInfo.GetCultureInfo("pt-BR"), true));
                foreach (GlossaryTerm t in terms)
                {
                    Entry(t.Nome, Marked(t.Texto, t.AConferir), highlight: t.Id == _term);
                }

                break;
        }
    }

    private static string Marked(string text, bool pending) =>
        pending ? $"{text} {TranslationServer.Translate(EncyclopediaContent.MarcaAConferir)}" : text;

    private void Entry(string title, string text, bool highlight = false, bool section = false)
    {
        var card = _body.Add(new PanelContainer());
        card.AddThemeStyleboxOverride("panel", section ? Palette.Box(Palette.Painel, 0, 4) : Palette.Box(highlight ? Palette.CartaoAlto : Palette.Cartao, 8, 16, highlight ? Palette.Acao : null, 2));
        var col = Ui.Column(card, 6);
        var heading = Ui.Label(col, title, section ? 26 : 22);

        // Título do próprio verbete: não é link para ele mesmo (o teste de explicação ignora pelo nome).
        heading.Name = "TituloVerbete";
        heading.AddThemeFontOverride("font", Palette.Weight(Palette.Inter, 600));
        Glossary.Rich(col, text, 20, Palette.Texto2, Palette.Serif);
    }

    private void BuildWorld(Game game)
    {
        GameState? state = game.State;
        if (state is null)
        {
            Entry(Tr("Nenhuma carreira aberta"), Tr("Comece ou carregue uma carreira para ver os partidos, institutos e pessoas deste mundo."));
            return;
        }

        BuildIndicators(game, state);
        foreach (GameParty p in state.Parties.Where(p => Matches(p.Name + p.Sigla)).OrderBy(p => p.Number))
        {
            string family = game.World.Parties.Families.Families.FirstOrDefault(f => f.Id == p.FamilyId)?.Name ?? p.FamilyId;
            Entry($"{p.Sigla} · {p.Number} ({Tr("partido")})", string.Format(Tr("{0}. Família ideológica: {1}. Tamanho no início: {2}. Partido fictício."), p.Name, family, p.Size));
        }

        foreach (var pollster in game.Electorate.Pollsters.Pollsters.Where(p => Matches(p.Name)))
        {
            Entry($"{pollster.Name} ({Tr(pollster.Internal ? "pesquisa interna" : "instituto de pesquisa")})",
                string.Format(Tr("Amostra de {0} a {1} entrevistas. Instituto fictício; cada um tem tendência própria, e a margem de erro depende da amostra."), pollster.SampleMin, pollster.SampleMax));
        }

        if (state.City is { } city && game.Engine is { } engine)
        {
            foreach (Leader leader in city.Leaders.Where(l => l.Known && Matches(engine.NameOf(l.PersonId))))
            {
                LeaderType type = game.Campaign.Leaders.Type(leader.Type);
                Entry($"{engine.NameOf(leader.PersonId)} ({type.Papel})",
                    string.Format(Tr("{0}. Atua em {1}, entre os eleitores do grupo {2}. Influência {3} de 5. Pessoa fictícia; a ficha completa fica na área Pessoas."), type.Nome, engine.ZoneName(leader.Zones[0]), EffectSummary.GroupName(leader.Publico), leader.Influence));
            }
        }

        if (state.City is { } team && game.Engine is { } teamEngine)
        {
            foreach (StaffMember member in team.Staff.Where(s => s.Hired && Matches(teamEngine.NameOf(s.PersonId))))
            {
                StaffRole role = game.Campaign.Staff.Role(member.Role);
                Entry($"{teamEngine.NameOf(member.PersonId)} ({role.Papel})",
                    string.Format(Tr("{0}. {1} Vínculo: {2}. Pessoa fictícia; a ficha completa fica em Campanha, Equipe."), role.Nome, role.Descricao, member.BondText));
            }
        }

        if (state.City is { } media && game.Engine is { } mediaEngine)
        {
            var ptBr = CultureInfo.GetCultureInfo("pt-BR");
            foreach (OutletView o in mediaEngine.Outlets().Where(o => Matches(o.Label)))
            {
                Entry(o.Label,
                    string.Format(ptBr, Tr("Alcança {0:P0} dos eleitores da cidade. Temas de interesse: {1}. Quem manda: {2}. Jornalista principal: {3}. Veículo fictício; a ficha completa fica na área Mídia."),
                        o.ReachShare, string.Join(", ", o.Outlet.Themes.Select(t => game.World.Parties.Themes.Themes.FirstOrDefault(x => x.Id == t)?.Name ?? t)), o.OwnerName, o.JournalistName));
            }

            foreach (DonorView d in mediaEngine.Donors().Where(d => Matches(d.Name)))
            {
                Entry(string.Format(Tr("{0} (doa como pessoa física)"), d.Name),
                    string.Format(ptBr, Tr("Teto legal de doação neste ano: R$ {0:N0}. Em troca, {1}. Pessoa fictícia; a ficha completa fica em Finanças, Contas."), d.LegalCap, d.RequestText));
            }
        }

        if (state.Campaign is { } campaign)
        {
            foreach (Candidate c in campaign.Candidates.Where(c => !c.IsBulk && !c.IsPlayer && Matches(c.Name)))
            {
                string party = state.Party(c.PartyId)?.Sigla ?? c.PartyId;
                Entry($"{c.Name} ({Tr(c.Office == Core.Elections.Office.Prefeito ? "candidato(a) a prefeito(a)" : "candidato(a) a vereador(a)")}, {party})",
                    string.Format(CultureInfo.GetCultureInfo("pt-BR"), Tr("Número {0}. Reconhecimento na cidade: {1:0}%. {2}Pessoa fictícia."), c.Number, c.Recognition, c.Incumbent ? Tr("Tem mandato hoje. ") : ""));
            }
        }
    }

    /// <summary>
    /// Números oficiais de partida do município em foco (GDD 17.17): valor na unidade real, fonte e ano, e a marca de
    /// quando o valor é substituição. O jogo só mostra o dado e a fonte, sem juízo sobre a cidade.
    /// </summary>
    private void BuildIndicators(Game game, GameState state)
    {
        Core.Municipios.Municipio? m = game.Municipios.All.FirstOrDefault(x => x.CodigoIbge == state.FocusMunicipio);
        if (m is null || game.IndicadoresDe(m) is not { } i || game.RealIndicators is not { } definitions || !Matches(m.Nome + Tr("indicadores números reais")))
        {
            return;
        }

        var culture = CultureInfo.GetCultureInfo("pt-BR");
        Dictionary<string, decimal> values = IndicatorDefinitions.RealValues(i, m);
        string anos = i.Saude.Anos.Count == 0 ? "" : $"{i.Saude.Anos.Min()}-{i.Saude.Anos.Max()}";
        var years = new Dictionary<string, string>
        {
            [IndicatorDefinitions.Ideb] = i.Ideb.Ano.ToString(culture) + i.Ideb.Fonte switch
            {
                IdebMunicipal.EdicaoAnterior => Tr(", edição anterior"),
                IdebMunicipal.DaUf => Tr(", média da rede municipal no estado"),
                IdebMunicipal.RedeEstadual => Tr(", rede distrital"),
                _ => "",
            },
            [IndicatorDefinitions.MortalidadeInfantil] = anos + (i.Saude.MortalidadeInfantilFonte == SaudeMunicipal.DaRegiaoImediata ? Tr(", taxa da região imediata") : ""),
            [IndicatorDefinitions.Homicidios] = anos,
            [IndicatorDefinitions.Leitos] = $"{i.Saude.LeitosCompetencia % 100:00}/20{i.Saude.LeitosCompetencia / 100:00}",
            [IndicatorDefinitions.RedeAgua] = "2022",
            [IndicatorDefinitions.RedeEsgoto] = "2022",
            [IndicatorDefinitions.EmpregoFormal] = i.Emprego.Ano.ToString(culture),
            [IndicatorDefinitions.PessoalRcl] = (i.Financas.RgfExercicio ?? i.Financas.Exercicio).ToString(culture),
            [IndicatorDefinitions.ReceitaPerCapita] = i.Financas.Exercicio.ToString(culture) + (i.Financas.Fonte == FinancasEnte.Estimativa ? Tr(", estimativa pelos municípios do mesmo porte") : ""),
            [IndicatorDefinitions.PibPerCapita] = i.Pib?.Ano.ToString(culture) ?? "",
        };

        var lines = new List<string>();
        foreach (RealIndicatorDefinition d in definitions.Indicadores.Where(d => values.ContainsKey(d.Id)))
        {
            lines.Add(string.Format(culture, "{0}: {1:#,0.##} ({2}; {3}, {4})", d.Nome, values[d.Id], d.Unidade, d.Fonte, years.GetValueOrDefault(d.Id, "")));
        }

        lines.Add(string.Format(culture, Tr("Receita corrente da prefeitura: R$ {0:#,0} ({1}, {2})."), i.Financas.ReceitaCorrente, "Tesouro Nacional, SICONFI", i.Financas.Exercicio));
        Entry(
            string.Format(Tr("{0} ({1}): números oficiais de partida"), m.Nome, m.Uf),
            string.Join(". ", lines) + " " + Tr("São o ponto de partida; a partir do primeiro dia, tudo evolui pela simulação."));
    }

    /// <summary>
    /// Sua carreira: o Livro de compromissos (promessas, dívidas, posições assumidas e esqueletos, GDD 17.2) e a
    /// linha do tempo das decisões em eventos.
    /// </summary>
    private void BuildCareer(Game game)
    {
        GameState? state = game.State;
        if (state is null)
        {
            Entry(Tr("Nenhuma carreira aberta"), Tr("Comece ou carregue uma carreira para ver o seu Livro de compromissos e as suas decisões."));
            return;
        }

        var ptBr = CultureInfo.GetCultureInfo("pt-BR");
        string Date(DateOnly d) => d.ToString("dd/MM/yyyy", ptBr);
        string Person(string id)
        {
            string name = id == RaceSetup.PlayerId ? state.Player.BallotName : state.Npcs.FirstOrDefault(n => n.Id == id)?.BallotName ?? id;
            return state.City?.Leader(id) is { } leader ? $"{name} ({game.Campaign.Leaders.Type(leader.Type).Papel})" : name;
        }

        string Zone(string id) => game.Engine?.ZoneName(id) ?? id;
        Core.People.Ledger ledger = state.Ledger;
        Entry(Tr("Livro de compromissos"), Tr("O que fica das suas decisões e atravessa eleições e mandatos: promessas, dívidas, posições assumidas e esqueletos. Quem cobra são as pessoas a quem você deve, os rivais e a imprensa."), section: true);

        if (ledger.Promises.Count == 0)
        {
            Entry(Tr("Promessas"), Tr("Nenhuma promessa feita. Aceitar o pedido de uma liderança grava uma promessa aqui, com prazo."));
        }

        foreach (Core.People.Promise p in ledger.Promises.AsEnumerable().Reverse().Where(p => Matches(p.Text)))
        {
            string who = p.ToKind switch
            {
                Core.People.Promise.ParaPessoa => Person(p.ToId),
                Core.People.Promise.ParaZona => Zone(p.ToId),
                _ => EffectSummary.GroupName(p.ToId),
            };
            string status = p.State switch
            {
                Core.People.PromiseState.Aberta => Tr("em aberto"),
                Core.People.PromiseState.Cumprida => Tr("cumprida"),
                Core.People.PromiseState.Quebrada => Tr("quebrada"),
                _ => Tr("vencida sem cumprir"),
            };
            Entry(string.Format(Tr("Promessa · {0}"), status),
                string.Format(Tr("A quem: {0}. O quê: {1}.{2} Feita em {3}. Prazo: {4}. Estado: {5}."), who, p.Text, p.ZoneId is null ? "" : " " + string.Format(Tr("Onde: {0}."), Zone(p.ZoneId)), Date(p.Made), Date(p.Deadline), status));
        }

        if (ledger.Debts.Count == 0)
        {
            Entry(Tr("Dívidas"), Tr("Nenhuma dívida política. Um apoio fechado em negociação, sem promessa, fica aqui como dívida."));
        }

        foreach (Core.People.Debt d in ledger.Debts.AsEnumerable().Reverse().Where(d => Matches(d.Text)))
        {
            Entry(string.Format(Tr("Dívida · {0}"), d.Settled ? Tr("quitada") : Tr("em aberto")),
                string.Format(Tr("A quem: {0}. O quê: {1}. Desde {2}."), Person(d.ToPersonId), d.Text, Date(d.Made)));
        }

        if (ledger.Positions.Count == 0)
        {
            Entry(Tr("Posições assumidas"), Tr("Nenhuma posição dita em público ainda."));
        }

        foreach (Core.People.StatedPosition position in ledger.Positions.AsEnumerable().Reverse())
        {
            Core.Ideology.ThemeDefinition? definition = game.World.Parties.Themes.Themes.FirstOrDefault(t => t.Id == position.Theme);
            string theme = definition?.Name ?? position.Theme;
            string pole = definition is null || position.Value == 0 ? Tr("no meio") : position.Value > 0 ? definition.PositivePole : definition.NegativePole;
            Entry(string.Format(Tr("Posição assumida · {0}"), theme), string.Format(ptBr, Tr("Tema: {0}. Posição: {1:+0;−0;0} ({2}), numa escala de −2 a +2. Dita em {3}. Onde: {4}."), theme, position.Value, pole, Date(position.Date), position.Where));
        }

        if (ledger.Skeletons.Count == 0)
        {
            Entry(Tr("Esqueletos"), Tr("Nenhum. Um gasto ou acordo irregular fica guardado aqui, com a lista de quem sabe."));
        }

        foreach (Core.People.LedgerSkeleton skeleton in ledger.Skeletons.AsEnumerable().Reverse().Where(k => Matches(k.Text)))
        {
            Entry(string.Format(Tr("Esqueleto · gravidade {0}"), skeleton.Severity),
                string.Format(Tr("{0}. Desde {1}. Quem sabe: {2}. {3}"), skeleton.Text, Date(skeleton.Created),
                    skeleton.KnownBy.Count == 0 ? Tr("ninguém além de você") : string.Join(", ", skeleton.KnownBy.Select(Person)),
                    skeleton.Exposed ? Tr("Já veio a público.") : Tr("Ainda não veio a público.")));
        }

        Entry(Tr("Linha do tempo"), Tr("Suas decisões em eventos, da mais recente à mais antiga."), section: true);
        if (state.Campaign is not { } campaign || campaign.Events.History.Count == 0)
        {
            Entry(Tr("Nada registrado ainda"), Tr("Suas decisões em eventos aparecem aqui, com a data e o resultado."));
            return;
        }

        foreach (var entry in campaign.Events.History.AsEnumerable().Reverse().Where(h => Matches(h.Choice + h.Text)))
        {
            string title = game.Events.Find(entry.EventId)?.Title ?? entry.EventId;
            Entry($"{entry.Week:dd/MM/yyyy} · {title}", $"{Tr("Você escolheu")}: {entry.Choice}. {entry.Text}");
        }
    }
}
