using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace WheresMyTime.Views;

/// <summary>
/// Colours for the stats charts. The series colours are fixed: their order is the
/// colour-blind-safety mechanism (adjacent slots stay distinguishable), validated on the app's
/// light and dark surfaces, so keep it. Dark mode uses its own steps of the same hues.
/// Everything else (ink, grid, surfaces) comes from the app's Material colour roles.
/// </summary>
public sealed record ChartPalette(
    IReadOnlyList<Color> Series, Color Other,
    Color Deemphasis, Color DeemphasisLine, Color Surface,
    Color Text, Color SecondaryText, Color MutedText,
    Color Grid, Color Baseline, Color Hover,
    Color TooltipBackground, Color TooltipBorder)
{
    static readonly Color[] LightSeries = Parse("#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6");
    static readonly Color[] DarkSeries = Parse("#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9");

    public static ChartPalette For(StyledElement element)
    {
        var theme = element.ActualThemeVariant;
        Color Role(string key) =>
            element.TryFindResource(key, theme, out var value) && value is ISolidColorBrush brush ? brush.Color : Colors.Gray;

        var onSurface = Role("OnSurface");
        return new ChartPalette(
            Series: theme == ThemeVariant.Dark ? DarkSeries : LightSeries,
            Other: Role("Outline"),
            Deemphasis: Role("SurfaceContainerHighest"),
            DeemphasisLine: Role("OutlineVariant"),
            Surface: Role("Surface"),
            Text: onSurface,
            SecondaryText: Role("OnSurfaceVariant"),
            MutedText: Role("Outline"),
            Grid: Role("SurfaceContainerHighest"),
            Baseline: Role("OutlineVariant"),
            Hover: Color.FromArgb(0x14, onSurface.R, onSurface.G, onSurface.B),
            TooltipBackground: Role("SurfaceContainer"),
            TooltipBorder: Colors.Transparent);
    }

    public Color SeriesColor(int? slot) => slot is { } s && s < Series.Count ? Series[s] : Other;

    static Color[] Parse(params string[] hex) => hex.Select(Color.Parse).ToArray();
}
