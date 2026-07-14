# Tasks: Ticketera Online MVP

> Origin: restructured from `.kiro/specs/ticketera-online/tasks.md`. No new content added. Checkboxes reflect the progress state recorded in the source plan.

## Overview

This implementation plan breaks down the Ticketera Online MVP into discrete coding tasks covering the complete system: monorepo setup, ASP.NET Core backend with Entity Framework Core, React frontend, Supabase PostgreSQL database, JWT authentication, event management with Cloudflare R2 image storage, reservation system with automatic expiration, Mercado Pago payment integration, HMAC-SHA256 signed QR codes, Resend email delivery, ticket lookup, organizer dashboard with metrics, QR scanner interface, and admin panel. The plan includes property-based tests for all 51 correctness properties defined in the design document.

## Tasks

- [x] 1. Set up monorepo structure and project scaffolding
  - Create `/backend` folder with ASP.NET Core 8.0 Web API project
  - Create `/frontend` folder with React 18+ application (using Vite or Create React App)
  - Configure solution file for backend
  - Add README.md with local development setup instructions
  - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5_

- [x] 2. Configure backend infrastructure and dependencies
  - [x] 2.1 Install NuGet packages
    - Install Entity Framework Core 8.0 and PostgreSQL provider
    - Install Microsoft.AspNetCore.Authentication.JwtBearer
    - Install AWSSDK.S3 for Cloudflare R2 integration
    - Install BCrypt.Net for password hashing
    - Install QRCoder for QR code image generation
    - Install FsCheck or similar for property-based testing
    - _Requirements: 15.1, 1.1, 3.1_

  - [x] 2.2 Configure database connection
    - Add Supabase PostgreSQL connection string to appsettings.json
    - Configure connection pooling (port 6543 for runtime, port 5432 for migrations)
    - Set up DbContext with proper configuration
    - _Requirements: 15.1, 15.5_

  - [x] 2.3 Configure JWT authentication
    - Add JWT configuration to appsettings.json (secret key, issuer, audience, expiration)
    - Configure authentication middleware in Program.cs
    - Set up JWT bearer token validation
    - _Requirements: 1.1, 1.5_

  - [x] 2.4 Configure Cloudflare R2 storage
    - Add R2 credentials to appsettings.json (access key, secret key, bucket name, endpoint)
    - Configure AWS S3 client for R2 compatibility
    - _Requirements: 3.1_

  - [x] 2.5 Configure external services
    - Add Mercado Pago credentials to appsettings.json (access token, webhook secret)
    - Add Resend API key to appsettings.json
    - Add HMAC secret key for QR code signing to appsettings.json
    - _Requirements: 5.1, 7.5, 6.2_

- [x] 3. Define data models and Entity Framework Core entities
  - [x] 3.1 Create User entity
    - Define User class with Id, Email, PasswordHash, Role, CreatedAt properties
    - Define UserRole enum (Organizador, Staff, Admin)
    - Configure navigation properties for OrganizedEvents
    - _Requirements: 1.2, 15.2_

  - [x] 3.2 Create Event entity
    - Define Event class with Id, Name, Description, Date, Location, ImageUrl, OrganizerId, CreatedAt, UpdatedAt
    - Configure navigation properties for Organizer, TicketTypes, Tickets
    - _Requirements: 2.1, 10.1, 15.2_

  - [x] 3.3 Create TicketType entity
    - Define TicketType class with Id, EventId, Name, Price, Quantity, CreatedAt
    - Configure navigation property for Event
    - Add RowVersion property for optimistic concurrency control
    - _Requirements: 10.8, 15.2_

  - [x] 3.4 Create Reservation entity
    - Define Reservation class with Id, UserId, EventId, TicketTypeId, Quantity, ExpiresAt, Status, CreatedAt
    - Define ReservationStatus enum (Active, Expired, Confirmed, Cancelled)
    - Configure navigation properties for User, Event, TicketType
    - _Requirements: 4.1, 15.2_

  - [x] 3.5 Create Ticket entity
    - Define Ticket class with Id, EventId, TicketTypeId, PurchaserEmail, PurchaserDNI, QRCodeData, IsUsed, UsedAt, CreatedAt
    - Configure navigation properties for Event, TicketType
    - _Requirements: 6.4, 8.1, 15.2_

  - [x] 3.6 Create Transaction entity
    - Define Transaction class with Id, ReservationId, MercadoPagoId, Amount, Status, CreatedAt, UpdatedAt
    - Define TransactionStatus enum (Pending, Approved, Rejected, Refunded)
    - Configure navigation property for Reservation
    - _Requirements: 5.1, 15.2_

  - [x] 3.7 Configure DbContext and entity relationships
    - Create ApplicationDbContext inheriting from DbContext
    - Configure all entity relationships and foreign keys
    - Configure indexes for performance (Email, EventId, ReservationId, QRCodeData)
    - Add database seeding for initial admin user (optional)
    - _Requirements: 15.2, 15.4_

- [ ] 4. Create and run database migrations
  - [x] 4.1 Generate initial migration
    - Run `dotnet ef migrations add InitialCreate` using direct connection (port 5432)
    - Review generated migration files
    - _Requirements: 15.1_

  - [x] 4.2 Apply migration to database
    - Run `dotnet ef database update` using direct connection (port 5432)
    - Verify all tables created successfully
    - Verify indexes created
    - _Requirements: 15.1, 15.4_

- [x] 5. Implement authentication service and endpoints
  - [x] 5.1 Create IAuthService interface and implementation
    - Implement RegisterAsync method with password hashing using BCrypt
    - Implement LoginAsync method with credential validation
    - Implement JWT token generation with user ID and role claims
    - Implement ValidateTokenAsync method
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

  - [x] 5.2 Create AuthController with registration and login endpoints
    - Implement POST /api/auth/register endpoint
    - Implement POST /api/auth/login endpoint
    - Add input validation and error handling
    - Return appropriate HTTP status codes
    - _Requirements: 1.2, 1.3, 1.4, 16.2, 16.3_

  - [x] 5.3 Write property tests for authentication
    - **Property 1: User Registration Creates Valid Accounts** (Validates: Requirements 1.2)
    - **Property 2: Valid Login Returns Valid JWT** (Validates: Requirements 1.3)
    - **Property 3: Invalid Credentials Rejected** (Validates: Requirements 1.4)

  - [x] 5.4 Write property test for role-based authorization
    - **Property 4: Role-Based Authorization Enforcement** (Validates: Requirements 1.6)

- [x] 6. Implement authorization middleware and policies
  - [x] 6.1 Create custom authorization handlers
    - Implement event ownership authorization handler
    - Implement role-based authorization policies
    - Configure authorization policies in Program.cs
    - _Requirements: 1.6, 10.7_

  - [x] 6.2 Apply authorization attributes to controllers
    - Add [Authorize] attribute to protected endpoints
    - Add [Authorize(Roles = "Admin")] to admin-only endpoints
    - Add custom authorization requirements to event management endpoints
    - _Requirements: 1.6, 14.1, 14.2, 14.3_

- [x] 7. Implement event service and image storage
  - [x] 7.1 Create IEventService interface and implementation
    - Implement CreateEventAsync with ownership assignment
    - Implement GetEventByIdAsync with ticket availability calculation
    - Implement GetAllPublishedEventsAsync
    - Implement UpdateEventAsync with ownership validation
    - Implement DeleteEventAsync with ownership validation and image cleanup
    - _Requirements: 2.1, 2.4, 2.5, 10.1, 10.3, 10.4, 10.5, 10.6, 10.7_

  - [x] 7.2 Implement image upload to Cloudflare R2
    - Implement UploadEventImageAsync using AWS S3 SDK
    - Generate unique image identifiers (GUID-based)
    - Validate image file types (JPEG, PNG, WebP) and size limits (max 5MB)
    - Return R2 storage URL
    - _Requirements: 3.1, 3.2, 3.3, 3.4_

  - [x] 7.3 Implement image deletion from R2
    - Delete associated images when events are deleted
    - Handle cases where image deletion fails gracefully
    - _Requirements: 3.6_

  - [x] 7.4 Create EventController with CRUD endpoints
    - Implement GET /api/events (all published events)
    - Implement GET /api/events/{id} (single event with availability)
    - Implement POST /api/events (create event, Organizador/Admin only)
    - Implement PUT /api/events/{id} (update event, owner/Admin only)
    - Implement DELETE /api/events/{id} (delete event, owner/Admin only)
    - Implement POST /api/events/{id}/image (upload image, owner/Admin only)
    - _Requirements: 2.4, 2.5, 10.1, 10.5, 10.6_

  - [x] 7.5 Write property tests for event management
    - **Property 5: Event Rendering Includes All Required Fields** (Requirements 2.2)
    - **Property 6: Ticket Availability Calculation Correctness** (Requirements 2.6)
    - **Property 30: Event Creation Establishes Ownership** (Requirements 10.3)
    - **Property 31: Event Validation Rejects Invalid Data** (Requirements 10.4)
    - **Property 32: Non-Owner Modification Prevention** (Requirements 10.7)

  - [x]* 7.6 Write property tests for image storage
    - **Property 7: Image ID Uniqueness** (Requirements 3.2)
    - **Property 8: Invalid Image File Rejection** (Requirements 3.4)
    - **Property 9: Event Deletion Removes Associated Images** (Requirements 3.6)

- [x] 8. Checkpoint - Verify authentication and event management
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Implement reservation service with concurrency control
  - [x] 9.1 Create IReservationService interface and implementation
    - Implement CreateReservationAsync with 10-minute expiration
    - Use database transactions with optimistic concurrency control (RowVersion)
    - Decrement ticket inventory atomically
    - Implement ValidateReservationAsync to check reservation status
    - Implement ReleaseExpiredReservationsAsync to restore inventory
    - Implement ConfirmReservationAsync to convert reservation to tickets
    - Implement CancelReservationAsync to release reservation
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 12.6_

  - [x] 9.2 Create ReservationController with endpoints
    - Implement POST /api/reservations (create reservation)
    - Implement GET /api/reservations/{id} (get reservation status)
    - Add error handling for insufficient inventory
    - Add error handling for concurrency conflicts
    - _Requirements: 4.1, 4.3, 16.2, 16.3_

  - [x] 9.3 Write property tests for reservations
    - **Property 10: Reservation Creation Sets Correct Expiration** (Requirements 4.1)
    - **Property 11: Reservation Decrements Inventory** (Requirements 4.2)
    - **Property 12: Active Reservations Prevent Double-Booking** (Requirements 4.4)
    - **Property 13: Expired Reservations Restore Inventory** (Requirements 4.5)
    - **Property 41: Concurrent Purchase Prevention (No Overselling)** (Requirements 12.6)

- [x] 10. Implement reservation expiration background service
  - [x] 10.1 Create ReservationExpirationService as IHostedService
    - Implement StartAsync to start timer (check every 30 seconds)
    - Implement CheckExpiredReservations to call ReleaseExpiredReservationsAsync
    - Implement StopAsync to dispose timer
    - Register service in Program.cs
    - _Requirements: 4.5, 4.6, 4.7_

  - [x] 10.2 Write integration test for expiration service
    - Test service startup and periodic execution
    - Test inventory restoration on expiration
    - _Requirements: 4.6, 4.7_

- [x] 11. Implement QR code generation and validation service
  - [x] 11.1 Create ITicketService interface and QR code methods
    - Implement GenerateQRCode method with HMAC-SHA256 signing (format `{ticketId}:{timestamp}:{signature}`)
    - Implement VerifyQRCodeSignature method
    - Implement CreateTicketsAsync to generate tickets from confirmed reservation
    - Generate visual QR code images using QRCoder library
    - _Requirements: 6.1, 6.2, 6.3, 6.5, 6.6_

  - [x] 11.2 Implement QR code validation with double-scan prevention
    - Implement ValidateQRCodeAsync with signature verification
    - Check ticket usage status atomically
    - Check event association
    - Mark ticket as used with timestamp
    - Use database transaction to prevent double-scanning
    - _Requirements: 6.6, 6.7, 9.3, 9.4, 9.5, 9.6_

  - [x] 11.3 Implement ticket lookup functionality
    - Implement LookupTicketsAsync to query by email and DNI
    - Return all matching tickets with QR codes
    - _Requirements: 8.2, 8.3, 8.5_

  - [x] 11.4 Create TicketController with endpoints
    - Implement GET /api/tickets/lookup?email={email}&dni={dni}
    - Implement POST /api/tickets/validate (Staff/Admin only)
    - Add error handling for invalid QR codes, already used tickets, wrong event
    - _Requirements: 8.1, 8.2, 9.1, 9.2, 9.7_

  - [x] 11.5 Write property tests for QR codes
    - **Property 18: QR Code Uniqueness** (Requirements 6.1)
    - **Property 19: QR Code Signature Validity** (Requirements 6.2)
    - **Property 20: QR Code Format Correctness** (Requirements 6.3)
    - **Property 21: QR Code Signature Verification** (Requirements 6.6, 6.7)
    - **Property 27: Double-Scan Prevention** (Requirements 9.4)
    - **Property 28: Event-Specific Ticket Validation** (Requirements 9.5)
    - **Property 29: Valid Ticket Marked as Used** (Requirements 9.6)

  - [x] 11.6 Write property test for ticket lookup
    - **Property 26: Ticket Lookup Returns Correct Matches** (Requirements 8.2, 8.3, 8.5)

- [x] 12. Implement payment service with Mercado Pago integration
  - [x] 12.1 Create IPaymentService interface and implementation
    - Implement CreatePaymentPreferenceAsync to create Mercado Pago preference
    - Include reservation details, ticket quantities, and total amount
    - Return checkout URL and preference ID
    - _Requirements: 5.1, 5.2, 5.3_

  - [x] 12.2 Implement webhook processing
    - Implement ProcessWebhookAsync to handle payment notifications
    - Validate webhook signature using HMAC-SHA256
    - Process successful payments: confirm reservation, create tickets
    - Process failed payments: release reservation
    - Log all webhook events
    - _Requirements: 5.5, 5.6, 5.7, 5.8, 16.5_

  - [x] 12.3 Implement refund functionality
    - Implement InitiateRefundAsync for stock failures
    - Log refund transactions
    - _Requirements: 12.2, 12.3_

  - [x] 12.4 Create PaymentController with endpoints
    - Implement POST /api/payments/create-preference
    - Implement POST /api/payments/webhook (public endpoint)
    - Add webhook signature validation
    - _Requirements: 5.1, 5.5, 5.8_

  - [x]* 12.5 Write property tests for payment processing
    - **Property 14: Payment Preference Contains Complete Data** (Requirements 5.2)
    - **Property 15: Successful Payment Creates Tickets** (Requirements 5.6)
    - **Property 16: Failed Payment Releases Reservation** (Requirements 5.7)
    - **Property 17: Webhook Signature Validation** (Requirements 5.8)
    - **Property 38: Stock Failure Triggers Refund** (Requirements 12.2)
    - **Property 39: Refund Logging** (Requirements 12.3)

  - [x] 12.6 Fix purchaser DNI on ticket creation from payment webhook
    - **Problem:** `PaymentService.ProcessApprovedPaymentAsync` (PaymentService.cs:150) creates tickets via `_ticketService.CreateTicketsAsync(reservation.Id, email, "00000000")`, hardcoding the purchaser DNI to a placeholder.
    - **Impact:** `Ticket.PurchaserDNI` is `IsRequired` / non-nullable (EF + migration enforce it). `TicketService.LookupTicketsAsync` filters by `Where(t => t.PurchaserEmail == email && t.PurchaserDNI == dni)` (tickets spec.md:96-106, Requirement 6.x). Any ticket created via the approved-payment webhook path will have DNI `"00000000"`, so the real lookup-by-DNI returns empty for production tickets — silent correctness bug on the only real purchase path.
    - **Root cause:** `Reservation` model has no `PurchaserDNI` field (Reservation.cs has only Id, UserId, EventId, TicketTypeId, Quantity, ExpiresAt, Status, CreatedAt + navigations), so the payment webhook has no real DNI source at ticket-creation time.
    - **Fix scope (delegated slice):**
      - Add `PurchaserDNI` (string, required, max 50) to `Models/Reservation.cs`.
      - Update `Data/ApplicationDbContext.cs` Reservation configuration if needed.
      - Add EF Core migration (with a non-null default for existing rows, e.g. `"00000000"`, then backfill is out of scope for fresh dev DBs).
      - Capture DNI at reservation creation: update `ReservationService` create flow + create-reservation DTO + `ReservationController` to accept `purchaserDNI`.
      - `PaymentService.ProcessApprovedPaymentAsync`: pass `reservation.PurchaserDNI` instead of `"00000000"`.
      - Update affected tests: `ReservationControllerTests`, `ReservationServiceTests`, `ReservationPropertyTests`, `PaymentPropertyTests`, `PaymentControllerTests` to pass a real DNI through the reservation path.
      - Add a regression test asserting tickets created via the approved-payment webhook carry the reservation's real DNI (not `"00000000"`).
    - _Requirements: 5.6, 6.x (tickets lookup by email + DNI), 16.5_

- [ ] 12.7 Guard purchaser DNI sentinel in payment webhook (deferred from 12.6 review)
  - **Problem:** `PaymentService.ProcessApprovedPaymentAsync` has no guard against `reservation.PurchaserDNI` being empty/whitespace or the legacy migration sentinel "00000000". Pre-existing reservations (pre-deploy) flowing through the webhook would mint tickets with the placeholder DNI, silently re-introducing the Task 12.6 bug.
  - **Why deferred:** Project is pre-production with no legacy Active reservations; the regression window is theoretical, not real. Chosen 2026-07-07 to keep velocity for the 30-day bulk presentation.
  - **Fix scope:**
    - In `PaymentService.ProcessApprovedPaymentAsync`: before `CreateTicketsAsync`, if `string.IsNullOrWhiteSpace(reservation.PurchaserDNI) || reservation.PurchaserDNI == "00000000"`, log a structured warning (reservation.Id, paymentId) and fail the webhook (do not mint tickets with placeholder).
    - Test in `PaymentPropertyTests.cs`: legacy reservation with `PurchaserDNI = "00000000"` → webhook does not create tickets and logs warning.
  - _Requirements: 5.6, 6.x, 16.5_
  - _Status: deferred — track for post-presentation hardening._

- [x] 13. Checkpoint - Verify reservation, QR, and payment systems
  - Ensure all tests pass, ask the user if questions arise.

- [x] 14. Implement email service with Resend integration
  - [x] 14.1 Create IEmailService interface and implementation
    - Implement SendTicketEmailAsync with QR codes embedded
    - Include event details (name, date, location) in email
    - Include purchase confirmation details
    - Implement SendRefundNotificationAsync
    - Implement retry logic for failed deliveries
    - Log all email attempts and results
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 7.6, 12.4_

  - [x] 14.2 Create email templates
    - Design HTML email template for ticket confirmation
    - Design HTML email template for refund notification
    - Include QR code images in ticket email
    - _Requirements: 7.2, 7.3, 7.4_

  - [x]* 14.3 Write property tests for email delivery
    - **Property 22: Email Contains All Ticket QR Codes** (Requirements 7.2)
    - **Property 23: Email Contains Event Details** (Requirements 7.3)
    - **Property 24: Email Contains Purchase Confirmation** (Requirements 7.4)
    - **Property 25: Email Delivery Retry on Failure** (Requirements 7.6)
    - **Property 40: Refund Notification Email** (Requirements 12.4)

- [x] 15. Implement metrics service for organizer dashboard
  - [x] 15.1 Create IMetricsService interface and implementation
    - Implement GetEventMetricsAsync to calculate metrics for single event
    - Calculate total tickets sold from Ticket table
    - Calculate total revenue from Ticket and TicketType tables
    - Calculate remaining inventory (quantity - sold - active reservations)
    - Calculate tickets scanned (IsUsed = true count)
    - Implement GetOrganizerMetricsAsync to get metrics for all organizer's events
    - _Requirements: 11.1, 11.3, 11.4, 11.5, 11.6, 11.7, 11.8_

  - [x] 15.2 Create MetricsController with endpoints
    - Implement GET /api/metrics/events/{id} (Organizador owner/Admin only)
    - Implement GET /api/metrics/organizer (Organizador/Admin only)
    - _Requirements: 11.7_

  - [x]* 15.3 Write property tests for metrics calculations
    - **Property 33: Dashboard Displays Owner's Events Only** (Requirements 11.2)
    - **Property 34: Tickets Sold Calculation Correctness** (Requirements 11.3)
    - **Property 35: Revenue Calculation Correctness** (Requirements 11.4)
    - **Property 36: Remaining Inventory Calculation Correctness** (Requirements 11.5)
    - **Property 37: Scanned Tickets Count Correctness** (Requirements 11.6)

- [x] 16. Implement admin endpoints and audit logging
  - [x] 16.1 Create AdminController with system-wide endpoints
    - Implement GET /api/admin/users (Admin only)
    - Implement GET /api/admin/events (Admin only, all events)
    - _Requirements: 14.4, 14.5_

  - [x] 16.2 Implement audit logging for admin actions
    - Create audit log table/entity
    - Log all admin actions with timestamp, user ID, action type, resource ID
    - _Requirements: 14.6_

  - [x]* 16.3 Write property tests for admin capabilities
    - **Property 42: Admin Access to All Events** (Requirements 14.1, 14.2, 14.3)
    - **Property 43: Admin Action Audit Logging** (Requirements 14.6)

  - [x] 16.4 Harden admin endpoints and audit coverage (post-4R review)
    - Introduce `AuditActionType` and `AuditResourceType` enums with EF Core string conversions.
    - Add `AuditLogContext`, best-effort audit logging with `ILogger`, and deterministic log ordering (`Timestamp desc, Id desc`).
    - Paginate `GET /api/admin/users` and `GET /api/admin/events` with a hard 200-row cap.
    - Add `GET /api/admin/audit-logs` with optional `userId` filter.
    - Create `TicketeraControllerBase` for shared `TryGetUserId` helper and remove duplicated controller code.
    - Wire audit logging into `EventController` admin update/delete paths.
    - Update and expand `AdminControllerTests`, `EventControllerTests`, and `AdminPropertyTests` for new behavior and FsCheck v3 API.
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 14.6_

- [x] 17. Implement global error handling and logging
  - [x] 17.1 Create global exception handler
    - Implement IExceptionHandler for centralized error handling
    - Map exceptions to appropriate HTTP status codes
    - Return user-friendly error messages
    - Log all errors with timestamp, context, and stack trace
    - Ensure sensitive data is not exposed in logs or error messages
    - _Requirements: 16.1, 16.2, 16.3, 16.4, 16.7_

  - [x] 17.2 Configure logging infrastructure
    - Configure structured logging (Serilog or built-in logging)
    - Add log levels (Debug, Info, Warning, Error)
    - Configure log output (console, file, or external service)
    - _Requirements: 16.1, 16.5, 16.6_

  - [x] 17.3 Write property tests for error handling
    - **Property 44: Database Connection Failure Handling** (Requirements 15.5)
    - **Property 45: Database Error Logging** (Requirements 15.6)
    - **Property 46: Error Logging Format** (Requirements 16.1)
    - **Property 47: HTTP Status Code Correctness** (Requirements 16.2)
    - **Property 48: User-Friendly Error Messages** (Requirements 16.3)
    - **Property 49: Payment Webhook Audit Logging** (Requirements 16.5)
    - **Property 50: QR Validation Audit Logging** (Requirements 16.6)
    - **Property 51: Sensitive Data Protection in Logs** (Requirements 16.7)

  - [x] 17.4 Harden error handling and logging (post-4R review)
    - Fix R1-1: global redacting console formatter protecting every `_logger.*` call site.
    - Fix R1-2: hash DNI in `TicketController` lookup logs; add PII keys to `LogRedactor`.
    - Fix R1-3: complete `LogRedactor.SensitiveKeys` denylist and add regex failover for Bearer/JWT/long secrets.
    - Fix R1-4: drop raw `{Error}` from webhook warning log.
    - Fix R4-1: wrap `GlobalExceptionHandler.TryHandleAsync` in self-protection catch; special-case `OperationCanceledException` as 499 / Information log.
    - Fix R4-2: webhook authentication failure → 401; processing failure → 200 OK with `{paymentId, status: "failed", error: "PROCESSING_FAILED"}`.
    - Fix R4-3: add audit-write-failure variants for Properties 49 and 50; wrap audit catch logger call in inner try/catch.
    - Fix R3-1: drive Property 51 from real `LogRedactor.SensitiveKeys`; add negative property for non-sensitive keys.
    - Fix R3-2: convert Property 47 to parameterized `[Theory]` against the spec matrix.
    - Fix R3-3: assert `StackTrace` key in Property 46.
    - [x] 17.4.1 Micro-slice hardening (fresh-context 4R re-review)
      - Fix R1-NF-1: email leak in TicketController logs.
      - Fix R4-N-1: `OperationCanceledException` throws on already-cancelled token.
      - Fix R4-N-2: self-protection catch missing `Response.HasStarted` guard.
      - Fix R3-NF-2: `LogError` overload missing exception object.
      - Non-blocking: add formatter self-protection (R1-NF-3) and end-to-end Bearer/JWT test (R3-NF-4).
      - Non-blocking deferred: base64 over-redaction (R3-NF-3).
    - _Requirements: 16.1, 16.2, 16.3, 16.5, 16.6, 16.7_

- [x] 18. Checkpoint - Verify backend completeness
  - Ensure all tests pass, ask the user if questions arise.

- [x] 19. Set up frontend React application
  - [x] 19.1 Initialize React project and install dependencies
    - Create React app using Vite or Create React App
    - Install React Router for navigation
    - Install Axios for HTTP requests
    - Install QR code scanner library (html5-qrcode or react-qr-reader)
    - Install QR code display library (qrcode.react)
    - _Requirements: 13.2_

  - [x] 19.2 Configure API client and authentication
    - Create Axios instance with base URL configuration
    - Implement JWT token storage (localStorage or sessionStorage)
    - Create authentication context/provider
    - Implement token refresh logic (if applicable)
    - Add request interceptor to include JWT token in headers
    - _Requirements: 1.7_

  - [x] 19.3 Create routing structure
    - Set up React Router with routes for all pages
    - Implement protected route component for authenticated routes
    - Implement role-based route guards
    - Configure redirect to login for unauthenticated access
    - _Requirements: 1.8_

- [x] 20. Implement authentication components
  - [x] 20.1 Create registration component
    - Build registration form with email, password, role selection
    - Implement form validation
    - Call POST /api/auth/register endpoint
    - Store JWT token on successful registration
    - Redirect to appropriate page based on role
    - Display error messages
    - _Requirements: 1.2, 1.7_

  - [x] 20.2 Create login component
    - Build login form with email and password
    - Implement form validation
    - Call POST /api/auth/login endpoint
    - Store JWT token on successful login
    - Redirect to appropriate page based on role
    - Display error messages
    - _Requirements: 1.3, 1.4, 1.7, 1.8_

  - [x]* 20.3 Write unit tests for authentication components
    - Test form validation
    - Test successful login/registration flows
    - Test error handling and display

- [x] 21. Implement event catalog and browsing components
  - [x] 21.1 Create event catalog component
    - Fetch events from GET /api/events
    - Display events in grid or list view
    - Show event cards with image, name, date, location
    - Implement click handler to navigate to event detail page
    - Add loading and error states
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 21.2 Create event detail component
    - Fetch single event from GET /api/events/{id}
    - Display full event information
    - Display ticket types with prices and availability
    - Implement ticket quantity selector
    - Add "Reserve Tickets" button
    - _Requirements: 2.2, 2.3, 2.5, 2.6_

  - [x]* 21.3 Write unit tests for event browsing components
    - Test event catalog rendering
    - Test event detail display
    - Test navigation between pages
    - Test empty state handling

- [ ] 22. Implement reservation and checkout flow
  - [x] 22.1 Create reservation component
    - Call POST /api/reservations to create reservation
    - Display reservation confirmation with expiration time
    - Implement countdown timer showing remaining time
    - Handle reservation expiration (clear state, show notification)
    - _Requirements: 4.1, 4.3, 4.8, 4.9_

  - [x] 22.2 Create checkout component
    - Display reservation summary (event, tickets, total)
    - Show countdown timer
    - Call POST /api/payments/create-preference
    - Redirect to Mercado Pago checkout URL
    - _Requirements: 4.8, 5.3, 5.4_

  - [x] 22.3 Create payment return handler
    - Handle return from Mercado Pago (success/failure)
    - Display confirmation message or error
    - Show email delivery status
    - _Requirements: 5.4, 7.7_

  - [x]* 22.4 Write unit tests for checkout flow
    - Test reservation creation and timer
    - Test payment redirect
    - Test return handling

- [x] 23. Implement ticket lookup component
  - [x] 23.1 Create ticket lookup form
    - Build form with email and DNI inputs
    - Implement form validation
    - Call GET /api/tickets/lookup with query parameters
    - Display retrieved tickets with QR codes
    - Implement download/print functionality for QR codes
    - Handle no results case with appropriate message
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_

  - [x]* 23.2 Write unit tests for ticket lookup
    - Test form validation
    - Test successful lookup display
    - Test no results handling
    - Test QR code display

- [x] 24. Checkpoint - Verify frontend guest features
  - Ensure all tests pass, ask the user if questions arise.

- [x] 25. Implement QR scanner component for staff
  - [x] 25.1 Create QR scanner interface
    - Implement web-based QR scanner using html5-qrcode or react-qr-reader
    - Request camera permissions
    - Display camera feed
    - Implement QR code detection
    - _Requirements: 9.1_

  - [x] 25.2 Implement validation and feedback
    - Call POST /api/tickets/validate with scanned QR code and event ID
    - Display validation results (success or error with reason)
    - Implement visual feedback (green for success, red for error)
    - Implement audio feedback (beep sounds)
    - Show scan history log
    - Add role-based access control (Staff only)
    - _Requirements: 9.2, 9.7, 9.8, 9.9_

  - [x]* 25.3 Write unit tests for QR scanner
    - Test camera initialization
    - Test validation result display
    - Test visual and audio feedback
    - Test role-based access

- [ ] 26. Implement organizer dashboard and event management
  - [x] 26.1 Create event creation/edit form component
    - Build form with fields: name, date, location, description, image upload
    - Add ticket type management (add/remove ticket types with name, price, quantity)
    - Implement form validation
    - Call POST /api/events to create event
    - Call PUT /api/events/{id} to update event
    - Call POST /api/events/{id}/image to upload image
    - Display success/error messages
    - _Requirements: 10.1, 10.2, 10.4, 10.5, 3.1_

  - [x] 26.2 Create organizer dashboard component
    - Fetch organizer's events and metrics from GET /api/metrics/organizer
    - Display list of events with metrics (tickets sold, revenue, inventory, scans)
    - Add create event button
    - Add edit/delete buttons for each event
    - Implement delete confirmation dialog
    - Call DELETE /api/events/{id} to delete event
    - Refresh metrics on page load
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.9, 10.6_

  - [x] 26.3 Create event detail metrics view
    - Fetch single event metrics from GET /api/metrics/events/{id}
    - Display detailed metrics for the event
    - Show charts or visualizations (optional enhancement)
    - _Requirements: 11.7_

  - [x]* 26.4 Write unit tests for organizer dashboard
    - Test event list display
    - Test metrics display
    - Test event creation form
    - Test event edit/delete functionality

- [x] 27. Implement admin panel
  - [x] 27.1 Create admin dashboard component
    - Fetch all events from GET /api/admin/events
    - Fetch all users from GET /api/admin/users
    - Display events with owner information
    - Display user list with roles
    - Add edit/delete buttons for any event
    - Implement role-based access (Admin only)
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5_

  - [x]* 27.2 Write unit tests for admin panel
    - Test admin access control
    - Test event list display
    - Test user list display

- [x] 28. Implement UI/UX enhancements and styling
  - [x] 28.1 Add global styles and theme
    - Set up CSS framework (Tailwind, Bootstrap, or Material-UI)
    - Define color scheme and typography
    - Create reusable UI components (buttons, cards, forms, modals)
    - Ensure responsive design for mobile and desktop
    - _Requirements: 2.1, 2.2_

  - [x] 28.2 Add loading states and error handling
    - Implement loading spinners for async operations
    - Display error messages consistently across all components
    - Add toast notifications for success/error feedback
    - _Requirements: 16.4_

  - [x]* 28.3 Write accessibility tests
    - Test keyboard navigation
    - Test screen reader compatibility
    - Test color contrast ratios

- [x] 29. Checkpoint - Verify frontend completeness
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 30. Integration testing and end-to-end verification ⚠️ POSPUESTA — requiere credenciales externas (R2, MP, Resend)
  - [ ]* 30.1 Write integration tests for external services
    - Test Cloudflare R2 image upload/delete with 1-2 sample images
    - Test Mercado Pago payment preference creation with 1-2 sample reservations
    - Test Mercado Pago webhook reception with 1-2 sample payloads
    - Test Resend email delivery with 1-2 sample emails
    - _Requirements: 3.1, 5.1, 5.5, 7.5_

  - [ ]* 30.2 Write integration tests for database operations
    - Test connection pooling via Supabase (port 6543)
    - Test transaction handling and rollback
    - Test migration execution (port 5432)
    - Test index performance
    - _Requirements: 15.1, 15.3, 15.4_

  - [ ]* 30.3 Write integration tests for background service
    - Test reservation expiration service startup
    - Test periodic expiration checks
    - Test inventory restoration on expiration
    - _Requirements: 4.6, 4.7_

  - [ ]* 30.4 Write smoke tests for infrastructure setup
    - Test JWT authentication configured
    - Test database connection established
    - Test R2 storage configured
    - Test Mercado Pago credentials configured
    - Test Resend email service configured
    - Test monorepo structure exists
    - Test frontend and backend can run independently
    - _Requirements: 13.1, 13.2, 13.3, 13.4_

- [x] 31. Documentation and deployment preparation
  - [x] 31.1 Update README with setup instructions
    - Document prerequisites (Node.js 18+, .NET 9, PostgreSQL/Supabase)
    - Document environment variables for backend
    - Document environment variables for frontend
    - Document database migration steps
    - Document how to run backend and frontend locally
    - _Requirements: 13.5_

  - [x] 31.2 Create environment configuration templates
    - Create appsettings.json.template for backend
    - Create .env.template for frontend
    - Document all required configuration values
    - _Requirements: 13.5_

  - [x] 31.3 Add API documentation
    - Document all API endpoints in README API reference table
    - Enable Swagger XML comments (GenerateDocumentationFile + IncludeXmlComments)
    - Add Swagger/OpenAPI description and XML doc integration
    - Document authentication requirements in README
    - _Requirements: 1.1, 2.4, 5.1, 9.1_

- [x] 32. Final checkpoint and system verification
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery.
- Each task references specific requirements for traceability.
- Checkpoints ensure incremental validation throughout development.
- Property tests validate universal correctness properties (51 total properties).
- Unit tests validate specific examples and edge cases.
- Integration tests verify external service integration with minimal sample data.
- The backend uses ASP.NET Core 8.0 with Entity Framework Core and PostgreSQL.
- The frontend uses React 18+ with React Router and Axios.
- Database uses Supabase PostgreSQL with connection pooling (port 6543) and direct connection for migrations (port 5432).
- External services: Cloudflare R2 (images), Mercado Pago (payments), Resend (email).
- QR codes use HMAC-SHA256 signing for security.
- Reservation expiration runs as IHostedService background worker.
- All 51 correctness properties from the design document have corresponding property-based tests.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1", "2.1"] },
    { "id": 1, "tasks": ["2.2", "2.3", "2.4", "2.5"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3", "3.4", "3.5", "3.6"] },
    { "id": 3, "tasks": ["3.7"] },
    { "id": 4, "tasks": ["4.1"] },
    { "id": 5, "tasks": ["4.2"] },
    { "id": 6, "tasks": ["5.1", "6.1"] },
    { "id": 7, "tasks": ["5.2", "6.2"] },
    { "id": 8, "tasks": ["5.3", "5.4"] },
    { "id": 9, "tasks": ["7.1", "7.2", "7.3"] },
    { "id": 10, "tasks": ["7.4"] },
    { "id": 11, "tasks": ["7.5", "7.6"] },
    { "id": 12, "tasks": ["9.1"] },
    { "id": 13, "tasks": ["9.2"] },
    { "id": 14, "tasks": ["9.3", "10.1"] },
    { "id": 15, "tasks": ["10.2"] },
    { "id": 16, "tasks": ["11.1", "11.2", "11.3"] },
    { "id": 17, "tasks": ["11.4"] },
    { "id": 18, "tasks": ["11.5", "11.6"] },
    { "id": 19, "tasks": ["12.1", "12.2", "12.3"] },
    { "id": 20, "tasks": ["12.4"] },
    { "id": 21, "tasks": ["12.5"] },
    { "id": 22, "tasks": ["14.1", "14.2"] },
    { "id": 23, "tasks": ["14.3"] },
    { "id": 24, "tasks": ["15.1"] },
    { "id": 25, "tasks": ["15.2"] },
    { "id": 26, "tasks": ["15.3"] },
    { "id": 27, "tasks": ["16.1"] },
    { "id": 28, "tasks": ["16.2"] },
    { "id": 29, "tasks": ["16.3"] },
    { "id": 30, "tasks": ["17.1", "17.2"] },
    { "id": 31, "tasks": ["17.3"] },
    { "id": 32, "tasks": ["19.1"] },
    { "id": 33, "tasks": ["19.2", "19.3"] },
    { "id": 34, "tasks": ["20.1", "20.2"] },
    { "id": 35, "tasks": ["20.3"] },
    { "id": 36, "tasks": ["21.1", "21.2"] },
    { "id": 37, "tasks": ["21.3"] },
    { "id": 38, "tasks": ["22.1", "22.2", "22.3"] },
    { "id": 39, "tasks": ["22.4"] },
    { "id": 40, "tasks": ["23.1"] },
    { "id": 41, "tasks": ["23.2"] },
    { "id": 42, "tasks": ["25.1"] },
    { "id": 43, "tasks": ["25.2"] },
    { "id": 44, "tasks": ["25.3"] },
    { "id": 45, "tasks": ["26.1", "26.2", "26.3"] },
    { "id": 46, "tasks": ["26.4"] },
    { "id": 47, "tasks": ["27.1"] },
    { "id": 48, "tasks": ["27.2"] },
    { "id": 49, "tasks": ["28.1", "28.2"] },
    { "id": 50, "tasks": ["28.3"] },
    { "id": 51, "tasks": ["30.1", "30.2", "30.3", "30.4"] },
    { "id": 52, "tasks": ["31.1", "31.2", "31.3"] }
  ]
}
```

---

## ⚖️ Judgment Day Audit — Round 1 (2026-07-14)

> Revisión adversarial ciega por dos jueces independientes. **No se aplicaron fixes.**
> Veredicto: `JUDGMENT: ESCALATED ⚠️` — 8 CRITICALes confirmados.

### 🔴 CRITICAL — Confirmados (ambos jueces)

- [ ] **JD-C1 — Registro público indebido: NADIE se auto-registra**
  - **Regla de negocio**: En el momento 0 solo existe 1 cuenta Admin (la del cliente). Es el Admin —y solo el Admin— quien crea, asigna roles y gestiona todos los demás usuarios. No existe el auto-registro ni la auto-asignación de roles. Sin embargo, `POST /auth/register` es un endpoint público sin `[Authorize]` que permite a cualquiera crear una cuenta con cualquier rol, incluido Admin. También expone `Staff` en el dropdown del frontend (`Register.jsx`).
  - Archivos: `backend/Controllers/AuthController.cs:32-71`, `backend/Services/AuthService.cs:65-73`, `frontend/src/pages/Register.jsx:5-9,45-49`
  - Fix: eliminar el endpoint público `POST /auth/register` (o protegerlo con `[Authorize(Policy = "RequireAdminRole")]`). Crear un endpoint admin `POST /api/admin/users` donde solo el Admin crea usuarios y asigna roles (`Organizador`, `Staff`, `Admin`). Eliminar la página/componente `Register.jsx` del frontend público; mover la creación de usuarios al `AdminPanel`.

- [ ] **JD-C2 — Lookup de tickets expone QR codes sin protección**
  - `GET /api/tickets/lookup` (sin auth) devuelve QR codes completos en base64. Con email+DNI cualquiera puede robar entradas ajenas.
  - **Regla de negocio definida**: El lookup DEBE existir como red de seguridad sin fricción para el comprador (guest o registrado), pero solo debe devolver **información básica**: evento, fecha, ubicación, tipo de entrada, cantidad, estado ("Pagado"/"Usado"), e instrucciones de reenvío. El QR vive **exclusivamente en el email**.
  - Además, se necesita un **endpoint de reenvío** `POST /api/tickets/resend` con: rate limit (máx 3 por hora por email), CAPTCHA (Turnstile), y respuesta genérica ("Si hay entradas asociadas, recibirás un email") para evitar enumeración.
  - Archivos: `backend/Controllers/TicketController.cs:38-80`, `frontend/src/pages/TicketLookup.jsx:75-97`
  - Fix: (1) Eliminar `qrCodeData` y `qrSrc` de la respuesta del lookup público. (2) Crear `POST /api/tickets/resend` con rate limit + CAPTCHA + respuesta genérica. (3) Actualizar UI del lookup para mostrar info básica + botón de reenvío.

- [ ] **JD-C3 — Endpoint de detalle de reserva innecesario y peligroso**
  - `GET /api/reservations/{id}` con `[AllowAnonymous]` expone datos de la reserva (evento, tipo de entrada, precio, estado) y el **token de reserva** —que es la llave para crear una preferencia de pago en Mercado Pago— a cualquiera que tenga el GUID.
  - El endpoint no tiene caso de uso real: el `POST /api/reservations` ya devuelve toda la data necesaria en el body de la respuesta (línea 67). Si el comprador pierde el GUID, la reserva expiró en 10 minutos de todas formas. No hay flujo que justifique su existencia.
  - Archivos: `backend/Controllers/ReservationController.cs:109-162`
  - Fix: eliminar el endpoint `GetReservation`. Si en el futuro se necesita recuperar una reserva activa desde otro dispositivo, implementarlo con el token HMAC como parámetro de validación (`GET /api/reservations/{id}?token=xxx`).

- [ ] **JD-C4 — Tickets con email falso + email nunca enviado**
  - `PaymentService.ProcessApprovedPaymentAsync` (línea 171) crea tickets con `"guest@ticketera.com"` cuando `reservation.User` es null (guest checkout). `SendTicketEmailAsync` nunca se llama desde el flujo de pago. El DNI se recibe del frontend pero el email —que el formulario de checkout sí recolecta— **nunca se envía al backend** porque `CreateReservationRequest` no tiene campo `PurchaserEmail`. Irónicamente, el modelo `Ticket` ya tiene el campo `PurchaserEmail` y las migraciones lo crearon desde el día 1. `CheckoutReturn.jsx` muestra "Tus entradas fueron enviadas a tu email" mintiendo al comprador.
  - **Regla de negocio definida**: (1) El comprador ingresa su email en el checkout. (2) El frontend lo manda al backend en la creación de la reserva. (3) El backend lo almacena y lo usa al crear los tickets. (4) `PaymentService.ProcessApprovedPaymentAsync` llama a `IEmailService.SendTicketEmailAsync` después de crear los tickets. (5) El email solo se envía tras confirmación de pago; si el envío falla, no debe revertir la confirmación (mejor intentar reenvío que perder la venta).
  - **Validación anti-typo**: doble input de email en el frontend, donde el campo de confirmación tiene `onPaste` bloqueado (`e.preventDefault()`) para forzar escritura manual. Backend valida que ambos campos coincidan. Si no coinciden, `400 Bad Request` antes de crear la reserva.
  - Archivos: `backend/Controllers/ReservationController.cs:30-101`, `backend/Services/IReservationService.cs:82-88` (CreateReservationRequest), `backend/Services/PaymentService.cs:164-217`, `backend/Services/EmailService.cs`, `backend/Services/TicketService.cs:40-83`, `frontend/src/pages/Checkout.jsx:70-71,121-126,209-244`, `frontend/src/pages/CheckoutReturn.jsx:9-12`
  - Fix: (1) Agregar `PurchaserEmail` a `CreateReservationRequest`. (2) Enviarlo desde `Checkout.jsx` en el POST. (3) Almacenarlo en `Reservation` (o pasarlo al crear tickets). (4) Usarlo en `CreateTicketsAsync` en vez de `User?.Email ?? "guest@ticketera.com"`. (5) Llamar `_emailService.SendTicketEmailAsync` dentro de `ProcessApprovedPaymentAsync` tras crear tickets, con manejo de error que no revierta el pago. (6) Doble input de email en frontend con bloqueo de pegado en confirmación. (7) Validación server-side de coincidencia de emails.

- [ ] **JD-C5 — Race condition en stock de reservas: sobreventa por concurrencia**
  - El código actual calcula disponibilidad restando tickets vendidos + reservas activas del stock en memoria, sin bloqueo. Dos requests concurrentes leen "queda 1", ambas crean reserva → sobreventa. El `try-catch` de `DbUpdateConcurrencyException` con loop de reintento es **código muerto**: la creación de `Reservation` no modifica `TicketType.RowVersion`, así que nunca se dispara.
  - **Approach definido**: reemplazar el cálculo en memoria por un UPDATE condicional atómico con `ExecuteUpdateAsync`. Se agrega un campo `CurrentlyReserved` a `TicketType`. Al reservar: `UPDATE TicketType SET CurrentlyReserved += quantity WHERE Id = @id AND (Quantity - CurrentlyReserved - SoldCount) >= quantity`. Si el UPDATE afecta 0 filas → sin stock. Al expirar reserva: `UPDATE TicketType SET CurrentlyReserved -= quantity`. PostgreSQL serializa los UPDATE sobre la misma fila automáticamente, eliminando la race condition sin raw SQL.
  - **Escalabilidad**: este approach banca cientos de requests concurrentes por tipo de entrada. Si en el futuro se necesita Redis o colas para miles de concurrentes, la lógica de validación condicional se migra sin cambiar el diseño.
  - Archivos: `backend/Services/ReservationService.cs:75-128`, `backend/Services/ReservationExpirationService.cs:51-78`, `backend/Models/TicketType.cs`, `backend/Data/ApplicationDbContext.cs`
  - Fix: (1) Agregar columna `CurrentlyReserved` (int, default 0) a `TicketType` vía migración. (2) Reemplazar la lógica de disponibilidad en `CreateReservationAsync` por `ExecuteUpdateAsync` condicional. (3) Eliminar el loop de reintento con `RowVersion`. (4) Actualizar `ReleaseExpiredReservationsAsync` para decrementar `CurrentlyReserved` con `ExecuteUpdateAsync`. (5) Mantener `SoldCount` (se actualiza al crear tickets, no en reserva).

- [ ] **JD-C6 — Funcionalidad de impresión y QR expuesta en lookup público**
  - `TicketLookup.jsx` (`TicketCard.handlePrint`) inyecta datos del evento (controlados por el organizador) vía `document.write()` sin sanitizar → stored XSS. Además, `TicketCard` muestra el QR code en pantalla y permite descarga, exponiendo la entrada fuera del email.
  - **Regla de negocio definida**: El QR vive **exclusivamente en el email de confirmación**. El lookup público solo muestra información básica (evento, fecha, tipo de entrada, estado). No hay QR, no hay botón de impresión, no hay botón de descarga. La versión imprimible con QR es parte del template del email, no del frontend público.
  - Archivos: `frontend/src/pages/TicketLookup.jsx:64-162` (componente `TicketCard` completo)
  - Fix: reemplazar `TicketCard` por una tarjeta simplificada que muestre solo: nombre del evento, fecha, ubicación, tipo de entrada, precio, estado ("Válida"/"Usada"), e instrucciones ("Revisá tu email para ver el QR"). Eliminar `handlePrint`, `handleDownload` y cualquier renderizado de QR. Si en el futuro se necesita impresión, se resuelve del lado del template de email (HTML a PDF inline).

- [ ] **JD-C7 — Código scaffold en producción**
  - `/weatherforecast` (endpoint + record `WeatherForecast`) es basura de template .NET. `TestAuthorizationController` expone endpoints de diagnóstico público en `/api/testauthorization/*`.
  - **Decisión**: eliminar ambos sin reemplazo.
  - Archivos: `backend/Program.cs:218-243` (endpoint + record), `backend/Controllers/TestAuthorizationController.cs` (archivo completo)
  - Fix: eliminar el bloque `MapGet("/weatherforecast", ...)` y el record `WeatherForecast` de `Program.cs`. Eliminar `TestAuthorizationController.cs` completo.

- [ ] **JD-C8 — Idempotencia en webhooks de Mercado Pago**
  - Mercado Pago puede enviar el mismo webhook múltiples veces (documentado). El código actual procesa el duplicado como una anomalía: ve que la reserva ya no está `Active` → dispara `InitiateRefundAsync` → el cliente que pagó y recibió sus entradas recibe un refund automático.
  - **Approach definido**: Unique constraint sobre `Transaction.MercadoPagoId`. Al recibir webhook: (1) validar firma, (2) buscar `Transaction` por `MercadoPagoId` → si existe → `200 OK` (ya procesado), (3) si no existe → insertar `Transaction` + confirmar reserva + crear tickets. Si falla el insert por unique constraint (webhook concurrente) → devolver `200 OK`. PostgreSQL garantiza atomicidad del constraint, sin lookup extra ni tabla nueva.
  - Archivos: `backend/Services/PaymentService.cs:156-217`, `backend/Data/ApplicationDbContext.cs`, `backend/Models/Transaction.cs`
  - Fix: (1) Agregar índice unique a `Transaction.MercadoPagoId` en `ApplicationDbContext.OnModelCreating` + migración. (2) Reordenar `ProcessApprovedPaymentAsync`: buscar transacción existente por `MercadoPagoId` antes de cualquier mutación. (3) Si ya existe, retornar sin modificar nada. (4) Envolver el insert en try-catch de `DbUpdateException` por unique constraint violation → retornar 200 OK.

### 🟡 CRITICAL — Sospechosos (un solo juez)

- [ ] **JD-S1 — JWT placeholder podría llegar a producción** (Juez A)
  - `appsettings.json:14`: `Jwt:SecretKey` = `YOUR_JWT_SECRET_KEY_MINIMUM_32_CHARACTERS_LONG_FOR_SECURITY` pasa cualquier validación de largo (>32 chars) pero es un placeholder público. Si se deploya sin cambiar, cualquiera con acceso al repo forgea tokens.
  - **Decisión**: en producción la key vendrá de variables de entorno. El placeholder debe eliminarse de `appsettings.json` y reemplazarse por validación de startup que rechace valores que empiecen con `YOUR_` o que no vengan de `ASPNETCORE_` / environment.
  - Archivos: `backend/appsettings.json:14`, `backend/Program.cs:86-87`
  - Fix: (1) Quitar `SecretKey` de `appsettings.json`. (2) En `Program.cs`, validar que la key no sea placeholder (`starts with YOUR_`) y que cumpla `Length >= 32`. (3) Documentar en `appsettings.json.template` que la key se inyecta por variable de entorno.

- [ ] **JD-S2 — Sin rate limiting en creación anónima de reservas** (Juez A)
  - `POST /api/reservations` con `[AllowAnonymous]` permite crear reservas sin límite. Atacante automatizado puede saturar el inventario cíclicamente (reservas de 10 min → expiran → nuevas reservas), bloqueando a compradores reales.
  - **Decisión**: rate limiter nativo de ASP.NET Core. 5 reservas por minuto por IP. No requiere servicios externos, no agrega fricción al usuario real.
  - Archivos: `backend/Program.cs`, `backend/Controllers/ReservationController.cs:28-30`
  - Fix: (1) `builder.Services.AddRateLimiter` con `FixedWindowLimiter` (5 reservas/minuto/cliente) en `Program.cs`. (2) `[EnableRateLimiting("Reservations")]` en el endpoint. (3) `app.UseRateLimiter()` en el pipeline.

- [ ] **JD-S3 — Firma de webhook de MP validada incorrectamente** (Juez A)
  - `ValidateWebhookSignature` re-serializa el payload con `JsonSerializer.Serialize` en vez de usar los bytes crudos de `Request.Body`. La re-serialización produce output binario distinto al original → webhooks reales de MP fallarán verificación. Si un atacante descubre que la validación es frágil, puede forjar webhooks.
  - **Decisión**: fix obligatorio. Leer `Request.Body` como bytes crudos y validar contra eso. Verificar además que el formato de firma coincida con la doc de MP (HMAC-SHA256 sobre `data.id` + `x-request-id` + `secret`).
  - Archivos: `backend/Services/PaymentService.cs:280-283`, `backend/Controllers/PaymentController.cs:94-136`
  - Fix: (1) En el controller, leer `Request.Body` como `byte[]` crudo ANTES de deserializar. (2) Pasar los bytes crudos a `ValidateWebhookSignature`. (3) Revisar el formato de firma contra documentación de MP y ajustar si es necesario. (4) Agregar startup validation de `WebhookSecret` no vacío/placeholder.

- [ ] **JD-S4 — Confirmación de pago no atómica** (Juez A)
  - `ProcessApprovedPaymentAsync` hace tres operaciones secuenciales sin transacción: (1) `SaveChangesAsync` confirma reserva, (2) `CreateTicketsAsync` crea tickets, (3) `SaveChangesAsync` guarda transacción. Si el paso 2 falla, la reserva queda confirmada sin tickets ni registro → plata cobrada, sin entregable.
  - **Decisión**: envolver los tres pasos en una transacción de EF Core (`BeginTransactionAsync` / `CommitAsync` / `RollbackAsync`). Si algo falla, todo vuelve atrás y MP reenvía el webhook. El envío de email (`SendTicketEmailAsync`) se ejecuta **después del commit**, no dentro de la transacción.
  - Archivos: `backend/Services/PaymentService.cs:166-184`
  - Fix: (1) `using var transaction = await _context.Database.BeginTransactionAsync()`. (2) Mover confirmación, creación de tickets e insert de transacción dentro del bloque try. (3) `await transaction.CommitAsync()` al final. (4) `catch { await transaction.RollbackAsync(); throw; }`. (5) Llamar `SendTicketEmailAsync` después del commit (JD-C4).

- [ ] **JD-S5 — Test asume registro público que ya no debe existir** (Juez A) ⚠️ **AFECTADO POR JD-C1**
  - `AuthenticationPropertyTests.UserRegistration_CreatesValidAccount_WithProvidedData` prueba un endpoint público de registro que, según la regla de negocio corregida, no debe existir. Tras eliminar el registro público (JD-C1), estos tests deben migrarse a testear el nuevo endpoint admin `POST /api/admin/users`.
  - Archivos: `backend/Tests/AuthenticationPropertyTests.cs:76-103`
  - Fix: eliminar tests de registro público; crear tests para `POST /api/admin/users` verificando que solo Admin puede crear usuarios y asignar roles.

- [ ] **JD-S6 — JWT key sin validación de longitud mínima** (Juez B) ⚠️ **CUBIERTO POR JD-S1**
  - Validación de `secretKey.Length >= 32` y rechazo de placeholders ya incluidos en el fix de JD-S1.

- [ ] **JD-S7 — `int.Parse` sin defensa en `ExpirationMinutes`** (Juez B)
  - `int.Parse(jwtSettings["ExpirationMinutes"] ?? "1440")`: si el valor no es numérico → `FormatException` no manejada → crashea generación de tokens → 500.
  - Archivos: `backend/Services/AuthService.cs:220`
  - Fix: `int.TryParse` con fallback a 1440. Agregar validación al startup para valores no numéricos en config.

- [ ] **JD-S8 — HttpClient.BaseAddress mutado en cliente compartido** (Juez B)
  - `MercadoPagoClient` setea `_httpClient.BaseAddress` en el constructor sobre el `HttpClient` inyectado. No es thread-safe y afecta otros consumidores.
  - Archivos: `backend/Services/MercadoPagoClient.cs:27`, `backend/Program.cs:40`
  - Fix: mover `BaseAddress` al delegate de `AddHttpClient<T>` en `Program.cs` (`client.BaseAddress = new Uri(...)`). Eliminar la asignación del constructor.

- [ ] **JD-S9 — `async void` puede crashear el proceso** (Juez B)
  - `ReservationExpirationService.CheckExpiredReservations` es `async void`. Excepción no manejada → `SynchronizationContext` → crashea el runtime completo.
  - Archivos: `backend/Services/ReservationExpirationService.cs:51`
  - Fix: cambiar a `async Task`. Reemplazar `Timer` por `PeriodicTimer` para manejo correcto de cancelación vía `CancellationToken`.

- [ ] **JD-S10 — `TicketTypeId` FK no asignado al crear tickets** (Juez B)
  - `TicketService.CreateTicketsAsync` crea `Ticket` pero nunca setea `TicketTypeId`. La navegación `TicketType` queda huérfana (FK = 0). Cualquier `.Include(t => t.TicketType)` sobre tickets devuelve null.
  - Archivos: `backend/Services/TicketService.cs:80-84`, `backend/Models/Ticket.cs:7`
  - Fix: setear `TicketTypeId = reservation.TicketTypeId` al instanciar `Ticket`.

### 🟠 WARNING (real) — Confirmados (prioridad alta/media)

- [ ] **JD-W1 — `purchaserEmail` nunca se envía al backend** ⚠️ **CUBIERTO POR JD-C4**
- [ ] **JD-W2 — `Include(e => e.Tickets)` carga todos los tickets en memoria (OOM)**
  - `GetEventByIdAsync` y `GetAllPublishedEventsAsync` hacen `.Include(e => e.Tickets)` para contar disponibilidad. Evento popular con 50k tickets → OOM. Con JD-C5 (`CurrentlyReserved` + `SoldCount` en `TicketType`), la disponibilidad ya está precalculada en la entidad y no necesita conteo en absoluto.
  - Archivos: `backend/Services/EventService.cs:119-146, 425-456`
  - Fix: (1) Eliminar `.Include(e => e.Tickets)` de los queries. (2) Calcular disponibilidad desde `TicketType.Quantity - CurrentlyReserved - SoldCount` directamente. (3) `MapToEventWithAvailability` pasa a ser O(1) por ticket type en vez de O(N) por ticket.
- [ ] **JD-W3 — JWT en `localStorage` → migrar a httpOnly cookie**
  - El token JWT se almacena en `localStorage`, accesible desde cualquier script en la página. Un XSS roba el token y el atacante obtiene acceso total con los permisos de la víctima.
  - **Decisión**: migrar a cookie `httpOnly; Secure; SameSite=Lax`. La cookie no es accesible desde JavaScript y viaja automáticamente en cada request. `SameSite=Lax` protege contra CSRF en requests POST/PUT/DELETE cross-site.
  - Archivos: `backend/Controllers/AuthController.cs` (login/logout), `backend/Program.cs:88-106` (JWT config), `frontend/src/api/client.js:14-32` (interceptor), `frontend/src/context/AuthProvider.jsx:22-26` (persistencia), `frontend/src/context/auth.js`
  - Fix: (1) `AuthController.Login`: setear cookie httpOnly en respuesta (`Response.Cookies.Append("token", token, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Expires = ... })`). (2) `AuthController.Logout`: endpoint que borra la cookie (`MaxAge = 0`). (3) `Program.cs`: configurar `AddJwtBearer` para leer token de cookie (`options.Events.OnMessageReceived = ctx => { ctx.Token = ctx.Request.Cookies["token"]; return Task.CompletedTask; }`). (4) Frontend `client.js`: eliminar lógica de lectura/escritura de token en `localStorage`. (5) Agregar `GET /auth/me` para que el frontend sepa quién está autenticado sin leer la cookie. (6) `AuthProvider`: reemplazar `localStorage` por llamada a `/auth/me` en mount y después de login.
- [ ] **JD-W4 — N×5 queries en `MetricsService`**
  - `GetOrganizerMetricsAsync` itera sobre cada evento y llama `CalculateMetricsAsync`, que hace 5 queries separadas. Para 50 eventos = 250 round-trips.
  - Archivos: `backend/Services/MetricsService.cs:62-64, 75-114`
  - Fix: consolidar en una sola consulta con `GroupBy(eventId)` para obtener todas las métricas en un round-trip.
- [ ] **JD-W5 — `GetAllLogsAsync` sin paginación** → timeout futuro | `backend/Services/AdminService.cs:33-46` — Fix: agregar `page`/`pageSize`.
- [ ] **JD-W6 — `formatEventDate`, `formatCurrency`, `getErrorMessage` duplicados en 7+ archivos** | `frontend/src/pages/` — Fix: extraer a `src/lib/format.js` y `src/lib/apiError.js`.
- [ ] **JD-W7 — `RoleGuard` redirige sin feedback al usuario**
  - Si un usuario sin permisos accede a una ruta protegida, es redirigido a `/` sin toast ni explicación. No sabe qué pasó.
  - Archivos: `frontend/src/components/RoleGuard.jsx`
  - Fix: mostrar página 403 "No autorizado" o toast de error + redirect. No redirigir en silencio.
- [ ] **JD-W8 — `EventOwnershipHandler` solo lee `id` de ruta** → frágil si otra ruta usa `eventId` | `backend/Authorization/EventOwnershipHandler.cs:57` — Fix: pasar nombre de parámetro desde el requirement.
- [ ] **JD-W9 — Doble scan de QR sin `ConcurrencyToken`** ❌ **RECHAZADO** — El riesgo de que dos staff escaneen exactamente el mismo QR en el mismo milisegundo es insignificante para la escala del negocio. No se justifica la complejidad adicional.
- [ ] **JD-W10 — `GenerateQRCodeImage` síncrono bloquea request thread** | `backend/Services/TicketService.cs:132-160` — Fix: cachear PNG renderizado.
- [ ] **JD-W11 — `PUT /events/undefined` si `initialData?.id` no existe** | `frontend/src/components/EventForm.jsx:145-171` — Fix: validar `eventId` antes de submit.
- [ ] **JD-W12 — Catch block pone `feedback.type = 'success'` al fallar upload** | `frontend/src/components/EventForm.jsx:81,99-115` — Fix: usar `warning` o `error`.
- [ ] **JD-W13 — Focus trap de Modal no actualiza nodos focusables** | `frontend/src/components/Modal.jsx:46-82` — Fix: re-evaluar en cada Tab.
- [ ] **JD-W14 — `nextId` a nivel módulo persiste entre HMR** | `frontend/src/context/ToastProvider.jsx:48` — Fix: `useRef`.
- [ ] **JD-W15 — StaffScan: sin validación GUID ni guarda de scanner** | `frontend/src/pages/StaffScan.jsx:156-186` — Fix: validar GUID; `useRef` + cleanup.
- [ ] **JD-W16 — `exception.StackTrace` logueado (paths y datos sensibles)**
  - `GlobalExceptionHandler` loguea `exception.StackTrace` completo. Puede exponer paths del servidor y datos internos en logs.
  - Archivos: `backend/Middleware/GlobalExceptionHandler.cs:54`
  - Fix: loguear solo `exception.Message`. Emitir `StackTrace` como propiedad estructurada separada que pueda filtrarse en producción.
- [ ] **JD-W17 — Fallos de auditoría silenciosos** | `backend/Services/AuditLogService.cs:46-49` — Fix: cola out-of-band + métrica.
- [ ] **JD-W18 — Password ≥6 back vs ≥8 front** | `backend/Services/AuthService.cs:42-49` — Fix: unificar ≥8 en servidor.
- [ ] **JD-W19 — `AuditLog.UserId` sin FK a Users** | `backend/Data/ApplicationDbContext.cs:149-165` — Fix: FK con `OnDelete(Restrict)`.
- [ ] **JD-W20 — `TryGetUserRole` retorna `true` aunque `Enum.TryParse` falle**
  - Si el claim de rol no existe o tiene un valor inválido, `Enum.TryParse` retorna `false` pero el método lo ignora y retorna `true` con `Organizador` como fallback. Un usuario sin rol se autentica como Organizador.
  - Archivos: `backend/Controllers/EventController.cs:233-238`
  - Fix: retornar `false` si `Enum.TryParse` falla. El caller debe manejar el caso de usuario sin rol (403 Forbidden).
- [ ] **JD-W21 — `CheckoutReturn` miente: "enviadas a tu email"** ⚠️ **CUBIERTO POR JD-C4**
- [ ] **JD-W22 — `api/client.js` cae a `http://localhost:5193` en producción**
  - Lógica invertida: en dev usa `VITE_API_BASE_URL`, en prod hace fallback a localhost.
  - Archivos: `frontend/src/api/client.js:4-6`
  - Fix: invertir. En prod usar `VITE_API_BASE_URL` (obligatorio), en dev fallback a `http://localhost:5193`.
- [ ] **JD-W23 — Tests con EF Core InMemory no prueban constraints reales** | `backend/Tests/` — Fix: integration tests con PostgreSQL (Testcontainers).
- [ ] **JD-W24 — Webhook audit log usa `UserId: Guid.Empty`** | `backend/Controllers/PaymentController.cs:94-136` — Fix: `System` user o `MercadoPagoId`.
- [ ] **JD-W25 — Sin rate limiting en `POST /auth/login`**
  - Sin límite de intentos → brute-force factible contra contraseñas débiles.
  - Archivos: `backend/Services/AuthService.cs:104-167`, `backend/Controllers/AuthController.cs:79-114`
  - Fix: rate limiter tipo `SlidingWindowLimiter` (ej. 10 intentos por minuto por IP) en el endpoint. Alternativa futura: exponential lockout por email tras N fallos.
- [ ] **JD-W26 — Reservation token sin nonce/expiry → replayable** | `backend/Services/ReservationService.cs:344-356` — Fix: incluir timestamp; validar no expirado.
- [ ] **JD-W27 — QR timestamp no validado al escanear**
  - El QR contiene `ticketId:timestamp:signature`, pero `VerifyQRCodeSignature` solo valida la firma HMAC, no el timestamp. Un QR robado es válido indefinidamente.
  - Archivos: `backend/Services/TicketService.cs:111-217`, `backend/Helpers/HmacHelper.cs`
  - Fix: al validar, verificar que el timestamp del QR esté dentro de una ventana razonable (ej. desde la fecha de compra hasta 24h post-evento). Si el evento ya pasó, el QR se rechaza.
- [ ] **JD-W28 — `PaymentService` muta `reservation.Status` directo** ⚠️ **CUBIERTO POR JD-S4**
- [ ] **JD-W29 — PII (email, DNI) logueado sin redactar** | `backend/Services/TicketService.cs:330-339` — Fix: `LogRedactor.HashIdentifier`.
- [ ] **JD-W30 — `OrganizerEventDetail` carga datos con endpoint anónimo** | `frontend/src/pages/OrganizerEventDetail.jsx:37-62` — Fix: `GET /events/{id}/manage` con `EventOwnership`.
- [ ] **JD-W31 — `EventForm` `Content-Type` explícito rompe boundary** | `frontend/src/components/EventForm.jsx:174-189` — Fix: dejar axios auto-detectar.
- [ ] **JD-W32 — `ReservationService` reintento no re-lee `TicketType`** ⚠️ **CUBIERTO POR JD-C5**
- [ ] **JD-W33 — Sin trazabilidad IP/user-agent en reservas guest** | `backend/Controllers/ReservationController.cs:38-184` — Fix: persistir IP + User-Agent.

### 🔵 SUGGESTION — Mejoras

- [ ] **JD-SG1 — Agregar `Name` al modelo `User`** o quitar campo del frontend | `backend/Models/User.cs`, `frontend/src/pages/Register.jsx:45-49`
- [ ] **JD-SG2 — `DeleteEventAsync`: si falla el delete de imagen en R2, queda huérfana** | `backend/Services/EventService.cs:207-228`
- [ ] **JD-SG3 — Sin `<ErrorBoundary>` en rutas React** → crash de un componente tumba toda la app | `frontend/src/App.jsx:21-98`
- [ ] **JD-SG4 — Sin key rotation strategy para JWT** | `backend/Program.cs:47-49`
- [ ] **JD-SG5 — `OrganizerId` expuesto a clientes anónimos en `GET /events`** | `backend/Services/EventService.cs`
- [ ] **JD-SG6 — AuditLog sin IP/User-Agent del request** | `backend/Controllers/MetricsController.cs`, `backend/Models/AuditLog.cs`
- [ ] **JD-SG7 — Historial de scans de Staff solo en estado local (se pierde al refrescar)** | `frontend/src/pages/StaffScan.jsx:280-305`
- [ ] **JD-SG8 — `<Card {...rest}>` propaga props arbitrarios al DOM** | `frontend/src/components/Card.jsx:16-19`
- [ ] **JD-SG9 — `vi` referenciado sin import explícito (depende de `globals: true`)** | `frontend/src/components/__tests__/accessibility.test.jsx:2-9`
- [ ] **JD-SG10 — Validación de email duplicada en `Login.jsx` y `Register.jsx`** | `frontend/src/pages/Login.jsx:13`, `frontend/src/pages/Register.jsx:18`
- [ ] **JD-SG11 — `div` con `role="button"` + keyboard manual → usar `<button>` nativo** | `frontend/src/pages/EventList.jsx:26,29`
- [ ] **JD-SG12 — 404 sin link de navegación (usuario queda varado)** | `frontend/src/pages/NotFound.jsx:1-7`
- [ ] **JD-SG13 — `String.ToLower()` en LINQ puede impedir uso de índice** | `backend/Services/AuthService.cs:52-53`
- [ ] **JD-SG14 — Índice unique en `QRCodeData` (incluye timestamp → siempre único, overhead innecesario)** | `backend/Data/ApplicationDbContext.cs:112`
- [ ] **JD-SG15 — Validación de config duplicada en `Program.cs`** (extraer helper `GetRequiredValue`) | `backend/Program.cs:50-56, 86-87, 137-139`
- [ ] **JD-SG16 — `TryGetUserId` duplicado entre base controller y `ReservationController`** | `backend/Controllers/TicketeraControllerBase.cs:16-21`
- [ ] **JD-SG17 — Complejidad O(N×M) en `MapToEventWithAvailability`** | `backend/Services/EventService.cs:425-456`
- [ ] **JD-SG18 — `EventForm` upload de imagen de evento pasado falla por validación de Date** | `backend/Controllers/EventController.cs:161-217`
- [ ] **JD-SG19 — `Register.jsx` completo debe eliminarse o migrarse a `AdminPanel`** ⚠️ **CUBIERTO POR JD-C1** — `Staff` en dropdown público y toda la página de registro público desaparecen con la corrección de JD-C1. La UI de creación de usuarios se mueve al panel de Admin. | `frontend/src/pages/Register.jsx`
- [ ] **JD-SG20 — Emails con QR inline en base64 → spam filters + límites de tamaño Resend** | `backend/Services/EmailService.cs:43-45`
- [ ] **JD-SG21 — `auth.js` no valida token en mount** → UI muestra "autenticado" con token expirado | `frontend/src/context/AuthProvider.jsx:22-26`
- [ ] **JD-SG22 — `ReservationExpirationService` con `Timer` en vez de `PeriodicTimer`** | `backend/Services/ReservationExpirationService.cs:51-78`
- [ ] **JD-SG23 — `structured logging` con regex en vez de campos explícitos para redaction** | `backend/Helpers/LogRedactor.cs:15-54, 137-141`
- [ ] **JD-SG24 — Config de `HttpClient` estática para MP/Resend (no refresca con `IOptionsMonitor`)** | `backend/Services/MercadoPagoClient.cs:16-29`, `backend/Services/ResendClient.cs:16-23`

> **Totales: 8 CRITICAL confirmados + 10 sospechosos + 33 WARNING reales + 24 SUGGESTION = 75 hallazgos**