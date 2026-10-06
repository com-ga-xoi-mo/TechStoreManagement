# data-seeding Specification

## Purpose

Provides automated, deterministic, and idempotent baseline seed data populating all 21 tables in PostgreSQL 16 to empower development and manual verification immediately upon system startup.

## Requirements

### Requirement: Role and Administrative Identity Seeding
The data seeder SHALL seed system roles and an initial administrative user account into PostgreSQL 16.

#### Scenario: Administrative roles and admin user created
- **WHEN** the seeder executes on an empty identity database
- **THEN** exactly 4 roles MUST be created (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`), and an active user with username `admin`, email `admin@techstore.vn`, and valid password hash corresponding to `Admin@123` MUST be assigned the `Admin` role.

### Requirement: Product Catalog Baseline Seeding
The data seeder SHALL seed a realistic hardware catalog taxonomy comprising categories, hardware products with JSONB specifications, and product variants.

#### Scenario: Realistic catalog taxonomy population
- **WHEN** the seeder runs
- **THEN** at least 2 distinct categories (e.g., Smartphones, Laptops) and at least 5 products with non-empty `specs` JSONB payloads and at least 2 product variants MUST be persisted in the catalog tables.

### Requirement: Inventory Stock and Device Tracking Seeding
The data seeder SHALL initialize inventory records conforming to anti-overselling and serial-tracking invariants.

#### Scenario: Serial and IMEI consistency
- **WHEN** serial-tracked hardware products are seeded
- **THEN** valid `serial_imeis` records in `InStock` status MUST exist with valid 15-digit numeric IMEIs or non-empty serial numbers, matching `inventory_stocks.quantity` exactly, accompanied by append-only `inventory_movements` ledger entries.

#### Scenario: Supplier and procurement order seeding
- **WHEN** inventory baseline is created
- **THEN** at least 1 supplier and at least 1 completed purchase order with line items MUST be recorded.

### Requirement: Customer Profiles and Promotional Vouchers Seeding
The data seeder SHALL seed customer accounts and discount vouchers.

#### Scenario: Customer profiles with loyalty balance
- **WHEN** customer tables are seeded
- **THEN** at least 2 active customer profiles with unique phone numbers, loyalty points, and tier classifications MUST be present, along with at least 1 active voucher with a valid code and future expiration date.

### Requirement: Retail Orders and Sales Fulfillment Seeding
The data seeder SHALL seed representative sales orders demonstrating end-to-end checkout completion.

#### Scenario: Completed order with line item snapshots
- **WHEN** sales history is seeded
- **THEN** at least 2 orders in `Completed` status MUST exist with line items capturing product snapshot names, unit prices, sold serial/IMEI linkages, and corresponding payment records.

### Requirement: Idempotent Seeding Execution
The data seeder SHALL be idempotent, ensuring repeated application startups or CLI executions do not produce duplicate key errors or duplicate entity entries.

#### Scenario: Repeated seeder execution
- **WHEN** the data seeder runs consecutively against an already-seeded database
- **THEN** the execution MUST complete successfully without throwing unique constraint violations and without creating duplicate records.
