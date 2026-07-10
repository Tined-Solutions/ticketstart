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
        var request = new CreatePaymentPreferenceRequest { ReservationId = reservationId };
        var preference = new PaymentPreference
        {
            CheckoutUrl = "https://mp.test/checkout/pref-123",
            PreferenceId = "pref-123"
        };

        _mockPaymentService.Setup(s => s.CreatePaymentPreferenceAsync(reservationId)).ReturnsAsync(preference);

        var result = await _controller.CreatePreference(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic? value = okResult.Value as dynamic;
        Assert.NotNull(value);
        Assert.Equal(preference.CheckoutUrl, value!.checkoutUrl);
        Assert.Equal(preference.PreferenceId, value.preferenceId);
    }

    [Fact]
    public async Task CreatePreference_ServiceThrowsKeyNotFound_ReturnsNotFound()
    {
        var request = new CreatePaymentPreferenceRequest { ReservationId = Guid.NewGuid() };

        _mockPaymentService.Setup(s => s.CreatePaymentPreferenceAsync(It.IsAny<Guid>())).ThrowsAsync(new KeyNotFoundException("Reservation not found"));

        var result = await _controller.CreatePreference(request);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);
    }

    [Fact]
    public async Task CreatePreference_ServiceThrowsInvalidOperation_ReturnsBadRequest()
    {
        var request = new CreatePaymentPreferenceRequest { ReservationId = Guid.NewGuid() };

        _mockPaymentService.Setup(s => s.CreatePaymentPreferenceAsync(It.IsAny<Guid>())).ThrowsAsync(new InvalidOperationException("Reservation expired"));

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

        _mockPaymentService.Setup(s => s.ProcessWebhookAsync(payload, signature))
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

        _mockPaymentService.Setup(s => s.ProcessWebhookAsync(payload, signature))
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

        _mockPaymentService.Setup(s => s.ProcessWebhookAsync(payload, signature))
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

}
