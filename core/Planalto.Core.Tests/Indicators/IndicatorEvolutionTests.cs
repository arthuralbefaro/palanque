using Planalto.Core.Executive;
using Planalto.Core.Indicators;
using Planalto.Core.Municipios;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.Tests.Executive;
using Planalto.Core.Tests.Parties;
using Xunit;
using Xunit.Abstractions;

namespace Planalto.Core.Tests.Indicators;

/// <summary>Conteúdo dos indicadores e montagem do estado para os testes.</summary>
internal static class IndicatorKit
{
    private static readonly Lazy<IndicatorContent> LazyContent = new(() => IndicatorContent.LoadOrThrow(TestContent.DataDirectory));

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, IndicadoresUfFile> Ufs = new();

    public static IndicatorContent Content => LazyContent.Value;

    public static readonly DateOnly Start = new(2028, 1, 3);

    public static IndicadoresUfFile Uf(string uf) => Ufs.GetOrAdd(uf, u => IndicadoresPaths.Load(TestContent.DataDirectory, u).GetValueOrThrow());

    public static MunicipalIndicatorsState State(Municipio m, ulong seed) =>
        IndicatorSetup.Create(Content, m, Uf(m.Uf), CampaignKit.C.Municipios, Start, seed);

    public static IndicatorEvolution Evolution(Municipio m) => new(Content, m, CampaignKit.C.Projection);

    /// <summary>Um município de cada porte, de UFs diferentes, mais um com lacuna de PIB ou de saneamento, se houver.</summary>
    public static List<Municipio> Sample()
    {
        var all = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais).OrderBy(m => m.CodigoIbge).ToList();
        var sample = new List<Municipio>();
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            List<Municipio> ofPorte = all.Where(m => m.Porte == porte).ToList();
            sample.Add(ofPorte[ofPorte.Count / 5]);
            sample.Add(ofPorte[ofPorte.Count * 4 / 5]);
        }

        return sample.DistinctBy(m => m.CodigoIbge).ToList();
    }
}

/// <summary>Indicadores em unidade real com modelo de evolução (GDD 17.17).</summary>
public class IndicatorEvolutionTests(ITestOutputHelper output)
{
    private static IndicatorDefinitions Definitions => IndicatorKit.Content.Definitions;

    private static IndicatorModelConfig Model => IndicatorKit.Content.Model;

    [Fact]
    public void Valor_inicial_e_igual_ao_dado_importado()
    {
        // Três municípios de cada UF (o primeiro, o do meio e o último pelo código), mais a amostra por porte.
        var municipios = CampaignKit.C.Municipios.All.GroupBy(m => m.Uf)
            .SelectMany(g => { var l = g.OrderBy(m => m.CodigoIbge).ToList(); return new[] { l[0], l[l.Count / 2], l[^1] }; })
            .Concat(IndicatorKit.Sample()).DistinctBy(m => m.CodigoIbge).ToList();
        Assert.True(municipios.Count >= 27);
        foreach (Municipio m in municipios)
        {
            MunicipalIndicatorsState state = IndicatorKit.State(m, 1);
            Dictionary<string, decimal> imported = IndicatorDefinitions.RealValues(IndicatorKit.Uf(m.Uf).Municipios.First(i => i.CodigoIbge == m.CodigoIbge), m);
            Assert.Equal(IndicatorDefinitions.Ids, state.Indicadores.Select(i => i.Id));
            foreach (IndicatorValue v in state.Indicadores)
            {
                Assert.Equal(v.Partida, v.Valor);
                Assert.Equal(v.Partida, v.InicioMandato);
                if (imported.TryGetValue(v.Id, out decimal value))
                {
                    Assert.Equal((double)value, v.Valor);
                    Assert.True(v.AnoFonte is >= 2005 and <= 2026, $"{m.CodigoIbge} {v.Id}: ano {v.AnoFonte}");
                }
                else
                {
                    // Lacuna: preenchida pela regra da configuração e marcada como estimativa.
                    Assert.True(v.Estimativa, $"{m.CodigoIbge} {v.Id}");
                    Assert.False(string.IsNullOrWhiteSpace(v.RegraEstimativa));
                }

                IndicatorReading reading = IndicatorQueries.Current(state, Definitions, v.Id);
                Assert.False(reading.Simulado);
                Assert.False(string.IsNullOrWhiteSpace(reading.Unidade));
                Assert.False(string.IsNullOrWhiteSpace(reading.Fonte));
            }

            Assert.Equal(m.Populacao, state.Populacao);
        }
    }

    [Fact]
    public void Lacuna_e_preenchida_pela_regra_e_marcada_como_estimativa()
    {
        // O índice do jogo nunca tem dado importado; quem não entregou o RGF não tem despesa com pessoal.
        Municipio semRgf = CampaignKit.C.Municipios.All.OrderBy(m => m.CodigoIbge)
            .First(m => IndicatorKit.Uf(m.Uf).Municipios.First(i => i.CodigoIbge == m.CodigoIbge).Financas.PessoalPctRcl is null);
        MunicipalIndicatorsState state = IndicatorKit.State(semRgf, 1);
        IndicatorValue pessoal = state.Get(IndicatorDefinitions.PessoalRcl);
        Assert.True(pessoal.Estimativa);
        Assert.Contains(pessoal.RegraEstimativa, new[] { IndicatorSetup.RegraMedianaPorteUf, IndicatorSetup.RegraMedianaUf });
        Assert.Equal(pessoal.ReferenciaPartida, pessoal.Partida);
        RealIndicatorDefinition d = Definitions.Get(IndicatorDefinitions.PessoalRcl);
        Assert.InRange(pessoal.Partida, (double)d.Min, (double)d.Max);

        IndicatorValue social = state.Get(IndicatorDefinitions.ProtecaoSocial);
        Assert.True(social.Estimativa);
        Assert.Equal(IndicatorSetup.RegraIndiceDoJogo, social.RegraEstimativa);
        Assert.Equal(Model.Of(IndicatorDefinitions.ProtecaoSocial).Valor, social.Partida);

        // Substituição já feita pelo importador também aparece como estimativa.
        Municipio regiao = CampaignKit.C.Municipios.All.OrderBy(m => m.CodigoIbge)
            .First(m => IndicatorKit.Uf(m.Uf).Municipios.First(i => i.CodigoIbge == m.CodigoIbge).Saude.MortalidadeInfantilFonte == SaudeMunicipal.DaRegiaoImediata);
        Assert.True(IndicatorKit.State(regiao, 1).Get(IndicatorDefinitions.MortalidadeInfantil).Estimativa);
    }

    [Fact]
    public void Conversao_de_ida_e_volta_nao_perde_o_valor()
    {
        int checkedValues = 0;
        double worst = 0;
        foreach (Municipio m in CampaignKit.C.Municipios.All)
        {
            IndicadoresMunicipio i = IndicatorKit.Uf(m.Uf).Municipios.First(x => x.CodigoIbge == m.CodigoIbge);
            foreach (var (id, value) in IndicatorDefinitions.RealValues(i, m))
            {
                RealIndicatorDefinition d = Definitions.Get(id);
                double real = (double)value;
                double back = IndicatorScale.FromGame(d, IndicatorScale.ToGame(d, real));
                // "Sem perda" = até a precisão do ponto flutuante: o erro relativo da volta fica abaixo de 1 em 100 bilhões.
                double error = Math.Abs(back - real) / Math.Max(1, Math.Abs(real));
                worst = Math.Max(worst, error);
                Assert.True(error <= 1e-11, $"{m.CodigoIbge} {id}: {real:R} virou {back:R}");
                checkedValues++;
            }
        }

        Assert.True(checkedValues > 50_000);
        output.WriteLine($"{checkedValues} valores importados convertidos e trazidos de volta; maior erro relativo: {worst:E2}");
        foreach (RealIndicatorDefinition d in Definitions.Indicadores)
        {
            for (double game = -50; game <= 150; game += 2.5)
            {
                Assert.Equal(game, IndicatorScale.ToGame(d, IndicatorScale.FromGame(d, game)), 9);
            }

            // 0 é o pior limite e 100 o melhor, qualquer que seja o sentido do indicador.
            Assert.Equal(d.MaiorMelhor ? d.Piso : d.Teto, IndicatorScale.FromGame(d, 0), 9);
            Assert.Equal(d.MaiorMelhor ? d.Teto : d.Piso, IndicatorScale.FromGame(d, 100), 6);
        }
    }

    /// <summary>40 anos, semana a semana, em 8 municípios e 5 sementes, sem prefeito (só tendência, ciclo e choques) e com governos que vêm e vão.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Evolucao_fica_dentro_dos_limites_plausiveis_em_40_anos(bool comPressoes)
    {
        var ranges = IndicatorDefinitions.Ids.ToDictionary(id => id, _ => (Min: double.MaxValue, Max: double.MinValue, Desvio: 0.0));
        List<Municipio> municipios = IndicatorKit.Sample();
        int runs = 0;
        foreach (Municipio m in municipios)
        {
            IndicatorEvolution evolution = IndicatorKit.Evolution(m);
            for (ulong seed = 1; seed <= 5; seed++)
            {
                MunicipalIndicatorsState state = IndicatorKit.State(m, seed);
                var pressureRng = new Rng(seed + 900);
                DateOnly end = IndicatorKit.Start.AddYears(40);
                while (state.UltimoPasso < end)
                {
                    // Governos que vêm e vão: de tempos em tempos, uma pressão forte, para um lado ou para o outro.
                    if (comPressoes && pressureRng.Chance(0.02))
                    {
                        string id = pressureRng.Pick(IndicatorDefinitions.Ids);
                        evolution.ApplyPressure(state, id, pressureRng.NextDouble(-40, 40), pressureRng.NextInt(26, 209), state.UltimoPasso.AddDays(7), "teste", "pressão de teste");
                    }

                    evolution.StepWeek(state);
                    foreach (IndicatorValue v in state.Indicadores)
                    {
                        RealIndicatorDefinition d = Definitions.Get(v.Id);
                        var (min, max) = IndicatorScale.Bounds(d, v.Partida);
                        Assert.True(double.IsFinite(v.Valor) && v.Valor >= min && v.Valor <= max, $"{m.Nome} semente {seed} {state.UltimoPasso}: {v.Id} = {v.Valor} fora de [{min}, {max}]");
                        Assert.True(v.Referencia >= (double)d.Min && v.Referencia <= (double)d.Max);
                        var r = ranges[v.Id];
                        double deviation = Math.Abs(IndicatorScale.ToGame(d, v.Valor) - IndicatorScale.ToGame(d, v.Partida));
                        ranges[v.Id] = (Math.Min(r.Min, v.Valor), Math.Max(r.Max, v.Valor), Math.Max(r.Desvio, deviation));
                    }
                }

                Assert.True(state.Populacao > 0);
                Assert.Equal(CampaignKit.C.Projection.Population(m, state.UltimoPasso.Year), state.Populacao);
                Assert.True(Math.Abs(state.Ciclo.Hiato) <= IndicatorKit.Content.Cycle.HiatoMaximo);
                runs++;
            }
        }

        output.WriteLine($"{(comPressoes ? "Com pressões sorteadas" : "Sem prefeito")}: {runs} simulações de 40 anos em {municipios.Count} municípios: {string.Join(", ", municipios.Select(m => $"{m.Nome}/{m.Uf} ({m.Porte})"))}");
        foreach (var (id, r) in ranges)
        {
            RealIndicatorDefinition d = Definitions.Get(id);
            output.WriteLine($"{id}: de {r.Min:0.###} a {r.Max:0.###} {d.Unidade} (limites {d.Piso} a {d.Teto}); maior afastamento da partida: {r.Desvio:0.#} pontos de jogo");
            Assert.True(r.Max > r.Min, $"{id} não se mexeu em 40 anos");

            // Sem prefeito, o mundo se mexe mas não dispara: ninguém se afasta mais de 40 pontos do ponto de partida.
            Assert.True(comPressoes || r.Desvio < 40, $"{id}: afastou {r.Desvio:0.#} pontos sem nenhuma pressão");
        }
    }

    [Fact]
    public void Pressao_extrema_por_40_anos_para_no_limite_plausivel()
    {
        Municipio m = IndicatorKit.Sample()[2];
        IndicatorEvolution evolution = IndicatorKit.Evolution(m);
        foreach (double points in new[] { 500.0, -500.0 })
        {
            MunicipalIndicatorsState state = IndicatorKit.State(m, 7);
            foreach (string id in IndicatorDefinitions.Ids)
            {
                evolution.ApplyPressure(state, id, points, null, IndicatorKit.Start, "teste", "pressão extrema");
            }

            evolution.AdvanceTo(state, IndicatorKit.Start.AddYears(40));
            foreach (IndicatorValue v in state.Indicadores)
            {
                RealIndicatorDefinition d = Definitions.Get(v.Id);
                var (min, max) = IndicatorScale.Bounds(d, v.Partida);
                bool best = points > 0;
                Assert.Equal(best == d.MaiorMelhor ? max : min, v.Valor, 6);
                Assert.Contains(IndicatorQueries.LastChange(state, v.Id)!.Fatores, f => f.Tipo == IndicatorFactor.Limite);
            }
        }
    }

    [Fact]
    public void Pressao_move_o_indicador_no_prazo_e_aparece_na_explicacao()
    {
        Municipio m = IndicatorKit.Sample()[1];
        IndicatorEvolution evolution = IndicatorKit.Evolution(m);
        MunicipalIndicatorsState control = IndicatorKit.State(m, 11);
        MunicipalIndicatorsState pressed = IndicatorKit.State(m, 11);
        const string Id = IndicatorDefinitions.MortalidadeInfantil;
        IndicatorPressure pressure = evolution.ApplyPressure(pressed, Id, 15, 104, IndicatorKit.Start, "politica:teste", "Programa de teste");
        Assert.Equal(IndicatorKit.Start.AddDays((7 * 104) - 1), pressure.Ate);

        evolution.AdvanceTo(control, IndicatorKit.Start.AddDays(7 * 100));
        evolution.AdvanceTo(pressed, IndicatorKit.Start.AddDays(7 * 100));

        // Mortalidade infantil: melhorar é cair. Os outros indicadores seguem iguais (mesma semente, mesmos sorteios).
        Assert.True(pressed.Get(Id).Valor < control.Get(Id).Valor - 0.5, $"{pressed.Get(Id).Valor} x {control.Get(Id).Valor}");
        Assert.Equal(control.Get(IndicatorDefinitions.Ideb).Valor, pressed.Get(IndicatorDefinitions.Ideb).Valor);

        IndicatorVariation last = IndicatorQueries.LastChange(pressed, Id)!;
        IndicatorFactor factor = Assert.Single(last.Fatores, f => f.Tipo == IndicatorFactor.Pressao);
        Assert.Equal("politica:teste", factor.Origem);
        Assert.Equal("Programa de teste", factor.Texto);
        Assert.True(factor.Pontos > 0 && factor.Delta < 0);
        Assert.Equal(last.Depois - last.Antes, last.Fatores.Sum(f => f.Delta), 9);
        IndicatorVariation year = IndicatorQueries.YearChange(pressed, Id)!;
        Assert.Equal(year.Depois - year.Antes, year.Fatores.Sum(f => f.Delta), 6);

        // Acabado o prazo, a pressão sai e o indicador volta a andar junto com o controle.
        evolution.AdvanceTo(control, IndicatorKit.Start.AddYears(12));
        evolution.AdvanceTo(pressed, IndicatorKit.Start.AddYears(12));
        Assert.Empty(pressed.Pressoes);
        Assert.Equal(control.Get(Id).Valor, pressed.Get(Id).Valor, 2);
    }

    [Fact]
    public void Consultas_dao_variacao_no_mandato_comparacao_com_a_uf_e_fatores()
    {
        Municipio m = IndicatorKit.Sample()[3];
        IndicatorEvolution evolution = IndicatorKit.Evolution(m);
        MunicipalIndicatorsState state = IndicatorKit.State(m, 5);
        evolution.AdvanceTo(state, IndicatorKit.Start.AddYears(1));
        DateOnly posse = state.UltimoPasso;
        IndicatorEvolution.MarkMandateStart(state, posse);
        double scoreAtStart = IndicatorQueries.GameScore(state, Definitions, Model, "saude")!.Value;
        evolution.ApplyGamePressure(state, "saude", 20, null, posse, "obra:1", "Hospital novo");
        evolution.AdvanceTo(state, posse.AddYears(2));

        foreach (string id in new[] { IndicatorDefinitions.MortalidadeInfantil, IndicatorDefinitions.Leitos })
        {
            IndicatorChange change = IndicatorQueries.MandateChange(state, Definitions, id);
            Assert.Equal(posse, change.Desde);
            Assert.Equal(change.Agora - change.Antes, change.Delta);
            Assert.True(change.Melhorou, id);
        }

        IndicatorComparison comparison = IndicatorQueries.CompareUf(state, Definitions, IndicatorDefinitions.Leitos);
        Assert.Equal(m.Uf, comparison.Uf);
        Assert.True(comparison.Municipios >= 1);
        Assert.Equal(comparison.Valor - comparison.Referencia, comparison.Diferenca);

        IndicatorReading reading = IndicatorQueries.Current(state, Definitions, IndicatorDefinitions.Leitos);
        Assert.True(reading.Simulado);
        Assert.Equal("leitos por mil habitantes", reading.Unidade);
        Assert.Equal(11, IndicatorQueries.All(state, Definitions).Count);

        // A nota que o eleitor julga sobe com a melhora no mandato, mesmo partindo de qualquer nível.
        Assert.True(IndicatorQueries.GameScore(state, Definitions, Model, "saude") > scoreAtStart + 5);
        Assert.Equal(2, IndicatorEvolution.RemovePressure(state, "obra:1"));
        Assert.Null(IndicatorQueries.GameScore(state, Definitions, Model, "inexistente"));
    }

    [Fact]
    public void Todo_indicador_da_prefeitura_tem_mapa_para_os_indicadores_reais()
    {
        foreach (IndicatorDefinition indicator in CampaignKit.C.Executive.Config.Indicators)
        {
            Assert.True(Model.Jogo.ContainsKey(indicator.Id), indicator.Id);
        }
    }

    [Fact]
    public void Save_e_load_no_meio_terminam_iguais()
    {
        Municipio m = IndicatorKit.Sample()[0];
        IndicatorEvolution evolution = IndicatorKit.Evolution(m);
        GameState state = CampaignKit.NewCareer(m.CodigoIbge, 3).Context.State;
        state.Indicators = IndicatorKit.State(m, 3);
        evolution.ApplyPressure(state.Indicators, IndicatorDefinitions.RedeEsgoto, 12, 300, IndicatorKit.Start, "obra:2", "Rede de esgoto");
        evolution.AdvanceTo(state.Indicators, IndicatorKit.Start.AddYears(3));

        GameState copy = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        evolution.AdvanceTo(state.Indicators, IndicatorKit.Start.AddYears(9));
        evolution.AdvanceTo(copy.Indicators!, IndicatorKit.Start.AddYears(9));
        Assert.Equal(GameStateSerializer.Serialize(state), GameStateSerializer.Serialize(copy));
    }

    [Fact]
    public void Prefeitura_le_a_escala_de_jogo_derivada_do_valor_real()
    {
        var (state, _) = ExecutiveTests.ElectedMayor();
        Municipio m = CampaignKit.Municipio(state.FocusMunicipio);
        var evolution = new IndicatorEvolution(IndicatorKit.Content, m, CampaignKit.C.Projection);
        state.Indicators = IndicatorSetup.Create(IndicatorKit.Content, m, IndicatorKit.Uf(m.Uf), CampaignKit.C.Municipios, state.Calendar.WeekStart, state.World!.Seed);

        ExecutiveEngine Engine(GameState s) => new(WithEvolution(CampaignKit.ExecutiveContext(s), evolution));
        GameState other = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        ExecutiveEngine engine = Engine(state);
        ExecutiveEngine spender = Engine(other);
        Assert.True(engine.UsesRealIndicators);

        // Mais dinheiro na saúde, tirado da reserva e do custeio dentro do mínimo.
        var allocation = new Dictionary<string, double>(other.Executive!.Allocation, StringComparer.Ordinal);
        allocation["saude"] += 0.08;
        allocation["reserva"] -= 0.08;
        Assert.Null(spender.SetAllocation(allocation));

        for (int week = 0; week < 150; week++)
        {
            engine.ResolveWeek(automatic: true);
            state.Calendar.AdvanceWeek();
            spender.ResolveWeek(automatic: true);
            other.Calendar.AdvanceWeek();
        }

        MunicipalIndicatorsState real = state.Indicators;
        Assert.Equal(state.Executive!.Start, real.MandatoDesde);
        Assert.Equal(state.Calendar.WeekStart.AddDays(-7), real.UltimoPasso);
        foreach (IndicatorDefinition indicator in CampaignKit.C.Executive.Config.Indicators)
        {
            double score = IndicatorQueries.GameScore(real, Definitions, Model, indicator.Id)!.Value;
            var bounds = CampaignKit.C.Executive.Config.IndicatorBounds;
            Assert.Equal(Math.Clamp(score, bounds.Min, bounds.Max), state.Executive.Indicators[indicator.Id], 9);
        }

        // Quem pôs mais dinheiro na saúde tem mortalidade infantil menor, mais leitos e nota de saúde maior.
        Assert.True(other.Indicators!.Get(IndicatorDefinitions.MortalidadeInfantil).Valor < real.Get(IndicatorDefinitions.MortalidadeInfantil).Valor);
        Assert.True(other.Indicators.Get(IndicatorDefinitions.Leitos).Valor >= real.Get(IndicatorDefinitions.Leitos).Valor);
        Assert.True(other.Executive.Indicators["saude"] > state.Executive.Indicators["saude"]);
        Assert.Contains(other.Indicators.Pressoes, p => p.Origem == ExecutiveEngine.BudgetOrigin + "saude" && p.Pontos > 0);
        IndicatorVariation year = IndicatorQueries.YearChange(other.Indicators, IndicatorDefinitions.MortalidadeInfantil)!;
        Assert.Contains(year.Fatores, f => f.Tipo == IndicatorFactor.Pressao && f.Origem == ExecutiveEngine.BudgetOrigin + "saude");
    }

    private static ExecutiveContext WithEvolution(ExecutiveContext ctx, IndicatorEvolution evolution) => new()
    {
        State = ctx.State,
        Municipio = ctx.Municipio,
        Content = ctx.Content,
        Legislature = ctx.Legislature,
        Electorate = ctx.Electorate,
        RecognitionScale = ctx.RecognitionScale,
        Indicadores = ctx.Indicadores,
        IndicatorEvolution = evolution,
    };
}
