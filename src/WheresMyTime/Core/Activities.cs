namespace WheresMyTime.Core;

/// <summary>Activity names are matched case-insensitively so "meetings" and "Meetings" report together.</summary>
public static class Activities
{
    /// <summary>Trims and collapses inner whitespace.</summary>
    public static string Clean(string? text) =>
        string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Cleans the name and reuses the spelling of an existing activity that differs only by case.</summary>
    public static string Normalize(string? activity, IEnumerable<TimeEntry> existing)
    {
        var clean = Clean(activity);
        if (clean.Length == 0) return clean;
        return existing
            .Select(e => e.Activity)
            .FirstOrDefault(a => string.Equals(a, clean, StringComparison.OrdinalIgnoreCase)) ?? clean;
    }

    /// <summary>Distinct activity names, most recently used first.</summary>
    public static List<string> Suggestions(IEnumerable<TimeEntry> entries) =>
        entries
            .Where(e => e.Activity.Length > 0)
            .GroupBy(e => e.Activity, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.MaxBy(e => e.Start)!)
            .OrderByDescending(e => e.Start)
            .Select(e => e.Activity)
            .ToList();

    /// <summary>Distinct details texts, most recently used first.</summary>
    public static List<string> DetailSuggestions(IEnumerable<TimeEntry> entries, int limit = 500) =>
        entries
            .Where(e => !string.IsNullOrWhiteSpace(e.Details))
            .GroupBy(e => e.Details.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.MaxBy(e => e.Start)!)
            .OrderByDescending(e => e.Start)
            .Select(e => e.Details.Trim())
            .Take(limit)
            .ToList();
}
