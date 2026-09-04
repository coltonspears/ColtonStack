using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// Live text filter over the conversation, composed into <see cref="ChatViewModel"/>. Owns the
/// source list and publishes a read-only, filtered projection that stays in sync as messages
/// arrive. No WPF types: the same class runs in a unit test and behind a ListBox.
/// </summary>
public sealed partial class MessageSearch : ObservableObject, IDisposable
{
    private readonly ObservableCollection<MessageViewModel> _results = [];

    public MessageSearch()
    {
        Results = new ReadOnlyObservableCollection<MessageViewModel>(_results);
        Source.CollectionChanged += OnSourceChanged;
    }

    /// <summary>Every message in the current channel, in arrival order.</summary>
    public ObservableCollection<MessageViewModel> Source { get; } = [];

    /// <summary>What the message list binds to: all of <see cref="Source"/>, or only the matches while filtering.</summary>
    public ReadOnlyObservableCollection<MessageViewModel> Results { get; }

    /// <summary>The active search text. Whitespace-only means "no filter".</summary>
    public string Filter
    {
        get => field;
        set
        {
            var next = value.Trim();
            if (SetProperty(ref field, next))
            {
                Rebuild();
                OnPropertyChanged(nameof(IsFiltering));
            }
        }
    } = string.Empty;

    public bool IsFiltering => Filter.Length > 0;

    public int Count => _results.Count;

    public void Dispose() => Source.CollectionChanged -= OnSourceChanged;

    private static bool Matches(MessageViewModel message, string filter) =>
        message.AuthorName.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || message.Text.Contains(filter, StringComparison.OrdinalIgnoreCase);

    private void OnSourceChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Appends are the hot path (live arrivals); everything else is rare enough to rebuild.
        if (e.Action == NotifyCollectionChangedAction.Add
            && e.NewItems is { Count: 1 }
            && e.NewStartingIndex == Source.Count - 1
            && e.NewItems[0] is MessageViewModel added)
        {
            if (!IsFiltering || Matches(added, Filter))
            {
                _results.Add(added);
                OnPropertyChanged(nameof(Count));
            }

            return;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        _results.Clear();
        foreach (var message in Source)
        {
            if (!IsFiltering || Matches(message, Filter))
            {
                _results.Add(message);
            }
        }

        OnPropertyChanged(nameof(Count));
    }
}
