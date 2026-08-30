# Past Event Consultation — Organizer Metricas Entry Reconciliation

**Requirements covered**: PEC-004 (MODIFIED)

## Purpose

`remove-organizer-delete-metrics` removes the organizer dashboard's "Metricas" kebab entry for ALL rows — past rows included (`role-access` EHE-006). PEC-004 previously REQUIRED that entry to remain enabled on past rows; without this reconciliation the main spec corpus would contradict the settled product decision. This delta only narrows PEC-004: the Admin "Compras" entry requirement is untouched, and organizer consultation of past events ("Ver", PEC-001) is preserved.

## MODIFIED Requirements

### Requirement: PEC-004: Purchases and metrics consultation preserved

On past-event rows, the purchases ("Compras") entry (Admin) MUST remain enabled and functional. The organizer metrics ("Metricas") entry MUST NOT appear on any organizer dashboard row (it was removed change-wide — see `role-access` EHE-006); organizer per-event metrics remain available only through the backend `GET /metrics/events/{id}` for owner/Admin.
(Previously: both the Admin "Compras" entry and the organizer "Metricas" entry were required to remain enabled and functional on past rows; the organizer Metricas entry no longer exists on any row.)

#### Scenario: Compras stays enabled on past row

- GIVEN a past event row in AdminPanel
- WHEN the row renders
- THEN the "Compras" action is enabled and navigates to purchases

#### Scenario: Metricas entry no longer present on past row

- GIVEN a past event row in OrganizerDashboard
- WHEN the row renders
- THEN no "Metricas" action is present (and no dead navigation target remains)

## Coverage Matrix

| Requirement | Scenarios |
|-------------|-----------|
| PEC-004 | compras-enabled, metricas-entry-absent |
