using Planalto.Core.State;

namespace Planalto.Core.Tests.State;

public class CalendarTests
{
    // 1º turno: primeiro domingo de outubro; 2º turno: último domingo de outubro;
    // filiação: 6 meses antes do 1º turno; campanha: 16 de agosto.
    [Theory]
    [InlineData(2028, ElectionKind.Municipal, "2028-10-01", "2028-10-29", "2028-04-01")]
    [InlineData(2030, ElectionKind.General, "2030-10-06", "2030-10-27", "2030-04-06")]
    [InlineData(2032, ElectionKind.Municipal, "2032-10-03", "2032-10-31", "2032-04-03")]
    [InlineData(2034, ElectionKind.General, "2034-10-01", "2034-10-29", "2034-04-01")]
    [InlineData(2036, ElectionKind.Municipal, "2036-10-05", "2036-10-26", "2036-04-05")]
    [InlineData(2038, ElectionKind.General, "2038-10-03", "2038-10-31", "2038-04-03")]
    [InlineData(2040, ElectionKind.Municipal, "2040-10-07", "2040-10-28", "2040-04-07")]
    public void ElectionFor_CalculaDatasLegais(int year, ElectionKind kind, string first, string second, string affiliation)
    {
        ElectionSchedule schedule = Calendar.ElectionFor(year);

        Assert.Equal(kind, schedule.Kind);
        Assert.Equal(DateOnly.Parse(first), schedule.FirstRound);
        Assert.Equal(DateOnly.Parse(second), schedule.SecondRound);
        Assert.Equal(new DateOnly(year, 8, 16), schedule.CampaignStart);
        Assert.Equal(DateOnly.Parse(affiliation), schedule.PartyAffiliationDeadline);
        Assert.Equal(DayOfWeek.Sunday, schedule.FirstRound.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, schedule.SecondRound.DayOfWeek);
    }

    [Theory]
    [InlineData(2029)]
    [InlineData(2031)]
    [InlineData(2039)]
    public void AnosImpares_NaoTemEleicao(int year)
    {
        Assert.Null(Calendar.ElectionKindOf(year));
        Assert.Throws<ArgumentException>(() => Calendar.ElectionFor(year));
    }

    [Fact]
    public void AdvanceWeek_AvancaSeteDiasEViraOAno()
    {
        var calendar = Calendar.FromIsoWeek(2028, 52);
        Assert.Equal(new DateOnly(2028, 12, 25), calendar.WeekStart);

        calendar.AdvanceWeek();

        Assert.Equal(new DateOnly(2029, 1, 1), calendar.WeekStart);
        Assert.Equal(2029, calendar.Year);
        Assert.Equal(1, calendar.Week);
    }

    [Fact]
    public void ForDate_PosicionaNaSegundaDaSemanaEContemOPrimeiroTurno()
    {
        var calendar = Calendar.ForDate(new DateOnly(2028, 10, 1));

        Assert.Equal(new DateOnly(2028, 9, 25), calendar.WeekStart);
        Assert.True(calendar.Contains(Calendar.ElectionFor(2028).FirstRound));
    }

    [Fact]
    public void NextElection_PulaEleicaoCujoSegundoTurnoJaPassou()
    {
        var antes = Calendar.ForDate(new DateOnly(2028, 10, 23));
        var depois = Calendar.ForDate(new DateOnly(2028, 11, 6));

        Assert.Equal(2028, antes.NextElection().Year);
        Assert.Equal(2030, depois.NextElection().Year);
    }

    [Fact]
    public void Construtor_RejeitaDiaQueNaoESegunda()
    {
        Assert.Throws<ArgumentException>(() => new Calendar(new DateOnly(2028, 10, 1)));
    }
}
