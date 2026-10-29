using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheresMyTime.Core;
using WheresMyTime.Services;

namespace WheresMyTime.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    const string AppTitle = "Where's my time";

    readonly EntryStore _store;
    readonly IUiServices _ui;
    DateOnly _today = TimeUtil.Today();
    DateTime _statusUntil;

    public MainViewModel(EntryStore store, IUiServices ui)
    {
        _store = store;
        _ui = ui;
        if (store.Running is { } running)
        {
            Activity = running.Activity;
            Details = running.Details;
        }
        SelectedDay = _today;
        RefreshSuggestions();
        RefreshDay();
        Tick();
    }

    public ObservableCollection<EntryRow> DayEntries { get; } = [];
    public string DataDirectory => _store.DataDirectory;
    public bool IsRunning => _store.Running is not null;

    [ObservableProperty]
    public partial string ClockTime { get; set; } = "";

    [ObservableProperty]
    public partial string ClockDate { get; set; } = "";

    [ObservableProperty]
    public partial string WindowTitle { get; set; } = AppTitle;

    [ObservableProperty]
    public partial string? Activity { get; set; } = "";

    [ObservableProperty]
    public partial string? Details { get; set; } = "";

    [ObservableProperty]
    public partial string StartStopText { get; set; } = "Start";

    [ObservableProperty]
    public partial string RunningInfo { get; set; } = "";

    [ObservableProperty]
    public partial IReadOnlyList<string> ActivitySuggestions { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<string> DetailSuggestions { get; set; } = [];

    [ObservableProperty]
    public partial DateOnly SelectedDay { get; set; }

    [ObservableProperty]
    public partial string DayTitle { get; set; } = "";

    [ObservableProperty]
    public partial string DayTotal { get; set; } = "";

    [ObservableProperty]
    public partial bool HasDayEntries { get; set; }

    [ObservableProperty]
    public partial bool IsToday { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    partial void OnSelectedDayChanged(DateOnly value) => RefreshDay();

    // Keep the running timer's fields current so any save (and closing the app) keeps them.
    partial void OnActivityChanged(string? value)
    {
        if (_store.Running is { } running) running.Activity = Activities.Clean(value);
    }

    partial void OnDetailsChanged(string? value)
    {
        if (_store.Running is { } running) running.Details = (value ?? "").Trim();
    }

    /// <summary>Called a few times per second by the window.</summary>
    public void Tick()
    {
        var now = DateTime.Now;
        ClockTime = now.ToString("HH:mm:ss", TimeUtil.Invariant);
        ClockDate = now.ToString("dddd, d MMMM yyyy", TimeUtil.Invariant);

        var today = DateOnly.FromDateTime(now);
        if (today != _today)
        {
            var followToday = SelectedDay == _today;
            _today = today;
            if (followToday) SelectedDay = today;
            else RefreshDay();
        }

        if (_store.Running is { } running)
        {
            var elapsed = TimeUtil.FormatElapsed(now - running.Start);
            StartStopText = $"Stop  ·  {elapsed}";
            var startDay = DateOnly.FromDateTime(running.Start);
            var since = startDay == today
                ? TimeUtil.FormatClock(running.Start)
                : $"{TimeUtil.FormatDay(startDay)} {TimeUtil.FormatClock(running.Start)}";
            RunningInfo = $"Started {since} · keeps running if you close the app";
            var name = Activities.Clean(Activity);
            WindowTitle = $"{elapsed[..^3]} {(name.Length > 0 ? name : "running")} — {AppTitle}";
        }
        else
        {
            StartStopText = "Start";
            RunningInfo = "";
            WindowTitle = AppTitle;
        }

        if (StatusText.Length > 0 && now > _statusUntil) StatusText = "";
    }

    /// <summary>Writes the running timer's fields when the window closes.</summary>
    public void SaveOnClose()
    {
        if (_store.Running is null) return;
        try
        {
            _store.Save();
        }
        catch
        {
            // Closing; the timer's start time was already saved when it started.
        }
    }

    [RelayCommand]
    async Task StartStop()
    {
        if (_store.Running is null) await Start();
        else await Stop();
    }

    async Task Start()
    {
        var running = new RunningTimer
        {
            Start = TimeUtil.Now(),
            Activity = Activities.Clean(Activity),
            Details = (Details ?? "").Trim(),
        };
        if (await TrySave(() => _store.Running = running)) RunningChanged();
    }

    async Task Stop()
    {
        var running = _store.Running!;
        var draft = new TimeEntry
        {
            Start = running.Start,
            End = TimeUtil.Now(),
            Activity = Activities.Clean(Activity),
            Details = (Details ?? "").Trim(),
        };
        var dialog = NewDialog(EntryDialogMode.Stop, draft);

        switch (await _ui.ShowEntryDialogAsync(dialog))
        {
            case EntryDialogResult.Save:
                var entry = dialog.ToEntry();
                entry.Activity = Activities.Normalize(entry.Activity, _store.Entries);
                if (!await TrySave(() =>
                    {
                        _store.Entries.Add(entry);
                        _store.Running = null;
                    }))
                {
                    return;
                }
                ClearInputs();
                RunningChanged();
                AfterEntriesChanged(DateOnly.FromDateTime(entry.Start));
                break;

            case EntryDialogResult.Discard:
                var confirmed = await _ui.ConfirmAsync("Discard timer?",
                    $"The {TimeUtil.FormatDuration(draft.Duration)} since {TimeUtil.FormatClock(draft.Start)} won't be recorded.",
                    "Discard", destructive: true);
                if (!confirmed || !await TrySave(() => _store.Running = null)) return;
                ClearInputs();
                RunningChanged();
                break;

            default:
                // Keep running, but keep anything typed into the dialog.
                Activity = dialog.Activity;
                Details = dialog.Details;
                break;
        }
    }

    [RelayCommand]
    void PreviousDay() => SelectedDay = SelectedDay.AddDays(-1);

    [RelayCommand(CanExecute = nameof(CanGoToNextDay))]
    void NextDay() => SelectedDay = SelectedDay.AddDays(1);

    bool CanGoToNextDay() => SelectedDay < _today;

    [RelayCommand]
    void GoToToday() => SelectedDay = _today;

    [RelayCommand]
    async Task CopyEntry(EntryRow row)
    {
        await _ui.SetClipboardTextAsync(TimeUtil.ToClipboardLine(row.Entry));
        ShowStatus("Copied to clipboard");
    }

    [RelayCommand]
    async Task EditEntry(EntryRow row)
    {
        var dialog = NewDialog(EntryDialogMode.Edit, row.Entry);
        if (await _ui.ShowEntryDialogAsync(dialog) != EntryDialogResult.Save) return;

        var updated = dialog.ToEntry();
        updated.Activity = Activities.Normalize(updated.Activity, _store.Entries.Where(e => e.Id != updated.Id));
        var index = _store.Entries.FindIndex(e => e.Id == updated.Id);
        if (index < 0) return;
        if (await TrySave(() => _store.Entries[index] = updated))
            AfterEntriesChanged(DateOnly.FromDateTime(updated.Start));
    }

    [RelayCommand]
    async Task DeleteEntry(EntryRow row)
    {
        var id = row.Entry.Id;
        if (!await _ui.ConfirmAsync("Delete entry?", TimeUtil.ToClipboardLine(row.Entry), "Delete", destructive: true)) return;
        if (await TrySave(() => _store.Entries.RemoveAll(e => e.Id == id)))
            AfterEntriesChanged(SelectedDay);
    }

    [RelayCommand]
    async Task AddEntry()
    {
        // Default to the gap after the day's last entry.
        var lastEnd = _store.Entries
            .Where(e => DateOnly.FromDateTime(e.End) == SelectedDay)
            .Select(e => (DateTime?)e.End)
            .Max();
        DateTime start, end;
        if (SelectedDay == _today)
        {
            end = TimeUtil.TruncateToMinutes(DateTime.Now);
            start = lastEnd is { } last && last < end ? last : end.AddMinutes(-30);
        }
        else
        {
            start = lastEnd ?? SelectedDay.ToDateTime(new TimeOnly(9, 0));
            end = start.AddMinutes(30);
        }

        var dialog = NewDialog(EntryDialogMode.Add, new TimeEntry { Start = start, End = end });
        if (await _ui.ShowEntryDialogAsync(dialog) != EntryDialogResult.Save) return;

        var entry = dialog.ToEntry();
        entry.Activity = Activities.Normalize(entry.Activity, _store.Entries);
        if (await TrySave(() => _store.Entries.Add(entry)))
            AfterEntriesChanged(DateOnly.FromDateTime(entry.Start));
    }

    [RelayCommand]
    async Task Export()
    {
        if (_store.Entries.Count == 0)
        {
            await _ui.ShowMessageAsync("Nothing to export", "There are no time entries yet.");
            return;
        }
        try
        {
            var name = $"wheresmytime-{TimeUtil.FormatDate(_today)}.csv";
            if (await _ui.SaveTextFileAsync("Export all time entries", name, CsvFormat.Write(_store.Entries)))
                ShowStatus($"Exported {Plural(_store.Entries.Count, "entry", "entries")}");
        }
        catch (Exception ex)
        {
            await _ui.ShowMessageAsync("Export failed", ex.Message);
        }
    }

    [RelayCommand]
    async Task Import()
    {
        string? text;
        try
        {
            text = await _ui.OpenTextFileAsync("Import time entries (CSV)");
        }
        catch (Exception ex)
        {
            await _ui.ShowMessageAsync("Import failed", ex.Message);
            return;
        }
        if (text is null) return;

        var read = CsvFormat.Read(text);
        if (read.FatalError is not null)
        {
            await _ui.ShowMessageAsync("Import failed", read.FatalError);
            return;
        }

        MergeResult merged = new(0, 0);
        if (!await TrySave(() =>
            {
                _store.BackupBeforeImport();
                merged = EntryMerge.Merge(_store.Entries, read.Entries);
            }))
        {
            return;
        }
        AfterEntriesChanged(SelectedDay);

        var message = new StringBuilder($"Added {Plural(merged.Added, "entry", "entries")}.");
        if (merged.Duplicates > 0) message.Append($"\nSkipped {merged.Duplicates} already here.");
        if (read.Errors.Count > 0)
        {
            message.Append($"\n\n{Plural(read.Errors.Count, "row", "rows")} couldn't be read:\n");
            message.AppendJoin('\n', read.Errors.Take(10));
            if (read.Errors.Count > 10) message.Append("\n…");
        }
        await _ui.ShowMessageAsync("Import finished", message.ToString());
    }

    [RelayCommand]
    void OpenStats() => _ui.ShowStats(() => new StatsViewModel(_store, _ui));

    [RelayCommand]
    async Task OpenDataFolder()
    {
        try
        {
            await _ui.OpenFolderAsync(_store.DataDirectory);
        }
        catch (Exception ex)
        {
            await _ui.ShowMessageAsync("Data folder", $"{_store.DataDirectory}\n\n{ex.Message}");
        }
    }

    EntryDialogViewModel NewDialog(EntryDialogMode mode, TimeEntry entry) =>
        new(mode, entry, ActivitySuggestions, DetailSuggestions);

    async Task<bool> TrySave(Action change)
    {
        try
        {
            change();
            _store.Save();
            return true;
        }
        catch (Exception ex)
        {
            await _ui.ShowMessageAsync("Couldn't save", $"Saving to {_store.FilePath} failed:\n{ex.Message}");
            return false;
        }
    }

    void ClearInputs()
    {
        Activity = "";
        Details = "";
    }

    void RunningChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        Tick();
    }

    void AfterEntriesChanged(DateOnly showDay)
    {
        RefreshSuggestions();
        if (SelectedDay != showDay) SelectedDay = showDay;
        else RefreshDay();
    }

    void RefreshSuggestions()
    {
        ActivitySuggestions = Activities.Suggestions(_store.Entries);
        DetailSuggestions = Activities.DetailSuggestions(_store.Entries);
    }

    void RefreshDay()
    {
        var entries = _store.Entries
            .Where(e => DateOnly.FromDateTime(e.Start) == SelectedDay)
            .OrderByDescending(e => e.Start)
            .ToList();
        DayEntries.Clear();
        foreach (var entry in entries) DayEntries.Add(new EntryRow(entry));

        HasDayEntries = entries.Count > 0;
        IsToday = SelectedDay == _today;
        DayTitle = SelectedDay == _today ? "Today"
            : SelectedDay == _today.AddDays(-1) ? "Yesterday"
            : TimeUtil.FormatDay(SelectedDay);
        DayTotal = entries.Count > 0
            ? TimeUtil.FormatDuration(entries.Aggregate(TimeSpan.Zero, (total, e) => total + e.Duration))
            : "";
        NextDayCommand.NotifyCanExecuteChanged();
    }

    void ShowStatus(string text)
    {
        StatusText = text;
        _statusUntil = DateTime.Now.AddSeconds(3);
    }

    static string Plural(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
}
