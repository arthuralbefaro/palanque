using Planalto.Core.Data;
using Planalto.Core.Parties;
using Planalto.Core.State;

namespace Planalto.Core.Characters;

/// <summary>Conteúdo de data/biography.json: modelos de biografia com as três formas de gênero (GDD 3.3).</summary>
public sealed class BiographyCatalog : IValidatable
{
    public const string File = "biography.json";

    public required List<GenderedText> Templates { get; init; }

    public required Dictionary<string, string> OriginPhrases { get; init; }

    /// <summary>Modelos para quem ainda não trabalhou (profissões de <see cref="WithoutWork"/>, como Estudante).</summary>
    public required List<GenderedText> TemplatesWithoutWork { get; init; }

    /// <summary>Profissões que não são trabalho ("trabalhou como estudante" não se diz): usam os modelos próprios.</summary>
    public required List<string> WithoutWork { get; init; }

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

        if (TemplatesWithoutWork is null || TemplatesWithoutWork.Count == 0)
        {
            yield return "a lista 'templates_without_work' está vazia.";
            yield break;
        }

        foreach (var (template, i) in TemplatesWithoutWork.Select((t, i) => (t, i)))
        {
            foreach (string error in DataChecks.Gendered(template, $"templates_without_work[{i}]", "biography.json"))
            {
                yield return error;
            }
        }

        foreach (GenderedText template in Templates.Concat(TemplatesWithoutWork))
        {
            foreach (string text in new[] { template.Masculine, template.Feminine, template.Neutral })
            {
                if (text.Contains(" pelo {partido}", StringComparison.Ordinal) || text.Contains(" o {partido}", StringComparison.Ordinal))
                {
                    yield return "use {pelo} e {o} antes de {partido}: o artigo concorda com o nome do partido.";
                }
            }
        }
    }

    /// <summary>Biografia gerada a partir das escolhas, respeitando o gênero do personagem; editável depois.</summary>
    public string Write(Character character, Rng rng, string cidade, string partido, string profissao, DateOnly today, GrammaticalGender partidoGenero = GrammaticalGender.Masculine)
    {
        bool withoutWork = character.ProfessionId is not null && WithoutWork.Contains(character.ProfessionId);
        string template = rng.Pick(withoutWork ? TemplatesWithoutWork : Templates).For(character.Gender);
        bool feminine = partidoGenero == GrammaticalGender.Feminine;
        string origem = character.SocialOrigin is not null && OriginPhrases.TryGetValue(character.SocialOrigin, out string? phrase) ? phrase : "";
        return template
            .Replace("{nome}", character.BallotName, StringComparison.Ordinal)
            .Replace("{idade}", character.AgeOn(today).ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{cidade}", cidade, StringComparison.Ordinal)
            .Replace("{uf}", character.HomeUf ?? "", StringComparison.Ordinal)
            .Replace("{origem}", origem, StringComparison.Ordinal)
            .Replace("{profissao}", profissao.ToLowerInvariant(), StringComparison.Ordinal)
            .Replace("{pelo}", feminine ? "pela" : "pelo", StringComparison.Ordinal)
            .Replace("{o}", feminine ? "a" : "o", StringComparison.Ordinal)
            .Replace("{partido}", partido, StringComparison.Ordinal);
    }
}
