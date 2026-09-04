using ColtonStack.Client.Messages;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;

namespace ColtonStack.Client.Services;

/// <summary>
/// Bridges <see cref="ILogger"/> into the app's messenger so the diagnostics panel can show
/// live application logs — retries, hub reconnects, audit failures — without any component
/// knowing about the panel. One provider, one boundary, exactly like UiThreadMessenger.
/// </summary>
public sealed class DiagnosticsLoggerProvider(IMessenger messenger) : ILoggerProvider
{
    /// <summary>Information and above reaches the panel; Debug/Trace stay in the Debug output.</summary>
    private const LogLevel MinimumVisibleLevel = LogLevel.Information;

    public ILogger CreateLogger(string categoryName) => new DiagnosticsLogger(categoryName, messenger, MinimumVisibleLevel);

    public void Dispose() => GC.SuppressFinalize(this);

    private sealed class DiagnosticsLogger(string category, IMessenger messenger, LogLevel minimumLevel) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= minimumLevel && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (exception is not null)
            {
                // Type.ToString() rather than Type.Name: the latter is a System.Reflection
                // member, and this assembly's architecture tests keep reflection out of Services.
                var typeName = exception.GetType().ToString();
                message = $"{message}\n{typeName[(typeName.LastIndexOf('.') + 1)..]}: {exception.Message}";
            }

            // UiThreadMessenger marshals this to the UI thread when logged off-thread.
            messenger.Send(new DiagnosticEntryMessage(logLevel, Shorten(category), message, DateTimeOffset.Now));
        }

        /// <summary>"ColtonStack.Client.Services.ChatHubClient" → "Services.ChatHubClient".</summary>
        private static string Shorten(string category)
        {
            var parts = category.Split('.');
            var take = Math.Min(2, parts.Length);
            return string.Join('.', parts.TakeLast(take));
        }
    }
}