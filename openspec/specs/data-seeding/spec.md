# data-seeding Specification

## Purpose

Provides automated, deterministic, and idempotent baseline seed data populating all 21 tables in PostgreSQL 16 to empower development and manual verification immediately upon system startup.

## Requirements

### Requirement: Role and Administrative Identity Seeding
The data seeder SHALL ensure the four system roles and initial administrative identity exist without resetting existing credentials or user data.

#### Scenario: Administrative roles and admin user created
- **WHEN** the seeder runs against an empty identity database
- **THEN** exactly four roles (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`) and an active `admin@techstore.vn` user with fixed identifier `11111111-1111-1111-1111-111111111111` and Admin role MUST be created.

#### Scenario: Existing identity records are preserved
- **WHEN** one or more roles or users already exist, including a Sprint 1 baseline
- **THEN** missing seed roles and identities MUST be added, while existing passwords, profile values, role assignments, and unrelated user records remain unchanged.

### Requirement: Product Catalog Baseline Seeding
The data seeder SHALL ensure a realistic hardware catalog with at least 5 distinct categories, 20 products with non-empty specifications, and product variants.

#### Scenario: Realistic catalog taxonomy population
- **WHEN** the seeder runs on an empty or Sprint 1 database
- **THEN** the database MUST contain at least 5 categories, 20 products with non-empty JSONB specifications, and 2 product variants; existing Sprint 1 catalog rows MUST be reconciled rather than duplicated.

### Requirement: Inventory Stock and Device Tracking Seeding
The data seeder SHALL ensure inventory data meets database constraints and order/inventory lifecycle invariants, including at least 50 unique schema-valid 15-digit numeric IMEIs.

#### Scenario: Serial and IMEI consistency
- **WHEN** the expanded inventory dataset is present
- **THEN** it MUST include at least 50 distinct 15-digit numeric IMEIs, and for each serial-tracked balance its quantity MUST equal the number of associated `InStock` and `Reserved` serials; reservation and sale movements and purchase receipts MUST reconcile with their source records.

#### Scenario: Supplier and procurement order seeding
- **WHEN** inventory baseline is created
- **THEN** at least one supplier and one received purchase order with line items MUST be present, and receipt ledger entries MUST be created once and reconcile to received quantities.

### Requirement: Customer Profiles and Promotional Vouchers Seeding
The data seeder SHALL ensure at least 10 active customer profiles with unique phone numbers and valid promotional voucher data.

#### Scenario: Customer profiles with loyalty balance
- **WHEN** the customer dataset is seeded
- **THEN** at least 10 active customer profiles with unique phone numbers, coherent loyalty balances and tier classifications, and at least one active voucher with a valid code and future expiration date MUST be present.

### Requirement: Retail Orders and Sales Fulfillment Seeding
The data seeder SHALL ensure at least 20 representative orders in multiple lifecycle states with internally consistent items, inventory effects, sessions, and payment records.

#### Scenario: Completed order with line item snapshots
- **WHEN** the expanded sales history is present
- **THEN** at least 20 orders MUST span multiple statuses, including at least two Completed orders; Completed orders MUST have item snapshots, matching paid/completed timestamps, required payment evidence, sold serial links where applicable, and corresponding stock, warranty, and sale-ledger effects.

#### Scenario: Non-completed order lifecycle consistency
- **WHEN** seeded orders are Pending, Processing, Cancelled, PartiallyRefunded, or Refunded
- **THEN** their timestamps, reservations, serial states, returns/refunds, POS session effects, and payment records MUST agree with the existing order and inventory lifecycle; an unpaid Pending cancellation MUST not create a reservation or sale movement.

### Requirement: Idempotent Seeding Execution
The data seeder SHALL be additive and idempotent across empty, legacy, rerun, and partially populated databases, using stable seed identity and deterministic timestamps.

#### Scenario: Repeated seeder execution
- **WHEN** the seeder runs consecutively against a database containing seed rows
- **THEN** it MUST create no duplicate rows, stock quantity changes, purchase receipts, sale movements, or transaction-date changes.

#### Scenario: Legacy baseline upgrade
- **WHEN** the seeder runs against the Sprint 1 baseline whose seeded rows have generated identifiers
- **THEN** it MUST identify matching rows by documented natural keys, reuse their identifiers and relationships, and add only missing seed-owned rows.

#### Scenario: Partial seed and conflicting records
- **WHEN** some seed rows are missing or a natural key matches a record whose immutable identity or lifecycle state conflicts with the expected seed fixture
- **THEN** missing seed-owned rows MAY be added without altering existing records, while conflicts MUST produce a clear failure and roll back the current seed transaction rather than silently overwrite or repair user-modified data.

#### Scenario: Stable seed timestamps
- **WHEN** a database is seeded more than once
- **THEN** seed-owned business timestamps MUST derive from the documented fixed UTC anchor and remain unchanged on reruns.

#### Scenario: Analytics reconciliation
- **WHEN** the deterministic demo dataset is queried under the existing Admin Analytics contract at its documented reference as-of time
- **THEN** the documented expected KPI, daily-revenue, recent-order, and low-stock results MUST match the aggregate query results, including zero-filled trend days and exact revenue reconciliation.
