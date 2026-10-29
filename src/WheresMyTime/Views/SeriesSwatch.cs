using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace WheresMyTime.Views;

/// <summary>The coloured square beside a legend entry; matches the chart's palette and theme.</summary>
public sealed class SeriesSwatch : Control
{
    public static readonly StyledProperty<int?> SlotProperty =
        AvaloniaProperty.Register<SeriesSwatch, int?>(nameof(Slot));

    static SeriesSwatch() => AffectsRender<SeriesSwatch>(SlotProperty);

    public SeriesSwatch() => ActualThemeVariantChanged += (_, _) => InvalidateVisual();

    public int? Slot
    {
        get => GetValue(SlotProperty);
        set => SetValue(SlotProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var color = ChartPalette.For(this).SeriesColor(Slot);
        context.DrawRectangle(new SolidColorBrush(color), null, new RoundedRect(new Rect(Bounds.Size), 2));
    }
}
