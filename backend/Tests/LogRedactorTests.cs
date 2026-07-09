using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Moq;
using System.Text;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for the defensive log redaction helper and the global redacting console formatter.
/// Validates R1-1, R1-2, R1-3 findings from the 4R review.
/// </summary>
public class LogRedactorTests
{
    #region R1-3: Sensitive key denylist

    [Theory]
    [InlineData("signature")]
    [InlineData("x-signature")]
    [InlineData("x_signature")]
    [InlineData("bearer")]
    [InlineData("pan")]
    [InlineData("cardholder")]
    [InlineData("external_reference")]
    [InlineData("qr_code_data")]
    [InlineData("qrdata")]
    [InlineData("refresh-token")]
    [InlineData("refresh_token")]
    [InlineData("email")]
    [InlineData("dni")]
    [InlineData("phone")]
    [InlineData("document")]
    [InlineData("documentnumber")]
    [InlineData("document_number")]
    public void RedactMessage_DenylistsSensitiveKey(string key)
    {
        var secret = "super-secret-value-123";
        var message = $"Request failed: {key}={secret}, other=ok";

        var redacted = LogRedactor.RedactMessage(message);

        Assert.DoesNotContain(secret, redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Fact]
    public void SensitiveKeys_DoesNotContainDuplicateRefreshToken()
    {
        var keys = LogRedactor.SensitiveKeys;

        Assert.Contains("refresh_token", keys);
        Assert.Contains("refresh-token", keys);
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    #endregion

    #region R1-3: Regex failover

    [Theory]
    [InlineData("Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    [InlineData("Authorization: Bearer abcdef1234567890abcdef1234567890")]
    [InlineData("token Bearer supersecrettokenvaluewithmanycharacters")]
    public void RedactMessage_RedactsBearerToken(string message)
    {
        var redacted = LogRedactor.RedactMessage(message);

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", redacted);
        Assert.DoesNotContain("abcdef1234567890abcdef1234567890", redacted);
        Assert.DoesNotContain("supersecrettokenvaluewithmanycharacters", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Theory]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjMifQ.SflKxwRJSMeKKF2QT4fwpMe")]
    [InlineData("Value: eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9")]
    public void RedactMessage_RedactsJwtPrefix(string message)
    {
        var redacted = LogRedactor.RedactMessage(message);

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9", redacted);
        Assert.DoesNotContain("eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Theory]
    [InlineData("secret=abcdef0123456789abcdef0123456789abcdef0123456789")]
    [InlineData("apiKey=abcdef0123456789abcdef0123456789")]
    public void RedactMessage_RedactsLongHexSecret(string message)
    {
        var redacted = LogRedactor.RedactMessage(message);

        Assert.DoesNotContain("abcdef0123456789abcdef0123456789", redacted);
        Assert.Contains("[REDACTED]", redacted);
    }

    [Theory]
    [InlineData("eventId=550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("correlationId=abc123")]
    [InlineData("page=2")]
    public void RedactMessage_DoesNotRedactNonSensitiveKey(string message)
    {
        var redacted = LogRedactor.RedactMessage(message);

        Assert.Equal(message, redacted);
    }

    #endregion

    #region R1-2: DNI hashing

    [Fact]
    public void HashIdentifier_ReturnsStableTruncatedHash()
    {
        var dni = "12345678";

        var hash1 = LogRedactor.HashIdentifier(dni);
        var hash2 = LogRedactor.HashIdentifier(dni);

        Assert.Equal(hash1, hash2);
        Assert.Equal(12, hash1.Length);
        Assert.DoesNotContain(dni, hash1);
    }

    [Fact]
    public void HashIdentifier_DifferentInputsProduceDifferentHashes()
    {
        var hash1 = LogRedactor.HashIdentifier("11111111");
        var hash2 = LogRedactor.HashIdentifier("22222222");

        Assert.NotEqual(hash1, hash2);
    }

    #endregion

    #region R1-1: Global redaction formatter

    [Fact]
    public void RedactingConsoleFormatter_RedactsMessageBeforeWriting()
    {
        var formatter = new RedactingConsoleFormatter();
        var message = "Request failed: token=super-secret-token-123";
        var entry = new LogEntry<object>(
            LogLevel.Information,
            "TestCategory",
            new EventId(1),
            state: new object(),
            exception: null,
            formatter: (state, ex) => message);

        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);

        formatter.Write(in entry, scopeProvider: null, writer);

        var output = sb.ToString();
        Assert.DoesNotContain("super-secret-token-123", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void RedactingConsoleFormatter_RedactsBearerTokenInFreeFormMessage()
    {
        var formatter = new RedactingConsoleFormatter();
        var token = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjMifQ.SflKxwRJSMeKKF2QT4fwpMe";
        var message = $"Authorization: Bearer {token}";
        var entry = new LogEntry<object>(
            LogLevel.Warning,
            "TestCategory",
            new EventId(2),
            state: new object(),
            exception: null,
            formatter: (state, ex) => message);

        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);

        formatter.Write(in entry, scopeProvider: null, writer);

        var output = sb.ToString();
        Assert.DoesNotContain(token, output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void RedactingConsoleFormatter_SwallowsFormatterException()
    {
        var formatter = new RedactingConsoleFormatter();
        var entry = new LogEntry<object>(
            LogLevel.Information,
            "TestCategory",
            new EventId(3),
            state: new object(),
            exception: null,
            formatter: (state, ex) => throw new InvalidOperationException("Formatter failure"));

        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);

        var exception = Record.Exception(() => formatter.Write(in entry, scopeProvider: null, writer));

        Assert.Null(exception);
    }

    #endregion

    #region R1-NF-1: Inline email redaction in controller-rendered messages

    /// <summary>
    /// Verifies that a real TicketController lookup log emission, which places the
    /// email inline in the rendered message (not as key=value), does not contain the
    /// raw email after passing through the redacting console formatter.
    /// </summary>
    [Fact]
    public void RedactingConsoleFormatter_RedactsInlineEmailInRenderedMessage()
    {
        var email = "user@example.com";
        var dni = "12345678";
        var collectingLogger = new CollectingLogger<TicketController>();

        var ticketService = new Mock<ITicketService>();
        ticketService
            .Setup(s => s.LookupTicketsAsync(email, dni))
            .ReturnsAsync(new List<Ticket>());

        var auditService = new Mock<IAuditLogService>();
        var controller = new TicketController(
            ticketService.Object,
            collectingLogger,
            auditService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        Task.Run(() => controller.LookupTickets(email, dni)).Wait();

        var emitted = collectingLogger.Entries
            .FirstOrDefault(e => e.Message.Contains("Ticket lookup request", StringComparison.Ordinal));
        Assert.NotNull(emitted);

        var formatter = new RedactingConsoleFormatter();
        var entry = new LogEntry<object>(
            emitted.LogLevel,
            "TicketController",
            emitted.EventId,
            state: new object(),
            exception: emitted.Exception,
            formatter: (state, ex) => emitted.Message);

        var sb = new StringBuilder();
        using var writer = new StringWriter(sb);
        formatter.Write(in entry, scopeProvider: null, writer);

        var output = sb.ToString();
        Assert.DoesNotContain(email, output);
    }

    #endregion

    #region Test Doubles

    /// <summary>
    /// Captures log entries emitted by controller tests in this fixture.
    /// </summary>
    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(
        LogLevel LogLevel,
        EventId EventId,
        Exception? Exception,
        string Message);

    #endregion
}
