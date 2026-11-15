using Planalto.Core.Elections;

namespace Planalto.Core.Tests.Elections;

public class MajoritarianTests
{
    private static CandidateVotes C(string id, long votes, int birthYear = 1975) =>
        new(id, votes, new DateOnly(birthYear, 6, 1));

    [Fact]
    public void PrimeiroTurno_DecididoComMaioriaAbsoluta()
    {
        // Válidos = 10000; A tem 5100 > 5000 (metade) → eleito no 1º turno.
        MajoritarianResult result = ElectionRules.Majoritarian([C("A", 5100), C("B", 3000), C("C", 1900)], hasRunoff: true);

        Assert.Equal(MajoritarianOutcome.Elected, result.Outcome);
        Assert.Equal("A", result.WinnerId);
        Assert.Equal(10000, result.ValidVotes);
    }

    [Fact]
    public void ExatamenteMetadeDosValidos_NaoEMaioriaAbsoluta()
    {
        MajoritarianResult result = ElectionRules.Majoritarian([C("A", 5000), C("B", 3000), C("C", 2000)], hasRunoff: true);

        Assert.Equal(MajoritarianOutcome.Runoff, result.Outcome);
        Assert.Equal(["A", "B"], result.RunoffCandidateIds);
    }

    [Fact]
    public void SegundoTurno_EntreOsDoisMaisVotadosEViradaNoSegundo()
    {
        // 1º turno: A = 40%, B = 35%, C = 25% → 2º turno entre A e B.
        MajoritarianResult first = ElectionRules.Majoritarian([C("A", 4000), C("B", 3500), C("C", 2500)], hasRunoff: true);
        Assert.Equal(MajoritarianOutcome.Runoff, first.Outcome);
        Assert.Null(first.WinnerId);
        Assert.Equal(["A", "B"], first.RunoffCandidateIds);

        // 2º turno: B vira com 5200 a 4800.
        MajoritarianResult second = ElectionRules.Runoff([C("A", 4800), C("B", 5200)]);
        Assert.Equal("B", second.WinnerId);
    }

    [Fact]
    public void EmpateNoSegundoLugar_ClassificaOMaisIdoso()
    {
        MajoritarianResult result = ElectionRules.Majoritarian(
            [C("A", 4000), C("B", 3000, birthYear: 1980), C("C", 3000, birthYear: 1965)], hasRunoff: true);

        Assert.Equal(["A", "C"], result.RunoffCandidateIds);
    }

    [Fact]
    public void EmpateNoSegundoTurno_ElegeOMaisIdoso()
    {
        MajoritarianResult result = ElectionRules.Runoff([C("A", 5000, birthYear: 1990), C("B", 5000, birthYear: 1970)]);

        Assert.Equal("B", result.WinnerId);
    }

    [Fact]
    public void SemSegundoTurno_ElegeOMaisVotado()
    {
        // Município com até 200 mil eleitores: maioria simples.
        MajoritarianResult result = ElectionRules.Majoritarian([C("A", 4000), C("B", 3500), C("C", 2500)], hasRunoff: false);

        Assert.Equal(MajoritarianOutcome.Elected, result.Outcome);
        Assert.Equal("A", result.WinnerId);
    }

    [Fact]
    public void SemVotosValidos_NaoElegeNinguem()
    {
        MajoritarianResult result = ElectionRules.Majoritarian([C("A", 0), C("B", 0)], hasRunoff: true);

        Assert.Equal(MajoritarianOutcome.NoValidVotes, result.Outcome);
    }

    [Fact]
    public void SenadoComDuasVagas_ElegeOsDoisMaisVotados()
    {
        // B e C empatam no 2º lugar; C é mais idoso e leva a segunda vaga.
        IReadOnlyList<string> elected = ElectionRules.Senate(
            [C("A", 5000), C("B", 4000, birthYear: 1972), C("C", 4000, birthYear: 1958), C("D", 1000)], seats: 2);

        Assert.Equal(["A", "C"], elected);
    }

    [Fact]
    public void SenadoComUmaVaga_ElegeSoOMaisVotado()
    {
        IReadOnlyList<string> elected = ElectionRules.Senate([C("A", 3000), C("B", 4500), C("C", 2500)], seats: 1);

        Assert.Equal(["B"], elected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Senado_SoAceitaUmaOuDuasVagas(int seats)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ElectionRules.Senate([C("A", 1)], seats));
    }
}
