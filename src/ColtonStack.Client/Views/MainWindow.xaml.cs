using System.Windows;
using System.Windows.Input;
using ColtonStack.Client.ViewModels;

namespace ColtonStack.Client.Views;

/// <summary>
/// Deliberately thin: layout, keyboard handling, and auto-scroll only. Everything else lives
/// in the view models — this file has no state and no logic worth unit testing.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.Chat.MessageArrived += (_, _) => ScrollToLatestMessage();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) =>
        _ = _viewModel.InitializeAsync();

    private void ComposerBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Enter sends; Shift+Enter stays in the box and makes a new line.
        if (e.Key != Key.Enter || (Keyboard.Modifiers & ModifierKeys.Shift) != ModifierKeys.None)
        {
            return;
        }

        e.Handled = true;
        if (_viewModel.Chat.SendMessageCommand.CanExecute(null))
        {
            _viewModel.Chat.SendMessageCommand.Execute(null);
        }
    }

    private void ScrollToLatestMessage()
    {
        if (MessagesList.Items.Count > 0)
        {
            MessagesList.ScrollIntoView(MessagesList.Items[^1]);
        }
    }
}
