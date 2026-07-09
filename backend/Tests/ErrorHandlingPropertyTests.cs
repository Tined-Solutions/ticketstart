using FsCheck;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Middleware;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;
using GenStatic = FsCheck.Fluent.Gen;
using PropStatic = FsCheck.Fluent.Prop;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Property-based tests for global error handling, structured logging, and audit logging.
/// Validates Requirements 15.5, 15.6, 16.1, 16.2, 16.3, 16.5, 16.6, 16.7
/// </summary>
public class ErrorHandlingPropertyTests
{
    #region Property 44: Database Connection Failure Handling

    /// <summary>
    /// Property 44: Database Connection Failure Handling
    /// For any database connection failure, the system SHALL handle it gracefully and return an appropriate error response without crashing.
    /// **Validates: Requirements 15.5**
    /// </summary>
    [Fact]
    public void Property44_DbException_HandlerReturns500WithoutCrashing()
    {
        var prop = PropStatic.ForAll(
            new CustomArbitrary<DbExceptionScenario>(GenDbExceptionScenario()),
            scenario =>
            {
                var logger = new CollectingLogger<GlobalExceptionHandler>();
                var handler = new GlobalExceptionHandler(logger);
                var context = CreateHttpContext("/api/events", "GET");

                var handled = handler.TryHandleAsync(context, scenario.Exception, CancellationToken.None).AsTask().Result;
                var problem = ReadResponseBody<ProblemDetails>(context);
                var expectedMessage = "An unexpected error occurred. Please try again later.";

                return handled
                    && context.Response.StatusCode == StatusCodes.Status500InternalServerError
                    && problem?.Status == StatusCodes.Status500InternalServerError
                    && problem?.Detail == expectedMessage;
            });

        Check.QuickThrowOnFailure(prop);
    }

    #endregion

    #region Property 45: Database Error Logging

    /// <summary>
    /// Property 45: Database Error Logging
    /// For any database error, the system SHALL log the error with timestamp, context, and error details.
    /// **Validates: Requirements 15.6**
    /// </summary>
    [Fact]
    public void Property45_DbException_LogsErrorWithContext()
    {
        var prop = PropStatic.ForAll(
            new CustomArbitrary<DbExceptionScenario>(GenDbExceptionScenario()),
            scenario =>
            {
                var logger = new CollectingLogger<GlobalExceptionHandler>();
                var handler = new GlobalExceptionHandler(logger);
                var context = CreateHttpContext("/api/reservations", "POST");

                handler.TryHandleAsync(context, scenario.Exception, CancellationToken.None).AsTask().Wait();

                var entry = logger.Entries.FirstOrDefault(e => e.LogLevel == LogLevel.Error);
                if (entry == null)
                    return false;

                return entry.Message.Contains("DbException", StringComparison.OrdinalIgnoreCase)
                    || (entry.State?.Any(kv => kv.Value?.ToString()?.Contains("DbException", StringComparison.OrdinalIgnoreCase) == true) ?? false);
            });

        Check.QuickThrowOnFailure(prop);
    }

    #endregion

    #region Property 46: Error Logging Format

    /// <summary>
    /// Property 46: Error Logging Format
    /// For any error, the system SHALL log it with timestamp, context, and stack trace.
    /// **Validates: Requirements 16.1**
    /// </summary>
    [Fact]
    public void Property46_Exception_LogsStructuredFields()
    {
        var prop = PropStatic.ForAll(
            new CustomArbitrary<ExceptionScenario>(GenExceptionScenario()),
            scenario =>
            {
                var logger = new CollectingLogger<GlobalExceptionHandler>();
                var handler = new GlobalExceptionHandler(logger);
                var context = CreateHttpContext("/api/payments/webhook", "POST");

                handler.TryHandleAsync(context, scenario.Exception, CancellationToken.None).AsTask().Wait();

                var entry = logger.Entries.FirstOrDefault(e => e.LogLevel == LogLevel.Error);
                if (entry == null)
                    return false;

                var keys = entry.State?.Select(kv => kv.Key).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
                return keys.Contains("ExceptionType")
                    && keys.Contains("Path")
                    && keys.Contains("Method")
                    && keys.Contains("StackTrace")
                    && entry.Exception != null;
            });

        Check.QuickThrowOnFailure(prop);
    }

    #endregion

    #region Property 47: HTTP Status Code Correctness

    /// <summary>
    /// Property 47: HTTP Status Code Correctness
    /// For any error condition, the system SHALL return the appropriate HTTP status code (400 for validation errors, 401 for authentication errors, 403 for authorization errors, 404 for not found, 409 for conflicts, 500 for server errors).
    /// **Validates: Requirements 16.2**
    /// </summary>
    [Theory]
    [InlineData(typeof(ArgumentException), StatusCodes.Status400BadRequest, "VALIDATION_ERROR")]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized, "UNAUTHORIZED")]
    [InlineData(typeof(ForbiddenException), StatusCodes.Status403Forbidden, "FORBIDDEN")]
    [InlineData(typeof(KeyNotFoundException), StatusCodes.Status404NotFound, "NOT_FOUND")]
    [InlineData(typeof(DbUpdateConcurrencyException), StatusCodes.Status409Conflict, "CONFLICT")]
    [InlineData(typeof(TestDbException), StatusCodes.Status500InternalServerError, "INTERNAL_ERROR")]
    [InlineData(typeof(Exception), StatusCodes.Status500InternalServerError, "INTERNAL_ERROR")]
    public void Property47_StatusCodeMapping_MatchesSpecMatrix(Type exceptionType, int expectedStatusCode, string expectedCode)
    {
        var logger = new CollectingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var context = CreateHttpContext("/api/test", "GET");
        var exception = CreateExceptionInstance(exceptionType);

        handler.TryHandleAsync(context, exception, CancellationToken.None).AsTask().Wait();
        var problem = ReadResponseBody<ProblemDetails>(context);

        Assert.Equal(expectedStatusCode, context.Response.StatusCode);
        Assert.NotNull(problem);
        Assert.Equal(expectedStatusCode, problem!.Status);
        Assert.Equal(expectedCode, problem.Title);
    }

    [Fact]
    public void Property47b_OperationCanceled_Returns499AndLogsInformation()
    {
        var logger = new CollectingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var context = CreateHttpContext("/api/test", "GET");
        var exception = new OperationCanceledException("client disconnected");

        handler.TryHandleAsync(context, exception, CancellationToken.None).AsTask().Wait();

        Assert.Equal(499, context.Response.StatusCode);
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Information);
        Assert.DoesNotContain(logger.Entries, e => e.LogLevel == LogLevel.Error);
    }

    [Fact]
    public void Property47c_HandlerSelfProtection_CatchesLoggerFailureAndReturns500()
    {
        var logger = new ThrowingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var context = CreateHttpContext("/api/test", "GET");
        context.Response.Body = new MemoryStream();
        var exception = new InvalidOperationException("something failed");

        handler.TryHandleAsync(context, exception, CancellationToken.None).AsTask().Wait();

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        var body = ReadRawResponseBody(context);
        Assert.Contains("INTERNAL_ERROR", body);
        Assert.Contains("An internal error occurred", body);
    }

    [Fact]
    public void Property47d_OperationCanceled_WithCancelledToken_ReturnsTrueWithoutWriting()
    {
        var logger = new CollectingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var context = CreateHttpContext("/api/test", "GET");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var exception = new OperationCanceledException();

        var handled = handler.TryHandleAsync(context, exception, cts.Token).AsTask().Result;

        Assert.True(handled);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.DoesNotContain(logger.Entries, e => e.LogLevel == LogLevel.Error);
    }

    [Fact]
    public void Property47e_HandlerSelfProtection_ResponseAlreadyStarted_ReturnsTrueWithoutWriting()
    {
        var logger = new CollectingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var context = CreateStartedHttpContext("/api/test", "GET");
        var exception = new InvalidOperationException("something failed");

        var handled = handler.TryHandleAsync(context, exception, CancellationToken.None).AsTask().Result;

        Assert.True(handled);
        Assert.Equal(0, context.Response.Body.Length);
    }

    #endregion

    #region Property 48: User-Friendly Error Messages

    /// <summary>
    /// Property 48: User-Friendly Error Messages
    /// For any error returned to the frontend, the error message SHALL be user-friendly and not expose sensitive system details.
    /// **Validates: Requirements 16.3**
    /// </summary>
    [Fact]
    public void Property48_SensitiveExceptionMessage_ResponseDoesNotExposeDetails()
    {
        var prop = PropStatic.ForAll(
            new CustomArbitrary<SensitiveMessageScenario>(GenSensitiveMessageScenario()),
            scenario =>
            {
                var logger = new CollectingLogger<GlobalExceptionHandler>();
                var handler = new GlobalExceptionHandler(logger);
                var context = CreateHttpContext("/api/admin/users", "GET");
                var exception = new InvalidOperationException($"Internal DB failure: {scenario.SensitiveMessage}");

                handler.TryHandleAsync(context, exception, CancellationToken.None).AsTask().Wait();
                var problem = ReadResponseBody<ProblemDetails>(context);
                var body = problem?.Detail ?? string.Empty;
                var expectedMessage = "An unexpected error occurred. Please try again later.";

                return body == expectedMessage
                    && !body.Contains(scenario.SensitiveMessage)
                    && !body.Contains("Internal DB failure");
            });

        Check.QuickThrowOnFailure(prop);
    }

    #endregion

    #region Property 49: Payment Webhook Audit Logging

    /// <summary>
    /// Property 49: Payment Webhook Audit Logging
    /// For any payment webhook received, the system SHALL log the webhook event with timestamp, payload, and processing result.
    /// **Validates: Requirements 16.5**
    /// </summary>
    [Fact]
    public void Property49_PaymentWebhook_LogsAuditEntry()
    {
        var prop = PropStatic.ForAll(
            new CustomArbitrary<WebhookScenario>(GenWebhookScenario()),
            scenario =>
            {
                var logger = new CollectingLogger<PaymentController>();
                var audit = new FakeAuditLogService();
                var paymentService = new Mock<IPaymentService>();
                paymentService
                    .Setup(s => s.ProcessWebhookAsync(scenario.Payload, scenario.Signature))
                    .ReturnsAsync(scenario.Result);

                var controller = new PaymentController(
                    paymentService.Object,
                    logger,
                    audit)
                {
                    ControllerContext = new ControllerContext
                    {
                        HttpContext = new DefaultHttpContext()
                    }
                };

                var actionResult = Task.Run(() => controller.Webhook(scenario.Payload, scenario.Signature)).Result;
                var auditEntry = audit.Contexts.FirstOrDefault();

                return audit.Contexts.Count == 1
                    && auditEntry?.Action == AuditActionType.ProcessWebhook
                    && auditEntry?.Resource == AuditResourceType.Payment
                    && auditEntry?.Details != null
                    && auditEntry.Details.Contains(scenario.Payload.PaymentId)
                    && auditEntry.Details.Contains(scenario.Result.Success.ToString());
            });

        Check.QuickThrowOnFailure(prop);
    }

    [Fact]
    public void Property49b_PaymentWebhook_AuditFailure_StillReturnsOkAndLogsError()
    {
        var logger = new CollectingLogger<PaymentController>();
        var audit = new FailingAuditLogService();
        var paymentService = new Mock<IPaymentService>();
        var payload = new WebhookPayload { PaymentId = "pay-123", ExternalReference = Guid.NewGuid().ToString(), Status = "approved" };
        var signature = "valid-signature";
        paymentService.Setup(s => s.ProcessWebhookAsync(payload, signature))
            .ReturnsAsync(new WebhookResult { Success = true, PaymentId = payload.PaymentId });

        var controller = new PaymentController(paymentService.Object, logger, audit)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = Task.Run(() => controller.Webhook(payload, signature)).Result;

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Error && e.Message.Contains("Audit logging failed"));
    }

    #endregion

    #region Property 50: QR Validation Audit Logging

    /// <summary>
    /// Property 50: QR Validation Audit Logging
    /// For any QR code validation attempt, the system SHALL log the attempt with timestamp, ticket ID, event ID, and validation result.
    /// **Validates: Requirements 16.6**
    /// </summary>
    [Fact]
    public void Property50_QrValidation_LogsAuditEntry()
    {
        var prop = PropStatic.ForAll(
            new CustomArbitrary<QrValidationScenario>(GenQrValidationScenario()),
            scenario =>
            {
                var logger = new CollectingLogger<TicketController>();
                var audit = new FakeAuditLogService();
                var ticketService = new Mock<ITicketService>();
                ticketService
                    .Setup(s => s.ValidateQRCodeAsync(scenario.Request.QRCodeData, scenario.Request.EventId))
                    .ReturnsAsync(scenario.Result);

                var controller = new TicketController(
                    ticketService.Object,
                    logger,
                    audit)
                {
                    ControllerContext = CreateStaffControllerContext(scenario.UserId)
                };

                var actionResult = Task.Run(() => controller.ValidateQRCode(scenario.Request)).Result;
                var auditEntry = audit.Contexts.FirstOrDefault();

                return audit.Contexts.Count == 1
                    && auditEntry?.UserId == scenario.UserId
                    && auditEntry?.Action == AuditActionType.ValidateQr
                    && auditEntry?.Resource == AuditResourceType.Ticket
                    && auditEntry?.Details != null
                    && auditEntry.Details.Contains(scenario.Request.EventId.ToString())
                    && auditEntry.Details.Contains(scenario.Result.IsValid.ToString());
            });

        Check.QuickThrowOnFailure(prop);
    }

    [Fact]
    public void Property50b_QrValidation_AuditFailure_StillReturnsOkAndLogsError()
    {
        var logger = new CollectingLogger<TicketController>();
        var audit = new FailingAuditLogService();
        var ticketService = new Mock<ITicketService>();
        var request = new ValidateQRCodeRequest { QRCodeData = "qr-data", EventId = Guid.NewGuid() };
        var ticketId = Guid.NewGuid();
        ticketService.Setup(s => s.ValidateQRCodeAsync(request.QRCodeData, request.EventId))
            .ReturnsAsync(new QRCodeValidationResult
            {
                IsValid = true,
                Ticket = new Ticket
                {
                    Id = ticketId,
                    EventId = request.EventId,
                    Event = new Event { Id = request.EventId, Name = "Test Event" },
                    TicketType = new TicketType { Id = Guid.NewGuid(), Name = "General" },
                    PurchaserEmail = "test@example.com"
                }
            });

        var controller = new TicketController(ticketService.Object, logger, audit)
        {
            ControllerContext = CreateStaffControllerContext(Guid.NewGuid())
        };

        var result = Task.Run(() => controller.ValidateQRCode(request)).Result;

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Error && e.Message.Contains("Audit logging failed"));
    }

    #endregion

    #region Property 51: Sensitive Data Protection in Logs

    /// <summary>
    /// Property 51: Sensitive Data Protection in Logs
    /// For any error or log entry, the system SHALL NOT expose sensitive information such as passwords, full payment details, or secret keys.
    /// **Validates: Requirements 16.7**
    /// </summary>
    [Fact]
    public void Property51_SensitiveQueryString_LogDoesNotExposeSecret()
    {
        var sensitiveKeys = LogRedactor.SensitiveKeys.ToArray();
        var prop = PropStatic.ForAll(
            new CustomArbitrary<SensitiveQueryScenario>(GenSensitiveQueryScenarioFromKeys(sensitiveKeys)),
            scenario =>
            {
                var logger = new CollectingLogger<GlobalExceptionHandler>();
                var handler = new GlobalExceptionHandler(logger);
                var context = CreateHttpContext("/api/auth/login", "POST", scenario.QueryString);
                var exception = new InvalidOperationException("Something went wrong");

                handler.TryHandleAsync(context, exception, CancellationToken.None).AsTask().Wait();

                var entry = logger.Entries.FirstOrDefault(e => e.LogLevel == LogLevel.Error);
                if (entry == null)
                    return false;

                return !entry.Message.Contains(scenario.SecretValue, StringComparison.Ordinal)
                    && (entry.State?.All(kv => kv.Value?.ToString()?.Contains(scenario.SecretValue) != true) ?? true);
            });

        Check.QuickThrowOnFailure(prop);
    }

    /// <summary>
    /// Property 51-negative: non-sensitive keys are preserved in logs.
    /// </summary>
    [Theory]
    [InlineData("eventId", "550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("page", "2")]
    [InlineData("correlationId", "abc-123")]
    public void Property51_Negative_NonSensitiveQueryString_IsPreservedInLog(string key, string value)
    {
        var logger = new CollectingLogger<GlobalExceptionHandler>();
        var handler = new GlobalExceptionHandler(logger);
        var context = CreateHttpContext("/api/test", "GET", $"?{key}={value}");
        var exception = new InvalidOperationException("Something went wrong");

        handler.TryHandleAsync(context, exception, CancellationToken.None).AsTask().Wait();

        var entry = logger.Entries.FirstOrDefault(e => e.LogLevel == LogLevel.Error);
        Assert.NotNull(entry);
        Assert.Contains(value, entry.Message);
    }

    #endregion

    #region Helpers

    private class CustomArbitrary<T> : Arbitrary<T>
    {
        public CustomArbitrary(Gen<T> generator)
        {
            Generator = generator;
        }

        public override Gen<T> Generator { get; }

        public override IEnumerable<T> Shrinker(T value) => Enumerable.Empty<T>();
    }

    private static DefaultHttpContext CreateHttpContext(string path, string method, string? queryString = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Method = method;
        if (!string.IsNullOrEmpty(queryString))
        {
            context.Request.QueryString = new QueryString(queryString);
        }
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static DefaultHttpContext CreateStartedHttpContext(string path, string method)
    {
        var body = new MemoryStream();
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature
        {
            Method = method,
            Path = path,
            PathBase = "",
            QueryString = "",
            RawTarget = path,
            Scheme = "http",
            Headers = new HeaderDictionary(),
            Body = Stream.Null
        });
        features.Set<IHttpResponseFeature>(new StartedResponseFeature { Body = body });
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(body));
        var context = new DefaultHttpContext(features);
        return context;
    }

    private static ControllerContext CreateStaffControllerContext(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, UserRole.Staff.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }


    private static T? ReadResponseBody<T>(HttpContext context) where T : class
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = reader.ReadToEnd();
        if (string.IsNullOrWhiteSpace(json))
            return null;
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static string ReadRawResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return reader.ReadToEnd();
    }

    private static Exception CreateExceptionInstance(Type exceptionType)
    {
        if (exceptionType == typeof(TestDbException))
            return new TestDbException("sample message", 1);

        return (Exception)Activator.CreateInstance(exceptionType, "sample message")!;
    }

    private static Gen<DbExceptionScenario> GenDbExceptionScenario()
    {
        return
            from message in GenSafeString()
            from errorCode in GenStatic.Choose(1, 1000)
            select new DbExceptionScenario(new TestDbException(message, errorCode));
    }

    private static Gen<ExceptionScenario> GenExceptionScenario()
    {
        return
            from message in GenSafeString()
            select new ExceptionScenario(new Exception(message));
    }

    private static Gen<StatusCodeScenario> GenStatusCodeScenario()
    {
        return GenStatic.Elements(new[]
        {
            new StatusCodeScenario(new ArgumentException("validation failed"), StatusCodes.Status400BadRequest),
            new StatusCodeScenario(new UnauthorizedAccessException("unauthorized"), StatusCodes.Status401Unauthorized),
            new StatusCodeScenario(new ForbiddenException("forbidden"), StatusCodes.Status403Forbidden),
            new StatusCodeScenario(new KeyNotFoundException("not found"), StatusCodes.Status404NotFound),
            new StatusCodeScenario(new DbUpdateConcurrencyException("conflict"), StatusCodes.Status409Conflict),
            new StatusCodeScenario(new TestDbException("db failure", 1), StatusCodes.Status500InternalServerError),
            new StatusCodeScenario(new Exception("unexpected"), StatusCodes.Status500InternalServerError)
        });
    }

    private static Gen<SensitiveMessageScenario> GenSensitiveMessageScenario()
    {
        return
            from secret in GenSafeString()
            select new SensitiveMessageScenario(secret);
    }

    private static Gen<WebhookScenario> GenWebhookScenario()
    {
        var guidGen = GenGuid();
        return
            from paymentId in GenSafeString()
            from externalRef in guidGen.Select(g => g.ToString())
            from status in GenStatic.Elements(new[] { "approved", "rejected", "pending" })
            from signature in GenSafeString()
            from success in GenStatic.Frequency((1, GenStatic.Constant(true)), (1, GenStatic.Constant(false)))
            select new WebhookScenario(
                new WebhookPayload { PaymentId = paymentId, ExternalReference = externalRef, Status = status },
                signature,
                new WebhookResult { Success = success, PaymentId = paymentId, Error = success ? null : "Invalid", FailureType = success ? WebhookFailureType.None : WebhookFailureType.Processing });
    }

    private static Gen<QrValidationScenario> GenQrValidationScenario()
    {
        var guidGen = GenGuid();
        return
            from userId in guidGen
            from ticketId in guidGen
            from eventId in guidGen
            from qrData in GenSafeString()
            from isValid in GenStatic.Frequency((2, GenStatic.Constant(true)), (1, GenStatic.Constant(false)))
            from error in GenStatic.Frequency(
                (1, GenStatic.Constant((string?)null)),
                (1, GenSafeString().Select(s => (string?)$"Error: {s}")))
            select new QrValidationScenario(
                userId,
                new ValidateQRCodeRequest { QRCodeData = qrData, EventId = eventId },
                new QRCodeValidationResult
                {
                    IsValid = isValid,
                    Error = error,
                    Ticket = new Ticket
                    {
                        Id = ticketId,
                        EventId = eventId,
                        Event = new Event { Id = eventId, Name = "Test Event" },
                        TicketType = new TicketType { Id = Guid.NewGuid(), Name = "General" },
                        PurchaserEmail = "test@example.com"
                    }
                });
    }


    private static Gen<string> GenSafeString()
    {
        var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-".ToCharArray();
        return GenStatic.Where(
            GenStatic.ArrayOf(GenStatic.Elements(chars)),
            arr => arr.Length >= 8 && arr.Length <= 30)
            .Select(arr => new string(arr));
    }

    private static Gen<Guid> GenGuid()
    {
        return GenStatic.ArrayOf(GenStatic.Choose(0, 255).Select(i => (byte)i), 16)
            .Select(bytes => new Guid(bytes));
    }

    private static Gen<SensitiveQueryScenario> GenSensitiveQueryScenarioFromKeys(string[] keys)
    {
        return
            from secret in GenSafeString()
            from key in GenStatic.Elements(keys)
            select new SensitiveQueryScenario($"?{key}={secret}&other=value", secret);
    }

    #endregion

    #region Test Data Records

    private record DbExceptionScenario(Exception Exception);
    private record ExceptionScenario(Exception Exception);
    private record StatusCodeScenario(Exception Exception, int ExpectedStatusCode);
    private record SensitiveMessageScenario(string SensitiveMessage);
    private record WebhookScenario(WebhookPayload Payload, string Signature, WebhookResult Result);
    private record QrValidationScenario(Guid UserId, ValidateQRCodeRequest Request, QRCodeValidationResult Result);
    private record SensitiveQueryScenario(string QueryString, string SecretValue);

    #endregion

    #region Test Doubles

    /// <summary>
    /// Concrete DbException used to simulate database failures in tests.
    /// </summary>
    private sealed class TestDbException : DbException
    {
        public TestDbException(string message, int errorCode) : base(message, errorCode)
        {
        }
    }

    /// <summary>
    /// Response feature that simulates a response whose headers have already been sent.
    /// Any attempt to mutate the status code throws, mirroring real ASP.NET Core behavior.
    /// </summary>
    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        private int _statusCode = StatusCodes.Status200OK;

        public int StatusCode
        {
            get => _statusCode;
            set => throw new InvalidOperationException("The response has already started.");
        }

        public string? ReasonPhrase { get; set; }

        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();

        public Stream Body { get; set; } = Stream.Null;

        public bool HasStarted => true;

        public void OnStarting(Func<object, Task> callback, object state) { }

        public void OnCompleted(Func<object, Task> callback, object state) { }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>
    /// Captures log entries for test assertions.
    /// </summary>
    private sealed class CollectingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state as IReadOnlyList<KeyValuePair<string, object>>;
            Entries.Add(new LogEntry(logLevel, eventId, exception, formatter(state, exception), values));
        }
    }

    private sealed record LogEntry(
        LogLevel LogLevel,
        EventId EventId,
        Exception? Exception,
        string Message,
        IReadOnlyList<KeyValuePair<string, object>>? State);

    /// <summary>
    /// In-memory audit log service that records all contexts for verification.
    /// </summary>
    private sealed class FakeAuditLogService : IAuditLogService
    {
        public List<AuditLogContext> Contexts { get; } = new();

        public Task LogActionAsync(AuditLogContext context)
        {
            Contexts.Add(context);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<AuditLogEntry>> GetAllLogsAsync() => Task.FromResult<IEnumerable<AuditLogEntry>>(new List<AuditLogEntry>());

        public Task<IEnumerable<AuditLogEntry>> GetLogsForUserAsync(Guid userId) => Task.FromResult<IEnumerable<AuditLogEntry>>(new List<AuditLogEntry>());
    }

    /// <summary>
    /// Audit log service that simulates a persistence failure.
    /// </summary>
    private sealed class FailingAuditLogService : IAuditLogService
    {
        public Task LogActionAsync(AuditLogContext context)
            => throw new InvalidOperationException("Audit persistence failure");

        public Task<IEnumerable<AuditLogEntry>> GetAllLogsAsync() => Task.FromResult<IEnumerable<AuditLogEntry>>(new List<AuditLogEntry>());

        public Task<IEnumerable<AuditLogEntry>> GetLogsForUserAsync(Guid userId) => Task.FromResult<IEnumerable<AuditLogEntry>>(new List<AuditLogEntry>());
    }

    /// <summary>
    /// Logger that throws on every log call, used to verify handler self-protection.
    /// </summary>
    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => throw new InvalidOperationException("Logger failure");
    }

    #endregion
}
