using Godot;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Electorate;
using Planalto.Core.Municipios;
using Planalto.Core.Scenarios;
using Planalto.Core.State;
using Planalto.Core.World;

namespace Planalto.Autoload;

/// <summary>
/// Ponto de entrada da UI no Core: carrega o conteúdo de data/, guarda o estado e troca de tela.
/// A UI só lê daqui e chama os métodos de comando; toda regra fica no Core.
/// </summary>
public partial class Game : Node
{
    public static Game Instance { get; private set; } = null!;

    public string DataDirectory { get; private set; } = "";

    public WorldContent World { get; private set; } = null!;

    public PeopleContent People { get; private set; } = null!;

    public ElectorateContent Electorate { get; private set; } = null!;

    public MunicipiosDataSet Municipios { get; private set; } = null!;

    public PorteCatalog Portes { get; private set; } = null!;

    private Brasil2026Scenario? _brasil2026;

    /// <summary>Mundo em configuração na tela "Novo Mundo"; null fora dela.</summary>
    public WorldSetup? PendingWorld { get; set; }

    /// <summary>Semente da carreira em criação.</summary>
    public ulong PendingSeed { get; set; }

    public GameState? State { get; private set; }

    public override void _Ready()
    {
        Instance = this;
        DataDirectory = ProjectSettings.GlobalizePath("res://data");
        Portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(DataDirectory, "portes.json")).GetValueOrThrow();
        var (municipios, errors) = MunicipiosDataSet.Load(DataDirectory, Portes);
        if (municipios is null)
        {
            GD.PushError("data/municipios inválido: " + string.Join("; ", errors.Take(5)));
            return;
        }

        Municipios = municipios;
        World = WorldContent.LoadOrThrow(DataDirectory);
        People = PeopleContent.LoadOrThrow(DataDirectory);
        ElectorateContent.Load(DataDirectory, World.Parties.Themes, Municipios, out ElectorateContent? electorate);
        Electorate = electorate ?? throw new DataValidationException(["data/electorate inválido."]);
    }

    /// <summary>Cenário Brasil 2026 completo (estados e municípios), carregado na primeira vez que for pedido.</summary>
    public Brasil2026Scenario? Brasil2026
    {
        get
        {
            if (_brasil2026 is null && Brasil2026Scenario.Exists(DataDirectory))
            {
                _brasil2026 = Brasil2026Scenario.Load(DataDirectory, Municipios, World.Parties).Scenario;
            }

            return _brasil2026;
        }
    }

    public void StartCareer(GameState state) => State = state;

    public void GoTo(string scene) => GetTree().ChangeSceneToFile(scene);

    public static class Scenes
    {
        public const string MainMenu = "res://src/UI/Screens/MainMenu.tscn";
        public const string NewWorld = "res://src/UI/Screens/NewWorldScreen.tscn";
        public const string CharacterCreator = "res://src/UI/Screens/CharacterCreatorScreen.tscn";
        public const string Hub = "res://src/UI/Screens/HubScreen.tscn";
    }
}
