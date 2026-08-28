namespace ColtonStack.Server.Webhooks;

/// <summary>Internal projection of a webhook row — includes the signing secret, unlike the public DTO.</summary>
public sealed record WebhookTarget(long Id, string Url, string? Secret);
