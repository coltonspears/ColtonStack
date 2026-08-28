using ColtonStack.Contracts;

namespace ColtonStack.Client.Messages;

/// <summary>
/// Broadcast on <see cref="CommunityToolkit.Mvvm.Messaging.IMessenger"/> when the SignalR
/// connection delivers a saved message. Multiple view models subscribe — the chat pane appends
/// it, the sidebar bumps the unread badge — one event, many decoupled consumers.
/// </summary>
public sealed record MessagePostedMessage(MessageDto Message);
