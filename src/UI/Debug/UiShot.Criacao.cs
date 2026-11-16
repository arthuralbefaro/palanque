using Godot;
using Planalto.Autoload;
using Planalto.Core.State;
using Planalto.Core.World;
using Planalto.UI.CampaignMaterials;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>Conferências da criação do personagem usadas pelo alvo "creator".</summary>
public partial class UiShot
{
    /// <summary>
    /// O partido escolhido na criação é o mesmo no santinho, no estado do jogo e na tela central: escolhe cada
    /// partido do mundo pela caixa da aba Partido e confere a sigla do santinho; depois começa a carreira com alguns
    /// deles e confere a sigla e o número no estado, na candidatura e no que a tela central mostra.
    /// </summary>
    private async Task<bool> CheckCreatorParty(CharacterCreatorScreen creator)
    {
        Game game = Game.Instance;
        WorldSetup world = game.PendingWorld!;
        var tabs = FindAll<TabContainer>(creator).First();
        tabs.CurrentTab = tabs.GetTabCount() - 1;
        await Frames(2);

        foreach (PartyDraft party in world.Parties.ToList())
        {
            string label = $"{party.Number} · {party.Sigla} — {party.Name}";
            if (FindAll<CheckBox>(creator).FirstOrDefault(b => b.Text == label) is not { } box)
            {
                Fail($"a aba Partido não lista {party.Sigla}.");
                return false;
            }

            box.ButtonPressed = true;
            box.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            if (SantinhoMismatch(creator, party.Id, party.Sigla, party.Number) is { } mismatch)
            {
                Fail($"partido {party.Sigla} escolhido na aba Partido: {mismatch}");
                return false;
            }
        }

        // Carreira começada com o primeiro, o do meio e o último partido da lista: estado, candidatura e tela central.
        var picks = new[] { world.Parties[0], world.Parties[world.Parties.Count / 2], world.Parties[^1] };
        foreach (PartyDraft party in picks)
        {
            game.PendingWorld = world;
            var fresh = GD.Load<PackedScene>(Game.Scenes.CharacterCreator).Instantiate<CharacterCreatorScreen>();
            AddChild(fresh);
            await Frames(3);
            var freshTabs = FindAll<TabContainer>(fresh).First();
            freshTabs.CurrentTab = freshTabs.GetTabCount() - 1;
            await Frames(2);
            var box = FindAll<CheckBox>(fresh).First(b => b.Text == $"{party.Number} · {party.Sigla} — {party.Name}");
            box.ButtonPressed = true;
            box.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            if (SantinhoMismatch(fresh, party.Id, party.Sigla, party.Number) is { } mismatch)
            {
                Fail($"partido {party.Sigla}: {mismatch}");
                return false;
            }

            if (!fresh.TryStartCareer() || game.State is not { } state)
            {
                Fail($"partido {party.Sigla}: a carreira não começou.");
                return false;
            }

            GameParty? inState = state.Party(state.Player.PartyId);
            var candidate = state.Campaign?.Player;
            if (inState?.Sigla != party.Sigla || inState.Number != party.Number || candidate?.PartyId != party.Id
                || candidate.Number != state.Player.Campaign?.BallotNumber || !candidate.Number.StartsWith(party.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
            {
                Fail($"santinho com {party.Sigla} ({party.Number}), mas o jogo começou com {inState?.Sigla} ({inState?.Number}), candidatura pelo partido {candidate?.PartyId} com o número {candidate?.Number} (santinho: {state.Player.Campaign?.BallotNumber}).");
                return false;
            }

            RemoveChild(fresh);
            fresh.QueueFree();
            var hub = GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate<HubScreen>();
            AddChild(hub);
            await Frames(5);
            string first = TextOf(hub);
            if (!first.Contains($"· {party.Sigla} · {Tr("número")} {candidate.Number}", StringComparison.Ordinal))
            {
                Fail($"a tela central abre sem dizer o partido ({party.Sigla}) e o número ({candidate.Number}) da candidatura.");
                return false;
            }

            Named<Button>(hub, "Area_partido")!.EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            if (!TextOf(hub).Contains($"{party.Sigla} · {party.Number}", StringComparison.Ordinal))
            {
                Fail($"a área Partido da tela central não mostra {party.Sigla} · {party.Number}.");
                return false;
            }

            RemoveChild(hub);
            hub.QueueFree();
            await Frames(2);
        }

        GD.Print($"  partido: {world.Parties.Count} escolhas conferidas no santinho; {picks.Length} carreiras conferidas no estado e na tela central");
        return true;
    }

    /// <summary>
    /// "Como te veem" na barra de cima: a faixa nomeada de data/campaign/config.json e a régua, sem número cru; o
    /// número fica no tooltip em três partes, com os limites da faixa.
    /// </summary>
    private async Task<bool> CheckImageStat(HubScreen hub)
    {
        Game game = Game.Instance;
        var config = game.Campaign.Config;
        var player = game.State!.Campaign!.Player;
        double original = player.Image;
        var culture = System.Globalization.CultureInfo.GetCultureInfo("pt-BR");
        foreach (double image in new[] { -1, -0.3, -0.02, 0, 0.06, 0.4, 1 })
        {
            player.Image = image;
            hub.Refresh();
            await Frames(2);
            string expected = config.ImageBandOf(image).Name;
            string number = Math.Abs(image).ToString("0.00", culture);
            var box = Named<Control>(hub, "Imagem");
            string shown = box is null ? "" : FindAll<Label>(box).FirstOrDefault(l => l.Name == "Faixa")?.Text ?? "";
            int ticks = box is null ? 0 : FindAll<HBoxContainer>(box).FirstOrDefault(r => r.Name == "Regua")?.GetChildCount() ?? 0;
            string tip = box?.TooltipText ?? "";
            if (!shown.EndsWith(expected, StringComparison.Ordinal) || shown.Any(char.IsDigit) || ticks != config.ImageScale.Count
                || !tip.Contains(number, StringComparison.Ordinal) || !tip.Contains(expected, StringComparison.Ordinal)
                || !tip.Contains(Tr("O que faz:"), StringComparison.Ordinal) || !tip.Contains(Tr("Quanto custa:"), StringComparison.Ordinal))
            {
                Fail($"imagem {image}: a barra mostra \"{shown}\" (esperado: faixa \"{expected}\", sem número), régua com {ticks} casas, tooltip \"{tip}\".");
                return false;
            }
        }

        player.Image = original;
        hub.Refresh();
        await Frames(2);
        GD.Print($"  imagem: {config.ImageScale.Count} faixas nomeadas na barra de cima, número só no tooltip");
        return true;
    }

    /// <summary>
    /// Todo seletor de cor da criação mostra a cor que guarda: em cada aba, a amostra tem área (o botão menos as
    /// margens do estilo) e o ponto do meio do botão, lido da imagem da tela, é da cor escolhida.
    /// </summary>
    private async Task<bool> CheckCreatorColors(CharacterCreatorScreen creator)
    {
        var tabs = FindAll<TabContainer>(creator).First();
        int checkedCount = 0;
        for (int tab = 0; tab < tabs.GetTabCount(); tab++)
        {
            tabs.CurrentTab = tab;
            await Frames(3);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            Image screen = GetViewport().GetTexture().GetImage();
            Rect2 window = GetViewport().GetVisibleRect();
            foreach (ColorPickerButton picker in FindAll<ColorPickerButton>(creator).Where(p => p.IsVisibleInTree()))
            {
                Vector2 swatch = picker.Size - picker.GetThemeStylebox("normal").GetMinimumSize();
                if (swatch.X < 24 || swatch.Y < 16)
                {
                    Fail($"aba {tabs.GetTabTitle(tab)}: seletor de cor com amostra de {swatch.X:0}×{swatch.Y:0} (o botão mede {picker.Size.X:0}×{picker.Size.Y:0}): a cor não aparece.");
                    return false;
                }

                // Só confere a imagem se o botão está inteiro na parte visível (a aba rola).
                Rect2 rect = picker.GetGlobalRect();
                Rect2 clip = FindAll<ScrollContainer>(creator).First(sc => sc.IsVisibleInTree() && sc.IsAncestorOf(picker)).GetGlobalRect();
                if (!clip.Encloses(rect) || !window.Encloses(rect))
                {
                    continue;
                }

                Vector2 point = GetViewport().GetFinalTransform() * rect.GetCenter();
                Color seen = screen.GetPixel((int)point.X, (int)point.Y);
                Color wanted = picker.Color;
                if (Math.Abs(seen.R - wanted.R) > 0.04f || Math.Abs(seen.G - wanted.G) > 0.04f || Math.Abs(seen.B - wanted.B) > 0.04f)
                {
                    Fail($"aba {tabs.GetTabTitle(tab)}: o seletor de cor guarda #{wanted.ToHtml(false)} e a tela mostra #{seen.ToHtml(false)} no meio dele.");
                    return false;
                }

                checkedCount++;
            }
        }

        if (checkedCount < 8)
        {
            Fail($"só {checkedCount} seletores de cor conferidos na imagem da criação (esperado: 8 ou mais).");
            return false;
        }

        GD.Print($"  cores: {checkedCount} seletores de cor conferidos na imagem da tela, em todas as abas");
        return true;
    }

    /// <summary>
    /// 17 anos com aniversário em dezembro (sem 18 anos no prazo do registro de 2028): a criação avisa, na aba
    /// Identidade e na base da tela, que a carreira começa em 2032; ao confirmar, o jogo abre direto na pré-campanha
    /// de 2032, na tela central, com o personagem mais velho.
    /// </summary>
    private async Task<bool> CheckCreatorSeventeen(CharacterCreatorScreen creator, string? shot)
    {
        Game game = Game.Instance;
        string normal = FindAll<Button>(creator).Last().Text;
        creator.SetAgeForTest(17, 12);
        await Frames(3);
        var tabs = FindAll<TabContainer>(creator).First();
        tabs.CurrentTab = 2;
        await Frames(2);
        Save(shot, "_aviso");
        bool warned = creator.AgeWarningsForTest.Any(w => w.Contains("2032", StringComparison.Ordinal) && w.Contains("2028", StringComparison.Ordinal));
        bool atBottom = Named<Label>(creator, "AvisoComecoTardio") is { Visible: true } notice && notice.Text.Contains("2032", StringComparison.Ordinal);
        bool onButton = FindAll<Button>(creator).Any(b => b.Text.StartsWith(Tr("Começar carreira"), StringComparison.Ordinal) && b.Text.Contains("2032", StringComparison.Ordinal));
        bool started = creator.TryStartCareer();
        if (!warned || !atBottom || !onButton || !started || game.State is not { } state)
        {
            Fail($"personagem de 17 anos: aviso na aba {warned}, aviso na base {atBottom}, ano no botão {onButton} (antes: \"{normal}\"), começou {started}.");
            return false;
        }

        int age = state.Player.AgeOn(state.Calendar.WeekStart);
        if (state.Calendar.Year != 2032 || game.Engine is null || state.Campaign is not { Phase: Core.Campaign.CampaignPhase.PreCampanha } campaign
            || campaign.ElectionDate.Year != 2032 || age < 20 || game.LastNavigation != Game.Scenes.Hub || !Core.Career.CareerFlow.CanRun(state, 2032))
        {
            Fail($"personagem de 17 anos: calendário em {state.Calendar.Year}, campanha {(game.Engine is null ? "nenhuma" : state.Campaign?.Phase.ToString())}, eleição de {state.Campaign?.ElectionDate.Year}, idade {age}, tela {game.LastNavigation}.");
            return false;
        }

        // A tela central abre de fato nessa pré-campanha.
        RemoveChild(creator);
        creator.QueueFree();
        var hub = GD.Load<PackedScene>(Game.Scenes.Hub).Instantiate<HubScreen>();
        AddChild(hub);
        await Frames(5);
        string text = TextOf(hub);
        if (!text.Contains(Tr("Pré-campanha"), StringComparison.Ordinal) || !text.Contains(campaign.Player.Number, StringComparison.Ordinal))
        {
            Fail("personagem de 17 anos: a tela central não abriu na pré-campanha de 2032.");
            return false;
        }

        GD.Print($"  17 anos: aviso na aba e na base; carreira aberta em {state.Calendar.WeekStart:dd/MM/yyyy}, pré-campanha de 2032, aos {age} anos");
        return true;
    }

    /// <summary>
    /// Troca de gênero na aba Identidade: sem edição à mão, a aparência é sorteada de novo na hora; com edição à mão,
    /// aparece a pergunta, e cada resposta faz o que diz (sortear de novo ou manter).
    /// </summary>
    private async Task<bool> CheckCreatorGender(CharacterCreatorScreen creator)
    {
        var tabs = FindAll<TabContainer>(creator).First();
        tabs.CurrentTab = 2;
        await Frames(2);
        async Task<bool> Pick(Core.Characters.Gender gender)
        {
            if (Named<OptionButton>(creator, "Genero") is not { } options)
            {
                return false;
            }

            options.Select((int)gender);
            options.EmitSignal(OptionButton.SignalName.ItemSelected, (int)gender);
            await Frames(3);
            return true;
        }

        ConfirmationDialog? Question() => FindAll<ConfirmationDialog>(creator).FirstOrDefault(d => d.Name == "ConfirmarAparencia" && IsInstanceValid(d) && !d.IsQueuedForDeletion());
        Core.Characters.Gender Other(int step) => (Core.Characters.Gender)(((int)creator.GenderForTest + step) % 3);

        // 1. Aparência só sorteada: troca direto, sem pergunta.
        var before = creator.AppearanceForTest;
        var target = Other(1);
        if (!await Pick(target) || Question() is not null || creator.GenderForTest != target || creator.AppearanceForTest == before)
        {
            Fail($"mudar o gênero sem edição à mão deveria sortear a aparência de novo, sem perguntar (gênero {creator.GenderForTest}, pergunta: {Question() is not null}).");
            return false;
        }

        // 2. Editada à mão: pergunta; "Sortear de novo" troca a aparência.
        creator.EditAppearanceForTest();
        await Frames(2);
        before = creator.AppearanceForTest;
        target = Other(1);
        await Pick(target);
        if (Question() is not { } ask || creator.AppearanceForTest != before)
        {
            Fail("mudar o gênero com a aparência editada à mão deveria perguntar antes de sortear.");
            return false;
        }

        ask.GetOkButton().EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        if (Question() is not null || creator.GenderForTest != target || creator.AppearanceForTest == before)
        {
            Fail("\"Sortear de novo\" deveria mudar o gênero e a aparência.");
            return false;
        }

        // 3. Editada à mão de novo: "Manter a aparência" muda só o gênero.
        creator.EditAppearanceForTest();
        await Frames(2);
        before = creator.AppearanceForTest;
        target = Other(1);
        await Pick(target);
        if (Question() is not { } keep)
        {
            Fail("a segunda troca de gênero com edição à mão deveria perguntar de novo.");
            return false;
        }

        keep.GetCancelButton().EmitSignal(BaseButton.SignalName.Pressed);
        await Frames(3);
        if (Question() is not null || creator.GenderForTest != target || creator.AppearanceForTest != before)
        {
            Fail("\"Manter a aparência\" deveria mudar só o gênero.");
            return false;
        }

        GD.Print("  gênero: sorteio direto sem edição à mão; pergunta com edição; \"Sortear de novo\" e \"Manter a aparência\" conferidos");
        return true;
    }

    /// <summary>
    /// Número de urna digitado na aba Campanha: três algarismos, com zeros à esquerda e repetidos ("000", "111");
    /// menos de três ou letra não vale, o motivo aparece embaixo do campo e na base da tela, e a carreira não começa.
    /// </summary>
    private async Task<bool> CheckCreatorBallot(CharacterCreatorScreen creator, string? shot)
    {
        var tabs = FindAll<TabContainer>(creator).First();
        tabs.CurrentTab = 3;
        await Frames(2);
        async Task Type(string text)
        {
            var field = Named<LineEdit>(creator, "NumeroUrna")!;
            field.Text = text;
            field.EmitSignal(LineEdit.SignalName.TextChanged, text);
            await Frames(2);
        }

        if (Named<LineEdit>(creator, "NumeroUrna") is not { MaxLength: 3 })
        {
            Fail("a aba Campanha não tem o campo de 3 algarismos do número de urna.");
            return false;
        }

        string prefix = creator.BallotNumberForTest![..2];
        foreach (string valid in new[] { "000", "111", "007" })
        {
            await Type(valid);
            string santinho = TextOf(FindAll<Santinho>(creator).First());
            if (creator.BallotNumberForTest != prefix + valid || !santinho.Contains(prefix + valid, StringComparison.Ordinal) || !creator.CanStartForTest
                || Named<Label>(creator, "NumeroUrnaMotivo") is { Visible: true })
            {
                Fail($"número de urna {valid}: ficou {creator.BallotNumberForTest}, começar liberado: {creator.CanStartForTest}.");
                return false;
            }
        }

        foreach (var (typed, shown) in new[] { ("07", "07"), ("", ""), ("1a", "1"), ("x9y", "9") })
        {
            await Type(typed);
            if (typed == "07")
            {
                Save(shot, "_urna_invalida");
            }

            var reason = Named<Label>(creator, "NumeroUrnaMotivo");
            bool listed = creator.ProblemsForTest.Any(p => p.Contains(Tr("Número de urna"), StringComparison.Ordinal));
            if (Named<LineEdit>(creator, "NumeroUrna")!.Text != shown || creator.BallotNumberForTest != prefix + "007" || creator.CanStartForTest || !listed
                || reason is not { Visible: true } || !reason.Text.Contains("3", StringComparison.Ordinal))
            {
                Fail($"número de urna \"{typed}\": campo \"{Named<LineEdit>(creator, "NumeroUrna")!.Text}\", número {creator.BallotNumberForTest}, começar liberado: {creator.CanStartForTest}, motivo na base: {listed}, motivo no campo: \"{reason?.Text}\".");
                return false;
            }
        }

        // O rascunho inválido sobrevive à troca de aba; o número válido volta a liberar a carreira.
        tabs.CurrentTab = 2;
        await Frames(2);
        tabs.CurrentTab = 3;
        await Type("120");
        if (creator.BallotNumberForTest != prefix + "120" || !creator.CanStartForTest)
        {
            Fail("o número de urna 120 deveria valer depois de uma tentativa inválida.");
            return false;
        }

        GD.Print($"  número de urna: 000, 111 e 007 aceitos; \"07\", vazio e letras recusados com o motivo; ficou {creator.BallotNumberForTest}");
        return true;
    }

    /// <summary>Diferença entre o partido escolhido e o que o santinho e o personagem mostram; null se batem.</summary>
    private static string? SantinhoMismatch(CharacterCreatorScreen creator, string partyId, string sigla, int number)
    {
        if (creator.PartyIdForTest != partyId)
        {
            return $"o personagem ficou no partido {creator.PartyIdForTest}.";
        }

        string santinho = TextOf(FindAll<Santinho>(creator).First());
        string prefix = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string? ballot = creator.BallotNumberForTest;
        if (!santinho.Contains($" · {sigla}\n", StringComparison.Ordinal) || !santinho.Contains($"{sigla} — ", StringComparison.Ordinal))
        {
            return $"o santinho não mostra a sigla {sigla}: \"{santinho.Replace("\n", " | ", StringComparison.Ordinal)}\".";
        }

        return ballot is null || !ballot.StartsWith(prefix, StringComparison.Ordinal) || !santinho.Contains(ballot, StringComparison.Ordinal)
            ? $"o número de urna ({ballot}) não começa com o número do partido ({prefix}) ou não está no santinho."
            : null;
    }
}
