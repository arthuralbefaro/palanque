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
