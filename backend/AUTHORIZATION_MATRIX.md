# Authorization Matrix

## Current Controllers Authorization Status

| Controller | Endpoint | Method | Authorization | Status |
|------------|----------|--------|---------------|--------|
| **AuthController** | `/api/auth/register` | POST | `[AllowAnonymous]` | ✅ Applied |
| **AuthController** | `/api/auth/login` | POST | `[AllowAnonymous]` | ✅ Applied |
> `TestAuthorizationController` was removed; `ScaffoldRemovalTests` asserts 404 for all its former routes.

## Future Controllers Authorization Plan

### EventController (Task 7.4)

| Endpoint | Method | Authorization | Reason |
|----------|--------|---------------|--------|
| `/api/events` | GET | `[AllowAnonymous]` | Public event browsing |
| `/api/events/{id}` | GET | `[AllowAnonymous]` | Public event details |
| `/api/events` | POST | `[Authorize(Policy = "RequireOrganizadorRole")]` | Only organizers can create events |
| `/api/events/manage` | GET | `[Authorize(Policy = "RequireScanAccessRole")]` | Scannable event list for the staff scan chooser (Staff/Organizador/Admin) |
| `/api/events/{id}` | PUT | `[Authorize(Policy = "EventOwnership")]` | Only owner or admin can update |
| `/api/events/{id}` | DELETE | `[Authorize(Policy = "EventOwnership")]` | Only owner or admin can delete |
| `/api/events/{id}/image` | POST | `[Authorize(Policy = "EventOwnership")]` | Only owner or admin can upload images |

### ReservationController (Task 9.2)

| Endpoint | Method | Authorization | Reason |
|----------|--------|---------------|--------|
| `/api/reservations` | POST | `[AllowAnonymous]` | Guests can reserve tickets |
| `/api/reservations/{id}` | GET | `[AllowAnonymous]` | Anyone can check reservation status |

### TicketController (Task 11.4)

| Endpoint | Method | Authorization | Reason |
|----------|--------|---------------|--------|
| `/api/tickets/lookup` | GET | `[AllowAnonymous]` | Public ticket lookup by email/DNI |
| `/api/tickets/validate` | POST | `[Authorize(Policy = "RequireScanAccessRole")]` | Staff and organizers can validate tickets (organizer scans as staff) |

### PaymentController (Task 12.4)

| Endpoint | Method | Authorization | Reason |
|----------|--------|---------------|--------|
| `/api/payments/create-preference` | POST | `[AllowAnonymous]` | Guests can initiate payment |
| `/api/payments/webhook` | POST | `[AllowAnonymous]` | Mercado Pago webhook (external) |

### MetricsController (Task 15.2)

| Endpoint | Method | Authorization | Reason |
|----------|--------|---------------|--------|
| `/api/metrics/events/{id}` | GET | `[Authorize(Policy = "EventOwnership")]` | Only owner or admin can view metrics |
| `/api/metrics/organizer` | GET | `[Authorize(Policy = "RequireOrganizadorRole")]` | Only organizers can view their metrics |

### AdminController (Task 16.1)

| Endpoint | Method | Authorization | Reason |
|----------|--------|---------------|--------|
| `/api/admin/users` | GET | `[Authorize(Roles = "Admin")]` | Admin-only system management |
| `/api/admin/events` | GET | `[Authorize(Roles = "Admin")]` | Admin-only system management |
| `/api/admin/audit-logs` | GET | `[Authorize(Roles = "Admin")]` | Admin-only audit access |

## Role Capabilities Matrix

| Feature | Guest | Organizador | Staff | Admin |
|---------|-------|-------------|-------|-------|
| Browse events | ✅ | ✅ | ✅ | ✅ |
| View event details | ✅ | ✅ | ✅ | ✅ |
| Reserve tickets | ✅ | ✅ | ✅ | ✅ |
| Lookup tickets | ✅ | ✅ | ✅ | ✅ |
| Create events | ❌ | ✅ | ❌ | ✅ |
| Edit own events | ❌ | ✅ | ❌ | ✅ |
| Delete own events | ❌ | ✅ | ❌ | ✅ |
| View own metrics | ❌ | ✅ | ❌ | ✅ |
| Scan tickets | ❌ | ✅ | ✅ | ✅ |
| Validate tickets | ❌ | ✅ | ✅ | ✅ |
| Edit any event | ❌ | ❌ | ❌ | ✅ |
| Delete any event | ❌ | ❌ | ❌ | ✅ |
| View all users | ❌ | ❌ | ❌ | ✅ |
| View audit logs | ❌ | ❌ | ❌ | ✅ |

## Authorization Policies

| Policy Name | Description | Allowed Roles |
|-------------|-------------|---------------|
| `EventOwnership` | Requires event ownership or admin | Event Owner, Admin |
| `RequireOrganizadorRole` | Requires organizador or admin | Organizador, Admin |
| `RequireScanAccessRole` | Requires staff, organizador or admin | Staff, Organizador, Admin |
| `RequireAdminRole` | Requires admin only | Admin |

## Custom Authorization Handlers

| Handler | Requirement | Logic |
|---------|-------------|-------|
| `EventOwnershipHandler` | `EventOwnershipRequirement` | Checks if user owns the event (via OrganizerId) or is an Admin |

## Testing Checklist

- [x] Public endpoints accessible without token
- [x] Protected endpoints require valid JWT token
- [x] Role-based endpoints enforce correct roles
- [x] Event ownership policy works correctly
- [x] Admin can access all resources
- [x] Non-owners cannot modify events
- [x] Staff and Organizadores can validate tickets (organizer scans as staff)
- [x] Organizadores can create events

## Requirements Coverage

| Requirement | Description | Status |
|-------------|-------------|--------|
| 1.6 | Role-based authorization enforcement | ✅ Implemented |
| 14.1 | Admin access to all events | ✅ Implemented |
| 14.2 | Admin can modify any event | ✅ Implemented |
| 14.3 | Admin can delete any event | ✅ Implemented |

## Security Best Practices Applied

1. ✅ **Explicit authorization** - All endpoints have explicit authorization attributes
2. ✅ **Fail secure** - Default to requiring authorization
3. ✅ **Least privilege** - Users only get access to what they need
4. ✅ **Policy-based** - Using policies instead of hardcoded role checks
5. ✅ **Custom handlers** - Event ownership validated at authorization level
6. ✅ **JWT validation** - Tokens validated on every request
7. ✅ **Role claims** - Roles stored in JWT claims for efficient checking

## Notes

- All existing controllers have proper authorization applied
- Future controllers have documented authorization patterns
- Authorization is configured in `Program.cs`
- Custom handlers are in `Authorization/` folder
- Test controller available for verification
- Documentation is comprehensive and up-to-date
