using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planalto.Core.State;

/// <summary>Estado completo do gerador, para salvar e continuar a mesma sequência.</summary>
public readonly record struct RngState(ulong Seed, ulong S0, ulong S1, ulong S2, ulong S3);

/// <summary>
/// Gerador pseudoaleatório determinístico (xoshiro256**) usado por toda a simulação.
/// A mesma semente produz sempre a mesma sequência, em qualquer versão do .NET.
/// </summary>
[JsonConverter(typeof(RngJsonConverter))]
public sealed class Rng
{
    private ulong _s0;
    private ulong _s1;
    private ulong _s2;
    private ulong _s3;

    public Rng(ulong seed)
    {
        Seed = seed;

        // SplitMix64 espalha a semente pelos 256 bits de estado e garante que ele nunca seja todo zero.
        ulong x = seed;
        _s0 = SplitMix64(ref x);
        _s1 = SplitMix64(ref x);
        _s2 = SplitMix64(ref x);
        _s3 = SplitMix64(ref x);
    }

    private Rng(RngState state)
    {
        if ((state.S0 | state.S1 | state.S2 | state.S3) == 0)
        {
            throw new ArgumentException("O estado do xoshiro256** não pode ser todo zero.", nameof(state));
        }

        Seed = state.Seed;
        _s0 = state.S0;
        _s1 = state.S1;
        _s2 = state.S2;
        _s3 = state.S3;
    }

    public ulong Seed { get; }

    public RngState GetState() => new(Seed, _s0, _s1, _s2, _s3);

    public static Rng FromState(RngState state) => new(state);

    public ulong NextUInt64()
    {
        ulong result = BitOperations.RotateLeft(_s1 * 5, 7) * 9;
        ulong t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = BitOperations.RotateLeft(_s3, 45);

        return result;
    }

    /// <summary>Inteiro em [0, maxExclusive).</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Deve ser maior que zero.");
        }

        return (int)NextBounded((ulong)maxExclusive);
    }

    /// <summary>Inteiro em [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Deve ser maior que minInclusive.");
        }

        ulong range = (ulong)((long)maxExclusive - minInclusive);
        return (int)(minInclusive + (long)NextBounded(range));
    }

    /// <summary>Real em [0, 1), com 53 bits de precisão.</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    // Rejeição dos valores abaixo de 2^64 mod bound, para que o resto não favoreça os números pequenos.
    private ulong NextBounded(ulong bound)
    {
        ulong threshold = (0UL - bound) % bound;
        while (true)
        {
            ulong r = NextUInt64();
            if (r >= threshold)
            {
                return r % bound;
            }
        }
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        ulong z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}

internal sealed class RngJsonConverter : JsonConverter<Rng>
{
    public override Rng Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Rng.FromState(JsonSerializer.Deserialize<RngState>(ref reader, options));

    public override void Write(Utf8JsonWriter writer, Rng value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value.GetState(), options);
}
