# Event Date Change Notification Specification

## Purpose

When an admin or organizer modifies an event's `Date` field via `PUT api/events/{id}`, the system MUST automatically send an email notification to every non-refunded ticket buyer announcing the new date with a refund-request contact section. The update itself MUST succeed independently of email delivery. Zero buyers is a silent no-op, and every date change re-notifies.

## Non-Goals

Location/time notifications (extensibility hook only, not built now), deduplication tracking, admin confirmation dialog, frontend changes, batch/scheduled delivery, and per-reseller date contracts.

## Requirements

### Requirement: EDC-001: Date change detection

`EventService.UpdateEventAsync` MUST compare the existing event's `Date` with the incoming request's `Date` before overwriting. It MUST capture `oldDate` for the notification when dates differ. Non-date edits SHALL trigger no notification.

#### Scenario: Date changes trigger detection

- GIVEN an event with Date = 2026-10-15
- WHEN an admin PUTs the event with Date = 2026-11-01
- THEN the system captures oldDate=2026-10-15 and proceeds to notify buyers

#### Scenario: Non-date edits are silent

- GIVEN an event with Date = 2026-10-15
- WHEN an admin PUTs the event with the same Date but a changed Name
- THEN no notification is sent

### Requirement: EDC-002: Buyer query after commit

After `SaveChangesAsync` commits the updated event, the system MUST query all distinct `PurchaserEmail` values from non-refunded tickets (`IsRefunded == false`) linked to that event. Refunded tickets SHALL be excluded.

#### Scenario: Distinct buyers queried

- GIVEN three non-refunded tickets for the event: two with email A, one with email B
- WHEN the date change commits
- THEN the system queries two distinct emails: A and B

#### Scenario: Refunded buyers excluded

- GIVEN one non-refunded ticket (email A) and one refunded ticket (also email A)
- WHEN the date change commits
- THEN the refunded ticket is excluded; email A receives one notification

### Requirement: EDC-003: Email notification content

Each notification MUST be rendered via `EventDateChangeTemplate` and sent through `IEmailService`. The email MUST include the event name, old date, new date, and a refund-request section with the contact address. The sender name MUST use `Resend:FromName` ("Ticketera") and the refund contact MUST use `Resend:FromEmail`.

#### Scenario: Email contains required fields

- GIVEN event "Rock Fest" changes from 2026-10-15 to 2026-11-01
- WHEN the notification is rendered
- THEN the body includes the event name, old date, new date, and refund contact email

#### Scenario: Sender identity matches ticket purchase emails

- GIVEN a date-change notification is sent
- WHEN the email is delivered
- THEN the From name is "Ticketera" and the refund contact equals `Resend:FromEmail`

### Requirement: EDC-004: Email failure isolation

Email sending failures MUST NOT prevent the event update from succeeding. Each send SHALL be wrapped in try/catch, logged, and on failure a `PendingEmailSend` retry record MUST be inserted for the failed recipient. The HTTP response SHALL return success regardless of email outcomes.

#### Scenario: Event update succeeds despite email failure

- GIVEN a date change triggers notifications and one buyer's email send throws
- WHEN the update completes
- THEN the event Date is persisted, a `PendingEmailSend` is queued for the failed recipient, and PUT returns 200

#### Scenario: All emails succeed

- GIVEN all buyer emails send successfully
- WHEN the update completes
- THEN no `PendingEmailSend` records are inserted and PUT returns 200

### Requirement: EDC-005: Zero buyers no-op

When the buyer query returns zero distinct emails, the system MUST silently skip all notification logic — no email send, no log warning, and no error.

#### Scenario: Event with no ticket sales

- GIVEN an event with zero ticket purchases
- WHEN the Date is changed
- THEN the update succeeds with no email activity whatsoever

### Requirement: EDC-006: Repeat notifications

Every Date change MUST trigger a new notification round. The system SHALL NOT track prior notifications, implement deduplication, or suppress repeat sends.

#### Scenario: Back-and-forth date changes re-notify each time

- GIVEN an event Date changes from A → B, then B → A
- WHEN each change occurs
- THEN buyers receive one email per change (two total)

### Requirement: EDC-007: Extensibility for future event changes

The date-change detection logic SHALL be structured in a single condition block so that future location or time change notifications can be added as additional conditions without a refactor.

#### Scenario: Single extensible condition block

- GIVEN the `UpdateEventAsync` implementation
- WHEN a developer reads the method
- THEN the notification logic lives in one `if (dateChanged)` block, not scattered across the method
