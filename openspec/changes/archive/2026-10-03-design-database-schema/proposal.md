# Proposal: Database Schema Design with Supabase Postgres Best Practices (DBML for dbdiagram)

## Why

The TechStore Management System is in Milestone 1 (Foundation & Database), requiring a robust, enterprise-grade PostgreSQL schema design that team members can clearly visualize, inspect, and review on dbdiagram.io before and during application development.

Designing the schema directly into DBML (Database Markup Language) while incorporating Supabase PostgreSQL best practices ensures the team has an unambiguous visual blueprint. It prevents common architectural pitfalls from day one: missing foreign key indexes, unindexed JSONB queries, timezone inaccuracies, floating-point currency drift, missing check constraints, and race conditions during stock deduction.

## What Changes

- **DBML Schema Specification for dbdiagram.io**:
  - Deliver a complete, self-contained DBML specification file (`docs/database/schema.dbml`) ready for direct import and visualization on [dbdiagram.io](https://dbdiagram.io).
  - Organize tables into logical `TableGroup` definitions across all 6 domain modules:
    - **Catalog**: `categories`, `products`, `product_variants`
    - **Inventory**: `inventory_stocks`, `suppliers`, `purchase_orders`, `purchase_order_items`, `serial_imeis`
    - **Orders**: `orders`, `order_items`, `order_returns`
    - **Customers**: `customers`, `customer_loyalty_points`, `vouchers`
    - **Sales**: `pos_sessions`, `vietqr_transactions`
    - **Identity**: `users`, `roles`, `user_roles`, `refresh_tokens`
- **Supabase Postgres Best Practices Embedded in DBML**:
  - **Primary Keys**: Explicit UUID primary keys (`uuid [pk, default: 'gen_random_uuid()']`).
  - **Foreign Key Indexing**: Explicit B-tree index declarations on **every** foreign key column in table `indexes {}` blocks to prevent full table scans and lock contention.
  - **Data Types**:
    - `timestamptz` (`timestamp with time zone`) for all temporal audit and event fields.
    - `numeric(18, 2)` for all monetary prices, subtotals, discounts, taxes, and totals.
    - Snake_case lowercase identifiers for all tables, columns, indexes, and constraints.
  - **JSONB Specs Indexing**: GIN index on `specs` JSONB column with note for `jsonb_path_ops`.
  - **Partial / Filtered Indexes**: Partial index definitions with filter predicates documented in index notes (e.g., `(product_id, status) [name: 'idx_serial_imeis_instock', note: 'WHERE status = "InStock"']`).
  - **Data Integrity & Constraints**: Field notes specifying `CHECK` constraints (non-negative stock, non-negative price, valid discount range).
  - **Domain Enums**: Comprehensive DBML `Enum` declarations for state lifecycles (`order_status`, `serial_imei_status`, `role_type`, `payment_method`, etc.).
  - **Relationships**: Explicit `Ref` declarations representing one-to-many, many-to-many, and cascading delete policies.

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
