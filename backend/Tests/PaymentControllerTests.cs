using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Unit tests for PaymentController.
/// Validates: Requirements 5.1, 5.5, 5.8, 16.2, 16.3
/// </summary>
public class PaymentControllerTests
{
    private readonly Mock<IPaymentService> _mockPaymentService;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<ILogger<PaymentController>> _mockLogger;
    private readonly PaymentController _controller;

    public PaymentControllerTests()
    {
        _mockPaymentService = new Mock<IPaymentService>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockLogger = new Mock<ILogger<PaymentController>>();
        _controller = new PaymentController(_mockPaymentService.Object, _mockLogger.Object, _mockAuditLogService.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public async Task CreatePreference_AnonymousUser_ReturnsOkWithPreference()
    {
        var reservationId = Guid.NewGuid();
        var request = new CreatePaymentPreferenceRequest { ReservationId = reservationId, Token = "valid-token" };
        var preference = new PaymentPreference
        {
            CheckoutUrl = "https://mp.test/checkout/pref-123",
            PreferenceId = "pref-123"
        };

        _mockPaymentService.Setup(s => s.CreatePaymentPreferenceAsync(reservationId, request.Token)).ReturnsAsync(preference);

        var result = await _controller.CreatePreference(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic? value = okResult.Value as dynamic;
        Assert.NotNull(value);
        Assert.Equal(preference.CheckoutUrl, value!.checkoutUrl);
        Assert.Equal(preference.PreferenceId, value.preferenceId);
    }

    [Fact]
    public async Task CreatePreference_WithoutToken_ReturnsUnauthorized()
    {
        var request = new CreatePaymentPreferenceRequest { ReservationId = Guid.NewGuid() };

        var result = await _controller.CreatePreference(request);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task CreatePreference_WithInvalidToken_ReturnsUnauthorized()
    {
        var reservationId = Guid.NewGuid();
        var request = new CreatePaymentPreferenceRequest { ReservationId = reservationId, Token = "invalid-token" };

        _mockPaymentService.Setup(s => s.CreatePaymentPreferenceAsync(reservationId, request.Token))
            .ThrowsAsync(new UnauthorizedAccessException("Invalid reservation token"));

        var result = await _controller.CreatePreference(request);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task CreatePreference_ServiceThrowsKeyNotFound_ReturnsNotFound()
    {
        var reservationId = Guid.NewGuid();
        var request = new CreatePaymentPreferenceRequest { ReservationId = reservationId, Token = "valid-token" };

        _mockPaymentService.Setup(s => s.CreatePaymentPreferenceAsync(reservationId, request.Token)).ThrowsAsync(new KeyNotFoundException("Reservation not found"));

        var result = await _controller.CreatePreference(request);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task CreatePreference_ServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        var reservationId = Guid.NewGuid();
        var request = new CreatePaymentPreferenceRequest { ReservationId = reservationId, Token = "valid-token" };

        _mockPaymentService.Setup(s => s.CreatePaymentPreferenceAsync(reservationId, request.Token)).ThrowsAsync(new InvalidOperationException("Reservation expired"));

        var result = await _controller.CreatePreference(request);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task Webhook_ValidSignature_ReturnsOk()
    {
        var payload = new WebhookPayload
        {
            PaymentId = "pay-123",
            ExternalReference = Guid.NewGuid().ToString(),
            Status = "approved"
        };
        var signature = "valid-signature";

        _mockPaymentService.Setup(s => s.ProcessWebhookAsync(payload, signature, null))
            .ReturnsAsync(new WebhookResult { Success = true, PaymentId = payload.PaymentId });

        var result = await _controller.Webhook(payload, signature);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_ReturnsUnauthorized()
    {
        var payload = new WebhookPayload
        {
            PaymentId = "pay-123",
            ExternalReference = Guid.NewGuid().ToString(),
            Status = "approved"
        };
        var signature = "invalid-signature";

        _mockPaymentService.Setup(s => s.ProcessWebhookAsync(payload, signature, null))
            .ReturnsAsync(new WebhookResult { Success = false, Error = "Invalid webhook signature", PaymentId = payload.PaymentId, FailureType = WebhookFailureType.Authentication });

        var result = await _controller.Webhook(payload, signature);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task Webhook_ProcessingFailure_ReturnsOkWithFailedStatus()
    {
        var payload = new WebhookPayload
        {
            PaymentId = "pay-123",
            ExternalReference = Guid.NewGuid().ToString(),
            Status = "approved"
        };
        var signature = "valid-signature";

        _mockPaymentService.Setup(s => s.ProcessWebhookAsync(payload, signature, null))
            .ReturnsAsync(new WebhookResult { Success = false, Error = "Internal processing error", PaymentId = payload.PaymentId, FailureType = WebhookFailureType.Processing });

        var result = await _controller.Webhook(payload, signature);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        dynamic? value = okResult.Value as dynamic;
        Assert.NotNull(value);
        Assert.Equal(payload.PaymentId, value!.paymentId);
        Assert.Equal("failed", value.status);
        Assert.Equal("PROCESSING_FAILED", value.error);
    }

    #region Batch 4: Payment Pipeline Tests

    [Fact]
    public async Task Batch4_Webhook_DuplicateMercadoPagoId_Returns200()
    {
        // RED: idempotency not implemented — duplicate payment ID would cause 500
        var payload = new WebhookPayload
        {
            PaymentId = "pay-dup",
            ExternalReference = Guid.NewGuid().ToString(),
            Status = "approved"
        };
        var signature = "valid-signature";

        _mockPaymentService
            .Setup(s => s.ProcessWebhookAsync(payload, signature, It.IsAny<byte[]>()))
            .ReturnsAsync(new WebhookResult { Success = true, PaymentId = payload.PaymentId });

        // Simulate raw body
        var rawBody = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(payload));
        _controller.ControllerContext.HttpContext!.Request.Body = new System.IO.MemoryStream(rawBody);
        _controller.ControllerContext.HttpContext.Request.ContentType = "application/json";

        var result = await _controller.Webhook(payload, signature);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task Batch4_Webhook_RawBytesPassedToService()
    {
        // RED: controller does not read raw bytes from body and pass to ProcessWebhookAsync
        var payload = new WebhookPayload
        {
            PaymentId = "pay-raw",
            ExternalReference = Guid.NewGuid().ToString(),
            Status = "approved"
        };
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);
        var rawBody = Encoding.UTF8.GetBytes(payloadJson);
        var signature = "valid-signature";

        _controller.ControllerContext.HttpContext!.Request.Body = new System.IO.MemoryStream(rawBody);
        _controller.ControllerContext.HttpContext.Request.ContentType = "application/json";

        _mockPaymentService
            .Setup(s => s.ProcessWebhookAsync(It.IsAny<WebhookPayload>(), signature, It.IsAny<byte[]>()))
            .ReturnsAsync(new WebhookResult { Success = true, PaymentId = payload.PaymentId });

        var result = await _controller.Webhook(payload, signature);

        var okResult = Assert.IsType<OkObjectResult>(result);
        _mockPaymentService.Verify(
            s => s.ProcessWebhookAsync(It.IsAny<WebhookPayload>(), signature, It.Is<byte[]>(b => b.SequenceEqual(rawBody))),
            Times.Once);
    }

    #endregion
}
