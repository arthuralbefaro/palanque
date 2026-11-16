using Planalto.Core.Campaign;
using Planalto.Core.Elections;
using Planalto.Core.Electorate;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.Core.Tests.Campaign;

namespace Planalto.Core.Tests.Territory;

/// <summary>Território na simulação (GDD 17.5): os segmentos repartidos pelas zonas e o voto por zona fecham com a cidade.</summary>
public class ZoneElectorateTests
{
    /// <summary>Dois municípios de cada porte, mais a maior metrópole e uma capital com zona rural sem moradores.</summary>
    public static TheoryData<int> Cities()
    {
        var all = CampaignKit.C.Municipios.All.Where(m => m.EleicoesMunicipais).OrderBy(m => m.CodigoIbge).ToList();
        var data = new TheoryData<int>();
        foreach (string porte in new[] { "pequeno", "medio", "grande", "metropole" })
        {
            var pool = all.Where(m => m.Porte == porte).ToList();
            data.Add(pool[0].CodigoIbge);
            data.Add(pool[pool.Count / 2].CodigoIbge);
        }

        data.Add(3550308);
        data.Add(2927408);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cities))]
    public void Soma_dos_eleitores_das_zonas_e_o_eleitorado_do_municipio(int ibge)
    {
        CampaignContext ctx = CampaignKit.NewCareer(ibge, 51).Context;
        IReadOnlyList<ZoneElectorate> zones = ctx.ZoneElectorates;
        double total = ctx.Electorate.Voters;

        Assert.Equal(ctx.Zonas.Zonas.Count, zones.Count);
        Assert.Equal(ctx.Municipio.Eleitorado, total, total * 1e-9);
        Assert.Equal(total, zones.Sum(z => z.Voters), total * 1e-9);
        Assert.All(zones, z => Assert.All(z.Segments, s => Assert.True(s.Voters > 0)));

        // Segmento a segmento: o que mora nas zonas é o que o município tem.
        foreach (Segment segment in ctx.Electorate.Segments)
        {
            double inZones = zones.Sum(z => z.Segments.Where(s => s.Id == segment.Id).Sum(s => s.Voters));
            Assert.Equal(segment.Voters, inZones, Math.Max(1e-6, segment.Voters * 1e-9));
        }

        // A repartição de partida pelos eleitores de cada zona também soma o eleitorado, e zona sem eleitor na
        // fonte (sem local de votação) fica sem segmento.
        double[] target = ZoneElectorateBuilder.ZoneVoters(ctx.Zonas.Zonas, total);
        Assert.Equal(total, target.Sum(), total * 1e-9);
        for (int z = 0; z < zones.Count; z++)
        {
            Assert.True(target[z] > 0 || zones[z].Voters == 0);
        }

        // Cada grupo (idade, renda, religião...) soma nas zonas o que tem na cidade.
        foreach (string group in ctx.ElectorateContent.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id))
        {
            double inCity = ctx.Electorate.Segments.Where(s => s.Has(group)).Sum(s => s.Voters);
            Assert.Equal(inCity, zones.Sum(z => z.VotersIn(group)), Math.Max(1e-6, inCity * 1e-9));
        }
    }

    [Theory]
    [MemberData(nameof(Cities))]
    [Trait("Categoria", "Lento")]
    public void Projecao_por_zona_somada_e_a_da_cidade(int ibge)
    {
        CampaignEngine engine = CampaignKit.NewCareer(ibge, 52);
        CampaignContext ctx = engine.Context;
        foreach (Office office in new[] { Office.Vereador, Office.Prefeito })
        {
            var (city, zones) = engine.ProjectByZone(office, noise: null);
            Assert.Equal(ctx.ZoneElectorates.Count, zones.Count);
            foreach (Candidate c in ctx.State.Campaign!.ForOffice(office))
            {
                Assert.Equal(city.Votes[c.Id], zones.Sum(z => z.Votes[c.Id]), Math.Max(1e-6, city.Votes[c.Id] * 1e-9));
            }

            Assert.Equal(city.BlankNull, zones.Sum(z => z.BlankNull), 1e-6);
            Assert.Equal(city.Abstention, zones.Sum(z => z.Abstention), 1e-6);
            string any = ctx.State.Campaign.ForOffice(office).First().Id;
            Assert.Equal(engine.Project(office, null).Votes[any], city.Votes[any], 1e-6);

            // Todo eleitor da cidade vota em alguém, anula ou falta.
            double accounted = city.Votes.Values.Sum() + city.BlankNull + city.Abstention;
            Assert.Equal(ctx.Electorate.Voters, accounted, ctx.Electorate.Voters * 1e-6);

            // Cada zona fecha com os próprios eleitores.
            for (int z = 0; z < zones.Count; z++)
            {
                double inZone = zones[z].Votes.Values.Sum() + zones[z].BlankNull + zones[z].Abstention;
                Assert.Equal(ctx.ZoneElectorates[z].Voters, inZone, Math.Max(1e-6, ctx.ZoneElectorates[z].Voters * 1e-6));
            }
        }
    }

    [Fact]
    public void Presenca_maior_numa_zona_rende_votos_nessa_zona_e_nao_nas_outras()
    {
        CampaignEngine engine = CampaignKit.NewCareer(3106200, 53);
        TerritoryState t = engine.Territory;
        int zone = Enumerable.Range(0, t.Zones.Count).First(z => z != t.HomeZone && engine.Context.ZoneElectorates[z].Voters > 0);
        var before = engine.ProjectByZone(Office.Vereador, null).Zones.Select(p => p.Votes[RaceSetup.PlayerId]).ToList();
        t.Presence[RaceSetup.PlayerId][zone] += 30;
        var after = engine.ProjectByZone(Office.Vereador, null).Zones.Select(p => p.Votes[RaceSetup.PlayerId]).ToList();

        Assert.True(after[zone] > before[zone], "Mais presença na zona tem de render votos nela.");
        for (int z = 0; z < before.Count; z++)
        {
            if (z != zone)
            {
                Assert.Equal(before[z], after[z], 1e-9);
            }
        }
    }

    [Fact]
    public void Reconhecimento_e_a_media_das_presencas_pela_populacao()
    {
        CampaignEngine engine = CampaignKit.NewCareer(4106902, 54);
        TerritoryState t = engine.Territory;
        CampaignState campaign = engine.Context.State.Campaign!;
        double pop = t.Population.Sum(p => (double)p);
        foreach (Candidate c in campaign.Candidates)
        {
            double mean = t.Presence[c.Id].Select((p, z) => p * t.Population[z]).Sum() / pop;
            Assert.Equal(mean, t.Recognition(c.Id), 9);
            Assert.Equal(mean, c.Recognition, 9);
        }

        // Um compromisso muda a presença de uma zona, e o reconhecimento acompanha a média.
        campaign.Events.Pending.Clear();
        string zone = t.Zones[(t.HomeZone + 1) % t.Zones.Count];
        Assert.Null(engine.Schedule(new CommitmentRequest("caminhada", zone, engine.Today, Turno.Manha)).Failure);
        CommitmentResult result = Assert.Single(engine.RunToday());
        Assert.True(result.Presence[zone] > 0);
        double now = t.Presence[RaceSetup.PlayerId].Select((p, z) => p * t.Population[z]).Sum() / pop;
        Assert.Equal(now, campaign.Player.Recognition, 9);

        // O jogador começa mais conhecido na zona em que mora; rival a vereador, nos seus redutos.
        double[] mine = t.Presence[RaceSetup.PlayerId];
        Assert.All(Enumerable.Range(0, mine.Length).Where(z => z != t.HomeZone && z != t.IndexOf(zone) && !engine.Context.Zonas.Zonas[z].Vizinhas.Contains(zone)), z => Assert.True(mine[t.HomeZone] >= mine[z]));
        Assert.NotEmpty(t.Redutos);
        Assert.All(t.Redutos, r => Assert.Contains(campaign.Candidates, c => c.Id == r.Key && !c.IsBulk && c.Office == Office.Vereador));
    }
}
