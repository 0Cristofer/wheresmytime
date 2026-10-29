using CommunityToolkit.Mvvm.ComponentModel;
using WheresMyTime.Core;

namespace WheresMyTime.ViewModels;

public enum EntryDialogMode { Stop, Edit, Add }

public enum EntryDialogResult { Cancel, Save, Discard }

/// <summary>The one dialog used to confirm a stopped timer, edit an entry, or add one by hand.</summary>
public sealed partial class EntryDialogViewModel : ObservableObject
{
    readonly TimeEntry _original;
    DateTime _start, _end;

    public EntryDialogViewModel(
        EntryDialogMode mode, TimeEntry original,
        IReadOnlyList<string> activitySuggestions, IReadOnlyList<string> detailSuggestions)
    {
        Mode = mode;
        _original = original;
        ActivitySuggestions = activitySuggestions;
        DetailSuggestions = detailSuggestions;
        Activity = original.Activity;
        Details = original.Details;
        Date = original.Start.Date;
        StartText = TimeUtil.FormatClock(original.Start);
        EndText = TimeUtil.FormatClock(original.End);
        Recompute();
    }

    public EntryDialogMode Mode { get; }
    public IReadOnlyList<string> ActivitySuggestions { get; }
    public IReadOnlyList<string> DetailSuggestions { get; }

    public string Title => Mode switch
    {
        EntryDialogMode.Stop => "Save time entry",
        EntryDialogMode.Edit => "Edit entry",
        _ => "Add entry",
    };

    public bool CanDiscard => Mode == EntryDialogMode.Stop;
    public string CancelText => Mode == EntryDialogMode.Stop ? "Keep running" : "Cancel";
    public bool CanSave => TimesValid && Activities.Clean(Activity).Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial string? Activity { get; set; }

    [ObservableProperty]
    public partial string? Details { get; set; }

    [ObservableProperty]
    public partial DateTime? Date { get; set; }

    [ObservableProperty]
    public partial string? StartText { get; set; }

    [ObservableProperty]
    public partial string? EndText { get; set; }

    [ObservableProperty]
    public partial string DurationText { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    public partial string? Warning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    public partial bool TimesValid { get; set; }

    partial void OnDateChanged(DateTime? value) => Recompute();
    partial void OnStartTextChanged(string? value) => Recompute();
    partial void OnEndTextChanged(string? value) => Recompute();

    public TimeEntry ToEntry() => new()
    {
        Id = _original.Id,
        Start = _start,
        End = _end,
        Activity = Activities.Clean(Activity),
        Details = (Details ?? "").Trim(),
    };

    void Recompute()
    {
        Warning = null;
        if (Date is not { } date)
        {
            Fail("Pick a date.");
            return;
        }
        if (!EntryTimes.TryResolve(_original.Start, _original.End, DateOnly.FromDateTime(date),
                StartText, EndText, out var start, out var end, out var error))
        {
            Fail(error!);
            return;
        }

        _start = start;
        _end = end;
        Error = null;
        TimesValid = true;

        var duration = end - start;
        DurationText = end.Date == start.Date ? TimeUtil.FormatDuration(duration)
            : end.Date == start.Date.AddDays(1) ? $"{TimeUtil.FormatDuration(duration)} · ends next day"
            : $"{TimeUtil.FormatDuration(duration)} · ends {TimeUtil.FormatDay(DateOnly.FromDateTime(end))}";
        if (duration > TimeSpan.FromHours(12))
            Warning = "That's a long entry — double-check the times.";
    }

    void Fail(string message)
    {
        Error = message;
        TimesValid = false;
        DurationText = "";
    }
}
