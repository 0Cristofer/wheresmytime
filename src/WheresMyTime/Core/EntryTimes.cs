namespace WheresMyTime.Core;

public static class EntryTimes
{
    /// <summary>
    /// Turns the date + "HH:mm" fields of the entry dialog back into start and end times.
    /// A time the user didn't touch keeps its original seconds (and the original end keeps its day),
    /// and an edited end earlier than the start means the entry ran past midnight.
    /// </summary>
    public static bool TryResolve(
        DateTime originalStart, DateTime originalEnd, DateOnly date, string? startText, string? endText,
        out DateTime start, out DateTime end, out string? error)
    {
        start = end = default;
        if (!TimeUtil.TryParseClock(startText, out var startClock))
        {
            error = "Start time isn't valid — use HH:mm.";
            return false;
        }
        if (!TimeUtil.TryParseClock(endText, out var endClock))
        {
            error = "End time isn't valid — use HH:mm.";
            return false;
        }

        var sameDate = date == DateOnly.FromDateTime(originalStart);
        start = sameDate && startClock == ClockOf(originalStart) ? originalStart : date.ToDateTime(startClock);

        if (sameDate && endClock == ClockOf(originalEnd))
        {
            end = originalEnd;
        }
        else
        {
            end = date.ToDateTime(endClock);
            if (end <= start) end = end.AddDays(1);
        }

        if (end <= start)
        {
            error = "End must be after start.";
            return false;
        }

        error = null;
        return true;
    }

    static TimeOnly ClockOf(DateTime t) => new(t.Hour, t.Minute);
}
