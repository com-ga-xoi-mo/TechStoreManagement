# Proposal: Database Schema Design with Supabase Postgres Best Practices (DBML for dbdiagram)

## Why

The TechStore Management System is in Milestone 1 (Foundation & Database), requiring a robust, enterprise-grade PostgreSQL schema design that team members can clearly visualize, inspect, and review on dbdiagram.io before and during application development.

Designing the schema directly into DBML (Database Markup Language) while incorporating Supabase PostgreSQL best practices ensures the team has an unambiguous visual blueprint. It prevents common architectural pitfalls from day one: missing foreign key indexes, unindexed JSONB queries, timezone inaccuracies, floating-point currency drift, missing check constraints, and race conditions during stock deduction.

## What Changes

- **DBML Schema Specification for dbdiagram.io**:
  - Deliver a complete, self-contained DBML specification file (`docs/database/schema.dbml`) ready for direct import and visualization on [dbdiagram.io](https://dbdiagram.io).
  - Organize tables into logical `TableGroup` definitions across all 6 domain modules:
    - **Catalog**: `categories`, `products`, `product_variants` (3 tables)
    - **Inventory**: `inventory_stocks`, `inventory_movements`, `suppliers`, `purchase_orders`, `purchase_order_items`, `serial_imeis` (6 tables)
    - **Orders**: `orders`, `order_items`, `order_returns` (3 tables)
    - **Customers**: `customers`, `customer_loyalty_points`, `vouchers` (3 tables)
    - **Sales**: `pos_sessions`, `vietqr_transactions` (2 tables)
    - **Identity**: `users`, `roles`, `user_roles`, `refresh_tokens` (4 tables)
- **Supabase Postgres Best Practices Embedded in DBML**:
  - **Primary Keys**: Explicit UUID primary keys (`uuid [pk, default: 'gen_random_uuid()']`).
  - **Foreign Key Indexing**: Explicit B-tree index declarations on **every** foreign key column in table `indexes {}` blocks to prevent full table scans and lock contention.
  - **Data Types**:
    - `timestamptz` (`timestamp with time zone`) for all temporal audit and event fields.
    - `numeric(18, 2)` for all monetary prices, subtotals, discounts, taxes, and totals.
    - Snake_case lowercase identifiers for all tables, columns, indexes, and constraints.
  - **JSONB Specs Indexing**: GIN index on `specs` JSONB column with note for `jsonb_path_ops`.
  - **Composite Foreign Keys**: `(variant_id, product_id) > product_variants.(id, product_id)` to guarantee variant-to-parent integrity across dependent tables.
  - **PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT`**: Enforced on `inventory_stocks(product_id, variant_id)` and `purchase_order_items(purchase_order_id, product_id, variant_id)`.
  - **Partial / Filtered Indexes**: Partial index definitions with filter predicates documented in index notes (e.g., `(product_id, status) [name: 'idx_serial_imeis_instock', note: 'WHERE status = "InStock"']`, `vouchers` on `(is_active, expires_at)` WHERE `is_active = true`, `refresh_tokens` on `(user_id, token_hash)` WHERE `is_revoked = false`).
  - **Data Integrity & Constraints**: Field notes specifying `CHECK` constraints (non-negative stock, non-negative price, valid discount range, 15-digit IMEI regex).
  - **Domain Enums**: 10 comprehensive DBML `Enum` declarations for state lifecycles (`order_status`, `payment_method`, `serial_imei_status`, `inventory_movement_type`, `customer_tier`, `voucher_type`, `purchase_order_status`, `pos_session_status`, `vietqr_status`, `role_type`).
  - **Relationships**: Explicit `Ref` declarations representing one-to-many, many-to-many, and cascading delete policies (38 relations).

## Capabilities

### New Capabilities
- `database-schema`: Core PostgreSQL relational schema modeled in DBML for dbdiagram.io visualization, incorporating Supabase Postgres best practices (100% FK indexing, JSONB GIN indexing, partial indexing, timestamptz/numeric(18,2) types, check constraints).

### Modified Capabilities
(None)

## Impact

- **Affected Files**:
  - `docs/database/schema.dbml`: Newly created DBML schema definition file.
- **Dependencies**: None (pure DBML specification readable by dbdiagram.io and dbdocs.io).
- **Breaking Changes**: None.
- **Migration Considerations**: The DBML file serves as the definitive visual architecture blueprint for subsequent EF Core entity classes, configurations, and migration scripts.
