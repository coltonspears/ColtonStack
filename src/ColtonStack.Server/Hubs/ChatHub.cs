using ColtonStack.Contracts;
using ColtonStack.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace ColtonStack.Server.Hubs;

/// <summary>
/// Real-time push hub. Typed via <c>Hub&lt;IChatHubClient&gt;</c>: every broadcast below is an
/// interface method call, so the wire contract is checked by the compiler — no event-name strings.
/// </summary>
public sealed class ChatHub(IUserService users) : Hub<IChatHubClient>
{
    public static string GroupNameFor(long channelId) => $"channel-{channelId}";

    public Task JoinChannelAsync(long channelId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupNameFor(channelId));

    public Task LeaveChannelAsync(long channelId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupNameFor(channelId));

    /// <summary>
    /// Relays a typing notification to everyone else in the channel. The server resolves who is
    /// typing from the workspace profile — the client sends only the channel, so it can never
    /// announce a stale name. No persistence — pure push.
    /// </summary>
    public async Task NotifyTypingAsync(long channelId)
    {
        var self = await users.GetSelfAsync(Context.ConnectionAborted).ConfigureAwait(false);
        await Clients.OthersInGroup(GroupNameFor(channelId))
            .UserTypingAsync(channelId, self.DisplayName)
            .ConfigureAwait(false);
    }
}
