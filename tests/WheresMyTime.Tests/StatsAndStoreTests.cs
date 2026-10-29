using WheresMyTime.Core;
using WheresMyTime.Services;

namespace WheresMyTime.Tests;

public class StatsAndStoreTests
{
    static DateTime At(int day, int hour, int minute) => new(2026, 10, day, hour, minute, 0);
    static DateOnly Day(int day) => new(2026, 10, day);

    [Fact]
    public void Stats_groups_by_activity_ignoring_case_with_details_and_percentages()
    {
        var entries = new List<TimeEntry>
        {
            new() { Start = At(27, 9, 0), End = At(27, 10, 0), Activity = "meetings", Details = "Standup" },
            new() { Start = At(28, 9, 0), End = At(28, 11, 0), Activity = "Meetings", Details = "Planning" },
            new() { Start = At(28, 11, 0), End = At(28, 12, 0), Activity = "Chat" },
            new() { Start = At(20, 9, 0), End = At(20, 18, 0), Activity = "Outside range" },
        };

        var stats = StatsBuilder.Build(entries, Day(27), Day(28));

        Assert.Equal(TimeSpan.FromHours(4), stats.Total);
        Assert.Equal(3, stats.EntryCount);
        Assert.Equal(["Meetings", "Chat"], stats.Activities.Select(a => a.Activity));
        Assert.Equal(75, stats.Activities[0].Percent, 3);
        Assert.Equal(["Planning", "Standup"], stats.Activities[0].Details.Select(d => d.Details));
        Assert.Equal(StatsBuilder.NoDetails, stats.Activities[1].Details[0].Details);
        Assert.Equal([(Day(27), TimeSpan.FromHours(1)), (Day(28), TimeSpan.FromHours(3))],
            stats.Days.Select(d => (d.Date, d.Total)));
    }

    [Fact]
    public void Stats_splits_entries_at_midnight_and_range_edges()
    {
        var entries = new List<TimeEntry>
        {
            new() { Start = At(27, 22, 0), End = At(28, 2, 0), Activity = "Deploy" },
        };

        var both = StatsBuilder.Build(entries, Day(27), Day(28));
        Assert.Equal(TimeSpan.FromHours(4), both.Total);
        Assert.Equal([TimeSpan.FromHours(2), TimeSpan.FromHours(2)], both.Days.Select(d => d.Total));

        var secondDayOnly = StatsBuilder.Build(entries, Day(28), Day(28));
        Assert.Equal(TimeSpan.FromHours(2), secondDayOnly.Total);
        Assert.Single(StatsBuilder.EntriesInRange(entries, Day(28), Day(28)));
        Assert.Empty(StatsBuilder.EntriesInRange(entries, Day(29), Day(30)));
    }

    [Fact]
    public void Stats_text_lists_activities_details_and_days()
    {
        var entries = new List<TimeEntry>
        {
            new() { Start = At(27, 9, 0), End = At(27, 10, 0), Activity = "Chat", Details = "Support" },
            new() { Start = At(28, 9, 0), End = At(28, 10, 30), Activity = "Meetings" },
        };
        var text = StatsBuilder.ToText(StatsBuilder.Build(entries, Day(27), Day(28)), includeDetails: true);
        Assert.Contains("Total: 2h 30m (2.50 h) in 2 entries", text);
        Assert.Contains("Meetings — 1h 30m (60.0%)", text);
        Assert.Contains("    • Support — 1h 00m", text);
        Assert.Contains("Tue 2026-10-27 — 1h 00m", text);
    }

    [Fact]
    public void Store_saves_loads_and_locks()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wheresmytime-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var store = EntryStore.Open(dir))
            {
                Assert.Throws<DataLockedException>(() => EntryStore.Open(dir));

                store.Entries.Add(new TimeEntry { Start = At(28, 9, 0), End = At(28, 9, 30), Activity = "Chat" });
                store.Running = new RunningTimer { Start = At(28, 10, 0), Activity = "Coding" };
                store.Save();
                store.Save();
                Assert.True(File.Exists(store.FilePath + ".bak"));
            }

            using (var reopened = EntryStore.Open(dir))
            {
                var entry = Assert.Single(reopened.Entries);
                Assert.Equal(At(28, 9, 0), entry.Start);
                Assert.Equal("Chat", entry.Activity);
                Assert.Equal("Coding", reopened.Running?.Activity);
            }

            File.WriteAllText(Path.Combine(dir, EntryStore.DataFileName), "{ not json");
            Assert.Throws<DataFileException>(() => EntryStore.Open(dir));
            Assert.Equal("{ not json", File.ReadAllText(Path.Combine(dir, EntryStore.DataFileName)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
