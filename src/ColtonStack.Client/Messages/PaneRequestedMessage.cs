namespace ColtonStack.Client.Messages;

/// <summary>Asks the shell to activate a sidebar pane by id — how the palette navigates without holding the shell.</summary>
public sealed record PaneRequestedMessage(string PaneId);
