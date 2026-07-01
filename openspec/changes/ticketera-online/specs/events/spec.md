# spec.md — Events domain

> Origin: restructured from `.kiro/specs/ticketera-online/requirements.md`, Requirements 2 (Event Catalog and Browsing), 3 (Event Image Storage), and 10 (Organizer Event Management). No new content added.

## ADDED Requirements

### Requirement: Event Catalog and Browsing

The system SHALL allow guests to browse published events and view event details including ticket availability.

#### Scenario: Frontend displays the published event catalog
- **GIVEN** published events exist in the system
- **WHEN** a guest opens the event catalog
- **THEN** the Frontend SHALL display a catalog of all published events

#### Scenario: Event details include all required fields
- **GIVEN** an event is displayed in the Frontend
- **WHEN** the event details are rendered
- **THEN** the Frontend SHALL display event details including name, date, location, description, and image
- Validates design property 5: Event Rendering Includes All Required Fields

#### Scenario: Guest navigates to event detail page
- **GIVEN** a guest is viewing the event catalog
- **WHEN** the guest clicks on an event
- **THEN** the Frontend SHALL navigate to the event detail page

#### Scenario: Backend exposes all published events endpoint
- **GIVEN** the Frontend requests the event list
- **WHEN** the Backend handles the request
- **THEN** the Backend SHALL provide an API endpoint to retrieve all published events

#### Scenario: Backend exposes single event endpoint
- **GIVEN** the Frontend requests a specific event by identifier
- **WHEN** the Backend handles the request
- **THEN** the Backend SHALL provide an API endpoint to retrieve a single event by identifier

#### Scenario: Backend returns event data with ticket availability
- **GIVEN** an event has ticket types with quantities and sold tickets
- **WHEN** the Backend returns the event data
- **THEN** the Backend SHALL return event data including ticket availability counts (quantity minus confirmed tickets sold)
- Validates design property 6: Ticket Availability Calculation Correctness

### Requirement: Event Image Storage

The system SHALL allow organizadores to upload, retrieve, and clean up event images stored in Cloudflare R2.

#### Scenario: Uploaded image is stored in R2
- **GIVEN** an organizador uploads an event image
- **WHEN** the Backend processes the upload
- **THEN** the Backend SHALL store the image in R2_Storage

#### Scenario: Uploaded image receives a unique identifier
- **GIVEN** images are uploaded to R2_Storage
- **WHEN** the Backend stores each image
- **THEN** the Backend SHALL generate a unique identifier for each uploaded image
- Validates design property 7: Image ID Uniqueness

#### Scenario: Backend returns the R2 URL for the uploaded image
- **GIVEN** an image was successfully uploaded
- **WHEN** the Backend responds to the upload request
- **THEN** the Backend SHALL return the R2_Storage URL for the uploaded image

#### Scenario: Invalid image files are rejected before upload
- **GIVEN** an organizador uploads a file that does not meet type or size requirements
- **WHEN** the Backend validates the file before upload
- **THEN** the Backend SHALL reject the file with a validation error
- Validates design property 8: Invalid Image File Rejection

#### Scenario: Frontend displays event images from R2 URLs
- **GIVEN** an event has an associated R2_Storage image URL
- **WHEN** the Frontend renders the event
- **THEN** the Frontend SHALL display event images from R2_Storage URLs

#### Scenario: Event deletion removes associated images from R2
- **GIVEN** an event with an associated image is deleted
- **WHEN** the Backend deletes the event
- **THEN** the Backend SHALL remove associated images from R2_Storage
- Validates design property 9: Event Deletion Removes Associated Images

### Requirement: Organizer Event Management

The system SHALL allow organizadores to create and manage their own events with ticket types, enforcing ownership-based authorization.

#### Scenario: Frontend provides an event creation form for organizadores
- **GIVEN** an authenticated Organizador user accesses the Frontend
- **WHEN** the user wants to create an event
- **THEN** the Frontend SHALL provide an event creation form for Organizador users

#### Scenario: Organizador submitting the form creates an event
- **GIVEN** an organizador has filled the event form
- **WHEN** the organizador submits the event form
- **THEN** the Backend SHALL create the event record

#### Scenario: Created event is associated with the creating organizador
- **GIVEN** an organizador creates an event
- **WHEN** the event record is persisted
- **THEN** the Backend SHALL associate the event with the creating organizador
- Validates design property 30: Event Creation Establishes Ownership

#### Scenario: Required event fields are validated
- **GIVEN** an event creation request is submitted
- **WHEN** the request is missing required fields (name, date, location, ticket types, quantities, prices)
- **THEN** the Backend SHALL reject the request with a validation error
- Validates design property 31: Event Validation Rejects Invalid Data

#### Scenario: Organizadores can edit their own events
- **GIVEN** an organizador owns an event
- **WHEN** the organizador edits the event
- **THEN** the Frontend SHALL allow organizadores to edit their own events

#### Scenario: Organizadores can delete their own events
- **GIVEN** an organizador owns an event
- **WHEN** the organizador deletes the event
- **THEN** the Frontend SHALL allow organizadores to delete their own events

#### Scenario: Non-owners cannot modify events they do not own
- **GIVEN** a user attempts to modify an event they do not own (and is not an Admin)
- **WHEN** the Backend authorizes the modification
- **THEN** the Backend SHALL prevent organizadores from modifying events they do not own
- Validates design property 32: Non-Owner Modification Prevention

#### Scenario: Ticket type details are stored on event creation
- **GIVEN** an organizador defines ticket types during event creation
- **WHEN** the organizador creates ticket types
- **THEN** the Backend SHALL store ticket type details (name, price, quantity)