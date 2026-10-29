using System.Globalization;
using System.Text;

namespace WheresMyTime.Core;

public sealed record CsvReadResult(List<TimeEntry> Entries, List<string> Errors, string? FatalError = null);

/// <summary>
/// The CSV used to export and import entries: one row per entry, local wall-clock times.
/// Import is lenient: ',' ';' or tab separators, a separate date column with time-only
/// start/end, ISO 8601 timestamps, and a few common alternative header names.
/// </summary>
public static class CsvFormat
{
    public static readonly string[] Columns = ["start", "end", "duration_minutes", "activity", "details", "id"];

    const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[] DateTimeFormats =
    [
        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm:ss", "yyyy-MM-dd H:mm",
        "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm",
    ];
    static readonly string[] ClockFormats = ["H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss"];

    public static string Write(IEnumerable<TimeEntry> entries)
    {
        var sb = new StringBuilder();
        AppendRow(sb, Columns);
        foreach (var e in entries.OrderBy(e => e.Start))
        {
            AppendRow(sb,
            [
                e.Start.ToString(DateTimeFormat, Inv),
                e.End.ToString(DateTimeFormat, Inv),
                TimeUtil.RoundedMinutes(e.Duration).ToString(Inv),
                e.Activity,
                e.Details,
                e.Id,
            ]);
        }
        return sb.ToString();
    }

    static void AppendRow(StringBuilder sb, IReadOnlyList<string> fields)
    {
        for (var i = 0; i < fields.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var field = fields[i];
            if (field.AsSpan().IndexOfAny(",;\t\"\r\n") >= 0)
                sb.Append('"').Append(field.Replace("\"", "\"\"")).Append('"');
            else
                sb.Append(field);
        }
        sb.Append("\r\n");
    }

    public static CsvReadResult Read(string text)
    {
        var entries = new List<TimeEntry>();
        var errors = new List<string>();
        var records = ParseRecords(text, DetectDelimiter(text));
        if (records.Count == 0 || records[0].Fields.All(string.IsNullOrWhiteSpace))
            return new(entries, errors, "The file is empty.");

        var header = records[0].Fields.Select(h => h.Trim().TrimStart('﻿').ToLowerInvariant().Replace(' ', '_')).ToArray();
        int Column(params string[] names) => Array.FindIndex(header, names.Contains);

        var startCol = Column("start", "start_time", "starttime", "begin", "from");
        var endCol = Column("end", "end_time", "endtime", "stop", "finish", "to");
        var dateCol = Column("date", "day");
        var activityCol = Column("activity", "task", "project", "category", "topic", "name");
        var detailsCol = Column("details", "detail", "description", "notes", "note", "what");
        var idCol = Column("id");
        if (startCol < 0 || endCol < 0 || activityCol < 0)
        {
            return new(entries, errors,
                "Couldn't find start, end and activity columns. The first row must be a header like:\n" +
                string.Join(',', Columns));
        }

        foreach (var (line, fields) in records.Skip(1))
        {
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            string Field(int col) => col >= 0 && col < fields.Length ? fields[col].Trim() : "";

            var date = Field(dateCol);
            if (!TryParseTime(Field(startCol), date, out var start, out _))
            {
                errors.Add($"Line {line}: can't read start \"{Field(startCol)}\".");
                continue;
            }
            if (!TryParseTime(Field(endCol), date, out var end, out var endIsClockOnly))
            {
                errors.Add($"Line {line}: can't read end \"{Field(endCol)}\".");
                continue;
            }
            if (end <= start && endIsClockOnly) end = end.AddDays(1);
            if (end <= start)
            {
                errors.Add($"Line {line}: end is not after start.");
                continue;
            }

            var activity = Activities.Clean(Field(activityCol));
            if (activity.Length == 0)
            {
                errors.Add($"Line {line}: activity is empty.");
                continue;
            }

            var id = Field(idCol);
            entries.Add(new TimeEntry
            {
                Id = id.Length > 0 ? id : TimeEntry.NewId(),
                Start = start,
                End = end,
                Activity = activity,
                Details = Field(detailsCol),
            });
        }

        return new(entries, errors);
    }

    static bool TryParseTime(string value, string date, out DateTime result, out bool clockOnly)
    {
        clockOnly = false;
        if (DateTime.TryParseExact(value, DateTimeFormats, Inv, DateTimeStyles.None, out result))
        {
            result = TimeUtil.TruncateToSeconds(result);
            return true;
        }

        // Other ISO 8601 forms, e.g. fractional seconds or an offset (converted to local time).
        if (value.Length > 10 && value[4] == '-' && value[7] == '-' &&
            DateTimeOffset.TryParse(value, Inv, DateTimeStyles.AssumeLocal, out var withOffset))
        {
            result = TimeUtil.TruncateToSeconds(withOffset.LocalDateTime);
            return true;
        }

        if (DateOnly.TryParseExact(date, "yyyy-MM-dd", Inv, DateTimeStyles.None, out var day) &&
            TimeOnly.TryParseExact(value, ClockFormats, Inv, DateTimeStyles.None, out var clock))
        {
            result = day.ToDateTime(clock);
            clockOnly = true;
            return true;
        }

        result = default;
        return false;
    }

    /// <summary>Picks whichever of ',' ';' or tab appears most in the header line.</summary>
    internal static char DetectDelimiter(string text)
    {
        var end = text.AsSpan().IndexOfAny('\r', '\n');
        var header = end < 0 ? text : text[..end];
        return new[] { ',', ';', '\t' }.MaxBy(d => header.Count(c => c == d));
    }

    /// <summary>RFC 4180 parsing; quoted fields may contain separators, quotes and line breaks.</summary>
    internal static List<(int Line, string[] Fields)> ParseRecords(string text, char delimiter)
    {
        var records = new List<(int, string[])>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;

        for (var i = text.StartsWith('﻿') ? 1 : 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c != '"')
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }
                else if (i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = false;
                }
            }
            else if (c == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                fields.Add(field.ToString());
                field.Clear();
                records.Add((recordLine, fields.ToArray()));
                fields.Clear();
                line++;
                recordLine = line;
            }
            else
            {
                field.Append(c);
            }
        }

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add((recordLine, fields.ToArray()));
        }
        return records;
    }
}
