using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using System.IO;

namespace TicketeraOnline.Api.Helpers;

/// <summary>
/// Global console formatter that redacts emitted log messages through
/// <see cref="LogRedactor.RedactMessage"/> before writing to stdout.
/// This protects every <c>_logger.*</c> call site automatically instead of
/// requiring per-call wrapping.
/// </summary>
public sealed class RedactingConsoleFormatter : ConsoleFormatter
{
    public RedactingConsoleFormatter() : base("redacted")
    {
    }

    public override void Write<TState>(
        in LogEntry<TState> logEntry,
        IExternalScopeProvider? scopeProvider,
        TextWriter textWriter)
    {
        try
        {
            var originalMessage = logEntry.Formatter(logEntry.State, logEntry.Exception);
            var redactedMessage = LogRedactor.RedactMessage(originalMessage);

            textWriter.WriteLine($"[{logEntry.LogLevel}] {logEntry.Category}[{logEntry.EventId.Id}]: {redactedMessage}");

            if (logEntry.Exception != null)
            {
                textWriter.WriteLine(LogRedactor.RedactMessage(logEntry.Exception.ToString()));
            }
        }
        catch
        {
            // Logging must never fail the request.
        }
    }
}
