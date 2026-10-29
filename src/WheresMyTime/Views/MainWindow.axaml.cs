using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using WheresMyTime.ViewModels;

namespace WheresMyTime.Views;

public partial class MainWindow : Window
{
    readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MainWindow()
    {
        InitializeComponent();
        _clock.Tick += (_, _) => ViewModel?.Tick();
        _clock.Start();
        ActivityBox.KeyDown += OnInputKeyDown;
        DetailsBox.KeyDown += OnInputKeyDown;
    }

    MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        ViewModel?.SaveOnClose();
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _clock.Stop();
        base.OnClosed(e);
    }

    // Enter in the activity/details fields starts the timer (but never stops it).
    void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is AutoCompleteBox { IsDropDownOpen: true }) return;
        if (ViewModel is { IsRunning: false } vm)
        {
            vm.StartStopCommand.Execute(null);
            e.Handled = true;
        }
    }

    void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is not null) return;
        if (sender is Control { DataContext: EntryRow row } && ViewModel is { } vm)
            vm.EditEntryCommand.Execute(row);
    }
}
