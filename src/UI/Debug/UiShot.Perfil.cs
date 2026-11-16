using Godot;
using Planalto.Autoload;
using FileAccess = Godot.FileAccess;

namespace Planalto.UI.Debug;

/// <summary>
/// Perfil de dados de usuário dos testes de fumaça. O alvo "perfil" confere o isolamento: grava opções e um arquivo
/// de save com o próprio nome, espera, lê de novo e falha se algum arquivo foi trocado por outra rodada. Rodado em
/// paralelo com dois perfis diferentes, os dois têm de sair com código 0.
/// </summary>
public partial class UiShot
{
    /// <summary>Nome do branch git do projeto, ou "padrao" se não houver git.</summary>
    private static string CurrentBranch()
    {
        var output = new Godot.Collections.Array();
        int code = OS.Execute("git", ["-C", ProjectSettings.GlobalizePath("res://"), "rev-parse", "--abbrev-ref", "HEAD"], output);
        string branch = code == 0 && output.Count > 0 ? output[0].AsString().Trim() : "";
        return branch.Length == 0 ? "padrao" : branch;
    }

    private async Task ProfileIsolation(string profile)
    {
        string expectedBase = $"user://smoke/{profile}/";
        if (UserPaths.Base != expectedBase)
        {
            Fail($"o perfil devia gravar em {expectedBase}, mas grava em {UserPaths.Base}.");
            return;
        }

        string marker = SaveManager.PathOf("marca_perfil");
        if (FileAccess.FileExists(marker) || UI.Settings.PauseOverrides.Count > 0)
        {
            Fail("a pasta do perfil não foi apagada no início da rodada.");
            return;
        }

        DirAccess.MakeDirRecursiveAbsolute(SaveManager.Folder);
        using (FileAccess file = FileAccess.Open(marker, FileAccess.ModeFlags.Write))
        {
            file.StoreString(profile);
        }

        UI.Settings.SetPause("perfil_" + profile, true);

        // Tempo para outra rodada em paralelo gravar os mesmos arquivos, se estivesse na mesma pasta.
        await ToSignal(GetTree().CreateTimer(4.0), SceneTreeTimer.SignalName.Timeout);

        UI.Settings.Reload();
        string stored = FileAccess.GetFileAsString(marker);
        var keys = UI.Settings.PauseOverrides.Keys.ToList();
        if (stored != profile || keys.Count != 1 || keys[0] != "perfil_" + profile)
        {
            Fail($"o perfil {profile} leu dados de outra rodada: save com \"{stored}\", opções com [{string.Join(", ", keys)}].");
            return;
        }

        GD.Print($"Smoke perfil: ok ({UserPaths.Base})");
        GetTree().Quit(0);
    }
}
