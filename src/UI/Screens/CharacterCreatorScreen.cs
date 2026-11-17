using System.Globalization;
using System.Text.Json;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.Municipios;
using Planalto.Core.Parties;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.CampaignMaterials;
using Planalto.UI.Portrait;
using Planalto.UI.Theme;
using Planalto.UI.Widgets;
using Expression = Planalto.Core.Characters.Expression;
using FileAccess = Godot.FileAccess;

namespace Planalto.UI.Screens;

/// <summary>
/// Criação de personagem (GDD 3 e 14.2 A): abas Aparência, Vestuário, Identidade, Campanha e Partido, com
/// pré-visualização do retrato, santinho e material permitido (adesivo, bandeira e peça de redes; outdoor é proibido); aleatório geral e por aba, desfazer e presets.
/// </summary>
public partial class CharacterCreatorScreen : Control
{
    private static string PresetFolder => Autoload.UserPaths.Of("presets");
    private static readonly string[] Religions = ["catolica", "evangelica", "outras", "sem_religiao"];

    private Game _game = null!;
    private WorldSetup _world = null!;
    private Rng _rng = null!;
    private Character _c = null!;
    private readonly Stack<(string Character, bool FaceEdited, bool WardrobeEdited)> _undo = new();

    // O jogador mexeu à mão na aparência ou nas roupas: mudar o gênero pergunta antes de sortear de novo.
    private bool _faceEdited;

    /// <summary>Biografia escrita à mão: deixa de acompanhar as escolhas até "Gerar de novo".</summary>
    private bool _bioEdited;

    /// <summary>Semente do modelo de biografia: a mesma frase acompanha as escolhas; "Gerar de novo" troca.</summary>
    private ulong _bioSeed;
    private bool _wardrobeEdited;
    private List<string> _offeredTraits = [];
    private TabContainer _tabs = null!;
    private PortraitView _portrait = null!;
    private Santinho _santinho = null!;
    private MateriaisPermitidos _materiais = null!;
    private VBoxContainer _status = null!;
    private Button _start = null!;
    private bool _ownParty;

    // Dígitos do número de urna como o jogador digitou, enquanto não formam um número válido; null quando valem.
    private string? _ballotDraft;
    private Label? _ballotIssue;

    private PeopleContent People => _game.People;

    private DateOnly Today => new(_world.Options.StartYear, 1, 1);

    public override void _Ready()
    {
        _game = Game.Instance;
        if (_game.PendingWorld is null)
        {
            _game.GoTo(Game.Scenes.NewWorld);
            return;
        }

        _world = _game.PendingWorld;
        _rng = new Rng(_game.PendingSeed + 1);
        Municipio home = _rng.Pick(_game.Municipios.All.Where(m => m.EleicoesMunicipais).ToList());
        _c = PersonGenerator.Generate(_rng, People, new PersonRequest("jogador", home.Uf, home.CodigoIbge, Today, PartyId: _rng.Pick(_world.Parties).Id, Ages: People.Origins.StartingAge));
        _offeredTraits = OfferTraits(_c.TraitIds);
        _c.Campaign = NewCampaign();
        _c.Biography = WriteBiography();

        VBoxContainer screen = Ui.Screen(this);
        HBoxContainer top = Ui.Row(screen, 12);
        Ui.Label(top, "Criação de personagem", 36);
        top.Add(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        Ui.Button(top, "Aleatório", () => Change(RandomizeAll), Tip("Sorteia o personagem inteiro.", "gera de novo aparência, roupas, identidade, campanha e, se você não criou partido, o partido; a cidade natal fica.", "nada; Desfazer volta atrás."));
        Ui.Button(top, "Aleatório só nesta aba", () => Change(() => RandomizeTab(_tabs.CurrentTab)), Tip("Sorteia só a aba aberta.", "as outras abas ficam como estão.", "nada; Desfazer volta atrás."));
        Ui.Button(top, "Desfazer", Undo, Tip("Desfaz a última mudança.", "volta o personagem ao estado anterior, uma mudança por clique.", "nada."));
        Ui.Button(top, "Salvar preset", SavePreset, Tip("Grava a aparência num arquivo.", "dá para carregar a mesma aparência em outro personagem; só a aparência e as roupas vão para o arquivo.", "nada."));
        Ui.Button(top, "Carregar preset", LoadPreset, Tip("Abre uma aparência salva antes.", "troca a aparência e as roupas pelas do arquivo; identidade, campanha e partido ficam.", "nada; Desfazer volta atrás."));

        HBoxContainer body = Ui.Row(screen, 16);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        _tabs = body.Add(new TabContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(820, 0) });

        VBoxContainer preview = Ui.Column(body, 10);
        _portrait = preview.Add(new PortraitView { CustomMinimumSize = new Vector2(240, 240) });
        _santinho = preview.Add(new Santinho());
        _materiais = preview.Add(new MateriaisPermitidos());

        HBoxContainer bottom = Ui.Row(screen, 16);
        _status = Ui.Column(bottom, 2);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Ui.Button(bottom, "Voltar ao Novo Mundo", () => { _world.RemovePlayerParty(); _game.GoTo(Game.Scenes.NewWorld); }, Tip("Volta ao Novo Mundo.", "dá para mudar os partidos e as opções do mundo; o partido que você criou aqui sai da lista.", "nada; o personagem desta tela se perde."));
        _start = Ui.Button(bottom, "Começar carreira", StartCareer);

        BuildTabs();
        RefreshPreview();
    }

    // ---------- Estado e desfazer ----------

    private void Change(Action action)
    {
        PushUndo();
        action();
        if (!_bioEdited)
        {
            // Idade, partido, profissão, cidade e origem mudam a biografia sugerida; a escrita à mão fica.
            _c.Biography = WriteBiography();
        }

        BuildTabs();
        RefreshPreview();
    }

    private void Edit(Action action)
    {
        PushUndo();
        action();
        if (!_bioEdited)
        {
            _c.Biography = WriteBiography();
        }

        RefreshPreview();
    }

    private void PushUndo() => _undo.Push((JsonSerializer.Serialize(_c, JsonData.Options), _faceEdited, _wardrobeEdited));

    private void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        var (character, faceEdited, wardrobeEdited) = _undo.Pop();
        _c = JsonSerializer.Deserialize<Character>(character, JsonData.Options)!;
        _faceEdited = faceEdited;
        _wardrobeEdited = wardrobeEdited;
        _ballotDraft = null;
        BuildTabs();
        RefreshPreview();
    }

    private void SetAppearance(Func<Appearance, Appearance> change) => Edit(() =>
    {
        _c.Appearance = change(_c.Appearance!);
        _faceEdited = true;
    });

    /// <summary>
    /// Troca de gênero: a aparência e as roupas são sorteadas de novo com os pesos do gênero novo. Se o jogador já
    /// editou alguma delas à mão, pergunta antes: sortear de novo ou manter o que ele fez.
    /// </summary>
    private void RequestGender(Gender gender)
    {
        if (gender == _c.Gender)
        {
            return;
        }

        if (!_faceEdited && !_wardrobeEdited)
        {
            Change(() => SetGender(gender, reroll: true));
            return;
        }

        var dialog = new ConfirmationDialog
        {
            Name = "ConfirmarAparencia",
            Title = Tr("Mudar o gênero"),
            DialogText = Tr("Você já editou a aparência à mão. Sortear uma aparência nova, com os pesos do gênero escolhido, ou manter a que você fez?"),
            OkButtonText = Tr("Sortear de novo"),
            CancelButtonText = Tr("Manter a aparência"),
            DialogAutowrap = true,
            Exclusive = true,
        };
        bool answered = false;
        void Answer(bool reroll)
        {
            if (answered)
            {
                return;
            }

            answered = true;
            dialog.QueueFree();
            Change(() => SetGender(gender, reroll));
        }

        dialog.Confirmed += () => Answer(true);
        dialog.Canceled += () => Answer(false);
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(640, 0));
    }

    private void SetGender(Gender gender, bool reroll)
    {
        _c.Gender = gender;
        if (reroll)
        {
            _c.Appearance = AppearanceGenerator.Random(_rng, People.Appearance, gender, _c.AgeOn(Today), _c.HomeUf);
            _faceEdited = false;
            _wardrobeEdited = false;
        }
    }

    private GameParty? CurrentPartyFromWorld()
    {
        PartyDraft? draft = _world.Find(_c.PartyId ?? "");
        return draft is null ? null : GameParty.From(draft);
    }

    // ---------- Sorteios ----------

    /// <summary>
    /// Os 3 traços oferecidos. Os já escolhidos ficam sempre entre eles: um traço escolhido fora da lista contaria
    /// para o limite de 2 sem aparecer na tela, e o jogador não conseguiria desmarcá-lo.
    /// </summary>
    private List<string> OfferTraits(IEnumerable<string>? keep = null)
    {
        List<string> offered = (keep ?? []).Distinct().Take(3).ToList();
        offered.AddRange(_rng.Shuffled(People.Traits.Traits.Where(t => t.SelectableAtCreation && !offered.Contains(t.Id)).Select(t => t.Id)).Take(3 - offered.Count));
        return offered;
    }

    private CampaignIdentity NewCampaign()
    {
        GameParty? party = CurrentPartyFromWorld();
        _ballotDraft = null;
        return new CampaignIdentity
        {
            BallotNumber = party is null ? null : $"{party.Number}{_rng.NextInt(1000):000}",
            Slogan = SuggestSlogan(),
            LogoIcon = _rng.Pick(_game.World.Parties.LogoIcons.Icons).Id,
            LogoFont = _rng.Pick(People.Campaign.Fonts).Id,
            LogoPrimaryColor = party?.Colors.Primary ?? "#1D3557",
            LogoSecondaryColor = party?.Colors.Secondary ?? "#F1FAEE",
            Jingle = _rng.Pick(People.Campaign.Jingles).Id,
            PhotoPose = _rng.Pick(People.Campaign.Poses).Id,
            PhotoExpression = Expression.Sorriso,
            PhotoBackground = "background_solido",
        };
    }

    private string SuggestSlogan() => _rng.Pick(People.Campaign.Slogans).Replace("{cidade}", HomeName(), StringComparison.Ordinal);

    private string HomeName() => _game.Municipios.All.FirstOrDefault(m => m.CodigoIbge == _c.HomeMunicipio)?.Nome ?? "";

    private string WriteBiography()
    {
        GameParty? party = CurrentPartyFromWorld();
        ProfessionDefinition? profession = People.Professions.Professions.FirstOrDefault(p => p.Id == _c.ProfessionId);
        string partyName = party?.Name ?? "";
        return People.Biography.Write(_c, new Rng(_game.PendingSeed + 11 + _bioSeed), HomeName(), partyName, profession?.Name.For(_c.Gender) ?? "", Today, _game.World.Parties.NameTerms.GenderOf(partyName));
    }

    /// <summary>Nome do atributo como o jogador lê, com acento.</summary>
    private string AttributeName(AttributeId attribute) => Tr(attribute switch
    {
        AttributeId.Carisma => "Carisma",
        AttributeId.Oratoria => "Oratória",
        AttributeId.Articulacao => "Articulação",
        AttributeId.Gestao => "Gestão",
        AttributeId.Integridade => "Integridade",
        _ => attribute.ToString(),
    });

    private void RecomputeStats()
    {
        SocialOriginDefinition origin = People.Origins.Get(_c.SocialOrigin!);
        ProfessionDefinition profession = People.Professions.Professions.First(p => p.Id == _c.ProfessionId);
        PersonGenerator.ApplyStartingStats(_c, new Rng(_game.PendingSeed + 7), People, origin, profession);
    }

    private void RandomizeAll()
    {
        for (int tab = 0; tab < 5; tab++)
        {
            RandomizeTab(tab);
        }
    }

    private void RandomizeTab(int tab)
    {
        int age = _c.AgeOn(Today);
        switch (tab)
        {
            case 0:
                var wardrobe = _c.Appearance!.Wardrobe;
                _c.Appearance = AppearanceGenerator.Random(_rng, People.Appearance, _c.Gender, age, _c.HomeUf) with { Wardrobe = wardrobe };
                _faceEdited = false;
                break;
            case 1:
                _c.Appearance = _c.Appearance! with
                {
                    Wardrobe = Enum.GetValues<WardrobeContext>().ToDictionary(c => c, c => AppearanceGenerator.Outfit(_rng, People.Appearance, _c.Gender, c)),
                };
                _wardrobeEdited = false;
                break;
            case 2:
                Character fresh = PersonGenerator.Generate(_rng, People, new PersonRequest("jogador", _c.HomeUf!, _c.HomeMunicipio, Today, PartyId: _c.PartyId, Ages: People.Origins.StartingAge));
                fresh.Appearance = _c.Appearance;
                fresh.Campaign = _c.Campaign;
                _c = fresh;
                _offeredTraits = OfferTraits();
                _c.TraitIds.Clear();
                _c.TraitIds.AddRange(_offeredTraits.Take(2));
                RecomputeStats();
                _c.Biography = WriteBiography();
                break;
            case 3:
                _c.Campaign = NewCampaign();
                break;
            default:
                if (!_ownParty)
                {
                    _c.PartyId = _rng.Pick(_world.Parties.Where(p => p.Id != _world.PlayerPartyId).ToList()).Id;
                    ApplyPartyNumber();
                }

                break;
        }
    }

    private void ApplyPartyNumber()
    {
        GameParty? party = CurrentPartyFromWorld();
        if (party is not null && _c.Campaign is not null)
        {
            string suffix = CandidateNumbers.SuffixOf(_c.Campaign.BallotNumber, Office.Vereador);
            _c.Campaign.BallotNumber = $"{party.Number}{(suffix.Length > 0 ? suffix : $"{_rng.NextInt(1000):000}")}";
        }
    }

    // ---------- Abas ----------

    private void BuildTabs()
    {
        int current = Math.Max(0, _tabs.CurrentTab);
        Ui.Clear(_tabs);
        BuildAppearanceTab(Tab("Aparência"));
        BuildWardrobeTab(Tab("Vestuário"));
        BuildIdentityTab(Tab("Identidade"));
        BuildCampaignTab(Tab("Campanha"));
        BuildPartyTab(Tab("Partido"));
        string[] tabTips =
        [
            Tip("Aba Aparência: rosto, corpo, cabelo, marcas e acessórios.", "muda o retrato, o santinho e o material de campanha; aparência nunca altera votos nem atributos.", "nada."),
            Tip("Aba Vestuário: a roupa de cada ocasião.", "escolhe o que o personagem veste no gabinete, na rua, no debate, no dia a dia e nas festas regionais; nunca altera votos nem atributos.", "nada."),
            Tip("Aba Identidade: nome, gênero, idade, cidade, origem, profissão, família, traços e biografia.", "a origem, a profissão e os traços definem os atributos de partida; a cidade natal é o palco da primeira campanha.", "nada."),
            Tip("Aba Campanha: número de urna, slogan, logotipo, jingle e foto.", "monta a identidade da campanha que aparece no santinho, na urna e no material.", "nada."),
            Tip("Aba Partido: filiar-se a um partido do mundo ou criar o seu.", "o partido dá o começo do número de urna e pesa no voto de quem já gosta dele; a lista mostra a força de cada um na sua cidade.", "nada."),
        ];
        for (int i = 0; i < _tabs.GetTabCount() && i < tabTips.Length; i++)
        {
            _tabs.SetTabTooltip(i, tabTips[i]);
        }

        _tabs.CurrentTab = Math.Min(current, _tabs.GetTabCount() - 1);
        Callable.From(LinkTabs).CallDeferred();
    }

    /// <summary>Tooltip em três partes, com as três partes traduzidas.</summary>
    private string Tip(string what, string does, string cost) => Palette.Tip(Tr(what), Tr(does), Tr(cost));

    /// <summary>Tooltip dos controles de aparência: o que muda e a regra de que aparência não altera votos.</summary>
    private string LookTip(string label) => Palette.Tip(
        string.Format(Tr("{0} do personagem."), Tr(label)),
        Tr("muda só a aparência no retrato, no santinho e no material de campanha; aparência nunca altera votos nem atributos."),
        Tr("nada; Desfazer volta atrás."));

    /// <summary>Os termos do glossário nas abas viram link (as abas são montadas de novo a cada mudança).</summary>
    private void LinkTabs()
    {
        if (IsInstanceValid(_tabs) && _tabs.IsInsideTree())
        {
            Glossary.LinkTerms(_tabs);
        }
    }

    private VBoxContainer Tab(string title)
    {
        var scroll = new ScrollContainer { Name = Tr(title), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _tabs.AddChild(scroll);
        VBoxContainer column = Ui.Column(scroll, 6);
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        return column;
    }

    private void PartField(Node parent, string label, string category, string? current, Action<string?> set, bool allowNone = false)
    {
        HBoxContainer row = Ui.Field(parent, label);
        var items = People.Appearance.Catalog.InCategory(category).Select(p => (p.Id, p.Name)).ToList();
        if (allowNone)
        {
            items.Insert(0, ("", TranslationServer.Translate("Nenhum")));
        }

        Ui.Options(row, items, current ?? "", id => set(id.Length == 0 ? null : id), translate: false).TooltipText = LookTip(label);
    }

    private void SliderField(Node parent, string label, double value, Action<double> set, double min = 0, double max = 1)
    {
        HBoxContainer row = Ui.Field(parent, label);
        HSlider slider = Ui.Slider(row, value, _ => { }, min, max);
        slider.TooltipText = LookTip(label);
        slider.DragEnded += changed =>
        {
            if (changed)
            {
                set(slider.Value);
            }
        };
    }

    private void ColorField(Node parent, string label, string hex, Action<string> set)
    {
        HBoxContainer row = Ui.Field(parent, label);
        Ui.ColorPicker(row, hex, set).TooltipText = LookTip(label);
    }

    private void BuildAppearanceTab(VBoxContainer t)
    {
        Appearance a = _c.Appearance!;
        PartField(t, "Corpo", "body", a.Body, v => SetAppearance(x => x with { Body = v! }));
        SliderField(t, "Largura dos ombros", a.ShoulderWidth, v => SetAppearance(x => x with { ShoulderWidth = v }));
        HBoxContainer skin = Ui.Field(t, "Tom de pele");
        int tone = Math.Max(0, People.Appearance.Palettes.Skin.IndexOf(a.SkinColor));
        Ui.Slider(skin, tone, v => SetAppearance(x => x with { SkinColor = People.Appearance.Palettes.Skin[(int)v] }), 0, 23, 1).TooltipText = LookTip("Tom de pele (na escala de tons)");
        Ui.ColorPicker(skin, a.SkinColor, c => SetAppearance(x => x with { SkinColor = c })).TooltipText = LookTip("Tom de pele (cor livre)");
        Ui.Options(Ui.Field(t, "Subtom"), Enum.GetValues<SkinUndertone>().Select(u => (u.ToString(), u switch { SkinUndertone.Quente => "Quente", SkinUndertone.Neutro => "Neutro", _ => "Frio" })), a.Undertone.ToString(), u => SetAppearance(x => x with { Undertone = Enum.Parse<SkinUndertone>(u) })).TooltipText = LookTip("Subtom da pele");
        PartField(t, "Rosto", "face", a.Face, v => SetAppearance(x => x with { Face = v! }));
        SliderField(t, "Maxilar", a.Jaw, v => SetAppearance(x => x with { Jaw = v }));
        SliderField(t, "Queixo", a.Chin, v => SetAppearance(x => x with { Chin = v }));
        SliderField(t, "Maçãs do rosto", a.Cheekbones, v => SetAppearance(x => x with { Cheekbones = v }));
        PartField(t, "Olhos", "eyes", a.Eyes, v => SetAppearance(x => x with { Eyes = v! }));
        SliderField(t, "Tamanho dos olhos", a.EyeSize, v => SetAppearance(x => x with { EyeSize = v }));
        ColorField(t, "Cor dos olhos", a.EyeColor, c => SetAppearance(x => x with { EyeColor = c }));
        PartField(t, "Sobrancelhas", "brows", a.Brows, v => SetAppearance(x => x with { Brows = v! }));
        SliderField(t, "Espessura das sobrancelhas", a.BrowThickness, v => SetAppearance(x => x with { BrowThickness = v }));
        PartField(t, "Nariz", "nose", a.Nose, v => SetAppearance(x => x with { Nose = v! }));
        SliderField(t, "Largura do nariz", a.NoseWidth, v => SetAppearance(x => x with { NoseWidth = v }));
        PartField(t, "Boca", "mouth", a.Mouth, v => SetAppearance(x => x with { Mouth = v! }));
        PartField(t, "Orelhas", "ears", a.Ears, v => SetAppearance(x => x with { Ears = v! }));
        PartField(t, "Cabelo", "hair", a.Hair, v => SetAppearance(x => x with { Hair = v! }));
        ColorField(t, "Cor do cabelo", a.HairColor, c => SetAppearance(x => x with { HairColor = c, BrowColor = c }));
        SliderField(t, "Grisalho", a.HairGray, v => SetAppearance(x => x with { HairGray = v }));
        PartField(t, "Barba", "beard", a.Beard, v => SetAppearance(x => x with { Beard = v }), allowNone: true);
        PartField(t, "Bigode", "mustache", a.Mustache, v => SetAppearance(x => x with { Mustache = v }), allowNone: true);
        ColorField(t, "Cor dos pelos faciais", a.FacialHairColor, c => SetAppearance(x => x with { FacialHairColor = c }));
        SliderField(t, "Sardas", a.Freckles, v => SetAppearance(x => x with { Freckles = v }));
        SliderField(t, "Rugas", a.Wrinkles, v => SetAppearance(x => x with { Wrinkles = v }));
        SliderField(t, "Olheiras", a.EyeBags, v => SetAppearance(x => x with { EyeBags = v }));
        PartField(t, "Cicatriz", "scar", a.Scar, v => SetAppearance(x => x with { Scar = v }), allowNone: true);
        HBoxContainer flags = Ui.Field(t, "Marcas");
        var moles = flags.Add(new CheckBox { Text = Tr("Pintas"), ButtonPressed = a.Moles, TooltipText = LookTip("Pintas") });
        moles.Toggled += on => SetAppearance(x => x with { Moles = on });
        var dimples = flags.Add(new CheckBox { Text = Tr("Covinhas"), ButtonPressed = a.Dimples, TooltipText = LookTip("Covinhas") });
        dimples.Toggled += on => SetAppearance(x => x with { Dimples = on });
        HBoxContainer lips = Ui.Field(t, "Batom");
        Ui.ColorPicker(lips, a.LipstickColor ?? People.Appearance.Palettes.MakeupLipstick[0], c => SetAppearance(x => x with { LipstickColor = c })).TooltipText = LookTip("Cor do batom");
        Ui.Slider(lips, a.LipstickIntensity, v => SetAppearance(x => x with { LipstickIntensity = v, LipstickColor = x.LipstickColor ?? People.Appearance.Palettes.MakeupLipstick[0] })).TooltipText = LookTip("Intensidade do batom");
        HBoxContainer shadow = Ui.Field(t, "Sombra");
        Ui.ColorPicker(shadow, a.EyeshadowColor ?? People.Appearance.Palettes.MakeupEyeshadow[0], c => SetAppearance(x => x with { EyeshadowColor = c })).TooltipText = LookTip("Cor da sombra");
        Ui.Slider(shadow, a.EyeshadowIntensity, v => SetAppearance(x => x with { EyeshadowIntensity = v, EyeshadowColor = x.EyeshadowColor ?? People.Appearance.Palettes.MakeupEyeshadow[0] })).TooltipText = LookTip("Intensidade da sombra");
        SliderField(t, "Delineado", a.EyelinerIntensity, v => SetAppearance(x => x with { EyelinerIntensity = v }));
        PartField(t, "Óculos", "glasses", a.Glasses, v => SetAppearance(x => x with { Glasses = v }), allowNone: true);
        PartField(t, "Brincos", "earrings", a.Earrings, v => SetAppearance(x => x with { Earrings = v }), allowNone: true);
        PartField(t, "Piercing", "piercing", a.Piercing, v => SetAppearance(x => x with { Piercing = v }), allowNone: true);
        PartField(t, "Colar", "necklace", a.Necklace, v => SetAppearance(x => x with { Necklace = v }), allowNone: true);
        var pin = Ui.Field(t, "Broche").Add(new CheckBox { Text = Tr("Broche do partido na lapela"), ButtonPressed = a.PartyPin, TooltipText = LookTip("Broche do partido na lapela") });
        pin.Toggled += on => SetAppearance(x => x with { PartyPin = on });
    }

    private void BuildWardrobeTab(VBoxContainer t)
    {
        Ui.Label(t, "Roupas e aparência nunca alteram atributos nem votos.", color: Ui.Muted);
        GameParty? party = CurrentPartyFromWorld();
        foreach (WardrobeContext context in Enum.GetValues<WardrobeContext>())
        {
            OutfitChoice outfit = _c.Appearance!.Wardrobe[context];
            HBoxContainer row = Ui.Field(t, ContextName(context));
            var items = People.Appearance.Catalog.InCategory("outfit").Where(p => p.Contexts.Contains(context)).Select(p => (p.Id, p.Name));
            string where = ContextName(context);
            Ui.Options(row, items, outfit.PartId, id => SetOutfit(context, o => o with { PartId = id }), translate: false).TooltipText = LookTip(string.Format(Tr("Roupa: {0}"), where));
            Ui.ColorPicker(row, outfit.PrimaryColor, c => SetOutfit(context, o => o with { PrimaryColor = c })).TooltipText = LookTip(string.Format(Tr("Cor principal da roupa: {0}"), where));
            Ui.ColorPicker(row, outfit.SecondaryColor, c => SetOutfit(context, o => o with { SecondaryColor = c })).TooltipText = LookTip(string.Format(Tr("Cor secundária da roupa: {0}"), where));
            if (party is not null)
            {
                Ui.Button(row, "Cor do partido", () => Change(() => SetOutfitRaw(context, o => o with { PrimaryColor = party.Colors.Primary, SecondaryColor = party.Colors.Secondary })),
                    Palette.Tip(string.Format(Tr("Pinta a roupa ({0}) com as cores do {1}."), where, party.Sigla), Tr("troca as duas cores da roupa pelas do partido; aparência nunca altera votos nem atributos."), Tr("nada; Desfazer volta atrás.")));
            }
        }
    }

    private static string ContextName(WardrobeContext c) => TranslationServer.Translate(c switch
    {
        WardrobeContext.Institucional => "Institucional",
        WardrobeContext.Campanha => "Campanha de rua",
        WardrobeContext.Debate => "Debate e TV",
        WardrobeContext.Casual => "Casual",
        _ => "Regional",
    });

    private void SetOutfit(WardrobeContext context, Func<OutfitChoice, OutfitChoice> change) => Edit(() => SetOutfitRaw(context, change));

    private void SetOutfitRaw(WardrobeContext context, Func<OutfitChoice, OutfitChoice> change)
    {
        var wardrobe = new Dictionary<WardrobeContext, OutfitChoice>(_c.Appearance!.Wardrobe) { [context] = change(_c.Appearance.Wardrobe[context]) };
        _c.Appearance = _c.Appearance with { Wardrobe = wardrobe };
        _wardrobeEdited = true;
    }

    private void BuildIdentityTab(VBoxContainer t)
    {
        var civil = Ui.Field(t, "Nome civil").Add(new LineEdit { Text = _c.CivilName, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        civil.TextChanged += s => Edit(() => _c.CivilName = s);
        civil.TooltipText = Tip("Nome civil do personagem.", "aparece nos documentos, na biografia e no noticiário; é obrigatório.", "nada.");
        HBoxContainer ballotRow = Ui.Field(t, "Nome de urna");
        var ballot = ballotRow.Add(new LineEdit { Text = _c.BallotName, SizeFlagsHorizontal = SizeFlags.ExpandFill, MaxLength = 30 });
        ballot.TextChanged += s => Edit(() => _c.BallotName = s);
        ballot.TooltipText = Tip("Nome de urna: como o eleitor conhece você.", "aparece na urna, no santinho, no mapa e na apuração; é obrigatório.", "nada.");
        Ui.Button(ballotRow, "Sugerir", () => Change(() =>
            _c.BallotName = PersonGenerator.BallotName(_rng, People, _c.CivilName.Split(' ')[0], _c.CivilName.Split(' ')[^1], _c.ProfessionId ?? "", _c.Gender)),
            Tip("Sugere um nome de urna.", "monta um a partir do nome civil e da profissão, como os candidatos costumam fazer.", "nada; Desfazer volta atrás."));

        var gender = Ui.Options(Ui.Field(t, "Gênero e pronomes"), Enum.GetValues<Gender>().Select(g => (g.ToString(), g switch { Gender.Masculino => "Masculino (ele)", Gender.Feminino => "Feminino (ela)", _ => "Não binário (elu)" })), _c.Gender.ToString(), g => RequestGender(Enum.Parse<Gender>(g)));
        gender.Name = "Genero";
        gender.TooltipText = Tip("Gênero e pronomes do personagem.", "muda os pronomes em todos os textos do jogo e sorteia de novo a aparência com os pesos do gênero; se você já editou a aparência, o jogo pergunta antes. Nunca altera votos nem atributos.", "nada.");
        Ui.Label(t, "Mudar o gênero sorteia de novo a aparência e as roupas; se você já as editou, o jogo pergunta antes.", 16, Ui.Muted);

        AgeRange ages = People.Origins.StartingAge;
        HBoxContainer ageRow = Ui.Field(t, "Idade");
        var age = ageRow.Add(new SpinBox { MinValue = ages.Min, MaxValue = ages.Max, Value = _c.AgeOn(Today) });
        age.ValueChanged += v => Change(() => SetAge((int)v, _c.BirthDate.Month));
        age.TooltipText = Palette.Tip(string.Format(Tr("Idade no começo do jogo, de {0} a {1} anos."), ages.Min, ages.Max), Tr("muda as profissões e a família possíveis e envelhece o rosto; sem 18 anos no registro da primeira eleição, a carreira começa na eleição seguinte."), Tr("nada."));
        Ui.Label(ageRow, "Aniversário em", color: Ui.Muted);
        Ui.Options(ageRow, Enumerable.Range(1, 12).Select(m => (m.ToString(CultureInfo.InvariantCulture), MonthName(m))), _c.BirthDate.Month.ToString(CultureInfo.InvariantCulture),
            m => Change(() => SetAge(_c.AgeOn(Today), int.Parse(m, CultureInfo.InvariantCulture))), translate: false).TooltipText =
            Tip("Mês do aniversário.", "decide se quem tem 17 anos chega aos 18 até o prazo do registro de candidatura (15 de agosto).", "nada.");
        foreach (string warning in AgeWarnings())
        {
            Ui.Paragraph(t, warning, 760, 15).AddThemeColorOverride("font_color", Ui.Warning);
        }

        HBoxContainer homeRow = Ui.Field(t, "Cidade natal");
        Ui.Value(homeRow, $"{HomeName()} ({_c.HomeUf})");
        Ui.Button(homeRow, "Escolher cidade", () => AddChild(new MunicipioPicker(m => Change(() =>
        {
            _c.HomeMunicipio = m.CodigoIbge;
            _c.HomeUf = m.Uf;
            RecomputeStats();
            _c.Biography = WriteBiography();
        }))), Tip("Abre o seletor de municípios.", "a cidade natal é o palco da primeira campanha; o seletor mostra população, eleitorado, vagas na Câmara e a dificuldade sugerida de cada uma.", "nada."));
        Ui.Label(t, "A cidade natal é também o palco da primeira campanha.", color: Ui.Muted);

        Ui.Options(Ui.Field(t, "Origem social"), People.Origins.Origins.Select(o => (o.Id, o.Name.For(_c.Gender))), _c.SocialOrigin, o => Change(() => { _c.SocialOrigin = o; RecomputeStats(); }), translate: false).TooltipText =
            Tip("Origem social do personagem.", "define parte dos atributos e do dinheiro de partida (os números aparecem na linha de atributos, mais abaixo).", "nada.");
        // Profissões indisponíveis na idade aparecem desativadas, com a idade mínima.
        int currentAge = _c.AgeOn(Today);
        var professions = People.Professions.Professions;
        var professionOptions = Ui.Options(
            Ui.Field(t, "Profissão anterior"),
            professions.Select(p => (p.Id, p.AvailableAt(currentAge) ? p.Name.For(_c.Gender) : string.Format(Tr("{0} (a partir de {1} anos)"), p.Name.For(_c.Gender), p.MinAge))),
            _c.ProfessionId,
            p => Change(() => { _c.ProfessionId = p; RecomputeStats(); }),
            translate: false);
        professionOptions.TooltipText = Tip("Profissão antes da política.", "define parte dos atributos de partida e entra na biografia; as que pedem idade mínima aparecem bloqueadas na lista, com a idade.", "nada.");
        for (int i = 0; i < professions.Count; i++)
        {
            professionOptions.SetItemDisabled(i, !professions[i].AvailableAt(currentAge));
        }

        Ui.Options(Ui.Field(t, "Religião"), new[] { ("", "Não declarar") }.Concat(Religions.Select(r => (r, r switch { "catolica" => "Católica", "evangelica" => "Evangélica", "outras" => "Outra religião", _ => "Sem religião" }))), _c.Religion ?? "", r => Edit(() => _c.Religion = r.Length == 0 ? null : r)).TooltipText =
            Tip("Religião do personagem, ou não declarar.", "entra na biografia e no perfil do personagem.", "nada.");
        FamilyConfig family = People.Family;
        var statuses = Enum.GetValues<MaritalStatus>();
        var marital = Ui.Options(Ui.Field(t, "Estado civil"), statuses.Select(m => (m.ToString(), m switch { MaritalStatus.Solteiro => "Solteiro(a)", MaritalStatus.Casado => "Casado(a)", MaritalStatus.UniaoEstavel => "União estável", MaritalStatus.Divorciado => "Divorciado(a)", _ => "Viúvo(a)" })), _c.MaritalStatus.ToString(),
            m => Change(() => { _c.MaritalStatus = Enum.Parse<MaritalStatus>(m); FamilyRules.Generate(_rng, People, _c, Today); }));
        marital.TooltipText = Tip("Estado civil.", "gera de novo a família do personagem; as opções que a idade não permite aparecem bloqueadas na lista.", "nada.");
        for (int i = 0; i < statuses.Length; i++)
        {
            marital.SetItemDisabled(i, !FamilyRules.Allows(family, statuses[i], currentAge));
        }

        int maxChildren = FamilyRules.MaxChildren(family, currentAge);
        HBoxContainer childrenRow = Ui.Field(t, "Filhos");
        var children = childrenRow.Add(new SpinBox { MinValue = 0, MaxValue = maxChildren, Value = _c.Children });
        children.ValueChanged += v => Change(() => { _c.Children = (int)v; FamilyRules.Generate(_rng, People, _c, Today); });
        children.TooltipText = Palette.Tip(Tr("Número de filhos."), string.Format(Tr("gera de novo a família; até {0} aos {1} anos."), maxChildren, currentAge), Tr("nada."));
        Ui.Label(childrenRow, string.Format(Tr("até {0} aos {1} anos"), maxChildren, currentAge), color: Ui.Muted);
        if (_c.Family.Count > 0)
        {
            string members = string.Join(" · ", _c.Family.Select(f => string.Format(Tr(f.Relation == FamilyRelation.Conjuge ? "{0}, cônjuge, {1} anos" : "{0}, {1} anos"), f.Name, f.AgeOn(Today))));
            Ui.Paragraph(t, members, 760, 15);
        }

        Ui.Label(t, "Traços de personalidade: escolha 2 de 3", 20);
        if (_c.TraitIds.Any(id => !_offeredTraits.Contains(id)))
        {
            // Desfazer e presets podem trazer traços de outra oferta: eles voltam para a lista, marcados.
            _offeredTraits = OfferTraits(_c.TraitIds);
        }

        foreach (string id in _offeredTraits)
        {
            TraitDefinition trait = People.Traits.Traits.First(x => x.Id == id);
            var box = t.Add(new CheckBox { Text = $"{trait.Name.For(_c.Gender)} — {trait.Description}", ButtonPressed = _c.TraitIds.Contains(id) });
            box.TooltipText = Palette.Tip(string.Format(Tr("Traço de personalidade: {0}."), trait.Name.For(_c.Gender)), Tr("marca ou desmarca o traço; são exatamente 2 de 3, e eles mudam os atributos de partida."), Tr("nada."));
            box.Toggled += on => Change(() =>
            {
                if (on && !_c.TraitIds.Contains(id))
                {
                    _c.TraitIds.Add(id);
                }
                else if (!on)
                {
                    _c.TraitIds.Remove(id);
                }

                RecomputeStats();
            });
        }

        Ui.Button(t, "Outras opções de traços", () => Change(() => { _offeredTraits = OfferTraits(); _c.TraitIds.Clear(); RecomputeStats(); }), Tip("Sorteia outros 3 traços para escolher.", "troca as opções de traço e desmarca os escolhidos.", "nada; Desfazer volta atrás."));
        Ui.Value(t, string.Join(" · ", Enum.GetValues<AttributeId>().Select(a => $"{AttributeName(a)}: {_c.Attributes.Get(a)}")));

        HBoxContainer bioRow = Ui.Field(t, "Biografia");
        Ui.Button(bioRow, "Gerar de novo", () => Change(() => { _bioSeed++; _bioEdited = false; }), Tip("Escreve outra biografia.", "monta um texto novo com a cidade, a origem, a profissão e o partido; a biografia sugerida acompanha as suas escolhas até você editá-la à mão.", "nada; Desfazer volta atrás."));
        var bio = t.Add(new TextEdit { Text = _c.Biography ?? "", CustomMinimumSize = new Vector2(0, 110), WrapMode = TextEdit.LineWrappingMode.Boundary });
        bio.TooltipText = Tip("Biografia do personagem.", "dá para editar à vontade; editada à mão, ela deixa de acompanhar as escolhas até \"Gerar de novo\". Aparece na Enciclopédia, na sua carreira.", "nada.");
        bio.TextChanged += () =>
        {
            if (bio.Text != _c.Biography)
            {
                _bioEdited = true;
                _c.Biography = bio.Text;
            }
        };
    }

    /// <summary>
    /// Idade no início do jogo e mês do aniversário: o mês decide se um personagem de 17 anos chega aos 18 até o
    /// prazo do registro. Ajusta profissão e família ao que a nova idade permite.
    /// </summary>
    private void SetAge(int age, int month)
    {
        DateOnly birth = month == 1 ? new DateOnly(Today.Year - age, 1, 1) : new DateOnly(Today.Year - age - 1, month, 1);
        ReplaceBirth(birth);
        _c.Appearance = AppearanceAging.Apply(_c.Appearance!, age, 0, _c.Gender, People.Appearance);
        if (People.Professions.Professions.FirstOrDefault(p => p.Id == _c.ProfessionId) is { } current && !current.AvailableAt(age))
        {
            _c.ProfessionId = People.Professions.Professions.Where(p => p.AvailableAt(age)).OrderByDescending(p => p.MinAge).First().Id;
            RecomputeStats();
        }

        FamilyRules.Generate(_rng, People, _c, Today);
    }

    private static string MonthName(int month) => TranslationServer.Translate(month switch
    {
        1 => "janeiro", 2 => "fevereiro", 3 => "março", 4 => "abril", 5 => "maio", 6 => "junho",
        7 => "julho", 8 => "agosto", 9 => "setembro", 10 => "outubro", 11 => "novembro", _ => "dezembro",
    });

    /// <summary>Avisos (sem bloquear) quando a idade não permite disputar a primeira eleição ou a prefeitura.</summary>
    private IEnumerable<string> AgeWarnings()
    {
        int year = Planalto.Core.Campaign.RaceSetup.NextMunicipalYear(Today.Year);
        var rules = CandidacyRulesConfig.Default;
        if (!rules.MeetsAge(_c.BirthDate, Office.Vereador, year))
        {
            DateOnly registration = rules.AgeReferenceDate(Office.Vereador, year, new DateOnly(year + 1, 1, 1));
            yield return string.Format(
                Tr("Com essa idade, você não terá 18 anos no prazo do registro de candidatura ({0}) e não poderá disputar a eleição de {1}. A carreira vai começar em {2}, direto na pré-campanha da eleição daquele ano, a primeira que você pode disputar, com você aos {3} anos."),
                registration.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                year,
                CareerStart.AddDays(6).Year,
                _c.AgeOn(CareerStart));
        }
        else if (!rules.MeetsAge(_c.BirthDate, Office.Prefeito, year))
        {
            yield return string.Format(Tr("Na eleição de {0} você poderá disputar para vereador, mas ainda não terá a idade mínima para a prefeitura (21 anos na posse)."), year);
        }
    }

    /// <summary>Segunda-feira em que a carreira começa (pelo Core): mais tarde para quem não tem idade para a primeira eleição.</summary>
    private DateOnly CareerStart => NewGame.StartCalendar(_world.Options.StartYear, _c.BirthDate).WeekStart;

    /// <summary>A carreira começa depois do ano inicial do mundo, porque o personagem ainda não pode disputar a primeira eleição.</summary>
    private bool StartsLater => Planalto.Core.Career.CareerFlow.CareerStartYear(_c.BirthDate, _world.Options.StartYear) != _world.Options.StartYear;

    private void ReplaceBirth(DateOnly birth)
    {
        string json = JsonSerializer.Serialize(_c, JsonData.Options);
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        node["birth_date"] = birth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        _c = node.Deserialize<Character>(JsonData.Options)!;
    }

    private void BuildCampaignTab(VBoxContainer t)
    {
        CampaignIdentity id = _c.Campaign!;
        GameParty? party = CurrentPartyFromWorld();
        HBoxContainer number = Ui.Field(t, "Número de urna");
        Ui.Value(number, party is null ? "--" : party.Number.ToString(CultureInfo.InvariantCulture), 22);
        int suffixDigits = CandidateNumberRulesConfig.Default.SuffixDigitsFor(Office.Vereador);
        var digits = number.Add(new LineEdit
        {
            Name = "NumeroUrna",
            Text = _ballotDraft ?? CandidateNumbers.SuffixOf(id.BallotNumber, Office.Vereador),
            MaxLength = suffixDigits,
            PlaceholderText = new string('0', suffixDigits),
            Alignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(96, 0),
            VirtualKeyboardType = LineEdit.VirtualKeyboardTypeEnum.Number,
            Editable = party is not null,
        });
        digits.AddThemeFontSizeOverride("font_size", 22);
        string digitsTip = Palette.Tip(Tr("Os 3 últimos algarismos do seu número de urna."), Tr("o número completo é o do partido mais estes 3 algarismos; zeros à esquerda e algarismos repetidos valem (000, 111)."), Tr("nada."));
        digits.TooltipText = party is null ? Palette.Blocked(Tr("escolha um partido antes, na aba Partido."), digitsTip) : digitsTip;
        digits.TextChanged += typed =>
        {
            // Só algarismos entram no campo; o resto é conferido pelo Core.
            string clean = new(typed.Where(char.IsAsciiDigit).ToArray());
            if (clean != typed)
            {
                digits.Text = clean;
                digits.CaretColumn = clean.Length;
            }

            Edit(() => TypeBallotSuffix(clean));
        };
        Ui.Label(number, "Vereador: número do partido + 3 algarismos, de 000 a 999.", color: Ui.Muted);
        _ballotIssue = Ui.Paragraph(t, "", 760, 18);
        _ballotIssue.Name = "NumeroUrnaMotivo";
        _ballotIssue.AddThemeColorOverride("font_color", Ui.Warning);
        ShowBallotIssue();

        HBoxContainer sloganRow = Ui.Field(t, "Slogan");
        var slogan = sloganRow.Add(new LineEdit { Text = id.Slogan, SizeFlagsHorizontal = SizeFlags.ExpandFill, MaxLength = 60 });
        slogan.TextChanged += s => Edit(() => id.Slogan = s);
        slogan.TooltipText = Tip("Slogan da campanha, até 60 letras.", "aparece no santinho, na bandeira e na peça para redes.", "nada.");
        Ui.Button(sloganRow, "Sugerir", () => Change(() => id.Slogan = SuggestSlogan()), Tip("Sugere um slogan.", "sorteia um slogan com o nome da sua cidade.", "nada; Desfazer volta atrás."));

        HBoxContainer logo = Ui.Field(t, "Logotipo");
        string material = Tr("aparece no santinho, na bandeira, no adesivo e na peça para redes; a identidade visual nunca altera votos.");
        Ui.Options(logo, _game.World.Parties.LogoIcons.Icons.Select(i => (i.Id, i.Name)), id.LogoIcon, v => Edit(() => id.LogoIcon = v), translate: false).TooltipText = Palette.Tip(Tr("Símbolo do logotipo da campanha."), material, Tr("nada."));
        Ui.Options(logo, People.Campaign.Fonts.Select(f => (f.Id, f.Name)), id.LogoFont, v => Edit(() => id.LogoFont = v), translate: false).TooltipText = Palette.Tip(Tr("Letra do logotipo da campanha."), material, Tr("nada."));
        Ui.ColorPicker(logo, id.LogoPrimaryColor, c => Edit(() => id.LogoPrimaryColor = c)).TooltipText = Palette.Tip(Tr("Cor principal do logotipo."), material, Tr("nada."));
        Ui.ColorPicker(logo, id.LogoSecondaryColor, c => Edit(() => id.LogoSecondaryColor = c)).TooltipText = Palette.Tip(Tr("Cor secundária do logotipo."), material, Tr("nada."));
        if (party is not null)
        {
            Ui.Button(logo, "Cores do partido", () => Change(() => { id.LogoPrimaryColor = party.Colors.Primary; id.LogoSecondaryColor = party.Colors.Secondary; }),
                Palette.Tip(string.Format(Tr("Usa as cores do {0} no logotipo."), party.Sigla), Tr("troca as duas cores do logotipo pelas do partido."), Tr("nada; Desfazer volta atrás.")));
        }

        Ui.Options(Ui.Field(t, "Jingle"), People.Campaign.Jingles.Select(j => (j.Id, j.Name)), id.Jingle, v => Edit(() => id.Jingle = v), translate: false).TooltipText =
            Tip("Jingle da campanha.", "fica na identidade da campanha, junto do slogan e do logotipo; nunca altera votos.", "nada.");
        Ui.Options(Ui.Field(t, "Pose da foto"), People.Campaign.Poses.Select(p => (p.Id, p.Name)), id.PhotoPose, v => Edit(() => id.PhotoPose = v), translate: false).TooltipText =
            Tip("Pose da foto oficial.", "a foto aparece no santinho e no material de campanha.", "nada.");
        Ui.Options(Ui.Field(t, "Expressão da foto"), Enum.GetValues<Expression>().Select(e => (e.ToString(), ExpressionName(e))), id.PhotoExpression.ToString(), v => Edit(() => id.PhotoExpression = Enum.Parse<Expression>(v))).TooltipText =
            Tip("Expressão do rosto na foto oficial.", "a foto aparece no santinho e no material de campanha.", "nada.");
        Ui.Options(Ui.Field(t, "Fundo da foto"), People.Appearance.Catalog.InCategory("background").Select(b => (b.Id, b.Name)), id.PhotoBackground, v => Edit(() => id.PhotoBackground = v), translate: false).TooltipText =
            Tip("Fundo da foto oficial.", "a foto aparece no santinho e no material de campanha.", "nada.");
    }

    /// <summary>Aplica os dígitos digitados se formam um número de urna válido; senão guarda o rascunho e mantém o número anterior.</summary>
    private void TypeBallotSuffix(string typed)
    {
        GameParty? party = CurrentPartyFromWorld();
        if (party is null || _c.Campaign is null)
        {
            return;
        }

        if (CandidateNumbers.TryParseSuffix(typed, Office.Vereador, party.Number, OccupiedBallotSuffixes(), out CandidateNumber number) is null)
        {
            _c.Campaign.BallotNumber = number.Text;
            _ballotDraft = null;
        }
        else
        {
            _ballotDraft = typed;
        }
    }

    /// <summary>
    /// Finais já ocupados no partido, na cidade, para vereador. Na criação ainda não há candidatura registrada: o Core
    /// reserva primeiro o número do jogador e só depois sorteia, entre os livres, os dos colegas de chapa.
    /// </summary>
    private static IReadOnlyCollection<int> OccupiedBallotSuffixes() => [];

    /// <summary>Motivo de o número de urna digitado não valer; null se vale.</summary>
    private string? BallotProblem(bool detail = false)
    {
        GameParty? party = CurrentPartyFromWorld();
        if (party is null || _c.Campaign is null)
        {
            return null;
        }

        string typed = _ballotDraft ?? CandidateNumbers.SuffixOf(_c.Campaign.BallotNumber, Office.Vereador);
        return CandidateNumbers.TryParseSuffix(typed, Office.Vereador, party.Number, OccupiedBallotSuffixes(), out _) switch
        {
            null => null,
            CandidateNumberIssue.JaEmUso => string.Format(Tr("Número de urna: {0}{1} já é de outra candidatura do {2}. Escolha outros 3 algarismos."), party.Number, typed, party.Sigla),
            CandidateNumberIssue.NumeroDePartidoInvalido => Tr("Número de urna: o partido precisa de um número de 10 a 99."),
            _ when detail => string.Format(Tr("Número de urna: digite exatamente 3 algarismos depois do {0} do partido (zeros à esquerda valem, como 007). Enquanto isso, o número continua {1}."), party.Number, _c.Campaign.BallotNumber),
            _ => Tr("Número de urna: digite exatamente 3 algarismos (aba Campanha)."),
        };
    }

    private void ShowBallotIssue()
    {
        if (_ballotIssue is not null && IsInstanceValid(_ballotIssue))
        {
            string? problem = BallotProblem(detail: true);
            _ballotIssue.Text = problem ?? "";
            _ballotIssue.Visible = problem is not null;
        }
    }

    public static string ExpressionName(Expression e) => e switch
    {
        Expression.Neutra => "Neutra",
        Expression.Sorriso => "Sorriso",
        Expression.SorrisoLargo => "Sorriso largo",
        Expression.Preocupado => "Preocupada",
        Expression.Bravo => "Brava",
        Expression.Triste => "Triste",
        Expression.Surpreso => "Surpresa",
        _ => "Cansada",
    };

    /// <summary>Força dos partidos no município escolhido, a mesma que a campanha vai usar (semente do mundo).</summary>
    private IReadOnlyDictionary<string, double>? LocalStrength()
    {
        if (_c.HomeMunicipio is not int ibge)
        {
            return null;
        }

        var parties = Planalto.Core.Campaign.CampaignContext.ElectoralParties(_world.Parties.Select(GameParty.From), _game.World.Parties.Generator.SizeShareWeights);
        var local = _world.Preset.Scenario == "brasil_2026" ? _game.Brasil2026?.Municipio(ibge) : null;
        return Planalto.Core.Campaign.CampaignContext.PartyStrength(_game.PendingSeed, ibge, _game.Electorate, parties, local);
    }

    private void BuildPartyTab(VBoxContainer t)
    {
        HBoxContainer mode = Ui.Row(t);
        var join = mode.Add(new CheckBox { Text = Tr("Filiar-se a um partido existente"), ButtonPressed = !_ownParty, ButtonGroup = new ButtonGroup() });
        join.TooltipText = Tip("Filia o personagem a um partido do mundo.", "mostra os partidos ordenados pela força na sua cidade; o partido dá o começo do número de urna e pesa no voto de quem já gosta dele.", "nada.");
        var create = mode.Add(new CheckBox { Text = Tr("Criar meu partido"), ButtonPressed = _ownParty, ButtonGroup = join.ButtonGroup });
        create.TooltipText = Tip("Cria um partido seu, que entra no mundo.", "você escolhe nome, sigla, número, família, posição, cores e estatuto; o partido novo começa nanico, sem força em nenhuma cidade.", "nada; a eleição fica bem mais difícil.");
        join.Pressed += () => Change(() =>
        {
            _ownParty = false;
            _world.RemovePlayerParty();
            _c.PartyId = _world.Parties[0].Id;
            ApplyPartyNumber();
        });
        create.Pressed += () => Change(() =>
        {
            _ownParty = true;
            PartyDraft? own = _world.AddPlayerParty(_rng);
            if (own is null)
            {
                _ownParty = false;
                return;
            }

            own.Charter = new PartyCharter(PartyDiscipline.Moderada, InternalDemocracy.ConvencoesAbertas, PartyStance.Independente, SuggestSlogan());
            _c.PartyId = own.Id;
            ApplyPartyNumber();
        });

        if (!_ownParty)
        {
            // Força de cada partido na cidade escolhida: sem ela, a campanha de vereador fica muito difícil.
            IReadOnlyDictionary<string, double>? local = LocalStrength();
            if (local is not null)
            {
                Ui.Label(t, string.Format(Tr("Ordenados pela força na cidade ({0}). Partido sem força local torna a eleição muito difícil."), HomeName()), 16, Ui.Muted);
            }

            foreach (PartyDraft p in local is null ? _world.Parties : _world.Parties.OrderByDescending(p => local.GetValueOrDefault(p.Id)).ToList())
            {
                HBoxContainer row = Ui.Row(t, 10);
                var badge = row.Add(new PartyBadge(36));
                badge.Set(p.LogoIcon, p.Colors.Primary, p.Colors.Secondary);
                string family = _game.World.Parties.Families.Find(p.FamilyId)?.Name ?? p.FamilyId;
                string strength = local is null ? "" : string.Format(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), " · {0} {1:0.0}%", Tr("força local"), local.GetValueOrDefault(p.Id) * 100);

                // Duas linhas (nome em cima, detalhes embaixo), para a lista não alargar a tela.
                VBoxContainer text = Ui.Column(row, 0);
                var choose = text.Add(new CheckBox { Text = $"{p.Number} · {p.Sigla} — {p.Name}", ButtonPressed = p.Id == _c.PartyId });
                choose.TooltipText = Palette.Tip(
                    string.Format(Tr("{0} — {1}, partido fictício ({2}, {3})."), p.Sigla, p.Name, family, WorldTexts.Size(p.Size)),
                    Tr("filia o personagem a este partido: o número de urna passa a começar pelo número dele, e quem gosta do partido tende a votar em você."),
                    local is null ? Tr("nada.") : string.Format(CultureInfo.GetCultureInfo("pt-BR"), Tr("nada; partido com pouca força local ({0:0.0}% aqui) torna a eleição muito difícil."), local.GetValueOrDefault(p.Id) * 100));
                choose.AddThemeFontSizeOverride("font_size", 18);
                Ui.Label(text, $"{family} · {WorldTexts.Size(p.Size)}{strength}", 15, Ui.Muted);
                choose.Pressed += () => Change(() => { _c.PartyId = p.Id; ApplyPartyNumber(); });
            }

            return;
        }

        PartyDraft party = _world.Find(_world.PlayerPartyId!)!;
        Ui.Label(t, "Seu partido começa nanico: sem fundo partidário relevante, pouco tempo de TV e sem bancada.", color: Ui.Muted);
        var name = Ui.Field(t, "Nome").Add(new LineEdit { Text = party.Name, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        name.TextChanged += s => Edit(() => party.Name = s);
        name.TooltipText = Tip("Nome do seu partido.", "aparece na urna, no santinho e no noticiário; não pode conter o nome de um partido real.", "nada.");
        var sigla = Ui.Field(t, "Sigla").Add(new LineEdit { Text = party.Sigla, MaxLength = 8 });
        sigla.TextChanged += s => Edit(() => party.Sigla = s);
        sigla.TooltipText = Tip("Sigla do seu partido, de 2 a 8 letras.", "aparece no mapa, na apuração e ao lado do seu nome; nunca a de um partido real nem as mesmas letras em outra ordem.", "nada.");
        var number = Ui.Field(t, "Número").Add(new SpinBox { MinValue = 10, MaxValue = 99, Value = party.Number });
        number.ValueChanged += v => Edit(() => { party.Number = (int)v; ApplyPartyNumber(); });
        number.TooltipText = Tip("Número do seu partido, de 10 a 99.", "é o começo do seu número de urna; não pode repetir o de outro partido.", "nada.");
        Ui.Options(Ui.Field(t, "Família ideológica"), _game.World.Parties.Families.Families.Select(f => (f.Id, f.Name)), party.FamilyId, f => Change(() =>
        {
            party.FamilyId = f;
            party.Position = new PartyGenerator(_game.World.Parties).GeneratePosition(_rng, _game.World.Parties.Families.Get(f));
        }), translate: false).TooltipText = Tip("Família ideológica do seu partido.", "sorteia de novo a posição do partido dentro da faixa da família.", "nada.");
        HBoxContainer axes = Ui.Field(t, "Posição (econ., costumes)");
        Ui.Slider(axes, party.Position.Economic, v => Edit(() => party.Position = party.Position with { Economic = (int)v }), -100, 100, 1).TooltipText =
            Tip("Posição do partido no eixo econômico, de −100 a +100.", "pesa no voto de cada grupo de eleitores, conforme o que ele pensa sobre economia; fora da faixa da família é erro.", "nada.");
        Ui.Slider(axes, party.Position.Social, v => Edit(() => party.Position = party.Position with { Social = (int)v }), -100, 100, 1).TooltipText =
            Tip("Posição do partido no eixo de costumes, de −100 a +100.", "pesa no voto de cada grupo de eleitores, conforme o que ele pensa sobre costumes; fora da faixa da família é erro.", "nada.");
        HBoxContainer visual = Ui.Field(t, "Identidade visual");
        Ui.ColorPicker(visual, party.Colors.Primary, c => Edit(() => party.Colors = party.Colors with { Primary = c })).TooltipText = Tip("Cor principal do seu partido.", "pinta o partido no mapa, na legenda e no logotipo.", "nada.");
        Ui.ColorPicker(visual, party.Colors.Secondary, c => Edit(() => party.Colors = party.Colors with { Secondary = c })).TooltipText = Tip("Cor secundária do seu partido.", "aparece no logotipo e no material de campanha.", "nada.");
        Ui.Options(visual, _game.World.Parties.LogoIcons.Icons.Select(i => (i.Id, i.Name)), party.LogoIcon, i => Edit(() => party.LogoIcon = i), translate: false).TooltipText = Tip("Símbolo do seu partido.", "aparece no logotipo do partido e no material de campanha.", "nada.");
        PartyCharter charter = party.Charter!;
        string statute = Tr("fica gravado no estatuto do partido; nesta versão ainda não muda a simulação.");
        Ui.Options(Ui.Field(t, "Disciplina interna"), Enum.GetValues<PartyDiscipline>().Select(d => (d.ToString(), d switch { PartyDiscipline.Rigida => "Rígida", PartyDiscipline.Moderada => "Moderada", _ => "Frouxa" })), charter.Discipline.ToString(), d => Edit(() => party.Charter = party.Charter! with { Discipline = Enum.Parse<PartyDiscipline>(d) })).TooltipText =
            Palette.Tip(Tr("Disciplina interna: quanto o partido cobra que os filiados votem com ele."), statute, Tr("nada."));
        Ui.Options(Ui.Field(t, "Democracia interna"), Enum.GetValues<InternalDemocracy>().Select(d => (d.ToString(), d == InternalDemocracy.ConvencoesAbertas ? "Convenções abertas" : "Cúpula decide")), charter.Democracy.ToString(), d => Edit(() => party.Charter = party.Charter! with { Democracy = Enum.Parse<InternalDemocracy>(d) })).TooltipText =
            Palette.Tip(Tr("Democracia interna: quem escolhe os candidatos do partido."), statute, Tr("nada."));
        Ui.Options(Ui.Field(t, "Postura"), Enum.GetValues<PartyStance>().Select(s => (s.ToString(), s switch { PartyStance.OposicaoPermanente => "Oposição permanente", PartyStance.Independente => "Independente", _ => "Adesista" })), charter.Stance.ToString(), s => Edit(() => party.Charter = party.Charter! with { Stance = Enum.Parse<PartyStance>(s) })).TooltipText =
            Palette.Tip(Tr("Postura do partido diante dos governos."), statute, Tr("nada."));
        var partySlogan = Ui.Field(t, "Slogan do partido").Add(new LineEdit { Text = charter.Slogan, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        partySlogan.TextChanged += s => party.Charter = party.Charter! with { Slogan = s };
        partySlogan.TooltipText = Tip("Slogan do seu partido.", "fica no estatuto do partido; o slogan da sua campanha é outro, na aba Campanha.", "nada.");
    }

    // ---------- Pré-visualização e validação ----------

    private void RefreshPreview()
    {
        GameParty? party = CurrentPartyFromWorld();
        _portrait.Show(_c.Appearance!, new PortraitOptions(Expression.Sorriso, WardrobeContext.Institucional, party?.Colors.Primary, "background_solido", party?.Colors.Secondary), People.Appearance);
        if (party is not null)
        {
            var card = new CampaignCard(_c, party.Sigla, party.Name, party.Colors.Primary, party.Colors.Secondary, People.Offices.Name(Office.Vereador, _c.Gender), HomeName());
            _santinho.Set(card);
            _materiais.Set(card);
        }

        ShowBallotIssue();
        Ui.Clear(_status);
        var problems = Problems().ToList();
        foreach (string problem in problems)
        {
            Ui.Value(_status, "✕ " + problem).AddThemeColorOverride("font_color", Ui.Error);
        }

        // Aviso que não bloqueia, à vista em qualquer aba: a carreira começa anos depois, com o personagem mais velho.
        int startYear = CareerStart.AddDays(6).Year;
        if (StartsLater)
        {
            Label notice = Ui.Paragraph(_status, string.Format(Tr("Atenção: sem 18 anos no registro da primeira eleição, a carreira começa em {0}, na pré-campanha, com você aos {1} anos."), startYear, _c.AgeOn(CareerStart)), 400);
            notice.Name = "AvisoComecoTardio";
            notice.AddThemeColorOverride("font_color", Ui.Warning);
        }

        _start.Text = StartsLater ? string.Format(Tr("Começar carreira em {0}"), startYear) : Tr("Começar carreira");
        _start.Disabled = problems.Count > 0;
        string startTip = Palette.Tip(
            Tr("Começa a carreira com este personagem."),
            StartsLater ? string.Format(Tr("a carreira começa em {0}, na pré-campanha da primeira eleição que o personagem pode disputar."), startYear) : Tr("a carreira começa na pré-campanha da próxima eleição municipal, na cidade natal."),
            Tr("nada."));
        _start.TooltipText = _start.Disabled ? Palette.Blocked(string.Join(" ", problems), startTip) : startTip;
        Glossary.LinkTerms(_status);
    }

    private IEnumerable<string> Problems()
    {
        if (string.IsNullOrWhiteSpace(_c.CivilName) || string.IsNullOrWhiteSpace(_c.BallotName))
        {
            yield return Tr("Preencha o nome civil e o nome de urna.");
        }

        if (_c.TraitIds.Count != 2)
        {
            List<string> names = _c.TraitIds.Select(id => People.Traits.Traits.FirstOrDefault(x => x.Id == id)?.Name.For(_c.Gender) ?? id).ToList();
            yield return names.Count == 0
                ? Tr("Escolha exatamente 2 traços: nenhum está marcado.")
                : string.Format(Tr(names.Count == 1 ? "Escolha exatamente 2 traços: {0} está marcado ({1})." : "Escolha exatamente 2 traços: {0} estão marcados ({1})."), names.Count, string.Join(", ", names));
        }

        if (_c.HomeMunicipio is null)
        {
            yield return Tr("Escolha a cidade natal.");
        }

        if (_c.PartyId is null || _world.Find(_c.PartyId) is null)
        {
            yield return Tr("Escolha um partido.");
        }

        if (BallotProblem() is { } ballot)
        {
            yield return ballot;
        }

        WorldValidationReport report = _world.Validate();
        string Sigla(string? id) => id is null ? "" : _world.Find(id)?.Sigla ?? id;
        foreach (WorldIssue issue in report.Errors)
        {
            yield return WorldTexts.Issue(issue, Sigla);
        }
    }

    /// <summary>Sorteia tudo (usado também pelo teste de fumaça da UI).</summary>
    public void RandomizeEverything() => Change(RandomizeAll);

    /// <summary>Teste de fumaça: idade e mês de aniversário, como os controles da aba Identidade.</summary>
    public void SetAgeForTest(int age, int month) => Change(() => SetAge(age, month));

    /// <summary>Teste de fumaça: avisos de idade mostrados na aba Identidade.</summary>
    public IReadOnlyList<string> AgeWarningsForTest => AgeWarnings().ToList();

    /// <summary>Teste de fumaça: filia ao partido mais forte da cidade escolhida, como a aba Partido sugere.</summary>
    public void ChooseStrongestPartyForTest()
    {
        if (LocalStrength() is { } local && !_ownParty)
        {
            _c.PartyId = local.MaxBy(s => s.Value).Key;
            ApplyPartyNumber();
        }
    }

    /// <summary>Teste de fumaça: aparência e gênero do personagem em criação.</summary>
    public Appearance AppearanceForTest => _c.Appearance!;

    public Gender GenderForTest => _c.Gender;

    /// <summary>Teste de fumaça: uma edição à mão na aparência, como o controle "Pintas" da aba Aparência.</summary>
    public void EditAppearanceForTest() => SetAppearance(a => a with { Moles = !a.Moles });

    /// <summary>Teste de fumaça: pendências que impedem começar a carreira, como aparecem na base da tela.</summary>
    public IReadOnlyList<string> ProblemsForTest => Problems().ToList();

    /// <summary>Teste de fumaça: o botão de começar está liberado.</summary>
    public bool CanStartForTest => !_start.Disabled;

    /// <summary>Teste de fumaça: partido do personagem em criação.</summary>
    public string? PartyIdForTest => _c.PartyId;

    /// <summary>Teste de fumaça: número de urna completo do personagem em criação.</summary>
    public string? BallotNumberForTest => _c.Campaign?.BallotNumber;

    /// <summary>Começa a carreira se não houver pendência; devolve false caso contrário.</summary>
    public bool TryStartCareer()
    {
        bool ok = !Problems().Any();
        StartCareer();
        return ok;
    }

    private void StartCareer()
    {
        if (Problems().Any())
        {
            return;
        }

        GameState state = NewGame.Create(_world, _c, _game.PendingSeed);
        _game.PendingWorld = null;
        _game.StartCareer(state);

        // Sem idade para a primeira eleição, o Core abre o calendário na pré-campanha da primeira que dá para disputar.
        _game.GoToCareer();
    }

    // ---------- Presets de aparência (pasta "presets" da base de UserPaths) ----------

    private void SavePreset()
    {
        DirAccess.MakeDirRecursiveAbsolute(PresetFolder);
        var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.SaveFile, Access = FileDialog.AccessEnum.Userdata, CurrentDir = PresetFolder, Filters = ["*.json"] };
        dialog.FileSelected += path =>
        {
            using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreString(JsonSerializer.Serialize(_c.Appearance, JsonData.Options));
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }

    private void LoadPreset()
    {
        DirAccess.MakeDirRecursiveAbsolute(PresetFolder);
        var dialog = new FileDialog { FileMode = FileDialog.FileModeEnum.OpenFile, Access = FileDialog.AccessEnum.Userdata, CurrentDir = PresetFolder, Filters = ["*.json"] };
        dialog.FileSelected += path =>
        {
            try
            {
                Appearance? loaded = JsonSerializer.Deserialize<Appearance>(FileAccess.GetFileAsString(path), JsonData.Options);
                if (loaded is not null)
                {
                    Change(() =>
                    {
                        _c.Appearance = loaded;
                        _faceEdited = true;
                        _wardrobeEdited = true;
                    });
                }
            }
            catch (JsonException)
            {
                Ui.Value(_status, Tr("Preset inválido.")).AddThemeColorOverride("font_color", Ui.Error);
            }
        };
        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(900, 600));
    }
}
