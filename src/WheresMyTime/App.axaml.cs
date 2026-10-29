using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WheresMyTime.Services;
using WheresMyTime.ViewModels;
using WheresMyTime.Views;

namespace WheresMyTime;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            try
            {
                var store = EntryStore.Open();
                var window = new MainWindow();
                window.DataContext = new MainViewModel(store, new UiServices(window));
                desktop.MainWindow = window;
                desktop.Exit += (_, _) => store.Dispose();
            }
            catch (Exception ex)
            {
                desktop.MainWindow = new MessageWindow("Where's my time", ex.Message)
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ShowInTaskbar = true,
                };
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
