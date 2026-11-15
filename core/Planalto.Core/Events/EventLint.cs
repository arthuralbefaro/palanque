using System.Text.RegularExpressions;
using Planalto.Core.Characters;
using Planalto.Core.Municipios;

namespace Planalto.Core.Events;

public sealed record LintReport(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, IReadOnlyDictionary<string, (int Count, int Target)> Distribution);

/// <summary>
/// Verificações do conteúdo de eventos (GDD 15.5): referências, variáveis inexistentes, cadeias quebradas,
/// eventos inalcançáveis e distribuição por categoria frente às metas da seção 15.2.
/// </summary>
public static partial class EventLint
{
    /// <summary>Metas de eventos por categoria (GDD 15.2).</summary>
    public static readonly IReadOnlyDictionary<string, int> Targets = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["campanha"] = 80,
        ["mandato_legislativo"] = 60,
        ["mandato_executivo"] = 70,
        ["crises"] = 50,
        ["economia"] = 40,
        ["escandalos"] = 60,
        ["midia"] = 50,
        ["vida_pessoal"] = 60,
        ["bastidores"] = 50,
        ["sociedade"] = 40,
        ["regionais"] = 40,
        ["raros"] = 20,
    };

    private static readonly string[] Regioes = ["N", "NE", "CO", "SE", "S"];

    public static LintReport Run(EventContent content, PorteCatalog portes, TraitCatalog traits)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var events = content.Events;
        var ids = events.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var scheduled = events.SelectMany(AllEffects).SelectMany(fx => fx.Schedule).Select(s => s.Event).ToHashSet(StringComparer.Ordinal);
        var flagsSet = events.SelectMany(AllEffects).SelectMany(fx => fx.SetFlags).ToHashSet(StringComparer.Ordinal);

        foreach (EventDefinition e in events)
        {
            string where = $"{e.Id}";
            if (!Targets.ContainsKey(e.Category))
            {
                errors.Add($"{where}: categoria '{e.Category}' fora da lista da seção 15.2.");
            }

            foreach (string text in Texts(e))
            {
                foreach (Match m in VariablePattern().Matches(text))
                {
                    if (!EventValidation.Variables.Contains(m.Groups[1].Value))
                    {
                        errors.Add($"{where}: variável inexistente {{{m.Groups[1].Value}}}.");
                    }
                }

                if (text.Count(c => c == '{') != text.Count(c => c == '}'))
                {
                    errors.Add($"{where}: chaves desbalanceadas em \"{text}\".");
                }
            }

            foreach (Campaign.ScheduledEvent s in AllEffects(e).SelectMany(fx => fx.Schedule))
            {
                if (!ids.Contains(s.Event))
                {
                    errors.Add($"{where}: cadeia quebrada, agenda '{s.Event}', que não existe.");
                }
            }

            if (e.ChainOnly && !scheduled.Contains(e.Id))
            {
                errors.Add($"{where}: inalcançável, só dispara por cadeia e nenhum evento o agenda.");
            }

            EventConditions c = e.Conditions;
            if (c.Flag is not null && !flagsSet.Contains(c.Flag))
            {
                errors.Add($"{where}: inalcançável, exige a flag '{c.Flag}', que nenhum evento liga.");
            }

            foreach (string porte in c.Porte.Where(p => portes.Portes.All(x => x.Id != p)))
            {
                errors.Add($"{where}: porte '{porte}' inexistente.");
            }

            foreach (string regiao in c.Regiao.Where(r => !Regioes.Contains(r)))
            {
                errors.Add($"{where}: região '{regiao}' inexistente.");
            }

            foreach (string? trait in new[] { c.Trait, c.NotTrait }.Where(t => t is not null))
            {
                if (traits.Traits.All(t => t.Id != trait))
                {
                    errors.Add($"{where}: traço '{trait}' inexistente.");
                }
            }

            if (c.AttributeMin.Values.Any(v => v > Attributes.Max) || (c.MoneyLt is not null && c.MoneyGt is not null && c.MoneyLt <= c.MoneyGt))
            {
                errors.Add($"{where}: inalcançável, condições impossíveis (atributo acima de 100 ou faixa de dinheiro vazia).");
            }

            if (e.Choices.All(ch => ch.Requires is not null))
            {
                warnings.Add($"{where}: todas as escolhas têm requisito; o jogador pode ficar sem opção.");
            }
        }

        var distribution = Targets.ToDictionary(t => t.Key, t => (events.Count(e => e.Category == t.Key), t.Value), StringComparer.Ordinal);
        return new LintReport(errors, warnings, distribution);
    }

    private static IEnumerable<Campaign.Effects> AllEffects(EventDefinition e) =>
        e.Choices.SelectMany(c => c.Test is null ? [c.Effects] : new[] { c.Effects, c.Test.Success, c.Test.Failure });

    private static IEnumerable<string> Texts(EventDefinition e) =>
        e.Variants.Append(e.Title).Concat(e.Choices.SelectMany(c => new[] { c.Label, c.Result, c.Test?.SuccessText, c.Test?.FailureText }).Where(t => t is not null).Select(t => t!));

    [GeneratedRegex(@"\{([^{}]*)\}")]
    private static partial Regex VariablePattern();
}
