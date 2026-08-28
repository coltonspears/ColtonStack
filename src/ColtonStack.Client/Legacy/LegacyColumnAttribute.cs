// THE OLD WORLD — for demo contrast only. See LegacyTableAttribute.cs.

#pragma warning disable RS0030 // Banned APIs are the point of this file.

namespace ColtonStack.Client.Legacy;

/// <summary>Maps a property to a database column by string name — the compile has no idea if it matches.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class LegacyColumnAttribute(string columnName) : Attribute
{
    public string ColumnName { get; } = columnName;
}
