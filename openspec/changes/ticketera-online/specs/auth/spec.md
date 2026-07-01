# spec.md — Auth domain

> Origin: restructured from `.kiro/specs/ticketera-online/requirements.md`, Requirement 1: User Authentication and Authorization. No new content added.

## ADDED Requirements

### Requirement: User Authentication and Authorization

The system SHALL provide JWT-based authentication with role-based authorization so that users access only the features appropriate to their role.

#### Scenario: JWT-based authentication on protected endpoints
- **GIVEN** the Backend exposes protected endpoints
- **WHEN** any protected endpoint is invoked
- **THEN** the Backend SHALL authenticate the request using JWT-based authentication

#### Scenario: User registration creates account with assigned role
- **GIVEN** a user submits registration data (email, password, role)
- **WHEN** the registration request is processed
- **THEN** the Backend SHALL create an account with the provided email, a hashed password, and the assigned role (Organizador, Staff, or Admin)
- Validates design property 1: User Registration Creates Valid Accounts

#### Scenario: Valid login returns a JWT token
- **GIVEN** a user is registered
- **WHEN** the user logs in with valid credentials
- **THEN** the Backend SHALL return a JWT token valid for the session
- Validates design property 2: Valid Login Returns Valid JWT

#### Scenario: Invalid login is rejected
- **GIVEN** a user submits login credentials
- **WHEN** the credentials are invalid (non-existent email or incorrect password)
- **THEN** the Backend SHALL return an authentication error
- Validates design property 3: Invalid Credentials Rejected

#### Scenario: JWT tokens validated on protected endpoints
- **GIVEN** a request reaches a protected endpoint
- **WHEN** the Backend processes the request
- **THEN** the Backend SHALL validate the JWT token on all protected endpoints

#### Scenario: Role-based authorization enforced
- **GIVEN** a role-specific operation is requested
- **WHEN** the Backend authorizes the operation
- **THEN** the Backend SHALL enforce role-based authorization for role-specific operations
- Validates design property 4: Role-Based Authorization Enforcement

#### Scenario: Frontend stores and sends JWT tokens securely
- **GIVEN** the Frontend has obtained a JWT token
- **WHEN** the user performs authenticated requests
- **THEN** the Frontend SHALL store JWT tokens securely and include them in authenticated requests

#### Scenario: Unauthenticated access redirects to login
- **GIVEN** an unauthenticated user attempts to access a protected route
- **WHEN** the access is evaluated by the Frontend
- **THEN** the Frontend SHALL redirect the user to the login page