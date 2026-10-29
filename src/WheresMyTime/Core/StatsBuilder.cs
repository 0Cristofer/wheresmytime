using System.Text;

namespace WheresMyTime.Core;

public sealed record DetailTotal(string Details, TimeSpan Total, int Entries);

public sealed record ActivityTotal(string Activity, TimeSpan Total, int Entries, double Percent, IReadOnlyList<DetailTotal> Details);

public sealed record DayTotal(DateOnly Date, TimeSpan Total);

public sealed record Stats(
    DateOnly From, DateOnly To, TimeSpan Total, int EntryCount,
    IReadOnlyList<ActivityTotal> Activities, IReadOnlyList<DayTotal> Days);

public static class StatsBuilder
{
    public const string NoDetails = "(no details)";

    /// <summary>
    /// Totals per activity (and per details text within it) and per day, for the days
    /// <paramref name="from"/> to <paramref name="to"/> inclusive. Entries that cross the
    /// range edges or midnight are split so each day only counts its own part.
    /// </summary>
    public static Stats Build(IEnumerable<TimeEntry> entries, DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        var rangeStart = from.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var parts = entries
            .Select(e => (Entry: e, Start: Max(e.Start, rangeStart), End: Min(e.End, rangeEnd)))
            .Where(p => p.End > p.Start)
            .ToList();

        var total = Sum(parts.Select(p => p.End - p.Start));

        var activities = parts
            .GroupBy(p => p.Entry.Activity, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var activityTotal = Sum(g.Select(p => p.End - p.Start));
                var details = g
                    .GroupBy(p => string.IsNullOrWhiteSpace(p.Entry.Details) ? NoDetails : p.Entry.Details.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(d => new DetailTotal(d.Key, Sum(d.Select(p => p.End - p.Start)), d.Count()))
                    .OrderByDescending(d => d.Total)
                    .ToList();
                var name = g.MaxBy(p => p.Entry.Start).Entry.Activity;
                var percent = total > TimeSpan.Zero ? activityTotal / total * 100 : 0;
                return new ActivityTotal(name, activityTotal, g.Count(), percent, details);
            })
            .OrderByDescending(a => a.Total)
            .ToList();

        var days = SplitByDay(parts.Select(p => p.Entry), from, to)
            .GroupBy(p => p.Day)
            .OrderBy(g => g.Key)
            .Select(g => new DayTotal(g.Key, Sum(g.Select(p => p.Duration))))
            .ToList();

        return new Stats(from, to, total, parts.Count, activities, days);
    }

    /// <summary>Each entry's time inside the date range, cut at midnight into one piece per day.</summary>
    internal static IEnumerable<(TimeEntry Entry, DateOnly Day, TimeSpan Duration)> SplitByDay(
        IEnumerable<TimeEntry> entries, DateOnly from, DateOnly to)
    {
        var rangeStart = from.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        foreach (var entry in entries)
        {
            var end = Min(entry.End, rangeEnd);
            for (var cursor = Max(entry.Start, rangeStart); cursor < end;)
            {
                var pieceEnd = Min(cursor.Date.AddDays(1), end);
                yield return (entry, DateOnly.FromDateTime(cursor), pieceEnd - cursor);
                cursor = pieceEnd;
            }
        }
    }

    /// <summary>Entries that overlap the date range, unclipped (for exporting the range).</summary>
    public static List<TimeEntry> EntriesInRange(IEnumerable<TimeEntry> entries, DateOnly from, DateOnly to)
    {
        if (to < from) (from, to) = (to, from);
        var rangeStart = from.ToDateTime(TimeOnly.MinValue);
        var rangeEnd = to.AddDays(1).ToDateTime(TimeOnly.MinValue);
        return entries.Where(e => e.End > rangeStart && e.Start < rangeEnd).OrderBy(e => e.Start).ToList();
    }

    /// <summary>Plain-text version of the stats for pasting into chat, mail or notes.</summary>
    public static string ToText(Stats stats, bool includeDetails)
    {
        var sb = new StringBuilder();
        sb.Append($"Where's my time — {TimeUtil.FormatDate(stats.From)}");
        if (stats.To != stats.From) sb.Append($" to {TimeUtil.FormatDate(stats.To)}");
        sb.AppendLine();
        sb.AppendLine($"Total: {TimeUtil.FormatDuration(stats.Total)} ({TimeUtil.FormatHours(stats.Total)} h) in {stats.EntryCount} entries");
        sb.AppendLine();

        foreach (var a in stats.Activities)
        {
            sb.AppendLine($"{a.Activity} — {TimeUtil.FormatDuration(a.Total)} ({a.Percent.ToString("0.0", TimeUtil.Invariant)}%)");
            if (!includeDetails) continue;
            foreach (var d in a.Details)
                sb.AppendLine($"    • {d.Details} — {TimeUtil.FormatDuration(d.Total)}");
        }

        if (stats.Days.Count > 1)
        {
            sb.AppendLine();
            sb.AppendLine("By day");
            foreach (var d in stats.Days)
                sb.AppendLine($"{TimeUtil.FormatDay(d.Date)} — {TimeUtil.FormatDuration(d.Total)}");
        }
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    internal static TimeSpan Sum(IEnumerable<TimeSpan> spans) => spans.Aggregate(TimeSpan.Zero, (a, b) => a + b);
    static DateTime Max(DateTime a, DateTime b) => a > b ? a : b;
    static DateTime Min(DateTime a, DateTime b) => a < b ? a : b;
}
