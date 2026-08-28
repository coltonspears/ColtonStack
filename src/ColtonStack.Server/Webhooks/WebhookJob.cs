using ColtonStack.Contracts;

namespace ColtonStack.Server.Webhooks;

/// <summary>One pending outbound webhook event, queued when a message is saved.</summary>
public sealed record WebhookJob(string EventType, MessageDto Message);
