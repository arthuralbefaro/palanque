using Godot;
using Planalto.UI.Theme;

namespace Planalto.UI.Screens;

/// <summary>Enquadramento do mapa: núcleo urbano ou cidade inteira (ZoneMapView.Frame.cs).</summary>
public partial class HubScreen
{
    /// <summary>
    /// Em cidade com zona rural grande o mapa abre no núcleo urbano; um botão só afasta para o município inteiro e
    /// volta. Onde o enquadramento inicial já é a cidade inteira, o botão desfaz a aproximação. Vale para a campanha e
    /// para a tela central fora de campanha (mandato, prefeitura, entressafra).
    /// </summary>
    private void AddFramingButton()
    {
        Action rebuild = _gov ? BuildGovMap : BuildMap;
        Button button;
        if (_map.HasNucleusView && _map.ShowingWhole)
        {
            button = Ui.Button(_modes, "Núcleo urbano", () => { _map.ShowNucleus(); rebuild(); }, Palette.Tip(
                Tr("Volta a enquadrar o núcleo urbano da sede."),
                Tr("aproxima as zonas urbanas da sede; a roda do mouse aproxima, o botão direito arrasta e o duplo clique desfaz a aproximação."),
                Tr("nada.")));
        }
        else
        {
            button = Ui.Button(_modes, "Cidade inteira", () => { _map.ShowWhole(); rebuild(); }, Palette.Tip(
                Tr("Enquadra o município inteiro, com a zona rural e os distritos."),
                Tr("mostra todas as zonas; a roda do mouse aproxima, o botão direito arrasta e o duplo clique desfaz a aproximação."),
                Tr("nada.")));
        }

        button.Name = "Enquadrar";
    }
}
