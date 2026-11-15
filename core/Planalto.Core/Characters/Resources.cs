using System.Text.Json.Serialization;

namespace Planalto.Core.Characters;

/// <summary>
/// Recursos do personagem. Energia, Saúde, Estresse e Reconhecimento ficam entre 0 e 100;
/// Capital político não fica negativo; Dinheiro pode ficar negativo (dívida de campanha).
/// </summary>
public sealed class Resources
{
    public const int Min = 0;
    public const int Max = 100;

    private int _politicalCapital;
    private int _energy;
    private int _health;
    private int _stress;
    private Dictionary<string, int> _recognition = new(StringComparer.Ordinal);

    public decimal Money { get; set; }

    public int PoliticalCapital { get => _politicalCapital; set => _politicalCapital = Math.Max(Min, value); }

    public int Energy { get => _energy; set => _energy = Clamp(value); }

    public int Health { get => _health; set => _health = Clamp(value); }

    public int Stress { get => _stress; set => _stress = Clamp(value); }

    /// <summary>Reconhecimento por região (chave livre: macrorregião, UF ou município).</summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, int> Recognition => _recognition;

    [JsonInclude]
    [JsonPropertyName("recognition")]
    private Dictionary<string, int> RecognitionData
    {
        get => _recognition;
        set => _recognition = value.ToDictionary(kv => kv.Key, kv => Clamp(kv.Value), StringComparer.Ordinal);
    }

    public int GetRecognition(string region) => _recognition.GetValueOrDefault(region);

    public void SetRecognition(string region, int value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        _recognition[region] = Clamp(value);
    }

    public void AddRecognition(string region, int delta) => SetRecognition(region, GetRecognition(region) + delta);

    private static int Clamp(int value) => Math.Clamp(value, Min, Max);
}
