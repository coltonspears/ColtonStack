using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// One sidebar row. Uses <c>[ObservableProperty]</c> as a public partial property — no backing
/// field is declared anywhere, the source generator writes the whole implementation.
/// </summary>
public sealed partial class ChannelListItemViewModel(ChannelSummaryDto summary) : ObservableObject
{
    public long Id { get; } = summary.Id;

    public string Name { get; } = summary.Name;

    public string Topic { get; } = summary.Topic;

    public string DisplayName => $"# {Name}";

    /// <summary>The summary this row was created from (id/name/topic are immutable per channel).</summary>
    public ChannelSummaryDto Summary { get; } = summary;

    [ObservableProperty]
    public partial string Preview { get; set; } = summary.LastMessagePreview ?? "No messages yet";

    /// <summary>
    /// The `field` keyword at work: a property with validation logic, yet no explicit backing
    /// field. Negative counts are clamped away and the unread badge notification chains off
    /// the same assignment.
    /// </summary>
    public int UnreadCount
    {
        get => field;
        set
        {
            if (SetProperty(ref field, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(HasUnread));
            }
        }
    }

    public bool HasUnread => UnreadCount > 0;

    /// <summary>Replaces the sidebar summary after a refresh or new activity.</summary>
    public void UpdateFrom(ChannelSummaryDto summary)
    {
        Preview = summary.LastMessagePreview ?? "No messages yet";
    }
}
