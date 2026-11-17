using Godot;
using Planalto.Core.Territory;
using Planalto.UI.Map;
using Planalto.UI.Screens;

namespace Planalto.UI.Debug;

/// <summary>
/// D-058, no alvo "mapa_pequeno": o mapa da cidade mostra o crédito "© colaboradores do OpenStreetMap" só quando o
/// município tem zona com nome de lá, dentro do mapa e sem cobrir nome de zona nem controle; e a Ficha da zona diz a
/// origem do nome (OpenStreetMap numa zona com nome de lá e a origem de uma zona com outra fonte), aberta pelo clique.
/// </summary>
public partial class UiShot
{
    private async Task<List<string>> CheckOsmNames(HubScreen hub, string where, bool expectOsm)
    {
        var problems = new List<string>();
        ZoneMapView map = hub.ZoneMap;
        bool usesOsm = map.Zonas.Values.Any(z => z.NomeFonte == Zona.NomeOsm);
        if (expectOsm && !usesOsm)
        {
            problems.Add($"{where}: esperava zona com nome do OpenStreetMap");
        }

        string? expected = usesOsm ? Tr(ZoneNameOrigin.CreditoOsm) : null;
        if (map.Credit != expected)
        {
            problems.Add($"{where}: crédito do mapa \"{map.Credit}\", esperado \"{expected}\"");
        }

        if (map.CreditRect() is { } credit)
        {
            if (!new Rect2(Vector2.Zero, map.Size).Encloses(credit))
            {
                problems.Add($"{where}: o crédito do OpenStreetMap sai do mapa");
            }

            problems.AddRange(map.LabelBoxes().Where(l => l.Rect.Intersects(credit)).Select(l => $"{where}: \"{l.Text}\" cobre o crédito do OpenStreetMap"));
            if ((map.Obstacles?.Invoke() ?? []).Any(o => o.Intersects(credit)))
            {
                problems.Add($"{where}: o crédito do OpenStreetMap fica sob um controle do mapa");
            }
        }

        if (!usesOsm)
        {
            return problems;
        }

        // A Ficha diz a origem: a zona com nome do OpenStreetMap e uma com outra fonte, cada uma aberta pelo mapa.
        Zona osm = map.Zonas.Values.Where(z => z.NomeFonte == Zona.NomeOsm).OrderByDescending(z => z.Populacao).First();
        Zona? other = map.Zonas.Values.Where(z => z.NomeFonte != Zona.NomeOsm).OrderByDescending(z => z.Populacao).FirstOrDefault();
        var zonas = new List<Zona> { osm };
        if (other is not null)
        {
            zonas.Add(other);
        }

        foreach (Zona z in zonas)
        {
            Vector2 at = map.ScreenCenterOf(z.Id);
            if (map.Hit(at) == z.Id && new Rect2(Vector2.Zero, map.Size).Grow(-40).HasPoint(at))
            {
                await ClickAt(map, map.GlobalPosition + at);
            }
            else
            {
                map.EmitZoneClickForTest(z.Id);
                await Frames(3);
            }

            string frase = Tr(ZoneNameOrigin.Frase(z));
            if (hub.SheetName != "Zona" || map.Selected != z.Id)
            {
                problems.Add($"{where}: o mapa não abriu a Ficha de {z.Nome}");
            }
            else if (!TextOf(hub).Contains(frase, StringComparison.Ordinal))
            {
                problems.Add($"{where}: a Ficha de {z.Nome} ({z.NomeFonte}) não diz \"{frase}\"");
            }
            else
            {
                GD.Print($"  {where}: Ficha de {z.Nome} diz \"{frase}\"");
            }

            if (Named<Button>(hub, "Voltar") is { } back)
            {
                await Click(back);
            }
        }

        return problems;
    }
}
