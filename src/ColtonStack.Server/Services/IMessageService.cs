using ColtonStack.Contracts;

namespace ColtonStack.Server.Services;

/// <summary>Reads channels and messages, and owns the save pipeline for new messages.</summary>
public interface IMessageService
{
    /// <summary>Fetches messages for a channel; pass <paramref name="afterId"/> to fetch only newer rows (incremental catch-up).</summary>
    Task<IReadOnlyList<MessageDto>> GetRecentAsync(long channelId, long afterId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// The full save pipeline for one message posted by the current user: persist with Dapper →
    /// audit → broadcast over SignalR → enqueue webhook delivery. The dumb <see cref="MessageDto"/>
    /// flows through smart services; it never saves itself. Extensions post rich content by
    /// passing an <paramref name="attachment"/> — the pipeline is closed to modification.
    /// </summary>
    Task<MessageDto> SendAsync(long channelId, string text, MessageAttachmentDto? attachment, CancellationToken cancellationToken);

    /// <summary>Same pipeline, but posting as a specific (non-self) user — used by the chat-activity simulator.</summary>
    Task<MessageDto> SendAsUserAsync(long channelId, long userId, string text, CancellationToken cancellationToken);
}
