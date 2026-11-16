using System.Globalization;
using System.Text.Json;

namespace Planalto.DataImporter.Rd;

/// <summary>
/// Baixa do SICONFI (Tesouro Nacional, API do Data Lake) as contas anuais (DCA: receitas, Anexo I-C, e despesas por
/// função, Anexo I-E) e o Relatório de Gestão Fiscal (RGF, Anexo 01: despesa com pessoal e receita corrente líquida)
/// de cada município e de cada UF. A API responde um ente por chamada; cada resposta fica num arquivo do cache
/// (fora do Git), e a execução pode ser interrompida e retomada. Resposta vazia também é gravada: é a evidência de
/// que o ente não entregou a declaração naquele exercício (GDD 17.17, regra de substituição).
/// </summary>
public static class SiconfiDownload
{
    public const string BaseUrl = "https://apidatalake.tesouro.gov.br/ords/siconfi/tt";

    /// <summary>Exercícios tentados, do mais recente para trás (GDD 17.17: até 2 anos de reserva).</summary>
    public static readonly int[] Exercicios = [2025, 2024, 2023];

    private const int Paralelas = 3;
    private const int Tentativas = 5;

    public static string Folder(string cacheDirectory) => Path.Combine(cacheDirectory, "rd", "siconfi");

    public static string DcaFile(string cacheDirectory, string anexo, int exercicio, int ente) =>
        Path.Combine(Folder(cacheDirectory), string.Create(CultureInfo.InvariantCulture, $"dca_{anexo}_{exercicio}_{ente}.json"));

    public static string RgfFile(string cacheDirectory, int exercicio, int ente) =>
        Path.Combine(Folder(cacheDirectory), string.Create(CultureInfo.InvariantCulture, $"rgf_a01_{exercicio}_{ente}.json"));

    public static string DcaUrl(string anexo, int exercicio, int ente) =>
        string.Create(CultureInfo.InvariantCulture, $"{BaseUrl}/dca?an_exercicio={exercicio}&no_anexo=DCA-Anexo%20{anexo}&id_ente={ente}");

    /// <summary>
    /// RGF do Executivo. Municípios com menos de 50 mil habitantes podem entregar o RGF simplificado, semestral
    /// (LRF, art. 63); os demais, quadrimestral. O último período do exercício cobre os 12 meses.
    /// </summary>
    public static string RgfUrl(int exercicio, int ente, bool estado, bool simplificado) =>
        string.Create(CultureInfo.InvariantCulture, $"{BaseUrl}/rgf?an_exercicio={exercicio}")
        + (simplificado
            ? "&in_periodicidade=S&nr_periodo=2&co_tipo_demonstrativo=RGF%20Simplificado"
            : "&in_periodicidade=Q&nr_periodo=3&co_tipo_demonstrativo=RGF")
        + string.Create(CultureInfo.InvariantCulture, $"&no_anexo=RGF-Anexo%2001&co_esfera={(estado ? "E" : "M")}&co_poder=E&id_ente={ente}");

    /// <param name="paralelas">Chamadas simultâneas (padrão 3); a API responde devagar e recusa quando há chamadas demais.</param>
    public static async Task<int> RunAsync(SourceCache cache, HttpClient http, int paralelas = Paralelas)
    {
        CachedFile localidades = await cache.GetAsync("ibge_localidades_municipios.json", Ibge.LocalidadesUrl);
        List<IbgeMunicipio> municipios = Ibge.ReadLocalidades(localidades.Path);
        Directory.CreateDirectory(Folder(cache.Directory));

        var entes = municipios.Select(m => (Id: m.Id, Estado: false))
            .Concat(municipios.Select(m => m.UfId).Distinct().Order().Select(uf => (Id: uf, Estado: true)))
            .ToList();
        Console.WriteLine($"SICONFI: {entes.Count} entes, exercícios {string.Join(", ", Exercicios)} (só desce de ano quando o anterior veio vazio)");

        int done = 0;
        var falhas = new List<string>();
        using var gate = new SemaphoreSlim(Math.Clamp(paralelas, 1, 16));
        var tasks = entes.Select(async e =>
        {
            await gate.WaitAsync();
            try
            {
                foreach (string anexo in new[] { "I-C", "I-E" })
                {
                    foreach (int ano in Exercicios)
                    {
                        if (await FetchAsync(http, DcaUrl(anexo, ano, e.Id), DcaFile(cache.Directory, anexo, ano, e.Id)) > 0)
                        {
                            break;
                        }
                    }
                }

                foreach (int ano in Exercicios)
                {
                    string file = RgfFile(cache.Directory, ano, e.Id);
                    if (File.Exists(file))
                    {
                        if (Count(file) > 0)
                        {
                            break;
                        }

                        continue;
                    }

                    // Quadrimestral primeiro; vazio, tenta o simplificado. Grava o que tiver itens (ou o vazio).
                    string temp = file + ".q";
                    int n = await FetchAsync(http, RgfUrl(ano, e.Id, e.Estado, simplificado: false), temp);
                    if (n == 0 && !e.Estado)
                    {
                        File.Delete(temp);
                        n = await FetchAsync(http, RgfUrl(ano, e.Id, e.Estado, simplificado: true), temp);
                    }

                    File.Move(temp, file, overwrite: true);
                    if (n > 0)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or JsonException)
            {
                lock (falhas)
                {
                    falhas.Add($"{e.Id}: {ex.Message}");
                }
            }
            finally
            {
                gate.Release();
                int n = Interlocked.Increment(ref done);
                if (n % 200 == 0)
                {
                    Console.WriteLine($"  {n}/{entes.Count} entes ({falhas.Count} falhas)");
                }
            }
        }).ToList();
        await Task.WhenAll(tasks);

        Console.WriteLine($"SICONFI: {done} entes percorridos, {falhas.Count} com falha de rede (rode de novo para retomar).");
        falhas.Take(20).ToList().ForEach(f => Console.Error.WriteLine($"  {f}"));
        return falhas.Count == 0 ? 0 : 2;
    }

    /// <summary>Número de itens de uma resposta gravada.</summary>
    public static int Count(string file)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllBytes(file));
        return doc.RootElement.GetProperty("items").GetArrayLength();
    }

    /// <summary>Baixa todas as páginas de uma consulta para um arquivo só ({"items":[...]}); devolve o número de itens.</summary>
    private static async Task<int> FetchAsync(HttpClient http, string url, string file)
    {
        if (File.Exists(file))
        {
            return Count(file);
        }

        var items = new List<string>();
        int offset = 0;
        while (true)
        {
            string body = await GetWithRetryAsync(http, offset == 0 ? url : string.Create(CultureInfo.InvariantCulture, $"{url}&offset={offset}"));
            using JsonDocument doc = JsonDocument.Parse(body);
            foreach (JsonElement item in doc.RootElement.GetProperty("items").EnumerateArray())
            {
                items.Add(item.GetRawText());
            }

            if (!doc.RootElement.TryGetProperty("hasMore", out JsonElement more) || !more.GetBoolean())
            {
                break;
            }

            offset = items.Count;
        }

        string partial = file + ".part";
        await File.WriteAllTextAsync(partial, "{\"items\":[" + string.Join(",", items) + "]}");
        File.Move(partial, file, overwrite: true);
        return items.Count;
    }

    /// <summary>Intervalo mínimo entre o início de duas chamadas: a API do Tesouro recusa (HTTP 429) acima de cerca de 1 por segundo.</summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(1.1);

    private static readonly SemaphoreSlim Ritmo = new(1, 1);

    private static DateTime _proxima = DateTime.MinValue;

    private static async Task EsperarVezAsync()
    {
        await Ritmo.WaitAsync();
        try
        {
            TimeSpan wait = _proxima - DateTime.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait);
            }

            _proxima = DateTime.UtcNow + Intervalo;
        }
        finally
        {
            Ritmo.Release();
        }
    }

    private static async Task<string> GetWithRetryAsync(HttpClient http, string url)
    {
        for (int attempt = 1; ; attempt++)
        {
            await EsperarVezAsync();
            try
            {
                using HttpResponseMessage response = await http.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }

                if ((int)response.StatusCode == 429)
                {
                    // Limite de chamadas atingido: espera um minuto e tenta de novo, sem contar como tentativa perdida.
                    await Task.Delay(TimeSpan.FromSeconds(60));
                    attempt--;
                    continue;
                }

                if (attempt >= Tentativas)
                {
                    throw new HttpRequestException($"HTTP {(int)response.StatusCode} em {url}");
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && attempt < Tentativas)
            {
                // Tenta de novo abaixo.
            }

            await Task.Delay(TimeSpan.FromSeconds(3 * attempt));
        }
    }
}
