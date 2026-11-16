using Godot;
using Planalto.Core.Save;
using Planalto.Core.State;
using FileAccess = Godot.FileAccess;

namespace Planalto.Autoload;

/// <summary>
/// Saves na pasta "saves" da base de UserPaths (GDD 7.6): 3 slots, autosave semanal e antes da eleição. No modo Ironman há um
/// único save, gravado automaticamente a cada semana.
/// </summary>
public partial class SaveManager : Node
{
    public const int Slots = 3;
    public static string Folder => UserPaths.Of("saves");
    public const string AutosaveSlot = "autosave";
    public const string ElectionSlot = "antes_da_eleicao";
    public const string IronmanSlot = "ironman";

    public static SaveManager Instance { get; private set; } = null!;

    public override void _Ready() => Instance = this;

    public static string PathOf(string slot) => $"{Folder}/{slot}.json";

    public bool Save(string slot)
    {
        GameState? state = Game.Instance.State;
        if (state is null)
        {
            return false;
        }

        DirAccess.MakeDirRecursiveAbsolute(Folder);
        using FileAccess file = FileAccess.Open(PathOf(slot), FileAccess.ModeFlags.Write);
        if (file is null)
        {
            GD.PushError($"Não foi possível gravar {PathOf(slot)}.");
            return false;
        }

        file.StoreString(GameStateSerializer.Serialize(state));
        return true;
    }

    /// <summary>Autosave da semana; no Ironman, o único save da carreira.</summary>
    public void Autosave()
    {
        bool ironman = Game.Instance.State?.World?.Options.Ironman ?? false;
        Save(ironman ? IronmanSlot : AutosaveSlot);
    }

    public bool Exists(string slot) => FileAccess.FileExists(PathOf(slot));

    /// <summary>Carrega o slot e retoma a carreira; devolve a mensagem de erro, ou null se deu certo.</summary>
    public string? Load(string slot)
    {
        if (!Exists(slot))
        {
            return "Save não encontrado.";
        }

        try
        {
            GameState state = GameStateSerializer.Deserialize(FileAccess.GetFileAsString(PathOf(slot)));
            Game.Instance.Resume(state);
            return null;
        }
        catch (SaveFormatException ex)
        {
            return ex.Message;
        }
    }

    public IEnumerable<string> ManualSlots() => Enumerable.Range(1, Slots).Select(i => $"slot{i}");
}
