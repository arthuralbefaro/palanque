using Planalto.Core.Campaign;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Equipe (GDD 17.7): pessoas com rosto e memória, contratação, folha, conselho, eventos próprios e delegação.</summary>
public class StaffTests
{

    private static int CityOf(string porte, int skip = 3) =>
        CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == porte).OrderBy(m => m.CodigoIbge).Skip(skip).First().CodigoIbge;

    internal static CampaignEngine Fresh(int ibge, ulong seed)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, seed);
        Quiet(engine);
        engine.Context.State.Player.Resources.Money = 1_000_000;
        return engine;
    }

    /// <summary>Tira do caminho o que pausa o relógio e não é assunto do teste.</summary>
    internal static void Quiet(CampaignEngine engine)
    {
        engine.Context.State.Campaign!.Events.Pending.Clear();
        engine.Context.State.Campaign.Debate = null;
    }

    internal static void Week(CampaignEngine engine, bool keepTeamDecisions = false)
    {
        Quiet(engine);
        if (!keepTeamDecisions)
        {
            engine.Team.Pending.Clear();
        }

        engine.AdvanceWeek();
        Quiet(engine);
    }

    internal static StaffMember HireFirst(CampaignEngine engine, string role, Func<StaffMember, bool>? filter = null)
    {
        StaffMember member = engine.StaffCandidates(role).Select(v => v.Member).First(m => filter?.Invoke(m) ?? true);
        Assert.Null(engine.Hire(member.PersonId).Failure);
        return member;
    }

    public static TheoryData<int, ulong> Cities()
    {
        var data = new TheoryData<int, ulong>();
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            foreach (ulong seed in new ulong[] { 91, 92, 93 })
            {
                data.Add(CityOf(porte, (int)seed % 7), seed);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cities))]
    public void Toda_pessoa_da_equipe_tem_nome_aparencia_interesse_e_lembrancas(int ibge, ulong seed)
    {
        CampaignEngine engine = Fresh(ibge, seed);
        GameState state = engine.Context.State;
        StaffCatalog catalog = engine.Context.Campaign.Staff;
        Assert.NotEmpty(engine.City.Staff);
        foreach (StaffRole role in catalog.Funcoes)
        {
            Assert.InRange(engine.StaffCandidates(role.Id).Count, 2, 4);
        }

        Assert.All(engine.City.Staff, s =>
        {
            var npc = Assert.Single(state.Npcs, n => n.Id == s.PersonId);
            Person person = engine.City.Person(s.PersonId)!;
            Assert.False(string.IsNullOrWhiteSpace(npc.BallotName));
            Assert.NotNull(npc.Appearance);
            Assert.Contains(PersonRole.Equipe, person.Roles);
            Assert.NotEmpty(person.Interests);
            Assert.NotNull(person.Memories);
            Assert.InRange(s.Quality, 1, 5);
            Assert.InRange(s.Quality, s.QualityShownMin, s.QualityShownMax);
            Assert.Equal(1, s.QualityShownMax - s.QualityShownMin);
            Assert.True(s.Salary > 0);
            Assert.InRange(s.Loyalty, 0, 100);
            Assert.False(string.IsNullOrWhiteSpace(s.BondText));
            Assert.DoesNotContain("{", s.BondText);
            switch (s.BondKind)
            {
                case StaffBondType.Rival:
                    Assert.NotNull(state.Campaign!.Find(s.BondPersonId!));
                    break;
                case StaffBondType.Lideranca:
                    Assert.NotNull(engine.City.Leader(s.BondPersonId!));
                    break;
                default:
                    Assert.Contains(s.BondKind, new[] { StaffBondType.Familiar, StaffBondType.Nenhum });
                    break;
            }
        });

        // Vínculos dos três tipos aparecem no conjunto dos candidatos de alguma das cidades testadas; aqui, ao menos um.
        Assert.Contains(engine.City.Staff, s => s.BondKind != StaffBondType.Nenhum);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Vagas_seguem_o_porte_e_a_funcao_so_tem_uma_pessoa()
    {
        var expected = new Dictionary<string, int> { ["pequeno"] = 3, ["medio"] = 4, ["grande"] = 5, ["metropole"] = 6 };
        foreach (var (porte, slots) in expected)
        {
            CampaignEngine engine = Fresh(CityOf(porte), 5);
            Assert.Equal(slots, engine.TeamSlots);
            int hired = 0;
            foreach (StaffRole role in engine.Context.Campaign.Staff.Funcoes)
            {
                StaffMember first = engine.StaffCandidates(role.Id)[0].Member;
                CommandFailure? failure = engine.Hire(first.PersonId).Failure;
                if (hired < slots)
                {
                    Assert.Null(failure);
                    hired++;
                    StaffMember second = engine.StaffCandidates(role.Id)[1].Member;
                    Assert.Equal(CommandFailure.FuncaoOcupada, engine.Hire(second.PersonId).Failure);
                }
                else
                {
                    Assert.Equal(CommandFailure.SemVaga, failure);
                }
            }

            Assert.Equal(Math.Min(slots, 5), engine.TeamSlotsUsed);
        }
    }

    [Fact]
    public void Contratar_tem_previsao_com_faixa_e_explicacao_e_grava_lembranca()
    {
        CampaignEngine engine = Fresh(CityOf("medio"), 7);
        foreach (StaffView view in engine.StaffCandidates(StaffRole.Marqueteiro))
        {
            CommandForecast forecast = engine.ForecastHire(view.Member.PersonId);
            Assert.Null(forecast.Blocked);
            Assert.NotEmpty(forecast.Explanation);
            Assert.All(forecast.Explanation, line => Assert.DoesNotContain("{", line));
            ForecastLine quality = forecast.Line("quality")!;
            Assert.True(quality.Max > quality.Min);
            Assert.True(forecast.Contains("quality", null, view.Member.Quality), "A qualidade real fica na faixa mostrada.");
            Assert.False(view.QualityKnown);

            // O efeito da função também vem em faixa, e o real cai nela.
            StaffScale yield = engine.Context.Campaign.Staff.Role(StaffRole.Marqueteiro).Efeitos[StaffRole.Rendimento];
            Assert.True(forecast.Contains(StaffRole.Rendimento, StaffRole.Marqueteiro, yield.At(view.Member.Quality)));
        }

        StaffMember member = engine.StaffCandidates(StaffRole.Marqueteiro)[0].Member;
        var (failure, memory) = engine.Hire(member.PersonId);
        Assert.Null(failure);
        Assert.Equal("contratacao", memory!.Fact);
        Assert.Contains(engine.City.Person(member.PersonId)!.Memories, m => m.Fact == "contratacao");
        Assert.Equal(member, engine.StaffIn(StaffRole.Marqueteiro));
        Assert.Equal(engine.Context.Campaign.Staff.Role(StaffRole.Marqueteiro).Efeitos[StaffRole.Rendimento].At(member.Quality), engine.StaffEffect(StaffRole.Marqueteiro, StaffRole.Rendimento));

        // Depois das semanas de trabalho a qualidade deixa de ser faixa.
        for (int i = 0; i < engine.Context.Campaign.Staff.Regras.SemanasParaConhecer; i++)
        {
            Week(engine);
        }

        StaffView known = Assert.Single(engine.TeamView());
        Assert.True(known.QualityKnown);
        Assert.Equal(member.Quality, known.QualityMin);
    }

    [Fact]
    public void Parente_preterido_lembra()
    {
        // Procura uma função com um candidato da família e contrata outro.
        for (ulong seed = 1; seed < 40; seed++)
        {
            CampaignEngine engine = Fresh(CityOf("medio"), seed);
            StaffMember? relative = engine.City.Staff.FirstOrDefault(s => s.BondKind == StaffBondType.Familiar);
            if (relative is null)
            {
                continue;
            }

            StaffMember other = engine.City.Staff.First(s => s.Role == relative.Role && s != relative);
            Assert.Contains(engine.ForecastHire(other.PersonId).Explanation, line => line.Contains(engine.NameOf(relative.PersonId), StringComparison.Ordinal));
            engine.Hire(other.PersonId);
            Assert.Contains(engine.City.Person(relative.PersonId)!.Memories, m => m.Fact == "preterido" && m.Pontos < 0);
            return;
        }

        Assert.Fail("Nenhum candidato da família em 40 sementes.");
    }

    [Fact]
    public void Folha_sai_toda_semana_e_o_caixa_baixo_acende_o_alerta()
    {
        CampaignEngine engine = Fresh(CityOf("medio"), 9);
        StaffMember a = HireFirst(engine, StaffRole.Coordenador);
        StaffMember b = HireFirst(engine, StaffRole.Tesoureiro);
        PayrollView payroll = engine.Payroll();
        Assert.Equal(a.Salary + b.Salary, payroll.Weekly);
        Assert.False(payroll.Alert);

        decimal before = engine.Context.State.Player.Resources.Money;
        Week(engine);
        Assert.Equal(before - payroll.Weekly, engine.Context.State.Player.Resources.Money);

        // Caixa para uma semana e meia de folha: abaixo das duas do alerta.
        engine.Context.State.Player.Resources.Money = payroll.Weekly * 1.5m;
        Week(engine);
        Assert.True(engine.Team.PayrollAlert);
        Assert.True(engine.Payroll().Alert);
        Assert.Contains(engine.Agenda.Feed, f => f.Kind == Clock.CaixaBaixo);

        // Sem caixa, a equipe fica sem receber, lembra e perde lealdade.
        int loyalty = a.Loyalty;
        engine.Context.State.Player.Resources.Money = 0;
        Week(engine);
        Assert.True(a.Loyalty < loyalty);
        Assert.Contains(engine.City.Person(a.PersonId)!.Memories, m => m.Fact == "salario_atrasado");
        Assert.Equal(0, engine.Context.State.Player.Resources.Money);
    }

    [Fact]
    public void Conselho_da_semana_seguido_ou_ignorado_mexe_na_lealdade_e_grava_lembranca()
    {
        CampaignEngine engine = Fresh(CityOf("medio"), 11);
        StaffMember coordinator = HireFirst(engine, StaffRole.Coordenador);
        int step = engine.Context.Campaign.Staff.Regras.LealdadeConselho;
        Assert.Equal(2, step);
        Week(engine);
        Advice advice = engine.Team.Advice!;
        Assert.Equal(coordinator.PersonId, advice.FromPersonId);
        Assert.Contains(advice.ZoneId, engine.Territory.Zones);
        Assert.False(string.IsNullOrWhiteSpace(advice.Text));
        Assert.DoesNotContain("{", advice.Text);

        // Ignorado: a semana passa sem compromisso na zona aconselhada.
        int loyalty = coordinator.Loyalty;
        Week(engine);
        Assert.Equal(loyalty - step, coordinator.Loyalty);
        Assert.Contains(engine.City.Person(coordinator.PersonId)!.Memories, m => m.Fact == "conselho_ignorado");
        Assert.Equal(AdviceState.Ignorado, engine.Team.AdviceLog[^1].State);

        // Seguido: compromissos na zona aconselhada.
        advice = engine.Team.Advice!;
        loyalty = coordinator.Loyalty;
        int needed = engine.AdviceProgress().Needed;
        for (int i = 0; i < needed; i++)
        {
            Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", advice.ZoneId, engine.Today.AddDays(i), Turno.Manha)).Failure);
        }

        Week(engine);
        Assert.Equal(loyalty + step, coordinator.Loyalty);
        Assert.Contains(engine.City.Person(coordinator.PersonId)!.Memories, m => m.Fact == "conselho_seguido");

        // Dispensar na hora conta como ignorar.
        loyalty = coordinator.Loyalty;
        Assert.True(engine.IgnoreAdvice());
        Assert.Equal(loyalty - step, coordinator.Loyalty);
        Assert.Null(engine.Team.Advice);
    }

    [Fact]
    public void Conselho_do_coordenador_ruim_erra_mais_que_o_do_bom()
    {
        StaffScale error = CampaignKit.C.Campaign.Staff.Role(StaffRole.Coordenador).Efeitos[StaffRole.ErroConselho];
        for (int q = 1; q < 5; q++)
        {
            Assert.True(error.At(q) > error.At(q + 1), "O erro do conselho cai com a qualidade.");
        }
    }

    [Fact]
    public void Briga_pede_um_lado_e_os_dois_lembram()
    {
        CampaignEngine engine = Fresh(CityOf("medio"), 13);
        HireFirst(engine, StaffRole.Coordenador);
        HireFirst(engine, StaffRole.Tesoureiro);
        StaffDecision decision = engine.TriggerStaffEvent(StaffEventDefinition.Briga)!;
        Assert.True(engine.DecisionPending);
        Assert.Throws<InvalidOperationException>(() => engine.AdvanceDay());
        Assert.DoesNotContain("{", decision.Text);
        StaffMember first = engine.Staff(decision.PersonId)!;
        StaffMember second = engine.Staff(decision.OtherPersonId!)!;
        int loyaltyFirst = first.Loyalty;
        int loyaltySecond = second.Loyalty;

        StaffEventDefinition definition = engine.Context.Campaign.Staff.Event(StaffEventDefinition.Briga);
        foreach (StaffEventOption option in definition.Opcoes)
        {
            CommandForecast forecast = engine.ForecastStaffDecision(decision.Id, option.Id);
            Assert.NotEmpty(forecast.Explanation);
            Assert.All(forecast.Explanation, line => Assert.DoesNotContain("{", line));
            Assert.True(engine.DecisionText(decision, option.Rotulo, option).Length <= 40, "Rótulo de escolha com até 40 caracteres.");
        }

        CommandForecast chosen = engine.ForecastStaffDecision(decision.Id, "lado_primeiro");
        StaffDecisionResult result = engine.AnswerStaffDecision(decision.Id, "lado_primeiro");
        Assert.False(engine.DecisionPending);
        Assert.DoesNotContain("{", result.Text);
        Assert.True(first.Loyalty > loyaltyFirst);
        Assert.True(second.Loyalty < loyaltySecond);
        Assert.True(chosen.Contains("loyalty", first.PersonId, first.Loyalty - loyaltyFirst));
        Assert.True(chosen.Contains("loyalty", second.PersonId, second.Loyalty - loyaltySecond));
        Assert.Contains(engine.City.Person(first.PersonId)!.Memories, m => m.Fact == "briga_a_favor");
        Assert.Contains(engine.City.Person(second.PersonId)!.Memories, m => m.Fact == "briga_contra" && m.Pontos < 0);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Eventos_da_equipe_aumento_proposta_de_rival_e_porta_aberta()
    {
        CampaignEngine engine = Fresh(CityOf("grande"), 17);
        StaffRules rules = engine.Context.Campaign.Staff.Regras;
        foreach (StaffRole role in engine.Context.Campaign.Staff.Funcoes)
        {
            HireFirst(engine, role.Id);
        }

        for (int i = 0; i < rules.SemanasParaPedirAumento; i++)
        {
            Week(engine);
        }

        // Aumento concedido: salário sobe, lealdade sobe, lembrança.
        StaffDecision raise = engine.TriggerStaffEvent(StaffEventDefinition.Aumento)!;
        StaffMember asked = engine.Staff(raise.PersonId)!;
        decimal salary = asked.Salary;
        CommandForecast forecast = engine.ForecastStaffDecision(raise.Id, "conceder");
        engine.AnswerStaffDecision(raise.Id, "conceder");
        Assert.True(asked.Salary > salary);
        Assert.True(forecast.Contains("salary", asked.PersonId, (double)asked.Salary));
        Assert.Contains(engine.City.Person(asked.PersonId)!.Memories, m => m.Fact == "aumento_concedido");

    }

    [Fact]
    public void Proposta_de_rival_deixar_ir_tira_da_equipe_e_reforca_o_rival()
    {
        for (ulong seed = 1; seed < 40; seed++)
        {
            CampaignEngine engine = Fresh(CityOf("medio"), seed);
            StaffMember? wanted = engine.City.Staff.FirstOrDefault(s => s.BondKind == StaffBondType.Rival && engine.Territory.Redutos.ContainsKey(s.BondPersonId!));
            if (wanted is null)
            {
                continue;
            }

            Assert.Null(engine.Hire(wanted.PersonId).Failure);
            StaffDecision offer = engine.TriggerStaffEvent(StaffEventDefinition.PropostaRival)!;
            Assert.Equal(wanted.BondPersonId, offer.RivalId);
            double rival = engine.Territory.Recognition(offer.RivalId!);
            Assert.Single(engine.ForecastStaffDecision(offer.Id, "liberar").Outcomes);
            StaffDecisionResult left = engine.AnswerStaffDecision(offer.Id, "liberar");
            Assert.True(left.Left);
            Assert.False(wanted.Hired);
            Assert.True(wanted.Left);
            Assert.Contains(engine.City.Person(wanted.PersonId)!.Memories, m => m.Fact == "saiu");
            Assert.DoesNotContain(engine.StaffCandidates(wanted.Role), v => v.Member == wanted);
            Assert.True(engine.Territory.Recognition(offer.RivalId!) > rival, "O rival ganha fôlego nos redutos.");
            return;
        }

        Assert.Fail("Nenhum candidato ligado a rival com reduto em 40 sementes.");
    }

    [Fact]
    public void Doador_trazido_pela_equipe_rende_dinheiro_e_um_favor_no_Livro()
    {
        for (ulong seed = 1; seed < 40; seed++)
        {
            CampaignEngine engine = Fresh(CityOf("medio"), seed);
            StaffMember? member = engine.City.Staff.FirstOrDefault(s => s.BondKind is StaffBondType.Familiar or StaffBondType.Nenhum);
            if (member is null)
            {
                continue;
            }

            Assert.Null(engine.Hire(member.PersonId).Failure);
            StaffDecision donor = engine.TriggerStaffEvent(StaffEventDefinition.TrazDoador)!;
            decimal money = engine.Context.State.Player.Resources.Money;
            int debts = engine.Context.State.Ledger.Debts.Count;
            CommandForecast donation = engine.ForecastStaffDecision(donor.Id, "aceitar");
            StaffDecisionResult accepted = engine.AnswerStaffDecision(donor.Id, "aceitar");
            Assert.True(accepted.Money > 0);
            Assert.True(donation.Contains("money", null, (double)(engine.Context.State.Player.Resources.Money - money)));
            Assert.Equal(debts + 1, engine.Context.State.Ledger.Debts.Count);
            Assert.Contains(engine.City.Person(member.PersonId)!.Memories, m => m.Fact == "trouxe_doador");
            return;
        }

        Assert.Fail("Nenhum candidato sem vínculo ou da família em 40 sementes.");
    }

    [Fact]
    public void Porta_aberta_pelo_vinculo_apresenta_a_lideranca()
    {
        for (ulong seed = 1; seed < 40; seed++)
        {
            CampaignEngine engine = Fresh(CityOf("medio"), seed);
            StaffMember? linked = engine.City.Staff.FirstOrDefault(s => s.BondKind == StaffBondType.Lideranca && engine.City.Leader(s.BondPersonId!) is { Known: false });
            if (linked is null)
            {
                continue;
            }

            Assert.Null(engine.Hire(linked.PersonId).Failure);
            StaffDecision decision = engine.TriggerStaffEvent(StaffEventDefinition.TrazLideranca)!;
            Leader leader = engine.City.Leader(decision.LeaderId!)!;
            double relation = engine.RelationOf(leader.PersonId);
            StaffDecisionResult result = engine.AnswerStaffDecision(decision.Id, "aceitar");
            Assert.True(leader.Known);
            Assert.True(engine.RelationOf(leader.PersonId) > relation);
            Assert.True(result.LedgerEntry);
            Assert.Contains(engine.Context.State.Ledger.Debts, d => d.ToPersonId == linked.PersonId);
            Assert.Null(engine.TriggerStaffEvent(StaffEventDefinition.TrazLideranca));
            return;
        }

        Assert.Fail("Nenhum candidato ligado a liderança desconhecida em 40 sementes.");
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Demissao_grava_lembranca_e_demitido_com_lealdade_baixa_que_conhece_esqueleto_pode_vazar()
    {
        int leaks = 0;
        for (ulong seed = 1; seed <= 12; seed++)
        {
            CampaignEngine engine = Fresh(CityOf("medio"), seed);
            GameState state = engine.Context.State;
            StaffMember treasurer = HireFirst(engine, StaffRole.Tesoureiro);

            // Uma irregularidade acontece enquanto a pessoa cuida das contas: na virada da semana ela fica sabendo.
            LedgerSkeleton skeleton = state.Ledger.AddSkeleton("gasto_nao_declarado", "Gasto sem registro", 2, engine.Today, []);
            Week(engine);
            Assert.Contains(treasurer.PersonId, skeleton.KnownBy);

            treasurer.Loyalty = 10;
            CommandForecast forecast = engine.ForecastFire(treasurer.PersonId);
            Assert.NotEmpty(forecast.Explanation);
            ForecastOutcome leak = Assert.Single(forecast.Outcomes);
            Assert.Equal(engine.Context.Campaign.Staff.Regras.ChanceVazamento, leak.Probability);
            double image = state.Campaign!.Player.Image;

            StaffDeparture departure = engine.Fire(treasurer.PersonId)!;
            Assert.Equal("demissao", departure.Memory.Fact);
            Assert.True(departure.Memory.Pontos < 0);
            Assert.False(treasurer.Hired);
            Assert.Null(engine.StaffIn(StaffRole.Tesoureiro));
            Assert.True(forecast.Contains("image", null, state.Campaign.Player.Image - image));
            if (departure.Leaked)
            {
                leaks++;
                Assert.Contains(skeleton.Id, departure.SkeletonsExposed);
                Assert.Contains(departure.LeakedTo!, skeleton.KnownBy);
                Assert.True(state.Campaign.Player.Image < image);
                Assert.Contains(engine.Agenda.Feed, f => f.Kind == "vazamento");
            }
            else
            {
                Assert.Equal(image, state.Campaign.Player.Image);
            }
        }

        Assert.InRange(leaks, 1, 11);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Demitido_leal_ou_que_nao_sabe_de_nada_nao_vaza()
    {
        for (ulong seed = 1; seed <= 6; seed++)
        {
            CampaignEngine engine = Fresh(CityOf("medio"), seed);
            StaffMember loyal = HireFirst(engine, StaffRole.Tesoureiro);
            StaffMember unaware = HireFirst(engine, StaffRole.Marqueteiro);
            engine.Context.State.Ledger.AddSkeleton("gasto_nao_declarado", "Gasto sem registro", 2, engine.Today, []);
            Week(engine);
            loyal.Loyalty = 90;
            unaware.Loyalty = 5;
            Assert.Empty(engine.ForecastFire(loyal.PersonId).Outcomes);
            Assert.Empty(engine.ForecastFire(unaware.PersonId).Outcomes);
            Assert.False(engine.Fire(loyal.PersonId)!.Leaked);
            Assert.False(engine.Fire(unaware.PersonId)!.Leaked);
        }
    }

    [Fact]
    public void Delegar_rende_metade_nao_ocupa_o_turno_e_quem_recebeu_lembra()
    {
        CampaignEngine engine = Fresh(CityOf("medio"), 21);
        string zone = engine.Territory.Zones[0];
        var own = new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha);
        var delegated = own with { Delegado = true };
        Assert.Equal(ScheduleFailure.SemCoordenador, engine.CanSchedule(delegated));

        StaffMember coordinator = HireFirst(engine, StaffRole.Coordenador);
        Assert.Equal(coordinator.Quality, engine.DelegationsPerWeek);
        CommitmentForecast full = engine.Forecast(own);
        CommitmentForecast half = engine.Forecast(delegated);
        Assert.Null(half.Blocked);
        Assert.Equal(0.5, half.Factors.Single(f => f.Id == "delegado").Multiplier);
        Assert.Equal(full.Yield * 0.5, half.Yield, 9);
        Assert.Equal(0, half.Stress);

        // O representante vai no mesmo turno em que o candidato faz outra coisa.
        Assert.Null(engine.Schedule(own).Failure);
        Assert.Null(engine.Schedule(delegated with { ZoneId = engine.Territory.Zones[^1] }).Failure);
        Assert.Equal(ScheduleFailure.NaoDelegavel, engine.CanSchedule(new CommitmentRequest("entrevista", null, engine.Today, Turno.Tarde, Delegado: true)));

        // Limite semanal pela qualidade do coordenador.
        for (int i = 1; i < coordinator.Quality; i++)
        {
            Assert.Null(engine.Schedule(delegated with { Turno = Turno.Tarde, Date = engine.Today.AddDays(i % 6) }).Failure);
        }

        Assert.Equal(ScheduleFailure.SemDelegacao, engine.CanSchedule(delegated with { Turno = Turno.Noite }));
        List<CommitmentResult> results = engine.RunToday();
        CommitmentResult result = results.First(r => r.Commitment.Delegado);
        Assert.InRange(result.Presence.Values.Sum(), 0.0001, 1000);
        Assert.Equal(0, result.Stress);

        // Visita delegada: +1 em vez de +4, a pessoa lembra que o candidato não foi e o pedido não é ouvido.
        CampaignEngine other = Fresh(CityOf("medio"), 22);
        HireFirst(other, StaffRole.Coordenador);
        Leader leader = other.City.Leaders.First(l => l.Known);
        var visit = new CommitmentRequest(CommitmentCatalog.Visita, null, other.Today, Turno.Manha, PersonId: leader.PersonId);
        Assert.Equal(other.Context.Campaign.Leaders.Regras.PontosVisita, other.Forecast(visit).RelationPoints);
        Assert.Equal(1, other.Forecast(visit with { Delegado = true }).RelationPoints);
        Assert.Null(other.Schedule(visit with { Delegado = true }).Failure);
        CommitmentResult sent = Assert.Single(other.RunToday());
        Assert.Equal(1, sent.RelationPoints);
        Assert.Null(other.Agenda.PendingRequest);
        Assert.False(leader.RequestKnown);
        Assert.Contains(other.City.Person(leader.PersonId)!.Memories, m => m.Fact == "visita_delegada" && m.Pontos == 1);
    }

    [Fact]
    public void Marqueteiro_muda_o_comicio_e_estreita_a_previsao_e_social_media_muda_a_gravacao()
    {
        CampaignEngine engine = Fresh(CityOf("medio"), 23);
        StaffCatalog catalog = engine.Context.Campaign.Staff;
        var video = new CommitmentRequest("gravacao", null, engine.Today, Turno.Manha);
        CommitmentForecast plain = engine.Forecast(video);
        Assert.DoesNotContain(plain.Factors, f => f.Id == "equipe");
        Assert.Contains(plain.Outcomes, o => Math.Abs(o.Probability - catalog.Role(StaffRole.SocialMedia).Efeitos[StaffRole.Viral].Sem) < 1e-9);

        StaffMember social = HireFirst(engine, StaffRole.SocialMedia);
        CommitmentForecast withSocial = engine.Forecast(video);
        Assert.Equal(catalog.Role(StaffRole.SocialMedia).Efeitos[StaffRole.Alcance].At(social.Quality), withSocial.Factors.Single(f => f.Id == "equipe").Multiplier);
        Assert.Contains(withSocial.Outcomes, o => Math.Abs(o.Probability - catalog.Role(StaffRole.SocialMedia).Efeitos[StaffRole.Viral].At(social.Quality)) < 1e-9);

        var walk = new CommitmentRequest("caminhada", engine.Territory.Zones[0], engine.Today, Turno.Tarde);
        CommitmentForecast wide = engine.Forecast(walk);
        StaffMember marketer = HireFirst(engine, StaffRole.Marqueteiro, m => m.Quality >= 2);
        CommitmentForecast narrow = engine.Forecast(walk);
        Assert.True(narrow.PresenceMax - narrow.PresenceMin < wide.PresenceMax - wide.PresenceMin, "O marqueteiro estreita a faixa da previsão.");
        Assert.Equal(wide.PresenceExpected, narrow.PresenceExpected, 9);

        // E o resultado continua caindo na faixa estreita.
        Assert.Null(engine.Schedule(walk).Failure);
        CommitmentResult result = Assert.Single(engine.RunToday());
        Assert.InRange(result.Presence[walk.ZoneId!], narrow.PresenceMin - 1e-9, narrow.PresenceMax + 1e-9);
        Assert.Equal(catalog.Role(StaffRole.Marqueteiro).Efeitos[StaffRole.Rendimento].At(marketer.Quality), engine.StaffEffect(StaffRole.Marqueteiro, StaffRole.Rendimento));
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Tesoureiro_melhor_erra_menos_e_o_erro_vira_esqueleto_no_Livro()
    {
        StaffScale scale = CampaignKit.C.Campaign.Staff.Role(StaffRole.Tesoureiro).Efeitos[StaffRole.ErroContabil];
        Assert.Equal(0.08, scale.At(1));
        Assert.Equal(0.005, scale.At(5));
        Assert.Equal(0.35, CampaignKit.C.Campaign.Staff.Role(StaffRole.Advogado).Efeitos[StaffRole.Exito].Sem);

        // Com folha paga toda semana e sem tesoureiro, em 40 semanas o erro aparece (8% por semana).
        CampaignEngine engine = Fresh(CityOf("medio"), 25);
        HireFirst(engine, StaffRole.Coordenador);
        Assert.Equal(0.35, engine.LawyerSuccessChance);
        int weeks = 0;
        while (engine.Context.State.Campaign!.Phase == CampaignPhase.PreCampanha && weeks++ < 30)
        {
            Week(engine);
        }

        Assert.Contains(engine.Context.State.Ledger.Skeletons, s => s.Type == "erro_contabil");
    }

    [Fact]
    public void Equipe_vai_e_volta_pelo_save()
    {
        CampaignEngine engine = Fresh(CityOf("medio"), 27);
        HireFirst(engine, StaffRole.Coordenador);
        HireFirst(engine, StaffRole.Tesoureiro);
        Week(engine);
        engine.Team.Pending.Clear();
        Assert.NotNull(engine.TriggerStaffEvent(StaffEventDefinition.Briga));
        Assert.NotNull(engine.Team.Advice);
        string json = GameStateSerializer.Serialize(engine.Context.State);
        GameState loaded = GameStateSerializer.Deserialize(json);
        Assert.Equal(json, GameStateSerializer.Serialize(loaded));
        CampaignEngine resumed = CampaignEngine.Start(CampaignKit.Context(loaded));
        Assert.Equal(json, GameStateSerializer.Serialize(resumed.Context.State));
        Assert.Equal(2, resumed.HiredStaff.Count());
        Assert.Single(resumed.Team.Pending);
    }
}
