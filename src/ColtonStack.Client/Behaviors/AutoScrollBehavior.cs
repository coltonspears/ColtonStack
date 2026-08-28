using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace ColtonStack.Client.Behaviors;

/// <summary>
/// Attached behavior: keeps a ListBox scrolled to its newest item. "Scroll on new message" is a
/// view-only concern, so it lives here as one reusable line of XAML — not in code-behind and
/// not as an event a view model would have to raise.
/// </summary>
public static class AutoScrollBehavior
{
    public static readonly DependencyProperty ScrollToNewItemProperty = DependencyProperty.RegisterAttached(
        "ScrollToNewItem",
        typeof(bool),
        typeof(AutoScrollBehavior),
        new PropertyMetadata(false, OnScrollToNewItemChanged));

    public static bool GetScrollToNewItem(DependencyObject element) => (bool)element.GetValue(ScrollToNewItemProperty);

    public static void SetScrollToNewItem(DependencyObject element, bool value) => element.SetValue(ScrollToNewItemProperty, value);

    private static void OnScrollToNewItemChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        // Designed to be set once (to true) in XAML; toggling off at runtime isn't supported.
        if (element is not ListBox listBox || e.NewValue is not true)
        {
            return;
        }

        // ItemCollection forwards the ItemsSource's collection changes, so this single
        // subscription survives ItemsSource swaps for the lifetime of the ListBox.
        ((INotifyCollectionChanged)listBox.Items).CollectionChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Add && listBox.Items.Count > 0)
            {
                listBox.ScrollIntoView(listBox.Items[^1]);
            }
        };
    }
}
