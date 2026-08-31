using ColtonStack.Client.Configuration;
using ColtonStack.Client.Messages;
using ColtonStack.Contracts;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ColtonStack.Client.Services;

/// <summary>
/// Owns the SignalR connection. Push replaces polling: whenever the server saves a message it
/// calls <see cref="IChatHubClient"/>, and this class re-publishes it onto the app's messenger.
///
/// Resilience comes from two layers:
///   * <see cref="HubConnectionExtensions.WithAutomaticReconnect"/> heals brief blips, and
///   * a connect loop keeps trying (2s cadence) when the server is down entirely — including at
///     startup, so the client can be launched before the server.
///
/// Registered as a singleton and started by the Generic Host (it implements IHostedService),
/// which is why it needs no explicit lifecycle management from any view model.
/// </summary>
public sealed partial class ChatHubClient(
    IMessenger messenger,
    IOptions<ColtonStackOptions> options,
    ILogger<ChatHubClient> logger) : IHostedService, IDisposable
{
    private readonly CancellationTokenSource _stopping = new();
    private HubConnection? _connection;
    private long? _joinedChannelId;
    private bool _hasConnectedOnce;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = ConnectLoopAsync();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync().ConfigureAwait(false);
        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }
    }

    public void Dispose() => _stopping.Dispose();

    /// <summary>Joins (or switches to) a channel's SignalR group; remembered and re-applied after reconnects.</summary>
    public async Task JoinChannelAsync(long channelId)
    {
        _joinedChannelId = channelId;
        var connection = _connection;
        if (connection?.State == HubConnectionState.Connected)
        {
            try
            {
                await connection.InvokeAsync(ChatHubMethods.JoinChannel, channelId).ConfigureAwait(false);
                JoinedChannel(channelId);
            }
            catch (Exception ex)
            {
                // The connection could drop between the state check and InvokeAsync;
                // the reconnect loop handles re-joining automatically.
                JoinChannelFailed(ex, channelId);
            }
        }
    }

    /// <summary>Fire-and-forget typing notification, throttled by the caller.</summary>
    public async Task NotifyTypingAsync(long channelId)
    {
        var connection = _connection;
        if (connection?.State == HubConnectionState.Connected)
        {
            await connection.InvokeAsync(ChatHubMethods.NotifyTyping, channelId).ConfigureAwait(false);
        }
    }

    private async Task ConnectLoopAsync()
    {
        var hubUrl = new Uri(new Uri(options.Value.ServerUrl), "hubs/chat").ToString();

        while (!_stopping.IsCancellationRequested)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect()
                .Build();

            WireHandlers(connection);

            try
            {
                await RunConnectionAsync(connection).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _connection = null;
                Publish(ConnectionState.Connecting, $"Waiting for server at {options.Value.ServerUrl}…");
                HubStartFailed(ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), _stopping.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// One connection's lifetime: start, re-apply the channel group, publish reconnect news,
    /// then park until the connection drops so the loop can rebuild it.
    /// </summary>
    private async Task RunConnectionAsync(HubConnection connection)
    {
        // Park on this until the connection drops; created and awaited here so ownership is
        // unambiguous (VSTHRD003).
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.Closed += _ =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        await connection.StartAsync(_stopping.Token).ConfigureAwait(false);
        _connection = connection;
        Publish(ConnectionState.Connected, "Connected to ColtonStack");
        HubConnected();

        // Every successful connect after the first is a recovery from a hard outage: tell
        // the app so consumers can catch up incrementally (the chat pane re-fetches from
        // its newest known message id, not from zero).
        if (_hasConnectedOnce)
        {
            messenger.Send(new HubReconnectedMessage());
        }

        _hasConnectedOnce = true;

        if (_joinedChannelId is { } channel)
        {
            await JoinChannelAsync(channel).ConfigureAwait(false);
        }

        // Park until the connection drops, then loop around and rebuild.
        await closed.Task.ConfigureAwait(false);
        _connection = null;
        if (!_stopping.IsCancellationRequested)
        {
            Publish(ConnectionState.Reconnecting, "Connection lost — reconnecting…");
        }
    }

    private void WireHandlers(HubConnection connection)
    {
        // Hub events arrive on background threads; UiThreadMessenger delivers them on the UI
        // thread, so recipients never marshal anything.
        connection.On<MessageDto>(nameof(IChatHubClient.MessagePostedAsync),
            message => messenger.Send(new MessagePostedMessage(message)));

        connection.On<ChannelSummaryDto>(nameof(IChatHubClient.ChannelCreatedAsync),
            channel => messenger.Send(new ChannelCreatedMessage(channel)));

        connection.On<long, string>(nameof(IChatHubClient.UserTypingAsync),
            (channelId, userDisplayName) => messenger.Send(new UserTypingMessage(channelId, userDisplayName)));

        connection.Reconnecting += error =>
        {
            Publish(ConnectionState.Reconnecting, "Reconnecting…");
            return Task.CompletedTask;
        };

        connection.Reconnected += reconnectMessage =>
        {
            Publish(ConnectionState.Connected, "Connected to ColtonStack");
            messenger.Send(new HubReconnectedMessage());
            if (_joinedChannelId is { } channel)
            {
                _ = JoinChannelAsync(channel);
            }

            return Task.CompletedTask;
        };
    }

    private void Publish(ConnectionState state, string detail) =>
        messenger.Send(new ConnectionStatusMessage(state, detail));

    [LoggerMessage(Level = LogLevel.Information, Message = "Joined channel group {ChannelId}")]
    private partial void JoinedChannel(long channelId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SignalR hub connected")]
    private partial void HubConnected();

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to join channel group {ChannelId} — re-join happens on reconnect")]
    private partial void JoinChannelFailed(Exception exception, long channelId);

    [LoggerMessage(Level = LogLevel.Information, Message = "SignalR start failed: {Reason} — will keep retrying")]
    private partial void HubStartFailed(string reason);
}
