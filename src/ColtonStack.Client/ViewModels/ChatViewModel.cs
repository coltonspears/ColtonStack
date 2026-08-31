using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
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
///
/// There is deliberately no threading code here: the messenger delivers every message on the
/// UI thread (see UiThreadMessenger), and commands start on the UI thread and stay there.
/// </summary>
public sealed partial class ChatViewModel : ObservableObject, IRecipient<ChannelSelectedMessage>, IRecipient<MessagePostedMessage>, IRecipient<UserTypingMessage>, IRecipient<HubReconnectedMessage>, IDisposable
{
    private static readonly TimeSpan _groupingGap = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _typingThrottle = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _typingIndicatorDecay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _searchDebounce = TimeSpan.FromMilliseconds(300);

    private readonly ColtonStackApiClient _api;
    private readonly ChatHubClient _hub;
    private readonly IMessenger _messenger;
    private readonly ILogger<ChatViewModel> _logger;
    private DateTimeOffset? _lastTypingSentAt;
    private CancellationTokenSource? _typingDecay;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<MessageViewModel> Messages { get; } = new ObservableCollection<MessageViewModel>();

    /// <summary>
    /// WPF's built-in live filtering view over <see cref="Messages"/>. Created once on the UI
    /// thread; the filter predicate updates as the user types. The XAML ListBox binds here
    /// instead of <c>Messages</c> so filtered results show automatically.
    /// </summary>
    public ICollectionView FilteredMessages { get; }

    public ChatViewModel(
        ColtonStackApiClient api,
        ChatHubClient hub,
        IMessenger messenger,
        ILogger<ChatViewModel> logger)
    {
        _api = api;
        _hub = hub;
        _messenger = messenger;
        _logger = logger;
        FilteredMessages = CollectionViewSource.GetDefaultView(Messages);
    }

    [ObservableProperty]
    public partial ChannelSummaryDto? CurrentChannel { get; set; }

    [ObservableProperty]
    public partial string Draft { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoadingHistory { get; set; }

    [ObservableProperty]
    public partial string TypingIndicator { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearSearchCommand))]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsSearchActive { get; set; }

    [ObservableProperty]
    public partial int SearchResultCount { get; set; }

    public string ChannelTitle => CurrentChannel is { } channel ? $"# {channel.Name}" : "No channel selected";

    public string ComposerPlaceholder => CurrentChannel is { } channel ? $"Message #{channel.Name}" : "Select a channel to start chatting";

    partial void OnCurrentChannelChanged(ChannelSummaryDto? value)
    {
        OnPropertyChanged(nameof(ChannelTitle));
        OnPropertyChanged(nameof(ComposerPlaceholder));
    }

    partial void OnDraftChanged(string value)
    {
        SendMessageCommand.NotifyCanExecuteChanged();
        NotifyTypingThrottled();
    }

    partial void OnSearchTextChanged(string value)
    {
        DebounceSearch();
    }

    partial void OnIsSearchActiveChanged(bool value)
    {
        if (!value)
        {
            // Closing search: clear the filter and reset the box
            SearchText = string.Empty;
            FilteredMessages.Filter = null;
            SearchResultCount = 0;
        }
    }

    [RelayCommand]
    private void ToggleSearch()
    {
        IsSearchActive = !IsSearchActive;
        if (IsSearchActive)
        {
            // Focus is handled by the XAML behavior; just fire the filter.
            SearchResultCount = Messages.Count;
        }
    }

    private bool CanClearSearch() => IsSearchActive && SearchText.Length > 0;

    [RelayCommand(CanExecute = nameof(CanClearSearch))]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        FilteredMessages.Filter = null;
        SearchResultCount = Messages.Count;
    }

    /// <summary>
    /// Debounces the search filter: each keystroke cancels the previous pending refresh,
    /// so the filter only applies after the user pauses typing for <see cref="SearchDebounce"/>.
    /// </summary>
    private void DebounceSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = ApplyFilterAfterDelayAsync(token);
    }

    private async Task ApplyFilterAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_searchDebounce, cancellationToken).ConfigureAwait(true);

            var filter = SearchText.Trim();
            if (filter.Length == 0)
            {
                FilteredMessages.Filter = null;
                SearchResultCount = Messages.Count;
                return;
            }

            // Case-insensitive search across author name and message text
            FilteredMessages.Filter = item =>
                item is MessageViewModel msg
                && (msg.AuthorName.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || msg.Text.Contains(filter, StringComparison.OrdinalIgnoreCase));

            SearchResultCount = FilteredMessages.Cast<MessageViewModel>().Count(m => FilteredMessages.Filter(m));
        }
        catch (OperationCanceledException)
        {
            // A newer keystroke replaced this search — nothing to do
        }
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
            var saved = await _api.SendMessageAsync(channel.Id, text, cancellationToken);

            // Show our own copy immediately; when the hub echoes it back, AppendMessage dedupes by id.
            AppendMessage(saved);
        }
        catch (Exception ex)
        {
            SendFailed(ex, channel.Id);
            Draft = text; // give the text back — the resilience pipeline exhausted its attempts
            _messenger.Send(new HttpRetryMessage(0, $"Send failed: {ex.Message}"));
        }
    }

    private bool CanSend() =>
        CurrentChannel is not null && !string.IsNullOrWhiteSpace(Draft);

    public void Receive(ChannelSelectedMessage message)
    {
        CurrentChannel = message.Channel;
        TypingIndicator = string.Empty;
        Messages.Clear();

        // Clear any active search when switching channels
        if (IsSearchActive)
        {
            SearchText = string.Empty;
            FilteredMessages.Filter = null;
            SearchResultCount = 0;
        }

        _ = LoadHistoryAsync(message.Channel);
    }

    public void Receive(MessagePostedMessage message)
    {
        if (message.Message.ChannelId != CurrentChannel?.Id)
        {
            return; // another channel — the sidebar owns the unread badge
        }

        AppendMessage(message.Message);
    }

    public void Receive(UserTypingMessage message)
    {
        if (message.ChannelId != CurrentChannel?.Id)
        {
            return;
        }

        TypingIndicator = $"{message.UserDisplayName} is typing…";
        RestartTypingDecay();
    }

    /// <summary>
    /// Connection recovered after a hard drop: fetch only messages newer than the newest one
    /// already on screen (<c>afterId</c> catch-up) instead of reloading the whole history.
    /// Bounded re-sync is the standard move in database-heavy apps — the server still caps
    /// the query, and AppendMessage's id-dedupe absorbs any overlap with live hub pushes.
    /// </summary>
    public void Receive(HubReconnectedMessage message)
    {
        if (CurrentChannel is { } channel)
        {
            _ = CatchUpAsync(channel);
        }
    }

    private async Task CatchUpAsync(ChannelSummaryDto channel)
    {
        try
        {
            var lastId = Messages.Count > 0 ? Messages[^1].Id : 0;
            var missed = await _api.GetMessagesAsync(channel.Id, afterId: lastId, CancellationToken.None);
            if (CurrentChannel?.Id != channel.Id)
            {
                return; // user switched away during the catch-up request
            }

            foreach (var message in missed)
            {
                AppendMessage(message);
            }

            CaughtUp(missed.Count, channel.Id);
        }
        catch (Exception ex)
        {
            CatchUpFailed(ex, channel.Id);
            _messenger.Send(new HttpRetryMessage(0, $"Could not catch up on missed messages: {ex.Message}"));
        }
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
            var history = await _api.GetMessagesAsync(channel.Id, afterId: 0, CancellationToken.None);
            if (CurrentChannel?.Id != channel.Id)
            {
                return; // the user switched away while the request was in flight
            }

            Messages.Clear();
            foreach (var message in history)
            {
                AppendMessage(message);
            }
        }
        catch (Exception ex)
        {
            HistoryLoadFailed(ex, channel.Id);
            _messenger.Send(new HttpRetryMessage(0, $"Could not load history: {ex.Message}"));
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
            || message.CreatedAtUtc - previous.CreatedAtUtc > _groupingGap;

        Messages.Add(new MessageViewModel(message, isFirstOfGroup));

        // Keep search result count in sync if a filter is active
        if (IsSearchActive && FilteredMessages.Filter is { } filter)
        {
            SearchResultCount = FilteredMessages.Cast<MessageViewModel>().Count(m => filter(m));
        }
    }

    private void NotifyTypingThrottled()
    {
        if (CurrentChannel is null || string.IsNullOrWhiteSpace(Draft))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastTypingSentAt is { } last && now - last < _typingThrottle)
        {
            return;
        }

        _lastTypingSentAt = now;
        _ = _hub.NotifyTypingAsync(CurrentChannel.Id);
    }

    private void RestartTypingDecay()
    {
        _typingDecay?.Cancel();
        _typingDecay?.Dispose();
        _typingDecay = new CancellationTokenSource();
        _ = ClearTypingIndicatorLaterAsync(_typingDecay.Token);
    }

    private async Task ClearTypingIndicatorLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Started on the UI thread, so the continuation resumes there too.
            await Task.Delay(_typingIndicatorDecay, cancellationToken);
            TypingIndicator = string.Empty;
        }
        catch (OperationCanceledException)
        {
            // a newer typing notification replaced this one
        }
    }

    public void Dispose()
    {
        _typingDecay?.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending a message to channel {ChannelId} failed after retries")]
    private partial void SendFailed(Exception exception, long channelId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading history for channel {ChannelId} failed after retries")]
    private partial void HistoryLoadFailed(Exception exception, long channelId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconnect catch-up fetched {Count} missed message(s) for channel {ChannelId}")]
    private partial void CaughtUp(int count, long channelId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconnect catch-up for channel {ChannelId} failed after retries")]
    private partial void CatchUpFailed(Exception exception, long channelId);
}
