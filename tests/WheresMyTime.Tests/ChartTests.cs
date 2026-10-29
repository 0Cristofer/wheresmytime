using WheresMyTime.Core;

namespace WheresMyTime.Tests;

public class ChartTests
{
    static DateTime At(int day, int hour, int minute = 0) => new(2026, 10, day, hour, minute, 0);
    static DateOnly Day(int day) => new(2026, 10, day);

    static TimeEntry Entry(int day, int hour, double hours, string activity) =>
        new() { Start = At(day, hour), End = At(day, hour).AddHours(hours), Activity = activity };

    [Fact]
    public void One_column_per_day_stacked_by_activity()
    {
        var entries = new List<TimeEntry>
        {
            Entry(26, 9, 2, "Meetings"),
            Entry(26, 11, 1, "chat"),
            Entry(27, 9, 1, "Chat"),
            Entry(28, 23, 2, "Deploy"), // runs past midnight into the 29th
        };

        var chart = ChartBuilder.Build(entries, Day(26), Day(29), today: Day(1));

        Assert.Equal(ChartGrouping.Day, chart.Grouping);
        Assert.Equal(["Mon 26", "Tue 27", "Wed 28", "Thu 29"], chart.Buckets.Select(b => b.Label));
        Assert.Equal("Mon 2026-10-26", chart.Buckets[0].Title);
        // All-time totals: Meetings 2h, Chat 2h, Deploy 2h -> ties broken by name.
        Assert.Equal([("Chat", 0), ("Deploy", 1), ("Meetings", 2)], chart.Series.Select(s => (s.Name, s.Slot ?? -1)));
        Assert.Equal([TimeSpan.FromHours(1), TimeSpan.Zero, TimeSpan.FromHours(2)], chart.Buckets[0].Values);
        Assert.Equal(TimeSpan.FromHours(1), chart.Buckets[2].Total);
        Assert.Equal(TimeSpan.FromHours(1), chart.Buckets[3].Total);
    }

    [Fact]
    public void Long_ranges_group_by_week_then_month()
    {
        var entries = new List<TimeEntry> { Entry(28, 9, 1, "Chat") };

        var weeks = ChartBuilder.Build(entries, new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 31));
        Assert.Equal(ChartGrouping.Week, weeks.Grouping);
        Assert.Equal(new DateOnly(2026, 8, 31), weeks.Buckets[0].Start); // the Monday before
        Assert.Equal("Week of Mon 2026-10-26 (partial)", weeks.Buckets.Single(b => b.Total > TimeSpan.Zero).Title);

        var months = ChartBuilder.Build(entries, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        Assert.Equal(ChartGrouping.Month, months.Grouping);
        Assert.Equal(12, months.Buckets.Count);
        Assert.Equal("Oct 2026", months.Buckets.Single(b => b.Total > TimeSpan.Zero).Label);
    }

    [Fact]
    public void Cut_off_and_current_periods_are_marked_in_the_title()
    {
        var entries = new List<TimeEntry> { Entry(28, 9, 1, "Chat") };

        // 2026-09-02 (Wed) to 2026-10-28 (Wed): first and last weeks are cut off by the range.
        var weeks = ChartBuilder.Build(entries, new DateOnly(2026, 9, 2), Day(28), today: Day(28));
        Assert.Equal("Week of Mon 2026-08-31 (partial)", weeks.Buckets[0].Title);
        Assert.Equal("Week of Mon 2026-09-07", weeks.Buckets[1].Title);
        Assert.Equal("Week of Mon 2026-10-26 (partial)", weeks.Buckets[^1].Title);

        Assert.Equal([true, false], [weeks.Buckets[0].Incomplete, weeks.Buckets[1].Incomplete]);

        var days = ChartBuilder.Build(entries, Day(26), Day(30), today: Day(28));
        Assert.Equal(["Mon 2026-10-26", "Tue 2026-10-27", "Wed 2026-10-28 (so far)", "Thu 2026-10-29", "Fri 2026-10-30"],
            days.Buckets.Select(b => b.Title));
        Assert.Equal([false, false, true, false, false], days.Buckets.Select(b => b.Incomplete));
    }

    [Fact]
    public void Top_activities_keep_their_colour_in_every_range()
    {
        var entries = new List<TimeEntry>
        {
            Entry(26, 9, 5, "Project"),
            Entry(26, 14, 3, "Meetings"),
            Entry(27, 9, 1, "Chat"),
            Entry(28, 9, 0.5, "Meetings"),
        };

        int? SlotOf(ChartData chart, string name) => chart.Series.Single(s => s.Name == name).Slot;

        var week = ChartBuilder.Build(entries, Day(26), Day(28));
        var lastDay = ChartBuilder.Build(entries, Day(28), Day(28));
        Assert.Equal(1, SlotOf(week, "Meetings"));
        Assert.Equal(1, SlotOf(lastDay, "Meetings"));
        Assert.Equal(2, SlotOf(week, "Chat"));
    }

    [Fact]
    public void Smaller_activities_borrow_unused_colours_and_the_rest_fold_into_other()
    {
        // Nine activities: A (biggest) ... I (smallest). Only A-G get a home colour.
        var names = "ABCDEFGHI".Select(c => c.ToString()).ToList();
        var entries = names.Select((n, i) => Entry(1 + i, 9, 9 - i, n)).ToList();

        var slots = ChartBuilder.AssignSlots(entries, ["H", "B", "I"]);
        Assert.Equal(new Dictionary<string, int> { ["B"] = 1, ["H"] = 0, ["I"] = 2 }, slots);

        var everything = ChartBuilder.Build(entries, Day(1), Day(9));
        Assert.Equal(8, everything.Series.Count);
        var other = everything.Series[^1];
        Assert.Equal(ChartBuilder.OtherName, other.Name);
        Assert.Null(other.Slot);
        Assert.Equal(TimeSpan.FromHours(2 + 1), other.Total);
    }
}
