using Planalto.Core.Campaign;
using Planalto.Core.Career;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.State;
using Planalto.Core.Tests.Campaign;
using Planalto.Core.World;

namespace Planalto.Core.Tests.Characters;

/// <summary>Idade inicial de 17 a 45 anos, profissões com idade mínima, Estudante e família coerente (GDD 3.3, D-036).</summary>
public class StartingAgeTests
{
    private static PeopleContent People => CampaignKit.C.People;

    private static readonly DateOnly Today = new(2028, 1, 1);

    // Vereador: 18 anos completos na data-limite do registro, 15 de agosto (Lei 9.504, art. 11, § 2º).
    [Theory]
    [InlineData(2010, 8, 15, 2028, true)]
    [InlineData(2010, 8, 16, 2028, false)]
    [InlineData(2010, 8, 16, 2032, true)]
    [InlineData(2010, 1, 1, 2028, true)]
    public void Vereador_tem_18_anos_aferidos_em_15_de_agosto(int year, int month, int day, int election, bool expected)
    {
        Assert.Equal(expected, CandidacyRulesConfig.Default.MeetsAge(new DateOnly(year, month, day), Office.Vereador, election));
    }

    // Prefeito: 21 anos na posse, 1º de janeiro do ano seguinte à eleição (CF, art. 14, § 3º, VI, c).
    [Theory]
    [InlineData(2008, 1, 1, true)]
    [InlineData(2008, 1, 2, false)]
    public void Prefeito_tem_21_anos_aferidos_na_posse(int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, CandidacyRulesConfig.Default.MeetsAge(new DateOnly(year, month, day), Office.Prefeito, 2028));
    }

    [Fact]
    public void Faixas_de_idade_do_jogador_e_dos_npcs()
    {
        Assert.Equal(17, People.Origins.StartingAge.Min);
        Assert.Equal(45, People.Origins.StartingAge.Max);
        Assert.Equal(25, People.Origins.NpcAge.Min);
        Assert.Equal(45, People.Origins.NpcAge.Max);

        var rng = new Rng(11);
        for (int i = 0; i < 500; i++)
        {
            Character npc = PersonGenerator.Generate(rng, People, new PersonRequest($"npc_{i}", "BA", 2927408, Today));
            Assert.InRange(npc.AgeOn(Today), 25, 45);
        }
    }

    [Fact]
    public void Profissao_so_aparece_a_partir_da_idade_minima()
    {
        ProfessionDefinition estudante = People.Professions.Professions.Single(p => p.Id == "estudante");
        ProfessionDefinition medico = People.Professions.Professions.Single(p => p.Id == "medico");
        Assert.True(estudante.AvailableAt(17));
        Assert.False(medico.AvailableAt(24));
        Assert.True(medico.AvailableAt(25));

        var rng = new Rng(12);
        var seen = new HashSet<string>();
        for (int i = 0; i < 1_000; i++)
        {
            int age = 17 + (i % 6);
            Character c = PersonGenerator.Generate(rng, People, new PersonRequest("jogador", "MG", 3106200, Today, Age: age, Ages: People.Origins.StartingAge));
            ProfessionDefinition profession = People.Professions.Professions.Single(p => p.Id == c.ProfessionId);
            Assert.True(profession.AvailableAt(c.AgeOn(Today)), $"{profession.Id} aos {c.AgeOn(Today)} anos");
            if (c.AgeOn(Today) == 17)
            {
                seen.Add(profession.Id);
            }
        }

        Assert.Contains("estudante", seen);
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Familia_sempre_coerente_com_a_idade_em_10_mil_sorteios()
    {
        FamilyConfig config = People.Family;
        var rng = new Rng(13);
        for (int i = 0; i < 10_000; i++)
        {
            Character c = PersonGenerator.Generate(rng, People, new PersonRequest("jogador", "RS", 4314902, Today, Ages: People.Origins.StartingAge));
            int age = c.AgeOn(Today);
            Assert.Empty(FamilyRules.Problems(config, c, Today));
            Assert.True(FamilyRules.Allows(config, c.MaritalStatus, age), $"{c.MaritalStatus} aos {age}");
            Assert.InRange(c.Children, 0, FamilyRules.MaxChildren(config, age));
            Assert.All(c.Family.Where(f => f.Relation == FamilyRelation.Conjuge), s => Assert.True(s.AgeOn(Today) >= config.SpouseMinAge));
            Assert.All(c.Family.Where(f => f.Relation == FamilyRelation.Filho), k => Assert.True(k.BirthDate >= c.BirthDate.AddYears(config.MinParentAge) && k.BirthDate < Today));
            if (age < 18)
            {
                Assert.False(FamilyRules.HasSpouse(c.MaritalStatus));
            }
        }
    }

    [Fact]
    public void Configuracao_de_familia_invalida_e_reportada()
    {
        FamilyConfig config = People.Family;
        var broken = new FamilyConfig
        {
            Note = config.Note,
            ChildrenCapByAge = [new ChildrenCap { Age = 17, Max = 5 }],
            MinParentAge = config.MinParentAge,
            ChildSpacingYears = config.ChildSpacingYears,
            MaritalMinAge = new Dictionary<string, int> { ["solteiro"] = 0, ["casado"] = 18, ["noivo"] = 18 },
            SpouseAgeGap = config.SpouseAgeGap,
            SpouseMinAge = config.SpouseMinAge,
        };

        var errors = broken.Validate().ToList();
        Assert.Contains(errors, e => e.Contains("não cabem", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("uniao_estavel", StringComparison.Ordinal));
        Assert.Contains(errors, e => e.Contains("noivo", StringComparison.Ordinal));
        Assert.Empty(config.Validate());
    }

    // 18 anos no registro (15/8): quem faz 18 até lá começa no ano inicial; quem faz depois começa na eleição seguinte.
    [Theory]
    [InlineData(2010, 8, 15, 2028, 2028)]
    [InlineData(2010, 8, 16, 2028, 2032)]
    [InlineData(2011, 1, 1, 2028, 2032)]
    [InlineData(1993, 5, 20, 2028, 2028)]
    [InlineData(2010, 8, 16, 2026, 2032)]
    [InlineData(1993, 5, 20, 2026, 2026)]
    public void A_carreira_comeca_no_ano_da_primeira_eleicao_que_o_personagem_pode_disputar(int year, int month, int day, int worldStart, int expected)
    {
        var birth = new DateOnly(year, month, day);

        Assert.Equal(expected, CareerFlow.CareerStartYear(birth, worldStart));
        Calendar calendar = NewGame.StartCalendar(worldStart, birth);
        Assert.Equal(expected, calendar.Year);
        Assert.Equal(1, calendar.Week);
        Assert.True(CandidacyRulesConfig.Default.MeetsAge(birth, Office.Vereador, CareerFlow.FirstEligibleElectionYear(birth, worldStart)));
    }

    [Fact]
    [Trait("Categoria", "Lento")]
    public void Jogador_de_17_anos_comeca_direto_na_pre_campanha_da_primeira_eleicao_em_que_e_elegivel_e_disputa_ela()
    {
        // Procura, de forma determinística, um jogador de 17 anos que ainda não tem 18 em 15/8/2028.
        WorldSetup world = WorldSetup.Create(CampaignKit.C.World, "brasil_2026", new Rng(21)).Setup!;
        Character? player = null;
        for (ulong seed = 1; player is null; seed++)
        {
            var rng = new Rng(seed);
            Character c = PersonGenerator.Generate(rng, People, new PersonRequest(RaceSetup.PlayerId, "PR", 4106902, Today, Age: 17, PartyId: world.Parties[0].Id, Ages: People.Origins.StartingAge));
            if (!CandidacyRulesConfig.Default.MeetsAge(c.BirthDate, Office.Vereador, 2028))
            {
                c.Campaign = new CampaignIdentity
                {
                    BallotNumber = "10123",
                    Slogan = "Teste",
                    LogoIcon = "sol",
                    LogoFont = "sans_forte",
                    LogoPrimaryColor = "#123456",
                    LogoSecondaryColor = "#654321",
                    Jingle = "forro",
                    PhotoPose = "frente",
                    PhotoBackground = "background_solido",
                };
                player = c;
            }
        }

        Assert.Equal(17, player.AgeOn(Today));
        GameState state = NewGame.Create(world, player, 21);

        // O calendário já abre em 2032, com o personagem mais velho, filiado e apto a disputar.
        Assert.Equal(2032, state.Calendar.Year);
        Assert.Equal(1, state.Calendar.Week);
        Assert.InRange(state.Player.AgeOn(state.Calendar.WeekStart), 20, 21);
        Assert.Equal(state.Calendar.WeekStart, state.Player.AffiliatedOn);
        Assert.Equal(2032, CareerFlow.NextEligibleElectionYear(state));
        Assert.True(CareerFlow.CanRun(state, 2032));
        Assert.Equal(CareerMode.ForaDeMandato, CareerFlow.Mode(state));

        CampaignEngine campaign = CampaignEngine.Start(CampaignKit.Context(state));
        Assert.Equal(CareerMode.Campanha, CareerFlow.Mode(state));
        Assert.Equal(CampaignPhase.PreCampanha, state.Campaign!.Phase);
        Assert.Equal(2032, state.Campaign.ElectionDate.Year);
        Assert.Equal(Office.Vereador, state.Campaign.Player.Office);
        CampaignAutoplayTests.Play(campaign, 21);
        Assert.NotNull(state.Campaign!.Outcome);
    }
}
