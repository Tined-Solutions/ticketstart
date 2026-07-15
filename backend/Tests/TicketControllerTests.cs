using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TicketeraOnline.Api.Controllers;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;
using Xunit;

namespace TicketeraOnline.Api.Tests;

/// <summary>
/// Controller tests for TicketController.
/// Validates: Batch 5 (Ticket Lookup) — B5.1, B5.2, B5.3
/// </summary>
public class TicketControllerTests
{
    private readonly Mock<ITicketService> _mockTicketService;
    private readonly Mock<ILogger<TicketController>> _mockLogger;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly TicketController _controller;

    public TicketControllerTests()
    {
        _mockTicketService = new Mock<ITicketService>();
        _mockLogger = new Mock<ILogger<TicketController>>();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _controller = new TicketController(
            _mockTicketService.Object,
            _mockLogger.Object,
            _mockAuditLogService.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    #region B5.1 — Lookup Response Excludes QR Fields

    [Fact]
    public async Task LookupByEmailOnly_ReturnsInfoOnlyResponse_WithoutQRFields()
    {
        // Arrange
        var email = "buyer@test.com";
        var mockResponse = new List<TicketLookupInfoResponse>
        {
            new TicketLookupInfoResponse
            {
                EventName = "Test Event",
                EventDate = DateTime.UtcNow.AddDays(10),
                TicketType = "General Admission",
                Quantity = 2,
                PurchaserEmail = "b***@test.com" // masked email
            }
        };

        _mockTicketService
            .Setup(s => s.LookupTicketsByEmailAsync(email))
            .ReturnsAsync(mockResponse);

        // Act — lookup with email only (no DNI)
        var result = await _controller.LookupTickets(email);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseList = Assert.IsAssignableFrom<IEnumerable<TicketLookupInfoResponse>>(okResult.Value);

        var response = responseList.First();
        // Verify info fields are present
        Assert.Equal("Test Event", response.EventName);
        Assert.Equal("General Admission", response.TicketType);
        Assert.Equal(2, response.Quantity);
        Assert.Equal("b***@test.com", response.PurchaserEmail);

        // Verify NO QR fields exist on the response type
        var responseType = typeof(TicketLookupInfoResponse);
        Assert.Null(responseType.GetProperty("QRCodeData"));
        Assert.Null(responseType.GetProperty("QRCodeImage"));
        Assert.Null(responseType.GetProperty("QRCodeSrc"));
    }

    [Fact]
    public async Task LookupByEmailOnly_NonexistentEmail_Returns200WithGenericMessage()
    {
        // Arrange
        var email = "nonexistent@test.com";
        var emptyResponse = new List<TicketLookupInfoResponse>();

        _mockTicketService
            .Setup(s => s.LookupTicketsByEmailAsync(email))
            .ReturnsAsync(emptyResponse);

        // Act — lookup with email only (no DNI)
        var result = await _controller.LookupTickets(email);

        // Assert — should return 200 OK, not 404
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var responseList = Assert.IsAssignableFrom<IEnumerable<TicketLookupInfoResponse>>(okResult.Value);
        Assert.Empty(responseList);
    }

    [Fact]
    public async Task LookupByEmailOnly_EmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        string email = "";

        // Act — lookup with empty email
        var result = await _controller.LookupTickets(email);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    #endregion

    #region B5.2 — Resend Tickets Endpoint

    [Fact]
    public async Task ResendTickets_ValidRequest_ReturnsGenericSuccess()
    {
        // Arrange
        var request = new ResendTicketsRequest
        {
            Email = "buyer@test.com",
            CaptchaToken = "valid-captcha-token"
        };

        _mockTicketService
            .Setup(s => s.ResendTicketsByEmailAsync(request.Email, request.CaptchaToken))
            .ReturnsAsync(true); // Always returns success

        // Act
        var result = await _controller.ResendTickets(request);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        // Verify generic message (no info leak)
        var responseObj = okResult.Value;
        var messageProp = responseObj!.GetType().GetProperty("message");
        Assert.NotNull(messageProp);
        var message = messageProp.GetValue(responseObj) as string;
        Assert.NotNull(message);
        Assert.Contains("registered", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResendTickets_NonexistentEmail_StillReturns200()
    {
        // Arrange
        var request = new ResendTicketsRequest
        {
            Email = "nonexistent@test.com",
            CaptchaToken = "valid-captcha-token"
        };

        _mockTicketService
            .Setup(s => s.ResendTicketsByEmailAsync(request.Email, request.CaptchaToken))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.ResendTickets(request);

        // Assert — same generic success, no info leak
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var responseObj = okResult.Value;
        var messageProp = responseObj!.GetType().GetProperty("message");
        Assert.NotNull(messageProp);
        var message = messageProp.GetValue(responseObj) as string;
        Assert.NotNull(message);
        Assert.Contains("registered", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResendTickets_EmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResendTicketsRequest
        {
            Email = "",
            CaptchaToken = "token"
        };

        // Act
        var result = await _controller.ResendTickets(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    [Fact]
    public async Task ResendTickets_NullBody_ReturnsBadRequest()
    {
        // Act
        var result = await _controller.ResendTickets(null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequestResult.StatusCode);
    }

    #endregion
}