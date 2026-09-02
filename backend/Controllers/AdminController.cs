using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using TicketeraOnline.Api.Models;
using TicketeraOnline.Api.Services;

namespace TicketeraOnline.Api.Controllers;

/// <summary>
/// Controller for system-wide admin operations.
/// All endpoints require the Admin role.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = "RequireAdminRole")]
public class AdminController : TicketeraControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IAuthService _authService;
    private readonly IAuditLogService _auditLogService;
    private readonly IEventService _eventService;
    private readonly IAdminPurchaseService _adminPurchaseService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminService adminService,
        IAuthService authService,
        IAuditLogService auditLogService,
        ILogger<AdminController> logger,
        IEventService eventService,
        IAdminPurchaseService adminPurchaseService)
    {
        _adminService = adminService;
        _authService = authService;
        _auditLogService = auditLogService;
        _logger = logger;
        _eventService = eventService;
        _adminPurchaseService = adminPurchaseService;
    }

    /// <summary>
    /// Creates a new user account. Only administrators can create users.
    /// </summary>
    /// <param name="request">User creation details including name, email, password, and role</param>
    /// <returns>Created user information</returns>
    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] AdminCreateUserRequest request)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized();
        }

        try
        {
            var result = await _authService.CreateUserAsync(request.Name, request.Email, request.Password, request.Role);

            if (!result.Success)
            {
                _logger.LogWarning("User creation failed for email {Email}: {Error}", request.Email, result.Error);

                if (result.Error.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                {
                    return Conflict(new { error = result.Error });
                }

                return BadRequest(new { error = result.Error });
            }

            await TryLogAuditAsync(adminId, new AuditLogContext(
                adminId,
                AuditActionType.CreateUser,
                AuditResourceType.User,
                result.UserId,
                $"Admin created user {result.Email} with role {result.Role}"));

            _logger.LogInformation("User created successfully by admin {AdminId}: {Email}", adminId, result.Email);

            var response = new AdminUserResponse
            {
                Id = result.UserId,
                Name = result.Name,
                Email = result.Email,
                Role = result.Role
            };

            return Created($"/api/admin/users/{result.UserId}", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user for admin {AdminId}", adminId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while creating the user" });
        }
    }

    /// <summary>
    /// Gets all user accounts in the system.
    /// Requires Admin role.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var users = await _adminService.GetAllUsersAsync(page, pageSize);
            await TryLogAuditAsync(userId, new AuditLogContext(userId, AuditActionType.ViewUsers, AuditResourceType.User, null, "Admin viewed all users"));

            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all users for admin {AdminId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving users" });
        }
    }

    /// <summary>
    /// Gets all events in the system regardless of ownership.
    /// Requires Admin role.
    /// </summary>
    [HttpGet("events")]
    public async Task<IActionResult> GetAllEvents([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var events = await _adminService.GetAllEventsAsync(page, pageSize);
            await TryLogAuditAsync(userId, new AuditLogContext(userId, AuditActionType.ViewEvents, AuditResourceType.Event, null, "Admin viewed all events"));

            return Ok(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all events for admin {AdminId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving events" });
        }
    }

    /// <summary>
    /// Gets audit logs for the system. Optionally filters by admin user ID.
    /// Requires Admin role.
    /// </summary>
    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] Guid? userId = null)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized();
        }

        try
        {
            var logs = userId.HasValue
                ? await _auditLogService.GetLogsForUserAsync(userId.Value)
                : await _auditLogService.GetAllLogsAsync();

            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving audit logs for admin {AdminId}", adminId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while retrieving audit logs" });
        }
    }

    /// <summary>
    /// Increments the stock (Quantity) of an existing ticket type on an event.
    /// Concurrency-safe: mirrors the ReservationService SELECT ... FOR UPDATE row lock (ATS-003).
    /// </summary>
    /// <param name="eventId">ID of the event owning the ticket type</param>
    /// <param name="ticketTypeId">ID of the ticket type to increment</param>
    /// <param name="request">Body with the positive additional quantity (≤ 1000)</param>
    /// <returns>200 with the updated ticket type and recomputed availability</returns>
    [HttpPost("events/{eventId:guid}/ticket-types/{ticketTypeId:guid}/stock")]
    public async Task<IActionResult> AddTicketStock(Guid eventId, Guid ticketTypeId, [FromBody] AddTicketStockRequest request)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            var tt = await _eventService.AddTicketStockAsync(eventId, ticketTypeId, request.AdditionalQuantity);
            await TryLogAuditAsync(adminId, new AuditLogContext(adminId, AuditActionType.AddTicketStock,
                AuditResourceType.Event, eventId, Truncate($"Admin added {request.AdditionalQuantity} tickets to ticket type {tt.Name} (event {eventId})", 1000)));
            return Ok(tt);
        }
        catch (KeyNotFoundException) { return NotFound(new { error = "Event or ticket type not found" }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        // PEM-002/ADR-5: a finalized event is immutable — 409 RFC 7807, no audit.
        catch (EventFinalizedException)
        {
            return Problem(
                detail: "This event has already finished and can no longer be modified.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Event has already finished",
                type: "event-finalized");
        }
        catch (Exception ex) { _logger.LogError(ex, "Error adding ticket stock"); return StatusCode(500, new { error = "An error occurred while adding ticket stock" }); }
    }

    /// <summary>
    /// Creates a new ticket type (different zone/price) on an existing event (ATS-004).
    /// </summary>
    /// <param name="eventId">ID of the event to attach the new ticket type to</param>
    /// <param name="request">Body with name, price and initial quantity</param>
    /// <returns>201 with the created ticket type and recomputed availability</returns>
    [HttpPost("events/{eventId:guid}/ticket-types")]
    public async Task<IActionResult> AddTicketType(Guid eventId, [FromBody] AddTicketTypeRequest request)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            var tt = await _eventService.AddTicketTypeAsync(eventId, request.Name, request.Price, request.Quantity);
            await TryLogAuditAsync(adminId, new AuditLogContext(adminId, AuditActionType.AddTicketType,
                AuditResourceType.Event, eventId, Truncate($"Admin created ticket type {tt.Name} (price {tt.Price}, quantity {tt.Quantity}) for event {eventId}", 1000)));
            return CreatedAtAction(nameof(AddTicketType), new { eventId, ticketTypeId = tt.Id }, tt);
        }
        catch (KeyNotFoundException) { return NotFound(new { error = "Event not found" }); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        // PEM-002/ADR-5: a finalized event is immutable — 409 RFC 7807, no audit.
        catch (EventFinalizedException)
        {
            return Problem(
                detail: "This event has already finished and can no longer be modified.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Event has already finished",
                type: "event-finalized");
        }
        catch (Exception ex) { _logger.LogError(ex, "Error adding ticket type"); return StatusCode(500, new { error = "An error occurred while adding the ticket type" }); }
    }

    /// <summary>
    /// Lists an event's confirmed purchases with masked buyer data and per-event
    /// totalRefunded (APR-002). Admin-only via the class-level RequireAdminRole policy.
    /// </summary>
    /// <param name="eventId">Event whose purchases are listed</param>
    /// <returns>200 with the listing, or 404 when the event does not exist</returns>
    [HttpGet("events/{eventId:guid}/purchases")]
    public async Task<IActionResult> GetPurchases(Guid eventId)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            var result = await _adminPurchaseService.GetPurchasesAsync(eventId);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Event not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing purchases for event {EventId}", eventId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while listing purchases" });
        }
    }

    /// <summary>
    /// Refunds K tickets of a purchase atomically (APR-003/004/012/013): marks the K
    /// oldest non-refunded, non-used tickets refunded, inserts one Refunds ledger row
    /// and flips the Approved Transaction to Refunded only at zero active tickets — no
    /// MP money movement, no email, no motivo (APR-008). Audits RefundPurchase/Payment
    /// after commit.
    /// </summary>
    /// <param name="eventId">Event owning the purchase (used in the audit detail)</param>
    /// <param name="reservationId">Confirmed reservation to refund</param>
    /// <param name="request">Body with the number of tickets to refund (K &gt; 0) and the
    /// admin-defined refund amount (0 &lt; A ≤ unit price × K)</param>
    /// <returns>200 on success; 404 when the reservation is missing; 409 when K ≤ 0,
    /// K &gt; active remaining, the amount is invalid (A ≤ 0, A &gt; unit price × K, A
    /// with more than 2 decimal places), the purchase has no Approved transaction or a
    /// ticket IsUsed</returns>
    [HttpPost("events/{eventId:guid}/purchases/{reservationId:guid}/refund")]
    public async Task<IActionResult> RefundPurchase(Guid eventId, Guid reservationId,
        [FromBody] RefundPurchaseRequest request)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            await _adminPurchaseService.RefundPurchaseAsync(reservationId, request.Quantity, request.Amount, adminId);

            // APR-007: audit AFTER the transaction commits, best-effort, no motivo.
            // The admin-defined refund amount is part of the audit trail (D5) —
            // formatted with InvariantCulture so the detail is culture-free.
            await TryLogAuditAsync(adminId, new AuditLogContext(
                adminId,
                AuditActionType.RefundPurchase,
                AuditResourceType.Payment,
                reservationId,
                Truncate(string.Create(CultureInfo.InvariantCulture,
                    $"Admin refunded {request.Quantity} tickets of purchase {reservationId} for event {eventId}, amount {request.Amount}"), 1000)));

            return Ok(new { message = "Purchase refunded successfully" });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Reservation not found" });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding purchase {ReservationId} for event {EventId}", reservationId, eventId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while refunding the purchase" });
        }
    }

    /// <summary>
    /// Approves an event (EA-003): flips it to Approved so it appears in the public
    /// catalog. Any status may be approved (EA-005 — no state machine). Audits AFTER
    /// service success; an unknown event (KeyNotFoundException) writes NO audit.
    /// </summary>
    /// <param name="eventId">ID of the event to approve</param>
    /// <returns>200 with the updated event summary, or 404 when the event is unknown</returns>
    [HttpPost("events/{eventId:guid}/approve")]
    public async Task<IActionResult> ApproveEvent(Guid eventId)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            var summary = await _adminService.ApproveEventAsync(eventId);
            await TryLogAuditAsync(adminId, new AuditLogContext(
                adminId,
                AuditActionType.ApproveEvent,
                AuditResourceType.Event,
                eventId,
                Truncate($"Admin approved event {eventId}", 1000)));
            return Ok(summary);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Event not found" });
        }
        // PEM-002/ADR-5: a finalized event is immutable — 409 RFC 7807, no audit.
        catch (EventFinalizedException)
        {
            return Problem(
                detail: "This event has already finished and can no longer be modified.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Event has already finished",
                type: "event-finalized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving event {EventId}", eventId);
            return StatusCode(500, new { error = "An error occurred while approving the event" });
        }
    }

    /// <summary>
    /// Rejects an event (EA-004): flips it to Rejected, hiding it from the public
    /// catalog. The rejection reason is OPTIONAL and audit-only (never stored on the
    /// event); Details is truncated to the varchar(1000) cap. Audits AFTER service
    /// success; an unknown event writes NO audit.
    /// </summary>
    /// <param name="eventId">ID of the event to reject</param>
    /// <param name="request">Optional body carrying the rejection reason</param>
    /// <returns>200 with the updated event summary, or 404 when the event is unknown</returns>
    [HttpPost("events/{eventId:guid}/reject")]
    public async Task<IActionResult> RejectEvent(Guid eventId, [FromBody] RejectEventRequest? request)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            var summary = await _adminService.RejectEventAsync(eventId, request?.Reason);
            var details = Truncate(
                $"Admin rejected event {eventId}{(request?.Reason is { Length: > 0 } r ? $": {r}" : "")}", 1000);
            await TryLogAuditAsync(adminId, new AuditLogContext(
                adminId,
                AuditActionType.RejectEvent,
                AuditResourceType.Event,
                eventId,
                details));
            return Ok(summary);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "Event not found" });
        }
        // PEM-002/ADR-5: a finalized event is immutable — 409 RFC 7807, no audit.
        catch (EventFinalizedException)
        {
            return Problem(
                detail: "This event has already finished and can no longer be modified.",
                statusCode: StatusCodes.Status409Conflict,
                title: "Event has already finished",
                type: "event-finalized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting event {EventId}", eventId);
            return StatusCode(500, new { error = "An error occurred while rejecting the event" });
        }
    }

    /// <summary>
    /// Updates a user's role (AUM-001). Inherited RequireAdminRole policy. The
    /// self-edit guard (D4) runs BEFORE the service call so a self-edit leaves
    /// no role change and no audit row. Unknown users → 404; every successful
    /// edit records an UpdateUserRole audit entry referencing the target id
    /// (ids + role only — no credentials, no email). The account row is never
    /// deleted: role editing is the only revoke mechanism (SinAcceso grants
    /// nothing). Changes apply on the target's next login (AUM-004).
    /// </summary>
    /// <param name="userId">ID of the target user</param>
    /// <param name="request">The new role</param>
    /// <returns>200 with the updated user summary; 400 self-edit; 404 unknown user</returns>
    [HttpPut("users/{userId:guid}/role")]
    public async Task<IActionResult> UpdateUserRole(Guid userId, [FromBody] AdminUpdateUserRoleRequest request)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        // D4: controller-level self-edit guard, pre-service — guarantees the
        // spec's "no role change or audit row is persisted" without service coupling.
        if (userId == adminId)
        {
            return BadRequest(new { error = "You cannot change your own role" });
        }

        try
        {
            var summary = await _adminService.UpdateUserRoleAsync(userId, request.Role);

            await TryLogAuditAsync(adminId, new AuditLogContext(
                adminId,
                AuditActionType.UpdateUserRole,
                AuditResourceType.User,
                userId,
                Truncate($"Admin updated role for user {userId} to {request.Role}", 1000)));

            return Ok(summary);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "User not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while updating the user role" });
        }
    }

    /// <summary>
    /// Resets a user's password (AUM-003). Inherited RequireAdminRole policy.
    /// Generates a cryptographically secure temporary password server-side,
    /// persists only its BCrypt hash, and returns the cleartext credential
    /// EXACTLY ONCE in the response body for out-of-band handoff (admins never
    /// see, set, or choose user passwords). Self reset is allowed (the self
    /// role-edit guard does not apply — no lockout risk). The credential is
    /// never audited or logged; `Cache-Control: no-store` defends against any
    /// intermediary caching of the one-time body (D11). Unknown users → 404.
    /// </summary>
    /// <param name="userId">ID of the target user</param>
    /// <returns>200 with the one-time credential; 404 unknown user</returns>
    [HttpPost("users/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            var result = await _authService.ResetPasswordAsync(userId);

            if (!result.Success)
            {
                _logger.LogWarning("Password reset failed for user {UserId}: {Error}", userId, result.Error);

                // D6: mirrors CreateUser's string-mapping precedent.
                if (result.Error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new { error = result.Error });
                }

                return BadRequest(new { error = result.Error });
            }

            // D10: audit details carry ids only — NEVER the credential.
            await TryLogAuditAsync(adminId, new AuditLogContext(
                adminId,
                AuditActionType.ResetPassword,
                AuditResourceType.User,
                userId,
                Truncate($"Admin reset password for user {userId}", 1000)));

            // D11: the credential's single appearance — marked no-store.
            Response.Headers.CacheControl = "no-store";
            return Ok(new AdminResetPasswordResponse
            {
                TemporaryPassword = result.TemporaryPassword
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting password for user {UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An error occurred while resetting the password" });
        }
    }

    private async Task TryLogAuditAsync(Guid adminId, AuditLogContext context)
    {
        try
        {
            await _auditLogService.LogActionAsync(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Audit logging failed for admin {AdminId} action {ActionType} resource {ResourceType} id {ResourceId}; continuing with response",
                adminId, context.Action, context.Resource, context.ResourceId);
        }
    }

    /// <summary>
    /// Truncates a string to the given maximum length, mirroring the AuditLog.Details varchar(1000) column cap (D-6).
    /// </summary>
    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>
/// Request body for creating a new user account as an administrator.
/// </summary>
public record AdminCreateUserRequest(string Name, string Email, string Password, UserRole Role);

/// <summary>
/// Request body for a purchase refund with an admin-defined amount (APR-003). Plain
/// positional record with NO data annotations — validation lives in the service,
/// which throws InvalidOperationException → 409 for K ≤ 0, K &gt; active or an
/// invalid amount (A ≤ 0, A &gt; unit price × K, A with more than 2 decimal places —
/// uniform 200/404/409/500 mapping). Missing body → automatic 400 via [ApiController].
/// </summary>
public record RefundPurchaseRequest(int Quantity, decimal Amount);

/// <summary>
/// Response returned after a user is created by an administrator.
/// </summary>
public class AdminUserResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

/// <summary>
/// Request body for rejecting an event (EA-004). Reason is OPTIONAL (MAY be null)
/// and audit-only — it is never stored on the event.
/// </summary>
public record RejectEventRequest(string? Reason = null);

/// <summary>
/// Request body for updating a user's role (AUM-001). Binds via the
/// JsonStringEnumConverter — an invalid enum string fails automatic model
/// validation ([ApiController] → 400) before reaching the action.
/// </summary>
public record AdminUpdateUserRoleRequest(UserRole Role);

/// <summary>
/// Response body of a successful password reset (AUM-003): the one-time
/// temporary credential for out-of-band handoff. It is returned exactly once,
/// with `Cache-Control: no-store`, and is never stored, logged, or audited.
/// </summary>
public class AdminResetPasswordResponse
{
    public string TemporaryPassword { get; set; } = string.Empty;
}
