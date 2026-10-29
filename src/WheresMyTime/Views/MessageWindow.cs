using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace WheresMyTime.Views;

/// <summary>A small message or confirmation box. ShowDialog&lt;bool&gt; returns true for the confirm button.</summary>
public sealed class MessageWindow : Window
{
    public MessageWindow(string title, string message, string confirmText = "OK", string? cancelText = null)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        if (cancelText is not null)
        {
            var cancel = new Button { Content = cancelText, IsCancel = true };
            cancel.Click += (_, _) => Close(false);
            buttons.Children.Add(cancel);
        }
        var confirm = new Button { Content = confirmText, IsDefault = true, Classes = { "accent" } };
        confirm.Click += (_, _) => Close(true);
        buttons.Children.Add(confirm);

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.SemiBold },
                new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                buttons,
            },
        };
        Opened += (_, _) => confirm.Focus();
    }
}
