using Planalto.Core.Campaign;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Objetivos que nascem do jogo (GDD 17.2, peça 6, e princípio h).</summary>
public class ObjectiveTests
{
    private static int CityOf(string porte, int skip = 3) =>
        CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == porte).OrderBy(m => m.CodigoIbge).Skip(skip).First().CodigoIbge;

    private static CampaignEngine Fresh(string porte, ulong seed) => StaffTests.Fresh(CityOf(porte), seed);

    /// <summary>De 1 a 3 objetivos ativos, cada um com condição mensurável, prazo e origem.</summary>
    internal static void AssertPanel(CampaignEngine engine)
    {
        CampaignState campaign = engine.Context.State.Campaign!;
        IReadOnlyList<ObjectiveView> views = engine.ActiveObjectives();
        Assert.InRange(views.Count, 1, 3);
        Assert.Equal(views.Count, views.Select(v => v.Objective.Kind).Distinct().Count());
        Assert.All(views, v =>
        {
            Objective o = v.Objective;
            Assert.Contains(o.Kind, ObjectiveType.Ids);
            Assert.False(string.IsNullOrWhiteSpace(o.Text));
            Assert.False(string.IsNullOrWhiteSpace(o.Origin));
            Assert.DoesNotContain("{", o.Text);
            Assert.DoesNotContain("{", o.Origin);
            Assert.False(string.IsNullOrWhiteSpace(o.TargetId));
            Assert.True(o.Deadline >= o.Created, "O prazo não pode ser anterior à criação.");
            Assert.True(o.Deadline < campaign.ElectionDate, "O prazo cabe na campanha.");
            Assert.Equal(ObjectiveStatus.Ativo, o.Status);
            Assert.True(double.IsFinite(v.Current) && double.IsFinite(v.Goal), "A condição é mensurável.");
            Assert.InRange(v.Progress, 0, 1);
            Assert.False(string.IsNullOrWhiteSpace(v.TypeName));
        });
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void No_primeiro_dia_ha_de_um_a_tres_objetivos_com_condicao_prazo_e_origem()
    {
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            CampaignEngine engine = Fresh(porte, 71);
            AssertPanel(engine);
            Assert.Equal(engine.Context.Campaign.Objectives.Regras.Maximo, engine.Objectives.Active.Count);
        }
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Em_qualquer_dia_da_campanha_ha_de_um_a_tres_objetivos_e_aparecem_quatro_tipos_ou_mais()
    {
        CampaignEngine engine = CampaignKit.NewCareer(CityOf("medio"), 72);
        var robot = new CampaignRobot(engine, 72, new RobotOptions { Answer = LeaderAnswer.Aceitar });
        var kinds = new HashSet<string>();
        int days = 0;
        robot.ResolveDecisions();
        while (engine.Context.State.Campaign!.Phase != CampaignPhase.Votacao)
        {
            if (engine.Today.DayOfWeek == DayOfWeek.Monday)
            {
                robot.PlanWeek();
            }

            AssertPanel(engine);
            kinds.UnionWith(engine.Objectives.Active.Select(o => o.Kind));
            robot.PlayDay();
            Assert.True(++days < 400, "A campanha não chegou à eleição.");
        }

        kinds.UnionWith(engine.Objectives.History.Select(o => o.Kind));
        Assert.True(kinds.Count >= 4, $"Só {kinds.Count} tipos de objetivo na campanha: {string.Join(", ", kinds)}.");
        Assert.Contains(engine.Objectives.History, o => o.Status == ObjectiveStatus.Cumprido);
    }

    [Fact]
    public void Cumprir_um_objetivo_rende_lealdade_na_equipe_e_lembranca_em_quem_se_beneficiou()
    {
        CampaignEngine engine = Fresh("medio", 73);
        foreach (Leader l in engine.City.Leaders)
        {
            l.Known = true;
        }

        Objective objective = engine.Objectives.Active.Single(o => o.Kind == ObjectiveType.TrazerLideranca);
        Leader leader = engine.City.Leader(objective.TargetId)!;
        Assert.Equal(leader.PersonId, objective.BeneficiaryId);
        StaffMember treasurer = StaffTests.HireFirst(engine, StaffRole.Tesoureiro);
        StaffMember marketer = StaffTests.HireFirst(engine, StaffRole.Marqueteiro);
        int[] loyalty = [treasurer.Loyalty, marketer.Loyalty];
        ObjectiveView before = engine.ActiveObjectives().Single(v => v.Objective == objective);
        Assert.False(before.Done);

        // A liderança fecha com o jogador: pedido atendido, relação acima do preço, nenhum rival na frente.
        leader.RequestMet = true;
        leader.Favor.Clear();
        engine.City.Person(leader.PersonId)!.Affinity = 100;
        leader.AlignedWith = RaceSetup.PlayerId;
        Assert.True(engine.ActiveObjectives().Single(v => v.Objective == objective).Done);

        StaffTests.Week(engine);
        Assert.DoesNotContain(objective, engine.Objectives.Active);
        Assert.Contains(objective, engine.Objectives.History);
        Assert.Equal(ObjectiveStatus.Cumprido, objective.Status);
        int step = engine.Context.Campaign.Objectives.Regras.LealdadeEquipe;
        Assert.Equal(2, step);
        Assert.Equal(Math.Min(100, loyalty[0] + step), treasurer.Loyalty);
        Assert.Equal(Math.Min(100, loyalty[1] + step), marketer.Loyalty);
        Assert.Contains(engine.City.Person(leader.PersonId)!.Memories, m => m.Fact == "objetivo_cumprido" && m.Pontos > 0);
        Assert.Contains(engine.Agenda.Feed, f => f.Kind == "objetivo");
        AssertPanel(engine);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Objetivo_de_presenca_mede_a_presenca_na_zona_e_vence_no_prazo()
    {
        CampaignEngine engine = Fresh("medio", 74);
        Objective objective = engine.Objectives.Active.Single(o => o.Kind == ObjectiveType.PresencaZona);
        int zone = engine.Territory.IndexOf(objective.TargetId);
        Assert.True(zone >= 0);
        double[] presence = engine.Territory.Presence[RaceSetup.PlayerId];
        Assert.Equal(presence[zone], objective.Start, 9);
        Assert.True(objective.Goal > objective.Start);
        Assert.Contains(engine.ZoneName(objective.TargetId), objective.Text);

        // Metade do caminho: progresso de 50%.
        presence[zone] = (objective.Start + objective.Goal) / 2;
        ObjectiveView half = engine.ActiveObjectives().Single(v => v.Objective == objective);
        Assert.Equal(0.5, half.Progress, 6);
        Assert.False(half.Done);

        // Fixado, fica até o prazo; passado o prazo sem cumprir, vence e vira fato no feed.
        Assert.True(engine.PinObjective(objective.Id));
        presence[zone] = 0;
        int guard = 0;
        while (engine.Objectives.Active.Contains(objective))
        {
            Assert.True(++guard < 20);
            Assert.True(engine.Today <= objective.Deadline.AddDays(7));
            presence[zone] = 0;
            StaffTests.Week(engine);
        }

        Assert.Equal(ObjectiveStatus.Vencido, objective.Status);
        Assert.True(engine.Today > objective.Deadline);
        AssertPanel(engine);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void O_jogador_pode_fixar_ou_dispensar_e_a_proposta_nao_fixada_e_trocada()
    {
        CampaignEngine engine = Fresh("medio", 75);
        ObjectiveRules rules = engine.Context.Campaign.Objectives.Regras;
        Objective pinned = engine.Objectives.Active[0];
        Objective loose = engine.Objectives.Active[1];
        Assert.True(engine.PinObjective(pinned.Id));
        Assert.False(engine.PinObjective("nao_existe"));
        for (int i = 0; i < rules.ValidadePropostaSemanas; i++)
        {
            StaffTests.Week(engine);
        }

        Assert.Contains(pinned, engine.Objectives.Active);
        Assert.DoesNotContain(loose, engine.Objectives.Active);
        Assert.Contains(loose.Status, new[] { ObjectiveStatus.Retirado, ObjectiveStatus.Cumprido, ObjectiveStatus.Vencido });
        AssertPanel(engine);

        // Dispensar tira do painel; dispensando todos, o gerador propõe outro na hora.
        Assert.True(engine.DismissObjective(pinned.Id));
        Assert.Equal(ObjectiveStatus.Dispensado, pinned.Status);
        Assert.DoesNotContain(pinned, engine.Objectives.Active);
        foreach (Objective o in engine.Objectives.Active.ToList())
        {
            Assert.True(engine.DismissObjective(o.Id));
            AssertPanel(engine);
        }

        Assert.False(engine.DismissObjective(pinned.Id));
        Assert.All(engine.Objectives.Active, o => Assert.Equal(engine.Today, o.Created));
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Os_tipos_nascem_do_estado_caixa_posicao_equipe_rival_e_promessa()
    {
        CampaignEngine engine = Fresh("medio", 76);
        GameState state = engine.Context.State;
        var seen = new Dictionary<string, Objective>();
        void Collect()
        {
            foreach (Objective o in engine.Objectives.Active.Concat(engine.Objectives.History))
            {
                seen.TryAdd(o.Kind, o);
            }
        }

        // Uma promessa de defender um tema, aberta, vira objetivo de cumpri-la.
        LeaderCatalog leaders = engine.Context.Campaign.Leaders;
        Leader leader = engine.City.Leaders[0];
        leader.Known = true;
        leader.PromiseId = state.Ledger.AddPromise(Promise.ParaPessoa, leader.PersonId, leaders.Pedidos.First(p => p.Kind == LeaderRequestDefinition.Posicao).Id, "Defender em público a pauta", leader.Zones[0], engine.Today, state.Campaign!.ElectionDate).Id;

        Collect();
        for (int week = 0; week < 14 && seen.Count < ObjectiveType.Ids.Count; week++)
        {
            StaffTests.Week(engine);
            Collect();
        }

        Assert.Equal(ObjectiveType.Ids.Order(), seen.Keys.Order());
        Objective cash = seen[ObjectiveType.JuntarCaixa];
        Assert.True(cash.Goal > cash.Start);
        Objective known = seen[ObjectiveType.PosicaoConhecida];
        Assert.NotNull(engine.Context.Themes.Themes.SingleOrDefault(t => t.Id == known.TargetId));
        Assert.InRange(known.Goal, 0.01, 1);
        Objective rival = seen[ObjectiveType.PassarRival];
        Assert.NotNull(state.Campaign.Find(rival.TargetId));
        Assert.True(rival.Start < 0, "O rival proposto está à frente do jogador.");
        Objective team = seen[ObjectiveType.MontarEquipe];
        Assert.NotNull(engine.Context.Campaign.Staff.Funcoes.SingleOrDefault(f => f.Id == team.TargetId));
        Objective promise = seen[ObjectiveType.CumprirPromessa];
        Assert.Equal(leader.PromiseId, promise.TargetId);
        Assert.Equal(leader.PersonId, promise.BeneficiaryId);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Contratar_cumpre_o_objetivo_de_montar_a_equipe_e_falar_do_tema_o_de_posicao_conhecida()
    {
        CampaignEngine engine = Fresh("medio", 77);
        int guard = 0;
        while (!engine.Objectives.Active.Any(o => o.Kind == ObjectiveType.MontarEquipe) || !engine.Objectives.Active.Any(o => o.Kind == ObjectiveType.PosicaoConhecida))
        {
            Assert.True(++guard < 20, "Os objetivos de equipe e de posição não apareceram juntos.");
            foreach (Objective o in engine.Objectives.Active.Where(o => o.Kind is ObjectiveType.MontarEquipe or ObjectiveType.PosicaoConhecida))
            {
                engine.PinObjective(o.Id);
            }

            StaffTests.Week(engine);
        }

        Objective team = engine.Objectives.Active.Single(o => o.Kind == ObjectiveType.MontarEquipe);
        Objective known = engine.Objectives.Active.Single(o => o.Kind == ObjectiveType.PosicaoConhecida);
        StaffMember hired = StaffTests.HireFirst(engine, team.TargetId);
        Assert.True(engine.ActiveObjectives().Single(v => v.Objective == team).Done);

        for (int day = 0; day < 3 && !engine.ActiveObjectives().Single(v => v.Objective == known).Done; day++)
        {
            foreach (Turno turno in Enum.GetValues<Turno>())
            {
                Assert.Null(engine.Schedule(new CommitmentRequest("gravacao", null, engine.Today, turno, Tema: known.TargetId)).Failure);
            }

            engine.RunToday();
            StaffTests.Quiet(engine);
            engine.AdvanceDay();
        }

        Assert.True(engine.ActiveObjectives().Single(v => v.Objective == known).Done);
        while (engine.Today.DayOfWeek != DayOfWeek.Monday || engine.Objectives.Active.Contains(team))
        {
            StaffTests.Quiet(engine);
            engine.Team.Pending.Clear();
            engine.AdvanceDay();
        }

        Assert.Equal(ObjectiveStatus.Cumprido, team.Status);
        Assert.Equal(ObjectiveStatus.Cumprido, known.Status);
        Assert.Equal(hired.PersonId, team.BeneficiaryId);
        Assert.Contains(engine.City.Person(hired.PersonId)!.Memories, m => m.Fact == "objetivo_cumprido");
    }

    [Fact]
    public void Objetivos_vao_e_voltam_pelo_save()
    {
        CampaignEngine engine = Fresh("medio", 78);
        engine.PinObjective(engine.Objectives.Active[0].Id);
        engine.DismissObjective(engine.Objectives.Active[1].Id);
        StaffTests.Week(engine);
        string json = GameStateSerializer.Serialize(engine.Context.State);
        GameState loaded = GameStateSerializer.Deserialize(json);
        Assert.Equal(json, GameStateSerializer.Serialize(loaded));
        CampaignEngine resumed = CampaignEngine.Start(CampaignKit.Context(loaded));
        Assert.Equal(json, GameStateSerializer.Serialize(resumed.Context.State));
        Assert.Equal(engine.Objectives.Active.Select(o => o.Id), resumed.Objectives.Active.Select(o => o.Id));
        AssertPanel(resumed);
    }
}
