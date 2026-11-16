using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Characters;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Rede de lideranças (GDD 17.10), pessoa com memória e Livro de compromissos (17.2).</summary>
public class LeaderTests
{
    public static TheoryData<int, ulong> Cities()
    {
        var all = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais).OrderBy(m => m.CodigoIbge).ToList();
        var data = new TheoryData<int, ulong>();
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            var pool = all.Where(m => m.Porte == porte).ToList();
            foreach (ulong seed in new ulong[] { 81, 82, 83 })
            {
                data.Add(pool[(int)(seed % (ulong)pool.Count)].CodigoIbge, seed);
            }
        }

        return data;
    }

    private static CampaignEngine Fresh(int ibge, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed);
        engine.Context.State.Campaign!.Events.Pending.Clear();
        engine.Context.State.Player.Resources.Money = 1_000_000;
        foreach (Leader l in engine.City.Leaders)
        {
            l.Known = true;
        }

        return engine;
    }

    /// <summary>Visita hoje, no primeiro turno livre; devolve o resultado (o pedido fica esperando resposta).</summary>
    private static CommitmentResult Visit(CampaignEngine engine, Leader leader)
    {
        foreach (Turno turno in Enum.GetValues<Turno>())
        {
            if (engine.Schedule(new CommitmentRequest(CommitmentCatalog.Visita, null, engine.Today, turno, PersonId: leader.PersonId)).Failure is null)
            {
                return Assert.Single(engine.RunToday());
            }
        }

        // Os três turnos de hoje já foram usados: a visita fica para amanhã cedo.
        Assert.Empty(engine.AdvanceDay());
        engine.Context.State.Campaign!.Events.Pending.Clear();
        engine.Context.State.Campaign.Debate = null;
        Assert.Null(engine.Schedule(new CommitmentRequest(CommitmentCatalog.Visita, null, engine.Today, Turno.Manha, PersonId: leader.PersonId)).Failure);
        return Assert.Single(engine.RunToday());
    }

    [Theory]
    [MemberData(nameof(Cities))]
    public void Toda_lideranca_tem_nome_aparencia_interesse_e_lembrancas_e_a_quantidade_segue_o_porte(int ibge, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed);
        CampaignContext ctx = engine.Context;
        GameState state = ctx.State;
        CityState city = state.City!;
        LeaderCatalog catalog = ctx.Campaign.Leaders;

        IntRange range = catalog.Regras.QuantidadePorPorte[ctx.Municipio.Porte];
        Assert.InRange(city.Leaders.Count, range.Min, range.Max);
        Assert.Equal(ctx.Municipio.CodigoIbge, city.Municipio);
        Assert.Equal(city.Leaders.Count, city.Leaders.Select(l => l.PersonId).Distinct().Count());
        Assert.Equal(city.Leaders.Count, city.People.Count(p => p.Has(PersonRole.Lideranca)));

        Assert.All(city.Leaders, leader =>
        {
            Character npc = Assert.Single(state.Npcs, n => n.Id == leader.PersonId);
            Assert.False(string.IsNullOrWhiteSpace(npc.BallotName));
            Assert.False(string.IsNullOrWhiteSpace(npc.CivilName));
            Assert.NotNull(npc.Appearance);
            Assert.Equal(npc.BallotName, engine.NameOf(leader.PersonId));

            Person person = city.Person(leader.PersonId)!;
            Assert.NotEmpty(person.Interests);
            Assert.InRange(person.Interests.Count, 1, 3);
            Assert.NotNull(person.Memories);
            Assert.Contains(person.Interests, i => i.Kind == Interest.Tema && i.Target == leader.Theme);
            Assert.Contains(ctx.Themes.Themes, t => t.Id == leader.Theme);

            LeaderType type = catalog.Type(leader.Type);
            Assert.InRange(leader.Zones.Count, 1, 2);
            Assert.All(leader.Zones, z => Assert.Contains(z, engine.Territory.Zones));
            Assert.True(type.Situacao == "qualquer" || ctx.Zonas.Zonas.Single(z => z.Id == leader.Zones[0]).Situacao == type.Situacao);
            Assert.Contains(leader.Publico, type.Publicos.Keys);
            Assert.InRange(leader.Influence, 1, 5);
            Assert.Contains(leader.RequestId, type.Pedidos);
            Assert.False(string.IsNullOrWhiteSpace(leader.RequestText));
            Assert.DoesNotContain("{", leader.RequestText);
            Assert.True(leader.Price > 0);
            Assert.All(leader.Rivals, r => Assert.Contains(city.Leaders, o => o.PersonId == r && o.Publico == leader.Publico && o.Zones.Intersect(leader.Zones).Any()));
            Assert.True(leader.AlignedWith is null || state.Campaign!.Find(leader.AlignedWith) is not null);
        });

        // O jogador começa conhecendo só uma parte delas.
        int known = city.Leaders.Count(l => l.Known);
        Assert.Equal((int)Math.Ceiling(city.Leaders.Count * catalog.Regras.ConhecidasNoInicio), known);
        Assert.True(known < city.Leaders.Count);
    }

    [Fact]
    public void Visita_promessa_negociacao_e_recusa_gravam_lembranca()
    {
        CampaignEngine engine = Fresh(3550308, 84);
        GameState state = engine.Context.State;
        LeaderRules rules = engine.Context.Campaign.Leaders.Regras;
        var facts = new HashSet<string>(StringComparer.Ordinal);
        int index = 0;

        Person Answered(LeaderAnswer answer, int articulacao, out Leader leader, out Memory memory)
        {
            leader = engine.City.Leaders[index++];
            state.Player.Attributes.Set(AttributeId.Articulacao, articulacao);
            Person person = engine.City.Person(leader.PersonId)!;
            Assert.Empty(person.Memories);
            double before = engine.RelationOf(leader.PersonId);

            // A visita grava a própria lembrança, revela o pedido e deixa a decisão esperando: o tempo não anda.
            CommitmentResult result = Visit(engine, leader);
            Assert.Equal(rules.PontosVisita, result.RelationPoints);
            Memory visit = Assert.Single(person.Memories);
            Assert.Equal("visita", visit.Fact);
            Assert.Equal(engine.Today, visit.Date);
            Assert.Equal(engine.Today, person.LastContact);
            Assert.True(leader.RequestKnown);
            Assert.Equal(leader.PersonId, engine.Agenda.PendingRequest);
            Assert.True(engine.DecisionPending);
            Assert.Throws<InvalidOperationException>(() => engine.AdvanceDay());
            Assert.Equal(ScheduleFailure.DecisaoPendente, engine.Schedule(new CommitmentRequest("descanso", null, engine.Today.AddDays(1), Turno.Noite)).Failure);

            memory = engine.AnswerLeader(answer);
            Assert.Null(engine.Agenda.PendingRequest);
            Assert.Equal(2, person.Memories.Count);
            Assert.Same(memory, person.Memories[^1]);
            Assert.False(string.IsNullOrWhiteSpace(memory.Text));
            Assert.DoesNotContain("{", memory.Text);
            Assert.Equal(before + visit.Pontos + memory.Pontos, engine.RelationOf(leader.PersonId), 6);
            facts.Add(memory.Fact);
            return person;
        }

        // Promessa aceita: lembrança que não se apaga e relação que sobe.
        Answered(LeaderAnswer.Aceitar, 50, out Leader accepted, out Memory promise);
        Assert.Equal("promessa", promise.Fact);
        Assert.Equal(3, promise.Peso);
        Assert.Equal(rules.PontosPromessa, promise.Pontos);
        Assert.True(accepted.RequestMet);

        // Recusa: lembrança negativa.
        Answered(LeaderAnswer.Recusar, 50, out Leader refused, out Memory refusal);
        Assert.Equal("recusa", refusal.Fact);
        Assert.True(refusal.Pontos < 0 && refusal.Peso < 0);
        Assert.False(refused.RequestMet);

        // Não se comprometer: a porta fica aberta.
        Answered(LeaderAnswer.NaoSeComprometer, 50, out Leader open, out Memory noCommitment);
        Assert.Equal("visita_sem_compromisso", noCommitment.Fact);
        Assert.False(open.RequestMet);

        // Negociação: com Articulação alta dá certo (vira dívida no Livro); com baixa, falha. As duas ficam na memória.
        int debts = state.Ledger.Debts.Count;
        while (!(facts.Contains("negociacao") && facts.Contains("negociacao_falha")))
        {
            Assert.True(index < engine.City.Leaders.Count, "Faltou liderança para ver os dois desfechos da negociação.");
            bool wantSuccess = !facts.Contains("negociacao");
            Answered(LeaderAnswer.Negociar, wantSuccess ? 100 : 0, out Leader leader, out Memory memory);
            if (memory.Fact == "negociacao")
            {
                Assert.True(memory.Pontos > 0);
                Assert.True(leader.RequestMet);
                Assert.Equal(leader.PersonId, state.Ledger.Debts[^1].ToPersonId);
                Assert.False(string.IsNullOrWhiteSpace(state.Ledger.Debts[^1].Text));
            }
            else
            {
                Assert.Equal("negociacao_falha", memory.Fact);
                Assert.True(memory.Pontos < 0);
                Assert.False(leader.RequestMet);
            }
        }

        Assert.True(state.Ledger.Debts.Count > debts);
        Assert.Equal(0.95, Chance(engine, 100), 9);
        Assert.Equal(0.05, Chance(engine, 0), 9);
        Assert.Superset(new HashSet<string> { "promessa", "recusa", "visita_sem_compromisso", "negociacao", "negociacao_falha" }, facts);
    }

    private static double Chance(CampaignEngine engine, int articulacao)
    {
        engine.Context.State.Player.Attributes.Set(AttributeId.Articulacao, articulacao);
        return engine.NegotiationChance;
    }

    [Fact]
    public void Lideranca_sem_contato_por_oito_semanas_perde_relacao()
    {
        CampaignEngine engine = Fresh(3106200, 85);
        LeaderRules rules = engine.Context.Campaign.Leaders.Regras;
        Leader leader = engine.City.Leaders[0];
        Leader stranger = engine.City.Leaders[1];
        Person person = engine.City.Person(leader.PersonId)!;
        Person other = engine.City.Person(stranger.PersonId)!;
        stranger.Known = false;
        Visit(engine, leader);
        engine.AnswerLeader(LeaderAnswer.NaoSeComprometer);
        double affinity = person.Affinity;
        double otherAffinity = other.Affinity;
        Assert.Equal(8, rules.SemanasSemContato);

        // Até completar as 8 semanas nada muda na afinidade.
        for (int week = 0; week < rules.SemanasSemContato; week++)
        {
            engine.AdvanceWeek();
            engine.Context.State.Campaign!.Events.Pending.Clear();
            Assert.Equal(affinity, person.Affinity);
        }

        // Daí em diante, 1 ponto por semana; a relação cai junto.
        double relation = engine.RelationOf(leader.PersonId);
        for (int week = 1; week <= 3; week++)
        {
            engine.AdvanceWeek();
            engine.Context.State.Campaign!.Events.Pending.Clear();
            Assert.Equal(affinity - week, person.Affinity);
        }

        Assert.True(engine.RelationOf(leader.PersonId) <= relation - 3 + 0.2);

        // Um novo contato zera a contagem; quem o jogador nunca procurou não tem o que esfriar.
        Visit(engine, leader);
        engine.AnswerLeader(LeaderAnswer.NaoSeComprometer);
        double after = person.Affinity;
        engine.AdvanceWeek();
        Assert.Equal(after, person.Affinity);
        Assert.Equal(otherAffinity, other.Affinity);
        Assert.Empty(other.Memories);
    }

    [Fact]
    public void Lideranca_fecha_com_o_jogador_da_apoio_na_zona_e_esfria_as_rivais()
    {
        CampaignEngine engine = Fresh(3550308, 86);
        LeaderRules rules = engine.Context.Campaign.Leaders.Regras;
        PeopleConfig people = engine.Context.Campaign.People;
        Leader leader = engine.City.Leaders.First(l => l.Rivals.Count > 0 && engine.Context.Campaign.Leaders.Request(l.RequestId).Kind != LeaderRequestDefinition.Dinheiro);
        leader.Favor.Clear();
        leader.AlignedWith = null;
        Person person = engine.City.Person(leader.PersonId)!;
        person.Affinity = leader.Price;
        var rivalsBefore = leader.Rivals.ToDictionary(r => r, engine.RelationOf);
        int z = engine.Territory.IndexOf(leader.Zones[0]);
        double supportBefore = engine.Territory.Support.GetValueOrDefault(RaceSetup.PlayerId)?[z].GetValueOrDefault(leader.Publico) ?? 0;

        // Só a relação não basta: falta atender o pedido.
        Visit(engine, leader);
        Assert.Null(leader.AlignedWith);
        engine.AnswerLeader(LeaderAnswer.Aceitar);

        Assert.Equal(RaceSetup.PlayerId, leader.AlignedWith);
        double expected = leader.Influence * Math.Clamp(person.Relation(engine.Today, people), 0, 100) / 100 * rules.ApoioPorInfluencia;
        Assert.Equal(Math.Min(1, supportBefore + expected), engine.Territory.Support[RaceSetup.PlayerId][z][leader.Publico], 9);
        Assert.Contains(engine.Agenda.Feed, f => f.Kind == "lideranca" && f.PersonId == leader.PersonId && f.CandidateId == RaceSetup.PlayerId);

        // Quem disputa o mesmo público na mesma zona esfria e lembra por quê.
        Assert.All(leader.Rivals, r =>
        {
            Assert.Equal(rivalsBefore[r] + rules.PontosRivalidade, engine.RelationOf(r), 6);
            Assert.Contains(engine.City.Person(r)!.Memories, m => m.Fact == "rivalidade" && m.Text.Contains(engine.NameOf(leader.PersonId), StringComparison.Ordinal));
        });

        // Com a liderança aliada junto, o compromisso de rua na zona dela rende mais, e a previsão diz.
        CommitmentForecast together = engine.Forecast(new CommitmentRequest("caminhada", leader.Zones[0], engine.Today, Turno.Noite, PersonId: leader.PersonId));
        Assert.Equal(1 + engine.Context.Campaign.Commitments.Regras.LiderancaBonus, Assert.Single(together.Factors, f => f.Id == "lideranca").Multiplier, 9);
        Assert.DoesNotContain(engine.Forecast(new CommitmentRequest("caminhada", leader.Zones[0], engine.Today, Turno.Noite)).Factors, f => f.Id == "lideranca");
    }

    [Fact]
    public void Pedido_de_dinheiro_vira_esqueleto_que_a_lideranca_conhece()
    {
        CampaignEngine engine = Fresh(3550308, 87);
        GameState state = engine.Context.State;
        Leader leader = engine.City.Leaders.First(l => engine.Context.Campaign.Leaders.Request(l.RequestId).Kind == LeaderRequestDefinition.Dinheiro);
        Visit(engine, leader);
        decimal money = state.Player.Resources.Money;
        engine.AnswerLeader(LeaderAnswer.Aceitar);

        Assert.True(state.Player.Resources.Money < money);
        LedgerSkeleton skeleton = Assert.Single(state.Ledger.Skeletons);
        Assert.Equal([leader.PersonId], skeleton.KnownBy);
        Assert.Equal(engine.Context.Campaign.Leaders.Regras.GravidadeDinheiro, skeleton.Severity);
        Assert.False(string.IsNullOrWhiteSpace(skeleton.Text));
        Assert.Empty(state.Ledger.Promises);
        Assert.Null(leader.PromiseId);
    }

    [Fact]
    public void Toda_promessa_aceita_esta_no_Livro_e_sobrevive_ao_save_e_a_passagem_para_o_mandato()
    {
        // Campanha inteira aceitando todo pedido: cada promessa feita a uma liderança tem de estar no Livro.
        CampaignEngine engine = CampaignKit.NewCareer(3106200, 88, strongestParty: true);
        GameState state = engine.Context.State;
        var robot = new CampaignRobot(engine, 88, new RobotOptions { Answer = LeaderAnswer.Aceitar });
        robot.PlayToElection();

        LeaderCatalog catalog = engine.Context.Campaign.Leaders;
        List<Leader> promised = state.City!.Leaders.Where(l => l.RequestMet && catalog.Request(l.RequestId).Kind != LeaderRequestDefinition.Dinheiro).ToList();
        Assert.True(promised.Count >= 3, $"Só {promised.Count} promessas na campanha inteira.");
        Assert.Equal(promised.Count, state.Ledger.Promises.Count);
        foreach (Leader leader in promised)
        {
            Promise promise = Assert.Single(state.Ledger.Promises, p => p.Id == leader.PromiseId);
            Assert.Equal(Promise.ParaPessoa, promise.ToKind);
            Assert.Equal(leader.PersonId, promise.ToId);
            Assert.Equal(leader.RequestId, promise.Object);
            Assert.Equal(leader.Zones[0], promise.ZoneId);
            Assert.False(string.IsNullOrWhiteSpace(promise.Text));
            Assert.DoesNotContain("{", promise.Text);
            Assert.True(promise.Deadline >= state.Campaign!.ElectionDate, "O prazo não pode vencer antes da eleição.");
            Assert.True(promise.Made < promise.Deadline);

            // A promessa de comparecer pode já ter sido cumprida na campanha; as demais só se cumprem no mandato.
            bool presenca = catalog.Request(leader.RequestId).Kind == LeaderRequestDefinition.Presenca;
            Assert.True(promise.State == PromiseState.Aberta || (presenca && promise.State == PromiseState.Cumprida), $"{promise.Text}: {promise.State}");
            Assert.Contains(state.City.Person(leader.PersonId)!.Memories, m => m.Fact == "promessa" && m.Peso == 3 && m.Date == promise.Made);
        }

        // Pedido de dinheiro aceito não é promessa: é esqueleto.
        Assert.All(
            state.City.Leaders.Where(l => l.RequestMet && catalog.Request(l.RequestId).Kind == LeaderRequestDefinition.Dinheiro),
            l => Assert.Contains(state.Ledger.Skeletons, s => s.KnownBy.Contains(l.PersonId)));

        string Book(GameState s) => System.Text.Json.JsonSerializer.Serialize(s.Ledger, Planalto.Core.Data.JsonData.Options);
        string book = Book(state);
        string city = System.Text.Json.JsonSerializer.Serialize(state.City, Planalto.Core.Data.JsonData.Options);

        // Save e load: o Livro e a cidade voltam iguais.
        GameState loaded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        Assert.Equal(book, Book(loaded));
        Assert.Equal(promised.Select(l => l.PromiseId), loaded.City!.Leaders.Where(l => l.PromiseId is not null).Select(l => l.PromiseId));

        // Apuração e passagem para o mandato (ou para a entressafra): o Livro e a cidade atravessam.
        CampaignEngine resumed = CampaignEngine.Start(CampaignKit.Context(loaded));
        new CampaignRobot(resumed, 88).Vote();
        CareerFlow.ConcludeElection(resumed.Context, CampaignKit.LegislatureContext(loaded));
        Assert.Null(loaded.Campaign);
        Assert.Equal(book, Book(loaded));
        Assert.Equal(city, System.Text.Json.JsonSerializer.Serialize(loaded.City, Planalto.Core.Data.JsonData.Options));
        Assert.All(loaded.Ledger.Promises, p => Assert.Contains(loaded.Npcs, n => n.Id == p.ToId));

        // E o save do mandato também as guarda.
        GameState inOffice = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(loaded));
        Assert.Equal(book, Book(inOffice));
    }

    [Fact]
    public void Promessa_com_prazo_vencido_vira_vencida_no_Livro()
    {
        var ledger = new Ledger();
        var made = new DateOnly(2028, 3, 6);
        Promise near = ledger.AddPromise(Promise.ParaPessoa, "npc_1", "presenca_evento", "Comparecer ao evento", "z1", made, made.AddDays(30));
        Promise far = ledger.AddPromise(Promise.ParaZona, "z2", "obra_creche", "Uma creche", "z2", made, made.AddDays(900));
        Assert.NotEqual(near.Id, far.Id);

        Assert.Empty(ledger.Expire(made.AddDays(30)));
        Assert.Equal([near], ledger.Expire(made.AddDays(31)));
        Assert.Equal(PromiseState.Vencida, near.State);
        Assert.Equal([far], ledger.Open);
        Assert.Empty(ledger.Expire(made.AddDays(60)));
    }
}
