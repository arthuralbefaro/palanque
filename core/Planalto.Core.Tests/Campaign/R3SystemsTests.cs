using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Events;
using Planalto.Core.Media;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;
using Xunit;

namespace Planalto.Core.Tests.Campaign;

/// <summary>
/// R3: mídia local (GDD 17.13), doadores e contas (17.12), comandos novos com previsão (princípio f), lint de rastro
/// (princípio d) e a campanha inteira com os sistemas novos indo e voltando pelo save.
/// </summary>
public class R3SystemsTests
{
    private static int City(string porte) => R3WorldTests.City(porte);

    private static CampaignEngine Rich(string porte, ulong seed, int idleWeeks = 0)
    {
        CampaignEngine engine = CampaignKit.NewCareer(City(porte), seed);
        for (int i = 0; i < idleWeeks; i++)
        {
            R3WorldTests.IdleWeek(engine);
        }

        engine.Context.State.Player.Resources.Money = 500_000;
        return engine;
    }

    /// <summary>Leva o calendário até a semana da data (sem comandos do jogador).</summary>
    private static void IdleUntil(CampaignEngine engine, DateOnly date)
    {
        while (engine.Context.State.Calendar.WeekEnd < date && engine.Context.State.Campaign!.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha)
        {
            R3WorldTests.IdleWeek(engine);
        }
    }

    // ------------------------------------------------------------------ mídia

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Veiculos_por_porte_com_nome_que_diz_o_que_e_e_fora_da_lista_de_bloqueio()
    {
        MediaContent media = CampaignKit.C.Campaign.Media;
        Assert.Empty(MediaContent.CrossCheck(media.Outlets, media.Blocklist));
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            var (min, max) = OutletCatalog.PorteCount[porte];
            for (ulong seed = 1; seed <= 6; seed++)
            {
                CampaignEngine engine = CampaignKit.NewCareer(City(porte), 900 + seed);
                List<Outlet> outlets = engine.City.Outlets;
                Assert.InRange(outlets.Count, min, max);
                Assert.Equal(outlets.Count, outlets.Select(o => o.Name).Distinct().Count());
                foreach (Outlet o in outlets)
                {
                    Assert.True(o.Name.Length <= OutletCatalog.MaxName, o.Name);
                    Assert.False(media.Blocklist.Blocks(o.Name), $"'{o.Name}' coincide com um veículo real.");
                    Assert.Contains(o.Line, EditorialLine.All);
                    Assert.Equal(2, o.Themes.Distinct().Count());
                    Assert.InRange(o.Sensationalism, 0, 1);
                    Assert.Equal(engine.Territory.Zones.Count, o.Reach.Count);
                    Assert.All(o.Reach, r => Assert.InRange(r, 0, 1));

                    // "Rádio Vale (rádio local, linha governista)": o nome sempre com o que é.
                    string label = engine.OutletLabel(o);
                    Assert.StartsWith(o.Name + " (", label);
                    Assert.Contains(media.Outlets.Type(o.Type).Nome, label);
                    Assert.Contains(media.Outlets.Line(o.Line).Nome, label);
                }

                Assert.Contains(outlets, o => media.Outlets.Type(o.Type).Rede);
                Assert.Equal(outlets.Count, engine.Outlets().Count);
            }
        }
    }

    [Theory]
    [InlineData("Rádio Globo", true)]
    [InlineData("radio  GLOBO", true)]
    [InlineData("TV Tupi", true)]
    [InlineData("Gazeta do Povo", true)]
    [InlineData("Jornal Estadão Hoje", true)]
    [InlineData("Gazeta Vale", false)]
    [InlineData("Rádio Globosfera", false)]
    [InlineData("Portal Mirante", false)]
    public void Lista_de_bloqueio_barra_nome_igual_ou_que_contem_um_veiculo_real(string name, bool blocked) =>
        Assert.Equal(blocked, CampaignKit.C.Campaign.Media.Blocklist.Blocks(name));

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Nenhuma_noticia_sem_fato_o_tom_segue_a_linha_e_boato_vem_marcado()
    {
        int total = 0;
        int coherent = 0;
        int rumors = 0;
        foreach (var (porte, seed) in new[] { ("medio", 101UL), ("grande", 102UL), ("metropole", 103UL) })
        {
            CampaignEngine engine = CampaignKit.NewCareer(City(porte), seed);
            CampaignState campaign = engine.Context.State.Campaign!;
            OutletCatalog catalog = engine.Context.Campaign.Media.Outlets;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int week = 0; week < 14; week++)
            {
                // Um boato por semana, para conferir a marca: fato de rede sem autor conhecido.
                Fact rumor = engine.Emit(FactKind.Boato, "mensagem sem autor circula nas redes", campaign.Rivals.Records[0].CandidateId, target: RaceSetup.PlayerId, rumor: true, falso: week % 2 == 0);
                R3WorldTests.IdleWeek(engine);
                Assert.Contains(campaign.Media.News, n => n.FactId == rumor.Id);
                foreach (NewsItem n in campaign.Media.News.Where(n => seen.Add(n.Id)))
                {
                    Fact? fact = campaign.Facts.Find(n.FactId);
                    Assert.NotNull(fact);
                    Assert.True(fact!.Visibility >= 1, "Fato de visibilidade 0 virou notícia.");
                    Outlet outlet = engine.City.Outlet(n.OutletId)!;
                    Assert.True(n.Headline.Length is > 0 and <= TemplateCatalog.MaxHeadline, n.Headline);
                    Assert.Contains(n.Tone, Tone.All);
                    total++;
                    coherent += catalog.Line(outlet.Line).Coerente(n.Lado, n.Tone) ? 1 : 0;

                    // A linha decide o tom, nunca o fato: a notícia fala do mesmo ator, zona e tema do fato.
                    Assert.Equal(fact.ActorId, n.ActorId);
                    Assert.Equal(fact.ZoneId, n.ZoneId);
                    Assert.Equal(fact.Theme, n.Theme);
                    Assert.Equal(fact.Rumor, n.Boato);
                    if (fact.Rumor)
                    {
                        rumors++;
                        Assert.StartsWith(engine.Context.Campaign.Media.Templates.BoatoPrefixo, n.Headline);
                        Assert.True(catalog.Type(outlet.Type).Rede, "Boato fora de página de rede.");
                    }
                }
            }
        }

        Assert.True(total > 300, $"Só {total} notícias.");
        Assert.True(rumors >= 30);
        Assert.True(coherent >= 0.95 * total, $"Tom coerente com a linha em {coherent} de {total}.");
    }

    [Fact]
    public void Uma_semana_de_noticias_sobre_um_tema_o_poe_em_alta_e_rende_30_por_cento_a_mais()
    {
        CampaignEngine engine = Rich("medio", 111);
        CampaignState campaign = engine.Context.State.Campaign!;
        MediaRules rules = engine.Context.Campaign.Media.Outlets.Regras;
        Assert.Equal(0.3, rules.TemaEmAltaBonus);
        string theme = "saude";
        string zone = campaign.Territory.Zones[0];
        Assert.Empty(engine.HotThemes());

        // Fatos de visibilidade máxima sobre o tema, todos os dias da semana.
        for (int i = 0; i < 6; i++)
        {
            engine.Emit(FactKind.Cidade, $"Fila no posto de saúde cresce ({i})", zone: zone, theme: theme, visibility: 3);
            R3WorldTests.ResolveEvents(engine);
            engine.AdvanceDay();
        }

        R3WorldTests.ResolveEvents(engine);
        engine.AdvanceDay();
        HotThemeView hot = Assert.Single(engine.HotThemes(), h => h.Theme == theme);
        Assert.Contains(zone, hot.Zones);
        Assert.True(hot.News >= rules.TemaEmAltaNoticias);
        Assert.NotNull(engine.HotThemeOn(theme, zone, engine.Today));
        Assert.Contains(campaign.Facts.Items, f => f.Kind == FactKind.TemaEmAlta && f.Theme == theme);

        // O compromisso que trata do tema em alta rende 30% a mais; o de outro tema, não.
        var hotRequest = new CommitmentRequest("gravacao", null, engine.Today, Turno.Manha, Tema: theme);
        CommitmentForecast withHot = engine.Forecast(hotRequest);
        CommitmentForecast other = engine.Forecast(hotRequest with { Tema = "educacao" });
        YieldFactor factor = Assert.Single(withHot.Factors, f => f.Id == "tema_em_alta");
        Assert.Equal(1.3, factor.Multiplier, 6);
        Assert.DoesNotContain(other.Factors, f => f.Id == "tema_em_alta");
        Assert.Equal(1.3, withHot.Yield / other.Yield, 6);

        // O bônus fica travado quando o compromisso é marcado, e o resultado traz o fator.
        R3WorldTests.ResolveEvents(engine);
        Assert.Null(engine.Schedule(hotRequest).Failure);
        CommitmentResult result = Assert.Single(engine.RunToday());
        Assert.Contains(result.Factors, f => f.Id == "tema_em_alta");
        Assert.InRange(result.Support, withHot.SupportMin - 1e-9, withHot.SupportMax + 1e-9);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Entrevista_em_veiculo_critico_e_mais_dificil_e_convence_mais()
    {
        MediaRules rules = CampaignKit.C.Campaign.Media.Outlets.Regras;
        bool sawCritic = false;
        bool sawFriend = false;
        for (ulong seed = 120; seed < 140 && !(sawCritic && sawFriend); seed++)
        {
            CampaignEngine engine = Rich("metropole", seed);
            CampaignState campaign = engine.Context.State.Campaign!;
            var plain = new CommitmentRequest(CommitmentCatalog.Entrevista, null, engine.Today, Turno.Manha);
            double baseChance = engine.Forecast(plain).Outcomes[0].Probability;
            foreach (Outlet outlet in engine.City.Outlets)
            {
                CommitmentRequest request = plain with { OutletId = outlet.Id };
                CommitmentForecast forecast = engine.Forecast(request);
                Assert.Null(forecast.Blocked);
                Assert.Contains(forecast.Factors, f => f.Id == "veiculo_alcance");
                string stance = engine.StanceToward(outlet, RaceSetup.PlayerId);
                double shift = forecast.Outcomes[0].Probability - baseChance;
                double relation = engine.RelationOf(outlet.JournalistId) * rules.EntrevistaChancePorRelacao;
                if (stance == "critico")
                {
                    sawCritic = true;
                    Assert.Contains(forecast.Factors, f => f.Id == "veiculo_critico" && f.Multiplier > 1);
                    Assert.Equal(rules.EntrevistaCriticoChance + relation, shift, 6);
                }
                else if (stance == "amigo")
                {
                    sawFriend = true;
                    Assert.Contains(forecast.Factors, f => f.Id == "veiculo_amigo" && f.Multiplier < 1);
                    Assert.Equal(rules.EntrevistaAmigoChance + relation, shift, 6);
                }
            }

            // A entrevista acontece: o veículo publica e o jornalista lembra; o resultado cai na faixa da previsão.
            Outlet chosen = engine.City.Outlets[0];
            CommitmentRequest marked = plain with { OutletId = chosen.Id };
            CommitmentForecast before = engine.Forecast(marked);
            Assert.NotEmpty(before.Factors);
            Assert.Null(engine.Schedule(marked).Failure);
            CommitmentResult result = Assert.Single(engine.RunToday());
            Assert.InRange(result.Image, before.ImageMin - 1e-9, before.ImageMax + 1e-9);
            Assert.Contains(campaign.Media.News, n => n.OutletId == chosen.Id && n.CitaJogador);
            Assert.Contains(engine.City.Person(chosen.JournalistId)!.Memories, m => m.Fact == "entrevista");
            Assert.Equal(ScheduleFailure.VeiculoInvalido, engine.CanSchedule(plain with { OutletId = "nao_existe", Turno = Turno.Tarde }));
            Assert.Equal(ScheduleFailure.VeiculoInvalido, engine.CanSchedule(new CommitmentRequest("gravacao", null, engine.Today, Turno.Tarde, OutletId: chosen.Id)));
        }

        Assert.True(sawCritic && sawFriend, "Faltou veículo crítico ou amigo nas sementes testadas.");
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Sugerir_pauta_e_anunciar_tem_previsao_com_faixa_e_o_resultado_cai_nela()
    {
        int pitches = 0;
        int published = 0;
        int ads = 0;
        for (ulong seed = 150; seed < 162; seed++)
        {
            CampaignEngine engine = Rich("grande", seed, idleWeeks: 2);
            CampaignState campaign = engine.Context.State.Campaign!;
            foreach (Outlet outlet in engine.City.Outlets)
            {
                Fact? fact = engine.StoryCandidates().FirstOrDefault(f => engine.CanSuggestStory(outlet.Id, f.Id) is null);
                if (fact is null)
                {
                    continue;
                }

                CommandForecast forecast = engine.ForecastStory(outlet.Id, fact.Id);
                Assert.Null(forecast.Blocked);
                Assert.NotEmpty(forecast.Explanation);
                Assert.All(forecast.Explanation, line => Assert.False(string.IsNullOrWhiteSpace(line)));
                Assert.Equal(2, forecast.Outcomes.Count);
                Assert.Equal(1, forecast.Outcomes.Sum(o => o.Probability), 6);
                int newsBefore = campaign.Media.News.Count;
                StoryResult result = engine.SuggestStory(outlet.Id, fact.Id).Result!;
                pitches++;
                published += result.Published ? 1 : 0;
                bool own = fact.ActorId == RaceSetup.PlayerId;
                Assert.True(forecast.Contains(own ? "image" : "rival_image", own ? null : fact.ActorId, result.ImageDelta));
                Assert.True(forecast.Contains("relation", outlet.JournalistId, result.Memory.Pontos));
                Assert.Equal(result.Published ? newsBefore + 1 : newsBefore, campaign.Media.News.Count);
                if (result.Published)
                {
                    // A pauta só garante que o veículo publique: o fato é o mesmo, e o tom é o da linha.
                    Assert.Equal(fact.Id, result.News!.FactId);
                    Assert.Equal(outlet.Id, result.News.OutletId);
                }

                // Uma pauta por jornalista por semana.
                Assert.Equal(CommandFailure.JaFeito, engine.CanSuggestStory(outlet.Id, fact.Id));
            }

            // Anúncio: só imprensa escrita, só na campanha oficial, até 10 por veículo, em datas diversas (art. 43).
            PropagandaLawConfig law = engine.Context.PropagandaLaw;
            Outlet? paper = engine.City.Outlets.FirstOrDefault(o => engine.Context.Campaign.Media.Outlets.Type(o.Type).Escrita);
            Outlet radio = engine.City.Outlets.First(o => engine.Context.Campaign.Media.Outlets.Type(o.Type).Emissora);
            Assert.Equal(CommandFailure.GastoVedado, engine.CanAdvertise(radio.Id));
            if (paper is null || seed > 152)
            {
                continue;
            }

            Assert.Equal(CommandFailure.CanalFechado, engine.CanAdvertise(paper.Id));
            IdleUntil(engine, law.PrimeiroDiaDePropaganda(campaign.ElectionDate.Year).AddDays(7));
            engine.Context.State.Player.Resources.Money = 500_000;
            for (int i = 0; i < law.AnunciosPorVeiculo; i++)
            {
                CommandForecast forecast = engine.ForecastAd(paper.Id);
                Assert.Null(forecast.Blocked);
                Assert.NotEmpty(forecast.Explanation);
                decimal money = engine.Context.State.Player.Resources.Money;
                AdResult result = engine.Advertise(paper.Id).Result!;
                ads++;
                Assert.All(result.Presence, p => Assert.True(forecast.Contains("presence", p.Key, p.Value)));
                Assert.True(forecast.Contains("money", null, (double)result.Money));
                Assert.Equal(money + result.Money, engine.Context.State.Player.Resources.Money);
                Assert.Equal(law.AnunciosPorVeiculo - i - 1, result.AdsLeft);

                // No mesmo dia, no mesmo veículo, não: datas diversas.
                Assert.Equal(CommandFailure.LimiteLegal, engine.CanAdvertise(paper.Id));
                R3WorldTests.ResolveEvents(engine);
                engine.AdvanceDay();
                engine.Context.State.Player.Resources.Money = 500_000;
            }

            Assert.Equal(CommandFailure.LimiteLegal, engine.CanAdvertise(paper.Id));
            Assert.Equal(CommandFailure.LimiteLegal, engine.Advertise(paper.Id).Failure);
        }

        Assert.True(pitches >= 30, $"Só {pitches} pautas.");
        Assert.InRange(published, 1, pitches - 1);
        Assert.True(ads >= 10);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Horario_gratuito_so_onde_ha_emissora_no_periodo_da_lei_e_pelo_tempo_do_partido()
    {
        CampaignEngine engine = CampaignKit.NewCareer(City("grande"), 171, strongestParty: true);
        CampaignState campaign = engine.Context.State.Campaign!;
        PropagandaLawConfig law = engine.Context.PropagandaLaw;
        FreeAirtimeView view = engine.FreeAirtime();
        Assert.True(view.HasBroadcaster);
        Assert.True(view.SeatsKnown);
        Assert.False(view.OnAir);
        Assert.Equal(law.HorarioGratuitoComeca(campaign.ElectionDate), view.Starts);
        Assert.Equal(35, view.Ends.DayNumber - view.Starts.DayNumber + 1);
        Assert.InRange(view.PartyShare, 0.0001, 1);
        Assert.Equal(view.PartyShare * 28, view.MinutesDaily, 6);
        Assert.True(view.WeeklyPresence > 0);

        // Os tempos somam 100% e seguem a regra 90% pela bancada, 10% igual (quem não tem deputado só tem a parte igual).
        var parties = campaign.ForOffice(Office.Vereador).Select(c => c.PartyId).Distinct().ToList();
        var seats = engine.Context.CamaraSeats!;
        var shares = PartyFunding.TvTimeShares(parties, seats);
        Assert.Equal(1, shares.Values.Sum(), 9);
        Assert.Equal(shares[campaign.Player.PartyId], view.PartyShare, 9);
        double total = parties.Sum(p => (double)seats.GetValueOrDefault(p));
        foreach (string party in parties)
        {
            Assert.Equal((0.10 / parties.Count) + (total > 0 ? 0.90 * seats.GetValueOrDefault(party) / total : 0.90 / parties.Count), shares[party], 9);
        }

        // Fora do período não rende nada; dentro, toda semana.
        IdleUntil(engine, view.Starts.AddDays(-8));
        Assert.DoesNotContain(campaign.Facts.Items, f => f.Kind == FactKind.HorarioGratuito);
        IdleUntil(engine, view.Starts.AddDays(8));
        Assert.Contains(campaign.Facts.Items, f => f.Kind == FactKind.HorarioGratuito);
        Assert.True(engine.FreeAirtime().OnAir || engine.Today > view.Ends);

        // Sem emissora na cidade, não há horário gratuito.
        CampaignEngine none = CampaignKit.NewCareer(City("pequeno"), 172);
        none.City.Outlets.RemoveAll(o => none.Context.Campaign.Media.Outlets.Type(o.Type).Emissora);
        FreeAirtimeView empty = none.FreeAirtime();
        Assert.False(empty.HasBroadcaster);
        Assert.Equal(0, empty.WeeklyPresence);
    }

    // ------------------------------------------------------------------ ataques, respostas e pactos

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Atacar_tem_previsao_com_faixa_explicacao_e_o_resultado_cai_nela()
    {
        AttackCatalog attacks = CampaignKit.C.Campaign.Attacks;
        int done = 0;
        int sued = 0;
        for (ulong seed = 200; seed < 215; seed++)
        {
            CampaignEngine engine = Rich("medio", seed, idleWeeks: 1);
            CampaignState campaign = engine.Context.State.Campaign!;
            List<Candidate> rivals = campaign.ForOffice(campaign.Player.Office).Where(c => !c.IsPlayer && !c.IsBulk).ToList();
            int i = 0;
            foreach (Candidate rival in rivals)
            {
                AttackType type = attacks.Tipos[i % attacks.Tipos.Count];
                AttackChannel channel = attacks.Canais.Where(c => !c.SoCampanha).ToList()[i++ % 2];
                RivalRecord record = campaign.Rivals.Record(rival.Id)!;
                RivalWeakness? weakness = record.Weaknesses.FirstOrDefault(w => w.Type == type.Id);
                if (weakness is not null && i % 2 == 0)
                {
                    weakness.Known = true;
                }

                var request = new AttackRequest(rival.Id, type.Id, channel.Id, weakness is { Known: true } ? weakness.Id : null, "saude", channel.ExigeVeiculo ? engine.City.Outlets[0].Id : null);
                CommandForecast forecast = engine.ForecastAttack(request);
                Assert.Null(forecast.Blocked);
                Assert.True(forecast.Explanation.Count >= 4);
                Assert.All(forecast.Explanation, line => Assert.False(string.IsNullOrWhiteSpace(line)));
                Assert.NotNull(forecast.Line("rival_image", rival.Id));
                Assert.NotEmpty(forecast.Outcomes);
                decimal money = engine.Context.State.Player.Resources.Money;
                double image = campaign.Player.Image;
                AttackResult result = engine.AttackRival(request).Result!;
                done++;
                sued += result.Sued ? 1 : 0;
                Assert.True(forecast.Contains("rival_image", rival.Id, result.RivalImage), $"{type.Id}: {result.RivalImage}");
                Assert.True(forecast.Contains("image", null, result.Image));
                Assert.True(forecast.Contains("money", null, (double)result.Money));
                Assert.Equal(money + result.Money, engine.Context.State.Player.Resources.Money);
                Assert.Equal(image + result.Image, campaign.Player.Image, 9);
                Assert.Equal(-result.RivalImage, engine.DamageNow(result.Attack), 9);
                Assert.True(engine.AttackPenalty(rival.Id) >= -result.RivalImage - 1e-9);
                Assert.NotNull(campaign.Facts.Find(result.Attack.FactId));

                // Prova rende mais que acusação sem prova; atacar de novo o mesmo rival tem intervalo.
                Assert.Equal(CommandFailure.JaFeito, engine.CanAttack(request));
            }

            // Sem prova não se cita ponto fraco que não foi descoberto.
            RivalRecord any = campaign.Rivals.Records.First(r => r.Weaknesses.Any(w => !w.Known));
            RivalWeakness hidden = any.Weaknesses.First(w => !w.Known);
            Assert.Equal(CommandFailure.SemProva, engine.CanAttack(new AttackRequest(any.CandidateId, hidden.Type, AttackChannel.Redes, hidden.Id)));
            Assert.Equal(CommandFailure.CanalFechado, engine.CanAttack(new AttackRequest(any.CandidateId, AttackType.Tema, AttackChannel.Palanque, Theme: "saude")));
            Assert.Equal(CommandFailure.AlvoInvalido, engine.CanAttack(new AttackRequest(any.CandidateId, AttackType.Tema, AttackChannel.Redes)));
        }

        Assert.True(done >= 100, $"Só {done} ataques.");
        Assert.True(sued > 0, "Nenhum processo em ataques sem prova.");
    }

    [Fact]
    public void Ataque_com_prova_pega_mais_e_se_apaga_em_26_semanas_sem_resposta()
    {
        CampaignEngine engine = Rich("medio", 221, idleWeeks: 1);
        CampaignState campaign = engine.Context.State.Campaign!;
        AttackRules rules = engine.Context.Campaign.Attacks.Regras;
        RivalRecord record = campaign.Rivals.Records.First(r => campaign.Find(r.CandidateId)!.Office == campaign.Player.Office && r.Weaknesses.Count > 0);
        RivalWeakness weakness = record.Weaknesses[0];
        var blind = new AttackRequest(record.CandidateId, weakness.Type, AttackChannel.Redes);
        double without = -engine.ForecastAttack(blind).Line("rival_image", record.CandidateId)!.Min;
        weakness.Known = true;
        double with = -engine.ForecastAttack(blind with { WeaknessId = weakness.Id }).Line("rival_image", record.CandidateId)!.Min;
        Assert.True(with > without);
        Assert.DoesNotContain(engine.ForecastAttack(blind with { WeaknessId = weakness.Id }).Outcomes, o => o.Text == engine.Context.Campaign.Attacks.Texto("processo"));

        AttackResult result = engine.AttackRival(blind with { WeaknessId = weakness.Id }).Result!;
        Assert.False(result.Sued);
        Assert.Equal(26, rules.SemanasSemResposta);
        Assert.Equal(engine.Today.AddDays(7 * 26), result.Attack.Ends);
        double start = engine.DamageNow(result.Attack);
        Assert.True(start > 0);

        // O voto projetado do rival cai com o ataque de pé.
        Candidate rival = campaign.Find(record.CandidateId)!;
        Assert.Equal(Math.Clamp(rival.Image - start, -1, 1), engine.ToVote(rival).Image, 9);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Responder_a_ataque_tem_prazo_de_48_horas_e_bem_respondido_some_em_4_semanas()
    {
        AttackCatalog catalog = CampaignKit.C.Campaign.Attacks;
        AttackRules rules = catalog.Regras;
        Assert.Equal(48, rules.PrazoRespostaHoras);
        Assert.Equal(4, rules.SemanasRespondido);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        int expired = 0;
        for (ulong seed = 230; seed < 250; seed++)
        {
            CampaignEngine engine = Rich("medio", seed, idleWeeks: 1);
            CampaignState campaign = engine.Context.State.Campaign!;

            // O jogador ataca os combativos; em 3 a 7 dias eles revidam.
            foreach (Candidate rival in campaign.ForOffice(campaign.Player.Office).Where(c => !c.IsPlayer && !c.IsBulk && engine.City.Person(c.Id)!.Temperament == Temperament.Combativo))
            {
                DateOnly when = engine.Today;
                AttackResult made = engine.AttackRival(new AttackRequest(rival.Id, AttackType.Tema, AttackChannel.Redes, Theme: "saude")).Result!;
                RivalReaction? reaction = campaign.Rivals.Reactions.FirstOrDefault(r => r.AttackId == made.Attack.Id);
                if (reaction is not null)
                {
                    Assert.Equal(RivalReaction.Revide, reaction.Kind);
                    Assert.InRange(reaction.Due.DayNumber - when.DayNumber, 3, 7);
                }
            }

            StaffMember? lawyer = engine.City.Staff.FirstOrDefault(m => m.Role == StaffRole.Advogado);
            if (lawyer is not null && seed % 2 == 0)
            {
                lawyer.Hired = true;
                lawyer.HiredOn = engine.Today;
            }

            for (int day = 0; day < 12; day++)
            {
                R3WorldTests.ResolveEvents(engine);
                engine.AdvanceDay();
                engine.Context.State.Player.Resources.Money = 500_000;
                foreach (PendingAttackView pending in engine.PendingAttacks())
                {
                    Assert.Equal(pending.Attack.Date.AddDays(2), pending.Deadline);
                    Assert.Equal(5, pending.Options.Count);
                    Assert.All(pending.Options, o => Assert.True(o.Label.Length is > 0 and <= 40));
                    if (pending.Attack.Id.GetHashCode(StringComparison.Ordinal) % 5 == 0 || day % 4 == 3)
                    {
                        continue;
                    }

                    var available = pending.Options.Where(o => o.Blocked is null).ToList();
                    AttackResponse option = available[(int)((seed + (ulong)day) % (ulong)available.Count)].Response;
                    CommandForecast forecast = engine.ForecastResponse(pending.Attack.Id, option.Id);
                    Assert.Null(forecast.Blocked);
                    Assert.True(forecast.Explanation.Count >= 2);
                    Assert.All(forecast.Explanation, line => Assert.False(string.IsNullOrWhiteSpace(line)));
                    double before = engine.DamageNow(pending.Attack);
                    ResponseResult result = engine.RespondToAttack(pending.Attack.Id, option.Id).Result!;
                    seen[option.Id] = seen.GetValueOrDefault(option.Id) + 1;
                    Assert.True(forecast.Contains("attack_damage", pending.Attack.Id, result.DamageAfter), $"{option.Id}: {result.DamageAfter}");
                    Assert.True(forecast.Contains("attack_weeks", pending.Attack.Id, result.WeeksLeft));
                    Assert.True(forecast.Contains("money", null, (double)result.Money));
                    Assert.Equal(before, result.DamageBefore, 9);
                    if (result.Passed == true)
                    {
                        Assert.True(pending.Attack.WellAnswered);
                        Assert.Equal(before * rules.RespondidoFator, result.DamageAfter, 9);
                        Assert.True(pending.Attack.Ends <= engine.Today.AddDays(28));
                    }
                    else if (option.Id == AttackResponse.Silencio)
                    {
                        Assert.Equal(before, result.DamageAfter, 9);
                        Assert.Equal(pending.Attack.Date.AddDays(7 * 26), pending.Attack.Ends);
                    }
                    else if (option.Id == AttackResponse.DireitoDeResposta)
                    {
                        Assert.InRange(pending.Attack.RulingOn!.Value.DayNumber - engine.Today.DayNumber, 2, 4);
                    }

                    Assert.Equal(CommandFailure.JaFeito, engine.CanRespond(pending.Attack.Id, option.Id));
                }
            }

            // Passado o prazo não dá mais para responder; no dia seguinte fica valendo o silêncio.
            foreach (Attack attack in campaign.Rivals.Attacks.Where(a => a.TargetId == RaceSetup.PlayerId && a.Deadline < engine.Today))
            {
                Assert.NotNull(engine.CanRespond(attack.Id, AttackResponse.MesmoVeiculo));
                if (attack.Deadline < engine.Today.AddDays(-1))
                {
                    Assert.NotNull(attack.Response);
                    expired += attack.Response == AttackResponse.Silencio ? 1 : 0;
                }
            }
        }

        Assert.True(seen.Count >= 4, $"Respostas usadas: {string.Join(", ", seen.Keys)}.");
        Assert.True(seen.Values.Sum() >= 15);
        Assert.True(expired > 0);
    }

    [Fact]
    public void Pacto_tem_previsao_fecha_zonas_e_quebrar_grava_menos_3_e_vira_noticia()
    {
        RivalCatalog catalog = CampaignKit.C.Campaign.Rivals;
        int accepted = 0;
        int refused = 0;
        bool brokeByAttack = false;
        bool brokeByZone = false;
        bool dobradinha = false;
        for (ulong seed = 260; seed < 290 && !(brokeByAttack && brokeByZone && dobradinha && refused > 0); seed++)
        {
            CampaignEngine engine = Rich("medio", seed);
            CampaignState campaign = engine.Context.State.Campaign!;
            foreach (Candidate rival in campaign.Candidates.Where(c => !c.IsPlayer && !c.IsBulk).ToList())
            {
                PactKind kind = rival.Office == campaign.Player.Office ? PactKind.NaoAgressao : PactKind.Dobradinha;
                CommandForecast forecast = engine.ForecastPact(rival.Id, kind);
                if (forecast.Blocked == CommandFailure.PactoEmVigor)
                {
                    continue;
                }

                Assert.Null(forecast.Blocked);
                Assert.True(forecast.Explanation.Count >= 3);
                Assert.Equal(1, forecast.Outcomes.Sum(o => o.Probability), 6);
                Assert.Equal(CommandFailure.AlvoInvalido, engine.CanProposePact(rival.Id, kind == PactKind.Dobradinha ? PactKind.NaoAgressao : PactKind.Dobradinha));
                PactResult result = engine.ProposePact(rival.Id, kind).Result!;
                Assert.True(forecast.Contains("relation", rival.Id, result.Memory.Pontos));
                Assert.Contains(engine.City.Person(rival.Id)!.Memories, m => ReferenceEquals(m, result.Memory));
                Assert.NotNull(engine.CanProposePact(rival.Id, kind));
                if (!result.Accepted)
                {
                    refused++;
                    continue;
                }

                accepted++;
                Pact pact = result.Pact!;
                Assert.Equal(Posture.Compor, campaign.Rivals.Record(rival.Id)!.Plan!.Postures[RaceSetup.PlayerId]);
                if (kind == PactKind.Dobradinha)
                {
                    dobradinha = true;
                    Assert.Contains(engine.Context.State.Ledger.Debts, d => d.ToPersonId == rival.Id);
                    double before = campaign.Player.Recognition;
                    R3WorldTests.IdleWeek(engine);
                    Assert.True(campaign.Player.Recognition > before - 1);
                    continue;
                }

                Assert.Equal(campaign.Territory.Redutos[rival.Id].Select(z => campaign.Territory.Zones[z]), pact.ClosedZones);
                if (!brokeByAttack)
                {
                    // Atacar quem tem pacto com você quebra o pacto.
                    var request = new AttackRequest(rival.Id, AttackType.Tema, AttackChannel.Redes, Theme: "saude");
                    Assert.Contains(engine.ForecastAttack(request).Explanation, line => line.Contains("pacto", StringComparison.OrdinalIgnoreCase));
                    AttackResult attack = engine.AttackRival(request).Result!;
                    Assert.NotNull(attack.PactBroken);
                    brokeByAttack = true;
                }
                else if (!brokeByZone && pact.ClosedZones.Count > 0)
                {
                    // Ato de rua numa zona fechada pelo pacto também.
                    var walk = new CommitmentRequest("caminhada", pact.ClosedZones[0], engine.Today, Turno.Manha);
                    Assert.Equal(rival.Id, engine.PactBrokenBy(walk));
                    Assert.Null(engine.PactBrokenBy(walk with { ZoneId = campaign.Territory.Zones.First(z => !pact.ClosedZones.Contains(z)) }));
                    Assert.Null(engine.Schedule(walk).Failure);
                    engine.RunToday();
                    brokeByZone = true;
                }
                else
                {
                    continue;
                }

                Assert.True(pact.Broken);
                Assert.Null(campaign.Rivals.PactWith(rival.Id));
                Memory broken = Assert.Single(engine.City.Person(rival.Id)!.Memories, m => m.Fact == "pacto_quebrado");
                Assert.Equal(-3, broken.Peso);
                Assert.Equal(-3, catalog.Memory("pacto_quebrado").Peso);
                Assert.Contains(campaign.Facts.Items, f => f.Kind == FactKind.Pacto && f.Sub == "quebrado" && f.Visibility >= 2 && f.TargetId == rival.Id);
            }
        }

        Assert.True(accepted > 0 && refused > 0, $"Aceitos {accepted}, recusados {refused}.");
        Assert.True(brokeByAttack && brokeByZone && dobradinha);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Investigar_revela_ponto_fraco_que_vira_prova()
    {
        int found = 0;
        int nothing = 0;
        for (ulong seed = 300; seed < 310; seed++)
        {
            CampaignEngine engine = Rich("pequeno", seed);
            CampaignState campaign = engine.Context.State.Campaign!;
            foreach (RivalRecord record in campaign.Rivals.Records.Where(r => campaign.Find(r.CandidateId)!.Office == campaign.Player.Office))
            {
                CommandForecast forecast = engine.ForecastInvestigation(record.CandidateId);
                Assert.Null(forecast.Blocked);
                Assert.NotEmpty(forecast.Explanation);
                Assert.NotEmpty(forecast.Outcomes);
                int known = record.Weaknesses.Count(w => w.Known);
                InvestigationResult result = engine.Investigate(record.CandidateId).Result!;
                Assert.True(forecast.Contains("money", null, (double)result.Money));
                if (result.Found is not null)
                {
                    found++;
                    Assert.Equal(known + 1, record.Weaknesses.Count(w => w.Known));
                    Assert.Null(engine.CanAttack(new AttackRequest(record.CandidateId, result.Found.Type, AttackChannel.Redes, result.Found.Id)));
                    Assert.Contains(engine.RivalSheetOf(record.CandidateId)!.KnownWeaknesses, w => w.Id == result.Found.Id);
                }
                else
                {
                    nothing++;
                    Assert.Equal(result.Discovered, engine.City.Person(record.CandidateId)!.Memories.Any(m => m.Fact == "investigado"));
                }
            }
        }

        Assert.True(found > 0 && nothing > 0);
    }

    // ------------------------------------------------------------------ doadores e contas

    private static DonorOffer Offer(CampaignEngine engine, bool porFora, decimal? amount = null)
    {
        Donor donor = engine.City.Donors.First(d => engine.Finance.Offers.All(o => o.DonorId != d.PersonId));
        var offer = new DonorOffer
        {
            Id = $"teste{engine.Finance.NextId++}",
            DonorId = donor.PersonId,
            Amount = amount ?? (porFora ? Math.Round(engine.SpendingLimit * 0.12m) : Math.Round(engine.DonorCapLeft(donor) / 2)),
            PorFora = porFora,
            Deadline = engine.Today.AddDays(7),
        };
        engine.Finance.Offers.Add(offer);
        return offer;
    }

    [Fact]
    public void Aceitar_doacao_tem_previsao_e_cada_resposta_deixa_o_rastro_dela()
    {
        CampaignEngine engine = CampaignKit.NewCareer(City("medio"), 321);
        GameState state = engine.Context.State;
        FinanceCatalog catalog = engine.Context.Campaign.Finance;
        CampaignFinanceLawConfig law = engine.Context.FinanceLaw;
        Assert.All(engine.Donors(), d => Assert.Equal(Math.Floor(d.Donor.Income * 0.10m), d.LegalCap));
        Assert.All(engine.City.Donors, d => Assert.False(string.IsNullOrWhiteSpace(d.RequestText)));

        // Com recibo, até o teto: dinheiro no caixa, dívida no Livro, lembrança no doador, risco igual.
        DonorOffer official = Offer(engine, porFora: false);
        Donor donor = engine.City.Donor(official.DonorId)!;
        Assert.Equal(CommandFailure.ValorInvalido, engine.CanAnswerDonation(official.Id, DonationAnswer.PorFora));
        Assert.Equal(CommandFailure.ValorInvalido, engine.CanAnswerDonation(official.Id, DonationAnswer.OficialIntegral));
        CommandForecast forecast = engine.ForecastDonation(official.Id, DonationAnswer.Oficial);
        Assert.Null(forecast.Blocked);
        Assert.True(forecast.Explanation.Count >= 3);
        Assert.Contains(forecast.Explanation, line => line.Contains("art. 23", StringComparison.Ordinal));
        decimal money = state.Player.Resources.Money;
        DonationResult result = engine.AnswerDonation(official.Id, DonationAnswer.Oficial).Result!;
        Assert.Equal(official.Amount, result.Money);
        Assert.True(forecast.Contains("money", null, (double)result.Money));
        Assert.True(forecast.Contains("risk", null, result.RiskAfter - result.RiskBefore));
        Assert.True(forecast.Contains("relation", donor.PersonId, result.Memory.Pontos));
        Assert.Equal(money + result.Money, state.Player.Resources.Money);
        Assert.Equal(result.RiskBefore, result.RiskAfter);
        Assert.NotNull(result.Debt);
        Assert.Contains(state.Ledger.Debts, d => d.ToPersonId == donor.PersonId);
        Assert.Null(result.Skeleton);
        Assert.Equal(official.Amount, engine.Finance.BySource[FinanceCatalog.PessoaFisica]);
        Assert.Contains(engine.City.Person(donor.PersonId)!.Memories, m => m.Fact == "doacao_aceita");
        Assert.Contains(state.Campaign!.Facts.Items, f => f.Kind == FactKind.Doacao && f.ActorId == donor.PersonId && f.Visibility >= 1);

        // Oferta acima do teto: com recibo só entra o que cabe no teto; aceitar tudo vira irregularidade.
        decimal left = engine.DonorCapLeft(donor);
        DonorOffer big = new() { Id = "grande", DonorId = donor.PersonId, Amount = left + 3000, PorFora = false, Deadline = engine.Today.AddDays(3) };
        engine.Finance.Offers.Add(big);
        Assert.Equal((double)left, engine.ForecastDonation(big.Id, DonationAnswer.Oficial).Line("money")!.Max);
        CommandForecast over = engine.ForecastDonation(big.Id, DonationAnswer.OficialIntegral);
        Assert.Null(over.Blocked);
        Assert.True(over.Line("risk")!.Max > 0);
        DonationResult all = engine.AnswerDonation(big.Id, DonationAnswer.OficialIntegral).Result!;
        Assert.Equal(left + 3000, all.Money);
        Assert.Equal(3000, all.Skeleton!.Amount);
        Assert.Equal(FinanceCatalog.DoacaoAcimaDoLimite, all.Skeleton.Type);
        Assert.True(all.RiskAfter > all.RiskBefore);
        Assert.True(over.Contains("risk", null, all.RiskAfter - all.RiskBefore));
        Assert.Equal(3000m, law.MultaMaximaPorExcesso(donor.Donated, donor.Income));
        Assert.Null(all.Debt);

        // "Por fora": entra tudo, sem limite; vira esqueleto com quem sabe; a notícia não sai (visibilidade 0).
        DonorOffer offBooks = Offer(engine, porFora: true);
        CommandForecast dirty = engine.ForecastDonation(offBooks.Id, DonationAnswer.PorFora);
        Assert.Contains(dirty.Explanation, line => line.Contains(engine.NameOf(offBooks.DonorId), StringComparison.Ordinal));
        DonationResult taken = engine.AnswerDonation(offBooks.Id, DonationAnswer.PorFora).Result!;
        Assert.Equal(offBooks.Amount, taken.Money);
        Assert.Equal(FinanceCatalog.CaixaDois, taken.Skeleton!.Type);
        Assert.Contains(offBooks.DonorId, taken.Skeleton.KnownBy);
        Assert.Equal(offBooks.Amount, taken.Skeleton.Amount);
        Assert.True(dirty.Contains("risk", null, taken.RiskAfter - taken.RiskBefore));
        Assert.True(taken.RiskAfter >= taken.RiskBefore + 10);
        Assert.Contains(state.Campaign.Facts.Items, f => f.Kind == FactKind.Doacao && f.Sub == "por_fora" && f.Visibility == 0);

        // Recusar o "por fora" e pedir recibo: entra bem menos. Recusar tudo: nada entra, o doador lembra.
        DonorOffer second = Offer(engine, porFora: true);
        decimal asOfficial = (decimal)engine.ForecastDonation(second.Id, DonationAnswer.Oficial).Line("money")!.Max;
        Assert.True(asOfficial < second.Amount);
        DonationResult refused = engine.AnswerDonation(second.Id, DonationAnswer.Recusar).Result!;
        Assert.Equal(0, refused.Money);
        Assert.True(refused.Memory.Pontos < 0);
        Assert.Equal(CommandFailure.NaoEncontrado, engine.CanAnswerDonation(second.Id, DonationAnswer.Oficial));

        // Painel Contas: arrecadado por fonte, gasto, barra do limite, irregularidades com "quem sabe" e o risco.
        AccountsView accounts = engine.Accounts();
        Assert.Equal(engine.SpendingLimit, accounts.Limit);
        Assert.True(accounts.LimitIsGameParameter);
        Assert.Equal(official.Amount + all.Money + offBooks.Amount, accounts.Raised);
        Assert.Equal(offBooks.Amount, accounts.OffBooks);
        Assert.Equal((double)(offBooks.Amount / accounts.Limit), accounts.OffBooksShare, 9);
        Assert.Equal(2, accounts.Irregularities.Count);
        IrregularityView irregular = Assert.Single(accounts.Irregularities, x => x.Type == FinanceCatalog.CaixaDois);
        Assert.Contains(engine.NameOf(offBooks.DonorId), irregular.KnownByNames);
        Assert.InRange(accounts.Risk, 1, 100);
        Assert.Equal(taken.RiskAfter, accounts.Risk);
        Assert.Equal(law.PrestacaoParcial(state.Campaign.ElectionDate.Year), accounts.PartialReport);
        Assert.Equal(state.Campaign.ElectionDate.AddDays(30), accounts.FinalReport);
        Assert.All(catalog.Respostas.Keys, key => Assert.Contains(key, FinanceCatalog.AnswerKeys));
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Limite_de_gastos_e_substituicao_marcada_e_gasto_proprio_e_acima_do_limite_viram_irregularidade()
    {
        FinanceCatalog catalog = CampaignKit.C.Campaign.Finance;
        Assert.Contains("LACUNA", catalog.Lacuna);
        Assert.False(CampaignFinanceLawConfig.Default.TabelaDoTseImportada);
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            CampaignEngine engine = CampaignKit.NewCareer(City(porte), 331);
            SpendingLimitRule rule = catalog.LimiteVereador[porte];
            decimal expected = Math.Round(Math.Clamp(rule.PorEleitor * engine.Context.Municipio.Eleitorado, rule.Piso, rule.Teto) / 100) * 100;
            Assert.Equal(expected, engine.SpendingLimit);
            AccountsView clean = engine.Accounts();
            Assert.Equal(0, clean.Risk);
            Assert.Empty(clean.Irregularities);
            Assert.Equal(Math.Floor(expected * 0.10m), clean.OwnResourcesLimit);

            // Gasto todo com dinheiro próprio, acima de 10% do limite: irregularidade (art. 23, § 2º-A).
            engine.Finance.Spent = clean.OwnResourcesLimit + 1000;
            AccountsView own = engine.Accounts();
            IrregularityView excess = Assert.Single(own.Irregularities);
            Assert.Equal(FinanceCatalog.RecursosPropriosAcima, excess.Type);
            Assert.Equal(1000, excess.Amount);

            // Gasto declarado acima do limite do cargo.
            engine.Finance.BySource[FinanceCatalog.Fundo] = expected * 2;
            engine.Finance.Spent = expected + 5000;
            AccountsView over = engine.Accounts();
            IrregularityView limit = Assert.Single(over.Irregularities);
            Assert.Equal(FinanceCatalog.GastoAcimaDoLimite, limit.Type);
            Assert.Equal(5000, limit.Amount);
            Assert.True(over.LimitUsed > 1);
            Assert.True(over.Risk > 0);
        }
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Fundo_eleitoral_coletivo_e_prestacao_parcial_chegam_nas_datas()
    {
        CampaignEngine engine = CampaignKit.NewCareer(City("medio"), 341, strongestParty: true);
        GameState state = engine.Context.State;
        CampaignState campaign = state.Campaign!;
        CampaignFinanceLawConfig law = engine.Context.FinanceLaw;
        int year = campaign.ElectionDate.Year;
        Assert.Equal(new DateOnly(year, 5, 15), law.FinanciamentoColetivoAbre(year));

        // A cota do fundo é decisão do dirigente: relação, viabilidade e distância da linha do partido.
        FundView fund = engine.FundQuota();
        Assert.NotNull(fund.BossId);
        Assert.True(engine.City.Person(fund.BossId!)!.Has(PersonRole.Dirigente));
        Assert.NotEmpty(fund.Explanation);
        Assert.False(fund.Paid);
        Assert.Equal(0, fund.LineDistance);
        engine.City.Person(fund.BossId!)!.Affinity += 60;
        Assert.True(engine.FundQuota().Quota > fund.Quota);
        engine.City.Person(fund.BossId!)!.Affinity -= 60;
        campaign.Platform.Positions["saude"] = engine.PartyPosition("saude") > 0 ? -2 : 2;
        Assert.True(engine.FundQuota().LineDistance > 0);
        Assert.True(engine.FundQuota().Quota < fund.Quota);
        campaign.Platform.Positions.Clear();

        // Antes de 15 de maio não há financiamento coletivo; depois junta, mas fica retido até o registro.
        IdleUntil(engine, new DateOnly(year, 5, 1));
        Assert.Equal(0, engine.Finance.CrowdHeld);
        Assert.False(engine.Finance.BySource.ContainsKey(FinanceCatalog.Coletivo));
        IdleUntil(engine, new DateOnly(year, 7, 15));
        Assert.True(engine.Finance.CrowdHeld > 0);
        Assert.False(engine.Finance.BySource.ContainsKey(FinanceCatalog.Coletivo));
        Assert.False(engine.Finance.FundPaid);
        decimal held = engine.Finance.CrowdHeld;

        // Com o registro (15 de agosto) o retido é liberado; no início da campanha oficial sai a cota do fundo.
        IdleUntil(engine, new DateOnly(year, 9, 1));
        Assert.Equal(0, engine.Finance.CrowdHeld);
        Assert.True(engine.Finance.BySource[FinanceCatalog.Coletivo] >= held);
        Assert.True(engine.Finance.FundPaid);
        Assert.True(engine.Finance.BySource.GetValueOrDefault(FinanceCatalog.Fundo) > 0);
        Assert.Contains(state.Ledger.Debts, d => d.ToPersonId == fund.BossId);
        Assert.Contains(campaign.Facts.Items.Concat(campaign.Agenda.Feed.Select(f => campaign.Facts.Find(f.FactId!)).Where(f => f is not null).Select(f => f!)), f => f.Kind == FactKind.Fundo);

        // Prestação parcial em setembro.
        Assert.False(engine.Finance.PartialDone);
        IdleUntil(engine, law.PrestacaoParcial(year).AddDays(7));
        Assert.True(engine.Finance.PartialDone);
        Assert.Contains(campaign.Agenda.Feed, f => f.Kind == FactKind.Contas);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Esqueleto_vem_a_tona_pela_formula_base_gravidade_quem_sabe_e_exposicao()
    {
        CampaignEngine engine = CampaignKit.NewCareer(City("medio"), 351);
        GameState state = engine.Context.State;
        FinanceRules rules = engine.Context.Campaign.Finance.Regras;
        Donor a = engine.City.Donors[0];
        Donor b = engine.City.Donors[1];
        LedgerSkeleton skeleton = state.Ledger.AddSkeleton(FinanceCatalog.CaixaDois, "Dinheiro por fora", 3, engine.Today, [a.PersonId, b.PersonId], 5000);

        // Ninguém hostil sabe: não vem à tona.
        Assert.Empty(engine.HostileKnowers(skeleton));
        Assert.Equal(0, engine.SurfaceChance(skeleton));

        // Relação abaixo de −20 em quem sabe: chance = base × gravidade × pessoas × exposição.
        engine.City.Person(a.PersonId)!.Affinity = -50;
        double exposure = engine.Exposure();
        Assert.True(exposure >= rules.ExposicaoPorCargo["vereador"]);
        Assert.Single(engine.HostileKnowers(skeleton));
        Assert.Equal(rules.EsqueletoBase * 3 * 1 * exposure, engine.SurfaceChance(skeleton), 9);
        engine.City.Person(b.PersonId)!.Affinity = -50;
        Assert.Equal(rules.EsqueletoBase * 3 * 2 * exposure, engine.SurfaceChance(skeleton), 9);
        Assert.Equal(-20, rules.RelacaoHostil);

        // Membro da equipe que sabe: vale a lealdade.
        StaffMember member = engine.City.Staff[0];
        member.Hired = true;
        member.Loyalty = rules.LealdadeHostil - 1;
        skeleton.KnownBy.Add(member.PersonId);
        Assert.Equal(3, engine.HostileKnowers(skeleton).Count);
        member.Loyalty = rules.LealdadeHostil;
        Assert.Equal(2, engine.HostileKnowers(skeleton).Count);

        // Com o tempo, vem à tona: vira fato de visibilidade 3, custa imagem e conta como denúncia.
        for (int week = 0; week < 60 && !skeleton.Exposed && state.Campaign!.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha; week++)
        {
            R3WorldTests.IdleWeek(engine);
        }

        if (skeleton.Exposed)
        {
            Assert.Contains(state.Campaign!.Facts.Items.Select(f => f.Kind).Concat(state.Campaign.Agenda.Feed.Select(f => f.Kind)), k => k == FactKind.Esqueleto);
            Assert.NotEmpty(engine.Finance.Denuncias);
            Assert.Equal(0, engine.SurfaceChance(skeleton));
        }
    }

    [Fact]
    public void Contas_depois_da_eleicao_julgamento_e_representacao_do_artigo_30A()
    {
        FinanceCatalog catalog = CampaignKit.C.Campaign.Finance;
        FinanceRules rules = catalog.Regras;
        CampaignFinanceLawConfig law = CampaignFinanceLawConfig.Default;
        var election = new DateOnly(2028, 10, 1);

        AccountsCase Case(decimal offBooks, int risk, int hostile = 0, bool elected = true) => new()
        {
            Municipio = 1, ElectionDate = election, Elected = elected, Limit = 100_000, Spent = 60_000, OffBooks = offBooks, Risk = risk, HostileKnowers = hostile, Exposed = false, Denuncias = 0,
        };

        // O processo de contas anda por data, sem depender da campanha: basta o estado com o processo aberto.
        GameState template = CampaignKit.NewCareer(City("pequeno"), 361).Context.State;
        GameState Run(AccountsCase accounts, ulong seed)
        {
            template.Accounts = accounts;
            var rng = new Rng(seed);
            for (DateOnly day = election; day < election.AddDays(7 * 118); day = day.AddDays(7))
            {
                AccountsEngine.Advance(template, catalog, law, day, rng);
            }

            return template;
        }

        // Campanha limpa: contas aprovadas, nenhuma representação, nunca perde o mandato.
        for (ulong seed = 1; seed <= 40; seed++)
        {
            AccountsCase clean = Run(Case(0, 0), seed).Accounts!;
            Assert.Equal(AccountsVerdict.Aprovadas, clean.Verdict);
            Assert.Equal(AccountsStage.Encerrado, clean.Stage);
            Assert.False(clean.MandateLost);
            Assert.DoesNotContain(clean.Log, h => h.Kind == "representacao");
            Assert.True(clean.JudgmentOn >= law.PrestacaoFinal(election));
        }

        Assert.Equal(0, AccountsEngine.DenunciationChance(Case(0, 0, hostile: 3), rules));
        Assert.False(AccountsEngine.Grave(Case(5_000, 20), rules));
        Assert.True(AccountsEngine.Grave(Case(30_000, 60), rules));
        Assert.Equal(AccountsVerdict.AprovadasComRessalvas, Run(Case(0, rules.RiscoRessalvas), 1).Accounts!.Verdict);
        Assert.Equal(AccountsVerdict.Desaprovadas, Run(Case(0, rules.RiscoDesaprovacao), 1).Accounts!.Verdict);

        // Mais de 30% do limite "por fora": parte perde o mandato, sempre depois da posse e em até 2 anos.
        int lost = 0;
        int represented = 0;
        const int Runs = 400;
        for (ulong seed = 1; seed <= Runs; seed++)
        {
            AccountsCase dirty = Run(Case(50_000, 85, hostile: 1), seed).Accounts!;
            represented += dirty.Log.Any(h => h.Kind == "representacao" && h.Text == catalog.Texto("representacao")) ? 1 : 0;
            if (dirty.MandateLost)
            {
                lost++;
                Assert.True(dirty.MandateLostOn > new DateOnly(2029, 1, 1));
                Assert.True(dirty.MandateLostOn < new DateOnly(2031, 1, 1));
            }

            Assert.False(dirty.RepresentationOpen);
        }

        Assert.InRange(lost / (double)Runs, 0.2, 0.45);
        Assert.True(represented > lost);
        Assert.True(AccountsEngine.CassationChance(Case(60_000, 90), rules) > AccountsEngine.CassationChance(Case(35_000, 70), rules));
        Assert.True(AccountsEngine.DenunciationChance(Case(35_000, 70, hostile: 3), rules) > AccountsEngine.DenunciationChance(Case(35_000, 70), rules));

        // Quem não se elegeu não tem diploma a cassar.
        Assert.All(Enumerable.Range(1, 30), seed => Assert.False(Run(Case(35_000, 70, elected: false), (ulong)seed).Accounts!.MandateLost));
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void A_apuracao_abre_o_processo_de_contas_com_o_que_a_campanha_fez()
    {
        CampaignEngine engine = CampaignKit.NewCareer(City("pequeno"), 371, strongestParty: true);
        GameState state = engine.Context.State;
        DonorOffer offer = Offer(engine, porFora: true, Math.Round(engine.SpendingLimit * 0.4m));
        engine.AnswerDonation(offer.Id, DonationAnswer.PorFora);
        engine.City.Person(offer.DonorId)!.Affinity = -80;
        while (state.Campaign!.Phase != CampaignPhase.Votacao)
        {
            R3WorldTests.IdleWeek(engine);
        }

        engine.CastPlayerVote(new UrnaVote(Office.Vereador, state.Campaign.Player.Number, false));
        ElectionOutcome outcome = engine.ResolveElection();
        AccountsCase accounts = state.Accounts!;
        Assert.Equal(outcome.PlayerElected, accounts.Elected);
        Assert.Equal(engine.SpendingLimit, accounts.Limit);
        Assert.True(accounts.OffBooks >= offer.Amount);
        Assert.True(accounts.OffBooksShare > 0.3);
        Assert.True(accounts.Risk >= 40);
        Assert.True(accounts.HostileKnowers >= 1 || accounts.Exposed);
        Assert.Equal(AccountsStage.AguardandoPrestacao, accounts.Stage);

        // O processo de contas vai e volta pelo save.
        GameState loaded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        Assert.Equal(CampaignKit.Canonical(state), CampaignKit.Canonical(loaded));
        Assert.Equal(accounts.OffBooks, loaded.Accounts!.OffBooks);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Depois_da_posse_o_processo_de_contas_segue_e_a_cassacao_encerra_o_mandato()
    {
        CampaignEngine engine = CampaignKit.NewCareer(City("pequeno"), 375, strongestParty: true);
        GameState state = engine.Context.State;
        var robot = new CampaignRobot(engine, 375, new RobotOptions { Answer = LeaderAnswer.Aceitar });
        robot.PlayToElection();
        ElectionOutcome outcome = robot.Vote();
        FinanceCatalog content = engine.Context.Campaign.Finance;
        CampaignFinanceLawConfig law = engine.Context.FinanceLaw;
        Planalto.Core.Career.CareerFlow.ConcludeElection(engine.Context, CampaignKit.LegislatureContext(state));

        // Na posse, a prestação final já foi entregue e o prazo da representação (15 dias da diplomação) já correu.
        AccountsCase accounts = state.Accounts!;
        Assert.Equal(AccountsStage.AguardandoJulgamento, accounts.Stage);
        Assert.True(accounts.RepresentationChecked);
        Assert.Equal(outcome.PlayerElected, state.Mandate is not null);
        if (state.Mandate is null)
        {
            return;
        }

        // Com uma representação aberta e julgada procedente, o diploma é cassado e o mandato acaba.
        state.Accounts = new AccountsCase
        {
            Municipio = accounts.Municipio, ElectionDate = accounts.ElectionDate, Elected = true, Limit = 100_000, Spent = 90_000, OffBooks = 80_000, Risk = 100,
            HostileKnowers = 2, Exposed = true, Denuncias = 1, Stage = AccountsStage.Julgadas, Verdict = AccountsVerdict.Desaprovadas, RepresentationChecked = true,
            RepresentationOpen = true, RulingOn = state.Calendar.WeekStart,
        };
        for (ulong seed = 1; seed < 60 && state.Mandate is not null; seed++)
        {
            state.Accounts.RepresentationOpen = true;
            state.Accounts.RulingOn = state.Calendar.WeekStart;
            state.Accounts.Stage = AccountsStage.Julgadas;
            Planalto.Core.Career.CareerFlow.AccountsWeek(state, content, law, new Rng(seed));
        }

        Assert.True(state.Accounts.MandateLost);
        Assert.Null(state.Mandate);
        Assert.Contains(state.Accounts.Log, h => h.Kind == "cassacao");
    }

    // ------------------------------------------------------------------ eventos ligados ao save e lint de rastro

    [Fact]
    public void Lint_de_rastro_e_de_troca_passam_e_reprovam_escolha_relevante_sem_rastro()
    {
        EventContent events = CampaignKit.C.Events;
        Assert.Empty(DecisionLint.Run(CampaignKit.C.Campaign, events));
        Assert.Empty(DecisionLint.Trace(events));
        Assert.Contains(events.Events, e => e.Choices.Any(c => c.Relevant));
        Assert.All(events.Events.SelectMany(e => e.Choices).Where(c => c.Relevant), c =>
            Assert.True(c.Effects.LeavesTrace || (c.Test is not null && c.Test.Success.LeavesTrace && c.Test.Failure.LeavesTrace)));

        EventDefinition Event(EventChoice choice) => new()
        {
            Id = "teste", Category = "campanha", Phases = ["campanha"], Rarity = Rarity.Comum, CooldownWeeks = 0, Title = "t", Variants = ["v"],
            Choices = [choice, new EventChoice { Label = "outra" }],
        };
        EventContent With(EventChoice choice) => new() { Config = events.Config, Events = [Event(choice)] };
        var memory = new MemoryEffect { Quem = EventRole.Rival, Fato = "x", Peso = 1, Pontos = 5, Texto = "Lembra" };

        // Relevante sem Livro nem lembrança: reprova, mesmo abrindo uma cadeia.
        var untraced = new EventChoice { Label = "a", Relevant = true, Gain = ["dinheiro"], Cost = ["risco"], Effects = new Effects { Money = 100, Schedule = [new ScheduledEvent("teste", 2)] } };
        Assert.Contains(DecisionLint.Trace(With(untraced)), e => e.Contains("sem rastro", StringComparison.Ordinal));
        Assert.Contains(DecisionLint.Events(With(untraced)), e => e.Contains("sem rastro", StringComparison.Ordinal));

        // Com lembrança em alguém, passa.
        var traced = new EventChoice { Label = "a", Relevant = true, Gain = ["relacao"], Cost = ["dinheiro"], Effects = new Effects { Money = -100, Memories = [memory] } };
        Assert.Empty(DecisionLint.Events(With(traced)));

        // Com teste, o rastro tem de ficar nos dois desfechos.
        var half = new EventChoice
        {
            Label = "a", Relevant = true, Gain = ["relacao"], Cost = ["risco"],
            Test = new ChoiceTest { Attribute = Planalto.Core.Characters.AttributeId.Oratoria, SuccessText = "s", Success = new Effects { Memories = [memory] }, FailureText = "f", Failure = new Effects() },
        };
        Assert.Contains(DecisionLint.Trace(With(half)), e => e.Contains("sem rastro", StringComparison.Ordinal));

        // O que fica no Livro não pode passar por escolha comum.
        var hidden = new EventChoice { Label = "a", Effects = new Effects { Promise = new PromiseEffect { Para = EventRole.Lideranca, Objeto = "obra", Texto = "Fazer" } } };
        Assert.Contains(DecisionLint.Events(With(hidden)), e => e.Contains("não está marcada como relevante", StringComparison.Ordinal));
    }

    [Fact]
    public void Eventos_usam_pessoas_zonas_e_veiculos_do_save_e_gravam_rastro()
    {
        CampaignEngine engine = CampaignKit.NewCareer(City("medio"), 381);
        CampaignContext ctx = engine.Context;
        GameState state = ctx.State;
        CampaignState campaign = state.Campaign!;
        EventContent events = ctx.Events!;
        Assert.DoesNotContain(events.Events.SelectMany(e => e.Choices).SelectMany(c => c.Test is null ? [c.Effects] : new[] { c.Effects, c.Test.Success, c.Test.Failure }), fx => fx.Energy != 0);

        // Todo evento que cita alguém do save resolve o nome: nenhuma variável sobra no texto.
        foreach (EventDefinition e in events.Events.Where(e => EventEngine.CanBind(ctx, e) && e.Conditions.NeedsCommitment is null))
        {
            campaign.Events.Pending.Clear();
            PendingEvent pending = EventEngine.Trigger(ctx, e.Id, state.Rng);
            Assert.DoesNotContain("{", pending.Text);
            Assert.DoesNotContain("{", pending.Title);
            HashSet<string> needs = EventValidation.Needs(e);
            foreach (string role in EventRole.People.Where(r => r != EventRole.Rival && needs.Contains(r)))
            {
                Assert.NotNull(engine.City.Person(pending.People[role]));
                Assert.Contains(engine.NameOf(pending.People[role]), pending.Variables[$"{role}.nome"]);
            }

            if (needs.Contains("veiculo"))
            {
                // Nome fictício sempre com o que é.
                Assert.Equal(engine.OutletLabel(engine.City.Outlet(pending.OutletId!)!), pending.Variables["veiculo.nome"]);
            }

            if (needs.Contains("zona"))
            {
                Assert.Contains(pending.ZoneId, campaign.Territory.Zones);
            }
        }

        // Apoio com preço: a promessa vai para o Livro em nome da liderança, que lembra; a cobrança volta à mesma pessoa.
        campaign.Events.Pending.Clear();
        PendingEvent offer = EventEngine.Trigger(ctx, "campanha_apoio_lider_local", state.Rng);
        string leaderId = offer.People[EventRole.Lideranca];
        int promises = state.Ledger.Promises.Count;
        EventEngine.Resolve(ctx, offer, 0, state.Rng);
        Promise promise = Assert.Single(state.Ledger.Promises.Skip(promises));
        Assert.Equal(leaderId, promise.ToId);
        Assert.Equal(offer.ZoneId, promise.ZoneId);
        Assert.DoesNotContain("{", promise.Text);
        Assert.Contains(engine.City.Person(leaderId)!.Memories, m => m.Fact == "promessa" && m.Peso == 3);
        DueEvent due = Assert.Single(campaign.Events.Scheduled, s => s.EventId == "campanha_cobranca_lider");
        Assert.Equal(leaderId, due.People![EventRole.Lideranca]);
        campaign.Events.Scheduled.Remove(due);
        campaign.Events.Scheduled.Add(due with { Due = state.Calendar.WeekStart });
        EventEngine.Roll(ctx, new Rng(1));
        PendingEvent charge = Assert.Single(campaign.Events.Pending, p => p.EventId == "campanha_cobranca_lider");
        Assert.Equal(leaderId, charge.People[EventRole.Lideranca]);
        Assert.Equal(offer.ZoneId, charge.ZoneId);

        // Dinheiro por fora do evento: esqueleto com o doador entre quem sabe, e entra nas contas como "por fora".
        campaign.Events.Pending.Clear();
        PendingEvent cash = EventEngine.Trigger(ctx, "campanha_doacao_por_fora", state.Rng);
        EventEngine.Resolve(ctx, cash, 0, state.Rng);
        LedgerSkeleton skeleton = state.Ledger.Skeletons[^1];
        Assert.Contains(cash.People[EventRole.Doador], skeleton.KnownBy);
        Assert.Equal(8000, skeleton.Amount);
        Assert.Equal(8000, engine.Finance.OffBooks);
        Assert.Equal(8000, engine.Accounts().OffBooks);

        // Chuva no comício só com comício marcado; adiar desmarca o comício.
        EventDefinition rain = events.Find("campanha_comicio_chuva")!;
        Assert.Equal("comicio", rain.Conditions.NeedsCommitment);
        Assert.False(EventEngine.Fits(ctx, rain, state.Calendar.WeekStart));
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Evento_de_chuva_so_com_comicio_marcado_e_eventos_dinamicos_disparam_por_fato()
    {
        CampaignEngine engine = Rich("medio", 391);
        CampaignContext ctx = engine.Context;
        GameState state = ctx.State;
        CampaignState campaign = state.Campaign!;
        EventDefinition rain = ctx.Events!.Find("campanha_comicio_chuva")!;
        IdleUntil(engine, campaign.CampaignStart.AddDays(7));
        state.Player.Resources.Money = 500_000;
        R3WorldTests.ResolveEvents(engine);
        Assert.False(EventEngine.IsEligible(ctx, rain, state.Calendar.WeekStart));
        CampaignKit.FundRua(engine);
        var rally = new CommitmentRequest(CommitmentCatalog.Comicio, campaign.Territory.Zones[campaign.Territory.HomeZone], engine.Today.AddDays(1), Turno.Tarde);
        Assert.Null(engine.Schedule(rally).Failure);
        Assert.True(EventEngine.IsEligible(ctx, rain, state.Calendar.WeekStart));
        PendingEvent pending = EventEngine.Trigger(ctx, rain.Id, state.Rng);
        EventEngine.Resolve(ctx, pending, 1, state.Rng);
        Assert.DoesNotContain(campaign.Agenda.Scheduled, c => c.Type == CommitmentCatalog.Comicio);

        // Eventos dinâmicos (GDD 15.4) não entram no sorteio da semana: só disparam com o fato deles.
        List<EventDefinition> dynamic = ctx.Events.Events.Where(e => e.Conditions.Trigger is not null).ToList();
        Assert.True(dynamic.Count >= 4);
        Assert.All(dynamic, e => Assert.False(EventEngine.IsEligible(ctx, e, state.Calendar.WeekStart)));

        // Liderança que deixa o jogador → "aliado vai embora", com a própria liderança e a zona dela.
        campaign.Events.Pending.Clear();
        Leader leader = engine.City.Leaders[0];
        bool fired = false;
        for (int i = 0; i < 40 && !fired; i++)
        {
            campaign.Events.LastFired.Clear();
            campaign.Events.Pending.Clear();
            Fact fact = engine.Emit(FactKind.LiderancaPerdida, "saiu", null, leader.Zones[0], leader.PersonId);
            PendingEvent? left = campaign.Events.Pending.FirstOrDefault(p => p.EventId == "campanha_din_lideranca_perdida");
            if (left is not null)
            {
                fired = true;
                Assert.Equal(leader.PersonId, left.People[EventRole.Lideranca]);
                Assert.Equal(leader.Zones[0], left.ZoneId);
                Assert.Equal(fact.Id, left.FactId);
                Assert.Contains(engine.NameOf(leader.PersonId), left.Text);
                int memories = engine.City.Person(leader.PersonId)!.Memories.Count;
                EventEngine.Resolve(ctx, left, 2, state.Rng);
                Assert.Equal(memories + 1, engine.City.Person(leader.PersonId)!.Memories.Count);
            }
        }

        Assert.True(fired, "O fato não disparou o evento dinâmico.");
    }

    // ------------------------------------------------------------------ campanha inteira com os sistemas da R3

    public static TheoryData<int, ulong> PorPorte()
    {
        var data = new TheoryData<int, ulong>();
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            foreach (ulong seed in new ulong[] { 401, 402, 403 })
            {
                data.Add(City(porte), seed);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PorPorte))]
    [Trait("Categoria", "Lento")]
    public void Campanha_inteira_com_os_sistemas_da_R3_e_save_e_load_no_meio_termina_igual(int ibge, ulong seed)
    {
        var options = new RobotOptions { R2b = true, R3 = true, Answer = LeaderAnswer.Aceitar };
        CampaignEngine straight = CampaignKit.NewCareer(ibge, seed);
        var robotA = new CampaignRobot(straight, seed, options);
        robotA.PlayToElection(_ => CampaignAutoplayTests.AssertState(straight));
        robotA.Vote();

        CampaignEngine saved = CampaignKit.NewCareer(ibge, seed);
        var robotB = new CampaignRobot(saved, seed, options);
        int loads = 0;
        robotB.PlayToElection(week =>
        {
            if (week % 9 == 5)
            {
                GameState state = robotB.Engine.Context.State;
                string json = GameStateSerializer.Serialize(state);
                GameState loaded = GameStateSerializer.Deserialize(json);

                // O estado novo vai e volta: fatos, notícias, planos e verba dos rivais, ataques, doadores, veículos.
                Assert.Equal(CampaignKit.Canonical(state), CampaignKit.Canonical(loaded));
                CampaignState before = state.Campaign!;
                CampaignState after = loaded.Campaign!;
                Assert.Equal(before.Facts.Items.Count, after.Facts.Items.Count);
                Assert.Equal(before.Media.News.Count, after.Media.News.Count);
                Assert.Equal(before.Rivals.Records.Select(r => r.Plan!.Strategy), after.Rivals.Records.Select(r => r.Plan!.Strategy));
                Assert.Equal(before.Rivals.Records.Sum(r => r.Budget.Sum(a => a.Amount)), after.Rivals.Records.Sum(r => r.Budget.Sum(a => a.Amount)));
                Assert.Equal(before.Rivals.Attacks.Count, after.Rivals.Attacks.Count);
                Assert.Equal(before.Finance.Spent, after.Finance.Spent);
                Assert.Equal(state.City!.Outlets.Select(o => o.Name), loaded.City!.Outlets.Select(o => o.Name));
                Assert.Equal(state.City.Donors.Sum(d => d.Donated), loaded.City.Donors.Sum(d => d.Donated));
                robotB.Engine = CampaignEngine.Start(CampaignKit.Context(loaded));
                loads++;
            }
        });
        robotB.Vote();

        Assert.True(loads >= 3, $"Só {loads} cargas no meio da campanha.");
        Assert.Equal(CampaignPhase.Encerrada, robotB.Engine.Context.State.Campaign!.Phase);
        Assert.Equal(CampaignKit.Canonical(straight.Context.State), CampaignKit.Canonical(robotB.Engine.Context.State));
        Assert.NotNull(straight.Context.State.Accounts);

        // A campanha usou os sistemas novos.
        CampaignState campaign = straight.Context.State.Campaign!;
        Assert.NotEmpty(campaign.Media.News);
        Assert.NotEmpty(campaign.Facts.Items);
        Assert.True(campaign.Finance.Spent > 0);
        Assert.Contains(robotA.R2bCommands.Keys, k => k.StartsWith("doacao_", StringComparison.Ordinal) || k == "entrevista_veiculo" || k == "pauta");
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void O_robo_da_R3_usa_todos_os_comandos_novos_ao_longo_das_campanhas()
    {
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (porte, seed) in new[] { ("medio", 411UL), ("grande", 412UL), ("medio", 413UL), ("grande", 414UL) })
        {
            CampaignEngine engine = CampaignKit.NewCareer(City(porte), seed, strongestParty: true);
            var robot = new CampaignRobot(engine, seed, new RobotOptions { R2b = true, R3 = true, Answer = LeaderAnswer.Aceitar });
            robot.PlayToElection();
            robot.Vote();
            foreach (var (key, count) in robot.R2bCommands)
            {
                used[key] = used.GetValueOrDefault(key) + count;
            }
        }

        // (A resposta a ataque depende de algum rival atacar o jogador nessas campanhas; ela tem o próprio teste.)
        List<string> missing = new[] { "doacao_", "ataque", "pacto", "investigar", "pauta", "entrevista_veiculo" }
            .Where(prefix => !used.Keys.Any(k => k.StartsWith(prefix, StringComparison.Ordinal))).ToList();
        Assert.True(missing.Count == 0, $"Comandos que o robô não usou: {string.Join(", ", missing)}. Usados: {string.Join(", ", used.Keys.Order())}.");
    }
}
