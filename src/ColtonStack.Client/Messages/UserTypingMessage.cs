namespace ColtonStack.Client.Messages;

/// <summary>Broadcast when someone is typing in a channel (pushed over SignalR, not stored anywhere).</summary>
public sealed record UserTypingMessage(long ChannelId, string UserDisplayName);
