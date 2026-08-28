using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Data;

/// <summary>The <c>Channels</c> table — see <see cref="UserRow"/> for how these row classes work.</summary>
[Table("Channels")]
public sealed class ChannelRow
{
    [Key]
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Topic { get; set; } = string.Empty;
}
