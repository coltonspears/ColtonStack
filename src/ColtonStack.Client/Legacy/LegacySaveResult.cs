// THE OLD WORLD — for demo contrast only. See LegacyTableAttribute.cs.

namespace ColtonStack.Client.Legacy;

/// <summary>What a legacy save produced. Mostly ceremony.</summary>
public sealed record LegacySaveResult(string Table, int ColumnCount, bool Audited);
