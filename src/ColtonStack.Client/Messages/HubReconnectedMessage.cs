namespace ColtonStack.Client.Messages;

/// <summary>
/// Published when the SignalR connection has been re-established after a drop — either a
/// healed automatic reconnect or the connect loop recovering from a hard outage. The chat
/// pane reacts by fetching only messages newer than what it already holds (afterId
/// catch-up), demonstrating bounded re-synchronization instead of a full reload.
/// </summary>
public sealed record HubReconnectedMessage;