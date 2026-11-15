using System.Text.Json;
using Godot;
using Planalto.Autoload;
using Planalto.Core.Characters;
using Planalto.Core.Data;

namespace Planalto.UI.Portrait;

/// <summary>
/// Gera retratos como textura num SubViewport e guarda em cache por aparência, opções e tamanho
/// (GDD 7.4: 512, 256 e 96), para listas, manchetes e mapas sem custo de redesenho.
/// </summary>
public partial class PortraitCache : Node
{
    public static PortraitCache Instance { get; private set; } = null!;

    private readonly Dictionary<string, Texture2D> _cache = [];

    public override void _Ready() => Instance = this;

    public async Task<Texture2D> GetAsync(Appearance appearance, PortraitOptions options, int size)
    {
        string key = $"{size}|{options}|{JsonSerializer.Serialize(appearance, JsonData.Options)}";
        if (_cache.TryGetValue(key, out Texture2D? cached))
        {
            return cached;
        }

        var viewport = new SubViewport
        {
            Size = new Vector2I(size, size),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
        };
        AddChild(viewport);
        var view = new PortraitView { Size = new Vector2(size, size) };
        viewport.AddChild(view);
        view.Show(appearance, options, Game.Instance.People.Appearance);

        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var texture = ImageTexture.CreateFromImage(viewport.GetTexture().GetImage());
        viewport.QueueFree();
        _cache[key] = texture;
        return texture;
    }

    public void Clear() => _cache.Clear();
}
