using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Events;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Events;

public class EventLintTests
{
    private static TraitCatalog Traits => JsonData.LoadFile<TraitCatalog>(Path.Combine(TestContent.DataDirectory, "traits.json")).GetValueOrThrow();

    [Fact]
    public void ConteudoDoProjeto_SemErros()
    {
        LintReport report = EventLint.Run(CampaignKit.C.Events, CampaignKit.C.Portes, Traits);

        Assert.Empty(report.Errors);
        Assert.True(report.Distribution["campanha"].Count >= 30);
    }

    [Fact]
    public void DetectaCadeiaQuebradaVariavelInexistenteEInalcancavel()
    {
        var content = new EventContent
        {
            Config = CampaignKit.C.Events.Config,
            Events =
            [
                Event("a", "Olá {jogador.apelido}", new Effects { Schedule = [new ScheduledEvent("nao_existe", 2)] }),
                Event("b", "Continuação", Effects.None, chainOnly: true),
                Event("c", "Precisa de flag", Effects.None, flag: "nunca_ligada"),
            ],
        };

        LintReport report = EventLint.Run(content, CampaignKit.C.Portes, Traits);

        Assert.Contains(report.Errors, e => e.Contains("variável inexistente {jogador.apelido}"));
        Assert.Contains(report.Errors, e => e.Contains("cadeia quebrada"));
        Assert.Contains(report.Errors, e => e.StartsWith("b:") && e.Contains("inalcançável"));
        Assert.Contains(report.Errors, e => e.StartsWith("c:") && e.Contains("nunca_ligada"));
    }

    private static EventDefinition Event(string id, string text, Effects effects, bool chainOnly = false, string? flag = null) => new()
    {
        Id = id,
        Category = "campanha",
        Phases = ["campanha"],
        Rarity = Rarity.Comum,
        CooldownWeeks = 1,
        ChainOnly = chainOnly,
        Conditions = new EventConditions { Flag = flag },
        Title = "t",
        Variants = [text],
        Choices = [new EventChoice { Label = "x", Effects = effects }, new EventChoice { Label = "y" }],
    };
}
