using Godot;

namespace Planalto.UI;

/// <summary>
/// A janela do jogo contra a área útil do monitor (a tela menos a barra de tarefas). O projeto desenha em
/// 1920×1080; numa tela desse tamanho, uma janela comum de 1920×1080 mais a moldura não cabe: a barra de título
/// some para cima e a base da tela fica atrás da barra de tarefas. Por isso o jogo abre maximizado
/// (project.godot) e, se for aberto em janela comum, ela é ajustada aqui.
/// </summary>
public static class WindowFit
{
    /// <summary>
    /// Folga em pixels reais: a moldura invisível de redimensionamento do Windows entra no retângulo "com decoração"
    /// e fica fora da tela numa janela encostada na borda, sem esconder nada.
    /// </summary>
    public const int Tolerance = 10;

    public static bool HasWindow => DisplayServer.GetName() != "headless" && !Engine.IsEmbeddedInEditor();

    /// <summary>Área útil do monitor em que a janela está.</summary>
    public static Rect2I Usable => DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());

    /// <summary>Retângulo da janela com a moldura e a barra de título.</summary>
    public static Rect2I Decorated => new(DisplayServer.WindowGetPositionWithDecorations(), DisplayServer.WindowGetSizeWithDecorations());

    /// <summary>Espessura da moldura: quanto o tamanho com decoração passa do tamanho da área de desenho.</summary>
    public static Vector2I Frame => DisplayServer.WindowGetSizeWithDecorations() - DisplayServer.WindowGetSize();

    /// <summary>Uma área de desenho desse tamanho, com a moldura, cabe na área útil?</summary>
    public static bool CanHold(Vector2I clientSize)
    {
        Vector2I size = clientSize + Frame;
        Rect2I usable = Usable;
        return size.X <= usable.Size.X + (2 * Tolerance) && size.Y <= usable.Size.Y + Tolerance;
    }

    /// <summary>
    /// O que está errado com a janela real, ou null se ela cabe: maximizada e tela cheia cabem por definição (o
    /// sistema as encaixa); janela comum precisa estar inteira dentro da área útil.
    /// </summary>
    public static string? Problem()
    {
        if (!HasWindow || DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
        {
            return null;
        }

        Rect2I window = Decorated;
        Rect2I usable = Usable;
        bool fits = window.Position.X >= usable.Position.X - Tolerance && window.Position.Y >= usable.Position.Y - Tolerance
            && window.End.X <= usable.End.X + Tolerance && window.End.Y <= usable.End.Y + Tolerance;
        return fits ? null : $"a janela com moldura ocupa {window.Position} a {window.End}, fora da área útil do monitor ({usable.Position} a {usable.End})";
    }

    /// <summary>Janela comum que não cabe na área útil vira maximizada; a que cabe e está fora dela é centralizada.</summary>
    public static void EnsureFits()
    {
        if (!HasWindow || DisplayServer.WindowGetMode() != DisplayServer.WindowMode.Windowed)
        {
            return;
        }

        if (!CanHold(DisplayServer.WindowGetSize()))
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Maximized);
        }
        else if (Problem() is not null)
        {
            Center();
        }
    }

    /// <summary>
    /// Capturas e conferência de layout: a área de desenho fica exatamente do tamanho pedido. Devolve true se a
    /// janela com moldura cabe na área útil (fica centralizada nela); se não cabe, a janela fica sem moldura no
    /// canto da tela, só para a captura, e devolve false.
    /// </summary>
    public static bool ExactForCapture(Vector2I size)
    {
        if (!HasWindow)
        {
            return false;
        }

        DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, false);
        bool framed = CanHold(size);
        DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Borderless, !framed);
        DisplayServer.WindowSetSize(size);
        if (framed)
        {
            Center();
        }
        else
        {
            DisplayServer.WindowSetPosition(DisplayServer.ScreenGetPosition(DisplayServer.WindowGetCurrentScreen()));
        }

        return framed;
    }

    /// <summary>Centraliza a janela comum (com a moldura) na área útil.</summary>
    public static void Center()
    {
        Rect2I usable = Usable;
        Vector2I size = DisplayServer.WindowGetSizeWithDecorations();
        Vector2I inset = DisplayServer.WindowGetPosition() - DisplayServer.WindowGetPositionWithDecorations();
        Vector2I corner = usable.Position + ((usable.Size - size) / 2);
        DisplayServer.WindowSetPosition(new Vector2I(Math.Max(corner.X, usable.Position.X - Tolerance), Math.Max(corner.Y, usable.Position.Y)) + inset);
    }
}
