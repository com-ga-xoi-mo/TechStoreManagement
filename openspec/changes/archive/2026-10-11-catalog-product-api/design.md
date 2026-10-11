# Design

## Context

See `proposal.md` for motivation and `specs/product-catalog/spec.md` for the required behavior.

Current state that shapes the approach:

- `Product` and `ProductVariant` store `Specs` as a C# `string` mapped to a `jsonb` column with a GIN `jsonb_path_ops` index (`ProductConfiguration`, `ProductVariantConfiguration`). Unique indexes exist on `products.sku` and `product_variants.sku` separately and are case-sensitive. Check constraints already enforce `base_price >= 0`, `cost_price >= 0`, `price >= 0`.
- Every FK that points at `products` / `product_variants` from other modules is `Restrict`; `product_variants` cascades from `products`.
- `backend/Program.cs` registers services one line each (`AddScoped<IDataSeeder, DataSeeder>()`). There is no exception middleware, no shared paging type, and no JSON option customization.
- `shared/DTOs/` and `shared/Requests/` are flat folders (see their READMEs). The Uno client references `TechStore.Shared` (`net10.0`).
- `tests/Backend.UnitTests/` is flat, namespace `TechStore.UnitTests`, and has no database fixture; there is no integration test project yet.
- Module layout per `docs/ARCHITECTURE.md` 2.2: `Entities/`, `Configurations/`, `Services/`, and the controller file at the module root.

## Goals / Non-Goals

**Goals:**
- Product and category work in `backend/Catalog/` can proceed in parallel without editing the same files.
- All three endpoints run against the existing schema with no migration.
- DB-independent rules (spec parsing, spec validation, effective-spec merge, display price, paging normalization) are unit-testable without PostgreSQL.
- `PagedResultDto<T>` is generic enough for other modules to reuse unchanged.

**Non-Goals:**
- Using the GIN index for effective-spec filtering (correctness first; dataset is tens of products).
- Database-enforced case-insensitive SKU uniqueness (would need a migration owned by Dev 1).
- Barcode uniqueness, accent-insensitive search, subcategory expansion.
- A cross-module error-handling convention (shared middleware, exception types).

## Decisions

### D1. Split Catalog services by owner, documentation first
Replace the documented `ICatalogService` / `CatalogService` with `IProductService` / `ProductService` (Dev 2) and `ICategoryService` / `CategoryService` (Dev 3), plus `ProductsController` and `CategoriesController` at the module root. The documentation edit is its own commit before any code.
- *Why*: two developers work in the same module during Sprint 1; one service class would be a constant merge conflict. The project already accepts several services per module (`ICustomerService` + `AnalyticsService`, `IAuthService` + `JwtService`).
- *Alternative*: keep `ICatalogService` and coordinate edits — rejected, high conflict rate for no design benefit.
- `ProductService` checks category existence by reading `AppDbContext.Categories` directly instead of depending on `ICategoryService`, so Dev 2 is not blocked by Dev 3.

### D2. Service returns explicit results; controller maps them to ProblemDetails
`IProductService` methods return result records (e.g. success / validation failed with an errors dictionary / conflict with SKUs / not found) rather than throwing. `ProductsController` maps them with the built-in `[ApiController]` helpers: `ValidationProblem(...)` for 400, `NotFound()` for 404, `Problem(statusCode: 409, ...)` for 409, `CreatedAtAction(...)` for 201.
- *Why*: no shared exception middleware exists and introducing one is not agreed with the team. Results keep the HTTP mapping in one place and make the service easy to test.
- *Alternative*: custom exceptions + try/catch in the controller — works but spreads control flow through exceptions and would be thrown away once a shared middleware exists.

### D3. One hand-written validator for the create request
Validation for `CreateProductRequest` is a single validator that returns `Dictionary<string, string[]>` keyed by camelCase field path (`name`, `basePrice`, `variants[1].sku`, `variants[0].specs`). It collects all input errors in one pass. DB-dependent checks (category exists, SKU already used) run after input validation succeeds.
- *Why*: the spec requires camelCase nested paths and all errors at once; DataAnnotations produce PascalCase C# property paths and cannot express cross-item SKU duplication.
- Model-binding failures (malformed JSON, non-Guid id) still use ASP.NET Core's automatic 400 ProblemDetails; their key style may differ, which is acceptable.
- **Request contracts are fully nullable.** The backend has `<Nullable>enable</Nullable>` and does not suppress implicit required attributes, so `[ApiController]` would treat every non-nullable reference property as `[Required]` and return its own 400 (PascalCase keys, one error) before this validator runs. It would also turn a missing `basePrice` into `0`, which passes `>= 0`. Therefore every input property in `ProductSearchRequest`, `CreateProductRequest` and `CreateProductVariantRequest` is nullable (`string?`, `decimal?`, `Guid?`, `bool?`, `List<...>?`, `Dictionary<...>?`), and the validator reports missing required fields (`categoryId`, `name`, `sku`, `brand`, `basePrice`, `costPrice`, variant `variantName`, `sku`, `price`). Defaults (`specs` → `{}`, `isSerialTracked` → `true`, `variants` → empty) are applied by the service, not by property initializers.

### D4. Specs: string in the entity, `Dictionary<string, JsonElement>` at the boundary
Entities keep `Specs` as a JSON string (no schema change). DTOs and requests expose `Dictionary<string, JsonElement>`, serialized as a plain JSON object with original value types. The effective-spec merge for the detail endpoint is done in C# with the same semantics as PostgreSQL `||` on flat objects (variant keys overwrite product keys).
- *Alternative*: `JsonDocument`/`JsonObject` on entities — needs model and snapshot churn owned by Dev 1; `Dictionary<string, object>` loses type fidelity on round-trip.

### D5. Listing query: LINQ for regular filters, composed raw SQL for spec containment
- Keyword: `EF.Functions.ILike` with `%`, `_` and `\` in user input escaped, applied to product name/sku/barcode and an `Any` over variants (sku/barcode/variant name).
- Brand: case-insensitive equality. Category: equality on `CategoryId`. Active: equality when provided.
- Display price: `priceFrom = variants.Any() ? variants.Min(price) : basePrice`, `priceTo` likewise with `Max`; EF Core translates these to correlated subqueries, reused for range filter (overlap: `priceFrom <= maxPrice && priceTo >= minPrice`) and `price` sorting.
- Spec filter: the parsed filters are combined into one JSON object and passed as a `jsonb` parameter. The base set comes from `FromSql` on `products` with the predicate "no variants and `p.specs @> @filter`, or EXISTS a variant with `(p.specs || v.specs) @> @filter`", then the LINQ filters above are composed on top. Requiring all filters in one object enforces "satisfied by the same effective specs".
- Ordering: chosen sort key, then `Id` as tie-breaker for stable paging. `Count` runs on the same filtered query before `Skip/Take`.
- *Why raw SQL*: `EF.Functions.JsonContains` cannot translate `p.specs || v.specs`. *Trade-off*: the merged expression bypasses the GIN index (acceptable, see Non-Goals).

### D6. Typed spec parsing
`spec=key:value` splits at the first colon. Key must match `^[a-z][a-z0-9_]*$`; value must be non-empty after trimming. Value typing: `true`/`false` → boolean; invariant-culture number (integer or decimal) → number; otherwise string. The same key repeated with different values is a 400 (cannot be satisfied by containment).
- *Alternative*: accept a raw JSON object in one `specs` parameter — harder to type in a URL and for the client, no benefit at this scale.

### D7. SKU uniqueness: case-insensitive pre-check + 23505 fallback
Before insert, compare the request's SKUs (trimmed, compared lower-case) against `products.sku` and `product_variants.sku` (lower-cased). Any hit → 409 listing the conflicting SKUs. SKUs are stored as entered (trimmed). If `SaveChanges` throws `DbUpdateException` whose inner `PostgresException.SqlState == "23505"`, the service returns the same 409 result.
- *Why*: covers the common case and the exact-match race using the existing unique indexes.
- *Trade-off*: case-variant or cross-table races (`ABC` vs `abc`, or product SKU vs another request's variant SKU at the same instant) are not blocked by the database. See Risks.

### D8. Shared paging contract
`shared/DTOs/PagedResultDto.cs` defines `PagedResultDto<T>` with `Items`, `Page`, `PageSize`, `TotalCount`, `TotalPages` (`ceil(TotalCount / PageSize)`, 0 when empty). Paging normalization (page < 1 → 1, pageSize default 20, clamp 1..100) is a small static helper in `backend/Catalog/Services/` so other modules can copy or later promote it to `Common/`. `ProductSearchRequest` carries `Page` and `PageSize` and is bound with `[FromQuery]`.

### D9. Pure-logic helpers are public static classes in `Catalog/Services/`
Spec filter parsing, spec validation, effective-spec merging, display-price calculation and paging normalization live in small `public static` classes under `backend/Catalog/Services/`.
- *Why*: unit tests can call them directly; `public` avoids adding `InternalsVisibleTo` to `TechStore.Api.csproj`.

### D10. Creation is a single `SaveChanges`
Product and variants are added to the context and saved once; EF Core wraps it in one transaction, satisfying atomicity without an explicit transaction. No inventory entities are touched. `IsSerialTracked` defaults to `true` (entity/DB default) when omitted; `Specs` defaults to `{}`.

### D11. Registration, routes and action names
`Program.cs` gets one line `builder.Services.AddScoped<IProductService, ProductService>();` next to the existing seeder registration. Routes: `api/products`, `api/products/{id:guid}`. `CreatedAtAction` targets the detail action so `Location` is `/api/products/{id}`.
- Controller actions are named **without** the `Async` suffix (`Search`, `GetById`, `Create`). ASP.NET Core strips `Async` from action names by default (`SuppressAsyncSuffixInActionNames`), so `CreatedAtAction(nameof(GetByIdAsync), ...)` would fail with "No route matches the supplied values" and turn a successful create into a 500. Service methods keep the `Async` suffix required by AGENTS.md (`SearchAsync`, `GetByIdAsync`, `CreateAsync`).

### D12. Namespaces
Namespaces follow folders, matching existing code (`TechStore.Api.Catalog.Entities`, `TechStore.Shared.Enums`):
- `shared/DTOs/*` → `TechStore.Shared.DTOs`; `shared/Requests/*` → `TechStore.Shared.Requests`
- `backend/Catalog/Services/*` → `TechStore.Api.Catalog.Services`; `backend/Catalog/ProductsController.cs` → `TechStore.Api.Catalog`
- `tests/Backend.UnitTests/ProductServiceTests.cs` → `TechStore.UnitTests`

### D13. Branch and commits
Work happens on a feature branch `feat/catalog-product-api` created from `dev`, merged back through a pull request like previous milestones. Four commits, following the precedent of `b81d591` (OpenSpec artifacts committed on their own): (1) planning artifacts under `openspec/changes/catalog-product-api/`, (2) documentation split, (3) Product API code, (4) task-progress update of `tasks.md`. The code commit contains nothing under `openspec/`. The pre-existing uncommitted `.gitignore` edit is not part of any commit.

## Risks / Trade-offs

- [Case-insensitive / cross-table SKU races are not DB-enforced] → Pre-check covers normal use; propose a later migration (functional unique index on `lower(sku)` or a shared SKU table) through Dev 1 if needed.
- [Spec filter ignores the GIN index] → Fine for Milestone 2 data volume; revisit with a `jsonb_path_ops`-friendly strategy if the catalog grows.
- [No integration tests: SQL composition and 23505 handling are only verified manually] → `TechStore.Api.http` contains one request per scenario and `tasks.md` holds a manual checklist run against seeded data before merge.
- [Documentation split must be accepted by Dev 3] → Share the docs commit with Dev 3 before merging; Dev 3 implements `ICategoryService` / `CategoriesController` under these names.
- [Prices use `>= 0` while REQUIREMENTS mention `> 0` for a valid product] → Matches existing DB constraints; if the team tightens it, it becomes a separate spec change.
- [ILIKE does not match text typed without Vietnamese diacritics] → Seed data is unaccented; accent-insensitive search is out of scope.
- [`PagedResultDto<T>` introduced by Catalog may diverge from what other modules want] → Kept minimal and generic; other modules reuse it as-is.

## Migration Plan

- No database migration. Branch `feat/catalog-product-api` from `dev`; four commits as described in D13; merge via pull request.
- Manual verification in `backend/TechStore.Api.http` never hard-codes seed ids (they are generated with `Guid.NewGuid()` per database); ids are read from named lookup requests so the file works on every developer's machine.
- Rollback: revert the commits; no data or schema changes to undo.
