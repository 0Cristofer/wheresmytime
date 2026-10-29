using WheresMyTime.Core;

namespace WheresMyTime.Tests;

public class TimeTests
{
    static DateTime At(int day, int hour, int minute, int second = 0) => new(2026, 10, day, hour, minute, second);

    [Theory]
    [InlineData("9", 9, 0)]
    [InlineData("9:30", 9, 30)]
    [InlineData("09.30", 9, 30)]
    [InlineData("9h30", 9, 30)]
    [InlineData("9h", 9, 0)]
    [InlineData("930", 9, 30)]
    [InlineData("0930", 9, 30)]
    [InlineData(" 23:59 ", 23, 59)]
    public void Parses_clock_formats(string text, int hour, int minute)
    {
        Assert.True(TimeUtil.TryParseClock(text, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData("")]
    [InlineData("24:00")]
    [InlineData("9:60")]
    [InlineData("9:5")]
    [InlineData("abc")]
    [InlineData("12345")]
    public void Rejects_bad_clocks(string text) => Assert.False(TimeUtil.TryParseClock(text, out _));

    [Theory]
    [InlineData(0, "0m")]
    [InlineData(45, "45m")]
    [InlineData(60, "1h 00m")]
    [InlineData(65, "1h 05m")]
    [InlineData(750, "12h 30m")]
    public void Formats_durations(int minutes, string expected) =>
        Assert.Equal(expected, TimeUtil.FormatDuration(TimeSpan.FromMinutes(minutes)));

    [Fact]
    public void Clipboard_line_has_date_range_duration_activity_and_details()
    {
        var entry = new TimeEntry { Start = At(28, 9, 0), End = At(28, 10, 30), Activity = "Meetings", Details = "Sprint planning" };
        Assert.Equal("2026-10-28 09:00–10:30 (1h 30m) Meetings — Sprint planning", TimeUtil.ToClipboardLine(entry));

        entry.Details = "  ";
        Assert.Equal("2026-10-28 09:00–10:30 (1h 30m) Meetings", TimeUtil.ToClipboardLine(entry));

        entry.Start = At(27, 23, 0);
        Assert.Equal("2026-10-27 23:00–2026-10-28 10:30 (11h 30m) Meetings", TimeUtil.ToClipboardLine(entry));
    }

    [Fact]
    public void Untouched_times_keep_their_seconds()
    {
        var start = At(28, 9, 0, 41);
        var end = At(28, 10, 30, 12);
        Assert.True(EntryTimes.TryResolve(start, end, new DateOnly(2026, 10, 28), "09:00", "10:30", out var s, out var e, out _));
        Assert.Equal(start, s);
        Assert.Equal(end, e);
    }

    [Fact]
    public void Edited_end_before_start_runs_past_midnight()
    {
        Assert.True(EntryTimes.TryResolve(At(28, 22, 0), At(28, 23, 0), new DateOnly(2026, 10, 28), "22:00", "01:15", out var s, out var e, out _));
        Assert.Equal(At(28, 22, 0), s);
        Assert.Equal(At(29, 1, 15), e);
    }

    [Fact]
    public void Changing_the_date_moves_the_whole_entry()
    {
        Assert.True(EntryTimes.TryResolve(At(28, 9, 0, 41), At(28, 10, 0, 5), new DateOnly(2026, 10, 27), "09:00", "10:00", out var s, out var e, out _));
        Assert.Equal(At(27, 9, 0), s);
        Assert.Equal(At(27, 10, 0), e);
    }

    [Fact]
    public void Timer_left_running_for_days_keeps_its_real_end()
    {
        var end = At(30, 8, 0, 3);
        Assert.True(EntryTimes.TryResolve(At(28, 9, 0), end, new DateOnly(2026, 10, 28), "09:00", "08:00", out _, out var e, out _));
        Assert.Equal(end, e);

        // Fixing the end time brings it back to the same day.
        Assert.True(EntryTimes.TryResolve(At(28, 9, 0), end, new DateOnly(2026, 10, 28), "09:00", "17:30", out _, out e, out _));
        Assert.Equal(At(28, 17, 30), e);
    }

    [Fact]
    public void Start_moved_past_untouched_end_is_an_error()
    {
        Assert.False(EntryTimes.TryResolve(At(28, 9, 0), At(28, 10, 0), new DateOnly(2026, 10, 28), "11:00", "10:00", out _, out _, out var error));
        Assert.Equal("End must be after start.", error);
        Assert.False(EntryTimes.TryResolve(At(28, 9, 0), At(28, 10, 0), new DateOnly(2026, 10, 28), "nope", "10:00", out _, out _, out error));
        Assert.Contains("Start", error);
    }
}
