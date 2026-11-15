using Planalto.Core.State;

namespace Planalto.Core.Tests.State;

public class RngTests
{
    private const int Draws = 1000;

    [Fact]
    public void MesmaSemente_GeraMesmaSequencia()
    {
        var a = new Rng(20261115);
        var b = new Rng(20261115);

        for (int i = 0; i < Draws; i++)
        {
            Assert.Equal(a.NextUInt64(), b.NextUInt64());
            Assert.Equal(a.NextInt(100), b.NextInt(100));
            Assert.Equal(a.NextInt(-50, 50), b.NextInt(-50, 50));
            Assert.Equal(a.NextDouble(), b.NextDouble());
        }
    }

    [Fact]
    public void SementesDiferentes_GeramSequenciasDiferentes()
    {
        var a = new Rng(1);
        var b = new Rng(2);

        var sequenciaA = Enumerable.Range(0, Draws).Select(_ => a.NextUInt64()).ToArray();
        var sequenciaB = Enumerable.Range(0, Draws).Select(_ => b.NextUInt64()).ToArray();

        Assert.NotEqual(sequenciaA, sequenciaB);
    }

    [Fact]
    public void Sorteios_RespeitamOsLimites()
    {
        var rng = new Rng(42);

        for (int i = 0; i < Draws; i++)
        {
            Assert.InRange(rng.NextInt(10), 0, 9);
            Assert.InRange(rng.NextInt(-3, 4), -3, 3);
            Assert.InRange(rng.NextInt(int.MinValue, int.MaxValue), int.MinValue, int.MaxValue - 1);

            double d = rng.NextDouble();
            Assert.True(d >= 0.0 && d < 1.0, $"NextDouble fora de [0, 1): {d}");
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
    }
}
