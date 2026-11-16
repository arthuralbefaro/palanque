using System.Text.RegularExpressions;
using Planalto.Core.Data;

namespace Planalto.Core.Encyclopedia;

public sealed class GlossaryTerm
{
    public required string Id { get; init; }

    public required string Nome { get; init; }

    /// <summary>Formas em que o termo aparece nos textos (minúsculas ou como escrito); viram link para o verbete.</summary>
    public required List<string> Formas { get; init; }

    public required string Texto { get; init; }
}

public sealed class EncyclopediaArticle
{
    public required string Id { get; init; }

    public required string Titulo { get; init; }

    public required string Texto { get; init; }
}

/// <summary>Glossário e artigos da Enciclopédia (data/encyclopedia/glossary.json, GDD 17.1, princípio j).</summary>
public sealed partial class EncyclopediaContent : IValidatable
{
    public const string File = "encyclopedia/glossary.json";

    public required string Note { get; init; }

    public required List<GlossaryTerm> Termos { get; init; }

    public required List<EncyclopediaArticle> Artigos { get; init; }

    public GlossaryTerm? Find(string id) => Termos.FirstOrDefault(t => t.Id == id);

    public IEnumerable<string> Validate()
    {
        foreach (string error in DataChecks.UniqueIds(Termos.Select(t => t.Id), "Termo"))
        {
            yield return error;
        }

        foreach (string error in DataChecks.UniqueIds(Artigos.Select(a => a.Id), "Artigo"))
        {
            yield return error;
        }

        foreach (GlossaryTerm t in Termos.Where(t => t.Formas.Count == 0 || string.IsNullOrWhiteSpace(t.Texto) || t.Texto.Length > 600))
        {
            yield return $"termo '{t.Id}': ao menos uma forma e texto de até 600 caracteres.";
        }

        var forms = Termos.SelectMany(t => t.Formas.Select(f => (Form: f.ToLowerInvariant(), t.Id))).ToList();
        foreach (var dup in forms.GroupBy(f => f.Form).Where(g => g.Select(x => x.Id).Distinct().Count() > 1))
        {
            yield return $"forma '{dup.Key}' usada por mais de um termo.";
        }
    }

    /// <summary>
    /// Ocorrências dos termos num texto, sem sobreposição, preferindo a forma mais longa (palavra inteira, sem
    /// diferença de maiúsculas). A interface usa isso para sublinhar e ligar ao verbete.
    /// </summary>
    public IReadOnlyList<(int Start, int Length, string TermId)> Occurrences(string s)
    {
        var hits = new List<(int Start, int Length, string TermId)>();
        foreach (var (form, id) in Termos.SelectMany(t => t.Formas.Select(f => (f, t.Id))).OrderByDescending(p => p.f.Length))
        {
            foreach (Match m in Regex.Matches(s, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(form)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (hits.All(h => m.Index + m.Length <= h.Start || m.Index >= h.Start + h.Length))
                {
                    hits.Add((m.Index, m.Length, id));
                }
            }
        }

        return hits.OrderBy(h => h.Start).ToList();
    }
}
