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
    private readonly TimeProvider _clock;
    private readonly IOptions<HideExpiredEventsOptions> _hideExpiredOptions;

    public PaymentService(
        ApplicationDbContext context,
        IMercadoPagoClient mercadoPagoClient,
        IOptions<MercadoPagoOptions> options,
        IOptions<ReservationTokenOptions> tokenOptions,
        ITicketService ticketService,
        IEmailService emailService,
        ILogger<PaymentService> logger,
        TimeProvider timeProvider,
        IOptions<HideExpiredEventsOptions> hideExpiredOptions)
    {
        _context = context;
        _mercadoPagoClient = mercadoPagoClient;
        _options = options.Value;
        _tokenOptions = tokenOptions.Value;
        _ticketService = ticketService;
        _emailService = emailService;
        _logger = logger;
        _clock = timeProvider;
        _hideExpiredOptions = hideExpiredOptions;
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

        // Validate token format: reservationId:nonce:timestamp:signature
        var tokenParts = token.Split(':');
        if (tokenParts.Length != 4)
        {
            _logger.LogWarning("Invalid reservation token format for reservation {ReservationId}", reservationId);
            throw new UnauthorizedAccessException("Invalid reservation token");
        }

        if (!Guid.TryParse(tokenParts[0], out var tokenReservationId))
        {
            _logger.LogWarning("Invalid reservation ID in token for reservation {ReservationId}", reservationId);
            throw new UnauthorizedAccessException("Invalid reservation token");
        }

        var nonce = tokenParts[1];
        var timestampStr = tokenParts[2];
        var providedSignature = tokenParts[3];

        if (!long.TryParse(timestampStr, out var ts))
        {
            _logger.LogWarning("Invalid timestamp in reservation token for reservation {ReservationId}", reservationId);
            throw new UnauthorizedAccessException("Invalid reservation token");
        }

        // Check token expiry (10 minutes)
        var tokenTime = DateTimeOffset.FromUnixTimeSeconds(ts).UtcDateTime;
        if ((_clock.GetUtcNow().UtcDateTime - tokenTime).TotalMinutes > 10)
        {
            _logger.LogWarning("Reservation token expired for reservation {ReservationId}", reservationId);
            throw new UnauthorizedAccessException("Reservation token has expired");
        }

        // Verify signature
        var dataToVerify = $"{tokenReservationId}:{nonce}:{ts}";
        if (!HmacHelper.ValidateHmacSha256(dataToVerify, _tokenOptions.TokenSecretKey, providedSignature))
        {
            _logger.LogWarning("Invalid reservation token signature for reservation {ReservationId}", reservationId);
            throw new UnauthorizedAccessException("Invalid reservation token");
        }

        if (tokenReservationId != reservationId)
        {
            _logger.LogWarning("Reservation token is bound to a different reservation than requested {ReservationId}", reservationId);
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

        if (reservation.Status != ReservationStatus.Active || reservation.ExpiresAt <= _clock.GetUtcNow().UtcDateTime)
        {
            _logger.LogWarning("Reservation {ReservationId} is not active or has expired", reservationId);
            throw new InvalidOperationException("Reservation must be active and not expired to create a payment preference");
        }

        // EHE-005 purchase guard (defense-in-depth for the reservation-exists-but-event-expired
        // race): reject payment preferences for expired events. The Event navigation is already
        // loaded by the .Include(r => r.Event) above — no extra round-trip. Guard is a no-op
        // when HideExpiredEvents.Enabled=false (EHE-009). IsExpired uses the injected clock so
        // the 13:59→14:01 race is deterministic. ProcessApprovedPaymentAsync is NOT guarded
        // (EHE-011 — confirmed payments must still produce tickets).
        if (_hideExpiredOptions.Value.Enabled &&
            reservation.Event.IsExpired(_clock.GetUtcNow().UtcDateTime))
        {
            _logger.LogWarning("Event {EventId} for reservation {ReservationId} has already started; payment preference rejected",
                reservation.EventId, reservationId);
            throw new EventExpiredException();
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
            ],
            NotificationUrl = string.IsNullOrEmpty(_options.WebhookBaseUrl) ? null : $"{_options.WebhookBaseUrl}/api/payments/webhook",
            BackUrls = new MercadoPagoBackUrls
            {
                Success = $"{_options.FrontendUrl}/checkout/success?preference_id={{preference_id}}",
                Failure = $"{_options.FrontendUrl}/checkout/return?status=failure",
                Pending = $"{_options.FrontendUrl}/checkout/return?status=pending"
            }
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
    public async Task<WebhookResult> ProcessWebhookAsync(MercadoPagoWebhookEnvelope envelope, string signature, byte[]? rawBody = null)
    {
        var dataId = envelope.Data?.Id;

        _logger.LogInformation(
            "Processing webhook for payment {PaymentId}",
            dataId);

        // Validate signature when present, but NEVER block processing.
        // notification_url webhooks are already authenticated by the merchant preference
        // and the real verification happens via GetPaymentByIdAsync (MP API call).
        // A signature mismatch is logged as a warning but does not stop the flow —
        // the payment is retrieved and validated against MP's API anyway.
        if (!string.IsNullOrEmpty(signature))
        {
            bool signatureValid;
            if (rawBody != null)
            {
                signatureValid = ValidateWebhookSignature(rawBody, signature, _options.WebhookSecret);
            }
            else
            {
                var payloadJson = JsonSerializer.Serialize(envelope);
                signatureValid = ValidateWebhookSignature(payloadJson, signature, _options.WebhookSecret);
            }

            if (!signatureValid)
            {
                _logger.LogWarning(
                    "Webhook signature mismatch for payment {PaymentId} — continuing anyway (payment will be verified via MP API)",
                    dataId);
            }
            else
            {
                _logger.LogDebug("Webhook signature verified for payment {PaymentId}", dataId);
            }
        }
        else
        {
            _logger.LogInformation("Webhook received without signature for payment {PaymentId} — processing anyway (notification_url mode)", dataId);
        }

        // No data.id → ACK and ignore (MP retries on non-200, so return 200)
        if (string.IsNullOrEmpty(dataId))
        {
            _logger.LogWarning("Webhook received without data.id — ignoring (200 ACK)");
            return new WebhookResult { Success = true, PaymentId = "ignored", FailureType = WebhookFailureType.None };
        }

        // Fetch real payment status from MP API
        var payment = await _mercadoPagoClient.GetPaymentByIdAsync(dataId);
        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found in Mercado Pago (404) — ignoring (200 ACK)", dataId);
            return new WebhookResult { Success = true, PaymentId = dataId, FailureType = WebhookFailureType.None };
        }

        if (!Guid.TryParse(payment.ExternalReference, out var reservationId))
        {
            _logger.LogWarning("Invalid external reference {ExternalReference} for payment {PaymentId}", payment.ExternalReference, dataId);
            return new WebhookResult
            {
                Success = false,
                Error = "Invalid external reference",
                PaymentId = dataId,
                FailureType = WebhookFailureType.Processing
            };
        }

        var reservation = await _context.Reservations
            .Include(r => r.TicketType)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == reservationId);

        if (reservation == null)
        {
            _logger.LogWarning("Reservation {ReservationId} not found for payment {PaymentId}", reservationId, dataId);
            return new WebhookResult
            {
                Success = false,
                Error = "Reservation not found",
                PaymentId = dataId,
                FailureType = WebhookFailureType.Processing
            };
        }

        var amount = reservation.Quantity * reservation.TicketType.Price;

        if (payment.Status.Equals("approved", StringComparison.OrdinalIgnoreCase))
        {
            return await ProcessApprovedPaymentAsync(reservation, dataId, amount);
        }

        return await ProcessFailedPaymentAsync(reservation, dataId, amount);
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
            // B4.5: Execution strategy wraps the transaction for Npgsql retry compatibility.
            // NpgsqlRetryingExecutionStrategy does not support user-initiated transactions
            // directly; ExecuteAsync provides the retry-safe wrapper.
            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                using var dbTransaction = await _context.Database.BeginTransactionAsync();

                try
                {
                    reservation.Status = ReservationStatus.Confirmed;
                    await _context.SaveChangesAsync();

                    var email = reservation.PurchaserEmail ?? reservation.User?.Email ?? "guest@ticketstart.com";
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
                            await _emailService.SendTicketEmailAsync(email, tickets, eventEntity, reservation.PurchaserName ?? reservation.User?.Name);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex,
                                "Failed to send ticket email for reservation {ReservationId}, payment {PaymentId}, recipient {Email}. Queuing for retry.",
                                reservation.Id, paymentId, email);
                            await QueueEmailRetryAsync(
                                reservation.Id,
                                paymentId,
                                email,
                                tickets.Select(t => t.Id).ToArray(),
                                ex.Message);
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
            });
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

    /// <inheritdoc />
    public async Task<WebhookResult> ConfirmPaymentAsync(string preferenceId)
    {
        _logger.LogInformation("Confirming payment for preference {PreferenceId}", preferenceId);

        try
        {
            var preference = await _mercadoPagoClient.GetPreferenceAsync(preferenceId);
            if (preference == null || !Guid.TryParse(preference.ExternalReference, out var reservationId))
            {
                return new WebhookResult { Success = false, Error = "Invalid preference or external reference" };
            }

            var payments = await _mercadoPagoClient.SearchPaymentsByExternalReferenceAsync(preference.ExternalReference);
            var approvedPayment = payments.FirstOrDefault(p =>
                p.Status.Equals("approved", StringComparison.OrdinalIgnoreCase));

            if (approvedPayment == null)
            {
                _logger.LogWarning("No approved payment found for reservation {ReservationId}", reservationId);
                return new WebhookResult { Success = false, Error = "No approved payment found" };
            }

            var existingTransaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.MercadoPagoId == approvedPayment.Id);
            if (existingTransaction != null)
            {
                _logger.LogInformation("Payment {PaymentId} already processed", approvedPayment.Id);
                return new WebhookResult { Success = true, PaymentId = approvedPayment.Id };
            }

            var reservation = await _context.Reservations
                .Include(r => r.TicketType)
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == reservationId);

            if (reservation == null)
            {
                return new WebhookResult
                {
                    Success = false,
                    Error = "Reservation not found",
                    PaymentId = approvedPayment.Id
                };
            }

            var amount = reservation.Quantity * reservation.TicketType.Price;
            return await ProcessApprovedPaymentAsync(reservation, approvedPayment.Id, amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming payment for preference {PreferenceId}", preferenceId);
            return new WebhookResult { Success = false, Error = ex.Message };
        }
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

    /// <inheritdoc />
    public async Task QueueEmailRetryAsync(Guid reservationId, string paymentId, string recipientEmail, Guid[] ticketIds, string error)
    {
        _logger.LogInformation(
            "Queueing email retry for reservation {ReservationId}, payment {PaymentId}, recipient {RecipientEmail}",
            reservationId, paymentId, recipientEmail);

        var now = DateTime.UtcNow;
        _context.PendingEmailSends.Add(new PendingEmailSend
        {
            Id = Guid.NewGuid(),
            ReservationId = reservationId,
            PaymentId = paymentId,
            RecipientEmail = recipientEmail,
            TicketIds = ticketIds.ToList(),
            LastError = error,
            Attempts = 0,
            MaxAttempts = 5,
            Status = "pending",
            CreatedAt = now,
            UpdatedAt = now
        });
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<RetryPendingEmailsResponse> RetryPendingEmailsAsync()
    {
        _logger.LogInformation("Starting retry of pending emails");

        var pending = await _context.PendingEmailSends
            .Where(p => p.Status == "pending" && p.Attempts < p.MaxAttempts)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();

        var response = new RetryPendingEmailsResponse { Attempted = pending.Count };

        foreach (var row in pending)
        {
            try
            {
                var reservation = await _context.Reservations
                    .FirstOrDefaultAsync(r => r.Id == row.ReservationId);

                if (reservation == null)
                {
                    _logger.LogWarning("Reservation {ReservationId} not found for email retry row {RowId}",
                        row.ReservationId, row.Id);
                    row.Status = "exhausted";
                    row.LastError = "Reservation not found";
                    row.Attempts = row.MaxAttempts;
                    row.UpdatedAt = DateTime.UtcNow;
                    response.Exhausted++;
                    continue;
                }

                var tickets = await _context.Tickets
                    .Include(t => t.Event)
                    .Where(t => row.TicketIds.Contains(t.Id))
                    .ToListAsync();

                if (tickets.Count == 0)
                {
                    _logger.LogWarning("No tickets found for email retry row {RowId}", row.Id);
                    row.Status = "exhausted";
                    row.LastError = "No tickets found";
                    row.Attempts = row.MaxAttempts;
                    row.UpdatedAt = DateTime.UtcNow;
                    response.Exhausted++;
                    continue;
                }

                await _emailService.SendTicketEmailAsync(row.RecipientEmail, tickets, tickets[0].Event, reservation.PurchaserName ?? reservation.User?.Name);

                row.Attempts++;
                row.Status = "sent";
                row.LastError = null;
                row.LastAttemptAt = DateTime.UtcNow;
                row.UpdatedAt = DateTime.UtcNow;
                response.Sent++;

                _logger.LogInformation("Retry email sent for row {RowId} to {RecipientEmail}", row.Id, row.RecipientEmail);
            }
            catch (Exception ex)
            {
                row.Attempts++;
                row.LastError = ex.Message;
                row.LastAttemptAt = DateTime.UtcNow;
                row.UpdatedAt = DateTime.UtcNow;

                if (row.Attempts >= row.MaxAttempts)
                {
                    row.Status = "exhausted";
                    response.Exhausted++;
                    _logger.LogError(ex, "Email retry exhausted for row {RowId} after {Attempts} attempts",
                        row.Id, row.Attempts);
                }
                else
                {
                    response.Failed++;
                    _logger.LogWarning(ex, "Email retry attempt {Attempts}/{MaxAttempts} failed for row {RowId}",
                        row.Attempts, row.MaxAttempts, row.Id);
                }
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Email retry complete: {Attempted} attempted, {Sent} sent, {Failed} failed, {Exhausted} exhausted",
            response.Attempted, response.Sent, response.Failed, response.Exhausted);

        return response;
    }
}
