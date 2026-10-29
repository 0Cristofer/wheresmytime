using WheresMyTime.Core;

namespace WheresMyTime.Tests;

public class CsvAndMergeTests
{
    static DateTime At(int day, int hour, int minute, int second = 0) => new(2026, 10, day, hour, minute, second);

    [Fact]
    public void Export_then_import_round_trips_tricky_text()
    {
        var entries = new List<TimeEntry>
        {
            new() { Start = At(28, 9, 0, 15), End = At(28, 10, 30, 45), Activity = "Meetings", Details = "Planning, \"Q4\"; budget" },
            new() { Start = At(28, 11, 0), End = At(28, 11, 20), Activity = "Reunião", Details = "line one\nline two" },
            new() { Start = At(28, 23, 30), End = At(29, 0, 45), Activity = "Deploy", Details = "" },
        };

        var csv = CsvFormat.Write(entries);
        Assert.StartsWith("start,end,duration_minutes,activity,details,id\r\n2026-10-28 09:00:15,2026-10-28 10:30:45,91,Meetings,", csv);

        var read = CsvFormat.Read(csv);
        Assert.Null(read.FatalError);
        Assert.Empty(read.Errors);
        Assert.Equal(3, read.Entries.Count);
        for (var i = 0; i < entries.Count; i++)
        {
            Assert.Equal(entries[i].Id, read.Entries[i].Id);
            Assert.Equal(entries[i].Start, read.Entries[i].Start);
            Assert.Equal(entries[i].End, read.Entries[i].End);
            Assert.Equal(entries[i].Activity, read.Entries[i].Activity);
            Assert.Equal(entries[i].Details, read.Entries[i].Details);
        }
    }

    [Fact]
    public void Imports_semicolons_date_column_and_other_header_names()
    {
        const string csv = "﻿Date;Start Time;End Time;Task;Description\r\n" +
                           "2026-10-28;9:00;10:15;Chat;answering support\r\n" +
                           "2026-10-28;23:00;00:30;On call;\r\n";
        var read = CsvFormat.Read(csv);
        Assert.Null(read.FatalError);
        Assert.Empty(read.Errors);
        Assert.Equal(2, read.Entries.Count);
        Assert.Equal(At(28, 9, 0), read.Entries[0].Start);
        Assert.Equal(At(28, 10, 15), read.Entries[0].End);
        Assert.Equal("Chat", read.Entries[0].Activity);
        Assert.Equal("answering support", read.Entries[0].Details);
        Assert.Equal(At(29, 0, 30), read.Entries[1].End);
    }

    [Fact]
    public void Imports_iso_timestamps()
    {
        var read = CsvFormat.Read("start,end,activity\n2026-10-28T09:00:00.123,2026-10-28T09:45:00,Review\n");
        Assert.Empty(read.Errors);
        Assert.Equal(At(28, 9, 0), read.Entries[0].Start);
        Assert.Equal(At(28, 9, 45), read.Entries[0].End);
    }

    [Fact]
    public void Reports_bad_rows_with_line_numbers_and_keeps_the_good_ones()
    {
        const string csv = "start,end,activity,details\n" +
                           "2026-10-28 09:00,2026-10-28 10:00,Ok,\"multi\nline\"\n" +
                           "yesterday,2026-10-28 10:00,Bad start,\n" +
                           "2026-10-28 11:00,2026-10-28 10:00,Backwards,\n" +
                           "\n" +
                           "2026-10-28 12:00,2026-10-28 13:00,  ,\n";
        var read = CsvFormat.Read(csv);
        Assert.Single(read.Entries);
        Assert.Equal(
            ["Line 4: can't read start \"yesterday\".", "Line 5: end is not after start.", "Line 7: activity is empty."],
            read.Errors);
    }

    [Fact]
    public void Missing_columns_is_a_fatal_error()
    {
        Assert.NotNull(CsvFormat.Read("when,what\n2026-10-28,x\n").FatalError);
        Assert.NotNull(CsvFormat.Read("").FatalError);
    }

    [Fact]
    public void Merge_skips_known_ids_and_identical_entries_and_reuses_activity_spelling()
    {
        var existing = new List<TimeEntry>
        {
            new() { Id = "a", Start = At(28, 9, 0), End = At(28, 10, 0), Activity = "Meetings" },
        };
        var incoming = new List<TimeEntry>
        {
            new() { Id = "a", Start = At(1, 1, 0), End = At(1, 2, 0), Activity = "Same id" },
            new() { Id = "b", Start = At(28, 9, 0), End = At(28, 10, 0), Activity = "meetings" },
            new() { Id = "c", Start = At(28, 11, 0), End = At(28, 12, 0), Activity = " MEETINGS " },
            new() { Id = "d", Start = At(28, 13, 0), End = At(28, 14, 0), Activity = "Chat" },
            new() { Id = "e", Start = At(28, 13, 0), End = At(28, 14, 0), Activity = "chat" },
        };

        var result = EntryMerge.Merge(existing, incoming);

        Assert.Equal(new MergeResult(2, 3), result);
        Assert.Equal(["a", "c", "d"], existing.Select(e => e.Id));
        Assert.Equal("Meetings", existing[1].Activity);

        // Importing the same thing again changes nothing.
        Assert.Equal(new MergeResult(0, 5), EntryMerge.Merge(existing, incoming));
    }

    [Fact]
    public void Activity_names_are_cleaned_and_matched_ignoring_case()
    {
        var entries = new List<TimeEntry>
        {
            new() { Start = At(27, 9, 0), Activity = "Project X", Details = "old" },
            new() { Start = At(28, 9, 0), Activity = "Chat", Details = "slack" },
            new() { Start = At(26, 9, 0), Activity = "chat", Details = "Slack" },
        };
        Assert.Equal("Project X", Activities.Normalize("  project   x ", entries));
        Assert.Equal("New thing", Activities.Normalize("New   thing", entries));
        Assert.Equal(["Chat", "Project X"], Activities.Suggestions(entries));
        Assert.Equal(["slack", "old"], Activities.DetailSuggestions(entries));
    }
}
