using Planalto.Core.Data;
using Planalto.Core.State;

namespace Planalto.Core.Characters;

/// <summary>Conteúdo de data/biography.json: modelos de biografia com as três formas de gênero (GDD 3.3).</summary>
public sealed class BiographyCatalog : IValidatable
{
    public const string File = "biography.json";

    public required List<GenderedText> Templates { get; init; }

    public required Dictionary<string, string> OriginPhrases { get; init; }

    public IEnumerable<string> Validate()
    {
        if (Templates is null || Templates.Count == 0)
        {
            yield return "a lista 'templates' está vazia.";
            yield break;
        }

        foreach (var (template, i) in Templates.Select((t, i) => (t, i)))
        {
            foreach (string error in DataChecks.Gendered(template, $"templates[{i}]", "biography.json"))
            {
                yield return error;
            }
        }
    }

    /// <summary>Biografia gerada a partir das escolhas, respeitando o gênero do personagem; editável depois.</summary>
    public string Write(Character character, Rng rng, string cidade, string partido, string profissao, DateOnly today)
    {
        string template = rng.Pick(Templates).For(character.Gender);
        string origem = character.SocialOrigin is not null && OriginPhrases.TryGetValue(character.SocialOrigin, out string? phrase) ? phrase : "";
        return template
            .Replace("{nome}", character.BallotName, StringComparison.Ordinal)
            .Replace("{idade}", character.AgeOn(today).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{cidade}", cidade, StringComparison.Ordinal)
            .Replace("{uf}", character.HomeUf ?? "", StringComparison.Ordinal)
            .Replace("{origem}", origem, StringComparison.Ordinal)
            .Replace("{profissao}", profissao.ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("{partido}", partido, StringComparison.Ordinal);
    }
}
