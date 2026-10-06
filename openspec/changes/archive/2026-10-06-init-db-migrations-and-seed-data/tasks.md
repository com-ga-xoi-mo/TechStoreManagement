# Tasks

## 1. Domain Enums and Common Infrastructure Setup

- [x] 1.1 Implement all 10 domain enums (`OrderStatus`, `PaymentMethod`, `SerialImeiStatus`, `InventoryMovementType`, `CustomerTier`, `VoucherType`, `PurchaseOrderStatus`, `PosSessionStatus`, `VietQrStatus`, `RoleType`) in `shared/Enums/` and verify compilation via `dotnet build shared/TechStore.Shared.csproj`.
- [x] 1.2 Update `backend/Common/Data/AppDbContext.cs` to register `DbSet<T>` properties for all 21 domain entities and verify project compilation via `dotnet build backend/TechStore.Api.csproj`.

## 2. Catalog & Identity Entities and Configurations

- [x] 2.1 Implement `Category`, `Product`, and `ProductVariant` entities in `backend/Catalog/Entities/` and their Fluent API configurations in `backend/Catalog/Configurations/` enforcing GIN `jsonb_path_ops` on `specs`, composite unique key `(Id, ProductId)`, and foreign key indexing, verified by unit tests in `tests/Backend.UnitTests/CatalogEntityTests.cs`.
- [x] 2.2 Implement `User`, `Role`, `UserRole`, and `RefreshToken` entities in `backend/Identity/Entities/` and their configurations in `backend/Identity/Configurations/` enforcing SHA-256 token hashing and partial index `idx_refresh_tokens_active` (`WHERE is_revoked = false`), verified by unit tests in `tests/Backend.UnitTests/IdentityEntityTests.cs`.

## 3. Inventory Entities and Configurations

- [x] 3.1 Implement `Supplier`, `PurchaseOrder`, and `PurchaseOrderItem` entities and configurations in `backend/Inventory/` enforcing composite FK `(VariantId, ProductId)`, PostgreSQL 16 `AreNullsDistinct(false)` unique line constraint, and non-negative check constraints, verified by unit tests in `tests/Backend.UnitTests/PurchaseOrderEntityTests.cs`.
- [x] 3.2 Implement `InventoryStock`, `InventoryMovement`, and `SerialImei` entities and configurations in `backend/Inventory/` enforcing PostgreSQL 16 `AreNullsDistinct(false)` balance uniqueness, 15-digit IMEI check constraint regex (`^[0-9]{15}$`), anti-overselling bounds (`reserved_quantity <= quantity`), and partial index `idx_serial_imeis_instock`, verified by unit tests in `tests/Backend.UnitTests/InventoryEntityTests.cs`.

## 4. Orders, Customers, and Sales Entities and Configurations

- [x] 4.1 Implement `Customer`, `CustomerLoyaltyPoint`, and `Voucher` entities and configurations in `backend/Customers/` enforcing phone uniqueness, positive loyalty balances, voucher check constraints, and partial index `idx_vouchers_active` (`WHERE is_active = true`), verified by unit tests in `tests/Backend.UnitTests/CustomerEntityTests.cs`.
- [x] 4.2 Implement `Order`, `OrderItem`, and `OrderReturn` entities and configurations in `backend/Orders/` enforcing composite FK to `ProductVariant`, historical price snapshots, monetary check constraints, and delete behaviors, verified by unit tests in `tests/Backend.UnitTests/OrderEntityTests.cs`.
- [x] 4.3 Implement `PosSession` and `VietQrTransaction` entities and configurations in `backend/Sales/` enforcing check constraints, shift balance invariants, and partial unique indexes for active cashier sessions and confirmed payments, verified by unit tests in `tests/Backend.UnitTests/SalesEntityTests.cs`.

## 5. EF Core Migrations Generation and Database Synchronization

- [x] 5.1 Generate initial EF Core migration `InitialCreate` in `backend/Common/Data/Migrations/` via `dotnet ef migrations add InitialCreate --project backend/TechStore.Api.csproj` and verify that the generated migration code defines all 21 tables, foreign keys, indexes, and check constraints.
- [x] 5.2 Start PostgreSQL 16 container via `docker compose up -d` and execute `dotnet ef database update --project backend/TechStore.Api.csproj`, verifying that all 21 tables are created in PostgreSQL 16 with matching constraints and indexes via database inspection.

## 6. Baseline Data Seeder Implementation and Application Bootstrap

- [x] 6.1 Implement `DataSeeder` in `backend/Common/Data/DataSeeder.cs` populating roles (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`), default Admin account (`admin` / `Admin@123`), ~2 categories, ~5 products with JSONB specs, suppliers, stock balance rows, serial/IMEIs, ~2 customers, and ~2 completed sales orders with idempotent execution checks, verified by seeder unit tests.
- [x] 6.2 Integrate database migration and `DataSeeder` invocation into `backend/Program.cs` in Development mode and verify complete end-to-end bootstrapping by executing `dotnet run --project backend/TechStore.Api.csproj` and asserting seed records exist in the database.
