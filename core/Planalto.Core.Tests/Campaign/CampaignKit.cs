using Planalto.Core.Campaign;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Electorate;
using Planalto.Core.Events;
using Planalto.Core.Legislature;
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
            Projection = PopulationProjection.Load(dir, Municipios);
            Legislature = LegislatureContent.LoadOrThrow(
                dir,
                Electorate.Segments.Dimensions.SelectMany(d => d.Groups).Select(g => g.Id).ToHashSet(StringComparer.Ordinal),
                World.Parties.Themes.Themes.Select(t => t.Id).ToHashSet(StringComparer.Ordinal));
        }

        public LegislatureContent Legislature { get; }

        /// <summary>Projeção como o jogo a carrega: reescalada pela projeção por UF de data/demography/.</summary>
        public PopulationProjection Projection { get; }

        public Planalto.Core.Debate.DebateContent Debate { get; } = JsonData.LoadFile<Planalto.Core.Debate.DebateContent>(Path.Combine(TestContent.DataDirectory, Planalto.Core.Debate.DebateContent.File)).GetValueOrThrow();

        public Planalto.Core.Executive.ExecutiveContent Executive { get; } = Planalto.Core.Executive.ExecutiveContent.LoadOrThrow(TestContent.DataDirectory);

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

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, Planalto.Core.Territory.ZonasMunicipio> ZonasCache = new();

    /// <summary>Zonas do município (data/zonas/), lidas uma vez por município: o arquivo é o da UF inteira.</summary>
    public static Planalto.Core.Territory.ZonasMunicipio Zonas(Municipio m) =>
        ZonasCache.GetOrAdd(m.CodigoIbge, _ => CampaignContext.LoadZonas(TestContent.DataDirectory, m));

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

    /// <summary>Contexto do mandato no município em foco, com o mesmo eleitorado da campanha.</summary>
    public static LegislatureContext LegislatureContext(GameState state)
    {
        CampaignContext campaign = Context(state);
        return new LegislatureContext
        {
            State = state,
            Municipio = campaign.Municipio,
            Content = C.Legislature,
            People = C.People,
            Electorate = campaign.Electorate,
            RecognitionScale = C.Campaign.Config.PorteRecognitionMultiplier.GetValueOrDefault(campaign.Municipio.Porte, 1),
        };
    }

    /// <summary>Contexto da prefeitura no município em foco.</summary>
    public static Planalto.Core.Executive.ExecutiveContext ExecutiveContext(GameState state, Planalto.Core.Indicators.IndicadoresMunicipio? indicadores = null)
    {
        CampaignContext campaign = Context(state);
        return new Planalto.Core.Executive.ExecutiveContext
        {
            State = state,
            Municipio = campaign.Municipio,
            Content = C.Executive,
            Legislature = C.Legislature.Config,
            Electorate = campaign.Electorate,
            RecognitionScale = C.Campaign.Config.PorteRecognitionMultiplier.GetValueOrDefault(campaign.Municipio.Porte, 1),
            Indicadores = indicadores,
        };
    }

    /// <summary>
    /// Save com as chaves de cada objeto em ordem: dois estados iguais dão o mesmo texto mesmo quando um dicionário
    /// foi preenchido em outra ordem (o que acontece depois de carregar um save e seguir jogando).
    /// </summary>
    public static string Canonical(GameState state)
    {
        static System.Text.Json.Nodes.JsonNode? Sort(System.Text.Json.Nodes.JsonNode? node) => node switch
        {
            // "flags" é um conjunto (HashSet): a ordem dos itens não faz parte do estado.
            System.Text.Json.Nodes.JsonObject o => new System.Text.Json.Nodes.JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => KeyValuePair.Create(
                p.Key,
                p.Key == "flags" && p.Value is System.Text.Json.Nodes.JsonArray set
                    ? new System.Text.Json.Nodes.JsonArray(set.Select(x => x!.GetValue<string>()).Order(StringComparer.Ordinal).Select(x => (System.Text.Json.Nodes.JsonNode?)System.Text.Json.Nodes.JsonValue.Create(x)).ToArray())
                    : Sort(p.Value)))),
            System.Text.Json.Nodes.JsonArray a => new System.Text.Json.Nodes.JsonArray(a.Select(Sort).ToArray()),
            null => null,
            _ => node.DeepClone(),
        };

        return Sort(System.Text.Json.Nodes.JsonNode.Parse(Planalto.Core.Save.GameStateSerializer.Serialize(state)))!.ToJsonString();
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
            Debate = C.Debate,
            Projection = C.Projection,
            Zonas = Zonas(m),
        };
    }
}
