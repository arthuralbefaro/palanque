using System.Globalization;
using System.Text;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Municipios;
using Planalto.UI.Theme;

namespace Planalto.UI.Widgets;

/// <summary>Mini-mapa: um ponto por município (lat/lon), com os filtrados em destaque e o escolhido maior.</summary>
public partial class MiniMap : Control
{
    // Retângulo que contém o Brasil (graus).
    private const float West = -74.5f;
    private const float East = -32.0f;
    private const float North = 6.0f;
    private const float South = -34.5f;

    public IReadOnlyList<Municipio> All { get; set; } = [];

    public HashSet<int> Highlight { get; set; } = [];

    public Municipio? Selected { get; set; }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("#0B1626"));
        foreach (Municipio m in All)
        {
            bool on = Highlight.Count == 0 || Highlight.Contains(m.CodigoIbge);
            DrawRect(new Rect2(Project(m) - Vector2.One, new Vector2(2, 2)), on ? new Color("#5FA8D3") : new Color("#2A3B55"));
        }

        if (Selected is not null)
        {
            DrawCircle(Project(Selected), 6, new Color("#E9C46A"));
        }
    }

    private Vector2 Project(Municipio m)
    {
        float scale = Mathf.Min(Size.X / (East - West), Size.Y / (North - South));
        return new Vector2(((float)m.Lon - West) * scale, (North - (float)m.Lat) * scale);
    }
}

/// <summary>
/// Escolha da cidade natal e palco da primeira campanha (GDD 12.6): busca com autocompletar, filtro por UF e
/// região, mini-mapa e ficha com população, eleitorado, vagas, porte e dificuldade sugerida.
/// </summary>
public partial class MunicipioPicker : Window
{
    private static readonly string[] Regioes = ["N", "NE", "CO", "SE", "S"];

    private readonly Action<Municipio> _onChosen;
    private readonly List<Municipio> _all;
    private LineEdit _search = null!;
    private ItemList _results = null!;
    private VBoxContainer _card = null!;
    private MiniMap _map = null!;
    private Button _choose = null!;
    private string? _uf;
    private string? _regiao;
    private List<Municipio> _shown = [];
    private Municipio? _selected;

    public MunicipioPicker(Action<Municipio> onChosen)
    {
        _onChosen = onChosen;
        _all = Game.Instance.Municipios.All.Where(m => m.EleicoesMunicipais).OrderBy(m => m.Nome, StringComparer.Create(new CultureInfo("pt-BR"), true)).ToList();
        Title = TranslationServer.Translate("Escolha a cidade");
        Size = new Vector2I(1300, 760);
        CloseRequested += QueueFree;
    }

    public override void _Ready()
    {
        var margins = this.Add(new MarginContainer());
        margins.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        foreach (string side in new[] { "left", "top", "right", "bottom" })
        {
            margins.AddThemeConstantOverride($"margin_{side}", 16);
        }

        HBoxContainer body = Ui.Row(margins, 16);
        VBoxContainer left = Ui.Column(body, 8);
        left.CustomMinimumSize = new Vector2(420, 0);
        _search = left.Add(new LineEdit { PlaceholderText = TranslationServer.Translate("Buscar município"), ClearButtonEnabled = true });
        _search.TextChanged += _ => Filter();
        _search.TooltipText = Palette.Tip(T("Busca pelo nome do município."), T("filtra a lista enquanto você digita, sem diferença de acento ou de maiúscula; os pontos acesos no mapa são os encontrados."), T("nada."));
        HBoxContainer filters = Ui.Row(left);
        Ui.Options(filters, new[] { ("", "Todas as UFs") }.Concat(Game.Instance.Municipios.Files.Select(f => (f.Uf, f.Uf))), "", uf => { _uf = uf.Length == 0 ? null : uf; Filter(); }).TooltipText =
            Palette.Tip(T("Filtro por estado."), T("mostra só os municípios da UF escolhida."), T("nada."));
        Ui.Options(filters, new[] { ("", "Todas as regiões") }.Concat(Regioes.Select(r => (r, Region(r)))), "", r => { _regiao = r.Length == 0 ? null : r; Filter(); }).TooltipText =
            Palette.Tip(T("Filtro por região do país."), T("mostra só os municípios da região escolhida."), T("nada."));
        _results = left.Add(new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill });
        _results.TooltipText = Palette.Tip(T("Municípios encontrados (até 300)."), T("clique num nome para ver a ficha: população, eleitorado, vagas na Câmara, porte e dificuldade sugerida."), T("nada; a escolha só vale no botão Escolher esta cidade."));
        _results.ItemSelected += i => Select(_shown[(int)i]);

        VBoxContainer right = Ui.Column(body, 8);
        right.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _map = right.Add(new MiniMap { CustomMinimumSize = new Vector2(560, 440), All = _all });
        _card = Ui.Column(right, 4);
        _choose = Ui.Button(right, "Escolher esta cidade", () =>
        {
            if (_selected is not null)
            {
                _onChosen(_selected);
                QueueFree();
            }
        });
        _choose.Disabled = true;
        ChooseTip();
        Filter();
    }

    private static string Region(string id) => TranslationServer.Translate(id switch
    {
        "N" => "Norte",
        "NE" => "Nordeste",
        "CO" => "Centro-Oeste",
        "SE" => "Sudeste",
        _ => "Sul",
    });

    private void Filter()
    {
        string query = Fold(_search.Text);
        _shown = _all
            .Where(m => (_uf is null || m.Uf == _uf) && (_regiao is null || m.Regiao == _regiao) && (query.Length == 0 || Fold(m.Nome).Contains(query, StringComparison.Ordinal)))
            .OrderBy(m => query.Length > 0 && !Fold(m.Nome).StartsWith(query, StringComparison.Ordinal))
            .Take(300)
            .ToList();
        _results.Clear();
        foreach (Municipio m in _shown)
        {
            _results.AddItem($"{m.Nome} ({m.Uf})");
        }

        _map.Highlight = _uf is null && _regiao is null && query.Length == 0 ? [] : _shown.Select(m => m.CodigoIbge).ToHashSet();
        _map.QueueRedraw();
    }

    /// <summary>Busca e escolhe o primeiro resultado (teste de fumaça da UI).</summary>
    public void ChooseFirstForTest(string query)
    {
        _search.Text = query;
        Filter();
        if (_shown.Count > 0)
        {
            Select(_shown[0]);
            _onChosen(_shown[0]);
        }
    }

    /// <summary>Busca e seleciona o primeiro resultado, sem confirmar a escolha (teste de explicação: a ficha aberta).</summary>
    public void SelectFirstForTest(string query)
    {
        _search.Text = query;
        Filter();
        if (_shown.Count > 0)
        {
            Select(_shown[0]);
        }
    }

    private void Select(Municipio m)
    {
        _selected = m;
        _choose.Disabled = false;
        ChooseTip();
        _map.Selected = m;
        _map.QueueRedraw();
        Ui.Clear(_card);
        PorteDefinition porte = Game.Instance.Portes.Get(m.Porte);
        string difficulty = Game.Instance.World.Options.Difficulties.FirstOrDefault(d => d.Id == porte.SuggestedDifficulty)?.Name ?? porte.SuggestedDifficulty;
        Ui.Value(_card, $"{m.Nome} — {m.Uf}", 24);
        Ui.Value(_card, string.Format(CultureInfo.GetCultureInfo("pt-BR"), "{0}: {1:N0} · {2}: {3:N0}", TranslationServer.Translate("População"), m.Populacao, TranslationServer.Translate("Eleitorado"), m.Eleitorado));
        Ui.Value(_card, $"{TranslationServer.Translate("Vagas na Câmara")}: {m.VagasVereador} · {TranslationServer.Translate("Porte")}: {porte.Name} · {TranslationServer.Translate("Dificuldade sugerida")}: {difficulty}");
        Ui.Value(_card, $"{TranslationServer.Translate("Região imediata")}: {m.RegiaoImediata.Nome}");
        Glossary.LinkTerms(_card);
    }

    /// <summary>Tooltip do botão de escolha: as três partes e, sem cidade selecionada, o motivo do bloqueio.</summary>
    private void ChooseTip()
    {
        string tip = Palette.Tip(
            T("Escolhe a cidade selecionada como cidade natal."),
            T("a cidade natal é o palco da primeira campanha: o porte dela define quantos votos elegem um vereador e quantos rivais você enfrenta."),
            T("nada; dá para trocar enquanto a carreira não começa."));
        _choose.TooltipText = _choose.Disabled ? Palette.Blocked(T("nenhuma cidade selecionada; clique num nome da lista."), tip) : tip;
    }

    private static string T(string text) => TranslationServer.Translate(text);

    /// <summary>Minúsculas e sem acento, para a busca ignorar acentuação.</summary>
    private static string Fold(string text)
    {
        var sb = new StringBuilder();
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}
