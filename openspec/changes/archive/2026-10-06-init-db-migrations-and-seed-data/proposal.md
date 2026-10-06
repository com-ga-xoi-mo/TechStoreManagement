# Proposal

## Why

The TechStore application requires a live, synchronized PostgreSQL 16 database matching the 21-table architecture defined in `docs/database/schema.dbml` and `openspec/specs/database-schema/spec.md`. Currently, the backend contains empty module skeletons and lacks Entity Framework Core model entities, Fluent API configurations, migration scripts, and baseline seed data. Setting up automated EF Core migrations and an initial data seeder enables all developers on the team to immediately run `docker compose up -d` and `dotnet ef database update`, launching a fully operational database populated with realistic hardware catalog, inventory, customer, order, and administrative credentials (`admin` / `Admin@123`).

## What Changes

- **Entity & Fluent API Modeling**: Implement domain entities inheriting from `BaseEntity` and EF Core `IEntityTypeConfiguration<T>` configurations across all 6 modules (Catalog, Inventory, Orders, Customers, Sales, Identity) covering all 21 tables.
- **PostgreSQL 16 & Supabase Standards Enforcement**: Configure 100% foreign key B-tree indexing (`idx_<table>_<col>`), composite foreign keys `(variant_id, product_id)`, PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT` (`.AreNullsDistinct(false)`), GIN indexes with `jsonb_path_ops` on `specs`, partial indexes, and database check constraints.
- **EF Core Database Migrations**: Generate and execute the initial EF Core migration (`InitialCreate`) applying the 21 tables, constraints, and indexes cleanly to PostgreSQL 16.
- **Automated Baseline Data Seeding**: Create `DataSeeder` to deterministically populate initial test data:
  - Default RBAC roles (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`) and default Admin user (`admin` / `Admin@123`).
  - Catalog taxonomy (~2 categories: Laptops, Smartphones; ~5 products with JSONB specifications and variants).
  - Inventory baseline (initial suppliers, purchase orders, serial/IMEI records, inventory stocks, and movement ledger entries).
  - Customer profiles (~2 customers with phone numbers, loyalty points, VIP tiers) and promotional vouchers.
  - Retail sales history (~2 initial orders with line items, payments, and completed fulfillment).
- **Application Startup Integration**: Integrate database migration / seeder hook into `backend/Program.cs` for smooth local development bootstrapping.

## Capabilities

### New Capabilities
- `database-migrations`: EF Core 10 entity definitions, Fluent API mapping, migration lifecycle management (`dotnet ef migrations add`, `dotnet ef database update`), and synchronization of all 21 tables on PostgreSQL 16.
- `data-seeding`: Automated, idempotent baseline dataset seeder populating administrative accounts, catalog items, inventory balances, customers, and orders for rapid development and testing.

### Modified Capabilities
<!-- Existing capabilities whose REQUIREMENTS are changing (not just implementation). -->
*(None. The existing `database-schema` DBML specification remains authoritative and unchanged.)*

## Impact

- **Backend Architecture (`backend/`)**:
  - `Common/Data/AppDbContext.cs`: Registers DbSets and scans assembly configurations.
  - `Common/Data/Migrations/`: Hosts generated EF Core migration files.
  - `Catalog/`, `Inventory/`, `Orders/`, `Customers/`, `Sales/`, `Identity/`: Receives entity models and `Configurations/` classes.
  - `Program.cs`: Adds optional startup migration/seed execution in Development environment.
- **Shared Contracts (`shared/Enums/`)**: Provides shared enum definitions (`OrderStatus`, `PaymentMethod`, `SerialImeiStatus`, `InventoryMovementType`, `CustomerTier`, `VoucherType`, `PurchaseOrderStatus`, `PosSessionStatus`, `VietQrStatus`, `RoleType`) consumed by backend and tests.
- **Database (`techstore_db`)**: Creates 21 tables, associated indexes, foreign keys, and seed records in PostgreSQL 16.
- **Development Workflow**: Eliminates manual schema setup; developers can bootstrap a complete test environment in seconds.
