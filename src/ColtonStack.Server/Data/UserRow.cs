using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Data;

/// <summary>
/// The <c>Users</c> table. Dapper.Contrib derives INSERT/SELECT/UPDATE/DELETE from this shape,
/// so a typo'd property is a compile error at the call site instead of a runtime surprise.
/// </summary>
[Table("Users")]
public sealed class UserRow
{
    [Key]
    public long Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string AvatarColor { get; set; } = string.Empty;

    public bool IsSelf { get; set; }
}
