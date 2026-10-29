using WheresMyTime.Core;

namespace WheresMyTime.ViewModels;

public sealed class EntryRow(TimeEntry entry)
{
    public TimeEntry Entry { get; } = entry;
    public string TimeRange => $"{TimeUtil.FormatClock(Entry.Start)}–{TimeUtil.FormatClock(Entry.End)}";

    public string DurationText => Entry.End.Date > Entry.Start.Date
        ? $"{TimeUtil.FormatDuration(Entry.Duration)} · +1d"
        : TimeUtil.FormatDuration(Entry.Duration);

    public string Activity => Entry.Activity;
    public string Details => Entry.Details;
    public bool HasDetails => !string.IsNullOrWhiteSpace(Entry.Details);
}
