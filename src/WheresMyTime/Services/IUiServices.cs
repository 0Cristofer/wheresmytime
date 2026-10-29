using WheresMyTime.ViewModels;

namespace WheresMyTime.Services;

/// <summary>Windows, pickers and the clipboard, kept out of the view models.</summary>
public interface IUiServices
{
    Task<EntryDialogResult> ShowEntryDialogAsync(EntryDialogViewModel dialog);
    Task<bool> ConfirmAsync(string title, string message, string confirmText, bool destructive = false);
    Task ShowMessageAsync(string title, string message);
    Task SetClipboardTextAsync(string text);

    /// <summary>Asks where to save and writes the text as UTF-8; false if the user cancelled.</summary>
    Task<bool> SaveTextFileAsync(string title, string suggestedName, string content);

    /// <summary>Asks for a file and returns its text, or null if the user cancelled.</summary>
    Task<string?> OpenTextFileAsync(string title);

    Task OpenFolderAsync(string path);
    void ShowStats(Func<StatsViewModel> create);
}
