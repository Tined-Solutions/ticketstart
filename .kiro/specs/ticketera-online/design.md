# Design Document

## Overview

Ticketera Online is a full-stack online ticketing platform built as a monorepo with a React frontend and ASP.NET Core backend. The system enables event organizers to create and manage events, sell tickets through Mercado Pago integration, and validate attendees via QR code scanning. The architecture emphasizes security, reliability, and scalability with automatic reservation management, cryptographically signed tickets, and comprehensive audit logging.

## Architecture

### System Architecture

The system follows a client-server architecture with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────────┐
│                         Frontend                             │
│                      (React SPA)                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐   │
│  │  Event   │  │  Ticket  │  │Organizer │  │   QR     │   │
│  │ Catalog  │  │ Purchase │  │Dashboard │  │ Scanner  │   │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘   │
└─────────────────────────────────────────────────────────────┘
                            │
                         HTTPS/JSON
                            │
┌─────────────────────────────────────────────────────────────┐
│                    Backend (ASP.NET Core)                    │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐   │
│  │   Auth   │  │  Event   │  │  Ticket  │  │ Payment  │   │
│  │   API    │  │   API    │  │   API    │  │ Webhook  │   │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘   │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │     Expiration Service (IHostedService)              │  │
│  │     - Monitors expired reservations                  │  │
│  │     - Releases inventory automatically               │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
         │              │              │              │
    PostgreSQL    Cloudflare R2   Mercado Pago    Resend
    (Supabase)    (Image Storage)  (Payments)     (Email)
```


### Technology Stack

**Frontend:**
- React 18+ (SPA)
- React Router for navigation
- Axios for HTTP requests
- JWT storage in localStorage/sessionStorage
- QR code scanner library (e.g., html5-qrcode)

**Backend:**
- ASP.NET Core 8.0 Web API
- Entity Framework Core 8.0
- PostgreSQL (Supabase)
- JWT authentication (Microsoft.AspNetCore.Authentication.JwtBearer)
- HMAC-SHA256 for QR code signing

**External Services:**
- Supabase PostgreSQL (Transaction mode pooler on port 6543 for runtime, direct connection on port 5432 for migrations)
- Cloudflare R2 with AWS S3 SDK for image storage
- Mercado Pago Checkout Pro for payment processing
- Resend for transactional email delivery

**Infrastructure:**
- Monorepo structure: `/frontend` and `/backend`
- IHostedService for background reservation expiration


## Data Models

### Entity Relationship Diagram

```
┌─────────────┐         ┌─────────────┐         ┌─────────────┐
│    User     │         │    Event    │         │ TicketType  │
├─────────────┤         ├─────────────┤         ├─────────────┤
│ Id (PK)     │         │ Id (PK)     │         │ Id (PK)     │
│ Email       │         │ Name        │         │ EventId (FK)│
│ PasswordHash│         │ Description │         │ Name        │
│ Role        │◄───────┐│ Date        │◄───────┐│ Price       │
│ CreatedAt   │        ││ Location    │        ││ Quantity    │
└─────────────┘        ││ ImageUrl    │        │└─────────────┘
                       ││ OrganizerId │        │
                       │└─────────────┘        │
                       │                       │
                       │                       │
┌─────────────┐        │ ┌─────────────┐      │
│ Reservation │        │ │   Ticket    │      │
├─────────────┤        │ ├─────────────┤      │
│ Id (PK)     │        │ │ Id (PK)     │      │
│ UserId (FK) │────────┘ │ EventId (FK)│──────┘
│ EventId (FK)│──────────│ TicketTypeId│
│ TicketTypeId│          │ PurchaserEmail
│ Quantity    │          │ PurchaserDNI│
│ ExpiresAt   │          │ QRCodeData  │
│ Status      │          │ IsUsed      │
│ CreatedAt   │          │ UsedAt      │
└─────────────┘          │ CreatedAt   │
                         └─────────────┘

┌─────────────┐
│ Transaction │
├─────────────┤
│ Id (PK)     │
│ ReservationId (FK)
│ MercadoPagoId
│ Amount      │
│ Status      │
│ CreatedAt   │
│ UpdatedAt   │
└─────────────┘
```


### Entity Definitions

#### User
```csharp
public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public UserRole Role { get; set; } // Organizador, Staff, Admin
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public ICollection<Event> OrganizedEvents { get; set; }
}

public enum UserRole
{
    Organizador,
    Staff,
    Admin
}
```

#### Event
```csharp
public class Event
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public DateTime Date { get; set; }
    public string Location { get; set; }
    public string ImageUrl { get; set; }
    public Guid OrganizerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public User Organizer { get; set; }
    public ICollection<TicketType> TicketTypes { get; set; }
    public ICollection<Ticket> Tickets { get; set; }
}
```


#### TicketType
```csharp
public class TicketType
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Event Event { get; set; }
}
```

#### Reservation
```csharp
public class Reservation
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; } // Nullable for guest purchases
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public int Quantity { get; set; }
    public DateTime ExpiresAt { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public User User { get; set; }
    public Event Event { get; set; }
    public TicketType TicketType { get; set; }
}

public enum ReservationStatus
{
    Active,
    Expired,
    Confirmed,
    Cancelled
}
```


#### Ticket
```csharp
public class Ticket
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public Guid TicketTypeId { get; set; }
    public string PurchaserEmail { get; set; }
    public string PurchaserDNI { get; set; }
    public string QRCodeData { get; set; } // Contains ticket ID + HMAC signature
    public bool IsUsed { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Event Event { get; set; }
    public TicketType TicketType { get; set; }
}
```

#### Transaction
```csharp
public class Transaction
{
    public Guid Id { get; set; }
    public Guid ReservationId { get; set; }
    public string MercadoPagoId { get; set; }
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public Reservation Reservation { get; set; }
}

public enum TransactionStatus
{
    Pending,
    Approved,
    Rejected,
    Refunded
}
```


## Components and Interfaces

### Backend Components

#### 1. Authentication Service
**Responsibility:** User registration, login, JWT token generation and validation

```csharp
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request);
    Task<AuthResult> LoginAsync(LoginRequest request);
    Task<User> ValidateTokenAsync(string token);
}

public class AuthResult
{
    public bool Success { get; set; }
    public string Token { get; set; }
    public string Error { get; set; }
}
```

**Key Operations:**
- Hash passwords using BCrypt or PBKDF2
- Generate JWT tokens with user ID and role claims
- Validate JWT tokens on protected endpoints
- Enforce role-based authorization


#### 2. Event Service
**Responsibility:** Event CRUD operations, image upload, authorization

```csharp
public interface IEventService
{
    Task<Event> CreateEventAsync(CreateEventRequest request, Guid organizerId);
    Task<Event> GetEventByIdAsync(Guid eventId);
    Task<IEnumerable<Event>> GetAllPublishedEventsAsync();
    Task<Event> UpdateEventAsync(Guid eventId, UpdateEventRequest request, Guid userId, UserRole role);
    Task DeleteEventAsync(Guid eventId, Guid userId, UserRole role);
    Task<string> UploadEventImageAsync(Stream imageStream, string fileName);
}
```

**Key Operations:**
- Validate event ownership before modifications (except for Admin)
- Upload images to Cloudflare R2 using AWS S3 SDK
- Generate unique image identifiers
- Delete associated images when events are deleted
- Calculate ticket availability from TicketType quantities and sold tickets


#### 3. Reservation Service
**Responsibility:** Temporary ticket reservations with expiration management

```csharp
public interface IReservationService
{
    Task<Reservation> CreateReservationAsync(CreateReservationRequest request);
    Task<bool> ValidateReservationAsync(Guid reservationId);
    Task ReleaseExpiredReservationsAsync();
    Task ConfirmReservationAsync(Guid reservationId);
    Task CancelReservationAsync(Guid reservationId);
}
```

**Key Operations:**
- Create reservations with 10-minute expiration (DateTime.UtcNow.AddMinutes(10))
- Decrement ticket inventory atomically using database transactions
- Prevent double-booking through optimistic concurrency or row-level locking
- Release expired reservations back to inventory
- Handle race conditions for last available tickets


#### 4. Payment Service
**Responsibility:** Mercado Pago integration, webhook processing

```csharp
public interface IPaymentService
{
    Task<PaymentPreference> CreatePaymentPreferenceAsync(Guid reservationId);
    Task<WebhookResult> ProcessWebhookAsync(WebhookPayload payload, string signature);
    Task<RefundResult> InitiateRefundAsync(string mercadoPagoId, decimal amount);
}

public class PaymentPreference
{
    public string CheckoutUrl { get; set; }
    public string PreferenceId { get; set; }
}
```

**Key Operations:**
- Create Mercado Pago payment preferences with reservation details
- Validate webhook signatures using HMAC-SHA256
- Process successful payments: confirm reservation, create tickets
- Process failed payments: release reservation
- Initiate refunds for stock failures
- Log all webhook events for audit trail


#### 5. Ticket Service
**Responsibility:** Ticket generation, QR code creation, validation

```csharp
public interface ITicketService
{
    Task<IEnumerable<Ticket>> CreateTicketsAsync(Guid reservationId);
    Task<QRCodeValidationResult> ValidateQRCodeAsync(string qrCodeData, Guid eventId);
    Task<IEnumerable<Ticket>> LookupTicketsAsync(string email, string dni);
    string GenerateQRCode(Guid ticketId);
    bool VerifyQRCodeSignature(string qrCodeData);
}

public class QRCodeValidationResult
{
    public bool IsValid { get; set; }
    public string Error { get; set; }
    public Ticket Ticket { get; set; }
}
```

**Key Operations:**
- Generate unique QR codes for each ticket
- Sign QR codes using HMAC-SHA256: `HMAC(ticketId + timestamp, secretKey)`
- Encode QR code data: `{ticketId}:{timestamp}:{signature}`
- Verify signatures during validation
- Check ticket usage status and event association
- Mark tickets as used atomically to prevent double-scanning
- Generate visual QR code images using a library (e.g., QRCoder)


#### 6. Email Service
**Responsibility:** Transactional email delivery via Resend

```csharp
public interface IEmailService
{
    Task<EmailResult> SendTicketEmailAsync(string recipientEmail, IEnumerable<Ticket> tickets, Event eventDetails);
    Task<EmailResult> SendRefundNotificationAsync(string recipientEmail, decimal amount, string reason);
}

public class EmailResult
{
    public bool Success { get; set; }
    public string Error { get; set; }
}
```

**Key Operations:**
- Send ticket confirmation emails with QR codes embedded
- Include event details (name, date, location)
- Include purchase confirmation details
- Send refund notification emails
- Implement retry logic for failed deliveries
- Log all email attempts and results


#### 7. Metrics Service
**Responsibility:** Calculate event metrics for organizer dashboard

```csharp
public interface IMetricsService
{
    Task<EventMetrics> GetEventMetricsAsync(Guid eventId);
    Task<IEnumerable<EventMetrics>> GetOrganizerMetricsAsync(Guid organizerId);
}

public class EventMetrics
{
    public Guid EventId { get; set; }
    public int TotalTicketsSold { get; set; }
    public decimal TotalRevenue { get; set; }
    public int RemainingInventory { get; set; }
    public int TicketsScanned { get; set; }
}
```

**Key Operations:**
- Calculate tickets sold from Ticket table
- Calculate revenue from Ticket and TicketType tables
- Calculate remaining inventory from TicketType quantities minus sold tickets
- Calculate scanned tickets from Ticket.IsUsed flag
- Perform calculations in real-time based on current data


#### 8. Reservation Expiration Service (Background Worker)
**Responsibility:** Continuously monitor and release expired reservations

```csharp
public class ReservationExpirationService : IHostedService
{
    private Timer _timer;
    private readonly IServiceProvider _serviceProvider;
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Run every 30 seconds
        _timer = new Timer(CheckExpiredReservations, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        return Task.CompletedTask;
    }
    
    private async void CheckExpiredReservations(object state)
    {
        using var scope = _serviceProvider.CreateScope();
        var reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>();
        await reservationService.ReleaseExpiredReservationsAsync();
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        return Task.CompletedTask;
    }
}
```

**Key Operations:**
- Run as IHostedService background worker
- Check for expired reservations every 30 seconds
- Release expired reservations atomically
- Restore ticket inventory
- Log expiration events


### Frontend Components

#### 1. Event Catalog Component
**Responsibility:** Display all published events

**Features:**
- Grid/list view of events
- Event cards with image, name, date, location
- Click to navigate to event detail page
- Filter/search functionality (optional enhancement)

#### 2. Event Detail Component
**Responsibility:** Display single event with ticket purchase flow

**Features:**
- Full event information display
- Ticket type selection with quantity picker
- Real-time availability display
- "Reserve Tickets" button
- Countdown timer for active reservations

#### 3. Checkout Component
**Responsibility:** Handle payment flow

**Features:**
- Display reservation summary
- Countdown timer showing reservation expiration
- Redirect to Mercado Pago checkout
- Handle return from payment gateway
- Display confirmation or error messages


#### 4. Ticket Lookup Component
**Responsibility:** Allow users to retrieve tickets

**Features:**
- Form with email and DNI inputs
- Display retrieved tickets with QR codes
- Download/print functionality for QR codes
- Error handling for no results

#### 5. QR Scanner Component (Staff)
**Responsibility:** Scan and validate tickets at event entrance

**Features:**
- Web-based QR code scanner using device camera
- Real-time validation feedback (visual and audio)
- Display validation results (success/error with reason)
- Scan history log
- Role-based access (Staff only)

#### 6. Organizer Dashboard Component
**Responsibility:** Event management and metrics for organizers

**Features:**
- List of organizer's events
- Event metrics display (sales, revenue, inventory, scans)
- Create/edit/delete event functionality
- Event image upload
- Ticket type management


#### 7. Admin Panel Component
**Responsibility:** System-wide management for admins

**Features:**
- View all events (regardless of ownership)
- Modify/delete any event
- View all user accounts
- System audit logs
- Role-based access (Admin only)

#### 8. Authentication Components
**Responsibility:** User registration and login

**Features:**
- Registration form with email, password, role selection
- Login form with email and password
- JWT token storage and management
- Protected route handling
- Automatic redirect to login for unauthenticated access


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: User Registration Creates Valid Accounts

*For any* valid registration data (email, password, role), the system SHALL create a user account with the provided email, a hashed password, and the assigned role.

**Validates: Requirements 1.2**

### Property 2: Valid Login Returns Valid JWT

*For any* registered user with valid credentials, logging in SHALL return a JWT token that can be validated and contains the correct user ID and role claims.

**Validates: Requirements 1.3**

### Property 3: Invalid Credentials Rejected

*For any* invalid credentials (non-existent email or incorrect password), login attempts SHALL be rejected with an authentication error.

**Validates: Requirements 1.4**

### Property 4: Role-Based Authorization Enforcement

*For any* role-specific operation, the system SHALL enforce authorization rules such that only users with the appropriate role can perform the operation.

**Validates: Requirements 1.6**

### Property 5: Event Rendering Includes All Required Fields

*For any* event, the rendered output SHALL include name, date, location, description, and image URL.

**Validates: Requirements 2.2**

### Property 6: Ticket Availability Calculation Correctness

*For any* event with ticket types, the calculated availability SHALL equal the ticket type quantity minus the number of confirmed tickets sold.

**Validates: Requirements 2.6**

### Property 7: Image ID Uniqueness

*For any* set of uploaded images, all generated image identifiers SHALL be unique.

**Validates: Requirements 3.2**

### Property 8: Invalid Image File Rejection

*For any* file that does not meet image type or size requirements, the upload SHALL be rejected with a validation error.

**Validates: Requirements 3.4**

### Property 9: Event Deletion Removes Associated Images

*For any* event with an associated image, deleting the event SHALL remove the image from R2 storage.

**Validates: Requirements 3.6**

### Property 10: Reservation Creation Sets Correct Expiration

*For any* ticket selection, creating a reservation SHALL set the expiration time to exactly 10 minutes from creation.

**Validates: Requirements 4.1**

### Property 11: Reservation Decrements Inventory

*For any* reservation with quantity N, the available ticket inventory SHALL decrease by N.

**Validates: Requirements 4.2**

### Property 12: Active Reservations Prevent Double-Booking

*For any* active reservation, other users SHALL NOT be able to reserve the same tickets until the reservation expires or is confirmed.

**Validates: Requirements 4.4**

### Property 13: Expired Reservations Restore Inventory

*For any* expired reservation with quantity N, the available ticket inventory SHALL increase by N when the reservation is released.

**Validates: Requirements 4.5**

### Property 14: Payment Preference Contains Complete Data

*For any* reservation, the generated Mercado Pago payment preference SHALL include reservation details, ticket quantities, and the correct total amount.

**Validates: Requirements 5.2**

### Property 15: Successful Payment Creates Tickets

*For any* successful payment webhook, the system SHALL convert the associated reservation into confirmed tickets.

**Validates: Requirements 5.6**

### Property 16: Failed Payment Releases Reservation

*For any* failed payment webhook, the system SHALL release the associated reservation and restore inventory.

**Validates: Requirements 5.7**

### Property 17: Webhook Signature Validation

*For any* incoming webhook, the system SHALL validate the HMAC signature and reject webhooks with invalid signatures.

**Validates: Requirements 5.8**

### Property 18: QR Code Uniqueness

*For any* set of generated tickets, all QR codes SHALL be unique.

**Validates: Requirements 6.1**

### Property 19: QR Code Signature Validity

*For any* generated QR code, the HMAC-SHA256 signature SHALL be valid when verified with the secret key.

**Validates: Requirements 6.2**

### Property 20: QR Code Format Correctness

*For any* generated QR code, it SHALL encode the ticket identifier, timestamp, and HMAC signature in the format `{ticketId}:{timestamp}:{signature}`.

**Validates: Requirements 6.3**

### Property 21: QR Code Signature Verification

*For any* QR code presented for validation, the system SHALL verify the HMAC-SHA256 signature and reject codes with invalid signatures.

**Validates: Requirements 6.6, 6.7**

### Property 22: Email Contains All Ticket QR Codes

*For any* ticket confirmation email, the email SHALL include QR codes for all tickets in the purchase.

**Validates: Requirements 7.2**

### Property 23: Email Contains Event Details

*For any* ticket confirmation email, the email SHALL include the event name, date, and location.

**Validates: Requirements 7.3**

### Property 24: Email Contains Purchase Confirmation

*For any* ticket confirmation email, the email SHALL include purchase confirmation details.

**Validates: Requirements 7.4**

### Property 25: Email Delivery Retry on Failure

*For any* failed email delivery attempt, the system SHALL log the error and retry delivery.

**Validates: Requirements 7.6**

### Property 26: Ticket Lookup Returns Correct Matches

*For any* email and DNI combination, the ticket lookup SHALL return all and only tickets that match both the email and DNI.

**Validates: Requirements 8.2, 8.3, 8.5**

### Property 27: Double-Scan Prevention

*For any* ticket that has already been scanned and marked as used, subsequent scan attempts SHALL be rejected with an "already used" error.

**Validates: Requirements 9.4**

### Property 28: Event-Specific Ticket Validation

*For any* ticket, validation SHALL succeed only when scanned at the event for which the ticket was purchased.

**Validates: Requirements 9.5**

### Property 29: Valid Ticket Marked as Used

*For any* valid, unused ticket scanned at the correct event, the system SHALL mark the ticket as used and return success.

**Validates: Requirements 9.6**

### Property 30: Event Creation Establishes Ownership

*For any* event created by an organizador, the event SHALL be associated with that organizador as the owner.

**Validates: Requirements 10.3**

### Property 31: Event Validation Rejects Invalid Data

*For any* event creation request missing required fields (name, date, location, ticket types, quantities, prices), the system SHALL reject the request with a validation error.

**Validates: Requirements 10.4**

### Property 32: Non-Owner Modification Prevention

*For any* event, modification attempts by users who are not the owner (and not admins) SHALL be rejected with a forbidden error.

**Validates: Requirements 10.7**

### Property 33: Dashboard Displays Owner's Events Only

*For any* organizador viewing the dashboard, only events owned by that organizador SHALL be displayed.

**Validates: Requirements 11.2**

### Property 34: Tickets Sold Calculation Correctness

*For any* event, the displayed tickets sold count SHALL equal the number of confirmed tickets in the database for that event.

**Validates: Requirements 11.3**

### Property 35: Revenue Calculation Correctness

*For any* event, the displayed total revenue SHALL equal the sum of (ticket price × quantity) for all confirmed tickets.

**Validates: Requirements 11.4**

### Property 36: Remaining Inventory Calculation Correctness

*For any* event, the displayed remaining inventory SHALL equal the total ticket type quantities minus confirmed tickets sold minus active reservations.

**Validates: Requirements 11.5**

### Property 37: Scanned Tickets Count Correctness

*For any* event, the displayed scanned tickets count SHALL equal the number of tickets marked as used (IsUsed = true).

**Validates: Requirements 11.6**

### Property 38: Stock Failure Triggers Refund

*For any* confirmed payment where ticket inventory is insufficient, the system SHALL initiate a refund via Mercado Pago.

**Validates: Requirements 12.2**

### Property 39: Refund Logging

*For any* refund transaction, the system SHALL log the stock failure and refund details.

**Validates: Requirements 12.3**

### Property 40: Refund Notification Email

*For any* refund, the system SHALL send an email notification to the purchaser explaining the refund reason.

**Validates: Requirements 12.4**

### Property 41: Concurrent Purchase Prevention (No Overselling)

*For any* concurrent purchase attempts on the last available tickets, the system SHALL prevent overselling by ensuring total confirmed tickets never exceed ticket type quantity.

**Validates: Requirements 12.6**

### Property 42: Admin Access to All Events

*For any* admin user, they SHALL have access to view, modify, and delete all events regardless of ownership.

**Validates: Requirements 14.1, 14.2, 14.3**

### Property 43: Admin Action Audit Logging

*For any* admin action (view, modify, delete), the system SHALL log the action with timestamp, admin user ID, and action details.

**Validates: Requirements 14.6**

### Property 44: Database Connection Failure Handling

*For any* database connection failure, the system SHALL handle it gracefully and return an appropriate error response without crashing.

**Validates: Requirements 15.5**

### Property 45: Database Error Logging

*For any* database error, the system SHALL log the error with timestamp, context, and error details.

**Validates: Requirements 15.6**

### Property 46: Error Logging Format

*For any* error, the system SHALL log it with timestamp, context, and stack trace.

**Validates: Requirements 16.1**

### Property 47: HTTP Status Code Correctness

*For any* error condition, the system SHALL return the appropriate HTTP status code (400 for validation errors, 401 for authentication errors, 403 for authorization errors, 404 for not found, 409 for conflicts, 500 for server errors).

**Validates: Requirements 16.2**

### Property 48: User-Friendly Error Messages

*For any* error returned to the frontend, the error message SHALL be user-friendly and not expose sensitive system details.

**Validates: Requirements 16.3**

### Property 49: Payment Webhook Audit Logging

*For any* payment webhook received, the system SHALL log the webhook event with timestamp, payload, and processing result.

**Validates: Requirements 16.5**

### Property 50: QR Validation Audit Logging

*For any* QR code validation attempt, the system SHALL log the attempt with timestamp, ticket ID, event ID, and validation result.

**Validates: Requirements 16.6**

### Property 51: Sensitive Data Protection in Logs

*For any* error or log entry, the system SHALL NOT expose sensitive information such as passwords, full payment details, or secret keys.

**Validates: Requirements 16.7**

## API Endpoints

### Authentication Endpoints

```
POST /api/auth/register
Request: { email, password, role }
Response: { token, userId, role }

POST /api/auth/login
Request: { email, password }
Response: { token, userId, role }
```

### Event Endpoints

```
GET /api/events
Response: [ { id, name, date, location, description, imageUrl, ticketTypes, availability } ]

GET /api/events/{id}
Response: { id, name, date, location, description, imageUrl, ticketTypes, availability }

POST /api/events
Auth: Organizador, Admin
Request: { name, date, location, description, ticketTypes: [{ name, price, quantity }] }
Response: { id, ...eventData }

PUT /api/events/{id}
Auth: Organizador (owner), Admin
Request: { name, date, location, description }
Response: { id, ...eventData }

DELETE /api/events/{id}
Auth: Organizador (owner), Admin
Response: 204 No Content

POST /api/events/{id}/image
Auth: Organizador (owner), Admin
Request: multipart/form-data with image file
Response: { imageUrl }
```


### Reservation Endpoints

```
POST /api/reservations
Request: { eventId, ticketTypeId, quantity }
Response: { reservationId, expiresAt }

GET /api/reservations/{id}
Response: { id, eventId, ticketTypeId, quantity, expiresAt, status }
```

### Payment Endpoints

```
POST /api/payments/create-preference
Request: { reservationId }
Response: { checkoutUrl, preferenceId }

POST /api/payments/webhook
Request: Mercado Pago webhook payload
Response: 200 OK
```

### Ticket Endpoints

```
GET /api/tickets/lookup
Query: ?email={email}&dni={dni}
Response: [ { id, eventId, ticketTypeId, qrCodeData, isUsed } ]

POST /api/tickets/validate
Auth: Staff, Admin
Request: { qrCodeData, eventId }
Response: { isValid, error, ticketDetails }
```


### Metrics Endpoints

```
GET /api/metrics/events/{id}
Auth: Organizador (owner), Admin
Response: { eventId, totalTicketsSold, totalRevenue, remainingInventory, ticketsScanned }

GET /api/metrics/organizer
Auth: Organizador, Admin
Response: [ { eventId, eventName, totalTicketsSold, totalRevenue, remainingInventory, ticketsScanned } ]
```

### Admin Endpoints

```
GET /api/admin/users
Auth: Admin
Response: [ { id, email, role, createdAt } ]

GET /api/admin/events
Auth: Admin
Response: [ { id, name, organizerId, ...eventData } ]
```


## Security Design

### Authentication and Authorization

**JWT Token Structure:**
```json
{
  "sub": "userId",
  "email": "user@example.com",
  "role": "Organizador",
  "exp": 1234567890,
  "iat": 1234567890
}
```

**Authorization Rules:**
- **Guest**: Browse events, purchase tickets, lookup tickets
- **Organizador**: All guest actions + create/manage own events + view own metrics
- **Staff**: All guest actions + scan/validate tickets
- **Admin**: All actions on all resources

**Implementation:**
- Use `[Authorize]` attribute on protected endpoints
- Use `[Authorize(Roles = "Admin")]` for admin-only endpoints
- Custom authorization handlers for ownership checks (e.g., organizador can only modify own events)


### QR Code Security

**QR Code Format:**
```
{ticketId}:{timestamp}:{hmacSignature}
```

**Signature Generation:**
```csharp
public string GenerateQRCode(Guid ticketId)
{
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var message = $"{ticketId}:{timestamp}";
    var signature = ComputeHmacSha256(message, _secretKey);
    return $"{message}:{signature}";
}

private string ComputeHmacSha256(string message, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    return Convert.ToBase64String(hash);
}
```

**Signature Verification:**
```csharp
public bool VerifyQRCodeSignature(string qrCodeData)
{
    var parts = qrCodeData.Split(':');
    if (parts.Length != 3) return false;
    
    var message = $"{parts[0]}:{parts[1]}";
    var providedSignature = parts[2];
    var expectedSignature = ComputeHmacSha256(message, _secretKey);
    
    return providedSignature == expectedSignature;
}
```

**Security Properties:**
- QR codes cannot be forged without the secret key
- Timestamp prevents replay attacks (optional: add expiration check)
- Each ticket has a unique QR code
- Signature verification happens server-side


### Webhook Security

**Mercado Pago Webhook Validation:**
```csharp
public bool ValidateWebhookSignature(string payload, string signature, string secret)
{
    var expectedSignature = ComputeHmacSha256(payload, secret);
    return signature == expectedSignature;
}
```

**Webhook Processing Flow:**
1. Receive webhook POST request
2. Validate signature using Mercado Pago secret
3. If invalid, return 401 Unauthorized and log attempt
4. If valid, process payment status
5. Return 200 OK to acknowledge receipt

### Data Protection

**Sensitive Data Handling:**
- Store password hashes only (never plaintext passwords)
- Use BCrypt or PBKDF2 for password hashing
- Store HMAC secret key in environment variables (never in code)
- Store Mercado Pago credentials in environment variables
- Do not log sensitive data (passwords, payment details, full QR codes)
- Sanitize error messages to prevent information leakage

**Database Security:**
- Use parameterized queries (EF Core handles this)
- Implement proper indexing for performance
- Use connection pooling via Supabase pooler (port 6543)
- Use direct connection (port 5432) for migrations only


## Concurrency and Race Condition Handling

### Ticket Inventory Management

**Problem:** Multiple users attempting to purchase the last available tickets simultaneously.

**Solution:** Use database transactions with optimistic concurrency control or row-level locking.

**Implementation Option 1: Optimistic Concurrency (EF Core)**
```csharp
public class TicketType
{
    public Guid Id { get; set; }
    public int Quantity { get; set; }
    
    [Timestamp]
    public byte[] RowVersion { get; set; }
}

public async Task<Reservation> CreateReservationAsync(CreateReservationRequest request)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        var ticketType = await _context.TicketTypes.FindAsync(request.TicketTypeId);
        
        if (ticketType.Quantity < request.Quantity)
            throw new InsufficientInventoryException();
        
        ticketType.Quantity -= request.Quantity;
        
        var reservation = new Reservation
        {
            // ... reservation details
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync(); // Will throw DbUpdateConcurrencyException if conflict
        await transaction.CommitAsync();
        
        return reservation;
    }
    catch (DbUpdateConcurrencyException)
    {
        await transaction.RollbackAsync();
        throw new ConcurrencyException("Tickets no longer available");
    }
}
```


**Implementation Option 2: Pessimistic Locking (Raw SQL)**
```csharp
public async Task<Reservation> CreateReservationAsync(CreateReservationRequest request)
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // Lock the row for update
        var ticketType = await _context.TicketTypes
            .FromSqlRaw("SELECT * FROM \"TicketTypes\" WHERE \"Id\" = {0} FOR UPDATE", request.TicketTypeId)
            .FirstOrDefaultAsync();
        
        if (ticketType.Quantity < request.Quantity)
            throw new InsufficientInventoryException();
        
        ticketType.Quantity -= request.Quantity;
        
        var reservation = new Reservation
        {
            // ... reservation details
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        
        _context.Reservations.Add(reservation);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        
        return reservation;
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```


### Ticket Validation (Double-Scanning Prevention)

**Problem:** Staff member scans the same ticket multiple times.

**Solution:** Atomic update with check-and-set pattern.

```csharp
public async Task<QRCodeValidationResult> ValidateQRCodeAsync(string qrCodeData, Guid eventId)
{
    if (!VerifyQRCodeSignature(qrCodeData))
        return new QRCodeValidationResult { IsValid = false, Error = "Invalid signature" };
    
    var ticketId = ExtractTicketId(qrCodeData);
    
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        var ticket = await _context.Tickets
            .Where(t => t.Id == ticketId && t.EventId == eventId)
            .FirstOrDefaultAsync();
        
        if (ticket == null)
            return new QRCodeValidationResult { IsValid = false, Error = "Ticket not found" };
        
        if (ticket.IsUsed)
            return new QRCodeValidationResult { IsValid = false, Error = "Ticket already used" };
        
        ticket.IsUsed = true;
        ticket.UsedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        
        return new QRCodeValidationResult { IsValid = true, Ticket = ticket };
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
}
```


## Error Handling

### Error Response Format

All API errors return a consistent format:

```json
{
  "error": {
    "code": "INSUFFICIENT_INVENTORY",
    "message": "Not enough tickets available",
    "details": {
      "requested": 5,
      "available": 2
    }
  }
}
```

### Error Categories

**Client Errors (4xx):**
- `400 Bad Request`: Invalid input data
- `401 Unauthorized`: Missing or invalid JWT token
- `403 Forbidden`: Insufficient permissions
- `404 Not Found`: Resource does not exist
- `409 Conflict`: Concurrency conflict or business rule violation

**Server Errors (5xx):**
- `500 Internal Server Error`: Unexpected server error
- `503 Service Unavailable`: External service (payment, email) unavailable


### Global Exception Handler

```csharp
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception occurred");
        
        var (statusCode, errorCode, message) = exception switch
        {
            InsufficientInventoryException => (409, "INSUFFICIENT_INVENTORY", exception.Message),
            ConcurrencyException => (409, "CONCURRENCY_CONFLICT", exception.Message),
            UnauthorizedException => (401, "UNAUTHORIZED", "Authentication required"),
            ForbiddenException => (403, "FORBIDDEN", "Insufficient permissions"),
            NotFoundException => (404, "NOT_FOUND", exception.Message),
            ValidationException => (400, "VALIDATION_ERROR", exception.Message),
            _ => (500, "INTERNAL_ERROR", "An unexpected error occurred")
        };
        
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new
        {
            error = new
            {
                code = errorCode,
                message = message
            }
        }, cancellationToken);
        
        return true;
    }
}
```



## Testing Strategy

### Overview

The testing strategy for Ticketera Online employs a dual approach combining property-based testing (PBT) for universal correctness properties and example-based unit/integration tests for specific scenarios and infrastructure verification.

### Property-Based Testing

**Scope:** All correctness properties defined in the Correctness Properties section SHALL be implemented as property-based tests.

**Configuration:**
- Minimum 100 iterations per property test
- Each property test MUST reference its design document property using the tag format: **Feature: ticketera-online, Property {number}: {property_text}**

**Test Categories:**

1. **Authentication and Authorization Properties (Properties 1-4)**
   - Generate random valid/invalid user credentials
   - Test across different roles (Organizador, Staff, Admin)
   - Verify JWT token generation and validation
   - Verify role-based access control

2. **Event Management Properties (Properties 5-9, 30-32)**
   - Generate random event data with varying fields
   - Test event creation, modification, deletion
   - Verify ownership and authorization rules
   - Test image upload and cleanup

3. **Reservation and Inventory Properties (Properties 10-13, 36, 41)**
   - Generate random ticket selections and quantities
   - Test reservation creation and expiration
   - Verify inventory management and concurrency control
   - Test race conditions with concurrent purchases

4. **Payment Processing Properties (Properties 14-17, 38-40)**
   - Generate random payment webhooks (success/failure)
   - Test payment preference creation
   - Verify webhook signature validation
   - Test refund logic for stock failures

5. **QR Code Properties (Properties 18-21, 27-29)**
   - Generate random ticket data
   - Test QR code generation and uniqueness
   - Verify HMAC-SHA256 signature generation and validation
   - Test double-scan prevention and event-specific validation

6. **Email Properties (Properties 22-25, 40)**
   - Generate random ticket confirmations
   - Verify email content includes all required data
   - Test retry logic for failed deliveries

7. **Ticket Lookup Properties (Property 26)**
   - Generate random email/DNI combinations
   - Verify correct ticket matching logic

8. **Metrics Properties (Properties 33-37)**
   - Generate random events with varying sales, reservations, and scans
   - Verify calculation correctness for all metrics
   - Test dashboard filtering by ownership

9. **Admin Properties (Properties 42-43)**
   - Generate random admin actions across all events
   - Verify admin access to all resources
   - Verify audit logging

10. **Error Handling and Logging Properties (Properties 44-51)**
    - Generate random error conditions
    - Verify error logging format and content
    - Verify HTTP status codes
    - Verify sensitive data protection

### Example-Based Unit Tests

**Scope:** Specific scenarios, UI interactions, and edge cases not suitable for property-based testing.

**Test Categories:**

1. **Frontend Component Tests**
   - Event catalog rendering
   - Event detail page navigation
   - Checkout flow and redirect
   - QR scanner interface
   - Dashboard display
   - Form validation and submission

2. **UI Interaction Tests**
   - Login/registration flows
   - Ticket selection and reservation countdown
   - Scan result feedback (visual and audio)
   - Error message display

3. **Specific Edge Cases**
   - Empty event catalog
   - No tickets found in lookup
   - Reservation expiration during checkout
   - Network errors during payment redirect

### Integration Tests

**Scope:** External service integration and infrastructure verification.

**Test Categories:**

1. **Database Integration**
   - Connection pooling via Supabase (port 6543)
   - Transaction handling
   - Migration execution (port 5432)
   - Index performance

2. **External Service Integration**
   - Cloudflare R2 image upload/delete (1-2 sample images)
   - Mercado Pago payment preference creation (1-2 sample reservations)
   - Mercado Pago webhook reception (1-2 sample payloads)
   - Resend email delivery (1-2 sample emails)

3. **Background Service Integration**
   - Reservation expiration service startup
   - Periodic expiration checks
   - Inventory restoration on expiration

4. **API Endpoint Integration**
   - JWT authentication middleware
   - Protected endpoint access control
   - CORS configuration
   - Error handling middleware

### Smoke Tests

**Scope:** One-time setup and configuration verification.

**Test Categories:**

1. **Infrastructure Setup**
   - JWT authentication configured
   - Database connection established
   - R2 storage configured
   - Mercado Pago credentials configured
   - Resend email service configured

2. **Project Structure**
   - Monorepo structure (/frontend, /backend)
   - Frontend React application
   - Backend ASP.NET Core application
   - Independent application execution

3. **UI Component Existence**
   - Event catalog page
   - Ticket lookup form
   - QR scanner interface (Staff)
   - Organizer dashboard
   - Admin panel

### Test Implementation Guidelines

**Property-Based Test Structure:**
```csharp
[Fact]
public Property Property01_UserRegistrationCreatesValidAccounts()
{
    // Feature: ticketera-online, Property 1: For any valid registration data, 
    // the system SHALL create a user account with the provided email, 
    // a hashed password, and the assigned role.
    
    return Prop.ForAll(
        GenerateValidRegistrationData(),
        async (registrationData) =>
        {
            var result = await _authService.RegisterAsync(registrationData);
            
            Assert.True(result.Success);
            var user = await _userRepository.GetByEmailAsync(registrationData.Email);
            Assert.NotNull(user);
            Assert.Equal(registrationData.Email, user.Email);
            Assert.NotEqual(registrationData.Password, user.PasswordHash); // Hashed
            Assert.Equal(registrationData.Role, user.Role);
        }
    ).QuickCheckThrowOnFailure(iterations: 100);
}
```

**Integration Test Structure:**
```csharp
[Fact]
public async Task Integration_MercadoPagoPaymentPreferenceCreation()
{
    // Test with 1-2 sample reservations
    var reservation = await CreateSampleReservation();
    
    var preference = await _paymentService.CreatePaymentPreferenceAsync(reservation.Id);
    
    Assert.NotNull(preference);
    Assert.NotEmpty(preference.CheckoutUrl);
    Assert.NotEmpty(preference.PreferenceId);
}
```

**Smoke Test Structure:**
```csharp
[Fact]
public void Smoke_JwtAuthenticationConfigured()
{
    // Verify JWT authentication is configured
    var services = _serviceProvider.GetServices<IAuthenticationSchemeProvider>();
    Assert.NotEmpty(services);
}
```

### Test Data Management

**Property-Based Test Generators:**
- Use FsCheck or similar library for C# property-based testing
- Create custom generators for domain objects (User, Event, Ticket, Reservation)
- Ensure generators produce valid data within business constraints
- Include edge cases in generators (empty strings, boundary values, special characters)

**Integration Test Data:**
- Use test database with isolated schema
- Clean up test data after each test
- Use realistic sample data for external service calls
- Mock external services where appropriate to reduce costs

### Continuous Integration

**Test Execution:**
- Run all property-based tests (100 iterations each) on every commit
- Run integration tests on every commit
- Run smoke tests on deployment
- Fail build on any test failure

**Test Coverage:**
- Target 80%+ code coverage for backend services
- Target 70%+ code coverage for frontend components
- Measure property coverage: all 51 properties must have corresponding tests

### Performance Testing

**Load Testing Scenarios:**
- Concurrent ticket purchases (test Property 41)
- Concurrent QR code validations
- Dashboard metrics calculation under load
- Reservation expiration service under high reservation volume

**Performance Targets:**
- API response time: < 200ms for 95th percentile
- QR code validation: < 100ms
- Metrics calculation: < 500ms
- Reservation expiration check: < 5 seconds for 1000 expired reservations

### Security Testing

**Security Test Categories:**
1. **Authentication Testing**
   - JWT token tampering
   - Expired token handling
   - Missing token handling

2. **Authorization Testing**
   - Role escalation attempts
   - Cross-user resource access
   - Admin privilege verification

3. **Input Validation Testing**
   - SQL injection attempts (via parameterized queries)
   - XSS attempts in event descriptions
   - File upload validation bypass attempts

4. **Cryptographic Testing**
   - QR code signature forgery attempts
   - Webhook signature validation
   - Password hashing strength

### Test Maintenance

**Property Test Maintenance:**
- Update property tests when requirements change
- Add new properties for new features
- Refactor generators as domain model evolves
- Review property test failures for potential bugs

**Integration Test Maintenance:**
- Update integration tests when external APIs change
- Monitor external service test costs
- Replace integration tests with mocks when costs are prohibitive
- Keep integration tests focused on critical paths

**Documentation:**
- Maintain mapping between requirements, properties, and tests
- Document test data generators and their constraints
- Document known limitations and test gaps
- Document test environment setup and configuration
