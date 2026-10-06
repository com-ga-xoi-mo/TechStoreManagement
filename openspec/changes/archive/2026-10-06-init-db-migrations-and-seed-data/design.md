# Design

## Context

The TechStore backend is an ASP.NET Core 10 Web API modular monolith (`backend/TechStore.Api.csproj`) backed by PostgreSQL 16 (`techstore_db`) running via Docker Compose. The database schema has been authoritatively specified across 21 tables in `docs/database/schema.dbml` and `openspec/specs/database-schema/spec.md`. Currently, the domain modules (`Catalog`, `Inventory`, `Orders`, `Customers`, `Sales`, `Identity`) contain only placeholder README files, and `AppDbContext` has no registered entities or migration history.

This design defines the technical architecture for:
1. Mapping all 21 tables into C# domain entities inheriting from `BaseEntity`.
2. Encapsulating table mappings within modular `IEntityTypeConfiguration<T>` classes adhering to Supabase Postgres best practices.
3. Managing migration lifecycles via EF Core 10 tools (`dotnet ef migrations add InitialCreate`, `dotnet ef database update`).
4. Providing an automated, idempotent `DataSeeder` service that populates realistic baseline development data upon application startup.

## Goals / Non-Goals

**Goals:**
- **Exact Schema Parity**: Guarantee that generated EF Core migration DDL matches the 21-table specification in `docs/database/schema.dbml` including foreign keys, check constraints, GIN JSONB indexes, and composite keys.
- **PostgreSQL 16 Advanced Constraints**: Correctly utilize PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT` (`.AreNullsDistinct(false)`) and partial indexes (`.HasFilter(...)`).
- **Domain Enum Unification**: Place all 10 domain enums into `TechStore.Shared/Enums/` for reuse across Backend, Client, and Unit Tests.
- **Idempotent Data Seeding**: Provide a resilient seeding pipeline that populates test data safely without failing on repeated runs.
- **Zero-Friction Dev Onboarding**: Allow any developer to run `docker compose up -d` and `dotnet run --project backend/TechStore.Api.csproj` to get a working API with seed data ready.

**Non-Goals:**
- Building business logic controllers or application service implementations for Catalog, Orders, Inventory, etc. (deferred to subsequent milestone tasks).
- Production multi-tenant migration pipelines or external migration orchestrators.
- Dynamic runtime schema alterations outside EF Core migrations.

## Decisions

### Decision 1: Shared Domain Enums in `TechStore.Shared/Enums/`
All 10 domain enums (`OrderStatus`, `PaymentMethod`, `SerialImeiStatus`, `InventoryMovementType`, `CustomerTier`, `VoucherType`, `PurchaseOrderStatus`, `PosSessionStatus`, `VietQrStatus`, `RoleType`) are defined as strongly typed C# enums in `shared/Enums/`.
- **EF Core Mapping**: Configured with `.HasConversion<string>()` across all entity properties.
- **Rationale**: Aligns with the explicit string-based enum identifiers in `schema.dbml` and prevents integer alignment errors when new enum values are added.
- **Alternative considered**: Mapping as numeric integers. *Rejected* because debugging and direct database reporting on Supabase/PostgreSQL is significantly clearer with readable string enum values.

### Decision 2: Modular Entity Configuration Pattern (`Configurations/`)
Rather than accumulating 21 entity configurations in a massive `OnModelCreating` method in `AppDbContext`, each domain module contains a `Configurations/` subfolder:
- `Catalog/Configurations/`: `CategoryConfiguration`, `ProductConfiguration`, `ProductVariantConfiguration`
- `Inventory/Configurations/`: `InventoryStockConfiguration`, `InventoryMovementConfiguration`, `SupplierConfiguration`, `PurchaseOrderConfiguration`, `PurchaseOrderItemConfiguration`, `SerialImeiConfiguration`
- `Orders/Configurations/`: `OrderConfiguration`, `OrderItemConfiguration`, `OrderReturnConfiguration`
- `Customers/Configurations/`: `CustomerConfiguration`, `CustomerLoyaltyPointConfiguration`, `VoucherConfiguration`
- `Sales/Configurations/`: `PosSessionConfiguration`, `VietQrTransactionConfiguration`
- `Identity/Configurations/`: `UserConfiguration`, `RoleConfiguration`, `UserRoleConfiguration`, `RefreshTokenConfiguration`
- **Assembly Scanning**: `AppDbContext.OnModelCreating` executes `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly)` to automatically register all configurations.
- **Rationale**: Preserves modular monolith boundaries and ensures high maintainability.

### Decision 3: PostgreSQL 16 & Supabase Standards Implementation
1. **100% Foreign Key Indexing**: Every single foreign key property has an explicit `builder.HasIndex(x => x.<FkProperty>).HasDatabaseName("idx_<table>_<col>")`.
2. **Composite Foreign Keys**:
   - `ProductVariant` declares a unique composite key `builder.HasIndex(x => new { x.Id, x.ProductId }).IsUnique().HasDatabaseName("uq_product_variants_id_product")`.
   - Dependent tables (`InventoryStock`, `PurchaseOrderItem`, `SerialImei`, `OrderItem`) configure composite FKs:
     `builder.HasOne<ProductVariant>().WithMany().HasForeignKey(x => new { x.VariantId, x.ProductId }).HasPrincipalKey(x => new { x.Id, x.ProductId }).OnDelete(DeleteBehavior.Restrict)`.
3. **PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT`**:
   - `inventory_stocks(product_id, variant_id)`:
     `builder.HasIndex(x => new { x.ProductId, x.VariantId }).IsUnique().AreNullsDistinct(false).HasDatabaseName("idx_inventory_stocks_product_variant")`.
   - `purchase_order_items(purchase_order_id, product_id, variant_id)`:
     `builder.HasIndex(x => new { x.PurchaseOrderId, x.ProductId, x.VariantId }).IsUnique().AreNullsDistinct(false).HasDatabaseName("uq_po_items_order_product_variant")`.
4. **JSONB GIN Indexing**:
   - For `specs` in `products` and `product_variants`:
     `builder.Property(x => x.Specs).HasColumnType("jsonb").HasDefaultValue("{}")`.
     `builder.HasIndex(x => x.Specs).HasMethod("gin").HasOperators("jsonb_path_ops").HasDatabaseName("idx_<table>_specs_gin")`.
5. **Database Check Constraints**:
   - Enforced via `builder.ToTable(t => { t.HasCheckConstraint("ck_name", "sql_expression"); })`.
6. **Partial Indexes**:
   - `serial_imeis`: `builder.HasIndex(x => new { x.ProductId, x.Status }).HasFilter("status = 'InStock'").HasDatabaseName("idx_serial_imeis_instock")`.
   - `vouchers`: `builder.HasIndex(x => new { x.IsActive, x.ExpiresAt }).HasFilter("is_active = true").HasDatabaseName("idx_vouchers_active")`.
   - `refresh_tokens`: `builder.HasIndex(x => new { x.UserId, x.TokenHash }).HasFilter("is_revoked = false").HasDatabaseName("idx_refresh_tokens_active")`.

### Decision 4: Deterministic, Idempotent `DataSeeder` Service
- Located at `backend/Common/Data/DataSeeder.cs` with interface `IDataSeeder`.
- Password hashing for default Admin (`admin` / `Admin@123`) using PBKDF2 with SHA-256 (via standard ASP.NET Core `IPasswordHasher<User>` or HMACSHA256).
- **Execution Order**:
  1. Roles (`Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`)
  2. Users (Admin user `admin`, email `admin@techstore.vn`) & UserRoles
  3. Categories (Laptops `laptops`, Smartphones `smartphones`)
  4. Products (~5 products with JSONB specs: iPhone 15 Pro Max, MacBook Pro 14 M3, Samsung Galaxy S24 Ultra, Dell XPS 15, Sony WH-1000XM5) & Variants
  5. Suppliers (e.g. FPT Synnex) & Purchase Orders
  6. Inventory Stocks, Movement Ledger, and Serial/IMEIs with valid 15-digit IMEIs
  7. Customers (~2 profiles: Nguyen Van A, Tran Thi B) & Vouchers (`TECHSTORE100K`)
  8. POS Sessions & Orders (~2 completed orders with items and payment transactions)
- **Idempotency**: Seeder inspects existing records (e.g., checks if roles or admin user exist before inserting), safely skipping already-populated stages.

### Decision 5: Development Startup Migration Hook
In `backend/Program.cs`:
- In Development mode (`app.Environment.IsDevelopment()`), execute migration and seed logic:
  ```csharp
  using (var scope = app.Services.CreateScope())
  {
      var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      await dbContext.Database.MigrateAsync();
      var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
      await seeder.SeedAsync();
  }
  ```
- **Rationale**: Ensures any developer or CI runner starting the application has an up-to-date database without requiring separate manual CLI commands.

## Risks / Trade-offs

- **[Risk] Migration failure due to Docker PostgreSQL not running**:
  - *Mitigation*: The startup migration hook is wrapped in try-catch with informative logging instructing the developer to run `docker compose up -d`.
- **[Risk] Composite Foreign Key nullability constraints**:
  - *Mitigation*: PostgreSQL allows composite foreign keys with NULL components if configured properly. Ensure `variant_id` nullability aligns with parent `product_variants` key.
- **[Risk] JSONB dynamic property mapping**:
  - *Mitigation*: Store `Specs` as a `string` property mapped to PostgreSQL `jsonb` column with default value `'{}'::jsonb`. This permits arbitrary flat JSON dictionaries while fully supporting PostgreSQL GIN `@>` operator queries.
- **[Risk] Password security of seeded Admin account**:
  - *Mitigation*: Hardcoded initial credentials (`admin` / `Admin@123`) are explicitly restricted to development seeding and must be accompanied by documentation to change credentials in staging/production.
