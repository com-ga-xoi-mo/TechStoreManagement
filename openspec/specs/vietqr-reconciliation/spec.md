# vietqr-reconciliation Specification

## Purpose
TBD - created by archiving change m4-vietqr-settlement. Update Purpose after archive.
## Requirements
### Requirement: Keep every bank credit that cannot complete a sale
The system SHALL store in `vietqr_bank_events` each notification of money received that is not applied to a sale: a late transfer after expiry, cancel or shift close; a transfer with a wrong amount, unknown transfer code or wrong account; and a transfer whose reference was already used. Such a notification SHALL NOT complete an order, SHALL NOT change a closed shift and SHALL NOT be discarded. Each event SHALL have a unique `(provider, provider_transaction_id)`, the received `amount`, `transfer_content`, `account_number`, `transferred_at`, the matched `vietqr_transaction_id` when one was found, a `status` (`NeedsReconciliation` or `Resolved`), a `reason` code, and an immutable copy of the verified payload.

#### Scenario: Money after expiry
- **WHEN** a valid signed notification matches a QR that is already `Expired`
- **THEN** the response is 200, the order stays `Cancelled`, the shift is unchanged and an event with status `NeedsReconciliation` and reason `LateAfterExpiry` is stored linked to that QR

#### Scenario: Wrong amount
- **WHEN** a signed notification carries an amount different from the QR amount
- **THEN** the QR stays `Pending`, the order is unchanged and an event with reason `AmountMismatch` is stored

#### Scenario: Unknown transfer code
- **WHEN** no QR carries the transfer code in the notification
- **THEN** an event with reason `Unmatched` is stored and nothing else changes

#### Scenario: Money after the shift closed
- **WHEN** a notification matches a QR of a shift that is already closed
- **THEN** the closed shift totals do not change and an event with reason `LateAfterShiftClose` is stored

#### Scenario: Duplicate notification
- **WHEN** a notification repeats a stored `(provider, transactionId)`
- **THEN** no second event is stored and the response is 200

### Requirement: Review and resolve events
`GET /api/payments/vietqr/events` SHALL list events newest first, filterable by `status`, for Admin and Manager only. `POST /api/payments/vietqr/events/{id}/resolve` SHALL mark an event `Resolved` with a required note of at most 500 characters, the resolver and the time; resolving twice SHALL leave the first resolution unchanged. Resolution does not move money; refunding is done outside this change.

#### Scenario: Manager resolves
- **WHEN** a Manager resolves a `NeedsReconciliation` event with a note
- **THEN** the event is `Resolved` with the manager, time and note recorded

#### Scenario: Sales denied
- **WHEN** a SalesStaff lists or resolves events
- **THEN** the response is 403

#### Scenario: Resolve without a note
- **WHEN** the note is empty
- **THEN** the response is 400 and the event stays open

