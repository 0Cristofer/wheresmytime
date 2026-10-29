namespace WheresMyTime.Core;

public sealed record MergeResult(int Added, int Duplicates);

public static class EntryMerge
{
    /// <summary>
    /// Adds imported entries to <paramref name="existing"/>, skipping any whose id is already present
    /// or that have the same start, end and activity as an existing entry. Importing the same file
    /// twice therefore changes nothing.
    /// </summary>
    public static MergeResult Merge(List<TimeEntry> existing, IEnumerable<TimeEntry> incoming)
    {
        var ids = existing.Select(e => e.Id).ToHashSet();
        var keys = existing.Select(Key).ToHashSet();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in existing) names.TryAdd(e.Activity, e.Activity);

        int added = 0, duplicates = 0;
        foreach (var entry in incoming)
        {
            if (ids.Contains(entry.Id) || keys.Contains(Key(entry)))
            {
                duplicates++;
                continue;
            }

            entry.Activity = Activities.Clean(entry.Activity);
            if (names.TryGetValue(entry.Activity, out var spelling)) entry.Activity = spelling;
            else names[entry.Activity] = entry.Activity;

            existing.Add(entry);
            ids.Add(entry.Id);
            keys.Add(Key(entry));
            added++;
        }
        return new MergeResult(added, duplicates);
    }

    static string Key(TimeEntry e) =>
        $"{e.Start:yyyy-MM-ddTHH:mm:ss}|{e.End:yyyy-MM-ddTHH:mm:ss}|{Activities.Clean(e.Activity).ToUpperInvariant()}";
}
