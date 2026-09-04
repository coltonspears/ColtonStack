using System.Windows;
using System.Windows.Threading;

namespace ColtonStack.Client.Behaviors;

/// <summary>
/// Attached behavior: moves keyboard focus to an element whenever a bound flag turns true —
/// e.g. the palette's text box when <c>Palette.IsOpen</c> flips. Focus is a view concern, so the
/// view model only flips a bool.
/// </summary>
public static class FocusBehavior
{
    public static readonly DependencyProperty WhenTrueProperty = DependencyProperty.RegisterAttached(
        "WhenTrue", typeof(bool), typeof(FocusBehavior), new PropertyMetadata(false, OnWhenTrueChanged));

    public static bool GetWhenTrue(DependencyObject element) => (bool)element.GetValue(WhenTrueProperty);

    public static void SetWhenTrue(DependencyObject element, bool value) => element.SetValue(WhenTrueProperty, value);

    private static void OnWhenTrueChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not UIElement target || e.NewValue is not true)
        {
            return;
        }

        // The element may be inside a Popup that is still opening; defer one layout pass.
        _ = target.Dispatcher.BeginInvoke(DispatcherPriority.Input, () => _ = target.Focus());
    }
}
