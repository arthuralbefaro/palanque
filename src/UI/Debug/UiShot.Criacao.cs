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
