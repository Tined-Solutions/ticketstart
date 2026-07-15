using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TicketeraOnline.Api.Data;
using TicketeraOnline.Api.Helpers;
using TicketeraOnline.Api.Models;

namespace TicketeraOnline.Api.Services;

/// <summary>
/// Service implementation for Mercado Pago payment integration and webhook processing.
/// </summary>
public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly IMercadoPagoClient _mercadoPagoClient;
    private readonly MercadoPagoOptions _options;
    private readonly ReservationTokenOptions _tokenOptions;
    private readonly ITicketService _ticketService;
    private readonly IEmailService _emailService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        ApplicationDbContext context,
        IMercadoPagoClient mercadoPagoClient,
        IOptions<MercadoPagoOptions> options,
        IOptions<ReservationTokenOptions> tokenOptions,
        ITicketService ticketService,
        IEmailService emailService,
        ILogger<PaymentService> logger)
    {
        _context = context;
        _mercadoPagoClient = mercadoPagoClient;
        _options = options.Value;
        _tokenOptions = tokenOptions.Value;
        _ticketService = ticketService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PaymentPreference> CreatePaymentPreferenceAsync(Guid reservationId, string token)
    {
        _logger.LogInformation("Creating payment preference for reservation {ReservationId}", reservationId);

        if (string.IsNullOrEmpty(_tokenOptions.TokenSecretKey))
        {
            throw new InvalidOperationException("Reservation:TokenSecretKey is not configured");
        }

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("Missing reservation token for reservation {ReservationId}", reservationId);
            throw new UnauthorizedAccessException("Invalid reservation token");
        }

        if (!HmacHelper.ValidateHmacSha256(reservationId.ToString(), _tokenOptions.TokenSecretKey, token))
        {
            _logger.LogWarning("Invalid reservation token for reservation {ReservationId}", reservationId);
            throw new UnauthorizedAccessException("Invalid reservation token");
        }

        var reservation = await _context.Reservations
            .Include(r => r.TicketType)
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found", reservationId);
            throw new KeyNotFoundException($"Reservation {reservationId} not found");
        }

        if (reservation.Status != ReservationStatus.Active || reservation.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Reservation {ReservationId} is not active or has expired", reservationId);
            throw new InvalidOperationException("Reservation must be active and not expired to create a payment preference");
        }

        var request = new MercadoPagoPreferenceRequest
        {
            ExternalReference = reservation.Id.ToString(),
            Items =
            [
                new MercadoPagoItemRequest
                {
                    Title = $"{reservation.Event.Name} - {reservation.TicketType.Name}",
                    Quantity = reservation.Quantity,
                    UnitPrice = reservation.TicketType.Price
                }
            ]
        };

        var response = await _mercadoPagoClient.CreatePreferenceAsync(request);

        _logger.LogInformation(
            "Created payment preference {PreferenceId} for reservation {ReservationId}",
            response.Id, reservationId);

        return new PaymentPreference
        {
            CheckoutUrl = response.InitPoint,
            PreferenceId = response.Id
        };
    }

    /// <inheritdoc />
    public async Task<WebhookResult> ProcessWebhookAsync(WebhookPayload payload, string signature, byte[]? rawBody = null)
    {
        _logger.LogInformation(
            "Processing webhook for payment {PaymentId} with status {Status}",
            payload.PaymentId, payload.Status);

        var payloadJson = JsonSerializer.Serialize(payload);

        bool signatureValid;
        if (rawBody != null)
        {
            signatureValid = ValidateWebhookSignature(rawBody, signature, _options.WebhookSecret);
        }
        else
        {
            signatureValid = ValidateWebhookSignature(payloadJson, signature, _options.WebhookSecret);
        }

        if (!signatureValid)
        {
            _logger.LogWarning("Invalid webhook signature for payment {PaymentId}", payload.PaymentId);
            return new WebhookResult
            {
                Success = false,
                Error = "Invalid webhook signature",
                PaymentId = payload.PaymentId,
                FailureType = WebhookFailureType.Authentication
            };
        }

        if (!Guid.TryParse(payload.ExternalReference, out var reservationId))
        {
            _logger.LogWarning("Invalid external reference {ExternalReference}", payload.ExternalReference);
            return new WebhookResult
            {
                Success = false,
                Error = "Invalid external reference",
                PaymentId = payload.PaymentId,
                FailureType = WebhookFailureType.Processing
            };
        }

        var reservation = await _context.Reservations
            .Include(r => r.TicketType)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found for payment {PaymentId}", reservationId, payload.PaymentId);
            return new WebhookResult
            {
                Success = false,
                Error = "Reservation not found",
                PaymentId = payload.PaymentId,
                FailureType = WebhookFailureType.Processing
            };
        }

        var amount = reservation.Quantity * reservation.TicketType.Price;

        if (payload.Status.Equals("approved", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessApprovedPaymentAsync(reservation, payload.PaymentId, amount);
        }

        return await ProcessFailedPaymentAsync(reservation, payload.PaymentId, amount);
    }

    private async Task<WebhookResult> ProcessApprovedPaymentAsync(Reservation reservation, string paymentId, decimal amount)
    {
        // B4.5: Check idempotency — if a transaction with this MercadoPagoId already exists, return 200
        var existingTransaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.MercadoPagoId == paymentId);
        if (existingTransaction != null)
        {
            _logger.LogInformation(
                "Duplicate webhook for payment {PaymentId} — transaction already exists. Returning 200 (idempotent).",
                paymentId);
            return new WebhookResult { Success = true, PaymentId = paymentId };
        }

        if (reservation.Status == ReservationStatus.Active && reservation.ExpiresAt > DateTime.UtcNow)
        {
            // B4.5: Wrap in transaction for atomicity
            using var dbTransaction = await _context.Database.BeginTransactionAsync();

            try
            {
                reservation.Status = ReservationStatus.Confirmed;
                await _context.SaveChangesAsync();

                var email = reservation.PurchaserEmail ?? reservation.User?.Email ?? "guest@ticketera.com";
                var tickets = (await _ticketService.CreateTicketsAsync(reservation.Id, email, reservation.PurchaserDNI)).ToList();

                _context.Transactions.Add(new Transaction
                {
                    Id = Guid.NewGuid(),
                    ReservationId = reservation.Id,
                    MercadoPagoId = paymentId,
                    Amount = amount,
                    Status = TransactionStatus.Approved,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                await dbTransaction.CommitAsync();

                _logger.LogInformation(
                    "Payment {PaymentId} approved; reservation {ReservationId} confirmed and tickets created",
                    paymentId, reservation.Id);

                // B4.5: Send email AFTER commit with try/catch (log only, don't rollback)
                if (tickets.Count > 0)
                {
                    try
                    {
                        var eventEntity = tickets[0].Event ?? reservation.Event;
                        await _emailService.SendTicketEmailAsync(email, tickets, eventEntity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to send ticket email for reservation {ReservationId}. Payment was successful.",
                            reservation.Id);
                    }
                }

                return new WebhookResult { Success = true, PaymentId = paymentId };
            }
            catch (DbUpdateException) when (_context.Transactions.Any(t => t.MercadoPagoId == paymentId))
            {
                // B4.5: Concurrent duplicate — another request already inserted this transaction
                await dbTransaction.RollbackAsync();
                _logger.LogInformation(
                    "Concurrent duplicate webhook for payment {PaymentId} — transaction already exists. Returning 200.",
                    paymentId);
                return new WebhookResult { Success = true, PaymentId = paymentId };
            }
            catch (Exception)
            {
                // B4.5: Atomic rollback — any failure rolls back the entire operation
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        _logger.LogWarning(
            "Stock failure for reservation {ReservationId}; payment {PaymentId} will be refunded",
            reservation.Id, paymentId);

        var refund = await InitiateRefundAsync(paymentId, amount, reservation.Id);

        if (reservation.Status == ReservationStatus.Active)
        {
            reservation.Status = ReservationStatus.Cancelled;
            await _context.SaveChangesAsync();
        }

        if (!refund.Success)
        {
            return new WebhookResult
            {
                Success = false,
                Error = refund.Error,
                PaymentId = paymentId,
                FailureType = WebhookFailureType.Processing
            };
        }

        return new WebhookResult { Success = true, PaymentId = paymentId };
    }

    private async Task<WebhookResult> ProcessFailedPaymentAsync(Reservation reservation, string paymentId, decimal amount)
    {
        if (reservation.Status == ReservationStatus.Active)
        {
            reservation.Status = ReservationStatus.Cancelled;
        }

        _context.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            ReservationId = reservation.Id,
            MercadoPagoId = paymentId,
            Amount = amount,
            Status = TransactionStatus.Rejected,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Payment {PaymentId} rejected; reservation {ReservationId} cancelled",
            paymentId, reservation.Id);

        return new WebhookResult { Success = true, PaymentId = paymentId };
    }

    /// <inheritdoc />
    public async Task<RefundResult> InitiateRefundAsync(string mercadoPagoId, decimal amount, Guid reservationId)
    {
        _logger.LogInformation(
            "Initiating refund for payment {PaymentId}, reservation {ReservationId}, amount {Amount}",
            mercadoPagoId, reservationId, amount);

        var response = await _mercadoPagoClient.RefundPaymentAsync(mercadoPagoId, amount);

        _context.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            MercadoPagoId = mercadoPagoId,
            Amount = amount,
            Status = TransactionStatus.Refunded,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Refund {RefundId} processed for payment {PaymentId}",
            response.Id, mercadoPagoId);

        return new RefundResult
        {
            Success = true,
            RefundId = response.Id
        };
    }

    /// <summary>
    /// Validates a webhook HMAC-SHA256 signature from a string payload.
    /// </summary>
    public static bool ValidateWebhookSignature(string payload, string signature, string secret)
    {
        return HmacHelper.ValidateHmacSha256(payload, secret, signature);
    }

    /// <summary>
    /// Validates a webhook HMAC-SHA256 signature from raw bytes.
    /// Use this overload when the webhook body was received as raw bytes to avoid
    /// encoding mismatches between the sender and receiver.
    /// </summary>
    public static bool ValidateWebhookSignature(byte[] rawBody, string signature, string secret)
    {
        return HmacHelper.ValidateHmacSha256(rawBody, secret, signature);
    }
}
