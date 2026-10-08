# Proposal

## Why

The team has agreed that every backend module separates data access from business logic: services must not query `AppDbContext` directly, all EF Core / SQL work lives in per-aggregate repositories that return entities, and transactions plus `SaveChanges` go through a shared `IUnitOfWork`. The documentation still describes the old pattern (services opening transactions on `_dbContext`, no `Repositories/` folder), so Dev 3 (Inventory) and Dev 4 (Orders) would start Sprint 1 code against a convention the team has already replaced. The docs must be updated before more module code is written.

## What Changes

Documentation only — no code is changed in this change.

- **Layer convention** (`AGENTS.md`, `docs/ARCHITECTURE.md`): Controller → Service → Repository → `AppDbContext`.
  - Controllers call services only.
  - Services hold business rules, validation and DTO mapping; they depend on repository interfaces and `IUnitOfWork`, never on `AppDbContext`.
  - Repositories are the only classes that use `AppDbContext`; one repository per aggregate (e.g. `IProductRepository` covers `Product` + `ProductVariant`); methods express intent; they return entities (or entity lists plus counts), never DTOs and never `IQueryable`; no generic `IRepository<T>`.
- **Unit of Work** (`AGENTS.md`, `docs/ARCHITECTURE.md`): `IUnitOfWork` in `backend/Common/Data/` wraps `AppDbContext` with `SaveChangesAsync`, `BeginTransactionAsync`, commit/rollback; repositories stage changes and never call `SaveChanges`; `IUnitOfWork` translates PostgreSQL unique violations (`23505`) into a shared `DuplicateKeyException` carrying the constraint name.
- **Concurrency pattern** (`AGENTS.md` section 4, `docs/ARCHITECTURE.md` concurrency notes): the transaction example is rewritten so the service opens the transaction through `IUnitOfWork` and row locks (`SELECT ... FOR UPDATE`) are repository methods such as `GetForUpdateAsync`, valid only inside that transaction.
- **Folder structure**: `Repositories/` added to every module in `AGENTS.md` key directories and `docs/ARCHITECTURE.md` section 2.2; `Common/Data/` lists `IUnitOfWork` / `UnitOfWork` / `DuplicateKeyException`.
- **Dependency injection and naming** (`AGENTS.md`): repositories and `IUnitOfWork` registered as Scoped; naming rows for `I<Aggregate>Repository` / `<Aggregate>Repository`; example field `_productRepository`.
- **Testing guidance** (`AGENTS.md`): services are unit-tested with hand-written fake repositories and a fake `IUnitOfWork` (no mocking library is added).
- **Module READMEs** (`backend/Catalog|Inventory|Orders|Customers|Sales|Identity/README.md`): each "module structure" section gains a `Repositories/` entry listing its planned repositories; the stale `Controllers/` folder entries are corrected to "controller files at the module root" to match `docs/ARCHITECTURE.md`.

Out of scope: implementing `IUnitOfWork`, any repository, or refactoring `ProductService` (a follow-up code change); changing API behavior; `docs/MILESTONE_2_PLAN.md` task wording.

## Capabilities

### New Capabilities
*(None.)*

### Modified Capabilities
*(None. This change only updates developer documentation; no system behavior changes, so the change sets `skip_specs: true`.)*

## Impact

- **Documentation**: `AGENTS.md`, `docs/ARCHITECTURE.md`, and the six module READMEs under `backend/*/README.md`.
- **Team**: Dev 1 (Common owner) implements `IUnitOfWork` / `DuplicateKeyException` in a follow-up change; Dev 2–6 write new data access as repositories. Existing `ProductService` stays as-is until the follow-up refactor change.
- **Code, database, API**: none.
