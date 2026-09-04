namespace ColtonStack.Client.Services;

/// <summary>
/// What a view model may ask of the live SignalR connection: join a channel group and announce
/// typing. Everything else about the connection (lifetime, reconnects, inbound events) is owned
/// by <see cref="ChatHubClient"/> and surfaces through the messenger — no view model holds the
/// connection object. Named apart from <c>IChatHubClient</c>, which is the server-to-client
/// callback contract in Contracts.
/// </summary>
public interface IChatConnection
{
    /// <summary>Joins (or switches to) a channel's SignalR group; remembered and re-applied after reconnects.</summary>
    Task JoinChannelAsync(long channelId, CancellationToken cancellationToken);

    /// <summary>Announces that the current user is typing in a channel. Throttled by the caller.</summary>
    Task NotifyTypingAsync(long channelId, CancellationToken cancellationToken);
}
