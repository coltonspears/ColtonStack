namespace ColtonStack.Contracts;

/// <summary>
/// The typed SignalR "client callback" contract.
///
/// Server-side, <c>ChatHub</c> implements calls through <c>Hub&lt;IChatHubClient&gt;</c>, so a
/// broadcast is an interface method — <c>Clients.Group(...).MessagePosted(dto)</c> — with
/// rename-safe, compile-checked method names. No magic strings on either side.
/// </summary>
public interface IChatHubClient
{
    /// <summary>A message was saved to the channel.</summary>
    Task MessagePostedAsync(MessageDto message);

    /// <summary>A channel was created.</summary>
    Task ChannelCreatedAsync(ChannelSummaryDto channel);

    /// <summary>Someone is typing in the given channel (sent to everyone else in it).</summary>
    Task UserTypingAsync(long channelId, string userDisplayName);
}
