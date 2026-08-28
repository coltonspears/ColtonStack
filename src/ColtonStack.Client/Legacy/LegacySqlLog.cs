// THE OLD WORLD — for demo contrast only. See LegacyTableAttribute.cs.

namespace ColtonStack.Client.Legacy;

/// <summary>File-scoped logger so the legacy mapper stays self-contained.</summary>
public static class LegacySqlLog
{
    public static void Trace(string message) => System.Diagnostics.Debug.WriteLine($"[legacy-sql] {message}");

    public static void Trace(string message, int parameterCount) =>
        System.Diagnostics.Debug.WriteLine($"[legacy-sql] {message} ({parameterCount} parameters)");
}
