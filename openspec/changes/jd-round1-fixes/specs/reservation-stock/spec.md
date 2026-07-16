# Reservation & Stock Specification

## Purpose

Eliminate the race condition in reservation stock management by using atomic database updates, precalculated availability, and a hardened expiration background service.

## JD Findings Covered

JD-C5, JD-W2, JD-S9, JD-SG17

## Requirements

### REQ-1: Atomic Stock Reservation via ExecuteUpdateAsync

The system MUST use a conditional `ExecuteUpdateAsync` on `TicketType.CurrentlyReserved` to atomically reserve stock, eliminating the race condition.

**JD-C5** — Files: `backend/Services/ReservationService.cs`, `backend/Models/TicketType.cs`, `backend/Data/ApplicationDbContext.cs`

#### Scenario: Successful reservation decrements available stock atomically

- GIVEN a TicketType with `Quantity=100, CurrentlyReserved=0, SoldCount=0`
- WHEN `CreateReservationAsync` requests 2 tickets
- THEN `CurrentlyReserved` becomes 2 via a single conditional UPDATE
- AND the reservation is created

#### Scenario: Concurrent reservations do not oversell

- GIVEN a TicketType with 1 available ticket
- WHEN two concurrent requests each request 1 ticket
- THEN exactly one succeeds and the other returns "insufficient stock"

#### Scenario: Insufficient stock returns error

- GIVEN a TicketType with 0 available tickets
- WHEN a reservation is requested
- THEN the UPDATE affects 0 rows and the service returns a stock-exhausted error

**Tests**: xUnit integration test with concurrent tasks; FsCheck property test for invariant `CurrentlyReserved + SoldCount <= Quantity`.

---

### REQ-2: CurrentlyReserved Column Migration

The system MUST add `CurrentlyReserved` (int, default 0) to `TicketType` via EF Core migration, reset to 0 with no backfill.

**JD-C5** — Files: `backend/Models/TicketType.cs`, EF Core migration

#### Scenario: Migration adds column with default

- GIVEN the migration is applied
- WHEN `TicketType` rows are queried
- THEN all existing rows have `CurrentlyReserved = 0`

**Tests**: Integration test verifying migration and default value.

---

### REQ-3: Reservation Expiry Decrements CurrentlyReserved

The system MUST decrement `CurrentlyReserved` atomically when reservations expire.

**JD-C5** — File: `backend/Services/ReservationExpirationService.cs`

#### Scenario: Expired reservation releases stock

- GIVEN an expired reservation for 3 tickets on TicketType X
- WHEN the expiration service processes it
- THEN `CurrentlyReserved` on TicketType X is decremented by 3 via `ExecuteUpdateAsync`

**Tests**: Integration test for expiry flow verifying stock release.

---

### REQ-4: Precalculated Availability (O(1))

The system MUST calculate ticket availability from `TicketType.Quantity - CurrentlyReserved - SoldCount` without loading ticket collections.

**JD-W2, JD-SG17** — File: `backend/Services/EventService.cs`

#### Scenario: Availability returned without Include(Tickets)

- GIVEN an event with multiple ticket types
- WHEN `GetEventByIdAsync` or `GetAllPublishedEventsAsync` is called
- THEN availability is computed from `TicketType` fields only (O(1) per type)
- AND no `.Include(e => e.Tickets)` is present in the query

**Tests**: Unit test verifying no `Include(Tickets)` in queries; integration test for correct availability math.

---

### REQ-5: Expiration Service Uses async Task + PeriodicTimer

The system MUST use `async Task` (not `async void`) and `PeriodicTimer` for the reservation expiration background service.

**JD-S9** — File: `backend/Services/ReservationExpirationService.cs`

#### Scenario: Unhandled exception does not crash the process

- GIVEN the expiration service encounters an error
- WHEN the exception propagates
- THEN it is caught and logged without crashing the host process

#### Scenario: Cancellation token honored

- GIVEN the host is shutting down
- WHEN the `CancellationToken` is signaled
- THEN the `PeriodicTimer` loop exits gracefully

**Tests**: Unit test verifying method signature is `async Task`; integration test for graceful shutdown.
