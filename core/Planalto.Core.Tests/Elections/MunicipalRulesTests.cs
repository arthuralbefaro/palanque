using Planalto.Core.Elections;

namespace Planalto.Core.Tests.Elections;

public class MunicipalRulesTests
{
    // CF, art. 29, IV: cada par testa o último habitante de uma faixa e o primeiro da seguinte.
    [Theory]
    [InlineData(0, 9)]
    [InlineData(15_000, 9)]
    [InlineData(15_001, 11)]
    [InlineData(30_000, 11)]
    [InlineData(30_001, 13)]
    [InlineData(50_000, 13)]
    [InlineData(50_001, 15)]
    [InlineData(80_000, 15)]
    [InlineData(80_001, 17)]
    [InlineData(120_000, 17)]
    [InlineData(120_001, 19)]
    [InlineData(160_000, 19)]
    [InlineData(160_001, 21)]
    [InlineData(300_000, 21)]
    [InlineData(300_001, 23)]
    [InlineData(450_000, 23)]
    [InlineData(450_001, 25)]
    [InlineData(600_000, 25)]
    [InlineData(600_001, 27)]
    [InlineData(750_000, 27)]
    [InlineData(750_001, 29)]
    [InlineData(900_000, 29)]
    [InlineData(900_001, 31)]
    [InlineData(1_050_000, 31)]
    [InlineData(1_050_001, 33)]
    [InlineData(1_200_000, 33)]
    [InlineData(1_200_001, 35)]
    [InlineData(1_350_000, 35)]
    [InlineData(1_350_001, 37)]
    [InlineData(1_500_000, 37)]
    [InlineData(1_500_001, 39)]
    [InlineData(1_800_000, 39)]
    [InlineData(1_800_001, 41)]
    [InlineData(2_400_000, 41)]
    [InlineData(2_400_001, 43)]
    [InlineData(3_000_000, 43)]
    [InlineData(3_000_001, 45)]
    [InlineData(4_000_000, 45)]
    [InlineData(4_000_001, 47)]
    [InlineData(5_000_000, 47)]
    [InlineData(5_000_001, 49)]
    [InlineData(6_000_000, 49)]
    [InlineData(6_000_001, 51)]
    [InlineData(7_000_000, 51)]
    [InlineData(7_000_001, 53)]
    [InlineData(8_000_000, 53)]
    [InlineData(8_000_001, 55)]
    [InlineData(12_000_000, 55)]
    public void VagasVereador_SegueAsFaixasDaConstituicao(long populacao, int vagas)
    {
        Assert.Equal(vagas, ElectionRules.VagasVereador(populacao));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(200_000, false)]
    [InlineData(200_001, true)]
    public void TemSegundoTurno_SoAcimaDeDuzentosMilEleitores(long eleitorado, bool esperado)
    {
        Assert.Equal(esperado, ElectionRules.TemSegundoTurno(eleitorado));
    }

    [Fact]
    public void ValoresNegativos_LancamExcecao()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ElectionRules.VagasVereador(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ElectionRules.TemSegundoTurno(-1));
    }
}
