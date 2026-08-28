using System.Windows;

namespace ColtonStack.Client.Views;

/// <summary>
/// No code-behind logic at all: Enter-to-send is a KeyBinding, auto-scroll is an attached
/// behavior, and the composition root (App) assigns the DataContext and kicks off the initial
/// load. Everything testable lives in the view models.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
