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

- [ ] 20. Implement authentication components
  - [ ] 20.1 Create registration component
    - Build registration form with email, password, role selection
    - Implement form validation
    - Call POST /api/auth/register endpoint
    - Store JWT token on successful registration
    - Redirect to appropriate page based on role
    - Display error messages
    - _Requirements: 1.2, 1.7_

  - [ ] 20.2 Create login component
    - Build login form with email and password
    - Implement form validation
    - Call POST /api/auth/login endpoint
    - Store JWT token on successful login
    - Redirect to appropriate page based on role
    - Display error messages
    - _Requirements: 1.3, 1.4, 1.7, 1.8_

  - [ ]* 20.3 Write unit tests for authentication components
    - Test form validation
    - Test successful login/registration flows
    - Test error handling and display

- [ ] 21. Implement event catalog and browsing components
  - [ ] 21.1 Create event catalog component
    - Fetch events from GET /api/events
    - Display events in grid or list view
    - Show event cards with image, name, date, location
    - Implement click handler to navigate to event detail page
    - Add loading and error states
    - _Requirements: 2.1, 2.2, 2.3_

  - [ ] 21.2 Create event detail component
    - Fetch single event from GET /api/events/{id}
    - Display full event information
    - Display ticket types with prices and availability
    - Implement ticket quantity selector
    - Add "Reserve Tickets" button
    - _Requirements: 2.2, 2.3, 2.5, 2.6_

  - [ ]* 21.3 Write unit tests for event browsing components
    - Test event catalog rendering
    - Test event detail display
    - Test navigation between pages
    - Test empty state handling

- [ ] 22. Implement reservation and checkout flow
  - [ ] 22.1 Create reservation component
    - Call POST /api/reservations to create reservation
    - Display reservation confirmation with expiration time
    - Implement countdown timer showing remaining time
    - Handle reservation expiration (clear state, show notification)
    - _Requirements: 4.1, 4.3, 4.8, 4.9_

  - [ ] 22.2 Create checkout component
    - Display reservation summary (event, tickets, total)
    - Show countdown timer
    - Call POST /api/payments/create-preference
    - Redirect to Mercado Pago checkout URL
    - _Requirements: 4.8, 5.3, 5.4_

  - [ ] 22.3 Create payment return handler
    - Handle return from Mercado Pago (success/failure)
    - Display confirmation message or error
    - Show email delivery status
    - _Requirements: 5.4, 7.7_

  - [ ]* 22.4 Write unit tests for checkout flow
    - Test reservation creation and timer
    - Test payment redirect
    - Test return handling

- [ ] 23. Implement ticket lookup component
  - [ ] 23.1 Create ticket lookup form
    - Build form with email and DNI inputs
    - Implement form validation
    - Call GET /api/tickets/lookup with query parameters
    - Display retrieved tickets with QR codes
    - Implement download/print functionality for QR codes
    - Handle no results case with appropriate message
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6_

  - [ ]* 23.2 Write unit tests for ticket lookup
    - Test form validation
    - Test successful lookup display
    - Test no results handling
    - Test QR code display

- [ ] 24. Checkpoint - Verify frontend guest features
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 25. Implement QR scanner component for staff
  - [ ] 25.1 Create QR scanner interface
    - Implement web-based QR scanner using html5-qrcode or react-qr-reader
    - Request camera permissions
    - Display camera feed
    - Implement QR code detection
    - _Requirements: 9.1_

  - [ ] 25.2 Implement validation and feedback
    - Call POST /api/tickets/validate with scanned QR code and event ID
    - Display validation results (success or error with reason)
    - Implement visual feedback (green for success, red for error)
    - Implement audio feedback (beep sounds)
    - Show scan history log
    - Add role-based access control (Staff only)
    - _Requirements: 9.2, 9.7, 9.8, 9.9_

  - [ ]* 25.3 Write unit tests for QR scanner
    - Test camera initialization
    - Test validation result display
    - Test visual and audio feedback
    - Test role-based access

- [ ] 26. Implement organizer dashboard and event management
  - [ ] 26.1 Create event creation/edit form component
    - Build form with fields: name, date, location, description, image upload
    - Add ticket type management (add/remove ticket types with name, price, quantity)
    - Implement form validation
    - Call POST /api/events to create event
    - Call PUT /api/events/{id} to update event
    - Call POST /api/events/{id}/image to upload image
    - Display success/error messages
    - _Requirements: 10.1, 10.2, 10.4, 10.5, 3.1_

  - [ ] 26.2 Create organizer dashboard component
    - Fetch organizer's events and metrics from GET /api/metrics/organizer
    - Display list of events with metrics (tickets sold, revenue, inventory, scans)
    - Add create event button
    - Add edit/delete buttons for each event
    - Implement delete confirmation dialog
    - Call DELETE /api/events/{id} to delete event
    - Refresh metrics on page load
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 11.6, 11.9, 10.6_

  - [ ] 26.3 Create event detail metrics view
    - Fetch single event metrics from GET /api/metrics/events/{id}
    - Display detailed metrics for the event
    - Show charts or visualizations (optional enhancement)
    - _Requirements: 11.7_

  - [ ]* 26.4 Write unit tests for organizer dashboard
    - Test event list display
    - Test metrics display
    - Test event creation form
    - Test event edit/delete functionality

- [ ] 27. Implement admin panel
  - [ ] 27.1 Create admin dashboard component
    - Fetch all events from GET /api/admin/events
    - Fetch all users from GET /api/admin/users
    - Display events with owner information
    - Display user list with roles
    - Add edit/delete buttons for any event
    - Implement role-based access (Admin only)
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5_

  - [ ]* 27.2 Write unit tests for admin panel
    - Test admin access control
    - Test event list display
    - Test user list display

- [ ] 28. Implement UI/UX enhancements and styling
  - [ ] 28.1 Add global styles and theme
    - Set up CSS framework (Tailwind, Bootstrap, or Material-UI)
    - Define color scheme and typography
    - Create reusable UI components (buttons, cards, forms, modals)
    - Ensure responsive design for mobile and desktop
    - _Requirements: 2.1, 2.2_

  - [ ] 28.2 Add loading states and error handling
    - Implement loading spinners for async operations
    - Display error messages consistently across all components
    - Add toast notifications for success/error feedback
    - _Requirements: 16.4_

  - [ ]* 28.3 Write accessibility tests
    - Test keyboard navigation
    - Test screen reader compatibility
    - Test color contrast ratios

- [ ] 29. Checkpoint - Verify frontend completeness
  - Ensure all tests pass, ask the user if questions arise.

- [ ] 30. Integration testing and end-to-end verification
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

- [ ] 31. Documentation and deployment preparation
  - [ ] 31.1 Update README with setup instructions
    - Document prerequisites (Node.js, .NET 8, PostgreSQL)
    - Document environment variables for backend
    - Document environment variables for frontend
    - Document database migration steps
    - Document how to run backend and frontend locally
    - _Requirements: 13.5_

  - [ ] 31.2 Create environment configuration templates
    - Create appsettings.json.template for backend
    - Create .env.template for frontend
    - Document all required configuration values
    - _Requirements: 13.5_

  - [ ] 31.3 Add API documentation
    - Document all API endpoints with request/response examples
    - Add Swagger/OpenAPI documentation to backend
    - Document authentication requirements
    - _Requirements: 1.1, 2.4, 5.1, 9.1_

- [ ] 32. Final checkpoint and system verification
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