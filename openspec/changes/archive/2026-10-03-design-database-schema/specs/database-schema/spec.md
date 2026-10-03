# Spec Delta: database-schema

## Purpose
Provides an enterprise relational PostgreSQL schema specification modeled in DBML (Database Markup Language) for dbdiagram.io visualization, enforcing data integrity, foreign key indexing, JSONB containment querying, row-level concurrency protection, and UTC time synchronization in accordance with Supabase Postgres best practices.

## ADDED Requirements

### Requirement: DBML Schema Specification for dbdiagram.io Visualization
The database schema MUST be fully defined in valid Database Markup Language (DBML) format within a single file (`docs/database/schema.dbml`), directly importable and visualizable on dbdiagram.io without syntax errors.

#### Scenario: Successful import into dbdiagram.io
- **WHEN** the DBML file is pasted or imported into dbdiagram.io
- **THEN** dbdiagram.io MUST render all tables, fields, types, indexes, enums, relationships, and TableGroups without syntax warnings or parse errors.

#### Scenario: Logical module organization via TableGroups
- **WHEN** the rendered diagram is viewed
- **THEN** tables MUST be organized into 6 color-coded `TableGroup` blocks: `Catalog`, `Inventory`, `Orders`, `Customers`, `Sales`, and `Identity`.

---

### Requirement: Relational Domain Integrity & Multi-Module Schema
The DBML specification MUST model all relational entities across the 6 domain modules with explicit primary keys (`uuid`), foreign keys, relationships (`Ref`), and check constraint documentation.

#### Scenario: Serial/IMEI lifecycle and uniqueness
- **WHEN** `serial_imeis` is inspected in the schema
- **THEN** it MUST declare unique indexes on `serial_number` and `imei`, reference `products.id` and `product_variants.id`, and track states via the `serial_imei_status` enum.

#### Scenario: Non-negative stock and price constraints
- **WHEN** fields representing quantities or prices are inspected
- **THEN** they MUST be annotated with check constraints (`quantity >= 0`, `reserved_quantity >= 0`, `price >= 0`, `unit_price >= 0`).

---

### Requirement: Supabase Postgres Foreign Key Indexing
Every foreign key column across all tables MUST have an explicit B-tree index defined in the table's `indexes {}` block to eliminate sequential scans, avoid slow joins, and prevent table-level locks during cascade validations.

#### Scenario: Comprehensive foreign key index coverage
- **WHEN** any table referencing another table (e.g. `products.category_id`, `order_items.order_id`, `purchase_orders.supplier_id`, `user_roles.role_id`) is inspected
- **THEN** an index with standard naming `idx_<table>_<fk_col>` MUST be explicitly declared in its `indexes {}` block.

---

### Requirement: High-Performance JSONB Hardware Specification Indexing
The dynamic hardware specifications column (`specs`) in `products` and `product_variants` MUST use the `jsonb` datatype and declare a GIN index configured with `jsonb_path_ops` for fast containment querying (`@>`).

#### Scenario: JSONB GIN index declaration
- **WHEN** the `products` table indexes block is inspected
- **THEN** it MUST declare an index on `specs` with type `gin` and note `'jsonb_path_ops'`.

---

### Requirement: High-Selectivity Partial / Filtered Indexing
The schema MUST declare partial indexes for high-frequency operational queries, documenting the filter predicate in the index definition.

#### Scenario: POS in-stock IMEI scanning index
- **WHEN** `serial_imeis` indexes are inspected
- **THEN** a partial index `(product_id, status)` MUST be declared with note `'WHERE status = "InStock"'`.

#### Scenario: Active voucher validation index
- **WHEN** `vouchers` indexes are inspected
- **THEN** a partial index on `code` MUST be declared with note `'WHERE is_active = true AND expires_at > now()'`.

#### Scenario: Active refresh token verification index
- **WHEN** `refresh_tokens` indexes are inspected
- **THEN** a partial index on `(user_id, token)` MUST be declared with note `'WHERE is_revoked = false'`.

---

### Requirement: Data Types and Precision Standardization
The DBML schema MUST enforce strict Supabase Postgres data typing rules:
1. All timestamp columns MUST use `timestamptz`.
2. All monetary columns MUST use `numeric(18, 2)`.
3. All identifiers MUST use lowercase `snake_case`.

#### Scenario: Datatype uniformity
- **WHEN** timestamp and currency columns are inspected across all tables
- **THEN** temporal columns MUST be `timestamptz` and monetary columns MUST be `numeric(18, 2)`.
