using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Elections;
using Planalto.Core.Electorate;
using Planalto.Core.Events;
using Planalto.Core.Media;
using Planalto.Core.People;
using Planalto.Core.State;
using Xunit;

namespace Planalto.Core.Tests.Campaign;

/// <summary>
/// R3 (GDD 17.11 a 17.13): o mundo se mexe sozinho (princípio b), rivais com plano e resposta a ataque (princípio c),
/// mídia que só publica fatos, e pessoas com rosto e memória nos papéis novos.
/// </summary>
public class R3WorldTests
{
    internal static int City(string porte)
    {
        var all = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais).OrderBy(m => m.CodigoIbge).ToList();
        return all.First(m => m.Porte == porte).CodigoIbge;
    }

    /// <summary>Uma semana sem nenhum comando do jogador: só resolve o evento que trava o tempo, pela primeira escolha possível.</summary>
    internal static void IdleWeek(CampaignEngine engine)
    {
        ResolveEvents(engine);
        engine.AdvanceWeek();
    }

    internal static void ResolveEvents(CampaignEngine engine)
    {
        GameState state = engine.Context.State;
        while (state.Campaign!.Events.Pending.Count > 0)
        {
            PendingEvent pending = state.Campaign.Events.Pending[0];
            var choices = engine.Context.Events!.Find(pending.EventId)!.Choices;
            EventEngine.Resolve(engine.Context, pending, Enumerable.Range(0, choices.Count).First(c => EventEngine.Blocked(engine.Context, choices[c]) is null), state.Rng);
        }

        while (engine.Team.Pending.Count > 0)
        {
            StaffDecision decision = engine.Team.Pending[0];
            engine.AnswerStaffDecision(decision.Id, engine.Context.Campaign.Staff.Eventos.First(e => e.Id == decision.EventId).Opcoes[0].Id);
        }
    }

    private static List<string> ZoneLeaders(CampaignEngine engine)
    {
        Office office = engine.Context.State.Campaign!.Player.Office;
        HashSet<string> named = engine.Context.State.Campaign.ForOffice(office).Where(c => !c.IsBulk).Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        return engine.ProjectByZone(office, null).Zones.Select(z => z.Votes.Where(v => named.Contains(v.Key)).MaxBy(v => v.Value).Key).ToList();
    }

    [Theory]
    [InlineData("pequeno", 5UL)]
    [InlineData("medio", 6UL)]
    [InlineData("grande", 7UL)]
    [InlineData("metropole", 8UL)]
    public void Em_40_semanas_sem_comando_o_mundo_se_mexe_sozinho(string porte, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(City(porte), seed);
        GameState state = engine.Context.State;
        CampaignState campaign = state.Campaign!;
        var territory = campaign.Territory;
        List<string> leadersBefore = ZoneLeaders(engine);
        var presenceBefore = campaign.Candidates.Where(c => !c.IsPlayer && !c.IsBulk).ToDictionary(c => c.Id, c => territory.Presence[c.Id].ToArray());
        var economyWeeks = new List<int>();
        var leaderChanged = false;
        int weeks = 0;
        int seenNews = 0;
        while (campaign.Phase is CampaignPhase.PreCampanha or CampaignPhase.Campanha && weeks < 40)
        {
            DateOnly from = state.Calendar.WeekStart;
            int factsBefore = campaign.Facts.NextSeq;
            IdleWeek(engine);
            weeks++;

            // Pelo menos 3 notícias na semana que não citam o jogador; nenhuma sem fato de origem.
            List<NewsItem> news = campaign.Media.News.Where(n => n.Date >= from && n.Date < from.AddDays(7)).ToList();
            Assert.True(news.Count(n => !n.CitaJogador) >= 3, $"Semana {weeks}: só {news.Count(n => !n.CitaJogador)} notícias sem o jogador.");
            Assert.All(news, n => Assert.NotNull(campaign.Facts.Find(n.FactId)));
            seenNews += news.Count;
            if (campaign.Facts.Items.Any(f => f.Seq >= factsBefore && f.Kind == FactKind.Economia))
            {
                economyWeeks.Add(weeks);
            }

            leaderChanged |= !ZoneLeaders(engine).SequenceEqual(leadersBefore);

            // O jogador não deu comando nenhum: nada marcado, nenhuma verba.
            Assert.Empty(campaign.Agenda.Scheduled);
            Assert.Empty(campaign.Budget.Plan);
        }

        Assert.True(weeks >= 30, $"A campanha só teve {weeks} semanas.");
        Assert.True(seenNews > weeks * 3);
        Assert.True(leaderChanged, "A liderança da intenção não mudou em nenhuma zona.");

        // A presença de algum rival muda em mais da metade das zonas.
        int zones = territory.Zones.Count;
        Assert.Contains(presenceBefore, p => Enumerable.Range(0, zones).Count(z => Math.Abs(territory.Presence[p.Key][z] - p.Value[z]) > 0.5) * 2 > zones);

        // Pelo menos 1 fato econômico a cada 8 semanas.
        Assert.NotEmpty(economyWeeks);
        Assert.True(economyWeeks[0] <= 8);
        Assert.All(economyWeeks.Zip(economyWeeks.Skip(1)), pair => Assert.True(pair.Second - pair.First <= 8));
        Assert.True(weeks - economyWeeks[^1] < 8);

        // Os rivais agiram em dias, com compromissos públicos que viraram fato com data dentro da semana.
        Assert.Contains(campaign.Facts.Items, f => f.Kind == FactKind.RivalZona && f.ActorId is not null);
        Assert.Contains(campaign.Agenda.Feed, f => f.Kind == FactKind.RivalZona && f.FactId is not null);
        Assert.All(campaign.Agenda.Feed, f => Assert.NotNull(f.FactId));
    }

    [Fact]
    public void Depois_de_um_ataque_os_rivais_nao_calculistas_respondem_em_ate_14_dias()
    {
        int attacked = 0;
        int answered = 0;
        foreach (var (porte, seed) in new[] { ("pequeno", 21UL), ("medio", 22UL), ("grande", 23UL), ("metropole", 24UL), ("medio", 25UL) })
        {
            CampaignEngine engine = CampaignKit.NewCareer(City(porte), seed);
            GameState state = engine.Context.State;
            CampaignState campaign = state.Campaign!;
            for (int i = 0; i < 3; i++)
            {
                IdleWeek(engine);
            }

            state.Player.Resources.Money = 1_000_000;
            string theme = engine.Context.Themes.Themes[0].Id;
            var targets = new Dictionary<string, DateOnly>(StringComparer.Ordinal);
            foreach (Candidate rival in campaign.ForOffice(campaign.Player.Office).Where(c => !c.IsPlayer && !c.IsBulk))
            {
                if (engine.City.Person(rival.Id)!.Temperament == Temperament.Calculista)
                {
                    continue;
                }

                var request = new AttackRequest(rival.Id, AttackType.Tema, AttackChannel.Redes, Theme: theme);
                var (failure, result) = engine.AttackRival(request);
                Assert.Null(failure);
                targets[rival.Id] = engine.Today;

                // A lembrança do ataque fica na pessoa do rival, e não se apaga.
                Memory memory = Assert.Single(engine.City.Person(rival.Id)!.Memories, m => m.Fact == "ataque");
                Assert.Equal(-3, memory.Peso);
                Assert.True(engine.RelationOf(rival.Id) < -15);
                Assert.True(result!.Attack.Damage > 0);
            }

            for (int day = 0; day < 14; day++)
            {
                ResolveEvents(engine);
                engine.AdvanceDay();
            }

            foreach (var (rivalId, when) in targets)
            {
                attacked++;
                bool responded = campaign.Facts.Items.Any(f => f.ActorId == rivalId && f.TargetId == RaceSetup.PlayerId && f.Date >= when
                    && f.Kind is FactKind.Ataque or FactKind.Boato or FactKind.Resposta or FactKind.Justica);
                answered += responded ? 1 : 0;
            }
        }

        Assert.True(attacked >= 30, $"Só {attacked} rivais atacados.");
        Assert.True(answered >= 0.8 * attacked, $"Responderam {answered} de {attacked}.");
    }

    [Theory]
    [InlineData("pequeno", 31UL)]
    [InlineData("grande", 32UL)]
    public void Rival_ultrapassado_no_proprio_reduto_troca_de_plano_em_ate_2_semanas(string porte, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(City(porte), seed);
        CampaignState campaign = engine.Context.State.Campaign!;
        IdleWeek(engine);
        var (rivalId, redutos) = campaign.Territory.Redutos.OrderBy(r => r.Key, StringComparer.Ordinal).First(r => campaign.Find(r.Key)!.Office == campaign.Player.Office);
        int zone = redutos[0];
        RivalRecord record = campaign.Rivals.Record(rivalId)!;
        Assert.NotNull(record.Plan);
        Assert.NotEqual(RivalPlan.Defender, record.Plan!.Strategy);
        DateOnly passedOn = engine.Today;

        // O jogador passa o rival no reduto dele (como se uma sequência de compromissos tivesse rendido ali).
        campaign.Territory.Presence[RaceSetup.PlayerId][zone] = Math.Min(100, campaign.Territory.Presence[rivalId][zone] + 15);
        for (int week = 0; week < 2 && record.Plan!.Strategy != RivalPlan.Defender; week++)
        {
            IdleWeek(engine);
        }

        RivalPlan plan = record.Plan!;
        Assert.Equal(RivalPlan.Defender, plan.Strategy);
        Assert.Equal(RivalPlan.GatilhoReduto, plan.Trigger);
        Assert.True(plan.ChangedOn.DayNumber - passedOn.DayNumber <= 14);
        Assert.Equal(zone, plan.DefendZone);
        Assert.Equal(zone, plan.TargetZones[0]);
        Assert.Equal(Posture.DisputarZona, plan.Postures[RaceSetup.PlayerId]);
        Assert.Contains(campaign.Facts.Items, f => f.Kind == FactKind.Plano && f.ActorId == rivalId && f.Visibility == 0);
    }

    [Fact]
    public void Rivais_tem_perfil_plano_semanal_verba_com_estado_e_o_coletivo_nao_tem_plano()
    {
        CampaignEngine engine = CampaignKit.NewCareer(City("medio"), 41);
        CampaignState campaign = engine.Context.State.Campaign!;
        List<Candidate> named = campaign.Candidates.Where(c => !c.IsPlayer && !c.IsBulk).ToList();
        Assert.Equal(named.Select(c => c.Id).Order(), campaign.Rivals.Records.Select(r => r.CandidateId).Order());
        Assert.All(campaign.Candidates.Where(c => c.IsBulk), c => Assert.Null(campaign.Rivals.Record(c.Id)));
        RivalCatalog catalog = engine.Context.Campaign.Rivals;
        foreach (RivalRecord record in campaign.Rivals.Records)
        {
            RivalProfile profile = catalog.Profile(record.ProfileId);
            RivalPlan plan = record.Plan!;
            Candidate c = campaign.Find(record.CandidateId)!;
            Assert.Equal(engine.Context.State.Calendar.WeekStart, plan.Week);
            Assert.InRange(plan.TargetZones.Count, 1, 3);
            Assert.Equal(Math.Min(profile.ZonasAlvo, campaign.Territory.Zones.Count), plan.TargetZones.Count);
            Assert.NotEmpty(plan.Actions);
            Assert.All(plan.Actions, a => Assert.InRange(a.Day, 0, 6));
            if (c.Office == campaign.Player.Office)
            {
                Assert.True(plan.Postures.ContainsKey(RaceSetup.PlayerId));
                Assert.True(plan.VoteGoal > 0);
                Assert.True(record.Votes > 0);
            }

            // Máquina só para quem está no cargo ou é do partido da prefeitura.
            if (profile.ExigeSituacao)
            {
                Assert.True(c.Incumbent || c.PartyId == engine.GovernmentPartyId);
            }
        }

        // A verba dos rivais tem estado: o que o jogador vê num canal e alvo é a soma do que cada um pôs ali.
        IdleWeek(engine);
        string zone = campaign.Territory.Zones[0];
        foreach (string channel in new[] { ChannelDefinition.Material, ChannelDefinition.Redes })
        {
            string target = channel == ChannelDefinition.Redes ? engine.ChannelTargets(channel)[0].Id : zone;
            decimal expected = campaign.Rivals.Records.Where(r => campaign.Find(r.CandidateId)!.Office == campaign.Player.Office)
                .Sum(r => r.Budget.Where(a => a.ChannelId == channel && a.TargetId == target).Sum(a => a.Amount));
            Assert.Equal(Math.Round(expected), engine.RivalBudget(channel, target));
        }

        Assert.Contains(campaign.Rivals.Records, r => r.Spent > 0);
        Assert.Contains(campaign.Rivals.Records, r => r.Budget.Count > 0);

        // Antes da campanha oficial, nada em Rua nem em Cabos (canais que a pré-campanha não abre).
        Assert.True(engine.BeforeOfficialCampaign);
        Assert.All(campaign.Rivals.Records.SelectMany(r => r.Budget), a => Assert.NotNull(engine.Context.Campaign.Channels.Get(a.ChannelId).PreCampanha));
    }

    [Fact]
    public void Perfil_de_campanha_nao_depende_de_ideologia_nem_de_partido()
    {
        // O sorteio só olha os pesos do perfil e se o rival está na situação: partidos de famílias diferentes têm a
        // mesma distribuição de perfis (fora o de máquina, que exige a situação).
        var byParty = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        for (ulong seed = 1; seed <= 12; seed++)
        {
            CampaignEngine engine = CampaignKit.NewCareer(City("medio"), 500 + seed);
            CampaignState campaign = engine.Context.State.Campaign!;
            foreach (RivalRecord record in campaign.Rivals.Records)
            {
                string party = campaign.Find(record.CandidateId)!.PartyId;
                if (!byParty.TryGetValue(party, out var counts))
                {
                    byParty[party] = counts = new Dictionary<string, int>(StringComparer.Ordinal);
                }

                counts[record.ProfileId] = counts.GetValueOrDefault(record.ProfileId) + 1;
            }
        }

        // Todo partido com bastante gente tem pelo menos três perfis diferentes entre os seus candidatos.
        var big = byParty.Where(p => p.Value.Values.Sum() >= 15).ToList();
        Assert.NotEmpty(big);
        Assert.All(big, p => Assert.True(p.Value.Count >= 3, $"Partido {p.Key} só tem {p.Value.Count} perfis."));
        Assert.All(CampaignKit.C.Campaign.Rivals.Perfis, p => Assert.DoesNotContain("esquerda", p.Descricao + p.Nome, StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("pequeno")]
    [InlineData("medio")]
    [InlineData("grande")]
    [InlineData("metropole")]
    public void Toda_pessoa_nos_papeis_novos_tem_nome_aparencia_interesse_e_lembrancas(string porte)
    {
        CampaignEngine engine = CampaignKit.NewCareer(City(porte), 51);
        GameState state = engine.Context.State;
        CityState city = engine.City;
        PersonRole[] roles = [PersonRole.Rival, PersonRole.Doador, PersonRole.Jornalista, PersonRole.DonoDeVeiculo, PersonRole.Dirigente];
        foreach (PersonRole role in roles)
        {
            Assert.Contains(city.People, p => p.Has(role));
        }

        foreach (Person person in city.People.Where(p => roles.Any(p.Has)))
        {
            Character character = Assert.Single(state.Npcs, n => n.Id == person.Id);
            Assert.False(string.IsNullOrWhiteSpace(character.BallotName));
            Assert.NotNull(character.Appearance);
            Assert.NotEmpty(person.Interests);
            Assert.NotNull(person.Memories);
            Assert.InRange(person.Relation(engine.Today, engine.Context.Campaign.People), Person.MinRelation, Person.MaxRelation);
        }

        // Cada rival nomeado, cada doador, cada jornalista e cada dono de veículo é uma pessoa da cidade.
        Assert.All(state.Campaign!.Candidates.Where(c => !c.IsPlayer && !c.IsBulk), c => Assert.True(city.Person(c.Id)!.Has(PersonRole.Rival)));
        Assert.All(city.Donors, d => Assert.True(city.Person(d.PersonId)!.Has(PersonRole.Doador)));
        Assert.All(city.Outlets, o =>
        {
            Assert.True(city.Person(o.JournalistId)!.Has(PersonRole.Jornalista));
            Assert.True(city.Person(o.OwnerId)!.Has(PersonRole.DonoDeVeiculo));
        });
        IntRange donors = engine.Context.Campaign.Finance.Regras.DoadoresPorPorte[porte];
        Assert.InRange(city.Donors.Count, Math.Max(4, donors.Min), Math.Min(12, donors.Max));

        // Depois de jogar, as lembranças aparecem: um ataque grava uma no rival.
        state.Player.Resources.Money = 100_000;
        Candidate rival = state.Campaign.ForOffice(state.Campaign.Player.Office).First(c => !c.IsPlayer && !c.IsBulk);
        Assert.Null(engine.AttackRival(new AttackRequest(rival.Id, AttackType.Tema, AttackChannel.Redes, Theme: engine.Context.Themes.Themes[0].Id)).Failure);
        Assert.NotEmpty(city.Person(rival.Id)!.Memories);
    }

    [Fact]
    public void Painel_da_chapa_diz_quantas_cadeiras_o_partido_faria_e_a_posicao_do_jogador()
    {
        foreach (string porte in new[] { "pequeno", "metropole" })
        {
            CampaignEngine engine = CampaignKit.NewCareer(City(porte), 61, strongestParty: true);
            CampaignState campaign = engine.Context.State.Campaign!;
            SlateView slate = engine.SlatePanel();
            Assert.Equal(campaign.Player.PartyId, slate.PartyId);
            Assert.InRange(slate.Seats, 0, engine.Context.SeatsIn(campaign.ElectionDate.Year));
            Assert.InRange(slate.Rank, 1, slate.ListSize);
            Assert.Contains(slate.Seats.ToString(System.Globalization.CultureInfo.InvariantCulture), slate.Text);
            Assert.Contains(slate.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture), slate.Text);
            Assert.Single(slate.Top, r => r.IsPlayer);
            Assert.Equal(slate.Top.OrderByDescending(r => r.Votes).Select(r => r.CandidateId), slate.Top.Select(r => r.CandidateId));
            Assert.Equal(Math.Min(slate.Seats, slate.Top.Count), slate.Top.Count(r => r.Seated));
            if (slate.Rank > slate.Seats && slate.Seats > 0)
            {
                Assert.True(slate.VotesToSeat > 0);
            }

            // É só consulta: não mexe no estado.
            string before = CampaignKit.Canonical(engine.Context.State);
            engine.SlatePanel();
            engine.RivalSheetOf(campaign.Rivals.Records[0].CandidateId);
            engine.Accounts();
            engine.Outlets();
            engine.NewsFeed();
            engine.HotThemes();
            engine.PendingAttacks();
            engine.Donors();
            engine.FundQuota();
            engine.FreeAirtime();
            Assert.Equal(before, CampaignKit.Canonical(engine.Context.State));
        }
    }

    [Fact]
    public void Ficha_do_rival_mostra_o_plano_conforme_a_confianca_do_coordenador()
    {
        CampaignEngine engine = CampaignKit.NewCareer(City("grande"), 71);
        CampaignState campaign = engine.Context.State.Campaign!;
        string rivalId = campaign.Territory.Redutos.Keys.Order(StringComparer.Ordinal).First(id => campaign.Find(id)!.Office == campaign.Player.Office);
        RivalSheet sheet = engine.RivalSheetOf(rivalId)!;

        // Sem coordenador: confiança zero; a estimativa são os redutos, sem postura nem perfil.
        Assert.Equal(0, sheet.Confidence);
        Assert.Equal(sheet.Redutos, sheet.EstimatedZones);
        Assert.Null(sheet.EstimatedPosture);
        Assert.Null(sheet.ProfileId);
        Assert.Empty(sheet.Tomorrow);
        Assert.NotEmpty(sheet.Redutos);

        // Com coordenador, a ficha mostra mais conforme a confiança dele: primeiro as zonas-alvo, depois a postura.
        RivalRules rules = engine.Context.Campaign.Rivals.Regras;
        bool sawZones = false;
        bool sawPosture = false;
        for (ulong seed = 71; seed < 95 && !(sawZones && sawPosture); seed++)
        {
            engine = CampaignKit.NewCareer(City("grande"), seed);
            campaign = engine.Context.State.Campaign!;
            rivalId = campaign.Territory.Redutos.Keys.Order(StringComparer.Ordinal).First(id => campaign.Find(id)!.Office == campaign.Player.Office);
            StaffMember best = engine.City.Staff.Where(m => m.Role == StaffRole.Coordenador).OrderByDescending(m => m.Quality).First();
            best.Hired = true;
            best.HiredOn = engine.Today;
            sheet = engine.RivalSheetOf(rivalId)!;
            RivalPlan plan = campaign.Rivals.Record(rivalId)!.Plan!;
            Assert.Equal(engine.RivalReadConfidence, sheet.Confidence);
            Assert.True(sheet.Confidence > 0);
            if (sheet.Confidence >= rules.ConfiancaZonas)
            {
                sawZones = true;
                Assert.Equal(plan.TargetZones.Select(z => campaign.Territory.Zones[z]), sheet.EstimatedZones);
            }
            else
            {
                Assert.Equal(sheet.Redutos, sheet.EstimatedZones);
            }

            if (sheet.Confidence >= rules.ConfiancaPostura)
            {
                sawPosture = true;
                Assert.Equal(plan.Postures[RaceSetup.PlayerId], sheet.EstimatedPosture);
                Assert.Equal(plan.Strategy, sheet.EstimatedStrategy);
                Assert.False(string.IsNullOrWhiteSpace(sheet.StrategyText));
                Assert.NotNull(sheet.ProfileName);
            }
            else
            {
                Assert.Null(sheet.EstimatedPosture);
            }
        }

        Assert.True(sawZones && sawPosture, "Nenhum coordenador bom o bastante nas sementes testadas.");
        Assert.Null(engine.RivalSheetOf("ninguem"));
    }
}
