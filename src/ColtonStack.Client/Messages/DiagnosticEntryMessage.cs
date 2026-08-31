using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.Messages;

/// <summary>
/// One log entry, published by the diagnostics logger provider onto the messenger. The
/// in-app diagnostics panel subscribes; nothing else knows logging goes anywhere but the
/// providers configured at startup. Immutable record — like every other message.
/// </summary>
public sealed record DiagnosticEntryMessage(
    LogLevel Level,
    string Category,
    string Message,
    DateTimeOffset TimestampUtc)
{
    /// <summary>Short local time for the log row.</summary>
    public string TimeText => TimestampUtc.LocalDateTime.ToString("HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture);
}