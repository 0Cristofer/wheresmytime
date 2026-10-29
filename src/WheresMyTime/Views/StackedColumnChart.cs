using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using WheresMyTime.Core;

namespace WheresMyTime.Views;

/// <summary>
/// One column per day (or week/month), stacked by activity. Clicking a segment highlights its
/// activity, which then moves to the bottom of every column so it reads against the baseline.
/// </summary>
public sealed class StackedColumnChart : TimeChart
{
    const double MaxBarWidth = 24;
    const double SegmentGap = 2;
    const double CornerRadius = 4;

    readonly List<(Rect Bounds, int Series)> _segments = [];

    protected override double MaxMinutes(ChartData data) => data.Buckets.Max(b => b.Total.TotalMinutes);

    protected override void DrawActiveSlot(DrawingContext context, int bucket)
    {
        var wash = new Rect(Plot.Left + Band * bucket, Plot.Top - LineHeight - 6, Band, Plot.Height + LineHeight + 6);
        context.DrawRectangle(Brush(Palette.Hover), null, new RoundedRect(wash, 4));
    }

    protected override void DrawMarks(DrawingContext context, ChartData data, int highlight)
    {
        _segments.Clear();
        var order = StackOrder(data, highlight);
        var barWidth = Math.Clamp(Band * 0.62, 2, MaxBarWidth);
        var brushes = Enumerable.Range(0, data.Series.Count)
            .Select(k => Brush(MarkColor(data, k, highlight, line: false)))
            .ToArray();

        // Bottom-up, with a gap between segments and a rounded top.
        for (var i = 0; i < data.Buckets.Count; i++)
        {
            var values = data.Buckets[i].Values;
            var stack = order.Where(k => values[k] > TimeSpan.Zero).ToList();
            var x = X(i) - barWidth / 2;
            double below = 0;
            for (var n = 0; n < stack.Count; n++)
            {
                var k = stack[n];
                var bottom = Y(below) - (n == 0 ? 0 : SegmentGap);
                below += values[k].TotalMinutes;
                var top = Y(below);
                var height = bottom - top;
                if (height < 0.5) continue;

                var rect = new Rect(x, top, barWidth, height);
                if (n == stack.Count - 1)
                {
                    var radius = Math.Min(CornerRadius, Math.Min(height / 2, barWidth / 2));
                    context.DrawRectangle(brushes[k], null, new RoundedRect(rect, radius));
                    context.FillRectangle(brushes[k], new Rect(x, top + radius, barWidth, height - radius));
                }
                else
                {
                    context.FillRectangle(brushes[k], rect);
                }
                _segments.Add((rect.Inflate(new Thickness(4, 1)), k));
            }
        }

        // Column totals on the caps, only when every one of them fits.
        var secondary = Brush(Palette.SecondaryText);
        var caps = data.Buckets
            .Select(b => b.Total > TimeSpan.Zero ? Text(TimeUtil.FormatDuration(b.Total), Regular, AxisFontSize, secondary) : null)
            .ToList();
        if (caps.All(t => t is null || t.Width + 4 <= Band))
        {
            for (var i = 0; i < caps.Count; i++)
            {
                if (caps[i] is not { } cap) continue;
                context.DrawText(cap, new Point(X(i) - cap.Width / 2, Y(data.Buckets[i].Total.TotalMinutes) - cap.Height - 3));
            }
        }
    }

    // From the top of the stack down, matching what's on screen.
    protected override IEnumerable<int> ReadoutSeries(ChartData data, int bucket, int highlight) =>
        StackOrder(data, highlight).AsEnumerable().Reverse().Where(k => data.Buckets[bucket].Values[k] > TimeSpan.Zero);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = SegmentAt(e.GetPosition(this)) >= 0 ? HandCursor : null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var series = SegmentAt(e.GetPosition(this));
        if (series >= 0) ToggleHighlight(series);
    }

    int SegmentAt(Point point)
    {
        foreach (var (bounds, series) in _segments)
        {
            if (bounds.Contains(point)) return series;
        }
        return -1;
    }

    static List<int> StackOrder(ChartData data, int highlight)
    {
        var order = Enumerable.Range(0, data.Series.Count).ToList();
        if (highlight >= 0)
        {
            order.Remove(highlight);
            order.Insert(0, highlight);
        }
        return order;
    }
}
