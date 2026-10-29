using Avalonia.Controls;

namespace WheresMyTime.Views;

public partial class StatsWindow : Window
{
    public StatsWindow()
    {
        InitializeComponent();
        Closed += (_, _) => (DataContext as IDisposable)?.Dispose();
    }
}
