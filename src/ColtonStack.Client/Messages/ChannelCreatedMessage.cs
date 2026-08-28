using ColtonStack.Contracts;

namespace ColtonStack.Client.Messages;

/// <summary>Broadcast when another client (or this one) created a channel.</summary>
public sealed record ChannelCreatedMessage(ChannelSummaryDto Channel);
