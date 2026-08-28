namespace ColtonStack.Contracts;

/// <summary>A registered webhook endpoint that receives chat events.</summary>
public sealed record WebhookRegistrationDto(
    long Id,
    string Url,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
