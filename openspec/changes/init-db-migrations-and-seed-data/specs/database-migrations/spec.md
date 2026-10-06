# Spec Delta

## Purpose

Enforces automated Entity Framework Core database migration management and schema synchronization across 21 relational tables on PostgreSQL 16, maintaining structural parity with the authoritative DBML schema specification and Supabase best practices.

## ADDED Requirements

### Requirement: Complete 21-Table Schema Synchronization
The database migration management system SHALL define and synchronize all 21 relational tables across the 6 domain modules into PostgreSQL 16: `categories`, `products`, `product_variants` (Catalog); `inventory_stocks`, `inventory_movements`, `suppliers`, `purchase_orders`, `purchase_order_items`, `serial_imeis` (Inventory); `orders`, `order_items`, `order_returns` (Orders); `customers`, `customer_loyalty_points`, `vouchers` (Customers); `pos_sessions`, `vietqr_transactions` (Sales); and `users`, `roles`, `user_roles`, `refresh_tokens` (Identity).

#### Scenario: Migration creation and execution produces 21 tables
- **WHEN** the EF Core database migration `InitialCreate` is applied to PostgreSQL 16 via `dotnet ef database update`
- **THEN** exactly 21 domain tables plus the standard `__EFMigrationsHistory` table MUST be created in the database public schema with matching columns and types.

### Requirement: 100% Foreign Key Indexing
Every foreign key column across all 21 tables SHALL have an explicit B-tree index created in PostgreSQL 16 to avoid sequential scans and eliminate table-level locks during cascade operations.

#### Scenario: Index existence for all relational foreign keys
- **WHEN** foreign key indexes are inspected in PostgreSQL system catalog `pg_indexes`
- **THEN** every foreign key relationship MUST possess a dedicated B-tree index adhering to the `idx_<table>_<column>` naming convention.

### Requirement: Composite Foreign Keys for Product-Variant Consistency
The database migration SHALL enforce composite foreign key relationships `(variant_id, product_id)` referencing `product_variants(id, product_id)` across `inventory_stocks`, `purchase_order_items`, `serial_imeis`, and `order_items`.

#### Scenario: Variant belongs strictly to parent product
- **WHEN** a row is inserted into `inventory_stocks`, `purchase_order_items`, `serial_imeis`, or `order_items` with a mismatched `(variant_id, product_id)` pair
- **THEN** PostgreSQL 16 MUST reject the insert with a foreign key constraint violation.

### Requirement: PostgreSQL 16 Unique Nulls Distinctness
The migration system SHALL configure composite unique constraints with PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT` on `inventory_stocks(product_id, variant_id)` and `purchase_order_items(purchase_order_id, product_id, variant_id)`.

#### Scenario: Single inventory balance row when variant is null
- **WHEN** an inventory stock balance is created for a product without variants (`variant_id` is NULL) and a second row is inserted for the same `product_id` with NULL `variant_id`
- **THEN** PostgreSQL 16 MUST reject the second row as a unique constraint violation.

### Requirement: JSONB Hardware Specifications Indexing
The migration SHALL configure PostgreSQL GIN indexes using the `jsonb_path_ops` operator class on the `specs` column of both `products` and `product_variants`.

#### Scenario: GIN index created with jsonb_path_ops
- **WHEN** index definitions for `products` and `product_variants` are inspected in `pg_indexes`
- **THEN** `idx_products_specs_gin` and `idx_product_variants_specs_gin` MUST be present using the `gin` access method with `jsonb_path_ops`.

### Requirement: Database Check Constraints
The database migration SHALL apply check constraints for financial amounts, quantities, dates, and device identifiers as specified in the schema architecture.

#### Scenario: Rejection of negative stock quantities
- **WHEN** an update or insert attempts to set `quantity < 0` or `reserved_quantity < 0` or `reserved_quantity > quantity` on `inventory_stocks`
- **THEN** PostgreSQL 16 MUST reject the transaction with a check constraint violation.

#### Scenario: Rejection of invalid IMEI format
- **WHEN** a non-null IMEI string is inserted into `serial_imeis` that is not exactly 15 numeric digits
- **THEN** PostgreSQL 16 MUST reject the insert with constraint violation `ck_serial_imeis_imei_format`.

### Requirement: Partial Operational Indexes
The migration SHALL create high-selectivity partial indexes for critical query paths.

#### Scenario: Partial index for in-stock devices
- **WHEN** indexes on `serial_imeis` are queried in `pg_indexes`
- **THEN** `idx_serial_imeis_instock` MUST exist with the predicate `WHERE status = 'InStock'`.

#### Scenario: Partial index for active vouchers
- **WHEN** indexes on `vouchers` are queried in `pg_indexes`
- **THEN** `idx_vouchers_active` MUST exist on `(is_active, expires_at)` with the predicate `WHERE is_active = true`.

#### Scenario: Partial index for active refresh tokens
- **WHEN** indexes on `refresh_tokens` are queried in `pg_indexes`
- **THEN** `idx_refresh_tokens_active` MUST exist on `(user_id, token_hash)` with the predicate `WHERE is_revoked = false`.
