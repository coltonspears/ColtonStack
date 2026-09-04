using System.Collections.ObjectModel;
using ColtonStack.Client.Extensions.Attachments;
using ColtonStack.Client.Extensions.Commands;
using ColtonStack.Client.Messages;
using ColtonStack.Client.Services;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.ViewModels;

/// <summary>
/// The active conversation: message history, the composer, live arrivals over SignalR, the
/// typing indicator, search and slash commands. State lives in generated partial properties;
/// behavior in commands; the two non-trivial concerns (filtering, slash parsing) are composed
/// in as <see cref="MessageSearch"/> and <see cref="SlashCommandInput"/> — small classes with
/// their own tests, not regions of a god class.
///
/// There is deliberately no threading code here: the messenger delivers every message on the
/// UI thread (see UiThreadMessenger), and commands start on the UI thread and stay there.
/// </summary>
public sealed partial class ChatViewModel(
    IColtonStackApiClient api,
    IChatConnection hub,
    ICommandRegistry commands,
    IAttachmentRegistry attachments,
    IMessenger messenger,
    ILogger<ChatViewModel> logger)
    : ObservableObject, IRecipient<ChannelSelectedMessage>, IRecipient<MessagePostedMessage>, IRecipient<UserTypingMessage>, IRecipient<HubReconnectedMessage>, IDisposable
{
    private static readonly TimeSpan GroupingGap = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TypingThrottle = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TypingIndicatorDecay = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SlashDebounce = TimeSpan.FromMilliseconds(250);

    private DateTimeOffset? _lastTypingSentAt;
    private CancellationTokenSource? _typingDecay;
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _historyCts;

    /// <summary>Filtering over the conversation; the list binds to <c>Search.Results</c>.</summary>
    public MessageSearch Search { get; } = new();

    /// <summary>Slash-command state for the composer popup.</summary>
    public SlashCommandInput Slash { get; } = new(commands, SlashDebounce);

    /// <summary>Every message in the current channel, oldest first.</summary>
    public ObservableCollection<MessageViewModel> Messages => Search.Source;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChannelTitle))]
    [NotifyPropertyChangedFor(nameof(ComposerPlaceholder))]
    [NotifyPropertyChangedFor(nameof(HasChannel))]
    public partial ChannelSummaryDto? CurrentChannel { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
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

    public bool HasChannel => CurrentChannel is not null;

    public string ChannelTitle => CurrentChannel is { } channel ? $"# {channel.Name}" : "No channel selected";

    public string ComposerPlaceholder => CurrentChannel is { } channel ? $"Message #{channel.Name}  —  type / for commands" : "Select a channel to start chatting";

    partial void OnDraftChanged(string value)
    {
        NotifyTypingThrottled();
        _ = RefreshSlashAsync(value);
    }

    private async Task RefreshSlashAsync(string draft)
    {
        await Slash.UpdateAsync(draft);
        NextSuggestionCommand.NotifyCanExecuteChanged();
        PreviousSuggestionCommand.NotifyCanExecuteChanged();
        AcceptSuggestionCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Mouse click on a suggestion row.</summary>
    [RelayCommand]
    private void PickSuggestion(SlashSuggestion? suggestion)
    {
        if (suggestion is not null)
        {
            Draft = suggestion.CompletedDraft;
        }
    }

    partial void OnSearchTextChanged(string value) => DebounceSearch();

    partial void OnIsSearchActiveChanged(bool value)
    {
        if (!value)
        {
            SearchText = string.Empty;
            Search.Filter = string.Empty;
        }
    }

    [RelayCommand]
    private void ToggleSearch() => IsSearchActive = !IsSearchActive;

    private bool CanClearSearch() => IsSearchActive && SearchText.Length > 0;

    [RelayCommand(CanExecute = nameof(CanClearSearch))]
    private void ClearSearch()
    {
        SearchText = string.Empty;
        Search.Filter = string.Empty;
    }

    /// <summary>Slash popup navigation — bound to Up/Down on the composer; inert when the popup is closed so the caret keeps working.</summary>
    [RelayCommand(CanExecute = nameof(IsSlashOpen))]
    private void NextSuggestion() => Slash.MoveSelection(+1);

    [RelayCommand(CanExecute = nameof(IsSlashOpen))]
    private void PreviousSuggestion() => Slash.MoveSelection(-1);

    /// <summary>Tab in the composer: take the highlighted suggestion.</summary>
    [RelayCommand(CanExecute = nameof(IsSlashOpen))]
    private void AcceptSuggestion()
    {
        if (Slash.Selected is { } selected)
        {
            Draft = selected.CompletedDraft;
        }
    }

    private bool IsSlashOpen() => Slash.IsActive && Slash.Suggestions.Count > 0;

    /// <summary>
    /// Debounces the search filter: each keystroke cancels the previous pending refresh, so the
    /// filter only applies after the user pauses typing.
    /// </summary>
    private void DebounceSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        _ = ApplyFilterAfterDelayAsync(_searchCts.Token);
    }

    private async Task ApplyFilterAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDebounce, cancellationToken);
            Search.Filter = SearchText;
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

        // Enter while the slash popup is open first completes the highlighted suggestion.
        if (Slash.IsActive && Slash.Selected is { } selected && !string.Equals(Draft.Trim(), selected.CompletedDraft.Trim(), StringComparison.Ordinal))
        {
            Draft = selected.CompletedDraft;
            if (!selected.IsArgument)
            {
                return; // the name is complete; now the argument gets typed
            }

            await Slash.UpdateAsync(Draft); // refresh state synchronously for TryResolve below
        }

        if (Draft.StartsWith('/'))
        {
            await RunSlashCommandAsync(channel, cancellationToken);
            return;
        }

        var text = Draft.Trim();
        Draft = string.Empty; // optimistic: clear the composer immediately

        try
        {
            var saved = await api.SendMessageAsync(channel.Id, text, cancellationToken);

            // Show our own copy immediately; when the hub echoes it back, AppendMessage dedupes by id.
            AppendMessage(saved);
        }
        catch (Exception ex)
        {
            SendFailed(ex, channel.Id);
            Draft = text; // give the text back — the resilience pipeline exhausted its attempts
            messenger.Send(new HttpRetryMessage(0, $"Send failed: {ex.Message}"));
        }
    }

    private async Task RunSlashCommandAsync(ChannelSummaryDto channel, CancellationToken cancellationToken)
    {
        if (!Slash.TryResolve(out var command, out var argument))
        {
            messenger.Send(new HttpRetryMessage(0, $"Unknown command: {Draft.Split(' ')[0]}"));
            return;
        }

        var draft = Draft;
        Draft = string.Empty;
        try
        {
            await command.ExecuteAsync(new CommandInvocation(argument, channel.Id, cancellationToken));
            CommandRan(command.Id, channel.Id);
        }
        catch (Exception ex)
        {
            CommandFailed(ex, command.Id);
            Draft = draft;
            messenger.Send(new HttpRetryMessage(0, $"/{command.SlashName} failed: {ex.Message}"));
        }
    }

    private bool CanSend() =>
        CurrentChannel is not null && !string.IsNullOrWhiteSpace(Draft);

    public void Receive(ChannelSelectedMessage message)
    {
        CurrentChannel = message.Channel;
        TypingIndicator = string.Empty;
        Messages.Clear();

        if (IsSearchActive)
        {
            SearchText = string.Empty;
            Search.Filter = string.Empty;
        }

        // A channel switch cancels whatever history request the previous channel had in flight.
        _historyCts?.Cancel();
        _historyCts?.Dispose();
        _historyCts = new CancellationTokenSource();
        _ = LoadHistoryAsync(message.Channel, _historyCts.Token);
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
            _ = CatchUpAsync(channel, _historyCts?.Token ?? CancellationToken.None);
        }
    }

    private async Task CatchUpAsync(ChannelSummaryDto channel, CancellationToken cancellationToken)
    {
        try
        {
            var lastId = Messages.Count > 0 ? Messages[^1].Id : 0;
            var missed = await api.GetMessagesAsync(channel.Id, afterId: lastId, cancellationToken);
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
        catch (OperationCanceledException)
        {
            // channel switched — the new channel's load takes over
        }
        catch (Exception ex)
        {
            CatchUpFailed(ex, channel.Id);
            messenger.Send(new HttpRetryMessage(0, $"Could not catch up on missed messages: {ex.Message}"));
        }
    }

    private async Task LoadHistoryAsync(ChannelSummaryDto? channel, CancellationToken cancellationToken)
    {
        if (channel is null)
        {
            return;
        }

        IsLoadingHistory = true;
        try
        {
            var history = await api.GetMessagesAsync(channel.Id, afterId: 0, cancellationToken);
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
        catch (OperationCanceledException)
        {
            // superseded by a newer channel selection
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
        var isFirstOfDay = previous is null
            || previous.CreatedAtUtc.ToLocalTime().Date != message.CreatedAtUtc.ToLocalTime().Date;
        var isFirstOfGroup = isFirstOfDay
            || !string.Equals(previous!.AuthorName, message.AuthorName, StringComparison.Ordinal)
            || message.CreatedAtUtc - previous.CreatedAtUtc > GroupingGap
            || message.Attachment is not null
            || previous.HasAttachment;

        Messages.Add(new MessageViewModel(message, isFirstOfGroup, isFirstOfDay, attachments.Materialize(message.Attachment)));
    }

    private void NotifyTypingThrottled()
    {
        if (CurrentChannel is null || string.IsNullOrWhiteSpace(Draft) || Draft.StartsWith('/'))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (_lastTypingSentAt is { } last && now - last < TypingThrottle)
        {
            return;
        }

        _lastTypingSentAt = now;
        _ = hub.NotifyTypingAsync(CurrentChannel.Id, CancellationToken.None);
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
            await Task.Delay(TypingIndicatorDecay, cancellationToken);
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
        _searchCts?.Dispose();
        _historyCts?.Dispose();
        Slash.Dispose();
        Search.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sending a message to channel {ChannelId} failed after retries")]
    private partial void SendFailed(Exception exception, long channelId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Loading history for channel {ChannelId} failed after retries")]
    private partial void HistoryLoadFailed(Exception exception, long channelId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reconnect catch-up fetched {Count} missed message(s) for channel {ChannelId}")]
    private partial void CaughtUp(int count, long channelId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconnect catch-up for channel {ChannelId} failed after retries")]
    private partial void CatchUpFailed(Exception exception, long channelId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Slash command {CommandId} ran in channel {ChannelId}")]
    private partial void CommandRan(string commandId, long channelId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Slash command {CommandId} failed")]
    private partial void CommandFailed(Exception exception, string commandId);
}
