using Godot;
using Planalto.Autoload;

namespace Planalto.UI;

/// <summary>
/// Preferências do jogador fora do save (opcoes.cfg na base de UserPaths): pausas automáticas desligadas ou ligadas e dicas de
/// primeira vez já vistas (GDD 17.1, princípio j: dica dispensada não volta sozinha; só pelo botão das Opções).
/// </summary>
public static class Settings
{
    private static string Path => UserPaths.Of("opcoes.cfg");
    private static ConfigFile? _file;

    private static ConfigFile File
    {
        get
        {
            if (_file is null)
            {
                _file = new ConfigFile();
                _file.Load(Path);
            }

            return _file;
        }
    }

    private static void Save() => File.Save(Path);

    public static IReadOnlyDictionary<string, bool> PauseOverrides =>
        File.HasSection("pausas")
            ? File.GetSectionKeys("pausas").ToDictionary(k => k, k => File.GetValue("pausas", k).AsBool())
            : new Dictionary<string, bool>();

    public static void SetPause(string id, bool value)
    {
        File.SetValue("pausas", id, value);
        Save();
    }

    public static bool HintsEnabled => File.GetValue("dicas", "ligadas", true).AsBool();

    public static bool HintSeen(string id) => File.GetValue("dicas_vistas", id, false).AsBool();

    public static void MarkHintSeen(string id)
    {
        File.SetValue("dicas_vistas", id, true);
        Save();
    }

    public static void DisableHints()
    {
        File.SetValue("dicas", "ligadas", false);
        Save();
    }

    /// <summary>Há o que religar: as dicas foram desligadas ou alguma já foi fechada com "Entendi".</summary>
    public static bool HintsToRestore => !HintsEnabled || (File.HasSection("dicas_vistas") && File.GetSectionKeys("dicas_vistas").Length > 0);

    /// <summary>Opções: religa as dicas de primeira vez e esquece as já vistas; cada uma volta na próxima visita à área.</summary>
    public static void RestoreHints()
    {
        File.SetValue("dicas", "ligadas", true);
        if (File.HasSection("dicas_vistas"))
        {
            File.EraseSection("dicas_vistas");
        }

        Save();
    }

    /// <summary>A pausa está ligada hoje: a escolha do jogador nas desligáveis, o padrão de data/time.json nas outras.</summary>
    public static bool PauseOn(Core.State.TimeConfig config, string id) => Core.State.Clock.ShouldPause(config, id, PauseOverrides);

    /// <summary>Esquece o que foi lido; a próxima consulta lê de novo o arquivo (troca de perfil nos testes de fumaça).</summary>
    public static void Reload() => _file = null;

    /// <summary>Testes de fumaça: volta às dicas ligadas e nenhuma vista, sem gravar.</summary>
    public static void ResetForTest()
    {
        _file = new ConfigFile();
    }
}
