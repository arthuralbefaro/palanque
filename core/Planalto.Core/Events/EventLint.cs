using System.Text.RegularExpressions;
using Planalto.Core.Characters;
using Planalto.Core.Municipios;

namespace Planalto.Core.Events;

/// <param name="SemRastro">Escolhas marcadas como "sem_rastro", com o motivo de cada uma.</param>
public sealed record LintReport(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, IReadOnlyDictionary<string, (int Count, int Target)> Distribution, IReadOnlyList<string>? SemRastro = null);

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

    /// <summary>Limite do rótulo de uma escolha (ART_GUIDE, Interface).</summary>
    public const int MaxLabel = 40;

    /// <summary>Tamanho mínimo do motivo de uma escolha marcada como "sem_rastro".</summary>
    public const int MinSemRastro = 15;

    /// <summary>Categorias em que um evento de morte pode estar (GDD 17.16): nunca entre os cômicos nem nas festas.</summary>
    public static readonly IReadOnlyList<string> DeathCategories = ["vida_pessoal", "bastidores", "campanha", "mandato_legislativo", "mandato_executivo"];

    /// <summary>Palavras que indicam que o texto fala de morte: o evento tem de estar marcado como luto.</summary>
    private static readonly string[] DeathWords = ["morte", "morreu", "morrer", "faleceu", "falecimento", "falecid", "velório", "velorio", "enterro", "sepultamento", "óbito", "obito", "luto"];

    private static readonly string[] HumorWords = ["kkk", "haha", "rsrs", "piada", "engraçad", "hilári", "hilari", "cômic", "comic", "risada", "meme", "brincadeira"];

    private static readonly string[] SensationalWords = ["brutal", "horrív", "horriv", "chocante", "macabr", "sangrent", "sangue", "terrível", "terrivel", "trágico fim", "escândalo", "escandalo", "bomba", "urgente", "assustador"];

    private static readonly string[] MockeryWords = ["zomb", "ironiz", "deboch", "comemor", "bem feito", "já vai tarde", "ja vai tarde", "atacar a memória", "atacar a memoria", "ridiculariz", "tirar proveito"];

    /// <summary>O texto fala de morte (para saber quais textos passam pelas regras de luto).</summary>
    public static bool MentionsDeath(string text)
    {
        string lower = text.ToLowerInvariant();
        return DeathWords.Any(w => System.Text.RegularExpressions.Regex.IsMatch(lower, $@"(?<![\p{{L}}]){System.Text.RegularExpressions.Regex.Escape(w)}"));
    }

    /// <summary>
    /// Regras de texto de morte (GDD 17.16, "morte tratada com seriedade"): sóbrio (sem exclamação nem palavra em
    /// caixa alta), sem humor, sem causa sensacionalista e sem zombar do morto. Serve a todo texto de morte de data/:
    /// abertura, resultado e rótulo de escolha de evento, manchete e texto de luto. Devolve os problemas encontrados.
    /// </summary>
    public static IReadOnlyList<string> CheckDeathText(string text)
    {
        var errors = new List<string>();
        string lower = text.ToLowerInvariant();
        if (text.Contains('!', StringComparison.Ordinal))
        {
            errors.Add("texto de morte com exclamação: o tom é sóbrio.");
        }

        if (text.Split([' ', ',', '.', ';', ':', '(', ')', '"'], StringSplitOptions.RemoveEmptyEntries).Any(w => w.Length >= 4 && w.All(char.IsLetter) && w.ToUpperInvariant() == w))
        {
            errors.Add("texto de morte com palavra em caixa alta: o tom é sóbrio.");
        }

        foreach (string word in HumorWords.Where(w => lower.Contains(w, StringComparison.Ordinal)))
        {
            errors.Add($"texto de morte com humor ('{word}').");
        }

        foreach (string word in SensationalWords.Where(w => lower.Contains(w, StringComparison.Ordinal)))
        {
            errors.Add($"texto de morte com causa ou tom sensacionalista ('{word}').");
        }

        foreach (string word in MockeryWords.Where(w => lower.Contains(w, StringComparison.Ordinal)))
        {
            errors.Add($"texto de morte que zomba do morto ou tira proveito ('{word}').");
        }

        return errors;
    }

    /// <summary>As regras de morte num evento inteiro: categoria e tom de todos os textos, inclusive os rótulos das escolhas.</summary>
    public static IReadOnlyList<string> CheckDeathEvent(EventDefinition e)
    {
        var errors = new List<string>();
        List<string> texts = Texts(e).ToList();
        bool mentions = texts.Any(MentionsDeath);
        if (mentions && !e.Luto)
        {
            errors.Add("fala de morte sem estar marcado como 'luto'.");
        }

        if (!e.Luto)
        {
            return errors;
        }

        if (!DeathCategories.Contains(e.Category))
        {
            errors.Add($"evento de luto na categoria '{e.Category}'; as permitidas são {string.Join(", ", DeathCategories)}.");
        }

        foreach (string text in texts)
        {
            errors.AddRange(CheckDeathText(text));
        }

        return errors;
    }

    /// <summary>
    /// Regra de rastro em todo evento (GDD 17.1, princípio d): cada escolha grava uma lembrança em alguém ou uma
    /// entrada no Livro, qualquer que seja o desfecho, ou traz a marca "sem_rastro" com o motivo.
    /// </summary>
    public static IEnumerable<string> CheckTrace(EventDefinition e)
    {
        for (int i = 0; i < e.Choices.Count; i++)
        {
            EventChoice choice = e.Choices[i];
            bool traced = choice.Effects.LeavesTrace || (choice.Test is { } test && test.Success.LeavesTrace && test.Failure.LeavesTrace);
            string where = $"{e.Id}, escolha {i + 1}";
            if (choice.SemRastro is null && !traced)
            {
                yield return $"{where}: sem rastro (nem lembrança em alguém, nem entrada no Livro) e sem a marca 'sem_rastro' com o motivo.";
            }
            else if (choice.SemRastro is not null && choice.SemRastro.Trim().Length < MinSemRastro)
            {
                yield return $"{where}: 'sem_rastro' precisa dizer o motivo.";
            }
            else if (choice.SemRastro is not null && (traced || choice.Relevant))
            {
                yield return $"{where}: 'sem_rastro' numa escolha relevante ou que deixa rastro.";
            }
        }
    }

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

            errors.AddRange(CheckTrace(e));
            errors.AddRange(CheckDeathEvent(e).Select(error => $"{where}: {error}"));

            // A energia semanal saiu com a agenda (GDD 17.6): evento não mexe mais nela; o custo de tempo é estresse.
            if (AllEffects(e).Any(fx => fx.Energy != 0))
            {
                errors.Add($"{where}: usa 'energy', que não existe mais; use 'stress'.");
            }

            foreach (Campaign.Effects fx in AllEffects(e))
            {
                foreach (Campaign.MemoryEffect memory in fx.Memories)
                {
                    if (!Campaign.EventRole.People.Contains(memory.Quem) || memory.Peso is < -3 or > 3 || memory.Peso == 0 || string.IsNullOrWhiteSpace(memory.Texto) || Math.Sign(memory.Pontos) != Math.Sign(memory.Peso))
                    {
                        errors.Add($"{where}: lembrança '{memory.Fato}' inválida (papel conhecido, peso de −3 a 3, pontos com o mesmo sinal e texto).");
                    }
                }

                if (fx.Promise is { } promise && promise.Para != "zona" && !Campaign.EventRole.People.Contains(promise.Para))
                {
                    errors.Add($"{where}: promessa para '{promise.Para}', que não é um papel do evento.");
                }

                if (fx.Debt is { } debt && !Campaign.EventRole.People.Contains(debt.Para))
                {
                    errors.Add($"{where}: dívida com '{debt.Para}', que não é um papel do evento.");
                }

                foreach (string role in fx.SkeletonKnownBy.Where(r => !Campaign.EventRole.People.Contains(r)))
                {
                    errors.Add($"{where}: esqueleto conhecido por '{role}', que não é um papel do evento.");
                }
            }

            foreach (EventChoice choice in e.Choices.Where(ch => ch.Label.Length > MaxLabel))
            {
                errors.Add($"{where}: rótulo de escolha com mais de {MaxLabel} caracteres: \"{choice.Label}\".");
            }

            if (e.Conditions.Trigger is not null && e.ChainOnly)
            {
                errors.Add($"{where}: evento dinâmico (disparado por fato) não pode ser só de cadeia.");
            }

            if (e.Conditions.ZoneKind is not (null or "urbana" or "rural") || e.Conditions.OutletKind is not (null or "emissora" or "escrita" or "rede"))
            {
                errors.Add($"{where}: zone_kind ou outlet_kind desconhecido.");
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

        // Troca (princípio e): em toda escolha relevante, ganho e custo de naturezas diferentes, sustentados pelos efeitos.
        errors.AddRange(Campaign.DecisionLint.Events(content));

        List<string> untraced = events.SelectMany(e => e.Choices.Select((c, i) => (e, c, i))).Where(x => x.c.SemRastro is not null)
            .Select(x => $"{x.e.Id}, escolha {x.i + 1} (\"{x.c.Label}\"): {x.c.SemRastro}").ToList();
        var distribution = Targets.ToDictionary(t => t.Key, t => (events.Count(e => e.Category == t.Key), t.Value), StringComparer.Ordinal);
        return new LintReport(errors.Distinct().ToList(), warnings, distribution, untraced);
    }

    private static IEnumerable<Campaign.Effects> AllEffects(EventDefinition e) =>
        e.Choices.SelectMany(c => c.Test is null ? [c.Effects] : new[] { c.Effects, c.Test.Success, c.Test.Failure });

    private static IEnumerable<string> Texts(EventDefinition e) =>
        e.Variants.Append(e.Title).Concat(e.Choices.SelectMany(c => new[] { c.Label, c.Result, c.Test?.SuccessText, c.Test?.FailureText }).Where(t => t is not null).Select(t => t!));

    [GeneratedRegex(@"\{([^{}]*)\}")]
    private static partial Regex VariablePattern();
}
