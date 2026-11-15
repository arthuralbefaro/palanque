using System.Text;
using Planalto.Core.Ideology;
using Planalto.Core.State;

namespace Planalto.Core.Parties;

public sealed record GeneratedName(string Name, string Sigla);

/// <summary>Partido gerado por inteiro, exceto o tamanho, que depende da predefinição do mundo.</summary>
public sealed record GeneratedParty(
    string Name,
    string Sigla,
    int Number,
    string FamilyId,
    IdeologicalPosition Position,
    PartyColors Colors,
    string LogoIcon,
    LeadershipStyle Leadership);

/// <summary>Nomes, siglas, números e cores já usados num mundo, para o gerador não repetir.</summary>
public sealed class TakenIdentities(PartyBlocklist blocklist)
{
    private readonly HashSet<string> _siglas = new(StringComparer.Ordinal);
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly HashSet<int> _numbers = [];
    private readonly List<OkLab> _primaryColors = [];

    public IReadOnlySet<int> Numbers => _numbers;

    public IReadOnlyList<OkLab> PrimaryColors => _primaryColors;

    public bool HasSigla(string sigla) => _siglas.Contains(PartyBlocklist.NormalizeSigla(sigla));

    public bool HasName(string name) => _names.Contains(blocklist.NormalizeName(name));

    public void Add(string? name, string? sigla, int? number, PartyColors? colors)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            _names.Add(blocklist.NormalizeName(name));
        }

        if (!string.IsNullOrWhiteSpace(sigla))
        {
            _siglas.Add(PartyBlocklist.NormalizeSigla(sigla));
        }

        if (number is int n)
        {
            _numbers.Add(n);
        }

        if (colors is not null && OkColor.TryParseHex(colors.Primary, out OkLab primary))
        {
            _primaryColors.Add(primary);
        }
    }
}

/// <summary>
/// Gera partidos fictícios (GDD 14.1 e 16.1). Toda escolha passa pelo <see cref="Rng"/> recebido,
/// então a mesma semente e o mesmo mundo produzem sempre os mesmos partidos.
/// </summary>
public sealed class PartyGenerator(PartyContent content)
{
    public PartyContent Content { get; } = content;

    private PartyGeneratorConfig Config => Content.Generator;

    /// <summary>
    /// Nome coerente com a família e sigla derivada, fora da lista de bloqueio e sem repetir nome
    /// ou sigla do mundo. Null se nenhuma combinação livre sair em names.max_attempts tentativas.
    /// </summary>
    public GeneratedName? GenerateName(Rng rng, FamilyDefinition family, TakenIdentities taken)
    {
        NameTermCatalog terms = Content.NameTerms;
        var pool = terms.Qualifiers.Where(q => QualifierWeight(q, family) > 0).ToList();

        for (int attempt = 0; attempt < Config.Names.MaxAttempts; attempt++)
        {
            NameHead head = rng.PickWeighted(terms.Heads, h => h.Weight);
            var headPool = pool.Where(q => !q.ExcludedHeads.Contains(head.Text)).ToList();
            NameQualifier first = rng.PickWeighted(headPool, q => QualifierWeight(q, family));
            var words = new List<string> { head.Text, first.For(head.Gender) };

            if (headPool.Count > 1 && rng.Chance(Config.Names.SecondQualifierChance))
            {
                var rest = headPool.Where(q => q != first).ToList();
                words.Add(rng.PickWeighted(rest, q => QualifierWeight(q, family)).For(head.Gender));
            }

            if (terms.Suffixes.Count > 0 && rng.Chance(Config.Names.SuffixChance))
            {
                words.Add(rng.Pick(terms.Suffixes).For(head.Gender));
            }

            string name = string.Join(' ', words);
            string sigla = DeriveSigla(name);
            if (IsSiglaLengthValid(sigla)
                && !Content.Blocklist.IsBlocked(name, sigla)
                && !taken.HasSigla(sigla)
                && !taken.HasName(name))
            {
                return new GeneratedName(name, sigla);
            }
        }

        return null;
    }

    /// <summary>Sigla com as iniciais das palavras do nome, sem preposições e artigos ("Frente Liberal do Brasil" → "FLB").</summary>
    public string DeriveSigla(string name)
    {
        var builder = new StringBuilder();
        foreach (string word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Content.Blocklist.NormalizeName(word).Length == 0)
            {
                continue;
            }

            builder.Append(PartyBlocklist.NormalizeSigla(word[..1]));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Sigla livre para um nome já escolhido: as iniciais e, se estiverem bloqueadas ou em uso,
    /// variantes que estendem uma inicial com a letra seguinte da palavra ("FLB" → "FLBr").
    /// Null se nenhuma variante servir.
    /// </summary>
    public string? PickSigla(string name, TakenIdentities taken)
    {
        foreach (string candidate in SiglaCandidates(name))
        {
            if (IsSiglaLengthValid(candidate) && Content.Blocklist.CheckSigla(candidate) is null && !taken.HasSigla(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public bool IsSiglaLengthValid(string sigla)
    {
        int length = PartyBlocklist.NormalizeSigla(sigla).Length;
        return length >= Config.Sigla.MinLength && length <= Config.Sigla.MaxLength;
    }

    /// <summary>
    /// Posição sorteada dentro da faixa da família. A família de faixa variável sorteia,
    /// pelo peso, a faixa de outra família.
    /// </summary>
    public IdeologicalPosition GeneratePosition(Rng rng, FamilyDefinition family)
    {
        FamilyDefinition source = family.HasVariableRange
            ? rng.PickWeighted(Content.Families.FixedRangeFamilies.ToList(), f => f.Weight)
            : family;

        return new IdeologicalPosition(
            rng.NextInt(source.Economic!.Min, source.Economic.Max + 1),
            rng.NextInt(source.Social!.Min, source.Social.Max + 1));
    }

    /// <summary>
    /// Primária distante das outras primárias do mundo e secundária contrastante. Se não acha cor livre,
    /// relaxa a distância aos poucos até colors.min_distance_floor, abaixo da qual nunca aceita.
    /// </summary>
    public PartyColors GenerateColors(Rng rng, TakenIdentities taken)
    {
        ColorGenerationConfig config = Config.Colors;
        double distance = config.MinDistance;
        while (true)
        {
            for (int attempt = 0; attempt < config.AttemptsPerLevel; attempt++)
            {
                OkLab candidate = OkColor.FromOkLch(
                    rng.NextDouble(config.Lightness.Min, config.Lightness.Max),
                    rng.NextDouble(config.Chroma.Min, config.Chroma.Max),
                    rng.NextDouble(0, 360));
                string? hex = OkColor.ToHex(candidate);
                if (hex is null || !OkColor.TryParseHex(hex, out OkLab primary))
                {
                    continue;
                }

                if (taken.PrimaryColors.All(c => c.DistanceTo(primary) >= distance))
                {
                    return new PartyColors(hex, GenerateSecondary(rng, primary));
                }
            }

            if (distance <= config.MinDistanceFloor)
            {
                throw new InvalidOperationException(
                    "Sem cor livre acima de colors.min_distance_floor; o mundo tem partidos demais para a faixa de cores configurada.");
            }

            distance = Math.Max(config.MinDistanceFloor, distance * config.RelaxFactor);
        }
    }

    /// <summary>Ícone sorteado entre os que servem à família.</summary>
    public string PickLogoIcon(Rng rng, string familyId) => rng.Pick(Content.LogoIcons.ForFamily(familyId)).Id;

    public LeadershipStyle PickLeadership(Rng rng) =>
        rng.PickWeighted(Enum.GetValues<LeadershipStyle>(), s => Config.LeadershipWeights[s]);

    public static PartySize PickSize(Rng rng, IReadOnlyDictionary<PartySize, int> weights) =>
        rng.PickWeighted(Enum.GetValues<PartySize>(), s => weights.TryGetValue(s, out int w) ? w : 0);

    /// <summary>Número livre entre min e max, inclusive; null se todos estiverem usados.</summary>
    public static int? PickNumber(Rng rng, TakenIdentities taken, int min, int max)
    {
        var free = Enumerable.Range(min, max - min + 1).Where(n => !taken.Numbers.Contains(n)).ToList();
        return free.Count == 0 ? null : rng.Pick(free);
    }

    /// <summary>Partido completo da família, já registrado em <paramref name="taken"/>; null se faltar nome ou número livre.</summary>
    public GeneratedParty? Generate(Rng rng, FamilyDefinition family, TakenIdentities taken, int numberMin, int numberMax)
    {
        GeneratedName? name = GenerateName(rng, family, taken);
        int? number = PickNumber(rng, taken, numberMin, numberMax);
        if (name is null || number is null)
        {
            return null;
        }

        PartyColors colors = GenerateColors(rng, taken);
        var party = new GeneratedParty(
            name.Name,
            name.Sigla,
            number.Value,
            family.Id,
            GeneratePosition(rng, family),
            colors,
            PickLogoIcon(rng, family.Id),
            PickLeadership(rng));
        taken.Add(party.Name, party.Sigla, party.Number, party.Colors);
        return party;
    }

    private IEnumerable<string> SiglaCandidates(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => Content.Blocklist.NormalizeName(w).Length > 0)
            .Select(w => PartyBlocklist.NormalizeSigla(w))
            .Where(w => w.Length > 0)
            .ToList();
        if (words.Count == 0)
        {
            yield break;
        }

        string Initials(int extended, int extraLetters) => string.Concat(words.Select((w, i) =>
            i == extended ? w[..1] + w.Substring(1, Math.Min(extraLetters, w.Length - 1)).ToLowerInvariant() : w[..1]));

        yield return Initials(-1, 0);
        for (int extra = 1; extra <= 2; extra++)
        {
            for (int i = words.Count - 1; i >= 0; i--)
            {
                if (words[i].Length > extra)
                {
                    yield return Initials(i, extra);
                }
            }
        }
    }

    private double QualifierWeight(NameQualifier qualifier, FamilyDefinition family)
    {
        if (qualifier.IsGeneric)
        {
            return Config.Names.GenericTermWeight;
        }

        return qualifier.Families.Contains(family.Id) ? Config.Names.FamilyTermWeight : 0;
    }

    private string GenerateSecondary(Rng rng, OkLab primary)
    {
        ColorGenerationConfig config = Config.Colors;
        for (int attempt = 0; attempt < config.AttemptsPerLevel; attempt++)
        {
            // Claro, escuro ou de matiz oposto, sempre contrastando com a primária.
            OkLab candidate = rng.NextInt(3) switch
            {
                0 => Neutral(rng, config.SecondaryLight),
                1 => Neutral(rng, config.SecondaryDark),
                _ => OkColor.FromOkLch(
                    rng.NextDouble(config.Lightness.Min, config.Lightness.Max),
                    rng.NextDouble(config.Chroma.Min, config.Chroma.Max),
                    (Math.Atan2(primary.B, primary.A) * 180 / Math.PI) + 180 + rng.NextDouble(-40, 40)),
            };

            string? hex = OkColor.ToHex(candidate);
            if (hex is not null && OkColor.TryParseHex(hex, out OkLab secondary) && secondary.DistanceTo(primary) >= config.SecondaryMinDistance)
            {
                return hex;
            }
        }

        // Neutro claro ou escuro, o que contrastar mais.
        ToneConfig fallback = primary.L < (config.Lightness.Min + config.Lightness.Max) / 2 ? config.SecondaryLight : config.SecondaryDark;
        return OkColor.ToHex(OkColor.FromOkLch((fallback.Lightness.Min + fallback.Lightness.Max) / 2, 0, 0))!;
    }

    private static OkLab Neutral(Rng rng, ToneConfig tone) =>
        OkColor.FromOkLch(rng.NextDouble(tone.Lightness.Min, tone.Lightness.Max), rng.NextDouble(tone.Chroma.Min, tone.Chroma.Max), rng.NextDouble(0, 360));
}
