using System.Runtime.CompilerServices;
using WheresMyTime.Core;

namespace WheresMyTime.Tests;

public class SampleDataTests
{
    static string RepoRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    static CsvReadResult ReadSample(string name)
    {
        var read = CsvFormat.Read(File.ReadAllText(Path.Combine(RepoRoot(), "samples", name)));
        Assert.Null(read.FatalError);
        Assert.Empty(read.Errors);
        Assert.Equal(new MergeResult(read.Entries.Count, 0), EntryMerge.Merge([], read.Entries));
        return read;
    }

    [Fact]
    public void Twelve_week_sample_imports_cleanly()
    {
        var read = ReadSample("sample-entries.csv");

        Assert.Equal(377, read.Entries.Count);
        Assert.Equal(10, read.Entries.Select(e => e.Activity).Distinct().Count());
        Assert.Contains(read.Entries, e => e.End.Date > e.Start.Date);
        Assert.Contains(read.Entries, e => e.Details == "Q4 \"stretch\" goals, first draft");
        Assert.Contains(read.Entries, e => e.Details == "Reunião de alinhamento");
    }

    [Fact]
    public void Few_days_sample_imports_cleanly()
    {
        var read = ReadSample("sample-few-days.csv");

        Assert.Equal(34, read.Entries.Count);
        Assert.Equal(
            [new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 23), new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 28)],
            read.Entries.Select(e => DateOnly.FromDateTime(e.Start)).Distinct());
        Assert.Contains(read.Entries, e => e.End.Date > e.Start.Date);
        Assert.Contains(read.Entries, e => e.Details == "Reviewing \"auth\" changes, part 2");
        Assert.Contains(read.Entries, e => e.Details == "Reunião de alinhamento");
    }
}
