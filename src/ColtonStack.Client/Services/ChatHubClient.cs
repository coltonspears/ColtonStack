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
            await connection.InvokeAsync(ChatHubMethods.JoinChannel, channelId).ConfigureAwait(false);
            JoinedChannel(channelId);
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

            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.Closed += _ =>
            {
                closed.TrySetResult();
                return Task.CompletedTask;
            };

            try
            {
                await connection.StartAsync(_stopping.Token).ConfigureAwait(false);
                _connection = connection;
                Publish(ConnectionState.Connected, "Connected to ColtonStack");
                HubConnected();

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

    private void WireHandlers(HubConnection connection)
    {
        // Hub events arrive on background threads; recipients marshal to the UI thread themselves.
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

    [LoggerMessage(Level = LogLevel.Information, Message = "SignalR start failed: {Reason} — will keep retrying")]
    private partial void HubStartFailed(string reason);
}
