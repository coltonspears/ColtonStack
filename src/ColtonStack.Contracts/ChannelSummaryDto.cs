namespace ColtonStack.Contracts;

/// <summary>
/// A channel with everything the sidebar needs (activity preview, message count),
/// computed server-side in one query — clients never aggregate over raw rows.
/// </summary>
public sealed record ChannelSummaryDto(
    long Id,
    string Name,
    string Topic,
    int MessageCount,
    long? LastMessageId,
    DateTimeOffset? LastMessageAtUtc,
    string? LastMessagePreview);
