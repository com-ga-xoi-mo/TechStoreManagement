# Repository Guidelines

## Project Overview
TechStore Management System is an enterprise retail POS (Point of Sale) and inventory management platform tailored for high-value technology hardware and accessories (smartphones, laptops, components). It is architected to handle:
- In-store POS barcode/IMEI scanning, checkout, and dynamic VietQR banking transfer generation.
- Serial/IMEI lifecycle tracking (`InStock`, `Reserved`, `Sold`, `Returned`, `UnderRepair`, `Defective`; warranty is derived from warranty dates) with warranty activation upon order settlement.
- Schema-less device specifications (CPU, RAM, GPU, Battery) persisted via PostgreSQL `JSONB` columns without schema migrations.
- Strict concurrency control preventing overselling via database row locks (`FOR UPDATE`) in ACID transactions.
- Customer VIP tier points accrual, discount vouchers, and sales/profit analytics dashboards.
- Server-enforced Role-Based Access Control (RBAC) across 4 roles: `Admin`, `Manager`, `SalesStaff`, `WarehouseStaff`.
- Grounded Gemini AI assistant leveraging tool/function calling strictly constrained by caller RBAC and real-time inventory.

---

## Architecture & Data Flow

### 1. High-Level Architecture (3-Tier API-First)
```
┌──────────────────────────────────────────────────────────────┐
│              CLIENT TIER: Uno Platform (WinUI 3)             │
│   • Targets: Windows 10/11 (WinAppSDK) & macOS/Linux (Skia)   │
│   • Pattern: Feature-First MVVM (CommunityToolkit.Mvvm)      │
│   • Communication: RESTful HTTPS + JWT Bearer                │
└──────────────────────────────┬───────────────────────────────┘
                               │ HTTP/HTTPS (REST JSON)
                               ▼
┌──────────────────────────────────────────────────────────────┐
│             APPLICATION TIER: ASP.NET Core 10 Web API        │
│   • Pattern: Modular Monolith (Cohesive Domain Modules)      │
│   • Security: JWT Authentication & RBAC Policy Enforcement   │
│   • Concurrency: Row-level locks ('FOR UPDATE') on stock     │
│   • AI Integration: Google Gemini Tool/Function Calling      │
└──────────────────────────────┬───────────────────────────────┘
                               │ EF Core 10 (Npgsql)
                               ▼
┌──────────────────────────────────────────────────────────────┐
│                  DATA TIER: PostgreSQL 16                    │
│   • JSONB dynamic specifications & relational ACID storage   │
└──────────────────────────────────────────────────────────────┘
```

> **Cardinal Rule:** The frontend client **never** connects directly to the database. All interactions must pass through ASP.NET Core REST endpoints secured by JWT Bearer tokens and role policies.

### 2. Key Modules
- **`backend/Catalog/`**: Product categories, variants, and dynamic hardware specifications stored in JSONB columns (`IProductService`, `ICategoryService`, `IProductRepository`, `ICategoryRepository`, `ProductsController`, `CategoriesController`).
- **`backend/Inventory/`**: Stock counts, append-only movement audit ledger (`inventory_movements`), supplier purchases, unique Serial/IMEI lifecycle states (`InStock`, `Reserved`, `Sold`, `Returned`, `UnderRepair`, `Defective`), composite foreign keys, and pessimistic row-locking (`IInventoryService`, `IInventoryStockRepository`, `ISerialImeiRepository`, `IPurchaseOrderRepository`, `ISupplierRepository`, `InventoryController`).
- **`backend/Orders/`**: Order processing, sold IMEI linkage, order status lifecycle, returns, and refunds (`IOrderService`, `IOrderRepository`, `OrdersController`).
- **`backend/Customers/`**: Customer profiles, loyalty point accrual, discount vouchers, and revenue analytics (`ICustomerService`, `AnalyticsService`, `ICustomerRepository`, `IVoucherRepository`, `CustomersController`).
- **`backend/Sales/`**: POS counter register, cashier cart calculation, and dynamic VietQR generation (`IPosService`, `VietQrService`, `IPosSessionRepository`, `IVietQrTransactionRepository`, `SalesController`).
- **`backend/Identity/`**: User credentials, refresh tokens, RBAC authorization, initial database seeding (`DataSeeder`), and Gemini AI service (`IAuthService`, `GeminiAiAgentService`, `IUserRepository`, `IRefreshTokenRepository`, `AuthController`, `AiController`).
- **`backend/Common/`**: Core infrastructure including `AppDbContext`, `IUnitOfWork` / `UnitOfWork` (saves and transactions), `DuplicateKeyException`, `BaseEntity`, and HTTP exception middleware.
- **Data access rule**: in every module, services reach the database only through the module's repositories (`Repositories/`) and `IUnitOfWork`; see *Layering & Repository Pattern* below.
- **`shared/`**: Common domain contracts consumed by backend, client, and tests (`DTOs/`, `Requests/`, `Enums/`).

### 3. Data Flow
1. **User Interaction**: UI triggers an `{x:Bind}` event command on a ViewModel in `frontend/TechStore.Client/Features/<Feature>/`.
2. **ViewModel Execution**: The ViewModel executes a command method decorated with `[RelayCommand]`, updates observable properties, and invokes an API client service in `frontend/TechStore.Client/Services/Api/`.
3. **HTTP Transport**: Request is sent via `HttpClient` over HTTPS with an `Authorization: Bearer <token>` header to the ASP.NET Core API (`http://localhost:5176` or `https://localhost:7207`).
4. **API Routing & RBAC**: Request reaches an `[ApiController]`, passes through JWT authentication and role authorization checks, then calls the corresponding domain service interface (controllers never call repositories).
5. **Business Logic & Concurrency**: The service validates input and applies business rules. When the use case locks rows or writes more than once, it opens a transaction through `IUnitOfWork.BeginTransactionAsync`.
6. **Data Access**: The service calls repository interfaces (`backend/<Module>/Repositories/`). Repositories are the only classes that use `AppDbContext`: they query, acquire row locks (`SELECT ... FOR UPDATE`) and stage changes, but never save.
7. **Data Persistence**: The service calls `IUnitOfWork.SaveChangesAsync` (and `CommitAsync` when a transaction is open), which writes the staged changes to PostgreSQL 16.
8. **Response**: The service maps entities to a shared DTO (from `shared/DTOs/`), which the controller returns to the client to be bound to UI components.

---

## Key Directories

```
SalesManagement/
├── backend/                       # ASP.NET Core 10 Web API modular monolith
│   │                              # each module: Entities/, Configurations/, Repositories/, Services/, *Controller.cs
│   ├── Catalog/                   # Products, categories, JSONB specs
│   ├── Common/                    # BaseEntity, Middlewares
│   │   └── Data/                  # AppDbContext, IUnitOfWork/UnitOfWork, DuplicateKeyException, Migrations, DataSeeder
│   ├── Customers/                 # Customers, loyalty points, analytics
│   ├── Identity/                  # RBAC, JWT, Gemini AI integration
│   ├── Inventory/                 # Stock, Serial/IMEI, supplier purchase orders
│   ├── Orders/                    # Order processing, items, returns
│   ├── Properties/                # launchSettings.json (ports 5176/7207)
│   └── Sales/                     # POS counter sales, VietQR payments
│
├── frontend/                      # Client desktop application
│   ├── Directory.Build.props      # CPM enablement and compiler warning suppressions
│   ├── Directory.Packages.props   # Central Package Management package definitions
│   └── TechStore.Client/          # Uno Platform 6.7 / WinUI 3 project
│       ├── App.xaml / App.xaml.cs # DI bootstrap and Uno application lifecycle
│       ├── Features/              # Feature-First MVVM (Pages, Dialogs, ViewModels)
│       ├── Controls/              # Reusable UI controls (StatCard, ProductCard)
│       ├── Converters/            # XAML value converters (Currency, StatusToColor)
│       ├── Platforms/             # Desktop (Skia) and Windows entry points
│       └── Services/              # Client services (API clients, Navigation, Dialogs)
│
├── shared/                        # Shared contracts across API, Client, and Tests
│   ├── DTOs/                      # Data Transfer Objects (e.g., ProductDto, OrderDto)
│   ├── Enums/                     # Shared enums (OrderStatus, RoleType, PaymentMethod)
│   └── Requests/                  # API request models (CreateOrderRequest, LoginRequest)
│
├── tests/                         # Automated test suites
│   └── Backend.UnitTests/         # xUnit unit tests (FluentAssertions, Coverlet)
│
└── docs/                          # Architecture, roadmap, tech reports, requirements
```

---

## Development Commands

### 1. Prerequisites & Services
```bash
# Start PostgreSQL 16 database in detached mode
docker compose up -d

# Verify database health check status
docker compose ps

# View database container logs
docker compose logs -f postgres

# Stop database container
docker compose down
```

### 2. Build & Restore
```bash
# Restore dependencies across solution
dotnet restore TechStore.sln

# Build entire solution (.NET 10)
dotnet build TechStore.sln

# Build backend API only
dotnet build backend/TechStore.Api.csproj

# Build frontend desktop target (cross-platform macOS/Linux/Windows Skia)
dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop

# Build frontend Windows native target (Windows only)
dotnet build frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-windows10.0.26100
```

### 3. Run Applications
```bash
# Run backend Web API (Swagger at http://localhost:5176)
dotnet run --project backend/TechStore.Api.csproj --launch-profile http

# Run frontend desktop client (Skia Desktop)
dotnet run --project frontend/TechStore.Client/TechStore.Client.csproj -f net10.0-desktop
```

### 4. Database Migrations (EF Core)
```bash
# Add a new migration (specify migration name)
dotnet ef migrations add <MigrationName> \
  --project backend/TechStore.Api.csproj \
  --output-dir Common/Data/Migrations

# Apply pending migrations to PostgreSQL database
dotnet ef database update --project backend/TechStore.Api.csproj

# Remove the most recent unapplied migration
dotnet ef migrations remove --project backend/TechStore.Api.csproj
```

### 5. Testing
```bash
# Run all tests in the solution
dotnet test TechStore.sln

# Run backend unit tests specifically
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj

# Run tests with detailed test names output
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --logger "console;verbosity=normal"

# Run tests with code coverage collection
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --collect:"XPlat Code Coverage"

# Run a specific test by fully qualified name or pattern
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~BaseEntityTests"
```

---

## Code Conventions & Common Patterns

### 1. Language & Formatting
- **Target Language:** C# 12 / C# 13 on .NET 10.
- **Nullability & Usings:** `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` are enforced across all projects. Use file-scoped namespaces (`namespace TechStore.Api.Catalog;`).
- **Constructors:** Prefer primary constructors or clean constructor dependency injection. Use target-typed `new()` where the type is obvious.

### 2. Naming Conventions
| Element | Convention | Example |
| :--- | :--- | :--- |
| Classes / Structs / Records | PascalCase | `ProductVariant`, `OrderService`, `AppDbContext` |
| Interfaces | `I` + PascalCase | `IProductService`, `IInventoryService`, `INavigationService` |
| Methods | PascalCase + `Async` suffix if asynchronous | `GetByIdAsync`, `StockInAsync`, `ProcessPaymentAsync` |
| Repositories | `I<Aggregate>Repository` / `<Aggregate>Repository` in `Repositories/` | `IProductRepository`, `InventoryStockRepository` |
| Private Readonly Fields | `_` + camelCase | `_productRepository`, `_unitOfWork`, `_logger` (`_dbContext` only inside repositories / `UnitOfWork`) |
| DTOs | PascalCase + `Dto` suffix | `ProductDto`, `OrderItemDto`, `CustomerDto` |
| Requests | PascalCase + `Request` suffix | `CreateProductRequest`, `StockInRequest` |
| Enums & Members | PascalCase (Singular name) | `OrderStatus.Pending`, `RoleType.SalesStaff` |
| Unit Test Methods | `UnitOfWork_StateUnderTest_ExpectedBehavior` | `BaseEntity_Should_Initialize_With_Valid_Guid_And_CreatedAt` |

### 3. Entity Pattern & Database Configuration
- All domain entities MUST inherit from `BaseEntity` (`backend/Common/Entities/BaseEntity.cs`):
  ```csharp
  public abstract class BaseEntity
  {
      public Guid Id { get; set; } = Guid.NewGuid();
      public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
      public DateTime? UpdatedAt { get; set; }
  }
  ```
- Database configurations must use EF Core **Fluent API** classes implementing `IEntityTypeConfiguration<T>` placed inside a `Configurations/` subfolder within each module. `AppDbContext` auto-scans them via `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);`.
- Avoid data annotations for schema configuration (`[Table]`, `[Column]`); reserve annotations strictly for input validation attributes (`[Required]`, `[StringLength]`).
- **PostgreSQL 16 & Supabase Best Practices (Strict Enforcement via `docs/database/schema.dbml`):**
  - **100% Foreign Key Indexing:** Every foreign key column across all 21 tables MUST have an explicit B-tree index to eliminate sequential scans and prevent table locks during cascade validations (`idx_<table>_<col>`).
  - **Composite Foreign Keys:** Enforce `(variant_id, product_id)` referencing `product_variants(id, product_id)` on `inventory_stocks`, `purchase_order_items`, `serial_imeis`, and `order_items`. `product_variants` defines unique index `uq_product_variants_id_product` on `(id, product_id)`.
  - **PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT`:** Enforce single balance row on `inventory_stocks(product_id, variant_id)` and single line per PO on `purchase_order_items(purchase_order_id, product_id, variant_id)` using `.AreNullsDistinct(false)`.
  - **JSONB Hardware Specs Indexing:** GIN index configured with `jsonb_path_ops` on `specs` column in `products` and `product_variants` for fast containment queries (`@>`). Flat JSON contract with snake_case keys; effective specs = `product.specs || variant.specs`.
  - **Strict Types:** All temporal columns use UTC `timestamptz`; all monetary amounts use `numeric(18, 2)`. Identifiers use lowercase `snake_case`.
  - **Partial Indexes:** High-selectivity operational indexes:
    - `serial_imeis`: `(product_id, status)` WHERE `status = 'InStock'` for POS scanning.
    - `vouchers`: `(is_active, expires_at)` WHERE `is_active = true` (filter `expires_at > now()` at query time as `now()` is not immutable).
    - `refresh_tokens`: `(user_id, token_hash)` WHERE `is_revoked = false` (`token_hash` stores SHA-256 hash).
  - **Anti-Overselling & Balance Invariants:** Database check constraints `quantity >= 0`, `reserved_quantity >= 0`, `reserved_quantity <= quantity`. Physical stock changes audited via append-only `inventory_movements` ledger (`quantity_change <> 0`, `quantity_after >= 0`). For serial-tracked products (`is_serial_tracked = true`), `quantity` must equal `COUNT(serial_imeis WHERE status IN ('InStock', 'Reserved'))`.

### 4. Concurrency & Anti-Overselling Pattern
- Inventory operations affecting stock quantities or assigning Serial/IMEIs must run inside an explicit ACID transaction with row-level locks. The **service** opens and closes the transaction through `IUnitOfWork`; the lock itself is a **repository** method (see section 9):
  ```csharp
  await _unitOfWork.BeginTransactionAsync(cancellationToken);
  try
  {
      var stock = await _stockRepository.GetForUpdateAsync(productId, variantId, cancellationToken); // SELECT ... FOR UPDATE
      // business rules in the service: check availability, change quantities, stage movement via repository
      await _unitOfWork.SaveChangesAsync(cancellationToken);
      await _unitOfWork.CommitAsync(cancellationToken);
  }
  catch
  {
      await _unitOfWork.RollbackAsync(cancellationToken);
      throw;
  }
  ```
- Lock methods (`SELECT ... FOR UPDATE`) are only valid inside a transaction opened by the calling service; outside one the lock is released immediately.
- The same precondition applies to repository methods using EF Core bulk operations (`ExecuteUpdateAsync`, `ExecuteDeleteAsync`) or raw write SQL: they run SQL immediately, bypassing the `ChangeTracker` and `SaveChanges`. Name or document them as direct writes (e.g. `MarkSoldAsync` — "executes immediately; call inside an `IUnitOfWork` transaction").

### 5. Asynchronous Programming Pattern
- Use pure non-blocking `async`/`await` end-to-end.
- Always propagate `CancellationToken cancellationToken = default` down to EF Core calls (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`).
- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` (causes thread pool starvation and deadlocks).
- Never use `async void`, except for top-level WinUI XAML event handlers where the signature is dictated by framework delegates.

### 6. Dependency Injection Pattern
- Backend services are registered in `backend/Program.cs` or module service collection extensions:
  - **Scoped**: Domain services (`IProductService`), repositories (`IProductRepository`), `IUnitOfWork`, and DbContext (`AppDbContext`) — all share one `AppDbContext` per request, so `IUnitOfWork.SaveChangesAsync` persists what the repositories staged.
  - **Singleton**: Stateless utility services, token issuers (`JwtService`), configuration wrappers.
  - **Transient**: Lightweight, state-free handlers or short-lived operations.
- Frontend services are registered in `frontend/TechStore.Client/App.xaml.cs`:
  - API client services (`ProductApiClient`, `OrderApiClient`) registered as Scoped or Transient.
  - ViewModels registered in DI container and resolved per feature view.

### 7. Frontend MVVM & State Management Pattern
- Uses **Feature-First MVVM** powered by `CommunityToolkit.Mvvm`:
  - Co-locate View (`.xaml`), Code-Behind (`.xaml.cs`), and ViewModel (`.cs`) inside `frontend/TechStore.Client/Features/<FeatureName>/`.
  - Use `[ObservableProperty]` on backing fields to generate bindable properties with change notification.
  - Use `[RelayCommand]` on asynchronous methods to generate UI-bindable commands.
  - Keep `.xaml.cs` minimal: call only `this.InitializeComponent();`. Zero business logic in code-behind.
  - Bind View to ViewModel using `{x:Bind ViewModel.Property, Mode=OneWay}`.

### 8. Error Handling & API Responses
- Backend employs centralized exception handling middleware to catch unhandled exceptions and return structured RFC 7807 `ProblemDetails` or standard JSON error payloads.
- Use explicit HTTP status codes:
  - `200 OK` / `201 Created`: Successful query or creation.
  - `400 Bad Request`: Validation failure or business constraint violation.
  - `401 Unauthorized`: Missing or expired JWT token.
  - `403 Forbidden`: Authenticated user lacks required `RoleType`.
  - `404 Not Found`: Entity with specified ID does not exist.
  - `409 Conflict`: Concurrency conflict or inventory oversell collision.
  - `500 Internal Server Error`: Unhandled server exception.

### 9. Layering & Repository Pattern
- **Layer rule:** `Controller → Service → Repository → AppDbContext`.
  - Controllers call services only.
  - Services hold business rules, validation, orchestration and entity → DTO mapping. They depend only on repository interfaces and `IUnitOfWork`; they never inject `AppDbContext` or reference EF Core/Npgsql types.
  - Repositories are the only classes that use `AppDbContext` (LINQ, `FromSql`, `EF.Functions`, `Include`, `AsNoTracking`).
- **Repositories** live in `backend/<Module>/Repositories/` as `I<Aggregate>Repository` / `<Aggregate>Repository`, one per aggregate root (e.g. `IProductRepository` covers `Product` and its `ProductVariant`s).
  - Methods express intent (`SearchAsync(criteria)`, `GetWithVariantsAsync(id)`, `FindExistingSkusAsync(skus)`, `Add(product)`, `GetForUpdateAsync(productId, variantId)`), not generic CRUD. No generic `IRepository<T>`.
  - They return entities, entity lists, or `(IReadOnlyList<TEntity> Items, int TotalCount)` for paged queries — never DTOs and never `IQueryable`. Read-only methods use `AsNoTracking`; methods whose results the service will modify return tracked entities.
  - Query inputs are module-internal criteria records with already-parsed values (e.g. `ProductSearchCriteria`), not `shared/Requests` contracts.
  - Repositories stage changes (`Add`, `Remove`, edits on tracked entities) and **never** call `SaveChanges`, `BeginTransaction`, `Commit` or `Rollback`.
- **Unit of Work:** `IUnitOfWork` / `UnitOfWork` in `backend/Common/Data/` wraps the request-scoped `AppDbContext`. It does not create its own transactions; it exposes EF Core's:
  - `Task<int> SaveChangesAsync(CancellationToken)` — writes all staged changes; translates PostgreSQL unique violations (`23505`) into `DuplicateKeyException` (with `ConstraintName`), which services map to their conflict result (e.g. `409`).
  - `Task BeginTransactionAsync(CancellationToken)` — throws if a transaction is already open (no nesting).
  - `Task CommitAsync(CancellationToken)` / `Task RollbackAsync(CancellationToken)` — rollback also clears the `ChangeTracker`.
- **The service owns the transaction boundary.** A use case that writes once (e.g. create product) only calls `SaveChangesAsync` (EF's implicit transaction is atomic). A use case that locks rows or saves more than once uses `BeginTransactionAsync` → repository calls → `SaveChangesAsync` → `CommitAsync`, and `RollbackAsync` on business failure or exception. Keep transactions short; never call external services (Gemini, VietQR) inside one.
- **Cross-module access:** a service may call another module's repository interface for **reads** (e.g. Catalog checks a category through `ICategoryRepository`). Writes to another module's tables go through that module's service. Table ownership follows the TableGroups in `docs/database/schema.dbml`.

---

## Important Files

### 1. Entry Points
- `backend/Program.cs`: ASP.NET Core application bootstrapper (DI container, EF Core Npgsql, Swagger, JWT middleware, routing).
- `frontend/TechStore.Client/Platforms/Desktop/Program.cs`: Uno Skia Desktop client bootstrapper for macOS and Linux.
- `frontend/TechStore.Client/App.xaml.cs`: WinUI/Uno application entry point, window management, frame navigation, and service provider setup.

### 2. Configuration & Orchestration
- `TechStore.sln`: Main Visual Studio solution linking API, Client, Shared, and UnitTests projects.
- `global.json`: SDK rules (`"allowPrerelease": false`) and Uno Platform SDK version (`"Uno.Sdk": "6.7.30"`).
- `docker-compose.yml`: PostgreSQL 16 container definition (`techstore_db`, port 5432, healthcheck).
- `backend/appsettings.Development.json`: Local development database connection string and logging levels.
- `backend/Properties/launchSettings.json`: Profiles for backend execution (HTTP: 5176, HTTPS: 7207).
- `frontend/Directory.Build.props`: Enables CPM (`<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`) and suppresses warning codes (`NU1507`, `NETSDK1201`, `PRI257`).
- `frontend/Directory.Packages.props`: Root package version specifications for frontend dependencies.

### 3. Core Domain & Architecture
- `backend/Common/Data/AppDbContext.cs`: Primary EF Core database context configuring Fluent API assembly scanning.
- `backend/Common/Entities/BaseEntity.cs`: Universal entity base class defining `Id` (Guid), `CreatedAt`, and `UpdatedAt`.
- `backend/TechStore.Api.http`: Scratchpad file for manual REST API testing in VS Code / Visual Studio.
- `docs/ARCHITECTURE.md`: Definitive architectural specification (3-Tier, Modular Monolith, Feature-First MVVM).

---

## Runtime & Tooling Preferences

- **SDK Requirement:** **.NET 10.0 SDK** is strictly required across all projects.
- **Frontend UI Framework:** **Uno Platform 6.7** (`Uno.Sdk` 6.7.30) multi-targeted to `net10.0-desktop` (Skia rendering for macOS/Linux) and `net10.0-windows10.0.26100` (WinAppSDK/WinUI 3 for Windows).
- **Package Management:**
  - `frontend/` enforces **Central Package Management (CPM)** via `Directory.Build.props` and `Directory.Packages.props` with `Uno.Sdk` implicit packages.
  - `backend/`, `shared/`, and `tests/` maintain explicit package references in their respective `.csproj` files.
- **Database Engine:** PostgreSQL 16 (`postgres:16-alpine`), operated via Docker Compose.
- **API Tooling:** Swashbuckle OpenAPI / Swagger UI mapped to the root URL (`http://localhost:5176/`) in development mode.

---

## Testing & QA

### 1. Frameworks & Libraries
- **Test Runner:** `xUnit` (`2.9.3`) with `Microsoft.NET.Test.Sdk` (`18.10.1`).
- **Assertion Library:** `FluentAssertions` (`8.11.0`) for human-readable, strongly-typed assertion chaining.
- **Coverage Tool:** `coverlet.collector` (`10.1.0`) for automated code coverage generation.
- **Integration Testing Host:** `Microsoft.AspNetCore.Mvc.Testing` (`WebApplicationFactory`) planned for end-to-end API and RBAC verification (`tests/Backend.IntegrationTests/`).

### 2. Testing Conventions
- Structure tests using the **Arrange-Act-Assert (AAA)** pattern with clear comments indicating each phase.
- Abstract entities must be tested via lightweight test doubles (e.g. `SampleEntity : BaseEntity`).
- Backend services are unit-tested without a database by injecting hand-written fake repositories and a fake `IUnitOfWork` (e.g. a fake whose `SaveChangesAsync` throws `DuplicateKeyException` to exercise the `409` path). No mocking library is used. Repository behavior that depends on PostgreSQL (jsonb containment, `FOR UPDATE`, constraints) is verified manually or by integration tests.
- ViewModels in `frontend/TechStore.Client/Features/` must remain completely decoupled from WinUI controls to enable headless unit testing in standard test runners without requiring a running UI thread.
- Test files must mirror the target project's namespace and folder structure under `tests/`.
- Ensure tests are idempotent and do not depend on execution order or persisted test state.
