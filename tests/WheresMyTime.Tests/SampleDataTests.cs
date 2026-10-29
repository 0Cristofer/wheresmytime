using System.Runtime.CompilerServices;
using WheresMyTime.Core;

namespace WheresMyTime.Tests;

public class SampleDataTests
{
    static string RepoRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));

    [Fact]
    public void Sample_csv_imports_cleanly()
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot(), "samples", "sample-entries.csv"));

        var read = CsvFormat.Read(text);

        Assert.Null(read.FatalError);
        Assert.Empty(read.Errors);
        Assert.Equal(377, read.Entries.Count);
        Assert.Equal(10, read.Entries.Select(e => e.Activity).Distinct().Count());
        Assert.Contains(read.Entries, e => e.End.Date > e.Start.Date);
        Assert.Contains(read.Entries, e => e.Details == "Q4 \"stretch\" goals, first draft");
        Assert.Contains(read.Entries, e => e.Details == "Reunião de alinhamento");

        var merged = EntryMerge.Merge([], read.Entries);
        Assert.Equal(new MergeResult(377, 0), merged);
    }
}
