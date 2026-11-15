using System.Globalization;
using System.Text.Json.Serialization;

namespace Planalto.Core.State;

/// <summary>
/// Calendário do jogo. Cada turno é uma semana, que começa na segunda-feira;
/// ano e número da semana seguem a ISO 8601.
/// </summary>
public sealed class Calendar
{
    // Primeira eleição geral do ciclo atual de 4 anos; municipais caem 2 anos depois.
    private const int GeneralElectionReferenceYear = 2026;

    [JsonConstructor]
    public Calendar(DateOnly weekStart)
    {
        if (weekStart.DayOfWeek != DayOfWeek.Monday)
        {
            throw new ArgumentException("A semana deve começar numa segunda-feira.", nameof(weekStart));
        }

        WeekStart = weekStart;
    }

    public DateOnly WeekStart { get; private set; }

    [JsonIgnore]
    public DateOnly WeekEnd => WeekStart.AddDays(6);

    [JsonIgnore]
    public int Year => ISOWeek.GetYear(WeekStart.ToDateTime(TimeOnly.MinValue));

    [JsonIgnore]
    public int Week => ISOWeek.GetWeekOfYear(WeekStart.ToDateTime(TimeOnly.MinValue));

    public static Calendar FromIsoWeek(int year, int week) =>
        new(DateOnly.FromDateTime(ISOWeek.ToDateTime(year, week, DayOfWeek.Monday)));

    /// <summary>Calendário posicionado na semana que contém a data informada.</summary>
    public static Calendar ForDate(DateOnly date)
    {
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return new Calendar(date.AddDays(-daysSinceMonday));
    }

    public void AdvanceWeek() => WeekStart = WeekStart.AddDays(7);

    public bool Contains(DateOnly date) => date >= WeekStart && date <= WeekEnd;

    /// <summary>Tipo de eleição do ano, ou null se o ano não tem eleição.</summary>
    public static ElectionKind? ElectionKindOf(int year)
    {
        int offset = year - GeneralElectionReferenceYear;
        if (offset % 2 != 0)
        {
            return null;
        }

        return offset % 4 == 0 ? ElectionKind.General : ElectionKind.Municipal;
    }

    public static ElectionSchedule ElectionFor(int year)
    {
        ElectionKind kind = ElectionKindOf(year)
            ?? throw new ArgumentException($"Não há eleição em {year}.", nameof(year));

        // CF, arts. 29, II, e 77: 1º turno no primeiro domingo de outubro, 2º no último.
        DateOnly firstRound = FirstSunday(year, 10);
        DateOnly secondRound = LastSunday(year, 10);

        // Lei 9.504/1997: propaganda a partir de 16 de agosto (art. 36)
        // e filiação deferida até 6 meses antes da eleição (art. 9º).
        var campaignStart = new DateOnly(year, 8, 16);
        DateOnly affiliationDeadline = firstRound.AddMonths(-6);

        return new ElectionSchedule(year, kind, firstRound, secondRound, campaignStart, affiliationDeadline);
    }

    /// <summary>Próxima eleição cujo 2º turno ainda não passou.</summary>
    public ElectionSchedule NextElection()
    {
        for (int year = WeekStart.Year; ; year++)
        {
            if (ElectionKindOf(year) is null)
            {
                continue;
            }

            ElectionSchedule schedule = ElectionFor(year);
            if (schedule.SecondRound >= WeekStart)
            {
                return schedule;
            }
        }
    }

    private static DateOnly FirstSunday(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        int daysUntilSunday = (7 - (int)first.DayOfWeek) % 7;
        return first.AddDays(daysUntilSunday);
    }

    private static DateOnly LastSunday(int year, int month)
    {
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return last.AddDays(-(int)last.DayOfWeek);
    }
}
