using Planalto.Core.Characters;
using Planalto.Core.Data;
using Planalto.Core.Events;
using Planalto.Core.Municipios;

// Uso: dotnet run --project tools/Planalto.EventLint [caminho/para/data]
// Verifica os eventos (GDD 15.5) e mostra a distribuição por categoria frente às metas da seção 15.2.
string? data = args.Length > 0 ? args[0] : DataPaths.FindDataDirectory(Directory.GetCurrentDirectory()) ?? DataPaths.FindDataDirectory(AppContext.BaseDirectory);
if (data is null)
{
    Console.Error.WriteLine("Pasta data/ não encontrada.");
    return 1;
}

IReadOnlyList<DataFileReport> loadReports = EventContent.Load(data, out EventContent? content);
if (content is null)
{
    foreach (string error in loadReports.SelectMany(r => r.Errors))
    {
        Console.WriteLine($"ERRO  {error}");
    }

    return 1;
}

PorteCatalog portes = JsonData.LoadFile<PorteCatalog>(Path.Combine(data, "portes.json")).GetValueOrThrow();
TraitCatalog traits = JsonData.LoadFile<TraitCatalog>(Path.Combine(data, "traits.json")).GetValueOrThrow();
LintReport report = EventLint.Run(content, portes, traits);

Console.WriteLine($"{content.Events.Count} eventos");
Console.WriteLine();
Console.WriteLine("Categoria               eventos   meta");
foreach (var (category, (count, target)) in report.Distribution)
{
    Console.WriteLine($"{category,-22} {count,7} {target,6}");
}

Console.WriteLine();
Console.WriteLine($"Escolhas marcadas como sem rastro: {report.SemRastro?.Count ?? 0}");
foreach (string line in report.SemRastro ?? [])
{
    Console.WriteLine($"  {line}");
}

Console.WriteLine();
foreach (string warning in report.Warnings)
{
    Console.WriteLine($"AVISO {warning}");
}

foreach (string error in report.Errors)
{
    Console.WriteLine($"ERRO  {error}");
}

Console.WriteLine(report.Errors.Count == 0 ? "Nenhum erro." : $"{report.Errors.Count} erro(s).");
return report.Errors.Count == 0 ? 0 : 1;
