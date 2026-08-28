using ColtonStack.Contracts;

namespace ColtonStack.Server.Services;

/// <summary>Channel reads and creation, including sidebar summaries and audit/broadcast side effects.</summary>
public interface IChannelService
{
    Task<IReadOnlyList<ChannelSummaryDto>> GetSummariesAsync(CancellationToken cancellationToken);

    /// <summary>Creates a channel, audits it and broadcasts it to all connected clients.</summary>
    Task<ChannelSummaryDto> CreateAsync(CreateChannelRequest request, CancellationToken cancellationToken);
}
