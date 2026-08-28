namespace ColtonStack.Server.Services;

/// <summary>The requested channel does not exist.</summary>
public sealed class ChannelNotFoundException(long channelId)
    : Exception($"Channel {channelId} does not exist.");
