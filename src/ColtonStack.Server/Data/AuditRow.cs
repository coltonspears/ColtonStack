using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Data;

/// <summary>The <c>AuditLog</c> table — see <see cref="UserRow"/> for how these row classes work.</summary>
[Table("AuditLog")]
public sealed class AuditRow
{
    [Key]
    public long Id { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public long EntityId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string Actor { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }

    public string? PayloadJson { get; set; }
}
