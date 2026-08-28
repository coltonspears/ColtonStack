using System.Globalization;
using ColtonStack.Contracts;

namespace ColtonStack.Server.Services;

/// <summary>
/// SQLite-native row shape for channel summaries: counts are 64-bit and timestamps come back
/// as ISO text. <see cref="ChannelService"/> maps rows to the public DTO explicitly — no
/// reflection conventions, no DTO reshaped around database quirks.
/// </summary>
public sealed record ChannelSummaryRow(
    long Id,
    string Name,
    string Topic,
    long MessageCount,
    long LastMessageId,
    string LastMessageAtUtc,
    string? LastMessagePreview)
{
    public ChannelSummaryDto ToDto() => new(
        Id,
        Name,
        Topic,
        MessageCount: checked((int)MessageCount),
        LastMessageId: LastMessageId == 0 ? null : LastMessageId,
        LastMessageAtUtc: string.IsNullOrEmpty(LastMessageAtUtc)
            ? null
            : DateTimeOffset.Parse(LastMessageAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        LastMessagePreview);
}
