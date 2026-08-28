namespace ColtonStack.Contracts;

/// <summary>Request body for registering a webhook.</summary>
public sealed record RegisterWebhookRequest(string Url, string? Secret);
