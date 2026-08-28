namespace ColtonStack.Contracts;

/// <summary>Request body for creating a channel.</summary>
public sealed record CreateChannelRequest(string Name, string Topic);
