using Planalto.Core.Campaign;
using Planalto.Core.Municipios;
using Planalto.Core.People;
using Planalto.Core.Save;
using Planalto.Core.State;
using Planalto.Core.Territory;
using Planalto.Core.Tests.Campaign;

namespace Planalto.Core.Tests.Territory;

/// <summary>
/// Com a zona rural dividida pelos distritos do IBGE, tudo o que a carreira guarda por zona aponta para zonas que
/// existem nos dados: lideranças, presença, promessas e dívidas do Livro, eventos da cidade e redutos dos rivais, em
/// mundo novo e depois de semanas de campanha, em três cidades pequenas com distritos de regiões diferentes.
/// </summary>
public class ZoneIntegrityTests
{
    /// <summary>Piratini (RS), Solonópole (CE) e Alvorada D'Oeste (RO).</summary>
    public static TheoryData<int> CidadesComDistritos => new() { 4314605, 2313005, 1100346 };

    [Theory]
    [MemberData(nameof(CidadesComDistritos))]
    public void Mundo_novo_e_campanha_apontam_so_para_zonas_que_existem(int ibge)
    {
        Municipio m = CampaignKit.Municipio(ibge);
        Assert.Equal("pequeno", m.Porte);
        ZonasMunicipio zonas = CampaignKit.Zonas(m);
        var ids = zonas.Zonas.Select(z => z.Id).ToList();
        var idSet = ids.ToHashSet(StringComparer.Ordinal);
        Assert.True(zonas.Zonas.Count(z => z.IsRural && z.NomeFonte == Zona.NomeDistrito) >= 2, $"{m.Nome}: zona rural sem divisão por distrito");

        CampaignEngine engine = CampaignKit.NewCareer(ibge, 41);
        GameState state = engine.Context.State;
        AssertAll(state, ids, idSet, played: false);

        var robot = new CampaignRobot(engine, 41, new RobotOptions { R2b = true });
        // Os eventos da cidade só existem na semana: conferidos semana a semana.
        int cityEvents = 0;
        for (int week = 0; week < 6; week++)
        {
            robot.PlayWeek();
            cityEvents += state.Campaign!.Agenda.CityEvents.Count;
            Assert.All(state.Campaign.Agenda.CityEvents, e => Assert.Contains(e.ZoneId, idSet));
        }

        Assert.True(cityEvents > 0, "nenhum evento da cidade em seis semanas");

        // Promessa a uma zona rural de distrito pelo comando do jogo (o robô nem sempre promete em seis semanas).
        string distrito = zonas.Zonas.First(z => z.IsRural && z.NomeFonte == Zona.NomeDistrito).Id;
        PromiseOption option = engine.PromiseOptions(Promise.ParaZona, distrito).First(o => o.Blocked is null);
        Assert.Null(engine.MakePromise(option.Definition.Id, Promise.ParaZona, distrito).Failure);
        Assert.Contains(state.Ledger.Promises, p => p.ToId == distrito);

        AssertAll(state, ids, idSet, played: true);

        // O mesmo estado, salvo e carregado, continua abrindo.
        GameState loaded = GameStateSerializer.Deserialize(GameStateSerializer.Serialize(state));
        Assert.Empty(SaveZoneCheck.Problems(loaded, code => code == ibge ? ids : null));
    }

    private static void AssertAll(GameState state, List<string> ids, HashSet<string> idSet, bool played)
    {
        CampaignState campaign = state.Campaign!;
        TerritoryState territory = campaign.Territory;

        // Presença por zona: a lista do território é a dos dados, na mesma ordem, e os vetores têm uma posição por zona.
        Assert.Equal(ids, territory.Zones);
        Assert.All(territory.Presence.Values, p => Assert.Equal(ids.Count, p.Length));
        Assert.InRange(territory.HomeZone, 0, ids.Count - 1);

        // Lideranças.
        List<Leader> leaders = state.City!.Leaders;
        Assert.NotEmpty(leaders);
        Assert.All(leaders, l => Assert.All(l.Zones, z => Assert.Contains(z, idSet)));

        // Redutos dos rivais (posições na lista do território).
        Assert.NotEmpty(territory.Redutos);
        Assert.All(territory.Redutos.Values.SelectMany(r => r), i => Assert.InRange(i, 0, ids.Count - 1));

        // Eventos da cidade.
        Assert.All(campaign.Agenda.CityEvents, e => Assert.Contains(e.ZoneId, idSet));
        Assert.All(campaign.Events.Pending.Select(p => p.ZoneId).OfType<string>(), z => Assert.Contains(z, idSet));

        // Promessas e dívidas do Livro: a zona prometida existe; a dívida é com uma pessoa da cidade, e a liderança
        // que a cobra está em zonas que existem.
        foreach (Promise p in state.Ledger.Promises)
        {
            if (p.ZoneId is not null)
            {
                Assert.Contains(p.ZoneId, idSet);
            }

            if (p.ToKind == Promise.ParaZona)
            {
                Assert.Contains(p.ToId, idSet);
            }
        }

        foreach (Debt d in state.Ledger.Debts)
        {
            Assert.Contains(state.City.People, person => person.Id == d.ToPersonId);
            Assert.All(leaders.Where(l => l.PersonId == d.ToPersonId).SelectMany(l => l.Zones), z => Assert.Contains(z, idSet));
        }

        if (played)
        {
            Assert.NotEmpty(state.Ledger.Promises.Concat<object>(state.Ledger.Debts));
        }

        // E a conferência geral do save: todo id de zona e toda posição de zona em qualquer lugar do estado.
        Assert.Empty(SaveZoneCheck.Problems(state, code => code == state.FocusMunicipio ? ids : null));
    }
}
