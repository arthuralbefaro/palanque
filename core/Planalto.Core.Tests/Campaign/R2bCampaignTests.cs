using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Events;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Campaign;

/// <summary>
/// A R2b inteira: campanha jogada sozinha com equipe, verba, plataforma e objetivos em 3 sementes por porte (com
/// save e load no meio), previsão de todo comando novo com o resultado dentro da faixa, e o lint das decisões.
/// </summary>
public class R2bCampaignTests
{
    private static int CityOf(string porte, int skip = 0) =>
        CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == porte).OrderBy(m => m.CodigoIbge).Skip(skip).First().CodigoIbge;

    public static TheoryData<string, ulong> Cases()
    {
        var data = new TheoryData<string, ulong>();
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            foreach (ulong seed in new ulong[] { 101, 102, 103 })
            {
                data.Add(porte, seed);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Campanha_inteira_jogada_sozinha_com_save_e_load_no_meio(string porte, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(CityOf(porte, (int)(seed % 3)), seed);

        // Caixa de partida folgado, no porte da cidade, para o robô usar todos os sistemas (equipe, verba, cabos).
        double scale = engine.Context.Config.PorteCostMultiplier[engine.Context.Municipio.Porte];
        engine.Context.State.Player.Resources.Money = (decimal)(150_000 * scale);
        var robot = new CampaignRobot(engine, seed, new RobotOptions { R2b = true });
        var kinds = new HashSet<string>();
        int loads = 0;
        robot.PlayToElection(week =>
        {
            CampaignAutoplayTests.AssertState(robot.Engine);
            kinds.UnionWith(robot.Engine.Objectives.Active.Select(o => o.Kind));
            if (week is 9 or 26 or 36)
            {
                // Salva, carrega e segue jogando no estado carregado: tudo o que é novo tem de ir e voltar.
                string json = GameStateSerializer.Serialize(robot.Engine.Context.State);
                GameState loaded = GameStateSerializer.Deserialize(json);
                Assert.Equal(json, GameStateSerializer.Serialize(loaded));
                robot.Engine = CampaignEngine.Start(CampaignKit.Context(loaded));
                Assert.Same(loaded, robot.Engine.Context.State);
                loads++;
            }
        });

        engine = robot.Engine;
        GameState state = engine.Context.State;
        CampaignAutoplayTests.AssertState(engine);
        Assert.Equal(3, loads);
        robot.Vote();
        Assert.Equal(CampaignPhase.Encerrada, state.Campaign!.Phase);
        Assert.NotNull(state.Campaign.Outcome);
        string final = GameStateSerializer.Serialize(state);
        Assert.Equal(final, GameStateSerializer.Serialize(GameStateSerializer.Deserialize(final)));

        // O robô de fato usou os sistemas novos.
        Assert.True(robot.R2bCommands.GetValueOrDefault("contratar") > 0, "Nenhuma contratação na campanha.");
        Assert.True(robot.R2bCommands.Where(c => c.Key.StartsWith("verba_", StringComparison.Ordinal)).Sum(c => c.Value) > 0, "Nenhuma verba repartida.");
        Assert.True(robot.R2bCommands.GetValueOrDefault("posicao") + robot.R2bCommands.GetValueOrDefault("promessa") + robot.R2bCommands.GetValueOrDefault("bandeiras") > 0, "Plataforma intocada.");
        Assert.True(state.Campaign.Budget.Spent > 0);

        // Toda promessa e toda mudança de posição do robô estão no Livro.
        Assert.True(state.Ledger.Promises.Count >= robot.R2bCommands.GetValueOrDefault("promessa"));
        Assert.True(state.Ledger.Positions.Count >= robot.R2bCommands.GetValueOrDefault("posicao"));

        // Princípio h: 4 ou mais tipos de objetivo numa campanha.
        kinds.UnionWith(state.Campaign.Objectives.History.Select(o => o.Kind));
        Assert.True(kinds.Count >= 4, $"Só {kinds.Count} tipos de objetivo: {string.Join(", ", kinds)}.");
    }

    [Theory]
    [InlineData("pequeno")]
    [InlineData("medio")]
    [InlineData("grande")]
    [InlineData("metropole")]
    public void Todo_comando_novo_tem_previsao_com_faixa_e_explicacao_e_o_resultado_cai_na_faixa(string porte)
    {
        int total = 0;
        int inside = 0;
        var outside = new List<string>();
        void Check(bool ok, string what)
        {
            total++;
            inside += ok ? 1 : 0;
            if (!ok)
            {
                outside.Add(what);
            }
        }

        static void Explained(CommandForecast f)
        {
            Assert.NotEmpty(f.Explanation);
            Assert.All(f.Explanation, line =>
            {
                Assert.False(string.IsNullOrWhiteSpace(line));
                Assert.DoesNotContain("{", line);
            });
            Assert.NotEmpty(f.Lines);
        }

        foreach (ulong seed in new ulong[] { 111, 112, 113 })
        {
            CampaignEngine engine = ChannelTests.InCampaign(porte, seed);
            GameState state = engine.Context.State;

            // Contratar: a qualidade real cai na faixa mostrada, e a folha prevista é a que fica.
            foreach (StaffRole role in engine.Context.Campaign.Staff.Funcoes)
            {
                StaffView candidate = engine.StaffCandidates(role.Id)[0];
                CommandForecast hire = engine.ForecastHire(candidate.Member.PersonId);
                Explained(hire);
                Assert.True(hire.Line("quality")!.Max > hire.Line("quality")!.Min, "Qualidade em faixa.");
                if (hire.Blocked is not null)
                {
                    Assert.Equal(CommandFailure.SemVaga, hire.Blocked);
                    continue;
                }

                Assert.Null(engine.Hire(candidate.Member.PersonId).Failure);
                Check(hire.Contains("quality", null, candidate.Member.Quality) && hire.Contains("payroll", null, (double)engine.Payroll().Weekly), "contratar " + role.Id);
            }

            // Evento da equipe: a lealdade muda o que a previsão disse.
            if (engine.TriggerStaffEvent(StaffEventDefinition.Briga) is { } fight)
            {
                StaffMember first = engine.Staff(fight.PersonId)!;
                StaffMember second = engine.Staff(fight.OtherPersonId!)!;
                int[] before = [first.Loyalty, second.Loyalty];
                CommandForecast side = engine.ForecastStaffDecision(fight.Id, "nenhum_lado");
                Explained(side);
                engine.AnswerStaffDecision(fight.Id, "nenhum_lado");
                Check(side.Contains("loyalty", first.PersonId, first.Loyalty - before[0]) && side.Contains("loyalty", second.PersonId, second.Loyalty - before[1]), "briga");
            }

            // Mudar de posição: imagem, incoerência e votos por grupo e por zona.
            foreach (var theme in engine.Context.Themes.Themes.Take(3))
            {
                engine.Platform.Positions[theme.Id] = engine.PositionOf(theme.Id);
                engine.Platform.Known[theme.Id] = 0.35;
                int value = engine.PositionOf(theme.Id) > 0 ? -1 : 2;
                var shares = engine.PlayerSharesNow();
                double image = state.Campaign!.Player.Image;
                CommandForecast change = engine.ForecastPosition(theme.Id, value);
                Explained(change);
                Assert.Contains(change.Lines, l => l.Kind == "votes_group");
                Assert.Contains(change.Lines, l => l.Kind == "votes_zone");
                var (failure, result) = engine.SetPosition(theme.Id, value);
                Assert.Null(failure);
                var after = engine.PlayerSharesNow();
                Check(change.Contains("image", null, state.Campaign.Player.Image - image)
                    && change.Contains("incoherence", theme.Id, result!.Change.Incoherence)
                    && change.Contains("votes", null, (after.City - shares.City) * 100)
                    && after.ByGroup.All(g => change.Contains("votes_group", g.Key, (g.Value - shares.ByGroup[g.Key]) * 100))
                    && after.ByZone.All(z => change.Contains("votes_zone", z.Key, (z.Value - shares.ByZone[z.Key]) * 100)), "posicao " + theme.Id);
            }

            // Prometer: o apoio imediato cai na faixa, e a credibilidade é a prevista.
            foreach (string zone in engine.Territory.Zones.Take(3))
            {
                PromiseOption? option = engine.PromiseOptions(Promise.ParaZona, zone).FirstOrDefault(o => o.Blocked is null);
                if (option is null)
                {
                    continue;
                }

                CommandForecast promise = engine.ForecastPromise(option.Definition.Id, Promise.ParaZona, zone);
                Explained(promise);
                Assert.Contains(promise.Lines, l => l.Kind == "support" && l.Max > l.Min);
                var (failure, result) = engine.MakePromise(option.Definition.Id, Promise.ParaZona, zone);
                Assert.Null(failure);
                Check(result!.Support.All(s => promise.Contains("support", s.Key, s.Value)) && promise.Contains("credibility", null, result.Credibility), "promessa " + option.Definition.Id);
            }

            // Repartir verba: o que cada canal rende na semana cai na faixa da previsão.
            var plan = new List<(string Channel, string Target, decimal Amount)>
            {
                (ChannelDefinition.Rua, engine.Territory.Zones[0], engine.ChannelStep * 8),
                (ChannelDefinition.Material, engine.Territory.Zones[^1], engine.ChannelStep * 5),
                (ChannelDefinition.Redes, engine.ChannelTargets(ChannelDefinition.Redes)[(int)(seed % 3)].Id, engine.ChannelStep * 6),
            };
            if (engine.CanAllocate(ChannelDefinition.Cabos, engine.Territory.Zones[0], engine.CabosGroupCost) is null)
            {
                plan.Add((ChannelDefinition.Cabos, engine.Territory.Zones[0], engine.CabosGroupCost));
            }

            foreach (var (channel, target, amount) in plan)
            {
                Assert.Null(engine.SetAllocation(channel, target, amount));
            }

            var forecasts = plan.ToDictionary(p => p.Channel, p => engine.ForecastChannel(p.Channel, p.Target, p.Amount));
            foreach (CommandForecast f in forecasts.Values)
            {
                Explained(f);
                Assert.Contains(f.Lines, l => l.Kind is "presence" or "support" && l.Max > l.Min);
            }

            StaffTests.Week(engine);
            Assert.Equal(plan.Count, engine.Budget.LastResults.Count);
            double pop = engine.Territory.Population.Sum(x => (double)x);
            foreach (ChannelResult r in engine.Budget.LastResults)
            {
                CommandForecast f = forecasts[r.ChannelId];
                bool presence = r.Presence.Count == 0
                    || (r.ChannelId == ChannelDefinition.Redes
                        ? f.Contains("presence", null, r.Presence.Sum(p => p.Value * engine.Territory.Population[engine.Territory.IndexOf(p.Key)]) / pop)
                        : f.Contains("presence", r.TargetId, r.Presence[r.TargetId]));
                Check(presence && r.Support.All(s => f.Contains("support", s.Key, s.Value)) && Math.Abs(f.Line("effect")!.Expected - r.Effect) < 1e-9, "verba " + r.ChannelId);
            }
        }

        Assert.True(total >= 30, $"Só {total} comandos conferidos.");
        Assert.True(inside == total, $"{inside} de {total} resultados dentro da faixa prevista; fora: {string.Join(", ", outside)}.");
    }

    [Fact]
    public void Lint_toda_decisao_tem_ganho_e_custo_de_naturezas_diferentes()
    {
        CampaignContent campaign = CampaignKit.C.Campaign;
        EventContent events = CampaignKit.C.Events;
        Assert.Empty(DecisionLint.Run(campaign, events));

        // O lint cobre os eventos da equipe, as quatro respostas a cada pedido de liderança e as escolhas relevantes.
        int staff = campaign.Staff.Eventos.Sum(e => e.Opcoes.Count);
        int relevant = events.Events.Sum(e => e.Choices.Count(c => c.Relevant));
        Assert.True(staff >= 12);
        Assert.True(relevant >= 2);
        Assert.Equal(staff + (campaign.Leaders.Pedidos.Count * 4) + relevant, DecisionLint.Count(campaign, events));
        Assert.All(campaign.Staff.Eventos.SelectMany(e => e.Opcoes), o =>
        {
            Assert.NotEmpty(o.Ganho);
            Assert.NotEmpty(o.Custo);
            Assert.Empty(o.Ganho.Intersect(o.Custo));
            Assert.All(o.Ganho.Concat(o.Custo), n => Assert.Contains(n, DecisionNature.All));
            Assert.True(o.Rotulo.Length <= 40);
        });
        Assert.All(campaign.Leaders.Pedidos, p => Assert.All(LeaderCatalog.AnswerKeys, a =>
        {
            DecisionTrade trade = campaign.Leaders.TradeOf(p, a)!;
            Assert.NotEmpty(trade.Ganho);
            Assert.NotEmpty(trade.Custo);
            Assert.Empty(trade.Ganho.Intersect(trade.Custo));
        }));

        // A tela lê a troca de cada resposta pelo motor.
        CampaignEngine engine = StaffTests.Fresh(CityOf("medio"), 5);
        Leader leader = engine.City.Leaders[0];
        Assert.All(Enum.GetValues<LeaderAnswer>(), a => Assert.NotNull(engine.LeaderAnswerTrade(leader, a)));
    }

    [Fact]
    public void Lint_reprova_decisao_sem_troca_ou_sem_rastro()
    {
        string staffJson = File.ReadAllText(Path.Combine(TestContent.DataDirectory, StaffCatalog.File));

        // Ganho e custo da mesma natureza.
        var same = JsonData.Parse<StaffCatalog>(staffJson.Replace("\"ganho\": [\"relacao\"], \"custo\": [\"dinheiro\"]", "\"ganho\": [\"dinheiro\"], \"custo\": [\"dinheiro\"]", StringComparison.Ordinal), "staff.json");
        Assert.Contains(same.Errors, e => e.Contains("mesma natureza", StringComparison.Ordinal));

        // Custo declarado que o efeito não sustenta.
        var unbacked = JsonData.Parse<StaffCatalog>(staffJson.Replace("\"custo\": [\"zona\"]", "\"custo\": [\"tempo\"]", StringComparison.Ordinal), "staff.json");
        Assert.Contains(unbacked.Errors, e => e.Contains("custo 'tempo' declarado sem efeito", StringComparison.Ordinal));

        // Opção sem ganho.
        var empty = JsonData.Parse<StaffCatalog>(staffJson.Replace("\"ganho\": [\"tempo\"]", "\"ganho\": []", StringComparison.Ordinal), "staff.json");
        Assert.Contains(empty.Errors, e => e.Contains("falta o ganho ou o custo", StringComparison.Ordinal));

        // Pedido de liderança: resposta sem custo.
        string leadersJson = File.ReadAllText(Path.Combine(TestContent.DataDirectory, LeaderCatalog.File));
        var leaders = JsonData.Parse<LeaderCatalog>(leadersJson.Replace("\"recusar\": { \"ganho\": [\"posicao\"], \"custo\": [\"relacao\"] }", "\"recusar\": { \"ganho\": [\"posicao\"], \"custo\": [] }", StringComparison.Ordinal), "leaders.json");
        Assert.Contains(leaders.Errors, e => e.Contains("resposta 'recusar'", StringComparison.Ordinal));

        // Escolha de evento marcada como relevante: sem rastro e com custo que os efeitos não sustentam.
        var content = new EventContent
        {
            Config = CampaignKit.C.Events.Config,
            Events =
            [
                new EventDefinition
                {
                    Id = "teste_relevante",
                    Category = "campanha",
                    Phases = ["campanha"],
                    Rarity = Rarity.Comum,
                    CooldownWeeks = 1,
                    Title = "t",
                    Variants = ["texto"],
                    Choices =
                    [
                        new EventChoice { Label = "Sem rastro", Relevant = true, Gain = ["dinheiro"], Cost = ["risco"], Effects = new Effects { Money = 100 } },
                        new EventChoice { Label = "Com rastro", Relevant = true, Gain = ["dinheiro"], Cost = ["risco"], Effects = new Effects { Money = 100, AddSkeleton = new Skeleton("caixa_dois", 2) } },
                        new EventChoice { Label = "Comum", Effects = new Effects { Money = -100 } },
                    ],
                },
            ],
        };
        List<string> errors = [.. DecisionLint.Events(content)];
        Assert.Contains(errors, e => e.Contains("escolha 1", StringComparison.Ordinal) && e.Contains("sem rastro", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("escolha 1", StringComparison.Ordinal) && e.Contains("custo 'risco'", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, e => e.Contains("escolha 2", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, e => e.Contains("escolha 3", StringComparison.Ordinal));
    }
}
