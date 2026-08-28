using System.Collections.ObjectModel;
using System.Windows.Threading;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The active conversation: message history, the composer, live arrivals over SignalR, and the
/// typing indicator. All state lives in generated partial properties; behavior in commands.
/// </summary>
public sealed partial class ChatViewModel(
    ColtonStackApiClient api,
    ChatHubClient hub,
    IMessenger messenger,
    Dispatcher dispatcher,
    ILogger<ChatViewModel> logger) : ObservableObject, IRecipient<ChannelSelectedMessage>, IRecipient<MessagePostedMessage>, IRecipient<UserTypingMessage>, IDisposable
{
    private static readonly TimeSpan GroupingGap = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TypingThrottle = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TypingIndicatorDecay = TimeSpan.FromSeconds(3);

    private DateTimeOffset? _lastTypingSentAt;
    private CancellationTokenSource? _typingDecay;

    /// <summary>Raised after a message lands; the view auto-scrolls (a view-only concern).</summary>
    public event EventHandler? MessageArrived;

    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    [ObservableProperty]
    public partial ChannelSummaryDto? CurrentChannel { get; set; }

    [ObservableProperty]
    public partial string Draft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoadingHistory { get; set; }

    [ObservableProperty]
    public partial string TypingIndicator { get; set; } = string.Empty;

    public string ChannelTitle => CurrentChannel is { } channel ? $"# {channel.Name}" : "No channel selected";

    partial void OnCurrentChannelChanged(ChannelSummaryDto? value) => OnPropertyChanged(nameof(ChannelTitle));

    partial void OnDraftChanged(string value)
    {
        SendMessageCommand.NotifyCanExecuteChanged();
        NotifyTypingThrottled();
    }

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanSend))]
    private async Task SendMessageAsync(CancellationToken cancellationToken)
    {
        if (CurrentChannel is not { } channel)
        {
            return;
        }

        var text = Draft.Trim();
        Draft = string.Empty; // optimistic: clear the composer immediately

        try
        {
            var saved = await api.SendMessageAsync(channel.Id, text, cancellationToken).ConfigureAwait(false);

            // Show our own copy immediately; when the hub echoes it back, AppendMessage dedupes by id.
            _ = dispatcher.InvokeAsync(() => AppendMessage(saved));
        }
        catch (Exception ex)
        {
            SendFailed(ex, channel.Id);
            Draft = text; // give the text back — the resilience pipeline exhausted its attempts
            messenger.Send(new HttpRetryMessage(0, $"Send failed: {ex.Message}"));
        }
    }

    private bool CanSend() =>
        CurrentChannel is not null && !string.IsNullOrWhiteSpace(Draft);

    public void Receive(ChannelSelectedMessage message)
    {
        _ = dispatcher.InvokeAsync(() =>
        {
            CurrentChannel = message.Channel;
            TypingIndicator = string.Empty;
            Messages.Clear();
        });
        _ = LoadHistoryAsync(message.Channel);
    }

    public void Receive(MessagePostedMessage message)
    {
        if (message.Message.ChannelId != CurrentChannel?.Id)
        {
            return; // another channel — the sidebar owns the unread badge
        }

        dispatcher.InvokeAsync(() => AppendMessage(message.Message));
    }

    public void Receive(UserTypingMessage message)
    {
        if (message.ChannelId != CurrentChannel?.Id)
        {
            return;
        }

        _ = dispatcher.InvokeAsync(() =>
        {
            TypingIndicator = $"{message.UserDisplayName} is typing…";
            RestartTypingDecay();
        });
    }

    private async Task LoadHistoryAsync(ChannelSummaryDto? channel)
    {
        if (channel is null)
        {
            return;
        }

        IsLoadingHistory = true;
        try
        {
            var history = await api.GetMessagesAsync(channel.Id, afterId: 0, CancellationToken.None).ConfigureAwait(false);
            _ = dispatcher.InvokeAsync(() =>
            {
                if (CurrentChannel?.Id != channel.Id)
                {
                    return; // the user switched away while the request was in flight
                }

                Messages.Clear();
                foreach (var message in history)
                {
                    AppendMessage(message);
                }
            });
        }
        catch (Exception ex)
        {
            HistoryLoadFailed(ex, channel.Id);
            messenger.Send(new HttpRetryMessage(0, $"Could not load history: {ex.Message}"));
        }
        finally
        {
            IsLoadingHistory = false;
        }
    }

    private void AppendMessage(MessageDto message)
    {
        if (Messages.Any(existing => existing.Id == message.Id))
        {
            return; // hub echo of our own send
        }

        var previous = Messages.Count > 0 ? Messages[^1] : null;
        var isFirstOfGroup = previous is null
            || !string.Equals(previous.AuthorName, message.AuthorName, StringComparison.Ordinal)
            || message.CreatedAtUtc - previous.CreatedAtUtc > GroupingGap;

        Messages.Add(new MessageViewModel(message, isFirstOfGroup));
        MessageArrived?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyTypingThrottled()
    {
        if (CurrentChannel is null || string.IsNullOrWhiteSpace(Draft))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastTypingSentAt is { } last && now - last < TypingThrottle)
        {
            return;
        }

        _lastTypingSentAt = now;
        _ = hub.NotifyTypingAsync(CurrentChannel.Id);
    }

    private void RestartTypingDecay()
    {
        _typingDecay?.Cancel();
        _typingDecay?.Dispose();
        _typingDecay = new CancellationTokenSource();
        var token = _typingDecay.Token;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(TypingIndicatorDecay, token).ConfigureAwait(false);
                    await dispatcher.InvokeAsync(() => TypingIndicator = string.Empty).Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // a newer typing notification replaced this one
                }
            },
            CancellationToken.None);
    }

    public void Dispose()
    {
        _typingDecay?.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending a message to channel {ChannelId} failed after retries")]
    private partial void SendFailed(Exception exception, long channelId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading history for channel {ChannelId} failed after retries")]
    private partial void HistoryLoadFailed(Exception exception, long channelId);
}
