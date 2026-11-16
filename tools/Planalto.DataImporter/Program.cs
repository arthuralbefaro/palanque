using System.Text.Encodings.Web;
using System.Text.Json;
using Planalto.Core.Data;
using Planalto.Core.Municipios;
using Planalto.DataImporter;

// Uso: dotnet run --project tools/Planalto.DataImporter
// Baixa as fontes oficiais para o cache (uma vez só) e gera data/municipios/.
// Com o argumento "cenario", gera data/parties/scenarios/brasil_2026/; com "aparencia", data/appearance/cor_raca_uf.json; com "nomes", data/names/first_names.json;
// com "geo", data/geo/ (contornos simplificados para o mapa); com "zonas", data/zonas/ e data/geo/zonas/ (setores do Censo 2022 agrupados em zonas).

// O .dbf da malha do IBGE pode declarar páginas de código que o .NET só lê com este provedor.
System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

string? dataDirectory = DataPaths.FindDataDirectory(Directory.GetCurrentDirectory())
    ?? DataPaths.FindDataDirectory(AppContext.BaseDirectory);
if (dataDirectory is null)
{
    Console.Error.WriteLine("Pasta data/ não encontrada. Rode a partir da raiz do projeto.");
    return 1;
}

string root = Directory.GetParent(dataDirectory)!.FullName;
string cacheDirectory = SourceCache.Locate(root);

PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(dataDirectory, "portes.json")).GetValueOrThrow();

using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
http.DefaultRequestHeaders.UserAgent.ParseAdd("Planalto.DataImporter/1.0");
var cache = new SourceCache(cacheDirectory, http);

// Modo "rd" (dados reais de partida, GDD 17.17): "rd siconfi-baixar" enche o cache do SICONFI (demorado, retomável).
if (args.Length > 1 && args[0] == "rd" && args[1] == "siconfi-baixar")
{
    return await Planalto.DataImporter.Rd.SiconfiDownload.RunAsync(cache, http, args.Length > 2 && int.TryParse(args[2], out int paralelas) ? paralelas : 3);
}

// "rd mortalidade": tábuas de mortalidade e população projetada por UF (data/demography/).
if (args.Length > 1 && args[0] == "rd" && args[1] == "mortalidade")
{
    return await Planalto.DataImporter.Rd.MortalityImport.RunAsync(cache, dataDirectory);
}

// Modos "cenario" (Brasil 2026, GDD 16.4) e "aparencia" (cor ou raça por UF, GDD 3.8).
if (args.Length > 0 && args[0] is "cenario" or "aparencia" or "nomes" or "geo" or "zonas" or "zonas-rd" or "rd")
{
    try
    {
        return args[0] switch
        {
            // "rd": monta data/indicadores_reais/ e data/economy/start.json (depois de "rd siconfi-baixar").
            "rd" => await Planalto.DataImporter.Rd.RdImport.RunAsync(cache, dataDirectory),
            "zonas-rd" => await Planalto.DataImporter.Rd.ZonasRdImport.RunAsync(cache, dataDirectory),
            "cenario" => await Planalto.DataImporter.Scenario.ScenarioImport.RunAsync(cache, dataDirectory),
            "aparencia" => await Planalto.DataImporter.Appearance.CorRacaImport.RunAsync(cache, dataDirectory),
            "geo" => await Planalto.DataImporter.Geo.GeoImport.RunAsync(cache, dataDirectory),
            "zonas" => await Planalto.DataImporter.Geo.ZonasImport.RunAsync(cache, dataDirectory),
            _ => await Planalto.DataImporter.Names.FirstNamesImport.RunAsync(cache, dataDirectory),
        };
    }
    catch (DownloadBlockedException ex)
    {
        Console.Error.WriteLine($"Download bloqueado ou falho: {ex.Message}");
        Console.Error.WriteLine($"  URL: {ex.Url}  arquivo: {ex.FileName}  destino: {ex.TargetPath}");
        return 2;
    }
    catch (ImportException ex)
    {
        Console.Error.WriteLine($"Importação interrompida: {ex.Message}");
        return 1;
    }
}

RawSources raw;
BuildResult result;
try
{
    raw = await Sources.FetchAsync(cache);
    result = MunicipiosBuilder.Build(raw, portes);
}
catch (DownloadBlockedException ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine($"Download bloqueado ou falho: {ex.Message}");
    Console.Error.WriteLine("Baixe manualmente e coloque no cache, depois rode o importador de novo:");
    Console.Error.WriteLine($"  URL:     {ex.Url}");
    Console.Error.WriteLine($"  arquivo: {ex.FileName}");
    Console.Error.WriteLine($"  destino: {ex.TargetPath}");
    return 2;
}
catch (ImportException ex)
{
    Console.Error.WriteLine($"Importação interrompida: {ex.Message}");
    return 1;
}

// Saída legível (acentos sem escape) e com quebra de linha LF em qualquer sistema.
var options = new JsonSerializerOptions(JsonData.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
string outputDirectory = Path.Combine(dataDirectory, MunicipiosDataSet.DirectoryName);
Directory.CreateDirectory(outputDirectory);
foreach (string stale in Directory.EnumerateFiles(outputDirectory, "*.json"))
{
    File.Delete(stale);
}

void Write(string fileName, object value) =>
    File.WriteAllText(
        Path.Combine(outputDirectory, fileName),
        JsonSerializer.Serialize(value, value.GetType(), options).ReplaceLineEndings("\n") + "\n");

foreach (MunicipioUfFile file in result.Files)
{
    Write($"{file.Uf}.json", file);
}

Write(MunicipiosDataSet.FontesFileName, result.Fontes);

int total = result.Files.Sum(f => f.Municipios.Count);
long bytes = Directory.EnumerateFiles(outputDirectory, "*.json").Sum(f => new FileInfo(f).Length);
Console.WriteLine();
Console.WriteLine($"{total} municípios em {result.Files.Count} arquivos ({bytes / 1024.0 / 1024.0:F1} MB) em {outputDirectory}");
Console.WriteLine($"Lacunas: {result.Fontes.Lacunas.Count}; vagas acima do teto: {result.Fontes.ExcecoesVagas.Count}");
foreach (ExcecaoVagas e in result.Fontes.ExcecoesVagas)
{
    Console.WriteLine($"  {e.CodigoIbge} {e.Nome} ({e.Uf}): {e.VagasVereador} vagas reais, teto {e.VagasVereadorMax}");
}

return 0;
