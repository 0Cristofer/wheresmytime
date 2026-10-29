using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using WheresMyTime.Core;

namespace WheresMyTime.Views;

/// <summary>
/// What the stats charts share: an hours axis, one slot per day/week/month along the bottom,
/// a hover box for the slot under the pointer (or picked with the arrow keys), and a
/// highlighted activity that is drawn in colour while the others turn grey.
/// </summary>
public abstract class TimeChart : Control
{
    public static readonly StyledProperty<ChartData?> DataProperty =
        AvaloniaProperty.Register<TimeChart, ChartData?>(nameof(Data));

    /// <summary>Name of the series to emphasise, or null for none.</summary>
    public static readonly StyledProperty<string?> HighlightedProperty =
        AvaloniaProperty.Register<TimeChart, string?>(nameof(Highlighted), defaultBindingMode: BindingMode.TwoWay);

    protected const double AxisFontSize = 11;
    const double ReadoutFontSize = 12;
    static readonly int[] StepMinutes = [15, 30, 60, 120, 180, 240, 300, 600, 1200, 2400, 3000, 6000, 12000];
    static readonly BoxShadows ReadoutShadow = BoxShadows.Parse("0 4 14 0 #33000000");
    protected static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    double _topMinutes = 1;

    static TimeChart()
    {
        AffectsRender<TimeChart>(DataProperty, HighlightedProperty);
        FocusableProperty.OverrideDefaultValue<TimeChart>(true);
    }

    protected TimeChart()
    {
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
        GotFocus += (_, _) =>
        {
            if (Active < 0 && !IsPointerOver) SetActive(LastWithData());
        };
        LostFocus += (_, _) => SetActive(-1);
    }

    public ChartData? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public string? Highlighted
    {
        get => GetValue(HighlightedProperty);
        set => SetValue(HighlightedProperty, value);
    }

    protected Rect Plot { get; private set; }
    protected double Band { get; private set; }
    protected int Active { get; private set; } = -1;
    protected double LineHeight { get; private set; }
    protected ChartPalette Palette { get; private set; } = ChartPalette.Light;
    protected Typeface Regular { get; private set; }
    protected Typeface Semibold { get; private set; }

    /// <summary>The largest value the hours axis has to fit, in minutes.</summary>
    protected abstract double MaxMinutes(ChartData data);

    protected abstract void DrawMarks(DrawingContext context, ChartData data, int highlight);

    /// <summary>The series to list in the hover box for a slot, in display order.</summary>
    protected abstract IEnumerable<int> ReadoutSeries(ChartData data, int bucket, int highlight);

    protected virtual void DrawActiveSlot(DrawingContext context, int bucket)
    {
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DataProperty) Active = -1;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        SetActive(IndexAt(e.GetPosition(this)));
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        SetActive(-1);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var count = Data?.Buckets.Count ?? 0;
        if (count == 0) return;
        if (e.Key == Key.Escape)
        {
            SetActive(-1);
            SetCurrentValue(HighlightedProperty, null);
            e.Handled = true;
            return;
        }
        int? next = e.Key switch
        {
            Key.Left => Active < 0 ? count - 1 : Math.Max(0, Active - 1),
            Key.Right => Active < 0 ? 0 : Math.Min(count - 1, Active + 1),
            Key.Home => 0,
            Key.End => count - 1,
            _ => null,
        };
        if (next is { } index)
        {
            SetActive(index);
            e.Handled = true;
        }
    }

    public sealed override void Render(DrawingContext context)
    {
        // A transparent fill makes the whole area, not just the marks, respond to the pointer.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (Data is not { Buckets.Count: > 0 } data) return;

        Palette = ChartPalette.For(ActualThemeVariant);
        var font = GetValue(TextElement.FontFamilyProperty);
        Regular = new Typeface(font);
        Semibold = new Typeface(font, FontStyle.Normal, FontWeight.SemiBold);
        var muted = Brush(Palette.MutedText);
        var count = data.Buckets.Count;

        // Hours axis: a round step giving at most five gridlines above the baseline, with a
        // little headroom so the labels on the tallest mark don't sit on the top gridline.
        var maxMinutes = Math.Max(1, MaxMinutes(data) * 1.08);
        var step = StepMinutes.FirstOrDefault(s => Math.Ceiling(maxMinutes / s) <= 5, StepMinutes[^1]);
        _topMinutes = Math.Ceiling(maxMinutes / step) * step;
        var ticks = Enumerable.Range(0, (int)(_topMinutes / step) + 1).Select(i => i * step).ToList();
        var tickTexts = ticks.Select(t => Text(FormatTick(t), Regular, AxisFontSize, muted)).ToList();

        LineHeight = tickTexts[0].Height;
        var axisWidth = tickTexts.Max(t => t.Width) + 8;
        Plot = new Rect(axisWidth, LineHeight + 8,
            Math.Max(0, Bounds.Width - axisWidth - 4),
            Math.Max(0, Bounds.Height - LineHeight * 2 - 16));
        Band = Plot.Width / count;
        if (Plot.Width <= 0 || Plot.Height <= 0) return;

        var gridPen = new Pen(Brush(Palette.Grid));
        var baselinePen = new Pen(Brush(Palette.Baseline));
        for (var i = 0; i < ticks.Count; i++)
        {
            var y = Math.Round(Y(ticks[i])) + 0.5;
            context.DrawLine(i == 0 ? baselinePen : gridPen, new Point(Plot.Left, y), new Point(Plot.Right, y));
            context.DrawText(tickTexts[i], new Point(axisWidth - 8 - tickTexts[i].Width, y - tickTexts[i].Height / 2));
        }

        var active = Active >= 0 && Active < count ? Active : -1;
        if (active >= 0) DrawActiveSlot(context, active);
        var highlight = HighlightIndex();
        DrawMarks(context, data, highlight);
        DrawSlotLabels(context, data, muted);
        if (active >= 0) DrawReadout(context, data, active, highlight);
    }

    protected double X(int bucket) => Plot.Left + Band * (bucket + 0.5);

    protected double Y(double minutes) => Plot.Bottom - minutes / _topMinutes * Plot.Height;

    /// <summary>The series colour, or grey when another series is highlighted.</summary>
    protected Color MarkColor(ChartData data, int series, int highlight, bool line) =>
        highlight < 0 || highlight == series ? Palette.SeriesColor(data.Series[series].Slot)
        : line ? Palette.DeemphasisLine
        : Palette.Deemphasis;

    /// <summary>Highlights the series, or clears the highlight if it already is.</summary>
    protected void ToggleHighlight(int series)
    {
        if (Data is not { } data || series < 0 || series >= data.Series.Count) return;
        SetCurrentValue(HighlightedProperty, HighlightIndex() == series ? null : data.Series[series].Name);
    }

    protected static IBrush Brush(Color color) => new SolidColorBrush(color);

    protected static FormattedText Text(string text, Typeface typeface, double size, IBrush brush) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, size, brush);

    int HighlightIndex()
    {
        if (Data is not { } data || Highlighted is not { } name) return -1;
        for (var i = 0; i < data.Series.Count; i++)
        {
            if (string.Equals(data.Series[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    /// <summary>Slot labels: long form if they all fit, else short form, thinned out if still crowded.</summary>
    void DrawSlotLabels(DrawingContext context, ChartData data, IBrush brush)
    {
        var labels = data.Buckets.Select(b => Text(b.Label, Regular, AxisFontSize, brush)).ToList();
        if (labels.Max(t => t.Width) + 8 > Band)
            labels = data.Buckets.Select(b => Text(b.ShortLabel, Regular, AxisFontSize, brush)).ToList();
        var every = Math.Max(1, (int)Math.Ceiling((labels.Max(t => t.Width) + 8) / Band));
        for (var i = 0; i < labels.Count; i += every)
        {
            var label = labels[i];
            var left = Math.Clamp(X(i) - label.Width / 2, 0, Math.Max(0, Bounds.Width - label.Width));
            context.DrawText(label, new Point(left, Plot.Bottom + 6));
        }
    }

    /// <summary>The hover box: the slot's date and total, then one row per activity, value first.</summary>
    void DrawReadout(DrawingContext context, ChartData data, int index, int highlight)
    {
        const double pad = 10, keyWidth = 12, keyGap = 8, nameGap = 8, rowGap = 4;
        var bucket = data.Buckets[index];
        var text = Brush(Palette.Text);
        var secondary = Brush(Palette.SecondaryText);

        var title = Text(bucket.Title, Regular, ReadoutFontSize, secondary);
        var total = Text(bucket.Total > TimeSpan.Zero ? $"{TimeUtil.FormatDuration(bucket.Total)} total" : "Nothing recorded",
            Semibold, ReadoutFontSize + 1, text);
        var rows = ReadoutSeries(data, index, highlight)
            .Select(k => (
                data.Series[k].Slot,
                Value: Text(TimeUtil.FormatDuration(bucket.Values[k]), Semibold, ReadoutFontSize, text),
                Name: Text(data.Series[k].Name, k == highlight ? Semibold : Regular, ReadoutFontSize, k == highlight ? text : secondary)))
            .ToList();

        var valueWidth = rows.Count > 0 ? rows.Max(r => r.Value.Width) : 0;
        var rowsWidth = rows.Count > 0 ? keyWidth + keyGap + valueWidth + nameGap + rows.Max(r => r.Name.Width) : 0;
        var rowHeight = rows.Count > 0 ? rows.Max(r => r.Value.Height) : 0;
        var width = Math.Max(Math.Max(title.Width, total.Width), rowsWidth) + pad * 2;
        var height = pad + title.Height + 2 + total.Height
                     + (rows.Count > 0 ? 8 + rows.Count * rowHeight + (rows.Count - 1) * rowGap : 0) + pad;

        // Beside the slot, flipping to its left near the right edge.
        var slotLeft = Plot.Left + Band * index;
        var x = slotLeft + Band + 6;
        if (x + width > Bounds.Width - 2) x = slotLeft - 6 - width;
        x = Math.Max(2, x);
        var y = Math.Max(2, Math.Min(Plot.Top, Bounds.Height - height - 2));

        context.DrawRectangle(Brush(Palette.TooltipBackground), new Pen(Brush(Palette.TooltipBorder)),
            new RoundedRect(new Rect(x, y, width, height), 8), ReadoutShadow);

        var cursor = y + pad;
        context.DrawText(title, new Point(x + pad, cursor));
        cursor += title.Height + 2;
        context.DrawText(total, new Point(x + pad, cursor));
        cursor += total.Height + 8;
        foreach (var row in rows)
        {
            var middle = cursor + rowHeight / 2;
            var keyPen = new Pen(Brush(Palette.SeriesColor(row.Slot)), 2, lineCap: PenLineCap.Round);
            context.DrawLine(keyPen, new Point(x + pad + 1, middle), new Point(x + pad + keyWidth - 1, middle));
            var valueLeft = x + pad + keyWidth + keyGap;
            context.DrawText(row.Value, new Point(valueLeft + valueWidth - row.Value.Width, cursor));
            context.DrawText(row.Name, new Point(valueLeft + valueWidth + nameGap, cursor));
            cursor += rowHeight + rowGap;
        }
    }

    void SetActive(int index)
    {
        if (index == Active) return;
        Active = index;
        InvalidateVisual();
    }

    int IndexAt(Point point)
    {
        var count = Data?.Buckets.Count ?? 0;
        if (count == 0 || Band <= 0 || point.X < Plot.Left || point.X >= Plot.Right) return -1;
        return Math.Clamp((int)((point.X - Plot.Left) / Band), 0, count - 1);
    }

    int LastWithData()
    {
        var buckets = Data?.Buckets ?? [];
        for (var i = buckets.Count - 1; i >= 0; i--)
        {
            if (buckets[i].Total > TimeSpan.Zero) return i;
        }
        return buckets.Count - 1;
    }

    static string FormatTick(int minutes) =>
        minutes == 0 ? "0" : minutes < 60 ? $"{minutes}m" : minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes / 60}h{minutes % 60:00}";
}
