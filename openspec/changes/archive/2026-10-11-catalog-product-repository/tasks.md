# Tasks

## 1. Branch, planning artifacts and behavior baseline

- [x] 1.1 Create branch `refactor/catalog-product-repository` from `docs/repository-unit-of-work`; verify `git branch --show-current` prints it and keep the pre-existing `.gitignore` edit out of every commit
- [x] 1.2 Commit `openspec/changes/catalog-product-repository/` as `docs(openspec): record catalog-product-repository change artifacts`; verify `git show --stat HEAD` lists only files under that folder
- [x] 1.3 Capture a pre-refactor baseline with the current code: start PostgreSQL and the API, run every GET request of `backend/TechStore.Api.http` (resolving `findMacbook` ids) and save status code + `totalCount` + item SKUs per request to a file outside the repository (e.g. `/tmp/catalog-baseline.txt`); verify the file has one line per GET request
- [x] 1.4 Run `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj` on the unchanged code; verify all tests pass and note the count as the baseline

## 2. Unit of Work in `backend/Common/Data/` (separate commit for Dev 1 review)

- [x] 2.1 Add `DuplicateKeyException.cs` (`: Exception`, `ConstraintName` property) and `IUnitOfWork.cs` with exactly `SaveChangesAsync`, `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync` (all `CancellationToken cancellationToken = default`), namespace `TechStore.Api.Common.Data`; verify `dotnet build backend/TechStore.Api.csproj` succeeds
- [x] 2.2 Add `UnitOfWork.cs` (primary constructor over `AppDbContext`) per design D1: `23505` → `DuplicateKeyException(ConstraintName)`; `BeginTransactionAsync` throws `InvalidOperationException` when a transaction is open; `CommitAsync` commits + disposes and throws when none is open; `RollbackAsync` rolls back + disposes when open (no-op otherwise) and calls `ChangeTracker.Clear()`; verify the backend builds
- [x] 2.3 Register `builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();` in `backend/Program.cs` next to the existing registrations; verify the API starts and `GET /api/products` still returns 200
- [x] 2.4 Commit only `backend/Common/Data/{IUnitOfWork,UnitOfWork,DuplicateKeyException}.cs` and the `Program.cs` line as `feat(common): add IUnitOfWork, UnitOfWork and DuplicateKeyException`; verify with `git show --stat HEAD`

## 3. Catalog repositories (`backend/Catalog/Repositories/`)

- [x] 3.1 Add `ProductSearchCriteria.cs` (record + `ProductSortField { Name, Price, CreatedAt }`) as in design D2; verify the backend builds
- [x] 3.2 Add minimal `ICategoryRepository.cs` / `CategoryRepository.cs` with only `GetByIdAsync` (`AsNoTracking`) and a header comment that Dev 3 owns and extends the file; verify the backend builds and no other method exists
- [x] 3.3 Add `IProductRepository.cs` / `ProductRepository.cs` with `GetWithVariantsAsync` (`Include` Category + Variants, `AsNoTracking`), `FindExistingSkusAsync` (lower-case match on `products` and `product_variants`, distinct), and `Add` (`Products.Add` only); verify the backend builds and `grep -n "SaveChanges\|BeginTransaction" backend/Catalog/Repositories/*.cs` returns nothing
- [x] 3.4 Implement `ProductRepository.SearchAsync(criteria)` by moving the existing query code from `ProductService.SearchAsync` verbatim in behavior: optional `FromSqlInterpolated` spec filter on `criteria.SpecFilterJson`, `EscapeLikePattern` (moved here) + `ILike` for keyword (product name/sku/barcode, variant sku/barcode/variant name) and brand, category/isActive filters, price-overlap filter, ordering by `SortField`/`Descending` + `ThenBy(Id)`, `CountAsync`, `Skip/Take`, then `AsNoTracking().Include(Category).Include(Variants)`; return `(Items, TotalCount)`; verify the backend builds
- [x] 3.5 Register `AddScoped<IProductRepository, ProductRepository>()` and `AddScoped<ICategoryRepository, CategoryRepository>()` in `backend/Program.cs`; verify the API starts

## 4. `ProductService` refactor

- [x] 4.1 Change `ProductService` to a primary constructor over `IProductRepository`, `ICategoryRepository`, `IUnitOfWork`; rewrite `GetByIdAsync` to use `GetWithVariantsAsync` with the existing mapping (variants by price then SKU, `MergeEffectiveSpecs`); verify the backend builds
- [x] 4.2 Rewrite `SearchAsync`: keep spec/price/sort validation and error keys/messages unchanged, map sort to `ProductSortField` + `Descending` (default `CreatedAt` desc), normalize paging, build `ProductSearchCriteria` (trimmed keyword/brand or null, `Guid.Empty` category → null), call the repository, map entities to `ProductSummaryDto` with `CalculateDisplayPrice`, `Variants.Count`, `Category.Name`; verify the backend builds
- [x] 4.3 Rewrite `CreateAsync`: validator → `ICategoryRepository.GetByIdAsync` → `FindExistingSkusAsync` → build entity (`IsActive = true`) → `Add` → `IUnitOfWork.SaveChangesAsync`, catching `DuplicateKeyException` → `CreateProductResult.Conflict(requestSkus)`; verify the backend builds
- [x] 4.4 Verify the layering rule: `grep -rnE "AppDbContext|Microsoft.EntityFrameworkCore|Npgsql" backend/Catalog/Services/` returns nothing, and `IProductService`, `ProductsController.cs`, `shared/DTOs/Product*`, `shared/Requests/*Product*` show no diff against `docs/repository-unit-of-work`

## 5. Unit tests with fakes (`tests/Backend.UnitTests/`, flat, namespace `TechStore.UnitTests`)

- [x] 5.1 Add `FakeUnitOfWork.cs` (call counters for save/begin/commit/rollback, optional exception thrown from `SaveChangesAsync`), `FakeProductRepository.cs` (in-memory products, `LastCriteria`, `SearchCallCount`, added products, configurable existing SKUs and search result), `FakeCategoryRepository.cs`; verify the test project builds and no mocking package was added to `TechStore.UnitTests.csproj`
- [x] 5.2 Add `GetByIdAsync` tests: unknown id → not found; existing product → variants ordered by price then SKU and `effectiveSpecs` merged (variant overrides product); verify with `dotnet test --filter "FullyQualifiedName~ProductServiceTests"`
- [x] 5.3 Add `CreateAsync` tests: valid → success, exactly one save, product and variants added with `IsActive = true`; unknown category → validation error on `categoryId`, zero saves; existing SKU → conflict, zero saves; `SaveChangesAsync` throws `DuplicateKeyException` → conflict; invalid request → validation errors and no repository calls; verify the filtered test run passes
- [x] 5.4 Add `SearchAsync` tests: malformed spec / `minPrice > maxPrice` / unknown sort → error on `spec` / `minPrice` / `sort` and `SearchCallCount == 0`; valid request → `LastCriteria` has expected `SortField`, `Descending`, normalized page/pageSize, `SpecFilterJson`, trimmed keyword/brand; summary `priceFrom`/`priceTo` from variants and from base price when no variants; verify the filtered test run passes
- [x] 5.5 Run the full suite `dotnet test tests/Backend.UnitTests/TechStore.UnitTests.csproj`; verify every pre-existing test still passes and the total is the 1.4 baseline plus the new tests

## 6. Docs, behavior check and commits

- [x] 6.1 Update `backend/Catalog/README.md` section 3: remove the note that `ProductService` still queries `AppDbContext`, state that `IProductRepository`, the minimal `ICategoryRepository` (Dev 3 extends it) and `IUnitOfWork` now exist; verify `grep -n "vẫn truy vấn trực tiếp" backend/Catalog/README.md` returns nothing
- [x] 6.2 Restart the API on the refactored code, rerun the same GET requests as 1.3 and compare with the baseline file; verify every status code, `totalCount` and SKU list is identical (including `brand=%` and `brand=a_ple` → 0 items)
- [x] 6.3 Rerun the POST requests of `backend/TechStore.Api.http`; verify 201 + `Location` for the valid create, 400 cases with the same error keys, 409 for `ip15pm-256-nt`, and two parallel identical creates with a new SKU yield exactly one 201 and one 409
- [x] 6.4 Run `dotnet build TechStore.sln`; verify zero errors
- [x] 6.5 Commit the Catalog repositories, `ProductService`, the remaining `Program.cs` lines, test files and `backend/Catalog/README.md` as `refactor(catalog): move product data access to repositories and unit of work` (no `openspec/`, no `.gitignore`); verify with `git show --stat HEAD`
- [x] 6.6 Commit the updated `tasks.md` as `docs(openspec): update catalog-product-repository task progress`; verify `git status --short` shows only the pre-existing `.gitignore` edit and `git log --oneline docs/repository-unit-of-work..HEAD` shows exactly four commits

## Workflow follow-up

- Tell Dev 3 (Bình) that `backend/Catalog/Repositories/ICategoryRepository.cs` / `CategoryRepository.cs` exist with `GetByIdAsync` and must be extended, not recreated.
- Ask Dev 1 (An) to review the `feat(common)` Unit of Work commit.
- Merge order: `feat/catalog-product-api` → `docs/repository-unit-of-work` → `refactor/catalog-product-repository`; rebase this branch if earlier PRs change.
- Archive this change after merge.
