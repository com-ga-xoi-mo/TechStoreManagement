# Proposal

## Why

The team convention recorded by `repository-unit-of-work-convention` (`AGENTS.md` section 9, `docs/ARCHITECTURE.md` 2.2.1) says services never touch `AppDbContext`: data access lives in per-aggregate repositories and saves/transactions go through `IUnitOfWork`. `backend/Catalog/Services/ProductService.cs` predates the convention and still injects `AppDbContext`, builds EF/SQL queries (`FromSqlInterpolated`, `EF.Functions.ILike`, correlated price subqueries), calls `SaveChangesAsync`, and catches `PostgresException` `23505`. As a result `ProductService` cannot be unit-tested without PostgreSQL (the existing tests only cover helpers) and it is the one place in the codebase that contradicts the documented rule. `IUnitOfWork` does not exist yet either, and Dev 2 has agreed to build it so Catalog is not blocked.

## What Changes

Internal refactor only — the three `product-catalog` endpoints keep the same requests, responses, error keys/messages and HTTP status codes.

- **Unit of Work (new, `backend/Common/Data/`)**: `IUnitOfWork`, `UnitOfWork` and `DuplicateKeyException` exactly as specified in `repository-unit-of-work-convention` design D3/D4; registered Scoped. Committed separately for review by Dev 1 (owner of `Common`).
- **Catalog repositories (new, `backend/Catalog/Repositories/`)**:
  - `IProductRepository` / `ProductRepository`: `SearchAsync(ProductSearchCriteria)`, `GetWithVariantsAsync(id)`, `FindExistingSkusAsync(skusLower)`, `Add(product)`; returns entities, never DTOs or `IQueryable`, never saves. All SQL currently in `ProductService.SearchAsync` moves here unchanged in behavior.
  - `ProductSearchCriteria` record and `ProductSortField` enum carrying already-validated search values.
  - `ICategoryRepository` / `CategoryRepository` **minimal**: only `GetByIdAsync`, created by Dev 2 so Catalog can check categories through a repository; Dev 3 owns and extends it.
- **`ProductService`**: depends on `IProductRepository`, `ICategoryRepository`, `IUnitOfWork` only; keeps validation, spec/sort/price parsing, paging normalization and entity→DTO mapping; maps `DuplicateKeyException` to the existing conflict result. `IProductService`, result records, `ProductsController`, shared DTOs/requests are untouched.
- **Tests**: hand-written fakes (`FakeUnitOfWork`, `FakeProductRepository`, `FakeCategoryRepository`) and new `ProductService` unit tests for detail, create and search paths; existing tests unchanged.
- **Docs**: `backend/Catalog/README.md` drops the "ProductService still uses AppDbContext" note and states the repositories and `IUnitOfWork` now exist.
- **DI**: `Program.cs` registers `IUnitOfWork`, `IProductRepository`, `ICategoryRepository` (Scoped).

Out of scope: any API behavior or contract change; additional `ICategoryRepository` methods; `CategoryService`/`CategoriesController`; other modules; migrations; shared error middleware; GIN-index optimization of spec filtering.

## Capabilities

### New Capabilities
*(None.)*

### Modified Capabilities
*(None. Externally observable behavior of `product-catalog` is unchanged, so the change sets `skip_specs: true`.)*

## Impact

- **Code**: new `backend/Common/Data/IUnitOfWork.cs`, `UnitOfWork.cs`, `DuplicateKeyException.cs`; new `backend/Catalog/Repositories/*`; rewritten `backend/Catalog/Services/ProductService.cs`; `backend/Program.cs` (3 registrations).
- **Tests**: new fake classes and additional cases in `tests/Backend.UnitTests/ProductServiceTests.cs`.
- **Docs**: `backend/Catalog/README.md`.
- **API / database**: none.
- **Team**: Dev 1 reviews the `Common` commit; Dev 3 must extend the new `CategoryRepository` instead of creating another one.
- **Branching**: builds on the unmerged `feat/catalog-product-api` and `docs/repository-unit-of-work` branches; merge order is catalog API → docs → this refactor.
