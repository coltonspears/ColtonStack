using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Data;

/// <summary>The <c>Messages</c> table — see <see cref="UserRow"/> for how these row classes work.</summary>
[Table("Messages")]
public sealed class MessageRow
{
    [Key]
    public long Id { get; set; }

    public long ChannelId { get; set; }

    public long UserId { get; set; }

    public string Text { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Attachment discriminator (e.g. "pokemon"); null for plain text messages.</summary>
    public string? AttachmentKind { get; set; }

    /// <summary>The extension-owned payload, stored as JSON text the core never parses.</summary>
    public string? AttachmentJson { get; set; }
}
