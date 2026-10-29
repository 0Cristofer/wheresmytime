namespace WheresMyTime.Core;

public enum ChartGrouping { Day, Week, Month }

/// <summary>One stacked series. Slot is its colour in the palette; null is the grey "Other" series.</summary>
public sealed record ChartSeries(string Name, int? Slot, TimeSpan Total);

/// <summary>
/// One column (a day, week or month). Values line up with <see cref="ChartData.Series"/>.
/// Incomplete means the period is cut off by the range or still in progress.
/// </summary>
public sealed record ChartBucket(DateOnly Start, string Label, string ShortLabel, string Title, IReadOnlyList<TimeSpan> Values, bool Incomplete)
{
    public TimeSpan Total => StatsBuilder.Sum(Values);
}

public sealed record ChartData(ChartGrouping Grouping, IReadOnlyList<ChartSeries> Series, IReadOnlyList<ChartBucket> Buckets);

/// <summary>Data for the stacked "time per day, by activity" chart.</summary>
public static class ChartBuilder
{
    /// <summary>Palette colours available to activities; any more fold into "Other".</summary>
    public const int ColoredSeries = 7;
    public const string OtherName = "Other";

    /// <summary>
    /// One column per day for ranges up to ~6 weeks, per week up to ~6 months, per month beyond.
    /// Series are stacked in colour-slot order with "Other" on top. A period cut off by the range,
    /// or still in progress, says so in its title, since its total isn't comparable to the others.
    /// </summary>
    public static ChartData Build(IReadOnlyCollection<TimeEntry> allEntries, DateOnly from, DateOnly to, DateOnly? today = null)
    {
        if (to < from) (from, to) = (to, from);
        var now = today ?? TimeUtil.Today();
        var dayCount = to.DayNumber - from.DayNumber + 1;
        var grouping = dayCount <= 45 ? ChartGrouping.Day : dayCount <= 26 * 7 ? ChartGrouping.Week : ChartGrouping.Month;

        var pieces = StatsBuilder.SplitByDay(allEntries, from, to).ToList();
        var activities = pieces
            .GroupBy(p => p.Entry.Activity, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Key: g.Key, Name: g.MaxBy(p => p.Entry.Start).Entry.Activity, Total: StatsBuilder.Sum(g.Select(p => p.Duration))))
            .OrderByDescending(a => a.Total)
            .ThenBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var slots = AssignSlots(allEntries, activities.Select(a => a.Key).ToList());

        var series = new List<ChartSeries>();
        var seriesIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in activities.Where(a => slots.ContainsKey(a.Key)).OrderBy(a => slots[a.Key]))
        {
            seriesIndex[a.Key] = series.Count;
            series.Add(new ChartSeries(a.Name, slots[a.Key], a.Total));
        }
        var others = activities.Where(a => !slots.ContainsKey(a.Key)).ToList();
        if (others.Count > 0)
        {
            foreach (var a in others) seriesIndex[a.Key] = series.Count;
            series.Add(new ChartSeries(OtherName, null, StatsBuilder.Sum(others.Select(a => a.Total))));
        }

        var firstBucket = BucketStart(from, grouping);
        var starts = new List<DateOnly>();
        for (var start = firstBucket; start <= to; start = NextBucket(start, grouping)) starts.Add(start);

        var values = starts.Select(_ => new TimeSpan[series.Count]).ToList();
        foreach (var (entry, day, duration) in pieces)
            values[BucketIndex(firstBucket, day, grouping)][seriesIndex[entry.Activity]] += duration;

        var buckets = starts
            .Select((start, i) =>
            {
                var end = NextBucket(start, grouping).AddDays(-1);
                var note = start < from || end > to ? " (partial)"
                    : start <= now && now <= end ? " (so far)"
                    : "";
                return Labelled(start, grouping, values[i], note);
            })
            .ToList();
        return new ChartData(grouping, series, buckets);
    }

    /// <summary>
    /// Gives each activity in the range a colour slot. The overall top activities (by all-time
    /// total) always keep their own slot, so their colour doesn't change with the date range;
    /// other activities borrow the slots of top activities that aren't in this range.
    /// Activities left without a slot fold into "Other".
    /// </summary>
    internal static Dictionary<string, int> AssignSlots(IEnumerable<TimeEntry> allEntries, IReadOnlyList<string> activitiesByRangeTotal)
    {
        var home = allEntries
            .GroupBy(e => e.Activity, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, Total: StatsBuilder.Sum(g.Select(e => e.Duration))))
            .OrderByDescending(a => a.Total)
            .ThenBy(a => a.Key, StringComparer.OrdinalIgnoreCase)
            .Take(ColoredSeries)
            .Select((a, slot) => (a.Key, slot))
            .ToDictionary(a => a.Key, a => a.slot, StringComparer.OrdinalIgnoreCase);

        var slots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var activity in activitiesByRangeTotal)
        {
            if (home.TryGetValue(activity, out var slot)) slots[activity] = slot;
        }

        var free = new Queue<int>(Enumerable.Range(0, ColoredSeries).Except(slots.Values));
        foreach (var activity in activitiesByRangeTotal)
        {
            if (!slots.ContainsKey(activity) && free.TryDequeue(out var slot)) slots[activity] = slot;
        }
        return slots;
    }

    static DateOnly BucketStart(DateOnly day, ChartGrouping grouping) => grouping switch
    {
        ChartGrouping.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
        ChartGrouping.Month => new DateOnly(day.Year, day.Month, 1),
        _ => day,
    };

    static DateOnly NextBucket(DateOnly start, ChartGrouping grouping) => grouping switch
    {
        ChartGrouping.Week => start.AddDays(7),
        ChartGrouping.Month => start.AddMonths(1),
        _ => start.AddDays(1),
    };

    static int BucketIndex(DateOnly first, DateOnly day, ChartGrouping grouping) => grouping switch
    {
        ChartGrouping.Week => (day.DayNumber - first.DayNumber) / 7,
        ChartGrouping.Month => (day.Year - first.Year) * 12 + day.Month - first.Month,
        _ => day.DayNumber - first.DayNumber,
    };

    static ChartBucket Labelled(DateOnly start, ChartGrouping grouping, TimeSpan[] values, string note)
    {
        var inv = TimeUtil.Invariant;
        var incomplete = note.Length > 0;
        return grouping switch
        {
            ChartGrouping.Week => new ChartBucket(start, start.ToString("MMM d", inv), start.ToString("M/d", inv),
                $"Week of {TimeUtil.FormatDay(start)}{note}", values, incomplete),
            ChartGrouping.Month => new ChartBucket(start, start.ToString("MMM yyyy", inv), start.ToString("MMM", inv),
                $"{start.ToString("MMMM yyyy", inv)}{note}", values, incomplete),
            _ => new ChartBucket(start, start.ToString("ddd d", inv), start.ToString("%d", inv),
                $"{TimeUtil.FormatDay(start)}{note}", values, incomplete),
        };
    }
}
