# Design: Relational Database Schema in DBML for dbdiagram.io

## Context

The TechStore Management System is an enterprise retail POS and inventory management platform for high-value technology hardware (smartphones, laptops, components). The backend is an ASP.NET Core 10 modular monolith connected to PostgreSQL 16.

To establish a clear visual blueprint for Milestone 1 (Foundation & Database), the database schema will be designed in DBML (Database Markup Language), allowing the team of 6 engineers to visualize the entire relational architecture on [dbdiagram.io](https://dbdiagram.io).

See `proposal.md` for motivation and scope.

## Goals / Non-Goals

**Goals:**
- Author a single, complete, syntactically valid DBML file (`docs/database/schema.dbml`) for import and rendering on dbdiagram.io.
- Define all entities across 6 domain modules:
  - **Catalog**: `categories`, `products`, `product_variants`
  - **Inventory**: `inventory_stocks`, `inventory_movements`, `suppliers`, `purchase_orders`, `purchase_order_items`, `serial_imeis`
  - **Orders**: `orders`, `order_items`, `order_returns`
  - **Customers**: `customers`, `customer_loyalty_points`, `vouchers`
  - **Sales**: `pos_sessions`, `vietqr_transactions`
  - **Identity**: `users`, `roles`, `user_roles`, `refresh_tokens`
- Embed Supabase PostgreSQL best practices directly into DBML constructs:
  - **100% Foreign Key Indexing**: Explicit B-tree indexes defined in `indexes {}` blocks for all foreign keys across all 21 tables.
  - **Composite Foreign Keys**: `(variant_id, product_id) > product_variants.(id, product_id)` to enforce variants belonging strictly to parent products.
  - **PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT`**: Enforced on `inventory_stocks(product_id, variant_id)` and `purchase_order_items(purchase_order_id, product_id, variant_id)`.
  - **JSONB GIN Indexing**: `[type: gin, note: 'jsonb_path_ops']` on `specs` column in `products` and `product_variants`.
  - **Partial Indexes**: Filter predicates annotated in index notes (`WHERE status = 'InStock'`, `WHERE is_active = true`, `WHERE is_revoked = false`).
  - **Data Types**: `timestamptz` for all timestamps, `numeric(18, 2)` for all monetary columns.
  - **Naming Convention**: Clean `snake_case` lowercase naming for all tables, columns, indexes, and constraints.
  - **TableGroups**: 6 color-coded `TableGroup` definitions for visual module boundaries.
  - **Enums**: 10 comprehensive DBML `Enum` declarations for domain states.

**Non-Goals:**
- Generating C# entity classes or EF Core migrations in this step (the DBML file will serve as the blueprint when applying entity implementation in future tasks).
- Writing database triggers, stored procedures, or seed scripts.

---

## Decisions

### 1. Delivery Format: Single Consolidated DBML File (`docs/database/schema.dbml`)
- **Decision**: Author the complete schema in one consolidated DBML file at `docs/database/schema.dbml`.
- **Rationale**: dbdiagram.io imports a single text script to render an interactive visual diagram. Having one complete file enables seamless copy-pasting into dbdiagram.io, while also being version-controlled in the repository.

### 2. Module Grouping via DBML `TableGroup`
- **Decision**: Use DBML `TableGroup` syntax to cluster tables by module:
  ```dbml
  TableGroup Catalog {
    categories
    products
    product_variants
  }
  TableGroup Inventory {
    inventory_stocks
    suppliers
    purchase_orders
    purchase_order_items
    serial_imeis
  }
  TableGroup Orders {
    orders
    order_items
    order_returns
  }
  TableGroup Customers {
    customers
    customer_loyalty_points
    vouchers
  }
  TableGroup Sales {
    pos_sessions
    vietqr_transactions
  }
  TableGroup Identity {
    users
    roles
    user_roles
    refresh_tokens
  }
  ```
- **Rationale**: Keeps the diagram visually structured, clean, and immediately understandable for team members working on different modules.

### 3. Representation of Supabase Best Practices in DBML
- **Foreign Key Indexes**:
  In each table with an FK column, define an explicit index in the `indexes {}` block:
  ```dbml
  Table products {
    id uuid [pk, default: `gen_random_uuid()`]
    category_id uuid [not null, ref: > categories.id]
    // ...
    indexes {
      category_id [name: 'idx_products_category_id']
    }
  }
  ```
- **JSONB GIN Indexing**:
  ```dbml
  specs jsonb [not null, default: `'{}'`, note: 'Dynamic hardware specifications (CPU, RAM, GPU, Battery)']
  indexes {
    specs [type: gin, name: 'idx_products_specs_gin', note: 'jsonb_path_ops index for @> containment queries']
  }
  ```
- **Partial / Filtered Indexes**:
  Document partial indexes with their filter predicates in DBML notes:
  ```dbml
  indexes {
    (product_id, status) [name: 'idx_serial_imeis_instock', note: 'Partial index: WHERE status = "InStock"']
  }
  ```
- **Check Constraints & Concurrency Guarantees**:
  Annotate non-negative stock and price rules in field notes:
  ```dbml
  quantity integer [not null, default: 0, note: 'CHECK quantity >= 0. Supports FOR UPDATE row-level lock concurrency']
  base_price "numeric(18, 2)" [not null, note: 'CHECK base_price >= 0']
  ```
- **Datatypes & Timestamps**:
  Use `"numeric(18, 2)"` (quoted for DBML type parser) and `timestamptz`.

---

## Detailed DBML Structure & Enums

### 1. Enums
- `order_status`: `Pending`, `Processing`, `Completed`, `Cancelled`, `Refunded`
- `payment_method`: `Cash`, `VietQr`, `Card`
- `serial_imei_status`: `InStock`, `Reserved`, `Sold`, `Returned`, `UnderRepair`, `Defective`
- `inventory_movement_type`: `OpeningBalance`, `PurchaseReceipt`, `Sale`, `CustomerReturn`, `Adjustment`, `Defective`
- `customer_tier`: `Standard`, `Silver`, `Gold`, `Platinum`
- `voucher_type`: `Percentage`, `FixedAmount`
- `purchase_order_status`: `Draft`, `Ordered`, `PartiallyReceived`, `Received`, `Cancelled`
- `pos_session_status`: `Open`, `Closed`
- `vietqr_status`: `Pending`, `Confirmed`, `Failed`, `Expired`
- `role_type`: `Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`

### 2. Table Summary (21 Tables)
1. `categories`: Hierarchical category tree with `parent_id` self-reference.
2. `products`: Core product catalog with JSONB specs and serial tracking flag.
3. `product_variants`: Variant skus, pricing, and composite target unique key `(id, product_id)`.
4. `inventory_stocks`: Aggregated stock count with anti-oversell constraints, `reserved_quantity`, and `UNIQUE NULLS NOT DISTINCT`.
5. `inventory_movements`: Append-only audit ledger tracking signed quantity changes, remaining balance, and origin operations.
6. `suppliers`: Vendor/supplier profiles.
7. `purchase_orders`: Inbound procurement orders with partial shipment support (`PartiallyReceived`).
8. `purchase_order_items`: PO line items with quantities, received counts, unit costs, and `UNIQUE NULLS NOT DISTINCT`.
9. `serial_imeis`: Unit-level device tracking (IMEI/Serial, 15-digit regex check, warranty dates, lifecycle status).
10. `orders`: Customer/POS sales orders with status, discount, tax, totals.
11. `order_items`: Order line items with sold IMEI linkage, quantities, and composite foreign keys.
12. `order_returns`: Post-sale returns, refund tracking, and restocking flags.
13. `customers`: Customer profiles and accrued loyalty points.
14. `customer_loyalty_points`: Point accrual/redemption history.
15. `vouchers`: Discount codes with limits, usage count, expiration, and partial index.
16. `pos_sessions`: Cash register shift tracking with opening/closing balances.
17. `vietqr_transactions`: Dynamic VietQR banking payment transactions.
18. `users`: System user credentials and active status.
19. `roles`: RBAC roles.
20. `user_roles`: Many-to-many join table between users and roles.
21. `refresh_tokens`: JWT refresh tokens with SHA-256 `token_hash`, revocation, and expiry tracking.

---

## Risks / Trade-offs

- **Syntax Compatibility**: DBML requires double quotes around complex types with commas like `"numeric(18, 2)"`. Ensuring proper quoting prevents parse errors in dbdiagram.io.
- **Index Attributes**: dbdiagram.io supports `type: btree`, `type: gin`, `type: hash`, `unique`, and `name`. Partial index filter predicates are represented in `note:` fields because standard DBML syntax does not have a native `where:` keyword.
