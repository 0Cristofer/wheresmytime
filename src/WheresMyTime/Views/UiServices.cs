using System.Text;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using WheresMyTime.Services;
using WheresMyTime.ViewModels;

namespace WheresMyTime.Views;

public sealed class UiServices(Window owner) : IUiServices
{
    static readonly FilePickerFileType CsvFiles = new("CSV files") { Patterns = ["*.csv"], MimeTypes = ["text/csv"] };

    StatsWindow? _stats;

    public Task<EntryDialogResult> ShowEntryDialogAsync(EntryDialogViewModel dialog) =>
        new EntryDialog { DataContext = dialog }.ShowDialog<EntryDialogResult>(owner);

    public Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false) =>
        new MessageWindow(title, message, confirmText, "Cancel", destructive).ShowDialog<bool>(owner);

    public Task ShowMessageAsync(string title, string message) =>
        new MessageWindow(title, message).ShowDialog(owner);

    public async Task SetClipboardTextAsync(string text)
    {
        if (owner.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
    }

    public async Task<bool> SaveTextFileAsync(string title, string suggestedName, string content)
    {
        var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = "csv",
            FileTypeChoices = [CsvFiles],
            ShowOverwritePrompt = true,
        });
        if (file is null) return false;

        await using var stream = await file.OpenWriteAsync();
        if (stream.CanSeek) stream.SetLength(0);
        // The BOM makes Excel read accented characters correctly.
        await using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        await writer.WriteAsync(content);
        return true;
    }

    public async Task<string?> OpenTextFileAsync(string title)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = [CsvFiles, FilePickerFileTypes.All],
        });
        if (files.Count == 0) return null;

        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    public async Task OpenFolderAsync(string path) =>
        await owner.Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(path));

    public void ShowStats(Func<StatsViewModel> create)
    {
        if (_stats is not null)
        {
            _stats.Activate();
            return;
        }
        _stats = new StatsWindow { DataContext = create() };
        _stats.Closed += (_, _) => _stats = null;
        _stats.Show();
    }
}
