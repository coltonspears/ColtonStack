// THE OLD WORLD — for demo contrast only. See LegacyTableAttribute.cs.

#pragma warning disable RS0030 // Banned APIs are the point of this file.

using System.Text;

namespace ColtonStack.Client.Legacy;

/// <summary>
/// Builds SQL by walking attributes at runtime. Nothing is checked against a real schema until
/// the statement executes — which is exactly when a renamed column or a typo surfaces.
/// (Deliberately does not open a connection; the point is the shape of the code, not its output.)
/// </summary>
public static class LegacySqlMapper
{
    public static string BuildInsert(string tableName, IEnumerable<string> columns)
    {
        var builder = new StringBuilder();
        builder.Append("INSERT INTO ").Append(tableName).Append(" (");

        var columnList = string.Join(", ", columns);
        var valueList = string.Join(", ", columns.Select((_, index) => "@p" + index));

        builder.Append(columnList).Append(") VALUES (").Append(valueList).Append(");");
        return builder.ToString();
    }

    /// <summary>Synchronous execution — the call sites ended up blocking UI threads for years.</summary>
    public static int Execute(string sql, IEnumerable<object?> parameters)
    {
        LegacySqlLog.Trace(sql, parameters.Count());
        return 0; // pretend
    }

    public static void ExecuteAuditTrail(string table, string actor, string state)
    {
        LegacySqlLog.Trace($"-- audit trail: table={table} actor={actor} state={state}");
    }
}
