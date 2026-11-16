using System.Text.Json.Nodes;
using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Municipios;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;
using Planalto.Core.World;

namespace Planalto.Core.Tests.Campaign;

/// <summary>
/// Conteúdo da R2a: eventos religiosos na proporção do Censo, lint de neutralidade (pesos de interesse e chances de
/// base), e o estado novo (território, agenda, cidade, Livro) indo e voltando pelo save.
/// </summary>
public class CityContentTests
{
    private static string Read(string file) => File.ReadAllText(Path.Combine(TestContent.DataDirectory, file));

    private static double Share(Municipio m, string religion) => (double)m.Perfil.Religiao[religion] / m.Perfil.Religiao.Values.Sum();

    /// <summary>Semanas simuladas de eventos da cidade num município; devolve quantas vezes cada tipo saiu.</summary>
    private static Dictionary<string, int> Simulate(CampaignEngine engine, int weeks)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        DateOnly monday = engine.Context.State.Calendar.WeekStart;
        for (int week = 1; week <= weeks; week++)
        {
            engine.Agenda.Rain.Clear();
            engine.Agenda.CityEvents.Clear();
            engine.PlanWeek(monday.AddDays(7 * week));
            foreach (CityEventInstance e in engine.Agenda.CityEvents)
            {
                counts[e.TypeId] = counts.GetValueOrDefault(e.TypeId) + 1;
            }
        }

        return counts;
    }

    [Fact]
    public void Eventos_religiosos_saem_na_proporcao_da_composicao_do_municipio()
    {
        // Os municípios vêm dos dados: o de maior fatia católica e o de maior fatia evangélica entre os de porte
        // médio ou maior (onde o Censo tem contagem bastante para a fatia não ser acidente).
        var pool = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte != "pequeno").ToList();
        Municipio catholic = pool.MaxBy(m => Share(m, "catolica"))!;
        Municipio evangelical = pool.MaxBy(m => Share(m, "evangelica"))!;
        Assert.True(Share(catholic, "catolica") > 0.5, "Esperava um município de maioria católica.");
        Assert.True(Share(evangelical, "evangelica") > Share(evangelical, "catolica"), "Esperava um município com mais evangélicos que católicos.");

        CityEventCatalog catalog = CampaignKit.C.Campaign.CityEvents;
        const int Weeks = 4000;
        foreach (Municipio m in new[] { catholic, evangelical })
        {
            CampaignEngine engine = CampaignKit.NewCareer(m.CodigoIbge, 91);
            Dictionary<string, int> counts = Simulate(engine, Weeks);
            double total = engine.Context.Electorate.Voters;
            var share = new Dictionary<string, double>(StringComparer.Ordinal);
            var frequency = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (CityEventType type in catalog.Tipos.Where(t => t.Religiao is not null))
            {
                share[type.Religiao!] = engine.Context.Electorate.Segments.Where(s => s.Has(type.Religiao!)).Sum(s => s.Voters) / total;
                frequency[type.Religiao!] = counts.GetValueOrDefault(type.Id) / (double)Weeks;

                // Frequência de cada religião = chance de base × fatia entre os eleitores × escala (4 desvios de folga).
                double expected = Math.Min(1, type.ChanceSemanal * share[type.Religiao!] * catalog.ReligiosoEscala);
                double sd = Math.Sqrt(expected * (1 - expected) / Weeks);
                Assert.InRange(frequency[type.Religiao!], expected - (4 * sd) - 1e-9, expected + (4 * sd) + 1e-9);
            }

            // A ordem das frequências é a da composição, e a razão entre católicos e evangélicos também.
            Assert.Equal(share.OrderByDescending(s => s.Value).Select(s => s.Key), frequency.OrderByDescending(f => f.Value).Select(f => f.Key));
            double ratio = frequency["catolica"] / frequency["evangelica"];
            double census = share["catolica"] / share["evangelica"];
            Assert.InRange(ratio, census * 0.8, census * 1.25);
        }

        // No município católico as festas da paróquia são mais frequentes; no outro, os encontros evangélicos.
        Dictionary<string, int> a = Simulate(CampaignKit.NewCareer(catholic.CodigoIbge, 92), 1500);
        Dictionary<string, int> b = Simulate(CampaignKit.NewCareer(evangelical.CodigoIbge, 92), 1500);
        Assert.True(a["celebracao_catolica"] > a.GetValueOrDefault("celebracao_evangelica"));
        Assert.True(b["celebracao_evangelica"] > b.GetValueOrDefault("celebracao_catolica"));
        Assert.True(a["celebracao_catolica"] > b.GetValueOrDefault("celebracao_catolica"));
        Assert.True(b["celebracao_evangelica"] > a.GetValueOrDefault("celebracao_evangelica"));
    }

    [Fact]
    public void Eventos_da_cidade_respeitam_porte_mes_dia_turno_e_situacao()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3106200, 93);
        CityEventCatalog catalog = CampaignKit.C.Campaign.CityEvents;
        DateOnly monday = engine.Context.State.Calendar.WeekStart;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int week = 1; week <= 400; week++)
        {
            engine.Agenda.Rain.Clear();
            engine.Agenda.CityEvents.Clear();
            engine.PlanWeek(monday.AddDays(7 * week));
            foreach (CityEventInstance e in engine.Agenda.CityEvents)
            {
                CityEventType type = catalog.Tipos.Single(t => t.Id == e.TypeId);
                Assert.Contains(engine.Context.Municipio.Porte, type.Portes);
                Assert.True(type.Meses.Count == 0 || type.Meses.Contains(e.Date.Month));
                Assert.Equal(type.DiaSemana, ((int)e.Date.DayOfWeek + 6) % 7);
                Assert.Equal(type.Turno, e.Turno);
                Assert.Equal(type.Publico, e.Publico);
                var zone = engine.Context.ZoneElectorates.Single(z => z.Zona.Id == e.ZoneId);
                Assert.True(type.Situacao == "qualquer" || zone.Zona.Situacao == type.Situacao);
                Assert.True(e.Publico == CommitmentType.PublicoGeral || zone.VotersIn(e.Publico) > 0);
                seen.Add(e.TypeId);
            }
        }

        Assert.True(seen.Count >= 6, $"Só {seen.Count} tipos de evento em 400 semanas.");
    }

    [Fact]
    public void Lint_chances_de_base_iguais_entre_as_religioes()
    {
        CityEventCatalog catalog = CampaignKit.C.Campaign.CityEvents;
        Assert.Empty(catalog.Validate());
        var byReligion = catalog.Tipos.Where(t => t.Religiao is not null).GroupBy(t => t.Religiao!).ToDictionary(g => g.Key, g => g.Sum(t => t.ChanceSemanal));
        Assert.True(byReligion.Count >= 3, "Faltam eventos de alguma religião.");
        Assert.All(byReligion.Values, v => Assert.Equal(byReligion.Values.First(), v, 9));

        // Toda religião declarada é um grupo dos segmentos (a fatia vem do Censo do município).
        var religions = CampaignKit.C.Electorate.Segments.Dimensions.Single(d => d.Id == "religiao").Groups.Select(g => g.Id).ToList();
        Assert.All(byReligion.Keys, r => Assert.Contains(r, religions));

        // Um catálogo que favorece uma religião reprova.
        JsonNode json = JsonNode.Parse(Read(CityEventCatalog.File))!;
        JsonNode biased = json["tipos"]!.AsArray().First(t => t!["religiao"]?.GetValue<string>() == "catolica")!;
        biased["chance_semanal"] = biased["chance_semanal"]!.GetValue<double>() + 0.2;
        DataLoadResult<CityEventCatalog> result = JsonData.Parse<CityEventCatalog>(json.ToJsonString(), "teste");
        Assert.Contains(result.Errors, e => e.Contains("religi", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Lint_pesos_de_interesse_por_tipo_de_lideranca_dentro_de_dois_para_um()
    {
        LeaderCatalog catalog = CampaignKit.C.Campaign.Leaders;
        Assert.Empty(catalog.Validate());
        var themes = CampaignKit.C.World.Parties.Themes.Themes.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);
        var groups = CampaignKit.C.Electorate.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(catalog.Tipos, t =>
        {
            Assert.True(t.Interesses.Values.Max() / t.Interesses.Values.Min() <= LeaderCatalog.MaxInterestRatio + 1e-9, t.Id);

            // Qualquer interesse pode ocorrer em qualquer tipo: todo tema do jogo aparece, com peso positivo.
            Assert.Equal(themes.Order(), t.Interesses.Keys.Order());
            Assert.All(t.Publicos.Keys, g => Assert.Contains(g, groups));
        });

        // Liderança religiosa: um público por religião dos segmentos (o sorteio segue o Censo do município).
        var religions = CampaignKit.C.Electorate.Segments.Dimensions.Single(d => d.Id == "religiao").Groups.Select(g => g.Id).ToList();
        Assert.All(catalog.Type(LeaderType.Religiosa).Publicos.Keys, r => Assert.Contains(r, religions));
        Assert.Equal(CampaignKit.C.Campaign.CityEvents.Tipos.Where(t => t.Religiao is not null).Select(t => t.Religiao!).Distinct().Order(), catalog.Type(LeaderType.Religiosa).Publicos.Keys.Order());

        // Um catálogo com um interesse pesando 3 para 1 reprova; exatamente 2 para 1 passa.
        JsonNode json = JsonNode.Parse(Read(LeaderCatalog.File))!;
        JsonObject interests = json["tipos"]![0]!["interesses"]!.AsObject();
        string heavy = interests.First().Key;
        double min = interests.Min(i => i.Value!.GetValue<double>());
        interests[heavy] = min * 3;
        Assert.Contains(JsonData.Parse<LeaderCatalog>(json.ToJsonString(), "teste").Errors, e => e.Contains("pesos de interesse", StringComparison.Ordinal));
        interests[heavy] = min * 2;
        Assert.Empty(JsonData.Parse<LeaderCatalog>(json.ToJsonString(), "teste").Errors);
        interests[heavy] = 0;
        Assert.NotEmpty(JsonData.Parse<LeaderCatalog>(json.ToJsonString(), "teste").Errors);
    }

    [Fact]
    public void Compromissos_e_pedidos_apontam_para_conteudo_que_existe()
    {
        CommitmentCatalog commitments = CampaignKit.C.Campaign.Commitments;
        Assert.Empty(commitments.Validate());
        var groups = CampaignKit.C.Electorate.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(commitments.Tipos.Where(t => t.PublicoFixo is not null), t => Assert.Contains(t.PublicoFixo!, groups));
        Assert.All(commitments.Tipos.Where(t => t.EscolhePublico || t.SoEvento), t => Assert.True(t.ExigeZona, t.Id));
        Assert.Contains(commitments.Tipos, t => t.Id == "caminhada" && t.Rua && t.ExigeZona);
        Assert.All(CampaignKit.C.Campaign.CityEvents.Tipos, t => Assert.True(t.Publico == CommitmentType.PublicoGeral || groups.Contains(t.Publico), t.Id));
        Assert.All(CampaignKit.C.Campaign.CityEvents.Tipos.Where(t => t.Religiao is not null), t => Assert.Contains(t.Religiao!, groups));

        // Os arquivos novos da R2a entram no relatório do DataValidator, sem erro e sem repetição.
        IReadOnlyList<DataFileReport> reports = DataValidation.ValidateAll(TestContent.DataDirectory);
        foreach (string file in new[] { CommitmentCatalog.File, LeaderCatalog.File, CityEventCatalog.File, Planalto.Core.People.PeopleConfig.File, Planalto.Core.Territory.TerritoryConfig.File, CampaignContent.ConfigFile })
        {
            DataFileReport report = Assert.Single(reports, r => r.File == file);
            Assert.True(report.IsValid, string.Join("; ", report.Errors));
            Assert.True(report.ItemCount > 0);
        }
    }

    [Fact]
    public void Estado_novo_vai_e_volta_igual_pelo_save()
    {
        // Meio de campanha com tudo o que a R2a acrescentou: compromissos marcados, convite, evento da cidade,
        // chuva por data, cansaço, feed, pedido esperando resposta, promessa, dívida e esqueleto no Livro.
        CampaignEngine engine = CampaignKit.NewCareer(3106200, 94);
        GameState state = engine.Context.State;
        var robot = new CampaignRobot(engine, 94, new RobotOptions { Answer = LeaderAnswer.Aceitar });
        for (int week = 0; week < 4; week++)
        {
            robot.PlayWeek();
        }

        foreach (Leader l in engine.City.Leaders)
        {
            l.Known = true;
        }

        state.Player.Resources.Money = 1_000_000;
        DateOnly today = engine.Today;
        Leader open = engine.City.Leaders.First(l => !l.RequestMet);
        Leader other = engine.City.Leaders.Last(l => !l.RequestMet);
        Assert.NotSame(open, other);

        // De manhã, visita com negociação (dívida no Livro); à tarde, caminhada; à noite, visita que deixa o pedido esperando.
        Assert.Null(engine.Schedule(new CommitmentRequest(CommitmentCatalog.Visita, null, today, Turno.Manha, PersonId: other.PersonId)).Failure);
        engine.RunToday();
        state.Player.Attributes.Set(Planalto.Core.Characters.AttributeId.Articulacao, 100);
        if (engine.AnswerLeader(LeaderAnswer.Negociar).Fact != "negociacao")
        {
            state.Ledger.AddDebt(other.PersonId, "Dívida de teste", today);
        }

        state.Ledger.AddSkeleton("teste", "Esqueleto de teste", 2, today, [open.PersonId]);
        state.Ledger.Positions.Add(new Planalto.Core.People.StatedPosition("saude", 40, today, "entrevista"));
        engine.Agenda.Invites.Add(new Invite { Id = "cv_teste", FromPersonId = open.PersonId, Text = "Convite", ZoneId = open.Zones[0], Date = today.AddDays(1), Turno = Turno.Noite, Publico = open.Publico });
        engine.Agenda.Invites.Add(new Invite { Id = "cv_teste2", FromPersonId = other.PersonId, Text = "Convite", ZoneId = other.Zones[0], Date = today.AddDays(2), Turno = Turno.Tarde, Publico = other.Publico });
        Assert.Null(engine.AcceptInvite("cv_teste").Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", engine.Territory.Zones[0], today, Turno.Tarde)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("entrevista", null, today.AddDays(1), Turno.Manha)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", engine.Territory.Zones[0], today.AddDays(8), Turno.Tarde)).Failure);
        Assert.Null(engine.Schedule(new CommitmentRequest(CommitmentCatalog.Visita, null, today, Turno.Noite, PersonId: open.PersonId)).Failure);
        engine.RunToday();

        Assert.Equal(open.PersonId, engine.Agenda.PendingRequest);
        Assert.NotEmpty(engine.Agenda.Scheduled);
        Assert.NotEmpty(engine.Agenda.Fatigue);
        Assert.NotEmpty(engine.Agenda.Log);
        Assert.NotEmpty(engine.Agenda.Feed);
        Assert.Equal(7, engine.Agenda.Rain.Count);
        Assert.NotEmpty(state.Ledger.Promises);
        Assert.NotEmpty(state.Ledger.Debts);

        string json = GameStateSerializer.Serialize(state);
        GameState loaded = GameStateSerializer.Deserialize(json);

        // A ordem das chaves dos dicionários pode mudar entre gravar e carregar; o conteúdo, não.
        Assert.Equal(CampaignKit.Canonical(state), CampaignKit.Canonical(loaded));

        // Campo a campo, o que é novo: território, agenda, cidade e Livro.
        CampaignState a = state.Campaign!;
        CampaignState b = loaded.Campaign!;
        Assert.Equal(a.Territory.Zones, b.Territory.Zones);
        Assert.Equal(a.Territory.Population, b.Territory.Population);
        Assert.Equal(a.Territory.HomeZone, b.Territory.HomeZone);
        Assert.Equal(a.Territory.Presence.Keys.Order(), b.Territory.Presence.Keys.Order());
        Assert.All(a.Territory.Presence, p => Assert.Equal(p.Value, b.Territory.Presence[p.Key]));
        Assert.All(a.Territory.Redutos, r => Assert.Equal(r.Value, b.Territory.Redutos[r.Key]));
        Assert.Equal(a.Territory.ActiveThisWeek[RaceSetup.PlayerId], b.Territory.ActiveThisWeek[RaceSetup.PlayerId]);
        for (int z = 0; z < a.Territory.Zones.Count; z++)
        {
            Assert.Equal(a.Territory.MeetingSupport[z], b.Territory.MeetingSupport[z]);
            Assert.Equal(a.Territory.Support[RaceSetup.PlayerId][z], b.Territory.Support[RaceSetup.PlayerId][z]);
        }

        Assert.Equal(a.Agenda.Rain, b.Agenda.Rain);
        Assert.Equal(a.Agenda.Fatigue, b.Agenda.Fatigue);
        Assert.Equal(a.Agenda.PendingRequest, b.Agenda.PendingRequest);
        Assert.Equal(a.Agenda.NextId, b.Agenda.NextId);
        Assert.Equal(a.Agenda.Scheduled.Select(c => (c.Id, c.Type, c.ZoneId, c.Date, c.Turno, c.Publico, c.PersonId, c.CityEventId, c.InviteId)), b.Agenda.Scheduled.Select(c => (c.Id, c.Type, c.ZoneId, c.Date, c.Turno, c.Publico, c.PersonId, c.CityEventId, c.InviteId)));
        Assert.Equal(a.Agenda.CityEvents.Select(e => (e.Id, e.TypeId, e.ZoneId, e.Date, e.Turno, e.Publico)), b.Agenda.CityEvents.Select(e => (e.Id, e.TypeId, e.ZoneId, e.Date, e.Turno, e.Publico)));
        Assert.Equal(a.Agenda.Invites.Select(i => (i.Id, i.FromPersonId, i.Date, i.Turno, i.Answered)), b.Agenda.Invites.Select(i => (i.Id, i.FromPersonId, i.Date, i.Turno, i.Answered)));
        Assert.Equal(a.Agenda.Feed, b.Agenda.Feed);
        Assert.Equal(a.Agenda.Log.Select(r => (r.Commitment.Id, r.Support, r.Image, r.Money, r.Stress, r.Text, r.Factors.Count)), b.Agenda.Log.Select(r => (r.Commitment.Id, r.Support, r.Image, r.Money, r.Stress, r.Text, r.Factors.Count)));

        Assert.Equal(state.City!.Leaders.Select(l => (l.PersonId, l.Type, l.Publico, l.Influence, l.AlignedWith, l.Known, l.RequestKnown, l.RequestMet, l.PromiseId, l.Price)), loaded.City!.Leaders.Select(l => (l.PersonId, l.Type, l.Publico, l.Influence, l.AlignedWith, l.Known, l.RequestKnown, l.RequestMet, l.PromiseId, l.Price)));
        Assert.Equal(state.City.People.Select(p => (p.Id, p.Affinity, p.Temperament, p.LastContact, p.Memories.Count)), loaded.City.People.Select(p => (p.Id, p.Affinity, p.Temperament, p.LastContact, p.Memories.Count)));
        Assert.Equal(state.City.People.SelectMany(p => p.Memories).Select(m => (m.Date, m.Fact, m.Peso, m.Pontos, m.Text)), loaded.City.People.SelectMany(p => p.Memories).Select(m => (m.Date, m.Fact, m.Peso, m.Pontos, m.Text)));
        Assert.Equal(state.Ledger.Promises.Select(p => (p.Id, p.ToKind, p.ToId, p.Object, p.Text, p.ZoneId, p.Made, p.Deadline, p.State)), loaded.Ledger.Promises.Select(p => (p.Id, p.ToKind, p.ToId, p.Object, p.Text, p.ZoneId, p.Made, p.Deadline, p.State)));
        Assert.Equal(state.Ledger.Debts.Select(d => (d.Id, d.ToPersonId, d.Text, d.Made, d.Settled)), loaded.Ledger.Debts.Select(d => (d.Id, d.ToPersonId, d.Text, d.Made, d.Settled)));
        Assert.Equal(state.Ledger.Skeletons.Select(s => (s.Id, s.Type, s.Severity, string.Join(",", s.KnownBy))), loaded.Ledger.Skeletons.Select(s => (s.Id, s.Type, s.Severity, string.Join(",", s.KnownBy))));
        Assert.Equal(state.Ledger.Positions, loaded.Ledger.Positions);
        Assert.Equal(state.Ledger.NextId, loaded.Ledger.NextId);
        Assert.Equal(state.Time.Day, loaded.Time.Day);

        // O jogo carregado segue igual ao que não foi salvo: mesma resposta, mesmos dias, mesmo estado.
        CampaignEngine resumed = CampaignEngine.Start(CampaignKit.Context(loaded));

        // A ordem das chaves dos dicionários pode mudar entre gravar e carregar; o conteúdo, não.
        Assert.Equal(CampaignKit.Canonical(state), CampaignKit.Canonical(loaded));
        foreach (CampaignEngine e in new[] { engine, resumed })
        {
            e.AnswerLeader(LeaderAnswer.Aceitar);
            for (int day = 0; day < 3; day++)
            {
                e.AdvanceDay();
            }
        }

        Assert.Equal(CampaignKit.Canonical(state), CampaignKit.Canonical(loaded));
    }

    /// <summary>Um município de cada porte, três sementes: campanha do começo à apuração trocando o motor pelo do save no meio.</summary>
    public static TheoryData<int, ulong> PorPorte()
    {
        var all = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais).OrderBy(m => m.CodigoIbge).ToList();
        var data = new TheoryData<int, ulong>();
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            var pool = all.Where(m => m.Porte == porte).ToList();
            foreach (ulong seed in new ulong[] { 101, 202, 303 })
            {
                data.Add(pool[(int)(seed % (ulong)pool.Count)].CodigoIbge, seed);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PorPorte))]
    public void Campanha_inteira_com_save_e_load_no_meio_termina_igual_a_sem_salvar(int ibge, ulong seed)
    {
        CampaignEngine straight = CampaignKit.NewCareer(ibge, seed);
        var robotA = new CampaignRobot(straight, seed);
        robotA.PlayToElection(_ => CampaignAutoplayTests.AssertState(straight));
        robotA.Vote();

        CampaignEngine saved = CampaignKit.NewCareer(ibge, seed);
        var robotB = new CampaignRobot(saved, seed);
        int loads = 0;
        robotB.PlayToElection(week =>
        {
            if (week % 9 == 5)
            {
                // Salva, carrega e segue no estado carregado, com um motor novo.
                GameState loaded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(robotB.Engine.Context.State));
                robotB.Engine = CampaignEngine.Start(CampaignKit.Context(loaded));
                loads++;
            }
        });
        robotB.Vote();

        Assert.True(loads >= 3, $"Só {loads} cargas no meio da campanha.");
        Assert.NotSame(saved, robotB.Engine);
        Assert.Equal(CampaignPhase.Encerrada, robotB.Engine.Context.State.Campaign!.Phase);
        Assert.Equal(CampaignKit.Canonical(straight.Context.State), CampaignKit.Canonical(robotB.Engine.Context.State));
        Assert.Equal(robotA.Results.Count, robotB.Results.Count);
        Assert.True(robotA.Results.Count > 100, "A campanha automática quase não marcou compromissos.");

        // Toda a variedade da agenda apareceu, e todo resultado veio explicado.
        Assert.True(robotA.Results.Select(r => r.Commitment.Type).Distinct().Count() >= 6);
        Assert.All(robotA.Results, r => Assert.NotEmpty(r.Factors));
    }
}
