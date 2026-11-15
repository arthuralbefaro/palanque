using Planalto.Core.Data;
using Planalto.Core.Ideology;
using Planalto.Core.Municipios;

namespace Planalto.Core.Electorate;

/// <summary>Conteúdo de data/electorate/: segmentos, modelo de voto e institutos.</summary>
public sealed class ElectorateContent
{
    public const string SegmentsFile = "electorate/segments.json";
    public const string VoteModelFile = "electorate/vote_model.json";
    public const string PollstersFile = "electorate/pollsters.json";

    public required SegmentCatalog Segments { get; init; }

    public required VoteModelConfig VoteModel { get; init; }

    public required PollsterCatalog Pollsters { get; init; }

    public static IReadOnlyList<DataFileReport> Load(string dataDirectory, ThemeCatalog? themes, MunicipiosDataSet? municipios, out ElectorateContent? content)
    {
        var reports = new List<DataFileReport>();
        SegmentCatalog? segments = Check<SegmentCatalog>(dataDirectory, SegmentsFile, c => c.Dimensions.Count, reports);
        VoteModelConfig? vote = Check<VoteModelConfig>(dataDirectory, VoteModelFile, _ => 1, reports);
        PollsterCatalog? pollsters = Check<PollsterCatalog>(dataDirectory, PollstersFile, c => c.Pollsters.Count, reports);

        if (segments is not null && themes is not null && municipios is not null)
        {
            Municipio sample = municipios.All.First();
            var categories = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                ["situacao"] = sample.Perfil.Situacao.Keys,
                ["idade"] = sample.Perfil.Idade.Keys,
                ["renda"] = sample.Perfil.Renda.Keys,
                ["religiao"] = sample.Perfil.Religiao.Keys,
                ["escolaridade"] = sample.Perfil.Escolaridade.Keys,
            };
            var extra = segments.ValidateAgainst(themes, categories).Select(e => $"segments.json: {e}").ToList();
            reports[0] = reports[0] with { Errors = [.. reports[0].Errors, .. extra] };
        }

        content = reports.All(r => r.IsValid) && segments is not null && vote is not null && pollsters is not null
            ? new ElectorateContent { Segments = segments, VoteModel = vote, Pollsters = pollsters }
            : null;
        return reports;
    }

    private static T? Check<T>(string dataDirectory, string file, Func<T, int> count, List<DataFileReport> reports)
        where T : class, IValidatable
    {
        DataLoadResult<T> result = JsonData.LoadFile<T>(Path.Combine(dataDirectory, file));
        reports.Add(new DataFileReport(file, result.Value is null ? 0 : count(result.Value), result.Errors));
        return result.IsValid ? result.Value : null;
    }
}
