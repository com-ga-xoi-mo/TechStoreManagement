# Repository Guidelines

## Project Overview
TechStore Management System is an enterprise retail POS (Point of Sale) and inventory management platform tailored for high-value technology hardware and accessories (smartphones, laptops, components). Key system capabilities include:
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
│   • Pattern: MVVM + Client Repositories + CommunityToolkit   │
│   • Communication: RESTful HTTPS + JWT Bearer via ApiClient  │
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
└──────────────────────────────┘
```

> **Cardinal Rule:** The frontend client **never** connects directly to the database. All interactions must pass through ASP.NET Core REST endpoints secured by JWT Bearer tokens and role policies.

### 2. Key Modules
- **`backend/Catalog/`**: Product categories, variants, and dynamic hardware specifications stored in JSONB columns (`IProductService`, `ICategoryService`, `IProductRepository`, `ICategoryRepository`, `ISpecDefinitionRepository`, `ProductsController`, `CategoriesController`, `IProductImageService`, `IProductImageRepository`, `ProductImagesController`); spec display names, units and value kinds live in the `spec_definitions` table.
- **`backend/Inventory/`**: Stock counts, append-only movement audit ledger (`inventory_movements`), supplier purchases, unique Serial/IMEI lifecycle states (`InStock`, `Reserved`, `Sold`, `Returned`, `UnderRepair`, `Defective`), composite foreign keys, and pessimistic row-locking (`IInventoryService`, `IInventoryStockRepository`, `ISerialImeiRepository`, `IInventoryFulfillmentService`, `InventoryController`).
- **`backend/Orders/`**: Order processing, sold IMEI linkage, order status lifecycle transitions, returns, and refunds (`IOrderService`, `IOrderRepository`, `OrdersController`).
- **`backend/Customers/`**: Customer profiles, loyalty point accrual, discount vouchers, and revenue analytics (`ICustomerRepository`, `CustomerRepository`).
- **`backend/Sales/`**: POS counter register sessions, cashier cart calculation, and dynamic VietQR generation (`PosSession`, `VietQrTransaction`).
- **`backend/Identity/`**: User credentials, refresh tokens, store configuration settings, RBAC authorization, initial database seeding (`IAuthService`, `IStoreSettingsService`, `AuthController`, `StoreController`, `DataSeeder`, `IDataSeeder`).
- **`backend/Common/`**: Core infrastructure including `AppDbContext`, `IUnitOfWork` / `UnitOfWork` (saves and transactions), `DuplicateKeyException`, `BaseEntity`, HTTP exception handling, and `Storage/` (`IObjectStorage`, `ObjectStorageOptions`, R2 or a local folder).
- **`frontend/TechStore.Client/`**: Cross-platform desktop application organized into `Views/`, `ViewModels/`, `Models/`, `Repositories/`, `Services/`, `Controls/`, and `Styles/`.
- **`shared/`**: Common domain contracts consumed across backend, client, and test projects (`DTOs/`, `Requests/`, `Enums/`).

### 3. Data Flow
1. **User Interaction**: UI triggers a bound command (C# Markup `.Command(x => x.Binding(() => ViewModel.XCommand))`, or `{x:Bind}` on the pages not yet converted) on a ViewModel in `frontend/TechStore.Client/ViewModels/<Module>/`.
2. **ViewModel Execution**: The ViewModel executes a command decorated with `[RelayCommand]`, coordinates loading/error/content state via `PageViewModel` (`PageContentState`), and invokes a client repository in `frontend/TechStore.Client/Repositories/`.
3. **Client Repository & HTTP Transport**: The client repository converts request parameters into query URIs, calls `IApiClient` / `ApiClient` (`GetAsync`, `PostAsync`, `PutAsync`, `PatchAsync`, `DeleteAsync`; backed by `HttpClient` configured with `BearerTokenHandler`), and maps response DTOs to client domain models (`frontend/TechStore.Client/Models/`).
4. **API Routing & RBAC**: The request reaches an `[ApiController]`, passes through JWT authentication and role authorization checks, then calls the corresponding domain service interface (controllers never call repositories directly).
5. **Business Logic & Concurrency**: The domain service validates input and applies business rules. When the use case locks rows or performs multi-step writes, it initiates a transaction via `IUnitOfWork.BeginTransactionAsync`.
6. **Data Access**: The service invokes repository interfaces (`backend/<Module>/Repositories/`). Repositories are the only classes that interact with `AppDbContext`: they query (using `AsNoTracking` for reads), acquire row locks (`SELECT ... FOR UPDATE`), and stage entity additions or removals, but never save.
7. **Data Persistence**: The service calls `IUnitOfWork.SaveChangesAsync` (and `CommitAsync` when a transaction is active), which writes staged changes to PostgreSQL 16. Unique constraint violations (`23505`) are translated into `DuplicateKeyException`.
8. **Response**: The service returns a domain result or DTO (from `shared/DTOs/`), which the controller converts into an HTTP response returned to the client.

---

## Key Directories

```
SalesManagement/
├── backend/                       # ASP.NET Core 10 Web API modular monolith
│   ├── Catalog/                   # Products, categories, JSONB specs (Services, Repositories, Entities, Configurations)
│   ├── Common/                    # Infrastructure: AppDbContext, IUnitOfWork/UnitOfWork, DuplicateKeyException, DataSeeder
│   │   ├── Data/                  # DbContext, UnitOfWork, Migrations/, DataSeeder
│   │   ├── Storage/               # IObjectStorage: R2 or local image files
│   │   └── Entities/              # BaseEntity
│   ├── Customers/                 # Customers, loyalty points, vouchers (Repositories, Entities, Configurations)
│   ├── Identity/                  # RBAC, users, roles, refresh tokens (Entities, Configurations)
│   ├── Inventory/                 # Stock, Serial/IMEI, supplier purchase orders (Services, Repositories, Entities, Configurations)
│   ├── Orders/                    # Order processing, line items, status transitions (Services, Repositories, Entities, Configurations)
│   ├── Properties/                # launchSettings.json (ports 5176/7207)
│   └── Sales/                     # POS sessions, VietQR payment transactions (Entities, Configurations)
│
├── frontend/                      # Client desktop application (Uno Platform 6.7 / WinUI 3)
│   ├── Directory.Build.props      # CPM enablement and compiler warning suppressions (NU1507, NETSDK1201, PRI257)
│   ├── Directory.Packages.props   # Central Package Management package definitions
│   └── TechStore.Client/          # Uno Platform multi-targeted client project
│       ├── App.xaml / App.xaml.cs # DI bootstrap (AddFrontendFoundation, AddFrontendUi) and window lifecycle
│       ├── Views/                 # UI pages: C# Markup (Catalog, Identity, Settings, Shell, Development/MarkupReferencePage) and legacy XAML (Customers, Dashboard, Inventory, Orders, Development/ComponentGalleryPage, MainPage)
│       ├── ViewModels/            # MVVM ViewModels extending PageViewModel (Catalog, Customers, Dashboard, Inventory, Orders)
│       ├── Repositories/          # Client data repositories mapping DTOs to client Models (Catalog, Analytics)
│       ├── Models/                # Client-side presentation models (ProductSummary, ProductDetail, DashboardSnapshot)
│       ├── Services/              # Client services (Api, Navigation, Session, Theme, Dialog, Window)
│       ├── Controls/              # Shared controls in C# Markup (StatCard, StatusBadge, SearchBoxControl, EmptyStateControl), Presentation/ (pure display rules), Charts/
│       ├── Styles/                # C# design tokens: AppColors, AppTypography, AppMetrics, AppIcons, AppControlStyles (+ *.Resources.cs accessors), AppResources (registration); StitchTokens (legacy static colors)
│       └── Platforms/             # Desktop (Skia macOS/Linux) and Windows entry points
│
├── shared/                        # Shared contracts across API, Client, and Tests (no NuGet dependencies)
│   ├── DTOs/                      # Data Transfer Objects (ProductDto, OrderDto, SerialImeiDto, PagedResultDto)
│   ├── Enums/                     # Shared enums (OrderStatus, RoleType, PaymentMethod, SerialImeiStatus, CustomerTier)
│   └── Requests/                  # API request models (CreateProductRequest, CreateOrderRequest, OrderSearchRequest)
│
├── tests/                         # Automated test suites (.NET 10)
│   ├── Backend.UnitTests/         # Backend unit tests using hand-written fake repositories (TechStore.UnitTests.csproj)
│   ├── Frontend.UnitTests/        # Headless MVVM, repository, transport, and navigation tests (TechStore.Client.UnitTests.csproj)
│   └── Frontend.IntegrationTests/ # Opt-in live API integration test suite (TechStore.Client.IntegrationTests.csproj)
│
└── docs/                          # Architecture blueprints, roadmaps, and database schema specifications
    └── database/schema.dbml       # Definitive DBML schema specification (PostgreSQL 16 / Supabase best practices)
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
# Restore dependencies across entire solution
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
# Run backend Web API (Swagger UI served at http://localhost:5176)
dotnet run --project backend/TechStore.Api.csproj --launch-profile http

# Run backend with HTTPS enabled (https://localhost:7207)
dotnet run --project backend/TechStore.Api.csproj --launch-profile https

# Run frontend desktop client (Skia Desktop for macOS/Linux/Windows)
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

### 5. Testing & Coverage
```bash
# Run all tests across the solution
dotnet test TechStore.sln

# Run backend unit tests specifically
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj

# Run frontend headless unit tests specifically
dotnet test tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj

# Run tests with detailed test names
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --logger "console;verbosity=normal"

# Run a specific test class or method by filter pattern
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --filter "FullyQualifiedName~OrderServiceTests"

# Run tests with code coverage collection
dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj --collect:"XPlat Code Coverage"
```

---

## Code Conventions & Common Patterns

### 1. Language & Formatting
- **Target Language:** C# 12 / C# 13 on **.NET 10.0 SDK**.
- **Nullability & Usings:** `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>` are enforced across all projects. Use file-scoped namespaces (`namespace TechStore.Api.Catalog;`).
- **Constructors:** Prefer primary constructors (`public class ProductService(...) : IProductService`) or clean constructor dependency injection. Use target-typed `new()` where the concrete type is obvious.

### 2. Naming Conventions
|Element|Convention|Example|
|:---|:---|:---|
|Classes / Records|PascalCase|`ProductVariant`, `OrderService`, `AppDbContext`|
|Interfaces|`I` + PascalCase|`IProductService`, `IInventoryStockRepository`, `IUnitOfWork`|
|Async Methods|PascalCase + `Async` suffix|`GetByIdAsync`, `SearchAsync`, `CreateAsync`, `SaveChangesAsync`|
|Repositories|`I<Aggregate>Repository` / `<Aggregate>Repository`|`IProductRepository`, `InventoryStockRepository`|
|Private Backing Fields|`_` + camelCase|`_productRepository`, `_unitOfWork`, `_dbContext`|
|DTOs|PascalCase + `Dto` suffix|`ProductDto`, `OrderDto`, `PagedResultDto`|
|Requests|PascalCase + `Request` suffix|`CreateProductRequest`, `CreateOrderRequest`|
|Enums & Members|PascalCase (Singular)|`OrderStatus.Pending`, `RoleType.SalesStaff`|
|Unit Test Methods|`Method_StateUnderTest_ExpectedBehavior`|`CreateAsync_WalkInOrderWithVariant_CreatesPendingOrderPricedFromVariant`|
|Behavior Test Methods|`Subject_Should_ExpectedBehavior`|`CategoryServiceTests.GetTreeAsync_Should_Build_Hierarchical_Tree`|

### 3. Backend Layering & Repository Pattern
- **Strict Layering Rule:** `Controller → Service → Repository → AppDbContext`.
  - **Controllers** call domain services only. Controllers never inject `AppDbContext` or repository interfaces.
  - **Services** encapsulate validation, business rules, transaction boundaries, orchestration, and entity-to-DTO mapping. Services depend only on repository interfaces, `IUnitOfWork` and infrastructure ports from `Common/` (`IObjectStorage`, `TimeProvider`).
  - **Repositories** live in `backend/<Module>/Repositories/` as `I<Aggregate>Repository` / `<Aggregate>Repository`. They are the **only** classes that reference `AppDbContext` (LINQ, `FromSqlInterpolated`, `AsNoTracking`, `Include`).
  - **Repository Query Inputs:** Repositories accept module-internal criteria records with pre-validated, parsed values (e.g. `ProductSearchCriteria`, `OrderSearchCriteria`), never raw `shared/Requests` models.
  - **Repository Methods:** Repositories express business query intent (`SearchAsync(criteria)`, `GetWithVariantsAsync(id)`, `FindExistingSkusAsync(skus)`, `Add(product)`). Read-only methods MUST use `AsNoTracking()`. Repositories stage changes (`Add`, `Remove`) and **never** invoke `SaveChanges`, `BeginTransaction`, `Commit`, or `Rollback`.
- **Unit of Work Pattern (`IUnitOfWork` / `UnitOfWork`):**
  - Wraps the request-scoped `AppDbContext`.
  - `SaveChangesAsync(cancellationToken)` writes all staged changes. PostgreSQL unique constraint violations (`23505`) are caught and translated into `DuplicateKeyException` (exposing `ConstraintName`), which domain services translate to conflict results (HTTP 409).
  - `BeginTransactionAsync(cancellationToken)` throws an `InvalidOperationException` if a transaction is already active (no nested transactions).
  - `RollbackAsync(cancellationToken)` rolls back the active transaction and clears the EF Core `ChangeTracker` (`dbContext.ChangeTracker.Clear()`) to prevent dirty state contamination.

### 4. Concurrency & Anti-Overselling Pattern
- Inventory operations that modify stock quantities or reserve Serial/IMEIs must execute inside an explicit ACID transaction with row-level locks:
  ```csharp
  await _unitOfWork.BeginTransactionAsync(cancellationToken);
  try
  {
      // SELECT ... FOR UPDATE executed via FromSqlInterpolated in repository
      var stock = await _stockRepository.GetForUpdateAsync(productId, variantId, cancellationToken);
      if (stock == null || stock.AvailableQuantity < requestedQuantity)
      {
          await _unitOfWork.RollbackAsync(cancellationToken);
          return Conflict("Insufficient available inventory.");
      }

      stock.ReservedQuantity += requestedQuantity;
      await _unitOfWork.SaveChangesAsync(cancellationToken);
      await _unitOfWork.CommitAsync(cancellationToken);
  }
  catch
  {
      await _unitOfWork.RollbackAsync(cancellationToken);
      throw;
  }
  ```
- **Repository Lock Implementation:** Repository lock methods use raw SQL interpolation inside an active transaction:
  ```csharp
  dbContext.InventoryStocks
      .FromSqlInterpolated($"SELECT * FROM inventory_stocks WHERE product_id = {productId} AND variant_id = {variantId} FOR UPDATE")
      .FirstOrDefaultAsync(cancellationToken);
  ```
- **Anti-Overselling Balance Invariants:** Enforced via database check constraints:
  - `quantity >= 0`
  - `reserved_quantity >= 0`
  - `reserved_quantity <= quantity`
  - Physical stock adjustments are tracked via the append-only `inventory_movements` ledger (`quantity_change <> 0`, `quantity_after >= 0`).

### 5. PostgreSQL 16 & Supabase Best Practices (Schema Contract: `docs/database/schema.dbml`)
- **100% Foreign Key Indexing:** Every foreign key column across all tables MUST have an explicit B-tree index (`idx_<table>_<column>`) to prevent table locks and sequential scans during cascade checks.
- **Composite Foreign Keys:** Enforce `(variant_id, product_id)` referencing `product_variants(id, product_id)` on `inventory_stocks`, `purchase_order_items`, `serial_imeis`, and `order_items`.
- **PostgreSQL 16 `UNIQUE NULLS NOT DISTINCT`:** Enforced on `inventory_stocks(product_id, variant_id)` and `purchase_order_items(purchase_order_id, product_id, variant_id)` using `.AreNullsDistinct(false)` in EF Core Fluent API:
  ```csharp
  builder.HasIndex(x => new { x.ProductId, x.VariantId })
      .IsUnique()
      .AreNullsDistinct(false)
      .HasDatabaseName("idx_inventory_stocks_product_variant");
  ```
- **Product images:** Files live in object storage; `product_images` stores references only. `products.image_url` is the server-computed cover (first shared image, else first image, else null). At most 20 images per product, at most 5 MB each, JPEG/PNG/WebP identified by content. Credentials only through user-secrets or environment variables.
- **Spec definitions:** spec keys stay snake_case data inside `specs`; their display name, unit and value kind live in `spec_definitions` (one row per key, seeded, insert-only through product create/update `specLabels`). Clients show `label`/`unit` from `GET /api/products/spec-definitions` and never store names inside `specs`.
- **JSONB Hardware Specs Indexing:** GIN index configured with `jsonb_path_ops` on `specs` column in `products` and `product_variants` for fast containment queries (`specs @> '{"ram_gb": 16}'`). Flat JSON contract with snake_case keys; effective specs = `product.specs || variant.specs`.
- **Partial Indexes:** High-selectivity operational indexes:
  - `serial_imeis`: `(product_id, status)` WHERE `status = 'InStock'` for fast POS scanning.
  - `vouchers`: `(is_active, expires_at)` WHERE `is_active = true`.
  - `refresh_tokens`: `(user_id, token_hash)` WHERE `is_revoked = false` (`token_hash` stores SHA-256 hash).
- **Strict Data Types:** All temporal columns use UTC `timestamptz`; all monetary amounts use `numeric(18, 2)`. Identifiers use lowercase `snake_case`.

### 6. Frontend MVVM & C# Markup Pattern
- **Base Architecture:**
  - Uses **MVVM combined with Client Repositories & C# Markup**: `View (C# Markup)` → `ViewModel` → `Client Repository` → `ApiClient` → `HTTP REST API`.
  - **Client Repositories** (`frontend/TechStore.Client/Repositories/`) isolate ViewModels from networking, HTTP headers, serialization, and error codes by converting DTOs into presentation models (`frontend/TechStore.Client/Models/`).
  - **`PageViewModel` Base Class:** ViewModels inherit from `PageViewModel`, providing observable `PageContentState` (`Idle`, `Loading`, `Empty`, `Content`, `Error`), `PageActivationLease` for lifecycle cleanup, cancellation token management (`ActivationToken`), and `RetryCommand`.
  - **CommunityToolkit.Mvvm:** Backing fields use `[ObservableProperty]` to generate change notifications; async actions use `[RelayCommand]`.
  - **Zero UI Controls in ViewModels:** ViewModels never reference WinUI or Uno controls, enabling 100% headless testing in standard unit test runners.
- **C# Markup syntax (the one working pattern; copy from `Views/Development/MarkupReferencePage.cs`, matches `LoginPage.cs` / `SettingsPage.cs`):**
  - Views are C# classes inheriting from `Page` (no `.xaml` / `.xaml.cs`). The page receives its ViewModel in the constructor, keeps a `ViewModel` property and sets `DataContext = viewModel`.
  - Binding: `.Text(x => x.Binding(() => ViewModel.Title))`; two-way: `.Text(x => x.Binding(() => ViewModel.Query).Mode(BindingMode.TwoWay))`; type change: `.Visibility(x => x.Binding(() => ViewModel.IsError).Convert((bool v) => v ? Visibility.Visible : Visibility.Collapsed))`; command: `.Command(x => x.Binding(() => ViewModel.SubmitCommand))`. The old `x.Bind(...)` form is not used.
  - Typed WinUI properties bind the same way, e.g. `this.RequestedTheme(x => x.Binding(() => ViewModel.ThemeIndex).Convert((int i) => ...))`.
  - The binding path is derived from the lambda text at run time, so a wrong path still builds. Always run the page and check the binding, not only the build.
  - Grid sizes in strings (`.ColumnDefinitions("2*, 1*, Auto")`) take whole numbers only: Uno Markup 6.7 reads a star size with the regex `(\d+)\*`, so `"1.15*"` silently becomes `15*`. Write `"6*, 5*"` instead of `"1.2*, 1*"`.
  - Item templates: Uno calls the builder passed to `ItemTemplate<T>(Func<T, UIElement>)` with `default`, so the item parameter is always `null` when the template is built. Use it only inside binding lambdas (`.Text(x => x.Binding(() => item.Name))`, `.CommandParameter(x => x.Binding(() => item))`, `.ItemsSource(x => x.Binding(() => item.Children))`); never `.Text(item.Name)`, `.CommandParameter(item)` or `.ItemsSource(item.Children)`, which build but break at run time. A command that receives such a `CommandParameter` binding takes `object?` and checks the type (`if (parameter is Row row)`): while a row is being removed the binding can briefly hand over the page's DataContext, and a typed `RelayCommand<T>` then throws.
- **Forbidden in View/Control files when syncing data with the ViewModel:** `PropertyChanged +=`, `TextChanged +=`, `Checked +=`, `Click +=`, `Color.FromArgb`, `new SolidColorBrush(...)`, and setting `Visibility`/`Text` from event handlers. Use bindings, converters and commands instead.
- **Allowed exceptions (documented in `openspec/changes/csharp-markup-foundation/design.md` D2):**
  - `AutoSuggestBox.QuerySubmitted` in `SearchBoxControl` to raise `SearchCommand` (no command exists for submit);
  - returning focus to the text box when "Xóa" is pressed (focus cannot be bound);
  - `PasswordBox` sync in `LoginPage` via `PasswordChanged` (and `ViewModel.PropertyChanged` for show/hide), because `PasswordBox.Password` is not a DependencyProperty;
  - a DependencyProperty changed-callback in a shared control that only computes derived display properties through `Controls/Presentation` helpers;
  - focus restore in Catalog pages (`ProductListPage` returns focus to the product card or heading after coming back from detail via `ViewModel.PropertyChanged` on `Snapshot`; `ProductDetailPage`/`ProductEditorPage` focus their heading on `Loaded`), because focus cannot be bound — see `openspec/changes/catalog-product-management-ui/design.md` D8.
  Any new exception must be added to design D2 first.
- **Theme tokens (`frontend/TechStore.Client/Styles/`):**
  - Values live in C#: `AppColors`, `AppTypography`, `AppMetrics`, `AppIcons`, `AppControlStyles`. Key names are string constants (`AppColors.Canvas == "TechStoreCanvasBrush"`, pure C#, unit-tested). `AppResources.Register(this)` merges them into `Application.Resources` in the `App` constructor, after `XamlControlsResources`. `App.xaml` has no `Styles/*.xaml`.
  - Typed accessors for C# Markup return theme references, so colors follow Light/Dark: `.Background(AppColors.Brushes.Canvas)`, `.Foreground(AppColors.Brushes.SecondaryText)`, `.Style(AppTypography.Styles.HeadingText)`, `.Style(AppControlStyles.Styles.PrimaryButton)`, `AppMetrics.Values.CardRadius`. Do not use a static `Brush` or `Color` for themed UI. When the same element also sets its own theme color (e.g. `.Foreground(AppColors.Brushes.SecondaryText)`), apply the style with `.Style(AppResources.GetStyle(AppTypography.CaptionText))` instead of `AppTypography.Styles.*`, so the style cannot override that color (see `MarkupReferencePage`).
  - Icons: pass the registered instance, `.IconSource(AppIcons.Get(AppIcons.EmptyIcon))`. Do not bind an `IconSource` through a Markup `StaticResource` builder: on Skia it made the first layout of the product list take about 8.5 s instead of 0.2 s.
  - **Theme colors and Styles (verified on Skia, see `design.md` D3 and "Việc về sau"):** in Uno C# Markup, a Style `Setter` whose value is a theme color (`new Style<T>().Setters(s => s.Background(AppColors.Brushes.Surface))`) is resolved once and does NOT update when the theme changes at run time. Rules:
    1. **In C# Markup views and controls, set theme colors directly on the element**: `.Background(AppColors.Brushes.Surface)`, `.Foreground(AppColors.Brushes.Text)`. This updates live on Light/Dark switch.
    2. **Markup `Style<T>` holds only non-color properties** (font, size, line height, padding, corner radius, min height). Do not put theme colors in a Markup style.
    3. **`AppResources.AddThemedStyles` is legacy compatibility only.** It builds the 10 existing styles that carry theme colors (`TechStore*TextStyle`, `TechStoreFieldErrorStyle`, `TechStoreTableHeaderStyle`, `TechStorePanelStyle`, `TechStoreDangerButtonStyle`) through `XamlReader.Load`, because the remaining XAML pages use them by name and need live theme updates. **Do not add new styles to it.** If a converted page needs the same look, use a Markup style for the non-color part plus colors on the element.
    4. **When converting a XAML page**, check with `git grep -n "<StyleKey>" -- '*.xaml'` whether any other XAML page still uses each themed style. When none does, move that style out of `AddThemedStyles`: color parts to the elements, the rest to a Markup `Style<T>`. When `AddThemedStyles` becomes empty, delete it. Icons (`AddIcons`) keep `XamlReader` until `PathIconSource.Foreground` can follow the theme from code.
    5. **Always check theme switching at run time** after touching colors or styles: run the app, switch Sáng/Tối in Settings (or in the gallery with `TECHSTORE_UI_GALLERY=1`) while the page is open, and confirm text, panels and buttons stay readable. Build and headless tests cannot catch this bug.
    6. On a Uno upgrade, re-test rule 1–2 in the gallery; if Markup style setters start following the theme, `AddThemedStyles` can be removed earlier.
  - Remaining XAML pages keep using the same keys (`{ThemeResource TechStoreCanvasBrush}`, `{StaticResource TechStorePanelStyle}`).
  - HighContrast maps to system colors in the token tables (tested); it cannot be exercised on Skia/Linux.
- **Shared controls (`Controls/`):** `StatCard`, `StatusBadge`, `SearchBoxControl`, `EmptyStateControl` are `UserControl`s built in C# Markup with an unchanged DependencyProperty API.
  - In a C# Markup page, set properties with the fluent extensions, bind like any element: `new SearchBoxControl().Text(x => x.Binding(() => ViewModel.Query).Mode(BindingMode.TwoWay)).SearchCommand(x => x.Binding(() => ViewModel.SearchCommand))`, `new StatusBadge().Label("Hoàn thành").Tone(StatusTone.Success)`. These extensions compile without an extra assembly attribute.
  - Inside a control, bind through a private `Self => this` property and set `DataContext(this)` on the control's root element (not on the control), so the consumer's `DataContext` is untouched.
  - Display rules (loading > error > content, `—` for missing values, action visibility, tone to color keys) live in the pure `Controls/Presentation/` helpers, which are linked into the headless tests.
  - After the first load, re-apply code-set theme brushes (`Loaded += ApplyThemeColors`), because a style setter can override a brush applied earlier.
- **Reference and gallery:** run with `TECHSTORE_UI_GALLERY=1` and press "Mở mẫu C# Markup" to see `MarkupReferencePage` (sample data, no backend). It is registered only when the gallery is enabled.
- **Conversion status of views:**

  |State|Views|
  |:---|:---|
  |C# Markup with theme-aware tokens|`MarkupReferencePage`, `OrderListPage`, `OrderCreatePage`, `OrderDetailDialog`, `ProductListPage`, `ProductDetailPage`, `ProductEditorPage`; shared controls `StatCard`, `StatusBadge`, `SearchBoxControl`, `EmptyStateControl`|
  |C# Markup, colors still static (`StitchTokens` / `Color.FromArgb`), light only|`LoginPage`, `ShellPage`, `SettingsPage` (backlog: move to the theme accessors, then delete `StitchTokens`)|
  |Still XAML|`DashboardPage`, `InventoryPage`, `CustomerListPage`, `ComponentGalleryPage`, `MainPage`|

### 7. Asynchronous Programming Pattern
- Pure non-blocking `async`/`await` end-to-end.
- Always propagate `CancellationToken cancellationToken = default` down to EF Core calls (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`) and HTTP calls.
- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` (causes thread pool starvation and deadlocks).
- Avoid `async void`, except for top-level event handlers where the framework delegate signature strictly requires it.

### 8. Error Handling & API Responses
- Centralized exception handling middleware returns RFC 7807 `ProblemDetails` or structured JSON payloads.
- Explicit HTTP status codes:
  - `200 OK` / `201 Created`: Successful query or entity creation.
  - `400 Bad Request`: Input validation failure or business constraint violation.
  - `401 Unauthorized`: Missing or invalid JWT Bearer token.
  - `403 Forbidden`: Authenticated user lacks required `RoleType`.
  - `404 Not Found`: Entity with specified ID does not exist.
  - `409 Conflict`: Concurrency conflict, inventory overselling, or unique constraint violation.
  - `500 Internal Server Error`: Unhandled server exception.

### 9. Dependency Injection Pattern
- **Backend (`backend/Program.cs`):**
  - **Scoped:** DbContext (`AppDbContext`), Unit of Work (`IUnitOfWork`), repositories (`IProductRepository`, `IOrderRepository`, `IProductImageRepository`), domain services (`IProductService`, `IOrderService`, `IProductImageService`), and `IDataSeeder`.
  - **Singleton:** Stateless utility services (`TimeProvider.System`), token generators, `IObjectStorage` (`R2ObjectStorage` or `LocalObjectStorage`).
- **Frontend (`frontend/TechStore.Client/App.xaml.cs`):**
  - `AddFrontendFoundation`: Registers `ApiClientOptions`, `ISessionStore` (Singleton), `BearerTokenHandler` (Transient), and configured `IApiClient` / `ApiClient` via `AddHttpClient`.
  - `AddFrontendUi`: Registers core client services (`INavigationService`, `IDialogService`, `IThemePreferenceService`, `IWindowLifetime`) as Singleton; ViewModels, Views, and Client Repositories (`IProductRepository`, `ICategoryRepository`) as Transient.

---

## Important Files

### 1. Entry Points
- `backend/Program.cs`: ASP.NET Core application bootstrapper (DI container, EF Core Npgsql, Swagger UI at root URL `/`, auto-migration/seeding, routing).
- `frontend/TechStore.Client/Platforms/Desktop/Program.cs`: Uno Skia Desktop client bootstrapper for macOS, Linux, and Windows desktop.
- `frontend/TechStore.Client/App.xaml.cs`: WinUI/Uno application entry point, service provider configuration, window lifecycle, and route initialization.

### 2. Configuration & Orchestration
- `TechStore.sln`: Main solution linking backend, client, shared contracts, and all test suites.
- `global.json` & `frontend/global.json`: Locks MSBuild SDK `Uno.Sdk: 6.7.30` with `"allowPrerelease": false`.
- `docker-compose.yml`: PostgreSQL 16 Alpine container (`techstore_postgres`, port `5432`, volume persistence, healthcheck).
- `backend/Properties/launchSettings.json`: Profiles for backend execution (HTTP `5176`, HTTPS `7207`).
- `frontend/Directory.Build.props`: Enables CPM (`ManagePackageVersionsCentrally=true`) and suppresses warnings (`NU1507`, `NETSDK1201`, `PRI257`).
- `frontend/Directory.Packages.props`: Central Package Management definitions for frontend dependencies.
- `backend/Common/Data/AppDbContext.cs`: EF Core database context configuring Fluent API assembly scanning.
- `backend/Common/Data/UnitOfWork.cs`: Transaction management and PostgreSQL `23505` duplicate key exception mapping.
- `backend/Common/Data/DataSeeder.cs`: Baseline database seeding for roles, admin/cashier accounts, and sample catalog items.
- `docs/database/schema.dbml`: Authoritative relational database schema definition.

### 3. Key Domain & UI Implementations
- `backend/Catalog/Services/ProductService.cs`: Product creation, update with variant upsert, activation status, unused-product deletion, SKU validation, JSONB spec parsing/merging, and paged catalog queries.
- `backend/Orders/Services/OrderService.cs`: Order lifecycle state machine, fulfillment reservations, and sale confirmation.
- `backend/Inventory/Repositories/InventoryStockRepository.cs`: Row-level locked stock queries (`GetForUpdateAsync`).
- `frontend/TechStore.Client/ViewModels/Pages/PageViewModel.cs`: Base ViewModel providing observable loading states and activation leases.
- `frontend/TechStore.Client/ServiceCollectionExtensions.cs`: Frontend networking, token handler, and DI foundation registration.
- `frontend/TechStore.Client/UiServiceCollectionExtensions.cs`: Frontend navigation, repositories, ViewModels, and Views DI registration.

---

## Runtime & Tooling Preferences

- **SDK Requirement:** **.NET 10.0 SDK** (`10.0.x`) is strictly required across all projects in the solution.
- **Frontend UI Framework:** **Uno Platform 6.7** (`Uno.Sdk: 6.7.30`) multi-targeting:
  - `net10.0-desktop`: Skia rendering for macOS, Linux, and Windows.
  - `net10.0-windows10.0.26100`: WinAppSDK / WinUI 3 for native Windows 10/11 execution.
  - **Features Enabled (`UnoFeatures`):** `Mvvm`, `SkiaRenderer`, `CSharpMarkup`. Views, dialogs, and controls are transitioning 100% to C# Markup using `Uno.Extensions.Markup.WinUI` and `CommunityToolkit.WinUI.Controls.SettingsControls` aligned with Google Stitch design prototypes (`stitch_techstore_retail_admin_dashboard/`).
- **Session & Persistence:** `ISessionStore` / `InMemorySessionStore` paired with `ISessionStorage` / `FileSessionStorage` (`%LocalAppData%/TechStore/session.json` on Windows or `~/.local/share/TechStore/session.json` on Unix) to support persistent login across desktop app restarts.
- **Package Management:**
  - **Frontend:** Enforces **Central Package Management (CPM)** via `frontend/Directory.Build.props` and `frontend/Directory.Packages.props`.
  - **Backend, Shared, and Tests:** Use explicit inline package versions in their respective `.csproj` files.
- **Database Engine:** PostgreSQL 16 (`postgres:16-alpine`), operated via Docker Compose.
- **API Tooling:** Swashbuckle OpenAPI / Swagger UI mapped to the root URL (`http://localhost:5176/`) in development mode.

---

## Testing & QA

### 1. Test Projects & Frameworks
|Project|Path|Target Framework|Purpose & Scope|
|:---|:---|:---|:---|
|**TechStore.UnitTests**|`tests/Backend.UnitTests/TechStore.UnitTests.csproj`|`net10.0`|Backend unit tests (domain logic, validators, helpers, status transitions, fake repositories).|
|**TechStore.Client.UnitTests**|`tests/Frontend.UnitTests/TechStore.Client.UnitTests.csproj`|`net10.0`|Headless client tests (ViewModels, client repositories, transport, session, navigation, dialogs).|
|**TechStore.Client.IntegrationTests**|`tests/Frontend.IntegrationTests/TechStore.Client.IntegrationTests.csproj`|`net10.0`|Live API integration test suite (`CatalogApiIntegrationTests.cs`) against a running backend.|

- **Test Runner:** `xUnit` (`2.9.3`) with `Microsoft.NET.Test.Sdk` (`18.10.1`) and `xunit.runner.visualstudio` (`4.0.0`).
- **Assertion Library:** `FluentAssertions` (`8.11.0`) for readable, strongly-typed assertion chaining.
- **Coverage Tool:** `coverlet.collector` (`10.1.0`) for code coverage reporting.

### 2. Testing Conventions & Patterns
- **AAA Pattern:** All tests follow the Arrange-Act-Assert pattern with explicit section comments (`// Arrange`, `// Act`, `// Assert`).
- **Zero Dynamic Mocking Libraries:** Backend and frontend unit tests do not use dynamic mocking libraries (such as Moq or NSubstitute). Instead, tests use hand-written test doubles:
  - `FakeUnitOfWork`: Tracks `SaveChangesCount`, `BeginTransactionCount`, `CommitCount`, `RollbackCount`, and can simulate exceptions (`DuplicateKeyException`).
  - `FakeProductRepository`, `FakeOrderRepository`, `FakeInventoryStockRepository`, `FakeSerialImeiRepository`, `FakeCustomerRepository`: Maintain in-memory state for deterministic execution.
  - `FixedTimeProvider`: Replaces system clock with fixed UTC timestamps.
- **Headless Frontend Testing:** `TechStore.Client.UnitTests.csproj` is a pure C# console test project targeting `net10.0`. It links production code via `<Compile Include="..." Link="..." />` without compiling WinUI UI trees or requiring an active UI thread, enabling rapid cross-platform CI execution.
- **Fixture Partitioning:** Complex service tests are split across C# `partial class` files (e.g. `OrderServiceTests.Fixture.cs`, `OrderServiceTests.Create.cs`, `OrderServiceTests.Query.cs`, `OrderServiceTests.Transitions.cs`).
- **Coverage Expectations:** Every business rule, status transition, inventory availability check, validation rule, and concurrency/duplicate key mapping must have corresponding automated unit test coverage.
