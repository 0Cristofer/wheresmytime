using System.Globalization;
using System.Text.RegularExpressions;

namespace WheresMyTime.Core;

/// <summary>Formatting and parsing of times and durations. Everything is invariant and 24-hour.</summary>
public static partial class TimeUtil
{
    public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Current local time, truncated to whole seconds.</summary>
    public static DateTime Now() => TruncateToSeconds(DateTime.Now);

    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);

    public static DateTime TruncateToSeconds(DateTime t) =>
        new(t.Year, t.Month, t.Day, t.Hour, t.Minute, t.Second, DateTimeKind.Unspecified);

    public static DateTime TruncateToMinutes(DateTime t) =>
        new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, DateTimeKind.Unspecified);

    public static int RoundedMinutes(TimeSpan duration) =>
        (int)Math.Round(duration.TotalMinutes, MidpointRounding.AwayFromZero);

    /// <summary>"45m", "1h 05m", "12h 30m".</summary>
    public static string FormatDuration(TimeSpan duration)
    {
        var minutes = RoundedMinutes(duration);
        return minutes < 60 ? $"{minutes}m" : $"{minutes / 60}h {minutes % 60:00}m";
    }

    /// <summary>"01:23:45", for the running timer.</summary>
    public static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        return $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }

    /// <summary>Decimal hours with two places, e.g. "1.50".</summary>
    public static string FormatHours(TimeSpan duration) =>
        (RoundedMinutes(duration) / 60.0).ToString("0.00", Invariant);

    public static string FormatClock(DateTime t) => t.ToString("HH:mm", Invariant);

    public static string FormatDate(DateOnly d) => d.ToString("yyyy-MM-dd", Invariant);

    /// <summary>"Tue 2026-10-28".</summary>
    public static string FormatDay(DateOnly d) => d.ToString("ddd yyyy-MM-dd", Invariant);

    /// <summary>"2026-10-28 09:00–10:30 (1h 30m) Meetings — Sprint planning".</summary>
    public static string ToClipboardLine(TimeEntry entry)
    {
        var end = entry.End.Date == entry.Start.Date
            ? FormatClock(entry.End)
            : entry.End.ToString("yyyy-MM-dd HH:mm", Invariant);
        var line = $"{entry.Start.ToString("yyyy-MM-dd HH:mm", Invariant)}–{end} ({FormatDuration(entry.Duration)}) {entry.Activity}";
        return string.IsNullOrWhiteSpace(entry.Details) ? line : $"{line} — {entry.Details.Trim()}";
    }

    [GeneratedRegex(@"^\s*(\d{1,2})(?:\s*[:.hH]\s*(\d{2})?)?\s*$")]
    private static partial Regex ClockWithSeparator();

    [GeneratedRegex(@"^\s*(\d{1,2})(\d{2})\s*$")]
    private static partial Regex ClockCompact();

    /// <summary>Parses "9", "9:30", "09.30", "9h30", "930" or "0930" as a time of day.</summary>
    public static bool TryParseClock(string? text, out TimeOnly time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = ClockWithSeparator().Match(text);
        if (!match.Success) match = ClockCompact().Match(text);
        if (!match.Success) return false;

        var hour = int.Parse(match.Groups[1].Value, Invariant);
        var minute = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, Invariant) : 0;
        if (hour > 23 || minute > 59) return false;

        time = new TimeOnly(hour, minute);
        return true;
    }
}
