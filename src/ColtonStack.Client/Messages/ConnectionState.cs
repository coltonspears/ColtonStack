namespace ColtonStack.Client.Messages;

/// <summary>High-level connectivity of the WPF client to the server (SignalR + HTTP surfaces).</summary>
public enum ConnectionState
{
    Connecting,
    Connected,
    Reconnecting,
    Disconnected,
}
