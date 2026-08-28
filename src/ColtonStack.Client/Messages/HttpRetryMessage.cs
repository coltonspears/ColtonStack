namespace ColtonStack.Client.Messages;

/// <summary>Broadcast by the HTTP resilience pipeline every time a failed request is retried.</summary>
public sealed record HttpRetryMessage(int Attempt, string Detail);
