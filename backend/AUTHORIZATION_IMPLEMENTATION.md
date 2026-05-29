# Authorization Implementation Summary

## Task 6.2: Apply Authorization Attributes to Controllers

This document summarizes the authorization implementation for the Ticketera Online API.

## Current Implementation Status

### Completed Controllers

#### 1. AuthController
**Location:** `Controllers/AuthController.cs`

All endpoints are public (no authorization required):

| Endpoint | Method | Authorization | Description |
|----------|--------|---------------|-------------|
| `/api/auth/register` | POST | `[AllowAnonymous]` | User registration |
| `/api/auth/login` | POST | `[AllowAnonymous]` | User authentication |

**Rationale:** Authentication endpoints must be publicly accessible to allow users to register and log in.

#### 2. TestAuthorizationController
**Location:** `Controllers/TestAuthorizationController.cs`

Demonstrates all authorization patterns:

| Endpoint | Method | Authorization | Description |
|----------|--------|---------------|-------------|
| `/api/testauthorization/public` | GET | None | Public endpoint |
| `/api/testauthorization/protected` | GET | `[Authorize]` | Any authenticated user |
| `/api/testauthorization/admin` | GET | `[Authorize(Roles = "Admin")]` | Admin only |
| `/api/testauthorization/organizador` | GET | `[Authorize(Policy = "RequireOrganizadorRole")]` | Organizador or Admin |
| `/api/testauthorization/staff` | GET | `[Authorize(Policy = "RequireStaffRole")]` | Staff or Admin |
| `/api/testauthorization/event/{id}` | GET | `[Authorize(Policy = "EventOwnership")]` | Event owner or Admin |

**Purpose:** Testing and demonstration of authorization patterns.

## Authorization Patterns Reference

### Pattern 1: Public Endpoints (No Authorization)
```csharp
[HttpGet("public")]
[AllowAnonymous] // Optional but explicit
public IActionResult PublicEndpoint()
{
    return Ok();
}
```

**Use Cases:**
- Event catalog browsing
- Event detail viewing
- Ticket lookup
- Authentication endpoints (register/login)

### Pattern 2: Authenticated Users Only
```csharp
[HttpGet("protected")]
[Authorize]
public IActionResult ProtectedEndpoint()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Ok();
}
```

**Use Cases:**
- User profile endpoints
- User-specific data retrieval

### Pattern 3: Admin Only
```csharp
[HttpGet("admin")]
[Authorize(Roles = "Admin")]
public IActionResult AdminOnlyEndpoint()
{
    return Ok();
}
```

**Use Cases:**
- System-wide user management
- Audit log viewing
- System configuration

### Pattern 4: Organizador or Admin
```csharp
[HttpPost("events")]
[Authorize(Policy = "RequireOrganizadorRole")]
public IActionResult CreateEvent()
{
    return Ok();
}
```

**Use Cases:**
- Event creation
- Viewing organizer dashboard
- Accessing organizer metrics

### Pattern 5: Staff or Admin
```csharp
[HttpPost("tickets/validate")]
[Authorize(Policy = "RequireStaffRole")]
public IActionResult ValidateTicket()
{
    return Ok();
}
```

**Use Cases:**
- QR code scanning
- Ticket validation at events

### Pattern 6: Event Ownership or Admin
```csharp
[HttpPut("events/{id}")]
[Authorize(Policy = "EventOwnership")]
public IActionResult UpdateEvent(Guid id)
{
    return Ok();
}
```

**Use Cases:**
- Event modification
- Event deletion
- Event image upload
- Event-specific metrics

## Future Controller Authorization Patterns

### EventController (Task 7.4)
```csharp
[ApiController]
[Route("api/[controller]")]
public class EventController : ControllerBase
{
    // Public - Browse events
    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetAllEvents() { }
    
    // Public - View event details
    [HttpGet("{id}")]
    [AllowAnonymous]
    public IActionResult GetEvent(Guid id) { }
    
    // Organizador or Admin - Create event
    [HttpPost]
    [Authorize(Policy = "RequireOrganizadorRole")]
    public IActionResult CreateEvent([FromBody] CreateEventRequest request) { }
    
    // Event owner or Admin - Update event
    [HttpPut("{id}")]
    [Authorize(Policy = "EventOwnership")]
    public IActionResult UpdateEvent(Guid id, [FromBody] UpdateEventRequest request) { }
    
    // Event owner or Admin - Delete event
    [HttpDelete("{id}")]
    [Authorize(Policy = "EventOwnership")]
    public IActionResult DeleteEvent(Guid id) { }
    
    // Event owner or Admin - Upload image
    [HttpPost("{id}/image")]
    [Authorize(Policy = "EventOwnership")]
    public IActionResult UploadImage(Guid id, IFormFile image) { }
}
```

### ReservationController (Task 9.2)
```csharp
[ApiController]
[Route("api/[controller]")]
public class ReservationController : ControllerBase
{
    // Public - Create reservation (guests can reserve)
    [HttpPost]
    [AllowAnonymous]
    public IActionResult CreateReservation([FromBody] CreateReservationRequest request) { }
    
    // Public - Get reservation status
    [HttpGet("{id}")]
    [AllowAnonymous]
    public IActionResult GetReservation(Guid id) { }
}
```

### TicketController (Task 11.4)
```csharp
[ApiController]
[Route("api/[controller]")]
public class TicketController : ControllerBase
{
    // Public - Lookup tickets by email and DNI
    [HttpGet("lookup")]
    [AllowAnonymous]
    public IActionResult LookupTickets([FromQuery] string email, [FromQuery] string dni) { }
    
    // Staff or Admin - Validate ticket QR code
    [HttpPost("validate")]
    [Authorize(Policy = "RequireStaffRole")]
    public IActionResult ValidateTicket([FromBody] ValidateTicketRequest request) { }
}
```

### PaymentController (Task 12.4)
```csharp
[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    // Public - Create payment preference (guests can pay)
    [HttpPost("create-preference")]
    [AllowAnonymous]
    public IActionResult CreatePaymentPreference([FromBody] CreatePaymentRequest request) { }
    
    // Public - Webhook endpoint (Mercado Pago calls this)
    [HttpPost("webhook")]
    [AllowAnonymous]
    public IActionResult ProcessWebhook([FromBody] WebhookPayload payload, [FromHeader(Name = "x-signature")] string signature) { }
}
```

### MetricsController (Task 15.2)
```csharp
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    // Event owner or Admin - Get event metrics
    [HttpGet("events/{id}")]
    [Authorize(Policy = "EventOwnership")]
    public IActionResult GetEventMetrics(Guid id) { }
    
    // Organizador or Admin - Get organizer metrics
    [HttpGet("organizer")]
    [Authorize(Policy = "RequireOrganizadorRole")]
    public IActionResult GetOrganizerMetrics() { }
}
```

### AdminController (Task 16.1)
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")] // All endpoints require Admin role
public class AdminController : ControllerBase
{
    // Admin only - Get all users
    [HttpGet("users")]
    public IActionResult GetAllUsers() { }
    
    // Admin only - Get all events
    [HttpGet("events")]
    public IActionResult GetAllEvents() { }
    
    // Admin only - Get audit logs
    [HttpGet("audit-logs")]
    public IActionResult GetAuditLogs() { }
}
```

## Authorization Configuration

The authorization system is configured in `Program.cs`:

### Policies Configured
1. **EventOwnership** - Requires event ownership or Admin role
2. **RequireOrganizadorRole** - Requires Organizador or Admin role
3. **RequireStaffRole** - Requires Staff or Admin role
4. **RequireAdminRole** - Requires Admin role only

### Custom Authorization Handler
- **EventOwnershipHandler** - Validates event ownership by checking the database

## Requirements Validated

This authorization implementation validates the following requirements:

- **Requirement 1.6**: Role-based authorization enforcement
- **Requirement 14.1**: Admin access to all events
- **Requirement 14.2**: Admin can modify any event
- **Requirement 14.3**: Admin can delete any event

## Testing Authorization

Use the `TestAuthorizationController` to verify authorization is working correctly:

1. **Test without token** - Should return 401 Unauthorized for protected endpoints
2. **Test with valid token** - Should return 200 OK for authorized endpoints
3. **Test with wrong role** - Should return 403 Forbidden
4. **Test event ownership** - Should allow owner and admin, deny others

### Example Test Scenarios

```bash
# Test public endpoint (should work without token)
curl http://localhost:5000/api/testauthorization/public

# Test protected endpoint (should fail without token)
curl http://localhost:5000/api/testauthorization/protected

# Test protected endpoint (should work with token)
curl -H "Authorization: Bearer YOUR_JWT_TOKEN" http://localhost:5000/api/testauthorization/protected

# Test admin endpoint (should fail with non-admin token)
curl -H "Authorization: Bearer ORGANIZADOR_TOKEN" http://localhost:5000/api/testauthorization/admin

# Test admin endpoint (should work with admin token)
curl -H "Authorization: Bearer ADMIN_TOKEN" http://localhost:5000/api/testauthorization/admin
```

## Best Practices

1. **Explicit is better than implicit** - Always use `[AllowAnonymous]` for public endpoints to make intent clear
2. **Use policies over roles** - Policies are more flexible and maintainable
3. **Document authorization** - Add XML comments explaining authorization requirements
4. **Test thoroughly** - Test all authorization scenarios (no token, wrong role, correct role)
5. **Fail secure** - Default to requiring authorization, explicitly allow anonymous access
6. **Validate ownership** - Always check ownership in addition to role for resource-specific operations

## Next Steps

When implementing future controllers:

1. Review this document to determine the appropriate authorization pattern
2. Apply the authorization attributes as documented
3. Add XML documentation comments explaining authorization requirements
4. Test the authorization with different user roles
5. Update this document if new patterns are needed

## Related Files

- `Program.cs` - Authorization configuration
- `Authorization/EventOwnershipHandler.cs` - Custom authorization handler
- `Authorization/EventOwnershipRequirement.cs` - Custom authorization requirement
- `AUTHORIZATION.md` - Detailed authorization guide
- `Controllers/TestAuthorizationController.cs` - Authorization testing controller
