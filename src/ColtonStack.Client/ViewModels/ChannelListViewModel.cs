using System.Collections.ObjectModel;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The sidebar. Loads channels over HTTP, creates new ones, tracks unread counts — and learns
/// about activity purely through the messenger, never by being poked from the chat pane.
/// Every message is delivered on the UI thread (see UiThreadMessenger), so the
/// ObservableCollection below is only ever touched from the right thread by construction.
/// </summary>
public sealed partial class ChannelListViewModel(
    ColtonStackApiClient api,
    ChatHubClient hub,
    IMessenger messenger,
    ILogger<ChannelListViewModel> logger) : ObservableObject, IRecipient<ChannelCreatedMessage>, IRecipient<MessagePostedMessage>
{
    public ObservableCollection<ChannelListItemViewModel> Channels { get; } = [];

    [ObservableProperty]
    public partial ChannelListItemViewModel? SelectedChannel { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Shows the inline "create a channel" box under the sidebar section header.</summary>
    [ObservableProperty]
    public partial bool IsAddingChannel { get; set; }

    [ObservableProperty]
    public partial string NewChannelName { get; set; } = string.Empty;

    [RelayCommand]
    private void ToggleAddChannel() => IsAddingChannel = !IsAddingChannel;

    partial void OnNewChannelNameChanged(string value) => CreateChannelCommand.NotifyCanExecuteChanged();

    partial void OnSelectedChannelChanged(ChannelListItemViewModel? value)
    {
        // One property change fans out: the chat pane loads history and the hub group switches.
        messenger.Send(new ChannelSelectedMessage(value?.Summary));
        if (value is not null)
        {
            value.UnreadCount = 0;
            _ = hub.JoinChannelAsync(value.Id);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsLoading = true;
        try
        {
            var summaries = await api.GetChannelsAsync(cancellationToken);
            var items = summaries.Select(summary => new ChannelListItemViewModel(summary)).ToList();

            Channels.Clear();
            foreach (var item in items)
            {
                Channels.Add(item);
            }

            SelectedChannel ??= Channels.FirstOrDefault();
            ChannelsLoaded(items.Count);
        }
        catch (Exception ex)
        {
            ChannelsLoadFailed(ex);
            messenger.Send(new HttpRetryMessage(0, $"Could not load channels: {ex.Message}"));
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanCreateChannel))]
    private async Task CreateChannelAsync(CancellationToken cancellationToken)
    {
        var name = NewChannelName.Trim();

        try
        {
            var created = await api.CreateChannelAsync(name, string.Empty, cancellationToken);

            NewChannelName = string.Empty;
            IsAddingChannel = false;

            // The hub also broadcasts ChannelCreated for our own create; InsertIfMissing dedupes,
            // so this call selects the channel the moment it exists.
            var item = InsertIfMissing(created);
            SelectedChannel = item;
        }
        catch (Exception ex)
        {
            // The name stays in the box so the user can correct it and retry.
            CreateChannelFailed(ex, name);
            messenger.Send(new HttpRetryMessage(0, $"Could not create #{name}: {ex.Message}"));
        }
    }

    private bool CanCreateChannel() =>
        NewChannelName.Trim().Length > 0;

    public void Receive(ChannelCreatedMessage message) =>
        InsertIfMissing(message.Channel);

    public void Receive(MessagePostedMessage message)
    {
        var item = Channels.FirstOrDefault(channel => channel.Id == message.Message.ChannelId);
        if (item is null)
        {
            return;
        }

        item.UpdateFrom(new ChannelSummaryDto(
            item.Id, item.Name, item.Topic,
            MessageCount: 0, LastMessageId: message.Message.Id,
            LastMessageAtUtc: message.Message.CreatedAtUtc,
            LastMessagePreview: message.Message.Text));

        if (SelectedChannel?.Id != message.Message.ChannelId)
        {
            item.UnreadCount++;
        }
    }

    private ChannelListItemViewModel InsertIfMissing(ChannelSummaryDto summary)
    {
        var existing = Channels.FirstOrDefault(channel => channel.Id == summary.Id);
        if (existing is not null)
        {
            existing.UpdateFrom(summary);
            return existing;
        }

        var item = new ChannelListItemViewModel(summary);
        Channels.Add(item);
        return item;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loaded {Count} channels")]
    private partial void ChannelsLoaded(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading channels failed after retries")]
    private partial void ChannelsLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Creating channel {Name} failed")]
    private partial void CreateChannelFailed(Exception exception, string name);
}
