using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Events;
using Planalto.Core.Media;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Parties;
using Xunit;

namespace Planalto.Core.Tests.Events;

/// <summary>
/// Regras que o EventLint cobra como erro desde a R3: rastro em toda escolha (princípio d), troca nas escolhas
/// relevantes (princípio e) e texto de morte sóbrio (GDD 17.16).
/// </summary>
public class EventLintRulesTests
{
    private static readonly TraitCatalog Traits = JsonData.LoadFile<TraitCatalog>(Path.Combine(TestContent.DataDirectory, "traits.json")).GetValueOrThrow();

    private static readonly MemoryEffect Memory = new() { Quem = EventRole.Rival, Fato = "x", Peso = 1, Pontos = 5, Texto = "Lembra" };

    private static EventDefinition Event(string category, bool luto, string text, params EventChoice[] choices) => new()
    {
        Id = "teste", Category = category, Phases = ["campanha"], Rarity = Rarity.Comum, CooldownWeeks = 0, Title = "Título", Variants = [text], Luto = luto,
        Choices = [.. choices],
    };

    private static EventChoice Traced(string label = "Com rastro") => new() { Label = label, Effects = new Effects { Memories = [Memory] } };

    private static LintReport Lint(EventDefinition e) =>
        EventLint.Run(new EventContent { Config = CampaignKit.C.Events.Config, Events = [e] }, CampaignKit.C.Portes, Traits);

    [Fact]
    public void Os_eventos_do_jogo_passam_e_as_escolhas_sem_rastro_estao_marcadas_com_motivo()
    {
        LintReport report = EventLint.Run(CampaignKit.C.Events, CampaignKit.C.Portes, Traits);
        Assert.Empty(report.Errors);
        Assert.NotNull(report.SemRastro);
        int marked = CampaignKit.C.Events.Events.Sum(e => e.Choices.Count(c => c.SemRastro is not null));
        Assert.Equal(marked, report.SemRastro!.Count);
        Assert.All(CampaignKit.C.Events.Events.SelectMany(e => e.Choices), c =>
            Assert.True(c.SemRastro is { Length: >= EventLint.MinSemRastro } || c.Effects.LeavesTrace || (c.Test is not null && c.Test.Success.LeavesTrace && c.Test.Failure.LeavesTrace), c.Label));

        // Os eventos antigos já não mexem em energia, e a chuva no comício só existe com comício marcado.
        Assert.DoesNotContain(CampaignKit.C.Events.Events.SelectMany(e => e.Choices), c => c.Effects.Energy != 0);
        Assert.Equal("comicio", CampaignKit.C.Events.Find("campanha_comicio_chuva")!.Conditions.NeedsCommitment);
    }

    [Fact]
    public void Regra_do_rastro_escolha_sem_lembranca_nem_Livro_reprova_salvo_com_a_marca_e_o_motivo()
    {
        var bare = new EventChoice { Label = "Sem nada", Effects = new Effects { Money = -100 } };
        Assert.Contains(Lint(Event("campanha", false, "Texto.", Traced(), bare)).Errors, e => e.Contains("sem rastro", StringComparison.Ordinal));

        // Com a marca explícita e o motivo, passa, e o relatório lista a escolha.
        var marked = new EventChoice { Label = "Sem nada", Effects = new Effects { Money = -100 }, SemRastro = "Só mexe no caixa do candidato." };
        LintReport ok = Lint(Event("campanha", false, "Texto.", Traced(), marked));
        Assert.Empty(ok.Errors);
        Assert.Contains("Só mexe no caixa", Assert.Single(ok.SemRastro!));

        // Marca sem motivo, ou numa escolha que deixa rastro, reprova.
        var lazy = new EventChoice { Label = "Sem nada", SemRastro = "nada" };
        Assert.Contains(Lint(Event("campanha", false, "Texto.", Traced(), lazy)).Errors, e => e.Contains("motivo", StringComparison.Ordinal));
        var both = new EventChoice { Label = "Com os dois", Effects = new Effects { Memories = [Memory] }, SemRastro = "Motivo que não se aplica aqui." };
        Assert.Contains(Lint(Event("campanha", false, "Texto.", Traced(), both)).Errors, e => e.Contains("deixa rastro", StringComparison.Ordinal));

        // Livro de compromissos também é rastro: esqueleto, promessa ou dívida (em escolha relevante, com a troca declarada).
        var promise = new EventChoice
        {
            Label = "Prometer", Relevant = true, Gain = ["relacao"], Cost = ["risco"],
            Effects = new Effects { Support = new Dictionary<string, double> { ["baixa"] = 0.02 }, Promise = new PromiseEffect { Para = EventRole.Lideranca, Objeto = "obra", Texto = "Fazer a obra" } },
        };
        Assert.Empty(Lint(Event("campanha", false, "Texto.", Traced(), promise)).Errors);

        // Com teste, o rastro tem de ficar nos dois desfechos.
        var half = new EventChoice
        {
            Label = "Arriscar",
            Test = new ChoiceTest { Attribute = AttributeId.Oratoria, SuccessText = "s", Success = new Effects { Memories = [Memory] }, FailureText = "f", Failure = new Effects() },
        };
        Assert.Contains(Lint(Event("campanha", false, "Texto.", Traced(), half)).Errors, e => e.Contains("sem rastro", StringComparison.Ordinal));
    }

    [Fact]
    public void Regra_da_troca_escolha_relevante_sem_ganho_e_custo_de_naturezas_diferentes_reprova()
    {
        var noCost = new EventChoice { Label = "Só ganha", Relevant = true, Gain = ["dinheiro"], Cost = [], Effects = new Effects { Money = 100, Memories = [Memory] } };
        Assert.Contains(Lint(Event("campanha", false, "Texto.", Traced(), noCost)).Errors, e => e.Contains("falta o ganho ou o custo", StringComparison.Ordinal));

        var same = new EventChoice { Label = "Mesma natureza", Relevant = true, Gain = ["relacao"], Cost = ["relacao"], Effects = new Effects { Memories = [Memory] } };
        Assert.Contains(Lint(Event("campanha", false, "Texto.", Traced(), same)).Errors, e => e.Contains("mesma natureza", StringComparison.Ordinal));

        var unsupported = new EventChoice { Label = "Sem efeito", Relevant = true, Gain = ["relacao"], Cost = ["dinheiro"], Effects = new Effects { Memories = [Memory] } };
        Assert.Contains(Lint(Event("campanha", false, "Texto.", Traced(), unsupported)).Errors, e => e.Contains("sem efeito que o sustente", StringComparison.Ordinal));

        var trade = new EventChoice { Label = "Troca", Relevant = true, Gain = ["relacao"], Cost = ["dinheiro"], Effects = new Effects { Money = -100, Memories = [Memory] } };
        Assert.Empty(Lint(Event("campanha", false, "Texto.", Traced(), trade)).Errors);
    }

    [Theory]
    [InlineData("Seu Olavo, vereador há cinco mandatos, morreu ontem aos 74 anos. A Câmara decretou luto de três dias.", 0)]
    [InlineData("O velório será na Câmara Municipal, das 8h às 16h.", 0)]
    [InlineData("Morreu o vereador!", 1)]
    [InlineData("URGENTE: morre o vereador", 2)]
    [InlineData("O vereador teve um fim brutal e chocante.", 2)]
    [InlineData("Morreu o vereador que virou meme na cidade, e a piada continua.", 2)]
    [InlineData("Aproveitar o velório para ironizar o adversário.", 1)]
    [InlineData("Dizer que já vai tarde.", 1)]
    public void Texto_de_morte_e_sobrio_sem_humor_sem_sensacionalismo_e_sem_zombaria(string text, int problems) =>
        Assert.Equal(problems, EventLint.CheckDeathText(text).Count);

    [Fact]
    public void Evento_de_morte_tem_de_estar_marcado_como_luto_na_categoria_certa_e_no_tom_certo()
    {
        const string Sober = "Seu Olavo, vereador há cinco mandatos, morreu ontem aos 74 anos.";
        Assert.True(EventLint.MentionsDeath(Sober));
        Assert.True(EventLint.MentionsDeath("A Câmara está de luto."));
        Assert.False(EventLint.MentionsDeath("A decisão foi absoluta e amortecida pelo tempo."));

        // Fala de morte sem a marca de luto: reprova.
        Assert.Contains(Lint(Event("vida_pessoal", false, Sober, Traced("Ir ao velório"), Traced("Mandar flores"))).Errors, e => e.Contains("sem estar marcado como 'luto'", StringComparison.Ordinal));

        // Marcado, sóbrio e na categoria certa: passa.
        Assert.Empty(Lint(Event("vida_pessoal", true, Sober, Traced("Ir ao velório"), Traced("Mandar flores"))).Errors);

        // Fora da categoria (os eventos cômicos e as festas não tratam de morte).
        Assert.Contains(Lint(Event("raros", true, Sober, Traced("Ir ao velório"), Traced("Mandar flores"))).Errors, e => e.Contains("categoria 'raros'", StringComparison.Ordinal));

        // Fora do tom, na abertura ou no rótulo de uma escolha (nenhuma escolha zomba do morto).
        Assert.Contains(Lint(Event("vida_pessoal", true, "Morreu o vereador!", Traced("Ir ao velório"), Traced("Mandar flores"))).Errors, e => e.Contains("exclamação", StringComparison.Ordinal));
        Assert.Contains(Lint(Event("vida_pessoal", true, Sober, Traced("Ir ao velório"), Traced("Ironizar o legado dele"))).Errors, e => e.Contains("zomba", StringComparison.Ordinal));
        Assert.All(EventLint.DeathCategories, c => Assert.True(EventLint.Targets.ContainsKey(c)));
    }

    [Fact]
    public void Noticia_de_morte_sai_em_tom_neutro_em_todos_os_veiculos()
    {
        CampaignEngine engine = CampaignKit.NewCareer(R3WorldTests.City("metropole"), 77);
        CampaignState campaign = engine.Context.State.Campaign!;
        string rival = campaign.Rivals.Records[0].CandidateId;
        var facts = Enumerable.Range(0, 6).Select(i => engine.Emit(FactKind.Morte, $"Nota de falecimento {i}", rival, visibility: 3)).Select(f => f.Id).ToHashSet();
        R3WorldTests.ResolveEvents(engine);
        engine.AdvanceDay();
        List<NewsItem> news = campaign.Media.News.Where(n => facts.Contains(n.FactId)).ToList();
        Assert.True(news.Count >= engine.City.Outlets.Count(o => !engine.Context.Campaign.Media.Outlets.Type(o.Type).Rede));
        Assert.All(news, n => Assert.Equal(Tone.Neutro, n.Tone));
        Assert.Contains(engine.City.Outlets.Select(o => o.Line).Distinct(), line => line != EditorialLine.Independente);
    }
}
