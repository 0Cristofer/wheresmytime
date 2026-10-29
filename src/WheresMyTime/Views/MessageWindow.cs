using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace WheresMyTime.Views;

/// <summary>
/// A small Material-style message or confirmation dialog. ShowDialog&lt;bool&gt; returns true for
/// the confirm button; a destructive confirm is shown in the error colour.
/// </summary>
public sealed class MessageWindow : Window
{
    public MessageWindow(string title, string message, string confirmText = "OK", string? cancelText = null, bool destructive = false)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Classes.Add("dialog");

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        if (cancelText is not null)
        {
            var cancel = new Button { Content = cancelText, IsCancel = true, Classes = { "text" } };
            cancel.Click += (_, _) => Close(false);
            buttons.Children.Add(cancel);
        }
        var confirm = new Button { Content = confirmText, IsDefault = true, Classes = { "text" } };
        if (destructive) confirm.Classes.Add("danger");
        confirm.Click += (_, _) => Close(true);
        buttons.Children.Add(confirm);

        Content = new StackPanel
        {
            Margin = new Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = title, Classes = { "headline-small" } },
                new SelectableTextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Classes = { "muted" } },
                buttons,
            },
        };
        Opened += (_, _) => confirm.Focus();
    }
}
