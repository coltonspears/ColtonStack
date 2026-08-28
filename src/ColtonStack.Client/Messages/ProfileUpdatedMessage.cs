using ColtonStack.Contracts;

namespace ColtonStack.Client.Messages;

/// <summary>Published after the current user's profile is saved; the people pane refreshes its row.</summary>
public sealed record ProfileUpdatedMessage(UserDto User);
