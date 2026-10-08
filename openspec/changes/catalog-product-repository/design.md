# Design

## Context

See `proposal.md` for motivation. Required behavior is the existing `product-catalog` capability (from `catalog-product-api`); this change must not alter it. The target structure is the convention in `AGENTS.md` section 9 and `repository-unit-of-work-convention` design D1–D9.

Current code (branch `docs/repository-unit-of-work`, on top of `feat/catalog-product-api` incl. `bd6e008`):

- `ProductService` (≈400 lines) injects `AppDbContext`; `SearchAsync` validates spec/price/sort, then builds the query (optional `FromSqlInterpolated` spec filter, escaped `EF.Functions.ILike` for `q` and `brand`, category/isActive filters, price-overlap filter and price sort via `Variants.Select(v => (decimal?)v.Price).Min()/Max() ?? BasePrice`, `ThenBy(Id)`, `CountAsync`, `Skip/Take`, SQL projection to `ProductSummaryDto`).
- `GetByIdAsync` loads `Include(Category).Include(Variants)` and maps in memory; `CreateAsync` validates, checks category via `_dbContext.Categories`, checks SKUs case-insensitively in both tables, adds the product (+variants via navigation), saves, and catches `DbUpdateException` with `PostgresException { SqlState: "23505" }`.
- Pure helpers (`ProductSpecFilterHelper`, `ProductSpecHelper`, `ProductSpecValidatorHelper`, `ProductPagingHelper`, `CreateProductValidator`) already have no EF dependency and 23 test methods (55 cases).
- `Program.cs` registers `AppDbContext` (only when a connection string exists), `IDataSeeder`, `IProductService`.

## Goals / Non-Goals

**Goals:**
- `backend/Catalog/Services/` contains no reference to `AppDbContext`, `Microsoft.EntityFrameworkCore` or `Npgsql`.
- `ProductService` create/detail/search paths are unit-testable with in-memory fakes.
- Identical HTTP behavior, verified by re-running the full `backend/TechStore.Api.http`.

**Non-Goals:**
- Changing SQL semantics or performance characteristics of search (beyond loading variants for the page, see D4).
- A generic repository, a shared fake library, or an integration-test project.

## Decisions

### D1. `IUnitOfWork` exactly as the convention specifies
Files in `backend/Common/Data/` (namespace `TechStore.Api.Common.Data`): `IUnitOfWork.cs`, `UnitOfWork.cs` (primary constructor over `AppDbContext`, one private `IDbContextTransaction?` field), `DuplicateKeyException.cs` (`ConstraintName`). Behavior per convention D3/D4: translate `23505`; no nested transactions; commit throws without an open transaction; rollback is a no-op without one and clears the `ChangeTracker`. Registered `AddScoped<IUnitOfWork, UnitOfWork>()`. Dev 2 implements it because Catalog needs it now; it is committed on its own so Dev 1 can review it independently.

### D2. Repository files and ownership
`backend/Catalog/Repositories/` (namespace `TechStore.Api.Catalog.Repositories`):
- `IProductRepository.cs`, `ProductRepository.cs` — Dev 2.
- `ProductSearchCriteria.cs` — `public sealed record ProductSearchCriteria(string? Keyword, string? Brand, Guid? CategoryId, bool? IsActive, decimal? MinPrice, decimal? MaxPrice, string? SpecFilterJson, ProductSortField SortField, bool Descending, int Page, int PageSize)` and `public enum ProductSortField { Name, Price, CreatedAt }`.
- `ICategoryRepository.cs`, `CategoryRepository.cs` — minimal, only `Task<Category?> GetByIdAsync(Guid id, CancellationToken)` (`AsNoTracking`); a header comment states Dev 3 owns and extends the file.
All repositories use primary constructors over `AppDbContext` and never call `SaveChanges` or transaction APIs.

### D3. What moves into `ProductRepository` and what stays in `ProductService`
- **Moves (data access):** the base query with optional `FromSqlInterpolated` spec filter, `EscapeLikePattern` + `ILike` for keyword and brand (escaping is a SQL concern, so the criteria carries raw trimmed text), category / isActive filters, price-overlap filter, ordering (+ `ThenBy(Id)`), `CountAsync`, `Skip/Take`; the detail load; the case-insensitive SKU lookup across `products` and `product_variants` — `FindExistingSkusAsync(IEnumerable<string> skus)` receives the raw request SKUs and itself drops blanks, trims, lower-cases and de-duplicates them before matching `lower(sku)`, returning the stored SKUs that conflict; `Add`.
- **Stays (business):** spec parsing/validation (`ProductSpecFilterHelper`), `minPrice > maxPrice` check, sort parsing into `ProductSortField` + `Descending` (default `CreatedAt` desc, `name|price|createdat` case-insensitive, direction default `asc`), paging normalization, create validation, conflict decisions, entity construction (`IsActive = true`), entity → DTO mapping. Every error key and message stays byte-for-byte the same.
- The service only constructs criteria after validation succeeds, so invalid input never reaches the repository (tests assert this).
- Keyword/brand: service passes `Trim()`med values, `null` when empty/whitespace; category: `null` when `Guid.Empty` — matching current behavior.

### D4. Search returns entities; summary computed in the service
`SearchAsync` returns `(IReadOnlyList<Product> Items, int TotalCount)` with `AsNoTracking().Include(p => p.Category).Include(p => p.Variants)` applied to the ordered, paged query. The service maps each entity: `PriceFrom/PriceTo = ProductSpecHelper.CalculateDisplayPrice(p.BasePrice, p.Variants.Select(v => v.Price))`, `VariantCount = p.Variants.Count`, `CategoryName = p.Category.Name`. This equals the previous SQL projection (both use all variants). Filtering and sorting by price remain in SQL, so page membership and order are unchanged.
- *Trade-off*: the page now loads variant rows (≤100 products) instead of projecting two aggregates in SQL. Acceptable at this data volume; `AsSplitQuery()` is not needed now.
- *Alternative*: repository returns DTOs — rejected by the team convention (repositories return entities).

### D5. Create flow and duplicate handling
Order is unchanged: validator → `ICategoryRepository.GetByIdAsync` (400 `categoryId` when null) → `FindExistingSkusAsync` (409 with existing SKUs) → build entity → `IProductRepository.Add` → `IUnitOfWork.SaveChangesAsync` → on `DuplicateKeyException` return `CreateProductResult.Conflict(requestSkus)` (same payload as today's `23505` branch). No explicit transaction: one `SaveChanges` is atomic (convention D3).

### D6. Service signature
`public class ProductService(IProductRepository productRepository, ICategoryRepository categoryRepository, IUnitOfWork unitOfWork) : IProductService` (primary constructor, per `AGENTS.md`). `IProductService`, result records and `ProductsController` are unchanged, so the controller and DI consumer code do not move.

### D7. Tests with hand-written fakes
Flat files in `tests/Backend.UnitTests/` (namespace `TechStore.UnitTests`, matching existing tests):
- `FakeUnitOfWork.cs` — counts `SaveChangesAsync`/`Begin`/`Commit`/`Rollback` calls; optional `ThrowOnSave` exception (e.g. `DuplicateKeyException`). Reusable by other modules.
- `FakeProductRepository.cs` — in-memory products; records `LastCriteria`, `SearchCallCount`, added products; configurable `ExistingSkus` and search result.
- `FakeCategoryRepository.cs` — in-memory categories.
New tests go into `ProductServiceTests.cs` beside the existing helper tests. `UnitOfWork` itself is not unit-tested (it needs a real `DbContext`/PostgreSQL); its translation is verified manually by the concurrent-create check.

### D8. Registration
`Program.cs` adds, next to the existing registrations: `AddScoped<IUnitOfWork, UnitOfWork>()`, `AddScoped<IProductRepository, ProductRepository>()`, `AddScoped<ICategoryRepository, CategoryRepository>()`. The `IProductService` line stays.

### D9. Branch and commits
Branch `refactor/catalog-product-repository` from `docs/repository-unit-of-work`. Commits: (1) `docs(openspec)` change artifacts; (2) `feat(common)` Unit of Work only (`backend/Common/Data/*` + its `Program.cs` line); (3) `refactor(catalog)` repositories, service, remaining DI lines, tests, Catalog README; (4) `docs(openspec)` task progress. The pre-existing `.gitignore` edit stays uncommitted.

## Risks / Trade-offs

- [Silent behavior drift while moving queries] → Move query code verbatim, keep error strings unchanged, re-run every request in `backend/TechStore.Api.http` and compare status codes and totals with the pre-refactor results (brand wildcard, spec filters, price range, paging, 404, 409).
- [`Include` + `Skip/Take` on a `FromSql` source] → EF Core supports composing `Include` over composable `FromSql`; verified manually with the spec-filter requests in the `.http` file.
- [`UnitOfWork` written by Dev 2 inside Dev 1's `Common`] → Separate commit, Dev 1 reviews before merge; shape fixed by the convention design.
- [Dev 3 creating a second category repository] → Header comment + workflow follow-up to notify Dev 3 before Dev 3 starts `CategoryService`.
- [Stacked unmerged branches] → Merge order catalog API → docs → refactor; rebase this branch if earlier PRs change.

## Migration Plan

- No database or API change. Deploy = merge; rollback = revert the `refactor(catalog)` commit (and the `feat(common)` commit if nothing else uses `IUnitOfWork` yet).
