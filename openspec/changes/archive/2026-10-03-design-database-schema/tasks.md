# Tasks: Database Schema DBML Generation for dbdiagram.io

## 1. Setup & DBML Domain Enums

- [x] 1.1 Create directory `docs/database/` to house the schema definition file.
- [x] 1.2 Define all 10 domain enums in DBML format: `order_status`, `payment_method`, `serial_imei_status`, `inventory_movement_type`, `customer_tier`, `voucher_type`, `purchase_order_status`, `pos_session_status`, `vietqr_status`, and `role_type`.

## 2. Catalog Module Tables in DBML

- [x] 2.1 Define `categories` table with `id uuid [pk]`, self-referencing `parent_id`, unique `slug` index, and foreign key index `idx_categories_parent_id`.
- [x] 2.2 Define `products` table with `specs jsonb`, GIN index `idx_products_specs_gin` (`jsonb_path_ops`), foreign key index `idx_products_category_id`, and check constraint notes (`base_price >= 0`, `cost_price >= 0`).
- [x] 2.3 Define `product_variants` table with FK index `idx_product_variants_product_id`, unique SKU index, and `"numeric(18, 2)"` price type.

## 3. Inventory Module Tables in DBML

- [x] 3.1 Define `inventory_stocks` table with FK indexes (`product_id`, `variant_id`), composite unique index `(product_id, variant_id)` with PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT`, check constraints (`quantity >= 0`, `reserved_quantity >= 0`, `reserved_quantity <= quantity`), and `FOR UPDATE` concurrency notes.
- [x] 3.2 Define `inventory_movements` table as an append-only audit ledger with FK indexes, signed `quantity_change <> 0`, and remaining balance `quantity_after >= 0`.
- [x] 3.3 Define `suppliers`, `purchase_orders`, and `purchase_order_items` tables with complete FK indexes, cost columns in `"numeric(18, 2)"`, composite FKs `(variant_id, product_id)`, and status enums (including `PartiallyReceived`).
- [x] 3.4 Define `serial_imeis` table with unique indexes on `serial_number` and `imei`, FK indexes on `product_id`, `variant_id`, `purchase_order_id`, composite FK `(variant_id, product_id)`, regex constraint on 15-digit IMEI, and partial index note `idx_serial_imeis_instock` (`WHERE status = 'InStock'`).

## 4. Orders Module Tables in DBML

- [x] 4.1 Define `orders` table with order status, payment method enums, monetary fields in `"numeric(18, 2)"`, and FK indexes on `customer_id`, `cashier_user_id`, `pos_session_id`, and `voucher_id`.
- [x] 4.2 Define `order_items` table with FK indexes on `order_id`, `product_id`, `variant_id`, `serial_imei_id`, and quantity check constraint notes.
- [x] 4.3 Define `order_returns` table with refund amount in `"numeric(18, 2)"` and FK indexes.

## 5. Customers & Sales Module Tables in DBML

- [x] 5.1 Define `customers`, `customer_loyalty_points`, and `vouchers` tables with tier enums, FK indexes, and partial index note `idx_vouchers_active` (`WHERE is_active = true`).
- [x] 5.2 Define `pos_sessions` and `vietqr_transactions` tables with shift register cash tracking, transaction codes, status enums, and FK indexes.

## 6. Identity Module Tables in DBML

- [x] 6.1 Define `users`, `roles`, and `user_roles` composite primary key join table with FK indexes.
- [x] 6.2 Define `refresh_tokens` table with FK index on `user_id`, SHA-256 `token_hash` unique index, and partial index note `idx_refresh_tokens_active` (`WHERE is_revoked = false`).

## 7. TableGroups, Relationships & Validation

- [x] 7.1 Define 6 color-coded `TableGroup` definitions (`Catalog`, `Inventory`, `Orders`, `Customers`, `Sales`, `Identity`) in `docs/database/schema.dbml`.
- [x] 7.2 Define explicit relationship lines (`Ref: ...`) for all entity associations across the schema.
- [x] 7.3 Validate complete DBML syntax in `docs/database/schema.dbml` against dbdiagram.io parser requirements and verify 100% adherence to Supabase Postgres best practices checklist (all FKs indexed, GIN on jsonb, timestamptz, numeric(18, 2), snake_case).
