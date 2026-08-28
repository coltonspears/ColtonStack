using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Data;

/// <summary>The <c>Webhooks</c> table — see <see cref="UserRow"/> for how these row classes work.</summary>
[Table("Webhooks")]
public sealed class WebhookRow
{
    [Key]
    public long Id { get; set; }

    public string Url { get; set; } = string.Empty;

    /// <summary>HMAC signing secret; never exposed through the public DTO.</summary>
    public string? Secret { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
