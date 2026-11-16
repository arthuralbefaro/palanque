using Planalto.Core.Campaign;
using Planalto.Core.Electorate;
using Planalto.Core.Ideology;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Plataforma (GDD 17.9): posições, posição conhecida, custo de mudar, bandeiras, promessas locais e credibilidade.</summary>
public class PlatformTests
{
    private static int CityOf(string porte, int skip = 3) =>
        CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == porte).OrderBy(m => m.CodigoIbge).Skip(skip).First().CodigoIbge;

    private static CampaignEngine Fresh(string porte, ulong seed) => StaffTests.Fresh(CityOf(porte), seed);

    /// <summary>Tema em que o partido do jogador tem lado, e uma posição do outro lado (salto grande).</summary>
    private static (string Theme, int Opposite) FarTheme(CampaignEngine engine)
    {
        string theme = engine.Context.Themes.Themes.MaxBy(t => Math.Abs(engine.PartyPosition(t.Id)))!.Id;
        return (theme, engine.PartyPosition(theme) > 0 ? ThemeDefinition.MinPosition : ThemeDefinition.MaxPosition);
    }

    private static IReadOnlyDictionary<string, double> Votes(CampaignEngine engine) =>
        engine.Project(engine.Context.State.Campaign!.Player.Office, null).Votes;

    [Fact]
    public void A_posicao_padrao_e_a_do_partido()
    {
        CampaignEngine engine = Fresh("medio", 51);
        Assert.Empty(engine.Platform.Positions);
        Assert.Empty(engine.Platform.Flags);
        Assert.Equal(10, engine.Context.Themes.Themes.Count);
        Assert.All(engine.Context.Themes.Themes, t =>
        {
            Assert.Equal((int)Math.Round(engine.PartyPosition(t.Id), MidpointRounding.AwayFromZero), engine.PositionOf(t.Id));
            Assert.InRange(engine.PositionOf(t.Id), ThemeDefinition.MinPosition, ThemeDefinition.MaxPosition);
            Assert.Equal(0, engine.KnownOf(t.Id));
            Assert.Equal(engine.PartyPosition(t.Id), engine.PerceivedPosition(t.Id), 12);
        });
    }

    [Fact]
    public void Posicao_desconhecida_vale_a_do_partido()
    {
        CampaignEngine engine = Fresh("medio", 52);
        var (theme, opposite) = FarTheme(engine);
        IReadOnlyDictionary<string, double> baseline = Votes(engine);

        // O jogador assume a posição oposta à do partido, mas ninguém sabe: o voto é o mesmo de antes.
        engine.Platform.Positions[theme] = opposite;
        engine.Platform.Known[theme] = 0;
        Assert.Equal(engine.PartyPosition(theme), engine.PerceivedPosition(theme), 12);
        IReadOnlyDictionary<string, double> unknown = Votes(engine);
        Assert.All(baseline, v => Assert.Equal(v.Value, unknown[v.Key], 6));

        // Conforme a posição fica conhecida, o eleitorado passa a enxergar a mistura, e o voto muda.
        engine.Platform.Known[theme] = 0.5;
        Assert.Equal((0.5 * opposite) + (0.5 * engine.PartyPosition(theme)), engine.PerceivedPosition(theme), 12);
        double half = Votes(engine)[RaceSetup.PlayerId];
        engine.Platform.Known[theme] = 1;
        Assert.Equal(opposite, engine.PerceivedPosition(theme), 12);
        double full = Votes(engine)[RaceSetup.PlayerId];
        Assert.NotEqual(baseline[RaceSetup.PlayerId], half, 3);
        Assert.NotEqual(half, full, 3);

        // O modelo de voto recebe exatamente essa mistura.
        VoteCandidate seen = engine.ToVote(engine.Context.State.Campaign!.Player);
        Assert.Equal(engine.PerceivedPosition(theme), seen.Themes[theme], 12);
        Assert.Single(seen.Themes);
        Assert.All(engine.Context.State.Campaign.Candidates.Where(c => !c.IsPlayer), c => Assert.Empty(engine.ToVote(c).Themes));
    }

    [Fact]
    public void Falar_do_tema_torna_a_posicao_conhecida_e_entra_no_Livro()
    {
        CampaignEngine engine = ChannelTests.InCampaign("medio", 53);
        CampaignKit.FundRua(engine);
        GameState state = engine.Context.State;
        string theme = "saude";
        string zone = engine.Territory.Zones[engine.Territory.HomeZone];
        Assert.Equal(ScheduleFailure.TemaInvalido, engine.CanSchedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha, Tema: theme)));
        Assert.Equal(ScheduleFailure.TemaInvalido, engine.CanSchedule(new CommitmentRequest("entrevista", null, engine.Today, Turno.Manha, Tema: "tema_que_nao_existe")));
        Assert.False(engine.SpeaksOfThemes("caminhada"));

        var talks = new[]
        {
            new CommitmentRequest("entrevista", null, engine.Today, Turno.Manha, Tema: theme),
            new CommitmentRequest("gravacao", null, engine.Today, Turno.Tarde, Tema: theme),
            new CommitmentRequest(CommitmentCatalog.Comicio, zone, engine.Today.AddDays(1), Turno.Manha, Tema: theme),
        };
        foreach (CommitmentRequest talk in talks)
        {
            Assert.True(engine.SpeaksOfThemes(talk.Type));
            double known = engine.KnownOf(theme);
            int stated = state.Ledger.Positions.Count;
            CommitmentForecast forecast = engine.Forecast(talk);
            Assert.Null(forecast.Blocked);
            Assert.True(forecast.ThemeKnownGain > 0);
            Assert.Null(engine.Schedule(talk).Failure);
            while (engine.Today < talk.Date)
            {
                StaffTests.Quiet(engine);
                engine.AdvanceDay();
            }

            CommitmentResult result = engine.RunToday().Single(r => r.Commitment.Type == talk.Type);
            Assert.Equal(forecast.ThemeKnownGain, result.ThemeKnown, 9);
            Assert.Equal(known + result.ThemeKnown, engine.KnownOf(theme), 9);
            Assert.Equal(stated + 1, state.Ledger.Positions.Count);
            StatedPosition entry = state.Ledger.Positions[^1];
            Assert.Equal(theme, entry.Theme);
            Assert.Equal(engine.PositionOf(theme), entry.Value);
            Assert.Equal(talk.Date, entry.Date);
            Assert.False(string.IsNullOrWhiteSpace(entry.Where));
        }

        Assert.InRange(engine.KnownOf(theme), 0.3, 1);
        Assert.True(engine.Platform.Positions.ContainsKey(theme));

        // Sem tema, a mesma entrevista não mexe na posição conhecida.
        double before = engine.KnownOf(theme);
        Assert.Equal(0, engine.Forecast(new CommitmentRequest("entrevista", null, engine.Today, Turno.Noite)).ThemeKnownGain);
        Assert.Equal(before, engine.KnownOf(theme));
    }

    [Fact]
    public void Mudar_e_desmudar_em_duas_semanas_custa_mais_que_mudar_uma_vez()
    {
        (double Incoherence, double Image, int Ledger) Play(bool revert)
        {
            CampaignEngine engine = Fresh("medio", 54);
            GameState state = engine.Context.State;
            var (theme, opposite) = FarTheme(engine);
            int original = engine.PositionOf(theme);
            engine.Platform.Positions[theme] = original;
            engine.Platform.Known[theme] = 0.5;
            state.Campaign!.Player.Image = 0.5;
            int ledger = state.Ledger.Positions.Count;

            var (failure, first) = engine.SetPosition(theme, opposite);
            Assert.Null(failure);
            double incoherence = first!.Change.Incoherence;
            Assert.Equal(Math.Abs(opposite - original) * 0.5, incoherence, 9);
            Assert.True(first.ImageDelta < 0);
            double image = first.ImageDelta;
            if (revert)
            {
                StaffTests.Week(engine);
                StaffTests.Week(engine);
                var (again, second) = engine.SetPosition(theme, original);
                Assert.Null(again);
                Assert.True(second!.Change.Incoherence > first.Change.Incoherence, "A segunda mudança em pouco tempo custa mais que a primeira.");
                incoherence += second.Change.Incoherence;
                image += second.ImageDelta;
                Assert.Equal(original, engine.PositionOf(theme));
            }

            return (incoherence, image, state.Ledger.Positions.Count - ledger);
        }

        var once = Play(revert: false);
        var twice = Play(revert: true);
        Assert.True(twice.Incoherence > once.Incoherence * 2, "Voltar atrás custa mais que o dobro de uma mudança.");
        Assert.True(twice.Image < once.Image);

        // Toda mudança de posição entra no Livro.
        Assert.Equal(1, once.Ledger);
        Assert.Equal(2, twice.Ledger);
    }

    [Fact]
    public void Mudanca_de_posicao_tem_previsao_por_grupo_e_por_zona_e_o_resultado_cai_na_faixa()
    {
        foreach (ulong seed in new ulong[] { 55, 56, 57 })
        {
            CampaignEngine engine = Fresh("medio", seed);
            GameState state = engine.Context.State;
            var (theme, opposite) = FarTheme(engine);
            engine.Platform.Positions[theme] = engine.PositionOf(theme);
            engine.Platform.Known[theme] = 0.4;
            foreach (Leader l in engine.City.Leaders)
            {
                l.Known = true;
            }

            var before = engine.PlayerSharesNow();
            double image = state.Campaign!.Player.Image;
            CommandForecast forecast = engine.ForecastPosition(theme, opposite);
            Assert.Null(forecast.Blocked);
            Assert.NotEmpty(forecast.Explanation);
            Assert.All(forecast.Explanation, line => Assert.DoesNotContain("{", line));
            Assert.True(forecast.Lines.Count(l => l.Kind == "votes_group") >= 10, "Uma linha por grupo de eleitores.");
            Assert.Equal(engine.Territory.Zones.Count, forecast.Lines.Count(l => l.Kind == "votes_zone"));
            ForecastLine city = forecast.Line("votes")!;
            Assert.True(city.Max > city.Min, "A faixa vai do efeito de hoje ao efeito quando todos conhecerem a posição.");


            var (failure, result) = engine.SetPosition(theme, opposite);
            Assert.Null(failure);
            Assert.True(forecast.Contains("incoherence", theme, result!.Change.Incoherence));
            Assert.True(forecast.Contains("image", null, state.Campaign.Player.Image - image));
            Assert.True(forecast.Contains("known", theme, engine.KnownOf(theme)));
            var after = engine.PlayerSharesNow();
            Assert.True(forecast.Contains("votes", null, (after.City - before.City) * 100), "Cidade fora da faixa.");
            foreach (var (group, share) in after.ByGroup)
            {
                Assert.True(forecast.Contains("votes_group", group, (share - before.ByGroup[group]) * 100), $"Grupo {group} fora da faixa.");
            }

            foreach (var (zone, share) in after.ByZone)
            {
                Assert.True(forecast.Contains("votes_zone", zone, (share - before.ByZone[zone]) * 100), $"Zona {zone} fora da faixa.");
                Assert.True(forecast.Contains("votes_zone_pct", zone, ((share / before.ByZone[zone]) - 1) * 100), $"Zona {zone} fora da faixa (variação).");
            }

            Assert.True(forecast.Contains("votes_pct", null, ((after.City / before.City) - 1) * 100));

            // Lideranças com interesse no tema lembram da mudança.
            List<Leader> interested = engine.City.Leaders.Where(l => l.Theme == theme).ToList();
            Assert.Equal(interested.Select(l => l.PersonId).Order(), result.LeadersWhoRemember.Order());
            Assert.All(interested, l => Assert.Contains(engine.City.Person(l.PersonId)!.Memories, m => m.Fact == "mudou_posicao" && m.Pontos < 0));
            Assert.Equal(CommandFailure.MesmaPosicao, engine.CanSetPosition(theme, opposite));
            Assert.Equal(CommandFailure.ValorInvalido, engine.CanSetPosition(theme, 3));
            Assert.Equal(CommandFailure.NaoEncontrado, engine.CanSetPosition("tema_que_nao_existe", 1));
        }
    }

    [Fact]
    public void Mudar_posicao_que_ninguem_conhece_nao_custa_imagem_nem_lembranca()
    {
        CampaignEngine engine = Fresh("medio", 58);
        var (theme, opposite) = FarTheme(engine);
        foreach (Leader l in engine.City.Leaders)
        {
            l.Known = true;
        }

        double image = engine.Context.State.Campaign!.Player.Image;
        CommandForecast forecast = engine.ForecastPosition(theme, opposite);
        Assert.Equal(0, forecast.Line("incoherence", theme)!.Expected);
        var (_, result) = engine.SetPosition(theme, opposite);
        Assert.Equal(0, result!.Change.Incoherence);
        Assert.Equal(image, engine.Context.State.Campaign.Player.Image);
        Assert.Empty(result.LeadersWhoRemember);
        Assert.True(engine.KnownOf(theme) > 0, "A mudança é notícia: a posição nova fica um pouco conhecida.");
        Assert.Contains(engine.Context.State.Ledger.Positions, p => p.Theme == theme && p.Value == opposite);
    }

    [Fact]
    public void Pedido_de_defender_o_tema_fica_cumprido_quando_o_jogador_assume_a_posicao_em_publico()
    {
        for (ulong seed = 1; seed < 60; seed++)
        {
            CampaignEngine engine = Fresh("medio", seed);
            LeaderCatalog catalog = engine.Context.Campaign.Leaders;
            Leader? leader = engine.City.Leaders.FirstOrDefault(l => catalog.Request(l.RequestId).Kind == LeaderRequestDefinition.Posicao);
            if (leader is null)
            {
                continue;
            }

            leader.Known = true;
            Assert.Null(engine.Schedule(new CommitmentRequest(CommitmentCatalog.Visita, null, engine.Today, Turno.Manha, PersonId: leader.PersonId)).Failure);
            engine.RunToday();
            engine.AnswerLeader(LeaderAnswer.Aceitar);
            Promise promise = engine.Context.State.Ledger.Promises.Single(p => p.Id == leader.PromiseId);
            Assert.Equal(PromiseState.Aberta, promise.State);

            // Falar de outro tema não cumpre; falar do tema dela, sim.
            string other = engine.Context.Themes.Themes.First(t => t.Id != leader.Theme).Id;
            Assert.Null(engine.Schedule(new CommitmentRequest("entrevista", null, engine.Today, Turno.Tarde, Tema: other)).Failure);
            engine.RunToday();
            Assert.Equal(PromiseState.Aberta, promise.State);
            Assert.Null(engine.Schedule(new CommitmentRequest("gravacao", null, engine.Today, Turno.Noite, Tema: leader.Theme)).Failure);
            engine.RunToday();
            Assert.Equal(PromiseState.Cumprida, promise.State);
            Assert.Contains(engine.City.Person(leader.PersonId)!.Memories, m => m.Fact == "promessa_cumprida" && m.Pontos > 0);
            return;
        }

        Assert.Fail("Nenhuma liderança com pedido de posição em 60 sementes.");
    }

    [Fact]
    public void Ate_tres_bandeiras_que_ficam_conhecidas_mais_depressa()
    {
        CampaignEngine engine = Fresh("medio", 59);
        PlatformRules rules = engine.Context.Campaign.Platform.Regras;
        Assert.Equal(3, rules.BandeirasMax);
        Assert.Equal(CommandFailure.BandeirasDemais, engine.SetFlags(["saude", "educacao", "seguranca", "impostos"]));
        Assert.Equal(CommandFailure.NaoEncontrado, engine.SetFlags(["tema_que_nao_existe"]));
        double plain = engine.ThemeGain("entrevista", "saude");
        Assert.Null(engine.SetFlags(["saude", "educacao", "seguranca"]));
        Assert.Equal(3, engine.Platform.Flags.Count);
        Assert.Equal(plain * rules.BandeiraBonus, engine.ThemeGain("entrevista", "saude"), 9);
        Assert.Equal(engine.ThemeGain("entrevista", "impostos"), plain, 9);

        // Enfatizar é assumir a posição, e o tema fica conhecido aos poucos mesmo sem fala.
        Assert.True(engine.Platform.Positions.ContainsKey("saude"));
        StaffTests.Week(engine);
        Assert.Equal(rules.BandeiraGanhoSemanal, engine.KnownOf("saude"), 9);
        Assert.Equal(0, engine.KnownOf("impostos"));
        Assert.True(engine.PlatformMatrix().Single(r => r.Theme.Id == "saude").Flag);
    }

    [Fact]
    public void Matriz_plataforma_por_publicos_cobre_todo_tema_grupo_e_zona()
    {
        CampaignEngine engine = Fresh("medio", 60);
        var (theme, opposite) = FarTheme(engine);
        IReadOnlyList<PlatformRow> matrix = engine.PlatformMatrix();
        int groups = engine.Context.ElectorateContent.Segments.Dimensions.Sum(d => d.Groups.Count);
        Assert.Equal(engine.Context.Themes.Themes.Select(t => t.Id), matrix.Select(r => r.Theme.Id));
        Assert.All(matrix, row =>
        {
            Assert.Equal(groups, row.Cells.Count(c => c.TargetKind == Promise.ParaGrupo));
            Assert.Equal(engine.Territory.Zones, row.Cells.Where(c => c.TargetKind == Promise.ParaZona).Select(c => c.TargetId));
            Assert.All(row.Cells, c =>
            {
                Assert.InRange(c.Agreement, -1.0001, 1.0001);
                Assert.InRange(c.Preference, ThemeDefinition.MinPosition, ThemeDefinition.MaxPosition);
                Assert.True(c.Priority >= 0);
            });
            Assert.Equal(engine.PositionOf(row.Theme.Id), row.Position);
            Assert.False(row.Explicit);
        });

        // Quem gosta e quem não gosta: num tema com lado, há público que concorda mais e público que concorda menos.
        PlatformRow before = matrix.Single(r => r.Theme.Id == theme);
        Assert.True(before.Cells.Max(c => c.Agreement) - before.Cells.Min(c => c.Agreement) > 0.05);
        engine.SetPosition(theme, opposite);
        PlatformRow after = engine.PlatformMatrix().Single(r => r.Theme.Id == theme);
        Assert.True(after.Explicit);
        Assert.Equal(opposite, after.Position);
        Assert.All(after.Cells, c => Assert.NotEqual(before.Cells.Single(x => x.TargetKind == c.TargetKind && x.TargetId == c.TargetId).Agreement, c.Agreement, 3));
    }

    [Fact]
    public void Toda_posicao_agrada_um_publico_e_desagrada_outro()
    {
        for (ulong seed = 70; seed < 90; seed++)
        {
            CampaignEngine engine = Fresh("medio", seed);

            // Procura na matriz um tema e uma posição que aproximem o jogador de um grupo e o afastem de outro.
            foreach (PlatformRow row in engine.PlatformMatrix())
            {
                List<PlatformCell> groups = row.Cells.Where(c => c.TargetKind == Promise.ParaGrupo && c.Priority > 0).ToList();
                for (int value = ThemeDefinition.MinPosition; value <= ThemeDefinition.MaxPosition; value++)
                {
                    double Closer(PlatformCell c) => Math.Abs(row.PartyPosition - c.Preference) - Math.Abs(value - c.Preference);
                    if (groups.Count == 0 || value == row.Position || groups.Max(Closer) < 0.3 || groups.Min(Closer) > -0.3)
                    {
                        continue;
                    }

                    // Ninguém conhece a posição ainda: mudar não custa imagem, e a faixa mostra o efeito quando ficar conhecida.
                    CommandForecast forecast = engine.ForecastPosition(row.Theme.Id, value);
                    Assert.Equal(0, forecast.Line("image")!.Expected);
                    Assert.Contains(forecast.Lines, l => l.Kind == "votes_group_pct" && l.Max > 0.01);
                    Assert.Contains(forecast.Lines, l => l.Kind == "votes_group_pct" && l.Min < -0.01);
                    Assert.Contains(forecast.Explanation, line => line.StartsWith("Ganha", StringComparison.Ordinal));
                    Assert.Contains(forecast.Explanation, line => line.StartsWith("Perde", StringComparison.Ordinal));
                    return;
                }
            }
        }

        Assert.Fail("Nenhuma cidade em que os grupos divergem num tema.");
    }

    [Fact]
    public void Promessa_local_da_apoio_imediato_tem_previsao_e_entra_no_Livro()
    {
        foreach (ulong seed in new ulong[] { 61, 62, 63 })
        {
            CampaignEngine engine = Fresh("medio", seed);
            GameState state = engine.Context.State;
            string zone = engine.Context.Zonas.Zonas.First(z => !z.IsRural).Id;
            int z = engine.Territory.IndexOf(zone);
            IReadOnlyList<PromiseOption> options = engine.PromiseOptions(Promise.ParaZona, zone);
            Assert.NotEmpty(options);
            Assert.All(options, o => Assert.DoesNotContain("{", o.Text));
            Assert.DoesNotContain(options, o => o.Definition.Situacao == "rural");

            PromiseOption option = options[(int)(seed % (ulong)options.Count)];
            CommandForecast forecast = engine.ForecastPromise(option.Definition.Id, Promise.ParaZona, zone);
            Assert.Null(forecast.Blocked);
            Assert.NotEmpty(forecast.Explanation);
            Assert.All(forecast.Explanation, line => Assert.DoesNotContain("{", line));
            Assert.Contains(forecast.Lines, l => l.Kind == "support" && l.Max > l.Min && l.Min > 0);

            var before = new Dictionary<string, double>(engine.Territory.MeetingSupport[z]);
            double votes = Votes(engine)[RaceSetup.PlayerId];
            var (failure, result) = engine.MakePromise(option.Definition.Id, Promise.ParaZona, zone);
            Assert.Null(failure);
            Assert.NotEmpty(result!.Support);
            foreach (var (group, gain) in result.Support)
            {
                Assert.True(forecast.Contains("support", group, gain), $"Apoio de {group} fora da faixa.");
                Assert.Equal(before.GetValueOrDefault(group) + gain, engine.Territory.MeetingSupport[z][group], 9);
            }

            Assert.True(Votes(engine)[RaceSetup.PlayerId] > votes, "O apoio da promessa vira voto na hora.");

            // Entra no Livro, com alvo, objeto e prazo depois da posse.
            Promise promise = Assert.Single(state.Ledger.Promises);
            Assert.Same(promise, result.Promise);
            Assert.Equal(Promise.ParaZona, promise.ToKind);
            Assert.Equal(zone, promise.ToId);
            Assert.Equal(option.Definition.Id, promise.Object);
            Assert.Equal(PromiseState.Aberta, promise.State);
            Assert.True(promise.Deadline > new DateOnly(state.Campaign!.ElectionDate.Year + 1, 1, 1));
            Assert.Contains(engine.ZoneName(zone), promise.Text);
            Assert.Equal(CommandFailure.JaPrometido, engine.CanPromise(option.Definition.Id, Promise.ParaZona, zone));
            Assert.Equal(CommandFailure.AlvoInvalido, engine.CanPromise("estrada", Promise.ParaZona, zone));
            Assert.Equal(CommandFailure.AlvoInvalido, engine.CanPromise(option.Definition.Id, Promise.ParaGrupo, "baixa"));
        }
    }

    [Fact]
    public void Promessa_a_um_grupo_e_promessas_demais_derrubam_a_credibilidade()
    {
        CampaignEngine engine = Fresh("grande", 64);
        GameState state = engine.Context.State;
        PromiseRules rules = engine.Context.Campaign.Promises.Regras;
        Assert.Equal(1, engine.Credibility);

        // A um grupo de eleitores: o apoio vale na cidade inteira.
        PromiseOption toGroup = engine.PromiseOptions(Promise.ParaGrupo, "baixa").First(o => o.Blocked is null);
        double support = state.Campaign!.Player.Support.GetValueOrDefault("baixa");
        CommandForecast groupForecast = engine.ForecastPromise(toGroup.Definition.Id, Promise.ParaGrupo, "baixa");
        var (_, first) = engine.MakePromise(toGroup.Definition.Id, Promise.ParaGrupo, "baixa");
        Assert.True(groupForecast.Contains("support", "baixa", first!.Support["baixa"]));
        Assert.Equal(support + first.Support["baixa"], state.Campaign.Player.Support["baixa"], 9);
        Assert.Equal(Promise.ParaGrupo, first.Promise.ToKind);

        // Promessas além do que a credibilidade aguenta: ela cai, e a mesma promessa passa a render menos.
        var made = new List<PromiseResult> { first };
        foreach (string zone in engine.Territory.Zones)
        {
            foreach (PromiseOption option in engine.PromiseOptions(Promise.ParaZona, zone).Where(o => o.Blocked is null).Take(2))
            {
                double credibility = engine.Credibility;
                CommandForecast forecast = engine.ForecastPromise(option.Definition.Id, Promise.ParaZona, zone);
                Assert.Equal(credibility, forecast.Factors.Single(f => f.Id == "credibilidade").Multiplier);
                var (failure, result) = engine.MakePromise(option.Definition.Id, Promise.ParaZona, zone);
                Assert.Null(failure);
                Assert.All(result!.Support, s => Assert.True(forecast.Contains("support", s.Key, s.Value)));
                Assert.True(forecast.Contains("credibility", null, result.Credibility));
                made.Add(result);
            }
        }

        Assert.True(made.Count > rules.PromessasSemCusto + 2);
        Assert.True(engine.Credibility < 1);
        Assert.True(engine.Credibility >= rules.CredibilidadeMinima);
        Assert.Equal(made.Count, state.Ledger.Promises.Count);
        Assert.All(made, r => Assert.Contains(r.Promise, state.Ledger.Promises));

        // O apoio previsto da próxima promessa é o de catálogo vezes a credibilidade.
        string free = engine.Territory.Zones[0];
        PromiseOption next = engine.PromiseOptions(Promise.ParaZona, free).First(o => o.Blocked is null);
        ForecastLine line = engine.ForecastPromise(next.Definition.Id, Promise.ParaZona, free).Lines.First(l => l.Kind == "support");
        Assert.Equal(next.Definition.Apoio * engine.Credibility, line.Expected, 9);

        // Promessa vencida também pesa.
        double open = engine.Credibility;
        made[0].Promise.State = PromiseState.Vencida;
        Assert.True(engine.Credibility <= open);
    }

    [Fact]
    public void Catalogo_de_promessas_e_ligado_a_obra_ou_servico_e_nao_mira_grupo_religioso()
    {
        PromiseCatalog catalog = CampaignKit.C.Campaign.Promises;
        var groups = CampaignKit.C.Electorate.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet();
        var religion = CampaignKit.C.Electorate.Segments.Dimensions.Single(d => d.Id == "religiao").Groups.Select(g => g.Id).ToHashSet();
        Assert.True(catalog.Promessas.Count >= 8);
        Assert.Contains(catalog.Promessas, p => p.Tipo == PromiseDefinition.TipoObra);
        Assert.Contains(catalog.Promessas, p => p.Tipo == PromiseDefinition.TipoServico);
        Assert.All(catalog.Promessas, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Obra));
            Assert.True(p.Nome.Length <= 28);
            Assert.All(p.Grupos, g => Assert.Contains(g, groups));
            Assert.DoesNotContain(p.Grupos, g => religion.Contains(g));
        });
    }

    [Fact]
    public void Plataforma_vai_e_volta_pelo_save()
    {
        CampaignEngine engine = Fresh("medio", 65);
        var (theme, opposite) = FarTheme(engine);
        engine.SetFlags([theme, "saude"]);
        engine.SetPosition(theme, opposite);
        string zone = engine.Territory.Zones[0];
        engine.MakePromise(engine.PromiseOptions(Promise.ParaZona, zone).First(o => o.Blocked is null).Definition.Id, Promise.ParaZona, zone);
        StaffTests.Week(engine);
        string json = GameStateSerializer.Serialize(engine.Context.State);
        GameState loaded = GameStateSerializer.Deserialize(json);
        Assert.Equal(json, GameStateSerializer.Serialize(loaded));
        CampaignEngine resumed = CampaignEngine.Start(CampaignKit.Context(loaded));
        Assert.Equal(opposite, resumed.PositionOf(theme));
        Assert.Equal(engine.KnownOf(theme), resumed.KnownOf(theme));
        Assert.Equal(engine.Credibility, resumed.Credibility);
        Assert.Equal(Votes(engine)[RaceSetup.PlayerId], Votes(resumed)[RaceSetup.PlayerId], 6);
    }
}
