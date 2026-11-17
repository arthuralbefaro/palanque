using System.Globalization;
using System.Text;
using System.Text.Json;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using Planalto.Core.Territory;

namespace Planalto.DataImporter.Geo;

/// <summary>Um lugar de bairro do OpenStreetMap: elemento, nome, prioridade do tipo e ponto (o do nó, ou o centro do contorno).</summary>
public sealed record OsmLugar(string Tipo, long Id, string Nome, string Categoria, int Prioridade, double Lon, double Lat);

/// <summary>Extração de uma UF no cache: arquivo, data do download, lugares lidos e data da base do OpenStreetMap.</summary>
public sealed record OsmArquivo(string Uf, CachedFile Arquivo, int Lidos, string? BaseOsm);

/// <summary>Setor de uma zona na escolha do nome: ponto interior e moradores.</summary>
public sealed record SetorPonto(double X, double Y, long Pop);

/// <summary>Zona de um município na escolha dos nomes do OpenStreetMap.</summary>
public sealed class ZonaOsm
{
    public required int Key { get; init; }

    public required long Pop { get; init; }

    /// <summary>Centro populacional da zona (longitude e latitude).</summary>
    public required double X { get; init; }

    public required double Y { get; init; }

    /// <summary>Pode receber nome do OpenStreetMap (nome gerado; urbana, ou rural se a configuração deixar).</summary>
    public required bool Elegivel { get; init; }

    /// <summary>Nome gerado que a zona teria, sem o número de repetição.</summary>
    public required string? NomeGerado { get; init; }

    public required IReadOnlyList<SetorPonto> Setores { get; init; }

    /// <summary>Lugares do OpenStreetMap cujo ponto cai num setor da zona.</summary>
    public required IReadOnlyList<OsmLugar> Lugares { get; init; }
}

/// <summary>
/// Nomes de bairro do OpenStreetMap (D-058): extração por UF pela Overpass API para o cache, leitura, filtro dos nomes,
/// ponto no polígono dos setores e escolha de um nome por zona. Do OpenStreetMap só entram nomes de bairro.
/// </summary>
public static class OsmBairros
{
    public const string OverpassUrl = "https://overpass-api.de/api/interpreter";
    public const string CopyrightUrl = "https://www.openstreetmap.org/copyright";
    public const string LicencaUrl = "https://opendatacommons.org/licenses/odbl/1-0/";
    public const string CacheFolder = "osm";

    /// <summary>Identificação do importador para a Overpass API, sem dado pessoal.</summary>
    public const string UserAgent = "Planalto.DataImporter/1.0 (importador de nomes de bairro do OpenStreetMap; uma consulta por UF, com pausa)";

    /// <summary>Pausa depois de cada consulta baixada, para não pesar no serviço público.</summary>
    public static readonly TimeSpan Pausa = TimeSpan.FromSeconds(20);

    public static string FileName(string uf) => Path.Combine(CacheFolder, $"bairros_{uf}.json");

    /// <summary>
    /// Consulta de uma UF: nós, contornos e relações com <c>place</c> de bairro e nome, e relações de limite
    /// administrativo do nível de bairro com nome. Dos nós sai o ponto; dos contornos e das relações, só as etiquetas e
    /// o centro (sem a geometria).
    /// </summary>
    public static string Query(string uf, OsmBairrosConfig c)
    {
        string places = string.Join('|', c.Lugares.Keys.Order(StringComparer.Ordinal));
        return "[out:json][timeout:900];"
            + $"area[\"ISO3166-2\"=\"BR-{uf}\"][\"admin_level\"=\"4\"]->.uf;"
            + $"(nwr[\"place\"~\"^({places})$\"][\"name\"](area.uf);rel[\"boundary\"=\"administrative\"][\"admin_level\"=\"{c.AdminLevel}\"][\"name\"](area.uf);)->.b;"
            + "node.b;out qt;(way.b;rel.b;);out tags center qt;";
    }

    public static string Url(string uf, OsmBairrosConfig c) => OverpassUrl + "?data=" + Uri.EscapeDataString(Query(uf, c));

    /// <summary>
    /// Baixa (o que falta no cache) e lê os lugares de todas as UFs, sem repetir elemento. Download recusado para a
    /// importação com <see cref="DownloadBlockedException"/>.
    /// </summary>
    public static async Task<(List<OsmLugar> Lugares, List<OsmArquivo> Arquivos)> LoadAsync(SourceCache cache, IEnumerable<string> ufs, OsmBairrosConfig c)
    {
        var lugares = new Dictionary<(string, long), OsmLugar>();
        var arquivos = new List<OsmArquivo>();
        foreach (string uf in ufs.Order(StringComparer.Ordinal))
        {
            string fileName = FileName(uf);
            string url = Url(uf, c);
            bool cached = File.Exists(Path.Combine(cache.Directory, fileName));
            CachedFile file = await cache.GetWithBackoffAsync(fileName, url, UserAgent, ContentError, attempts: 5, firstWait: TimeSpan.FromSeconds(30));
            if (file.Url != url)
            {
                throw new ImportException($"{fileName} no cache foi extraído com outra consulta ({file.Url}); a configuração osm_bairros mudou.");
            }

            string json = await File.ReadAllTextAsync(file.Path);
            List<OsmLugar> lidos = Parse(json, c);
            foreach (OsmLugar l in lidos)
            {
                lugares.TryAdd((l.Tipo, l.Id), l);
            }

            arquivos.Add(new OsmArquivo(uf, file, lidos.Count, BaseTimestamp(json)));
            Console.WriteLine($"  OSM {uf}: {lidos.Count} lugares de bairro ({new FileInfo(file.Path).Length / 1024} KB)");
            if (!cached)
            {
                await Task.Delay(Pausa);
            }
        }

        return (lugares.Values.OrderBy(l => l.Tipo, StringComparer.Ordinal).ThenBy(l => l.Id).ToList(), arquivos);
    }

    /// <summary>Modo "zonas-osm": só baixa para o cache o que falta e mostra quantos lugares cada UF tem.</summary>
    public static async Task<int> DownloadOnlyAsync(SourceCache cache, string dataDirectory)
    {
        Planalto.Core.Municipios.PorteCatalog portes = Planalto.Core.Data.JsonData.LoadFile<Planalto.Core.Municipios.PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        var municipios = Planalto.Core.Municipios.MunicipiosDataSet.Load(dataDirectory, portes).DataSet
            ?? throw new ImportException("data/municipios/ inválido.");
        TerritoryConfig config = Planalto.Core.Data.JsonData.LoadFile<TerritoryConfig>(Path.Combine(dataDirectory, TerritoryConfig.File)).GetValueOrThrow();
        Console.WriteLine("OpenStreetMap — nomes de bairro pela Overpass API");
        var (lugares, arquivos) = await LoadAsync(cache, municipios.All.Select(m => m.Uf).Distinct(), config.OsmBairros);
        long bytes = arquivos.Sum(a => new FileInfo(a.Arquivo.Path).Length);
        Console.WriteLine($"{lugares.Count} lugares em {arquivos.Count} UFs, {bytes / 1024.0 / 1024.0:0.0} MB no cache; extraídos de {arquivos.Min(a => a.Arquivo.DownloadedAt):u} a {arquivos.Max(a => a.Arquivo.DownloadedAt):u}");
        return 0;
    }

    /// <summary>Erro da resposta da Overpass (não é JSON, sem elementos ou com erro de execução); null se está boa.</summary>
    public static string? ContentError(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument doc = JsonDocument.Parse(stream);
            if (doc.RootElement.ValueKind != JsonValueKind.Object || !doc.RootElement.TryGetProperty("elements", out JsonElement elements) || elements.ValueKind != JsonValueKind.Array)
            {
                return "resposta sem a lista de elementos";
            }

            if (doc.RootElement.TryGetProperty("remark", out JsonElement remark) && remark.GetString() is { } text && text.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                return $"erro da Overpass: {text}";
            }

            return null;
        }
        catch (JsonException ex)
        {
            return $"resposta não é JSON ({ex.Message})";
        }
    }

    /// <summary>Data da base do OpenStreetMap que a Overpass usou na resposta (osm3s.timestamp_osm_base).</summary>
    public static string? BaseTimestamp(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("osm3s", out JsonElement osm3s) && osm3s.TryGetProperty("timestamp_osm_base", out JsonElement t) ? t.GetString() : null;
    }

    /// <summary>Lugares de bairro da resposta da Overpass, com a prioridade do tipo; elemento sem nome ou sem ponto fica de fora.</summary>
    public static List<OsmLugar> Parse(string json, OsmBairrosConfig c)
    {
        var result = new List<OsmLugar>();
        using JsonDocument doc = JsonDocument.Parse(json);
        foreach (JsonElement e in doc.RootElement.GetProperty("elements").EnumerateArray())
        {
            string type = e.GetProperty("type").GetString() ?? "";
            long id = e.GetProperty("id").GetInt64();
            if (!e.TryGetProperty("tags", out JsonElement tags) || Tag(tags, "name") is not { } name)
            {
                continue;
            }

            int? priority = null;
            string category = "";
            if (Tag(tags, "place") is { } place && c.Lugares.TryGetValue(place, out int p))
            {
                priority = p;
                category = "place=" + place;
            }

            if (type == "relation" && Tag(tags, "boundary") == "administrative" && Tag(tags, "admin_level") == c.AdminLevel
                && (priority is null || c.AdminLevelPrioridade < priority))
            {
                priority = c.AdminLevelPrioridade;
                category = "boundary=administrative";
            }

            JsonElement point = type == "node" ? e : e.TryGetProperty("center", out JsonElement center) ? center : default;
            if (priority is null || point.ValueKind != JsonValueKind.Object || !point.TryGetProperty("lat", out JsonElement lat) || !point.TryGetProperty("lon", out JsonElement lon))
            {
                continue;
            }

            result.Add(new OsmLugar(type, id, name, category, priority.Value, lon.GetDouble(), lat.GetDouble()));
        }

        return result.OrderBy(l => l.Tipo, StringComparer.Ordinal).ThenBy(l => l.Id).ToList();
    }

    private static string? Tag(JsonElement tags, string key) => tags.TryGetProperty(key, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Chave de comparação de nomes: sem acento, sem caixa, pontuação como espaço e espaços simples.</summary>
    public static string Chave(string nome)
    {
        var sb = new StringBuilder(nome.Length);
        foreach (char ch in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : ' ');
        }

        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>
    /// O nome limpo (espaços simples; todo em maiúsculas passa para a caixa normal; sem a palavra "Bairro" no começo), ou null se não serve para bairro:
    /// vazio, sem letra (só números ou sinais), longo demais, com sinal de anotação (dois-pontos, parênteses), igual a
    /// um nome excluído, começando por um prefixo excluído (rua, quadra) ou igual ao nome do município.
    /// </summary>
    public static string? Filtrar(string nome, string municipio, OsmBairrosConfig c)
    {
        string clean = string.Join(' ', nome.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (clean.Length == 0 || !clean.Any(char.IsLetter) || clean.Length > c.ComprimentoMaximo || c.SinaisExcluidos.Any(s => clean.Contains(s, StringComparison.Ordinal)))
        {
            return null;
        }

        // Nome todo em maiúsculas ("JARDIM DAS FLORES") vai para a caixa normal; algarismos romanos ficam como estão.
        if (clean.Where(char.IsLetter).All(char.IsUpper) && clean.Count(char.IsLetter) > 3)
        {
            clean = string.Join(' ', clean.Split(' ').Select((w, i) =>
                w.All(ch => "IVXL".Contains(ch, StringComparison.Ordinal)) ? w
                : i > 0 && c.Minusculas.Contains(w.ToLowerInvariant(), StringComparer.Ordinal) ? w.ToLowerInvariant()
                : w[..1] + w[1..].ToLowerInvariant()));
        }

        // "Bairro Esplanada" vira "Esplanada", como os nomes do IBGE; o nome que é só a palavra fica de fora abaixo.
        string[] words = clean.Split(' ');
        if (words.Length > 1 && c.PrefixosRemovidos.Any(p => Chave(p) == Chave(words[0])))
        {
            clean = string.Join(' ', words.Skip(1));
        }

        string key = Chave(clean);
        if (key.Length == 0 || key == Chave(municipio) || c.NomesExcluidos.Any(n => Chave(n) == key)
            || c.PrefixosExcluidos.Any(p => Chave(p) is { Length: > 0 } prefix && (key == prefix || key.StartsWith(prefix + " ", StringComparison.Ordinal))))
        {
            return null;
        }

        return clean;
    }

    /// <summary>
    /// Ponto no polígono: o setor que contém cada lugar (no limite entre dois, o de menor código). Lugar fora de todos
    /// os setores fica de fora.
    /// </summary>
    public static Dictionary<string, List<OsmLugar>> Localizar(IEnumerable<OsmLugar> lugares, IReadOnlyList<(string Code, Geometry Geometry)> setores)
    {
        var result = new Dictionary<string, List<OsmLugar>>(StringComparer.Ordinal);
        if (setores.Count == 0)
        {
            return result;
        }

        var tree = new STRtree<int>();
        for (int i = 0; i < setores.Count; i++)
        {
            tree.Insert(setores[i].Geometry.EnvelopeInternal, i);
        }

        tree.Build();
        GeometryFactory factory = setores[0].Geometry.Factory;
        foreach (OsmLugar l in lugares)
        {
            Point p = factory.CreatePoint(new Coordinate(l.Lon, l.Lat));
            foreach (int i in tree.Query(p.EnvelopeInternal).OrderBy(i => setores[i].Code, StringComparer.Ordinal))
            {
                if (setores[i].Geometry.Covers(p))
                {
                    if (!result.TryGetValue(setores[i].Code, out List<OsmLugar>? list))
                    {
                        result[setores[i].Code] = list = [];
                    }

                    list.Add(l);
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Um nome do OpenStreetMap por zona elegível de um município (chave da zona → nome). Regras:
    /// <list type="number">
    /// <item>só contam os lugares com ponto dentro da zona; o mesmo nome em vários elementos é um candidato só;</item>
    /// <item>entre vários, vale o tipo de maior prioridade (limite de bairro e <c>suburb</c>, depois <c>quarter</c>,
    /// depois <c>neighbourhood</c>); no mesmo tipo, o que tem mais moradores perto (cada setor da zona conta para o
    /// lugar mais próximo do seu ponto interior); no empate, o mais perto do centro populacional da zona e, por fim, a
    /// ordem alfabética;</item>
    /// <item>nome reservado (bairro ou distrito do IBGE, o próprio município, nome de outra zona com fonte oficial) não
    /// é usado, nem o nome gerado de outra zona; a zona passa ao candidato seguinte;</item>
    /// <item>o mesmo nome não vai para duas zonas: a mais populosa escolhe primeiro.</item>
    /// </list>
    /// Zona sem candidato que sirva fica com o nome gerado. O resultado não depende da ordem da entrada.
    /// </summary>
    public static Dictionary<int, string> Escolher(IReadOnlyList<ZonaOsm> zonas, IEnumerable<string> reservados, double xScale)
    {
        var taken = reservados.Select(Chave).Where(k => k.Length > 0).ToHashSet(StringComparer.Ordinal);
        var generated = zonas.Where(z => z.Elegivel && z.NomeGerado is not null).Select(z => (z.Key, Chave: Chave(z.NomeGerado!))).ToList();
        var result = new Dictionary<int, string>();
        foreach (ZonaOsm z in zonas.Where(z => z.Elegivel && z.Lugares.Count > 0).OrderByDescending(z => z.Pop).ThenBy(z => z.Key))
        {
            foreach (var (key, nome) in Ranking(z, xScale))
            {
                if (taken.Contains(key) || generated.Any(g => g.Key != z.Key && g.Chave == key))
                {
                    continue;
                }

                result[z.Key] = nome;
                taken.Add(key);
                break;
            }
        }

        return result;
    }

    /// <summary>Candidatos da zona na ordem de preferência (ver <see cref="Escolher"/>).</summary>
    private static IEnumerable<(string Key, string Nome)> Ranking(ZonaOsm z, double xScale)
    {
        double Dist(double x, double y, double x2, double y2) => Math.Pow((x - x2) * xScale, 2) + Math.Pow(y - y2, 2);
        var candidates = z.Lugares.GroupBy(l => Chave(l.Nome), StringComparer.Ordinal).Select(g =>
        {
            var ordered = g.OrderBy(l => l.Prioridade).ThenBy(l => l.Tipo, StringComparer.Ordinal).ThenBy(l => l.Id).ToList();
            return (Key: g.Key, ordered[0].Nome, Prioridade: ordered[0].Prioridade, Points: ordered);
        }).OrderBy(c => c.Key, StringComparer.Ordinal).ToList();

        foreach (var level in candidates.GroupBy(c => c.Prioridade).OrderBy(g => g.Key))
        {
            var list = level.ToList();
            var score = new long[list.Count];
            foreach (SetorPonto s in z.Setores)
            {
                int best = -1;
                double bestDist = double.MaxValue;
                for (int i = 0; i < list.Count; i++)
                {
                    double d = list[i].Points.Min(p => Dist(p.Lon, p.Lat, s.X, s.Y));
                    if (d < bestDist)
                    {
                        best = i;
                        bestDist = d;
                    }
                }

                if (best >= 0)
                {
                    score[best] += s.Pop;
                }
            }

            foreach (int i in Enumerable.Range(0, list.Count)
                .OrderByDescending(i => score[i])
                .ThenBy(i => list[i].Points.Min(p => Dist(p.Lon, p.Lat, z.X, z.Y)))
                .ThenBy(i => list[i].Key, StringComparer.Ordinal))
            {
                yield return (list[i].Key, list[i].Nome);
            }
        }
    }
}
