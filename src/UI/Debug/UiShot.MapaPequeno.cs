using Godot;
using Planalto.Autoload;
using Planalto.Core.Save;
using Planalto.UI.Map;
using Planalto.UI.Screens;
using FileAccess = Godot.FileAccess;

namespace Planalto.UI.Debug;

/// <summary>
/// Alvo "mapa_pequeno": abre a tela central em Mantena (MG), em Mesquita (MG), em duas cidades pequenas com distritos de
/// outras regiões e numa metrópole, em 1366×768 e 1920×1080. Confere por código que nenhum nome de zona cobre outro nome ou um
/// marcador, que todos ficam dentro do mapa, que o enquadramento inicial mostra o núcleo urbano ocupando boa parte
/// da vista e que o botão de enquadramento vai ao município inteiro e volta (clicado de verdade); tira as capturas.
/// Por fim, um save da divisão de zonas anterior é recusado na tela inicial com a mensagem ao jogador.
/// </summary>
public partial class UiShot
{
    private static readonly (int Codigo, string Label)[] MapaPequenoCities =
    [
        (3139607, "mantena_mg"),
        (3141702, "mesquita_mg"),
        (2313005, "solonopole_ce"),
        (4314605, "piratini_rs"),
        (3106200, "belo_horizonte_mg"),
    ];

    /// <summary>Fração mínima da largura ou da altura do mapa que o núcleo urbano ocupa no enquadramento inicial.</summary>
    private const float NucleusMinShare = 0.5f;

    private async Task PlayMapaPequeno(string? shot)
    {
        var problems = new List<string>();
        UI.Settings.ResetForTest();
        foreach (Vector2I size in new[] { new Vector2I(1366, 768), new Vector2I(1920, 1080) })
        {
            GetTree().Root.Size = size;
            DisplayServer.WindowSetSize(size);
            await Frames(4);
            string tag = $"{size.X}x{size.Y}";
            foreach (var (codigo, label) in MapaPequenoCities)
            {
                HubScreen hub = await OpenHubIn(codigo, 11);
                PrepareBusyWeek(hub);
                await Frames(4);
                ZoneMapView map = hub.ZoneMap;
                string where = $"{label} {tag}";
                problems.AddRange(CheckLabels(map, where));
                if (map.HasNucleusView)
                {
                    Rect2 nucleus = map.NucleusScreenRect();
                    var area = new Rect2(Vector2.Zero, map.Size);
                    float share = Math.Max(nucleus.Size.X / area.Size.X, nucleus.Size.Y / area.Size.Y);
                    if (!area.Grow(1).Encloses(nucleus) || share < NucleusMinShare)
                    {
                        problems.Add($"{where}: núcleo urbano {nucleus} fora do mapa ou pequeno ({share:P0} do mapa)");
                    }

                    // O núcleo é a sede: a zona "Centro", quando existe, está nele (não uma vila afastada).
                    if (map.Zonas.Values.FirstOrDefault(z => z.Nome == "Centro" && !z.IsRural) is { } centro && !map.Nucleus.Contains(centro.Id))
                    {
                        problems.Add($"{where}: o enquadramento inicial deixou o Centro de fora (núcleo: {string.Join(", ", map.Nucleus.Select(id => map.Zonas[id].Nome))})");
                    }
                }
                else if (label != "belo_horizonte_mg")
                {
                    problems.Add($"{where}: cidade pequena sem enquadramento no núcleo urbano");
                }

                Save(shot, $"_{label}_{tag}");
                if (label is "mantena_mg" or "mesquita_mg")
                {
                    problems.AddRange(await CheckContrast(hub, where, shot, $"_{label}_{tag}"));
                }

                // O botão de enquadramento: município inteiro e de volta ao núcleo.
                if (Named<Button>(hub, "Enquadrar") is not { } frame)
                {
                    problems.Add($"{where}: sem o botão de enquadramento");
                }
                else if (map.HasNucleusView)
                {
                    await Click(frame);
                    await Frames(3);
                    if (!map.ShowingWhole)
                    {
                        problems.Add($"{where}: \"Cidade inteira\" não mostrou o município inteiro");
                    }

                    problems.AddRange(CheckLabels(map, where + ", cidade inteira"));
                    Save(shot, $"_{label}_{tag}_inteiro");
                    if (Named<Button>(hub, "Enquadrar") is not { Text: "Núcleo urbano" } back)
                    {
                        problems.Add($"{where}: sem o botão de voltar ao núcleo urbano");
                    }
                    else
                    {
                        await Click(back);
                        await Frames(3);
                        if (map.ShowingWhole || !map.HasNucleusView)
                        {
                            problems.Add($"{where}: \"Núcleo urbano\" não voltou ao núcleo");
                        }
                    }
                }

                RemoveChild(hub);
                hub.QueueFree();
                await Frames(2);
            }
        }

        problems.AddRange(await CheckOldSaveRefused());
        if (problems.Count > 0)
        {
            Fail("mapa_pequeno:\n  " + string.Join("\n  ", problems));
            return;
        }

        GD.Print("Smoke mapa_pequeno: ok (Mantena, Mesquita, Solonópole, Piratini e Belo Horizonte em 1366x768 e 1920x1080: nomes sem sobreposição e dentro do mapa, núcleo urbano enquadrado, cidade inteira e volta; save da divisão de zonas anterior recusado com a mensagem)");
        GetTree().Quit(0);
    }

    /// <summary>
    /// P-09: em cidade pequena o modo Minha força (o da abertura) separa as zonas em faixas, e o modo Partido líder,
    /// mesmo com um partido à frente em todas as zonas, varia a intensidade da cor pela vantagem; captura os dois modos.
    /// </summary>
    private async Task<List<string>> CheckContrast(HubScreen hub, string where, string? shot, string suffix)
    {
        var problems = new List<string>();
        ZoneMapView map = hub.ZoneMap;
        if (Named<Button>(hub, "Modo_forca") is not { ButtonPressed: true })
        {
            problems.Add($"{where}: a tela central não abre no modo Minha força");
        }

        int strength = map.ZoneColors.Values.Distinct().Count();
        await Click(Named<Button>(hub, "Modo_lider")!);
        var leaders = map.ZoneColors.Values.Select(c => new Color(c, 1)).Distinct().Count();
        int shades = map.ZoneColors.Values.Distinct().Count();
        GD.Print($"  {where}: {map.Zonas.Count} zonas; Minha força em {strength} faixas; Partido líder com {leaders} partido(s) e {shades} tons");
        Save(shot, suffix + "_lider");
        if (map.Zonas.Count > 1 && strength < 2)
        {
            problems.Add($"{where}: o modo Minha força pinta todas as zonas da mesma cor");
        }

        if (map.Zonas.Count > 1 && shades < 2)
        {
            problems.Add($"{where}: o modo Partido líder pinta todas as zonas da mesma cor e do mesmo tom");
        }

        await Click(Named<Button>(hub, "Modo_forca")!);
        return problems;
    }

    /// <summary>Nenhum nome cobre outro nome, um marcador ou um controle sobre o mapa, e todos ficam dentro do mapa.</summary>
    private static List<string> CheckLabels(ZoneMapView map, string where)
    {
        var problems = new List<string>();
        IReadOnlyList<ZoneMapView.LabelBox> labels = map.LabelBoxes();
        IReadOnlyList<Rect2> markers = map.MarkerBoxes();
        var obstacles = map.Obstacles?.Invoke().ToList() ?? [];
        var area = new Rect2(Vector2.Zero, map.Size);
        if (labels.Count == 0)
        {
            problems.Add($"{where}: nenhum nome de zona no mapa");
        }

        for (int i = 0; i < labels.Count; i++)
        {
            ZoneMapView.LabelBox a = labels[i];
            if (!area.Encloses(a.Rect))
            {
                problems.Add($"{where}: \"{a.Text}\" sai do mapa");
            }

            for (int j = i + 1; j < labels.Count; j++)
            {
                if (a.Rect.Intersects(labels[j].Rect))
                {
                    problems.Add($"{where}: \"{a.Text}\" cobre \"{labels[j].Text}\"");
                }
            }

            if (markers.Any(m => m.Intersects(a.Rect)))
            {
                problems.Add($"{where}: \"{a.Text}\" cobre um marcador");
            }

            if (obstacles.Any(o => o.Intersects(a.Rect)))
            {
                problems.Add($"{where}: \"{a.Text}\" fica sob um controle do mapa");
            }

            if (a.Abbreviated && !map.Zonas[a.ZoneId].Nome.Equals(a.FullName, StringComparison.Ordinal))
            {
                problems.Add($"{where}: \"{a.Text}\" abreviado sem o nome inteiro");
            }
        }

        if (labels.Count > ZoneMapView.MaxLabels)
        {
            problems.Add($"{where}: {labels.Count} nomes, acima de {ZoneMapView.MaxLabels}");
        }

        return problems;
    }

    /// <summary>
    /// Grava, na pasta de saves do perfil da rodada, um save sem a versão da divisão de zonas (como os anteriores a ela)
    /// e outro com uma zona que não existe, e tenta abrir cada um pela tela inicial: os dois são recusados, a mensagem aparece e a tela continua no menu.
    /// </summary>
    private async Task<List<string>> CheckOldSaveRefused()
    {
        var problems = new List<string>();
        Game game = Game.Instance;
        HubScreen hub = await OpenHubIn(3139607, 11);
        RemoveChild(hub);
        hub.QueueFree();
        string json = GameStateSerializer.Serialize(game.State!);
        var root = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        root.Remove("zonas_versao");
        string home = game.State!.Campaign!.Territory.Zones[0];
        var cases = new (string Slot, string Text)[]
        {
            ("teste_zonas_antigas", root.ToJsonString()),
            ("teste_zona_inexistente", json.Replace($"\"{home}\"", "\"3139607-97\"", StringComparison.Ordinal)),
        };

        // Os saves de teste ficam no perfil da rodada (UserPaths), nunca nos saves do jogador.
        if (!SaveManager.Folder.StartsWith("user://smoke/", StringComparison.Ordinal))
        {
            problems.Add($"saves de teste fora do perfil: {SaveManager.Folder}");
            return problems;
        }

        DirAccess.MakeDirRecursiveAbsolute(SaveManager.Folder);
        foreach (var (slot, text) in cases)
        {
            using (FileAccess file = FileAccess.Open(SaveManager.PathOf(slot), FileAccess.ModeFlags.Write))
            {
                file.StoreString(text);
            }

            var menu = GD.Load<PackedScene>(Game.Scenes.MainMenu).Instantiate<MainMenu>();
            AddChild(menu);
            await Frames(3);
            string? error = menu.TryLoad(slot);
            await Frames(3);
            AcceptDialog? dialog = Named<AcceptDialog>(menu, "ErroAoCarregar");
            if (error != SaveZoneCheck.Mensagem)
            {
                problems.Add($"save {slot}: recusado com \"{error}\", esperado a mensagem da divisão de zonas");
            }

            if (dialog is not { Visible: true } || !dialog.DialogText.Contains("divisão de zonas anterior", StringComparison.Ordinal))
            {
                problems.Add($"save {slot}: a tela inicial não mostrou a mensagem");
            }

            if (!IsInstanceValid(menu) || !menu.IsInsideTree())
            {
                problems.Add($"save {slot}: a tela inicial saiu do lugar");
            }

            dialog?.GetOkButton().EmitSignal(BaseButton.SignalName.Pressed);
            await Frames(2);
            RemoveChild(menu);
            menu.QueueFree();
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(SaveManager.PathOf(slot)));
            await Frames(2);
        }

        return problems;
    }
}
