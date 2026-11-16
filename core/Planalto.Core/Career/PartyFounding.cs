using Planalto.Core.Campaign;
using Planalto.Core.Data;
using Planalto.Core.Elections;
using Planalto.Core.Municipios;
using Planalto.Core.Parties;
using Planalto.Core.State;

namespace Planalto.Core.Career;

public sealed class FoundingIntensity
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required int Energy { get; init; }

    public required decimal Money { get; init; }

    /// <summary>Assinaturas por semana, antes do reconhecimento.</summary>
    public required int Base { get; init; }

    public required int PerRecognition { get; init; }

    /// <summary>Acréscimo na chance de impugnação por semana de coleta nesta intensidade.</summary>
    public required double Risk { get; init; }
}

/// <summary>Parâmetros da fundação de partido (data/parties/founding.json, Lei 9.096, arts. 7º e 8º).</summary>
public sealed class FoundingConfig : IValidatable
{
    public const string File = "parties/founding.json";

    public required string Source { get; init; }

    public required int CapitalCost { get; init; }

    public required decimal MoneyCost { get; init; }

    public required int Founders { get; init; }

    public required int MinStates { get; init; }

    public required double NationalShare { get; init; }

    public required double StateMinShare { get; init; }

    public required double ValidVoteFactor { get; init; }

    public required int DeadlineWeeks { get; init; }

    public required List<FoundingIntensity> Intensities { get; init; }

    public required int FocusStatesMax { get; init; }

    public required IntRange ReviewWeeks { get; init; }

    public required double ImpugnationBase { get; init; }

    public required double ImpugnationLostShare { get; init; }

    public IEnumerable<string> Validate()
    {
        if (CapitalCost < 0 || MoneyCost < 0 || Founders < 1 || MinStates is < 1 or > 27 || DeadlineWeeks < 1 || FocusStatesMax < 1)
        {
            yield return "custos não negativos; fundadores, estados (1 a 27), prazo e estados por semana positivos.";
        }

        if (NationalShare is <= 0 or > 1 || StateMinShare is <= 0 or > 1 || ValidVoteFactor is <= 0 or > 1 || ImpugnationBase is < 0 or > 1 || ImpugnationLostShare is < 0 or > 1)
        {
            yield return "frações entre 0 e 1.";
        }

        if (Intensities.Count == 0 || Intensities.Any(i => i.Energy < 0 || i.Money < 0 || i.Base < 0 || i.Risk < 0))
        {
            yield return "intensities: ao menos uma, com valores não negativos.";
        }

        if (ReviewWeeks.Min < 1 || ReviewWeeks.Max < ReviewWeeks.Min)
        {
            yield return "review_weeks inválido.";
        }
    }
}

public enum FoundingStage
{
    Assinaturas,
    Registro,
    Registrado,
    Expirado,
}

/// <summary>Processo de fundação em curso (GDD 14.2 B).</summary>
public sealed class PartyFoundingState
{
    public required GameParty Draft { get; init; }

    public required DateOnly Started { get; init; }

    public FoundingStage Stage { get; set; } = FoundingStage.Assinaturas;

    public Dictionary<string, long> Signatures { get; init; } = [];

    public DateOnly? LastCollection { get; set; }

    /// <summary>Risco acumulado de impugnação (coletores pagos).</summary>
    public double Risk { get; set; }

    public DateOnly? ReviewUntil { get; set; }

    public int Impugnations { get; set; }

    public DateOnly? RegisteredOn { get; set; }

    public long Total => Signatures.Values.Sum();
}

public enum FoundingFailure
{
    SemCapital,
    SemDinheiro,
    SemEnergia,
    JaEmCurso,
    NomeInvalido,
    SiglaInvalida,
    NumeroInvalido,
    JaColetouNestaSemana,
    EstadosInvalidos,
    RequisitosIncompletos,
    ForaDeEtapa,
}

/// <summary>
/// Fundação de partido durante a carreira (GDD 14.2 B): fundadores (capital e dinheiro), coleta de apoio por
/// estados ao longo de meses, pedido de registro no TSE com chance de impugnação e prazo de 2 anos.
/// </summary>
public sealed class PartyFoundingEngine
{
    private readonly GameState _state;
    private readonly FoundingConfig _config;
    private readonly PartyContent _parties;
    private readonly CandidateNumberRulesConfig _numbers;
    private readonly Dictionary<string, long> _ufElectorate;

    /// <param name="camaraValidVotes">
    /// Votos válidos reais para a Câmara na última eleição geral (cenário Brasil 2026); sem eles, a meta usa a
    /// aproximação de <see cref="FoundingConfig.ValidVoteFactor"/> sobre o eleitorado.
    /// </param>
    public PartyFoundingEngine(GameState state, FoundingConfig config, PartyContent parties, MunicipiosDataSet municipios, CandidateNumberRulesConfig numbers, long? camaraValidVotes = null)
    {
        _state = state;
        _config = config;
        _parties = parties;
        _numbers = numbers;
        _ufElectorate = municipios.All.GroupBy(m => m.Uf).ToDictionary(g => g.Key, g => g.Sum(m => m.Eleitorado), StringComparer.Ordinal);
        UsesRealVotes = camaraValidVotes is > 0;
        NationalRequired = (long)Math.Ceiling((camaraValidVotes is > 0 ? camaraValidVotes.Value : _ufElectorate.Values.Sum() * config.ValidVoteFactor) * config.NationalShare);
    }

    public FoundingConfig Config => _config;

    public PartyFoundingState? Founding => _state.Founding;

    public long NationalRequired { get; }

    /// <summary>A meta nacional veio dos votos válidos reais (e não da aproximação pelo eleitorado).</summary>
    public bool UsesRealVotes { get; }

    public IReadOnlyCollection<string> States => _ufElectorate.Keys;

    public long StateRequired(string uf) => (long)Math.Ceiling(_ufElectorate.GetValueOrDefault(uf) * _config.ValidVoteFactor * _config.StateMinShare);

    /// <summary>Estados que já passaram do mínimo.</summary>
    public int StatesDone => Founding is null ? 0 : Founding.Signatures.Count(s => s.Value >= StateRequired(s.Key));

    public bool Ready => Founding is { Stage: FoundingStage.Assinaturas } f && f.Total >= NationalRequired && StatesDone >= _config.MinStates;

    /// <summary>Proposta de partido da família escolhida, com nome, sigla, número e cores livres no mundo.</summary>
    public GameParty Propose(Rng rng, string familyId)
    {
        var generator = new PartyGenerator(_parties);
        FamilyDefinition family = _parties.Families.Find(familyId) ?? throw new ArgumentException($"Família '{familyId}' inexistente.", nameof(familyId));
        TakenIdentities taken = Taken();
        GeneratedName name = generator.GenerateName(rng, family, taken) ?? new GeneratedName(family.Name, generator.DeriveSigla(family.Name));
        taken.Add(name.Name, name.Sigla, null, null);
        return new GameParty
        {
            Id = $"fundado_{_state.Parties.Count + 1}",
            Name = name.Name,
            Sigla = name.Sigla,
            Number = PartyGenerator.PickNumber(rng, taken, _numbers.PartyNumberMin, _numbers.PartyNumberMax) ?? _numbers.PartyNumberMin,
            FamilyId = familyId,
            Position = generator.GeneratePosition(rng, family),
            Size = PartySize.Nanico,
            Colors = generator.GenerateColors(rng, taken),
            LogoIcon = generator.PickLogoIcon(rng, familyId),
            Leadership = generator.PickLeadership(rng),
            Charter = new PartyCharter(PartyDiscipline.Moderada, InternalDemocracy.ConvencoesAbertas, PartyStance.Independente, ""),
        };
    }

    /// <summary>Problema com nome, sigla ou número do partido proposto, ou null.</summary>
    public FoundingFailure? CheckDraft(GameParty draft)
    {
        TakenIdentities taken = Taken();
        if (string.IsNullOrWhiteSpace(draft.Name) || taken.HasName(draft.Name) || _parties.Blocklist.CheckName(draft.Name) is not null)
        {
            return FoundingFailure.NomeInvalido;
        }

        if (!new PartyGenerator(_parties).IsSiglaLengthValid(draft.Sigla) || taken.HasSigla(draft.Sigla) || _parties.Blocklist.CheckSigla(draft.Sigla) is not null)
        {
            return FoundingFailure.SiglaInvalida;
        }

        return draft.Number < _numbers.PartyNumberMin || draft.Number > _numbers.PartyNumberMax || taken.Numbers.Contains(draft.Number)
            ? FoundingFailure.NumeroInvalido
            : null;
    }

    public FoundingFailure? CanStart(GameParty draft)
    {
        if (Founding is { Stage: FoundingStage.Assinaturas or FoundingStage.Registro })
        {
            return FoundingFailure.JaEmCurso;
        }

        if (_state.Player.Resources.PoliticalCapital < _config.CapitalCost)
        {
            return FoundingFailure.SemCapital;
        }

        return _state.Player.Resources.Money < _config.MoneyCost ? FoundingFailure.SemDinheiro : CheckDraft(draft);
    }

    /// <summary>Reúne os fundadores (custo em capital e dinheiro) e abre a coleta de apoio.</summary>
    public FoundingFailure? Start(GameParty draft)
    {
        if (CanStart(draft) is { } failure)
        {
            return failure;
        }

        _state.Player.Resources.PoliticalCapital -= _config.CapitalCost;
        _state.Player.Resources.Money -= _config.MoneyCost;
        _state.Founding = new PartyFoundingState { Draft = draft, Started = _state.Calendar.WeekStart };
        return null;
    }

    public FoundingFailure? CanCollect(FoundingIntensity intensity, IReadOnlyCollection<string> states)
    {
        if (Founding is not { Stage: FoundingStage.Assinaturas } founding)
        {
            return FoundingFailure.ForaDeEtapa;
        }

        if (founding.LastCollection == _state.Calendar.WeekStart)
        {
            return FoundingFailure.JaColetouNestaSemana;
        }

        if (states.Count == 0 || states.Count > _config.FocusStatesMax || states.Any(s => !_ufElectorate.ContainsKey(s)))
        {
            return FoundingFailure.EstadosInvalidos;
        }

        if (_state.Player.Resources.Money < intensity.Money)
        {
            return FoundingFailure.SemDinheiro;
        }

        return EnergyAvailable() < intensity.Energy ? FoundingFailure.SemEnergia : null;
    }

    /// <summary>Uma semana de coleta nos estados escolhidos; as assinaturas se dividem pelo tamanho do eleitorado.</summary>
    public long Collect(string intensityId, IReadOnlyCollection<string> states)
    {
        FoundingIntensity intensity = _config.Intensities.First(i => i.Id == intensityId);
        if (CanCollect(intensity, states) is not null)
        {
            return 0;
        }

        PartyFoundingState founding = Founding!;
        SpendEnergy(intensity.Energy);
        _state.Player.Resources.Money -= intensity.Money;
        founding.LastCollection = _state.Calendar.WeekStart;
        founding.Risk += intensity.Risk;

        double recognition = _state.Mandate?.Recognition ?? _state.Player.Resources.Recognition.Values.DefaultIfEmpty(0).Max();
        double noise = 0.8 + (_state.Rng.NextDouble() * 0.4);
        long total = (long)Math.Round((intensity.Base + (intensity.PerRecognition * recognition)) * noise);
        double weight = states.Sum(s => (double)_ufElectorate[s]);
        foreach (string uf in states)
        {
            founding.Signatures[uf] = founding.Signatures.GetValueOrDefault(uf) + (long)Math.Round(total * _ufElectorate[uf] / weight);
        }

        return total;
    }

    /// <summary>Pede o registro ao TSE quando o apoio está completo.</summary>
    public FoundingFailure? Submit()
    {
        if (!Ready)
        {
            return Founding is { Stage: FoundingStage.Assinaturas } ? FoundingFailure.RequisitosIncompletos : FoundingFailure.ForaDeEtapa;
        }

        PartyFoundingState founding = Founding!;
        founding.Stage = FoundingStage.Registro;
        founding.ReviewUntil = _state.Calendar.WeekStart.AddDays(7 * _state.Rng.NextInt(_config.ReviewWeeks.Min, _config.ReviewWeeks.Max + 1));
        return null;
    }

    /// <summary>Semana do processo: julgamento do registro (com impugnação possível) e prazo da coleta.</summary>
    public void Tick()
    {
        if (Founding is not { } founding)
        {
            return;
        }

        DateOnly week = _state.Calendar.WeekStart;
        if (founding.Stage == FoundingStage.Assinaturas && week >= founding.Started.AddDays(7 * _config.DeadlineWeeks))
        {
            founding.Stage = FoundingStage.Expirado;
            return;
        }

        if (founding.Stage != FoundingStage.Registro || week < founding.ReviewUntil)
        {
            return;
        }

        if (_state.Rng.NextDouble() < Math.Min(0.95, _config.ImpugnationBase + founding.Risk))
        {
            // Impugnado: parte das assinaturas cai e a coleta volta, dentro do mesmo prazo.
            founding.Impugnations++;
            founding.Stage = FoundingStage.Assinaturas;
            founding.ReviewUntil = null;
            foreach (string uf in founding.Signatures.Keys.ToList())
            {
                founding.Signatures[uf] = (long)Math.Round(founding.Signatures[uf] * (1 - _config.ImpugnationLostShare));
            }

            return;
        }

        founding.Stage = FoundingStage.Registrado;
        founding.RegisteredOn = week;
        founding.Draft.RegisteredOn = week;
        _state.Parties.Add(founding.Draft);
    }

    private TakenIdentities Taken()
    {
        var taken = new TakenIdentities(_parties.Blocklist);
        foreach (GameParty p in _state.Parties)
        {
            taken.Add(p.Name, p.Sigla, p.Number, p.Colors);
        }

        return taken;
    }

    private int EnergyAvailable() => _state.Campaign?.EnergyLeft ?? _state.Mandate?.EnergyLeft ?? int.MaxValue;

    private void SpendEnergy(int energy)
    {
        if (_state.Campaign is { } campaign)
        {
            campaign.EnergyLeft -= energy;
        }
        else if (_state.Mandate is { } mandate)
        {
            mandate.EnergyLeft -= energy;
        }
    }
}
