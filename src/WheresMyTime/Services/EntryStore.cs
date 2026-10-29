using System.Text.Json;
using WheresMyTime.Core;

namespace WheresMyTime.Services;

public sealed class DataLockedException(string directory)
    : Exception($"Where's my time is already open, using the data in:\n{directory}")
{
}

public sealed class DataFileException(string path, Exception inner)
    : Exception($"Couldn't read the data file:\n{path}\n\n{inner.Message}\n\nThe file was not changed. Fix or move it, then start the app again.", inner)
{
}

/// <summary>
/// Owns the JSON data file. Every change is written straight away (via a temp file, keeping the
/// previous version as .bak), and a lock file stops two copies of the app overwriting each other.
/// </summary>
public sealed class EntryStore : IDisposable
{
    public const string DataFileName = "wheresmytime-data.json";
    public const string DataDirectoryVariable = "WHERESMYTIME_DATA_DIR";
    const string LockFileName = "wheresmytime.lock";

    readonly FileStream _lock;
    readonly DataFile _data;

    EntryStore(string directory, FileStream lockStream, DataFile data)
    {
        DataDirectory = directory;
        _lock = lockStream;
        _data = data;
    }

    public string DataDirectory { get; }
    public string FilePath => Path.Combine(DataDirectory, DataFileName);
    public List<TimeEntry> Entries => _data.Entries;

    public RunningTimer? Running
    {
        get => _data.Running;
        set => _data.Running = value;
    }

    /// <summary>Raised after every successful save.</summary>
    public event Action? Saved;

    /// <summary>
    /// Opens the data in <paramref name="directory"/>, or by default next to the executable
    /// (falling back to the user's app-data folder when that isn't writable).
    /// </summary>
    public static EntryStore Open(string? directory = null)
    {
        directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory ?? ResolveDirectory()));
        Directory.CreateDirectory(directory);

        FileStream lockStream;
        try
        {
            lockStream = new FileStream(Path.Combine(directory, LockFileName), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            throw new DataLockedException(directory);
        }

        try
        {
            return new EntryStore(directory, lockStream, Load(Path.Combine(directory, DataFileName)));
        }
        catch
        {
            lockStream.Dispose();
            throw;
        }
    }

    public void Save()
    {
        _data.Entries.Sort((a, b) => a.Start.CompareTo(b.Start));
        var temp = FilePath + ".tmp";
        using (var stream = File.Create(temp))
        {
            JsonSerializer.Serialize(stream, _data, DataJsonContext.Default.DataFile);
            stream.Flush(flushToDisk: true);
        }

        if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
        else File.Move(temp, FilePath);
        Saved?.Invoke();
    }

    /// <summary>Copies the data file aside so an unwanted import can be undone by hand.</summary>
    public void BackupBeforeImport()
    {
        if (!File.Exists(FilePath)) return;
        var name = $"wheresmytime-data.before-import-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        File.Copy(FilePath, Path.Combine(DataDirectory, name), overwrite: true);
    }

    public void Dispose() => _lock.Dispose();

    static DataFile Load(string path)
    {
        if (!File.Exists(path)) return new DataFile();
        try
        {
            using var stream = File.OpenRead(path);
            var data = JsonSerializer.Deserialize(stream, DataJsonContext.Default.DataFile) ?? new DataFile();
            data.Entries ??= [];
            return data;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            throw new DataFileException(path, ex);
        }
    }

    static string ResolveDirectory()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(DataDirectoryVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) return fromEnvironment;

        var exeDirectory = AppContext.BaseDirectory;
        if (IsWritable(exeDirectory)) return exeDirectory;

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WheresMyTime");
    }

    static bool IsWritable(string directory)
    {
        try
        {
            var probe = Path.Combine(directory, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "");
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
