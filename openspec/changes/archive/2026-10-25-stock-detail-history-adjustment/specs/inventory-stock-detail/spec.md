# Spec Delta

## Purpose

Provides an authoritative view of one inventory balance and a safe workflow for correcting physical quantity while preserving reservation invariants and an auditable movement history.

## ADDED Requirements

### Requirement: Stock detail and movement history
The system SHALL let an authenticated staff user retrieve one stock balance with product metadata, total/reserved/available quantities, related serial count, and the latest movement entries.

#### Scenario: Detail is available
- **WHEN** an authenticated user requests an existing stock detail
- **THEN** the system returns HTTP 200 with the stock metrics and recent movements ordered newest first

#### Scenario: Detail is missing
- **WHEN** an authenticated user requests a stock ID that does not exist
- **THEN** the system returns HTTP 404 and the client keeps the dialog open with a recoverable message

### Requirement: Authorized stock adjustment
The system SHALL allow only Admin and WarehouseStaff users to submit a non-zero quantity adjustment with a required reason and optional note.

#### Scenario: Authorized adjustment succeeds
- **WHEN** an authorized user submits a valid increase or decrease
- **THEN** the system updates the quantity and returns the refreshed stock detail

#### Scenario: Unauthorized adjustment
- **WHEN** an unauthenticated, SalesStaff, or Manager user submits an adjustment
- **THEN** the system returns HTTP 401 or 403 and does not change the stock balance

### Requirement: Reservation-safe adjustment transaction
The system SHALL apply an adjustment atomically while preventing quantity from becoming negative or lower than ReservedQuantity.

#### Scenario: Decrease violates reservation invariant
- **WHEN** a decrease would make quantity lower than the reserved amount
- **THEN** the system returns HTTP 409, leaves quantity unchanged, and records no adjustment movement

#### Scenario: Concurrent adjustment
- **WHEN** two adjustments target the same stock balance concurrently
- **THEN** each transaction serializes on the balance row and the committed quantity and ledger entries remain consistent

### Requirement: Audited adjustment movement
The system SHALL record each successful adjustment as an immutable Adjustment movement containing the signed change, resulting quantity, reason/note context, timestamp, and performing user.

#### Scenario: Ledger entry is created
- **WHEN** an adjustment commits successfully
- **THEN** the returned detail and subsequent history query include the new movement with the correct quantity before and after values
