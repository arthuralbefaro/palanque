using Godot;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>Alvo "tracos": a escolha de 2 traços na criação do personagem, clicada com o mouse.</summary>
public partial class UiShot
{
    private const string TraitHeading = "Traços de personalidade: escolha 2 de 3";

    /// <summary>
    /// Abre a criação como o jogador a vê (sem sortear nada antes), desmarca os traços com o mouse, marca 2 e confere
    /// que "Começar carreira" libera e que a mensagem some; marca o terceiro e confere o bloqueio com a contagem e os
    /// nomes; desmarca um e confere que libera de novo.
    /// </summary>
    private async Task<bool> CheckCreatorTraits(CharacterCreatorScreen creator)
    {
        var tabs = FindAll<TabContainer>(creator).First();
        for (int i = 0; i < tabs.GetTabCount() && TraitBoxes(creator).Count == 0; i++)
        {
            tabs.CurrentTab = i;
            await Frames(2);
        }

        if (TraitBoxes(creator).Count != 3)
        {
            Fail($"a criação mostra {TraitBoxes(creator).Count} traços para escolher, e não 3.");
            return false;
        }

        // Desmarca o que vier marcado, um clique por vez (cada clique reconstrói a aba).
        while (TraitBoxes(creator).FirstOrDefault(b => b.ButtonPressed) is { } marked)
        {
            await Click(marked);
        }

        if (!StartBlocked(creator, "nenhum está marcado", out string why))
        {
            Fail("sem traço marcado: " + why);
            return false;
        }

        await Click(TraitBoxes(creator)[0]);
        await Click(TraitBoxes(creator)[1]);
        if (TraitBoxes(creator).Count(b => b.ButtonPressed) != 2)
        {
            Fail("dois cliques não marcaram dois traços.");
            return false;
        }

        if (StartButton(creator) is not { Disabled: false } || VisibleTexts(creator).FirstOrDefault(t => t.Contains("Escolha exatamente 2 traços", StringComparison.Ordinal)) is { } left)
        {
            Fail($"com 2 traços marcados o botão não liberou: {string.Join(" | ", VisibleTexts(creator).Where(t => t.Contains("Escolha", StringComparison.Ordinal) || t.Contains("Preencha", StringComparison.Ordinal)))}");
            return false;
        }

        await Click(TraitBoxes(creator)[2]);
        string[] names = TraitBoxes(creator).Select(b => b.Text.Split(" — ")[0]).ToArray();
        if (!StartBlocked(creator, $"3 estão marcados ({string.Join(", ", names)})", out why))
        {
            Fail("com 3 traços marcados: " + why);
            return false;
        }

        await Click(TraitBoxes(creator)[0]);
        if (StartButton(creator) is not { Disabled: false })
        {
            Fail("depois de desmarcar o terceiro traço o botão não liberou.");
            return false;
        }

        GD.Print("Smoke: traços marcados com o mouse; o botão libera com 2 e bloqueia com 3, com a contagem e os nomes.");
        return true;
    }

    /// <summary>As caixas dos traços oferecidos: as da mesma coluna do título dos traços, visíveis.</summary>
    private static List<CheckBox> TraitBoxes(CharacterCreatorScreen creator)
    {
        Label? heading = FindAll<Label>(creator).FirstOrDefault(l => l.Text == TraitHeading && l.IsVisibleInTree());
        if (heading?.GetParent() is not { } column)
        {
            return [];
        }

        return column.GetChildren().SkipWhile(n => n != heading).OfType<CheckBox>().Where(b => b.IsVisibleInTree()).ToList();
    }

    private static Button? StartButton(CharacterCreatorScreen creator) =>
        FindAll<Button>(creator).FirstOrDefault(b => b.Text.StartsWith("Começar carreira", StringComparison.Ordinal));

    private static List<string> VisibleTexts(Node root) =>
        FindAll<Control>(root).Where(c => c.IsVisibleInTree()).Select(c => c switch
        {
            Label l => l.Text,
            RichTextLabel r => r.GetParsedText(),
            _ => null,
        }).OfType<string>().ToList();

    private static bool StartBlocked(CharacterCreatorScreen creator, string expected, out string why)
    {
        string message = "Escolha exatamente 2 traços: " + expected;
        bool shown = VisibleTexts(creator).Any(t => t.Contains(message, StringComparison.Ordinal));
        why = StartButton(creator) is not { Disabled: true } ? "o botão Começar carreira não ficou desativado"
            : !shown ? $"a mensagem \"{message}\" não aparece; aparece: {string.Join(" | ", VisibleTexts(creator).Where(t => t.Contains("traço", StringComparison.Ordinal)))}"
            : "";
        return why.Length == 0;
    }
}
