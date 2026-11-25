# Spec Delta

## MODIFIED Requirements

### Requirement: Domain Enums and State Lifecycles
The database schema MUST define 10 domain enums representing exhaustive state machines and domain classifications across all modules.

#### Scenario: Enum completeness
- **WHEN** domain enums are inspected in `schema.dbml`
- **THEN** the schema MUST define exactly 10 enums:
  - `order_status`: `Pending`, `Processing`, `Completed`, `Cancelled`, `PartiallyRefunded`, `Refunded`.
  - `payment_method`: `Cash`, `VietQr`, `Card`.
  - `serial_imei_status`: `InStock`, `Reserved`, `Sold`, `Returned`, `UnderRepair`, `Defective`.
  - `inventory_movement_type`: `OpeningBalance`, `PurchaseReceipt`, `Sale`, `CustomerReturn`, `Adjustment`, `Defective`, `WarrantyReplacement`.
  - `customer_tier`: `Standard`, `Silver`, `Gold`, `Platinum`.
  - `voucher_type`: `Percentage`, `FixedAmount`.
  - `purchase_order_status`: `Draft`, `Ordered`, `PartiallyReceived`, `Received`, `Cancelled`.
  - `pos_session_status`: `Open`, `Closed`.
  - `vietqr_status`: `Pending`, `Confirmed`, `Failed`, `Expired`.
  - `role_type`: `Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`.

#### Scenario: Serial/IMEI lifecycle states
- **WHEN** `serial_imei_status` is inspected
- **THEN** it MUST include `Reserved` to lock devices for pending checkout, `Returned` for customer returns pending inspection, and warranty status MUST be derived from `warranty_start_at` and `warranty_end_at` rather than an enum value.

#### Scenario: Procurement partial shipment support
- **WHEN** `purchase_order_status` is inspected
- **THEN** it MUST include `PartiallyReceived` to support split deliveries from suppliers while keeping procurement orders auditable.

#### Scenario: Warranty replacement movement
- **WHEN** `inventory_movement_type` is inspected
- **THEN** it MUST include `WarrantyReplacement` for a device leaving stock as a warranty replacement (-1), kept separate from `Sale` so reports do not count it as revenue.

## ADDED Requirements

### Requirement: Warranty case storage
`warranty_cases` MUST hold one row per intake: a unique `case_code`, `issue_description`, `contact_name`, `contact_phone`, `inspection_note` and `rejection_reason` (all nullable for rows created before this change), plus its existing columns. `warranty_case_events` MUST be the append-only history. Every new FK MUST have an `idx_<table>_<column>` index. Statuses MUST be stored as text guarded by a check constraint. The change MUST ship as one EF Core migration that matches `schema.dbml`.

#### Scenario: Status values
- **WHEN** the status check of `warranty_cases` is inspected
- **THEN** it MUST allow only `Received`, `Repairing`, `PendingReplacement`, `Repaired`, `Rejected`, `ReturnedToCustomer` and `Replaced`

#### Scenario: Open and closed rows are consistent
- **WHEN** a row is stored
- **THEN** a check constraint MUST require:
  - open statuses to have no closing fields, replacement device, movement or replacement cost;
  - `ReturnedToCustomer` and `Replaced` to keep the existing closing rules;
  - `Rejected` to have a `rejection_reason`;
  - `replacement_unit_cost >= 0` when present

#### Scenario: One open case per device
- **WHEN** a second case with an open status is inserted for a device that already has one
- **THEN** the partial unique index `uq_warranty_cases_open_device` rejects it

#### Scenario: Existing rows migrated
- **WHEN** the migration runs on a database with PR #31 data
- **THEN**:
  - rows with status `UnderRepair` become `Received`;
  - every row gets a unique `case_code` `BH-yyyyMM-NNNN` from `opened_at`;
  - every row gets a receive event, and closed rows also get a closing event

#### Scenario: Event rows
- **WHEN** `warranty_case_events` is inspected
- **THEN** it MUST have `id`, `case_id` (FK `warranty_cases`), nullable `from_status`, `to_status`, `actor_id` (FK `users`), nullable `note` and `created_at`, with indexes on `(case_id, created_at)` and `actor_id`
