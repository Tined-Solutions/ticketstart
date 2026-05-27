# Requirements Document

## Introduction

Ticketera Online is a complete online ticketing MVP system that enables event organizers to create and manage events, sell tickets through Mercado Pago integration, and validate attendees via QR code scanning. The system provides automatic reservation management, secure ticket generation, email delivery, and comprehensive organizer dashboards with metrics.

## Glossary

- **System**: The complete Ticketera Online platform (frontend + backend)
- **Frontend**: React-based web application for user interactions
- **Backend**: ASP.NET Core API server handling business logic and data persistence
- **User**: Any person interacting with the system (Guest, Organizador, Staff, Admin)
- **Guest**: Unauthenticated user browsing events
- **Organizador**: Authenticated user with permission to create and manage events
- **Staff**: Authenticated user with permission to scan tickets at events
- **Admin**: Authenticated user with full system permissions
- **Event**: A ticketed occasion with date, location, and ticket inventory
- **Ticket**: A purchased admission credential with unique QR code
- **Reservation**: Temporary hold on ticket inventory with 10-minute expiration
- **QR_Code**: HMAC-SHA256 signed identifier for ticket validation
- **JWT**: JSON Web Token for authentication
- **R2_Storage**: Cloudflare R2 object storage for event images
- **Payment_Gateway**: Mercado Pago payment processing service
- **Email_Service**: Resend email delivery service
- **Expiration_Service**: IHostedService background worker for reservation cleanup
- **Dashboard**: Organizer interface displaying event metrics and management tools

## Requirements

### Requirement 1: User Authentication and Authorization

**User Story:** As a user, I want to register and log in with role-based access, so that I can access features appropriate to my role.

#### Acceptance Criteria

1. THE Backend SHALL provide JWT-based authentication for all protected endpoints
2. WHEN a user registers, THE Backend SHALL create an account with email, password, and assigned role (Organizador, Staff, or Admin)
3. WHEN a user logs in with valid credentials, THE Backend SHALL return a JWT token valid for the session
4. WHEN a user logs in with invalid credentials, THE Backend SHALL return an authentication error
5. THE Backend SHALL validate JWT tokens on all protected endpoints
6. THE Backend SHALL enforce role-based authorization for role-specific operations
7. THE Frontend SHALL store JWT tokens securely and include them in authenticated requests
8. THE Frontend SHALL redirect unauthenticated users to the login page when accessing protected routes

### Requirement 2: Event Catalog and Browsing

**User Story:** As a guest, I want to browse available events, so that I can discover events to attend.

#### Acceptance Criteria

1. THE Frontend SHALL display a catalog of all published events
2. THE Frontend SHALL display event details including name, date, location, description, and image
3. WHEN a guest clicks on an event, THE Frontend SHALL navigate to the event detail page
4. THE Backend SHALL provide an API endpoint to retrieve all published events
5. THE Backend SHALL provide an API endpoint to retrieve a single event by identifier
6. THE Backend SHALL return event data including ticket availability counts

### Requirement 3: Event Image Storage

**User Story:** As an organizador, I want to upload event images, so that my events are visually appealing to potential attendees.

#### Acceptance Criteria

1. WHEN an organizador uploads an event image, THE Backend SHALL store the image in R2_Storage
2. THE Backend SHALL generate a unique identifier for each uploaded image
3. THE Backend SHALL return the R2_Storage URL for the uploaded image
4. THE Backend SHALL validate image file types and size limits before upload
5. THE Frontend SHALL display event images from R2_Storage URLs
6. WHEN an event is deleted, THE Backend SHALL remove associated images from R2_Storage

### Requirement 4: Ticket Selection and Temporary Reservations

**User Story:** As a user, I want to reserve tickets temporarily while I complete checkout, so that my selected tickets are held for me.

#### Acceptance Criteria

1. WHEN a user selects tickets, THE Backend SHALL create a reservation with 10-minute expiration
2. THE Backend SHALL decrement available ticket inventory by the reserved quantity
3. THE Backend SHALL return a reservation identifier to the Frontend
4. WHILE a reservation is active, THE Backend SHALL prevent other users from purchasing the reserved tickets
5. WHEN a reservation expires, THE Expiration_Service SHALL release the reserved tickets back to inventory
6. THE Expiration_Service SHALL run continuously as an IHostedService background worker
7. THE Expiration_Service SHALL check for expired reservations at regular intervals
8. THE Frontend SHALL display a countdown timer showing remaining reservation time
9. WHEN a reservation expires, THE Frontend SHALL notify the user and clear the reservation

### Requirement 5: Payment Processing

**User Story:** As a user, I want to pay for tickets via Mercado Pago, so that I can complete my purchase securely.

#### Acceptance Criteria

1. WHEN a user initiates checkout, THE Backend SHALL create a Mercado Pago payment preference
2. THE Backend SHALL include reservation details, ticket quantities, and total amount in the payment preference
3. THE Backend SHALL return the Mercado Pago checkout URL to the Frontend
4. THE Frontend SHALL redirect the user to the Mercado Pago checkout URL
5. WHEN Mercado Pago processes a payment, THE Payment_Gateway SHALL send a webhook notification to the Backend
6. WHEN the Backend receives a successful payment webhook, THE Backend SHALL convert the reservation to confirmed tickets
7. WHEN the Backend receives a failed payment webhook, THE Backend SHALL release the reservation
8. THE Backend SHALL validate webhook signatures to ensure authenticity

### Requirement 6: QR Code Ticket Generation

**User Story:** As a user, I want to receive tickets with secure QR codes, so that I can prove my purchase at the event.

#### Acceptance Criteria

1. WHEN a payment is confirmed, THE Backend SHALL generate a unique QR_Code for each ticket
2. THE Backend SHALL sign each QR_Code using HMAC-SHA256 with a secret key
3. THE QR_Code SHALL encode the ticket identifier and signature
4. THE Backend SHALL store the ticket record with QR_Code data in the database
5. THE Backend SHALL generate a visual QR code image for each ticket
6. WHEN validating a QR_Code, THE Backend SHALL verify the HMAC-SHA256 signature
7. IF a QR_Code signature is invalid, THEN THE Backend SHALL reject the ticket as fraudulent

### Requirement 7: Email Ticket Delivery

**User Story:** As a user, I want to receive my tickets via email, so that I have immediate access after purchase.

#### Acceptance Criteria

1. WHEN tickets are confirmed, THE Backend SHALL send an email to the purchaser via Email_Service
2. THE Backend SHALL include all ticket QR codes in the email
3. THE Backend SHALL include event details (name, date, location) in the email
4. THE Backend SHALL include purchase confirmation details in the email
5. THE Email_Service SHALL use Resend for email delivery
6. IF email delivery fails, THEN THE Backend SHALL log the error and retry delivery
7. THE Frontend SHALL display a confirmation message indicating email delivery status

### Requirement 8: Ticket Lookup

**User Story:** As a user, I want to retrieve my tickets using email and DNI, so that I can access them if I lose the original email.

#### Acceptance Criteria

1. THE Frontend SHALL provide a ticket lookup form accepting email and DNI
2. WHEN a user submits the lookup form, THE Backend SHALL query tickets matching both email and DNI
3. THE Backend SHALL return all matching tickets with QR codes
4. THE Frontend SHALL display the retrieved tickets with downloadable QR codes
5. IF no tickets match the lookup criteria, THEN THE Backend SHALL return an empty result
6. THE Frontend SHALL display a message when no tickets are found

### Requirement 9: QR Code Scanning and Validation

**User Story:** As staff, I want to scan ticket QR codes at the event entrance, so that I can validate attendees.

#### Acceptance Criteria

1. THE Frontend SHALL provide a web-based QR scanner interface for Staff users
2. WHEN staff scans a QR_Code, THE Frontend SHALL send the code to the Backend for validation
3. THE Backend SHALL verify the QR_Code HMAC-SHA256 signature
4. THE Backend SHALL check if the ticket has already been used
5. THE Backend SHALL check if the ticket belongs to the scanned event
6. IF the ticket is valid and unused, THEN THE Backend SHALL mark the ticket as used and return success
7. IF the ticket is invalid, already used, or for a different event, THEN THE Backend SHALL return an error
8. THE Frontend SHALL display validation results (success or error reason) to staff
9. THE Frontend SHALL provide visual and audio feedback for scan results

### Requirement 10: Organizer Event Management

**User Story:** As an organizador, I want to create and manage events, so that I can sell tickets to my events.

#### Acceptance Criteria

1. THE Frontend SHALL provide an event creation form for Organizador users
2. WHEN an organizador submits the event form, THE Backend SHALL create the event record
3. THE Backend SHALL associate the event with the creating organizador
4. THE Backend SHALL validate required event fields (name, date, location, ticket types, quantities, prices)
5. THE Frontend SHALL allow organizadores to edit their own events
6. THE Frontend SHALL allow organizadores to delete their own events
7. THE Backend SHALL prevent organizadores from modifying events they do not own
8. WHEN an organizador creates ticket types, THE Backend SHALL store ticket type details (name, price, quantity)

### Requirement 11: Organizer Dashboard and Metrics

**User Story:** As an organizador, I want to view metrics for my events, so that I can track sales and attendance.

#### Acceptance Criteria

1. THE Frontend SHALL provide a Dashboard for Organizador users
2. THE Dashboard SHALL display all events owned by the organizador
3. THE Dashboard SHALL display total tickets sold per event
4. THE Dashboard SHALL display total revenue per event
5. THE Dashboard SHALL display remaining ticket inventory per event
6. THE Dashboard SHALL display number of tickets scanned (attendees checked in)
7. THE Backend SHALL provide API endpoints to retrieve event metrics
8. THE Backend SHALL calculate metrics in real-time based on current ticket data
9. THE Dashboard SHALL refresh metrics when the organizador navigates to the page

### Requirement 12: Automatic Refunds on Stock Failure

**User Story:** As a user, I want automatic refunds if ticket inventory fails after payment, so that I am not charged for tickets I cannot receive.

#### Acceptance Criteria

1. WHEN a payment is confirmed, THE Backend SHALL verify ticket inventory availability
2. IF ticket inventory is insufficient, THEN THE Backend SHALL initiate a refund via Payment_Gateway
3. THE Backend SHALL log the stock failure and refund transaction
4. THE Backend SHALL send an email notification to the user explaining the refund
5. THE Backend SHALL release any associated reservations
6. THE Backend SHALL handle race conditions where multiple users purchase the last tickets simultaneously

### Requirement 13: Monorepo Structure

**User Story:** As a developer, I want a clear monorepo structure, so that frontend and backend code are organized and maintainable.

#### Acceptance Criteria

1. THE System SHALL organize code in a monorepo with /frontend and /backend folders
2. THE Frontend SHALL be a standalone React application in /frontend
3. THE Backend SHALL be a standalone ASP.NET Core application in /backend
4. THE System SHALL provide configuration for running both applications independently
5. THE System SHALL provide documentation for local development setup

### Requirement 14: Admin Capabilities

**User Story:** As an admin, I want full system access, so that I can manage all events, users, and system operations.

#### Acceptance Criteria

1. THE Backend SHALL grant Admin users access to all events regardless of ownership
2. THE Backend SHALL allow Admin users to modify any event
3. THE Backend SHALL allow Admin users to delete any event
4. THE Backend SHALL allow Admin users to view all user accounts
5. THE Frontend SHALL provide admin-specific interfaces for system management
6. THE Backend SHALL log all admin actions for audit purposes

### Requirement 15: Data Persistence

**User Story:** As the system, I want to persist all data reliably, so that information is not lost.

#### Acceptance Criteria

1. THE Backend SHALL use a relational database for data persistence
2. THE Backend SHALL store user accounts, events, tickets, reservations, and transactions
3. THE Backend SHALL use database transactions for operations requiring atomicity
4. THE Backend SHALL implement proper indexing for query performance
5. THE Backend SHALL handle database connection failures gracefully
6. THE Backend SHALL log database errors for troubleshooting

### Requirement 16: Error Handling and Logging

**User Story:** As a developer, I want comprehensive error handling and logging, so that I can diagnose and fix issues.

#### Acceptance Criteria

1. THE Backend SHALL log all errors with timestamps, context, and stack traces
2. THE Backend SHALL return appropriate HTTP status codes for all error conditions
3. THE Backend SHALL return user-friendly error messages to the Frontend
4. THE Frontend SHALL display error messages to users in a clear format
5. THE Backend SHALL log all payment webhook events for audit and debugging
6. THE Backend SHALL log all QR code validation attempts and results
7. THE System SHALL not expose sensitive information in error messages or logs
