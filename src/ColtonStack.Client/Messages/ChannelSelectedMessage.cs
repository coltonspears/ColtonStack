using ColtonStack.Contracts;

namespace ColtonStack.Client.Messages;

/// <summary>Broadcast when the user selects a channel in the sidebar; the chat pane and hub group switch react to it.</summary>
public sealed record ChannelSelectedMessage(ChannelSummaryDto? Channel);
