using Avalonia.Media;
using Avalonia.Styling;

namespace WheresMyTime.Views;

/// <summary>
/// Colours for the stats chart. The series order is the colour-blind-safety mechanism (adjacent
/// slots stay distinguishable), validated on the app's white and black window backgrounds,
/// so keep the order. Dark mode uses its own steps of the same hues.
/// </summary>
public sealed record ChartPalette(
    IReadOnlyList<Color> Series, Color Other,
    Color Deemphasis, Color DeemphasisLine, Color Surface,
    Color Text, Color SecondaryText, Color MutedText,
    Color Grid, Color Baseline, Color Hover,
    Color TooltipBackground, Color TooltipBorder)
{
    public static readonly ChartPalette Light = new(
        Series: Parse("#2a78d6", "#eb6834", "#1baf7a", "#eda100", "#e87ba4", "#008300", "#6250d6"),
        Other: Color.Parse("#898781"),
        Deemphasis: Color.Parse("#e4e3dd"),
        DeemphasisLine: Color.Parse("#c9c8c0"),
        Surface: Color.Parse("#ffffff"),
        Text: Color.Parse("#0b0b0b"),
        SecondaryText: Color.Parse("#52514e"),
        MutedText: Color.Parse("#898781"),
        Grid: Color.Parse("#e1e0d9"),
        Baseline: Color.Parse("#c3c2b7"),
        Hover: Color.Parse("#0d000000"),
        TooltipBackground: Color.Parse("#ffffff"),
        TooltipBorder: Color.Parse("#1a0b0b0b"));

    public static readonly ChartPalette Dark = new(
        Series: Parse("#3987e5", "#d95926", "#199e70", "#c98500", "#d55181", "#008300", "#9085e9"),
        Other: Color.Parse("#898781"),
        Deemphasis: Color.Parse("#2e2e2c"),
        DeemphasisLine: Color.Parse("#4a4a46"),
        Surface: Color.Parse("#000000"),
        Text: Color.Parse("#f0efec"),
        SecondaryText: Color.Parse("#c3c2b7"),
        MutedText: Color.Parse("#898781"),
        Grid: Color.Parse("#2c2c2a"),
        Baseline: Color.Parse("#4a4a46"),
        Hover: Color.Parse("#14ffffff"),
        TooltipBackground: Color.Parse("#1f1f1e"),
        TooltipBorder: Color.Parse("#26ffffff"));

    public static ChartPalette For(ThemeVariant? theme) => theme == ThemeVariant.Dark ? Dark : Light;

    public Color SeriesColor(int? slot) => slot is { } s && s < Series.Count ? Series[s] : Other;

    static Color[] Parse(params string[] hex) => hex.Select(Color.Parse).ToArray();
}
