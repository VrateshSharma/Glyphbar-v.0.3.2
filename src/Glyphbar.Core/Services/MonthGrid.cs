using System.Globalization;

namespace Glyphbar.Core.Services;

public sealed record MonthCell(DateOnly Date, bool InMonth);

public static class MonthGrid
{
    public const int Rows = 6;

    /// <summary>Always 6 rows of 7, so the calendar does not change height between months.</summary>
    public static IReadOnlyList<MonthCell> Build(int year, int month, DayOfWeek firstDayOfWeek)
    {
        var first = new DateOnly(year, month, 1);
        int offset = ((int)first.DayOfWeek - (int)firstDayOfWeek + 7) % 7;
        var start = first.AddDays(-offset);
        var cells = new MonthCell[Rows * 7];
        for (int i = 0; i < cells.Length; i++)
        {
            var date = start.AddDays(i);
            cells[i] = new MonthCell(date, date.Month == month && date.Year == year);
        }
        return cells;
    }

    public static IReadOnlyList<string> WeekdayLabels(DayOfWeek firstDayOfWeek, CultureInfo culture)
    {
        var names = culture.DateTimeFormat.AbbreviatedDayNames;
        return Enumerable.Range(0, 7).Select(i => names[((int)firstDayOfWeek + i) % 7]).ToArray();
    }
}
