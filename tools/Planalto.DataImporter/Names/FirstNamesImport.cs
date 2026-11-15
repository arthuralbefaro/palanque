using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Municipios;

namespace Planalto.DataImporter.Names;

/// <summary>
/// Modo "nomes": os 20 prenomes mais frequentes por sexo, no Brasil e em cada UF (IBGE, Censo 2010,
/// API de nomes), usados nos NPCs (GDD 3.8). Grava data/names/first_names.json.
/// </summary>
public static class FirstNamesImport
{
    public const string BaseUrl = "https://servicodados.ibge.gov.br/api/v2/censos/nomes/ranking";

    public const string OutputFile = "names/first_names.json";

    public static async Task<int> RunAsync(SourceCache cache, string dataDirectory)
    {
        PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();
        MunicipiosDataSet municipios = MunicipiosDataSet.Load(dataDirectory, portes).DataSet
            ?? throw new ImportException("data/municipios/ inválido; rode o importador de municípios antes.");
        var ufs = municipios.All.GroupBy(m => m.Uf).ToDictionary(g => g.Key, g => g.First().CodigoIbge / 100_000);

        async Task<List<RankedName>> Fetch(string sexo, int? ufCode, int? decada = null)
        {
            string url = $"{BaseUrl}?sexo={sexo}" + (ufCode is null ? "" : $"&localidade={ufCode}") + (decada is null ? "" : $"&decada={decada}");
            string file = $"ibge_nomes_{sexo}_{(ufCode is null ? "BR" : ufCode.Value.ToString(CultureInfo.InvariantCulture))}"
                + (decada is null ? "" : $"_{decada}") + ".json";
            CachedFile cached = await cache.GetAsync(file, url);
            using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(cached.Path));
            return doc.RootElement[0].GetProperty("res").EnumerateArray()
                .Select(r => new RankedName { Nome = r.GetProperty("nome").GetString()!, Frequencia = r.GetProperty("frequencia").GetInt64() })
                .ToList();
        }

        FirstNamesFile output = new FirstNamesFile
        {
            Fonte = "IBGE, Censo Demográfico 2010, API de nomes (ranking dos prenomes mais frequentes por sexo, UF e década de nascimento).",
            Url = BaseUrl,
            ConsultadoEm = DateOnly.FromDateTime(DateTime.UtcNow),
            Brasil = new NamesBySex { Masculino = await Fetch("M", null), Feminino = await Fetch("F", null) },
            Ufs = [],
            ByDecade = [],
            Complementary = new ComplementaryNames { Masculino = [], Feminino = [] },
        };

        foreach (var (uf, code) in ufs.OrderBy(u => u.Key, StringComparer.Ordinal))
        {
            output.Ufs[uf] = new NamesBySex { Masculino = await Fetch("M", code), Feminino = await Fetch("F", code) };
        }

        // Décadas da API: 1930 (antes de 1930) a 2010 ([2000, 2010)), o último período do Censo 2010.
        for (int decada = 1930; decada <= 2010; decada += 10)
        {
            var place = new NamesByPlace
            {
                Brasil = new NamesBySex { Masculino = await Fetch("M", null, decada), Feminino = await Fetch("F", null, decada) },
                Ufs = [],
            };
            foreach (var (uf, code) in ufs.OrderBy(u => u.Key, StringComparer.Ordinal))
            {
                place.Ufs[uf] = new NamesBySex { Masculino = await Fetch("M", code, decada), Feminino = await Fetch("F", code, decada) };
            }

            output.ByDecade[decada] = place;
            Console.WriteLine($"  década {decada}: ok");
        }

        // Complementares: frequência nacional por década de cada candidato de names.json.
        NameStyleCatalog style = JsonData.LoadFile<NameStyleCatalog>(Path.Combine(dataDirectory, "names/names.json")).GetValueOrThrow();
        var known = output.ByDecade.Values.SelectMany(p => p.Ufs.Values.Append(p.Brasil))
            .Concat(output.Ufs.Values.Append(output.Brasil))
            .SelectMany(s => s.Masculino.Concat(s.Feminino)).Select(n => n.Nome).ToHashSet(StringComparer.Ordinal);
        async Task<List<DecadeName>> Complementary(string sexo, List<string> candidates)
        {
            var result = new List<DecadeName>();
            foreach (string nome in candidates.Where(c => !known.Contains(c)).Distinct())
            {
                CachedFile cached = await cache.GetAsync($"ibge_nome_{nome}_{sexo}.json", $"https://servicodados.ibge.gov.br/api/v2/censos/nomes/{nome.ToLowerInvariant()}?sexo={sexo}");
                using JsonDocument doc = JsonDocument.Parse(await File.ReadAllTextAsync(cached.Path));
                if (doc.RootElement.GetArrayLength() == 0)
                {
                    continue;
                }

                var byDecade = new Dictionary<int, long>();
                foreach (JsonElement period in doc.RootElement[0].GetProperty("res").EnumerateArray())
                {
                    // "1930[" = antes de 1930 → 1930; "[1950,1960[" → 1960 (mesma chave do ranking por década).
                    string p = period.GetProperty("periodo").GetString()!;
                    int key = int.Parse(p.TrimEnd('[').Split(',').Last(), CultureInfo.InvariantCulture);
                    byDecade[key] = period.GetProperty("frequencia").GetInt64();
                }

                if (byDecade.Values.Sum() >= style.ComplementaryMinFrequency)
                {
                    result.Add(new DecadeName { Nome = nome, ByDecade = byDecade });
                }
            }

            return result;
        }

        output = new FirstNamesFile
        {
            Fonte = output.Fonte,
            Url = output.Url,
            ConsultadoEm = output.ConsultadoEm,
            Brasil = output.Brasil,
            Ufs = output.Ufs,
            ByDecade = output.ByDecade,
            Complementary = new ComplementaryNames
            {
                Masculino = await Complementary("M", style.ComplementaryCandidates.GetValueOrDefault("masculino") ?? []),
                Feminino = await Complementary("F", style.ComplementaryCandidates.GetValueOrDefault("feminino") ?? []),
            },
        };

        var errors = output.Validate().ToList();
        if (errors.Count > 0)
        {
            throw new ImportException(string.Join("; ", errors));
        }

        var options = new JsonSerializerOptions(JsonData.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        string path = Path.Combine(dataDirectory, OutputFile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(output, options).ReplaceLineEndings("\n") + "\n");
        Console.WriteLine($"{output.Ufs.Count} UFs gravadas em {path}");
        return 0;
    }
}
