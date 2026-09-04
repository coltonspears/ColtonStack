using System.Windows;
using System.Windows.Input;

namespace ColtonStack.Client.Behaviors;

/// <summary>
/// Attached behavior that makes the WPF <see cref="SystemCommands"/> (minimize, maximize,
/// restore, close) work on a window with custom chrome. Set <c>WindowCaptionCommands.IsEnabled="True"</c>
/// on the Window and bind caption buttons to <c>{x:Static SystemCommands.CloseWindowCommand}</c>
/// etc. Window-state plumbing is a view-only concern, so it lives here — not in code-behind and
/// nowhere near a view model.
/// </summary>
public static class WindowCaptionCommands
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(WindowCaptionCommands), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(Window window) => (bool)window.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(Window window, bool value) => window.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Window window || e.NewValue is not true)
        {
            return;
        }

        window.CommandBindings.Add(new CommandBinding(SystemCommands.CloseWindowCommand, (_, _) => SystemCommands.CloseWindow(window)));
        window.CommandBindings.Add(new CommandBinding(SystemCommands.MinimizeWindowCommand, (_, _) => SystemCommands.MinimizeWindow(window)));
        window.CommandBindings.Add(new CommandBinding(
            SystemCommands.MaximizeWindowCommand,
            (_, _) => SystemCommands.MaximizeWindow(window),
            (_, args) => args.CanExecute = window.WindowState != WindowState.Maximized));
        window.CommandBindings.Add(new CommandBinding(
            SystemCommands.RestoreWindowCommand,
            (_, _) => SystemCommands.RestoreWindow(window),
            (_, args) => args.CanExecute = window.WindowState == WindowState.Maximized));
    }
}
