namespace ColtonStack.Server.Services;

/// <summary>A channel with the requested name already exists.</summary>
public sealed class DuplicateChannelException(string name)
    : Exception($"A channel named '{name}' already exists.");
