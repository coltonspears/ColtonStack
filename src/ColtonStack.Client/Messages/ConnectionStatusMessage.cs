namespace ColtonStack.Client.Messages;

/// <summary>Broadcast whenever overall client connectivity changes (SignalR lifecycle + notable HTTP failures).</summary>
public sealed record ConnectionStatusMessage(ConnectionState State, string Detail);
