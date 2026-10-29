using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using WheresMyTime.Core;

namespace WheresMyTime.Views;

/// <summary>
/// One line per activity over the days (or weeks/months), to see what's growing or shrinking.
/// Slots where nothing at all was recorded (weekends, days off, the future) are skipped rather
/// than drawn as zero. Clicking near a line highlights it; the rest turn grey.
/// </summary>
public sealed class TrendLineChart : TimeChart
{
    // Clicks within this distance of a line select it (a 24px-wide target around a 2px line).
    const double HitDistance = 12;

    // Each series' line: the buckets it has points in, and those points (filled in while
    // rendering, and used for hit testing).
    readonly List<List<int>> _lineBuckets = [];
    readonly List<Point[]> _lines = [];

    protected override double MaxMinutes(ChartData data) =>
        data.Buckets.SelectMany(b => b.Values).Select(v => v.TotalMinutes).DefaultIfEmpty(0).Max();

    protected override void DrawActiveSlot(DrawingContext context, int bucket)
    {
        var x = Math.Round(X(bucket)) + 0.5;
        context.DrawLine(new Pen(Brush(Palette.Baseline)), new Point(x, Plot.Top), new Point(x, Plot.Bottom));
    }

    protected override void DrawMarks(DrawingContext context, ChartData data, int highlight)
    {
        _lineBuckets.Clear();
        _lines.Clear();
        for (var k = 0; k < data.Series.Count; k++)
        {
            var series = k;
            var buckets = Enumerable.Range(0, data.Buckets.Count).Where(i => data.HasPoint(i, series)).ToList();
            _lineBuckets.Add(buckets);
            _lines.Add(buckets.Select(i => new Point(X(i), Y(data.Buckets[i].Values[series].TotalMinutes))).ToArray());
        }

        var order = DrawOrder(data, highlight);
        foreach (var k in order)
        {
            var points = _lines[k];
            var color = MarkColor(data, k, highlight, line: true);
            if (points.Length >= 2)
            {
                var thickness = k == highlight ? 2.5 : 2;
                var solid = new Pen(Brush(color), thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
                var faded = new Pen(Brush(Color.FromArgb((byte)(color.A * 0.35), color.R, color.G, color.B)), thickness, lineCap: PenLineCap.Round);
                DrawLine(context, data, _lineBuckets[k], points, solid, faded);
            }
            // A lone point has no line to show it; the highlighted line gets dots when there's room.
            if (points.Length == 1 || (k == highlight && Band >= 12))
            {
                foreach (var point in points) DrawDot(context, point, color);
            }
        }

        if (Active >= 0 && Active < data.Buckets.Count)
        {
            foreach (var k in order.Where(k => data.HasPoint(Active, k)))
                DrawDot(context, new Point(X(Active), Y(data.Buckets[Active].Values[k].TotalMinutes)), MarkColor(data, k, highlight, line: true));
        }

        if (highlight >= 0) LabelHighlightedLine(context, data, highlight, _lineBuckets[highlight]);
    }

    // Everything at this slot, biggest first, with the highlighted activity on top.
    protected override IEnumerable<int> ReadoutSeries(ChartData data, int bucket, int highlight)
    {
        var values = data.Buckets[bucket].Values;
        return Enumerable.Range(0, data.Series.Count)
            .Where(k => values[k] > TimeSpan.Zero)
            .OrderByDescending(k => k == highlight)
            .ThenByDescending(k => values[k]);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        Cursor = SeriesNear(e.GetPosition(this)) >= 0 ? HandCursor : null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var series = SeriesNear(e.GetPosition(this));
        if (series >= 0) ToggleHighlight(series);
    }

    /// <summary>
    /// The highest and the latest value of the highlighted line, so its growth reads without
    /// hovering. If the two labels would overlap, the highest wins.
    /// </summary>
    void LabelHighlightedLine(DrawingContext context, ChartData data, int highlight, List<int> withData)
    {
        if (withData.Count == 0) return;
        var text = Brush(Palette.Text);
        var last = withData[^1];
        var peak = withData.MaxBy(i => data.Buckets[i].Values[highlight]);

        FormattedText Label(int bucket) =>
            Text(TimeUtil.FormatDuration(data.Buckets[bucket].Values[highlight]), Semibold, AxisFontSize, text);

        void Draw(FormattedText label, int bucket)
        {
            var left = Math.Clamp(X(bucket) - label.Width / 2, 0, Math.Max(0, Bounds.Width - label.Width));
            var top = Y(data.Buckets[bucket].Values[highlight].TotalMinutes) - label.Height - 7;
            context.DrawText(label, new Point(left, Math.Max(0, top)));
        }

        var peakLabel = Label(peak);
        Draw(peakLabel, peak);
        if (peak != last)
        {
            var lastLabel = Label(last);
            if (Math.Abs(X(peak) - X(last)) > (peakLabel.Width + lastLabel.Width) / 2 + 6) Draw(lastLabel, last);
        }
    }

    /// <summary>
    /// Draws a series' line; segments into a period that is cut off or still in progress are
    /// faded, so a half-finished week doesn't read as a drop.
    /// </summary>
    static void DrawLine(DrawingContext context, ChartData data, List<int> buckets, Point[] points, Pen solid, Pen faded)
    {
        var run = new List<Point> { points[0] };
        for (var n = 1; n < points.Length; n++)
        {
            if (data.Buckets[buckets[n - 1]].Incomplete || data.Buckets[buckets[n]].Incomplete)
            {
                if (run.Count >= 2) context.DrawGeometry(null, solid, Polyline(run));
                context.DrawLine(faded, points[n - 1], points[n]);
                run = [points[n]];
            }
            else
            {
                run.Add(points[n]);
            }
        }
        if (run.Count >= 2) context.DrawGeometry(null, solid, Polyline(run));
    }

    void DrawDot(DrawingContext context, Point centre, Color color) =>
        context.DrawEllipse(Brush(color), new Pen(Brush(Palette.Surface), 2), centre, 5, 5);

    int SeriesNear(Point point)
    {
        var best = -1;
        var bestDistance = HitDistance;
        for (var k = 0; k < _lines.Count; k++)
        {
            var points = _lines[k];
            for (var i = 0; i < points.Length; i++)
            {
                var distance = i == 0 ? Distance(point, points[0]) : DistanceToSegment(point, points[i - 1], points[i]);
                if (distance <= bestDistance)
                {
                    best = k;
                    bestDistance = distance;
                }
            }
        }
        return best;
    }

    // "Other" and the smaller activities first, so the biggest and the highlighted end up on top.
    static List<int> DrawOrder(ChartData data, int highlight)
    {
        var order = Enumerable.Range(0, data.Series.Count).Reverse().ToList();
        if (highlight >= 0)
        {
            order.Remove(highlight);
            order.Add(highlight);
        }
        return order;
    }

    static StreamGeometry Polyline(IReadOnlyList<Point> points)
    {
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(points[0], isFilled: false);
        for (var i = 1; i < points.Count; i++) context.LineTo(points[i]);
        context.EndFigure(isClosed: false);
        return geometry;
    }

    static double Distance(Point a, Point b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return Distance(p, a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return Distance(p, new Point(a.X + t * dx, a.Y + t * dy));
    }
}
