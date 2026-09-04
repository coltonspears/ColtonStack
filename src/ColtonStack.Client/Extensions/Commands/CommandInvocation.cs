namespace ColtonStack.Client.Extensions.Commands;

/// <summary>Everything a command receives when it runs: the typed argument (empty for palette runs) and the current channel, if any.</summary>
public sealed record CommandInvocation(string Argument, long? ChannelId, CancellationToken CancellationToken);
