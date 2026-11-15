namespace Planalto.Core.Data;

public static class DataPaths
{
    /// <summary>
    /// Sobe a partir de <paramref name="startDirectory"/> até achar a raiz do projeto
    /// (pasta com project.godot e data/) e devolve o caminho de data/.
    /// </summary>
    public static string? FindDataDirectory(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            string data = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(dir.FullName, "project.godot")) && Directory.Exists(data))
            {
                return data;
            }
        }

        return null;
    }
}
