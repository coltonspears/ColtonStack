using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColtonStack.Client.Behaviors;

/// <summary>
/// Attached behavior for composer toolbar buttons: point a button at a TextBox with
/// <c>Target</c>, then either <c>Wrap</c> the selection in a formatting marker (*bold*,
/// _italic_, ~strike~, `code`) or <c>Insert</c> text (emoji) at the caret. Selection and
/// caret handling are inherently view concerns, so they live here — not in code-behind and
/// not in a view model.
/// </summary>
public static class ComposerActions
{
    // Inherits: set Target once on the composer's root element and every toolbar button —
    // including the ones stamped out inside the emoji popup's DataTemplate — picks it up.
    public static readonly DependencyProperty TargetProperty = DependencyProperty.RegisterAttached(
        "Target", typeof(TextBox), typeof(ComposerActions),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, OnTargetChanged));

    public static readonly DependencyProperty WrapProperty = DependencyProperty.RegisterAttached(
        "Wrap", typeof(string), typeof(ComposerActions), new PropertyMetadata(null));

    public static readonly DependencyProperty InsertProperty = DependencyProperty.RegisterAttached(
        "Insert", typeof(string), typeof(ComposerActions), new PropertyMetadata(null));

    public static TextBox? GetTarget(DependencyObject element) => (TextBox?)element.GetValue(TargetProperty);

    public static void SetTarget(DependencyObject element, TextBox? value) => element.SetValue(TargetProperty, value);

    public static string? GetWrap(DependencyObject element) => (string?)element.GetValue(WrapProperty);

    public static void SetWrap(DependencyObject element, string? value) => element.SetValue(WrapProperty, value);

    public static string? GetInsert(DependencyObject element) => (string?)element.GetValue(InsertProperty);

    public static void SetInsert(DependencyObject element, string? value) => element.SetValue(InsertProperty, value);

    private static void OnTargetChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
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
        var button = (ButtonBase)sender;
        if (GetTarget(button) is not { } box)
        {
            return;
        }

        if (GetWrap(button) is { Length: > 0 } marker)
        {
            WrapSelection(box, marker);
        }
        else if (GetInsert(button) is { Length: > 0 } text)
        {
            InsertAtCaret(box, text);
        }
    }

    private static void WrapSelection(TextBox box, string marker)
    {
        var start = box.SelectionStart;
        var selection = box.SelectedText;

        box.SelectedText = marker + selection + marker;
        box.SelectionLength = 0;

        // Empty selection: park the caret between the markers, ready to type.
        box.CaretIndex = selection.Length == 0
            ? start + marker.Length
            : start + selection.Length + (marker.Length * 2);
        _ = box.Focus();
    }

    private static void InsertAtCaret(TextBox box, string text)
    {
        box.SelectedText = text;
        box.CaretIndex = box.SelectionStart + text.Length;
        box.SelectionLength = 0;
        _ = box.Focus();
    }
}
