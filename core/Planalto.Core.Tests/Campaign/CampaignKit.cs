using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Electorate;
using Planalto.Core.Events;
using Planalto.Core.Municipios;
using Planalto.Core.Scenarios;
using Planalto.Core.State;
using Planalto.Core.Tests.Parties;
using Planalto.Core.World;

namespace Planalto.Core.Tests.Campaign;

/// <summary>Conteúdo real carregado uma vez e montagem de carreiras para os testes de campanha.</summary>
internal static class CampaignKit
{
    private static readonly Lazy<Loaded> Content = new(() => new Loaded());

    public static Loaded C => Content.Value;

    internal sealed class Loaded
    {
        public Loaded()
        {
            string dir = TestContent.DataDirectory;
            Portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dir, "portes.json")).GetValueOrThrow();
            Municipios = MunicipiosDataSet.Load(dir, Portes).DataSet!;
            World = TestContent.World;
            People = PeopleContent.LoadOrThrow(dir);
            ElectorateContent.Load(dir, World.Parties.Themes, Municipios, out ElectorateContent? electorate);
            Electorate = electorate!;
            Campaign = CampaignContent.LoadOrThrow(dir);
            Events = EventContent.LoadOrThrow(dir);
            Brasil2026 = Brasil2026Scenario.Load(dir, Municipios, World.Parties).Scenario!;
        }

        public PorteCatalog Portes { get; }

        public MunicipiosDataSet Municipios { get; }

        public WorldContent World { get; }

        public PeopleContent People { get; }

        public ElectorateContent Electorate { get; }

        public CampaignContent Campaign { get; }

        public EventContent Events { get; }

        public Brasil2026Scenario Brasil2026 { get; }
    }

    public static Municipio Municipio(int ibge) => C.Municipios.All.Single(m => m.CodigoIbge == ibge);

    /// <summary>Carreira nova no município, com a predefinição pedida, pronta para a campanha.</summary>
    /// <param name="strongestParty">Filia o jogador ao partido mais forte da cidade, como a aba Partido sugere.</param>
    public static CampaignEngine NewCareer(int ibge, ulong seed, string preset = "brasil_2026", bool strongestParty = false)
    {
        Municipio m = Municipio(ibge);
        WorldSetup world = WorldSetup.Create(C.World, preset, new Rng(seed)).Setup!;
        var rng = new Rng(seed + 1);
        string partyId = rng.Pick(world.Parties).Id;
        if (strongestParty)
        {
            var parties = CampaignContext.ElectoralParties(world.Parties.Select(GameParty.From), C.World.Parties.Generator.SizeShareWeights);
            MunicipalScenario? local = world.Preset.Scenario == "brasil_2026" ? C.Brasil2026.Municipio(ibge) : null;
            partyId = CampaignContext.PartyStrength(seed, ibge, C.Electorate, parties, local).MaxBy(s => s.Value).Key;
        }

        Character player = PersonGenerator.Generate(rng, C.People, new PersonRequest(RaceSetup.PlayerId, m.Uf, m.CodigoIbge, new DateOnly(world.Options.StartYear, 1, 1), PartyId: partyId));
        GameParty party = GameParty.From(world.Find(player.PartyId!)!);
        player.Campaign = new CampaignIdentity
        {
            BallotNumber = $"{party.Number}{rng.NextInt(1000):000}",
            Slogan = "Teste",
            LogoIcon = "sol",
            LogoFont = "sans_forte",
            LogoPrimaryColor = party.Colors.Primary,
            LogoSecondaryColor = party.Colors.Secondary,
            Jingle = "forro",
            PhotoPose = "frente",
            PhotoBackground = "background_solido",
        };
        GameState state = NewGame.Create(world, player, seed);
        return CampaignEngine.Start(Context(state));
    }

    public static CampaignContext Context(GameState state)
    {
        Municipio m = Municipio(state.FocusMunicipio);
        MunicipalScenario? local = state.World?.ScenarioId == "brasil_2026" ? C.Brasil2026.Municipio(m.CodigoIbge) : null;
        return new CampaignContext
        {
            State = state,
            Municipio = m,
            Local = local,
            Electorate = CampaignContext.BuildElectorate(state, m, C.Electorate, C.World.Parties.Themes, C.World.Parties.Generator.SizeShareWeights, local),
            People = C.People,
            ElectorateContent = C.Electorate,
            Campaign = C.Campaign,
            Themes = C.World.Parties.Themes,
            Events = C.Events,
            Portes = C.Portes,
        };
    }
}
