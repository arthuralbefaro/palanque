using Godot;

namespace Planalto.Autoload;

/// <summary>
/// Caminho base único dos dados do jogador fora de res:// (saves, opções, dicas vistas, presets e cenários).
/// No jogo normal é "user://". Nos testes de fumaça cada rodada usa um perfil próprio, em
/// "user://smoke/&lt;perfil&gt;/", apagado no início, para rodadas em paralelo não lerem os arquivos umas das outras.
/// </summary>
public static class UserPaths
{
    private const string Root = "user://";
    private const string SmokeRoot = "user://smoke/";

    /// <summary>Pasta base atual, sempre terminada em "/".</summary>
    public static string Base { get; private set; } = Root;

    /// <summary>Caminho de um arquivo ou pasta dentro da base atual.</summary>
    public static string Of(string relative) => Base + relative.TrimStart('/');

    /// <summary>
    /// Passa a usar o perfil de teste <paramref name="profile"/>: apaga a pasta dele, recria vazia e aponta a base
    /// para ela. Devolve o nome do perfil já limpo (só letras minúsculas, algarismos, "-" e "_").
    /// </summary>
    public static string UseSmokeProfile(string profile)
    {
        string name = Sanitize(profile);
        string folder = SmokeRoot + name + "/";
        DeleteRecursive(folder);
        DirAccess.MakeDirRecursiveAbsolute(folder);
        Base = folder;
        return name;
    }

    public static string Sanitize(string profile)
    {
        var chars = profile.Trim().ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' ? c : '_')
            .ToArray();
        string name = new string(chars).Trim('_');
        return name.Length == 0 ? "padrao" : name;
    }

    private static void DeleteRecursive(string folder)
    {
        if (!DirAccess.DirExistsAbsolute(folder))
        {
            return;
        }

        using DirAccess? dir = DirAccess.Open(folder);
        if (dir is null)
        {
            return;
        }

        foreach (string sub in dir.GetDirectories())
        {
            DeleteRecursive(folder + sub + "/");
        }

        foreach (string file in dir.GetFiles())
        {
            DirAccess.RemoveAbsolute(folder + file);
        }

        DirAccess.RemoveAbsolute(folder);
    }
}
