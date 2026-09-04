using System.Windows;
using System.Windows.Controls.Primitives;

namespace ColtonStack.Client.Behaviors;

/// <summary>
/// Attached behavior: a button with <c>ClipboardActions.CopyText="{Binding Text}"</c> copies that
/// text when clicked. The clipboard is a view-side resource; no view model touches it.
/// </summary>
public static class ClipboardActions
{
    public static readonly DependencyProperty CopyTextProperty = DependencyProperty.RegisterAttached(
        "CopyText", typeof(string), typeof(ClipboardActions), new PropertyMetadata(null, OnCopyTextChanged));

    public static string? GetCopyText(DependencyObject element) => (string?)element.GetValue(CopyTextProperty);

    public static void SetCopyText(DependencyObject element, string? value) => element.SetValue(CopyTextProperty, value);

    private static void OnCopyTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not ButtonBase button)
        {
            return;
        }

        button.Click -= OnClick;
        if (e.NewValue is not null)
        {
            button.Click += OnClick;
        }
    }

    private static void OnClick(object sender, RoutedEventArgs e)
    {
        if (GetCopyText((ButtonBase)sender) is { Length: > 0 } text)
        {
            Clipboard.SetText(text);
        }
    }
}
