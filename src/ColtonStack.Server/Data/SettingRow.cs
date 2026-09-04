using Dapper.Contrib.Extensions;

namespace ColtonStack.Server.Data;

/// <summary>The <c>Settings</c> table: one row per dotted key. <c>[ExplicitKey]</c> because the key is the string itself, not an autoincrement.</summary>
[Table("Settings")]
public sealed class SettingRow
{
    [ExplicitKey]
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
