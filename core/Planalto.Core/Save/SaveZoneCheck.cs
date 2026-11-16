using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Planalto.Core.State;

namespace Planalto.Core.Save;

/// <summary>
/// Confere se um save aponta só para zonas que existem nos dados atuais. Os ids de zona não são migrados quando a
/// divisão muda (decisão do autor): o save que não confere não abre, com <see cref="Mensagem"/>. A versão da
/// divisão já é conferida ao ler o save (<see cref="GameStateSerializer"/>); esta conferência pega o que a versão
/// não pega (dados regenerados sem mudar a versão).
/// </summary>
public static partial class SaveZoneCheck
{
    public const string Mensagem = "Este jogo salvo foi criado com uma divisão de zonas anterior e não pode ser aberto nesta versão. Comece uma carreira nova.";

    /// <summary>
    /// Campos do save que guardam a posição de uma zona na lista do território (<c>TerritoryState.Zones</c>), em vez
    /// do id: zona natal, redutos, zonas ativas na semana e as zonas do plano e dos atos dos rivais. −1 é "sem zona".
    /// </summary>
    private static readonly HashSet<string> IndexFields = new(StringComparer.Ordinal)
    {
        "home_zone", "redutos", "active_this_week", "zone", "target_zones", "defend_zone", "opportunity_zone",
    };

    /// <summary>Id de zona ("3139607-03"), sozinho ou dentro de uma chave composta ("caminhada|3139607-03|geral").</summary>
    [GeneratedRegex(@"(?<![\d-])\d{7}-\d{2}(?![\d-])")]
    private static partial Regex ZoneIdPattern();

    /// <summary>Todo id de zona citado no save (valor, chave ou parte de chave composta), onde quer que esteja.</summary>
    public static IReadOnlySet<string> ZoneIds(GameState state)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        void Collect(string text)
        {
            foreach (Match match in ZoneIdPattern().Matches(text))
            {
                ids.Add(match.Value);
            }
        }

        Walk(Tree(state), null, (key, node) =>
        {
            if (key is not null)
            {
                Collect(key);
            }

            if (node is JsonValue v && v.TryGetValue(out string? s))
            {
                Collect(s);
            }
        });
        return ids;
    }

    /// <summary>
    /// O que no save não confere com as zonas dos dados: id de zona que não existe, lista do território diferente da
    /// do município (os vetores de presença e apoio seguem a ordem dela) e posição de zona fora da lista. Vazio quando
    /// o save pode ser aberto.
    /// </summary>
    /// <param name="zonesOf">Ids das zonas do município, na ordem de data/zonas/; null se o município não existe.</param>
    public static List<string> Problems(GameState state, Func<int, IReadOnlyList<string>?> zonesOf)
    {
        var problems = new List<string>();
        var known = new Dictionary<int, HashSet<string>?>();
        foreach (string id in ZoneIds(state).Order(StringComparer.Ordinal))
        {
            int code = int.Parse(id.AsSpan(0, 7), System.Globalization.CultureInfo.InvariantCulture);
            if (!known.TryGetValue(code, out HashSet<string>? zones))
            {
                known[code] = zones = zonesOf(code)?.ToHashSet(StringComparer.Ordinal);
            }

            if (zones is null || !zones.Contains(id))
            {
                problems.Add($"zona {id} não existe nos dados");
            }
        }

        Territory.TerritoryState? territory = state.Campaign?.Territory;
        if (territory is not null)
        {
            IReadOnlyList<string>? current = zonesOf(state.FocusMunicipio);
            if (current is null || !current.SequenceEqual(territory.Zones, StringComparer.Ordinal))
            {
                problems.Add($"as zonas do território ({territory.Zones.Count}) não são as do município nos dados ({current?.Count ?? 0})");
            }

            int count = territory.Zones.Count;
            if (territory.Population.Count != count || territory.Presence.Values.Any(p => p.Length != count)
                || territory.MeetingSupport.Count is not 0 && territory.MeetingSupport.Count != count
                || territory.Support.Values.Any(s => s.Count != count))
            {
                problems.Add("vetores do território com tamanho diferente do número de zonas");
            }

            Walk(Tree(state), null, (key, node) =>
            {
                if (key is null || !IndexFields.Contains(key))
                {
                    return;
                }

                foreach (int index in Indices(node))
                {
                    if (index < -1 || index >= count)
                    {
                        problems.Add($"{key}: zona na posição {index}, com {count} zonas");
                    }
                }
            });
        }

        return problems;
    }

    private static JsonNode Tree(GameState state) => JsonNode.Parse(GameStateSerializer.Serialize(state))!;

    /// <summary>Visita cada nó, com a chave do objeto em que está (null dentro de listas e na raiz).</summary>
    private static void Walk(JsonNode? node, string? key, Action<string?, JsonNode?> visit)
    {
        visit(key, node);
        switch (node)
        {
            case JsonObject o:
                foreach (var (k, child) in o)
                {
                    Walk(child, k, visit);
                }

                break;
            case JsonArray a:
                foreach (JsonNode? child in a)
                {
                    Walk(child, null, visit);
                }

                break;
        }
    }

    /// <summary>Inteiros de um campo de posição: número, lista de números ou objeto de listas (redutos por rival).</summary>
    private static IEnumerable<int> Indices(JsonNode? node) => node switch
    {
        JsonValue v when v.TryGetValue(out int i) => [i],
        JsonArray a => a.SelectMany(Indices),
        JsonObject o => o.Select(p => p.Value).SelectMany(Indices),
        _ => [],
    };
}
