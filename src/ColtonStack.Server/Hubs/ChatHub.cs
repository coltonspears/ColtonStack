using ColtonStack.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace ColtonStack.Server.Hubs;

/// <summary>
/// Real-time push hub. Typed via <c>Hub&lt;IChatHubClient&gt;</c>: every broadcast below is an
/// interface method call, so the wire contract is checked by the compiler — no event-name strings.
/// </summary>
public sealed class ChatHub : Hub<IChatHubClient>
{
    public static string GroupNameFor(long channelId) => $"channel-{channelId}";

    public Task JoinChannelAsync(long channelId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(channelId));

    public Task LeaveChannelAsync(long channelId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNameFor(channelId));

    /// <summary>Relays a typing notification to everyone else in the channel. No persistence — pure push.</summary>
    public async Task NotifyTypingAsync(long channelId, string userDisplayName)
    {
        await Clients.OthersInGroup(GroupNameFor(channelId))
            .UserTypingAsync(channelId, userDisplayName)
            .ConfigureAwait(false);
    }
}
