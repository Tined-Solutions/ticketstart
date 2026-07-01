# spec.md — Reservations domain

> Origin: restructured from `.kiro/specs/ticketera-online/requirements.md`, Requirement 4: Ticket Selection and Temporary Reservations. No new content added.

## ADDED Requirements

### Requirement: Ticket Selection and Temporary Reservations

The system SHALL create temporary reservations with 10-minute expiration, decrement ticket inventory, prevent double-booking, and continuously release expired reservations through a background worker.

#### Scenario: Reservation is created with a 10-minute expiration
- **GIVEN** a user selects tickets for an event
- **WHEN** the selection is submitted to the Backend
- **THEN** the Backend SHALL create a reservation with a 10-minute expiration
- Validates design property 10: Reservation Creation Sets Correct Expiration

#### Scenario: Reservation decrements ticket inventory
- **GIVEN** a user creates a reservation with quantity N
- **WHEN** the reservation is created
- **THEN** the Backend SHALL decrement the available ticket inventory by the reserved quantity N
- Validates design property 11: Reservation Decrements Inventory

#### Scenario: Reservation identifier is returned to the Frontend
- **GIVEN** a reservation has been created
- **WHEN** the Backend responds to the reservation request
- **THEN** the Backend SHALL return a reservation identifier to the Frontend

#### Scenario: Active reservations prevent other users from purchasing reserved tickets
- **GIVEN** a reservation is currently active
- **WHEN** another user attempts to purchase the same tickets
- **THEN** the Backend SHALL prevent other users from purchasing the reserved tickets until the reservation expires or is confirmed
- Validates design property 12: Active Reservations Prevent Double-Booking

#### Scenario: Expired reservation releases tickets back to inventory
- **GIVEN** a reservation has expired
- **WHEN** the Expiration_Service processes the reservation
- **THEN** the Expiration_Service SHALL release the reserved tickets back to inventory
- Validates design property 13: Expired Reservations Restore Inventory

#### Scenario: Expiration Service runs continuously as IHostedService
- **GIVEN** the Backend is running
- **WHEN** the application starts
- **THEN** the Expiration_Service SHALL run continuously as an IHostedService background worker

#### Scenario: Expiration Service checks for expired reservations at regular intervals
- **GIVEN** the Expiration_Service is running
- **WHEN** the configured interval elapses
- **THEN** the Expiration_Service SHALL check for expired reservations at regular intervals (every 30 seconds)

#### Scenario: Frontend shows a countdown timer for the active reservation
- **GIVEN** a user has an active reservation
- **WHEN** the Frontend displays the reservation
- **THEN** the Frontend SHALL display a countdown timer showing remaining reservation time

#### Scenario: Frontend notifies the user when the reservation expires
- **GIVEN** a user's reservation expires while the user is on the Frontend
- **WHEN** the reservation expires
- **THEN** the Frontend SHALL notify the user and clear the reservation