# Design

## Context

See `proposal.md` for motivation. This change only edits documentation; the decisions below are the convention the documents must describe, so later code changes can follow them without re-deciding.

Current state of the documents:

- `AGENTS.md` (English): Data Flow steps 5–6 say the service opens an EF Core transaction and commits through `AppDbContext`; section 4 shows `_dbContext.Database.BeginTransactionAsync(...)` inside a service; section 6 lists "unit-of-work services" under Scoped without defining them; the key-directories tree has no `Repositories/`.
- `docs/ARCHITECTURE.md` (Vietnamese): section 2.2 lists `Entities/`, `Configurations/`, `Services/` and controller files per module; `Common/Data/` lists `AppDbContext`, migrations, `DataSeeder`; section 3 concurrency notes describe locking without saying where it lives.
- `backend/*/README.md` (Vietnamese): each "Cấu trúc Thành phần Module" section lists `Services/` and a `Controllers/` folder (Catalog already corrected to module-root controllers by `catalog-product-api`).
- `catalog-product-api` (branch `feat/catalog-product-api`, not merged yet) already edited `AGENTS.md`, `docs/ARCHITECTURE.md` and `backend/Catalog/README.md`.

## Goals / Non-Goals

**Goals:**
- One unambiguous layering rule every module follows, written where developers and agents look first (`AGENTS.md`), with the folder view in `docs/ARCHITECTURE.md` and per-module detail in each README.
- The concurrency example compiles conceptually against the new convention (no `_dbContext` in services).

**Non-Goals:**
- Writing `IUnitOfWork`, `DuplicateKeyException` or any repository code.
- Refactoring `ProductService` (follow-up change).
- Choosing exact method signatures for other developers' repositories beyond illustrative names.

## Decisions

### D1. Layer rule
`Controller → Service → Repository → AppDbContext`. Controllers call services only. Services contain business rules, validation, orchestration and entity→DTO mapping, and depend only on repository interfaces and `IUnitOfWork`. Repositories are the only classes that touch `AppDbContext` (LINQ, `FromSql`, `EF.Functions`, `Include`, `AsNoTracking`).
- *Alternative*: keep `AppDbContext` in services (current docs) — rejected by the team to make services unit-testable and keep SQL in one place.

### D2. Repository shape
- One repository per aggregate root: `I<Aggregate>Repository` / `<Aggregate>Repository` in `backend/<Module>/Repositories/` (e.g. `IProductRepository` covers `Product` and its `ProductVariant`s; `ICategoryRepository`; `IInventoryStockRepository`; `ISerialImeiRepository`; `IOrderRepository`).
- Methods express intent (`SearchAsync(criteria)`, `GetWithVariantsAsync(id)`, `FindExistingSkusAsync(skus)`, `Add(product)`, `GetForUpdateAsync(productId, variantId)`), not generic CRUD.
- Return entities, entity lists, or `(IReadOnlyList<TEntity> Items, int TotalCount)` for paged queries — never DTOs, never `IQueryable`. Read-only methods use `AsNoTracking`; methods whose results the service will modify return tracked entities.
- Query inputs are module-internal criteria records with already-parsed values (e.g. `ProductSearchCriteria`), not `shared/Requests` HTTP contracts, so parsing stays in the service.
- No generic `IRepository<T>`: `DbSet<T>` already is one, and a generic `Query()` would leak EF into services.

### D3. Unit of Work
**Decided (team, after comparing with "repositories save themselves" and `TransactionScope`): keep `IUnitOfWork`.** EF Core already provides the transactions (implicit per `SaveChanges`, explicit via `Database.BeginTransactionAsync`); `IUnitOfWork` is only the thin door that lets services control them without holding `AppDbContext`.

`IUnitOfWork` / `UnitOfWork` in `backend/Common/Data/`, Scoped, wrapping the request-scoped `AppDbContext`, with exactly these members:
- `Task<int> SaveChangesAsync(CancellationToken)` — delegates to `AppDbContext.SaveChangesAsync`; translates `23505` (see D4).
- `Task BeginTransactionAsync(CancellationToken)` — opens an explicit transaction via `AppDbContext.Database.BeginTransactionAsync`; throws `InvalidOperationException` if one is already open (no nesting).
- `Task CommitAsync(CancellationToken)` — commits and disposes the open transaction; throws if none is open.
- `Task RollbackAsync(CancellationToken)` — rolls back and disposes the open transaction if any (no-op otherwise) and clears the `ChangeTracker`.

Usage rules:
- The **service owns the transaction boundary**; repositories only run inside it. Repositories stage changes (`Add`, `Remove`, edits on tracked entities) and never call `SaveChanges`, `BeginTransaction`, `Commit` or `Rollback`.
- A use case that writes once (e.g. create product) only calls `SaveChangesAsync` — the implicit EF transaction makes it atomic.
- A use case that locks rows (`FOR UPDATE`) or saves more than once calls `BeginTransactionAsync` → repository calls → `SaveChangesAsync` → `CommitAsync`, and calls `RollbackAsync` on any business failure or exception (`try`/`catch`).
- Keep explicit transactions short; never call external services (Gemini, VietQR) while a transaction is open.
- *Alternatives*: each repository saves (breaks multi-repository atomicity because all repositories share one scoped `AppDbContext`); services inject `AppDbContext` for transactions (breaks D1). Both rejected.

### D4. Duplicate-key translation
`UnitOfWork.SaveChangesAsync` catches `DbUpdateException` whose inner `PostgresException.SqlState == "23505"` and throws `DuplicateKeyException` (in `backend/Common/Data/`) carrying `ConstraintName`. Services catch it and map to their conflict result (e.g. 409). Services never reference Npgsql types.

### D5. Row locks
Pessimistic locks are repository methods (e.g. `IInventoryStockRepository.GetForUpdateAsync`) issuing `SELECT ... FOR UPDATE`. They are only valid inside a transaction opened by the calling service through `IUnitOfWork`; the documentation states this precondition.

The same precondition applies to repository methods that use EF Core bulk operations (`ExecuteUpdateAsync`, `ExecuteDeleteAsync`) or raw write SQL: these run SQL immediately, bypassing the `ChangeTracker` and `SaveChanges`, so they must only be called inside a transaction opened by the service, and their name or XML comment must say they write directly (e.g. `MarkSoldAsync` — "executes immediately; call inside an IUnitOfWork transaction"). The rewritten `AGENTS.md` section 4 example:
```csharp
await _unitOfWork.BeginTransactionAsync(cancellationToken);
var stock = await _stockRepository.GetForUpdateAsync(productId, variantId, cancellationToken);
// business rules: check availability, change quantities, stage movement via repository
await _unitOfWork.SaveChangesAsync(cancellationToken);
await _unitOfWork.CommitAsync(cancellationToken);
```

### D6. Cross-module data access
A service may call another module's repository interface for **reads** (e.g. Catalog checking a category exists through `ICategoryRepository`). Writes to another module's tables go through that module's service, never its repository. The tables each module owns are the existing TableGroups in `docs/database/schema.dbml`.

### D7. Testing
Services are unit-tested with hand-written fake repositories and a fake `IUnitOfWork` in `tests/Backend.UnitTests/` (no mocking library added). Repository behavior that depends on PostgreSQL (jsonb, `FOR UPDATE`, constraints) is verified manually or by future integration tests.

### D8. Registration and naming
Repositories and `IUnitOfWork` are registered Scoped next to their services in `backend/Program.cs`. Naming rows added to `AGENTS.md`: interfaces `I<Aggregate>Repository`, classes `<Aggregate>Repository`, fields `_productRepository`, `_unitOfWork`.

### D9. Writing the documents
Each file keeps its language (English in `AGENTS.md`, Vietnamese in `docs/ARCHITECTURE.md` and module READMEs) and its existing formatting (box-drawing trees, tables). Module README entries list planned repository names marked as planned; owners may rename them in their own changes.

## Risks / Trade-offs

- [Docs describe types that do not exist yet (`IUnitOfWork`, repositories)] → Mark them as the required convention; Dev 1's follow-up change implements `IUnitOfWork` first, before other modules need transactions.
- [`ProductService` already merged-to-branch uses `AppDbContext` directly and violates the new rule] → Recorded as known debt; the follow-up `catalog-product-repository` change refactors it without changing API behavior.
- [Edits overlap with the unmerged `catalog-product-api` docs commit] → Branch from `feat/catalog-product-api` (or from `dev` after it merges) to avoid conflicts in `AGENTS.md`, `docs/ARCHITECTURE.md`, `backend/Catalog/README.md`.
- [Convention decided by Dev 2 for the whole team] → The team agreed on it; the PR is reviewed by Dev 1, Dev 3 and Dev 4 before merge.

## Migration Plan

- Branch `docs/repository-unit-of-work` from `feat/catalog-product-api` (or from `dev` once that PR is merged).
- Commits: (1) planning artifacts of this change, (2) documentation update, (3) task progress.
- Rollback: revert the documentation commit.
