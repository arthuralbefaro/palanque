using Planalto.Core.Data;

// Uso: dotnet run --project tools/Planalto.DataValidator [caminho/para/data]
string? dataDirectory = args.Length > 0
    ? args[0]
    : DataPaths.FindDataDirectory(Directory.GetCurrentDirectory())
      ?? DataPaths.FindDataDirectory(AppContext.BaseDirectory);

if (dataDirectory is null || !Directory.Exists(dataDirectory))
{
    Console.Error.WriteLine("Pasta data/ não encontrada. Rode a partir da raiz do projeto ou informe o caminho.");
    return 1;
}

IReadOnlyList<DataFileReport> reports = DataValidation.ValidateAll(dataDirectory);
foreach (DataFileReport report in reports)
{
    if (report.IsValid)
    {
        Console.WriteLine($"OK    {report.File} ({report.ItemCount} itens)");
        continue;
    }

    Console.WriteLine($"ERRO  {report.File}");
    foreach (string error in report.Errors)
    {
        Console.WriteLine($"      {error}");
    }
}

return reports.All(report => report.IsValid) ? 0 : 1;
