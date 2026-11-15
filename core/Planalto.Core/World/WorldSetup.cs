using Planalto.Core.Ideology;
using Planalto.Core.Parties;
using Planalto.Core.State;

namespace Planalto.Core.World;

/// <summary>Resultado de criar um mundo a partir de uma predefinição.</summary>
public sealed record WorldSetupCreation(WorldSetup? Setup, WorldValidationReport Report);

/// <summary>
/// Configuração do mundo antes da criação de personagem (GDD 16.1): lista de partidos editável,
/// sorteio por campo, por linha ou geral (sempre respeitando as travas) e opções gerais.
/// </summary>
public sealed class WorldSetup
{
    private readonly List<PartyDraft> _parties;
    private readonly PartyGenerator _generator;

    private WorldSetup(WorldContent content, string presetId, WorldOptions options, List<PartyDraft> parties, int nextSerial)
    {
        Content = content;
        PresetId = presetId;
        Options = options;
        _parties = parties;
        NextSerial = nextSerial;
        _generator = new PartyGenerator(content.Parties);
    }

    public WorldContent Content { get; }

    public string PresetId { get; private set; }

    public WorldOptions Options { get; }

    public IReadOnlyList<PartyDraft> Parties => _parties;

    /// <summary>Próximo número usado nos ids "p{n}"; ids nunca são reaproveitados.</summary>
    public int NextSerial { get; private set; }

    public WorldPreset Preset => Content.Presets.Find(PresetId) ?? Content.Presets.Custom;

    /// <summary>Limite geral de partidos, dado pela predefinição livre (Personalizado).</summary>
    public int MinParties => Content.Presets.Custom.MinParties!.Value;

    public int MaxParties => Content.Presets.Custom.MaxParties!.Value;

    private WorldPreset GenerationSource => Content.Presets.GenerationSourceOf(Preset);

    private FamilyCatalog Families => Content.Parties.Families;

    public static WorldSetupCreation Create(WorldContent content, string presetId, Rng rng)
    {
        WorldPreset? preset = content.Presets.Find(presetId);
        if (preset is null)
        {
            return new WorldSetupCreation(null, Report(new WorldIssue(WorldIssueCode.PresetDesconhecida, WorldIssueSeverity.Error, Detail: presetId)));
        }

        if (!preset.Available)
        {
            return new WorldSetupCreation(null, Report(new WorldIssue(WorldIssueCode.PresetIndisponivel, WorldIssueSeverity.Error, Detail: preset.UnavailableReason)));
        }

        var setup = new WorldSetup(content, presetId, content.Options.Defaults.Clone(), [], 1);
        setup.RestorePreset(rng);
        return new WorldSetupCreation(setup, setup.Validate());
    }

    /// <summary>Recria o mundo salvo, sem sortear nada.</summary>
    internal static WorldSetup Restore(WorldContent content, string presetId, WorldOptions options, List<PartyDraft> parties, int nextSerial) =>
        new(content, presetId, options, parties, nextSerial);

    public PartyDraft? Find(string partyId) => _parties.FirstOrDefault(p => p.Id == partyId);

    /// <summary>Descarta a lista (inclusive travas) e gera de novo a da predefinição atual.</summary>
    public void RestorePreset(Rng rng)
    {
        WorldPreset source = GenerationSource;
        int count = rng.NextInt(source.MinParties!.Value, source.MaxParties!.Value + 1);
        _parties.Clear();
        for (int i = 0; i < count; i++)
        {
            _parties.Add(NewPlaceholder());
        }

        RandomizeAll(rng);
    }

    /// <summary>Sorteia todos os campos não travados de todos os partidos, cobrindo o espectro.</summary>
    public void RandomizeAll(Rng rng)
    {
        var pending = _parties.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        AssignFamilies(_parties.Where(p => !p.IsLocked(PartyField.Family)).ToList(), rng);

        foreach (PartyDraft party in _parties)
        {
            FillParty(party, rng, pending, includeFamily: false, includeSize: false);
            pending.Remove(party.Id);
        }

        AssignSizes(_parties.Where(p => !p.IsLocked(PartyField.Size)).ToList(), rng);
        EnsureSpectrumCoverage(rng);
    }

    /// <summary>Sorteia a linha inteira, respeitando as travas.</summary>
    public bool RandomizeParty(string partyId, Rng rng)
    {
        PartyDraft? party = Find(partyId);
        if (party is null)
        {
            return false;
        }

        FillParty(party, rng, pending: null, includeFamily: true, includeSize: true);
        return true;
    }

    /// <summary>
    /// Sorteia um único campo; não faz nada (false) se ele estiver travado. Nome leva junto a sigla
    /// e família leva junto a posição, quando destravadas, para a linha continuar coerente.
    /// </summary>
    public bool RandomizeField(string partyId, PartyField field, Rng rng)
    {
        PartyDraft? party = Find(partyId);
        if (party is null || party.IsLocked(field))
        {
            return false;
        }

        TakenIdentities taken = BuildTaken(party, pending: null);
        switch (field)
        {
            case PartyField.Family:
                party.FamilyId = ChooseFamily(party, rng).Id;
                if (!party.IsLocked(PartyField.Position))
                {
                    party.Position = _generator.GeneratePosition(rng, Families.Get(party.FamilyId));
                }

                break;
            case PartyField.Position:
                party.Position = _generator.GeneratePosition(rng, Families.Get(party.FamilyId));
                break;
            case PartyField.Name:
                GenerateNameAndSigla(party, rng, taken, regenerateName: true);
                break;
            case PartyField.Sigla:
                party.Sigla = _generator.PickSigla(party.Name, taken) ?? _generator.DeriveSigla(party.Name);
                break;
            case PartyField.Number:
                party.Number = PickNumber(rng, taken) ?? party.Number;
                break;
            case PartyField.Size:
                party.Size = PartyGenerator.PickSize(rng, GenerationSource.SizeWeights);
                break;
            case PartyField.Visual:
                GenerateVisual(party, rng, taken);
                break;
            case PartyField.Leadership:
                party.Leadership = _generator.PickLeadership(rng);
                break;
        }

        return true;
    }

    public bool SetLocked(string partyId, PartyField field, bool locked)
    {
        PartyDraft? party = Find(partyId);
        party?.SetLocked(field, locked);
        return party is not null;
    }

    /// <summary>Adiciona um partido sorteado; null se o mundo já estiver no limite geral.</summary>
    public PartyDraft? AddParty(Rng rng)
    {
        if (_parties.Count >= MaxParties)
        {
            return null;
        }

        PartyDraft party = NewPlaceholder();
        _parties.Add(party);
        FillParty(party, rng, pending: null, includeFamily: true, includeSize: true);
        SwitchToCustomIfOutOfRange();
        return party;
    }

    public bool RemoveParty(string partyId)
    {
        PartyDraft? party = Find(partyId);
        if (party is null || _parties.Count <= MinParties)
        {
            return false;
        }

        _parties.Remove(party);
        SwitchToCustomIfOutOfRange();
        return true;
    }

    /// <summary>
    /// Cópia de família, posição, tamanho, logotipo e cúpula, com nome, sigla, número e cores novos
    /// (que não podem se repetir) e sem travas.
    /// </summary>
    public PartyDraft? DuplicateParty(string partyId, Rng rng)
    {
        PartyDraft? original = Find(partyId);
        if (original is null || _parties.Count >= MaxParties)
        {
            return null;
        }

        PartyDraft copy = original.Clone(NextId());
        copy.Locks.Clear();
        _parties.Insert(_parties.IndexOf(original) + 1, copy);

        TakenIdentities taken = BuildTaken(copy, pending: null);
        GenerateNameAndSigla(copy, rng, taken, regenerateName: true);
        taken.Add(copy.Name, copy.Sigla, null, null);
        copy.Number = PickNumber(rng, taken) ?? copy.Number;
        copy.Colors = _generator.GenerateColors(rng, taken);
        SwitchToCustomIfOutOfRange();
        return copy;
    }

    public WorldValidationReport Validate() => WorldSetupValidator.Validate(this);

    private static WorldValidationReport Report(params WorldIssue[] issues) => new(issues);

    private PartyDraft NewPlaceholder() => new()
    {
        Id = NextId(),
        Name = "",
        Sigla = "",
        Number = 0,
        FamilyId = Families.Families[0].Id,
        Position = default,
        Size = PartySize.Medio,
        Colors = new PartyColors("", ""),
        LogoIcon = "",
        Leadership = LeadershipStyle.Pragmatica,
    };

    private string NextId() => $"p{NextSerial++}";

    private void SwitchToCustomIfOutOfRange()
    {
        WorldPreset preset = Preset;
        if (!preset.Custom && (_parties.Count < preset.MinParties || _parties.Count > preset.MaxParties))
        {
            PresetId = Content.Presets.Custom.Id;
        }
    }

    /// <summary>Gera os campos não travados da linha, na ordem em que um depende do outro.</summary>
    private void FillParty(PartyDraft party, Rng rng, ISet<string>? pending, bool includeFamily, bool includeSize)
    {
        if (includeFamily && !party.IsLocked(PartyField.Family))
        {
            party.FamilyId = ChooseFamily(party, rng).Id;
        }

        if (!party.IsLocked(PartyField.Position))
        {
            party.Position = _generator.GeneratePosition(rng, Families.Get(party.FamilyId));
        }

        TakenIdentities taken = BuildTaken(party, pending);
        GenerateNameAndSigla(party, rng, taken, regenerateName: !party.IsLocked(PartyField.Name));

        if (!party.IsLocked(PartyField.Number))
        {
            party.Number = PickNumber(rng, taken) ?? party.Number;
        }

        if (!party.IsLocked(PartyField.Visual))
        {
            GenerateVisual(party, rng, taken);
        }

        if (!party.IsLocked(PartyField.Leadership))
        {
            party.Leadership = _generator.PickLeadership(rng);
        }

        if (includeSize && !party.IsLocked(PartyField.Size))
        {
            party.Size = PartyGenerator.PickSize(rng, GenerationSource.SizeWeights);
        }
    }

    private void GenerateNameAndSigla(PartyDraft party, Rng rng, TakenIdentities taken, bool regenerateName)
    {
        bool siglaLocked = party.IsLocked(PartyField.Sigla);
        if (regenerateName)
        {
            GeneratedName? generated = _generator.GenerateName(rng, Families.Get(party.FamilyId), taken);
            if (generated is not null)
            {
                party.Name = generated.Name;
                if (!siglaLocked)
                {
                    party.Sigla = generated.Sigla;
                }

                return;
            }
        }

        if (!siglaLocked && party.Name.Length > 0)
        {
            party.Sigla = _generator.PickSigla(party.Name, taken) ?? _generator.DeriveSigla(party.Name);
        }
    }

    private void GenerateVisual(PartyDraft party, Rng rng, TakenIdentities taken)
    {
        party.Colors = _generator.GenerateColors(rng, taken);

        // Prefere ícones que ainda não aparecem no mundo.
        var used = _parties.Where(p => p != party).Select(p => p.LogoIcon).ToHashSet(StringComparer.Ordinal);
        var unused = Content.Parties.LogoIcons.Icons.Where(i => !used.Contains(i.Id)).ToList();
        party.LogoIcon = unused.Count > 0 ? rng.Pick(unused).Id : _generator.PickLogoIcon(rng);
    }

    private int? PickNumber(Rng rng, TakenIdentities taken) =>
        PartyGenerator.PickNumber(rng, taken, Content.NumberRules.PartyNumberMin, Content.NumberRules.PartyNumberMax);

    /// <summary>
    /// Identidades usadas pelos outros partidos. Os ainda pendentes num sorteio geral só contam
    /// com os campos travados, porque os demais vão ser sorteados de novo.
    /// </summary>
    private TakenIdentities BuildTaken(PartyDraft exclude, ISet<string>? pending)
    {
        var taken = new TakenIdentities(Content.Parties.Blocklist);
        foreach (PartyDraft other in _parties.Where(p => p != exclude))
        {
            bool all = pending is null || !pending.Contains(other.Id);
            taken.Add(
                all || other.IsLocked(PartyField.Name) ? other.Name : null,
                all || other.IsLocked(PartyField.Sigla) ? other.Sigla : null,
                all || other.IsLocked(PartyField.Number) ? other.Number : null,
                all || other.IsLocked(PartyField.Visual) ? other.Colors : null);
        }

        return taken;
    }

    private double FamilyWeight(FamilyDefinition family) => family.Weight * GenerationSource.FamilyMultiplier(family.Id);

    /// <summary>
    /// Família para uma linha só: compatível com a posição travada, preferindo famílias ausentes do mundo
    /// e, se for o único partido de um lado do eixo econômico, uma família do mesmo lado.
    /// </summary>
    private FamilyDefinition ChooseFamily(PartyDraft party, Rng rng)
    {
        IEnumerable<FamilyDefinition> candidates = Families.Families;
        if (party.IsLocked(PartyField.Position))
        {
            candidates = candidates.Where(f => f.Contains(party.Position));
        }

        var others = _parties.Where(p => p != party && p.Name.Length > 0).ToList();
        if (others.Count > 0 && !party.IsLocked(PartyField.Position))
        {
            if (!others.Any(p => p.Position.Economic < 0))
            {
                candidates = PreferIfAny(candidates, IsStrictlyLeft);
            }
            else if (!others.Any(p => p.Position.Economic > 0))
            {
                candidates = PreferIfAny(candidates, IsStrictlyRight);
            }
        }

        var used = others.Select(p => p.FamilyId).ToHashSet(StringComparer.Ordinal);
        var list = PreferIfAny(candidates, f => !used.Contains(f.Id)).ToList();
        return list.Count == 0 ? rng.PickWeighted(Families.Families, FamilyWeight) : rng.PickWeighted(list, FamilyWeight);
    }

    /// <summary>
    /// Famílias para um sorteio geral: com partidos suficientes, cada família aparece ao menos uma vez
    /// (GDD 14.1); senão, sorteio ponderado sem repetição enquanto houver famílias livres.
    /// </summary>
    private void AssignFamilies(List<PartyDraft> targets, Rng rng)
    {
        // Posição travada restringe a família; essas linhas escolhem primeiro.
        foreach (PartyDraft party in targets.Where(p => p.IsLocked(PartyField.Position)))
        {
            party.FamilyId = rng.PickWeighted(Families.Families.Where(f => f.Contains(party.Position)).ToList(), FamilyWeight).Id;
        }

        var free = rng.Shuffled(targets.Where(p => !p.IsLocked(PartyField.Position)));
        var used = _parties.Where(p => !free.Contains(p)).Select(p => p.FamilyId).ToHashSet(StringComparer.Ordinal);
        bool coverAll = _parties.Count >= Families.Families.Count;

        foreach (PartyDraft party in free)
        {
            var unused = Families.Families.Where(f => !used.Contains(f.Id)).ToList();
            FamilyDefinition family = unused.Count == 0
                ? rng.PickWeighted(Families.Families, FamilyWeight)
                : coverAll ? rng.Pick(unused) : rng.PickWeighted(unused, FamilyWeight);
            party.FamilyId = family.Id;
            used.Add(family.Id);
        }
    }

    /// <summary>
    /// Tamanhos pela distribuição da predefinição (rateio por maiores restos), garantindo um partido
    /// principal e no máximo o número de dominantes de generator.json. As famílias com viés recebem
    /// os maiores tamanhos com mais frequência.
    /// </summary>
    private void AssignSizes(List<PartyDraft> targets, Rng rng)
    {
        if (targets.Count == 0)
        {
            return;
        }

        SizeWarningConfig limits = Content.Parties.Generator.SizeWarnings;
        Dictionary<PartySize, int> counts = Apportion(_parties.Count, GenerationSource.SizeWeights);

        if (!counts.Any(c => c.Key >= limits.MinMainSize && c.Value > 0))
        {
            PartySize smallest = counts.Where(c => c.Value > 0).Min(c => c.Key);
            counts[smallest]--;
            counts[limits.MinMainSize]++;
        }

        if (counts[PartySize.Dominante] > limits.MaxDominant)
        {
            counts[PartySize.Grande] += counts[PartySize.Dominante] - limits.MaxDominant;
            counts[PartySize.Dominante] = limits.MaxDominant;
        }

        foreach (PartyDraft locked in _parties.Where(p => p.IsLocked(PartyField.Size)))
        {
            if (counts[locked.Size] > 0)
            {
                counts[locked.Size]--;
            }
        }

        var sizes = counts.OrderByDescending(c => c.Key).SelectMany(c => Enumerable.Repeat(c.Key, c.Value)).Take(targets.Count).ToList();
        while (sizes.Count < targets.Count)
        {
            sizes.Add(PartyGenerator.PickSize(rng, GenerationSource.SizeWeights));
        }

        sizes.Sort((a, b) => b.CompareTo(a));

        // Ordem ponderada (Efraimidis–Spirakis): chave u^(1/viés), maiores chaves pegam os maiores tamanhos.
        var ordered = targets
            .Select(p => (Party: p, Key: Math.Pow(rng.NextDouble(), 1 / GenerationSource.SizeBias(p.FamilyId))))
            .OrderByDescending(x => x.Key)
            .Select(x => x.Party)
            .ToList();
        for (int i = 0; i < ordered.Count; i++)
        {
            ordered[i].Size = sizes[i];
        }
    }

    /// <summary>
    /// Garante ao menos um partido de cada lado do eixo econômico, trocando a família de linhas com
    /// família e posição destravadas. Se as travas impedirem, a validação emite o aviso.
    /// </summary>
    private void EnsureSpectrumCoverage(Rng rng)
    {
        if (_parties.Count < 2)
        {
            return;
        }

        foreach (bool left in new[] { true, false })
        {
            bool covered = _parties.Any(p => left ? p.Position.Economic < 0 : p.Position.Economic > 0);
            if (covered)
            {
                continue;
            }

            // Tira de quem não faz falta do outro lado: primeiro posições em zero, depois o lado com sobra.
            var candidates = _parties
                .Where(p => !p.IsLocked(PartyField.Family) && !p.IsLocked(PartyField.Position))
                .OrderBy(p => p.Position.Economic == 0 ? 0 : 1)
                .ThenBy(_ => rng.NextDouble())
                .ToList();
            int otherSide = _parties.Count(p => left ? p.Position.Economic > 0 : p.Position.Economic < 0);
            PartyDraft? chosen = candidates.FirstOrDefault(p => p.Position.Economic == 0 || otherSide > 1);
            if (chosen is null)
            {
                continue;
            }

            FamilyDefinition family = rng.PickWeighted(Families.FixedRangeFamilies.Where(left ? IsStrictlyLeft : IsStrictlyRight).ToList(), FamilyWeight);
            chosen.FamilyId = family.Id;
            chosen.Position = _generator.GeneratePosition(rng, family);
            if (!chosen.IsLocked(PartyField.Name))
            {
                GenerateNameAndSigla(chosen, rng, BuildTaken(chosen, pending: null), regenerateName: true);
            }
        }
    }

    private static bool IsStrictlyLeft(FamilyDefinition family) => family.Economic is { Max: < 0 };

    private static bool IsStrictlyRight(FamilyDefinition family) => family.Economic is { Min: > 0 };

    private static IEnumerable<FamilyDefinition> PreferIfAny(IEnumerable<FamilyDefinition> families, Func<FamilyDefinition, bool> predicate)
    {
        var list = families.ToList();
        var preferred = list.Where(predicate).ToList();
        return preferred.Count > 0 ? preferred : list;
    }

    /// <summary>Rateio de <paramref name="total"/> pelos pesos, pelo método dos maiores restos.</summary>
    private static Dictionary<PartySize, int> Apportion(int total, IReadOnlyDictionary<PartySize, int> weights)
    {
        var sizes = Enum.GetValues<PartySize>();
        double sum = sizes.Sum(s => (double)weights.GetValueOrDefault(s));
        var quotas = sizes.ToDictionary(s => s, s => total * weights.GetValueOrDefault(s) / sum);
        var counts = sizes.ToDictionary(s => s, s => (int)Math.Floor(quotas[s]));
        int remaining = total - counts.Values.Sum();

        // Empate nos restos: o tamanho maior leva, para não faltar partido principal.
        foreach (PartySize size in sizes.OrderByDescending(s => quotas[s] - counts[s]).ThenByDescending(s => s).Take(remaining))
        {
            counts[size]++;
        }

        return counts;
    }
}
