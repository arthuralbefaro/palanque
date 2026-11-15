using Planalto.Core.Data;

namespace Planalto.Core.Events;

/// <summary>Todos os eventos de data/events/{categoria}/*.json e a configuração do motor.</summary>
public sealed class EventContent
{
    public const string Directory = "events";
    public const string ConfigFile = "events/config.json";

    public required EventConfig Config { get; init; }

    public required IReadOnlyList<EventDefinition> Events { get; init; }

    public EventDefinition? Find(string id) => Events.FirstOrDefault(e => e.Id == id);

    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, out EventContent? content)
    {
        var reports = new List<DataFileReport>();
        DataLoadResult<EventConfig> config = JsonData.LoadFile<EventConfig>(Path.Combine(dataDirectory, ConfigFile));
        reports.Add(new DataFileReport(ConfigFile, 1, config.Errors));

        var events = new List<EventDefinition>();
        string root = Path.Combine(dataDirectory, Directory);
        foreach (string file in System.IO.Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories).Where(f => !f.EndsWith("config.json", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            DataLoadResult<EventFile> result = JsonData.LoadFile<EventFile>(file);
            string name = Path.GetRelativePath(dataDirectory, file).Replace('\\', '/');
            var errors = result.Errors.ToList();
            string folder = Path.GetFileName(Path.GetDirectoryName(file))!;
            foreach (EventDefinition e in result.Value?.Events ?? [])
            {
                if (e.Category != folder)
                {
                    errors.Add($"{name}: evento '{e.Id}' com categoria '{e.Category}' fora da pasta '{folder}'.");
                }
            }

            reports.Add(new DataFileReport(name, result.Value?.Events.Count ?? 0, errors));
            events.AddRange(result.Value?.Events ?? []);
        }

        foreach (var dup in events.GroupBy(e => e.Id).Where(g => g.Count() > 1))
        {
            reports.Add(new DataFileReport(Directory + "/", 0, [$"evento '{dup.Key}' definido mais de uma vez."]));
        }

        foreach (EventDefinition e in events)
        {
            foreach (var target in e.Choices.SelectMany(c => c.Effects.Schedule.Concat(c.Test is null ? [] : c.Test.Success.Schedule.Concat(c.Test.Failure.Schedule))))
            {
                if (events.All(x => x.Id != target.Event))
                {
                    reports.Add(new DataFileReport(Directory + "/", 0, [$"evento '{e.Id}' agenda '{target.Event}', que não existe."]));
                }
            }
        }

        content = reports.All(r => r.IsValid) && config.IsValid ? new EventContent { Config = config.Value!, Events = events } : null;
        return reports;
    }

    public static EventContent LoadOrThrow(string dataDirectory)
    {
        IReadOnlyList<DataFileReport> reports = Load(dataDirectory, out EventContent? content);
        return content ?? throw new DataValidationException(reports.SelectMany(r => r.Errors).ToList());
    }
}
