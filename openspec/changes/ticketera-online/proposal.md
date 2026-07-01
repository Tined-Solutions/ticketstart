# Proposal: Ticketera Online MVP

## Summary

Ticketera Online is a complete online ticketing MVP system that enables event organizers to create and manage events, sell tickets through Mercado Pago integration, and validate attendees via QR code scanning. The system provides automatic reservation management, secure ticket generation, email delivery, and comprehensive organizer dashboards with metrics.

## Goals

- Enable event organizers to create and manage events with images stored in Cloudflare R2.
- Allow guests and authenticated users to browse events, reserve tickets temporarily, and pay via Mercado Pago.
- Generate cryptographically signed (HMAC-SHA256) QR codes per ticket and validate them at event entrances.
- Deliver tickets by email via Resend, support ticket lookup by email + DNI, and automatic refunds on stock failure.
- Provide organizer dashboard with real-time sales, revenue, inventory, and scan metrics.
- Provide admin capabilities with system-wide access and audit logging.
- Maintain a clear monorepo structure (`/frontend` React SPA + `/backend` ASP.NET Core 8.0 Web API) with reliable data persistence, comprehensive error handling, and logging.

## Non-Goals

This MVP does not include: native mobile clients, multi-language localization beyond the existing UI strings, refund flows for reasons other than stock failure, secondary-market ticket resale, or performance/caching optimizations beyond the documented targets.

## Stakeholders

- **Guest**: Unauthenticated user browsing events, purchasing tickets, looking up tickets.
- **Organizador**: Authenticated user with permission to create and manage own events and view own metrics.
- **Staff**: Authenticated user with permission to scan and validate tickets at events.
- **Admin**: Authenticated user with full system permissions across all resources.

## Glossary

- **System**: The complete Ticketera Online platform (frontend + backend).
- **Frontend**: React-based web application for user interactions.
- **Backend**: ASP.NET Core API server handling business logic and data persistence.
- **User**: Any person interacting with the system (Guest, Organizador, Staff, Admin).
- **Guest**: Unauthenticated user browsing events.
- **Organizador**: Authenticated user with permission to create and manage events.
- **Staff**: Authenticated user with permission to scan tickets at events.
- **Admin**: Authenticated user with full system permissions.
- **Event**: A ticketed occasion with date, location, and ticket inventory.
- **Ticket**: A purchased admission credential with unique QR code.
- **Reservation**: Temporary hold on ticket inventory with 10-minute expiration.
- **QR_Code**: HMAC-SHA256 signed identifier for ticket validation.
- **JWT**: JSON Web Token for authentication.
- **R2_Storage**: Cloudflare R2 object storage for event images.
- **Payment_Gateway**: Mercado Pago payment processing service.
- **Email_Service**: Resend email delivery service.
- **Expiration_Service**: IHostedService background worker for reservation cleanup.
- **Dashboard**: Organizer interface displaying event metrics and management tools.

## Approach

The change is delivered as a single full-stack MVP covering the sixteen functional and non-functional requirements defined in the source Kiro spec:

1. **Auth domain** (Requirement 1): JWT-based registration/login with role-based authorization (Organizador, Staff, Admin).
2. **Events domain** (Requirements 2, 3, 10): Event catalog browsing, single-event retrieval with availability, R2 image upload/delete with validation, and organizer event CRUD with ownership enforcement.
3. **Reservations domain** (Requirement 4): Temporary reservations with 10-minute expiration, inventory decrement, double-booking prevention, and an `IHostedService` background worker releasing expired reservations every 30 seconds.
4. **Payments domain** (Requirements 5, 12): Mercado Pago Checkout Pro preference creation, webhook processing with signature validation, automatic refunds on stock failure with email notification, and concurrent-purchase overselling prevention.
5. **Tickets domain** (Requirements 6, 7, 8, 9): HMAC-SHA256 signed QR code generation in `{ticketId}:{timestamp}:{signature}` format, Resend email delivery with retry, ticket lookup by email + DNI, and staff QR scanning validation with double-scan prevention.
6. **Management domain** (Requirements 11, 14): Organizer dashboard with real-time metrics (sold, revenue, remaining inventory, scanned) and admin panel with system-wide access and audit logging.
7. **Platform domain** (Requirements 13, 15, 16): Monorepo structure, relational persistence (Supabase PostgreSQL with pooling port 6543 / migrations port 5432), global exception handling, structured logging, and HTTP status code correctness.

##milestones / Dependency Order

Per the source task plan, the implementation follows waves: monorepo scaffolding → backend infrastructure → data models and migrations → auth → events and images → reservations and expiration service → QR codes and ticket lookup → payments and webhooks → email → metrics → admin and audit → error handling → frontend guest flows → staff scanner → organizer dashboard → admin panel → integration and documentation.

Notes from the source plan are preserved:
- Tasks marked `*` are optional for faster MVP delivery.
- All 51 correctness properties from the design document map to property-based tests (PBT) with a minimum of 100 iterations each.
- Each property test references its design property using the tag format `Feature: ticketera-online, Property {number}: {property_text}`.

## Risks / Constraints

- External service dependencies (Supabase PostgreSQL, Cloudflare R2, Mercado Pago, Resend) require valid credentials and network access; integration tests use minimal 1–2 sample calls to limit cost.
- Concurrency control for last-available tickets relies on EF Core optimistic concurrency (`RowVersion`) or pessimistic row locking; the design documents both options.
- QR code and webhook security depend on secret keys stored in environment variables — never in code or logs.
- Webhook signature validation is mandatory for payment authenticity; invalid signatures are rejected with `401 Unauthorized` and logged.
- Performance targets: API p95 < 200ms, QR validation < 100ms, metrics calculation < 500ms, expiration check for 1000 expired reservations < 5 seconds.

## Source Origin

This proposal restructures, without adding new content, the existing Kiro specification located at `.kiro/specs/ticketera-online/requirements.md` (Introduction, Glossary, 16 Requirements with acceptance criteria) and `.kiro/specs/ticketera-online/design.md`, `.kiro/specs/ticketera-online/tasks.md`.