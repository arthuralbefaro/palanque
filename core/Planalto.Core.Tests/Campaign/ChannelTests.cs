using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Verba por canal (GDD 17.8): retorno decrescente, disputa de canal por zona, fases, regras de lei e previsão.</summary>
public class ChannelTests
{
    private static int CityOf(string porte, int skip = 3) =>
        CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais && m.Porte == porte).OrderBy(m => m.CodigoIbge).Skip(skip).First().CodigoIbge;

    private static CampaignEngine Fresh(string porte, ulong seed) => StaffTests.Fresh(CityOf(porte), seed);

    /// <summary>Anda até a campanha oficial (16 de agosto), quando todos os canais abrem.</summary>
    internal static CampaignEngine InCampaign(string porte, ulong seed)
    {
        CampaignEngine engine = Fresh(porte, seed);
        ToCampaign(engine);
        return engine;
    }

    internal static void ToCampaign(CampaignEngine engine)
    {
        int guard = 0;
        while (engine.Context.State.Campaign!.Phase == CampaignPhase.PreCampanha)
        {
            Assert.True(++guard < 60, "A campanha oficial não começou.");
            StaffTests.Week(engine);
        }

        Assert.Equal(CampaignPhase.Campanha, engine.Context.State.Campaign.Phase);

        // A fase vira na semana que contém o dia 16; os canais abrem no dia.
        while (engine.BeforeOfficialCampaign)
        {
            StaffTests.Quiet(engine);
            engine.AdvanceDay();
        }

        StaffTests.Quiet(engine);
        engine.Context.State.Player.Resources.Money = 1_000_000;
    }

    /// <summary>Campanha oficial numa cidade que tenha zona sem verba de rival (nenhum reduto ali).</summary>
    private static CampaignEngine WithFreeZone(string porte, ulong seed)
    {
        CampaignEngine engine = InCampaign(porte, seed);

        // Os rivais refazem o plano de verba na segunda: a primeira semana inteira de campanha oficial já tem Rua e Cabos.
        StaffTests.Week(engine);
        engine.Context.State.Player.Resources.Money = 1_000_000;
        List<string> zones = engine.Territory.Zones;
        if (zones.All(z => engine.RivalBudget(ChannelDefinition.Rua, z) > 0))
        {
            // A verba dos rivais tem estado (R3): tira-se do plano desta semana o que eles puseram numa zona.
            string free = zones[^1];
            foreach (RivalRecord record in engine.Rivals.Records)
            {
                record.Budget.RemoveAll(a => a.TargetId == free);
            }
        }

        Assert.Contains(zones, z => engine.RivalBudget(ChannelDefinition.Rua, z) == 0);
        Assert.Contains(zones, z => engine.RivalBudget(ChannelDefinition.Rua, z) > 0);
        return engine;
    }

    private static string ZoneWithoutRival(CampaignEngine engine, string channel) =>
        engine.Territory.Zones.First(z => engine.RivalBudget(channel, z) == 0);

    private static string ZoneWithRival(CampaignEngine engine, string channel) =>
        engine.Territory.Zones.First(z => engine.RivalBudget(channel, z) > 0);

    [Fact]
    public void Dobrar_a_verba_de_um_canal_rende_entre_35_e_45_por_cento_a_mais()
    {
        CampaignEngine engine = WithFreeZone("grande", 31);
        foreach (string channel in new[] { ChannelDefinition.Rua, ChannelDefinition.Material, ChannelDefinition.Cabos })
        {
            string zone = ZoneWithoutRival(engine, channel);
            foreach (decimal verba in new[] { engine.CabosGroupCost * 2, engine.CabosGroupCost * 6, engine.CabosGroupCost * 20 })
            {
                double one = engine.ForecastChannel(channel, zone, verba).Line("effect")!.Expected;
                double two = engine.ForecastChannel(channel, zone, verba * 2).Line("effect")!.Expected;
                Assert.True(one > 0);
                Assert.InRange(two / one, 1.35, 1.45);
            }
        }

        // O mesmo vale para o que o canal rende de fato (a presença antes do teto de cada zona).
        string free = ZoneWithoutRival(engine, ChannelDefinition.Rua);
        int z = engine.Territory.IndexOf(free);
        engine.Territory.Presence[RaceSetup.PlayerId][z] = 0;
        double low = engine.ForecastChannel(ChannelDefinition.Rua, free, engine.ChannelStep * 5).Line("presence", free)!.Expected;
        double high = engine.ForecastChannel(ChannelDefinition.Rua, free, engine.ChannelStep * 10).Line("presence", free)!.Expected;
        Assert.InRange(high / low, 1.35, 1.45);
    }

    [Fact]
    public void Verba_de_rival_no_mesmo_canal_e_zona_reduz_o_efeito()
    {
        CampaignEngine engine = WithFreeZone("grande", 32);
        decimal verba = engine.ChannelStep * 10;
        string contested = ZoneWithRival(engine, ChannelDefinition.Rua);
        string free = ZoneWithoutRival(engine, ChannelDefinition.Rua);
        CommandForecast alone = engine.ForecastChannel(ChannelDefinition.Rua, free, verba);
        CommandForecast shared = engine.ForecastChannel(ChannelDefinition.Rua, contested, verba);
        Assert.Equal(1, alone.Line("share")!.Expected, 9);
        Assert.True(shared.Line("share")!.Expected < 1);
        Assert.Equal(alone.Line("return")!.Expected, shared.Line("return")!.Expected, 9);
        Assert.True(shared.Line("effect")!.Expected < alone.Line("effect")!.Expected, "Gritar onde todos gritam rende menos.");

        // Mais verba de rival no mesmo canal e na mesma zona: o efeito cai mais, e só ali e só nesse canal.
        double before = shared.Line("effect")!.Expected;
        double other = engine.ForecastChannel(ChannelDefinition.Material, contested, verba).Line("effect")!.Expected;
        engine.Budget.RivalExtra[BudgetState.Key(ChannelDefinition.Rua, contested)] = (double)verba * 3;
        Assert.True(engine.ForecastChannel(ChannelDefinition.Rua, contested, verba).Line("effect")!.Expected < before);
        Assert.Equal(other, engine.ForecastChannel(ChannelDefinition.Material, contested, verba).Line("effect")!.Expected, 9);
        Assert.Equal(alone.Line("effect")!.Expected, engine.ForecastChannel(ChannelDefinition.Rua, free, verba).Line("effect")!.Expected, 9);

        // A previsão mostra as duas contas.
        Assert.Contains(shared.Explanation, line => line.Contains('√'));
        Assert.Contains(shared.Explanation, line => line.Contains("rivais", StringComparison.Ordinal));
        Assert.All(shared.Explanation, line => Assert.DoesNotContain("{", line));
    }

    [Fact]
    public void Rivais_gastam_nas_zonas_alvo_do_plano_de_cada_um()
    {
        CampaignEngine engine = InCampaign("grande", 33);
        StaffTests.Week(engine);
        CampaignState campaign = engine.Context.State.Campaign!;
        List<RivalRecord> spending = campaign.Rivals.Records
            .Where(r => campaign.Find(r.CandidateId)!.Office == campaign.Player.Office && r.Budget.Any(a => a.ChannelId == ChannelDefinition.Rua)).ToList();
        var targets = spending.SelectMany(r => r.Plan!.TargetZones).Select(z => engine.Territory.Zones[z]).ToHashSet();
        Assert.NotEmpty(targets);
        Assert.All(engine.Territory.Zones, z => Assert.Equal(targets.Contains(z), engine.RivalBudget(ChannelDefinition.Rua, z) > 0));

        // O que cada rival põe na rua sai do caixa dele, e a soma por zona é o que o jogador enfrenta.
        Assert.All(spending, r => Assert.True(r.Spent > 0));
        string zone = targets.First();
        Assert.Equal(Math.Round(spending.Sum(r => r.Budget.Where(a => a.ChannelId == ChannelDefinition.Rua && a.TargetId == zone).Sum(a => a.Amount))), engine.RivalBudget(ChannelDefinition.Rua, zone));
    }

    [Fact]
    public void Na_pre_campanha_so_redes_sem_impulsionamento_e_material_institucional()
    {
        CampaignEngine engine = Fresh("medio", 34);
        Assert.Equal(CampaignPhase.PreCampanha, engine.Context.State.Campaign!.Phase);
        string zone = engine.Territory.Zones[0];
        string band = engine.ChannelTargets(ChannelDefinition.Redes)[0].Id;
        Assert.Equal(CommandFailure.CanalFechado, engine.CanAllocate(ChannelDefinition.Rua, zone, engine.ChannelStep));
        Assert.Equal(CommandFailure.CanalFechado, engine.CanAllocate(ChannelDefinition.Cabos, zone, engine.CabosGroupCost));
        Assert.Null(engine.PreCampaignCap(ChannelDefinition.Rua));

        decimal material = engine.PreCampaignCap(ChannelDefinition.Material)!.Value;
        decimal redes = engine.PreCampaignCap(ChannelDefinition.Redes)!.Value;
        Assert.Null(engine.CanAllocate(ChannelDefinition.Material, zone, material));
        Assert.Equal(CommandFailure.CanalFechado, engine.CanAllocate(ChannelDefinition.Material, zone, material * 2));
        Assert.Null(engine.CanAllocate(ChannelDefinition.Redes, band, redes));
        Assert.Contains(engine.ForecastChannel(ChannelDefinition.Redes, band, redes).Factors, f => f.Id == "pre_campanha" && f.Multiplier < 1);

        // Os canais abrem em 16 de agosto: na véspera a Rua ainda está fechada.
        DateOnly opening = new(engine.Context.State.Campaign.ElectionDate.Year, 8, 16);
        Assert.Equal(opening, engine.Context.State.Campaign.CampaignStart);
        while (engine.Today < opening.AddDays(-1))
        {
            StaffTests.Quiet(engine);
            engine.AdvanceDay();
        }

        Assert.Equal(CommandFailure.CanalFechado, engine.CanAllocate(ChannelDefinition.Rua, zone, engine.ChannelStep));
        ToCampaign(engine);
        Assert.Equal(opening, engine.Today);
        Assert.Null(engine.CanAllocate(ChannelDefinition.Rua, zone, engine.ChannelStep));
        Assert.Null(engine.CanAllocate(ChannelDefinition.Cabos, zone, engine.CabosGroupCost));
        Assert.Null(engine.CanAllocate(ChannelDefinition.Material, zone, material * 2));
        Assert.Null(engine.CanAllocate(ChannelDefinition.Redes, band, redes * 10));
        Assert.DoesNotContain(engine.ForecastChannel(ChannelDefinition.Redes, band, redes).Factors, f => f.Id == "pre_campanha");
    }

    [Fact]
    public void Regras_do_impulsionamento_ficam_em_objeto_de_configuracao()
    {
        ChannelLawConfig law = ChannelLawConfig.Default;
        Assert.Contains("57-C", law.ArtigoImpulsionamento);
        Assert.True(law.ImpulsionamentoSoNaCampanha);
        Assert.True(law.ImpulsionamentoSoParaPromover);
        Assert.True(law.ImpulsionamentoIdentificadoEContratadoPelaCampanha);

        // Conferido no texto oficial na R3 (docs/referencias/LEIS_CONFERIDAS.md), e o objeto diz isso.
        Assert.True(law.ConferidoNoTextoOficial);
        Assert.Contains("16/11/2026", law.Conferencia);
        Assert.True(law.ImpulsionamentoProvedorNoPais);
        Assert.Equal(5_000m, law.ImpulsionamentoMultaMinima);
        Assert.Equal(30_000m, law.ImpulsionamentoMultaMaxima);
        Assert.Equal(80_000m, law.ImpulsionamentoMultaMaximaPara(40_000m));

        // Com o padrão da lei, impulsionar (verba acima da produção) antes da campanha oficial é barrado.
        CampaignEngine engine = Fresh("medio", 35);
        string band = engine.ChannelTargets(ChannelDefinition.Redes)[0].Id;
        decimal boosted = engine.PreCampaignCap(ChannelDefinition.Redes)!.Value * 4;
        Assert.Equal(CommandFailure.LimiteLegal, engine.CanAllocate(ChannelDefinition.Redes, band, boosted));
        Assert.Equal(CommandFailure.LimiteLegal, engine.SetAllocation(ChannelDefinition.Redes, band, boosted));
        Assert.Equal(CommandFailure.LimiteLegal, engine.ForecastChannel(ChannelDefinition.Redes, band, boosted).Blocked);
        Assert.Empty(engine.Budget.Plan);

        // Trocando a regra no objeto de configuração, o mesmo comando passa: a regra não está no código do canal.
        var relaxed = new CampaignEngine(CampaignKit.Context(engine.Context.State, new ChannelLawConfig { ImpulsionamentoSoNaCampanha = false }));
        Assert.Null(relaxed.CanAllocate(ChannelDefinition.Redes, band, boosted));

        // O canal só promove o próprio candidato: os alvos são públicos (faixas de idade), nunca um adversário.
        Assert.All(engine.ChannelTargets(ChannelDefinition.Redes), t => Assert.Null(engine.Context.State.Campaign!.Find(t.Id)));
    }

    [Theory]
    [InlineData(Office.Prefeito, 8_000, null, 80)]
    [InlineData(Office.Prefeito, 30_000, null, 300)]
    [InlineData(Office.Prefeito, 30_499, null, 300)]
    [InlineData(Office.Prefeito, 30_500, null, 301)]
    [InlineData(Office.Prefeito, 8_050, null, 81)]
    [InlineData(Office.Vereador, 8_100, null, 41)]
    [InlineData(Office.Prefeito, 100_000, null, 370)]
    [InlineData(Office.Vereador, 8_000, null, 40)]
    [InlineData(Office.Vereador, 100_000, null, 185)]
    [InlineData(Office.Vereador, 100_000, 1_000_000L, 185)]
    [InlineData(Office.Vereador, 1_000_000, 1_000_000L, 356)]
    public void Limite_de_cabos_pelo_eleitorado_segue_o_artigo_100A(Office office, long eleitorado, long? maiorDaUf, int expected)
    {
        ChannelLawConfig law = ChannelLawConfig.Default;
        Assert.Contains("100-A", law.ArtigoCabos);
        Assert.Equal(expected, law.LimiteCabos(office, eleitorado, maiorDaUf));

        // Os números vêm do objeto: outra regra, outro limite.
        var half = new ChannelLawConfig { FracaoAteFaixaBase = 0.005 };
        Assert.True(half.LimiteCabos(office, eleitorado, maiorDaUf) < expected);
    }

    [Fact]
    public void Contratar_cabos_acima_do_limite_legal_e_barrado()
    {
        foreach (string porte in new[] { "pequeno", "medio", "grande" })
        {
            CampaignEngine engine = InCampaign(porte, 36);
            var m = engine.Context.Municipio;
            int limit = engine.CabosLimit;
            Assert.Equal(ChannelLawConfig.Default.LimiteCabos(Office.Vereador, m.Eleitorado, engine.Context.MaiorEleitoradoUf), limit);
            int perGroup = engine.Context.Campaign.Channels.Regras.CabosPorGrupo;
            int groups = limit / perGroup;
            string a = engine.Territory.Zones[0];
            string b = engine.Territory.Zones[^1];

            // Tudo numa zona: até o limite passa, um grupo a mais não.
            if (groups > 0)
            {
                Assert.Null(engine.CanAllocate(ChannelDefinition.Cabos, a, groups * engine.CabosGroupCost));
            }

            Assert.Equal(CommandFailure.LimiteLegal, engine.CanAllocate(ChannelDefinition.Cabos, a, (groups + 1) * engine.CabosGroupCost));

            // O limite é da candidatura: soma as zonas.
            if (groups >= 2 && a != b)
            {
                Assert.Null(engine.SetAllocation(ChannelDefinition.Cabos, a, (groups - 1) * engine.CabosGroupCost));
                Assert.Null(engine.CanAllocate(ChannelDefinition.Cabos, b, engine.CabosGroupCost));
                Assert.Equal(CommandFailure.LimiteLegal, engine.CanAllocate(ChannelDefinition.Cabos, b, 2 * engine.CabosGroupCost));
                Assert.Equal((groups - 1) * perGroup, engine.CabosPlanned);
                CommandForecast forecast = engine.ForecastChannel(ChannelDefinition.Cabos, b, engine.CabosGroupCost);
                Assert.Equal(perGroup, forecast.Line("people")!.Expected);
                Assert.Contains(forecast.Explanation, line => line.Contains(limit.ToString(System.Globalization.CultureInfo.GetCultureInfo("pt-BR")), StringComparison.Ordinal));
            }
        }
    }

    [Fact]
    public void Cabos_sao_contratados_em_grupos_e_ocupam_uma_vaga_da_equipe()
    {
        CampaignEngine engine = InCampaign("medio", 37);
        string zone = engine.Territory.Zones[0];
        decimal group = engine.CabosGroupCost;
        Assert.Equal(group * 2, engine.NormalizeAllocation(ChannelDefinition.Cabos, (group * 2) + (group / 2)));
        Assert.Equal(0, engine.TeamSlotsUsed);
        Assert.Null(engine.SetAllocation(ChannelDefinition.Cabos, zone, (group * 2) + (group / 2)));
        Assert.Equal(group * 2, engine.Budget.PlanOf(ChannelDefinition.Cabos, zone));
        Assert.True(engine.CabosNetworkActive);
        Assert.Equal(1, engine.TeamSlotsUsed);

        // Com a equipe cheia, não cabe montar a rede de cabos.
        CampaignEngine full = InCampaign("pequeno", 37);
        int hired = 0;
        foreach (StaffRole role in full.Context.Campaign.Staff.Funcoes.Take(full.TeamSlots))
        {
            StaffTests.HireFirst(full, role.Id);
            hired++;
        }

        Assert.Equal(full.TeamSlots, hired);
        if (full.CabosLimit >= full.Context.Campaign.Channels.Regras.CabosPorGrupo)
        {
            Assert.Equal(CommandFailure.SemVaga, full.CanAllocate(ChannelDefinition.Cabos, full.Territory.Zones[0], full.CabosGroupCost));
        }
    }

    [Fact]
    public void Nenhum_gasto_possivel_com_radio_TV_outdoor_ou_brinde()
    {
        ChannelLawConfig law = ChannelLawConfig.Default;
        ChannelCatalog catalog = CampaignKit.C.Campaign.Channels;
        string[] forbidden = [ChannelLawConfig.Radio, ChannelLawConfig.Tv, ChannelLawConfig.Outdoor, ChannelLawConfig.Brinde];
        Assert.Equal(forbidden.Order(), law.GastosVedados.Keys.Order());
        Assert.Contains("art. 44", law.GastosVedados[ChannelLawConfig.Radio]);
        Assert.Contains("art. 44", law.GastosVedados[ChannelLawConfig.Tv]);
        Assert.Contains("art. 39", law.GastosVedados[ChannelLawConfig.Outdoor]);
        Assert.Contains("art. 39", law.GastosVedados[ChannelLawConfig.Brinde]);

        // Os canais são só os quatro pagos; nenhum compromisso da agenda é um desses gastos.
        Assert.Equal(new[] { "cabos", "material", "redes", "rua" }, catalog.Canais.Select(c => c.Id).Order());
        Assert.DoesNotContain(CampaignKit.C.Campaign.Commitments.Tipos, t => forbidden.Contains(t.Id));

        CampaignEngine engine = InCampaign("medio", 38);
        string zone = engine.Territory.Zones[0];
        decimal money = engine.Context.State.Player.Resources.Money;
        foreach (string kind in forbidden)
        {
            Assert.False(law.GastoPermitido(kind));
            Assert.Empty(engine.ChannelTargets(kind));
            Assert.Equal(CommandFailure.GastoVedado, engine.CanAllocate(kind, zone, 1000));
            Assert.Equal(CommandFailure.GastoVedado, engine.SetAllocation(kind, zone, 1000));
            CommandForecast forecast = engine.ForecastChannel(kind, zone, 1000);
            Assert.Equal(CommandFailure.GastoVedado, forecast.Blocked);
            Assert.Contains(forecast.Explanation, line => line.Contains("Lei 9.504", StringComparison.Ordinal));
        }

        Assert.Equal(money, engine.Context.State.Player.Resources.Money);
        Assert.Empty(engine.Budget.Plan);

        // E o conteúdo não aceita um canal desses: o validador de dados reprova.
        string json = File.ReadAllText(Path.Combine(TestContent.DataDirectory, ChannelCatalog.File));
        foreach (string kind in forbidden)
        {
            var result = JsonData.Parse<ChannelCatalog>(json.Replace("\"id\": \"rua\"", $"\"id\": \"{kind}\"", StringComparison.Ordinal), "channels.json");
            Assert.Contains(result.Errors, e => e.Contains("vedado", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Comicio_exige_verba_de_Rua_na_zona_e_a_Rua_reforca_a_caminhada()
    {
        CampaignEngine engine = InCampaign("medio", 39);
        string zone = engine.Territory.Zones[engine.Territory.HomeZone];
        var rally = new CommitmentRequest(CommitmentCatalog.Comicio, zone, engine.Today, Turno.Manha);
        var walk = new CommitmentRequest("caminhada", zone, engine.Today.AddDays(1), Turno.Manha);
        Assert.Equal(ScheduleFailure.SemVerbaDeRua, engine.CanSchedule(rally));
        Assert.DoesNotContain(engine.Forecast(walk).Factors, f => f.Id == "verba_rua");

        // Verba abaixo do mínimo não basta; no mínimo, o comício pode ser marcado.
        Assert.Null(engine.SetAllocation(ChannelDefinition.Rua, zone, engine.ComicioRuaMinimum - engine.ChannelStep));
        Assert.Equal(ScheduleFailure.SemVerbaDeRua, engine.CanSchedule(rally));
        Assert.Null(engine.SetAllocation(ChannelDefinition.Rua, zone, engine.ComicioRuaMinimum));
        Assert.Null(engine.CanSchedule(rally));
        Assert.Equal(ScheduleFailure.SemVerbaDeRua, engine.CanSchedule(rally with { ZoneId = engine.Territory.Zones.First(z => z != zone) }));

        YieldFactor bonus = engine.Forecast(walk).Factors.Single(f => f.Id == "verba_rua");
        Assert.InRange(bonus.Multiplier, 1.0001, 1 + engine.Context.Campaign.Channels.Regras.RuaBonusAgenda);
        Assert.Contains(engine.Forecast(rally).Factors, f => f.Id == "verba_rua");
        Assert.Null(engine.Schedule(rally).Failure);
        CommitmentResult result = Assert.Single(engine.RunToday());
        Assert.DoesNotContain(result.Factors, f => f.Id == "sem_estrutura");
        Assert.Equal(0, result.Money);
    }

    [Fact]
    public void Cabos_seguram_a_presenca_e_rendem_metade_sem_material_grafico()
    {
        CampaignEngine with = InCampaign("medio", 40);
        CampaignEngine without = InCampaign("medio", 40);
        string zone = with.Territory.Zones[with.Territory.HomeZone];
        int z = with.Territory.IndexOf(zone);
        ChannelRules rules = with.Context.Campaign.Channels.Regras;
        decimal verba = with.CabosGroupCost;

        CommandForecast alone = with.ForecastChannel(ChannelDefinition.Cabos, zone, verba);
        Assert.Equal(rules.CabosSemMaterial, alone.Factors.Single(f => f.Id == "sem_material").Multiplier);
        Assert.Null(with.SetAllocation(ChannelDefinition.Material, zone, with.ChannelStep));
        CommandForecast paired = with.ForecastChannel(ChannelDefinition.Cabos, zone, verba);
        Assert.DoesNotContain(paired.Factors, f => f.Id == "sem_material");
        string group = alone.Lines.First(l => l.Kind == "support").Target!;
        Assert.Equal(paired.Line("support", group)!.Expected * rules.CabosSemMaterial, alone.Line("support", group)!.Expected, 9);

        // Semana sem compromisso: a presença cai, mas só a metade onde há cabos pagos. (O material sai para não render presença.)
        Assert.Null(with.SetAllocation(ChannelDefinition.Material, zone, 0));
        with.Budget.Active.Clear();
        with.Budget.Plan.Clear();
        Assert.Null(with.SetAllocation(ChannelDefinition.Cabos, zone, verba));
        double start = with.Territory.Presence[RaceSetup.PlayerId][z];
        Assert.Equal(start, without.Territory.Presence[RaceSetup.PlayerId][z], 9);
        StaffTests.Week(with);
        StaffTests.Week(without);
        double gained = with.Budget.LastResults.Single(r => r.ChannelId == ChannelDefinition.Cabos).Presence.GetValueOrDefault(zone);
        double held = start + gained - with.Territory.Presence[RaceSetup.PlayerId][z];
        double lost = start - without.Territory.Presence[RaceSetup.PlayerId][z];
        Assert.True(lost > 0);
        Assert.Equal(lost * rules.CabosFatorDecaimento, held, 6);
    }

    [Fact]
    public void Previsao_de_cada_canal_tem_faixa_e_explicacao_e_o_resultado_cai_nela()
    {
        foreach (ulong seed in new ulong[] { 41, 42, 43 })
        {
            CampaignEngine engine = InCampaign("medio", seed);
            StaffTests.HireFirst(engine, StaffRole.SocialMedia);
            var plan = new List<(string Channel, string Target, decimal Amount)>
            {
                (ChannelDefinition.Rua, engine.Territory.Zones[0], engine.ChannelStep * 8),
                (ChannelDefinition.Material, engine.Territory.Zones[1], engine.ChannelStep * 5),
                (ChannelDefinition.Cabos, engine.Territory.Zones[2 % engine.Territory.Zones.Count], engine.CabosGroupCost),
                (ChannelDefinition.Redes, engine.ChannelTargets(ChannelDefinition.Redes)[0].Id, engine.ChannelStep * 6),
            };
            foreach (var (channel, target, amount) in plan)
            {
                Assert.Null(engine.SetAllocation(channel, target, amount));
            }

            var forecasts = plan.ToDictionary(p => p.Channel, p => engine.ForecastChannel(p.Channel, p.Target, p.Amount));
            Assert.All(forecasts.Values, f =>
            {
                Assert.Null(f.Blocked);
                Assert.NotEmpty(f.Explanation);
                Assert.All(f.Explanation, line => Assert.DoesNotContain("{", line));
                Assert.Contains(f.Lines, l => l.Kind is "presence" or "support" && l.Max > l.Min);
            });

            StaffTests.Week(engine);
            Assert.Equal(plan.Count, engine.Budget.LastResults.Count);
            foreach (ChannelResult result in engine.Budget.LastResults)
            {
                CommandForecast f = forecasts[result.ChannelId];
                Assert.Equal(f.Line("effect")!.Expected, result.Effect, 9);
                Assert.Contains(result.Factors, x => x.Id == "sorte");
                foreach (var (group, gain) in result.Support)
                {
                    Assert.True(f.Contains("support", group, gain), $"{result.ChannelId}: apoio de {group} fora da faixa.");
                }

                if (result.ChannelId == ChannelDefinition.Redes)
                {
                    double pop = engine.Territory.Population.Sum(x => (double)x);
                    double city = result.Presence.Sum(p => p.Value * engine.Territory.Population[engine.Territory.IndexOf(p.Key)]) / pop;
                    Assert.True(f.Contains("presence", null, city), "Redes: presença na cidade fora da faixa.");
                }
                else if (result.Presence.Count > 0)
                {
                    Assert.True(f.Contains("presence", result.TargetId, result.Presence[result.TargetId]), $"{result.ChannelId}: presença fora da faixa.");
                }
            }
        }
    }

    [Fact]
    public void A_verba_e_cobrada_toda_segunda_e_o_que_o_caixa_nao_paga_nao_rende()
    {
        CampaignEngine engine = InCampaign("medio", 44);
        GameState state = engine.Context.State;
        string zone = engine.Territory.Zones[0];
        decimal verba = engine.ChannelStep * 10;
        decimal money = state.Player.Resources.Money;
        decimal raised = engine.Finance.Declared;

        // O aumento sai na hora; baixar o plano não devolve nada e só vale na segunda.
        Assert.Null(engine.SetAllocation(ChannelDefinition.Rua, zone, verba));
        Assert.Equal(money - verba, state.Player.Resources.Money);
        Assert.Null(engine.SetAllocation(ChannelDefinition.Rua, zone, verba / 2));
        Assert.Equal(money - verba, state.Player.Resources.Money);
        Assert.Equal(verba, engine.Budget.ActiveOf(ChannelDefinition.Rua, zone));
        Assert.Equal(verba / 2, engine.Budget.PlanOf(ChannelDefinition.Rua, zone));

        StaffTests.Week(engine);
        Assert.Equal(verba, Assert.Single(engine.Budget.LastResults).Spent);
        Assert.Equal(verba / 2, engine.Budget.ActiveOf(ChannelDefinition.Rua, zone));
        Assert.Equal(money - verba - (verba / 2) + engine.Finance.Declared - raised, state.Player.Resources.Money);
        BudgetView view = engine.BudgetSummary();
        Assert.Equal(verba / 2, view.PlanWeekly);
        Assert.Equal(verba / 2, view.ActiveWeekly);

        // Sem caixa na segunda: a verba não é paga, não rende, e o feed avisa.
        state.Player.Resources.Money = verba / 4;
        raised = engine.Finance.Declared;
        StaffTests.Week(engine);
        Assert.Equal(0, engine.Budget.ActiveOf(ChannelDefinition.Rua, zone));
        Assert.Equal((verba / 4) + engine.Finance.Declared - raised, state.Player.Resources.Money);
        Assert.Contains(engine.Agenda.Feed, f => f.Kind == "verba");
        StaffTests.Week(engine);
        Assert.Empty(engine.Budget.LastResults);
        Assert.Equal(CommandFailure.SemDinheiro, engine.CanAllocate(ChannelDefinition.Rua, zone, verba));
    }

    [Fact]
    public void Verba_repetida_no_mesmo_canal_e_alvo_satura_e_recupera_com_o_tempo()
    {
        // Na pré-campanha (há semanas de sobra para ver a saturação subir e passar), com material institucional.
        CampaignEngine engine = Fresh("medio", 47);
        ChannelRules rules = engine.Context.Campaign.Channels.Regras;
        string zone = engine.Territory.Zones[0];
        string other = engine.Territory.Zones[1];
        decimal verba = engine.PreCampaignCap(ChannelDefinition.Material)!.Value;
        string band = engine.ChannelTargets(ChannelDefinition.Redes)[0].Id;
        double Saturation(string channel, string target) =>
            engine.ForecastChannel(channel, target, verba).Factors.FirstOrDefault(f => f.Id == "saturacao")?.Multiplier ?? 1;

        Assert.Equal(1, Saturation(ChannelDefinition.Material, zone));
        Assert.Null(engine.SetAllocation(ChannelDefinition.Material, zone, verba));
        StaffTests.Week(engine);

        // Uma semana de verba: a próxima no mesmo canal e alvo rende menos, e a previsão mostra o fator (princípio g).
        double once = 1 / (1 + rules.SaturacaoFator);
        Assert.Equal(once, Saturation(ChannelDefinition.Material, zone), 9);
        Assert.Equal(once, engine.ChannelSaturationYield(ChannelDefinition.Material, zone), 9);
        Assert.Equal(1, Saturation(ChannelDefinition.Redes, band));
        Assert.Equal(1, Saturation(ChannelDefinition.Material, other));

        // Segunda semana seguida: satura mais. E o resultado traz o fator com que a verba rendeu.
        StaffTests.Week(engine);
        Assert.Contains(Assert.Single(engine.Budget.LastResults).Factors, f => f.Id == "saturacao" && Math.Abs(f.Multiplier - once) < 1e-9);
        double twice = 1 / (1 + (rules.SaturacaoFator * (2 - rules.SaturacaoDecaimentoSemanal)));
        Assert.Equal(twice, Saturation(ChannelDefinition.Material, zone), 9);
        Assert.True(twice < once);

        // Sem verba ali, a saturação passa.
        Assert.Null(engine.SetAllocation(ChannelDefinition.Material, zone, 0));
        StaffTests.Week(engine);
        int guard = 0;
        while (Saturation(ChannelDefinition.Material, zone) < 1)
        {
            Assert.True(++guard < 20);
            StaffTests.Week(engine);
        }

        Assert.Empty(engine.Budget.Saturation);
    }

    [Fact]
    public void Cabos_dispensados_vao_para_um_rival_da_zona()
    {
        CampaignEngine engine = InCampaign("medio", 45);
        string zone = engine.Territory.Zones[0];
        decimal verba = engine.CabosGroupCost * 2;
        Assert.Null(engine.SetAllocation(ChannelDefinition.Cabos, zone, verba));
        StaffTests.Week(engine);
        decimal rival = engine.RivalBudget(ChannelDefinition.Cabos, zone);
        Assert.Null(engine.SetAllocation(ChannelDefinition.Cabos, zone, 0));
        StaffTests.Week(engine);
        Assert.True(engine.RivalBudget(ChannelDefinition.Cabos, zone) > rival, "Os cabos dispensados engrossam a verba de um rival na zona.");
        Assert.Contains(engine.Agenda.Feed, f => f.Kind == "cabos" && f.ZoneId == zone);

        // E esfria com o tempo.
        decimal boosted = engine.RivalBudget(ChannelDefinition.Cabos, zone);
        StaffTests.Week(engine);
        Assert.True(engine.RivalBudget(ChannelDefinition.Cabos, zone) < boosted);
    }

    [Fact]
    public void Verba_vai_e_volta_pelo_save_e_as_faixas_de_idade_existem_no_eleitorado()
    {
        var groups = CampaignKit.C.Electorate.Segments.Dimensions.Single(d => d.Id == "idade").Groups.Select(g => g.Id).Order().ToList();
        Assert.Equal(groups, CampaignKit.C.Campaign.Channels.FaixasIdade.SelectMany(b => b.Grupos).Order().ToList());

        CampaignEngine engine = InCampaign("medio", 46);
        Assert.Null(engine.SetAllocation(ChannelDefinition.Rua, engine.Territory.Zones[0], engine.ChannelStep * 4));
        Assert.Null(engine.SetAllocation(ChannelDefinition.Redes, engine.ChannelTargets(ChannelDefinition.Redes)[1].Id, engine.ChannelStep * 4));
        StaffTests.Week(engine);
        string json = GameStateSerializer.Serialize(engine.Context.State);
        GameState loaded = GameStateSerializer.Deserialize(json);
        Assert.Equal(json, GameStateSerializer.Serialize(loaded));
        Assert.Equal(2, loaded.Campaign!.Budget.Active.Count);
        Assert.Equal(2, loaded.Campaign.Budget.LastResults.Count);
    }
}
