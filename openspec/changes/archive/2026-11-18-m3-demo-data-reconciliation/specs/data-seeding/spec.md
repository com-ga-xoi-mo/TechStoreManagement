# Spec Delta

## MODIFIED Requirements

### Requirement: Product Catalog Baseline Seeding

The data seeder SHALL ensure a realistic hardware catalog with at least 5 distinct categories, 20 products with non-empty specifications and seeded cost prices, and at least 2 product variants. Existing cost values SHALL be preserved during reconciliation.

#### Scenario: Realistic catalog taxonomy population

- **WHEN** the seeder runs on an empty or Sprint 1 database
- **THEN** the database MUST contain at least 5 categories, 20 products with non-empty JSONB specifications, and at least 2 product variants. New seeded products MUST have the positive cost price defined by the fixture, and matching Sprint 1 catalog rows MUST be reconciled without duplication or overwriting their cost values.

### Requirement: Inventory Stock and Device Tracking Seeding

The data seeder SHALL ensure inventory meets database constraints and order/inventory lifecycle invariants. Seeded IMEIs SHALL be unique and schema-valid 15-digit numeric strings.

#### Scenario: Serial and IMEI consistency

- **WHEN** the expanded inventory dataset is present
- **THEN** IMEIs in the fixture are distinct 15-digit numeric strings. For each serial-tracked balance, quantity MUST equal the number of associated `InStock` and `Reserved` serials, and reserved quantity MUST equal the number of associated `Reserved` serials. Reservation and sale movements and purchase receipts MUST reconcile with their source records.

#### Scenario: Supplier and procurement order seeding

- **WHEN** inventory baseline is created
- **THEN** at least one supplier and one received purchase order with line items MUST be present, and receipt ledger entries MUST be created once and reconcile to received quantities.

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

- **WHEN** an authorized caller requests `today`, `last7Days`, `thisMonth` and `custom` from the Analytics API against the seeded fixture in a test host anchored at `2026-10-18T05:00:00Z`
- **THEN** each response matches independently calculated Expected values for period bounds, revenue, order count, active products, low-stock count and items, and the list of up to five most recent orders. It MUST include each UTC+07 calendar day in order with zero on days without completed sales and the daily amounts MUST sum exactly to the period revenue. The verification record MUST show Expected and Actual separately.

#### Scenario: Live demo uses a historical reporting interval

- **WHEN** a user runs the demo from the same seed on another calendar date and selects the documented historical custom period
- **THEN** orders and revenue reflect the requested dates and response `asOf`, active products and low stock reflect the current snapshot, and the demo guide explains how that differs from the fixed reference run.

## ADDED Requirements

### Requirement: Usable cover images for seeded products

A fresh Development seed SHALL give each seeded product one shared cover image served by the configured object storage. The catalog SHALL store image metadata and the computed cover URL and retain existing galleries and legacy cover URLs when reconciling a database.

#### Scenario: Access images on a fresh demo database

- **WHEN** the seed completes against an empty database with writable configured storage and the demo reads the seeded product details
- **THEN** the gallery lists the shared image at sort order 0 with the same URL as the cover, and fetching that URL returns the stored image with the expected media type. Default Local storage serves the URL through the API.

#### Scenario: Fill a missing gallery and preserve existing images

- **WHEN** a fixture SKU on an existing database has neither catalog images nor a legacy cover URL, while another fixture SKU already has an image gallery or a legacy cover
- **THEN** the first product gains one shared image. The second product retains its references and cover. Rerunning against the same database and storage introduces no duplicate metadata or objects.

#### Scenario: Failed seed image writes leave no incomplete reference

- **WHEN** storing a seed image fails, the seed hits a fixture conflict, or its database transaction rolls back
- **THEN** the attempt reports failure, leaves existing images and seed data intact, and persists no new cover or image reference to a failed upload. After a confirmed rollback it attempts cleanup of objects created by that attempt.
