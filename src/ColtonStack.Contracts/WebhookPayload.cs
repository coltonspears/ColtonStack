namespace ColtonStack.Contracts;

/// <summary>
/// The payload POSTed to every webhook when a message is saved.
/// Sent as JSON, HMAC-signed via <c>X-ColtonStack-Signature</c>.
/// </summary>
public sealed record WebhookPayload(
    string EventType,
    MessageDto Message,
    DateTimeOffset OccurredAtUtc);
