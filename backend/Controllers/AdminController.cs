using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    /// Refunds an unused full purchase atomically (APR-003/004): marks its tickets
    /// refunded and flips the Approved Transaction to Refunded — no MP money movement,
    /// no email, no motivo (APR-008). Audits RefundPurchase/Payment after commit.
    /// </summary>
    /// <param name="eventId">Event owning the purchase (used in the audit detail)</param>
    /// <param name="reservationId">Confirmed reservation to refund</param>
    /// <returns>200 on success; 404 when the reservation is missing; 409 when the
    /// purchase has no Approved transaction, is already refunded or a ticket IsUsed</returns>
    [HttpPost("events/{eventId:guid}/purchases/{reservationId:guid}/refund")]
    public async Task<IActionResult> RefundPurchase(Guid eventId, Guid reservationId)
    {
        if (!TryGetUserId(out var adminId)) return Unauthorized();

        try
        {
            await _adminPurchaseService.RefundPurchaseAsync(reservationId, adminId);

            // APR-007: audit AFTER the transaction commits, best-effort, no motivo.
            await TryLogAuditAsync(adminId, new AuditLogContext(
                adminId,
                AuditActionType.RefundPurchase,
                AuditResourceType.Payment,
                reservationId,
                Truncate($"Admin refunded purchase {reservationId} for event {eventId}", 1000)));

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
/// Response returned after a user is created by an administrator.
/// </summary>
public class AdminUserResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
