using Godot;

namespace Planalto.UI.Screens;

/// <summary>Nomes das zonas no mapa (ZoneMapView.Labels.cs): o que eles devem evitar.</summary>
public partial class HubScreen
{
    /// <summary>Os nomes das zonas desviam do que fica sobre o mapa: seletor de modo, legenda e faixa de decisão.</summary>
    private void WireMapLabels(Control legendPanel)
    {
        _map.Obstacles = () => _mapArea.GetChildren().OfType<Control>()
            .Where(c => c != _map && c.Visible && c.Size.X * c.Size.Y < 0.5f * _mapArea.Size.X * _mapArea.Size.Y)
            .Select(c => new Rect2(c.Position, c.Size));
        _modes.Resized += _map.QueueRedraw;
        legendPanel.Resized += _map.QueueRedraw;
    }
}
