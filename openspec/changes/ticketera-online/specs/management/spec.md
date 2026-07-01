# spec.md — Management domain

> Origin: restructured from `.kiro/specs/ticketera-online/requirements.md`, Requirements 11 (Organizer Dashboard and Metrics) and 14 (Admin Capabilities). No new content added.

## ADDED Requirements

### Requirement: Organizer Dashboard and Metrics

The system SHALL provide an organizador dashboard with events owned by the organizador and real-time metrics (tickets sold, revenue, remaining inventory, tickets scanned).

#### Scenario: Frontend provides a Dashboard for organizadores
- **GIVEN** an authenticated Organizador user accesses the dashboard
- **WHEN** the dashboard is rendered
- **THEN** the Frontend SHALL provide a Dashboard for Organizador users

#### Scenario: Dashboard displays only the organizador's own events
- **GIVEN** an organizador is viewing the Dashboard
- **WHEN** the Dashboard loads the event list
- **THEN** the Dashboard SHALL display all events owned by the organizador only
- Validates design property 33: Dashboard Displays Owner's Events Only

#### Scenario: Dashboard displays total tickets sold per event
- **GIVEN** events with sold tickets are shown on the Dashboard
- **WHEN** the Dashboard renders metrics
- **THEN** the Dashboard SHALL display total tickets sold per event
- Validates design property 34: Tickets Sold Calculation Correctness

#### Scenario: Dashboard displays total revenue per event
- **GIVEN** events with revenue data are shown on the Dashboard
- **WHEN** the Dashboard renders metrics
- **THEN** the Dashboard SHALL display total revenue per event
- Validates design property 35: Revenue Calculation Correctness

#### Scenario: Dashboard displays remaining ticket inventory per event
- **GIVEN** events with inventory data are shown on the Dashboard
- **WHEN** the Dashboard renders metrics
- **THEN** the Dashboard SHALL display remaining ticket inventory per event
- Validates design property 36: Remaining Inventory Calculation Correctness

#### Scenario: Dashboard displays number of tickets scanned per event
- **GIVEN** events with scan data are shown on the Dashboard
- **WHEN** the Dashboard renders metrics
- **THEN** the Dashboard SHALL display number of tickets scanned (attendees checked in)
- Validates design property 37: Scanned Tickets Count Correctness

#### Scenario: Backend exposes API endpoints to retrieve event metrics
- **GIVEN** the Dashboard requests metrics for the organizador's events
- **WHEN** the Backend handles the metrics request
- **THEN** the Backend SHALL provide API endpoints to retrieve event metrics

#### Scenario: Metrics are computed in real time based on current ticket data
- **GIVEN** ticket data changes over time (sales, reservations, scans)
- **WHEN** the Backend calculates metrics
- **THEN** the Backend SHALL calculate metrics in real-time based on current ticket data

#### Scenario: Dashboard refreshes metrics on navigation
- **GIVEN** an organizador navigates to the Dashboard
- **WHEN** the page loads
- **THEN** the Dashboard SHALL refresh metrics when the organizador navigates to the page

### Requirement: Admin Capabilities

The system SHALL grant Admin users full access to all events regardless of ownership, system management capabilities, view access to all user accounts, and audit logging of admin actions.

#### Scenario: Admin users have access to all events regardless of ownership
- **GIVEN** an Admin user requests access to events
- **WHEN** the Backend authorizes the request
- **THEN** the Backend SHALL grant Admin users access to all events regardless of ownership
- Validates design property 42: Admin Access to All Events

#### Scenario: Admin users can modify any event
- **GIVEN** an Admin user requests to modify an event owned by another organizador
- **WHEN** the Backend authorizes the modification
- **THEN** the Backend SHALL allow Admin users to modify any event
- Validates design property 42: Admin Access to All Events

#### Scenario: Admin users can delete any event
- **GIVEN** an Admin user requests to delete an event owned by another organizador
- **WHEN** the Backend authorizes the deletion
- **THEN** the Backend SHALL allow Admin users to delete any event
- Validates design property 42: Admin Access to All Events

#### Scenario: Admin users can view all user accounts
- **GIVEN** an Admin user requests the list of users
- **WHEN** the Backend authorizes the request
- **THEN** the Backend SHALL allow Admin users to view all user accounts

#### Scenario: Frontend provides admin-specific interfaces for system management
- **GIVEN** an Admin user accesses the Frontend
- **WHEN** the Admin interacts with the system
- **THEN** the Frontend SHALL provide admin-specific interfaces for system management

#### Scenario: All admin actions are logged for audit
- **GIVEN** an Admin user performs an admin action (view, modify, delete)
- **WHEN** the action is executed by the Backend
- **THEN** the Backend SHALL log all admin actions for audit purposes with timestamp, admin user ID, and action details
- Validates design property 43: Admin Action Audit Logging