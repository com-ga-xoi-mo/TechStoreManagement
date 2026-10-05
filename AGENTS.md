# Repository Guidelines

## Project Overview
TechStore Management System is an enterprise retail POS (Point of Sale) and inventory management platform tailored for high-value technology hardware and accessories (smartphones, laptops, components). It is architected to handle:
- In-store POS barcode/IMEI scanning, checkout, and dynamic VietQR banking transfer generation.
- Serial/IMEI lifecycle tracking (`InStock`, `Sold`, `UnderRepair`, `Defective`; warranty is derived from warranty dates) with warranty activation upon order settlement.
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
- **`backend/Catalog/`**: Product categories, variants, and dynamic hardware specifications stored in JSONB columns (`ICatalogService`, `ProductsController`).
- **`backend/Inventory/`**: Stock counts, supplier purchases, unique Serial/IMEI lifecycle states, and pessimistic row-locking (`IInventoryService`, `InventoryController`).
- **`backend/Orders/`**: Order processing, sold IMEI linkage, order status lifecycle, returns, and refunds (`IOrderService`, `OrdersController`).
- **`backend/Customers/`**: Customer profiles, loyalty point accrual, discount vouchers, and revenue analytics (`ICustomerService`, `AnalyticsService`, `CustomersController`).
- **`backend/Sales/`**: POS counter register, cashier cart calculation, and dynamic VietQR generation (`IPosService`, `VietQrService`, `SalesController`).
- **`backend/Identity/`**: User credentials, refresh tokens, RBAC authorization, initial database seeding (`DataSeeder`), and Gemini AI service (`IAuthService`, `GeminiAiAgentService`, `AuthController`, `AiController`).
- **`backend/Common/`**: Core infrastructure including `AppDbContext`, `BaseEntity`, and HTTP exception middleware.
- **`shared/`**: Common domain contracts consumed by backend, client, and tests (`DTOs/`, `Requests/`, `Enums/`).

### 3. Data Flow
1. **User Interaction**: UI triggers an `{x:Bind}` event command on a ViewModel in `frontend/TechStore.Client/Features/<Feature>/`.
2. **ViewModel Execution**: The ViewModel executes a command method decorated with `[RelayCommand]`, updates observable properties, and invokes an API client service in `frontend/TechStore.Client/Services/Api/`.
3. **HTTP Transport**: Request is sent via `HttpClient` over HTTPS with an `Authorization: Bearer <token>` header to the ASP.NET Core API (`http://localhost:5176` or `https://localhost:7207`).
4. **API Routing & RBAC**: Request reaches an `[ApiController]`, passes through JWT authentication and role authorization checks, then executes the corresponding domain service interface.
5. **Business Logic & Concurrency**: The service opens an EF Core transaction, applies business logic (e.g. acquiring `FOR UPDATE` lock on inventory stock), and modifies state.
6. **Data Persistence**: Changes are committed through `AppDbContext` to PostgreSQL 16.
7. **Response**: Shared DTO response (from `shared/DTOs/`) is returned to the client and bound to UI components.

---

## Key Directories

```
SalesManagement/
├── backend/                       # ASP.NET Core 10 Web API modular monolith
│   ├── Catalog/                   # Products, categories, JSONB specs
│   ├── Common/                    # DbContext, BaseEntity, Middlewares, DataSeeder
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
| Interfaces | `I` + PascalCase | `ICatalogService`, `IInventoryService`, `INavigationService` |
| Methods | PascalCase + `Async` suffix if asynchronous | `GetByIdAsync`, `StockInAsync`, `ProcessPaymentAsync` |
| Private Readonly Fields | `_` + camelCase | `_dbContext`, `_logger`, `_catalogService` |
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

### 4. Concurrency & Anti-Overselling Pattern
- Inventory operations affecting stock quantities or assigning Serial/IMEIs must wrap mutations inside an explicit ACID transaction with row-level locks:
  ```csharp
  await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
  // Execute SELECT ... FOR UPDATE or pessimistic lock mechanism
  // Perform stock reduction and record allocation
  await _dbContext.SaveChangesAsync(cancellationToken);
  await transaction.CommitAsync(cancellationToken);
  ```

### 5. Asynchronous Programming Pattern
- Use pure non-blocking `async`/`await` end-to-end.
- Always propagate `CancellationToken cancellationToken = default` down to EF Core calls (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`).
- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` (causes thread pool starvation and deadlocks).
- Never use `async void`, except for top-level WinUI XAML event handlers where the signature is dictated by framework delegates.

### 6. Dependency Injection Pattern
- Backend services are registered in `backend/Program.cs` or module service collection extensions:
  - **Scoped**: Domain services (`ICatalogService`), DbContext (`AppDbContext`), unit-of-work services.
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
- ViewModels in `frontend/TechStore.Client/Features/` must remain completely decoupled from WinUI controls to enable headless unit testing in standard test runners without requiring a running UI thread.
- Test files must mirror the target project's namespace and folder structure under `tests/`.
- Ensure tests are idempotent and do not depend on execution order or persisted test state.
