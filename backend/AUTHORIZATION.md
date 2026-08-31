# Authorization Guide

This document explains how to apply authorization attributes to controllers in the Ticketera Online API.

## Overview

The system uses JWT-based authentication with role-based authorization. Three user roles are supported:
- **Admin**: Full system access
- **Organizador**: Can create and manage their own events
- **Staff**: Can scan tickets at events

## Authorization Policies

The following authorization policies are configured in `Program.cs`:

### 1. Basic Authentication
```csharp
[Authorize]
```
Requires any authenticated user with a valid JWT token.

### 2. Role-Based Authorization
```csharp
[Authorize(Roles = "Admin")]
[Authorize(Roles = "Organizador")]
[Authorize(Roles = "Staff")]
```
Requires specific role(s). Multiple roles can be specified comma-separated.

### 3. Policy-Based Authorization

#### RequireAdminRole Policy
```csharp
[Authorize(Policy = "RequireAdminRole")]
```
Requires Admin role only.

#### RequireOrganizadorRole Policy
```csharp
[Authorize(Policy = "RequireOrganizadorRole")]
```
Requires Organizador OR Admin role.

#### RequireScanAccessRole Policy
```csharp
[Authorize(Policy = "RequireScanAccessRole")]
```
Requires Staff, Organizador OR Admin role (organizers scan as staff).

#### EventOwnership Policy
```csharp
[Authorize(Policy = "EventOwnership")]
```
Requires the user to be the event owner OR an Admin. This policy:
- Extracts the event ID from the route parameter `{id}`
- Checks if the authenticated user owns the event
- Grants access to Admins regardless of ownership

## Usage Examples

### Example 1: Public Endpoint (No Authorization)
```csharp
[HttpGet("public")]
public IActionResult GetPublicData()
{
    return Ok(new { message = "Public data" });
}
```

### Example 2: Protected Endpoint (Any Authenticated User)
```csharp
[HttpGet("profile")]
[Authorize]
public IActionResult GetProfile()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    return Ok(new { userId });
}
```

### Example 3: Admin-Only Endpoint
```csharp
[HttpGet("admin/users")]
[Authorize(Roles = "Admin")]
public IActionResult GetAllUsers()
{
    // Only admins can access this
    return Ok(users);
}
```

### Example 4: Organizador or Admin Endpoint
```csharp
[HttpPost("events")]
[Authorize(Policy = "RequireOrganizadorRole")]
public IActionResult CreateEvent([FromBody] CreateEventRequest request)
{
    // Organizadores and Admins can create events
    return Ok();
}
```

### Example 5: Event Ownership Endpoint
```csharp
[HttpPut("events/{id}")]
[Authorize(Policy = "EventOwnership")]
public IActionResult UpdateEvent(Guid id, [FromBody] UpdateEventRequest request)
{
    // Only the event owner or an Admin can update this event
    return Ok();
}
```

### Example 6: Staff, Organizador or Admin Endpoint
```csharp
[HttpPost("tickets/validate")]
[Authorize(Policy = "RequireScanAccessRole")]
public IActionResult ValidateTicket([FromBody] ValidateTicketRequest request)
{
    // Staff, Organizadores and Admins can validate tickets
    return Ok();
}
```

## Controller-Level Authorization

You can apply authorization at the controller level, and override it at the action level:

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize] // All actions require authentication by default
public class EventsController : ControllerBase
{
    [HttpGet] // Inherits [Authorize] from controller
    public IActionResult GetEvents()
    {
        return Ok(events);
    }
    
    [HttpPost]
    [Authorize(Policy = "RequireOrganizadorRole")] // Override with specific policy
    public IActionResult CreateEvent([FromBody] CreateEventRequest request)
    {
        return Ok();
    }
    
    [AllowAnonymous] // Override to allow public access
    [HttpGet("{id}")]
    public IActionResult GetEvent(Guid id)
    {
        return Ok(event);
    }
}
```

## Accessing User Information

Within authorized endpoints, you can access user information from claims:

```csharp
[HttpGet("me")]
[Authorize]
public IActionResult GetCurrentUser()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = User.FindFirst(ClaimTypes.Email)?.Value;
    var role = User.FindFirst(ClaimTypes.Role)?.Value;
    
    if (!Guid.TryParse(userId, out var userGuid))
    {
        return Unauthorized();
    }
    
    return Ok(new { userId = userGuid, email, role });
}
```

## Future Controller Implementation

When implementing future controllers (EventController, TicketController, etc.), apply authorization attributes according to these patterns:

### EventController
- `GET /api/events` - Public (no authorization)
- `GET /api/events/{id}` - Public (no authorization)
- `POST /api/events` - `[Authorize(Policy = "RequireOrganizadorRole")]`
- `PUT /api/events/{id}` - `[Authorize(Policy = "EventOwnership")]`
- `DELETE /api/events/{id}` - `[Authorize(Policy = "EventOwnership")]`
- `POST /api/events/{id}/image` - `[Authorize(Policy = "EventOwnership")]`

### TicketController
- `GET /api/tickets/lookup` - Public (no authorization)
- `POST /api/tickets/validate` - `[Authorize(Policy = "RequireScanAccessRole")]`

### MetricsController
- `GET /api/metrics/events/{id}` - `[Authorize(Policy = "EventOwnership")]`
- `GET /api/metrics/organizer` - `[Authorize(Policy = "RequireOrganizadorRole")]`

### AdminController
- All endpoints - `[Authorize(Roles = "Admin")]`

## Testing Authorization

The `TestAuthorizationController` demonstrates all authorization patterns and can be used to verify that authorization is working correctly. Test endpoints:

- `GET /api/testauthorization/public` - No auth required
- `GET /api/testauthorization/protected` - Any authenticated user
- `GET /api/testauthorization/admin` - Admin only
- `GET /api/testauthorization/organizador` - Organizador or Admin
- `GET /api/testauthorization/staff` - Staff or Admin
- `GET /api/testauthorization/event/{id}` - Event owner or Admin

## Requirements Validated

This authorization implementation validates:
- **Requirement 1.6**: Role-based authorization enforcement
- **Requirement 14.1**: Admin access to all events
- **Requirement 14.2**: Admin can modify any event
- **Requirement 14.3**: Admin can delete any event
