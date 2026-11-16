using System.Globalization;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Campaign;
using Planalto.Core.Encyclopedia;
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
            ("carreira", "Sua carreira", "O que você decidiu até aqui."),
        })
        {
            var b = Ui.Button(tabs, name, () => { _section = id; _term = null; Build(); }, Palette.Tip(Tr(tip), Tr("mostra esta parte da Enciclopédia."), Tr("nada.")));
            b.ToggleMode = true;
            b.ButtonPressed = id == _section;
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
        Game game = Game.Instance;
        EncyclopediaContent content = game.Encyclopedia;
        switch (_section)
        {
            case "como_funciona":
                foreach (EncyclopediaArticle a in content.Artigos.Where(a => Matches(a.Titulo + a.Texto)))
                {
                    Entry(a.Titulo, a.Texto);
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
                    Entry(t.Nome, t.Texto, highlight: t.Id == _term);
                }

                break;
        }
    }

    private void Entry(string title, string text, bool highlight = false)
    {
        var card = _body.Add(new PanelContainer());
        card.AddThemeStyleboxOverride("panel", Palette.Box(highlight ? Palette.CartaoAlto : Palette.Cartao, 8, 16, highlight ? Palette.Acao : null, 2));
        var col = Ui.Column(card, 6);
        var heading = Ui.Label(col, title, 22);

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

    private void BuildCareer(Game game)
    {
        if (game.State?.Campaign is not { } campaign || campaign.Events.History.Count == 0)
        {
            Entry(Tr("Nada registrado ainda"), Tr("Suas decisões em eventos aparecem aqui, com a data e o resultado. A partir da R2a, também as promessas, dívidas e posições assumidas."));
            return;
        }

        foreach (var entry in campaign.Events.History.AsEnumerable().Reverse().Where(h => Matches(h.Choice + h.Text)))
        {
            string title = game.Events.Find(entry.EventId)?.Title ?? entry.EventId;
            Entry($"{entry.Week:dd/MM/yyyy} · {title}", $"{Tr("Você escolheu")}: {entry.Choice}. {entry.Text}");
        }
    }
}
