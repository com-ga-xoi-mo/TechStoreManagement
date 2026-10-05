# database-schema Specification

## Purpose
Provides an enterprise relational PostgreSQL 16+ schema specification modeled in DBML (Database Markup Language) for dbdiagram.io visualization and EF Core 10 implementation, enforcing data integrity, 100% foreign key indexing, composite foreign keys, PostgreSQL 16 NULLS NOT DISTINCT uniqueness, JSONB containment querying, anti-overselling concurrency protection, append-only movement audit ledgers, and UTC time synchronization in accordance with Supabase Postgres best practices.

## Requirements

### Requirement: DBML Schema Specification for dbdiagram.io Visualization
The database schema MUST be fully defined in valid Database Markup Language (DBML) format within a single file (`docs/database/schema.dbml`), directly importable and visualizable on dbdiagram.io and dbdocs.io without syntax errors or warnings.

#### Scenario: Successful import into dbdiagram.io
- **WHEN** the `docs/database/schema.dbml` file is pasted or imported into dbdiagram.io
- **THEN** dbdiagram.io MUST render all 21 tables, fields, data types, indexes, enums, relationships, check constraint annotations, and TableGroups without syntax warnings or parse errors.

#### Scenario: Logical module organization via TableGroups
- **WHEN** the rendered diagram is viewed
- **THEN** all 21 tables MUST be grouped into 6 color-coded `TableGroup` blocks matching modular monolith boundaries:
  - `TableGroup Catalog`: `categories`, `products`, `product_variants` (3 tables).
  - `TableGroup Inventory`: `inventory_stocks`, `inventory_movements`, `suppliers`, `purchase_orders`, `purchase_order_items`, `serial_imeis` (6 tables).
  - `TableGroup Orders`: `orders`, `order_items`, `order_returns` (3 tables).
  - `TableGroup Customers`: `customers`, `customer_loyalty_points`, `vouchers` (3 tables).
  - `TableGroup Sales`: `pos_sessions`, `vietqr_transactions` (2 tables).
  - `TableGroup Identity`: `users`, `roles`, `user_roles`, `refresh_tokens` (4 tables).

### Requirement: Domain Enums and State Lifecycles
The database schema MUST define 10 domain enums representing exhaustive state machines and domain classifications across all modules.

#### Scenario: Enum completeness
- **WHEN** domain enums are inspected in `schema.dbml`
- **THEN** the schema MUST define exactly 10 enums:
  - `order_status`: `Pending`, `Processing`, `Completed`, `Cancelled`, `Refunded`.
  - `payment_method`: `Cash`, `VietQr`, `Card`.
  - `serial_imei_status`: `InStock`, `Reserved`, `Sold`, `Returned`, `UnderRepair`, `Defective`.
  - `inventory_movement_type`: `OpeningBalance`, `PurchaseReceipt`, `Sale`, `CustomerReturn`, `Adjustment`, `Defective`.
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

### Requirement: Relational Domain Integrity & Multi-Module Schema
The DBML specification MUST model all relational entities across the 6 domain modules with explicit primary keys (`uuid`), foreign keys, relationships (`Ref`), and database check constraints.

#### Scenario: Quantity and monetary check constraints
- **WHEN** tables containing quantities or monetary amounts are inspected
- **THEN** they MUST declare non-negative check constraints:
  - `inventory_stocks`: `quantity >= 0`, `reserved_quantity >= 0`, `reserved_quantity <= quantity`, `min_stock_alert >= 0`.
  - `inventory_movements`: `quantity_change <> 0`, `quantity_after >= 0`.
  - `purchase_orders`: `total_cost >= 0`.
  - `purchase_order_items`: `ordered_quantity > 0`, `received_quantity >= 0 AND received_quantity <= ordered_quantity`, `unit_cost >= 0`.
  - `products`: `base_price >= 0`, `cost_price >= 0`.
  - `product_variants`: `price >= 0`.
  - `orders`: `subtotal >= 0`, `discount_amount >= 0`, `tax_amount >= 0`, `total_amount >= 0`.
  - `order_items`: `unit_price >= 0`, `quantity > 0`, `discount_amount >= 0`, `total_price >= 0`.
  - `order_returns`: `refund_amount >= 0`.
  - `customers`: `loyalty_points >= 0`, `date_of_birth <= CURRENT_DATE`.
  - `customer_loyalty_points`: `points_change <> 0`, `balance_after >= 0`.
  - `vouchers`: `discount_value > 0` (and `<= 100` if Percentage), `min_order_amount >= 0`, `max_discount_amount >= 0`, `usage_limit > 0`, `used_count >= 0 AND used_count <= usage_limit`, `expires_at > created_at`.
  - `pos_sessions`: `opening_balance >= 0`, `cash_sales_total >= 0`, `vietqr_sales_total >= 0`.
  - `vietqr_transactions`: `amount > 0`.

#### Scenario: Serial/IMEI hardware identifier format and unit cost
- **WHEN** `serial_imeis` table is inspected
- **THEN** it MUST enforce check constraints:
  - `NULLIF(BTRIM(serial_number), '') IS NOT NULL OR NULLIF(BTRIM(imei), '') IS NOT NULL` (device has at least one valid identifier).
  - `imei IS NULL OR imei ~ '^[0-9]{15}$'` (IMEI must be exactly 15 numeric digits).
  - `unit_cost IS NULL OR unit_cost >= 0`.

### Requirement: Supabase Postgres 100% Foreign Key Indexing
Every foreign key column across all 21 tables MUST have an explicit B-tree index defined in the table's `indexes {}` block to eliminate sequential scans, avoid slow joins, and prevent table-level locks during cascade operations.

#### Scenario: Complete foreign key index coverage
- **WHEN** any table referencing another entity is inspected
- **THEN** an index with naming `idx_<table>_<col>` MUST be declared on every FK column, including:
  - `categories.parent_id`
  - `products.category_id`
  - `product_variants.product_id`
  - `inventory_stocks.variant_id`
  - `inventory_movements.performed_by_user_id`, `purchase_order_item_id`, `order_item_id`, `order_return_id`
  - `purchase_orders.supplier_id`, `created_by_user_id`, `received_by_user_id`
  - `purchase_order_items.purchase_order_id`, `product_id`, `variant_id`
  - `serial_imeis.product_id`, `variant_id`, `purchase_order_id`
  - `orders.customer_id`, `cashier_user_id`, `pos_session_id`, `voucher_id`
  - `order_items.order_id`, `product_id`, `variant_id`, `serial_imei_id`
  - `order_returns.order_id`, `order_item_id`, `processed_by_user_id`
  - `customer_loyalty_points.customer_id`, `order_id`
  - `pos_sessions.cashier_user_id`
  - `vietqr_transactions.order_id`, `pos_session_id`
  - `user_roles.role_id`
  - `refresh_tokens.user_id`

### Requirement: Composite Foreign Keys and Referential Precision
The schema MUST enforce composite foreign key relationships `(variant_id, product_id)` to guarantee at database level that an allocated variant strictly belongs to its parent product.

#### Scenario: Product variant composite target key
- **WHEN** `product_variants` is inspected
- **THEN** it MUST declare a unique composite constraint `(id, product_id)` named `uq_product_variants_id_product` to serve as the composite target for dependent tables.

#### Scenario: Dependent tables composite foreign keys
- **WHEN** dependent tables referencing variants are inspected
- **THEN** composite foreign key relationships MUST be declared:
  - `inventory_stocks.(variant_id, product_id) > product_variants.(id, product_id)`
  - `purchase_order_items.(variant_id, product_id) > product_variants.(id, product_id)`
  - `serial_imeis.(variant_id, product_id) > product_variants.(id, product_id)`
  - `order_items.(variant_id, product_id) > product_variants.(id, product_id)`

### Requirement: PostgreSQL 16 Unique Nulls Handling
The schema MUST utilize PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT` composite indexes to prevent duplicate stock balance rows or duplicate PO lines for products without variants where `variant_id` is NULL.

#### Scenario: Inventory stock uniqueness
- **WHEN** `inventory_stocks` is inspected
- **THEN** it MUST declare a unique index on `(product_id, variant_id)` with `UNIQUE NULLS NOT DISTINCT` (EF Core `.AreNullsDistinct(false)`) ensuring exactly one balance record exists whether `variant_id` is specified or NULL.

#### Scenario: Purchase order item uniqueness
- **WHEN** `purchase_order_items` is inspected
- **THEN** it MUST declare a unique index `(purchase_order_id, product_id, variant_id)` with `UNIQUE NULLS NOT DISTINCT` ensuring a single line item and unit cost per product/variant on each procurement order.

### Requirement: High-Performance JSONB Hardware Specification Indexing
The dynamic hardware specifications column (`specs`) in `products` and `product_variants` MUST use the `jsonb` datatype and declare a GIN index configured with `jsonb_path_ops` for high-performance containment querying (`@>`).

#### Scenario: JSONB GIN index declaration
- **WHEN** `products` and `product_variants` index blocks are inspected
- **THEN** both MUST declare GIN indexes on `specs` with `jsonb_path_ops` (`idx_products_specs_gin`, `idx_product_variants_specs_gin`).

#### Scenario: Hardware specifications contract and inheritance
- **WHEN** specifications are stored and queried
- **THEN** specifications MUST follow flat JSON object structure with lowercase snake_case keys (`cpu`, `ram_gb`, `storage_gb`, `battery_mah`, `color`), and effective variant specs MUST resolve via runtime inheritance `product.specs || variant.specs` where variant keys override product keys.

### Requirement: High-Selectivity Partial / Filtered Indexing
The schema MUST declare partial indexes for high-frequency operational lookup paths, documenting valid PostgreSQL filter predicates.

#### Scenario: POS in-stock Serial/IMEI scanning index
- **WHEN** `serial_imeis` indexes are inspected
- **THEN** a partial index `(product_id, status)` MUST be declared with filter predicate `WHERE status = 'InStock'` for accelerated checkout barcode/IMEI scanning.

#### Scenario: Active voucher lookup index
- **WHEN** `vouchers` indexes are inspected
- **THEN** a composite index `(is_active, expires_at)` MUST be declared with filter predicate `WHERE is_active = true`, deferring the dynamic `expires_at > now()` comparison to query time because PostgreSQL forbids non-immutable functions in index predicates.

#### Scenario: Active refresh token verification index
- **WHEN** `refresh_tokens` indexes are inspected
- **THEN** `token_hash` MUST store a SHA-256 hash of the token, and a partial index `(user_id, token_hash)` MUST be declared with filter predicate `WHERE is_revoked = false`.

### Requirement: Anti-Overselling Concurrency & Audit Ledger Invariants
The schema MUST enforce anti-overselling constraints and an append-only inventory movement audit ledger to maintain 100% financial and physical stock consistency under concurrent transactions.

#### Scenario: Physical stock and reservation bound
- **WHEN** reservations or stock sales occur
- **THEN** `inventory_stocks` MUST enforce `reserved_quantity <= quantity`, and available-to-sell stock MUST be calculated as `quantity - reserved_quantity` under pessimistic row-level locking (`FOR UPDATE`).

#### Scenario: Serial-tracked quantity consistency
- **WHEN** a product has `is_serial_tracked = true`
- **THEN** `inventory_stocks.quantity` MUST equal `COUNT(serial_imeis WHERE status IN ('InStock', 'Reserved'))`.

#### Scenario: Append-only movement audit ledger
- **WHEN** physical stock balance changes
- **THEN** a row MUST be inserted into `inventory_movements` in the same database transaction recording `inventory_stock_id`, `movement_type`, signed `quantity_change`, `quantity_after`, `performed_by_user_id`, and origin reference (`purchase_order_item_id`, `order_item_id`, or `order_return_id`).

### Requirement: Data Types and Precision Standardization
The DBML schema MUST enforce strict Supabase Postgres data typing rules:
1. All timestamp columns MUST use UTC `timestamptz`.
2. All monetary columns MUST use `numeric(18, 2)`.
3. All identifiers MUST use lowercase `snake_case`.

#### Scenario: Datatype uniformity
- **WHEN** timestamp, currency, and identifier columns are inspected across all 21 tables
- **THEN** temporal columns MUST be `timestamptz`, monetary columns MUST be `numeric(18, 2)`, and all table, column, enum, index, and constraint names MUST use lowercase `snake_case`.
