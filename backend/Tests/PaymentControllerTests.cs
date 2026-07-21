using System.Text;
using System.Text.Json;
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
        var envelope = new MercadoPagoWebhookEnvelope
        {
            Action = "payment.updated",
            Type = "payment",
            Data = new MercadoPagoWebhookData { Id = "pay-123" }
        };
        var signature = "valid-signature";
        var rawBody = SetupRequestBody(envelope);

        _mockPaymentService.Setup(s => s.ProcessWebhookAsync(It.IsAny<MercadoPagoWebhookEnvelope>(), signature, It.IsAny<byte[]>()))
            .ReturnsAsync(new WebhookResult { Success = true, PaymentId = "pay-123" });

        var result = await _controller.Webhook(signature);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task Webhook_InvalidSignature_ReturnsUnauthorized()
    {
        var envelope = new MercadoPagoWebhookEnvelope
        {
            Action = "payment.updated",
            Type = "payment",
            Data = new MercadoPagoWebhookData { Id = "pay-123" }
        };
        var signature = "invalid-signature";
        SetupRequestBody(envelope);

        _mockPaymentService.Setup(s => s.ProcessWebhookAsync(It.IsAny<MercadoPagoWebhookEnvelope>(), signature, It.IsAny<byte[]>()))
            .ReturnsAsync(new WebhookResult { Success = false, Error = "Invalid webhook signature", PaymentId = "pay-123", FailureType = WebhookFailureType.Authentication });

        var result = await _controller.Webhook(signature);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task Webhook_ProcessingFailure_ReturnsOkWithFailedStatus()
    {
        var envelope = new MercadoPagoWebhookEnvelope
        {
            Action = "payment.updated",
            Type = "payment",
            Data = new MercadoPagoWebhookData { Id = "pay-123" }
        };
        var signature = "valid-signature";
        SetupRequestBody(envelope);

        _mockPaymentService.Setup(s => s.ProcessWebhookAsync(It.IsAny<MercadoPagoWebhookEnvelope>(), signature, It.IsAny<byte[]>()))
            .ReturnsAsync(new WebhookResult { Success = false, Error = "Internal processing error", PaymentId = "pay-123", FailureType = WebhookFailureType.Processing });

        var result = await _controller.Webhook(signature);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        dynamic? value = okResult.Value as dynamic;
        Assert.NotNull(value);
        Assert.Equal("pay-123", value!.paymentId);
        Assert.Equal("failed", value.status);
        Assert.Equal("PROCESSING_FAILED", value.error);
    }

    [Fact]
    public async Task Webhook_EmptyBody_Returns200Ack()
    {
        // Malformed/unreadable body — controller returns 200 ACK
        _controller.ControllerContext.HttpContext!.Request.Body = new MemoryStream([]);
        _controller.ControllerContext.HttpContext.Request.ContentType = "application/json";

        var result = await _controller.Webhook(null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    #region Batch 4: Payment Pipeline Tests

    [Fact]
    public async Task Batch4_Webhook_DuplicateMercadoPagoId_Returns200()
    {
        var envelope = new MercadoPagoWebhookEnvelope
        {
            Action = "payment.updated",
            Type = "payment",
            Data = new MercadoPagoWebhookData { Id = "pay-dup" }
        };
        var signature = "valid-signature";
        SetupRequestBody(envelope);

        _mockPaymentService
            .Setup(s => s.ProcessWebhookAsync(It.IsAny<MercadoPagoWebhookEnvelope>(), signature, It.IsAny<byte[]>()))
            .ReturnsAsync(new WebhookResult { Success = true, PaymentId = "pay-dup" });

        var result = await _controller.Webhook(signature);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public async Task Batch4_Webhook_RawBytesPassedToService()
    {
        var envelope = new MercadoPagoWebhookEnvelope
        {
            Action = "payment.updated",
            Type = "payment",
            Data = new MercadoPagoWebhookData { Id = "pay-raw" }
        };
        var rawBody = SetupRequestBody(envelope);
        var signature = "valid-signature";

        _mockPaymentService
            .Setup(s => s.ProcessWebhookAsync(It.IsAny<MercadoPagoWebhookEnvelope>(), signature, It.IsAny<byte[]>()))
            .ReturnsAsync(new WebhookResult { Success = true, PaymentId = "pay-raw" });

        var result = await _controller.Webhook(signature);

        var okResult = Assert.IsType<OkObjectResult>(result);
        _mockPaymentService.Verify(
            s => s.ProcessWebhookAsync(It.IsAny<MercadoPagoWebhookEnvelope>(), signature, It.Is<byte[]>(b => b.SequenceEqual(rawBody))),
            Times.Once);
    }

    #endregion

    private byte[] SetupRequestBody(MercadoPagoWebhookEnvelope envelope)
    {
        var json = JsonSerializer.Serialize(envelope);
        var bytes = Encoding.UTF8.GetBytes(json);
        _controller.ControllerContext.HttpContext!.Request.Body = new MemoryStream(bytes);
        _controller.ControllerContext.HttpContext.Request.ContentType = "application/json";
        return bytes;
    }
}
