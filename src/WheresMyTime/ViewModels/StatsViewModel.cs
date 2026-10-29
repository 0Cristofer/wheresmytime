using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WheresMyTime.Core;
using WheresMyTime.Services;

namespace WheresMyTime.ViewModels;

public sealed record DetailRow(string Name, string TotalText);

public sealed record ActivityRow(string Name, string TotalText, string PercentText, double Percent, IReadOnlyList<DetailRow> Details);

public sealed record DayRow(string Label, string TotalText, double Percent);

public sealed partial class LegendItem(string name, string totalText, int? slot) : ObservableObject
{
    public string Name { get; } = name;
    public string TotalText { get; } = totalText;
    public int? Slot { get; } = slot;

    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    public partial bool IsDimmed { get; set; }
}

public sealed partial class StatsViewModel : ObservableObject, IDisposable
{
    readonly EntryStore _store;
    readonly IUiServices _ui;
    Stats? _stats;

    public StatsViewModel(EntryStore store, IUiServices ui)
    {
        _store = store;
        _ui = ui;
        Preset("ThisWeek");
        _store.Saved += Rebuild;
    }

    public ObservableCollection<ActivityRow> Activities { get; } = [];
    public ObservableCollection<DayRow> Days { get; } = [];
    public ObservableCollection<LegendItem> Legend { get; } = [];

    [ObservableProperty]
    public partial ChartData? Chart { get; set; }

    [ObservableProperty]
    public partial string ChartTitle { get; set; } = "";

    [ObservableProperty]
    public partial string TrendTitle { get; set; } = "";

    [ObservableProperty]
    public partial string TrendHint { get; set; } = "";

    [ObservableProperty]
    public partial bool HasLegend { get; set; }

    /// <summary>The activity highlighted in both charts (set by the legend or by clicking a chart), or null.</summary>
    [ObservableProperty]
    public partial string? Highlighted { get; set; }

    [ObservableProperty]
    public partial DateTime? From { get; set; }

    [ObservableProperty]
    public partial DateTime? To { get; set; }

    [ObservableProperty]
    public partial bool ShowDetails { get; set; } = true;

    [ObservableProperty]
    public partial string TotalText { get; set; } = "";

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasData { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    partial void OnFromChanged(DateTime? value) => Rebuild();
    partial void OnToChanged(DateTime? value) => Rebuild();
    partial void OnHighlightedChanged(string? value) => UpdateLegendHighlight();

    public void Dispose() => _store.Saved -= Rebuild;

    [RelayCommand]
    void ToggleHighlight(LegendItem item) =>
        Highlighted = IsHighlighted(item.Name) ? null : item.Name;

    [RelayCommand]
    void Preset(string name)
    {
        var today = TimeUtil.Today();
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var (from, to) = name switch
        {
            "Today" => (today, today),
            "Yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "LastWeek" => (monday.AddDays(-7), monday.AddDays(-1)),
            "ThisMonth" => (firstOfMonth, firstOfMonth.AddMonths(1).AddDays(-1)),
            "LastMonth" => (firstOfMonth.AddMonths(-1), firstOfMonth.AddDays(-1)),
            // Whole weeks, so every point on the trend covers the same span.
            "Last12Weeks" => (monday.AddDays(-7 * 11), monday.AddDays(6)),
            "ThisYear" => (new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
            _ => (monday, monday.AddDays(6)),
        };
        From = from.ToDateTime(TimeOnly.MinValue);
        To = to.ToDateTime(TimeOnly.MinValue);
    }

    [RelayCommand]
    async Task Copy()
    {
        if (_stats is null) return;
        await _ui.SetClipboardTextAsync(StatsBuilder.ToText(_stats, ShowDetails));
        StatusText = "Copied to clipboard";
        await Task.Delay(TimeSpan.FromSeconds(3));
        StatusText = "";
    }

    [RelayCommand]
    async Task Export()
    {
        if (_stats is null) return;
        var entries = StatsBuilder.EntriesInRange(_store.Entries, _stats.From, _stats.To);
        var name = $"wheresmytime-{TimeUtil.FormatDate(_stats.From)}_{TimeUtil.FormatDate(_stats.To)}.csv";
        try
        {
            await _ui.SaveTextFileAsync("Export entries in this range", name, CsvFormat.Write(entries));
        }
        catch (Exception ex)
        {
            await _ui.ShowMessageAsync("Export failed", ex.Message);
        }
    }

    void Rebuild()
    {
        if (From is not { } from || To is not { } to) return;
        var stats = StatsBuilder.Build(_store.Entries, DateOnly.FromDateTime(from), DateOnly.FromDateTime(to));
        _stats = stats;

        Activities.Clear();
        foreach (var a in stats.Activities)
        {
            Activities.Add(new ActivityRow(
                a.Activity,
                TimeUtil.FormatDuration(a.Total),
                $"{a.Percent.ToString("0.0", TimeUtil.Invariant)}%",
                a.Percent,
                a.Details.Select(d => new DetailRow(d.Details, TimeUtil.FormatDuration(d.Total))).ToList()));
        }

        Days.Clear();
        var longestDay = stats.Days.Count > 0 ? stats.Days.Max(d => d.Total) : TimeSpan.Zero;
        foreach (var d in stats.Days)
        {
            var percent = longestDay > TimeSpan.Zero ? d.Total / longestDay * 100 : 0;
            Days.Add(new DayRow(TimeUtil.FormatDay(d.Date), TimeUtil.FormatDuration(d.Total), percent));
        }

        HasData = stats.EntryCount > 0;
        TotalText = TimeUtil.FormatDuration(stats.Total);
        var range = stats.From == stats.To
            ? TimeUtil.FormatDay(stats.From)
            : $"{TimeUtil.FormatDate(stats.From)} → {TimeUtil.FormatDate(stats.To)}";
        SummaryText = $"{range} · {stats.EntryCount} entries · {TimeUtil.FormatHours(stats.Total)} h";

        var chart = ChartBuilder.Build(_store.Entries, stats.From, stats.To);
        Chart = chart;
        var period = chart.Grouping switch
        {
            ChartGrouping.Week => "week",
            ChartGrouping.Month => "month",
            _ => "day",
        };
        ChartTitle = $"Time per {period}, by activity";
        TrendTitle = $"Each activity per {period}";
        TrendHint = $"Click a line or a name to highlight it. {char.ToUpperInvariant(period[0])}{period[1..]}s with nothing recorded "
                    + $"are skipped; an unfinished {period} is faded.";

        Legend.Clear();
        foreach (var s in chart.Series) Legend.Add(new LegendItem(s.Name, TimeUtil.FormatDuration(s.Total), s.Slot));
        HasLegend = chart.Series.Count > 1;
        if (Highlighted is not null && !chart.Series.Any(s => IsHighlighted(s.Name))) Highlighted = null;
        UpdateLegendHighlight();
    }

    bool IsHighlighted(string name) => string.Equals(name, Highlighted, StringComparison.OrdinalIgnoreCase);

    void UpdateLegendHighlight()
    {
        foreach (var item in Legend)
        {
            item.IsActive = IsHighlighted(item.Name);
            item.IsDimmed = Highlighted is not null && !item.IsActive;
        }
    }
}
