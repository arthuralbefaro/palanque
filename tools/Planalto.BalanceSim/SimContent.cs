using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Electorate;
using Planalto.Core.Events;
using Planalto.Core.Municipios;
using Planalto.Core.Scenarios;
using Planalto.Core.State;
using Planalto.Core.World;

namespace Planalto.BalanceSim;

/// <summary>Conteúdo de data/ carregado uma vez, e a montagem de uma carreira nova para o robô jogar.</summary>
public sealed class SimContent
{
    public SimContent(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        Portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        Municipios = MunicipiosDataSet.Load(dataDirectory, Portes).DataSet ?? throw new InvalidOperationException("data/municipios inválido.");
        World = WorldContent.LoadOrThrow(dataDirectory);
        People = PeopleContent.LoadOrThrow(dataDirectory);
        ElectorateContent.Load(dataDirectory, World.Parties.Themes, Municipios, out ElectorateContent? electorate);
        Electorate = electorate ?? throw new InvalidOperationException("data/electorate inválido.");
        Campaign = CampaignContent.LoadOrThrow(dataDirectory);
        Events = EventContent.LoadOrThrow(dataDirectory);
        Brasil2026 = Brasil2026Scenario.Load(dataDirectory, Municipios, World.Parties).Scenario ?? throw new InvalidOperationException("cenário Brasil 2026 inválido.");
        Projection = PopulationProjection.Load(dataDirectory, Municipios);
    }

    public string DataDirectory { get; }

    public PorteCatalog Portes { get; }

    public MunicipiosDataSet Municipios { get; }

    public WorldContent World { get; }

    public PeopleContent People { get; }

    public ElectorateContent Electorate { get; }

    public CampaignContent Campaign { get; }

    public EventContent Events { get; }

    public Brasil2026Scenario Brasil2026 { get; }

    public PopulationProjection Projection { get; }

    /// <summary>Carreira nova no município, filiada ao partido sorteado (ou ao mais forte da cidade), pronta para a campanha.</summary>
    public CampaignEngine NewCareer(Municipio m, ulong seed, bool strongestParty)
    {
        WorldSetup world = WorldSetup.Create(World, "brasil_2026", new Rng(seed)).Setup!;
        var rng = new Rng(seed + 1);
        string partyId = rng.Pick(world.Parties).Id;
        if (strongestParty)
        {
            var parties = CampaignContext.ElectoralParties(world.Parties.Select(GameParty.From), World.Parties.Generator.SizeShareWeights);
            partyId = CampaignContext.PartyStrength(seed, m.CodigoIbge, Electorate, parties, Brasil2026.Municipio(m.CodigoIbge)).MaxBy(s => s.Value).Key;
        }

        Character player = PersonGenerator.Generate(rng, People, new PersonRequest(RaceSetup.PlayerId, m.Uf, m.CodigoIbge, new DateOnly(world.Options.StartYear, 1, 1), PartyId: partyId));
        GameParty party = GameParty.From(world.Find(player.PartyId!)!);
        player.Campaign = new CampaignIdentity
        {
            BallotNumber = $"{party.Number}{rng.NextInt(1000):000}",
            Slogan = "Simulação",
            LogoIcon = "sol",
            LogoFont = "sans_forte",
            LogoPrimaryColor = party.Colors.Primary,
            LogoSecondaryColor = party.Colors.Secondary,
            Jingle = "forro",
            PhotoPose = "frente",
            PhotoBackground = "background_solido",
        };
        GameState state = NewGame.Create(world, player, seed);
        return CampaignEngine.Start(Context(state, m));
    }

    public CampaignContext Context(GameState state, Municipio m)
    {
        MunicipalScenario? local = state.World?.ScenarioId == "brasil_2026" ? Brasil2026.Municipio(m.CodigoIbge) : null;
        return new CampaignContext
        {
            State = state,
            Municipio = m,
            Local = local,
            Electorate = CampaignContext.BuildElectorate(state, m, Electorate, World.Parties.Themes, World.Parties.Generator.SizeShareWeights, local),
            People = People,
            ElectorateContent = Electorate,
            Campaign = Campaign,
            Themes = World.Parties.Themes,
            Events = Events,
            Portes = Portes,
            Projection = Projection,
            Zonas = CampaignContext.LoadZonas(DataDirectory, m),
        };
    }
}
