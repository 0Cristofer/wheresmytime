using System.Text.Json.Serialization;

namespace WheresMyTime.Core;

/// <summary>
/// One block of recorded time. Start and End are local wall-clock times without an offset,
/// so "09:00" stays "09:00" when the data moves to another machine.
/// </summary>
public sealed class TimeEntry
{
    public string Id { get; set; } = NewId();
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public string Activity { get; set; } = "";
    public string Details { get; set; } = "";

    [JsonIgnore]
    public TimeSpan Duration => End - Start;

    public static string NewId() => Guid.NewGuid().ToString("N");
}

/// <summary>The timer that is currently running, persisted so it survives closing the app.</summary>
public sealed class RunningTimer
{
    public DateTime Start { get; set; }
    public string Activity { get; set; } = "";
    public string Details { get; set; } = "";
}

public sealed class DataFile
{
    public int Version { get; set; } = 1;
    public RunningTimer? Running { get; set; }
    public List<TimeEntry> Entries { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DataFile))]
internal sealed partial class DataJsonContext : JsonSerializerContext
{
}
