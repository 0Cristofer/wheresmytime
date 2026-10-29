using Avalonia.Controls;
using Avalonia.Interactivity;
using WheresMyTime.ViewModels;

namespace WheresMyTime.Views;

public partial class EntryDialog : Window
{
    public EntryDialog() => InitializeComponent();

    EntryDialogViewModel? ViewModel => DataContext as EntryDialogViewModel;

    // With the activity already filled in, Enter saves straight away.
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (ViewModel is { CanSave: true }) SaveButton.Focus();
        else ActivityBox.Focus();
    }

    void OnSave(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { CanSave: true }) Close(EntryDialogResult.Save);
    }

    void OnCancel(object? sender, RoutedEventArgs e) => Close(EntryDialogResult.Cancel);

    void OnDiscard(object? sender, RoutedEventArgs e) => Close(EntryDialogResult.Discard);
}
