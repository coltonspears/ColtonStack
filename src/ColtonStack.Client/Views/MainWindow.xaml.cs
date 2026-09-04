using System.Windows;

namespace ColtonStack.Client.Views;

/// <summary>
/// Pure XAML shell. The composition root sets DataContext; every behavior lives in bindings,
/// styles, attached behaviors and view models — nothing here but the required constructor.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
